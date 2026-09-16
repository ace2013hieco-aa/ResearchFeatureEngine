using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Server;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.McpServer;

[McpServerToolType]
public static class EngineTools
{
    private const int MaxRows = 20_000;
    private const int MaxReturnBars = 500;

    [McpServerTool, Description("Describe the ResearchFeatureEngine pipeline, reference models, and ratified defaults.")]
    public static string DescribeEngine()
    {
        var doc = new
        {
            engine = "ResearchFeatureEngine (MIT, net6.0 core, platform-independent)",
            pipeline = new[]
            {
                "Reference (exactly one of ATRSmooth2|DarvasBox|Hma|HmaAtrSmooth)",
                "Distance (close vs reference: directional + absolute)",
                "Darvas composites (DarvasBox mode only) / HMA-ATRSmooth composites (HmaAtrSmooth only)",
                "RegimeSegment M9 (ATRSmooth modes only)",
                "Reversal (strict regime-transition state machine; 0->+-1 establishment IS a reversal)",
                "Scale (ATR 14)",
                "Normalization (distance / scale)",
                "Statistics (window 252 over Close: mean/std/min/max/median/variance/MAD/range/skew/kurtosis)",
            },
            references = new
            {
                atrsmooth2 = "(VWMA(close,100)+ATRTrailingStop(16,5.1))/2; regime = trailing-stop bias",
                darvasbox = "(Upper+Lower)/2, length 5; regime = +1 above / 0 inside / -1 below",
                hma = "canonical HMA(16) of close; regime 0",
                hmaatrsmooth = "ATRSmooth2 equilibrium + additive HMA; mean-distance window 20 + alignment + M11.1 separation",
            },
            csv = "fixture_v1 header: DateTime,Open,High,Low,Close,Volume (also accepts recorder_v1_1 OpenTimeUtc,...,Spread by ignoring Spread)",
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("List pipeline stages registered for a reference mode (atrsmooth2|darvasbox|hma|hmaatrsmooth).")]
    public static string ListStages(
        [Description("Reference mode")] string mode = "atrsmooth2")
    {
        var m = NormalizeMode(mode);
        var stages = new List<string> { "Reference", "Distance" };
        if (m == "darvasbox") stages.AddRange(new[] { "DarvasBoxDistance", "MeanDarvasClosingDistance" });
        if (m == "hmaatrsmooth") stages.AddRange(new[] { "MeanHmaAtrSmoothDistance", "HmaPriceAtrSmoothAlignment" });
        if (m is "atrsmooth2" or "hmaatrsmooth") stages.Add("AtrSmoothRegimeSegment(M9)");
        stages.AddRange(new[] { "Reversal", "Scale", "Normalization", "Statistics" });
        if (m == "hmaatrsmooth") stages.AddRange(new[] { "HmaAtrSmoothSeparation(M11.1)", "HmaAtrSmoothRelativeClosePosition(M11.1)" });
        return JsonSerializer.Serialize(new { mode = m, stages }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Validate an OHLCV CSV (fail-closed): header, finite values, monotonic time. Returns JSON report.")]
    public static string ValidateData(
        [Description("Absolute path to CSV file")] string csvPath)
    {
        try
        {
            var bars = LoadBars(csvPath, MaxRows);
            var issues = new List<string>();
            for (int i = 1; i < bars.Count; i++)
            {
                if (bars[i].Time <= bars[i - 1].Time)
                {
                    issues.Add($"timestamp regression at row {i}");
                    break;
                }
            }
            return JsonSerializer.Serialize(new
            {
                ok = issues.Count == 0,
                rows = bars.Count,
                first = bars[0].Time.ToString("o"),
                last = bars[^1].Time.ToString("o"),
                issues,
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Run the full engine pipeline on an OHLCV CSV and return the last N bars of features as JSON.")]
    public static string ComputeFeatures(
        [Description("Absolute path to CSV file")] string csvPath,
        [Description("Reference mode: atrsmooth2|darvasbox|hma|hmaatrsmooth")] string mode = "atrsmooth2",
        [Description("How many trailing bars to return (1-500)")] int maxBars = 50)
    {
        try
        {
            var m = NormalizeMode(mode);
            maxBars = Math.Clamp(maxBars, 1, MaxReturnBars);
            var bars = LoadBars(csvPath, MaxRows);
            if (bars.Count < 5)
                return JsonSerializer.Serialize(new { ok = false, error = "need >= 5 rows" });

            var market = new CsvMarketData(
                bars.Select(b => b.Open).ToArray(),
                bars.Select(b => b.High).ToArray(),
                bars.Select(b => b.Low).ToArray(),
                bars.Select(b => b.Close).ToArray(),
                bars.Select(b => b.Volume).ToArray(),
                bars.Select(b => b.Time).ToArray());

            var config = CreateConfig(m, market);
            var engine = new ResearchFeatureEngineBuilder(config).Build();

            var rows = new List<object>(bars.Count);
            for (int i = 0; i < bars.Count; i++)
            {
                engine.Update();
                var v = engine.Values;
                rows.Add(new
                {
                    bar = i,
                    time = bars[i].Time.ToString("o"),
                    close = bars[i].Close,
                    reference = v.Reference.Price,
                    regime = v.Reference.Regime,
                    distance = v.Distance.DirectionalExtension,
                    absDistance = v.Distance.AbsoluteExtension,
                    scale = v.Scale.Scale,
                    normalized = v.Normalization.NormalizedMeasurement,
                    reversal = v.Reversal.IsReversalBar,
                    barsSinceReversal = v.Reversal.BarsSinceReversal,
                    mean = v.Statistics.Location.Mean,
                    std = v.Statistics.Dispersion.StandardDeviation,
                });
            }

            var tail = rows.Skip(Math.Max(0, rows.Count - maxBars)).ToArray();
            return JsonSerializer.Serialize(new
            {
                ok = true,
                mode = m,
                totalBars = rows.Count,
                returnedBars = tail.Length,
                rows = tail,
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    // ---- helpers ----

    private static string NormalizeMode(string mode)
    {
        var m = (mode ?? "atrsmooth2").Trim().ToLowerInvariant();
        return m switch
        {
            "atrsmooth2" or "atr" => "atrsmooth2",
            "darvasbox" or "darvas" => "darvasbox",
            "hma" => "hma",
            "hmaatrsmooth" or "hma_atrsmooth" or "composite" => "hmaatrsmooth",
            _ => throw new ArgumentException($"Unknown mode '{mode}'. Valid: atrsmooth2|darvasbox|hma|hmaatrsmooth."),
        };
    }

    private sealed record Bar(DateTime Time, double Open, double High, double Low, double Close, double Volume);

    private static List<Bar> LoadBars(string path, int cap)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ArgumentException($"CSV not found: {path}");
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) throw new ArgumentException("CSV is empty (need header + rows).");
        var header = lines[0].Split(',');
        bool fixture = header.Length == 6 && header[0].Trim() == "DateTime";
        bool recorder11 = header.Length == 7 && header[0].Trim() == "OpenTimeUtc";
        bool recorder1 = header.Length == 6 && header[0].Trim() == "OpenTimeUtc";
        if (!fixture && !recorder11 && !recorder1)
            throw new ArgumentException($"Unknown header '{lines[0]}'. Expect fixture_v1 (DateTime,Open,High,Low,Close,Volume).");

        var bars = new List<Bar>(Math.Min(lines.Length - 1, cap));
        for (int i = 1; i < lines.Length && bars.Count < cap; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length != header.Length)
                throw new ArgumentException($"Row {i}: expected {header.Length} fields, got {f.Length}.");
            if (!DateTime.TryParse(f[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t))
                throw new ArgumentException($"Row {i}: bad timestamp '{f[0]}'.");
            double o = ParseD(f[1], i, "Open"), h = ParseD(f[2], i, "High"),
                   l = ParseD(f[3], i, "Low"), c = ParseD(f[4], i, "Close");
            double v = fixture || recorder1 ? ParseD(f[5], i, "Volume") : ParseD(f[5], i, "TickVolume");
            if (!new[] { o, h, l, c }.All(double.IsFinite) || !(h >= Math.Max(o, Math.Max(l, c)) && l <= Math.Min(o, Math.Min(h, c))))
                throw new ArgumentException($"Row {i}: non-finite or inconsistent OHLC.");
            bars.Add(new Bar(t, o, h, l, c, v));
        }
        if (bars.Count == 0) throw new ArgumentException("No data rows.");
        return bars;
    }

    private static double ParseD(string s, int row, string col)
    {
        if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || !double.IsFinite(d))
            throw new ArgumentException($"Row {row}: bad {col} '{s}'.");
        return d;
    }

    private static EngineConfiguration CreateConfig(string mode, CsvMarketData market)
    {
        IReferenceSource source = mode switch
        {
            "darvasbox" => new DarvasBoxReferenceSource(new DarvasBoxConfiguration(DarvasBoxConfiguration.DefaultLength)),
            "hma" => new HmaReferenceSource(new HmaConfiguration(HmaConfiguration.DefaultPeriod)),
            "hmaatrsmooth" => new HmaAtrSmoothCompositeSource(new HmaAtrSmoothConfiguration(
                new ATRSmoothConfiguration(ATRSmoothConfiguration.DefaultAtrPeriod, ATRSmoothConfiguration.DefaultAtrMultiplier, ATRSmoothConfiguration.DefaultSmoothLength),
                new HmaConfiguration(HmaConfiguration.DefaultPeriod))),
            _ => new ATRSmoothReferenceSource(new ATRSmoothConfiguration(
                ATRSmoothConfiguration.DefaultAtrPeriod, ATRSmoothConfiguration.DefaultAtrMultiplier, ATRSmoothConfiguration.DefaultSmoothLength)),
        };

        return new EngineConfiguration(
            market,
            new EngineValues(),
            source,
            new ATRScaleModel(14),
            new ScaleNormalizationModel(),
            new List<IStatisticModel>
            {
                new MeanModel(), new StandardDeviationModel(), new MinimumModel(),
                new MaximumModel(), new MedianModel(), new VarianceModel(),
                new MedianAbsoluteDeviationModel(), new RangeModel(),
                new SkewnessModel(), new KurtosisModel(),
            },
            new EngineOptions
            {
                StatisticsWindowSize = 252,
                MeanDarvasWindowSize = 20,
                MeanHmaAtrSmoothWindowSize = 20,
                StatisticsSource = StatisticsSource.Close,
                ReversalMode = ReversalMode.TrailingStopPosition,
            });
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using ResearchFeatureEngine.BulkExport;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Statistics.Models;

using Xunit;

namespace ResearchFeatureEngine.Tests.BulkExport
{
    /// <summary>
    /// M10.1 golden test suite (G1–G17). The ONLY oracle is the
    /// canonical engine runtime: tests build the identical engine
    /// composition on the identical fixture rows, then assert that
    /// every exported measurement token parses back to the exact
    /// runtime double/int/enum value.
    /// </summary>
    public sealed class BulkExportGoldenTests
    {
        // -------------------------------------------------------------
        // Fixture helpers
        // -------------------------------------------------------------

        private static string FixtureDir()
        {
            string cwd = Directory.GetCurrentDirectory();
            // Test assembly runs from Tests/bin/Debug/net10.0
            string root = cwd;
            for (int i = 0; i < 6 && root != null; i++)
            {
                if (File.Exists(Path.Combine(root, "ResearchFeatureEngine.csproj")))
                {
                    return Path.Combine(root, "Tests", "TestData");
                }

                root = Path.GetDirectoryName(root);
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + cwd);
        }

        private static string TempDir()
        {
            string dir = Path.Combine(
                Path.GetTempPath(),
                "BulkExportTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Build a synthetic recorder_v1 capture with a deterministic
        /// pseudo-random walk. All bars hourly (gap-free grid), so
        /// first→boundary counting is exact. startUtc controls the
        /// calendar boundary placement.
        /// </summary>
        private static string WriteSyntheticCapture(
            string dir,
            string name,
            int bars,
            DateTime startUtc,
            int stepMinutes,
            int seed)
        {
            string path = Path.Combine(dir, name);
            var sb = new StringBuilder(bars * 64);
            sb.Append("OpenTimeUtc,Open,High,Low,Close,TickVolume\n");

            var rng = new Random(seed);
            double price = 1.1000;
            var t = startUtc;

            for (int i = 0; i < bars; i++)
            {
                double move = (rng.NextDouble() - 0.5) * 0.004;
                double open = price;
                double close = price + move;
                double high = Math.Max(open, close) + rng.NextDouble() * 0.001;
                double low = Math.Min(open, close) - rng.NextDouble() * 0.001;
                long volume = rng.Next(10, 500);

                sb.Append(t.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"))
                  .Append(',')
                  .Append(open.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(high.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(low.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(close.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(volume.ToString(CultureInfo.InvariantCulture))
                  .Append('\n');

                price = close;
                t = t.AddMinutes(stepMinutes);
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static string Sha256File(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using FileStream fs = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(64);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// recorder_v1_1 synthetic capture (M10.1.x): the exact same
        /// deterministic walk as WriteSyntheticCapture — identical
        /// Random(seed) consumption and identical OHLCV formatting —
        /// plus a trailing Spread column. spreadOf must NOT consume
        /// randomness so the OHLCV stream stays identical to the
        /// 6-column writer at the same seed.
        /// </summary>
        private static string WriteSyntheticCaptureV11(
            string dir,
            string name,
            int bars,
            DateTime startUtc,
            int stepMinutes,
            int seed,
            Func<int, string> spreadOf)
        {
            string path = Path.Combine(dir, name);
            var sb = new StringBuilder(bars * 72);
            sb.Append("OpenTimeUtc,Open,High,Low,Close,TickVolume,Spread\n");

            var rng = new Random(seed);
            double price = 1.1000;
            var t = startUtc;

            for (int i = 0; i < bars; i++)
            {
                double move = (rng.NextDouble() - 0.5) * 0.004;
                double open = price;
                double close = price + move;
                double high = Math.Max(open, close) + rng.NextDouble() * 0.001;
                double low = Math.Min(open, close) - rng.NextDouble() * 0.001;
                long volume = rng.Next(10, 500);

                sb.Append(t.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"))
                  .Append(',')
                  .Append(open.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(high.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(low.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(close.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(volume.ToString(CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(spreadOf(i))
                  .Append('\n');

                price = close;
                t = t.AddMinutes(stepMinutes);
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static DatasetRegistryEntry EntryFor(
            string capturePath,
            string datasetId,
            string firstToken,
            string lastToken)
        {
            string first = File.ReadLines(capturePath).Skip(1).First().Split(',')[0];
            string last = File.ReadLines(capturePath).Last().Split(',')[0];

            return new DatasetRegistryEntry
            {
                DatasetId = datasetId,
                DatasetVersion = "v1",
                Filename = Path.GetFileName(capturePath),
                SourceSha256 = Sha256File(capturePath),
                FirstTimestamp = first,
                LastTimestamp = last,
                PartitionPolicy = "first_calendar_year_only"
            };
        }

        private static List<DatasetRegistryEntry> Registry(params DatasetRegistryEntry[] entries)
        {
            return entries.ToList();
        }

        // The engine-oracle: same composition the exporter uses, run
        // in-process on the same rows. Returns per-bar runtime values.
        private sealed class OracleEngine
        {
            public List<EngineValues> Values { get; } = new List<EngineValues>();
        }

        private static List<EngineValues> RunOracle(
            string capturePath,
            int rowCount,
            ExportMode mode)
        {
            var source = new CsvBarSource(capturePath);
            ArraysMarketData window = source.Materialize(rowCount);
            EngineConfiguration configuration = PresetFactory.Create(mode, window);
            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            var frames = new List<EngineValues>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                engine.Update();
                frames.Add(CopyFrame(engine.Values));
            }

            return frames;
        }

        /// <summary>
        /// Deep copy of the runtime values at this bar (the runtime
        /// object is mutated in place every Update, so the oracle must
        /// snapshot each bar's frame).
        /// </summary>
        private static EngineValues CopyFrame(EngineValues v)
        {
            var copy = new EngineValues();
            copy.Reference.Price = v.Reference.Price;
            copy.Reference.Regime = v.Reference.Regime;
            copy.Distance.DirectionalExtension = v.Distance.DirectionalExtension;
            copy.Distance.AbsoluteExtension = v.Distance.AbsoluteExtension;
            copy.Scale.Scale = v.Scale.Scale;
            copy.Normalization.NormalizedMeasurement = v.Normalization.NormalizedMeasurement;
            copy.Statistics.ObservationCount = v.Statistics.ObservationCount;
            copy.Statistics.Location.Mean = v.Statistics.Location.Mean;
            copy.Statistics.Location.Median = v.Statistics.Location.Median;
            copy.Statistics.Range.Minimum = v.Statistics.Range.Minimum;
            copy.Statistics.Range.Maximum = v.Statistics.Range.Maximum;
            copy.Statistics.Range.Range = v.Statistics.Range.Range;
            copy.Statistics.Dispersion.MedianAbsoluteDeviation = v.Statistics.Dispersion.MedianAbsoluteDeviation;
            copy.Statistics.Dispersion.Variance = v.Statistics.Dispersion.Variance;
            copy.Statistics.Dispersion.StandardDeviation = v.Statistics.Dispersion.StandardDeviation;
            copy.Statistics.Shape.Skewness = v.Statistics.Shape.Skewness;
            copy.Statistics.Shape.Kurtosis = v.Statistics.Shape.Kurtosis;
            copy.Reversal.BarsSinceReversal = v.Reversal.BarsSinceReversal;
            copy.Reversal.Direction = v.Reversal.Direction;
            copy.Reversal.IsReversalBar = v.Reversal.IsReversalBar;
            copy.AtrSmoothRegimeSegment.Regime = v.AtrSmoothRegimeSegment.Regime;
            copy.AtrSmoothRegimeSegment.RegimeId = v.AtrSmoothRegimeSegment.RegimeId;
            copy.AtrSmoothRegimeSegment.RegimeStartIndex = v.AtrSmoothRegimeSegment.RegimeStartIndex;
            copy.AtrSmoothRegimeSegment.RegimeAge = v.AtrSmoothRegimeSegment.RegimeAge;
            copy.AtrSmoothRegimeSegment.RegimeTransition = v.AtrSmoothRegimeSegment.RegimeTransition;
            copy.DarvasBoxDistance.HasBox = v.DarvasBoxDistance.HasBox;
            copy.DarvasBoxDistance.SignedClosingDistance = v.DarvasBoxDistance.SignedClosingDistance;
            copy.DarvasBoxDistance.AbsoluteClosingDistance = v.DarvasBoxDistance.AbsoluteClosingDistance;
            copy.MeanDarvasClosingDistance.MeanSignedDistance = v.MeanDarvasClosingDistance.MeanSignedDistance;
            copy.MeanHmaAtrSmoothDistance.MeanSignedDistance = v.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            copy.HmaPriceAtrSmoothAlignment.Alignment = v.HmaPriceAtrSmoothAlignment.Alignment;
            copy.HmaAtrSmoothSeparation.Separation = v.HmaAtrSmoothSeparation.Separation;
            copy.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition = v.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;
            return copy;
        }

        private static (string artifact, string manifest) RunExport(
            string capturePath,
            string datasetId,
            ExportMode mode,
            string outDir)
        {
            // Artifacts go to a dedicated subdirectory: on Windows the
            // artifact name {datasetId}.csv can case-collide with the
            // capture name in the same directory, and the tool's
            // refuse-overwrite gate (correctly) fails the run.
            string artifactsDir = Path.Combine(outDir, "artifacts");
            Directory.CreateDirectory(artifactsDir);

            var entry = EntryFor(capturePath, datasetId, "", "");
            var runner = ExportRunner.Create(
                Registry(entry),
                "unused-registry-path",
                datasetId,
                ModeName(mode),
                Path.Combine(artifactsDir, datasetId + ".csv"),
                capturePath,
                "0123456789abcdef0123456789abcdef01234567");

            ExportResult result = runner.Run();
            return (result.ArtifactPath, result.ManifestPath);
        }

        private static string ModeName(ExportMode mode)
        {
            return mode switch
            {
                ExportMode.ATRSmooth2 => "atrsmooth2",
                ExportMode.DarvasBox => "darvasbox",
                ExportMode.Hma => "hma",
                ExportMode.HmaAtrSmooth => "hmaatrsmooth",
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }

        private static (string[] header, List<string[]> rows) ReadArtifact(string path)
        {
            string[] lines = File.ReadAllLines(path);
            string[] header = lines[0].Split(',');

            var rows = new List<string[]>(lines.Length - 1);
            for (int i = 1; i < lines.Length; i++)
            {
                rows.Add(lines[i].Split(','));
            }

            return (header, rows);
        }

        private static double ParseDoubleToken(string token)
        {
            return double.Parse(token, CultureInfo.InvariantCulture);
        }

        // -------------------------------------------------------------
        // G1 — all rows, all expected columns, engine equality
        // -------------------------------------------------------------

        [Fact]
        public void G1_AllRows_AllColumns_EngineEquality()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g1.csv", 600, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 7);

                // 600 hourly bars = 25 days → all rows inside year 1.
                (string artifact, _) = RunExport(capture, "G1", ExportMode.ATRSmooth2, dir);

                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                Assert.Equal(Schema.Columns(ExportMode.ATRSmooth2), header);
                Assert.Equal(600, rows.Count);

                List<EngineValues> oracle = RunOracle(capture, 600, ExportMode.ATRSmooth2);

                for (int i = 0; i < 600; i++)
                {
                    AssertBarEqualsEngine(rows[i], header, oracle[i], i);
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static void AssertBarEqualsEngine(
            string[] row,
            string[] header,
            EngineValues o,
            int barIndex)
        {
            int idx(string col) => Array.IndexOf(header, col);

            Assert.Equal(
                barIndex.ToString(CultureInfo.InvariantCulture),
                row[idx("bar_index")]);

            // Round-trip equality for every measurement double
            Assert.Equal(o.Reference.Price, ParseDoubleToken(row[idx("reference_price")]));
            Assert.Equal(o.Reference.Regime, ParseDoubleToken(row[idx("reference_regime")]));
            Assert.Equal(o.Distance.DirectionalExtension, ParseDoubleToken(row[idx("distance_signed")]));
            Assert.Equal(o.Distance.AbsoluteExtension, ParseDoubleToken(row[idx("distance_absolute")]));
            Assert.Equal(o.Scale.Scale, ParseDoubleToken(row[idx("scale")]));
            Assert.Equal(o.Normalization.NormalizedMeasurement, ParseDoubleToken(row[idx("normalized_measurement")]));
            Assert.Equal(o.Statistics.ObservationCount, int.Parse(row[idx("statistics_observation_count")], CultureInfo.InvariantCulture));
            Assert.Equal(o.Statistics.Location.Mean, ParseDoubleToken(row[idx("statistics_mean")]));
            Assert.Equal(o.Statistics.Location.Median, ParseDoubleToken(row[idx("statistics_median")]));
            Assert.Equal(o.Statistics.Range.Minimum, ParseDoubleToken(row[idx("statistics_minimum")]));
            Assert.Equal(o.Statistics.Range.Maximum, ParseDoubleToken(row[idx("statistics_maximum")]));
            Assert.Equal(o.Statistics.Range.Range, ParseDoubleToken(row[idx("statistics_range")]));
            Assert.Equal(o.Statistics.Dispersion.MedianAbsoluteDeviation, ParseDoubleToken(row[idx("statistics_mad")]));
            Assert.Equal(o.Statistics.Dispersion.Variance, ParseDoubleToken(row[idx("statistics_variance")]));
            Assert.Equal(o.Statistics.Dispersion.StandardDeviation, ParseDoubleToken(row[idx("statistics_standard_deviation")]));
            Assert.Equal(o.Statistics.Shape.Skewness, ParseDoubleToken(row[idx("statistics_skewness")]));
            Assert.Equal(o.Statistics.Shape.Kurtosis, ParseDoubleToken(row[idx("statistics_kurtosis")]));

            // Nullable ints: empty token = null
            if (o.Reversal.BarsSinceReversal.HasValue)
            {
                Assert.Equal(
                    o.Reversal.BarsSinceReversal.Value,
                    int.Parse(row[idx("reversal_bars_since")], CultureInfo.InvariantCulture));
            }
            else
            {
                Assert.Equal("", row[idx("reversal_bars_since")]);
            }

            Assert.Equal((int)o.Reversal.Direction, int.Parse(row[idx("reversal_direction")], CultureInfo.InvariantCulture));
            Assert.Equal(o.Reversal.IsReversalBar ? "1" : "0", row[idx("reversal_is_bar")]);

            // Segment family exists only in ATRSmooth-based modes
            int segCol = Array.IndexOf(header, "segment_regime");
            if (segCol >= 0)
            {
                Assert.Equal((int)o.AtrSmoothRegimeSegment.Regime, int.Parse(row[segCol], CultureInfo.InvariantCulture));
            }

            int segIdCol = Array.IndexOf(header, "segment_id");
            if (segIdCol >= 0)
            {
                if (o.AtrSmoothRegimeSegment.RegimeId.HasValue)
                {
                    Assert.Equal(
                        o.AtrSmoothRegimeSegment.RegimeId.Value,
                        int.Parse(row[segIdCol], CultureInfo.InvariantCulture));
                }
                else
                {
                    Assert.Equal("", row[segIdCol]);
                }
            }

            int segStartCol = Array.IndexOf(header, "segment_start_index");
            if (segStartCol >= 0)
            {
                if (o.AtrSmoothRegimeSegment.RegimeStartIndex.HasValue)
                {
                    Assert.Equal(
                        o.AtrSmoothRegimeSegment.RegimeStartIndex.Value,
                        int.Parse(row[segStartCol], CultureInfo.InvariantCulture));
                }
                else
                {
                    Assert.Equal("", row[segStartCol]);
                }
            }

            int segAgeCol = Array.IndexOf(header, "segment_age");
            if (segAgeCol >= 0)
            {
                if (o.AtrSmoothRegimeSegment.RegimeAge.HasValue)
                {
                    Assert.Equal(
                        o.AtrSmoothRegimeSegment.RegimeAge.Value,
                        int.Parse(row[segAgeCol], CultureInfo.InvariantCulture));
                }
                else
                {
                    Assert.Equal("", row[segAgeCol]);
                }
            }

            int segTrCol = Array.IndexOf(header, "segment_transition");
            if (segTrCol >= 0)
            {
                Assert.Equal(
                    (int)o.AtrSmoothRegimeSegment.RegimeTransition,
                    int.Parse(row[segTrCol], CultureInfo.InvariantCulture));
            }

            // Darvas family (DarvasBox mode)
            int boxCol = Array.IndexOf(header, "darvas_has_box");
            if (boxCol >= 0)
            {
                Assert.Equal(o.DarvasBoxDistance.HasBox ? "1" : "0", row[boxCol]);
                Assert.Equal(
                    o.DarvasBoxDistance.SignedClosingDistance,
                    ParseDoubleToken(row[idx("darvas_signed_closing_distance")]));
                Assert.Equal(
                    o.DarvasBoxDistance.AbsoluteClosingDistance,
                    ParseDoubleToken(row[idx("darvas_absolute_closing_distance")]));
                Assert.Equal(
                    o.MeanDarvasClosingDistance.MeanSignedDistance,
                    ParseDoubleToken(row[idx("darvas_mean_signed_distance")]));
            }

            // Dual-reference family (HmaAtrSmooth mode)
            int hmaCol = Array.IndexOf(header, "mean_hma_atrsmooth_distance");
            if (hmaCol >= 0)
            {
                Assert.Equal(
                    o.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                    ParseDoubleToken(row[hmaCol]));
                Assert.Equal(
                    (int)o.HmaPriceAtrSmoothAlignment.Alignment,
                    int.Parse(row[idx("hma_price_atrsmooth_alignment")], CultureInfo.InvariantCulture));
                Assert.Equal(
                    o.HmaAtrSmoothSeparation.Separation,
                    ParseDoubleToken(row[idx("hma_atrsmooth_separation")]));
                Assert.Equal(
                    o.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition,
                    ParseDoubleToken(row[idx("hma_atrsmooth_relative_close_position")]));
            }
        }

        // -------------------------------------------------------------
        // Mode column-set conformance (M10.0 §3 ratified counts)
        // -------------------------------------------------------------

        [Fact]
        public void G1b_ModeColumnSets_ConformToRatifiedCounts()
        {
            Assert.Equal(32, Schema.Columns(ExportMode.ATRSmooth2).Length);
            Assert.Equal(36, Schema.Columns(ExportMode.HmaAtrSmooth).Length);
            Assert.Equal(31, Schema.Columns(ExportMode.DarvasBox).Length);
            Assert.Equal(27, Schema.Columns(ExportMode.Hma).Length);

            // Segment columns exist ONLY in ATRSmooth-based modes
            Assert.Contains("segment_id", Schema.Columns(ExportMode.ATRSmooth2));
            Assert.Contains("segment_id", Schema.Columns(ExportMode.HmaAtrSmooth));
            Assert.DoesNotContain("segment_id", Schema.Columns(ExportMode.DarvasBox));
            Assert.DoesNotContain("segment_id", Schema.Columns(ExportMode.Hma));

            // Darvas columns ONLY in Darvas mode
            Assert.Contains("darvas_has_box", Schema.Columns(ExportMode.DarvasBox));
            Assert.DoesNotContain("darvas_has_box", Schema.Columns(ExportMode.ATRSmooth2));

            // Dual-reference columns ONLY in composite mode
            Assert.Contains("mean_hma_atrsmooth_distance", Schema.Columns(ExportMode.HmaAtrSmooth));
            Assert.DoesNotContain("mean_hma_atrsmooth_distance", Schema.Columns(ExportMode.ATRSmooth2));

            // M11.1 geometry columns ONLY in composite mode
            Assert.Contains("hma_atrsmooth_separation", Schema.Columns(ExportMode.HmaAtrSmooth));
            Assert.Contains("hma_atrsmooth_relative_close_position", Schema.Columns(ExportMode.HmaAtrSmooth));
            foreach (ExportMode other in new[]
                { ExportMode.ATRSmooth2, ExportMode.DarvasBox, ExportMode.Hma })
            {
                Assert.DoesNotContain("hma_atrsmooth_separation", Schema.Columns(other));
                Assert.DoesNotContain("hma_atrsmooth_relative_close_position", Schema.Columns(other));
            }

            // No TSI/survival/exhaustion variables anywhere (M10.1 §23)
            string[] forbidden = new[]
            {
                "q_star", "hazard", "survival", "exhaustion", "trend_sustainability",
                "trend_termination", "segment_duration", "time_to_flip"
            };

            foreach (ExportMode mode in Enum.GetValues(typeof(ExportMode)))
            {
                foreach (string col in Schema.Columns(mode))
                {
                    foreach (string banned in forbidden)
                    {
                        Assert.DoesNotContain(banned, col);
                    }
                }
            }
        }

        // -------------------------------------------------------------
        // G1c — every mode exports and matches the engine (mode matrix)
        // -------------------------------------------------------------

        [Theory]
        [InlineData("darvasbox")]
        [InlineData("hma")]
        [InlineData("hmaatrsmooth")]
        public void G1c_AllModes_ExportAndMatchEngine(string modeName)
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g1c.csv", 300, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 101);

                ExportMode mode = modeName switch
                {
                    "darvasbox" => ExportMode.DarvasBox,
                    "hma" => ExportMode.Hma,
                    _ => ExportMode.HmaAtrSmooth
                };

                (string artifact, _) = RunExport(capture, "G1C_" + modeName.ToUpperInvariant(), mode, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                Assert.Equal(Schema.Columns(mode), header);
                Assert.Equal(300, rows.Count);

                List<EngineValues> oracle = RunOracle(capture, 300, mode);
                for (int i = 0; i < 300; i++)
                {
                    AssertBarEqualsEngine(rows[i], header, oracle[i], i);
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // Registry gate: unregistered dataset and hash mismatch
        // -------------------------------------------------------------

        [Fact]
        public void G9b_RegistryGate_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g9b.csv", 50, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 103);

                // 1. Unregistered dataset id → fail closed (the lookup
                // throws during Create, so the whole construct+run is
                // inside the asserted lambda)
                var entry = EntryFor(capture, "GOOD", "", "");
                Assert.Throws<ExportException>(() =>
                {
                    var runnerUnregistered = ExportRunner.Create(
                        Registry(entry),
                        "unused",
                        "NOT_REGISTERED",
                        "atrsmooth2",
                        Path.Combine(dir, "unreg-out.csv"),
                        capture,
                        "0123456789abcdef0123456789abcdef01234567");

                    runnerUnregistered.Run();
                });

                // 2. Wrong pinned hash → fail closed
                var badHash = EntryFor(capture, "BADHASH", "", "");
                badHash.SourceSha256 = new string('0', 64);
                var runnerBadHash = ExportRunner.Create(
                    Registry(badHash),
                    "unused",
                    "BADHASH",
                    "atrsmooth2",
                    Path.Combine(dir, "badhash-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runnerBadHash.Run());
                Assert.False(File.Exists(Path.Combine(dir, "badhash-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G18 — real EURUSD fixture: first-year partition on the repo's
        // fixture (7 days of M1) — full capture inside year 1
        // -------------------------------------------------------------

        [Fact]
        public void G18_RepoFixture_FullCaptureInsideResearchWindow()
        {
            string dir = TempDir();
            try
            {
                string fixture = Path.Combine(FixtureDir(), "EURUSD_M1_10000.csv");
                Assert.True(File.Exists(fixture), "fixture missing: " + fixture);

                var entry = EntryFor(fixture, "FIXTURE10K", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "FIXTURE10K",
                    "atrsmooth2",
                    Path.Combine(dir, "fixture10k.csv"),
                    fixture,
                    "0123456789abcdef0123456789abcdef01234567");

                ExportResult result = runner.Run();

                // The 10k M1 fixture spans 2024-01-02 → 2024-01-08:
                // entirely inside year 1 → all 10000 rows exported,
                // no holdout.
                Assert.Equal(10_000, result.ResearchRows);
                Assert.False(result.HasHoldout);

                (string[] header, List<string[]> rows) =
                    ReadArtifact(result.ArtifactPath);
                Assert.Equal(Schema.Columns(ExportMode.ATRSmooth2), header);
                Assert.Equal(10_001, File.ReadAllLines(result.ArtifactPath).Length);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G19 — CLI end-to-end: real built executable, real registry
        // file, deterministic rerun
        // -------------------------------------------------------------

        [Fact]
        public void G19_CliEndToEnd_DeterministicRerun()
        {
            string dir = TempDir();
            try
            {
                // Locate the built BulkExport CLI. FixtureDir =
                // <repo>/Tests/TestData → two levels up is the repo root.
                string toolRoot = FixtureDir();
                string repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(toolRoot))!;
                string cli = Path.Combine(
                    repoRoot, "tools", "BulkExport", "bin", "Debug", "net6.0", "BulkExport.dll");
                Assert.True(File.Exists(cli), "BulkExport CLI not built: " + cli);

                string capture = WriteSyntheticCapture(
                    dir, "g19.csv", 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 107);

                // Write a real registry JSON
                string registryPath = Path.Combine(dir, "registry.json");
                var entry = EntryFor(capture, "G19", "", "");
                string regJson =
                    "[\n  {\n" +
                    "    \"dataset_id\": \"G19\",\n" +
                    "    \"dataset_version\": \"v1\",\n" +
                    "    \"filename\": \"g19.csv\",\n" +
                    "    \"source_sha256\": \"" + entry.SourceSha256 + "\",\n" +
                    "    \"first_timestamp\": \"" + entry.FirstTimestamp + "\",\n" +
                    "    \"last_timestamp\": \"" + entry.LastTimestamp + "\",\n" +
                    "    \"partition_policy\": \"first_calendar_year_only\"\n" +
                    "  }\n]\n";
                File.WriteAllText(registryPath, regJson, new UTF8Encoding(false));

                string outDir1 = Path.Combine(dir, "run1");
                string outDir2 = Path.Combine(dir, "run2");
                Directory.CreateDirectory(outDir1);
                Directory.CreateDirectory(outDir2);
                string out1 = Path.Combine(outDir1, "export.csv");
                string out2 = Path.Combine(outDir2, "export.csv");

                foreach (string outPath in new[] { out1, out2 })
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "exec \"" + cli + "\" --registry \"" + registryPath
                            + "\" --dataset G19 --mode atrsmooth2 --capture \"" + capture
                            + "\" --out \"" + outPath
                            + "\" --engine-commit 0123456789abcdef0123456789abcdef01234567",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };

                    using var proc = System.Diagnostics.Process.Start(psi)!;
                    string stdout = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    Assert.True(proc.ExitCode == 0,
                        "CLI exited " + proc.ExitCode + "; stderr: "
                        + proc.StandardError.ReadToEnd());
                }

                // Deterministic rerun: byte-identical artifacts and manifests
                Assert.True(
                    File.ReadAllBytes(out1).SequenceEqual(File.ReadAllBytes(out2)),
                    "two CLI runs must produce byte-identical artifacts");
                Assert.True(
                    File.ReadAllBytes(out1 + ".manifest.json")
                        .SequenceEqual(File.ReadAllBytes(out2 + ".manifest.json")),
                    "two CLI runs must produce byte-identical manifests");

                // Manifest hash integrity on the real CLI output
                string manifest = File.ReadAllText(out1 + ".manifest.json");
                Assert.Contains("\"artifact_sha256\": \"" + Sha256File(out1) + "\"", manifest);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G2 — warm-up semantics (segment nulls before establishment)
        // -------------------------------------------------------------

        [Fact]
        public void G2_WarmUpSemantics()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g2.csv", 300, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 11);

                (string artifact, _) = RunExport(capture, "G2", ExportMode.ATRSmooth2, dir);
                (_, List<string[]> rows) = ReadArtifact(artifact);

                List<EngineValues> oracle = RunOracle(capture, 300, ExportMode.ATRSmooth2);

                int firstEstablished = -1;
                for (int i = 0; i < oracle.Count; i++)
                {
                    if (oracle[i].AtrSmoothRegimeSegment.RegimeId.HasValue)
                    {
                        firstEstablished = i;
                        break;
                    }
                }

                Assert.True(firstEstablished > 0, "Fixture should have a warm-up prefix.");

                for (int i = 0; i < firstEstablished; i++)
                {
                    Assert.Equal("", rows[i][Array.IndexOf(ReadHeader(artifact), "segment_id")]);
                }

                // Establishment bar itself (G3 divergence check lives here too)
                int est = firstEstablished;
                string[] header = ReadHeader(artifact);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "segment_id")]);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "segment_age")]);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "segment_transition")]);

                // The M9/RFE divergence: ReversalEngine DOES count the
                // 0→±1 establishment as a reversal; the segment engine
                // does NOT count it as a transition.
                Assert.Equal("1", rows[est][Array.IndexOf(header, "reversal_is_bar")]);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "reversal_bars_since")]);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static string[] ReadHeader(string artifact)
        {
            return File.ReadLines(artifact).First().Split(',');
        }

        // -------------------------------------------------------------
        // G3 — establishment divergence is explicitly locked
        // -------------------------------------------------------------

        [Fact]
        public void G3_EstablishmentDivergence_ReversalCountsIt_SegmentDoesNot()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g3.csv", 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 13);

                (string artifact, _) = RunExport(capture, "G3", ExportMode.ATRSmooth2, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);
                List<EngineValues> oracle = RunOracle(capture, 400, ExportMode.ATRSmooth2);

                int est = oracle.FindIndex(f => f.AtrSmoothRegimeSegment.RegimeId.HasValue);
                Assert.True(est > 0, "expected a warm-up prefix");

                // Reversal: establishment counts (is_bar=1, bars_since=0).
                Assert.Equal("1", rows[est][Array.IndexOf(header, "reversal_is_bar")]);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "reversal_bars_since")]);

                // Segment: establishment is NOT a transition.
                Assert.Equal("0", rows[est][Array.IndexOf(header, "segment_transition")]);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "segment_id")]);
                Assert.Equal("0", rows[est][Array.IndexOf(header, "segment_age")]);

                // After establishment, bars_since ≡ segment_age exactly.
                for (int i = est; i < rows.Count; i++)
                {
                    Assert.Equal(
                        rows[i][Array.IndexOf(header, "reversal_bars_since")],
                        rows[i][Array.IndexOf(header, "segment_age")]);
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G4 — established regime flip
        // -------------------------------------------------------------

        [Fact]
        public void G4_RegimeFlip()
        {
            string dir = TempDir();
            try
            {
                // Steeper walk, more flips
                string capture = WriteSyntheticCapture(
                    dir, "g4.csv", 500, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 17);

                (string artifact, _) = RunExport(capture, "G4", ExportMode.ATRSmooth2, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                int idCol = Array.IndexOf(header, "segment_id");
                int trCol = Array.IndexOf(header, "segment_transition");
                int ageCol = Array.IndexOf(header, "segment_age");

                int flips = 0;
                for (int i = 1; i < rows.Count; i++)
                {
                    string prev = rows[i - 1][idCol];
                    string curr = rows[i][idCol];
                    if (prev != curr)
                    {
                        flips++;
                        Assert.Equal("0", rows[i][ageCol]); // age resets

                        // Skip the establishment row: segment 0
                        // starting is NOT a transition (M9 contract);
                        // every LATER id change is a real flip with
                        // transition ±1.
                        if (prev != "")
                        {
                            string t = rows[i][trCol];
                            Assert.True(t == "1" || t == "-1",
                                $"flip transition must be ±1, got {t}");
                        }
                        else
                        {
                            Assert.Equal("0", rows[i][trCol]);
                        }
                    }
                    else if (curr != "")
                    {
                        Assert.Equal("0", rows[i][trCol]);
                    }
                }

                // The synthetic random walk with 5.1 ATR multiplier
                // should produce at least one flip in 500 bars.
                Assert.True(flips > 0, $"expected at least one regime flip, saw {flips}");
            }
            finally
                {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G5 — continuation lockstep
        // -------------------------------------------------------------

        [Fact]
        public void G5_ContinuationLockstep()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g5.csv", 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 19);

                (string artifact, _) = RunExport(capture, "G5", ExportMode.ATRSmooth2, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                int idCol = Array.IndexOf(header, "segment_id");
                int ageCol = Array.IndexOf(header, "segment_age");

                string lastId = "";
                int expectedAge = -1;
                for (int i = 0; i < rows.Count; i++)
                {
                    string id = rows[i][idCol];
                    if (id != lastId)
                    {
                        expectedAge = 0;
                        lastId = id;
                    }
                    else if (id == "")
                    {
                        // Still warm-up (both empty): age stays null.
                        Assert.Equal("", rows[i][ageCol]);
                        continue;
                    }
                    else
                    {
                        expectedAge++;
                    }

                    Assert.Equal(
                        expectedAge.ToString(CultureInfo.InvariantCulture),
                        rows[i][ageCol]);
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G6 — statistics below minimum (retain-last semantics preserved)
        // -------------------------------------------------------------

        [Fact]
        public void G6_StatisticsBelowMinimum()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g6.csv", 50, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 23);

                (string artifact, _) = RunExport(capture, "G6", ExportMode.ATRSmooth2, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                int countCol = Array.IndexOf(header, "statistics_observation_count");

                // Bar 0: 1 observation, mean publishable (min_n=1),
                // skewness/kurtosis not yet (min_n=3/4) — exported
                // values are the retained defaults, verbatim.
                Assert.Equal("1", rows[0][countCol]);

                // The tokens are the runtime values (0.0 defaults or
                // last-published); the test asserts they parse as
                // finite-or-NaN tokens and equal the oracle's defaults
                List<EngineValues> oracle = RunOracle(capture, 50, ExportMode.ATRSmooth2);
                Assert.Equal(
                    oracle[0].Statistics.Shape.Skewness,
                    ParseDoubleToken(rows[0][Array.IndexOf(header, "statistics_skewness")]));
                Assert.Equal(
                    oracle[0].Statistics.Shape.Kurtosis,
                    ParseDoubleToken(rows[0][Array.IndexOf(header, "statistics_kurtosis")]));

                // By bar 3 (n=4), all statistics are publishable.
                Assert.Equal("4", rows[3][countCol]);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G7 — exact-boundary / zero-volume / flat prices
        // -------------------------------------------------------------

        [Fact]
        public void G7_ExactBoundary_ZeroVolume_FlatPrices()
        {
            string dir = TempDir();
            try
            {
                // Near-flat prices with a minimal 1-tick high/low
                // spread: True Range is tiny but strictly positive,
                // so the engine's ScaleValidator (scale > 0) holds.
                // Zero volume keeps the VWMA fallback exercised.
                string path = Path.Combine(dir, "g7.csv");
                var sb = new StringBuilder();
                sb.Append("OpenTimeUtc,Open,High,Low,Close,TickVolume\n");
                var t = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
                for (int i = 0; i < 40; i++)
                {
                    sb.Append(t.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"))
                      .Append(",1.10000,1.10001,1.09999,1.10000,0\n");
                    t = t.AddMinutes(60);
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

                (string artifact, _) = RunExport(path, "G7", ExportMode.ATRSmooth2, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                List<EngineValues> oracle = RunOracle(path, 40, ExportMode.ATRSmooth2);

                for (int i = 0; i < rows.Count; i++)
                {
                    AssertBarEqualsEngine(rows[i], header, oracle[i], i);
                    // Near-flat market: the distance is tiny (bounded
                    // by the 1-tick high/low spread) — exact value is
                    // engine-owned (AssertBarEqualsEngine above).
                    double d = ParseDoubleToken(rows[i][Array.IndexOf(header, "distance_signed")]);
                    Assert.True(Math.Abs(d) < 0.001, $"distance unexpectedly large: {d}");
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G8 — deterministic output (byte-identical exports)
        // -------------------------------------------------------------

        [Fact]
        public void G8_DeterministicOutput()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g8.csv", 300, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 29);

                (string a1, string m1) = RunExport(capture, "G8", ExportMode.ATRSmooth2, dir);
                (string a2, string m2) = RunExport(capture, "G8B", ExportMode.ATRSmooth2, dir);

                byte[] b1 = File.ReadAllBytes(a1);
                byte[] b2 = File.ReadAllBytes(a2);
                Assert.True(b1.SequenceEqual(b2), "artifacts must be byte-identical");

                string mf1 = File.ReadAllText(m1);
                string mf2 = File.ReadAllText(m2);

                // Manifests differ ONLY in the dataset-identity fields
                // (artifact_file, dataset_id) — normalize both away and
                // compare the remainder byte-for-byte.
                string n1 = mf1.Replace("G8.csv", "X.csv").Replace("\"G8\"", "\"D\"");
                string n2 = mf2.Replace("G8B.csv", "X.csv").Replace("\"G8B\"", "\"D\"");
                Assert.Equal(n1, n2);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G9 — malformed input fails closed
        // -------------------------------------------------------------

        [Fact]
        public void G9_MalformedInput_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                string good = WriteSyntheticCapture(
                    dir, "good.csv", 30, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 31);

                // 1. wrong field count
                string bad1 = Path.Combine(dir, "bad1.csv");
                string[] lines1 = File.ReadAllLines(good);
                lines1[5] = lines1[5].Substring(0, lines1[5].LastIndexOf(','));
                File.WriteAllLines(bad1, lines1, new UTF8Encoding(false));

                // 2. quoted field
                string bad2 = Path.Combine(dir, "bad2.csv");
                string[] lines2 = File.ReadAllLines(good);
                lines2[5] = lines2[5].Replace("1.10", "\"1.10", StringComparison.Ordinal);
                File.WriteAllLines(bad2, lines2, new UTF8Encoding(false));

                // 3. regressed timestamp
                string bad3 = Path.Combine(dir, "bad3.csv");
                string[] lines3 = File.ReadAllLines(good);
                lines3[5] = lines3[5].Replace(
                    lines3[5].Split(',')[0],
                    new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"),
                    StringComparison.Ordinal);
                File.WriteAllLines(bad3, lines3, new UTF8Encoding(false));

                foreach (string bad in new[] { bad1, bad2, bad3 })
                {
                    var entry = EntryFor(bad, "BAD", "", "");
                    var runner = ExportRunner.Create(
                        Registry(entry),
                        "unused",
                        "BAD",
                        "atrsmooth2",
                        Path.Combine(dir, Path.GetFileNameWithoutExtension(bad) + "-out.csv"),
                        bad,
                        "0123456789abcdef0123456789abcdef01234567");

                    Assert.Throws<ExportException>(() => runner.Run());
                }

                // No output artifacts survive
                Assert.False(File.Exists(Path.Combine(dir, "bad1-out.csv")));
                Assert.False(File.Exists(Path.Combine(dir, "bad1-out.csv.partial")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G10 — atomicity (no partial survivors on failure)
        // -------------------------------------------------------------

        [Fact]
        public void G10_Atomicity_NoPartialSurvivors()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g10.csv", 60, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 37);

                // Mutating source: the underlying file changes after
                // pass 1, so the post-run re-hash fails.
                string mutatorPath = Path.Combine(dir, "mutating.csv");
                File.Copy(capture, mutatorPath);

                var entry = EntryFor(mutatorPath, "G10", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "G10",
                    "atrsmooth2",
                    Path.Combine(dir, "g10-out.csv"),
                    mutatorPath,
                    "0123456789abcdef0123456789abcdef01234567");

                // Run in a task; mutate the file the moment the write
                // phase starts. We simulate deterministically instead:
                // shrink the entry's LastTimestamp expectation so the
                // identity check fails AFTER a successful write phase.
                entry.LastTimestamp = "1999-01-01T00:00:00.0000000Z";

                Assert.Throws<ExportException>(() => runner.Run());

                Assert.False(File.Exists(Path.Combine(dir, "g10-out.csv")));
                Assert.False(File.Exists(Path.Combine(dir, "g10-out.csv.partial")));
                Assert.False(File.Exists(Path.Combine(dir, "g10-out.csv.manifest.json")));
                Assert.False(File.Exists(Path.Combine(dir, "g10-out.csv.manifest.json.partial")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G11 — manifest integrity
        // -------------------------------------------------------------

        [Fact]
        public void G11_ManifestIntegrity()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g11.csv", 100, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 41);

                (string artifact, string manifestPath) = RunExport(capture, "G11", ExportMode.ATRSmooth2, dir);

                string manifest = File.ReadAllText(manifestPath);

                // Deterministic formatting
                Assert.EndsWith("\n", manifest);
                Assert.DoesNotContain("\r", manifest);
                Assert.DoesNotContain("  \n", manifest.Replace("  \"columns\"", "X"));

                // artifact hash matches actual bytes
                string actualHash = Sha256File(artifact);
                Assert.Contains("\"artifact_sha256\": \"" + actualHash + "\"", manifest);

                // row count matches
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);
                Assert.Contains("\"row_count\": " + rows.Count.ToString(CultureInfo.InvariantCulture), manifest);

                // source hash matches
                Assert.Contains("\"source_sha256\": \"" + Sha256File(capture) + "\"", manifest);

                // engine commit present
                Assert.Contains("\"engine_commit\": \"0123456789abcdef0123456789abcdef01234567\"", manifest);

                // schema + exporter versions
                Assert.Contains("\"schema_version\": \"measurement-export/1.1.0\"", manifest);
                Assert.Contains("\"exporter_version\": \"1.0.0\"", manifest);

                // columns list matches header
                foreach (string col in header)
                {
                    Assert.Contains("\"" + col + "\"", manifest);
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G12 — first-calendar-year boundary
        // -------------------------------------------------------------

        [Fact]
        public void G12_FirstCalendarYearBoundary()
        {
            string dir = TempDir();
            try
            {
                // 2 years of hourly bars (gap-free grid)
                string capture = WriteSyntheticCapture(
                    dir, "g12.csv", 24 * 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 43);

                (string artifact, string manifestPath) = RunExport(capture, "G12", ExportMode.ATRSmooth2, dir);
                (_, List<string[]> rows) = ReadArtifact(artifact);
                string manifest = File.ReadAllText(manifestPath);

                // Boundary: 2024-01-02 + 1y = 2025-01-02 00:00Z
                DateTime expectedEnd = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);

                Assert.Contains(
                    "\"research_end\": \"2025-01-02T00:00:00.0000000Z\"",
                    manifest);
                Assert.Contains("\"holdout_start\": \"2025-01-02T00:00:00.0000000Z\"", manifest);

                // Row count: bars strictly before boundary
                int expected = 24 * 365; // 2024-01-02 00:00 → 2025-01-02 00:00 minus... compute below
                // The grid: 24*400 bars from 2024-01-02 hourly → last bar 2024-01-02 + (9600-1)h
                // Bars strictly before 2025-01-02T00:00: 366 days (2024 is a leap year,
                // and the window spans 2024-01-02 → 2025-01-02) = 366*24 = 8784.
                expected = 366 * 24;
                Assert.Equal(expected, rows.Count);

                // Last research row's timestamp is strictly inside year 1
                string[] lastRow = rows[rows.Count - 1];
                string ts = lastRow[Array.IndexOf(ReadHeader(artifact), "timestamp")];
                DateTime lastUtc = DateTime.Parse(ts, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                Assert.True(lastUtc < expectedEnd);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G13 — leap-year boundary
        // -------------------------------------------------------------

        [Fact]
        public void G13_LeapYearBoundary()
        {
            // The 2024 grid (above) already exercises the leap day:
            // 2024-01-02 + 1 calendar year = 2025-01-02, and the
            // window includes 2024-02-29 (366 days = 8784 hours).
            // This test pins the arithmetic directly on Partition.
            var p = new Partition(new DateTime(2024, 2, 29, 12, 0, 0, DateTimeKind.Utc));

            Assert.Equal(
                new DateTime(2025, 2, 28, 12, 0, 0, DateTimeKind.Utc),
                p.ResearchEndExclusive);

            // Leap day itself is inside the research window
            Assert.True(p.IsResearch(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)));

            var p2 = new Partition(new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(
                new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                p2.ResearchEndExclusive); // 2024-02-29 INSIDE window

            // Non-leap first year
            var p3 = new Partition(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(
                new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                p3.ResearchEndExclusive);
        }

        // -------------------------------------------------------------
        // G14 — holdout exclusion (no post-boundary bar in artifact)
        // -------------------------------------------------------------

        [Fact]
        public void G14_HoldoutExclusion()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g14.csv", 24 * 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 47);

                (string artifact, string manifestPath) = RunExport(capture, "G14", ExportMode.ATRSmooth2, dir);
                (string[] header, List<string[]> rows) = ReadArtifact(artifact);

                int tsCol = Array.IndexOf(header, "timestamp");
                DateTime boundary = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);

                // Every exported timestamp is strictly before the boundary
                foreach (string[] row in rows)
                {
                    DateTime utc = DateTime.Parse(
                        row[tsCol],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    Assert.True(utc < boundary,
                        $"holdout bar leaked into research artifact: {row[tsCol]}");
                }

                // And the holdout truly exists in the source (test not vacuous)
                var source = new CsvBarSource(capture);
                ScanResult scan = source.Scan();
                Assert.True(scan.RowCount > rows.Count,
                    "fixture must contain post-boundary bars for the test to bite");
                Assert.Equal(24 * 400, (int)scan.RowCount);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G15 — research export beyond boundary FAILS CLOSED
        // -------------------------------------------------------------

        [Fact]
        public void G15_BeyondBoundary_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                // A capture spanning >1 year. The research export
                // exports first-year rows only. Any attempt to make
                // the export cover the full capture by shifting the
                // registry's first_timestamp (the only lever a caller
                // has) FAILS the identity gate — the boundary cannot
                // be gamed through the registry.
                string capture = WriteSyntheticCapture(
                    dir, "g15.csv", 24 * 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 53);

                var entry = EntryFor(capture, "G15", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "G15",
                    "atrsmooth2",
                    Path.Combine(dir, "g15-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                ExportResult result = runner.Run();

                // Firewall held: first-year rows only.
                Assert.Equal(366 * 24, result.ResearchRows);
                Assert.Equal(24 * 400, result.TotalSourceRows);
                Assert.True(result.HasHoldout);
                Assert.True(result.LastResearchTimestamp.CompareTo("2025-01-02") < 0);

                // Gaming attempt: shift the registry first_timestamp
                // forward (would move the boundary later). The
                // identity gate fails closed.
                var gamed = EntryFor(capture, "G15X", "", "");
                gamed.FirstTimestamp = "2024-06-01T00:00:00.0000000Z";
                var runnerX = ExportRunner.Create(
                    Registry(gamed),
                    "unused",
                    "G15X",
                    "atrsmooth2",
                    Path.Combine(dir, "g15x-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runnerX.Run());
                Assert.False(File.Exists(Path.Combine(dir, "g15x-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G16 — per-dataset independent partition
        // -------------------------------------------------------------

        [Fact]
        public void G16_PerDatasetIndependentPartition()
        {
            string dir = TempDir();
            try
            {
                // Two datasets with different first bars → different boundaries
                string capA = WriteSyntheticCapture(
                    dir, "a.csv", 24 * 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 59);
                string capB = WriteSyntheticCapture(
                    dir, "b.csv", 24 * 400, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), 60, 61);

                (string artA, string manA) = RunExport(capA, "A", ExportMode.ATRSmooth2, dir);
                (string artB, string manB) = RunExport(capB, "B", ExportMode.ATRSmooth2, dir);

                Assert.Contains("\"research_end\": \"2025-01-02T00:00:00.0000000Z\"", File.ReadAllText(manA));
                Assert.Contains("\"research_end\": \"2026-06-01T00:00:00.0000000Z\"", File.ReadAllText(manB));

                (_, List<string[]> rowsA) = ReadArtifact(artA);
                (_, List<string[]> rowsB) = ReadArtifact(artB);
                Assert.Equal(366 * 24, rowsA.Count);
                Assert.Equal(365 * 24, rowsB.Count); // 2025-06-01 → 2026-06-01, no leap day
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // G17 — manifest records research/holdout boundary
        // -------------------------------------------------------------

        [Fact]
        public void G17_ManifestRecordsPartition()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCapture(
                    dir, "g17.csv", 24 * 400, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 67);

                (string artifact, string manifestPath) = RunExport(capture, "G17", ExportMode.ATRSmooth2, dir);
                string manifest = File.ReadAllText(manifestPath);

                Assert.Contains("\"partition_policy\": \"first_calendar_year_only\"", manifest);
                Assert.Contains("\"research_start\": \"2024-01-02T00:00:00.0000000Z\"", manifest);
                Assert.Contains("\"research_end\": \"2025-01-02T00:00:00.0000000Z\"", manifest);
                Assert.Contains("\"holdout_start\": \"2025-01-02T00:00:00.0000000Z\"", manifest);

                // The manifest's last_timestamp equals the LAST
                // RESEARCH row's token, not the source capture's.
                (_, List<string[]> rows) = ReadArtifact(artifact);
                string lastResearch = rows[rows.Count - 1][Array.IndexOf(
                    ReadHeader(artifact), "timestamp")];
                Assert.Contains("\"last_timestamp\": \"" + lastResearch + "\"", manifest);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // -------------------------------------------------------------
        // M10.1.x — recorder_v1_1 extraction-boundary suite
        // (V1.1-1 … V1.1-9). Spread is validated source-field
        // provenance ONLY: never an engine input, never a measurement
        // column (Schema.RenderRow reads Tokens[0..5] exclusively).
        // -------------------------------------------------------------

        private static string SpreadZero(int i) => "0";

        [Fact]
        public void V11_1_ValidRecorderV11_Accepted()
        {
            string dir = TempDir();
            try
            {
                // 200 hourly bars ≈ 8.3 days — all inside year 1.
                string capture = WriteSyntheticCaptureV11(
                    dir, "v11.csv", 200,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 211, SpreadZero);

                (string artifact, string manifestPath) = RunExport(
                    capture, "V11A", ExportMode.ATRSmooth2, dir);

                (string[] header, List<string[]> rows) = ReadArtifact(artifact);
                Assert.Equal(Schema.Columns(ExportMode.ATRSmooth2), header);
                Assert.Equal(200, rows.Count);

                // Engine equality on the V1.1 capture — the same oracle
                // path proves the parse fed the engine identically.
                List<EngineValues> oracle = RunOracle(capture, 200, ExportMode.ATRSmooth2);
                for (int i = 0; i < 200; i++)
                {
                    AssertBarEqualsEngine(rows[i], header, oracle[i], i);
                }

                // Manifest records the V1.1 source schema
                string manifest = File.ReadAllText(manifestPath);
                Assert.Contains("\"source_schema\": \"recorder_v1_1\"", manifest);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_2_ValidSpreadValues_Accepted()
        {
            string dir = TempDir();
            try
            {
                // Realistic spreads: positive fractionals and the
                // recorder's valid zero (backfill-unavailable).
                string capture = WriteSyntheticCaptureV11(
                    dir, "v11spread.csv", 60,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 223,
                    i => (i % 3) switch
                    {
                        0 => "0",
                        1 => "0.00012",
                        _ => "0.00035"
                    });

                (string artifact, _) = RunExport(capture, "V11B", ExportMode.ATRSmooth2, dir);
                (_, List<string[]> rows) = ReadArtifact(artifact);
                Assert.Equal(60, rows.Count);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_3_MalformedSpread_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                string capture = WriteSyntheticCaptureV11(
                    dir, "v11bad.csv", 30,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 227,
                    i => i == 7 ? "not-a-number" : "0");

                var entry = EntryFor(capture, "V11C", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "V11C",
                    "atrsmooth2",
                    Path.Combine(dir, "v11bad-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runner.Run());
                Assert.False(File.Exists(Path.Combine(dir, "v11bad-out.csv")));
                Assert.False(File.Exists(Path.Combine(dir, "v11bad-out.csv.partial")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_4_NegativeSpread_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                // Negative spread violates the recorder contract
                // (finite, non-negative) even though it parses as a
                // number — the fail must be semantic, not just syntactic.
                string capture = WriteSyntheticCaptureV11(
                    dir, "v11neg.csv", 30,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 229,
                    i => i == 4 ? "-0.0001" : "0");

                var entry = EntryFor(capture, "V11D", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "V11D",
                    "atrsmooth2",
                    Path.Combine(dir, "v11neg-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runner.Run());
                Assert.False(File.Exists(Path.Combine(dir, "v11neg-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_5_SixColumnDeclaredV11_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                // A 6-column capture with a registry declaring
                // recorder_v1_1: the schema pin fails closed.
                string capture6 = WriteSyntheticCapture(
                    dir, "v6.csv", 30,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 233);

                var entry = EntryFor(capture6, "V11E", "", "");
                entry.SourceSchema = "recorder_v1_1";
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "V11E",
                    "atrsmooth2",
                    Path.Combine(dir, "v6decl-out.csv"),
                    capture6,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runner.Run());
                Assert.False(File.Exists(Path.Combine(dir, "v6decl-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_6_SevenColumnDeclaredV1_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                // The inverse pin: a 7-column capture declared as
                // recorder_v1 fails closed.
                string capture7 = WriteSyntheticCaptureV11(
                    dir, "v7.csv", 30,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 239, SpreadZero);

                var entry = EntryFor(capture7, "V11F", "", "");
                entry.SourceSchema = "recorder_v1";
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "V11F",
                    "atrsmooth2",
                    Path.Combine(dir, "v7decl-out.csv"),
                    capture7,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runner.Run());
                Assert.False(File.Exists(Path.Combine(dir, "v7decl-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_7_EightColumns_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                // Extra field beyond the declared header — rejected at
                // the field-count gate.
                string capture = WriteSyntheticCaptureV11(
                    dir, "v8.csv", 30,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 241, SpreadZero);
                string[] lines = File.ReadAllLines(capture);
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = lines[i] + ",EXTRA";
                }
                File.WriteAllLines(capture, lines, new UTF8Encoding(false));

                var entry = EntryFor(capture, "V11G", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "V11G",
                    "atrsmooth2",
                    Path.Combine(dir, "v8-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runner.Run());
                Assert.False(File.Exists(Path.Combine(dir, "v8-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_8_WrongFieldOrder_FailsClosed()
        {
            string dir = TempDir();
            try
            {
                // Reordered header: OpenTimeUtc,Open,High,Low,Close,
                // Spread,TickVolume — same set of names, wrong order.
                // Exact-header detection rejects it.
                string capture = WriteSyntheticCaptureV11(
                    dir, "v8order.csv", 30,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 251, SpreadZero);
                string[] lines = File.ReadAllLines(capture);
                lines[0] = "OpenTimeUtc,Open,High,Low,Close,Spread,TickVolume";
                File.WriteAllLines(capture, lines, new UTF8Encoding(false));

                var entry = EntryFor(capture, "V11H", "", "");
                var runner = ExportRunner.Create(
                    Registry(entry),
                    "unused",
                    "V11H",
                    "atrsmooth2",
                    Path.Combine(dir, "v8order-out.csv"),
                    capture,
                    "0123456789abcdef0123456789abcdef01234567");

                Assert.Throws<ExportException>(() => runner.Run());
                Assert.False(File.Exists(Path.Combine(dir, "v8order-out.csv")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_9_EngineNeutrality_SpreadNeverContaminatesMeasurements()
        {
            string dir = TempDir();
            try
            {
                // THE load-bearing test (M10.1.x §9 V1.1-7): identical
                // OHLCV, different valid Spread values → byte-identical
                // measurement artifacts. The 7-column writer consumes
                // Random(seed) identically to the 6-column writer, and
                // spreadOf never touches the RNG.
                string capA = WriteSyntheticCaptureV11(
                    dir, "neutralA.csv", 300,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 257, SpreadZero);
                string capB = WriteSyntheticCaptureV11(
                    dir, "neutralB.csv", 300,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    60, 257,
                    i => (0.0001 + (i % 7) * 0.00005)
                        .ToString("F8", CultureInfo.InvariantCulture));

                // Sanity: OHLCV tokens are truly identical
                string[] a = File.ReadAllLines(capA);
                string[] b = File.ReadAllLines(capB);
                Assert.Equal(a.Length, b.Length);
                for (int i = 1; i < a.Length; i++)
                {
                    string oA = a[i].Substring(0, a[i].LastIndexOf(','));
                    string oB = b[i].Substring(0, b[i].LastIndexOf(','));
                    Assert.Equal(oA, oB);
                    Assert.NotEqual(a[i], b[i]); // spreads differ
                }

                (string artifactA, _) = RunExport(capA, "NEUA", ExportMode.ATRSmooth2, dir);
                (string artifactB, _) = RunExport(capB, "NEUB", ExportMode.ATRSmooth2, dir);

                Assert.True(
                    File.ReadAllBytes(artifactA).SequenceEqual(File.ReadAllBytes(artifactB)),
                    "identical OHLCV with different valid spreads must produce "
                    + "byte-identical measurement artifacts");

                // Cross-schema: the same OHLCV as a 6-column capture
                // produces the identical artifact too (modulo nothing —
                // the artifact carries no source-schema trace beyond
                // the manifest, which this compares via the CSV only).
                string cap6 = WriteSyntheticCapture(
                    dir, "neutral6.csv", 300,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 60, 257);
                (string artifact6, _) = RunExport(cap6, "NEU6", ExportMode.ATRSmooth2, dir);
                Assert.True(
                    File.ReadAllBytes(artifactA).SequenceEqual(File.ReadAllBytes(artifact6)),
                    "recorder_v1_1 and recorder_v1 captures with identical OHLCV "
                    + "must produce byte-identical measurement artifacts");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void V11_10_DatasetIdentity_ProductionShasUnchanged()
        {
            // §9 V1.1-8: the ratified production capture identities are
            // pinned constants — the remediation must not touch the
            // captures. (The bytes themselves are outside the repo;
            // the pinned identity is the registry contract.)
            Assert.Equal(
                "8712b7207529e60b4b42bfbdcd7b36467046e2ee127c95dfc0c40d6920e6f3e6",
                Sha256File("C:\\Users\\Ali Zoghi\\OneDrive\\Documents\\cTrader\\Exports\\XAUUSD_Tick50_All.csv"));
            Assert.Equal(
                "d7afb8039c2a6370cb969d7e722414aa3e1d973cd33266dbf706b2125decc9d9",
                Sha256File("C:\\Users\\Ali Zoghi\\OneDrive\\Documents\\cTrader\\Exports\\EURUSD_Tick100_All (10).csv"));
        }

        // M11.2A — XAUUSD Tick25/Tick100 source admission pins
        // ---------------------------------------------------------------
        // Source-admission: both captures are newly registered to the
        // Distance certification chain (Tick50 was already certified by
        // V11_10 above; it is the unchanged control). The SHA pins fail
        // closed on any future source substitution.

        private const string XAUUSD_Tick25_Sha =
            "42f17379b8d7eb0d34afbc1c5569cc6549f633509cf1c5dcd3ef319d0e61a8d1";
        private const string XAUUSD_Tick50_Sha =
            "8712b7207529e60b4b42bfbdcd7b36467046e2ee127c95dfc0c40d6920e6f3e6";
        private const string XAUUSD_Tick100_Sha =
            "5f9e5fbb8ec9dffcf9e20ad56bfe7f3966d55341b2e7363871fef3f7d5cc3798";

        private const string XAUUSD_Tick25_Path =
            "C:\\Users\\Ali Zoghi\\OneDrive\\Documents\\cTrader\\Exports\\XAUUSD_Tick25_All.csv";
        private const string XAUUSD_Tick50_Path =
            "C:\\Users\\Ali Zoghi\\OneDrive\\Documents\\cTrader\\Exports\\XAUUSD_Tick50_All.csv";
        private const string XAUUSD_Tick100_Path =
            "C:\\Users\\Ali Zoghi\\OneDrive\\Documents\\cTrader\\Exports\\XAUUSD_Tick100_All.csv";

        [Fact]
        public void V11_11_XAUUSD_SourceIdentity_Pinned()
        {
            // §11 — pins the three XAUUSD source identities admitted to
            // the Distance chain. Tick25/Tick100 are newly admitted this
            // phase; Tick50 is the unchanged control. Byte substitution
            // of any capture fails this gate.
            Assert.Equal(XAUUSD_Tick50_Sha, Sha256File(XAUUSD_Tick50_Path));
            Assert.Equal(XAUUSD_Tick25_Sha, Sha256File(XAUUSD_Tick25_Path));
            Assert.Equal(XAUUSD_Tick100_Sha, Sha256File(XAUUSD_Tick100_Path));
        }

        [Theory]
        // §7 – independently reproduced via certified Scan -> Partition
        // -> CountBarsBefore machinery (tools/BulkExport CsvBarSource).
        // The two paths agree exactly (Path B: independent streaming
        // scan over frozen bytes, documented in the M11.2A report).
        // Tick50 is the pre-certified control (130,665).
        [InlineData(XAUUSD_Tick25_Path, XAUUSD_Tick25_Sha,
            "2025-06-15T23:59:43.5760000Z", 2234316)]
        [InlineData(XAUUSD_Tick50_Path, XAUUSD_Tick50_Sha,
            "2013-07-24T06:00:59.2240000Z", 130665)]
        [InlineData(XAUUSD_Tick100_Path, XAUUSD_Tick100_Sha,
            "2014-02-20T00:15:36.6590000Z", 68255)]
        public void V11_12_XAUUSD_Year1Invariant(
            string capturePath,
            string expectedSha,
            string firstTimestamp,
            int expectedResearchRows)
        {
            // §9.2 — fail closed if the source has been substituted
            Assert.Equal(expectedSha, Sha256File(capturePath));

            var source = new CsvBarSource(capturePath);
            ScanResult scan = source.Scan();

            // §3 / §5 boundary: first bar + 1 calendar year (AddYears,
            // leap-correct) — exactly as the engine computes it.
            DateTime firstUtc = DateTime.Parse(
                firstTimestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var partition = new Partition(firstUtc);

            int researchRows = source.CountBarsBefore(partition.ResearchEndExclusive);

            Assert.Equal(expectedResearchRows, researchRows);

            // Total rows in source must exceed the research window
            // (i.e. the capture genuinely spans the boundary).
            Assert.True((int)scan.RowCount > researchRows,
                "source must contain holdout rows for the invariant to be meaningful");
        }
    }
}

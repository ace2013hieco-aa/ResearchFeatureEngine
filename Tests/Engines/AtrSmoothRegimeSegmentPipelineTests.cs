using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

// The engine entry-point class name equals its namespace; alias it so
// the type resolves unambiguously inside this test namespace.
using Engine = ResearchFeatureEngine.ResearchFeatureEngine;
using ReversalDirection = ResearchFeatureEngine.Core.ReversalDirection;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Pipeline-level tests for the M9 ATRSmooth Regime Segment
    /// foundation:
    ///
    ///   1. explicit gating — the stage is active ONLY for the
    ///      ATRSmooth-based compositions; every other mode keeps its
    ///      exact previous behavior with the segment values at their
    ///      unavailable defaults;
    ///   2. the discriminating fixture on 10 000 real EURUSD M1
    ///      bars — price crossings of the ATRSmooth line without a
    ///      regime flip never create a segment transition, and every
    ///      transition is a canonical regime flip;
    ///   3. the published segment state equals an independent manual
    ///      walk over the canonical regime series;
    ///   4. re-tick idempotency through the full pipeline;
    ///   5. reset + replay determinism through the full pipeline;
    ///   6. existing-feature regression cross-checks (the reversal
    ///      stage's output remains exactly the canonical regime
    ///      strict-transition semantic, and the other stages remain
    ///      bit-identical between two identically-built engines).
    /// </summary>
    public sealed class AtrSmoothRegimeSegmentPipelineTests
    {
        private const string CsvName = "EURUSD_M1_10000.csv";

        private static string CsvPath()
        {
            string path = $@"TestData\{CsvName}";
            if (!System.IO.File.Exists(path))
            {
                path = $@"D:\Software\Distance\Tests\TestData\{CsvName}";
            }
            return path;
        }

        private static double[] SyntheticCloses(int n, int seed)
        {
            var rng = new Random(seed);
            double[] close = new double[n];
            double price = 100.0;
            for (int i = 0; i < n; i++)
            {
                price += (rng.NextDouble() - 0.5) * 2.0;
                close[i] = price;
            }
            return close;
        }

        private static FlatOhlcvMarketData MakeMarketData(double[] close, int seed)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            var rng = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + rng.NextDouble() * 0.5;
                low[i] = close[i] - rng.NextDouble() * 0.5;
                volume[i] = 100.0;
            }
            return new FlatOhlcvMarketData(open, high, low, close, volume);
        }

        private static Engine BuildAtrSmoothEngine(
            IMarketData marketData,
            ATRSmoothConfiguration? atrCfg = null,
            EngineOptions? options = null)
        {
            IReferenceSource source = new ATRSmoothReferenceSource(
                atrCfg ?? new ATRSmoothConfiguration(16, 5.1, 100));

            var configuration = new EngineConfiguration(
                marketData,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel> { new MeanModel() },
                options ?? new EngineOptions { StatisticsWindowSize = 252 });

            return new ResearchFeatureEngineBuilder(configuration).Build();
        }

        // -------------------------------------------------------------
        // 1. GATING
        // -------------------------------------------------------------

        [Fact]
        public void Gating_ATRSmooth2Mode_SegmentIsActiveAndMatchesCanonicalRegime()
        {
            double[] close = SyntheticCloses(200, 11);
            var engine = BuildAtrSmoothEngine(MakeMarketData(close, 12));

            bool sawUnavailable = false;
            bool sawEstablished = false;

            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
                var s = engine.Values.AtrSmoothRegimeSegment;

                if (engine.Values.Reference.Regime == 0.0)
                {
                    sawUnavailable = true;
                    Assert.Equal(AtrSmoothRegimeDirection.Unavailable, s.Regime);
                    Assert.Null(s.RegimeId);
                    Assert.Null(s.RegimeStartIndex);
                    Assert.Null(s.RegimeAge);
                    Assert.Equal(AtrSmoothRegimeTransition.None, s.RegimeTransition);
                }
                else
                {
                    sawEstablished = true;
                    Assert.NotNull(s.RegimeId);
                    Assert.NotNull(s.RegimeStartIndex);
                    Assert.NotNull(s.RegimeAge);
                    // The published segment regime equals the
                    // canonical regime sign, bar for bar.
                    Assert.Equal(
                        engine.Values.Reference.Regime > 0.0
                            ? AtrSmoothRegimeDirection.Bullish
                            : AtrSmoothRegimeDirection.Bearish,
                        s.Regime);
                }
            }

            Assert.True(sawUnavailable, "expected a warm-up phase on this fixture");
            Assert.True(sawEstablished, "expected an established regime on this fixture");
        }

        [Fact]
        public void Gating_HmaAtrSmoothCompositeMode_SegmentIsActive()
        {
            // The composite's published regime IS the canonical
            // ATRSmooth trailing-stop regime, so the segment stage is
            // registered there too.
            double[] close = SyntheticCloses(200, 21);
            var md = MakeMarketData(close, 22);

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            bool sawEstablished = false;
            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
                if (engine.Values.Reference.Regime != 0.0)
                {
                    sawEstablished = true;
                    var s = engine.Values.AtrSmoothRegimeSegment;
                    Assert.NotNull(s.RegimeId);
                    Assert.Equal(
                        engine.Values.Reference.Regime > 0.0
                            ? AtrSmoothRegimeDirection.Bullish
                            : AtrSmoothRegimeDirection.Bearish,
                        s.Regime);
                }
            }

            Assert.True(sawEstablished, "expected an established regime on this fixture");
        }

        [Fact]
        public void Gating_DarvasBoxMode_SegmentStaysAtUnavailableDefaults()
        {
            int n = 60;
            double[] close = SyntheticCloses(n, 31);
            var md = MakeMarketData(close, 32);

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.DarvasBox,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: new DarvasBoxConfiguration(5),
                hmaConfiguration: null);

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                var s = engine.Values.AtrSmoothRegimeSegment;

                Assert.Equal(AtrSmoothRegimeDirection.Unavailable, s.Regime);
                Assert.Null(s.RegimeId);
                Assert.Null(s.RegimeStartIndex);
                Assert.Null(s.RegimeAge);
                Assert.Equal(AtrSmoothRegimeTransition.None, s.RegimeTransition);
            }
        }

        [Fact]
        public void Gating_HmaOnlyMode_SegmentStaysAtUnavailableDefaults()
        {
            int n = 60;
            double[] close = SyntheticCloses(n, 41);
            var md = MakeMarketData(close, 42);

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.Hma,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                var s = engine.Values.AtrSmoothRegimeSegment;

                Assert.Equal(AtrSmoothRegimeDirection.Unavailable, s.Regime);
                Assert.Null(s.RegimeId);
                Assert.Null(s.RegimeStartIndex);
                Assert.Null(s.RegimeAge);
                Assert.Equal(AtrSmoothRegimeTransition.None, s.RegimeTransition);
            }
        }

        // -------------------------------------------------------------
        // 2 + 3. DISCRIMINATING FIXTURE on real data: price crossings
        // without flips never transition; the published state equals
        // an independent manual walk of the canonical regime series.
        // -------------------------------------------------------------

        [Fact]
        public void RealData_OnlyCanonicalFlipsCreateTransitions_SegmentMatchesManualWalk()
        {
            string csvPath = CsvPath();
            var md1 = new CsvMarketData(csvPath);
            var engine1 = BuildAtrSmoothEngine(md1);

            const int n = 10_000;

            // Independent records of the canonical series and the
            // price-vs-line relation.
            var regimeSeries = new double[n];
            var referenceSeries = new double[n];
            var closeSeries = new double[n];
            var published = new (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[n];

            int bars = 0;
            while (md1.MoveNext())
            {
                engine1.Update();
                var s = engine1.Values.AtrSmoothRegimeSegment;
                regimeSeries[bars] = engine1.Values.Reference.Regime;
                referenceSeries[bars] = engine1.Values.Reference.Price;
                closeSeries[bars] = md1.Close[bars];
                published[bars] = (s.Regime, s.RegimeId, s.RegimeStartIndex, s.RegimeAge, s.RegimeTransition);
                bars++;
            }

            Assert.Equal(n, bars);

            // Manual walk of the canonical regime series (§17-style
            // oracle, independent of the engine's internal state).
            int manualId = -1;
            bool manualEstablished = false;
            int manualStart = -1;
            var manualStates = new (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[n];

            for (int t = 0; t < n; t++)
            {
                double r = regimeSeries[t];
                if (r == 0.0)
                {
                    manualStates[t] = (
                        AtrSmoothRegimeDirection.Unavailable,
                        null, null, null,
                        AtrSmoothRegimeTransition.None);
                    continue;
                }

                var dir = r > 0.0
                    ? AtrSmoothRegimeDirection.Bullish
                    : AtrSmoothRegimeDirection.Bearish;

                if (!manualEstablished)
                {
                    manualEstablished = true;
                    manualId = 0;
                    manualStart = t;
                    manualStates[t] = (dir, 0, t, 0, AtrSmoothRegimeTransition.None);
                }
                else if (dir == manualStates[t - 1].Item1)
                {
                    manualStates[t] = (
                        dir, manualId, manualStart, t - manualStart,
                        AtrSmoothRegimeTransition.None);
                }
                else
                {
                    manualId++;
                    manualStart = t;
                    manualStates[t] = (
                        dir, manualId, t, 0,
                        dir == AtrSmoothRegimeDirection.Bullish
                            ? AtrSmoothRegimeTransition.Up
                            : AtrSmoothRegimeTransition.Down);
                }
            }

            int flips = 0;
            int crossesWithoutFlip = 0;
            int crossesInsideOneRegime = 0;
            int multipleFlipCount = 0;

            for (int t = 0; t < n; t++)
            {
                // The published state equals the manual walk exactly.
                Assert.Equal(manualStates[t].Item1, published[t].Item1);
                Assert.Equal(manualStates[t].Item2, published[t].Item2);
                Assert.Equal(manualStates[t].Item3, published[t].Item3);
                Assert.Equal(manualStates[t].Item4, published[t].Item4);
                Assert.Equal(manualStates[t].Item5, published[t].Item5);

                if (t == 0)
                    continue;

                bool regimeFlip = regimeSeries[t] != 0.0
                    && regimeSeries[t - 1] != 0.0
                    && regimeSeries[t] != regimeSeries[t - 1];

                bool priceCross = (closeSeries[t] >= referenceSeries[t])
                    != (closeSeries[t - 1] >= referenceSeries[t - 1]);

                if (regimeFlip)
                {
                    flips++;
                    if (published[t].Item5 != AtrSmoothRegimeTransition.None)
                        multipleFlipCount++;

                    // Every flip bar IS a transition bar with the
                    // correct direction.
                    Assert.Equal(
                        regimeSeries[t] > 0.0
                            ? AtrSmoothRegimeTransition.Up
                            : AtrSmoothRegimeTransition.Down,
                        published[t].Item5);
                }
                else if (priceCross)
                {
                    crossesWithoutFlip++;

                    // The adversarial core: a price cross of the
                    // ATRSmooth line without a regime flip must
                    // NEVER produce a segment transition.
                    Assert.Equal(
                        AtrSmoothRegimeTransition.None,
                        published[t].Item5);

                    // Crosses inside one established regime
                    // specifically counted.
                    if (regimeSeries[t] != 0.0
                        && regimeSeries[t - 1] == regimeSeries[t])
                    {
                        crossesInsideOneRegime++;
                    }
                }

                // Biconditional: transition != 0 iff a canonical flip.
                Assert.Equal(
                    regimeFlip,
                    published[t].Item5 != AtrSmoothRegimeTransition.None);
            }

            Assert.True(flips > 0, "expected at least one regime flip in 10k bars");
            Assert.True(flips >= 2, "expected multiple regime flips in 10k bars");
            Assert.Equal(flips, multipleFlipCount);
            Assert.True(crossesWithoutFlip > 0,
                "expected at least one price cross without a regime flip in 10k bars " +
                "— the exact scenario that must NOT produce a transition");
            Assert.True(crossesInsideOneRegime > 0,
                "expected at least one price cross inside a single ATRSmooth regime");
        }

        // -------------------------------------------------------------
        // 4. RE-TICK idempotency through the full pipeline.
        // -------------------------------------------------------------

        [Fact]
        public void PipelineReTick_SegmentStateEqualsSingleProcessing()
        {
            // Sinusoidal closes with a flippy ATRSmooth configuration
            // (short period, small multiplier): guarantees regime
            // establishment AND multiple flips so the re-tick paths
            // under test are genuinely exercised.
            const int n = 240;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.35) * 10.0;

            var md = MakeMarketData(close, 52);
            var engine = BuildAtrSmoothEngine(
                md, atrCfg: new ATRSmoothConfiguration(8, 3.1, 6));

            // Reference: single processing of every bar.
            var single = new (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[close.Length];
            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
                var s = engine.Values.AtrSmoothRegimeSegment;
                single[i] = (s.Regime, s.RegimeId, s.RegimeStartIndex, s.RegimeAge, s.RegimeTransition);
            }

            // Fresh engine: re-tick storm (bar, bar, bar, advance).
            var md2 = MakeMarketData(close, 52);
            var engine2 = BuildAtrSmoothEngine(
                md2, atrCfg: new ATRSmoothConfiguration(8, 3.1, 6));

            bool sawEstablished = false;
            bool sawFlip = false;
            AtrSmoothRegimeDirection lastDir = AtrSmoothRegimeDirection.Unavailable;

            for (int i = 0; i < close.Length; i++)
            {
                engine2.ProcessAt(i);
                engine2.ProcessAt(i);
                engine2.ProcessAt(i);

                var s = engine2.Values.AtrSmoothRegimeSegment;
                Assert.Equal(single[i].Item1, s.Regime);
                Assert.Equal(single[i].Item2, s.RegimeId);
                Assert.Equal(single[i].Item3, s.RegimeStartIndex);
                Assert.Equal(single[i].Item4, s.RegimeAge);
                Assert.Equal(single[i].Item5, s.RegimeTransition);

                if (s.Regime != AtrSmoothRegimeDirection.Unavailable)
                {
                    sawEstablished = true;
                    if (lastDir != AtrSmoothRegimeDirection.Unavailable
                        && s.Regime != lastDir)
                    {
                        sawFlip = true;
                    }
                    lastDir = s.Regime;
                }
            }

            // The fixture must genuinely exercise the established,
            // continuation, and flip paths (not pass vacuously).
            Assert.True(sawEstablished, "fixture must establish a regime");
            Assert.True(sawFlip, "fixture must contain at least one flip");
        }

        // -------------------------------------------------------------
        // 5. RESET + REPLAY through the full pipeline.
        // -------------------------------------------------------------

        [Fact]
        public void PipelineResetReplay_ReproducesEverySegmentOutput()
        {
            // Sinusoidal closes with a flippy ATRSmooth configuration:
            // guarantees establishment AND multiple flips so the
            // reset/replay paths are genuinely exercised.
            const int n = 240;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.35) * 10.0;

            var md = MakeMarketData(close, 62);
            var engine = BuildAtrSmoothEngine(
                md, atrCfg: new ATRSmoothConfiguration(8, 3.1, 6));

            var firstRun = new (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[close.Length];
            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
                var s = engine.Values.AtrSmoothRegimeSegment;
                firstRun[i] = (s.Regime, s.RegimeId, s.RegimeStartIndex, s.RegimeAge, s.RegimeTransition);
            }

            // Reset mid-lifecycle: the segment values must return to
            // the unavailable defaults with no state leakage.
            engine.Pipeline.Reset();
            var r = engine.Values.AtrSmoothRegimeSegment;
            Assert.Equal(AtrSmoothRegimeDirection.Unavailable, r.Regime);
            Assert.Null(r.RegimeId);
            Assert.Null(r.RegimeStartIndex);
            Assert.Null(r.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, r.RegimeTransition);

            // Replay from bar 0: bit-consistent reproduction.
            bool sawEstablished = false;
            bool sawFlip = false;
            AtrSmoothRegimeDirection lastDir = AtrSmoothRegimeDirection.Unavailable;

            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
                var s = engine.Values.AtrSmoothRegimeSegment;

                Assert.Equal(firstRun[i].Item1, s.Regime);
                Assert.Equal(firstRun[i].Item2, s.RegimeId);
                Assert.Equal(firstRun[i].Item3, s.RegimeStartIndex);
                Assert.Equal(firstRun[i].Item4, s.RegimeAge);
                Assert.Equal(firstRun[i].Item5, s.RegimeTransition);

                if (s.Regime != AtrSmoothRegimeDirection.Unavailable)
                {
                    sawEstablished = true;
                    if (lastDir != AtrSmoothRegimeDirection.Unavailable
                        && s.Regime != lastDir)
                    {
                        sawFlip = true;
                    }
                    lastDir = s.Regime;
                }
            }

            Assert.True(sawEstablished, "fixture must establish a regime");
            Assert.True(sawFlip, "fixture must contain at least one flip");
        }

        // -------------------------------------------------------------
        // 6. EXISTING-FEATURE REGRESSION cross-checks.
        // -------------------------------------------------------------

        [Fact]
        public void ExistingFeatures_UnchangedBySegmentStage()
        {
            // Two identically-built ATRSmooth engines on the same
            // data: every PRE-EXISTING published value must be
            // bit-identical (the segment stage must not perturb the
            // pipeline), and the reversal output must remain exactly
            // the canonical strict-transition semantic of the same
            // regime series.
            string csvPath = CsvPath();
            var md1 = new CsvMarketData(csvPath);
            var md2 = new CsvMarketData(csvPath);
            var engine1 = BuildAtrSmoothEngine(md1);
            var engine2 = BuildAtrSmoothEngine(md2);

            double prevRegime = 0.0;
            bool hasPrev = false;
            int bars = 0;

            while (md1.MoveNext() && md2.MoveNext())
            {
                engine1.Update();
                engine2.Update();
                bars++;

                var v1 = engine1.Values;
                var v2 = engine2.Values;

                // Bit-identical repeat build on every existing stage.
                Assert.Equal(v1.Reference.Price, v2.Reference.Price);
                Assert.Equal(v1.Reference.Regime, v2.Reference.Regime);
                Assert.Equal(v1.Distance.DirectionalExtension, v2.Distance.DirectionalExtension);
                Assert.Equal(v1.Distance.AbsoluteExtension, v2.Distance.AbsoluteExtension);
                Assert.Equal(v1.Scale.Scale, v2.Scale.Scale);
                Assert.Equal(v1.Normalization.NormalizedMeasurement, v2.Normalization.NormalizedMeasurement);
                Assert.Equal(v1.Statistics.Location.Mean, v2.Statistics.Location.Mean);

                // Existing stages remain valid (finite) throughout.
                Assert.False(double.IsNaN(v1.Reference.Price));
                Assert.False(double.IsNaN(v1.Distance.DirectionalExtension));
                Assert.False(double.IsNaN(v1.Scale.Scale));
                Assert.False(double.IsNaN(v1.Statistics.Location.Mean));

                // The Reversal stage output remains exactly the
                // canonical strict-transition semantic of the same
                // published regime series.
                double regime = v1.Reference.Regime;
                bool canonicalFlip = hasPrev && regime != prevRegime;

                if (canonicalFlip)
                {
                    Assert.True(v1.Reversal.IsReversalBar,
                        $"bar {bars - 1}: a canonical regime flip must remain a reversal bar");
                    Assert.Equal(
                        regime > prevRegime ? ReversalDirection.Up : ReversalDirection.Down,
                        v1.Reversal.Direction);
                }
                else
                {
                    Assert.False(v1.Reversal.IsReversalBar,
                        $"bar {bars - 1}: no canonical flip must remain no reversal bar");
                }

                // Cross-stage consistency: the segment transition and
                // the reversal event fire on exactly the same
                // established-regime flip bars.
                var s = v1.AtrSmoothRegimeSegment;
                if (hasPrev && prevRegime != 0.0 && regime != 0.0 && regime != prevRegime)
                {
                    Assert.True(s.RegimeTransition != AtrSmoothRegimeTransition.None);
                    Assert.True(v1.Reversal.IsReversalBar);
                }
                else if (hasPrev && prevRegime == 0.0)
                {
                    // First establishment: a reversal for the Reversal
                    // stage (strict state transition 0 -> +/-1), but
                    // NOT a segment transition (predecessor not an
                    // established directional state) — the documented
                    // distinction between the two stages.
                    Assert.Equal(AtrSmoothRegimeTransition.None, s.RegimeTransition);
                }

                prevRegime = regime;
                hasPrev = true;
            }

            Assert.Equal(10_000, bars);
        }
    }
}

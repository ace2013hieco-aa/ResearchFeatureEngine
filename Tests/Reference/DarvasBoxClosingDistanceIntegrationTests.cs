using System;

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

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Integration and causality tests for the Darvas Box
    /// closing-distance feature, driven through the PRODUCTION
    /// composition path (
    /// <see cref="ReferenceSourceFactory"/> → builder → pipeline →
    /// <see cref="Engines.DarvasBoxDistanceEngine"/>) with the
    /// canonical <see cref="DarvasBoxReferenceSource"/>.
    ///
    /// Verifies:
    ///   * warm-up (no box) publishes NaN/unavailable, never a
    ///     silent 0
    ///   * once a box exists, the published distances match an
    ///     independent in-test recomputation from the source's own
    ///     boundaries — bar for bar
    ///   * the absolute value is |signed|
    ///   * causality: changing future candles after index i does
    ///     not change the distance already produced for index i
    ///     (respecting the canonical source semantics)
    ///   * ATRSmooth engines are unaffected (feature absent, values
    ///     untouched)
    /// </summary>
    public sealed class DarvasBoxClosingDistanceIntegrationTests
    {
        // -------------------------------------------------------------
        // Fixture 1 from the Darvas golden suite (30 bars, boxp = 5):
        // boxes confirm at bars 9 ([110,96]), 16 ([113,94]), 23
        // ([120,102]); the golden regime series is
        // 0×13, +1@13, 0@14, -1@15, 0@16..29.
        // -------------------------------------------------------------

        private static readonly double[] Highs =
        {
            102.0, 101.0, 100.5, 101.0, 100.0,
            100.5, 110.0, 105.0, 104.0, 103.5,
            104.0, 103.0, 102.0, 113.0, 106.0,
            95.0, 101.0, 103.0, 104.0, 105.0,
            120.0, 115.0, 114.0, 113.5, 112.0,
            111.0, 110.5, 109.0, 108.0, 107.0
        };

        private static readonly double[] Lows =
        {
            99.0, 98.5, 98.0, 98.5, 99.0,
            96.0, 97.0, 97.5, 98.0, 98.5,
            99.0, 100.0, 101.0, 108.0, 100.0,
            94.0, 99.0, 100.0, 101.0, 102.0,
            103.0, 104.0, 105.0, 106.0, 107.0,
            108.0, 109.0, 106.5, 105.5, 104.5
        };

        private static readonly double[] Closes =
        {
            100.0, 99.5, 99.0, 99.5, 100.0,
            100.2, 108.0, 103.0, 102.5, 103.0,
            103.5, 102.5, 101.5, 112.0, 105.0,
            94.0, 100.0, 101.0, 102.0, 103.0,
            104.0, 105.0, 106.0, 107.0, 108.0,
            109.0, 110.0, 108.5, 107.5, 106.0
        };

        private static (ResearchFeatureEngine engine, DarvasBoxReferenceSource source)
            BuildDarvasEngine(
                double[] high, double[] low, double[] close)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.DarvasBox,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: new DarvasBoxConfiguration(5));

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel>
                {
                    new MeanModel()
                },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();
            return (engine, (DarvasBoxReferenceSource)source);
        }

        [Fact]
        public void Pipeline_WarmUpBars_PublishNaNNotZero()
        {
            var (engine, source) = BuildDarvasEngine(Highs, Lows, Closes);

            for (int i = 0; i < 9; i++)
            {
                engine.ProcessAt(i);

                Assert.False(engine.Values.DarvasBoxDistance.HasBox,
                    $"HasBox true at warm-up bar {i}");
                Assert.True(double.IsNaN(
                    engine.Values.DarvasBoxDistance.SignedClosingDistance),
                    $"Signed distance not NaN at warm-up bar {i}");
                Assert.True(double.IsNaN(
                    engine.Values.DarvasBoxDistance.AbsoluteClosingDistance),
                    $"Absolute distance not NaN at warm-up bar {i}");
            }

            Assert.False(source.HasBox);
        }

        [Fact]
        public void Pipeline_BarForBar_MatchesIndependentRecomputation()
        {
            var (engine, source) = BuildDarvasEngine(Highs, Lows, Closes);

            for (int i = 0; i < Closes.Length; i++)
            {
                engine.ProcessAt(i);

                var d = engine.Values.DarvasBoxDistance;
                double close = Closes[i];

                if (!source.HasBox)
                {
                    Assert.False(d.HasBox);
                    Assert.True(double.IsNaN(d.SignedClosingDistance));
                    continue;
                }

                double upper = source.Upper;
                double lower = source.Lower;

                // Independent in-test recomputation of the exact
                // formula (shares no code with the model under test).
                double expected;
                if (close > upper)
                    expected = close - upper;
                else if (close < lower)
                    expected = close - lower;
                else
                    expected = 0.0;

                Assert.True(d.HasBox, $"HasBox false at boxed bar {i}");
                Assert.Equal(expected, d.SignedClosingDistance, 12);
                Assert.Equal(Math.Abs(expected), d.AbsoluteClosingDistance, 12);
            }
        }

        [Fact]
        public void Pipeline_BoxedBars_ProduceKnownSignedValues()
        {
            var (engine, _) = BuildDarvasEngine(Highs, Lows, Closes);

            // Drive to bar 13 (bullish breakout bar: close 112,
            // box [110, 96] → +2).
            for (int i = 0; i <= 13; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.True(engine.Values.DarvasBoxDistance.HasBox);
            Assert.Equal(2.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);
            Assert.Equal(2.0,
                engine.Values.DarvasBoxDistance.AbsoluteClosingDistance, 12);

            // Bar 14: close 105 back inside → 0 (real zero, valid box).
            engine.ProcessAt(14);
            Assert.True(engine.Values.DarvasBoxDistance.HasBox);
            Assert.Equal(0.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);

            // Bar 15: close 94 < 96 → 94 - 96 = -2.
            engine.ProcessAt(15);
            Assert.Equal(-2.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);
            Assert.Equal(2.0,
                engine.Values.DarvasBoxDistance.AbsoluteClosingDistance, 12);
        }

        // -------------------------------------------------------------
        // Causality (spec §10)
        // -------------------------------------------------------------

        [Fact]
        public void Causality_ChangingFutureCandles_DoesNotChangePastDistances()
        {
            // Run the full 30-bar fixture and capture every
            // distance. Then re-run with the SAME history but a
            // MUTATED tail (bars after index 15), and verify the
            // distances for indices 0..15 are bit-identical.
            var (engineA, _) = BuildDarvasEngine(Highs, Lows, Closes);

            int n = Closes.Length;
            double[] signedA = new double[n];
            double[] absA = new double[n];
            for (int i = 0; i < n; i++)
            {
                engineA.ProcessAt(i);
                signedA[i] = engineA.Values.DarvasBoxDistance.SignedClosingDistance;
                absA[i] = engineA.Values.DarvasBoxDistance.AbsoluteClosingDistance;
            }

            // Mutate everything after bar 15 (the bearish-breakout
            // bar) — the mutations must not leak into bars 0..15.
            double[] highB = (double[])Highs.Clone();
            double[] lowB = (double[])Lows.Clone();
            double[] closeB = (double[])Closes.Clone();
            var rng = new Random(77);
            for (int i = 16; i < n; i++)
            {
                highB[i] = 90.0 + rng.NextDouble() * 40.0;
                lowB[i] = 85.0 + rng.NextDouble() * 5.0;
                closeB[i] = 86.0 + rng.NextDouble() * 40.0;
            }

            var (engineB, _) = BuildDarvasEngine(highB, lowB, closeB);

            for (int i = 0; i < n; i++)
            {
                engineB.ProcessAt(i);

                if (i <= 15)
                {
                    // The past is immutable: identical inputs up to
                    // bar 15 → identical outputs for bars 0..15.
                    Assert.Equal(signedA[i],
                        engineB.Values.DarvasBoxDistance.SignedClosingDistance);
                    Assert.Equal(absA[i],
                        engineB.Values.DarvasBoxDistance.AbsoluteClosingDistance);
                }
            }
        }

        [Fact]
        public void Causality_NoLookAheadAtBoundaryTransitionBars()
        {
            // The adversarial causality probe: distance at bar i is
            // computed only from the box state established by bars
            // <= i. Verify that the distance sequence equals a
            // source-driven recomputation where the source is fed
            // ONLY bars up to i (fresh source per prefix length —
            // expensive but the strongest no-look-ahead guarantee).
            // For the 30-bar fixture, spot-check the transition bars
            // (13 breakout, 14 return, 15 bearish) with prefix runs.
            for (int cut = 13; cut <= 15; cut++)
            {
                int len = cut + 1;
                double[] h = new double[len];
                double[] l = new double[len];
                double[] c = new double[len];
                Array.Copy(Highs, h, len);
                Array.Copy(Lows, l, len);
                Array.Copy(Closes, c, len);

                var (prefixEngine, prefixSource) = BuildDarvasEngine(h, l, c);
                for (int i = 0; i < len; i++)
                {
                    prefixEngine.ProcessAt(i);
                }

                // Full-run engine at the same bar:
                var (fullEngine, _) = BuildDarvasEngine(Highs, Lows, Closes);
                for (int i = 0; i <= cut; i++)
                {
                    fullEngine.ProcessAt(i);
                }

                double prefixSigned =
                    prefixEngine.Values.DarvasBoxDistance.SignedClosingDistance;
                double fullSigned =
                    fullEngine.Values.DarvasBoxDistance.SignedClosingDistance;

                Assert.Equal(fullSigned, prefixSigned, 12);
                Assert.Equal(
                    prefixSource.HasBox,
                    fullEngine.Values.DarvasBoxDistance.HasBox);
            }
        }

        // -------------------------------------------------------------
        // Box-transition discrimination (review §6): the replacement
        // fixture where a STALE-boundary read produces different
        // values than the correct current-bar read.
        //
        // Oracle-verified progression (boxp = 5):
        //   bars 0-7:  warm-up → NaN
        //   bar  8:    box A [110, 96]; close 101.5 inside → 0
        //   bar 12:    box B [111, 99] REPLACES A; close 103.2
        //              inside → 0 (boundary changed, close inside)
        //   bar 13:    close 112 → +1 from NEW Upper 111
        //              (stale A would read +2 from Upper 110)
        //   bar 14:    close 95 → -4 from NEW Lower 99
        //              (stale A would read -1 from Lower 96)
        //   bar 15:    close 96.5 → -2.5 from NEW Lower 99
        //              (stale A would read 0, inside)
        //   bar 16:    box C [112.5, 94] replaces B; close 97
        //              inside → 0 (second boundary change)
        // -------------------------------------------------------------

        private static readonly double[] THighs =
        {
            102.0, 101.5, 101.0, 101.5, 102.0,
            110.0, 105.0, 104.0, 103.5,
            111.0, 106.0, 105.0,
            104.0, 112.5, 100.0,
            99.0, 98.0
        };

        private static readonly double[] TLows =
        {
            99.0, 98.5, 98.0, 98.5, 99.0,
            96.0, 97.0, 98.0, 99.0,
            100.0, 101.0, 102.0,
            99.0, 111.2, 94.0,
            95.5, 96.0
        };

        private static readonly double[] TCloses =
        {
            100.0, 99.5, 99.0, 99.5, 100.0,
            108.0, 103.0, 102.0, 101.5,
            105.0, 104.0, 103.5,
            103.2, 112.0, 95.0,
            96.5, 97.0
        };

        [Fact]
        public void BoxTransition_ReplacementBar_UsesCurrentBarBoxNotStale()
        {
            var (engine, source) = BuildDarvasEngine(THighs, TLows, TCloses);

            double[] expected =
            {
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                double.NaN, double.NaN, double.NaN,
                0.0, 0.0, 0.0, 0.0,        // box A [110,96]: inside
                0.0,                        // bar 12: box B replaces A; inside
                1.0,                        // bar 13: +1 from NEW Upper 111
                -4.0,                       // bar 14: -4 from NEW Lower 99
                -2.5,                       // bar 15: -2.5 from NEW Lower 99
                0.0                         // bar 16: box C replaces B; inside
            };

            for (int i = 0; i < TCloses.Length; i++)
            {
                engine.ProcessAt(i);

                double actual =
                    engine.Values.DarvasBoxDistance.SignedClosingDistance;

                if (double.IsNaN(expected[i]))
                {
                    Assert.True(double.IsNaN(actual),
                        $"bar {i}: expected NaN, got {actual}");
                }
                else
                {
                    Assert.Equal(expected[i], actual, 12);
                    Assert.Equal(Math.Abs(expected[i]),
                        engine.Values.DarvasBoxDistance.AbsoluteClosingDistance,
                        12);
                }
            }

            // Final box is C.
            Assert.Equal(112.5, source.Upper, 10);
            Assert.Equal(94.0, source.Lower, 10);
        }

        [Fact]
        public void BoxTransition_BoundaryChangesWhileCloseStaysInside()
        {
            // Bars 12 and 16 change the box while the close is INSIDE
            // both old and new boxes: the value must be exactly 0 on
            // the transition bar (no sign flip from the jump itself).
            var (engine, source) = BuildDarvasEngine(THighs, TLows, TCloses);

            // Drive to bar 11 (end of box A tenure).
            for (int i = 0; i <= 11; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.Equal(110.0, source.Upper, 10);   // box A
            Assert.Equal(0.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);

            // Bar 12: box B [111, 99] confirms; close 103.2 inside.
            engine.ProcessAt(12);
            Assert.Equal(111.0, source.Upper, 10);    // box B now
            Assert.Equal(0.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);
            Assert.True(engine.Values.DarvasBoxDistance.HasBox);

            // Bar 16: box C [112.5, 94] confirms; close 97 inside.
            for (int i = 13; i <= 16; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.Equal(112.5, source.Upper, 10);   // box C now
            Assert.Equal(0.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);
        }

        // -------------------------------------------------------------
        // Reset / unavailability leak (review §5): a previously
        // VALID distance must not survive into an unavailable state.
        // -------------------------------------------------------------

        [Fact]
        public void Reset_AfterValidDistance_ClearsToUnavailable()
        {
            var (engine, source) = BuildDarvasEngine(Highs, Lows, Closes);

            // Drive into a valid boxed state (bar 13: close 112,
            // box [110, 96] → +2).
            for (int i = 0; i <= 13; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.True(engine.Values.DarvasBoxDistance.HasBox);
            Assert.Equal(2.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);

            // Full pipeline reset (fresh pass / reload semantics):
            // the previously valid value must NOT leak — back to
            // NaN / HasBox == false, the same state a fresh engine
            // exposes before its first Update.
            engine.Pipeline.Reset();

            Assert.False(engine.Values.DarvasBoxDistance.HasBox);
            Assert.True(double.IsNaN(
                engine.Values.DarvasBoxDistance.SignedClosingDistance),
                "valid distance leaked past reset");
            Assert.True(double.IsNaN(
                engine.Values.DarvasBoxDistance.AbsoluteClosingDistance),
                "valid absolute distance leaked past reset");

            // And after reset, replay from bar 0 reproduces the
            // original sequence exactly (no residue).
            for (int i = 0; i <= 13; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.True(engine.Values.DarvasBoxDistance.HasBox);
            Assert.Equal(2.0,
                engine.Values.DarvasBoxDistance.SignedClosingDistance, 12);
        }

        // -------------------------------------------------------------
        // ATRSmooth isolation (production integrity)
        // -------------------------------------------------------------

        [Fact]
        public void ATRSmoothPipeline_FeatureValuesRemainNaN_Defaults()
        {
            // With the ATRSmooth reference the stage is absent from
            // the pipeline; the (unused) runtime sub-object stays at
            // its defaults: NaN/unavailable.
            int n = 40;
            double[] close = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] open = new double[n];
            double[] volume = new double[n];
            var rng = new Random(9);
            for (int i = 0; i < n; i++)
            {
                close[i] = 100.0 + rng.NextDouble() * 10.0;
                high[i] = close[i] + 0.5;
                low[i] = close[i] - 0.5;
                open[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);

            IReferenceSource atrSource = ReferenceSourceFactory.Create(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20),
                darvasBoxConfiguration: null);

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                atrSource,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel>
                {
                    new MeanModel()
                },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                // The Darvas distance feature is unavailable — it
                // must NOT be silently 0, and the existing stages are
                // unaffected.
                Assert.True(double.IsNaN(
                    engine.Values.DarvasBoxDistance.SignedClosingDistance));
                Assert.False(double.IsNaN(
                    engine.Values.Reference.Price));
                Assert.False(double.IsNaN(
                    engine.Values.Distance.DirectionalExtension));
            }
        }
    }
}

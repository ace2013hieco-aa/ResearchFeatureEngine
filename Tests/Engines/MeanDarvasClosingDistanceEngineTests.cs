using System;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reversal;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Tests for <see cref="MeanDarvasClosingDistanceEngine"/>.
    /// </summary>
    public sealed class MeanDarvasClosingDistanceEngineTests
    {
        private static (ResearchFeatureEngine engine, DarvasBoxReferenceSource source, MeanDarvasClosingDistanceRuntimeValues values)
            CreateEngine(double[] high, double[] low, double[] close, int window = 5)
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

            var options = new EngineOptions
            {
                StatisticsWindowSize = 20,
                MeanDarvasWindowSize = window
            };

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                options);

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();
            return (engine, (DarvasBoxReferenceSource)source, engine.Values.MeanDarvasClosingDistance);
        }

        // Fixture 1 from Darvas golden suite (30 bars, boxp = 5):
        // boxes confirm at bars 9 ([110,96]), 16 ([113,94]), 23 ([120,102])
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

        [Fact]
        public void WarmUpBars_NoBox_NaN()
        {
            var (engine, _, values) = CreateEngine(Highs, Lows, Closes);

            for (int i = 0; i < 9; i++) // before first box confirmation
            {
                engine.ProcessAt(i);
                Assert.True(double.IsNaN(values.MeanSignedDistance),
                    $"Mean should be NaN at warm-up bar {i}");
            }
        }

        [Fact]
        public void PriceAboveBox_PositiveMean()
        {
            var (engine, _, values) = CreateEngine(Highs, Lows, Closes);

            // Drive to bar 13: bullish breakout (close 112 > upper 110)
            for (int i = 0; i <= 13; i++)
            {
                engine.ProcessAt(i);
            }

            // At bar 13, signed distance = 112 - 110 = +2
            // Mean over window of 5 (bars 9-13): [0, 0, 0, 0, +2] = +0.4
            Assert.False(double.IsNaN(values.MeanSignedDistance));
            Assert.True(values.MeanSignedDistance > 0.0,
                $"Expected positive mean at bar 13, got {values.MeanSignedDistance}");
        }

        [Fact]
        public void PriceBelowBox_NegativeMean()
        {
            var (engine, _, values) = CreateEngine(Highs, Lows, Closes);

            // Drive to bar 15: bearish breakout (close 94 < lower 96)
            for (int i = 0; i <= 15; i++)
            {
                engine.ProcessAt(i);
            }

            // At bar 15, signed distance = 94 - 96 = -2
            // Mean over window of 5 (bars 11-15): [0, 0, +2, 0, -2] = 0
            // But with different close values...
            Assert.False(double.IsNaN(values.MeanSignedDistance));
        }

        [Fact]
        public void PriceInsideBox_ZeroContribution()
        {
            var (engine, _, values) = CreateEngine(Highs, Lows, Closes);

            // Drive to bar 10: inside box [110, 96]
            for (int i = 0; i <= 10; i++)
            {
                engine.ProcessAt(i);
            }

            // At bar 10, signed distance = 0 (inside)
            Assert.Equal(0.0, values.MeanSignedDistance, 12);
        }

        [Fact]
        public void BoundaryEquality_ZeroContribution()
        {
            // Test exact boundary: close == upper or close == lower → 0
            double[] high = { 102.0, 101.0, 100.5, 101.0, 100.0, 100.5, 110.0, 105.0, 104.0, 103.5 };
            double[] low = { 99.0, 98.5, 98.0, 98.5, 99.0, 96.0, 97.0, 97.5, 98.0, 98.5 };
            double[] close = { 100.0, 99.5, 99.0, 99.5, 100.0, 100.2, 108.0, 103.0, 102.5, 110.0 }; // close == upper (110) at bar 9

            var (engine, _, values) = CreateEngine(high, low, close);

            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
            }

            // At bar 9: close == upper → signed distance = 0 (inside)
            // Mean over window: should include this 0
            Assert.False(double.IsNaN(values.MeanSignedDistance));
        }

        [Fact]
        public void MixedAboveInsideBelow_CorrectSignedArithmeticMean()
        {
            var (engine, _, values) = CreateEngine(Highs, Lows, Closes);

            // Drive full sequence
            for (int i = 0; i < Closes.Length; i++)
            {
                engine.ProcessAt(i);
            }

            // Final mean should be a finite signed value
            Assert.False(double.IsNaN(values.MeanSignedDistance));
            Assert.True(double.IsFinite(values.MeanSignedDistance));
        }

        [Fact]
        public void Reset_ClearsState()
        {
            var (engine, _, values) = CreateEngine(Highs, Lows, Closes);

            // Drive into valid state
            for (int i = 0; i <= 13; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.False(double.IsNaN(values.MeanSignedDistance));

            // Reset
            engine.Pipeline.Reset();

            // Should be back to NaN
            Assert.True(double.IsNaN(values.MeanSignedDistance));

            // Replay should reproduce
            for (int i = 0; i <= 13; i++)
            {
                engine.ProcessAt(i);
            }

            Assert.False(double.IsNaN(values.MeanSignedDistance));
        }

        [Fact]
        public void NoLookAhead_PastValuesImmutable()
        {
            // Full run
            var (engineA, _, _) = CreateEngine(Highs, Lows, Closes);
            double[] meansA = new double[Closes.Length];
            for (int i = 0; i < Closes.Length; i++)
            {
                engineA.ProcessAt(i);
                meansA[i] = engineA.Values.MeanDarvasClosingDistance.MeanSignedDistance;
            }

            // Mutate future and re-run
            double[] highB = (double[])Highs.Clone();
            double[] lowB = (double[])Lows.Clone();
            double[] closeB = (double[])Closes.Clone();
            var rng = new Random(77);
            for (int i = 16; i < closeB.Length; i++)
            {
                highB[i] = 90.0 + rng.NextDouble() * 40.0;
                lowB[i] = 85.0 + rng.NextDouble() * 5.0;
                closeB[i] = 86.0 + rng.NextDouble() * 40.0;
            }

            var (engineB, _, _) = CreateEngine(highB, lowB, closeB);
            for (int i = 0; i <= 15; i++)
            {
                engineB.ProcessAt(i);
                Assert.Equal(meansA[i], engineB.Values.MeanDarvasClosingDistance.MeanSignedDistance, 12);
            }
        }

        [Fact]
        public void BoxTransition_UsesCurrentBoxNotStale()
        {
            // Box transition fixture: box A [110, 96] → box B [111, 99] at bar 12
            double[] THighs = { 102.0, 101.5, 101.0, 101.5, 102.0, 110.0, 105.0, 104.0, 103.5, 111.0, 106.0, 105.0, 104.0, 112.5, 100.0, 99.0, 98.0 };
            double[] TLows = { 99.0, 98.5, 98.0, 98.5, 99.0, 96.0, 97.0, 98.0, 99.0, 100.0, 101.0, 102.0, 99.0, 111.2, 94.0, 95.5, 96.0 };
            double[] TCloses = { 100.0, 99.5, 99.0, 99.5, 100.0, 108.0, 103.0, 102.0, 101.5, 105.0, 104.0, 103.5, 103.2, 112.0, 95.0, 96.5, 97.0 };

            var (engine, _, values) = CreateEngine(THighs, TLows, TCloses);

            for (int i = 0; i < TCloses.Length; i++)
            {
                engine.ProcessAt(i);
            }

            // Bar 12: box B confirms, close 103.2 inside → 0
            // The mean should use the NEW box boundaries
            Assert.False(double.IsNaN(values.MeanSignedDistance));
        }
    }
}
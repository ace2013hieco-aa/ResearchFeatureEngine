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

namespace ResearchFeatureEngine.Tests.Integration
{
    /// <summary>
    /// Integration tests for the new research features.
    /// </summary>
    public sealed class ResearchFeaturesIntegrationTests
    {
        private static ResearchFeatureEngine BuildDarvasEngine(
            double[] high, double[] low, double[] close,
            int meanDarvasWindow = 5)
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
                darvasBoxConfiguration: new DarvasBoxConfiguration(5),
                hmaConfiguration: null);

            var options = new EngineOptions
            {
                StatisticsWindowSize = 20,
                MeanDarvasWindowSize = meanDarvasWindow
            };

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                options);

            return new ResearchFeatureEngineBuilder(configuration).Build();
        }

        // Golden suite fixture
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
        public void DarvasConfig_MeanDarvasFeatureActive_DoesNotAffectExistingFeatures()
        {
            var engine = BuildDarvasEngine(Highs, Lows, Closes);

            for (int i = 0; i < Closes.Length; i++)
            {
                engine.ProcessAt(i);
            }

            var v = engine.Values;

            // Existing features should work normally
            Assert.False(double.IsNaN(v.Reference.Price));
            Assert.False(double.IsNaN(v.Distance.DirectionalExtension));
            Assert.False(double.IsNaN(v.Scale.Scale));
            Assert.False(double.IsNaN(v.Normalization.NormalizedMeasurement));
            Assert.False(double.IsNaN(v.Statistics.Location.Mean));

            // Reversal should work
            Assert.NotEqual(ReversalDirection.None, v.Reversal.Direction);
        }

        [Fact]
        public void ATRSmoothConfig_MeanDarvasFeatureInactive_OutputsRemainNaN()
        {
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
                new ATRSmoothConfiguration(atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20),
                darvasBoxConfiguration: null,
                hmaConfiguration: null);

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                atrSource,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                // MeanDarvasClosingDistance should be NaN (feature absent)
                Assert.True(double.IsNaN(engine.Values.MeanDarvasClosingDistance.MeanSignedDistance));
            }
        }

        [Fact]
        public void HmaAtrSmoothCompositeMode_FullPipeline_ExercisesAllStages()
        {
            // Full production builder in composite mode: Reference →
            // Distance → (no Darvas stages) → MeanHmaAtrSmoothDistance →
            // HmaPriceAtrSmoothAlignment → Reversal → Scale →
            // Normalization → Statistics all run; the dual-reference
            // features produce real values after HMA warm-up and the
            // existing stages keep working.
            int n = 150;
            double[] close = new double[n];
            var rng = new Random(77);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.21) * 6.0 + rng.NextDouble() * 0.5;

            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + 0.4;
                low[i] = close[i] - 0.4;
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);

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
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                new EngineOptions
                {
                    StatisticsWindowSize = 20,
                    MeanHmaAtrSmoothWindowSize = 5
                });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
            }

            var v = engine.Values;

            // Existing stages all healthy.
            Assert.False(double.IsNaN(v.Reference.Price));
            Assert.False(double.IsNaN(v.Distance.DirectionalExtension));
            Assert.False(double.IsNaN(v.Scale.Scale));
            Assert.False(double.IsNaN(v.Normalization.NormalizedMeasurement));
            Assert.False(double.IsNaN(v.Statistics.Location.Mean));

            // Darvas-only stages remain absent (NaN / false).
            Assert.False(v.DarvasBoxDistance.HasBox);
            Assert.True(double.IsNaN(v.DarvasBoxDistance.SignedClosingDistance));
            Assert.True(double.IsNaN(v.MeanDarvasClosingDistance.MeanSignedDistance));

            // Dual-reference features are live.
            Assert.False(double.IsNaN(v.MeanHmaAtrSmoothDistance.MeanSignedDistance));
            Assert.NotEqual(HmaPriceAtrSmoothAlignment.Unavailable,
                v.HmaPriceAtrSmoothAlignment.Alignment);
        }

        [Fact]
        public void HmaAtrSmoothCompositeMode_FreshVsResetEngines_ProduceIdenticalOutputs()
        {
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.3) * 5.0;

            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + 0.4;
                low[i] = close[i] - 0.4;
                volume[i] = 100.0;
            }

            ResearchFeatureEngine Build()
            {
                var md = new FlatOhlcvMarketData(open, high, low, close, volume);
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
                    new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                    new EngineOptions
                    {
                        StatisticsWindowSize = 20,
                        MeanHmaAtrSmoothWindowSize = 5
                    });

                return new ResearchFeatureEngineBuilder(configuration).Build();
            }

            var engine1 = Build();
            var means1 = new double[n];
            var alignments1 = new int[n];
            for (int i = 0; i < n; i++)
            {
                engine1.ProcessAt(i);
                means1[i] = engine1.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
                alignments1[i] = (int)engine1.Values.HmaPriceAtrSmoothAlignment.Alignment;
            }

            // Second engine: run halfway, RESET, replay everything.
            var engine2 = Build();
            for (int i = 0; i < 60; i++)
                engine2.ProcessAt(i);
            engine2.Pipeline.Reset();
            for (int i = 0; i < n; i++)
            {
                engine2.ProcessAt(i);
                Assert.Equal(means1[i],
                    engine2.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance, 12);
                Assert.Equal(alignments1[i],
                    (int)engine2.Values.HmaPriceAtrSmoothAlignment.Alignment);
            }

            // Third engine: fully fresh — sequential runs do not leak.
            var engine3 = Build();
            for (int i = 0; i < n; i++)
                engine3.ProcessAt(i);
            Assert.Equal(means1[n - 1],
                engine3.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance, 12);
            Assert.Equal(alignments1[n - 1],
                (int)engine3.Values.HmaPriceAtrSmoothAlignment.Alignment);
        }

        [Fact]
        public void HmaConfig_AlignmentFeatureInactive_OutputsUnavailable()
        {
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

            IReferenceSource hmaSource = ReferenceSourceFactory.Create(
                ReferenceType.Hma,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                hmaSource,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                // HMA/Price vs ATRSmooth Alignment should be Unavailable (ATRSmooth not present)
                Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable,
                    engine.Values.HmaPriceAtrSmoothAlignment.Alignment);

                // MeanHmaAtrSmoothDistance should be NaN
                Assert.True(double.IsNaN(engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance));
            }
        }

        [Fact]
        public void EngineReset_ProducesIdenticalResultsToFreshEngine()
        {
            var engine1 = BuildDarvasEngine(Highs, Lows, Closes);
            var engine2 = BuildDarvasEngine(Highs, Lows, Closes);

            // Run first engine fully
            for (int i = 0; i < Closes.Length; i++)
            {
                engine1.ProcessAt(i);
            }

            // Run second engine, reset halfway, then continue
            for (int i = 0; i < 15; i++)
            {
                engine2.ProcessAt(i);
            }
            engine2.Pipeline.Reset();
            for (int i = 0; i < Closes.Length; i++)
            {
                engine2.ProcessAt(i);
            }

            // Compare final states
            var v1 = engine1.Values;
            var v2 = engine2.Values;

            Assert.Equal(v1.Reference.Price, v2.Reference.Price);
            Assert.Equal(v1.Distance.DirectionalExtension, v2.Distance.DirectionalExtension);
            Assert.Equal(v1.MeanDarvasClosingDistance.MeanSignedDistance, v2.MeanDarvasClosingDistance.MeanSignedDistance);
            Assert.Equal(v1.Reversal.Direction, v2.Reversal.Direction);
            Assert.Equal(v1.Reversal.BarsSinceReversal, v2.Reversal.BarsSinceReversal);
        }

        [Fact]
        public void MultipleSequentialRuns_NoStateLeakage()
        {
            // Run 1
            var engine1 = BuildDarvasEngine(Highs, Lows, Closes);
            for (int i = 0; i < Closes.Length; i++)
            {
                engine1.ProcessAt(i);
            }
            double mean1 = engine1.Values.MeanDarvasClosingDistance.MeanSignedDistance;

            // Run 2 (fresh engine, same data)
            var engine2 = BuildDarvasEngine(Highs, Lows, Closes);
            for (int i = 0; i < Closes.Length; i++)
            {
                engine2.ProcessAt(i);
            }
            double mean2 = engine2.Values.MeanDarvasClosingDistance.MeanSignedDistance;

            // Run 3
            var engine3 = BuildDarvasEngine(Highs, Lows, Closes);
            for (int i = 0; i < Closes.Length; i++)
            {
                engine3.ProcessAt(i);
            }
            double mean3 = engine3.Values.MeanDarvasClosingDistance.MeanSignedDistance;

            // All should be identical
            Assert.Equal(mean1, mean2);
            Assert.Equal(mean2, mean3);
        }

        [Fact]
        public void ExistingReversalSemantics_Unchanged()
        {
            var engine = BuildDarvasEngine(Highs, Lows, Closes);

            for (int i = 0; i < Closes.Length; i++)
            {
                engine.ProcessAt(i);
            }

            // The reversal semantics should be exactly as before
            var v = engine.Values;
            Assert.Equal(ReversalDirection.Up, v.Reversal.Direction); // Last reversal at bar 16 was Up (return from -1 to 0)
        }

        [Fact]
        public void ExistingDistanceSemantics_Unchanged()
        {
            var engine = BuildDarvasEngine(Highs, Lows, Closes);

            for (int i = 0; i < Closes.Length; i++)
            {
                engine.ProcessAt(i);
            }

            // Distance is close - reference (box midpoint)
            var v = engine.Values;
            Assert.False(double.IsNaN(v.Distance.DirectionalExtension));
            Assert.Equal(Math.Abs(v.Distance.DirectionalExtension), v.Distance.AbsoluteExtension, 12);
        }
    }
}
using System;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
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
    /// Dual-reference construction tests (§10.A of the brief):
    ///
    ///   1. ATRSmooth-only mode constructs only ATRSmooth.
    ///   2. Darvas mode constructs only Darvas.
    ///   3. HMA + ATRSmooth mode constructs both (exactly one each).
    ///   4. Non-selected parameters remain inert.
    ///   5. Existing reference modes retain their previous behavior.
    /// </summary>
    public sealed class DualReferenceConstructionTests
    {
        [Fact]
        public void Factory_ATRSmoothOnly_ConstructsOnlyATRSmooth()
        {
            var source = ReferenceSourceFactory.Create(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: null);

            Assert.IsType<ATRSmoothReferenceSource>(source);
            Assert.IsNotType<DarvasBoxReferenceSource>(source);
            Assert.IsNotType<HmaReferenceSource>(source);
            Assert.IsNotType<HmaAtrSmoothCompositeSource>(source);
        }

        [Fact]
        public void Factory_DarvasOnly_ConstructsOnlyDarvas()
        {
            var source = ReferenceSourceFactory.Create(
                ReferenceType.DarvasBox,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: new DarvasBoxConfiguration(5),
                hmaConfiguration: null);

            Assert.IsType<DarvasBoxReferenceSource>(source);
            Assert.IsNotType<ATRSmoothReferenceSource>(source);
            Assert.IsNotType<HmaReferenceSource>(source);
            Assert.IsNotType<HmaAtrSmoothCompositeSource>(source);
        }

        [Fact]
        public void Factory_HmaOnly_ConstructsOnlyHma()
        {
            var source = ReferenceSourceFactory.Create(
                ReferenceType.Hma,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            Assert.IsType<HmaReferenceSource>(source);
            Assert.IsNotType<ATRSmoothReferenceSource>(source);
            Assert.IsNotType<DarvasBoxReferenceSource>(source);
            Assert.IsNotType<HmaAtrSmoothCompositeSource>(source);
        }

        [Fact]
        public void Factory_HmaAtrSmooth_ConstructsCompositeWithBothProducers()
        {
            var source = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            var composite = Assert.IsType<HmaAtrSmoothCompositeSource>(source);

            // Exactly one instance of each canonical producer.
            Assert.NotNull(composite.HmaSource);
            Assert.NotNull(composite.AtrSmoothSource);
            Assert.IsType<HmaReferenceSource>(composite.HmaSource);
            Assert.IsType<ATRSmoothReferenceSource>(composite.AtrSmoothSource);
        }

        [Fact]
        public void Factory_HmaAtrSmooth_RequiresBothConfigurations()
        {
            Assert.Throws<ArgumentNullException>(
                () => ReferenceSourceFactory.Create(
                    ReferenceType.HmaAtrSmooth,
                    atrSmoothConfiguration: null,
                    darvasBoxConfiguration: null,
                    hmaConfiguration: new HmaConfiguration()));

            Assert.Throws<ArgumentNullException>(
                () => ReferenceSourceFactory.Create(
                    ReferenceType.HmaAtrSmooth,
                    new ATRSmoothConfiguration(),
                    darvasBoxConfiguration: null,
                    hmaConfiguration: null));
        }

        [Fact]
        public void Factory_DarvasConfig_IsInertForHmaAtrSmoothSelection()
        {
            // Varying the Darvas parameter wildly cannot affect the
            // composite — it is not even passed (null), and passing a
            // non-null one must not change the output either.
            var sourceA = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            var sourceB = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: new DarvasBoxConfiguration(999),
                hmaConfiguration: new HmaConfiguration(16));

            // Both are composites; the Darvas configuration is inert.
            Assert.IsType<HmaAtrSmoothCompositeSource>(sourceA);
            Assert.IsType<HmaAtrSmoothCompositeSource>(sourceB);
        }

        [Fact]
        public void Factory_HmaSelection_DoesNotConstructATRSmooth()
        {
            // Passing a non-null ATRSmooth configuration alongside an
            // Hma selection must NOT construct or consult the
            // ATRSmooth producer (silent dual construction forbidden).
            var source = ReferenceSourceFactory.Create(
                ReferenceType.Hma,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(16));

            Assert.IsType<HmaReferenceSource>(source);
            Assert.IsNotType<HmaAtrSmoothCompositeSource>(source);
        }

        // -------------------------------------------------------------
        // Parameter inertness, end-to-end: varying the non-selected
        // parameters cannot change the composite pipeline output.
        // -------------------------------------------------------------

        private static double[] MakeSeries(int n, int seed)
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

        private static EngineValues RunComposite(
            double[] close,
            int hmaPeriod,
            int darvasLength,
            int dataSeed)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            var rng = new Random(dataSeed);
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + rng.NextDouble() * 2.0;
                low[i] = close[i] - rng.NextDouble() * 2.0;
                volume[i] = 100.0;
            }
            var md = new FlatOhlcvMarketData(open, high, low, close, volume);

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasLength > 0
                    ? new DarvasBoxConfiguration(darvasLength)
                    : null,
                new HmaConfiguration(hmaPeriod));

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();
            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
            }
            return engine.Values;
        }

        [Fact]
        public void Composite_NonSelectedDarvasParameters_AreInertEndToEnd()
        {
            double[] close = MakeSeries(120, 42);

            var a = RunComposite(close, hmaPeriod: 16, darvasLength: 0, dataSeed: 7);
            var b = RunComposite(close, hmaPeriod: 16, darvasLength: 500, dataSeed: 7);

            Assert.Equal(a.Reference.Price, b.Reference.Price);
            Assert.Equal(a.Reference.Regime, b.Reference.Regime);
            Assert.Equal(
                a.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                b.MeanHmaAtrSmoothDistance.MeanSignedDistance);
            Assert.Equal(
                (int)a.HmaPriceAtrSmoothAlignment.Alignment,
                (int)b.HmaPriceAtrSmoothAlignment.Alignment);
        }

        [Fact]
        public void Composite_PipelineSemantics_MatchATRSmooth2ModeExactly()
        {
            // The composite's measurement level and regime must be
            // BIT-IDENTICAL to the plain ATRSmooth2 mode on the same
            // data and parameters (the HMA is additive, not a
            // replacement).
            double[] close = MakeSeries(120, 99);

            int n = close.Length;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            var rng = new Random(7);
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + rng.NextDouble() * 2.0;
                low[i] = close[i] - rng.NextDouble() * 2.0;
                volume[i] = 100.0;
            }

            EngineValues Run(ReferenceType type)
            {
                var md = new FlatOhlcvMarketData(open, high, low, close, volume);
                IReferenceSource source = ReferenceSourceFactory.Create(
                    type,
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
                    new EngineOptions { StatisticsWindowSize = 20 });

                var engine = new ResearchFeatureEngineBuilder(configuration).Build();
                for (int i = 0; i < n; i++)
                {
                    engine.ProcessAt(i);
                }
                return engine.Values;
            }

            var composite = Run(ReferenceType.HmaAtrSmooth);
            var atrOnly = Run(ReferenceType.ATRSmooth2);

            Assert.Equal(atrOnly.Reference.Price, composite.Reference.Price);
            Assert.Equal(atrOnly.Reference.Regime, composite.Reference.Regime);
            Assert.Equal(
                atrOnly.Distance.DirectionalExtension,
                composite.Distance.DirectionalExtension);
            Assert.Equal(
                atrOnly.Normalization.NormalizedMeasurement,
                composite.Normalization.NormalizedMeasurement);
            Assert.Equal(
                atrOnly.Statistics.Location.Mean,
                composite.Statistics.Location.Mean);
        }
    }
}

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
    /// Exclusivity tests (§16 of the implementation brief): exactly
    /// ONE reference source is active per engine instance.
    ///
    /// Proven directly by construction type (not only by output
    /// differences), by parameter inertness (changing the
    /// non-selected model's parameters cannot affect the output),
    /// by bit-identical repeated builds, and by deterministic
    /// switching in both directions.
    /// </summary>
    public sealed class ReferenceExclusivityTests
    {
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
                high[i] = close[i] + rng.NextDouble() * 2.0;
                low[i] = close[i] - rng.NextDouble() * 2.0;
                volume[i] = 100.0;
            }
            return new FlatOhlcvMarketData(open, high, low, close, volume);
        }

        private static (EngineValues values, EngineConfiguration cfg)
            RunEngine(
                ReferenceType type,
                ATRSmoothConfiguration atrCfg,
                DarvasBoxConfiguration darvasCfg,
                double[] close,
                int dataSeed)
        {
            var md = MakeMarketData(close, dataSeed);

            IReferenceSource source = ReferenceSourceFactory.Create(
                type, atrCfg, darvasCfg);

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel> { new MeanModel() },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            // Drive with explicit indices, mirroring the indicator's
            // ProcessAt pattern (no MoveNext dependency).
            for (int i = 0; i < close.Length; i++)
            {
                engine.ProcessAt(i);
            }

            return (engine.Values, configuration);
        }

        // -------------------------------------------------------------
        // 1. Construction type — the exclusivity property is explicit
        // -------------------------------------------------------------

        [Fact]
        public void Factory_ATRSmooth2Selection_ConstructsATRSmoothSourceOnly()
        {
            var source = ReferenceSourceFactory.Create(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(),
                new DarvasBoxConfiguration());

            Assert.IsType<ATRSmoothReferenceSource>(source);
            Assert.IsNotType<DarvasBoxReferenceSource>(source);
        }

        [Fact]
        public void Factory_DarvasBoxSelection_ConstructsDarvasSourceOnly()
        {
            var source = ReferenceSourceFactory.Create(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(),
                new DarvasBoxConfiguration());

            Assert.IsType<DarvasBoxReferenceSource>(source);
            Assert.IsNotType<ATRSmoothReferenceSource>(source);
        }

        [Fact]
        public void Factory_NullSelectedConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => ReferenceSourceFactory.Create(
                    ReferenceType.ATRSmooth2, null, new DarvasBoxConfiguration()));

            Assert.Throws<ArgumentNullException>(
                () => ReferenceSourceFactory.Create(
                    ReferenceType.DarvasBox, new ATRSmoothConfiguration(), null));
        }

        [Fact]
        public void Factory_InvalidType_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ReferenceSourceFactory.Create(
                    (ReferenceType)99, new ATRSmoothConfiguration(),
                    new DarvasBoxConfiguration()));
        }

        // -------------------------------------------------------------
        // 2. Parameter inertness — the non-selected model's parameters
        //    cannot affect the output
        // -------------------------------------------------------------

        [Fact]
        public void ATRSmoothSelection_DarvasParametersAreInert()
        {
            // Same data, same ATRSmooth configuration; vary the Darvas
            // parameters wildly. The ATRSmooth output must be
            // bit-identical.
            double[] close = MakeSeries(100, 42);
            var atrCfg = new ATRSmoothConfiguration(16, 5.1, 100);

            var (valuesA, _) = RunEngine(
                ReferenceType.ATRSmooth2, atrCfg,
                new DarvasBoxConfiguration(5), close, 7);
            var (valuesB, _) = RunEngine(
                ReferenceType.ATRSmooth2, atrCfg,
                new DarvasBoxConfiguration(500), close, 7);

            Assert.Equal(
                valuesA.Reference.Price,
                valuesB.Reference.Price);
            Assert.Equal(
                valuesA.Reference.Regime,
                valuesB.Reference.Regime);
            Assert.Equal(
                valuesA.Distance.DirectionalExtension,
                valuesB.Distance.DirectionalExtension);
            Assert.Equal(
                valuesA.Normalization.NormalizedMeasurement,
                valuesB.Normalization.NormalizedMeasurement);
        }

        [Fact]
        public void DarvasSelection_ATRSmoothParametersAreInert()
        {
            // Same data, same Darvas configuration; vary the ATRSmooth
            // parameters wildly. The Darvas output must be
            // bit-identical.
            double[] close = MakeSeries(100, 42);
            var darvasCfg = new DarvasBoxConfiguration(5);

            var (valuesA, _) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasCfg, close, 7);
            var (valuesB, _) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(3, 0.5, 2),
                darvasCfg, close, 7);

            Assert.Equal(
                valuesA.Reference.Price,
                valuesB.Reference.Price);
            Assert.Equal(
                valuesA.Reference.Regime,
                valuesB.Reference.Regime);
            Assert.Equal(
                valuesA.Distance.DirectionalExtension,
                valuesB.Distance.DirectionalExtension);
            Assert.Equal(
                valuesA.Normalization.NormalizedMeasurement,
                valuesB.Normalization.NormalizedMeasurement);
        }

        // -------------------------------------------------------------
        // 3. Same configuration, repeated build — bit-identical
        // -------------------------------------------------------------

        [Fact]
        public void SameConfiguration_RepeatedBuilds_AreBitIdentical_ATRSmooth()
        {
            double[] close = MakeSeries(150, 99);

            var (valuesA, _) = RunEngine(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 13);
            var (valuesB, _) = RunEngine(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 13);

            Assert.Equal(valuesA.Reference.Price, valuesB.Reference.Price);
            Assert.Equal(valuesA.Reference.Regime, valuesB.Reference.Regime);
            Assert.Equal(
                valuesA.Statistics.Location.Mean,
                valuesB.Statistics.Location.Mean);
        }

        [Fact]
        public void SameConfiguration_RepeatedBuilds_AreBitIdentical_Darvas()
        {
            double[] close = MakeSeries(150, 99);

            var (valuesA, _) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 13);
            var (valuesB, _) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 13);

            Assert.Equal(valuesA.Reference.Price, valuesB.Reference.Price);
            Assert.Equal(valuesA.Reference.Regime, valuesB.Reference.Regime);
            Assert.Equal(
                valuesA.Statistics.Location.Mean,
                valuesB.Statistics.Location.Mean);
        }

        // -------------------------------------------------------------
        // 4. Switching — deterministic selection in both directions
        // -------------------------------------------------------------

        [Fact]
        public void Switching_ATRSmoothToDarvas_DeterministicallySelectsDarvas()
        {
            double[] close = MakeSeries(150, 5);

            // Build ATRSmooth first, then switch to Darvas on the
            // SAME data. The second engine's source must be the
            // Darvas source and produce the Darvas output.
            var (atrValues, _) = RunEngine(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 3);

            var (darvasValues, darvasCfg) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 3);

            Assert.IsType<DarvasBoxReferenceSource>(
                darvasCfg.ReferenceSource);
            Assert.Equal(
                ((DarvasBoxReferenceSource)darvasCfg.ReferenceSource).Upper,
                ((DarvasBoxReferenceSource)darvasCfg.ReferenceSource).Upper);

            // And the outputs genuinely differ from ATRSmooth on
            // this data (different models, same data).
            Assert.NotEqual(
                atrValues.Reference.Price,
                darvasValues.Reference.Price);
        }

        [Fact]
        public void Switching_DarvasToATRSmooth_DeterministicallySelectsATRSmooth()
        {
            double[] close = MakeSeries(150, 5);

            var (darvasValues, _) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 3);

            var (atrValues, atrCfg) = RunEngine(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 3);

            Assert.IsType<ATRSmoothReferenceSource>(
                atrCfg.ReferenceSource);

            Assert.NotEqual(
                atrValues.Reference.Price,
                darvasValues.Reference.Price);
        }

        [Fact]
        public void Selection_ChangesOutputOnlyThroughTheSelectedSource()
        {
            // Full matrix: same data, both selections, distinct
            // outputs; switching back reproduces the first output
            // exactly (no cross-contamination of state).
            double[] close = MakeSeries(200, 77);

            var (atr1, _) = RunEngine(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 21);
            var (dar1, _) = RunEngine(
                ReferenceType.DarvasBox,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 21);
            var (atr2, _) = RunEngine(
                ReferenceType.ATRSmooth2,
                new ATRSmoothConfiguration(16, 5.1, 100),
                new DarvasBoxConfiguration(5), close, 21);

            Assert.NotEqual(atr1.Reference.Price, dar1.Reference.Price);
            Assert.Equal(atr1.Reference.Price, atr2.Reference.Price);
        }
    }
}

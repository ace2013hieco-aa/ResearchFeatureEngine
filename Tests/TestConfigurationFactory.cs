using System;
using System.Collections.Generic;
using System.Linq;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Tests
{
    internal static class TestConfigurationFactory
    {
        public static EngineConfiguration Create()
        {
            var marketData = new TestMarketData(100.0);
            var values = new EngineValues();

            IReferenceSource referenceSource = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14,
                    atrMultiplier: 5.1,
                    smoothLength: 20));

            IScaleModel scaleModel = new ATRScaleModel(period: 14);
            INormalizationModel normalizationModel = new ScaleNormalizationModel();

            var statisticModels = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new MinimumModel(),
                new MaximumModel()
            };

            return new EngineConfiguration(
                marketData,
                values,
                referenceSource,
                scaleModel,
                normalizationModel,
                statisticModels);
        }

        /// <summary>
        /// Creates a large dataset for long-run stability testing.
        /// </summary>
        /// <param name="barCount">Number of bars to generate.</param>
        public static EngineConfiguration CreateLargeDataset(int barCount)
        {
            var rng = new Random(42);

            double[] closePrices = new double[barCount];
            double price = 100.0;
            for (int i = 0; i < barCount; i++)
            {
                price += (rng.NextDouble() - 0.5) * 4.0;
                if (price < 1.0) price = 1.0;
                closePrices[i] = price;
            }

            return CreateFromClosePrices(
                closePrices,
                new ATRSmoothConfiguration(),
                new EngineOptions { StatisticsWindowSize = 252 },
                includeAllStatisticModels: true);
        }

        /// <summary>
        /// Creates a configuration backed by historical CSV data.
        /// </summary>
        /// <param name="csvPath">Path to the CSV file.</param>
        public static EngineConfiguration CreateFromHistoricalCsv(string csvPath)
        {
            var marketData = new CsvMarketData(csvPath);

            return new EngineConfiguration(
                marketData,
                new EngineValues(),
                new ATRSmoothReferenceSource(new ATRSmoothConfiguration()),
                new ATRScaleModel(period: 14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel>
                {
                    new MeanModel(),
                    new MedianModel(),
                    new VarianceModel(),
                    new StandardDeviationModel(),
                    new MedianAbsoluteDeviationModel(),
                    new MinimumModel(),
                    new MaximumModel()
                },
                options: new EngineOptions { StatisticsWindowSize = 252 });
        }

        /// <summary>
        /// Creates a configuration backed by historical CSV data
        /// using the specified platform adapter.
        /// </summary>
        /// <param name="csvPath">Path to the CSV file.</param>
        /// <param name="adapterType">The platform adapter to simulate.</param>
        public static EngineConfiguration CreateFromHistoricalCsv(
            string csvPath,
            AdapterType adapterType)
        {
            // Both adapters use identical CSV-backed market data,
            // ensuring cross-platform computation is identical.
            return CreateFromHistoricalCsv(csvPath);
        }

        /// <summary>
        /// Creates a known 20-bar dataset with monotonically increasing
        /// close prices (100, 102, ..., 138) and a reference produced by
        /// the ATR Smooth algorithm. The exact expected reference value
        /// for the final bar is computed in
        /// <see cref="ExpectedResultsFactory.CreateKnownDataset"/>.
        /// </summary>
        public static EngineConfiguration CreateKnownDataset()
        {
            const int barCount = 20;
            const double startPrice = 100.0;
            const double step = 2.0;

            double[] closePrices = Enumerable
                .Range(0, barCount)
                .Select(i => startPrice + i * step)
                .ToArray();

            // Synthetic OHLCV with tiny range so ATR is approximately
            // equal to the per-bar price increment.
            double[] open = closePrices;
            double[] high = closePrices.Select(c => c + 0.5).ToArray();
            double[] low = closePrices.Select(c => c - 0.5).ToArray();
            double[] volume = Enumerable.Range(0, barCount)
                .Select(_ => 100.0)
                .ToArray();

            var marketData = new FlatOhlcvMarketData(
                open, high, low, closePrices, volume);

            var options = new EngineOptions { StatisticsWindowSize = 20 };

            IReferenceSource referenceSource = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14,
                    atrMultiplier: 5.1,
                    smoothLength: 20));

            return new EngineConfiguration(
                marketData,
                new EngineValues(),
                referenceSource,
                new ATRScaleModel(period: 14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel>
                {
                    new MeanModel(),
                    new MedianModel(),
                    new VarianceModel(),
                    new StandardDeviationModel(),
                    new MedianAbsoluteDeviationModel(),
                    new MinimumModel(),
                    new MaximumModel()
                },
                options: options);
        }

        /// <summary>
        /// Creates a configuration from synthetic close prices using a
        /// flat OHLC band (high = close + eps, low = close - eps,
        /// volume = 1) and the supplied reference configuration.
        /// </summary>
        private static EngineConfiguration CreateFromClosePrices(
            double[] closePrices,
            ATRSmoothConfiguration referenceConfiguration,
            EngineOptions? options,
            bool includeAllStatisticModels)
        {
            int n = closePrices.Length;
            double eps = 1e-3;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];

            for (int i = 0; i < n; i++)
            {
                open[i] = closePrices[i];
                high[i] = closePrices[i] + eps;
                low[i] = closePrices[i] - eps;
                volume[i] = 1.0;
            }

            var marketData = new FlatOhlcvMarketData(
                open, high, low, closePrices, volume);

            var statisticModels = includeAllStatisticModels
                ? new List<IStatisticModel>
                {
                    new MeanModel(),
                    new MedianModel(),
                    new VarianceModel(),
                    new StandardDeviationModel(),
                    new MedianAbsoluteDeviationModel(),
                    new MinimumModel(),
                    new MaximumModel()
                }
                : new List<IStatisticModel>
                {
                    new MeanModel(),
                    new MedianModel(),
                    new MinimumModel(),
                    new MaximumModel()
                };

            return new EngineConfiguration(
                marketData,
                new EngineValues(),
                new ATRSmoothReferenceSource(referenceConfiguration),
                new ATRScaleModel(period: 14),
                new ScaleNormalizationModel(),
                statisticModels,
                options: options);
        }
    }
}

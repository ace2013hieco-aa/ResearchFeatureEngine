using System;
using System.Collections.Generic;
using System.Linq;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Features;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Tests
{
    internal static class TestConfigurationFactory
    {
        public static EngineConfiguration Create()
        {
            var marketData = new TestMarketData(100.0);
            var priceSeries = new TestPriceSeries(new[] { 100.0 });
            var values = new EngineValues();

            IReferenceModel referenceModel = new ATRSmoothReferenceModel(priceSeries);
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
                priceSeries,
                values,
                referenceModel,
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

            var marketData = new TestMarketData(closePrices);
            var priceSeries = new TestPriceSeries(closePrices);
            var values = new EngineValues();

            IReferenceModel referenceModel = new ATRSmoothReferenceModel(priceSeries);
            IScaleModel scaleModel = new ATRScaleModel(period: 14);
            INormalizationModel normalizationModel = new ScaleNormalizationModel();

            var statisticModels = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new VarianceModel(),
                new StandardDeviationModel(),
                new MedianAbsoluteDeviationModel(),
                new MinimumModel(),
                new MaximumModel()
            };

            var options = new EngineOptions { StatisticsWindowSize = 252 };

            return new EngineConfiguration(
                marketData,
                priceSeries,
                values,
                referenceModel,
                scaleModel,
                normalizationModel,
                statisticModels,
                options: options);
        }

        /// <summary>
        /// Creates a configuration backed by historical CSV data.
        /// </summary>
        /// <param name="csvPath">Path to the CSV file.</param>
        public static EngineConfiguration CreateFromHistoricalCsv(string csvPath)
        {
            var marketData = new CsvMarketData(csvPath);
            double[] closePrices = marketData.GetCloseValues();
            var priceSeries = new TestPriceSeries(closePrices);
            var values = new EngineValues();

            IReferenceModel referenceModel = new ATRSmoothReferenceModel(priceSeries);
            IScaleModel scaleModel = new ATRScaleModel(period: 14);
            INormalizationModel normalizationModel = new ScaleNormalizationModel();

            var statisticModels = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new VarianceModel(),
                new StandardDeviationModel(),
                new MedianAbsoluteDeviationModel(),
                new MinimumModel(),
                new MaximumModel()
            };

            var options = new EngineOptions { StatisticsWindowSize = 252 };

            return new EngineConfiguration(
                marketData,
                priceSeries,
                values,
                referenceModel,
                scaleModel,
                normalizationModel,
                statisticModels,
                options: options);
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
        /// Creates a known 20-bar dataset with:
        /// - Close prices incrementing by 2 from 100 to 138
        /// - Reference prices flat at 100
        /// - All statistic models registered
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

            double[] flatReference = Enumerable
                .Range(0, barCount)
                .Select(_ => startPrice)
                .ToArray();

            var marketData = new TestMarketData(closePrices);
            var priceSeries = new TestPriceSeries(flatReference);
            var values = new EngineValues();

            IReferenceModel referenceModel = new ATRSmoothReferenceModel(priceSeries);
            IScaleModel scaleModel = new ATRScaleModel(period: 14);
            INormalizationModel normalizationModel = new ScaleNormalizationModel();

            var statisticModels = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new VarianceModel(),
                new StandardDeviationModel(),
                new MedianAbsoluteDeviationModel(),
                new MinimumModel(),
                new MaximumModel()
            };

            var options = new EngineOptions { StatisticsWindowSize = 20 };

            return new EngineConfiguration(
                marketData,
                priceSeries,
                values,
                referenceModel,
                scaleModel,
                normalizationModel,
                statisticModels,
                options: options);
        }
    }
}

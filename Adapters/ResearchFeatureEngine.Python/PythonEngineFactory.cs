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
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Adapters
{
    /// <summary>
    /// Convenience factory that creates fully-composed
    /// <see cref="ResearchFeatureEngine"/> instances from Python-side
    /// parameters. This saves the Python layer from having to construct
    /// configuration objects, reference sources, scale/normalization
    /// models, statistic model lists, and the EngineConfiguration via
    /// reflection.
    /// </summary>
    public static class PythonEngineFactory
    {
        /// <summary>
        /// Builds an engine with the four standard statistic models
        /// (Mean, Median, Min, Max) and EngineOptions defaults.
        /// </summary>
        public static ResearchFeatureEngine Create(
            IMarketData marketData,
            ReferenceType referenceType,
            int atrPeriod = 16,
            double atrMultiplier = 5.1,
            int smoothLength = 100,
            int boxLength = 5,
            int hmaPeriod = 16,
            int scaleAtrPeriod = 14,
            int statisticsWindowSize = 20,
            int meanDarvasWindowSize = 20,
            int meanHmaAtrSmoothWindowSize = 20,
            StatisticsSource statisticsSource = StatisticsSource.Close,
            ReversalMode reversalMode = ReversalMode.TrailingStopPosition)
        {
            var values = new EngineValues();

            var referenceSource = ReferenceSourceFactory.Create(
                referenceType,
                new ATRSmoothConfiguration(atrPeriod, atrMultiplier, smoothLength),
                new DarvasBoxConfiguration(boxLength),
                new HmaConfiguration(hmaPeriod));

            var scaleModel = new ATRScaleModel(scaleAtrPeriod);
            var normalizationModel = new ScaleNormalizationModel();

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

            var options = new EngineOptions
            {
                StatisticsWindowSize = statisticsWindowSize,
                MeanDarvasWindowSize = meanDarvasWindowSize,
                MeanHmaAtrSmoothWindowSize = meanHmaAtrSmoothWindowSize,
                StatisticsSource = statisticsSource,
                ReversalMode = reversalMode
            };

            var config = new EngineConfiguration(
                marketData,
                values,
                referenceSource,
                scaleModel,
                normalizationModel,
                statisticModels,
                options);

            return new ResearchFeatureEngineBuilder(config).Build();
        }

        /// <summary>
        /// Convenience: build an engine and run it over all bars.
        /// Returns the populated EngineValues after processing
        /// every bar in the market data. Note: for a full per-bar
        /// result series, call Update() in a loop from the caller.
        /// </summary>
        public static EngineValues RunAll(
            IMarketData marketData,
            ReferenceType referenceType,
            int atrPeriod = 16,
            double atrMultiplier = 5.1,
            int smoothLength = 100,
            int boxLength = 5,
            int hmaPeriod = 16,
            int scaleAtrPeriod = 14,
            int statisticsWindowSize = 20,
            int meanDarvasWindowSize = 20,
            int meanHmaAtrSmoothWindowSize = 20,
            StatisticsSource statisticsSource = StatisticsSource.Close,
            ReversalMode reversalMode = ReversalMode.TrailingStopPosition)
        {
            var engine = Create(
                marketData,
                referenceType,
                atrPeriod,
                atrMultiplier,
                smoothLength,
                boxLength,
                hmaPeriod,
                scaleAtrPeriod,
                statisticsWindowSize,
                meanDarvasWindowSize,
                meanHmaAtrSmoothWindowSize,
                statisticsSource,
                reversalMode);

            for (int i = 0; i < marketData.Count; i++)
            {
                engine.Update();
            }

            return engine.Values;
        }
    }
}

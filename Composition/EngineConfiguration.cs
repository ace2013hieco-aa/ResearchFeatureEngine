using System;
using System.Collections.Generic;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Composition
{
    /// <summary>
    /// Describes how a <see cref="ResearchFeatureEngine"/> should be composed.
    /// </summary>
    public sealed class EngineConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="EngineConfiguration"/> class.
        /// </summary>
        public EngineConfiguration(
            IMarketData marketData,
            IPriceSeries priceSeries,
            EngineValues values,
            IReferenceModel referenceModel,
            IScaleModel scaleModel,
            INormalizationModel normalizationModel,
            IReadOnlyList<IStatisticModel> statisticModels,
            EngineOptions? options = null)
        {
            MarketData = marketData
                ?? throw new ArgumentNullException(nameof(marketData));

            PriceSeries = priceSeries
                ?? throw new ArgumentNullException(nameof(priceSeries));

            Values = values
                ?? throw new ArgumentNullException(nameof(values));

            ReferenceModel = referenceModel
                ?? throw new ArgumentNullException(nameof(referenceModel));

            ScaleModel = scaleModel
                ?? throw new ArgumentNullException(nameof(scaleModel));

            NormalizationModel = normalizationModel
                ?? throw new ArgumentNullException(nameof(normalizationModel));

            StatisticModels = statisticModels
                ?? throw new ArgumentNullException(nameof(statisticModels));

            Options = options
                ?? EngineOptions.Default;
        }

        /// <summary>
        /// Gets the market data adapter.
        /// </summary>
        public IMarketData MarketData { get; }

        /// <summary>
        /// Gets the historical price series adapter.
        /// </summary>
        public IPriceSeries PriceSeries { get; }

        /// <summary>
        /// Gets the shared runtime values.
        /// </summary>
        public EngineValues Values { get; }

        /// <summary>
        /// Gets the reference model.
        /// </summary>
        public IReferenceModel ReferenceModel { get; }

        /// <summary>
        /// Gets the scale model.
        /// </summary>
        public IScaleModel ScaleModel { get; }

        /// <summary>
        /// Gets the normalization model.
        /// </summary>
        public INormalizationModel NormalizationModel { get; }

        /// <summary>
        /// Gets the statistic models.
        /// </summary>
        public IReadOnlyList<IStatisticModel> StatisticModels { get; }

        /// <summary>
        /// Gets the engine options.
        /// </summary>
        public EngineOptions Options { get; }
    }
}

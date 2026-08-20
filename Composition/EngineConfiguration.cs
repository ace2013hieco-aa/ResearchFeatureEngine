using System;
using System.Collections.Generic;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
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
            EngineValues values,
            IReferenceSource referenceSource,
            IScaleModel scaleModel,
            INormalizationModel normalizationModel,
            IReadOnlyList<IStatisticModel> statisticModels,
            EngineOptions? options = null)
        {
            MarketData = marketData
                ?? throw new ArgumentNullException(nameof(marketData));

            Values = values
                ?? throw new ArgumentNullException(nameof(values));

            ReferenceSource = referenceSource
                ?? throw new ArgumentNullException(nameof(referenceSource));

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
        /// Gets the shared runtime values.
        /// </summary>
        public EngineValues Values { get; }

        /// <summary>
        /// Gets the reference source.
        /// </summary>
        public IReferenceSource ReferenceSource { get; }

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

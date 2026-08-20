using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Scale;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics;
using ResearchFeatureEngine.Statistics.Runtime;

namespace ResearchFeatureEngine.Composition
{
    public sealed class ResearchFeatureEngineBuilder
    {
        private readonly EngineConfiguration _configuration;

        public ResearchFeatureEngineBuilder(
            EngineConfiguration configuration)
        {
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
        }

        public ResearchFeatureEngine Build()
        {
            EngineContext context = BuildContext();

            EnginePipeline pipeline = BuildPipeline(context);

            pipeline.Initialize();

            return new ResearchFeatureEngine(
                context,
                pipeline);
        }

        private EngineContext BuildContext()
        {
            return new EngineContext(
                _configuration.MarketData,
                _configuration.Values);
        }

        private EnginePipeline BuildPipeline(
            EngineContext context)
        {
            var builder = new EnginePipelineBuilder();

            builder.Add(
                new ReferenceEngine(
                    context,
                    _configuration.ReferenceSource));

            builder.Add(
                new DistanceEngine(
                    context,
                    new ReferenceDistanceModel(
                        _configuration.MarketData)));

            builder.Add(
                new ScaleEngine(
                    context,
                    _configuration.ScaleModel));

            builder.Add(
                new NormalizationEngine(
                    context,
                    _configuration.NormalizationModel));

            builder.Add(
                new StatisticsEngine(
                    context,
                    new StatisticsWindow(
                        _configuration.Options.StatisticsWindowSize),
                    _configuration.StatisticModels));

            return builder.Build();
        }
    }
}

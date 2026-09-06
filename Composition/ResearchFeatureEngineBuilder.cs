using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reversal;
using ResearchFeatureEngine.Scale;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics;
using ResearchFeatureEngine.Statistics.Models;
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

            // Darvas Box closing-distance research feature: registered
            // ONLY when the composed reference source is the canonical
            // Darvas source (the feature reads the box boundaries
            // from that same instance). For any other reference type
            // the stage is absent — no behavior change for ATRSmooth
            // or future reference sources.
            if (_configuration.ReferenceSource is Reference.Sources.DarvasBoxReferenceSource darvasSource)
            {
                builder.Add(
                    new DarvasBoxDistanceEngine(
                        context,
                        new DarvasBoxClosingDistanceModel(),
                        darvasSource));
            }

            // Mean Darvas Closing Distance: only when the Darvas source is active
            if (_configuration.ReferenceSource is Reference.Sources.DarvasBoxReferenceSource darvasSource2)
            {
                builder.Add(
                    new MeanDarvasClosingDistanceEngine(
                        context,
                        new MeanDarvasClosingDistanceModel(_configuration.Options.MeanDarvasWindowSize),
                        darvasSource2));
            }

            builder.Add(
                new ReversalEngine(context, _configuration.Options.ReversalMode));

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
                    _configuration.StatisticModels,
                    _configuration.Options.StatisticsSource));

            return builder.Build();
        }
    }
}

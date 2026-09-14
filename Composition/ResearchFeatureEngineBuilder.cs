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

            // Dual-reference HMA + ATRSmooth research features: only
            // when the composite dual-reference source is active. The
            // composite (HmaAtrSmoothCompositeSource) has already
            // advanced BOTH canonical producers for this bar by the
            // time the Reference stage completes, so both stages below
            // read the current-bar canonical Runtime values from the
            // SAME source instances — no duplicate indicator
            // calculations, no re-instantiated copies. Registration is
            // type-gated exactly like the Darvas stages above: every
            // other reference mode (ATRSmooth2 / DarvasBox / Hma alone)
            // never constructs these stages and keeps its exact
            // previous behavior.
            if (_configuration.ReferenceSource is Reference.Sources.HmaAtrSmoothCompositeSource compositeSource)
            {
                builder.Add(
                    new MeanHmaAtrSmoothDistanceEngine(
                        context,
                        new MeanHmaAtrSmoothDistanceModel(_configuration.Options.MeanHmaAtrSmoothWindowSize),
                        compositeSource.HmaSource,
                        compositeSource.AtrSmoothSource));

                builder.Add(
                    new HmaPriceAtrSmoothAlignmentEngine(
                        context,
                        new HmaPriceAtrSmoothAlignmentModel(),
                        compositeSource.HmaSource,
                        compositeSource.AtrSmoothSource));
            }

            // M9 ATRSmooth regime segment foundation: registered
            // ONLY for the ATRSmooth-based compositions — the
            // ATRSmooth2 single reference or the HMA + ATRSmooth
            // composite (whose published regime IS the canonical
            // ATRSmooth trailing-stop regime, HmaAtrSmoothCompositeSource
            // .Regime => AtrSmoothSource.Regime). The stage consumes
            // the canonical published regime only; every other
            // reference mode (DarvasBox / Hma alone) never
            // constructs it and keeps its exact previous behavior,
            // with the segment values left at their unavailable
            // defaults.
            if (_configuration.ReferenceSource
                    is Reference.Sources.ATRSmoothReferenceSource
                || _configuration.ReferenceSource
                    is Reference.Sources.HmaAtrSmoothCompositeSource)
            {
                builder.Add(new AtrSmoothRegimeSegmentEngine(context));
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

            // M11.1 HMA/ATRSmooth geometry measurements: registered
            // ONLY inside the composite dual-reference type gate,
            // appended AFTER the Statistics stage because both
            // stages consume the current-bar canonical Scale value
            // published by the Scale stage (the placement rule from
            // MeasurementFamilyAddition.md: a stage is appended
            // after every producer it consumes). Both stages read
            // the SAME canonical producer instances the composite
            // drives — no second HMA source, no second ATRSmooth
            // source, no reconstruction from price data. Every
            // other reference mode (ATRSmooth2 / DarvasBox / Hma
            // alone) never constructs these stages and keeps its
            // exact previous behavior, with the published values
            // left at their unavailable (NaN) defaults.
            if (_configuration.ReferenceSource is Reference.Sources.HmaAtrSmoothCompositeSource compositeSource2)
            {
                builder.Add(
                    new HmaAtrSmoothSeparationEngine(
                        context,
                        new HmaAtrSmoothSeparationModel(),
                        compositeSource2.HmaSource,
                        compositeSource2.AtrSmoothSource));

                builder.Add(
                    new HmaAtrSmoothRelativeClosePositionEngine(
                        context,
                        new HmaAtrSmoothRelativeClosePositionModel(),
                        compositeSource2.HmaSource,
                        compositeSource2.AtrSmoothSource));
            }

            return builder.Build();
        }
    }
}

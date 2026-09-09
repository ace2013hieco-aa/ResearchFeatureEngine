using System;
using System.Collections.Generic;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Factory for engine compositions by export mode. Builds the
    /// EXACT configurations ratified in M10.0 §2 (defaults verified
    /// against source): ATRSmooth 16/5.1/100, Darvas length 5, HMA 16,
    /// scale 14, statistics window 252, source Close, reversal mode
    /// TrailingStopPosition, statistic models in the repo's canonical
    /// order (matching tools/CTraderHarness/Program.cs:60-70).
    /// The exporter NEVER constructs a second engine implementation —
    /// it composes the production builder with these parameters.
    /// </summary>
    public static class PresetFactory
    {
        public const int DefaultScalePeriod = 14;
        public const int DefaultStatisticsWindow = 252;

        public static EngineConfiguration Create(
            ExportMode mode,
            ArraysMarketData marketData)
        {
            IReferenceSource source = mode switch
            {
                ExportMode.ATRSmooth2 => new ATRSmoothReferenceSource(
                    new ATRSmoothConfiguration(
                        atrPeriod: ATRSmoothConfiguration.DefaultAtrPeriod,
                        atrMultiplier: ATRSmoothConfiguration.DefaultAtrMultiplier,
                        smoothLength: ATRSmoothConfiguration.DefaultSmoothLength)),

                ExportMode.DarvasBox => new DarvasBoxReferenceSource(
                    new DarvasBoxConfiguration(DarvasBoxConfiguration.DefaultLength)),

                ExportMode.Hma => new HmaReferenceSource(
                    new HmaConfiguration(HmaConfiguration.DefaultPeriod)),

                ExportMode.HmaAtrSmooth => new HmaAtrSmoothCompositeSource(
                    new HmaAtrSmoothConfiguration(
                        atrSmooth: new ATRSmoothConfiguration(
                            atrPeriod: ATRSmoothConfiguration.DefaultAtrPeriod,
                            atrMultiplier: ATRSmoothConfiguration.DefaultAtrMultiplier,
                            smoothLength: ATRSmoothConfiguration.DefaultSmoothLength),
                        hma: new HmaConfiguration(HmaConfiguration.DefaultPeriod))),

                _ => throw new ExportException($"Unknown export mode: {mode}")
            };

            var statisticModels = new List<IStatisticModel>
            {
                new MeanModel(),
                new StandardDeviationModel(),
                new MinimumModel(),
                new MaximumModel(),
                new MedianModel(),
                new VarianceModel(),
                new MedianAbsoluteDeviationModel(),
                new RangeModel(),
                new SkewnessModel(),
                new KurtosisModel()
            };

            return new EngineConfiguration(
                marketData: marketData,
                values: new EngineValues(),
                referenceSource: source,
                scaleModel: new ATRScaleModel(DefaultScalePeriod),
                normalizationModel: new ScaleNormalizationModel(),
                statisticModels: statisticModels,
                options: new EngineOptions
                {
                    StatisticsWindowSize = DefaultStatisticsWindow,
                    MeanDarvasWindowSize = 20,
                    MeanHmaAtrSmoothWindowSize = 20,
                    StatisticsSource = StatisticsSource.Close,
                    ReversalMode = ReversalMode.TrailingStopPosition
                });
        }
    }
}

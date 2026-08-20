using cAlgo.API;

using ResearchFeatureEngine.Adapters;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Indicators
{
    /// <summary>
    /// cTrader indicator that wires the production ResearchFeatureEngine
    /// pipeline to cTrader's bar stream.
    ///
    /// This indicator is a THIN ADAPTER. It does not contain any
    /// distance, scale, normalization, statistics, or reference math.
    /// All computation is delegated to the platform-independent
    /// pipeline via <see cref="ResearchFeatureEngineBuilder"/>.
    ///
    /// Pipeline:
    ///   cTrader Bars
    ///     → CTraderMarketData
    ///     → ATRSmoothReferenceSource
    ///     → ReferenceEngine → DistanceEngine → ScaleEngine →
    ///       NormalizationEngine → StatisticsEngine
    ///     → EngineValues
    ///     → Indicator outputs.
    /// </summary>
    [Indicator(
        IsOverlay = false,
        AccessRights = AccessRights.None,
        AutoRescale = true)]
    public class ResearchFeatureEngineIndicator : Indicator
    {
        // ---------------------------------------------------------
        // Parameters
        // ---------------------------------------------------------

        [Parameter("ATR Period", Group = "Reference", DefaultValue = 16)]
        public int AtrPeriod { get; set; }

        [Parameter("ATR Multiplier", Group = "Reference", DefaultValue = 5.1)]
        public double AtrMultiplier { get; set; }

        [Parameter("VWMA Smooth Length", Group = "Reference",
            DefaultValue = 100, MinValue = 1)]
        public int SmoothLength { get; set; }

        [Parameter("Scale ATR Period", Group = "Scale", DefaultValue = 14,
            MinValue = 1)]
        public int ScalePeriod { get; set; }

        [Parameter("Statistics Window", Group = "Statistics",
            DefaultValue = 252, MinValue = 2)]
        public int StatisticsWindowSize { get; set; }

        // ---------------------------------------------------------
        // Outputs
        // ---------------------------------------------------------

        [Output("Reference", LineColor = "DodgerBlue", Thickness = 1)]
        public IndicatorDataSeries ReferenceSeries { get; set; }

        [Output("Directional Distance", LineColor = "Orange", Thickness = 1)]
        public IndicatorDataSeries DirectionalSeries { get; set; }

        [Output("Absolute Distance", LineColor = "Magenta", Thickness = 1)]
        public IndicatorDataSeries AbsoluteSeries { get; set; }

        [Output("Scale (ATR)", LineColor = "Gray", Thickness = 1)]
        public IndicatorDataSeries ScaleSeries { get; set; }

        [Output("Normalized", LineColor = "Lime", Thickness = 2)]
        public IndicatorDataSeries NormalizedSeries { get; set; }

        [Output("Mean (Rolling)", LineColor = "Aqua", Thickness = 1)]
        public IndicatorDataSeries MeanSeries { get; set; }

        [Output("Std Dev (Rolling)", LineColor = "Yellow", Thickness = 1)]
        public IndicatorDataSeries StdDevSeries { get; set; }

        // ---------------------------------------------------------
        // Engine state
        // ---------------------------------------------------------

        private ResearchFeatureEngine _engine;
        private EngineValues _values;
        private IMarketData _marketData;
        private int _lastProcessedIndex;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        protected override void Initialize()
        {
            _marketData = new CTraderMarketData(Bars);

            var referenceSource = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: AtrPeriod,
                    atrMultiplier: AtrMultiplier,
                    smoothLength: SmoothLength));

            var statisticModels = new System.Collections.Generic.List<IStatisticModel>
            {
                new MeanModel(),
                new StandardDeviationModel(),
                new MinimumModel(),
                new MaximumModel(),
                new MedianModel(),
                new VarianceModel(),
                new MedianAbsoluteDeviationModel(),
                new Statistics.Models.RangeModel()
            };

            var options = new EngineOptions
            {
                StatisticsWindowSize = StatisticsWindowSize
            };

            var configuration = new EngineConfiguration(
                marketData: _marketData,
                values: new EngineValues(),
                referenceSource: referenceSource,
                scaleModel: new ATRScaleModel(ScalePeriod),
                normalizationModel: new ScaleNormalizationModel(),
                statisticModels: statisticModels,
                options: options);

            _engine = new ResearchFeatureEngineBuilder(configuration).Build();
            _values = _engine.Values;
            _lastProcessedIndex = -1;
        }

        public override void Calculate(int index)
        {
            // ---------------------------------------------------------
            // Detect a fresh pass (chart recalc / new history load /
            // indicator re-attach) and reset the pipeline.
            // ---------------------------------------------------------
            if (index == 0 && _lastProcessedIndex >= 0)
            {
                _engine.Pipeline.Reset();
                _lastProcessedIndex = -1;
            }

            // ---------------------------------------------------------
            // Drive the production pipeline for the current bar.
            // The engine's Update() advances the shared CurrentIndex
            // by 1, matching cTrader's left-to-right bar processing.
            // ---------------------------------------------------------
            _engine.Update();

            // ---------------------------------------------------------
            // Publish to cTrader outputs.
            // ---------------------------------------------------------
            ReferenceSeries[index]     = _values.Reference.Price;
            DirectionalSeries[index]   = _values.Distance.DirectionalExtension;
            AbsoluteSeries[index]      = _values.Distance.AbsoluteExtension;
            ScaleSeries[index]         = _values.Scale.Scale;
            NormalizedSeries[index]    = _values.Normalization.NormalizedMeasurement;
            MeanSeries[index]          = _values.Statistics.Location.Mean;
            StdDevSeries[index]        = _values.Statistics.Dispersion.StandardDeviation;

            _lastProcessedIndex = index;
        }
    }
}

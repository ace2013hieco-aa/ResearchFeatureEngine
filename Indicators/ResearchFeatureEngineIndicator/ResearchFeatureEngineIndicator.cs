using cAlgo.API;

// cTrader populates [Parameter] / [Output] properties and the
// Initialize()-set fields after construction, so the constructor
// legitimately leaves them null. Suppress the nullable-init warning
// for the indicator adapter only.
#pragma warning disable CS8618

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

        // 0 = CloseToReference, 1 = TrailingStopPosition
        [Parameter("Reversal Mode", Group = "Reversal",
            DefaultValue = ReversalMode.CloseToReference)]
        public ReversalMode ReversalMode { get; set; }

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

        [Output("Bars Since Reversal", LineColor = "White", Thickness = 1)]
        public IndicatorDataSeries BarsSinceReversalSeries { get; set; }

        [Output("Reversal Direction", LineColor = "Red", Thickness = 1)]
        public IndicatorDataSeries ReversalDirectionSeries { get; set; }

        // Step function: 1 on the reversal bar, 0 otherwise (gap
        // before the first reversal). Useful for alerting.
        [Output("Reversal Bar", LineColor = "White",
            PlotType = PlotType.Histogram, Thickness = 2)]
        public IndicatorDataSeries ReversalBarSeries { get; set; }

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
                StatisticsWindowSize = StatisticsWindowSize,
                ReversalMode = ReversalMode
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
            // ProcessAt(index) sets the shared CurrentIndex to the
            // cTrader-supplied bar index and runs the pipeline WITHOUT
            // auto-advancing. This is essential for live tick handling:
            // cTrader calls Calculate(currentIndex) once per tick on
            // the most-recent bar until that bar closes; auto-advance
            // would drift the engine past the bar and re-compute the
            // WRONG index's values for the same display bar.
            // ---------------------------------------------------------
            _engine.ProcessAt(index);

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

            // Reversal stage. BarsSinceReversal is null until the
            // first reversal; plot NaN (gap) in that case.
            // Direction is None until the first reversal; plot NaN,
            // then +1 (Up) / -1 (Down) for the most recent reversal.
            BarsSinceReversalSeries[index] =
                _values.Reversal.BarsSinceReversal.HasValue
                    ? _values.Reversal.BarsSinceReversal.Value
                    : double.NaN;

            ReversalDirectionSeries[index] =
                _values.Reversal.Direction == Core.ReversalDirection.None
                    ? double.NaN
                    : (int)_values.Reversal.Direction;

            // Reversal-bar signal (step function): 1 on the reversal
            // bar, 0 otherwise. NaN (gap) until the first reversal.
            ReversalBarSeries[index] =
                _values.Reversal.Direction == Core.ReversalDirection.None
                    ? double.NaN
                    : (_values.Reversal.IsReversalBar ? 1.0 : 0.0);

            _lastProcessedIndex = index;
        }
    }
}

#pragma warning restore CS8618

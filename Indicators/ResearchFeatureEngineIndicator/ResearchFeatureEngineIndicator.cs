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
    ///     → selected reference source (ATRSmooth2 or DarvasBox,
    ///       via ReferenceSourceFactory — exactly one is constructed)
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

        // The selected reference model. Exactly one source is
        // constructed in Initialize() (see ReferenceSourceFactory);
        // the non-selected group's parameters are inert — never
        // read, never validated. cTrader displays all parameter
        // groups simultaneously; that is expected.
        [Parameter("Reference Type", Group = "Reference",
            DefaultValue = ReferenceType.ATRSmooth2)]
        public ReferenceType ReferenceType { get; set; }

        // ATRSmooth2 parameters. Only read when the selected
        // reference type is ATRSmooth2.
        [Parameter("ATR Period", Group = "Reference: ATRSmooth2", DefaultValue = 16,
            MinValue = 1)]
        public int AtrPeriod { get; set; }

        [Parameter("ATR Multiplier", Group = "Reference: ATRSmooth2", DefaultValue = 5.1)]
        public double AtrMultiplier { get; set; }

        [Parameter("VWMA Smooth Length", Group = "Reference: ATRSmooth2",
            DefaultValue = 100, MinValue = 1)]
        public int SmoothLength { get; set; }

        // Darvas Box parameters. Only read when the selected
        // reference type is DarvasBox.
        [Parameter("Box Length", Group = "Reference: Darvas Box", DefaultValue = 5,
            MinValue = 3)]
        public int DarvasLength { get; set; }

        [Parameter("Mean Darvas Window", Group = "Research Features",
            DefaultValue = 20, MinValue = 1)]
        public int MeanDarvasWindowSize { get; set; }

        [Parameter("Scale ATR Period", Group = "Scale", DefaultValue = 14,
            MinValue = 1)]
        public int ScalePeriod { get; set; }

        [Parameter("Statistics Window", Group = "Statistics",
            DefaultValue = 252, MinValue = 2)]
        public int StatisticsWindowSize { get; set; }

        // 0 = CloseToReference, 1 = TrailingStopPosition
        [Parameter("Reversal Mode", Group = "Reversal",
            DefaultValue = ReversalMode.TrailingStopPosition)]
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

        [Output("Skewness (Rolling)", LineColor = "Pink", Thickness = 1)]
        public IndicatorDataSeries SkewnessSeries { get; set; }

        [Output("Kurtosis (Rolling)", LineColor = "Cyan", Thickness = 1)]
        public IndicatorDataSeries KurtosisSeries { get; set; }

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
        // Darvas Box specific outputs
        // ---------------------------------------------------------
        // These are ONLY populated when ReferenceType == DarvasBox.
        // For ATRSmooth2 they remain NaN (gap in Data Window).

        [Output("Darvas Upper", LineColor = "DodgerBlue", Thickness = 1)]
        public IndicatorDataSeries DarvasUpperSeries { get; set; }

        [Output("Darvas Lower", LineColor = "DodgerBlue", Thickness = 1)]
        public IndicatorDataSeries DarvasLowerSeries { get; set; }

        [Output("Darvas Midpoint", LineColor = "Orange", Thickness = 1)]
        public IndicatorDataSeries DarvasMidpointSeries { get; set; }

        [Output("Signed Closing Distance (Close - Box Outer)",
            LineColor = "Gray", Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries SignedClosingDistanceSeries { get; set; }

        [Output("Absolute Closing Distance (|Signed|)",
            LineColor = "Gray", Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries AbsoluteClosingDistanceSeries { get; set; }

        // ---------------------------------------------------------
        // Mean Darvas Closing Distance outputs
        // ---------------------------------------------------------
        // Only populated when ReferenceType == DarvasBox.

        [Output("Mean Darvas Signed Distance", LineColor = "Purple", Thickness = 1)]
        public IndicatorDataSeries MeanDarvasSignedDistanceSeries { get; set; }

        // ---------------------------------------------------------
        // Engine state
        // ---------------------------------------------------------

        private ResearchFeatureEngine _engine;
        private DarvasBoxReferenceSource? _darvasSource;
        private EngineValues _values;
        private IMarketData _marketData;
        private int _lastProcessedIndex;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        protected override void Initialize()
        {
            _marketData = new CTraderMarketData(Bars);

            // Exclusive construction: exactly ONE reference source is
            // built from the selected ReferenceType. The non-selected
            // model's parameters are never read or validated.
            var referenceSource = ReferenceSourceFactory.Create(
                ReferenceType,
                ReferenceType == Core.ReferenceType.ATRSmooth2
                    ? new ATRSmoothConfiguration(
                        atrPeriod: AtrPeriod,
                        atrMultiplier: AtrMultiplier,
                        smoothLength: SmoothLength)
                    : null,
                ReferenceType == Core.ReferenceType.DarvasBox
                    ? new DarvasBoxConfiguration(DarvasLength)
                    : null);

            // Typed view for Darvas read-only diagnostics (Upper/Lower/HasBox).
            // No math is performed here; only surfacing the production source's state.
            _darvasSource = referenceSource as DarvasBoxReferenceSource;

            var statisticModels = new System.Collections.Generic.List<IStatisticModel>
            {
                new MeanModel(),
                new StandardDeviationModel(),
                new MinimumModel(),
                new MaximumModel(),
                new MedianModel(),
                new VarianceModel(),
                new MedianAbsoluteDeviationModel(),
                new Statistics.Models.RangeModel(),
                new SkewnessModel(),
                new KurtosisModel()
            };

            var options = new EngineOptions
            {
                StatisticsWindowSize = StatisticsWindowSize,
                MeanDarvasWindowSize = MeanDarvasWindowSize,
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
            // Lifecycle contract enforcement.
            //
            // Supported transitions from the previously processed bar:
            //   * index == _lastProcessedIndex       live re-tick
            //   * index == _lastProcessedIndex + 1   next bar
            //   * first call after Initialize()      index 0
            //   * index == 0 after processing        fresh pass
            //     (chart recalc / history reload / re-attach)
            //
            // Any other transition is a discontinuity that cTrader can
            // produce on partial recalculation events (e.g. older-history
            // back-fill, which prepends bars and shifts indices). Feeding
            // such an index straight into the stateful engines would
            // silently corrupt the rolling statistics window, the
            // VWMA/ATR/trailing-stop state, and the reversal counters —
            // and every subsequent bar would inherit the contamination.
            //
            // On a discontinuity we therefore rebuild deterministic state
            // by replaying bars 0..index-1 through the freshly reset
            // pipeline, which reproduces bit-for-bit what a clean load
            // would have produced, and publish the recomputed values.
            // Cost is O(index) once per discontinuity event; the normal
            // streaming path (re-tick / next bar) stays O(1).
            // ---------------------------------------------------------
            bool freshPass = index == 0 && _lastProcessedIndex >= 0;

            bool contiguous = _lastProcessedIndex < 0
                ? index == 0
                : index == _lastProcessedIndex
                  || index == _lastProcessedIndex + 1;

            if (freshPass || !contiguous)
            {
                _engine.Pipeline.Reset();

                for (int i = 0; i < index; i++)
                {
                    _engine.ProcessAt(i);
                    Publish(i);
                }
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

            Publish(index);

            _lastProcessedIndex = index;
        }

        private void Publish(int index)
        {
            ReferenceSeries[index] = _values.Reference.Price;
            DirectionalSeries[index] = _values.Distance.DirectionalExtension;
            AbsoluteSeries[index] = _values.Distance.AbsoluteExtension;
            ScaleSeries[index] = _values.Scale.Scale;
            NormalizedSeries[index] = _values.Normalization.NormalizedMeasurement;
            MeanSeries[index] = _values.Statistics.Location.Mean;
            StdDevSeries[index] = _values.Statistics.Dispersion.StandardDeviation;
            SkewnessSeries[index] = _values.Statistics.Shape.Skewness;
            KurtosisSeries[index] = _values.Statistics.Shape.Kurtosis;

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

            // Darvas Box specific outputs (only when ReferenceType == DarvasBox).
            // NaN during warm-up (no box confirmed yet) or when using ATRSmooth2.
            if (_darvasSource != null && _darvasSource.HasBox)
            {
                DarvasUpperSeries[index] = _darvasSource.Upper;
                DarvasLowerSeries[index] = _darvasSource.Lower;
                DarvasMidpointSeries[index] = _values.Reference.Price;
                SignedClosingDistanceSeries[index] = _values.DarvasBoxDistance.SignedClosingDistance;
                AbsoluteClosingDistanceSeries[index] = _values.DarvasBoxDistance.AbsoluteClosingDistance;

                // Mean Darvas Closing Distance
                MeanDarvasSignedDistanceSeries[index] = _values.MeanDarvasClosingDistance.MeanSignedDistance;
            }
            else
            {
                DarvasUpperSeries[index] = double.NaN;
                DarvasLowerSeries[index] = double.NaN;
                DarvasMidpointSeries[index] = double.NaN;
                SignedClosingDistanceSeries[index] = double.NaN;
                AbsoluteClosingDistanceSeries[index] = double.NaN;
                MeanDarvasSignedDistanceSeries[index] = double.NaN;
            }
        }
    }
}

#pragma warning restore CS8618

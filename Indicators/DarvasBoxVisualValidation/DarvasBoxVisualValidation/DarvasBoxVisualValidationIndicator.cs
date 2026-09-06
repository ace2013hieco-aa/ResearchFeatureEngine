using System;
using cAlgo.API;

// cTrader populates [Parameter] / [Output] properties and the
// Initialize()-set fields after construction, so the constructor
// legitimately leaves them null. Suppress the nullable-init warning
// for the indicator adapter only.
#pragma warning disable CS8618

using ResearchFeatureEngine.Adapters;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reversal;

namespace ResearchFeatureEngine.Indicators
{
    /// <summary>
    /// TEMPORARY / DIAGNOSTIC — NOT A PRODUCTION FEATURE.
    ///
    /// Darvas Box visual-validation indicator: a thin visualization
    /// layer that drives the EXISTING production
    /// <see cref="DarvasBoxReferenceSource"/> (unmodified, built via
    /// the production <see cref="ReferenceSourceFactory"/> — the same
    /// exclusive-selection path as the production indicator) through
    /// the existing production engine stages (ReferenceEngine →
    /// DistanceEngine → ReversalEngine, the Darvas-relevant prefix of
    /// the production pipeline, composed with the production
    /// <see cref="EnginePipelineBuilder"/> /
    /// <see cref="ResearchFeatureEngine"/> wrappers), with the SAME
    /// bar-lifecycle semantics as the production
    /// ResearchFeatureEngineIndicator (live re-tick / next bar /
    /// fresh pass / discontinuity replay).
    ///
    /// NOTHING here re-implements Darvas math. Every value comes from
    /// the production source/engines under test.
    ///
    /// Visual contract being validated on real bars:
    ///   * box formation / persistence / replacement — translucent
    ///     rectangle per box cycle + Upper/Lower output lines, held
    ///     forward until a new confirmation replaces them
    ///   * midpoint = Reference.Price — orange dotted line per box
    ///     cycle + output line; jumps on replacement (intentional,
    ///     never smoothed)
    ///   * BREAKOUT (box-exit) markers — solid filled triangle ONLY
    ///     on the candle that closes outside the box: previous
    ///     regime 0 → current +1 = green up-triangle below the bar;
    ///     0 → -1 = red down-triangle above the bar. Returns INTO
    ///     the box (+1 → 0, -1 → 0) and direct flips (-1 ↔ +1) get
    ///     NO breakout triangle.
    ///   * OTHER reversal events — engine reversal bars that are NOT
    ///     box exits (returns into the box, direct flips) get a
    ///     small gray diamond at the midpoint level, visually
    ///     distinct from the colored breakout triangles. Engine
    ///     semantics are untouched: +1 → 0 is still a Down
    ///     reversal and -1 → 0 an Up reversal in the Data Window.
    ///   * optional regime state icons (OFF by default): lime
    ///     up-triangle above close-above-upper bars, red
    ///     down-triangle below close-below-lower bars;
    ///     exact-boundary closes (Close == Upper/Lower → INSIDE, 0)
    ///     show a gray circle at the midpoint level.
    ///
    /// Delete this indicator (and its project directory) once visual
    /// validation is complete. It must not be referenced by any
    /// production code.
    /// </summary>
    [Indicator(
        IsOverlay = true,
        AccessRights = AccessRights.None,
        AutoRescale = false)]
    public class DarvasBoxVisualValidationIndicator : Indicator
    {
        // ---------------------------------------------------------
        // Parameters (validation-scope only)
        // ---------------------------------------------------------

        // Structural validation (>= 3) is enforced by the production
        // DarvasBoxConfiguration constructor — not duplicated here.
        // cTrader's MinValue=3 mirrors it for the UI only.
        [Parameter("Box Length (boxp)", Group = "Darvas Validation",
            DefaultValue = 5, MinValue = 3)]
        public int BoxLength { get; set; }

        [Parameter("Marker Size (% of bar range)", Group = "Darvas Validation",
            DefaultValue = 50, MinValue = 3)]
        public int MarkerSizePct { get; set; }

        // Regime-state icons are OFF by default: on a real chart one
        // small icon per above/below bar clutters the candles. The
        // regime is always available per-bar in the Data Window.
        [Parameter("Show Regime Icons", Group = "Darvas Validation",
            DefaultValue = false)]
        public bool ShowRegimeIcons { get; set; }

        [Parameter("Show Exact-Boundary Circles (Close==Upper/Lower)", Group = "Darvas Validation",
            DefaultValue = true)]
        public bool ShowBoundaryIcons { get; set; }

        [Parameter("Show Box Fill", Group = "Darvas Validation",
            DefaultValue = true)]
        public bool ShowBoxFill { get; set; }

        // Breakout (box-exit) triangles + non-exit reversal diamonds.
        // ON by default — these are the events under validation.
        [Parameter("Show Breakout/Reversal Markers", Group = "Darvas Validation",
            DefaultValue = true)]
        public bool ShowBreakoutReversalMarkers { get; set; }

        // ---------------------------------------------------------
        // Outputs
        // ---------------------------------------------------------

        // Visible price-scale line outputs — these draw on the price
        // chart (overlay) and form the historical stepped box lines.
        // NaN before the first confirmed box (warm-up gap).
        [Output("Darvas Upper", LineColor = "DodgerBlue", Thickness = 1)]
        public IndicatorDataSeries UpperSeries { get; set; }

        [Output("Darvas Lower", LineColor = "DodgerBlue", Thickness = 1)]
        public IndicatorDataSeries LowerSeries { get; set; }

        // Midpoint only once a box exists, so the warm-up gap is
        // visually unambiguous (the raw Reference.Price — Close during
        // warm-up — is the hidden "Reference Price (raw)" output).
        [Output("Darvas Midpoint", LineColor = "Orange", Thickness = 1)]
        public IndicatorDataSeries MidpointSeries { get; set; }

        // Hidden value outputs (Data Window only — keeps the price
        // scale intact; a visible ±1 line would wreck an overlay).
        [Output("Reference Price (raw)", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries RawReferenceSeries { get; set; }

        [Output("Regime", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries RegimeSeries { get; set; }

        [Output("Signed Closing Distance (Close - Box Outer)", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries SignedClosingDistanceSeries { get; set; }

        [Output("Absolute Closing Distance (|Signed|)", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries AbsoluteClosingDistanceSeries { get; set; }

        [Output("Directional Extension (Close - Reference)", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries ExtensionSeries { get; set; }

        [Output("Reversal Bar (1/0)", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries ReversalBarSeries { get; set; }

        [Output("Reversal Direction (last, +1/-1)", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries ReversalDirectionSeries { get; set; }

        [Output("Bars Since Reversal", LineColor = "Gray",
            Thickness = 1, IsVisible = false)]
        public IndicatorDataSeries BarsSinceSeries { get; set; }

        // ---------------------------------------------------------
        // Engine state (production stages + production source)
        // ---------------------------------------------------------

        private EngineContext _context = null!;
        private ResearchFeatureEngine _engine = null!;
        private DarvasBoxReferenceSource _darvas = null!;
        private int _lastProcessedIndex = -1;

        // Box-cycle tracking for chart objects: the bar where the
        // CURRENT box confirmed (Upper or Lower changed) starts a
        // new object key; the rectangle/line extend to the latest
        // processed bar. Tracking BOTH boundaries (not Upper alone)
        // so a replacement whose Upper coincidentally equals the
        // old Upper still starts a new cycle.
        private double _lastUpper = double.NaN;
        private double _lastLower = double.NaN;
        private int _currentBoxStart = -1;

        // Highest bar index this instance has ever drawn objects for
        // (drives scoped cleanup of stale forming-bar markers).
        private int _maxDrawnIndex = -1;

        // Stable chart-object key prefixes (redraw with the same
        // name replaces the object — idempotent under re-tick).
        private const string BoxPrefix = "DVB_box_";
        private const string MidPrefix = "DVB_mid_";
        private const string RegimePrefix = "DVB_rg_";
        private const string RevPrefix = "DVB_rev_";
        private const string RetPrefix = "DVB_ret_";

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        protected override void Initialize()
        {
            // Clean slate on every (re-)initialization. cTrader calls
            // Initialize() not only on first attach but also when
            // the user edits parameters (e.g. resizing the markers):
            // without this, objects drawn by the previous parameter
            // run linger on the chart at their stale size/shape.
            RemoveAllOwnObjects();

            var marketData = new CTraderMarketData(Bars);

            // Production composition path: exclusive factory
            // selection, exactly as the production indicator does
            // for ReferenceType.DarvasBox. The production
            // DarvasBoxConfiguration enforces the structural
            // validation rules (length >= 3).
            IReferenceSource source = ReferenceSourceFactory.Create(
                Core.ReferenceType.DarvasBox,
                atrSmoothConfiguration: null,
                darvasBoxConfiguration: new DarvasBoxConfiguration(BoxLength));

            // Typed view for read-only diagnostics (Upper/Lower/
            // HasBox). No math is performed here.
            _darvas = (DarvasBoxReferenceSource)source;

            _context = new EngineContext(marketData, new EngineValues());

            // Production pipeline prefix relevant to Darvas state,
            // matching the production builder's Darvas stage order
            // exactly (Reference → Distance → DarvasBoxDistance →
            // Reversal → ...; the visual indicator stops after
            // Reversal because Scale/Normalization/Statistics are
            // not under visual validation).
            var pipelineBuilder = new EnginePipelineBuilder();
            pipelineBuilder.Add(new ReferenceEngine(_context, source));
            pipelineBuilder.Add(new DistanceEngine(
                _context, new ReferenceDistanceModel(marketData)));

            // Closing-distance research feature: the SAME production
            // stage the production builder registers for the Darvas
            // source — driven here for visual/Data-Window validation.
            pipelineBuilder.Add(new Engines.DarvasBoxDistanceEngine(
                _context,
                new DarvasBoxClosingDistanceModel(),
                _darvas));

            pipelineBuilder.Add(new ReversalEngine(_context));

            var pipeline = pipelineBuilder.Build();
            pipeline.Initialize();

            _engine = new ResearchFeatureEngine(_context, pipeline);

            _lastProcessedIndex = -1;
            _lastUpper = double.NaN;
            _lastLower = double.NaN;
            _currentBoxStart = -1;
            _maxDrawnIndex = -1;
        }

        public override void Calculate(int index)
        {
            // ---------------------------------------------------------
            // Lifecycle contract — mirrors the production
            // ResearchFeatureEngineIndicator's Calculate():
            //   * index == _lastProcessedIndex       live re-tick
            //   * index == _lastProcessedIndex + 1   next bar
            //   * first call after Initialize()      index 0
            //   * index == 0 after processing        fresh pass
            //     (chart recalc / history reload / re-attach)
            //
            // Any other transition is a discontinuity (e.g. older
            // history back-fill shifting indices). On fresh pass or
            // discontinuity: reset the production pipeline, drop all
            // of THIS indicator's chart objects (scoped to this
            // instance only), and replay bars 0..index-1 so both the
            // engine state and the drawings reproduce a clean load
            // exactly.
            // ---------------------------------------------------------
            bool freshPass = index == 0 && _lastProcessedIndex >= 0;

            bool contiguous = _lastProcessedIndex < 0
                ? index == 0
                : index == _lastProcessedIndex
                  || index == _lastProcessedIndex + 1;

            if (freshPass || !contiguous)
            {
                _engine.Pipeline.Reset();
                RemoveAllOwnObjects();

                _lastUpper = double.NaN;
                _lastLower = double.NaN;
                _currentBoxStart = -1;
                _maxDrawnIndex = -1;

                for (int i = 0; i < index; i++)
                {
                    _engine.ProcessAt(i);
                    Publish(i);
                }
            }

            // Drive the production pipeline for the current bar
            // (no auto-advance; re-ticks re-process the same bar
            // with the latest market data — same as production).
            _engine.ProcessAt(index);

            Publish(index);

            _lastProcessedIndex = index;
        }

        // ---------------------------------------------------------
        // Publishing: outputs + chart objects for one bar
        // ---------------------------------------------------------

        private void Publish(int index)
        {
            var v = _engine.Values;
            var rev = v.Reversal;

            // --- Output series (Data Window + visible lines) ---
            UpperSeries[index] = _darvas.HasBox ? _darvas.Upper : double.NaN;
            LowerSeries[index] = _darvas.HasBox ? _darvas.Lower : double.NaN;
            MidpointSeries[index] = _darvas.HasBox ? v.Reference.Price : double.NaN;

            RawReferenceSeries[index] = v.Reference.Price;
            RegimeSeries[index] = v.Reference.Regime;
            ExtensionSeries[index] = v.Distance.DirectionalExtension;

            // Closing-distance research feature (production stage
            // output — validated via the Data Window). NaN while no
            // box exists (warm-up), never a silent 0.
            SignedClosingDistanceSeries[index] =
                v.DarvasBoxDistance.SignedClosingDistance;
            AbsoluteClosingDistanceSeries[index] =
                v.DarvasBoxDistance.AbsoluteClosingDistance;

            ReversalBarSeries[index] = rev.IsReversalBar ? 1.0 : 0.0;
            ReversalDirectionSeries[index] = rev.Direction == Core.ReversalDirection.None
                ? double.NaN
                : (int)rev.Direction;
            BarsSinceSeries[index] = rev.BarsSinceReversal.HasValue
                ? rev.BarsSinceReversal.Value
                : double.NaN;

            // --- Chart objects ---

            DateTime tStart = Bars.OpenTimes[index];
            DateTime tEnd = tStart;

            if (_darvas.HasBox)
            {
                double upper = _darvas.Upper;
                double lower = _darvas.Lower;

                // New box cycle? (Either boundary changed => a
                // confirmation confirmed/replaced the box on this
                // bar; the FIRST box counts too, coming off NaN.)
                if (upper != _lastUpper || lower != _lastLower)
                {
                    _currentBoxStart = index;
                    _lastUpper = upper;
                    _lastLower = lower;
                }

                if (_currentBoxStart >= 0)
                {
                    DateTime tBoxStart = Bars.OpenTimes[_currentBoxStart];

                    // On the confirmation bar itself the box would be a
                    // zero-width sliver; extend the drawn span by one
                    // bar period so the new box is immediately
                    // visible, then keep extending on every bar.
                    long barTicks = index > _currentBoxStart
                        ? (Bars.OpenTimes[index] - Bars.OpenTimes[index - 1]).Ticks
                        : TimeSpan.FromMinutes(1).Ticks;
                    DateTime tBoxEnd = Bars.OpenTimes[index] + TimeSpan.FromTicks(barTicks);

                    // Box rectangle: translucent filled box spanning
                    // confirmation bar .. current bar. The key is per
                    // box cycle, so past boxes remain frozen on the
                    // chart when a new one confirms.
                    if (ShowBoxFill)
                    {
                        var rect = Chart.DrawRectangle(
                            BoxPrefix + _currentBoxStart,
                            tBoxStart, upper,
                            tBoxEnd, lower,
                            Color.FromArgb(25, Color.DodgerBlue));
                        rect.IsFilled = true;
                    }

                    // Midpoint: orange dotted line across the box.
                    Chart.DrawTrendLine(
                        MidPrefix + _currentBoxStart,
                        tBoxStart, v.Reference.Price,
                        tBoxEnd, v.Reference.Price,
                        Color.Orange, 1, LineStyle.Dots);
                }
            }

            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];
            double range = high - low;
            double offset = range * 0.25; // icon clearance

            // --- Forming-bar stale-marker hygiene ---
            // On re-ticks of the LIVE (still-forming) bar, a marker
            // drawn for a candidate state may no longer hold (the
            // bar's OHLC is still changing; e.g. a regime +1 flip
            // back to 0). Before (re)drawing this bar's markers,
            // remove every object this instance previously drew for
            // THIS bar so the final state is exactly what remains.
            // This avoids the classic live-feed repaint trap.
            RemoveOwnObjectsAt(index);

            // --- Regime-state icons (optional; OFF by default to keep
            // the chart clean — the regime per bar is in the Data
            // Window). INSIDE (0) bars get no icon. ---
            if (ShowRegimeIcons && _darvas.HasBox)
            {
                double regime = v.Reference.Regime;
                string key = RegimePrefix + index;

                if (regime > 0.0)
                {
                    // Close ABOVE upper: lime up-triangle above the bar.
                    Chart.DrawIcon(key, ChartIconType.UpTriangle,
                        index, high + offset, Color.Lime);
                }
                else if (regime < 0.0)
                {
                    // Close BELOW lower: red down-triangle below the bar.
                    Chart.DrawIcon(key, ChartIconType.DownTriangle,
                        index, low - offset, Color.Red);
                }
            }

            // --- Exact-boundary circles: INDEPENDENT of the regime
            // icons toggle. Close == Upper or == Lower is INSIDE (0)
            // by the ratified contract — the forensically interesting
            // boundary case; gray circle at the box midpoint. ---
            if (ShowBoundaryIcons && _darvas.HasBox
                && v.Reference.Regime == 0.0
                && (Bars.ClosePrices[index] == _darvas.Upper ||
                    Bars.ClosePrices[index] == _darvas.Lower))
            {
                Chart.DrawIcon(
                    RegimePrefix + index + "_b",
                    ChartIconType.Circle,
                    index,
                    v.Reference.Price,
                    Color.Gray);
            }

            // ---------------------------------------------------------
            // BREAKOUT (box-exit) markers + non-exit reversal markers.
            //
            // Breakout triangles are drawn ONLY on the candle that
            // closes OUTSIDE the current box from inside it:
            //   previous Regime == 0 AND current Regime == +1 → green
            //     solid filled up-triangle below the bar
            //   previous Regime == 0 AND current Regime == -1 → red
            //     solid filled down-triangle above the bar
            //
            // Returns INTO the box (+1 → 0, -1 → 0) and direct flips
            // (-1 → +1, +1 → -1) get NO breakout triangle. They are
            // still legitimate engine reversal events, so they get a
            // SMALL GRAY DIAMOND at the midpoint level — a visually
            // distinct, separate marker class. The engine's
            // semantics are NOT modified: the Data Window still
            // reports +1 → 0 as a Down reversal, -1 → 0 as Up.
            //
            // The previous bar's regime is read from this indicator's
            // own RegimeSeries (written by the previous Publish()
            // call — guaranteed by the streaming/replay lifecycle),
            // never from engine internals.
            // ---------------------------------------------------------
            if (ShowBreakoutReversalMarkers && rev.IsReversalBar
                && rev.Direction != Core.ReversalDirection.None)
            {
                double currentRegime = v.Reference.Regime;
                double previousRegime = index > 0
                    ? RegimeSeries[index - 1]
                    : 0.0;

                bool isBoxExitUp = previousRegime == 0.0 && currentRegime > 0.0;
                bool isBoxExitDown = previousRegime == 0.0 && currentRegime < 0.0;

                if ((isBoxExitUp || isBoxExitDown) && range > 0.0)
                {
                    // --- Breakout (box-exit) triangle ---
                    double height = (MarkerSizePct / 100.0) * range;
                    double gap = 0.3 * height;

                    // Base half-width in bars, scaled with the size
                    // setting (1.5 bars at the default 50%).
                    long barTicks = index > 0
                        ? (Bars.OpenTimes[index] - Bars.OpenTimes[index - 1]).Ticks
                        : TimeSpan.FromMinutes(1).Ticks;
                    long halfTicks = (long)(1.5 * (MarkerSizePct / 50.0) * barTicks);

                    DateTime tCenter = Bars.OpenTimes[index];
                    DateTime tLeft = tCenter - TimeSpan.FromTicks(halfTicks);
                    DateTime tRight = tCenter + TimeSpan.FromTicks(halfTicks);

                    if (isBoxExitUp)
                    {
                        // Solid green up-triangle below the bar.
                        var tri = Chart.DrawTriangle(
                            RevPrefix + index,
                            tLeft, low - gap - height,
                            tRight, low - gap - height,
                            tCenter, low - gap,
                            Color.Lime, 2);
                        tri.IsFilled = true;
                    }
                    else
                    {
                        // Solid red down-triangle above the bar.
                        var tri = Chart.DrawTriangle(
                            RevPrefix + index,
                            tLeft, high + gap + height,
                            tRight, high + gap + height,
                            tCenter, high + gap,
                            Color.Red, 2);
                        tri.IsFilled = true;
                    }
                }
                else if (rev.IsReversalBar && _darvas.HasBox)
                {
                    // --- Non-exit reversal event (return into box or
                    // direct flip): small gray diamond at the box
                    // midpoint level. Distinct marker class from the
                    // colored breakout triangles; the engine still
                    // counts this bar as a reversal (Data Window).
                    Chart.DrawIcon(
                        RetPrefix + index,
                        ChartIconType.Diamond,
                        index,
                        v.Reference.Price,
                        Color.Gray);
                }
            }

            if (index > _maxDrawnIndex)
            {
                _maxDrawnIndex = index;
            }
        }

        // ---------------------------------------------------------
        // Scoped chart-object cleanup
        // ---------------------------------------------------------

        /// <summary>
        /// Removes every chart object THIS indicator instance drew
        /// (identified by its "DVB_" key prefix). Never touches
        /// objects owned by other indicators or the user on the same
        /// chart — Chart.RemoveAllObjects() is chart-global and
        /// therefore forbidden here.
        /// </summary>
        private void RemoveAllOwnObjects()
        {
            // Snapshot the names first (never mutate the live
            // collection while iterating), then remove. Scoped to
            // this instance's "DVB_" prefix only —
            // Chart.RemoveAllObjects() is chart-global and would
            // destroy other indicators'/user's drawings.
            var names = new System.Collections.Generic.List<string>();
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name != null &&
                    obj.Name.StartsWith("DVB_", StringComparison.Ordinal))
                {
                    names.Add(obj.Name);
                }
            }

            foreach (string name in names)
            {
                Chart.RemoveObject(name);
            }
        }

        /// <summary>
        /// Removes the objects this instance previously drew for the
        /// given bar (regime icon + reversal marker). Called before
        /// re-drawing the same bar on re-ticks so the forming bar
        /// can never leave a stale marker behind when its final state
        /// differs from an earlier intra-bar state.
        /// </summary>
        private void RemoveOwnObjectsAt(int index)
        {
            if (index > _maxDrawnIndex)
            {
                return; // never drawn this bar before
            }

            RemoveIfExists(RegimePrefix + index);
            RemoveIfExists(RegimePrefix + index + "_b");
            RemoveIfExists(RevPrefix + index);
            RemoveIfExists(RetPrefix + index);
        }

        private void RemoveIfExists(string name)
        {
            if (Chart.FindObject(name) != null)
            {
                Chart.RemoveObject(name);
            }
        }
    }
}

#pragma warning restore CS8618

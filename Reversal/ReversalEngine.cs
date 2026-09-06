using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reversal.Runtime;
using ResearchFeatureEngine.Reversal.Validation;

namespace ResearchFeatureEngine.Reversal
{
    /// <summary>
    /// Executes the Reversal stage.
    ///
    /// Tracks the close-to-ATRSmooth relation as a deterministic
    /// state machine and publishes:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="ReversalRuntimeValues.BarsSinceReversal"/> — the
    /// bar distance from the most recent reversal (0 on the
    /// reversal bar), or <c>null</c> until the first reversal.
    /// </description></item>
    /// <item><description>
    /// <see cref="ReversalRuntimeValues.Direction"/> — the
    /// direction of the most recent reversal
    /// (<see cref="ReversalDirection.Up"/> / <see cref="ReversalDirection.Down"/>),
    /// persisting until the next reversal, or
    /// <see cref="ReversalDirection.None"/> until the first reversal.
    /// </description></item>
    /// <item><description>
    /// <see cref="ReversalRuntimeValues.IsReversalBar"/> —
    /// <c>true</c> only on the reversal bar itself (step function),
    /// for alerting/signal logic.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Reversal mode.</b> Two relations are supported, selected
    /// via <see cref="ReversalMode"/>:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="ReversalMode.TrailingStopPosition"/> (default): the
    /// relation is the source-declared signed regime state
    /// (<see cref="ReferenceRuntimeValues.Regime"/>). A reversal is a
    /// STRICT STATE TRANSITION of that value between consecutive bars:
    /// an increase (<c>0 → +1</c>, <c>-1 → 0</c>, <c>-1 → +1</c>) is
    /// Up, a decrease (<c>+1 → 0</c>, <c>0 → -1</c>, <c>+1 → -1</c>)
    /// is Down, and equal states are never a reversal. The engine does
    /// not interpret the regime's meaning — each reference source owns
    /// the semantics. For ATRSmooth2 the regime is the ATR trailing-stop
    /// position bias (+1 bullish / -1 bearish / 0 initial) and a
    /// reversal is a flip of that regime, NOT a candle crossing the ATR
    /// Smooth line. For Darvas Box the regime is the positional state
    /// (+1 above upper / 0 inside / -1 below lower) and a reversal
    /// includes breakout and return-to-box transitions.
    /// </description></item>
    /// <item><description>
    /// <see cref="ReversalMode.CloseToReference"/> (explicit opt-in):
    /// the relation is the sign of
    /// <see cref="Core.DistanceRuntimeValues.DirectionalExtension"/>
    /// (= close − reference). <c>close &gt;= reference</c> is ABOVE,
    /// <c>&lt;</c> is BELOW. A strict side change is a reversal.
    /// </description></item>
    /// </list>
    /// <para>
    /// Both modes use only the current and the previous bar (no
    /// lookahead), so historical replay and live/incremental
    /// processing produce identical results.
    /// </para>
    ///
    /// <para>
    /// <b>Live re-tick handling.</b> Live streaming consumers
    /// (cTrader indicator) re-call Update() for the same bar as ticks
    /// arrive. The engine snapshots the committed end-of-previous-bar
    /// state on the first call for a bar and restores it on re-ticks,
    /// so re-processing the same bar recomputes that bar's output
    /// from a clean baseline without double-incrementing the counter
    /// or manufacturing spurious reversals.
    /// </para>
    /// </summary>
    public sealed class ReversalEngine : EngineBase
    {
        private readonly ReversalMode _mode;

        // Committed state = "as of the end of the previous bar"
        // (or initial state before bar 0). Mutated only when a new
        // bar is observed.
        //   _previousRegime: source-declared signed state consumed in
        //     TrailingStopPosition mode (strict state transition).
        //   _previousAbove:  above/below relation consumed in
        //     CloseToReference mode (strict side change).
        private double _previousRegime;
        private bool _previousAbove;
        private bool _hasPrevious;
        private int? _barsSinceReversal;
        private ReversalDirection _direction = ReversalDirection.None;

        // Re-tick snapshot: captured on the first call for a bar and
        // restored on subsequent calls for the same bar.
        private int _snapshotIndex = int.MinValue;
        private double _snapshotPreviousRegime;
        private bool _snapshotPreviousAbove;
        private bool _snapshotHasPrevious;
        private int? _snapshotBarsSinceReversal;
        private ReversalDirection _snapshotDirection;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ReversalEngine"/> class using the
        /// <see cref="ReversalMode.TrailingStopPosition"/> mode —
        /// the canonical reversal semantic: a reversal is a flip of
        /// the ATR Smooth regime (trailing-stop position bias), not a
        /// candle crossing the ATR Smooth line.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        public ReversalEngine(EngineContext context)
            : this(context, ReversalMode.TrailingStopPosition)
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ReversalEngine"/> class with the specified
        /// reversal detection mode.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="mode">Reversal detection mode.</param>
        public ReversalEngine(EngineContext context, ReversalMode mode)
            : base(nameof(ReversalEngine), context)
        {
            _mode = mode;
        }

        /// <summary>
        /// Gets the configured reversal detection mode.
        /// </summary>
        public ReversalMode Mode => _mode;

        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();
            _previousRegime = 0.0;
            _previousAbove = false;
            _hasPrevious = false;
            _barsSinceReversal = null;
            _direction = ReversalDirection.None;
            _snapshotIndex = int.MinValue;

            var r = Context.Values.Reversal;
            r.BarsSinceReversal = null;
            r.Direction = ReversalDirection.None;
            r.IsReversalBar = false;
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            int index = Context.CurrentIndex;

            //--------------------------------------------------
            // Re-tick handling
            //--------------------------------------------------
            if (_snapshotIndex == index)
            {
                // Re-tick on the same bar: restore the committed
                // end-of-previous-bar state captured on the first
                // call for this bar, then recompute this bar's
                // output below from that clean baseline.
                _previousRegime = _snapshotPreviousRegime;
                _previousAbove = _snapshotPreviousAbove;
                _hasPrevious = _snapshotHasPrevious;
                _barsSinceReversal = _snapshotBarsSinceReversal;
                _direction = _snapshotDirection;
            }
            else
            {
                // First call for this bar: snapshot the committed
                // state (end of the previous bar / initial state).
                _snapshotPreviousRegime = _previousRegime;
                _snapshotPreviousAbove = _previousAbove;
                _snapshotHasPrevious = _hasPrevious;
                _snapshotBarsSinceReversal = _barsSinceReversal;
                _snapshotDirection = _direction;
                _snapshotIndex = index;
            }

            bool isReversalBar = false;

            if (_mode == ReversalMode.TrailingStopPosition)
            {
                // Generic regime mode: reversal is a STRICT STATE
                // TRANSITION of the source-declared signed regime
                // value between consecutive bars. The engine never
                // interprets the value's meaning (a Darvas 0 means
                // "inside box", NOT bearish); each reference source
                // owns the semantics.
                double regime = Context.Values.Reference.Regime;
                ReversalValidator.Validate(regime);

                if (_hasPrevious && regime != _previousRegime)
                {
                    // Strict state transition => reversal on this bar.
                    _barsSinceReversal = 0;
                    _direction = regime > _previousRegime
                        ? ReversalDirection.Up
                        : ReversalDirection.Down;
                    isReversalBar = true;
                }
                else if (_barsSinceReversal.HasValue)
                {
                    // Same regime, but a reversal has happened before
                    // => one more completed bar since the reversal.
                    _barsSinceReversal = _barsSinceReversal.Value + 1;
                }
                // else: no previous bar, or same regime with no prior
                // reversal => leave null/None (no reversal yet).

                _previousRegime = regime;
            }
            else
            {
                // CloseToReference mode: the relation is the sign of
                // the directional extension. Both branches read only
                // the current bar's published values (no lookahead).
                double extension = Context.Values.Distance.DirectionalExtension;
                ReversalValidator.Validate(extension);
                // >= reference is ABOVE, < is BELOW (equality rule).
                bool currentAbove = extension >= 0.0;

                if (_hasPrevious && currentAbove != _previousAbove)
                {
                    // Strict side change => reversal on this bar.
                    _barsSinceReversal = 0;
                    _direction = currentAbove
                        ? ReversalDirection.Up
                        : ReversalDirection.Down;
                    isReversalBar = true;
                }
                else if (_barsSinceReversal.HasValue)
                {
                    // Same side, but a reversal has happened before =>
                    // one more completed bar since the reversal.
                    _barsSinceReversal = _barsSinceReversal.Value + 1;
                }
                // else: no previous bar, or same side with no prior
                // reversal => leave null/None (no reversal yet).

                _previousAbove = currentAbove;
            }

            _hasPrevious = true;

            var r2 = Context.Values.Reversal;
            r2.BarsSinceReversal = _barsSinceReversal;
            r2.Direction = _direction;
            r2.IsReversalBar = isReversalBar;
        }
    }
}

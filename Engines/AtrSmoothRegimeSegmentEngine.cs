using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// ATRSmooth Regime Segment stage (M9).
    ///
    /// Layers bounded temporal-segment metadata on top of the
    /// canonical ATRSmooth regime — the source-declared signed
    /// trailing-stop position bias published by the Reference stage
    /// as <see cref="Core.EngineValues.Reference.Regime"/>
    /// (+1 bullish / -1 bearish / 0 initial-uncommitted). The stage
    /// is a pure CONSUMER of that canonical value: it performs no
    /// indicator mathematics, recomputes no ATRSmooth series, and
    /// defines no second reversal semantic. A regime flip here is
    /// exactly the canonical strict change of the published regime
    /// between consecutive bars — the same semantic the Reversal
    /// stage consumes in
    /// <see cref="Reversal.ReversalMode.TrailingStopPosition"/> mode.
    ///
    /// <para>
    /// <b>Published state</b> (all O(1), no per-bar allocation):
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Models.AtrSmoothRegimeSegmentRuntimeValues.Regime"/>
    /// — the current canonical regime direction
    /// (<see cref="AtrSmoothRegimeDirection.Bullish"/> /
    /// <see cref="AtrSmoothRegimeDirection.Bearish"/> /
    /// <see cref="AtrSmoothRegimeDirection.Unavailable"/> during
    /// warm-up).
    /// </description></item>
    /// <item><description>
    /// <see cref="Models.AtrSmoothRegimeSegmentRuntimeValues.RegimeId"/>
    /// — monotonic segment identifier: the first established
    /// directional regime receives ID 0 and every subsequent flip
    /// increments it by exactly 1. Never a timestamp, never a bar
    /// index, never recycled. <c>null</c> during warm-up.
    /// </description></item>
    /// <item><description>
    /// <see cref="Models.AtrSmoothRegimeSegmentRuntimeValues.RegimeStartIndex"/>
    /// — the index of the first bar of the current segment; changes
    /// exactly on a flip and never while the regime persists.
    /// <c>null</c> during warm-up.
    /// </description></item>
    /// <item><description>
    /// <see cref="Models.AtrSmoothRegimeSegmentRuntimeValues.RegimeAge"/>
    /// — zero-based bars elapsed since the segment began:
    /// <c>RegimeAge = index - RegimeStartIndex</c> (first bar of a
    /// segment = 0). Never one-based. <c>null</c> during warm-up.
    /// </description></item>
    /// <item><description>
    /// <see cref="Models.AtrSmoothRegimeSegmentRuntimeValues.RegimeTransition"/>
    /// — the flip itself, never a price crossing:
    /// <see cref="AtrSmoothRegimeTransition.Up"/> on a bearish →
    /// bullish flip, <see cref="AtrSmoothRegimeTransition.Down"/> on
    /// a bullish → bearish flip,
    /// <see cref="AtrSmoothRegimeTransition.None"/> otherwise. The
    /// FIRST establishment of a directional regime is not a
    /// transition (its predecessor state is not an established
    /// directional state).
    /// </description></item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>State machine.</b>
    /// <code>
    /// UNAVAILABLE --first established regime--> REGIME(id 0, age 0)
    /// REGIME --regime unchanged--> same segment (age + 1 each bar)
    /// REGIME --canonical flip--> REGIME(id + 1, age 0, transition)
    /// </code>
    /// The ATRSmooth trailing-stop position never returns to 0 once
    /// established (it is assigned only +1/-1 or carried forward),
    /// so the state machine has no REGIME → UNAVAILABLE edge; a
    /// published 0 after establishment is an upstream contract
    /// violation and fails closed.
    /// </para>
    ///
    /// <para>
    /// <b>Warm-up.</b> Before the first established directional
    /// regime exists (published regime 0), the stage publishes the
    /// unavailable state: Regime = Unavailable, RegimeId / Start /
    /// Age = <c>null</c>, Transition = None. No regime is
    /// manufactured during warm-up and zero is never assigned as an
    /// ID merely because it is convenient — the first actual
    /// directional regime receives the first valid ID (0).
    /// </para>
    ///
    /// <para>
    /// <b>No look-ahead.</b> Every value at bar t depends only on
    /// the published regime through bar t and on the committed
    /// end-of-previous-bar state. A later flip begins a new segment
    /// and never rewrites a historical bar's published state.
    /// </para>
    ///
    /// <para>
    /// <b>Live re-tick handling.</b> The stage snapshots the
    /// committed end-of-previous-bar segment state on the first
    /// call for a bar and restores it on re-ticks (the same
    /// snapshot/restore pattern as the Reversal stage and the
    /// reference sources), so re-processing the same bar is
    /// idempotent: the age, ID, start index, and transition are
    /// recomputed from the clean baseline and never double-apply.
    /// </para>
    ///
    /// <para>
    /// <b>Complexity.</b> True O(1) per bar: a fixed number of
    /// field reads/writes per update. No historical series scan,
    /// no per-bar allocation.
    /// </para>
    ///
    /// Pipeline placement: after the Reference stage, before the
    /// Reversal stage. Registered only for the ATRSmooth-based
    /// compositions (ATRSmooth2 single reference or the HMA +
    /// ATRSmooth composite, whose regime IS the ATRSmooth
    /// trailing-stop regime).
    /// </summary>
    public sealed class AtrSmoothRegimeSegmentEngine : EngineBase
    {
        // ---------------------------------------------------------
        // Committed segment state = "as of the end of the previous
        // bar" (or the initial state before bar 0). Mutated only
        // when a NEW bar is observed (never on a re-tick).
        //   _hasSegment:        an established directional regime
        //                       exists at all (false during warm-up).
        //   _segmentDirection:  the direction of the current
        //                       segment (valid iff _hasSegment).
        //   _segmentId:         the current segment's monotonic ID
        //                       (first established regime = 0).
        //   _segmentStartIndex: the first bar of the current
        //                       segment.
        // ---------------------------------------------------------
        private bool _hasSegment;
        private AtrSmoothRegimeDirection _segmentDirection;
        private int _segmentId;
        private int _segmentStartIndex;

        // ---------------------------------------------------------
        // Re-tick snapshot: captured on the first call for a bar,
        // restored on subsequent calls for the same bar.
        // ---------------------------------------------------------
        private int _snapshotIndex = int.MinValue;
        private bool _snapshotHasSegment;
        private AtrSmoothRegimeDirection _snapshotSegmentDirection;
        private int _snapshotSegmentId;
        private int _snapshotSegmentStartIndex;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="AtrSmoothRegimeSegmentEngine"/> class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        public AtrSmoothRegimeSegmentEngine(EngineContext context)
            : base(nameof(AtrSmoothRegimeSegmentEngine), context)
        {
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();
            _hasSegment = false;
            _segmentDirection = AtrSmoothRegimeDirection.Unavailable;
            _segmentId = 0;
            _segmentStartIndex = 0;
            _snapshotIndex = int.MinValue;

            PublishUnavailable();
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
                _hasSegment = _snapshotHasSegment;
                _segmentDirection = _snapshotSegmentDirection;
                _segmentId = _snapshotSegmentId;
                _segmentStartIndex = _snapshotSegmentStartIndex;
            }
            else
            {
                // First call for this bar: snapshot the committed
                // state (end of the previous bar / initial state).
                _snapshotHasSegment = _hasSegment;
                _snapshotSegmentDirection = _segmentDirection;
                _snapshotSegmentId = _segmentId;
                _snapshotSegmentStartIndex = _segmentStartIndex;
                _snapshotIndex = index;
            }

            //--------------------------------------------------
            // Canonical input: the published ATRSmooth regime.
            // The stage never touches market data, the reference
            // price, or any distance value — the regime flip IS the
            // segment semantic, and a price crossing of the
            // ATRSmooth line is invisible to this stage by
            // construction.
            //--------------------------------------------------
            double regime = Context.Values.Reference.Regime;
            AtrSmoothRegimeSegmentValidator.ValidateRegime(regime);

            var values = Context.Values.AtrSmoothRegimeSegment;

            if (regime == 0.0)
            {
                if (_hasSegment)
                {
                    // The canonical ATRSmooth trailing-stop position
                    // is assigned only +1/-1 (or carried forward), so
                    // it can never return to 0 after establishment.
                    // A 0 here means an upstream contract violation —
                    // fail closed rather than inventing a segment
                    // semantic the state machine does not define.
                    throw new InvalidOperationException(
                        "AtrSmoothRegimeSegment: the canonical ATRSmooth " +
                        "regime returned to the uncommitted state (0) after " +
                        "a directional regime was established — upstream " +
                        "contract violation.");
                }

                // Warm-up: no established directional regime yet.
                // Publish the unavailable state; do NOT manufacture
                // a regime or assign a convenience ID.
                values.Regime = AtrSmoothRegimeDirection.Unavailable;
                values.RegimeId = null;
                values.RegimeStartIndex = null;
                values.RegimeAge = null;
                values.RegimeTransition = AtrSmoothRegimeTransition.None;
                return;
            }

            AtrSmoothRegimeDirection direction = regime > 0.0
                ? AtrSmoothRegimeDirection.Bullish
                : AtrSmoothRegimeDirection.Bearish;

            if (!_hasSegment)
            {
                // First establishment of a directional regime: the
                // predecessor state is not an established directional
                // state, so this is NOT a transition. The first
                // actual regime receives the first valid ID: 0.
                _hasSegment = true;
                _segmentDirection = direction;
                _segmentId = 0;
                _segmentStartIndex = index;

                Publish(direction, 0, index, 0, AtrSmoothRegimeTransition.None);
            }
            else if (direction == _segmentDirection)
            {
                // Continuation: same segment, same ID, same start,
                // age = bars elapsed since the segment began.
                // The start index never moves while the regime is
                // unchanged, and the ID never increments on a
                // non-transition bar.
                Publish(
                    direction,
                    _segmentId,
                    _segmentStartIndex,
                    index - _segmentStartIndex,
                    AtrSmoothRegimeTransition.None);
            }
            else
            {
                // Canonical ATRSmooth flip: strict change of the
                // established regime between consecutive bars. A new
                // segment begins here — the ID increments by exactly
                // 1, the start moves to this bar, the age resets to
                // 0, and the transition flag carries the flip's
                // direction (the resulting regime's direction).
                _segmentDirection = direction;
                _segmentId = _segmentId + 1;
                _segmentStartIndex = index;

                AtrSmoothRegimeTransition transition = direction
                    == AtrSmoothRegimeDirection.Bullish
                        ? AtrSmoothRegimeTransition.Up
                        : AtrSmoothRegimeTransition.Down;

                Publish(direction, _segmentId, index, 0, transition);
            }
        }

        private void Publish(
            AtrSmoothRegimeDirection regime,
            int regimeId,
            int regimeStartIndex,
            int regimeAge,
            AtrSmoothRegimeTransition transition)
        {
            AtrSmoothRegimeSegmentValidator.Validate(
                regime, regimeId, regimeStartIndex, regimeAge, transition,
                Context.CurrentIndex);

            var values = Context.Values.AtrSmoothRegimeSegment;
            values.Regime = regime;
            values.RegimeId = regimeId;
            values.RegimeStartIndex = regimeStartIndex;
            values.RegimeAge = regimeAge;
            values.RegimeTransition = transition;
        }

        private void PublishUnavailable()
        {
            var values = Context.Values.AtrSmoothRegimeSegment;
            values.Regime = AtrSmoothRegimeDirection.Unavailable;
            values.RegimeId = null;
            values.RegimeStartIndex = null;
            values.RegimeAge = null;
            values.RegimeTransition = AtrSmoothRegimeTransition.None;
        }
    }
}

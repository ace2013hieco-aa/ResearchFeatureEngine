using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Runtime values for the ATRSmooth Regime Segment stage (M9).
    ///
    /// Describes the canonical ATRSmooth regime as a bounded temporal
    /// segment: identity, start, age, and transition. This is
    /// segment METADATA ONLY — it is not a trend score, an
    /// exhaustion measure, or a predictor of any kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Unavailable convention.</b> Follows the repository's
    /// established <c>int?</c> null convention
    /// (<see cref="Reversal.Runtime.ReversalRuntimeValues.BarsSinceReversal"/>):
    /// <see cref="RegimeId"/>, <see cref="RegimeStartIndex"/>, and
    /// <see cref="RegimeAge"/> are <c>null</c> while no established
    /// directional regime exists (warm-up); <see cref="Regime"/> is
    /// <see cref="AtrSmoothRegimeDirection.Unavailable"/> and
    /// <see cref="RegimeTransition"/> is
    /// <see cref="AtrSmoothRegimeTransition.None"/> in that state.
    /// </para>
    /// <para>
    /// <b>Canonical regime vs. segment metadata.</b>
    /// <see cref="Regime"/> republishes the canonical regime
    /// direction (identical semantics to
    /// <see cref="EngineValues.Reference.Regime"/> restricted to its
    /// directional values) for first-class consumption by later
    /// research features; <c>RegimeId</c> / <c>RegimeStartIndex</c> /
    /// <c>RegimeAge</c> / <c>RegimeTransition</c> are the segment
    /// metadata layered on top of it. No existing field is
    /// overloaded with a second meaning.
    /// </para>
    /// </remarks>
    public sealed class AtrSmoothRegimeSegmentRuntimeValues
    {
        /// <summary>
        /// Gets the direction of the current canonical ATRSmooth
        /// regime, or <see cref="AtrSmoothRegimeDirection.Unavailable"/>
        /// while no established directional regime exists (warm-up).
        /// </summary>
        public AtrSmoothRegimeDirection Regime { get; internal set; }
            = AtrSmoothRegimeDirection.Unavailable;

        /// <summary>
        /// Gets the monotonically increasing identifier of the
        /// current established regime segment. The first established
        /// directional regime receives ID 0; every subsequent
        /// canonical ATRSmooth flip increments the ID by exactly 1.
        /// All bars of the same segment share the same ID. IDs are
        /// never recycled during normal processing and are never
        /// timestamps or bar indexes. <c>null</c> during warm-up
        /// (the first actual directional regime gets the first valid
        /// ID, 0 — zero is never assigned merely because it is
        /// convenient).
        /// </summary>
        public int? RegimeId { get; internal set; }

        /// <summary>
        /// Gets the index of the first bar belonging to the current
        /// established regime segment. It changes exactly when the
        /// canonical ATRSmooth regime flips, and never moves while
        /// the regime remains unchanged. <c>null</c> during warm-up.
        /// </summary>
        public int? RegimeStartIndex { get; internal set; }

        /// <summary>
        /// Gets the zero-based number of bars elapsed since the
        /// current regime segment began:
        /// <c>RegimeAge = t - RegimeStartIndex</c>. The first bar of
        /// a segment has age 0, the second 1, and so on. Never
        /// one-based. <c>null</c> during warm-up.
        /// </summary>
        public int? RegimeAge { get; internal set; }

        /// <summary>
        /// Gets the canonical transition indicator for this bar:
        /// <see cref="AtrSmoothRegimeTransition.Up"/> on a bearish →
        /// bullish ATRSmooth flip, <see cref="AtrSmoothRegimeTransition.Down"/>
        /// on a bullish → bearish flip, and
        /// <see cref="AtrSmoothRegimeTransition.None"/> on
        /// continuation, warm-up, or unavailable bars. The flag
        /// represents the flip itself, never a price crossing of the
        /// ATRSmooth reference line.
        /// </summary>
        public AtrSmoothRegimeTransition RegimeTransition { get; internal set; }
            = AtrSmoothRegimeTransition.None;
    }
}

using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Engines;

namespace ResearchFeatureEngine.Engines.Validation
{
    /// <summary>
    /// Validates the published state of the
    /// <see cref="AtrSmoothRegimeSegmentEngine"/> (M9).
    ///
    /// Enforces the internal consistency of the segment state as a
    /// whole: every established regime must carry a non-null segment
    /// ID, start index, and age, and every unavailable state must
    /// leave all three null. It does NOT validate the regime
    /// semantics themselves — those are owned by the canonical
    /// ATRSmooth reference source.
    /// </summary>
    public static class AtrSmoothRegimeSegmentValidator
    {
        /// <summary>
        /// Validates the canonical regime input consumed by the
        /// segment stage before it is classified. The published
        /// regime must be one of the three canonical ATRSmooth
        /// states (-1, 0, +1) — never NaN, never infinity, never any
        /// other magnitude.
        /// </summary>
        /// <param name="regime">
        /// The source-declared signed regime published by the
        /// Reference stage.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the value is not a canonical regime state.
        /// </exception>
        public static void ValidateRegime(double regime)
        {
            if (double.IsNaN(regime))
                throw new InvalidOperationException(
                    "AtrSmoothRegimeSegment: received a NaN canonical regime.");

            if (double.IsInfinity(regime))
                throw new InvalidOperationException(
                    "AtrSmoothRegimeSegment: received an infinite " +
                    "canonical regime.");

            if (regime != 0.0
                && regime != 1.0
                && regime != -1.0)
            {
                throw new InvalidOperationException(
                    $"AtrSmoothRegimeSegment: canonical regime must be " +
                    $"-1, 0, or +1; got {regime}.");
            }
        }

        /// <summary>
        /// Validates a published ATRSmooth regime segment state.
        /// </summary>
        /// <param name="regime">Published segment regime direction.</param>
        /// <param name="regimeId">Published segment ID.</param>
        /// <param name="regimeStartIndex">Published segment start index.</param>
        /// <param name="regimeAge">Published zero-based segment age.</param>
        /// <param name="transition">Published transition flag.</param>
        /// <param name="index">The processing index of the current bar.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the segment state is internally inconsistent.
        /// </exception>
        public static void Validate(
            AtrSmoothRegimeDirection regime,
            int? regimeId,
            int? regimeStartIndex,
            int? regimeAge,
            AtrSmoothRegimeTransition transition,
            int index)
        {
            if (regime == AtrSmoothRegimeDirection.Unavailable)
            {
                // Unavailable (warm-up): ID / start / age must all be
                // null and no transition may be flagged.
                if (regimeId.HasValue
                    || regimeStartIndex.HasValue
                    || regimeAge.HasValue)
                {
                    throw new InvalidOperationException(
                        "AtrSmoothRegimeSegment: unavailable regime must leave " +
                        "RegimeId, RegimeStartIndex, and RegimeAge null.");
                }

                if (transition != AtrSmoothRegimeTransition.None)
                {
                    throw new InvalidOperationException(
                        "AtrSmoothRegimeSegment: unavailable regime must not " +
                        "report a transition.");
                }

                return;
            }

            // Established directional regime: all three metadata
            // fields must be present and mutually consistent.
            if (!regimeId.HasValue
                || !regimeStartIndex.HasValue
                || !regimeAge.HasValue)
            {
                throw new InvalidOperationException(
                    "AtrSmoothRegimeSegment: established regime requires " +
                    "non-null RegimeId, RegimeStartIndex, and RegimeAge.");
            }

            if (regimeStartIndex.Value < 0)
            {
                throw new InvalidOperationException(
                    $"AtrSmoothRegimeSegment: RegimeStartIndex " +
                    $"{regimeStartIndex.Value} is negative.");
            }

            // NOTE (no-look-ahead): the invariant
            // RegimeStartIndex <= current index holds for every
            // contract-conforming processing sequence (monotonic
            // advance + same-bar re-ticks) and is enforced by the
            // invariant test battery. It is deliberately NOT a
            // runtime check here: a raw backward ProcessAt jump is
            // outside the engine contract, and the repository's
            // documented convention (see
            // CTraderLifecycleAuditTests.RawProcessAt_BackwardJump_)
            // is that stateful stages then corrupt SILENTLY while
            // the adapter enforces monotonicity — matching
            // ReversalEngine's behavior under the same misuse.

            if (regimeAge.Value != index - regimeStartIndex.Value)
            {
                throw new InvalidOperationException(
                    $"AtrSmoothRegimeSegment: RegimeAge {regimeAge.Value} " +
                    $"violates RegimeAge == index - RegimeStartIndex " +
                    $"({index} - {regimeStartIndex.Value}).");
            }

            // An established regime bar may flag a flip only into the
            // OPPOSITE direction (a flip that lands on the same
            // direction is a state-machine violation).
            if (transition == AtrSmoothRegimeTransition.Up
                && regime != AtrSmoothRegimeDirection.Bullish)
            {
                throw new InvalidOperationException(
                    "AtrSmoothRegimeSegment: an Up transition requires a " +
                    "bullish resulting regime.");
            }

            if (transition == AtrSmoothRegimeTransition.Down
                && regime != AtrSmoothRegimeDirection.Bearish)
            {
                throw new InvalidOperationException(
                    "AtrSmoothRegimeSegment: a Down transition requires a " +
                    "bearish resulting regime.");
            }
        }
    }
}

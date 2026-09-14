using System;

namespace ResearchFeatureEngine.Engines.Validation
{
    /// <summary>
    /// Validates the output of the
    /// <see cref="HmaAtrSmoothRelativeClosePositionEngine"/>.
    /// </summary>
    public static class HmaAtrSmoothRelativeClosePositionValidator
    {
        /// <summary>
        /// Validates the computed relative close position.
        /// </summary>
        /// <param name="position">The computed relative close position.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the position is neither NaN (valid for warm-up
        /// and the zero-denominator state) nor a finite number.
        /// </exception>
        public static void Validate(double position)
        {
            // NaN is valid: HMA warm-up bars and the exact
            // zero-denominator (HMA == ATRSmooth) geometry. Any other
            // published value must be finite — no magnitude bound is
            // imposed (R is unbounded by construction). Fail closed
            // otherwise.
            if (!double.IsNaN(position) && !double.IsFinite(position))
            {
                throw new InvalidOperationException(
                    $"HmaAtrSmoothRelativeClosePosition must be finite or NaN; got {position}.");
            }
        }
    }
}

using System;

namespace ResearchFeatureEngine.Engines.Validation
{
    /// <summary>
    /// Validates the output of the
    /// <see cref="HmaAtrSmoothSeparationEngine"/>.
    /// </summary>
    public static class HmaAtrSmoothSeparationValidator
    {
        /// <summary>
        /// Validates the computed separation.
        /// </summary>
        /// <param name="separation">The computed separation.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the separation is neither NaN (the unavailable
        /// state) nor a finite number.
        /// </exception>
        public static void Validate(double separation)
        {
            // NaN is the unavailable state (HMA warm-up); any other
            // published value must be finite. Fail closed otherwise.
            if (!double.IsNaN(separation) && !double.IsFinite(separation))
            {
                throw new InvalidOperationException(
                    $"HmaAtrSmoothSeparation must be finite or NaN; got {separation}.");
            }
        }
    }
}

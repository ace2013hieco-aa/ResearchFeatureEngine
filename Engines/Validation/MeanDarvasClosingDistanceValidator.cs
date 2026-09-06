using System;

namespace ResearchFeatureEngine.Engines.Validation
{
    /// <summary>
    /// Validates the output of the <see cref="MeanDarvasClosingDistanceEngine"/>.
    /// </summary>
    public static class MeanDarvasClosingDistanceValidator
    {
        /// <summary>
        /// Validates the computed mean signed distance.
        /// </summary>
        /// <param name="mean">The computed mean signed distance.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the mean is not finite (NaN, +Inf, -Inf) and should be valid.
        /// </exception>
        public static void Validate(double mean)
        {
            // NaN is valid during warm-up; the engine publishes NaN explicitly.
            // If not NaN, it must be finite.
            if (!double.IsNaN(mean) && !double.IsFinite(mean))
            {
                throw new InvalidOperationException(
                    $"MeanDarvasClosingDistance mean must be finite or NaN; got {mean}.");
            }
        }
    }
}
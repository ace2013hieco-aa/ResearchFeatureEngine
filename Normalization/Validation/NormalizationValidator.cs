using System;

namespace ResearchFeatureEngine.Normalization.Validation
{
    /// <summary>
    /// Validates normalized measurements produced by the Normalization stage.
    ///
    /// A normalized value is considered valid if it is a finite, non-negative
    /// real number. NaN, Infinity, and negative values are invalid.
    /// </summary>
    public static class NormalizationValidator
    {
        /// <summary>
        /// Validates the normalized measurement.
        /// </summary>
        /// <param name="normalizedMeasurement">
        /// The normalized measurement to validate.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the normalized measurement is NaN, Infinity,
        /// or negative.
        /// </exception>
        public static void Validate(double normalizedMeasurement)
        {
            if (double.IsNaN(normalizedMeasurement))
                throw new InvalidOperationException(
                    $"Normalized measurement is NaN.");

            if (double.IsInfinity(normalizedMeasurement))
                throw new InvalidOperationException(
                    $"Normalized measurement is Infinity.");

            if (normalizedMeasurement < 0.0)
                throw new InvalidOperationException(
                    $"Normalized measurement is negative ({normalizedMeasurement}).");
        }
    }
}


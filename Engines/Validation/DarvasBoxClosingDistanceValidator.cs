using System;

namespace ResearchFeatureEngine.Engines.Validation
{
    /// <summary>
    /// Validates the inputs and outputs of the Darvas Box
    /// closing-distance stage.
    /// </summary>
    public static class DarvasBoxClosingDistanceValidator
    {
        /// <summary>
        /// Validates a Darvas Box closing-distance value produced
        /// by the model.
        /// </summary>
        /// <param name="signedClosingDistance">
        /// The signed closing distance (NaN is REJECTED here — the
        /// engine handles box-unavailability upstream of this
        /// validation).
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the value is NaN or Infinity.
        /// </exception>
        public static void Validate(double signedClosingDistance)
        {
            if (double.IsNaN(signedClosingDistance))
                throw new InvalidOperationException(
                    "Darvas Box closing distance cannot be NaN.");

            if (double.IsInfinity(signedClosingDistance))
                throw new InvalidOperationException(
                    "Darvas Box closing distance cannot be infinite.");
        }
    }
}

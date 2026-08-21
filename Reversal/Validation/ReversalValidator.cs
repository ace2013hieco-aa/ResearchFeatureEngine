using System;

namespace ResearchFeatureEngine.Reversal.Validation
{
    /// <summary>
    /// Validates the directional-extension input consumed by the
    /// Reversal stage before it is used to derive the
    /// close-to-reference relation.
    /// </summary>
    public static class ReversalValidator
    {
        /// <summary>
        /// Validates a directional extension value.
        /// </summary>
        /// <param name="directionalExtension">
        /// The signed close-to-reference distance produced by the
        /// Distance stage.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the value is NaN or Infinity (and therefore
        /// cannot resolve to a deterministic above/below relation).
        /// </exception>
        public static void Validate(double directionalExtension)
        {
            if (double.IsNaN(directionalExtension))
                throw new InvalidOperationException(
                    "Reversal stage received a NaN directional extension.");

            if (double.IsInfinity(directionalExtension))
                throw new InvalidOperationException(
                    "Reversal stage received an infinite directional extension.");
        }
    }
}

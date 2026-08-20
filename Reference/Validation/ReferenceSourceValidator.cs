using System;

namespace ResearchFeatureEngine.Reference.Validation
{
    /// <summary>
    /// Validates values produced by a reference source before they are
    /// published to <see cref="Core.EngineValues.Reference"/>.
    /// </summary>
    public static class ReferenceSourceValidator
    {
        /// <summary>
        /// Validates a freshly computed reference price.
        /// </summary>
        /// <param name="referencePrice">Computed reference price.</param>
        /// <param name="index">Processing index that produced the value.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the reference price is NaN, Infinity, or
        /// (defensively) non-finite for any reason.
        /// </exception>
        public static void Validate(double referencePrice, int index)
        {
            if (double.IsNaN(referencePrice))
                throw new InvalidOperationException(
                    $"Reference source produced NaN at index {index}.");

            if (double.IsInfinity(referencePrice))
                throw new InvalidOperationException(
                    $"Reference source produced Infinity at index {index}.");
        }
    }
}

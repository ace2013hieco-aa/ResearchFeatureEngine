using System;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Reference model that uses a precomputed ATR Smooth series
    /// as the market equilibrium.
    ///
    /// This class does not calculate ATR Smooth. It simply exposes
    /// an existing reference series through the IReferenceModel
    /// contract.
    /// </summary>
    public sealed class ATRSmoothReferenceModel : IReferenceModel
    {
        private readonly IPriceSeries _referenceSeries;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ATRSmoothReferenceModel"/> class.
        /// </summary>
        /// <param name="referenceSeries">
        /// Precomputed ATR Smooth series.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the supplied reference series is null.
        /// </exception>
        public ATRSmoothReferenceModel(IPriceSeries referenceSeries)
        {
            _referenceSeries = referenceSeries
                ?? throw new ArgumentNullException(nameof(referenceSeries));
        }

        /// <summary>
        /// Returns the reference price for the specified index.
        /// </summary>
        /// <param name="index">
        /// Current processing index.
        /// </param>
        /// <returns>
        /// ATR Smooth value at the specified index.
        /// </returns>
        public double Compute(int index)
        {
            if ((uint)index >= (uint)_referenceSeries.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _referenceSeries[index];
        }
    }
}

using System;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the signed displacement between the current
    /// market price and the supplied reference price.
    ///
    /// This is the default implementation of <see cref="IDistanceModel"/>.
    /// </summary>
    public sealed class ReferenceDistanceModel : IDistanceModel
    {
        private readonly IMarketData _marketData;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ReferenceDistanceModel"/> class.
        /// </summary>
        /// <param name="marketData">
        /// Read-only market data source.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="marketData"/> is null.
        /// </exception>
        public ReferenceDistanceModel(IMarketData marketData)
        {
            _marketData = marketData
                ?? throw new ArgumentNullException(nameof(marketData));
        }

        /// <summary>
        /// Computes the signed market extension.
        /// </summary>
        /// <param name="index">
        /// Current processing index.
        /// </param>
        /// <param name="referencePrice">
        /// Previously computed reference price.
        /// </param>
        /// <returns>
        /// Signed distance between the market price and
        /// the supplied reference price.
        /// </returns>
        public double Compute(int index, double referencePrice)
        {
            double price = _marketData.Close[index];

            return price - referencePrice;
        }
    }
}


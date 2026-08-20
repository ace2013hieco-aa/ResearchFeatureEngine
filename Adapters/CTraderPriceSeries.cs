using System;
using cAlgo.API;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Adapters
{
    /// <summary>
    /// Adapts a cTrader DataSeries to the platform-independent
    /// IPriceSeries interface.
    /// </summary>
    public sealed class CTraderPriceSeries : IPriceSeries
    {
        private readonly DataSeries _series;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="CTraderPriceSeries"/> class.
        /// </summary>
        /// <param name="series">
        /// Underlying cTrader data series.
        /// </param>
        public CTraderPriceSeries(DataSeries series)
        {
            _series = series ?? throw new ArgumentNullException(nameof(series));
        }

        /// <inheritdoc/>
        public int Count => _series.Count;

        /// <inheritdoc/>
        public double this[int index] => _series[index];
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Adapters
{
    /// <summary>
    /// Adapts cTrader Bars to the platform-independent
    /// IMarketData interface.
    /// </summary>
    public sealed class CTraderMarketData : IMarketData
    {
        private readonly Bars _bars;
        private readonly IReadOnlyList<DateTime> _time;

        public CTraderMarketData(Bars bars)
        {
            _bars = bars ?? throw new ArgumentNullException(nameof(bars));

            Open = new CTraderPriceSeries(_bars.OpenPrices);
            High = new CTraderPriceSeries(_bars.HighPrices);
            Low = new CTraderPriceSeries(_bars.LowPrices);
            Close = new CTraderPriceSeries(_bars.ClosePrices);
            Volume = new CTraderPriceSeries(_bars.TickVolumes);

            _time = Enumerable.Range(0, _bars.Count)
                .Select(i => _bars.OpenTimes[i])
                .ToList();
        }

        /// <inheritdoc/>
        public IPriceSeries Open { get; }

        /// <inheritdoc/>
        public IPriceSeries High { get; }

        /// <inheritdoc/>
        public IPriceSeries Low { get; }

        /// <inheritdoc/>
        public IPriceSeries Close { get; }

        /// <inheritdoc/>
        public IPriceSeries Volume { get; }

        /// <inheritdoc/>
        public IReadOnlyList<DateTime> Time => _time;

        /// <inheritdoc/>
        public int Count => _bars.Count;

        /// <inheritdoc/>
        public bool MoveNext() => false;
    }
}

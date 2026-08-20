using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests
{
    /// <summary>
    /// Test double for <see cref="IMarketData"/> that exposes separate
    /// OHLCV arrays. Unlike <see cref="TestMarketData"/>, this helper
    /// supports non-trivial high/low/volume series required by the
    /// ATR Smooth reference source and the ATR scale model.
    /// </summary>
    internal sealed class FlatOhlcvMarketData : IMarketData
    {
        private readonly double[] _open;
        private readonly double[] _high;
        private readonly double[] _low;
        private readonly double[] _close;
        private readonly double[] _volume;
        private int _currentIndex;

        public FlatOhlcvMarketData(
            double[] open,
            double[] high,
            double[] low,
            double[] close,
            double[] volume)
        {
            if (open.Length != high.Length ||
                high.Length != low.Length ||
                low.Length != close.Length ||
                close.Length != volume.Length)
            {
                throw new ArgumentException(
                    "All OHLCV arrays must have the same length.");
            }

            _open = open;
            _high = high;
            _low = low;
            _close = close;
            _volume = volume;
            _currentIndex = -1;
        }

        public IPriceSeries Open => new TestPriceSeries(_open);
        public IPriceSeries High => new TestPriceSeries(_high);
        public IPriceSeries Low => new TestPriceSeries(_low);
        public IPriceSeries Close => new TestPriceSeries(_close);
        public IPriceSeries Volume => new TestPriceSeries(_volume);

        public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
        public int Count => _close.Length;

        public bool MoveNext()
        {
            if (_currentIndex + 1 < _close.Length)
            {
                _currentIndex++;
                return true;
            }
            return false;
        }
    }
}

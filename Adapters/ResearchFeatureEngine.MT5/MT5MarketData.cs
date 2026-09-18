using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Interfaces;

#if MT5_STUB
using MetaTrader5;
#endif

// ---------------------------------------------------------------------------
// IMPORTANT: This file provides a STUB for MetaTrader5.dll types so that the
// MT5 adapter project compiles in CI / build environments where the MT5
// terminal is not installed.
//
// When building on a machine with MetaTrader 5 installed:
//   1. Pass -p:MT5DllPath="C:\Program Files\MetaTrader 5\MetaTrader5.dll"
//      (or set the MT5dllpath MSBuild property).
//   2. The stub type below will not be used; the real MetaTrader5.dll
//      types take precedence.
//
// The real MetaTrader5.dll provides MqlRates[] (bar data) with fields:
//   time (long, Unix seconds), open, high, low, close (double),
//   tick_volume (long). We only need MqlRates for this adapter.
// ---------------------------------------------------------------------------

#if MT5_STUB
// Stub definitions — used only when MetaTrader5.dll is unavailable.
namespace MetaTrader5
{
    /// <summary>
    /// Minimal stub matching the real MetaTrader5.dll MqlRates struct.
    /// Only the fields needed by the adapter are defined.
    /// </summary>
    public class MqlRates
    {
        public long time { get; set; }
        public double open { get; set; }
        public double high { get; set; }
        public double low { get; set; }
        public double close { get; set; }
        public long tick_volume { get; set; }
    }
}
#endif

namespace ResearchFeatureEngine.Adapters
{
    /// <summary>
    /// Read-only indexed price series backed by a managed double array.
    /// Created from a .NET-side array populated by the MT5 adapter
    /// from MqlRates data.
    /// </summary>
    internal sealed class MT5PriceSeries : IPriceSeries
    {
        private readonly double[] _values;

        public MT5PriceSeries(double[] values) =>
            _values = values ?? throw new ArgumentNullException(nameof(values));

        public int Count => _values.Length;
        public double this[int index] => _values[index];
    }

    /// <summary>
    /// Adapts MetaTrader 5 bar data (<see cref="MqlRates"/>[]) to the
    /// platform-independent <see cref="IMarketData"/> interface.
    ///
    /// The MQL5 indicator application passes the bar array from the
    /// OnCalculate event; this class holds the owning references so
    /// the GC keeps them alive for the lifetime of the adapter.
    ///
    /// This mirrors <see cref="CTraderMarketData"/> — same adapter pattern,
    /// different platform API. Core never references this project.
    /// </summary>
    public sealed class MT5MarketData : IMarketData
    {
        private readonly double[] _open;
        private readonly double[] _high;
        private readonly double[] _low;
        private readonly double[] _close;
        private readonly double[] _volume;
        private readonly MqlRates[] _rates;
        private readonly DateTime[] _time;
        private int _currentIndex = -1;

        /// <summary>
        /// Creates an adapter from an array of MqlRates (MT5 bar data).
        /// Each MqlRates contains: time, open, high, low, close, tick_volume.
        /// </summary>
        public MT5MarketData(MqlRates[] rates)
        {
            if (rates == null || rates.Length == 0)
                throw new ArgumentException(
                    "Rates array cannot be null or empty.", nameof(rates));

            _rates = rates;
            int n = rates.Length;

            _open = new double[n];
            _high = new double[n];
            _low = new double[n];
            _close = new double[n];
            _volume = new double[n];
            _time = new DateTime[n];

            for (int i = 0; i < n; i++)
            {
                _open[i] = rates[i].open;
                _high[i] = rates[i].high;
                _low[i] = rates[i].low;
                _close[i] = rates[i].close;
                _volume[i] = (double)rates[i].tick_volume;
                _time[i] = DateTimeOffset.FromUnixTimeSeconds(rates[i].time).DateTime;
            }

            _currentIndex = -1;
        }

        public IPriceSeries Open => new MT5PriceSeries(_open);
        public IPriceSeries High => new MT5PriceSeries(_high);
        public IPriceSeries Low => new MT5PriceSeries(_low);
        public IPriceSeries Close => new MT5PriceSeries(_close);
        public IPriceSeries Volume => new MT5PriceSeries(_volume);
        public IReadOnlyList<DateTime> Time => _time;
        public int Count => _close.Length;

        /// <inheritdoc/>
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Adapters
{
    /// <summary>
    /// Read-only indexed price series backed by a pinned managed
    /// double array. Created from a .NET-side array populated by the
    /// Python layer (which marshals from numpy arrays via
    /// Marshal.Copy / pythonnet memoryview handling).
    /// </summary>
    internal sealed class ArrayPriceSeries : IPriceSeries
    {
        private readonly double[] _values;

        public ArrayPriceSeries(double[] values) =>
            _values = values ?? throw new ArgumentNullException(nameof(values));

        public int Count => _values.Length;
        public double this[int index] => _values[index];
    }

    /// <summary>
    /// Adapts Python-side OHLCV data (delivered as parallel double
    /// arrays or numpy arrays) to the platform-independent
    /// <see cref="IMarketData"/> interface. The Python layer passes
    /// the arrays in; this class holds the owning references so the
    /// GC keeps them alive for the lifetime of the adapter.
    /// </summary>
    /// <remarks>
    /// Python-side callers populate the arrays via
    /// <see cref="LoadFromArrays"/>, which copies the Python-side
    /// values into managed arrays. This avoids pinning overhead and
    /// keeps the ownership model simple: the Python layer owns its
    /// arrays, and this adapter owns a .NET-side copy.
    /// </remarks>
    public sealed class PythonMarketData : IMarketData
    {
        private double[]? _open;
        private double[]? _high;
        private double[]? _low;
        private double[]? _close;
        private double[]? _volume;
        private DateTime[]? _time;
        private int _currentIndex = -1;

        /// <summary>
        /// Loads OHLCV arrays from the Python layer.
        /// Each argument may be a .NET double[] or a numpy array
        /// (pythonnet will accept a numpy array where a double[] is
        /// expected via automatic conversion; if conversion fails the
        /// caller should convert first via
        /// <c>np.ascontiguousarray(arr, dtype=np.float64)</c>).
        /// </summary>
        /// <param name="open">Open prices.</param>
        /// <param name="high">High prices.</param>
        /// <param name="low">Low prices.</param>
        /// <param name="close">Close prices.</param>
        /// <param name="volume">Volumes.</param>
        /// <param name="time">Optional datetime strings (ISO 8601).
        /// If null, synthetic UTC timestamps are generated.</param>
        public void LoadFromArrays(
            double[] open,
            double[] high,
            double[] low,
            double[] close,
            double[] volume,
            string[]? time = null)
        {
            int n = close.Length;
            if (open.Length != n)
                throw new ArgumentException(
                    $"Open length {open.Length} != close length {n}.");
            if (high.Length != n)
                throw new ArgumentException(
                    $"High length {high.Length} != close length {n}.");
            if (low.Length != n)
                throw new ArgumentException(
                    $"Low length {low.Length} != close length {n}.");
            if (volume.Length != n)
                throw new ArgumentException(
                    $"Volume length {volume.Length} != close length {n}.");

            _open = open;
            _high = high;
            _low = low;
            _close = close;
            _volume = volume;

            if (time is not null)
            {
                if (time.Length != n)
                    throw new ArgumentException(
                        $"Time length {time.Length} != close length {n}.");
                _time = new DateTime[n];
                for (int i = 0; i < n; i++)
                {
                    _time[i] = DateTime.Parse(
                        time[i], CultureInfo.InvariantCulture);
                }
            }
            else
            {
                _time = new DateTime[n];
                for (int i = 0; i < n; i++)
                    _time[i] = DateTime.UtcNow;
            }

            _currentIndex = -1;
        }

        public IPriceSeries Open =>
            _open is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : new ArrayPriceSeries(_open);

        public IPriceSeries High =>
            _high is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : new ArrayPriceSeries(_high);

        public IPriceSeries Low =>
            _low is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : new ArrayPriceSeries(_low);

        public IPriceSeries Close =>
            _close is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : new ArrayPriceSeries(_close);

        public IPriceSeries Volume =>
            _volume is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : new ArrayPriceSeries(_volume);

        public IReadOnlyList<DateTime> Time =>
            _time is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : _time;

        public int Count => _close?.Length ?? 0;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_close is null)
                throw new InvalidOperationException(
                    "Call LoadFromArrays before advancing.");
            if (_currentIndex + 1 < _close.Length)
            {
                _currentIndex++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a copy of the close values array.
        /// </summary>
        public double[] GetCloseValues() =>
            _close is null
                ? throw new InvalidOperationException(
                    "Call LoadFromArrays before accessing market data.")
                : (double[])_close.Clone();
    }
}

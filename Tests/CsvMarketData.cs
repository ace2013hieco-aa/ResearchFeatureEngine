using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests
{
    internal sealed class CsvMarketData : IMarketData
    {
        private readonly double[] _open;
        private readonly double[] _high;
        private readonly double[] _low;
        private readonly double[] _close;
        private readonly double[] _volume;
        private readonly DateTime[] _time;
        private int _currentIndex;

        public CsvMarketData(string filePath)
        {
            var lines = File.ReadAllLines(filePath);

            if (lines.Length < 2)
                throw new InvalidOperationException("CSV must contain a header and at least one data row.");

            var rows = lines
                .Skip(1)
                .Select(ParseRow)
                .ToList();

            _open = rows.Select(r => r.Open).ToArray();
            _high = rows.Select(r => r.High).ToArray();
            _low = rows.Select(r => r.Low).ToArray();
            _close = rows.Select(r => r.Close).ToArray();
            _volume = rows.Select(r => r.Volume).ToArray();
            _time = rows.Select(r => r.Time).ToArray();
            _currentIndex = -1;
        }

        public IPriceSeries Open => new TestPriceSeries(_open);

        public IPriceSeries High => new TestPriceSeries(_high);

        public IPriceSeries Low => new TestPriceSeries(_low);

        public IPriceSeries Close => new TestPriceSeries(_close);

        public IPriceSeries Volume => new TestPriceSeries(_volume);

        public IReadOnlyList<DateTime> Time => _time;

        public int Count => _close.Length;

        /// <summary>
        /// Returns a copy of the close values array.
        /// </summary>
        public double[] GetCloseValues() => (double[])_close.Clone();

        public bool MoveNext()
        {
            if (_currentIndex + 1 < _close.Length)
            {
                _currentIndex++;
                return true;
            }

            return false;
        }

        private static (DateTime Time, double Open, double High, double Low, double Close, double Volume) ParseRow(string line)
        {
            string[] parts = line.Split(',');

            if (parts.Length < 6)
                throw new FormatException($"Expected at least 6 comma-separated values, got {parts.Length}.");

            DateTime time = DateTime.Parse(parts[0], CultureInfo.InvariantCulture);
            double open = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double high = double.Parse(parts[2], CultureInfo.InvariantCulture);
            double low = double.Parse(parts[3], CultureInfo.InvariantCulture);
            double close = double.Parse(parts[4], CultureInfo.InvariantCulture);
            double volume = double.Parse(parts[5], CultureInfo.InvariantCulture);

            return (time, open, high, low, close, volume);
        }
    }
}

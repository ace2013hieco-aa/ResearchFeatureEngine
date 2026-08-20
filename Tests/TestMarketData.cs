using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests
{
    /// <summary>
    /// Test double for <see cref="IMarketData"/> that wraps
    /// pre-supplied price series for testing purposes.
    /// </summary>
    internal sealed class TestMarketData : IMarketData
    {
        private double[] _closeValues;
        private int _currentIndex;

        public TestMarketData(params double[] closeValues)
        {
            _closeValues = closeValues ?? throw new ArgumentNullException(nameof(closeValues));
            _currentIndex = -1;
        }

        public IPriceSeries Open => new TestPriceSeries(_closeValues);

        public IPriceSeries High => new TestPriceSeries(_closeValues);

        public IPriceSeries Low => new TestPriceSeries(_closeValues);

        IPriceSeries IMarketData.Close => new TestPriceSeries(_closeValues);

        /// <summary>
        /// Gets or sets the current close price.
        /// The close price is represented as a single-element series.
        /// </summary>
        public double Close
        {
            get => _closeValues[0];
            set => _closeValues = new[] { value };
        }

        public IPriceSeries Volume => new TestPriceSeries(Array.Empty<double>());

        public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();

        public int Count => _closeValues.Length;

        /// <summary>
        /// Sets the close price to a single-element series.
        /// </summary>
        public void SetClose(double value)
        {
            Close = value;
        }

        /// <summary>
        /// Advances the enumerator to the next data point.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator was successfully advanced;
        /// <c>false</c> if the enumerator has passed the end of the collection.
        /// </returns>
        public bool MoveNext()
        {
            if (_currentIndex + 1 < _closeValues.Length)
            {
                _currentIndex++;
                return true;
            }

            return false;
        }
    }
}

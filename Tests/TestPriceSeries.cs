using System;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests
{
    /// <summary>
    /// Test double for <see cref="IPriceSeries"/> backed by
    /// a pre-supplied array of values.
    /// </summary>
    internal sealed class TestPriceSeries : IPriceSeries
    {
        private readonly double[] _values;

        public TestPriceSeries(double[] values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public int Count => _values.Length;

        public double this[int index] => _values[index];
    }
}

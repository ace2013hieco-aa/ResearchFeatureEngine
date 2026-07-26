using System;
using Xunit;

namespace ResearchFeatureEngine.Tests
{
    internal static class NumericAssert
    {
        public static void IsFinite(double value)
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        }
    }
}

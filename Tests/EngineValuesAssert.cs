using Xunit;

using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Tests
{
    internal static class EngineValuesAssert
    {
        public static void Equal(
            EngineValues expected,
            EngineValues actual,
            int precision = 8)
        {
            // Reference
            Assert.Equal(expected.Reference.Price, actual.Reference.Price, precision);

            // Distance
            Assert.Equal(expected.Distance.DirectionalExtension, actual.Distance.DirectionalExtension, precision);
            Assert.Equal(expected.Distance.AbsoluteExtension, actual.Distance.AbsoluteExtension, precision);

            // Scale
            Assert.Equal(expected.Scale.Scale, actual.Scale.Scale, precision);

            // Normalization
            Assert.Equal(expected.Normalization.NormalizedMeasurement, actual.Normalization.NormalizedMeasurement, precision);

            // Statistics
            Assert.Equal(expected.Statistics.ObservationCount, actual.Statistics.ObservationCount);
            Assert.Equal(expected.Statistics.Location.Mean, actual.Statistics.Location.Mean, precision);
            Assert.Equal(expected.Statistics.Location.Median, actual.Statistics.Location.Median, precision);
            Assert.Equal(expected.Statistics.Dispersion.Variance, actual.Statistics.Dispersion.Variance, precision);
            Assert.Equal(expected.Statistics.Dispersion.StandardDeviation, actual.Statistics.Dispersion.StandardDeviation, precision);
            Assert.Equal(expected.Statistics.Dispersion.MedianAbsoluteDeviation, actual.Statistics.Dispersion.MedianAbsoluteDeviation, precision);
            Assert.Equal(expected.Statistics.Range.Minimum, actual.Statistics.Range.Minimum, precision);
            Assert.Equal(expected.Statistics.Range.Maximum, actual.Statistics.Range.Maximum, precision);
            Assert.Equal(expected.Statistics.Range.Range, actual.Statistics.Range.Range, precision);
        }
    }
}

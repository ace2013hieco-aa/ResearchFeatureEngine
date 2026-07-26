using System;

namespace ResearchFeatureEngine.Tests
{
    internal static class ExpectedResultsFactory
    {
        /// <summary>
        /// Creates expected results for the known dataset produced by
        /// <see cref="TestConfigurationFactory.CreateKnownDataset"/>.
        ///
        /// Market data:  20 bars, close = 100 + 2i for i = 0..19
        /// Reference:    flat at 100.0
        /// ATR period:   14
        /// Window size:  20
        ///
        /// Expected values correspond to the state after processing
        /// the last bar (index 19).
        /// </summary>
        public static ExpectedResults CreateKnownDataset()
        {
            const int barCount = 20;
            const double flatReference = 100.0;
            const double priceIncrement = 2.0;
            const double atrValue = 2.0;

            double lastClose = flatReference + (barCount - 1) * priceIncrement;

            double directional = lastClose - flatReference;
            double absolute = Math.Abs(directional);
            double scale = atrValue;
            double normalized = absolute / scale;

            // Close prices: 100, 102, 104, ..., 138
            double mean = (flatReference + lastClose) / 2.0;
            double median = mean;

            int n = barCount;
            double sumSquares = 0;

            for (int i = 0; i < n; i++)
            {
                double price = flatReference + i * priceIncrement;
                double dev = price - mean;
                sumSquares += dev * dev;
            }

            double variance = sumSquares / (n - 1);
            double stdDev = Math.Sqrt(variance);

            // MAD = median of absolute deviations from the median
            // For this symmetric even-length set, MAD = 10.0
            double medianAbsoluteDeviation = 10.0;

            var expected = new ExpectedResults();

            expected.Reference.Price = flatReference;
            expected.Distance.DirectionalExtension = directional;
            expected.Distance.AbsoluteExtension = absolute;
            expected.Scale.Scale = scale;
            expected.Normalization.NormalizedMeasurement = normalized;

            expected.Statistics.Location.Mean = mean;
            expected.Statistics.Location.Median = median;
            expected.Statistics.Dispersion.Variance = variance;
            expected.Statistics.Dispersion.StandardDeviation = stdDev;
            expected.Statistics.Dispersion.MedianAbsoluteDeviation = medianAbsoluteDeviation;
            expected.Statistics.Range.Minimum = flatReference;
            expected.Statistics.Range.Maximum = lastClose;

            return expected;
        }
    }
}

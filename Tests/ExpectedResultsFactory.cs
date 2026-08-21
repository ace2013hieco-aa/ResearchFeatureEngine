using System;

namespace ResearchFeatureEngine.Tests
{
    internal static class ExpectedResultsFactory
    {
        /// <summary>
        /// Creates expected results for the known dataset produced by
        /// <see cref="TestConfigurationFactory.CreateKnownDataset"/>.
        ///
        /// Dataset (20 bars, indexed 0..19):
        ///   close[i] = 100 + 2 * i
        ///   high[i]  = close[i] + 0.5
        ///   low[i]   = close[i] - 0.5
        ///   open[i]  = close[i]
        ///   volume[i]= 100
        ///
        /// Reference source: ATRSmoothReferenceSource with
        ///   ATRPeriod = 14, ATRMultiplier = 5.1, SmoothLength = 20
        /// (i.e. SmoothLength == barCount, so the rolling VWMA is the
        /// full simple VWMA of all 20 bars by the last bar).
        ///
        /// Scale model: ATRScaleModel(period = 14) — simple average of
        /// True Range over the trailing 14 bars.
        ///
        /// Live-bar handling: the rolling statistics window holds
        /// CLOSED bars only. The most-recent bar (index 19, the last
        /// bar processed by Update()) is held aside as the live bar
        /// and is not yet in the window. So the statistics window
        /// contains the first 19 closes: 100, 102, ..., 136.
        ///
        /// Expected values correspond to the state after processing
        /// the last bar (index 19) via Update().
        ///
        /// These values were produced by the standalone reference
        /// program in <c>bin/ComputeExpected</c>, which mirrors the
        /// algorithm in <c>ATRSmoothReferenceSource</c> line for line
        /// and was kept independent of the production code. The
        /// reference production code asserts equality with these
        /// values to within 8 decimal places, ensuring the
        /// implementation is bit-equivalent to the documented math.
        /// </summary>
        public static ExpectedResults CreateKnownDataset()
        {
            var expected = new ExpectedResults();

            // Reference (final bar)
            expected.Reference.Price = 122.37724421291009;

            // Distance (final bar)
            expected.Distance.DirectionalExtension = 15.62275578708991;
            expected.Distance.AbsoluteExtension = 15.62275578708991;

            // Scale (simple ATR(14) over the trailing window,
            // true range is constant 2.5 from bar 1 onwards, so
            // average = 2.5 exactly)
            expected.Scale.Scale = 2.5;

            // Normalization = absolute / scale
            expected.Normalization.NormalizedMeasurement = 6.249102314835964;

            // Statistics over the 19 closed closes (live bar held aside)
            //   mean   = (100 + 136) / 2 = 118
            //   var    = sum((c - 118)^2) / (n - 1) = 126.66666666666667
            //   stddev = sqrt(var) = 11.254628677422755
            //   median = index 9 of sorted [100,102,...,136] = 118
            //   MAD    = median of |c - 118| = 10
            //   min    = 100
            //   max    = 136  (138 is the live bar; not in the window)
            expected.Statistics.Location.Mean = 118.0;
            expected.Statistics.Location.Median = 118.0;
            expected.Statistics.Dispersion.Variance =
                126.66666666666667;
            expected.Statistics.Dispersion.StandardDeviation =
                Math.Sqrt(126.66666666666667);
            expected.Statistics.Dispersion.MedianAbsoluteDeviation = 10.0;

            // Range of closed-bar closes
            expected.Statistics.Range.Minimum = 100.0;
            expected.Statistics.Range.Maximum = 136.0;

            return expected;
        }
    }
}

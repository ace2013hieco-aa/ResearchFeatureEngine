using System;
using Xunit;

using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Tests.Statistics
{
    /// <summary>
    /// Deterministic model-level tests for the Fisher–Pearson
    /// bias-corrected skewness (G1) and Fisher bias-corrected excess
    /// kurtosis (G2).
    ///
    /// All hard-coded expected values were produced by an INDEPENDENT
    /// two-pass reference implementation (Python, IEEE-754 doubles)
    /// written separately from the production code, never by calling
    /// the implementation under test. Comments carry the reference
    /// computation for each sample.
    /// </summary>
    public sealed class SkewnessKurtosisModelTests
    {
        private static double Skewness(params double[] values) =>
            new SkewnessModel().Compute(new StatisticsInput(values));

        private static double Kurtosis(params double[] values) =>
            new KurtosisModel().Compute(new StatisticsInput(values));

        // ---------------------------------------------------------
        // A. Exact mathematical reference
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_MatchesIndependentReference_RightSkewedSample()
        {
            // Sample [1, 2, 4, 8, 16]: mean = 6.2,
            //   m2 = 29.76, m3 = 144.336
            //   G1 = sqrt(20)/3 * 144.336 / 29.76^1.5 = 1.3253147098134...
            Assert.Equal(1.3253147098134046, Skewness(1, 2, 4, 8, 16), 11);
        }

        [Fact]
        public void Kurtosis_MatchesIndependentReference_RightSkewedSample()
        {
            // Sample [1, 2, 4, 8, 16]: m2 = 29.76, m4 = 2059.9872,
            //   b2 = m4/m2^2 = 2.32531...
            //   G2 = ((6)(b2-3)+6)(4)/((3)(2)) = 1.3037634408602...
            Assert.Equal(1.3037634408602106, Kurtosis(1, 2, 4, 8, 16), 11);
        }

        [Fact]
        public void Kurtosis_IsFisherCorrected_NotUncorrectedB2Minus3()
        {
            // [1, 2, 4, 8]: m2 = 7.1875, m4 = 98.20703125,
            //   b2 = 1.90102... -> b2 - 3 = -1.09898 (uncorrected)
            //   G2 (Fisher-corrected) = 0.7576559546313795
            // The two disagree, proving the Fisher correction is
            // applied and neither raw b2 nor b2-3 was implemented.
            Assert.Equal(0.7576559546313795, Kurtosis(1, 2, 4, 8), 11);
            Assert.NotEqual(-1.09898, Kurtosis(1, 2, 4, 8), 2);
        }

        // ---------------------------------------------------------
        // B. Symmetric distribution -> G1 ~= 0
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_SymmetricSample_IsZero()
        {
            // [-3,-2,-1,0,1,2,3] is exactly symmetric: m3 = 0 -> G1 = 0.
            Assert.Equal(0.0, Skewness(-3, -2, -1, 0, 1, 2, 3), 12);
        }

        [Fact]
        public void Skewness_SymmetricEvenSample_IsZero()
        {
            Assert.Equal(0.0, Skewness(-1.5, -1.0, -0.5, 0.5, 1.0, 1.5), 12);
        }

        // ---------------------------------------------------------
        // C. Known asymmetric distribution: sign + magnitude
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_RightSkewed_Positive()
        {
            double value = Skewness(1, 2, 4, 8, 16);
            Assert.True(value > 0.0, $"Expected positive skew, got {value}");
        }

        [Fact]
        public void Skewness_LeftSkewed_NegativeMirrorOfRightSkewed()
        {
            // Mirroring a sample negates G1 exactly.
            double left = Skewness(-16, -8, -4, -2, -1);
            Assert.True(left < 0.0, $"Expected negative skew, got {left}");
            Assert.Equal(-1.3253147098134055, left, 11);
            // Magnitude must match the mirrored right-skewed sample.
            Assert.Equal(1.3253147098134046, -left, 11);
        }

        // ---------------------------------------------------------
        // D. Heavy-tailed distribution -> kurtosis above baseline
        // ---------------------------------------------------------

        [Fact]
        public void Kurtosis_HeavyTailedSample_ExceedsNormalLikeBaseline()
        {
            // Baseline: symmetric sample with excess kurtosis exactly 0.
            double baseline = Kurtosis(
                -1.281552, -0.841621, -0.524401, -0.253347,
                 0.0,       0.253347,  0.524401,  0.841621, 1.281552);
            Assert.Equal(-0.687151504962436, baseline, 11);

            // Same nine points plus one extreme tail observation (8.0).
            double heavy = Kurtosis(
                -1.281552, -0.841621, -0.524401, -0.253347,
                 0.0,       0.253347,  0.524401,  0.841621, 1.281552, 8.0);
            Assert.Equal(7.776192675348027, heavy, 11);

            Assert.True(heavy > baseline + 5.0,
                $"Heavy-tailed kurtosis {heavy} did not exceed baseline {baseline}");
        }

        [Fact]
        public void Skewness_HeavyTailedSample_MatchesReference()
        {
            Assert.Equal(2.66899925141486,
                Skewness(-1.281552, -0.841621, -0.524401, -0.253347,
                          0.0,       0.253347,  0.524401,  0.841621, 1.281552, 8.0),
                11);
        }

        // ---------------------------------------------------------
        // E. Excess-kurtosis convention: normal-like data -> G2 ~= 0,
        //    never ~= 3.
        // ---------------------------------------------------------

        [Fact]
        public void Kurtosis_ZeroExcessKurtosisConstruction_ReturnsZeroNotRaw()
        {
            // Sample { -c, -1, -1, 1, 1, c } with c = sqrt(5 + 2*sqrt(7))
            // is constructed so that m4/m2^2 = 15/7, which makes the
            // Fisher-corrected excess kurtosis exactly 0 (the normal
            // distribution's kurtosis signature). The RAW kurtosis of
            // this sample is 15/7 ~= 2.1429 (not 3), so an excess
            // implementation returns ~0 while a raw implementation
            // would return ~2.14 and fail.
            double c = Math.Sqrt(5.0 + 2.0 * Math.Sqrt(7.0));

            double g2 = Kurtosis(-c, -1.0, -1.0, 1.0, 1.0, c);

            Assert.True(Math.Abs(g2) < 1e-9,
                $"Excess kurtosis of zero-kurtosis sample should be ~0, got {g2}");
        }

        [Fact]
        public void Kurtosis_SymmetricLightTailedSample_IsNegativeExcess()
        {
            // [-3..3]: m2 = 4, m4 = 28 -> b2 = 1.75 -> G2 = -1.2
            // (light-tailed relative to normal: negative excess).
            Assert.Equal(-1.2, Kurtosis(-3, -2, -1, 0, 1, 2, 3), 12);
        }

        // ---------------------------------------------------------
        // F. Minimum n: skewness n>=3, kurtosis n>=4
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_BelowThreeObservations_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Skewness(1, 2));
        }

        [Fact]
        public void Kurtosis_BelowFourObservations_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Kurtosis(1, 2, 4));
        }

        [Fact]
        public void Skewness_ExactlyThreeObservations_MatchesReference()
        {
            Assert.Equal(0.9352195295828235, Skewness(1, 2, 4), 11);
        }

        [Fact]
        public void Kurtosis_ExactlyFourObservations_MatchesReference()
        {
            Assert.Equal(1.1376243669576889, Skewness(1, 2, 4, 8), 11);
            Assert.Equal(0.7576559546313795, Kurtosis(1, 2, 4, 8), 11);
        }

        // ---------------------------------------------------------
        // G. Zero variance: mathematically undefined -> NaN signal
        //    (the engine converts this into "do not publish").
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_ZeroVariance_ReturnsNaN()
        {
            Assert.True(double.IsNaN(Skewness(5, 5, 5, 5, 5)));
            Assert.True(double.IsNaN(Skewness(5, 5, 5)));
        }

        [Fact]
        public void Kurtosis_ZeroVariance_ReturnsNaN()
        {
            Assert.True(double.IsNaN(Kurtosis(5, 5, 5, 5, 5)));
        }

        // ---------------------------------------------------------
        // H. Numerical stability: large baseline + tiny dispersion
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_LargeBaseline_MatchesSmallScaleReference()
        {
            // The relative pattern [0, 1, 3, 7, 12] shifted by 1e6.
            // Central moments are translation-invariant, so G1 must
            // equal the small-scale value 0.9425068852664726. A naive
            // one-pass power-sum implementation would lose all
            // precision at this baseline (catastrophic cancellation).
            double smallScale = Skewness(0, 1, 3, 7, 12);
            Assert.Equal(0.9425068852664726, smallScale, 11);

            double largeBaseline = Skewness(1000000, 1000001, 1000003, 1000007, 1000012);
            NumericAssert.IsFinite(largeBaseline);
            Assert.Equal(0.9425068852900882, largeBaseline, 8);

            Assert.True(Math.Abs(largeBaseline - smallScale) < 1e-8,
                $"Translation invariance violated: {largeBaseline} vs {smallScale}");
        }

        [Fact]
        public void Kurtosis_LargeBaseline_MatchesSmallScaleReference()
        {
            double smallScale = Kurtosis(0, 1, 3, 7, 12);
            Assert.Equal(-0.264695422445764, smallScale, 11);

            double largeBaseline = Kurtosis(1000000, 1000001, 1000003, 1000007, 1000012);
            NumericAssert.IsFinite(largeBaseline);
            Assert.Equal(-0.2646954223923454, largeBaseline, 8);

            Assert.True(Math.Abs(largeBaseline - smallScale) < 1e-8,
                $"Translation invariance violated: {largeBaseline} vs {smallScale}");
        }

        // ---------------------------------------------------------
        // I. Outlier sensitivity: one extreme observation reacts
        //    materially in both statistics.
        // ---------------------------------------------------------

        [Fact]
        public void Skewness_OneExtremeObservation_ReactsMaterially()
        {
            // Symmetric 9-point baseline (exact normal-quantile grid,
            // G1 = 0), then the same nine points plus one extreme
            // observation (50.0): G1 = 3.148144676005027.
            double baseline = Skewness(
                -1.281552, -0.841621, -0.524401, -0.253347,
                 0.0,       0.253347,  0.524401,  0.841621, 1.281552);
            Assert.Equal(0.0, baseline, 11);

            double withOutlier = Skewness(
                -1.281552, -0.841621, -0.524401, -0.253347,
                 0.0,       0.253347,  0.524401,  0.841621, 1.281552, 50.0);
            Assert.Equal(3.148144676005027, withOutlier, 10);

            Assert.True(Math.Abs(withOutlier - baseline) > 2.5,
                $"Skewness did not react materially: {baseline} -> {withOutlier}");
        }

        [Fact]
        public void Kurtosis_OneExtremeObservation_ReactsMaterially()
        {
            // Symmetric 9-point baseline (G2 = -0.687151504962436),
            // then the same nine points plus one extreme observation
            // (50.0): G2 = 9.934501826659446.
            double baseline = Kurtosis(
                -1.281552, -0.841621, -0.524401, -0.253347,
                 0.0,       0.253347,  0.524401,  0.841621, 1.281552);
            Assert.Equal(-0.687151504962436, baseline, 11);

            double withOutlier = Kurtosis(
                -1.281552, -0.841621, -0.524401, -0.253347,
                 0.0,       0.253347,  0.524401,  0.841621, 1.281552, 50.0);
            Assert.Equal(9.934501826659446, withOutlier, 10);

            Assert.True(withOutlier - baseline > 8.0,
                $"Kurtosis did not react materially: {baseline} -> {withOutlier}");
        }
    }
}

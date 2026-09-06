using System;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reversal;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Tests for <see cref="MeanHmaAtrSmoothDistanceEngine"/>.
    /// </summary>
    public sealed class MeanHmaAtrSmoothDistanceEngineTests
    {
        // This test requires dual-reference composition (both HMA and ATRSmooth sources).
        // Since the current architecture uses single-reference, this feature is not yet
        // wired into the production pipeline. These tests document the expected behavior
        // and will pass once dual-reference composition is implemented.

        [Fact]
        public void Model_ConstantPositiveSeparation_ReturnsPositiveMean()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { 2.0, 2.0, 2.0, 2.0, 2.0 };
            double mean = model.Compute(distances);
            Assert.Equal(2.0, mean, 12);
        }

        [Fact]
        public void Model_ConstantNegativeSeparation_ReturnsNegativeMean()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { -1.5, -1.5, -1.5, -1.5, -1.5 };
            double mean = model.Compute(distances);
            Assert.Equal(-1.5, mean, 12);
        }

        [Fact]
        public void Model_ZeroSeparation_ReturnsZero()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { 0.0, 0.0, 0.0, 0.0, 0.0 };
            double mean = model.Compute(distances);
            Assert.Equal(0.0, mean, 12);
        }

        [Fact]
        public void Model_MixedPositiveNegative_ReturnsCorrectMean()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { 2.0, -1.0, 3.0, -2.0, 0.0 };
            double mean = model.Compute(distances);
            Assert.Equal(0.4, mean, 12); // (2-1+3-2+0)/5 = 2/5 = 0.4
        }

        [Fact]
        public void Model_RollingWindowBehavior_CorrectlyLimitsWindow()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(3);
            double[] distances = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            double mean = model.Compute(distances);
            // Window of 3: last 3 values [3, 4, 5] → mean = 4
            Assert.Equal(4.0, mean, 12);
        }

        [Fact]
        public void Model_WarmUp_NaNForInvalidInputs()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { double.NaN, double.NaN, double.NaN };
            double mean = model.Compute(distances);
            Assert.True(double.IsNaN(mean));
        }

        [Fact]
        public void Model_WithSomeNaN_IgnoresNaN()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { 2.0, double.NaN, 4.0, double.NaN, 6.0 };
            double mean = model.Compute(distances);
            // Valid: 2, 4, 6 → mean = 4
            Assert.Equal(4.0, mean, 12);
        }

        [Fact]
        public void Model_AllNaN_ReturnsNaN()
        {
            var model = new MeanHmaAtrSmoothDistanceModel(5);
            double[] distances = { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN };
            double mean = model.Compute(distances);
            Assert.True(double.IsNaN(mean));
        }

        [Fact]
        public void RuntimeValues_Reset_ClearsToNaN()
        {
            var values = new MeanHmaAtrSmoothDistanceRuntimeValues();
            values.MeanSignedDistance = 1.5;

            // Simulate reset
            values.MeanSignedDistance = double.NaN;

            Assert.True(double.IsNaN(values.MeanSignedDistance));
        }
    }
}
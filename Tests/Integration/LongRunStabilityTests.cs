using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class LongRunStabilityTests
    {
        [Fact]
        public void Update_WithLargeDataset_RemainsStable()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------

            const int NumberOfBars = 100_000;

            EngineConfiguration configuration =
                TestConfigurationFactory.CreateLargeDataset(NumberOfBars);

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData = configuration.MarketData;

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------

            while (marketData.MoveNext())
            {
                engine.Update();
            }

            // -------------------------------------------------------------
            // Assert - Core Objects
            // -------------------------------------------------------------

            Assert.NotNull(engine.Values);

            Assert.NotNull(engine.Values.Reference);
            Assert.NotNull(engine.Values.Distance);
            Assert.NotNull(engine.Values.Scale);
            Assert.NotNull(engine.Values.Normalization);
            Assert.NotNull(engine.Values.Statistics);

            // -------------------------------------------------------------
            // Assert - Distance
            // -------------------------------------------------------------

            NumericAssert.IsFinite(
                engine.Values.Distance.DirectionalExtension);

            NumericAssert.IsFinite(
                engine.Values.Distance.AbsoluteExtension);

            // -------------------------------------------------------------
            // Assert - Scale
            // -------------------------------------------------------------

            NumericAssert.IsFinite(
                engine.Values.Scale.Scale);

            Assert.True(
                engine.Values.Scale.Scale > 0);

            // -------------------------------------------------------------
            // Assert - Normalization
            // -------------------------------------------------------------

            NumericAssert.IsFinite(
                engine.Values.Normalization.NormalizedMeasurement);

            // -------------------------------------------------------------
            // Assert - Statistics
            // -------------------------------------------------------------

            NumericAssert.IsFinite(
                engine.Values.Statistics.Location.Mean);

            NumericAssert.IsFinite(
                engine.Values.Statistics.Location.Median);

            NumericAssert.IsFinite(
                engine.Values.Statistics.Dispersion.Variance);

            NumericAssert.IsFinite(
                engine.Values.Statistics.Dispersion.StandardDeviation);

            NumericAssert.IsFinite(
                engine.Values.Statistics.Dispersion.MedianAbsoluteDeviation);

            NumericAssert.IsFinite(
                engine.Values.Statistics.Range.Minimum);

            NumericAssert.IsFinite(
                engine.Values.Statistics.Range.Maximum);

            // -------------------------------------------------------------
            // Assert - Rolling Window
            // -------------------------------------------------------------

            Assert.True(
                engine.Values.Statistics.ObservationCount > 0);

            Assert.True(
                engine.Values.Statistics.ObservationCount <=
                configuration.Options.StatisticsWindowSize);
        }
    }
}

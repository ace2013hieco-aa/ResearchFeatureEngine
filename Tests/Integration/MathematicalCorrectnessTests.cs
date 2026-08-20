using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class MathematicalCorrectnessTests
    {
        [Fact]
        public void Update_WithKnownDataset_ProducesExpectedResults()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------

            EngineConfiguration configuration =
                TestConfigurationFactory.CreateKnownDataset();

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData = configuration.MarketData;

            ExpectedResults expected =
                ExpectedResultsFactory.CreateKnownDataset();

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------

            while (marketData.MoveNext())
            {
                engine.Update();
            }

            // -------------------------------------------------------------
            // Assert - Reference
            // -------------------------------------------------------------

            Assert.Equal(
                expected.Reference.Price,
                engine.Values.Reference.Price,
                8);

            // -------------------------------------------------------------
            // Assert - Distance
            // -------------------------------------------------------------

            Assert.Equal(
                expected.Distance.DirectionalExtension,
                engine.Values.Distance.DirectionalExtension,
                8);

            Assert.Equal(
                expected.Distance.AbsoluteExtension,
                engine.Values.Distance.AbsoluteExtension,
                8);

            // -------------------------------------------------------------
            // Assert - Scale
            // -------------------------------------------------------------

            Assert.Equal(
                expected.Scale.Scale,
                engine.Values.Scale.Scale,
                8);

            // -------------------------------------------------------------
            // Assert - Normalization
            // -------------------------------------------------------------

            Assert.Equal(
                expected.Normalization.NormalizedMeasurement,
                engine.Values.Normalization.NormalizedMeasurement,
                8);

            // -------------------------------------------------------------
            // Assert - Statistics
            // -------------------------------------------------------------

            Assert.Equal(
                expected.Statistics.Location.Mean,
                engine.Values.Statistics.Location.Mean,
                8);

            Assert.Equal(
                expected.Statistics.Location.Median,
                engine.Values.Statistics.Location.Median,
                8);

            Assert.Equal(
                expected.Statistics.Dispersion.Variance,
                engine.Values.Statistics.Dispersion.Variance,
                8);

            Assert.Equal(
                expected.Statistics.Dispersion.StandardDeviation,
                engine.Values.Statistics.Dispersion.StandardDeviation,
                8);

            Assert.Equal(
                expected.Statistics.Dispersion.MedianAbsoluteDeviation,
                engine.Values.Statistics.Dispersion.MedianAbsoluteDeviation,
                8);

            Assert.Equal(
                expected.Statistics.Range.Minimum,
                engine.Values.Statistics.Range.Minimum,
                8);

            Assert.Equal(
                expected.Statistics.Range.Maximum,
                engine.Values.Statistics.Range.Maximum,
                8);
        }
    }
}

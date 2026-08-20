using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class DeterminismTests
    {
        [Fact]
        public void Update_WithIdenticalInput_ProducesIdenticalOutput()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------

            EngineConfiguration configurationA =
                TestConfigurationFactory.CreateKnownDataset();

            EngineConfiguration configurationB =
                TestConfigurationFactory.CreateKnownDataset();

            ResearchFeatureEngine engineA =
                new ResearchFeatureEngineBuilder(configurationA)
                    .Build();

            ResearchFeatureEngine engineB =
                new ResearchFeatureEngineBuilder(configurationB)
                    .Build();

            IMarketData marketDataA = configurationA.MarketData;
            IMarketData marketDataB = configurationB.MarketData;

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------

            while (marketDataA.MoveNext() &&
                   marketDataB.MoveNext())
            {
                engineA.Update();
                engineB.Update();
            }

            // -------------------------------------------------------------
            // Assert - Reference
            // -------------------------------------------------------------

            Assert.Equal(
                engineA.Values.Reference.Price,
                engineB.Values.Reference.Price,
                8);

            // -------------------------------------------------------------
            // Assert - Distance
            // -------------------------------------------------------------

            Assert.Equal(
                engineA.Values.Distance.DirectionalExtension,
                engineB.Values.Distance.DirectionalExtension,
                8);

            Assert.Equal(
                engineA.Values.Distance.AbsoluteExtension,
                engineB.Values.Distance.AbsoluteExtension,
                8);

            // -------------------------------------------------------------
            // Assert - Scale
            // -------------------------------------------------------------

            Assert.Equal(
                engineA.Values.Scale.Scale,
                engineB.Values.Scale.Scale,
                8);

            // -------------------------------------------------------------
            // Assert - Normalization
            // -------------------------------------------------------------

            Assert.Equal(
                engineA.Values.Normalization.NormalizedMeasurement,
                engineB.Values.Normalization.NormalizedMeasurement,
                8);

            // -------------------------------------------------------------
            // Assert - Statistics
            // -------------------------------------------------------------

            Assert.Equal(
                engineA.Values.Statistics.Location.Mean,
                engineB.Values.Statistics.Location.Mean,
                8);

            Assert.Equal(
                engineA.Values.Statistics.Location.Median,
                engineB.Values.Statistics.Location.Median,
                8);

            Assert.Equal(
                engineA.Values.Statistics.Dispersion.Variance,
                engineB.Values.Statistics.Dispersion.Variance,
                8);

            Assert.Equal(
                engineA.Values.Statistics.Dispersion.StandardDeviation,
                engineB.Values.Statistics.Dispersion.StandardDeviation,
                8);

            Assert.Equal(
                engineA.Values.Statistics.Dispersion.MedianAbsoluteDeviation,
                engineB.Values.Statistics.Dispersion.MedianAbsoluteDeviation,
                8);

            Assert.Equal(
                engineA.Values.Statistics.Range.Minimum,
                engineB.Values.Statistics.Range.Minimum,
                8);

            Assert.Equal(
                engineA.Values.Statistics.Range.Maximum,
                engineB.Values.Statistics.Range.Maximum,
                8);
        }
    }
}

using System;
using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class PipelineExecutionTests
    {
        [Fact]
        public void Update_WithValidConfiguration_ExecutesEntirePipeline()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------

            EngineConfiguration configuration =
                TestConfigurationFactory.Create();

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
            // Assert
            // -------------------------------------------------------------

            Assert.NotNull(engine.Values);

            // Reference
            Assert.NotNull(engine.Values.Reference);

            // Distance
            Assert.NotNull(engine.Values.Distance);

            // Scale
            Assert.NotNull(engine.Values.Scale);

            // Normalization
            Assert.NotNull(engine.Values.Normalization);

            // Statistics
            Assert.NotNull(engine.Values.Statistics);

            // -------------------------------------------------------------
            // Numerical Sanity
            // -------------------------------------------------------------

            Assert.False(double.IsNaN(
                engine.Values.Distance.DirectionalExtension));

            Assert.False(double.IsInfinity(
                engine.Values.Distance.DirectionalExtension));

            Assert.False(double.IsNaN(
                engine.Values.Distance.AbsoluteExtension));

            Assert.False(double.IsInfinity(
                engine.Values.Distance.AbsoluteExtension));

            Assert.False(double.IsNaN(
                engine.Values.Scale.Scale));

            Assert.False(double.IsInfinity(
                engine.Values.Scale.Scale));

            Assert.False(double.IsNaN(
                engine.Values.Normalization.NormalizedMeasurement));

            Assert.False(double.IsInfinity(
                engine.Values.Normalization.NormalizedMeasurement));

            Assert.False(double.IsNaN(
                engine.Values.Statistics.Location.Mean));

            Assert.False(double.IsInfinity(
                engine.Values.Statistics.Location.Mean));
        }
    }
}

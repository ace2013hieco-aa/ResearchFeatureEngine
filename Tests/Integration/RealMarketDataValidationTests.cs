using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class RealMarketDataValidationTests
    {
        [Fact]
        public void Update_WithRealMarketData_ProducesStableAndValidResults()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------

            EngineConfiguration configuration =
                TestConfigurationFactory.CreateFromHistoricalCsv(
                    @"TestData\EURUSD_M1_10000.csv");

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData =
                configuration.MarketData;

            int processedBars = 0;

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------

            while (marketData.MoveNext())
            {
                engine.Update();

                processedBars++;

                // ---------------------------------------------------------
                // Periodic Validation
                // ---------------------------------------------------------

                if (processedBars % 1000 == 0)
                {
                    NumericAssert.IsFinite(
                        engine.Values.Reference.Price);

                    NumericAssert.IsFinite(
                        engine.Values.Distance.DirectionalExtension);

                    NumericAssert.IsFinite(
                        engine.Values.Distance.AbsoluteExtension);

                    NumericAssert.IsFinite(
                        engine.Values.Scale.Scale);

                    NumericAssert.IsFinite(
                        engine.Values.Normalization.NormalizedMeasurement);

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

                    Assert.True(
                        engine.Values.Statistics.ObservationCount <=
                        configuration.Options.StatisticsWindowSize);
                }
            }

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------

            Assert.Equal(10_000, processedBars);

            Assert.NotNull(engine.Values);

            Assert.NotNull(engine.Values.Reference);
            Assert.NotNull(engine.Values.Distance);
            Assert.NotNull(engine.Values.Scale);
            Assert.NotNull(engine.Values.Normalization);
            Assert.NotNull(engine.Values.Statistics);

            NumericAssert.IsFinite(
                engine.Values.Reference.Price);

            NumericAssert.IsFinite(
                engine.Values.Distance.DirectionalExtension);

            NumericAssert.IsFinite(
                engine.Values.Distance.AbsoluteExtension);

            NumericAssert.IsFinite(
                engine.Values.Scale.Scale);

            NumericAssert.IsFinite(
                engine.Values.Normalization.NormalizedMeasurement);

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

            Assert.True(
                engine.Values.Statistics.ObservationCount <=
                configuration.Options.StatisticsWindowSize);
        }
    }
}

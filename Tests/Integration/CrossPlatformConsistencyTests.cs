using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class CrossPlatformConsistencyTests
    {
        [Fact]
        public void Update_WithIdenticalData_ProducesIdenticalResultsAcrossPlatforms()
        {
            // -------------------------------------------------------------
            // Arrange
            // -------------------------------------------------------------

            EngineConfiguration cTraderConfiguration =
                TestConfigurationFactory.CreateFromHistoricalCsv(
                    @"TestData\EURUSD_M1_10000.csv",
                    AdapterType.CTrader);

            EngineConfiguration pythonConfiguration =
                TestConfigurationFactory.CreateFromHistoricalCsv(
                    @"TestData\EURUSD_M1_10000.csv",
                    AdapterType.Python);

            ResearchFeatureEngine cTraderEngine =
                new ResearchFeatureEngineBuilder(cTraderConfiguration)
                    .Build();

            ResearchFeatureEngine pythonEngine =
                new ResearchFeatureEngineBuilder(pythonConfiguration)
                    .Build();

            IMarketData cTraderData =
                cTraderConfiguration.MarketData;

            IMarketData pythonData =
                pythonConfiguration.MarketData;

            int processedBars = 0;

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------

            while (cTraderData.MoveNext() &&
                   pythonData.MoveNext())
            {
                cTraderEngine.Update();
                pythonEngine.Update();

                processedBars++;

                // ---------------------------------------------------------
                // Periodic Comparison
                // ---------------------------------------------------------

                if (processedBars % 1000 == 0)
                {
                    Assert.Equal(
                        cTraderEngine.Values.Reference.Price,
                        pythonEngine.Values.Reference.Price,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Distance.DirectionalExtension,
                        pythonEngine.Values.Distance.DirectionalExtension,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Distance.AbsoluteExtension,
                        pythonEngine.Values.Distance.AbsoluteExtension,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Scale.Scale,
                        pythonEngine.Values.Scale.Scale,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Normalization.NormalizedMeasurement,
                        pythonEngine.Values.Normalization.NormalizedMeasurement,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Location.Mean,
                        pythonEngine.Values.Statistics.Location.Mean,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Location.Median,
                        pythonEngine.Values.Statistics.Location.Median,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Dispersion.Variance,
                        pythonEngine.Values.Statistics.Dispersion.Variance,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Dispersion.StandardDeviation,
                        pythonEngine.Values.Statistics.Dispersion.StandardDeviation,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Dispersion.MedianAbsoluteDeviation,
                        pythonEngine.Values.Statistics.Dispersion.MedianAbsoluteDeviation,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Range.Minimum,
                        pythonEngine.Values.Statistics.Range.Minimum,
                        8);

                    Assert.Equal(
                        cTraderEngine.Values.Statistics.Range.Maximum,
                        pythonEngine.Values.Statistics.Range.Maximum,
                        8);
                }
            }

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------

            Assert.Equal(10_000, processedBars);

            EngineValuesAssert.Equal(
                cTraderEngine.Values,
                pythonEngine.Values,
                precision: 8);
        }
    }
}

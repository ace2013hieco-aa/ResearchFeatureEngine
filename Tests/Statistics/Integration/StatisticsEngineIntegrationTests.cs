using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Statistics;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Statistics.Integration
{
    public sealed partial class StatisticsEngineIntegrationTests
    {
        [Fact]
        public void Update_ComputesAndPublishes_AllRegisteredModels()
        {
            // Arrange

            var context = TestEngineContext.Create();

            var window = new StatisticsWindow(5);

            window.Add(10.0);
            window.Add(20.0);
            window.Add(30.0);

            var models = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new MinimumModel(),
                new MaximumModel(),
                new VarianceModel(),
                new StandardDeviationModel(),
                new MedianAbsoluteDeviationModel()
            };

            var engine = new StatisticsEngine(
                context,
                window,
                models);

            // Act

            engine.Update();

            // Assert

            Assert.Equal(20.0,
                context.Values!.Statistics.Location.Mean, 10);

            Assert.Equal(20.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(30.0,
                context.Values.Statistics.Range.Maximum, 10);

            Assert.Equal(100.0,
                context.Values.Statistics.Dispersion.Variance, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Dispersion.StandardDeviation, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Dispersion.MedianAbsoluteDeviation, 10);
        }

        [Fact]
        public void Update_InvalidInput_ThrowsAndDoesNotPublish()
        {
            // Arrange

            var context = TestEngineContext.Create();

            var window = new StatisticsWindow(5);

            // Empty window -> validation should fail

            var models = new List<IStatisticModel>
            {
                new MeanModel()
            };

            var engine = new StatisticsEngine(
                context,
                window,
                models);

            // Act / Assert

            Assert.Throws<InvalidOperationException>(
                () => engine.Update());

            // Runtime values must remain unchanged

            Assert.Equal(
                0.0,
                context.Values!.Statistics.Location.Mean);

            Assert.Equal(
                0.0,
                context.Values.Statistics.Location.Median);

            Assert.Equal(
                0.0,
                context.Values.Statistics.Range.Minimum);

            Assert.Equal(
                0.0,
                context.Values.Statistics.Range.Maximum);

            Assert.Equal(
                0.0,
                context.Values.Statistics.Dispersion.Variance);

            Assert.Equal(
                0.0,
                context.Values.Statistics.Dispersion.StandardDeviation);

            Assert.Equal(
                0.0,
                context.Values.Statistics.Dispersion.MedianAbsoluteDeviation);
        }

        [Fact]
        public void Update_RollingWindow_RecomputesStatistics()
        {
            // Arrange

            var context = TestEngineContext.Create();

            var marketData = (TestMarketData)context.MarketData;
            marketData.Close = 40.0;

            var window = new StatisticsWindow(3);

            window.Add(10.0);
            window.Add(20.0);
            window.Add(30.0);

            var models = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new MinimumModel(),
                new MaximumModel()
            };

            var engine = new StatisticsEngine(
                context,
                window,
                models);

            // Act

            engine.Update();

            // Window should now contain:
            //
            // 20
            // 30
            // 40

            // Assert

            Assert.Equal(
                30.0,
                context.Values.Statistics.Location.Mean,
                10);

            Assert.Equal(
                30.0,
                context.Values.Statistics.Location.Median,
                10);

            Assert.Equal(
                20.0,
                context.Values.Statistics.Range.Minimum,
                10);

            Assert.Equal(
                40.0,
                context.Values.Statistics.Range.Maximum,
                10);
        }

        [Fact]
        public void Update_MultipleCalls_RecomputesStatisticsCorrectly()
        {
            // Arrange

            var context = TestEngineContext.Create();
            var marketData = (TestMarketData)context.MarketData;

            var window = new StatisticsWindow(3);

            var models = new List<IStatisticModel>
            {
                new MeanModel(),
                new MedianModel(),
                new MinimumModel(),
                new MaximumModel()
            };

            var engine = new StatisticsEngine(
                context,
                window,
                models);

            // Update #1

            marketData.Close = 10.0;
            engine.Update();

            Assert.Equal(10.0,
                context.Values.Statistics.Location.Mean, 10);

            // Update #2

            marketData.Close = 20.0;
            engine.Update();

            Assert.Equal(15.0,
                context.Values.Statistics.Location.Mean, 10);

            // Update #3

            marketData.Close = 30.0;
            engine.Update();

            Assert.Equal(20.0,
                context.Values.Statistics.Location.Mean, 10);

            Assert.Equal(20.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(30.0,
                context.Values.Statistics.Range.Maximum, 10);

            // Update #4 (window rolls)

            marketData.Close = 40.0;
            engine.Update();

            // Window now contains:
            // 20, 30, 40

            Assert.Equal(30.0,
                context.Values.Statistics.Location.Mean, 10);

            Assert.Equal(30.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(20.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(40.0,
                context.Values.Statistics.Range.Maximum, 10);
        }
    }
}

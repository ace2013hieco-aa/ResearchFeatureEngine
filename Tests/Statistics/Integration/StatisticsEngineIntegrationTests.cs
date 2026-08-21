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
            // The statistics window holds CLOSED bars and the live
            // bar's close is appended for computation. Here the live
            // bar's close is 40, so the observations are
            // [10, 20, 30] (closed) + [40] (live) = [10, 20, 30, 40].
            //
            // Mean      = 25
            // Median    = 25 (average of 20 and 30)
            // Min/Max   = 10 / 40
            // Variance  = 166.6667 (sample)
            // StdDev    = 12.9099
            // MAD       = 7.5 (median 25; |10-25|,|20-25|,... = 15,5,5,15; median 7.5... actually 5 and 5 -> 5)
            //
            // Use TestEngineContext with a close series so the live
            // bar's close resolves to a known value.

            var context = TestEngineContext.Create(40.0);

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

            // Assert — observations [10, 20, 30, 40].

            Assert.Equal(25.0,
                context.Values!.Statistics.Location.Mean, 10);

            // Median of [10,20,30,40] = (20+30)/2 = 25.
            Assert.Equal(25.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(40.0,
                context.Values.Statistics.Range.Maximum, 10);

            // Sample variance of [10,20,30,40]: mean 25,
            // sum sq dev = 225+25+25+225 = 500; /3 = 166.6667.
            Assert.Equal(166.66666666666667,
                context.Values.Statistics.Dispersion.Variance, 4);

            Assert.Equal(12.90994448909018,
                context.Values.Statistics.Dispersion.StandardDeviation, 6);

            // MAD: median 25, deviations |10-25|,|20-25|,|30-25|,|40-25|
            //      = 15,5,5,15; median = (5+15)/2 = 10.
            Assert.Equal(10.0,
                context.Values.Statistics.Dispersion.MedianAbsoluteDeviation, 10);
        }

        [Fact]
        public void Update_FirstBar_PublishesMeanOverSingleLiveClose()
        {
            // With no committed closed bars yet, the statistics are
            // computed over just the live bar's close. Mean equals
            // that close; std dev (needs >= 2 observations) is
            // skipped and left at its default.
            var context = TestEngineContext.Create(42.0);

            var window = new StatisticsWindow(5);

            var models = new List<IStatisticModel>
            {
                new MeanModel(),
                new StandardDeviationModel()
            };

            var engine = new StatisticsEngine(
                context,
                window,
                models);

            engine.Update();

            Assert.Equal(42.0,
                context.Values!.Statistics.Location.Mean, 10);

            // Only one observation -> below StdDev's minimum (2).
            Assert.Equal(0.0,
                context.Values.Statistics.Dispersion.StandardDeviation);

            Assert.Equal(1,
                context.Values.Statistics.ObservationCount);
        }

        [Fact]
        public void Update_WithRolledWindow_RecomputesStatistics()
        {
            // Current-bar-inclusive semantics. Window (closed bars)
            // starts as [10, 20, 30] (capacity 3, full). The live
            // bar's close is appended on every tick.
            //
            // Close series: [40, 50, 60, 70].
            //
            //   SetIndex(0) + Update():
            //     lastSeen was -1 -> no commit. liveClose = 40.
            //     Observations: [10, 20, 30, 40]. Mean = 25,
            //     Min = 10, Max = 40.
            //   SetIndex(1) + Update():
            //     commit liveClose (40) -> window rolls: [20, 30, 40].
            //     liveClose = 50. Observations: [20, 30, 40, 50].
            //     Mean = 35, Min = 20, Max = 50.
            //   SetIndex(2) + Update():
            //     commit liveClose (50) -> window rolls: [30, 40, 50].
            //     liveClose = 60. Observations: [30, 40, 50, 60].
            //     Mean = 45, Min = 30, Max = 60.
            //
            // The live bar is included in every computation, matching
            // the other pipeline stages and the reference indicator.

            var context = TestEngineContext.Create(40.0, 50.0, 60.0, 70.0);

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

            // Tick 1: live bar's close is 40 (appended).
            context.SetIndex(0);
            engine.Update();

            Assert.Equal(
                25.0,
                context.Values.Statistics.Location.Mean,
                10);

            // Median of [10, 20, 30, 40] = (20+30)/2 = 25.
            Assert.Equal(
                25.0,
                context.Values.Statistics.Location.Median,
                10);

            Assert.Equal(
                10.0,
                context.Values.Statistics.Range.Minimum,
                10);

            Assert.Equal(
                40.0,
                context.Values.Statistics.Range.Maximum,
                10);

            // Tick 2: commit 40 (window rolls to [20, 30, 40]),
            // live close = 50. Observations [20, 30, 40, 50].
            context.SetIndex(1);
            engine.Update();

            Assert.Equal(
                35.0,
                context.Values.Statistics.Location.Mean,
                10);

            // Median of [20, 30, 40, 50] = (30+40)/2 = 35.
            Assert.Equal(
                35.0,
                context.Values.Statistics.Location.Median,
                10);

            Assert.Equal(
                20.0,
                context.Values.Statistics.Range.Minimum,
                10);

            Assert.Equal(
                50.0,
                context.Values.Statistics.Range.Maximum,
                10);

            // Tick 3: commit 50 (window rolls to [30, 40, 50]),
            // live close = 60. Observations [30, 40, 50, 60].
            context.SetIndex(2);
            engine.Update();

            Assert.Equal(
                45.0,
                context.Values.Statistics.Location.Mean,
                10);

            Assert.Equal(
                30.0,
                context.Values.Statistics.Range.Minimum,
                10);

            Assert.Equal(
                60.0,
                context.Values.Statistics.Range.Maximum,
                10);
        }

        [Fact]
        public void Update_MultipleCalls_RecomputesStatisticsCorrectly()
        {
            // Current-bar-inclusive semantics across a sequence of
            // distinct closes.
            //
            // Trace (window capacity 3, close series [10, 20, 30, 40]):
            //
            //   SetIndex(0) + Update():
            //     lastSeen was -1, no commit. liveClose = 10.
            //     Observations: [10]. Mean = 10. (single obs)
            //   SetIndex(1) + Update():
            //     commit 10. liveClose = 20.
            //     Observations: [10, 20]. Mean = 15.
            //   SetIndex(2) + Update():
            //     commit 20. liveClose = 30.
            //     Observations: [10, 20, 30]. Mean = 20.
            //   SetIndex(3) + Update():
            //     commit 30. liveClose = 40.
            //     Observations: [10, 20, 30, 40]. Mean = 25.
            //
            // The live bar is included in every computation.

            var context = TestEngineContext.Create(10.0, 20.0, 30.0, 40.0);

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

            // Update #1 — live bar (10) appended; no closed bars yet.
            context.SetIndex(0);
            engine.Update();

            Assert.Equal(10.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(1,
                context.Values.Statistics.ObservationCount);

            // Update #2 — commit 10; live close = 20.
            context.SetIndex(1);
            engine.Update();

            Assert.Equal(15.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(2,
                context.Values.Statistics.ObservationCount);

            // Median of [10, 20] is (10+20)/2 = 15.
            Assert.Equal(15.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(20.0,
                context.Values.Statistics.Range.Maximum, 10);

            // Update #3 — commit 20; live close = 30.
            context.SetIndex(2);
            engine.Update();

            Assert.Equal(20.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(3,
                context.Values.Statistics.ObservationCount);

            // Median of [10, 20, 30] (odd count) is the middle: 20.
            Assert.Equal(20.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(30.0,
                context.Values.Statistics.Range.Maximum, 10);

            // Update #4 — commit 30 (window full, rolls oldest out);
            // live close = 40. Observations [10, 20, 30, 40].
            context.SetIndex(3);
            engine.Update();

            Assert.Equal(25.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(4,
                context.Values.Statistics.ObservationCount);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(40.0,
                context.Values.Statistics.Range.Maximum, 10);
        }
    }
}

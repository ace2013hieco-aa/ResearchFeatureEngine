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
        public void Update_EmptyWindow_DoesNotThrow_AndLeavesValuesAtDefault()
        {
            // On the very first bar of a live stream there are no
            // closed bars yet, so the rolling window is empty. The
            // engine must NOT throw in that case. It gracefully
            // leaves all statistic values at their default (0.0)
            // and reports an observation count of zero.
            //
            // The validator's "needs at least one observation"
            // invariant is preserved for direct callers; the engine
            // just refuses to feed it an empty input.

            var context = TestEngineContext.Create();

            var window = new StatisticsWindow(5);

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

            // Act — must not throw

            engine.Update();

            // Assert — values stay at default

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

            Assert.Equal(
                0,
                context.Values.Statistics.ObservationCount);
        }

        [Fact]
        public void Update_WithRolledWindow_RecomputesStatistics()
        {
            // Verifies the closed-bars-only rolling semantics.
            //
            // Window is pre-populated with [10, 20, 30] (capacity 3,
            // full). Each Update() pulls a new close from a
            // pre-built 4-element series [40, 50, 60, 70]:
            //
            //   SetIndex(0) + Update():
            //                     lastSeen was -1, so no commit.
            //                     Window stays [10, 20, 30].
            //                     Mean=20, Min=10, Max=30.
            //   SetIndex(1) + Update():
            //                     commit 40, drop oldest (10).
            //                     Window: [20, 30, 40]. Mean=30,
            //                     Min=20, Max=40.
            //   SetIndex(2) + Update():
            //                     commit 50, drop oldest (20).
            //                     Window: [30, 40, 50]. Mean=40,
            //                     Min=30, Max=50.
            //
            // The key property under test: the window reflects
            // CLOSED bars, not the live (still-forming) bar.
            //
            // Note: StatisticsEngine.Update() does not auto-advance
            // the context index. The wrapper ResearchFeatureEngine
            // does that, but here we're driving the engine directly
            // to keep the unit test focused. SetIndex() simulates the
            // progression of bars between calls.

            // Pre-built close series: index 0..3.
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

            // Tick 1: live bar's close is 40 (held aside).
            context.SetIndex(0);
            engine.Update();

            Assert.Equal(
                20.0,
                context.Values.Statistics.Location.Mean,
                10);

            Assert.Equal(
                20.0,
                context.Values.Statistics.Location.Median,
                10);

            Assert.Equal(
                10.0,
                context.Values.Statistics.Range.Minimum,
                10);

            Assert.Equal(
                30.0,
                context.Values.Statistics.Range.Maximum,
                10);

            // Tick 2: live bar's close is 50 (held aside);
            // previous live close (40) is committed, oldest (10)
            // rolls out.
            context.SetIndex(1);
            engine.Update();

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

            // Tick 3: live bar's close is 60 (held aside);
            // previous live close (50) is committed, oldest (20)
            // rolls out.
            context.SetIndex(2);
            engine.Update();

            Assert.Equal(
                40.0,
                context.Values.Statistics.Location.Mean,
                10);

            Assert.Equal(
                30.0,
                context.Values.Statistics.Range.Minimum,
                10);

            Assert.Equal(
                50.0,
                context.Values.Statistics.Range.Maximum,
                10);
        }

        [Fact]
        public void Update_MultipleCalls_RecomputesStatisticsCorrectly()
        {
            // Verifies the closed-bars-only semantics across a
            // sequence of distinct closes.
            //
            // Trace (window capacity 3, pre-built close series
            // [10, 20, 30, 40]):
            //
            //   SetIndex(0) + Update():
            //                     lastSeen was -1, no commit.
            //                     Window: []. Mean=0.
            //   SetIndex(1) + Update():
            //                     commit 10. liveClose=20.
            //                     Window: [10]. Mean=10.
            //   SetIndex(2) + Update():
            //                     commit 20. liveClose=30.
            //                     Window: [10, 20]. Mean=15.
            //   SetIndex(3) + Update():
            //                     commit 30. liveClose=40.
            //                     Window: [10, 20, 30]. Mean=20.
            //
            // Note: StatisticsEngine.Update() does not auto-advance
            // the context index. The wrapper ResearchFeatureEngine
            // does that, but here we're driving the engine directly
            // to keep the unit test focused. SetIndex() simulates
            // the progression of bars between calls.

            // Pre-built close series: index 0..3.
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

            // Update #1 — first close is held aside as the live bar.
            context.SetIndex(0);
            engine.Update();

            Assert.Equal(0.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(0,
                context.Values.Statistics.ObservationCount);

            // Update #2 — first close is committed to the window.
            context.SetIndex(1);
            engine.Update();

            Assert.Equal(10.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(1,
                context.Values.Statistics.ObservationCount);

            // Update #3 — second close is committed.
            context.SetIndex(2);
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

            // Update #4 — third close is committed, window full.
            context.SetIndex(3);
            engine.Update();

            Assert.Equal(20.0,
                context.Values.Statistics.Location.Mean, 10);
            Assert.Equal(3,
                context.Values.Statistics.ObservationCount);

            // Median of [10, 20, 30] (odd count) is the middle
            // value: 20.
            Assert.Equal(20.0,
                context.Values.Statistics.Location.Median, 10);

            Assert.Equal(10.0,
                context.Values.Statistics.Range.Minimum, 10);

            Assert.Equal(30.0,
                context.Values.Statistics.Range.Maximum, 10);
        }
    }
}

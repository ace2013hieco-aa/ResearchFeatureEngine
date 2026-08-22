using System;
using System.Collections.Generic;
using System.Linq;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Statistics
{
    /// <summary>
    /// Lifecycle guarantees for <see cref="StatisticsSource"/> return
    /// configurations. The transformation from closes to returns is
    /// stateless (recomputed from the committed close window on every
    /// update), so the engine must inherit every previously
    /// established lifecycle guarantee without any lifecycle change:
    ///   * same-index re-ticks are idempotent,
    ///   * sequential processing equals an independent clean run,
    ///   * re-tick-heavy sequences match clean final outputs,
    ///   * forward gaps and backward jumps recover through the same
    ///     reset-and-replay adapter policy used by the cTrader
    ///     indicator,
    ///   * a fresh pass reproduces the original run bit-for-bit,
    ///   * two identical runs produce identical outputs.
    ///
    /// Ground truth is produced by independent fresh engines walking
    /// the final data sequentially — never by replaying through the
    /// code path under test.
    /// </summary>
    public sealed class StatisticsSourceLifecycleTests
    {
        private const int BarCount = 80;
        private const int WindowSize = 10;

        // ---------------------------------------------------------
        // Mutable market data (OHLCV derived consistently).
        // ---------------------------------------------------------

        private sealed class LifecycleMarketData : IMarketData
        {
            public readonly double[] CloseValues;
            private readonly double[] _high;
            private readonly double[] _low;

            public LifecycleMarketData(double[] closes)
            {
                CloseValues = (double[])closes.Clone();
                _high = new double[CloseValues.Length];
                _low = new double[CloseValues.Length];
                for (int i = 0; i < CloseValues.Length; i++)
                {
                    _high[i] = CloseValues[i] + 0.3;
                    _low[i] = CloseValues[i] - 0.3;
                }
            }

            private sealed class ArraySeries : IPriceSeries
            {
                private readonly double[] _values;
                public ArraySeries(double[] values) => _values = values;
                public int Count => _values.Length;
                public double this[int index] => _values[index];
            }

            public IPriceSeries Open => new ArraySeries(CloseValues);
            public IPriceSeries High => new ArraySeries(_high);
            public IPriceSeries Low => new ArraySeries(_low);
            public IPriceSeries Close => new ArraySeries(CloseValues);
            public IPriceSeries Volume =>
                new ArraySeries(Enumerable.Repeat(100.0, CloseValues.Length).ToArray());
            public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
            public int Count => CloseValues.Length;
            public bool MoveNext() => false;
        }

        // ---------------------------------------------------------
        // Engine construction.
        // ---------------------------------------------------------

        private static ResearchFeatureEngine BuildEngine(
            LifecycleMarketData md, StatisticsSource source)
        {
            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                new ATRSmoothReferenceSource(
                    new ATRSmoothConfiguration(atrPeriod: 8,
                        atrMultiplier: 3.1,
                        smoothLength: 6)),
                new ATRScaleModel(7),
                new ScaleNormalizationModel(),
                new List<IStatisticModel>
                {
                    new MeanModel(),
                    new StandardDeviationModel(),
                    new MinimumModel(),
                    new MaximumModel(),
                    new MedianModel(),
                    new VarianceModel(),
                    new MedianAbsoluteDeviationModel(),
                    new RangeModel()
                },
                options: new EngineOptions
                {
                    StatisticsWindowSize = WindowSize,
                    StatisticsSource = source
                });

            return new ResearchFeatureEngineBuilder(configuration)
                .Build();
        }

        // ---------------------------------------------------------
        // Snapshots — all published statistics, compared bit-for-bit.
        // ---------------------------------------------------------

        private sealed class StatSnap
        {
            public double Mean;
            public double Median;
            public double Variance;
            public double StandardDeviation;
            public double Mad;
            public double Minimum;
            public double Maximum;
            public double Range;
            public int ObservationCount;

            public static StatSnap Capture(ResearchFeatureEngine engine)
            {
                var s = engine.Values.Statistics;
                return new StatSnap
                {
                    Mean = s.Location.Mean,
                    Median = s.Location.Median,
                    Variance = s.Dispersion.Variance,
                    StandardDeviation = s.Dispersion.StandardDeviation,
                    Mad = s.Dispersion.MedianAbsoluteDeviation,
                    Minimum = s.Range.Minimum,
                    Maximum = s.Range.Maximum,
                    Range = s.Range.Range,
                    ObservationCount = s.ObservationCount
                };
            }

            public void AssertEqual(StatSnap other, string what)
            {
                Assert.True(Mean == other.Mean, $"{what}: Mean");
                Assert.True(Median == other.Median, $"{what}: Median");
                Assert.True(Variance == other.Variance, $"{what}: Variance");
                Assert.True(StandardDeviation == other.StandardDeviation,
                    $"{what}: StdDev");
                Assert.True(Mad == other.Mad, $"{what}: MAD");
                Assert.True(Minimum == other.Minimum, $"{what}: Min");
                Assert.True(Maximum == other.Maximum, $"{what}: Max");
                Assert.True(Range == other.Range, $"{what}: Range");
                Assert.True(ObservationCount == other.ObservationCount,
                    $"{what}: ObservationCount");
            }
        }

        /// <summary>
        /// Independent clean reference run over identical final data:
        /// a brand-new engine walking bars 0..lastIndex sequentially.
        /// </summary>
        private static StatSnap[] GroundTruth(
            double[] closes, int lastIndex, StatisticsSource source)
        {
            var md = new LifecycleMarketData(closes);
            var engine = BuildEngine(md, source);

            var snaps = new StatSnap[lastIndex + 1];
            for (int i = 0; i <= lastIndex; i++)
            {
                engine.ProcessAt(i);
                snaps[i] = StatSnap.Capture(engine);
            }
            return snaps;
        }

        // ---------------------------------------------------------
        // Harness mirroring the indicator's Calculate policy exactly:
        // reset + replay on a fresh pass or any discontinuity.
        // ---------------------------------------------------------

        private sealed class LifecycleHarness
        {
            private readonly ResearchFeatureEngine _engine;
            public readonly Dictionary<int, StatSnap> Published = new();
            private int _lastProcessedIndex = -1;

            public LifecycleHarness(ResearchFeatureEngine engine) =>
                _engine = engine;

            public void Calculate(int index)
            {
                bool freshPass = index == 0 && _lastProcessedIndex >= 0;

                bool contiguous = _lastProcessedIndex < 0
                    ? index == 0
                    : index == _lastProcessedIndex
                      || index == _lastProcessedIndex + 1;

                if (freshPass || !contiguous)
                {
                    _engine.Pipeline.Reset();

                    for (int i = 0; i < index; i++)
                    {
                        _engine.ProcessAt(i);
                        Published[i] = StatSnap.Capture(_engine);
                    }
                }

                _engine.ProcessAt(index);
                Published[index] = StatSnap.Capture(_engine);

                _lastProcessedIndex = index;
            }
        }

        private static void Run(LifecycleHarness h, IEnumerable<int> sequence)
        {
            foreach (int index in sequence)
                h.Calculate(index);
        }

        private static void AssertAllMatchTruth(
            LifecycleHarness h, int maxVisited, StatSnap[] truth, string what)
        {
            for (int i = 0; i <= maxVisited; i++)
            {
                Assert.True(h.Published.ContainsKey(i),
                    $"{what}: bar {i} never published");
                truth[i].AssertEqual(h.Published[i], $"{what} bar {i}");
            }
        }

        public static IEnumerable<object[]> ReturnSources()
        {
            yield return new object[] { StatisticsSource.SimpleReturn };
            yield return new object[] { StatisticsSource.LogReturn };
        }

        private static double[] SyntheticCloses()
        {
            var rng = new Random(20260822);
            var closes = new double[BarCount];
            double price = 100.0;
            for (int i = 0; i < BarCount; i++)
            {
                price += (rng.NextDouble() - 0.5) * 2.0 + 0.04;
                closes[i] = price;
            }
            return closes;
        }

        // ---------------------------------------------------------
        // Section 1 — sequential equality.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void SequentialRun_MatchesIndependentCleanRun(
            StatisticsSource source)
        {
            double[] closes = SyntheticCloses();
            var md = new LifecycleMarketData(closes);
            var harness = new LifecycleHarness(BuildEngine(md, source));
            int last = BarCount - 1;

            Run(harness, Enumerable.Range(0, last + 1));

            AssertAllMatchTruth(harness, last,
                GroundTruth(closes, last, source), "sequential");
        }

        // ---------------------------------------------------------
        // Section 2 — re-tick idempotence.
        // ---------------------------------------------------------

        [Fact]
        public void SameIndexReTicks_AreIdempotent_SimpleReturn()
        {
            double[] closes = SyntheticCloses();
            var md = new LifecycleMarketData(closes);
            var engine = BuildEngine(md, StatisticsSource.SimpleReturn);

            for (int i = 0; i <= 20; i++)
                engine.ProcessAt(i);

            var first = StatSnap.Capture(engine);
            int count = first.ObservationCount;
            Assert.Equal(WindowSize, count);

            for (int tick = 0; tick < 25; tick++)
                engine.ProcessAt(20);

            StatSnap.Capture(engine)
                .AssertEqual(first, "after re-ticks");

            // Crucially the observation count must not grow: no bar
            // was committed twice.
            Assert.Equal(count, engine.Values.Statistics.ObservationCount);
        }

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void RetickHeavySequence_FinalOutputMatchesCleanRun(
            StatisticsSource source)
        {
            double[] closes = SyntheticCloses();
            var md = new LifecycleMarketData(closes);
            var harness = new LifecycleHarness(BuildEngine(md, source));

            var heavy = new List<int>();
            for (int bar = 0; bar <= 15; bar++)
                for (int repeat = 0; repeat < bar % 3 + 1; repeat++)
                    heavy.Add(bar);

            Run(harness, heavy);

            BuildCleanFinal(closes, 15, source)
                .AssertEqual(harness.Published[15], "retick-heavy final");
        }

        // ---------------------------------------------------------
        // Section 3 — discontinuity recovery.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void ForwardGap_RecoversThroughReplay(StatisticsSource source)
        {
            // 0,1,2 then a jump to 5: the adapter policy replays
            // 0..4 before processing 5.
            double[] closes = SyntheticCloses();
            var md = new LifecycleMarketData(closes);
            var harness = new LifecycleHarness(BuildEngine(md, source));

            Run(harness, new[] { 0, 1, 2, 5 });

            AssertAllMatchTruth(harness, 5,
                GroundTruth(closes, 5, source), "gap");
        }

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void BackwardJump_RecoversThroughReplay(StatisticsSource source)
        {
            double[] closes = SyntheticCloses();
            var md = new LifecycleMarketData(closes);
            var harness = new LifecycleHarness(BuildEngine(md, source));

            Run(harness, new[] { 0, 1, 2, 3, 2, 3, 4 });

            AssertAllMatchTruth(harness, 4,
                GroundTruth(closes, 4, source), "backward-jump");
        }

        // ---------------------------------------------------------
        // Section 4 — fresh pass equality.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void FreshPass_ReproducesOriginalRunBitForBit(
            StatisticsSource source)
        {
            double[] closes = SyntheticCloses();
            var md = new LifecycleMarketData(closes);
            var harness = new LifecycleHarness(BuildEngine(md, source));
            int last = BarCount - 1;

            Run(harness, Enumerable.Range(0, last + 1));
            StatSnap originalFinal = harness.Published[last];

            // Chart recalculation: Calculate(0) after prior activity
            // triggers reset and a full fresh pass.
            Run(harness, Enumerable.Range(0, last + 1));

            originalFinal.AssertEqual(harness.Published[last],
                "fresh-pass final");
        }

        // ---------------------------------------------------------
        // Section 5 — determinism.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void TwoIdenticalRuns_ProduceIdenticalOutputs(
            StatisticsSource source)
        {
            double[] closes = SyntheticCloses();

            var mdA = new LifecycleMarketData(closes);
            var mdB = new LifecycleMarketData(closes);
            var engineA = BuildEngine(mdA, source);
            var engineB = BuildEngine(mdB, source);

            for (int i = 0; i < BarCount; i++)
            {
                engineA.ProcessAt(i);
                engineB.ProcessAt(i);

                StatSnap.Capture(engineA)
                    .AssertEqual(StatSnap.Capture(engineB),
                        $"determinism bar {i}");
            }
        }

        // ---------------------------------------------------------
        // Helpers.
        // ---------------------------------------------------------

        private static StatSnap BuildCleanFinal(
            double[] closes, int lastIndex, StatisticsSource source)
        {
            var md = new LifecycleMarketData(closes);
            var engine = BuildEngine(md, source);
            for (int i = 0; i <= lastIndex; i++)
                engine.ProcessAt(i);
            return StatSnap.Capture(engine);
        }
    }
}

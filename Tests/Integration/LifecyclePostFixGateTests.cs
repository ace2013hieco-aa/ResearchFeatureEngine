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

namespace ResearchFeatureEngine.Tests.Integration
{
    /// <summary>
    /// Post-fix verification gate for the P1 adapter lifecycle hardening.
    ///
    /// Establishes the invariant:
    ///   For any supported cTrader call sequence, the final output for
    ///   bar N is determined only by the market-data history through N,
    ///   not by the previous call sequence used to reach N.
    ///
    /// Independent from CTraderLifecycleAuditTests on purpose: this file
    /// re-derives ground truth with its own helpers and adds
    ///   * recovery-frequency instrumentation (exactly-once semantics),
    ///   * work instrumentation (O(1) normal path vs O(N) recovery),
    ///   * look-ahead guarding during recovery,
    ///   * internal ATRSmooth runtime-state comparison after recovery.
    /// </summary>
    public sealed class LifecyclePostFixGateTests
    {
        // ---------------------------------------------------------
        // Instrumented market data: counts accesses, optionally
        // rejects any access above an allowed maximum index
        // (future-data guard).
        // ---------------------------------------------------------

        private sealed class GuardedSeries : IPriceSeries
        {
            private readonly Func<int, double> _get;
            private readonly int _count;
            private readonly Func<int> _allowMax;
            public long Accesses;

            public GuardedSeries(
                Func<int, double> get, int count, Func<int> allowMax)
            {
                _get = get;
                _count = count;
                _allowMax = allowMax;
            }

            public int Count => _count;

            public double this[int index]
            {
                get
                {
                    int max = _allowMax();
                    Assert.True(index <= max,
                        $"Future-data access: index {index} > allowed {max}");
                    Accesses++;
                    return _get(index);
                }
            }
        }

        /// <summary>
        /// All series are DERIVED dynamically from one close array, so
        /// <see cref="SetClose"/> keeps O/H/L/C mutually consistent —
        /// matching how a real bar's range moves with its close.
        /// </summary>
        private sealed class GateMarketData : IMarketData
        {
            public readonly double[] CloseValues;
            private readonly GuardedSeries _open;
            private readonly GuardedSeries _high;
            private readonly GuardedSeries _low;
            private readonly GuardedSeries _close;
            private readonly GuardedSeries _volume;
            private int _allowMax = int.MaxValue;

            public GateMarketData(double[] close)
            {
                CloseValues = close;
                _open = new GuardedSeries(i => CloseValues[i],
                    CloseValues.Length, () => _allowMax);
                _high = new GuardedSeries(i => CloseValues[i] + 0.3,
                    CloseValues.Length, () => _allowMax);
                _low = new GuardedSeries(i => CloseValues[i] - 0.3,
                    CloseValues.Length, () => _allowMax);
                _close = new GuardedSeries(i => CloseValues[i],
                    CloseValues.Length, () => _allowMax);
                _volume = new GuardedSeries(_ => 100.0,
                    CloseValues.Length, () => _allowMax);
            }

            public IPriceSeries Open => _open;
            public IPriceSeries High => _high;
            public IPriceSeries Low => _low;
            public IPriceSeries Close => _close;
            public IPriceSeries Volume => _volume;
            public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
            public int Count => CloseValues.Length;

            public bool MoveNext() => false;

            public void SetClose(int index, double value) =>
                CloseValues[index] = value;

            /// Total series accesses across all five series.
            public long Accesses =>
                _open.Accesses + _high.Accesses + _low.Accesses +
                _close.Accesses + _volume.Accesses;

            public void ResetAccesses() { /* counters are cumulative */ }

            public void SetAllowMax(int max) => _allowMax = max;
        }

        // ---------------------------------------------------------
        // Engine construction with exposed reference source so
        // internal ATRSmooth state can be inspected after recovery.
        // ---------------------------------------------------------

        private const int BarCount = 160;

        private static double[] SyntheticCloses(int bars = BarCount)
        {
            var rng = new Random(20260822);
            var close = new double[bars];
            double price = 100.0;
            for (int i = 0; i < bars; i++)
            {
                price += (rng.NextDouble() - 0.5) * 2.0 + 0.04;
                close[i] = price;
            }
            return close;
        }

        private static EngineConfiguration MakeConfig(
            GateMarketData md,
            out ATRSmoothReferenceSource source)
        {
            source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 8, atrMultiplier: 3.1, smoothLength: 6));

            return new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(7),
                new ScaleNormalizationModel(),
                // All eight models: pins the statistics window multiset
                // tightly (median/min/max/MAD/range catch content or
                // ordering differences, not just mean/std collisions).
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
                    StatisticsWindowSize = 12,
                    ReversalMode = Core.ReversalMode.CloseToReference
                });
        }

        private static (ResearchFeatureEngine engine, GateMarketData md, ATRSmoothReferenceSource src)
            BuildEngine(int bars = BarCount)
        {
            var md = new GateMarketData(SyntheticCloses(bars));
            var engine = new ResearchFeatureEngineBuilder(MakeConfig(md, out var src)).Build();
            return (engine, md, src);
        }

        // ---------------------------------------------------------
        // Snapshots — all published stage values.
        // ---------------------------------------------------------

        private sealed class Snap
        {
            public double Reference;
            public double Directional;
            public double Absolute;
            public double Scale;
            public double Normalized;
            public double Mean;
            public double StdDev;
            public double Min;
            public double Max;
            public double Median;
            public int ObservationCount;
            public bool HasBarsSinceReversal;
            public int BarsSinceReversal;
            public int Direction;
            public bool IsReversalBar;

            public static Snap Capture(ResearchFeatureEngine e)
            {
                var v = e.Values;
                return new Snap
                {
                    Reference = v.Reference.Price,
                    Directional = v.Distance.DirectionalExtension,
                    Absolute = v.Distance.AbsoluteExtension,
                    Scale = v.Scale.Scale,
                    Normalized = v.Normalization.NormalizedMeasurement,
                    Mean = v.Statistics.Location.Mean,
                    StdDev = v.Statistics.Dispersion.StandardDeviation,
                    Min = v.Statistics.Range.Minimum,
                    Max = v.Statistics.Range.Maximum,
                    Median = v.Statistics.Location.Median,
                    ObservationCount = v.Statistics.ObservationCount,
                    HasBarsSinceReversal = v.Reversal.BarsSinceReversal.HasValue,
                    BarsSinceReversal = v.Reversal.BarsSinceReversal ?? -1,
                    Direction = (int)v.Reversal.Direction,
                    IsReversalBar = v.Reversal.IsReversalBar
                };
            }

            public void AssertEqual(Snap o, string what)
            {
                Assert.True(Reference == o.Reference, $"{what}: Reference");
                Assert.True(Directional == o.Directional, $"{what}: Directional");
                Assert.True(Absolute == o.Absolute, $"{what}: Absolute");
                Assert.True(Scale == o.Scale, $"{what}: Scale");
                Assert.True(Normalized == o.Normalized, $"{what}: Normalized");
                Assert.True(Mean == o.Mean, $"{what}: Mean");
                Assert.True(StdDev == o.StdDev, $"{what}: StdDev");
                Assert.True(Min == o.Min, $"{what}: Min");
                Assert.True(Max == o.Max, $"{what}: Max");
                Assert.True(Median == o.Median, $"{what}: Median");
                Assert.Equal(ObservationCount, o.ObservationCount);
                Assert.Equal(HasBarsSinceReversal, o.HasBarsSinceReversal);
                Assert.Equal(BarsSinceReversal, o.BarsSinceReversal);
                Assert.Equal(Direction, o.Direction);
                Assert.Equal(IsReversalBar, o.IsReversalBar);
            }
        }

        /// Fresh sequential walk over identical final data.
        private static Snap[] GroundTruth(int lastIndex, GateMarketData? overrideMd = null)
        {
            var md = overrideMd is not null
                ? new GateMarketData((double[])overrideMd.CloseValues.Clone())
                : new GateMarketData(SyntheticCloses());

            var engine = new ResearchFeatureEngineBuilder(MakeConfig(md, out _)).Build();
            var snaps = new Snap[lastIndex + 1];
            for (int i = 0; i <= lastIndex; i++)
            {
                engine.ProcessAt(i);
                snaps[i] = Snap.Capture(engine);
            }
            return snaps;
        }

        // ---------------------------------------------------------
        // Instrumented adapter-policy harness (mirrors indicator).
        // Counts recoveries and ProcessAt invocations.
        // ---------------------------------------------------------

        private sealed class GateHarness
        {
            private readonly ResearchFeatureEngine _engine;
            public readonly Dictionary<int, Snap> Published = new();
            public int DiscontinuityRecoveries;
            public int FreshPassResets;
            public long ProcessAtCalls;

            // Mirrors the indicator: -1 until the first processed bar.
            private int _lastProcessedIndex = -1;

            public GateHarness(ResearchFeatureEngine engine) => _engine = engine;

            public void Calculate(int index)
            {
                bool freshPass = index == 0 && _lastProcessedIndex >= 0;

                bool contiguous = _lastProcessedIndex < 0
                    ? index == 0
                    : index == _lastProcessedIndex
                      || index == _lastProcessedIndex + 1;

                if (freshPass)
                {
                    FreshPassResets++;
                    _engine.Pipeline.Reset();
                    for (int i = 0; i < index; i++)
                    {
                        _engine.ProcessAt(i);
                        ProcessAtCalls++;
                        Published[i] = Snap.Capture(_engine);
                    }
                }
                else if (!contiguous)
                {
                    DiscontinuityRecoveries++;
                    _engine.Pipeline.Reset();
                    for (int i = 0; i < index; i++)
                    {
                        _engine.ProcessAt(i);
                        ProcessAtCalls++;
                        Published[i] = Snap.Capture(_engine);
                    }
                }
                else if (_lastProcessedIndex < 0)
                {
                    // Very first call ever at bar 0: no reset needed.
                }

                _engine.ProcessAt(index);
                ProcessAtCalls++;
                Published[index] = Snap.Capture(_engine);

                _lastProcessedIndex = index;
            }
        }

        private static void RunSequence(GateHarness h, int[] seq)
        {
            foreach (int idx in seq)
                h.Calculate(idx);
        }

        private static void AssertAllPublishedMatchTruth(
            GateHarness h, int maxVisited, Snap[] truth, string what)
        {
            for (int i = 0; i <= maxVisited; i++)
            {
                Assert.True(h.Published.ContainsKey(i),
                    $"{what}: bar {i} never published");
                truth[i].AssertEqual(h.Published[i], $"{what} bar {i}");
            }
        }

        // ---------------------------------------------------------
        // Section 2 — required sequences vs clean reference run.
        // ---------------------------------------------------------

        public static IEnumerable<object[]> GateSequences()
        {
            yield return new object[]
                { new[] { 0, 1, 2, 3, 4, 5 }, 5, 0, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 2, 2, 3, 4, 5 }, 5, 0, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 5 }, 5, 1, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 3, 7 }, 7, 1, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 3, 2, 3, 4, 5 }, 5, 1, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 3, 0, 1, 2, 3, 4, 5 }, 5, 0, 1 };
            yield return new object[]
                { new[] { 0, 1, 2, 2, 2, 5 }, 5, 1, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 3, 3, 3, 1, 2, 3, 4 }, 4, 1, 0 };
            yield return new object[]
                { new[] { 0, 1, 2, 5, 6, 9, 10 }, 10, 2, 0 };
        }

        [Theory]
        [MemberData(nameof(GateSequences))]
        public void Sequence_OutputDeterminedOnlyByHistoryThroughN(
            int[] seq, int maxVisited, int expectedRecoveries, int expectedFreshResets)
        {
            var truth = GroundTruth(Math.Max(maxVisited, seq.Max()) + 3);

            var (engine, md, _) = BuildEngine();
            var harness = new GateHarness(engine);

            // Guard: no access may exceed the index currently being
            // processed by the policy (no future data consumption).
            int currentAllow = 0;
            md.SetAllowMax(0);
            foreach (int idx in seq)
            {
                currentAllow = Math.Max(currentAllow, idx);
                md.SetAllowMax(currentAllow);
                harness.Calculate(idx);
            }
            md.SetAllowMax(int.MaxValue);

            Assert.Equal(expectedRecoveries, harness.DiscontinuityRecoveries);
            Assert.Equal(expectedFreshResets, harness.FreshPassResets);

            // Every visited bar's FINAL published value equals clean-run.
            AssertAllPublishedMatchTruth(harness, maxVisited, truth, "visited");

            // Continuation: walk three more bars; any residual state
            // corruption would surface here permanently.
            for (int i = maxVisited + 1; i <= maxVisited + 3; i++)
            {
                harness.Calculate(i);
                truth[i].AssertEqual(harness.Published[i],
                    $"continuation after sequence [{string.Join(',', seq)}] bar {i}");
            }
        }

        // ---------------------------------------------------------
        // Section 3 — stateful components individually after recovery.
        // ---------------------------------------------------------

        [Fact]
        public void Recovery_PreservesATRSmoothInternalState_Exactly()
        {
            const int Target = 30;

            // Fresh reference: own data + own source instance, walked
            // 0..Target. Keep the source the engine ACTUALLY uses.
            var freshMd = new GateMarketData(SyntheticCloses());
            var freshEngine = new ResearchFeatureEngineBuilder(
                MakeConfig(freshMd, out var freshSrc)).Build();

            for (int i = 0; i <= Target; i++)
                freshEngine.ProcessAt(i);

            // Recovering engine: healthy 0..9 then jump to Target.
            var (recEngine, _, recSrc) = BuildEngine();
            var harness = new GateHarness(recEngine);
            for (int i = 0; i <= 9; i++)
                harness.Calculate(i);
            harness.Calculate(Target);

            // After recovery the internal ATRSmooth state must be
            // bit-for-bit identical to the fresh engine's state at the
            // same bar (EMA of TR, trailing stop, position, VWMA sums,
            // last reference, current index).
            Assert.Equal(freshSrc.Runtime.EmaTrueRange,
                recSrc.Runtime.EmaTrueRange);
            Assert.Equal(freshSrc.Runtime.TrailingStop,
                recSrc.Runtime.TrailingStop);
            Assert.Equal(freshSrc.Runtime.Position,
                recSrc.Runtime.Position);
            Assert.Equal(freshSrc.Runtime.SumPV, recSrc.Runtime.SumPV);
            Assert.Equal(freshSrc.Runtime.SumV, recSrc.Runtime.SumV);
            Assert.Equal(freshSrc.Runtime.LastReference,
                recSrc.Runtime.LastReference);
            Assert.Equal(Target, recSrc.Runtime.CurrentIndex);

            // Snapshot-state behavioral proxy: immediate re-ticks at the
            // recovered bar must stay exactly equal to truth (a stale or
            // wrong snapshot would corrupt the restore path).
            var truth = GroundTruth(Target + 1);
            for (int k = 0; k < 4; k++)
            {
                harness.Calculate(Target);
                truth[Target].AssertEqual(harness.Published[Target],
                    $"re-tick {k} after recovery");
            }
        }

        // ---------------------------------------------------------
        // Section 4 — re-tick + discontinuity interaction: recovery
        // must fire EXACTLY once per discontinuity event.
        // ---------------------------------------------------------

        [Fact]
        public void ReTicksDoNotTriggerRecovery_AndRecoveryFiresExactlyOnce()
        {
            var (engine, _, _) = BuildEngine();
            var harness = new GateHarness(engine);

            // Pure sequential + retick storm: zero recoveries expected.
            harness.Calculate(0);
            for (int i = 1; i <= 40; i++)
            {
                harness.Calculate(i);
                harness.Calculate(i);   // extra tick
                harness.Calculate(i);   // extra tick
            }
            Assert.Equal(0, harness.DiscontinuityRecoveries);
            Assert.Equal(0, harness.FreshPassResets);
            Assert.Equal(120 + 1, harness.ProcessAtCalls);

            // One gap: exactly one recovery.
            harness.Calculate(50);
            Assert.Equal(1, harness.DiscontinuityRecoveries);

            // Backward jump: exactly one more.
            harness.Calculate(20);
            Assert.Equal(2, harness.DiscontinuityRecoveries);

            // Contiguous continuation: no additional recoveries.
            harness.Calculate(21);
            harness.Calculate(22);
            Assert.Equal(2, harness.DiscontinuityRecoveries);
        }

        // ---------------------------------------------------------
        // Section 5 — data mutation on live bar then advance.
        // Final result must match a clean run using FINAL data,
        // across all stages simultaneously.
        // ---------------------------------------------------------

        [Fact]
        public void MutatedLiveBar_FinalAdvance_MatchesCleanRunWithFinalData()
        {
            var (engine, md, _) = BuildEngine();
            var harness = new GateHarness(engine);

            for (int i = 0; i < 40; i++)
                harness.Calculate(i);

            // Tick 1 on bar 40 with initial data...
            harness.Calculate(40);

            // ...then the bar's close changes (live tick) and we
            // re-process twice before advancing.
            md.SetClose(40, md.CloseValues[40] + 6.25);
            harness.Calculate(40);
            md.SetClose(40, md.CloseValues[40] - 1.5);
            harness.Calculate(40);

            // Ground truth over the FINAL data.
            var truthWithMutation = GroundTruth(41, md);
            truthWithMutation[40].AssertEqual(harness.Published[40],
                "mutated live bar 40");

            harness.Calculate(41);
            truthWithMutation[41].AssertEqual(harness.Published[41],
                "bar 41 committed mutated bar 40");

            // And a few bars beyond: contamination-free continuation.
            for (int i = 42; i <= 45; i++)
                harness.Calculate(i);

            var truthExtended = GroundTruth(45, md);
            for (int i = 42; i <= 45; i++)
                truthExtended[i].AssertEqual(harness.Published[i],
                    $"continuation bar {i}");
        }

        // ---------------------------------------------------------
        // Section 6/7 — complexity: normal path O(1), gap O(index),
        // recovery triggered only when necessary.
        // ---------------------------------------------------------

        [Fact]
        public void WorkPerCalculate_NormalPathBounded_GapScalesWithIndex()
        {
            var (engine, md, _) = BuildEngine();
            var harness = new GateHarness(engine);

            // Sequential streaming with per-bar reticks.
            var perCall = new List<long>();
            for (int i = 0; i <= 100; i++)
            {
                long before = md.Accesses;
                harness.Calculate(i);
                harness.Calculate(i);
                perCall.Add(md.Accesses - before);
            }

            // Zero recoveries on the fully supported stream.
            Assert.Equal(0, harness.DiscontinuityRecoveries);

            // Bounded work per pair of calls, no growth trend: compare
            // early vs late windows (allow modest drift from warm-up
            // window effects but reject O(N) growth).
            double earlyAvg = perCall.Take(10).Average();
            double lateAvg = perCall.Skip(80).Take(10).Average();
            Assert.True(lateAvg < earlyAvg * 3 + 200,
                $"Work grew with bar index: early {earlyAvg:F0}, late {lateAvg:F0}");
            Assert.True(lateAvg < 3000,
                $"Normal path work unbounded: {lateAvg:F0} accesses/call-pair");

            // Discontinuity at 100 -> 150: replay touches ~150 bars.
            long beforeGap = md.Accesses;
            harness.Calculate(150);
            long gapWork = md.Accesses - beforeGap;

            Assert.Equal(1, harness.DiscontinuityRecoveries);
            Assert.True(gapWork > lateAvg * 10,
                $"Recovery did less work than expected: {gapWork} vs {lateAvg:F0}/call");

            // Post-recovery normal path is bounded again immediately.
            harness.Calculate(151);
            long afterGap = md.Accesses - beforeGap - gapWork;
            Assert.True(afterGap < 3000,
                $"Post-recovery call unbounded: {afterGap}");
        }

        // ---------------------------------------------------------
        // Section 8 — reset boundary: recovery output for replayed
        // bar 0 must equal a clean load's bar 0 (proves Reset ran
        // BEFORE replay; processing index-only would corrupt even
        // the replay prefix).
        // ---------------------------------------------------------

        [Fact]
        public void Recovery_ReplaysFromCleanReset_PublishesCorrectPrefix()
        {
            var truth = GroundTruth(30);
            var (engine, _, _) = BuildEngine();
            var harness = new GateHarness(engine);

            for (int i = 0; i <= 9; i++)
                harness.Calculate(i);

            harness.Calculate(30);   // gap -> full replay 0..29

            // Entire replayed prefix + target must match clean run.
            AssertAllPublishedMatchTruth(harness, 30, truth, "recovered");
        }

        // ---------------------------------------------------------
        // Determinism: identical delivery patterns reaching the same
        // final bar produce byte-identical snapshots (invariant restated
        // directly).
        // ---------------------------------------------------------

        [Fact]
        public void Invariant_SameBarSameData_RegardlessOfDeliveryPattern()
        {
            var truth = GroundTruth(60);

            var patterns = new List<int[]>
            {
                Enumerable.Range(0, 61).ToArray(),
                new[] { 0, 1, 2, 2, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 0, 1, 2, 3, 7 },
                new[] { 0, 1, 2, 3, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 0, 1, 2, 5, 6, 9, 10, 11, 12, 13, 14, 15 }
            };

            foreach (var pattern in patterns)
            {
                var (engine, _, _) = BuildEngine();
                var harness = new GateHarness(engine);
                foreach (int idx in pattern)
                    harness.Calculate(idx);

                // Walk to 60 through the harness; every step must agree
                // with clean truth regardless of how we got here.
                for (int i = pattern.Max() + 1; i <= 60; i++)
                    harness.Calculate(i);

                for (int i = pattern.Max(); i <= 60; i++)
                    truth[i].AssertEqual(harness.Published[i],
                        $"pattern [{string.Join(',', pattern)}] bar {i}");
            }
        }
    }
}

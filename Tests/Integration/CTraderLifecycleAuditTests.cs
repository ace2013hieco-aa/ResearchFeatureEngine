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
    /// cTrader lifecycle and stateful-recalculation audit tests.
    ///
    /// Verifies the full production pipeline under every lifecycle
    /// pattern the cTrader indicator adapter can legally receive:
    ///   * initial sequential pass (Scenario A)
    ///   * same-bar live re-ticks, including on reversal bars (Scenario B)
    ///   * normal sequential progression (Scenario C)
    ///   * fresh pass beginning at Calculate(0) (Scenario D)
    ///   * repeated fresh passes (Scenario E)
    ///   * discontinuities: forward gaps, backward jumps, first-call
    ///     at a non-zero index — handled by the adapter's replay policy
    ///
    /// Ground truth is always an independent fresh engine walked
    /// sequentially over identical data; comparisons are exact
    /// (bit-for-bit) because both executions run the same operations
    /// in the same order on the same doubles.
    ///
    /// NOTE ON THE ADAPTER POLICY: the indicator project is not
    /// referenced by the test project (cAlgo dependency), so
    /// <see cref="LifecycleAdapterHarness"/> below mirrors the
    /// indicator's Calculate() dispatch logic exactly. If the
    /// indicator's policy changes, this harness must be updated in
    /// lockstep.
    /// </summary>
    public sealed class CTraderLifecycleAuditTests
    {
        // ---------------------------------------------------------
        // Mutable market data + engine construction
        // ---------------------------------------------------------

        private sealed class MutableOhlcvMarketData : IMarketData
        {
            private readonly double[] _open;
            private readonly double[] _high;
            private readonly double[] _low;
            private readonly double[] _close;
            private readonly double[] _volume;

            public MutableOhlcvMarketData(double[] close)
            {
                _close = close;
                _open = (double[])close.Clone();
                _high = close.Select(c => c + 0.3).ToArray();
                _low = close.Select(c => c - 0.3).ToArray();
                _volume = Enumerable.Repeat(100.0, close.Length).ToArray();
            }

            public IPriceSeries Open => new TestPriceSeries(_open);
            public IPriceSeries High => new TestPriceSeries(_high);
            public IPriceSeries Low => new TestPriceSeries(_low);
            public IPriceSeries Close => new TestPriceSeries(_close);
            public IPriceSeries Volume => new TestPriceSeries(_volume);
            public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
            public int Count => _close.Length;
            public void SetClose(int index, double value) => _close[index] = value;
            public bool MoveNext() => false;
        }

        private const int BarCount = 60;

        private static double[] SyntheticCloses()
        {
            // Seeded random walk with drift so that above/below flips
            // occur naturally several times across 60 bars.
            var rng = new Random(1234);
            var close = new double[BarCount];
            double price = 100.0;
            for (int i = 0; i < BarCount; i++)
            {
                price += (rng.NextDouble() - 0.5) * 2.0 + 0.05;
                close[i] = price;
            }
            return close;
        }

        private static EngineConfiguration MakeConfig(MutableOhlcvMarketData md)
        {
            return new EngineConfiguration(
                md,
                new EngineValues(),
                new ATRSmoothReferenceSource(
                    new ATRSmoothConfiguration(atrPeriod: 8, atrMultiplier: 3.1, smoothLength: 6)),
                new ATRScaleModel(7),
                new ScaleNormalizationModel(),
                new List<IStatisticModel>
                {
                    new MeanModel(),
                    new StandardDeviationModel()
                },
                options: new EngineOptions
                {
                    StatisticsWindowSize = 10,
                    ReversalMode = Core.ReversalMode.CloseToReference
                });
        }

        private static (ResearchFeatureEngine engine, MutableOhlcvMarketData md) BuildEngine()
        {
            var md = new MutableOhlcvMarketData(SyntheticCloses());
            var engine = new ResearchFeatureEngineBuilder(MakeConfig(md)).Build();
            return (engine, md);
        }

        // ---------------------------------------------------------
        // Snapshot helpers — capture ALL published stage values.
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
            public int ObservationCount;
            public bool HasBarsSinceReversal;
            public int BarsSinceReversal;
            public int Direction;
            public bool IsReversalBar;

            public void AssertEqual(Snap other, string what)
            {
                Assert.True(Reference == other.Reference,
                    $"{what}: Reference {Reference} != {other.Reference}");
                Assert.True(Directional == other.Directional,
                    $"{what}: Directional {Directional} != {other.Directional}");
                Assert.True(Absolute == other.Absolute,
                    $"{what}: Absolute {Absolute} != {other.Absolute}");
                Assert.True(Scale == other.Scale,
                    $"{what}: Scale {Scale} != {other.Scale}");
                Assert.True(Normalized == other.Normalized,
                    $"{what}: Normalized {Normalized} != {other.Normalized}");
                Assert.True(Mean == other.Mean,
                    $"{what}: Mean {Mean} != {other.Mean}");
                Assert.True(StdDev == other.StdDev,
                    $"{what}: StdDev {StdDev} != {other.StdDev}");
                Assert.Equal(ObservationCount, other.ObservationCount);
                Assert.Equal(HasBarsSinceReversal, other.HasBarsSinceReversal);
                Assert.Equal(BarsSinceReversal, other.BarsSinceReversal);
                Assert.Equal(Direction, other.Direction);
                Assert.Equal(IsReversalBar, other.IsReversalBar);
            }
        }

        private static Snap Capture(ResearchFeatureEngine e)
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
                ObservationCount = v.Statistics.ObservationCount,
                HasBarsSinceReversal = v.Reversal.BarsSinceReversal.HasValue,
                BarsSinceReversal = v.Reversal.BarsSinceReversal ?? -1,
                Direction = (int)v.Reversal.Direction,
                IsReversalBar = v.Reversal.IsReversalBar
            };
        }

        /// <summary>
        /// Ground truth: fresh engine over identical data (optionally
        /// with bar <paramref name="overrideIndex"/>'s close replaced),
        /// walked sequentially 0..lastIndex, capturing every bar.
        /// </summary>
        private static Snap[] GroundTruth(
            int lastIndex, int? overrideIndex = null, double? overrideClose = null)
        {
            var md = new MutableOhlcvMarketData(SyntheticCloses());
            if (overrideIndex.HasValue && overrideClose.HasValue)
                md.SetClose(overrideIndex.Value, overrideClose.Value);

            var engine = new ResearchFeatureEngineBuilder(MakeConfig(md)).Build();
            var snaps = new Snap[lastIndex + 1];
            for (int i = 0; i <= lastIndex; i++)
            {
                engine.ProcessAt(i);
                snaps[i] = Capture(engine);
            }
            return snaps;
        }

        // ---------------------------------------------------------
        // Adapter-policy harness.
        //
        // MIRRORS ResearchFeatureEngineIndicator.Calculate() exactly:
        // fresh-pass reset at index 0, replay-from-zero on any
        // discontinuity, O(1) fast path for re-tick / next-bar.
        // Keep in lockstep with the indicator.
        // ---------------------------------------------------------

        private sealed class LifecycleAdapterHarness
        {
            private readonly ResearchFeatureEngine _engine;

            /// Latest published snapshot per bar index (replay overwrites).
            public readonly Dictionary<int, Snap> Published = new();

            private int _lastProcessedIndex;

            public LifecycleAdapterHarness(ResearchFeatureEngine engine)
            {
                _engine = engine;
                _lastProcessedIndex = -1;
            }

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
                        Published[i] = Capture(_engine);
                    }
                }

                _engine.ProcessAt(index);
                Published[index] = Capture(_engine);

                _lastProcessedIndex = index;
            }
        }

        // ---------------------------------------------------------
        // Scenario A — initial initialization starts clean.
        // ---------------------------------------------------------

        [Fact]
        public void ScenarioA_InitialState_IsClean_BeforeAnyReversal()
        {
            var (engine, _) = BuildEngine();

            engine.ProcessAt(0);

            Assert.Null(engine.Values.Reversal.BarsSinceReversal);
            Assert.Equal(Core.ReversalDirection.None, engine.Values.Reversal.Direction);
            Assert.False(engine.Values.Reversal.IsReversalBar);
            Assert.Equal(1, engine.Values.Statistics.ObservationCount);
            Assert.True(double.IsFinite(engine.Values.Reference.Price));
            Assert.True(double.IsFinite(engine.Values.Normalization.NormalizedMeasurement));

            // The data contains reversals later; verify they are detected
            // only after the fact, never invented at the start.
            engine.ProcessAt(1);
            Assert.Null(engine.Values.Reversal.BarsSinceReversal);
        }

        // ---------------------------------------------------------
        // Scenario B — same-bar live ticks (full pipeline).
        // ---------------------------------------------------------

        [Fact]
        public void ScenarioB_RepeatedTicksOnLiveBar_MatchGroundTruthOfFinalData()
        {
            // Pick a mid-series live bar. Whatever mutations happen to
            // its close across ticks, after the last tick the published
            // values must equal a fresh engine fed the SAME final data.
            const int liveBar = 40;
            var (engine, md) = BuildEngine();
            for (int i = 0; i < liveBar; i++)
                engine.ProcessAt(i);

            // Tick storm with mutating close: up, down, wild, final.
            md.SetClose(liveBar, md.Close[liveBar] + 5.0);
            engine.ProcessAt(liveBar);
            md.SetClose(liveBar, md.Close[liveBar] - 10.0);
            engine.ProcessAt(liveBar);
            md.SetClose(liveBar, md.Close[liveBar] + 3.33);
            engine.ProcessAt(liveBar);
            engine.ProcessAt(liveBar);
            engine.ProcessAt(liveBar);

            var truth = GroundTruth(liveBar, liveBar, md.Close[liveBar]);
            truth[liveBar].AssertEqual(Capture(engine),
                "live-bar tick storm vs ground truth");

            // Counter must not have been inflated by repeated ticks:
            // whatever bars-since-reversal is now, it equals ground truth.
            Assert.Equal(truth[liveBar].BarsSinceReversal,
                engine.Values.Reversal.BarsSinceReversal ?? -1);
        }

        [Fact]
        public void ScenarioB_TicksAcrossTheReference_DoNotManufactureExtraReversals()
        {
            // Drive the live bar's close back and forth across the
            // reference so the relation flips between ticks. Each tick
            // must recompute from the end-of-previous-bar baseline:
            // the committed state must stay exactly what the sequential
            // prefix produced, and the next real bar must continue from
            // clean state.
            const int liveBar = 35;
            var (engine, md) = BuildEngine();
            for (int i = 0; i < liveBar; i++)
                engine.ProcessAt(i);

            double baseClose = md.Close[liveBar];
            for (int k = 0; k < 6; k++)
            {
                md.SetClose(liveBar, baseClose + (k % 2 == 0 ? 4.0 : -4.0));
                engine.ProcessAt(liveBar);
            }

            // Final data = last mutation. Compare to ground truth with
            // that same final close.
            var truth = GroundTruth(liveBar, liveBar, md.Close[liveBar]);
            truth[liveBar].AssertEqual(Capture(engine),
                "flip-across-reference tick storm vs ground truth");
        }

        // ---------------------------------------------------------
        // Scenario C — sequential progression is deterministic and
        // matches an independent fresh engine bit-for-bit.
        // ---------------------------------------------------------

        [Fact]
        public void ScenarioC_SequentialWalk_MatchesIndependentFreshEngine_Exactly()
        {
            var (a, _) = BuildEngine();
            var truth = GroundTruth(BarCount - 1);

            for (int i = 0; i < BarCount; i++)
            {
                a.ProcessAt(i);
                truth[i].AssertEqual(Capture(a), $"sequential bar {i}");
            }
        }

        // ---------------------------------------------------------
        // Scenarios D + E — fresh passes via reset-at-0.
        // ---------------------------------------------------------

        [Fact]
        public void ScenarioD_FreshPassBeginningAtIndex0_MatchesFirstPassExactly()
        {
            var (engine, _) = BuildEngine();
            var pass1 = new Snap[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                engine.ProcessAt(i);
                pass1[i] = Capture(engine);
            }

            // Fresh pass: adapter detects Calculate(0) after processing
            // and resets the pipeline.
            engine.Pipeline.Reset();
            for (int i = 0; i < BarCount; i++)
            {
                engine.ProcessAt(i);
                pass1[i].AssertEqual(Capture(engine), $"fresh-pass bar {i}");
            }
        }

        [Fact]
        public void ScenarioE_ThreeRepeatedFreshPasses_ProduceIdenticalOutputs()
        {
            var (engine, _) = BuildEngine();
            var referencePass = new Snap[BarCount];

            int last = -1;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < BarCount; i++)
                {
                    if (i == 0 && last >= 0)
                        engine.Pipeline.Reset();

                    engine.ProcessAt(i);
                    var snap = Capture(engine);

                    if (pass == 0)
                        referencePass[i] = snap;
                    else
                        referencePass[i].AssertEqual(snap,
                            $"pass {pass + 1} bar {i}");

                    last = i;
                }
            }
        }

        // ---------------------------------------------------------
        // Supported sequence: 0,1,2,3,3,3,4 (re-ticks then advance).
        // ---------------------------------------------------------

        [Fact]
        public void Sequence_ReticksThenAdvance_012334_MatchesGroundTruth()
        {
            var (engine, md) = BuildEngine();

            engine.ProcessAt(0);
            engine.ProcessAt(1);
            engine.ProcessAt(2);
            engine.ProcessAt(3);

            // Live ticks mutate bar 3's close before it closes.
            md.SetClose(3, md.Close[3] + 2.5);
            engine.ProcessAt(3);
            engine.ProcessAt(3);

            engine.ProcessAt(4);

            var truth = GroundTruth(4, 3, md.Close[3]);
            truth[4].AssertEqual(Capture(engine), "bar 4 after reticked bar 3");
        }

        // ---------------------------------------------------------
        // Discontinuities through the ADAPTER POLICY recover exactly.
        //
        // These sequences are NOT part of the supported raw-engine
        // contract; the adapter neutralizes them by replaying from 0.
        // ---------------------------------------------------------

        [Fact]
        public void AdapterPolicy_ForwardGap_012_then5_RecoversExactly()
        {
            var (engine, _) = BuildEngine();
            var harness = new LifecycleAdapterHarness(engine);
            var truth = GroundTruth(20);

            harness.Calculate(0);
            harness.Calculate(1);
            harness.Calculate(2);
            harness.Calculate(5);   // gap

            truth[5].AssertEqual(harness.Published[5], "gap target bar 5");

            foreach (int i in new[] { 6, 7, 8 })
            {
                harness.Calculate(i);
                truth[i].AssertEqual(harness.Published[i],
                    $"continuation after gap, bar {i}");
            }
        }

        [Fact]
        public void AdapterPolicy_BackwardJump_NotThroughZero_RecoversExactly()
        {
            var (engine, _) = BuildEngine();
            var harness = new LifecycleAdapterHarness(engine);
            var truth = GroundTruth(15);

            for (int i = 0; i <= 9; i++)
                harness.Calculate(i);

            harness.Calculate(4);           // backward jump, no Calculate(0)
            truth[4].AssertEqual(harness.Published[4], "backward jump target bar 4");

            foreach (int i in new[] { 5, 6 })
            {
                harness.Calculate(i);
                truth[i].AssertEqual(harness.Published[i],
                    $"continuation after backward jump, bar {i}");
            }
        }

        [Fact]
        public void AdapterPolicy_FirstCallAtNonZeroIndex_RecoversExactly()
        {
            // Defensive: even a first-ever call at a non-zero index
            // (attach mid-history) produces fresh-load-equivalent output.
            var (engine, _) = BuildEngine();
            var harness = new LifecycleAdapterHarness(engine);
            var truth = GroundTruth(12);

            harness.Calculate(12);
            truth[12].AssertEqual(harness.Published[12], "first call at bar 12");
        }

        [Fact]
        public void AdapterPolicy_ResetViaZeroMidSeries_ThenContinue_MatchesTruth()
        {
            var (engine, _) = BuildEngine();
            var harness = new LifecycleAdapterHarness(engine);
            var truth = GroundTruth(14);

            for (int i = 0; i <= 6; i++)
                harness.Calculate(i);

            // Partial recalculation restarts at 0 and walks a few bars.
            harness.Calculate(0);
            harness.Calculate(1);
            harness.Calculate(2);

            // Then jumps to the (shifted) live region.
            harness.Calculate(14);
            truth[14].AssertEqual(harness.Published[14],
                "live bar after reset+discontinuity");
        }

        [Fact]
        public void AdapterPolicy_TwoFullPasses_WithReticks_MatchTruthEverywhere()
        {
            var (engine, md) = BuildEngine();
            var harness = new LifecycleAdapterHarness(engine);

            // ---- Pass 1 over original data: every bar must match truth.
            var truthOriginal = GroundTruth(BarCount - 1);
            for (int i = 0; i < BarCount; i++)
                harness.Calculate(i);

            for (int i = 0; i < BarCount; i++)
                truthOriginal[i].AssertEqual(harness.Published[i],
                    $"pass 1, bar {i}");

            // ---- Live-tick mutation on bar 30, then two re-ticks.
            md.SetClose(30, md.Close[30] + 1.0);
            harness.Calculate(30);
            harness.Calculate(30);

            // The mutated close is now the FINAL data for bar 30; the
            // published bar-30 values must match ground truth computed
            // with that same final data.
            var truthMutatedBar30 = GroundTruth(30, 30, md.Close[30]);
            truthMutatedBar30[30].AssertEqual(harness.Published[30],
                "mutated live bar 30");

            // ---- Pass 2 (fresh recalculation from 0) with the mutated
            // dataset. Every bar must match ground truth of final data.
            var truthFinal = GroundTruth(BarCount - 1, 30, md.Close[30]);
            for (int i = 0; i < BarCount; i++)
                harness.Calculate(i);

            for (int i = 0; i < BarCount; i++)
                truthFinal[i].AssertEqual(harness.Published[i],
                    $"pass 2, bar {i}");
        }

        // ---------------------------------------------------------
        // Contract documentation: RAW ProcessAt misuse corrupts.
        // This test intentionally documents why the adapter MUST
        // enforce monotonicity/replay — do not "fix" the engine here.
        // ---------------------------------------------------------

        [Fact]
        public void RawProcessAt_BackwardJump_DocumentsSilentCorruption_ContractRequired()
        {
            var truth = GroundTruth(9);

            var (engine, _) = BuildEngine();
            for (int i = 0; i <= 9; i++)
                engine.ProcessAt(i);

            engine.ProcessAt(4);   // unsupported backward jump

            var drifted = Capture(engine);
            var expected = truth[4];

            bool corrupted =
                drifted.Reference != expected.Reference ||
                drifted.Mean != expected.Mean ||
                drifted.BarsSinceReversal != expected.BarsSinceReversal ||
                drifted.Direction != expected.Direction;

            Assert.True(corrupted,
                "Expected the raw engine to diverge on an unsupported " +
                "backward jump. If this ever becomes equal, the engines " +
                "have become order-independent and the adapter replay " +
                "policy can be simplified.");
        }

        // ---------------------------------------------------------
        // Mode A (Update auto-advance) vs Mode B (ProcessAt) vs
        // Mode C (re-tick-heavy walk) equivalence.
        // ---------------------------------------------------------

        [Fact]
        public void Modes_UpdateDriven_ProcessAtDriven_AndRetickHeavy_AgreeExactly()
        {
            // Mode A: Update()-driven backtest walk.
            var mdA = new MutableOhlcvMarketData(SyntheticCloses());
            var cfgA = MakeConfig(mdA);
            var engA = new ResearchFeatureEngineBuilder(cfgA).Build();
            var modeA = new Snap[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                mdA.MoveNext();
                engA.Update();
                modeA[i] = Capture(engA);
            }

            // Mode B: explicit indexed processing.
            var (engB, _) = BuildEngine();
            var modeB = new Snap[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                engB.ProcessAt(i);
                modeB[i] = Capture(engB);
            }

            // Mode C: re-tick-heavy live simulation. Intra-bar close
            // fluctuations are applied mid-bar and restored before the
            // closing tick, so the FINAL dataset is identical across
            // all three modes (only intra-bar tick dynamics differ).
            var (engC, mdC) = BuildEngine();
            double[] originalCloses = SyntheticCloses();
            var modeC = new Snap[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                engC.ProcessAt(i);          // first tick of bar i
                engC.ProcessAt(i);          // mid tick

                if (i > 0 && i % 3 == 0)
                {
                    mdC.SetClose(i, mdC.Close[i] + 0.25);
                    engC.ProcessAt(i);      // late tick with fluctuated close
                    mdC.SetClose(i, originalCloses[i]);
                }

                engC.ProcessAt(i);          // closing tick (final data)
                modeC[i] = Capture(engC);
            }

            for (int i = 0; i < BarCount; i++)
            {
                modeA[i].AssertEqual(modeB[i], $"Mode A vs B bar {i}");
                modeA[i].AssertEqual(modeC[i], $"Mode A vs C bar {i}");
            }
        }

        // ---------------------------------------------------------
        // Reset completeness: after Pipeline.Reset(), rerunning any
        // window reproduces fresh-engine values exactly — including
        // statistics window contents and reversal counters.
        // ---------------------------------------------------------

        [Fact]
        public void PipelineReset_ClearsAllStatefulEngines_RerunMatchesFreshEngine()
        {
            var truth = GroundTruth(29);

            var (engine, _) = BuildEngine();
            for (int i = 0; i < 50; i++)
                engine.ProcessAt(i);

            engine.Pipeline.Reset();

            for (int i = 0; i <= 29; i++)
            {
                engine.ProcessAt(i);
                truth[i].AssertEqual(Capture(engine), $"post-reset rerun bar {i}");
            }
        }
    }
}

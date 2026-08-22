using System;
using System.Collections.Generic;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Statistics;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Statistics
{
    /// <summary>
    /// Engine-level tests for the skewness/kurtosis models through
    /// the Statistics stage: publication semantics (minimum n,
    /// zero-variance retain), source integration (Close / SimpleReturn /
    /// LogReturn), live-tick correctness, fresh-pass equivalence,
    /// discontinuity recovery, and bit-for-bit determinism.
    ///
    /// Expected G1/G2 values are hard-coded from the INDEPENDENT
    /// Python reference (see <see cref="SkewnessKurtosisModelTests"/>),
    /// and the observation arrays are derived by hand from the engine's
    /// documented window semantics (committed closed bars + live bar).
    /// </summary>
    public sealed class SkewnessKurtosisEngineTests
    {
        // ---------------------------------------------------------
        // Build helpers
        // ---------------------------------------------------------

        private static (EngineContext context, StatisticsEngine engine) Build(
            double[] closes,
            int windowSize = 5,
            StatisticsSource source = StatisticsSource.Close,
            bool includeShape = true)
        {
            var context = TestEngineContext.Create(closes);

            var models = new List<IStatisticModel>
            {
                new MeanModel(),
                new StandardDeviationModel()
            };
            if (includeShape)
            {
                models.Add(new SkewnessModel());
                models.Add(new KurtosisModel());
            }

            var engine = new StatisticsEngine(
                context,
                new StatisticsWindow(windowSize),
                models,
                source);

            return (context, engine);
        }

        private static void Step(
            EngineContext context, StatisticsEngine engine, int index)
        {
            context.SetIndex(index);
            engine.Update();
        }

        /// <summary>
        /// Mutable close series so the live (still-forming) bar's close
        /// can be changed between ticks, mirroring cTrader live ticks.
        /// </summary>
        private sealed class MutableCloseMarketData : IMarketData
        {
            private readonly double[] _close;

            public MutableCloseMarketData(double[] close)
            {
                _close = (double[])close.Clone();
            }

            public IPriceSeries Open => new TestPriceSeries(_close);
            public IPriceSeries High => new TestPriceSeries(_close);
            public IPriceSeries Low => new TestPriceSeries(_close);
            public IPriceSeries Close => new TestPriceSeries(_close);
            public IPriceSeries Volume => new TestPriceSeries(Array.Empty<double>());
            public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
            public int Count => _close.Length;
            public void SetClose(int index, double value) => _close[index] = value;
            public bool MoveNext() => false;
        }

        // ---------------------------------------------------------
        // F. Minimum publication window at engine level
        // ---------------------------------------------------------

        [Fact]
        public void MinimumObservationCount_SkewnessAt3_KurtosisAt4()
        {
            // Closes [100, 101, 104, 103], window 5:
            //   bar 0: obs [100]          n=1
            //   bar 1: obs [100,101]      n=2
            //   bar 2: obs [100,101,104]  n=3  -> skewness publishes
            //   bar 3: obs [100,101,104,103] n=4 -> kurtosis publishes
            var (context, engine) = Build(new[] { 100.0, 101.0, 104.0, 103.0 });

            Step(context, engine, 0);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Skewness);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Kurtosis);

            Step(context, engine, 1);
            // n=2 < 3: nothing published; initial 0.0 retained.
            Assert.Equal(0.0, context.Values.Statistics.Shape.Skewness);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Kurtosis);

            Step(context, engine, 2);
            // n=3: skewness becomes available (nonzero -> proves it
            // was PUBLISHED, not merely retained at 0.0). Kurtosis
            // still n=3 < 4 -> retained.
            Assert.Equal(1.293342780733375,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Kurtosis);

            Step(context, engine, 3);
            // n=4: kurtosis becomes available (nonzero -> published).
            Assert.Equal(-3.3, context.Values.Statistics.Shape.Kurtosis, 11);
            Assert.Equal(4, context.Values.Statistics.ObservationCount);
        }

        // ---------------------------------------------------------
        // G. Zero-variance semantics: skip publication, retain last
        //    published value; never publish 0/NaN, never throw.
        // ---------------------------------------------------------

        [Fact]
        public void ZeroVariance_NeverPublishes_RetainsLastPublishedValue()
        {
            var md = new MutableCloseMarketData(new[]
                { 100.0, 100.0, 100.0, 100.0, 100.0, 100.0 });
            var context = new EngineContext(md, new EngineValues());
            var engine = new StatisticsEngine(
                context,
                new StatisticsWindow(5),
                new List<IStatisticModel>
                {
                    new MeanModel(),
                    new SkewnessModel(),
                    new KurtosisModel()
                });

            // Bars 0..4: every observation is 100 -> zero variance.
            // Nothing is ever published; initial 0.0 is retained and
            // no NaN/Infinity ever reaches the runtime.
            for (int i = 0; i <= 4; i++)
                Step(context, engine, i);

            Assert.Equal(0.0, context.Values.Statistics.Shape.Skewness);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Kurtosis);
            NumericAssert.IsFinite(context.Values.Statistics.Shape.Skewness);
            NumericAssert.IsFinite(context.Values.Statistics.Shape.Kurtosis);

            // Live tick: the bar 4 close moves to 110 -> variance
            // appears and both statistics publish.
            // Obs [100,100,100,100,110]:
            //   G1 = sqrt(20)/3 * 96/16^1.5 = sqrt(5) = 2.23606797749979
            //   G2 = 5.0
            md.SetClose(4, 110.0);
            Step(context, engine, 4);
            Assert.Equal(2.23606797749979,
                context.Values.Statistics.Shape.Skewness, 12);
            Assert.Equal(5.0, context.Values.Statistics.Shape.Kurtosis, 12);

            // Live tick back to a flat window: zero variance again.
            // The models signal "do not publish", so the runtime
            // RETAINS the last published values (2.236... and 5.0)
            // bit-for-bit — no reset to 0, no NaN, no exception.
            md.SetClose(4, 100.0);
            Step(context, engine, 4);
            Assert.Equal(2.23606797749979,
                context.Values.Statistics.Shape.Skewness);
            Assert.Equal(5.0, context.Values.Statistics.Shape.Kurtosis);
            NumericAssert.IsFinite(context.Values.Statistics.Shape.Skewness);
            NumericAssert.IsFinite(context.Values.Statistics.Shape.Kurtosis);
        }

        // ---------------------------------------------------------
        // J. LogReturn integration: independently calculated
        //    log(C_t / C_{t-1}) as the reference.
        // ---------------------------------------------------------

        [Fact]
        public void LogReturnSource_FeedsShapeModels_Correctly()
        {
            var closes = new[] { 100.0, 105.0, 98.0, 112.0, 104.0, 109.0 };
            var (context, engine) = Build(closes,
                windowSize: 5, source: StatisticsSource.LogReturn);

            // bar 0: no return exists (first bar).
            Step(context, engine, 0);
            Assert.Equal(0, context.Values.Statistics.ObservationCount);

            // bar 1: [ln(105/100)] n=1 ... bar 2: n=2 -> nothing.
            Step(context, engine, 1);
            Step(context, engine, 2);
            Assert.Equal(2, context.Values.Statistics.ObservationCount);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Skewness);

            // bar 3: n=3. Log returns
            //   [ln(105/100), ln(98/105), ln(112/98)]
            //   G1 = -0.4815785439725818
            Step(context, engine, 3);
            Assert.Equal(-0.4815785439725818,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Kurtosis);

            // bar 4: n=4. Log returns
            //   [ln(105/100), ln(98/105), ln(112/98), ln(104/112)]
            //   G1 = 0.5798132793158912, G2 = -2.730307325353534
            Step(context, engine, 4);
            Assert.Equal(0.5798132793158912,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-2.730307325353534,
                context.Values.Statistics.Shape.Kurtosis, 11);

            // bar 5: window full (closed [100,105,98,112,104]) +
            // live 109 -> 5 log returns
            //   [ln(105/100), ln(98/105), ln(112/98), ln(104/112), ln(109/104)]
            //   G1 = 0.13757713981338093, G2 = -1.6322135096411294
            Step(context, engine, 5);
            Assert.Equal(5, context.Values.Statistics.ObservationCount);
            Assert.Equal(0.13757713981338093,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-1.6322135096411294,
                context.Values.Statistics.Shape.Kurtosis, 11);
        }

        // ---------------------------------------------------------
        // K. Close compatibility: default source behavior unchanged.
        // ---------------------------------------------------------

        [Fact]
        public void CloseSource_DefaultBehavior_MatchesIndependentReference()
        {
            var closes = new[] { 100.0, 101.0, 104.0, 103.0, 106.0, 105.0 };
            var (context, engine) = Build(closes, windowSize: 5);

            Step(context, engine, 0);
            Step(context, engine, 1);

            // bar 2: obs [100,101,104] -> G1 = 1.293342780733375
            Step(context, engine, 2);
            Assert.Equal(1.293342780733375,
                context.Values.Statistics.Shape.Skewness, 11);

            // bar 3: obs [100,101,104,103] -> G1 = 0.0, G2 = -3.3
            Step(context, engine, 3);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-3.3, context.Values.Statistics.Shape.Kurtosis, 11);

            // bar 4: obs [100,101,104,103,106]
            //   G1 = 0.2057527970555756, G2 = -1.1172668513388697
            Step(context, engine, 4);
            Assert.Equal(0.2057527970555756,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-1.1172668513388697,
                context.Values.Statistics.Shape.Kurtosis, 11);

            // bar 5: bar 4's close (106) is committed, filling the
            // window (capacity 5) with the closed bars
            // [100,101,104,103,106], and bar 5 is the live bar
            // (105) -> observations are the 6 values
            // [100,101,104,103,106,105]
            //   G1 = -0.30028928507946373, G2 = -1.4177693761814674
            Step(context, engine, 5);
            Assert.Equal(6, context.Values.Statistics.ObservationCount);
            Assert.Equal(-0.30028928507946373,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-1.4177693761814674,
                context.Values.Statistics.Shape.Kurtosis, 11);
        }

        [Fact]
        public void RegisteringShapeModels_DoesNotAlterExistingOutputs()
        {
            // Regression: the same engine with and without the shape
            // models must produce identical ObservationCount / Mean /
            // StdDev on every bar (the NaN-skip added for shape models
            // must not change any existing model's publication).
            var closes = new[] { 100.0, 101.0, 104.0, 103.0, 106.0, 105.0 };

            var (contextPlain, enginePlain) = Build(closes,
                windowSize: 5, includeShape: false);
            var (contextShape, engineShape) = Build(closes,
                windowSize: 5, includeShape: true);

            for (int i = 0; i <= 5; i++)
            {
                Step(contextPlain, enginePlain, i);
                Step(contextShape, engineShape, i);

                Assert.Equal(
                    contextPlain.Values.Statistics.ObservationCount,
                    contextShape.Values.Statistics.ObservationCount);
                Assert.Equal(
                    contextPlain.Values.Statistics.Location.Mean,
                    contextShape.Values.Statistics.Location.Mean);
                Assert.Equal(
                    contextPlain.Values.Statistics.Dispersion.StandardDeviation,
                    contextShape.Values.Statistics.Dispersion.StandardDeviation);
            }
        }

        // ---------------------------------------------------------
        // L. SimpleReturn compatibility: models remain source-blind.
        // ---------------------------------------------------------

        [Fact]
        public void SimpleReturnSource_FeedsShapeModels_Correctly()
        {
            var closes = new[] { 100.0, 101.0, 104.0, 103.0, 106.0, 105.0 };
            var (context, engine) = Build(closes,
                windowSize: 5, source: StatisticsSource.SimpleReturn);

            // bar 5: simple returns
            //   [1/100, 3/101, -1/104, 3/103, -1/106]
            //   G1 = -0.0023327159520130066, G2 = -2.997599347393115
            for (int i = 0; i <= 5; i++)
                Step(context, engine, i);

            Assert.Equal(5, context.Values.Statistics.ObservationCount);
            Assert.Equal(-0.0023327159520130066,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-2.997599347393115,
                context.Values.Statistics.Shape.Kurtosis, 11);

            // The two return sources differ: the same closes produce
            // different shape values under SimpleReturn vs LogReturn,
            // proving the models receive the materialized observations
            // and never select the source themselves.
            var (contextLog, engineLog) = Build(closes,
                windowSize: 5, source: StatisticsSource.LogReturn);
            for (int i = 0; i <= 5; i++)
                Step(contextLog, engineLog, i);

            Assert.NotEqual(
                context.Values.Statistics.Shape.Skewness,
                contextLog.Values.Statistics.Shape.Skewness);
        }

        // ---------------------------------------------------------
        // M. Live tick: recompute from committed history + live
        //    observation, without double-counting.
        // ---------------------------------------------------------

        [Fact]
        public void LiveTick_MutatedClose_RecomputesWithoutDoubleCounting()
        {
            var md = new MutableCloseMarketData(new[]
                { 100.0, 101.0, 102.0, 103.0, 104.0, 105.0, 106.0 });
            var context = new EngineContext(md, new EngineValues());
            var engine = new StatisticsEngine(
                context,
                new StatisticsWindow(5),
                new List<IStatisticModel>
                {
                    new SkewnessModel(),
                    new KurtosisModel()
                });

            for (int i = 0; i <= 5; i++)
                Step(context, engine, i);

            // Bar 5 live: obs [100,101,102,103,104,105].
            Assert.Equal(6, context.Values.Statistics.ObservationCount);

            // Tick the live bar's close; skew/kurt must recompute from
            // the SAME committed window + the NEW live close, and the
            // observation count must not grow (no double-counting).
            double[] expectedObs = { 100, 101, 102, 103, 104, 130 };
            md.SetClose(5, 130.0);
            Step(context, engine, 5);
            Assert.Equal(6, context.Values.Statistics.ObservationCount);
            Assert.Equal(
                new SkewnessModel().Compute(new StatisticsInput(expectedObs)),
                context.Values.Statistics.Shape.Skewness);
            Assert.Equal(
                new KurtosisModel().Compute(new StatisticsInput(expectedObs)),
                context.Values.Statistics.Shape.Kurtosis);

            md.SetClose(5, 80.0);
            Step(context, engine, 5);
            Assert.Equal(6, context.Values.Statistics.ObservationCount);
            Assert.Equal(
                new SkewnessModel().Compute(
                    new StatisticsInput(new double[] { 100, 101, 102, 103, 104, 80 })),
                context.Values.Statistics.Shape.Skewness);

            // Bar 6 opens: bar 5's LAST tick (80) is committed and
            // rolls out the oldest closed bar (100).
            // Obs [101,102,103,104,80,106].
            Step(context, engine, 6);
            Assert.Equal(6, context.Values.Statistics.ObservationCount);
            Assert.Equal(
                new SkewnessModel().Compute(
                    new StatisticsInput(new double[] { 101, 102, 103, 104, 80, 106 })),
                context.Values.Statistics.Shape.Skewness);
        }

        // ---------------------------------------------------------
        // N. Fresh-pass equivalence.
        // ---------------------------------------------------------

        [Fact]
        public void FreshPass_AfterReset_MatchesCleanRun()
        {
            double[] closes = { 100, 97, 103, 99, 106, 102, 98, 104 };
            var (truthSkew, truthKurt) = GroundTruth(closes, 7);

            var (context, engine) = Build(closes, windowSize: 5);
            var harness = new StatisticsHarness(context, engine);

            // Build some state, then restart from bar 0 (fresh pass).
            harness.Calculate(0);
            harness.Calculate(1);
            harness.Calculate(2);
            harness.Calculate(3);
            harness.Calculate(0);   // fresh pass

            AssertShapeEqual(0, truthSkew, truthKurt, context);

            for (int i = 1; i <= 7; i++)
            {
                harness.Calculate(i);
                AssertShapeEqual(i, truthSkew, truthKurt, context);
            }
        }

        // ---------------------------------------------------------
        // O. Discontinuity recovery.
        // ---------------------------------------------------------

        [Theory]
        [InlineData(new[] { 0, 1, 2, 5 })]
        [InlineData(new[] { 0, 1, 2, 3, 2, 3, 4 })]
        public void Discontinuity_RecoversToCleanRunValues(int[] sequence)
        {
            double[] closes = { 100, 97, 103, 99, 106, 102, 98, 104 };
            var (truthSkew, truthKurt) = GroundTruth(closes, 7);

            var (context, engine) = Build(closes, windowSize: 5);
            var harness = new StatisticsHarness(context, engine);

            foreach (int idx in sequence)
                harness.Calculate(idx);

            // Every visited bar's FINAL published values equal the
            // clean sequential run, and continuation stays clean.
            foreach (int idx in sequence)
            {
                Assert.Equal(truthSkew[idx], harness.Published[idx].Skew);
                Assert.Equal(truthKurt[idx], harness.Published[idx].Kurt);
            }

            for (int i = sequence[sequence.Length - 1] + 1; i <= 7; i++)
            {
                harness.Calculate(i);
                Assert.Equal(truthSkew[i], harness.Published[i].Skew);
                Assert.Equal(truthKurt[i], harness.Published[i].Kurt);
            }
        }

        // ---------------------------------------------------------
        // P. Determinism: bit-for-bit identical outputs.
        // ---------------------------------------------------------

        [Fact]
        public void Determinism_TwoIdenticalRuns_BitForBitIdentical()
        {
            double[] closes = { 100, 97, 103, 99, 106, 102, 98, 104 };

            var (contextA, engineA) = Build(closes, windowSize: 5);
            var (contextB, engineB) = Build(closes, windowSize: 5);

            for (int i = 0; i <= 7; i++)
            {
                Step(contextA, engineA, i);
                Step(contextB, engineB, i);

                // Exact double equality (no tolerance).
                Assert.Equal(
                    contextA.Values.Statistics.Shape.Skewness,
                    contextB.Values.Statistics.Shape.Skewness);
                Assert.Equal(
                    contextA.Values.Statistics.Shape.Kurtosis,
                    contextB.Values.Statistics.Shape.Kurtosis);
            }
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private static (double[] skew, double[] kurt) GroundTruth(
            double[] closes, int lastIndex)
        {
            var (context, engine) = Build(closes, windowSize: 5);
            var skew = new double[lastIndex + 1];
            var kurt = new double[lastIndex + 1];

            for (int i = 0; i <= lastIndex; i++)
            {
                Step(context, engine, i);
                skew[i] = context.Values.Statistics.Shape.Skewness;
                kurt[i] = context.Values.Statistics.Shape.Kurtosis;
            }

            return (skew, kurt);
        }

        private static void AssertShapeEqual(
            int index, double[] truthSkew, double[] truthKurt,
            EngineContext context)
        {
            Assert.Equal(truthSkew[index],
                context.Values.Statistics.Shape.Skewness);
            Assert.Equal(truthKurt[index],
                context.Values.Statistics.Shape.Kurtosis);
        }

        /// <summary>
        /// Mirrors the indicator's Calculate policy for the Statistics
        /// stage: re-tick and next-bar are contiguous, any other
        /// transition resets the engine and replays bars 0..index-1
        /// before processing the target bar.
        /// </summary>
        private sealed class StatisticsHarness
        {
            private readonly EngineContext _context;
            private readonly StatisticsEngine _engine;
            private int _lastProcessedIndex = -1;

            /// <summary>
            /// The FINAL published shape values per bar index, so the
            /// test can compare every visited bar against the clean
            /// run even after later bars have overwritten the shared
            /// runtime values.
            /// </summary>
            public readonly Dictionary<int, (double Skew, double Kurt)>
                Published = new();

            public StatisticsHarness(
                EngineContext context, StatisticsEngine engine)
            {
                _context = context;
                _engine = engine;
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
                    _engine.Reset();
                    for (int i = 0; i < index; i++)
                    {
                        _context.SetIndex(i);
                        _engine.Update();
                    }
                }

                _context.SetIndex(index);
                _engine.Update();
                _lastProcessedIndex = index;

                Published[index] = (
                    _context.Values.Statistics.Shape.Skewness,
                    _context.Values.Statistics.Shape.Kurtosis);
            }
        }
    }
}

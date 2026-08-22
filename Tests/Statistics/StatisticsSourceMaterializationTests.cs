using System;
using System.Collections.Generic;
using System.Linq;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Statistics
{
    /// <summary>
    /// Verifies <see cref="StatisticsSource"/> materialization semantics:
    ///   * Close      — observations are raw closes (default, unchanged).
    ///   * SimpleReturn — r_t = C_t / C_{t-1} - 1 derived from adjacent
    ///     closes of the retained window; no synthetic first observation.
    ///   * LogReturn  — r_t = ln(C_t / C_{t-1}).
    ///
    /// Expected statistics are computed INDEPENDENTLY in this file with
    /// plain formulas over known close arrays — never by re-running the
    /// engine's own pipeline against itself.
    ///
    /// Window-boundary contract (explicit): the rolling window stores
    /// N closed-bar final closes plus the live close. The represented
    /// return series covers ONLY adjacent pairs inside that retained
    /// close sequence — the first retained return uses the OLDEST
    /// RETAINED close as its base; no earlier close is required.
    /// </summary>
    public sealed class StatisticsSourceMaterializationTests
    {
        // ---------------------------------------------------------
        // Mutable, optionally future-guarded market data.
        // OHLCV stay mutually consistent when a close mutates.
        // ---------------------------------------------------------

        private sealed class GuardedSeries : IPriceSeries
        {
            private readonly double[] _values;
            private readonly Func<int> _allowMax;

            public long Accesses;

            public GuardedSeries(double[] values, Func<int> allowMax)
            {
                _values = values;
                _allowMax = allowMax;
            }

            public int Count => _values.Length;

            public double this[int index]
            {
                get
                {
                    int max = _allowMax();
                    Assert.True(index <= max,
                        $"Future-data access: index {index} > allowed {max}");
                    Accesses++;
                    return _values[index];
                }
            }
        }

        private sealed class MutableOhlcvMarketData : IMarketData
        {
            public readonly double[] CloseValues;

            private readonly double[] _high;
            private readonly double[] _low;
            private readonly GuardedSeries _open;
            private readonly GuardedSeries _highSeries;
            private readonly GuardedSeries _lowSeries;
            private readonly GuardedSeries _close;
            private readonly GuardedSeries _volume;
            private int _allowMax = int.MaxValue;

            public MutableOhlcvMarketData(double[] closes)
            {
                CloseValues = (double[])closes.Clone();
                _high = new double[CloseValues.Length];
                _low = new double[CloseValues.Length];

                for (int i = 0; i < CloseValues.Length; i++)
                {
                    _high[i] = CloseValues[i] + 0.3;
                    _low[i] = CloseValues[i] - 0.3;
                }

                _open = new GuardedSeries(CloseValues, () => _allowMax);
                _highSeries = new GuardedSeries(_high, () => _allowMax);
                _lowSeries = new GuardedSeries(_low, () => _allowMax);
                _close = new GuardedSeries(CloseValues, () => _allowMax);
                _volume = new GuardedSeries(
                    Enumerable.Repeat(100.0, CloseValues.Length).ToArray(),
                    () => _allowMax);
            }

            /// <summary>
            /// Re-ticks a bar's close; high/low move with it so the
            /// bar stays mutually consistent.
            /// </summary>
            public void MutateClose(int index, double value)
            {
                CloseValues[index] = value;
                _high[index] = value + 0.3;
                _low[index] = value - 0.3;
            }

            public IPriceSeries Open => _open;
            public IPriceSeries High => _highSeries;
            public IPriceSeries Low => _lowSeries;
            public IPriceSeries Close => _close;
            public IPriceSeries Volume => _volume;
            public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
            public int Count => CloseValues.Length;
            public bool MoveNext() => false;

            public void SetAllowMax(int max) => _allowMax = max;

            /// <summary>Total accesses across all five series.</summary>
            public long Accesses =>
                _open.Accesses + _highSeries.Accesses +
                _lowSeries.Accesses + _close.Accesses + _volume.Accesses;
        }

        // ---------------------------------------------------------
        // Engine construction.
        // ---------------------------------------------------------

        private static ResearchFeatureEngine BuildEngine(
            MutableOhlcvMarketData md,
            int windowSize,
            StatisticsSource source)
        {
            var options = new EngineOptions
            {
                StatisticsWindowSize = windowSize,
                StatisticsSource = source
            };

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                new ATRSmoothReferenceSource(
                    new ATRSmoothConfiguration(atrPeriod: 14,
                        atrMultiplier: 5.1,
                        smoothLength: 5)),
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel>
                {
                    new MeanModel(),
                    new MedianModel(),
                    new MinimumModel(),
                    new MaximumModel(),
                    new VarianceModel(),
                    new StandardDeviationModel(),
                    new MedianAbsoluteDeviationModel(),
                    new RangeModel()
                },
                options);

            return new ResearchFeatureEngineBuilder(configuration)
                .Build();
        }

        // ---------------------------------------------------------
        // INDEPENDENT reference calculations (deliberately plain
        // formulas — no engine types involved).
        // ---------------------------------------------------------

        private static double[] IndependentSimpleReturns(IReadOnlyList<double> closes)
        {
            var result = new double[closes.Count - 1];
            for (int i = 0; i < result.Length; i++)
                result[i] = closes[i + 1] / closes[i] - 1.0;
            return result;
        }

        private static double[] IndependentLogReturns(IReadOnlyList<double> closes)
        {
            var result = new double[closes.Count - 1];
            for (int i = 0; i < result.Length; i++)
                result[i] = Math.Log(closes[i + 1] / closes[i]);
            return result;
        }

        private static double IndependentMean(double[] values)
        {
            double sum = 0.0;
            foreach (double v in values)
                sum += v;
            return sum / values.Length;
        }

        private static double IndependentMinimum(double[] values)
        {
            double best = values[0];
            foreach (double v in values)
                if (v < best) best = v;
            return best;
        }

        private static double IndependentMaximum(double[] values)
        {
            double best = values[0];
            foreach (double v in values)
                if (v > best) best = v;
            return best;
        }

        /// <summary>
        /// The close sequence the engine retains after a sequential run
        /// through bar <paramref name="t"/> with window capacity
        /// <paramref name="w"/>: closed bars max(0, t-w)..t-1 followed
        /// by the live close C_t. Returns are the adjacent pairs of
        /// that sequence.
        /// </summary>
        private static double[] RetainedCloseSequence(
            double[] closes, int t, int w)
        {
            int first = Math.Max(0, t - w);
            var sequence = new List<double>(t - first + 1);
            for (int i = first; i <= t - 1; i++)
                sequence.Add(closes[i]);
            sequence.Add(closes[t]);
            return sequence.ToArray();
        }

        private static double[] ExpectedReturns(
            double[] closes, int t, int w, StatisticsSource source)
        {
            double[] sequence = RetainedCloseSequence(closes, t, w);

            return source == StatisticsSource.SimpleReturn
                ? IndependentSimpleReturns(sequence)
                : IndependentLogReturns(sequence);
        }

        // ---------------------------------------------------------
        // Parameterization.
        // ---------------------------------------------------------

        public static IEnumerable<object[]> ReturnSources()
        {
            yield return new object[] { StatisticsSource.SimpleReturn };
            yield return new object[] { StatisticsSource.LogReturn };
        }

        public static IEnumerable<object[]> PriceSequences()
        {
            yield return new object[]
            {
                "rising",
                Enumerable.Range(0, 12).Select(i => 100.0 + 2.0 * i).ToArray()
            };
            yield return new object[]
            {
                "falling",
                Enumerable.Range(0, 12).Select(i => 130.0 - 2.0 * i).ToArray()
            };
            yield return new object[]
            {
                "mixed-fractional",
                new double[]
                {
                    50.25, 51.10, 49.87, 52.03, 51.44,
                    50.90, 53.21, 52.77, 54.05, 53.60
                }
            };
            yield return new object[]
            {
                "large-values",
                new double[]
                {
                    250000.5, 251300.75, 249800.25,
                    252450.0, 251000.5, 253900.25
                }
            };
            yield return new object[]
            {
                "small-positive",
                new double[]
                {
                    0.00010, 0.000105, 0.0000995,
                    0.00011, 0.0001045, 0.000112
                }
            };
        }

        // ---------------------------------------------------------
        // Section 1 — source selection & default compatibility.
        // ---------------------------------------------------------

        [Fact]
        public void EngineOptions_Default_UsesCloseSource()
        {
            Assert.Equal(StatisticsSource.Close,
                EngineOptions.Default.StatisticsSource);

            Assert.Equal(StatisticsSource.Close,
                new EngineOptions().StatisticsSource);
        }

        [Fact]
        public void UnspecifiedSource_BehavesIdenticallyToExplicitClose()
        {
            double[] closes = Enumerable.Range(0, 14)
                .Select(i => 98.0 + 1.7 * i + (i % 3) * 0.4)
                .ToArray();

            // Engine built WITHOUT touching StatisticsSource.
            var defaultMd = new MutableOhlcvMarketData(closes);
            var defaultConfig = new EngineConfiguration(
                defaultMd,
                new EngineValues(),
                new ATRSmoothReferenceSource(new ATRSmoothConfiguration(14, 5.1, 5)),
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new List<IStatisticModel> { new MeanModel(), new MaximumModel() },
                new EngineOptions { StatisticsWindowSize = 6 });
            var defaultEngine =
                new ResearchFeatureEngineBuilder(defaultConfig).Build();

            // Engine built WITH an explicit Close source.
            var closeMd = new MutableOhlcvMarketData(closes);
            var closeEngine = BuildEngine(closeMd, 6, StatisticsSource.Close);

            for (int i = 0; i < closes.Length; i++)
            {
                defaultEngine.ProcessAt(i);
                closeEngine.ProcessAt(i);

                Assert.Equal(
                    closeEngine.Values.Statistics.Location.Mean,
                    defaultEngine.Values.Statistics.Location.Mean);
                Assert.Equal(
                    closeEngine.Values.Statistics.ObservationCount,
                    defaultEngine.Values.Statistics.ObservationCount);
            }
        }

        [Fact]
        public void CloseSource_ObservationsAreCloses_IncludingLiveBar()
        {
            double[] closes = { 100.0, 102.5, 99.75, 101.25, 103.0, 104.5 };
            var md = new MutableOhlcvMarketData(closes);
            var engine = BuildEngine(md, 5, StatisticsSource.Close);

            for (int i = 0; i < closes.Length; i++)
                engine.ProcessAt(i);

            // Only five closed bars exist, so the committed window
            // holds bars 0..4 and the live bar 5 appends its close.
            // Observations = [100.0, 102.5, 99.75, 101.25, 103.0,
            // 104.5].
            double expectedMean =
                (100.0 + 102.5 + 99.75 + 101.25 + 103.0 + 104.5) / 6.0;

            Assert.Equal(6, engine.Values.Statistics.ObservationCount);
            Assert.Equal(expectedMean,
                engine.Values.Statistics.Location.Mean, 12);
            Assert.Equal(99.75, engine.Values.Statistics.Range.Minimum, 12);
            Assert.Equal(104.5, engine.Values.Statistics.Range.Maximum, 12);
        }

        // ---------------------------------------------------------
        // Section 2 — independent numerical equality.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(PriceSequences))]
        public void SimpleReturn_MatchesIndependentReference(string _, double[] closes)
            => RunSourceMatchesReference(StatisticsSource.SimpleReturn, closes);

        [Theory]
        [MemberData(nameof(PriceSequences))]
        public void LogReturn_MatchesIndependentReference(string _, double[] closes)
            => RunSourceMatchesReference(StatisticsSource.LogReturn, closes);

        private void RunSourceMatchesReference(
            StatisticsSource source, double[] closes)
        {
            // Window larger than the dataset: ALL returns are retained,
            // so the published statistics describe the complete return
            // series r_1..r_{n-1}.
            var md = new MutableOhlcvMarketData(closes);
            var engine = BuildEngine(md, closes.Length + 5, source);

            var s = engine.Values.Statistics;

            for (int i = 0; i < closes.Length; i++)
                engine.ProcessAt(i);

            double[] expectedReturns = source == StatisticsSource.SimpleReturn
                ? IndependentSimpleReturns(closes)
                : IndependentLogReturns(closes);

            Assert.Equal(closes.Length - 1, s.ObservationCount);

            Assert.Equal(IndependentMean(expectedReturns),
                s.Location.Mean, 12);
            Assert.Equal(IndependentMinimum(expectedReturns),
                s.Range.Minimum, 12);
            Assert.Equal(IndependentMaximum(expectedReturns),
                s.Range.Maximum, 12);
            Assert.Equal(
                IndependentMaximum(expectedReturns) -
                IndependentMinimum(expectedReturns),
                s.Range.Range, 12);

            // Sample variance (n-1 denominator), matching the model.
            double mean = IndependentMean(expectedReturns);
            double sumSq = 0.0;
            foreach (double r in expectedReturns)
                sumSq += (r - mean) * (r - mean);
            double expectedVariance = sumSq / (expectedReturns.Length - 1);

            Assert.Equal(expectedVariance, s.Dispersion.Variance, 10);
            Assert.Equal(Math.Sqrt(expectedVariance),
                s.Dispersion.StandardDeviation, 10);
        }

        // ---------------------------------------------------------
        // Section 3 — warm-up semantics.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void FirstBar_ProducesNoSyntheticReturn(StatisticsSource source)
        {
            double[] closes = { 100.0, 102.0, 99.0, 101.0 };
            var md = new MutableOhlcvMarketData(closes);
            var engine = BuildEngine(md, 4, source);

            engine.ProcessAt(0);

            var s = engine.Values.Statistics;
            Assert.Equal(0, s.ObservationCount);
            Assert.Equal(0.0, s.Location.Mean);
            Assert.Equal(0.0, s.Dispersion.Variance);
            Assert.Equal(0.0, s.Range.Minimum);
            Assert.Equal(0.0, s.Range.Maximum);
            NumericAssert.IsFinite(s.Location.Mean);
        }

        [Fact]
        public void FirstValidReturn_AtIndex1_ExactlyComputed()
        {
            double[] closes = { 100.0, 101.0, 99.0 };

            var simpleMd = new MutableOhlcvMarketData(closes);
            var simpleEngine =
                BuildEngine(simpleMd, 4, StatisticsSource.SimpleReturn);
            simpleEngine.ProcessAt(0);
            simpleEngine.ProcessAt(1);

            Assert.Equal(1, simpleEngine.Values.Statistics.ObservationCount);
            Assert.Equal(0.01,
                simpleEngine.Values.Statistics.Location.Mean, 15);

            var logMd = new MutableOhlcvMarketData(closes);
            var logEngine = BuildEngine(logMd, 4, StatisticsSource.LogReturn);
            logEngine.ProcessAt(0);
            logEngine.ProcessAt(1);

            Assert.Equal(1, logEngine.Values.Statistics.ObservationCount);
            Assert.Equal(Math.Log(1.01),
                logEngine.Values.Statistics.Location.Mean, 15);
        }

        // ---------------------------------------------------------
        // Section 4 — rolling-window count & boundary contract.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void ObservationCount_Progression_IsBoundedByWindow(
            StatisticsSource source)
        {
            double[] closes =
                { 100.0, 102.0, 99.0, 101.5, 103.25, 100.5, 104.0, 106.5 };
            var md = new MutableOhlcvMarketData(closes);
            const int windowSize = 3;
            var engine = BuildEngine(md, windowSize, source);

            for (int t = 0; t < closes.Length; t++)
            {
                engine.ProcessAt(t);

                int expected = Math.Min(windowSize, t);
                Assert.Equal(expected,
                    engine.Values.Statistics.ObservationCount);

                if (t == 0)
                    continue;

                double[] expectedReturns =
                    ExpectedReturns(closes, t, windowSize, source);

                Assert.Equal(IndependentMean(expectedReturns),
                    engine.Values.Statistics.Location.Mean, 12);
            }
        }

        [Fact]
        public void WindowBoundary_FirstRetainedReturn_UsesOldestRetainedClose()
        {
            // Distinct closes make every possible base distinguishable.
            double[] closes =
                { 100.0, 104.0, 97.5, 111.0, 93.25, 120.5, 88.75, 131.25 };
            var md = new MutableOhlcvMarketData(closes);
            const int windowSize = 5;
            var engine = BuildEngine(md, windowSize, StatisticsSource.SimpleReturn);

            for (int i = 0; i < closes.Length; i++)
                engine.ProcessAt(i);

            // At t=7 with capacity 5 the committed window holds the
            // FINAL closes of bars 2..6 and bar 7 is live. The retained
            // close sequence is [C2..C6, C7]; the FIRST retained return
            // must be C3/C2 - 1 — built from the oldest RETAINED close
            // C2 alone. C1 (or any earlier close) must NOT participate:
            // the expected statistics below are computed exclusively
            // over pairs inside [C2..C7].
            double[] retained =
                { closes[2], closes[3], closes[4], closes[5], closes[6], closes[7] };
            double[] expected = IndependentSimpleReturns(retained);

            Assert.Equal(5, engine.Values.Statistics.ObservationCount);
            Assert.Equal(expected.Length,
                engine.Values.Statistics.ObservationCount);
            Assert.Equal(IndependentMean(expected),
                engine.Values.Statistics.Location.Mean, 12);
            Assert.Equal(IndependentMaximum(expected),
                engine.Values.Statistics.Range.Maximum, 12);
            Assert.Equal(IndependentMinimum(expected),
                engine.Values.Statistics.Range.Minimum, 12);
        }

        // ---------------------------------------------------------
        // Section 5 — live-bar mutation.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void LiveBarReTick_ReturnMutatesWithCurrentClose(
            StatisticsSource source)
        {
            double[] initial =
                { 100.0, 102.0, 101.0, 103.0, 104.0, 105.0, 106.0 };
            var md = new MutableOhlcvMarketData(initial);
            const int windowSize = 4;
            var engine = BuildEngine(md, windowSize, source);

            for (int i = 0; i <= 4; i++)
                engine.ProcessAt(i);

            int countBeforeReticks =
                engine.Values.Statistics.ObservationCount;
            Assert.Equal(4, countBeforeReticks);

            // The live bar's close changes repeatedly; each re-tick
            // must recompute ONLY the last return against the still-
            // committed previous close C_4 (= 104). No additional
            // observation may be created.
            double[] liveCloses = { 108.0, 106.5, 107.25, 104.0001 };

            foreach (double liveClose in liveCloses)
            {
                md.MutateClose(5, liveClose);
                md.SetAllowMax(5);
                engine.ProcessAt(5);

                double[] retained =
                    ExpectedReturns(md.CloseValues, 5, windowSize, source);

                Assert.Equal(countBeforeReticks,
                    engine.Values.Statistics.ObservationCount);
                Assert.Equal(IndependentMean(retained),
                    engine.Values.Statistics.Location.Mean, 12);
            }

            // Bar 5 finally closes: its LAST tick value (104.0001) is
            // what gets committed and becomes the next return's base.
            md.SetAllowMax(6);
            md.MutateClose(6, 109.0);
            engine.ProcessAt(6);

            double[] afterCommit =
                ExpectedReturns(md.CloseValues, 6, windowSize, source);
            Assert.Equal(IndependentMean(afterCommit),
                engine.Values.Statistics.Location.Mean, 12);
        }

        // ---------------------------------------------------------
        // Section 6 — constant prices.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void ConstantPositivePrices_YieldExactlyZeroReturns(
            StatisticsSource source)
        {
            double[] closes = Enumerable.Repeat(100.0, 10).ToArray();
            var md = new MutableOhlcvMarketData(closes);
            var engine = BuildEngine(md, closes.Length, source);

            for (int i = 0; i < closes.Length; i++)
                engine.ProcessAt(i);

            var s = engine.Values.Statistics;
            Assert.Equal(closes.Length - 1, s.ObservationCount);

            // C_t == C_{t-1} exactly -> ratio exactly 1 ->
            // simple return exactly 0, ln(1) exactly 0. Downstream
            // zero-variance handling remains governed by the existing
            // statistic/validation semantics.
            Assert.Equal(0.0, s.Location.Mean);
            Assert.Equal(0.0, s.Dispersion.Variance);
            Assert.Equal(0.0, s.Dispersion.StandardDeviation);
            Assert.Equal(0.0, s.Range.Minimum);
            Assert.Equal(0.0, s.Range.Maximum);
            Assert.Equal(0.0, s.Range.Range);
        }

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void NearConstantPrices_RemainNumericallySensible(
            StatisticsSource source)
        {
            // C_t ≈ C_{t-1}: tiny moves produce tiny returns, far from
            // overflow/underflow territory, with no NaN or infinity.
            double[] closes =
                {
                    100.0, 100.0 + 1e-9, 100.0 - 1e-9,
                    100.0 + 2e-9, 100.0 - 5e-10
                };
            var md = new MutableOhlcvMarketData(closes);
            var engine = BuildEngine(md, closes.Length, source);

            for (int i = 0; i < closes.Length; i++)
                engine.ProcessAt(i);

            double[] expected = source == StatisticsSource.SimpleReturn
                ? IndependentSimpleReturns(closes)
                : IndependentLogReturns(closes);

            Assert.Equal(IndependentMean(expected),
                engine.Values.Statistics.Location.Mean, 15);
            foreach (double value in expected)
                NumericAssert.IsFinite(value);
        }

        // ---------------------------------------------------------
        // Section 7 — invalid input policy (fail loud).
        //
        // Driven through a standalone StatisticsEngine so the guard
        // under test — not an upstream stage — raises the failure.
        // ---------------------------------------------------------

        private static (EngineContext Context, StatisticsEngine Engine)
            BuildStandaloneEngine(IMarketData md, StatisticsSource source)
        {
            var context = new EngineContext(md, new EngineValues());

            var engine = new StatisticsEngine(
                context,
                new StatisticsWindow(8),
                new List<IStatisticModel> { new MeanModel() },
                source);

            return (context, engine);
        }

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void NonPositivePreviousClose_FailsLoud(StatisticsSource source)
        {
            var md = new MutableOhlcvMarketData(new double[] { 0.0, 100.0 });
            var (context, engine) = BuildStandaloneEngine(md, source);

            context.SetIndex(0);
            engine.Update();          // warm-up bar: no observation yet

            context.SetIndex(1);

            Assert.Throws<InvalidOperationException>(() => engine.Update());
        }

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void NonPositiveCurrentClose_FailsLoud(StatisticsSource source)
        {
            var md = new MutableOhlcvMarketData(new double[] { 100.0, -5.0 });
            var (context, engine) = BuildStandaloneEngine(md, source);

            context.SetIndex(0);
            engine.Update();

            context.SetIndex(1);

            Assert.Throws<InvalidOperationException>(() => engine.Update());
        }

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void ZeroCommittedClose_FailsLoudOnLaterBar(StatisticsSource source)
        {
            var md = new MutableOhlcvMarketData(
                new double[] { 100.0, 50.0, 0.0 });
            var (context, engine) = BuildStandaloneEngine(md, source);

            context.SetIndex(0);
            engine.Update();
            context.SetIndex(1);
            engine.Update();

            context.SetIndex(2);

            Assert.Throws<InvalidOperationException>(() => engine.Update());
        }

        [Fact]
        public void CloseSource_ToleratesAnyFiniteCloseValues()
        {
            // The positive-price policy applies to RETURN sources only;
            // the default Close source keeps its historical behavior.
            var md = new MutableOhlcvMarketData(
                new double[] { 100.0, 0.0, -5.0 });
            var (context, engine) = BuildStandaloneEngine(md, StatisticsSource.Close);
            var s = context.Values.Statistics;

            context.SetIndex(0);
            engine.Update();
            Assert.Equal(1, s.ObservationCount);

            context.SetIndex(1);
            engine.Update();
            Assert.Equal(2, s.ObservationCount);

            context.SetIndex(2);
            engine.Update();
            Assert.Equal(3, s.ObservationCount);
            Assert.Equal((100.0 + 0.0 + -5.0) / 3.0,
                s.Location.Mean, 12);
        }

        // ---------------------------------------------------------
        // Section 8 — no lookahead.
        // ---------------------------------------------------------

        [Theory]
        [MemberData(nameof(ReturnSources))]
        public void ReturnSources_NeverAccessFutureBars(StatisticsSource source)
        {
            // A guarded data source rejects ANY access beyond the
            // currently processed index while counting accesses.
            // SimpleReturn at t may depend only on C_t and C_{t-1};
            // C_{t+1} must never be read.
            var rng = new Random(20260822);
            var closes = new double[24];
            double price = 55.0;
            for (int i = 0; i < closes.Length; i++)
            {
                price += (rng.NextDouble() - 0.5) * 1.5;
                closes[i] = price;
            }

            var md = new MutableOhlcvMarketData(closes);
            var (context, engine) = BuildStandaloneEngine(md, source);

            for (int t = 0; t < closes.Length; t++)
            {
                md.SetAllowMax(t);
                context.SetIndex(t);
                engine.Update();

                // Same-index re-ticks must equally respect the guard.
                md.SetAllowMax(t);
                context.SetIndex(t);
                engine.Update();

                Assert.True(md.Accesses > 0);
            }
        }
    }
}

using System;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reversal;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reversal
{
    /// <summary>
    /// Darvas reversal-transition tests for the generic
    /// <see cref="ReversalEngine"/> in the DEFAULT
    /// <see cref="ReversalMode.TrailingStopPosition"/> mode (the
    /// regime-transition mode).
    ///
    /// These tests drive the engine in isolation with the
    /// source-declared Darvas positional regime set independently of
    /// the close-vs-reference relation — the same decoupling
    /// technique as ReversalRegimeFlipSemanticTests. The ratified
    /// transition table (strict transitions of Reference.Regime):
    ///
    /// <code>
    ///   0 → +1 = Up          +1 → 0 = Down
    ///   0 → -1 = Down        -1 → 0 = Up
    ///  -1 → +1 = Up          +1 → -1 = Down
    ///   0 →  0 / +1 → +1 / -1 → -1 = no reversal
    /// </code>
    ///
    /// Darvas 0 ("inside box") must NOT be treated as bearish: the
    /// engine operates on the signed state, not on a
    /// "positive vs non-positive" side split.
    /// </summary>
    public sealed class DarvasReversalTransitionTests
    {
        private static (ReversalEngine engine, EngineContext context, EngineValues values)
            Create()
        {
            var values = new EngineValues();
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            // Default mode = TrailingStopPosition = generic regime
            // transition mode (the mode under test).
            var engine = new ReversalEngine(context);
            engine.Initialize();
            return (engine, context, values);
        }

        private static void Process(
            ReversalEngine engine,
            EngineContext context,
            EngineValues values,
            int index,
            double regime,
            double directionalExtension)
        {
            // Simulate the Reference/Distance stages publishing for
            // this bar before the Reversal stage runs. The
            // directional extension is set independently so the
            // price-vs-line relation cannot mask regime transitions.
            values.Reference.Regime = regime;
            values.Distance.DirectionalExtension = directionalExtension;
            context.SetIndex(index);
            engine.Update();
        }

        // A directional extension that contradicts the regime move,
        // used to prove reversal detection reads the regime, not the
        // close-vs-reference relation.
        private const double OppositeExtension = -1000.0;

        [Fact]
        public void InsideToAbove_0ToPlus1_IsUpReversal()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, 0.0, OppositeExtension);
            Assert.False(values.Reversal.IsReversalBar);
            Assert.Null(values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 1, 1.0, OppositeExtension);
            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        [Fact]
        public void InsideToBelow_0ToMinus1_IsDownReversal()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, 0.0, OppositeExtension);
            Process(engine, context, values, 1, -1.0, OppositeExtension);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        [Fact]
        public void AboveToInside_Plus1To0_IsDownReversal()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, 1.0, OppositeExtension);
            Process(engine, context, values, 1, 0.0, OppositeExtension);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        [Fact]
        public void BelowToInside_Minus1To0_IsUpReversal()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, -1.0, OppositeExtension);
            Process(engine, context, values, 1, 0.0, OppositeExtension);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        [Fact]
        public void BelowToAbove_Minus1ToPlus1_IsUpTransition()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, -1.0, OppositeExtension);
            Process(engine, context, values, 1, 1.0, OppositeExtension);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        [Fact]
        public void AboveToBelow_Plus1ToMinus1_IsDownTransition()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, 1.0, OppositeExtension);
            Process(engine, context, values, 1, -1.0, OppositeExtension);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        [Fact]
        public void SameStates_NeverAReversal()
        {
            var (engine, context, values) = Create();

            // 0 → 0
            Process(engine, context, values, 0, 0.0, OppositeExtension);
            Process(engine, context, values, 1, 0.0, OppositeExtension);
            Assert.False(values.Reversal.IsReversalBar);
            Assert.Null(values.Reversal.BarsSinceReversal);

            // +1 → +1 (after an artificial jump to +1, no transition)
            var (e2, c2, v2) = Create();
            Process(e2, c2, v2, 0, 1.0, OppositeExtension);
            Process(e2, c2, v2, 1, 1.0, OppositeExtension);
            Assert.False(v2.Reversal.IsReversalBar);
            Assert.Null(v2.Reversal.BarsSinceReversal);

            // -1 → -1
            var (e3, c3, v3) = Create();
            Process(e3, c3, v3, 0, -1.0, OppositeExtension);
            Process(e3, c3, v3, 1, -1.0, OppositeExtension);
            Assert.False(v3.Reversal.IsReversalBar);
            Assert.Null(v3.Reversal.BarsSinceReversal);
        }

        [Fact]
        public void CounterIncrementsAfterReversal_AndResetsOnNext()
        {
            var (engine, context, values) = Create();

            // Inside, inside, breakout up (reversal), 2 continuations,
            // return to box (second reversal), continuation.
            Process(engine, context, values, 0, 0.0, OppositeExtension);
            Process(engine, context, values, 1, 0.0, OppositeExtension);
            Assert.Null(values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 2, 1.0, OppositeExtension);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 3, 1.0, OppositeExtension);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 4, 1.0, OppositeExtension);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 5, 0.0, OppositeExtension);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
            Assert.True(values.Reversal.IsReversalBar);

            Process(engine, context, values, 6, 0.0, OppositeExtension);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.False(values.Reversal.IsReversalBar);
        }

        [Fact]
        public void WarmupRegime0ToBoxedRegime0_IsNotAReversal()
        {
            // The Darvas warm-up regime is 0 (no box) and the first
            // confirmed box typically also yields 0 (close inside).
            // That 0 → 0 transition must NOT be flagged.
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, 0.0, OppositeExtension);
            Process(engine, context, values, 1, 0.0, OppositeExtension);

            Assert.False(values.Reversal.IsReversalBar);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
        }

        [Fact]
        public void NaNRegime_Throws()
        {
            var (engine, context, values) = Create();
            values.Reference.Regime = double.NaN;
            context.SetIndex(0);
            Assert.Throws<InvalidOperationException>(
                () => engine.Update());
        }

        // -------------------------------------------------------------
        // Full-pipeline re-tick determinism (§15): repeated
        // processing of the same index must reproduce identical
        // Price, Regime, Distance, Normalization, and reversal
        // outputs.
        // -------------------------------------------------------------

        [Fact]
        public void Pipeline_ReTickSameIndex_AllOutputsIdentical()
        {
            double[] high =
            {
                102.0, 101.0, 100.5, 101.0, 100.0,
                100.5, 110.0, 105.0, 104.0, 103.5,
                104.0, 103.0, 102.0, 113.0, 106.0
            };
            double[] low =
            {
                99.0, 98.5, 98.0, 98.5, 99.0,
                96.0, 97.0, 97.5, 98.0, 98.5,
                99.0, 100.0, 101.0, 108.0, 100.0
            };
            double[] close =
            {
                100.0, 99.5, 99.0, 99.5, 100.0,
                100.2, 108.0, 103.0, 102.5, 103.0,
                103.5, 102.5, 101.5, 112.0, 105.0
            };

            int n = close.Length;
            double[] open = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                new DarvasBoxReferenceSource(new DarvasBoxConfiguration(5)),
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel>
                {
                    new MeanModel()
                },
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration)
                .Build();

            // Straight pass 0..14, capturing bar 13 (breakout bar,
            // close 112 > Upper 110 -> regime +1 -> Up reversal) and
            // bar 14 (return to box -> Down reversal).
            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
            }

            double price1 = engine.Values.Reference.Price;
            double regime1 = engine.Values.Reference.Regime;
            double dir1 = engine.Values.Distance.DirectionalExtension;
            double abs1 = engine.Values.Distance.AbsoluteExtension;
            double norm1 = engine.Values.Normalization.NormalizedMeasurement;
            var barsSince1 = engine.Values.Reversal.BarsSinceReversal;
            var direction1 = engine.Values.Reversal.Direction;
            bool revBar1 = engine.Values.Reversal.IsReversalBar;

            // Bar 14 IS a reversal bar in this fixture (regime +1 -> 0).
            Assert.True(revBar1);

            // Re-tick bar 14 repeatedly with unchanged data: every
            // published output must be identical.
            for (int tick = 0; tick < 10; tick++)
            {
                engine.ProcessAt(14);

                Assert.Equal(price1, engine.Values.Reference.Price);
                Assert.Equal(regime1, engine.Values.Reference.Regime);
                Assert.Equal(dir1, engine.Values.Distance.DirectionalExtension);
                Assert.Equal(abs1, engine.Values.Distance.AbsoluteExtension);
                Assert.Equal(norm1, engine.Values.Normalization.NormalizedMeasurement);
                Assert.Equal(barsSince1, engine.Values.Reversal.BarsSinceReversal);
                Assert.Equal(direction1, engine.Values.Reversal.Direction);
                Assert.Equal(revBar1, engine.Values.Reversal.IsReversalBar);
            }
        }

        // -------------------------------------------------------------
        // Pipeline-level integration: the real DarvasBoxReferenceSource
        // drives the full engine (Reference → Distance → Reversal),
        // and the reversal output must equal the strict-transition
        // computation over the source's regime series.
        // -------------------------------------------------------------

        [Fact]
        public void Pipeline_DarvasReversalMatchesStrictRegimeTransitions()
        {
            // Fixture 1 (golden suite): confirmations at 9/16/23,
            // regime series has +1 at 13, 0 at 14, -1 at 15, rest 0.
            double[] high =
            {
                102.0, 101.0, 100.5, 101.0, 100.0,
                100.5, 110.0, 105.0, 104.0, 103.5,
                104.0, 103.0, 102.0, 113.0, 106.0,
                95.0, 101.0, 103.0, 104.0, 105.0,
                120.0, 115.0, 114.0, 113.5, 112.0,
                111.0, 110.5, 109.0, 108.0, 107.0
            };
            double[] low =
            {
                99.0, 98.5, 98.0, 98.5, 99.0,
                96.0, 97.0, 97.5, 98.0, 98.5,
                99.0, 100.0, 101.0, 108.0, 100.0,
                94.0, 99.0, 100.0, 101.0, 102.0,
                103.0, 104.0, 105.0, 106.0, 107.0,
                108.0, 109.0, 106.5, 105.5, 104.5
            };
            double[] close =
            {
                100.0, 99.5, 99.0, 99.5, 100.0,
                100.2, 108.0, 103.0, 102.5, 103.0,
                103.5, 102.5, 101.5, 112.0, 105.0,
                94.0, 100.0, 101.0, 102.0, 103.0,
                104.0, 105.0, 106.0, 107.0, 108.0,
                109.0, 110.0, 108.5, 107.5, 106.0
            };

            int n = close.Length;
            double[] open = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var values = new EngineValues();
            var ctx = new EngineContext(md, values);

            var referenceEngine = new ReferenceEngine(
                ctx, new DarvasBoxReferenceSource(
                    new DarvasBoxConfiguration(5)));
            var distanceEngine = new DistanceEngine(
                ctx, new ReferenceDistanceModel(md));
            var reversalEngine = new ReversalEngine(ctx);
            referenceEngine.Initialize();
            distanceEngine.Initialize();
            reversalEngine.Initialize();

            double prevRegime = 0.0;
            bool hasPrev = false;
            int? expectedBars = null;
            ReversalDirection expectedDirection = ReversalDirection.None;

            for (int i = 0; i < n; i++)
            {
                ctx.SetIndex(i);
                referenceEngine.Update();
                distanceEngine.Update();
                reversalEngine.Update();

                double regime = values.Reference.Regime;

                if (hasPrev && regime != prevRegime)
                {
                    expectedBars = 0;
                    expectedDirection = regime > prevRegime
                        ? ReversalDirection.Up
                        : ReversalDirection.Down;
                }
                else if (expectedBars.HasValue)
                {
                    expectedBars = expectedBars.Value + 1;
                }

                Assert.Equal(expectedBars, values.Reversal.BarsSinceReversal);
                Assert.Equal(expectedDirection, values.Reversal.Direction);
                Assert.Equal(
                    hasPrev && regime != prevRegime,
                    values.Reversal.IsReversalBar);

                prevRegime = regime;
                hasPrev = true;
            }

            // The fixture must actually exercise reversals; otherwise
            // this test would be vacuous. From the oracle regime
            // series (0×13, +1@13, 0@14, -1@15, 0@16..29):
            //   bar 13: 0 -> +1 = Up    (breakout)
            //   bar 14: +1 -> 0 = Down  (return to box)
            //   bar 15: 0 -> -1 = Down  (bearish breakout)
            //   bar 16: -1 -> 0 = Up    (return to box, LAST)
            // bars 17..29 are 13 continuation bars.
            Assert.Equal(ReversalDirection.Up, expectedDirection);
            Assert.Equal(13, values.Reversal.BarsSinceReversal);
            Assert.False(values.Reversal.IsReversalBar);
        }
    }
}

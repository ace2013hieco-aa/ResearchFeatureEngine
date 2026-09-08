using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Adversarial unit tests for the ATRSmooth Regime Segment
    /// state machine (<see cref="AtrSmoothRegimeSegmentEngine"/>).
    ///
    /// The engine consumes ONLY the canonical published regime
    /// (<see cref="EngineValues.Reference.Regime"/>). These tests
    /// decouple the canonical regime from the price-vs-line
    /// relation (DirectionalExtension) exactly as
    /// ReversalRegimeFlipSemanticTests does for the Reversal stage:
    /// setting the two independently proves that a price crossing
    /// of the ATRSmooth line can never create a segment transition.
    /// </summary>
    public sealed class AtrSmoothRegimeSegmentEngineTests
    {
        private static (AtrSmoothRegimeSegmentEngine engine, EngineContext context, EngineValues values)
            Create()
        {
            var values = new EngineValues();
            // The segment engine reads only Values.Reference.Regime —
            // never MarketData. A single-bar TestMarketData satisfies
            // the non-null requirement of EngineContext.
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            var engine = new AtrSmoothRegimeSegmentEngine(context);
            engine.Initialize();
            return (engine, context, values);
        }

        /// <summary>
        /// Processes one bar with the canonical regime and the
        /// price-vs-line relation set independently.
        /// </summary>
        private static void Process(
            AtrSmoothRegimeSegmentEngine engine,
            EngineContext context,
            EngineValues values,
            int index,
            double regime,
            double directionalExtension = 1.0)
        {
            values.Reference.Regime = regime;
            values.Distance.DirectionalExtension = directionalExtension;
            context.SetIndex(index);
            engine.Update();
        }

        // Canonical regime constants (trailing-stop position bias).
        private const double WarmUp = 0.0;    // pos = 0 (uncommitted)
        private const double Bullish = 1.0;   // pos = +1
        private const double Bearish = -1.0;  // pos = -1

        // Price-vs-line constants (sign of close - reference).
        private const double PriceAboveLine = 1.0;
        private const double PriceBelowLine = -1.0;

        private static void AssertUnavailable(EngineValues values)
        {
            Assert.Equal(AtrSmoothRegimeDirection.Unavailable, values.AtrSmoothRegimeSegment.Regime);
            Assert.Null(values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Null(values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Null(values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        // -------------------------------------------------------------
        // TEST A — WARM-UP: no regime exists before canonical
        // availability, even while price crosses the line.
        // -------------------------------------------------------------

        [Fact]
        public void TestA_WarmUp_NoRegimeOrIdOrTransition()
        {
            var (engine, context, values) = Create();

            // Price whipsaws across the line while the regime is
            // uncommitted — none of it may manufacture a regime.
            Process(engine, context, values, 0, WarmUp, PriceAboveLine);
            AssertUnavailable(values);

            Process(engine, context, values, 1, WarmUp, PriceBelowLine);
            AssertUnavailable(values);

            Process(engine, context, values, 2, WarmUp, PriceAboveLine);
            AssertUnavailable(values);

            Process(engine, context, values, 3, WarmUp, PriceBelowLine);
            AssertUnavailable(values);
        }

        // -------------------------------------------------------------
        // TEST B — FIRST REGIME: ID 0, age 0, start = establishment
        // bar; establishment is NOT a transition.
        // -------------------------------------------------------------

        [Fact]
        public void TestB_FirstRegime_IdZero_AgeZero_EstablishmentNotTransition()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, WarmUp);
            Process(engine, context, values, 1, WarmUp);
            AssertUnavailable(values);

            // First established directional regime at bar 2.
            Process(engine, context, values, 2, Bullish);

            Assert.Equal(AtrSmoothRegimeDirection.Bullish, values.AtrSmoothRegimeSegment.Regime);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            // The predecessor (0) is not an established directional
            // state, so the first establishment is NOT a transition.
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        [Fact]
        public void TestB2_FirstRegime_BearishEstablishment_SameIdRules()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, WarmUp);
            Process(engine, context, values, 1, WarmUp);
            Process(engine, context, values, 2, Bearish);

            Assert.Equal(AtrSmoothRegimeDirection.Bearish, values.AtrSmoothRegimeSegment.Regime);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        // -------------------------------------------------------------
        // TEST C — LONG CONTINUATION: 150 bars without a flip.
        // -------------------------------------------------------------

        [Fact]
        public void TestC_LongContinuation_SameIdSameStart_AgeIncrementsExactlyByOne()
        {
            var (engine, context, values) = Create();

            const int n = 150;
            for (int i = 0; i < n; i++)
            {
                Process(engine, context, values, i, Bullish);

                Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
                Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeStartIndex);
                Assert.Equal(i, values.AtrSmoothRegimeSegment.RegimeAge);
                Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
                Assert.Equal(AtrSmoothRegimeDirection.Bullish, values.AtrSmoothRegimeSegment.Regime);
            }
        }

        // -------------------------------------------------------------
        // TEST D — SINGLE FLIP: ID increments by exactly 1 on the
        // flip bar; start moves to the flip bar; age resets to 0.
        // -------------------------------------------------------------

        [Fact]
        public void TestD_SingleFlip_IdIncrementsByExactlyOne()
        {
            var (engine, context, values) = Create();

            for (int i = 0; i < 5; i++)
                Process(engine, context, values, i, Bullish);

            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(4, values.AtrSmoothRegimeSegment.RegimeAge);

            // Bullish -> bearish flip at bar 5.
            Process(engine, context, values, 5, Bearish);

            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(5, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.Down, values.AtrSmoothRegimeSegment.RegimeTransition);
            Assert.Equal(AtrSmoothRegimeDirection.Bearish, values.AtrSmoothRegimeSegment.Regime);

            // Continuation after the flip: the OLD bars keep their
            // values implicitly (never rewritten — no history is
            // stored), and the new segment persists.
            Process(engine, context, values, 6, Bearish);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(5, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);

            // Reverse flip at bar 7.
            Process(engine, context, values, 7, Bullish);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(7, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.Up, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        // -------------------------------------------------------------
        // TEST E — ALTERNATING FLIPS: IDs 0..7, every flip bar is a
        // transition bar with the correct direction.
        // -------------------------------------------------------------

        [Fact]
        public void TestE_AlternatingFlips_IdsZeroThroughSeven()
        {
            var (engine, context, values) = Create();

            // + - + - + - + - over 8 bars.
            double[] regime = { Bullish, Bearish, Bullish, Bearish, Bullish, Bearish, Bullish, Bearish };
            // (index 0 = establishment; each subsequent bar flips)
            for (int i = 0; i < regime.Length; i++)
            {
                Process(engine, context, values, i, regime[i]);

                Assert.Equal(i, values.AtrSmoothRegimeSegment.RegimeId);
                Assert.Equal(i, values.AtrSmoothRegimeSegment.RegimeStartIndex);
                Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);

                if (i == 0)
                {
                    Assert.Equal(AtrSmoothRegimeTransition.None,
                        values.AtrSmoothRegimeSegment.RegimeTransition);
                }
                else
                {
                    Assert.Equal(
                        regime[i] > 0 ? AtrSmoothRegimeTransition.Up : AtrSmoothRegimeTransition.Down,
                        values.AtrSmoothRegimeSegment.RegimeTransition);
                }
            }
        }

        // -------------------------------------------------------------
        // TEST F — PRICE CROSSING WITHOUT AN ATRSMOOTH FLIP must NOT
        // create a transition (the discriminating core).
        // -------------------------------------------------------------

        [Fact]
        public void TestF_PriceCrossingWithoutFlip_NoTransition()
        {
            var (engine, context, values) = Create();

            // Establish a bullish regime at bar 0.
            Process(engine, context, values, 0, Bullish, PriceAboveLine);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);

            // Candle crosses below the line (whipsaw), regime stays
            // bullish — five crosses, zero transitions.
            double[] priceSide = { PriceBelowLine, PriceAboveLine, PriceBelowLine, PriceBelowLine, PriceAboveLine };
            int transitions = 0;

            for (int i = 0; i < priceSide.Length; i++)
            {
                Process(engine, context, values, i + 1, Bullish, priceSide[i]);

                if (values.AtrSmoothRegimeSegment.RegimeTransition != AtrSmoothRegimeTransition.None)
                    transitions++;
            }

            Assert.Equal(0, transitions);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(priceSide.Length, values.AtrSmoothRegimeSegment.RegimeAge);
        }

        [Fact]
        public void TestF2_BearishRegime_PriceCrossesAbove_NoTransition()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, WarmUp, PriceBelowLine);
            Process(engine, context, values, 1, Bearish, PriceBelowLine);

            // Price crosses above the line; the regime stays bearish.
            Process(engine, context, values, 2, Bearish, PriceAboveLine);
            Process(engine, context, values, 3, Bearish, PriceAboveLine);
            Process(engine, context, values, 4, Bearish, PriceBelowLine);

            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(3, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        // -------------------------------------------------------------
        // TEST G — RE-TICK idempotency.
        // -------------------------------------------------------------

        [Fact]
        public void TestG_ReTick_EquivalentToProcessingOnce()
        {
            var (engine, context, values) = Create();

            // Warm-up, then establish, then re-tick the same bar
            // three times: the state must equal a single processing.
            Process(engine, context, values, 0, WarmUp);
            Process(engine, context, values, 1, WarmUp);
            Process(engine, context, values, 2, WarmUp);

            Process(engine, context, values, 3, Bullish);
            Process(engine, context, values, 3, Bullish);
            Process(engine, context, values, 3, Bullish);

            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(3, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        [Fact]
        public void TestG2_ReTick_FlipBarNotDoubleApplied()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Bullish);
            Process(engine, context, values, 1, Bullish);
            Process(engine, context, values, 2, Bullish);

            // Flip bar, re-ticked three times: the ID must increment
            // exactly ONCE and the age must stay 0.
            Process(engine, context, values, 3, Bearish);
            Process(engine, context, values, 3, Bearish);
            Process(engine, context, values, 3, Bearish);

            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(3, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.Down, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        [Fact]
        public void TestG3_BarThenReTickThenAdvanceThenReTick()
        {
            // bar t / re-tick t / advance to t+1 / re-tick t+1 —
            // the resulting state must equal single processing. The
            // last re-tick also carries CHANGED data (the live-bar
            // case: the intra-bar regime value changes with the
            // latest tick), which must apply exactly once.
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, WarmUp);
            Process(engine, context, values, 1, Bullish);
            Process(engine, context, values, 1, Bullish);   // re-tick bar 1
            Process(engine, context, values, 2, Bullish);    // advance
            Process(engine, context, values, 2, Bullish);    // re-tick bar 2

            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);

            // Re-tick bar 2 with changed data: the regime flips to
            // bearish intra-bar. The flip must apply exactly once.
            Process(engine, context, values, 2, Bearish);

            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.Down, values.AtrSmoothRegimeSegment.RegimeTransition);

            // And the next bar after the re-ticked flip still
            // increments exactly once.
            Process(engine, context, values, 3, Bearish);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
        }

        // -------------------------------------------------------------
        // TEST H — RESET + REPLAY: reset returns to the initial
        // unavailable state and replay reproduces every output.
        // -------------------------------------------------------------

        [Fact]
        public void TestH_Reset_ReplayReproducesExactOutputs()
        {
            var (engine, context, values) = Create();

            double[] regime = { WarmUp, WarmUp, Bullish, Bullish, Bullish, Bearish, Bearish, Bullish, Bullish, Bearish };

            var firstRun = new System.Collections.Generic.List<(AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)>();
            for (int i = 0; i < regime.Length; i++)
            {
                Process(engine, context, values, i, regime[i]);
                var s = values.AtrSmoothRegimeSegment;
                firstRun.Add((s.Regime, s.RegimeId, s.RegimeStartIndex, s.RegimeAge, s.RegimeTransition));
            }

            // Reset must return the engine to the initial
            // unavailable state.
            engine.Reset();
            AssertUnavailable(values);

            // Replay: bit-consistent with the first run.
            for (int i = 0; i < regime.Length; i++)
            {
                Process(engine, context, values, i, regime[i]);
                var s = values.AtrSmoothRegimeSegment;
                var expected = firstRun[i];

                Assert.Equal(expected.Item1, s.Regime);
                Assert.Equal(expected.Item2, s.RegimeId);
                Assert.Equal(expected.Item3, s.RegimeStartIndex);
                Assert.Equal(expected.Item4, s.RegimeAge);
                Assert.Equal(expected.Item5, s.RegimeTransition);
            }
        }

        [Fact]
        public void TestH2_ResetMidRun_NoStateLeaksAcrossReset()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Bullish);
            Process(engine, context, values, 1, Bearish);
            Process(engine, context, values, 2, Bullish);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeId);

            engine.Reset();
            AssertUnavailable(values);

            // After reset the first regime again receives ID 0 —
            // no ID/age state leaks from before the reset.
            Process(engine, context, values, 0, Bearish);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
        }

        // -------------------------------------------------------------
        // TEST I — BOUNDARY/EQUALITY: canonical equality behavior,
        // no epsilon logic.
        // -------------------------------------------------------------

        [Fact]
        public void TestI_EqualEstablishedStates_NeverATransition()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Bullish);
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);

            Process(engine, context, values, 1, 1.0);  // exactly +1 again
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeAge);

            Process(engine, context, values, 2, Bearish);
            Process(engine, context, values, 3, -1.0);  // exactly -1 again
            Assert.Equal(AtrSmoothRegimeTransition.None, values.AtrSmoothRegimeSegment.RegimeTransition);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeAge);
        }

        [Fact]
        public void TestI2_WarmUpEquality_ZeroToZero_StaysUnavailable()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, 0.0);
            Process(engine, context, values, 1, 0.0);
            Process(engine, context, values, 2, 0.0);
            AssertUnavailable(values);
        }

        [Fact]
        public void TestI3_EstablishedRegimeReturningToZero_FailsClosed()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Bullish);
            Process(engine, context, values, 1, Bearish);

            // The canonical ATRSmooth trailing-stop position can
            // never return to 0 after establishment; a published 0
            // here is an upstream contract violation and must fail
            // closed rather than invent a segment semantic.
            Assert.Throws<InvalidOperationException>(
                () => Process(engine, context, values, 2, WarmUp));
        }

        [Fact]
        public void TestI4_InvalidRegimeMagnitude_FailsClosed()
        {
            var (engine, context, values) = Create();

            Assert.Throws<InvalidOperationException>(
                () => Process(engine, context, values, 0, 0.5));

            Assert.Throws<InvalidOperationException>(
                () => Process(engine, context, values, 0, double.NaN));

            Assert.Throws<InvalidOperationException>(
                () => Process(engine, context, values, 0, double.PositiveInfinity));

            Assert.Throws<InvalidOperationException>(
                () => Process(engine, context, values, 0, 2.0));
        }

        // -------------------------------------------------------------
        // TEST J — LARGE REGIME AGE: no overflow / narrowing at
        // large indices.
        // -------------------------------------------------------------

        [Fact]
        public void TestJ_LargeRegimeAge_NoIntegerOverflow()
        {
            var (engine, context, values) = Create();

            // Establish at bar 0, then jump deep into a large index
            // space: the age is pure int arithmetic and must hold
            // the exact value.
            const int bigIndex = 1_500_000_000; // fits int (max 2,147,483,647)

            Process(engine, context, values, 0, Bullish);
            Process(engine, context, values, bigIndex, Bullish);

            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(bigIndex, values.AtrSmoothRegimeSegment.RegimeAge);

            // Flip at bigIndex + 1: ID 1, age 0.
            Process(engine, context, values, bigIndex + 1, Bearish);
            Assert.Equal(1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(bigIndex + 1, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
        }

        [Fact]
        public void TestJ2_AgeNearIntMaxValue_IsExact()
        {
            var (engine, context, values) = Create();

            const int nearMax = int.MaxValue - 3;

            Process(engine, context, values, nearMax - 2, Bullish);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);

            Process(engine, context, values, nearMax, Bullish);
            Assert.Equal(2, values.AtrSmoothRegimeSegment.RegimeAge);
            Assert.Equal(nearMax - 2, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeId);
        }

        [Fact]
        public void TestJ3_ManyFlips_IdRemainsExact()
        {
            var (engine, context, values) = Create();

            // 5,000 alternating flips: the ID counts every flip
            // exactly once.
            const int flips = 5_000;
            for (int i = 0; i < flips; i++)
            {
                double regime = i % 2 == 0 ? Bullish : Bearish;
                Process(engine, context, values, i, regime);
            }

            Assert.Equal(flips - 1, values.AtrSmoothRegimeSegment.RegimeId);
            Assert.Equal(flips - 1, values.AtrSmoothRegimeSegment.RegimeStartIndex);
            Assert.Equal(0, values.AtrSmoothRegimeSegment.RegimeAge);
        }
    }
}

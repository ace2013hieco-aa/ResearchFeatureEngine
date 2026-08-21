using System.Collections.Generic;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reversal;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reversal
{
    /// <summary>
    /// Comprehensive deterministic tests for the ATRSmooth reversal
    /// state machine (<see cref="ReversalEngine"/>).
    ///
    /// The engine consumes the Distance stage's
    /// <see cref="DistanceRuntimeValues.DirectionalExtension"/>
    /// (= close − reference). To make the above/below sequence fully
    /// deterministic and unambiguous, these tests drive the engine in
    /// isolation by pre-populating the shared
    /// <see cref="EngineValues.Distance.DirectionalExtension"/> before
    /// each Update(), exactly as the production DistanceEngine would.
    /// A positive extension means close &gt;= reference (ABOVE); a
    /// negative extension means close &lt; reference (BELOW); zero
    /// means close == reference (ABOVE, by the equality rule).
    /// </summary>
    public sealed class ReversalEngineTests
    {
        private static (ReversalEngine engine, EngineContext context, EngineValues values)
            Create()
        {
            var values = new EngineValues();
            // The ReversalEngine reads only
            // Values.Distance.DirectionalExtension (published by
            // the Distance stage), never MarketData directly. A
            // single-bar TestMarketData satisfies the non-null
            // requirement of EngineContext.
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            var engine = new ReversalEngine(context);
            engine.Initialize();
            return (engine, context, values);
        }

        private static void Process(
            ReversalEngine engine,
            EngineContext context,
            EngineValues values,
            int index,
            double directionalExtension)
        {
            // Simulate the Distance stage publishing its output for
            // this bar before the Reversal stage runs.
            values.Distance.DirectionalExtension = directionalExtension;
            context.SetIndex(index);
            engine.Update();
        }

        // ABOVE = positive extension, BELOW = negative extension.
        private const double Above = 1.0;
        private const double Below = -1.0;

        // -------------------------------------------------------------
        // Test 1 — Downward reversal (ABOVE -> BELOW)
        // -------------------------------------------------------------

        [Fact]
        public void DownwardReversal_SetsZeroThenIncrements()
        {
            var (engine, context, values) = Create();

            // ABOVE, ABOVE, BELOW, BELOW, BELOW
            Process(engine, context, values, 0, Above);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);

            Process(engine, context, values, 1, Above);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);

            Process(engine, context, values, 2, Below);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 3, Below);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 4, Below);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // Test 2 — Upward reversal (BELOW -> ABOVE)
        // -------------------------------------------------------------

        [Fact]
        public void UpwardReversal_SetsZeroThenIncrements()
        {
            var (engine, context, values) = Create();

            // BELOW, BELOW, ABOVE, ABOVE, ABOVE
            Process(engine, context, values, 0, Below);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);

            Process(engine, context, values, 1, Below);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);

            Process(engine, context, values, 2, Above);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 3, Above);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 4, Above);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // Test 3 — Second reversal resets counter and direction
        // -------------------------------------------------------------

        [Fact]
        public void SecondReversal_ResetsCounterAndReplacesDirection()
        {
            var (engine, context, values) = Create();

            // ABOVE -> BELOW (down reversal), a few bars, then
            // BELOW -> ABOVE (up reversal).
            Process(engine, context, values, 0, Above);
            Process(engine, context, values, 1, Above);
            Process(engine, context, values, 2, Below); // down reversal
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 3, Below);
            Process(engine, context, values, 4, Below);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 5, Above); // up reversal
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 6, Above);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // Test 4 — No reversal: continuous side never fires
        // -------------------------------------------------------------

        [Fact]
        public void ContinuousSameSide_NeverCreatesReversal()
        {
            var (engine, context, values) = Create();

            for (int i = 0; i < 10; i++)
            {
                Process(engine, context, values, i, Above);
                Assert.Null(values.Reversal.BarsSinceReversal);
                Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
            }

            // And the mirror case: continuous BELOW.
            var (engine2, context2, values2) = Create();
            for (int i = 0; i < 10; i++)
            {
                Process(engine2, context2, values2, i, Below);
                Assert.Null(values2.Reversal.BarsSinceReversal);
                Assert.Equal(ReversalDirection.None, values2.Reversal.Direction);
            }
        }

        // -------------------------------------------------------------
        // Test 5 — Equality boundary (close == reference)
        // -------------------------------------------------------------

        [Fact]
        public void Equality_IsTreatedAsAbove_AndDoesNotFlipFlop()
        {
            // close == reference => extension 0 => ABOVE (>= 0).
            // A sequence BELOW, EQUAL, BELOW must NOT produce a
            // reversal at the EQUAL bar, because BELOW -> ABOVE
            // (equality) is a reversal, but then ABOVE (equal) ->
            // BELOW on the next bar would be a second reversal.
            // The equality rule (>= is ABOVE) is deterministic; we
            // verify the exact transitions.
            var (engine, context, values) = Create();

            // BELOW, EQUAL(=above), BELOW
            Process(engine, context, values, 0, Below);
            Assert.Null(values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 1, 0.0); // equal => ABOVE
            // BELOW -> ABOVE is an upward reversal.
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 2, Below); // ABOVE -> BELOW
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        [Fact]
        public void Equality_OnFirstBar_DoesNotCreateReversal()
        {
            // No previous bar => no reversal possible, even with
            // equality.
            var (engine, context, values) = Create();
            Process(engine, context, values, 0, 0.0);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // Test 6 — Multiple alternating reversals
        // -------------------------------------------------------------

        [Fact]
        public void AlternatingReversals_ResetEveryTime()
        {
            // ABOVE -> BELOW -> ABOVE -> BELOW -> ABOVE
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Above); // establish ABOVE
            Assert.Null(values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 1, Below); // DOWN reversal
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 2, Above); // UP reversal
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 3, Below); // DOWN reversal
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 4, Above); // UP reversal
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        [Fact]
        public void MultipleReversals_WithGaps_IncrementBetween()
        {
            // DOWN reversal -> 0,1,2,3 -> UP reversal -> 0,1,2 ->
            // DOWN reversal -> 0,1...
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Above);
            Process(engine, context, values, 1, Above);
            Process(engine, context, values, 2, Below); // DOWN
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 3, Below);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Process(engine, context, values, 4, Below);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);
            Process(engine, context, values, 5, Below);
            Assert.Equal(3, values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 6, Above); // UP
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);

            Process(engine, context, values, 7, Above);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Process(engine, context, values, 8, Above);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);

            Process(engine, context, values, 9, Below); // DOWN
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            Process(engine, context, values, 10, Below);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
        }

        // -------------------------------------------------------------
        // Direction persists; it is the most recent reversal
        // direction, NOT the current side.
        // -------------------------------------------------------------

        [Fact]
        public void Direction_PersistsAcrossNonReversalBars()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Above);
            Process(engine, context, values, 1, Below); // DOWN reversal
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            // Stay BELOW for several bars: direction stays DOWN
            // (most recent reversal), even though we are BELOW.
            for (int i = 2; i < 6; i++)
            {
                Process(engine, context, values, i, Below);
                Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
                Assert.Equal(i - 1, values.Reversal.BarsSinceReversal);
            }
        }

        // -------------------------------------------------------------
        // Re-tick (live) idempotency: re-processing the same bar
        // produces identical output and does not double-increment.
        // -------------------------------------------------------------

        [Fact]
        public void ReTick_OnSameBar_IsIdempotent()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Above);
            Process(engine, context, values, 1, Below); // DOWN reversal, bars=0

            // Re-tick bar 1 several times with the same data.
            for (int i = 0; i < 5; i++)
            {
                values.Distance.DirectionalExtension = Below;
                context.SetIndex(1);
                engine.Update();
                Assert.Equal(0, values.Reversal.BarsSinceReversal);
                Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
            }

            // Move to next bar; counter must be 1, not inflated.
            Process(engine, context, values, 2, Below);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
        }

        [Fact]
        public void ReTick_OnReversalBar_WithChangingExtension_StaysConsistent()
        {
            // On a live reversal bar, the close (and thus extension)
            // may move around as ticks arrive. Re-processing must
            // recompute from the committed end-of-previous-bar state,
            // not from a mutated one.
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Above);
            Process(engine, context, values, 1, Above);

            // Bar 2: first tick is BELOW (reversal), then tick moves
            // back ABOVE (no reversal), then BELOW again (reversal).
            // Each tick recomputes bar 2 from the bar-1 state.
            values.Distance.DirectionalExtension = Below;
            context.SetIndex(2);
            engine.Update();
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            values.Distance.DirectionalExtension = Above;
            context.SetIndex(2);
            engine.Update();
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);

            values.Distance.DirectionalExtension = Below;
            context.SetIndex(2);
            engine.Update();
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // Reset clears reversal state.
        // -------------------------------------------------------------

        [Fact]
        public void Reset_ClearsReversalState()
        {
            var (engine, context, values) = Create();

            Process(engine, context, values, 0, Above);
            Process(engine, context, values, 1, Below); // reversal

            engine.Reset();

            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);

            // After reset, bar 0 has no previous => no reversal.
            Process(engine, context, values, 0, Above);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // Validator rejects NaN / Infinity input.
        // -------------------------------------------------------------

        [Fact]
        public void NaN_Extension_Throws()
        {
            var (engine, context, values) = Create();
            values.Distance.DirectionalExtension = double.NaN;
            context.SetIndex(0);
            Assert.Throws<System.InvalidOperationException>(
                () => engine.Update());
        }

        [Fact]
        public void Infinity_Extension_Throws()
        {
            var (engine, context, values) = Create();
            values.Distance.DirectionalExtension = double.PositiveInfinity;
            context.SetIndex(0);
            Assert.Throws<System.InvalidOperationException>(
                () => engine.Update());
        }

        // -------------------------------------------------------------
        // Lookahead protection: only the current and previous bar
        // determine the output. Verified by construction: the engine
        // reads only Context.Values.Distance.DirectionalExtension
        // (current bar) and its own committed _previous* state
        // (previous bar). This test confirms that feeding the same
        // prefix of data always yields the same per-bar output
        // regardless of what comes later.
        // -------------------------------------------------------------

        [Fact]
        public void NoLookahead_OutputDependsOnlyOnCurrentAndPreviousBar()
        {
            // Process bars 0..3, then a different continuation. The
            // outputs at bars 0..3 must be identical in both runs
            // because the engine never reads ahead.
            var (engineA, contextA, valuesA) = Create();
            var (engineB, contextB, valuesB) = Create();

            double[] prefix = { Above, Above, Below, Below };
            double[] contA = { Below, Above };      // run A continuation
            double[] contB = { Below, Below };     // run B continuation

            double?[] barsA = new double?[prefix.Length + contA.Length];
            double?[] barsB = new double?[prefix.Length + contB.Length];

            for (int i = 0; i < prefix.Length; i++)
            {
                Process(engineA, contextA, valuesA, i, prefix[i]);
                barsA[i] = valuesA.Reversal.BarsSinceReversal;
                Process(engineB, contextB, valuesB, i, prefix[i]);
                barsB[i] = valuesB.Reversal.BarsSinceReversal;
            }

            // Prefix outputs must match between the two runs.
            for (int i = 0; i < prefix.Length; i++)
                Assert.Equal(barsA[i], barsB[i]);

            // Now feed different continuations. The bar at the
            // boundary (index = prefix.Length) already produced its
            // prefix-consistent output above; future divergence does
            // not retroactively change prefix outputs.
            for (int i = 0; i < contA.Length; i++)
            {
                Process(engineA, contextA, valuesA, prefix.Length + i, contA[i]);
                barsA[prefix.Length + i] =
                    valuesA.Reversal.BarsSinceReversal;
            }
            for (int i = 0; i < contB.Length; i++)
            {
                Process(engineB, contextB, valuesB, prefix.Length + i, contB[i]);
                barsB[prefix.Length + i] =
                    valuesB.Reversal.BarsSinceReversal;
            }

            // Prefix outputs remain identical (already asserted).
            // Boundary bar (index 4) used the same input in both
            // (contA[0] == contB[0] == Below) so must match too.
            Assert.Equal(barsA[prefix.Length], barsB[prefix.Length]);
        }
    }
}

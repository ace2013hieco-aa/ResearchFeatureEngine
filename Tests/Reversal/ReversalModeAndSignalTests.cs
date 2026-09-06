using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reversal;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reversal
{
    /// <summary>
    /// Tests for the <see cref="ReversalMode.TrailingStopPosition"/>
    /// mode and the <see cref="ReversalRuntimeValues.IsReversalBar"/>
    /// step-function signal, plus mode-isolation checks.
    /// </summary>
    public sealed class ReversalModeAndSignalTests
    {
        // ---- helpers (mirror ReversalEngineTests but per-mode) ----

        private static (ReversalEngine engine, EngineContext context, EngineValues values)
            Create(ReversalMode mode)
        {
            var values = new EngineValues();
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            var engine = new ReversalEngine(context, mode);
            engine.Initialize();
            return (engine, context, values);
        }

        private static void ProcessClose(
            ReversalEngine engine, EngineContext context, EngineValues values,
            int index, double directionalExtension)
        {
            values.Distance.DirectionalExtension = directionalExtension;
            context.SetIndex(index);
            engine.Update();
        }

        private static void ProcessPosition(
            ReversalEngine engine, EngineContext context, EngineValues values,
            int index, double regime)
        {
            values.Reference.Regime = regime;
            context.SetIndex(index);
            engine.Update();
        }

        // -------------------------------------------------------------
        // IsReversalBar signal (step function) — CloseToReference mode
        // -------------------------------------------------------------

        [Fact]
        public void Signal_TrueOnlyOnReversalBar_FalseOtherwise()
        {
            var (engine, context, values) = Create(ReversalMode.CloseToReference);

            // ABOVE, ABOVE, BELOW(reversal), BELOW, BELOW, ABOVE(reversal)
            ProcessClose(engine, context, values, 0, 1.0);
            Assert.False(values.Reversal.IsReversalBar);

            ProcessClose(engine, context, values, 1, 1.0);
            Assert.False(values.Reversal.IsReversalBar);

            ProcessClose(engine, context, values, 2, -1.0); // reversal
            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);

            ProcessClose(engine, context, values, 3, -1.0);
            Assert.False(values.Reversal.IsReversalBar);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);

            ProcessClose(engine, context, values, 4, -1.0);
            Assert.False(values.Reversal.IsReversalBar);
            Assert.Equal(2, values.Reversal.BarsSinceReversal);

            ProcessClose(engine, context, values, 5, 1.0); // reversal
            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        [Fact]
        public void Signal_FalseBeforeFirstReversal()
        {
            var (engine, context, values) = Create(ReversalMode.CloseToReference);
            for (int i = 0; i < 5; i++)
            {
                ProcessClose(engine, context, values, i, 1.0);
                Assert.False(values.Reversal.IsReversalBar);
            }
        }

        // -------------------------------------------------------------
        // TrailingStopPosition mode
        // -------------------------------------------------------------

        [Fact]
        public void PositionMode_ReversalOnPositionSignFlip()
        {
            // position: +1 (long/ABOVE), +1, -1 (short/BELOW) => DOWN,
            // -1, +1 => UP.
            var (engine, context, values) = Create(ReversalMode.TrailingStopPosition);

            ProcessPosition(engine, context, values, 0, 1.0);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
            Assert.False(values.Reversal.IsReversalBar);

            ProcessPosition(engine, context, values, 1, 1.0);
            Assert.Null(values.Reversal.BarsSinceReversal);

            ProcessPosition(engine, context, values, 2, -1.0); // flip
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
            Assert.True(values.Reversal.IsReversalBar);

            ProcessPosition(engine, context, values, 3, -1.0);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.False(values.Reversal.IsReversalBar);

            ProcessPosition(engine, context, values, 4, 1.0); // flip
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
            Assert.True(values.Reversal.IsReversalBar);
        }

        [Fact]
        public void PositionMode_FlatZeroIsBelow()
        {
            // position 0 (flat) is BELOW. +1 -> 0 is a reversal (ABOVE->BELOW).
            var (engine, context, values) = Create(ReversalMode.TrailingStopPosition);

            ProcessPosition(engine, context, values, 0, 1.0);
            ProcessPosition(engine, context, values, 1, 0.0); // flat => BELOW
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        [Fact]
        public void PositionMode_NoFlip_NoReversal()
        {
            var (engine, context, values) = Create(ReversalMode.TrailingStopPosition);
            for (int i = 0; i < 6; i++)
            {
                ProcessPosition(engine, context, values, i, 1.0);
                Assert.Null(values.Reversal.BarsSinceReversal);
                Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
            }
        }

        [Fact]
        public void PositionMode_NaNPosition_Throws()
        {
            var (engine, context, values) = Create(ReversalMode.TrailingStopPosition);
            values.Reference.Regime = double.NaN;
            context.SetIndex(0);
            Assert.Throws<System.InvalidOperationException>(
                () => engine.Update());
        }

        // -------------------------------------------------------------
        // Mode isolation: the two modes can disagree on the same
        // close/position sequence, confirming they are genuinely
        // independent relations (not the same computation).
        // -------------------------------------------------------------

        [Fact]
        public void Modes_AreIndependent()
        {
            // A sequence where close-to-reference and trailing-stop
            // position move differently. We just assert both engines
            // run and produce valid, distinct state — the point is
            // that selecting the mode changes the relation source.
            var (eClose, cClose, vClose) = Create(ReversalMode.CloseToReference);
            var (ePos, cPos, vPos) = Create(ReversalMode.TrailingStopPosition);

            // Bar 0: close above ref (ext +1), position long (+1)
            ProcessClose(eClose, cClose, vClose, 0, 1.0);
            ProcessPosition(ePos, cPos, vPos, 0, 1.0);
            Assert.Equal(ReversalDirection.None, vClose.Reversal.Direction);
            Assert.Equal(ReversalDirection.None, vPos.Reversal.Direction);

            // Bar 1: close below ref (ext -1), position still long (+1)
            ProcessClose(eClose, cClose, vClose, 1, -1.0); // close-mode reversal
            ProcessPosition(ePos, cPos, vPos, 1, 1.0);      // position-mode no reversal

            Assert.Equal(ReversalDirection.Down, vClose.Reversal.Direction);
            Assert.Equal(0, vClose.Reversal.BarsSinceReversal);

            Assert.Equal(ReversalDirection.None, vPos.Reversal.Direction);
            Assert.Null(vPos.Reversal.BarsSinceReversal);
        }

        // -------------------------------------------------------------
        // Re-tick idempotency for the signal in position mode.
        // -------------------------------------------------------------

        [Fact]
        public void PositionMode_ReTick_IsIdempotent()
        {
            var (engine, context, values) = Create(ReversalMode.TrailingStopPosition);

            ProcessPosition(engine, context, values, 0, 1.0);
            ProcessPosition(engine, context, values, 1, -1.0); // reversal, bars=0

            for (int i = 0; i < 5; i++)
            {
                values.Reference.Regime = -1.0;
                context.SetIndex(1);
                engine.Update();
                Assert.Equal(0, values.Reversal.BarsSinceReversal);
                Assert.True(values.Reversal.IsReversalBar);
            }

            ProcessPosition(engine, context, values, 2, -1.0);
            Assert.Equal(1, values.Reversal.BarsSinceReversal);
            Assert.False(values.Reversal.IsReversalBar);
        }
    }
}

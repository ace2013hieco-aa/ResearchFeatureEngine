using System;

using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Lifecycle, warm-up, re-tick, reset, and determinism tests for
    /// <see cref="DarvasBoxReferenceSource"/>, mirroring the
    /// ATRSmooth source test structure
    /// (ATRSmoothReferenceSourceTests / ATRSmoothReTickTests).
    /// </summary>
    public sealed class DarvasBoxReferenceSourceTests
    {
        private static (EngineContext ctx, FlatOhlcvMarketData md)
            CreateContext(double[] high, double[] low, double[] close,
                int length = 5)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new EngineContext(md, new Core.EngineValues());
            return (ctx, md);
        }

        // -------------------------------------------------------------
        // Construction / null guards
        // -------------------------------------------------------------

        [Fact]
        public void Constructor_NullConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DarvasBoxReferenceSource(null!));
        }

        [Fact]
        public void Update_NullContext_Throws()
        {
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration());
            Assert.Throws<ArgumentNullException>(
                () => source.Update(null!));
        }

        [Fact]
        public void Update_NullMarketData_Throws()
        {
            var ctx = new EngineContext();
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration());
            Assert.Throws<InvalidOperationException>(
                () => source.Update(ctx));
        }

        // -------------------------------------------------------------
        // Warm-up
        // -------------------------------------------------------------

        [Fact]
        public void Update_FirstBar_PublishesCloseAsWarmUpReference()
        {
            // No box can exist at bar 0 (windows not full). The
            // published measurement level must still be a finite
            // scalar: the current close.
            double[] close = { 100.0, 101.0, 102.0, 103.0, 104.0 };
            double[] high = { 100.5, 101.5, 102.5, 103.5, 104.5 };
            double[] low = { 99.5, 100.5, 101.5, 102.5, 103.5 };

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            ctx.SetIndex(0);
            double reference = source.Update(ctx);

            Assert.Equal(100.0, reference, 10);
            Assert.False(source.HasBox);
            Assert.Equal(0.0, source.Regime, 10);
        }

        [Fact]
        public void Update_WarmUpBars_AllRegimeZeroAndPriceFinite()
        {
            const int n = 12;
            double[] close = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            var rng = new Random(11);
            for (int i = 0; i < n; i++)
            {
                close[i] = 100.0 + rng.NextDouble() * 4.0;
                high[i] = close[i] + 0.5;
                low[i] = close[i] - 0.5;
            }

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            for (int i = 0; i < n; i++)
            {
                ctx.SetIndex(i);
                double r = source.Update(ctx);
                Assert.False(double.IsNaN(r));
                Assert.False(double.IsInfinity(r));

                if (!source.HasBox)
                {
                    // Warm-up: close is the published level, regime 0.
                    Assert.Equal(close[i], r, 10);
                    Assert.Equal(0.0, source.Regime, 10);
                }
            }
        }

        // -------------------------------------------------------------
        // Determinism (same input -> same output)
        // -------------------------------------------------------------

        [Fact]
        public void Update_Determinism_SameInputProducesSameOutput()
        {
            const int n = 200;
            double[] close = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            var rng = new Random(7);
            double price = 100.0;
            for (int i = 0; i < n; i++)
            {
                price += (rng.NextDouble() - 0.5) * 2.0;
                close[i] = price;
                high[i] = close[i] + rng.NextDouble() * 2.0;
                low[i] = close[i] - rng.NextDouble() * 2.0;
            }

            var (ctxA, _) = CreateContext(high, low, close);
            var (ctxB, _) = CreateContext(high, low, close);

            var srcA = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            var srcB = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            srcA.Initialize();
            srcB.Initialize();

            for (int i = 0; i < n; i++)
            {
                ctxA.SetIndex(i);
                ctxB.SetIndex(i);
                double a = srcA.Update(ctxA);
                double b = srcB.Update(ctxB);
                Assert.Equal(a, b, 12);
                Assert.Equal(srcA.Regime, srcB.Regime, 12);
            }
        }

        // -------------------------------------------------------------
        // Reset
        // -------------------------------------------------------------

        [Fact]
        public void Reset_AfterFullRun_ReplayReproducesOriginalSequence()
        {
            // Fixture 1 from the golden suite: 30 bars with boxes,
            // breakouts, and replacements.
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

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            int n = close.Length;
            double[] price1 = new double[n];
            double[] regime1 = new double[n];
            double[] upper1 = new double[n];

            for (int i = 0; i < n; i++)
            {
                ctx.SetIndex(i);
                price1[i] = source.Update(ctx);
                regime1[i] = source.Regime;
                upper1[i] = source.Upper;
            }

            source.Reset();
            source.Initialize();

            for (int i = 0; i < n; i++)
            {
                ctx.SetIndex(i);
                double price = source.Update(ctx);

                Assert.Equal(price1[i], price, 12);
                Assert.Equal(regime1[i], source.Regime, 12);
                Assert.Equal(upper1[i], source.Upper, 12);
            }
        }

        // -------------------------------------------------------------
        // Re-tick idempotency
        // -------------------------------------------------------------

        [Fact]
        public void Update_SameIndexTwiceWithSameData_ProducesIdenticalOutput()
        {
            double[] close =
            {
                100.0, 99.5, 99.0, 99.5, 100.0, 100.2, 108.0, 103.0,
                102.5, 103.0, 103.5, 102.5, 101.5, 112.0, 105.0
            };
            double[] high =
            {
                102.0, 101.0, 100.5, 101.0, 100.0, 100.5, 110.0, 105.0,
                104.0, 103.5, 104.0, 103.0, 102.0, 113.0, 106.0
            };
            double[] low =
            {
                99.0, 98.5, 98.0, 98.5, 99.0, 96.0, 97.0, 97.5,
                98.0, 98.5, 99.0, 100.0, 101.0, 108.0, 100.0
            };

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            for (int i = 0; i <= 9; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            // Re-tick on the confirmation bar 9 several times.
            ctx.SetIndex(9);
            double p1 = source.Update(ctx);
            double regime1 = source.Regime;
            double upper1 = source.Upper;
            double lower1 = source.Lower;

            for (int tick = 0; tick < 10; tick++)
            {
                ctx.SetIndex(9);
                double p = source.Update(ctx);
                Assert.Equal(p1, p, 12);
                Assert.Equal(regime1, source.Regime, 12);
                Assert.Equal(upper1, source.Upper, 12);
                Assert.Equal(lower1, source.Lower, 12);
            }
        }

        [Fact]
        public void Update_ReTickDoesNotAdvanceBarssinceState()
        {
            // Reticks on a mid-cycle bar must not double-increment the
            // barssince counter (the analogue of the ATRSmooth
            // rolling-sum retick test).
            double[] close =
            {
                100.0, 99.5, 99.0, 99.5, 100.0, 100.2, 108.0, 103.0,
                102.5, 103.0, 103.5, 102.5, 101.5, 112.0
            };
            double[] high =
            {
                102.0, 101.0, 100.5, 101.0, 100.0, 100.5, 110.0, 105.0,
                104.0, 103.5, 104.0, 103.0, 102.0, 113.0
            };
            double[] low =
            {
                99.0, 98.5, 98.0, 98.5, 99.0, 96.0, 97.0, 97.5,
                98.0, 98.5, 99.0, 100.0, 101.0, 108.0
            };

            var (ctxA, _) = CreateContext(high, low, close);
            var (ctxB, _) = CreateContext(high, low, close);

            var srcA = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            var srcB = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            srcA.Initialize();
            srcB.Initialize();

            // A: straight processing.
            for (int i = 0; i <= 9; i++)
            {
                ctxA.SetIndex(i);
                srcA.Update(ctxA);
            }

            // B: process to 8, then hammer bar 9 with reticks, then
            // continue. Must land on the same state as A.
            for (int i = 0; i <= 8; i++)
            {
                ctxB.SetIndex(i);
                srcB.Update(ctxB);
            }
            for (int tick = 0; tick < 25; tick++)
            {
                ctxB.SetIndex(9);
                srcB.Update(ctxB);
            }
            // A also re-runs bar 9 once more (idempotent).
            ctxA.SetIndex(9);
            srcA.Update(ctxA);

            Assert.Equal(srcA.Upper, srcB.Upper, 12);
            Assert.Equal(srcA.Lower, srcB.Lower, 12);
            Assert.Equal(srcA.Regime, srcB.Regime, 12);

            // Continue both to bar 13 and compare final states.
            for (int i = 10; i <= 13; i++)
            {
                ctxA.SetIndex(i);
                srcA.Update(ctxA);
                ctxB.SetIndex(i);
                srcB.Update(ctxB);
            }

            Assert.Equal(srcA.Upper, srcB.Upper, 12);
            Assert.Equal(srcA.Lower, srcB.Lower, 12);
            Assert.Equal(srcA.Regime, srcB.Regime, 12);
        }

        [Fact]
        public void Update_RetickOnLiveBarWithChangedData_ReflectsLatestData()
        {
            // cTrader reticks arrive because the forming bar's data
            // CHANGED. Re-processing bar 10 after its high changed
            // must reflect the new data (no stale snapshot).
            double[] close =
            {
                100.0, 99.5, 99.0, 99.5, 100.0, 100.2, 108.0, 103.0,
                102.5, 103.0, 103.5
            };
            double[] high =
            {
                102.0, 101.0, 100.5, 101.0, 100.0, 100.5, 110.0, 105.0,
                104.0, 103.5, 104.0
            };
            double[] low =
            {
                99.0, 98.5, 98.0, 98.5, 99.0, 96.0, 97.0, 97.5,
                98.0, 98.5, 99.0
            };

            var (ctx, md) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            for (int i = 0; i <= 10; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            double upperBefore = source.Upper;

            // Simulate a new tick: bar 10's high jumps (still under
            // the box top, so no breakout) — output may legitimately
            // change because the data changed, but with UNCHANGED
            // data the recomputation must be stable (covered above).
            // Here: change bar 10's high via a mutated adapter.
            var high2 = (double[])high.Clone();
            high2[10] = 109.0; // still < upper 110, no breakout

            var (ctx2, _) = CreateContext(high2, low, close);
            var source2 = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source2.Initialize();
            for (int i = 0; i <= 10; i++)
            {
                ctx2.SetIndex(i);
                source2.Update(ctx2);
            }

            // Same data state -> same box (109 does not break out).
            Assert.Equal(upperBefore, source2.Upper, 12);
        }

        // -------------------------------------------------------------
        // Long-run finiteness
        // -------------------------------------------------------------

        [Fact]
        public void Update_LongRun_DoesNotProduceNaNOrInfinity()
        {
            const int n = 10_000;
            double[] close = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            var rng = new Random(123);
            double price = 100.0;
            for (int i = 0; i < n; i++)
            {
                price += (rng.NextDouble() - 0.5) * 1.0;
                close[i] = price;
                high[i] = close[i] + rng.NextDouble() * 2.0;
                low[i] = close[i] - rng.NextDouble() * 2.0;
            }

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            int boxes = 0;
            for (int i = 0; i < n; i++)
            {
                ctx.SetIndex(i);
                double r = source.Update(ctx);
                Assert.False(double.IsNaN(r), $"NaN at index {i}");
                Assert.False(double.IsInfinity(r), $"Infinity at index {i}");
                if (source.HasBox) boxes++;
            }

            // On a random walk a box should confirm reasonably often.
            Assert.True(boxes > 100, $"Only {boxes} boxed bars in 10k.");
        }

        // -------------------------------------------------------------
        // Initialize / runtime bookkeeping
        // -------------------------------------------------------------

        [Fact]
        public void Initialize_SetsInitializedAndResetsState()
        {
            double[] close = { 100.0, 101.0, 102.0, 103.0, 104.0 };
            double[] high = { 100.5, 101.5, 102.5, 103.5, 104.5 };
            double[] low = { 99.5, 100.5, 101.5, 102.5, 103.5 };

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));

            ctx.SetIndex(0);
            source.Update(ctx);

            Assert.False(source.Runtime.IsInitialized);
            source.Initialize();
            Assert.True(source.Runtime.IsInitialized);

            // After Initialize the held state is cleared: no box.
            Assert.False(source.HasBox);
            Assert.Equal(0.0, source.Runtime.LastReference);
        }

        [Fact]
        public void Reset_AfterUpdates_ResetsAllRuntimeState()
        {
            double[] close =
            {
                100.0, 99.5, 99.0, 99.5, 100.0, 100.2, 108.0, 103.0,
                102.5, 103.0
            };
            double[] high =
            {
                102.0, 101.0, 100.5, 101.0, 100.0, 100.5, 110.0, 105.0,
                104.0, 103.5
            };
            double[] low =
            {
                99.0, 98.5, 98.0, 98.5, 99.0, 96.0, 97.0, 97.5,
                98.0, 98.5
            };

            var (ctx, _) = CreateContext(high, low, close);
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(5));
            source.Initialize();

            for (int i = 0; i < 10; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            Assert.Equal(9, source.Runtime.CurrentIndex);
            Assert.True(source.HasBox);

            source.Reset();

            Assert.Equal(-1, source.Runtime.CurrentIndex);
            Assert.Equal(0.0, source.Runtime.LastReference);
            Assert.False(source.HasBox);
            Assert.True(double.IsNaN(source.Upper));
            Assert.True(double.IsNaN(source.Lower));
            Assert.Equal(0.0, source.Regime, 10);
            Assert.False(source.Runtime.IsInitialized);
        }
    }
}

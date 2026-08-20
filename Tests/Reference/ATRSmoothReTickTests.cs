using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reference.Configuration;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Verifies that <see cref="ATRSmoothReferenceSource"/> is
    /// idempotent for re-ticks: calling Update() repeatedly for the
    /// same bar index with the same data must produce the same
    /// reference value and must leave the source's state in a
    /// consistent position.
    /// </summary>
    public sealed class ATRSmoothReTickTests
    {
        private static (EngineContext ctx, FlatOhlcvMarketData md) MakeContext()
        {
            int n = 20;
            double[] close = new double[n];
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                close[i] = 100.0 + i * 2.0;
                open[i]  = close[i];
                high[i]  = close[i] + 0.5;
                low[i]   = close[i] - 0.5;
                volume[i] = 100.0;
            }
            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new EngineContext(md, new Core.EngineValues());
            return (ctx, md);
        }

        [Fact]
        public void Update_SameIndexTwiceWithSameData_ProducesIdenticalReference()
        {
            var (ctx, _) = MakeContext();
            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20));
            source.Initialize();

            ctx.SetIndex(5);
            source.Update(ctx);
            double ref1 = source.Runtime.LastReference;

            // Re-tick on the same bar.
            ctx.SetIndex(5);
            source.Update(ctx);
            double ref2 = source.Runtime.LastReference;

            Assert.Equal(ref1, ref2, 12);
        }

        [Fact]
        public void Update_ReTickDoesNotCorruptRollingSum()
        {
            // The bug: on re-tick, the VWMA sum would have bar N's
            // value added twice, so the sum would drift upward.
            // After the fix, the sum must equal what it would be
            // for a single processing of bar N.
            var (ctx, _) = MakeContext();
            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20));
            source.Initialize();

            for (int i = 0; i <= 4; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }
            double sumVAfter4 = source.Runtime.SumV;
            double sumPVAfter4 = source.Runtime.SumPV;

            for (int i = 0; i < 10; i++)
            {
                ctx.SetIndex(4);
                source.Update(ctx);
            }

            Assert.Equal(sumVAfter4, source.Runtime.SumV, 12);
            Assert.Equal(sumPVAfter4, source.Runtime.SumPV, 12);
        }

        [Fact]
        public void Update_ReTickDoesNotCorruptEmaTrueRange()
        {
            var (ctx, _) = MakeContext();
            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20));
            source.Initialize();

            for (int i = 0; i <= 4; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }
            double emaAfter4 = source.Runtime.EmaTrueRange;

            for (int i = 0; i < 10; i++)
            {
                ctx.SetIndex(4);
                source.Update(ctx);
            }

            Assert.Equal(emaAfter4, source.Runtime.EmaTrueRange, 12);
        }

        [Fact]
        public void Update_ReTickOnBar4ThenProcessBar5_AdvancesCorrectly()
        {
            var (ctxA, _) = MakeContext();
            var (ctxB, _) = MakeContext();

            var srcA = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(14, 5.1, 20));
            var srcB = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(14, 5.1, 20));

            srcA.Initialize();
            srcB.Initialize();

            for (int i = 0; i <= 5; i++)
            {
                ctxA.SetIndex(i);
                srcA.Update(ctxA);
            }
            double refA = srcA.Runtime.LastReference;

            for (int i = 0; i <= 4; i++)
            {
                ctxB.SetIndex(i);
                srcB.Update(ctxB);
            }
            for (int i = 0; i < 10; i++)
            {
                ctxB.SetIndex(4);
                srcB.Update(ctxB);
            }
            ctxB.SetIndex(5);
            srcB.Update(ctxB);
            double refB = srcB.Runtime.LastReference;

            Assert.Equal(refA, refB, 12);
        }

        [Fact]
        public void Reset_ClearsReTickSnapshot()
        {
            var (ctx, _) = MakeContext();
            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(14, 5.1, 20));
            source.Initialize();

            for (int i = 0; i <= 3; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            source.Reset();
            source.Initialize();

            ctx.SetIndex(0);
            source.Update(ctx);
            double ref0 = source.Runtime.LastReference;

            var fresh = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(14, 5.1, 20));
            fresh.Initialize();

            var (ctx2, _) = MakeContext();
            ctx2.SetIndex(0);
            fresh.Update(ctx2);

            Assert.Equal(fresh.Runtime.LastReference, ref0, 12);
        }
    }
}

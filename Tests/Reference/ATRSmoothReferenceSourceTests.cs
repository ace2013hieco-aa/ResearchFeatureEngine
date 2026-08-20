using System;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    public sealed class ATRSmoothReferenceSourceTests
    {
        private static (EngineContext ctx, FlatOhlcvMarketData md)
            CreateContext(double[] close, int atrPeriod = 14, double atrMult = 5.1,
                int smooth = 20, double[]? volume = null)
        {
            int n = close.Length;
            double eps = 0.5;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] vol = volume ?? new double[n];

            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + eps;
                low[i] = close[i] - eps;
                if (volume == null) vol[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, vol);
            var ctx = new EngineContext(md, new Core.EngineValues());
            return (ctx, md);
        }

        [Fact]
        public void Constructor_NullConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ATRSmoothReferenceSource(null!));
        }

        [Fact]
        public void Initialize_SetsInitializedAndResetsState()
        {
            var (ctx, _) = CreateContext(new[] { 100.0, 101.0, 102.0 });

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 3));

            // Touch state by computing once.
            ctx.SetIndex(0);
            source.Update(ctx);

            Assert.True(source.Runtime.IsInitialized == false); // before Initialize
            source.Initialize();
            Assert.True(source.Runtime.IsInitialized);

            // After Initialize, the last reference is reset to 0.
            Assert.Equal(0.0, source.Runtime.LastReference);
        }

        [Fact]
        public void Reset_AfterUpdates_ResetsAllRuntimeState()
        {
            var (ctx, _) = CreateContext(new[] { 100.0, 101.0, 102.0 });

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 3));

            ctx.SetIndex(0);
            source.Update(ctx);
            ctx.SetIndex(1);
            source.Update(ctx);

            Assert.Equal(1, source.Runtime.CurrentIndex);
            Assert.NotEqual(0.0, source.Runtime.LastReference);

            source.Reset();

            Assert.Equal(-1, source.Runtime.CurrentIndex);
            Assert.Equal(0.0, source.Runtime.LastReference);
            Assert.Equal(0.0, source.Runtime.TrailingStop);
            Assert.Equal(0.0, source.Runtime.Position);
            Assert.Equal(0.0, source.Runtime.SumPV);
            Assert.Equal(0.0, source.Runtime.SumV);
            Assert.Equal(0.0, source.Runtime.EmaTrueRange);
            Assert.False(source.Runtime.IsInitialized);
        }

        [Fact]
        public void Update_FirstBar_ProducesFiniteReference()
        {
            var (ctx, _) = CreateContext(new[] { 100.0 });

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 1));

            ctx.SetIndex(0);
            double reference = source.Update(ctx);

            Assert.False(double.IsNaN(reference));
            Assert.False(double.IsInfinity(reference));
        }

        [Fact]
        public void Update_NullContext_Throws()
        {
            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration());

            Assert.Throws<ArgumentNullException>(
                () => source.Update(null!));
        }

        [Fact]
        public void Update_NullMarketData_Throws()
        {
            var ctx = new EngineContext();
            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration());

            Assert.Throws<InvalidOperationException>(
                () => source.Update(ctx));
        }

        [Fact]
        public void Update_Determinism_SameInputProducesSameOutput()
        {
            double[] closes = new double[100];
            var rng = new Random(7);
            for (int i = 0; i < closes.Length; i++)
            {
                closes[i] = 100.0 + rng.NextDouble() * 10.0;
            }

            var (ctxA, _) = CreateContext(closes);
            var (ctxB, _) = CreateContext(closes);

            var srcA = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20));
            var srcB = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20));

            srcA.Initialize();
            srcB.Initialize();

            for (int i = 0; i < closes.Length; i++)
            {
                ctxA.SetIndex(i);
                ctxB.SetIndex(i);
                double a = srcA.Update(ctxA);
                double b = srcB.Update(ctxB);
                Assert.Equal(a, b, 12);
            }
        }

        [Fact]
        public void Update_AfterReset_RestoresFirstBarState()
        {
            var (ctx, _) = CreateContext(new[] { 100.0, 102.0, 104.0, 106.0 });

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 4));

            source.Initialize();

            for (int i = 0; i < 4; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            double afterBar3 = source.Runtime.LastReference;
            double trailingAfter3 = source.Runtime.TrailingStop;

            source.Reset();
            source.Initialize();

            ctx.SetIndex(0);
            double afterResetBar0 = source.Update(ctx);

            // After a full reset, the state at bar 0 is deterministic
            // and identical to a fresh source.
            var fresh = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 4));
            fresh.Initialize();

            var (ctx2, _) = CreateContext(new[] { 100.0, 102.0, 104.0, 106.0 });
            ctx2.SetIndex(0);
            double freshBar0 = fresh.Update(ctx2);

            Assert.Equal(freshBar0, afterResetBar0, 12);
            Assert.NotEqual(afterBar3, afterResetBar0);
        }

        [Fact]
        public void Update_LongRun_DoesNotProduceNaNOrInfinity()
        {
            const int N = 10_000;
            double[] closes = new double[N];
            var rng = new Random(123);
            double price = 100.0;
            for (int i = 0; i < N; i++)
            {
                price += (rng.NextDouble() - 0.5) * 0.5;
                closes[i] = price;
            }

            var (ctx, _) = CreateContext(closes, atrPeriod: 16, atrMult: 5.1, smooth: 100);

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(atrPeriod: 16, atrMultiplier: 5.1, smoothLength: 100));
            source.Initialize();

            for (int i = 0; i < N; i++)
            {
                ctx.SetIndex(i);
                double r = source.Update(ctx);
                Assert.False(double.IsNaN(r), $"NaN at index {i}");
                Assert.False(double.IsInfinity(r), $"Infinity at index {i}");
            }
        }
    }
}

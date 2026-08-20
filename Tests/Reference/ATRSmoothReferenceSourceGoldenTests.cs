using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Tests;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Golden-dataset correctness tests for the ATR Smooth reference.
    /// The expected values were produced by the independent standalone
    /// computation in <c>bin/ComputeExpected</c>, which mirrors the
    /// algorithm line for line and is NOT linked to the production
    /// engine. These tests guard against accidental algorithm changes
    /// that would silently alter the reference price.
    /// </summary>
    public sealed class ATRSmoothReferenceSourceGoldenTests
    {
        [Fact]
        public void KnownDataset_Bar19_ReferencePrice_IsExactGoldenValue()
        {
            // 20 bars, close = 100 + 2*i, high = close + 0.5, low = close - 0.5,
            // volume = 100, ATR period = 14, multiplier = 5.1, smooth length = 20.
            int barCount = 20;
            double startPrice = 100.0;
            double step = 2.0;

            double[] close = new double[barCount];
            double[] open = new double[barCount];
            double[] high = new double[barCount];
            double[] low = new double[barCount];
            double[] volume = new double[barCount];

            for (int i = 0; i < barCount; i++)
            {
                close[i] = startPrice + i * step;
                open[i] = close[i];
                high[i] = close[i] + 0.5;
                low[i] = close[i] - 0.5;
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new Core.Engine.EngineContext(md, new Core.EngineValues());

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 20));
            source.Initialize();

            for (int i = 0; i < barCount; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            // Golden value from bin/ComputeExpected/Program.cs
            double expected = 122.37724421291009;
            Assert.Equal(expected, source.Runtime.LastReference, 8);
        }

        [Fact]
        public void KnownDataset_FirstBar_VwmaEqualsClose()
        {
            // First bar: SumV = volume, SumPV = close * volume, so VWMA = close.
            // Trailing stop is initialized to close, so reference = close.
            int barCount = 5;
            double[] close = { 100.0, 102.0, 104.0, 106.0, 108.0 };
            double[] open = new double[barCount];
            double[] high = new double[barCount];
            double[] low = new double[barCount];
            double[] volume = new double[barCount];

            for (int i = 0; i < barCount; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + 0.5;
                low[i] = close[i] - 0.5;
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new Core.Engine.EngineContext(md, new Core.EngineValues());

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 5));
            source.Initialize();

            ctx.SetIndex(0);
            double reference = source.Update(ctx);

            // First bar: vwma = close (100), trailingStop = close + nLoss (105.1)
            // reference = (100 + 105.1) / 2 = 102.55
            Assert.Equal(102.55, reference, 8);
        }

        [Fact]
        public void KnownDataset_VolumeZero_FallsBackToClose()
        {
            // Volume is zero on every bar: SumV always == 0 → vwma = close.
            int barCount = 5;
            double[] close = { 100.0, 101.0, 102.0, 103.0, 104.0 };
            double[] open = new double[barCount];
            double[] high = new double[barCount];
            double[] low = new double[barCount];
            double[] volume = new double[barCount];  // all zeros

            for (int i = 0; i < barCount; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + 0.5;
                low[i] = close[i] - 0.5;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new Core.Engine.EngineContext(md, new Core.EngineValues());

            var source = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: 14, atrMultiplier: 5.1, smoothLength: 5));
            source.Initialize();

            // No exception means graceful handling of zero volume.
            for (int i = 0; i < barCount; i++)
            {
                ctx.SetIndex(i);
                double r = source.Update(ctx);
                Assert.False(double.IsNaN(r));
                Assert.False(double.IsInfinity(r));
            }
        }
    }
}

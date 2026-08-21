using System;
using System.Collections.Generic;
using System.Linq;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Statistics.Integration
{
    /// <summary>
    /// Reproduces the exact cTrader calling pattern: historical
    /// preload (one Calculate per closed bar) followed by live
    /// re-ticks on the most-recent forming bar. Confirms the
    /// current-bar-inclusive statistics behave consistently across
    /// the preload -> live transition (the source of the original
    /// "distorted on live bars" symptom).
    /// </summary>
    public sealed class CTraderCallingPatternRepro
    {
        private sealed class MutableOhlcvMarketData : IMarketData
        {
            private readonly double[] _open;
            private readonly double[] _high;
            private readonly double[] _low;
            private readonly double[] _close;
            private readonly double[] _volume;

            public MutableOhlcvMarketData(double[] close)
            {
                _close = (double[])close.Clone();
                _open = (double[])close.Clone();
                _high = close.Select(c => c + 0.5).ToArray();
                _low = close.Select(c => c - 0.5).ToArray();
                _volume = Enumerable.Repeat(100.0, close.Length).ToArray();
            }

            public IPriceSeries Open => new TestPriceSeries(_open);
            public IPriceSeries High => new TestPriceSeries(_high);
            public IPriceSeries Low => new TestPriceSeries(_low);
            public IPriceSeries Close => new TestPriceSeries(_close);
            public IPriceSeries Volume => new TestPriceSeries(_volume);
            public IReadOnlyList<DateTime> Time => Array.Empty<DateTime>();
            public int Count => _close.Length;
            public void SetClose(int index, double value) => _close[index] = value;
            public bool MoveNext() => false;
        }

        private static (ResearchFeatureEngine engine, MutableOhlcvMarketData md)
            Build(int windowSize, double[] close)
        {
            var md = new MutableOhlcvMarketData(close);
            var config = new EngineConfiguration(
                marketData: md,
                values: new EngineValues(),
                referenceSource: new ATRSmoothReferenceSource(
                    new ATRSmoothConfiguration(14, 5.1, 5)),
                scaleModel: new ATRScaleModel(14),
                normalizationModel: new ScaleNormalizationModel(),
                statisticModels: new List<IStatisticModel>
                {
                    new MeanModel(),
                    new StandardDeviationModel()
                },
                options: new EngineOptions
                {
                    StatisticsWindowSize = windowSize
                });
            return (new ResearchFeatureEngineBuilder(config).Build(), md);
        }

        [Fact]
        public void Preload_ThenFirstLiveTick_MeanAtLastBarIncludesLiveBar()
        {
            // 10 bars 100..109. cTrader preloads Calculate(0..9),
            // then re-ticks Calculate(9) as the live bar.
            //
            // With current-bar-inclusive semantics, after preload the
            // mean INCLUDES bar 9's close (it's the live bar), so
            // MeanSeries[9] during preload = mean(100..109).
            //
            // When the first live tick arrives at bar 9 with a wild
            // close, the mean tracks it (closed bars unchanged).
            var close = Enumerable.Range(100, 10).Select(i => (double)i).ToArray();
            var (engine, md) = Build(windowSize: 50, close);

            // Preload: one Calculate per closed bar (cTrader historical).
            for (int i = 0; i < close.Length; i++)
                engine.ProcessAt(i);

            double expectedPreload = Enumerable.Range(100, 10).Average();
            Assert.Equal(expectedPreload,
                engine.Values.Statistics.Location.Mean, 10);

            // First live tick on bar 9 with a wild close. Mean must
            // track the new live close (closed bars 0..8 unchanged).
            md.SetClose(9, 999.0);
            engine.ProcessAt(9);
            double expectedLive = (Enumerable.Range(100, 9).Select(i => (double)i)
                .Append(999.0)).Average();
            Assert.Equal(expectedLive,
                engine.Values.Statistics.Location.Mean, 8);
        }

        [Fact]
        public void LiveBar_WhenItCloses_NextBarCommitsFinalClose()
        {
            // After live ticks on bar 9, bar 9 closes and bar 10 opens.
            // Bar 9's FINAL close (last tick) is committed.
            var close = Enumerable.Range(100, 11).Select(i => (double)i).ToArray();
            var (engine, md) = Build(windowSize: 50, close);

            for (int i = 0; i < 10; i++)
                engine.ProcessAt(i);

            // Live ticks on bar 9.
            md.SetClose(9, 200.0); engine.ProcessAt(9);
            md.SetClose(9, 150.0); engine.ProcessAt(9); // final tick

            // Bar 10 opens. Commit bar 9's final close (150).
            // Closed window = bars 0..8 (100..108) + bar 9 (150);
            // bar 10 (live) close = 110.
            engine.ProcessAt(10);

            double expected = Enumerable.Range(100, 9)
                .Select(i => (double)i)
                .Append(150.0)
                .Append(110.0).Average();
            Assert.Equal(expected,
                engine.Values.Statistics.Location.Mean, 10);
        }
    }
}

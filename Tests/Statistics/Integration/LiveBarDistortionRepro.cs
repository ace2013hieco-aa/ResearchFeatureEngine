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
    /// Reproduces the cTrader live-bar scenario with a mutable close
    /// series (live tick value changing on the forming bar). With the
    /// current-bar-inclusive statistics semantics, the rolling mean
    /// tracks the live bar's latest close without double-counting,
    /// and the bar's final close is committed when it closes.
    /// </summary>
    public sealed class LiveBarDistortionRepro
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
            Build(int windowSize)
        {
            var close = new double[] { 100, 101, 102, 103, 104, 105, 106 };
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
        public void LiveBar_CloseChangingAcrossTicks_MeanTracksLatestClose()
        {
            // Process bars 0..5. Window (closed) = bars 0..4 =
            // [100,101,102,103,104]; bar 5 (live) close = 105.
            var (engine, md) = Build(windowSize: 5);
            for (int i = 0; i <= 5; i++)
                engine.ProcessAt(i);

            // Mean = (100+101+102+103+104+105)/6 = 615/6 = 102.5.
            Assert.Equal(102.5, engine.Values.Statistics.Location.Mean, 10);

            // Tick the live bar (index 5) through several closes. The
            // mean must track the latest close (closed bars unchanged).
            md.SetClose(5, 130.0); engine.ProcessAt(5);
            Assert.Equal((100 + 101 + 102 + 103 + 104 + 130) / 6.0,
                engine.Values.Statistics.Location.Mean, 10);

            md.SetClose(5, 80.0); engine.ProcessAt(5);
            Assert.Equal((100 + 101 + 102 + 103 + 104 + 80) / 6.0,
                engine.Values.Statistics.Location.Mean, 10);

            md.SetClose(5, 150.0); engine.ProcessAt(5);
            Assert.Equal((100 + 101 + 102 + 103 + 104 + 150) / 6.0,
                engine.Values.Statistics.Location.Mean, 10);

            // Observation count never grew (no double-count).
            Assert.Equal(6, engine.Values.Statistics.ObservationCount);
        }

        [Fact]
        public void LiveBar_CloseChangingAcrossTicks_LastTickCommittedOnNewBar()
        {
            // After re-ticking the live bar with various closes, the
            // LAST tick's value is what should be committed when the
            // next bar opens (the closed bar's final close).
            var (engine, md) = Build(windowSize: 200);

            for (int i = 0; i <= 5; i++)
                engine.ProcessAt(i);

            // Tick the live bar (index 5) through several closes.
            md.SetClose(5, 110.0); engine.ProcessAt(5);
            md.SetClose(5, 120.0); engine.ProcessAt(5);
            md.SetClose(5, 130.0); engine.ProcessAt(5); // last tick

            // Open bar 6. Bar 5's committed close should be 130.
            // Window (closed) = bars 0..5 = 100,101,102,103,104,130;
            // bar 6 (live) close = 106.
            engine.ProcessAt(6);

            double expectedMean = (100 + 101 + 102 + 103 + 104 + 130 + 106) / 7.0;
            Assert.Equal(expectedMean,
                engine.Values.Statistics.Location.Mean, 10);
        }
    }
}

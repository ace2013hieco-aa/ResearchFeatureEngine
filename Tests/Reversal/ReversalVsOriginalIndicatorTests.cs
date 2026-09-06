using System;
using System.Collections.Generic;
using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Tests.Reversal
{
    /// <summary>
    /// Compares the production Reversal stage in
    /// <see cref="ReversalMode.TrailingStopPosition"/> mode against
    /// an independent reimplementation of the original cTrader
    /// indicator <c>AtrTrailingStopSmoothed</c>'s <c>pos</c> series,
    /// on real EURUSD M1 data.
    ///
    /// The original indicator's <c>pos</c> (1/-1/0) is the ATR
    /// trailing-stop position bias. <c>pos</c> flips are the
    /// reference "reversals" we compare against. Because the
    /// production <see cref="ATRSmoothReferenceSource"/> is a faithful
    /// transcription of that same algorithm, the published
    /// <see cref="ReferenceRuntimeValues.Regime"/> must equal
    /// the original <c>pos</c> bar-for-bar, and therefore the
    /// production reversal bars/directions must match the original
    /// <c>pos</c> flips.
    /// </summary>
    public sealed class ReversalVsOriginalIndicatorTests
    {
        /// <summary>
        /// Independent, allocation-free reimplementation of the
        /// original <c>AtrTrailingStopSmoothed</c> algorithm's
        /// trailing-stop + position (pos) computation, kept
        /// intentionally separate from the production source so it
        /// can serve as a cross-check.
        /// </summary>
        private sealed class OriginalPosComputer
        {
            private readonly int _atrPeriod;
            private readonly double _atrMult;
            private double _emaTr;
            private double _stop;
            private double _pos;

            public OriginalPosComputer(int atrPeriod, double atrMult)
            {
                _atrPeriod = atrPeriod;
                _atrMult = atrMult;
            }

            public double Compute(
                int index,
                double close, double high, double low,
                double prevClose)
            {
                double trueRange;
                if (index == 0)
                {
                    trueRange = high - low;
                    _emaTr = trueRange;
                    _stop = close;
                    _pos = 0.0;
                }
                else
                {
                    trueRange = Math.Max(
                        high - low,
                        Math.Max(
                            Math.Abs(high - prevClose),
                            Math.Abs(low - prevClose)));
                    double alpha = 2.0 / (_atrPeriod + 1.0);
                    _emaTr = alpha * trueRange + (1.0 - alpha) * _emaTr;
                }

                double nLoss = _atrMult * _emaTr;
                double prevStop = index == 0 ? close : _stop;
                double prevPos = index == 0 ? 0.0 : _pos;

                double currentStop;
                if (close > prevStop && prevClose > prevStop)
                    currentStop = Math.Max(prevStop, close - nLoss);
                else if (close < prevStop && prevClose < prevStop)
                    currentStop = Math.Min(prevStop, close + nLoss);
                else if (close > prevStop)
                    currentStop = close - nLoss;
                else
                    currentStop = close + nLoss;

                double p;
                if (prevClose < prevStop && close > prevStop)
                    p = 1.0;
                else if (prevClose > prevStop && close < prevStop)
                    p = -1.0;
                else
                    p = prevPos;

                _stop = currentStop;
                _pos = p;
                return p;
            }
        }

        [Fact]
        public void ProductionReversal_MatchesOriginalPosFlips_OnRealData()
        {
            string csvPath =
                @"TestData\EURUSD_M1_10000.csv";
            if (!System.IO.File.Exists(csvPath))
            {
                // Tolerate running from a different working dir.
                csvPath =
                    @"D:\Software\Distance\Tests\TestData\EURUSD_M1_10000.csv";
            }

            var marketData = new CsvMarketData(csvPath);

            const int atrPeriod = 16;
            const double atrMult = 5.1;
            const int smoothLength = 100;

            var referenceSource = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod, atrMult, smoothLength));

            var config = new EngineConfiguration(
                marketData: marketData,
                values: new EngineValues(),
                referenceSource: referenceSource,
                scaleModel: new ATRScaleModel(14),
                normalizationModel: new ScaleNormalizationModel(),
                statisticModels: new List<IStatisticModel>
                {
                    new MeanModel(),
                    new StandardDeviationModel()
                },
                options: new EngineOptions
                {
                    StatisticsWindowSize = 252,
                    ReversalMode = ReversalMode.TrailingStopPosition
                });

            var engine = new ResearchFeatureEngineBuilder(config).Build();

            var original = new OriginalPosComputer(atrPeriod, atrMult);

            double prevPos = 0.0;
            int matchedFlips = 0;
            int originalFlips = 0;
            int productionReversals = 0;
            int bars = 0;

            int idx = 0;
            while (marketData.MoveNext())
            {
                engine.Update();

                double close = marketData.Close[idx];
                double high = marketData.High[idx];
                double low = marketData.Low[idx];
                double prevClose = idx == 0 ? close : marketData.Close[idx - 1];

                double pos = original.Compute(idx, close, high, low, prevClose);

                // The published Regime must equal the original
                // pos bar-for-bar (the production source is a faithful
                // transcription).
                Assert.Equal(pos, engine.Values.Reference.Regime, 10);

                // Original pos flip => expected reversal.
                bool originalFlip = idx > 0 && pos != prevPos && pos != 0.0;
                if (originalFlip)
                {
                    originalFlips++;
                    // Direction: pos 1 = Up (below->above), -1 = Down.
                    var expectedDir = pos > 0
                        ? ReversalDirection.Up
                        : ReversalDirection.Down;
                    Assert.Equal(0, engine.Values.Reversal.BarsSinceReversal);
                    Assert.Equal(expectedDir, engine.Values.Reversal.Direction);
                    Assert.True(engine.Values.Reversal.IsReversalBar);
                    matchedFlips++;
                }

                if (engine.Values.Reversal.IsReversalBar)
                    productionReversals++;

                // Continuation bars must not flag a reversal.
                if (!originalFlip && engine.Values.Reversal.BarsSinceReversal.HasValue)
                {
                    Assert.False(engine.Values.Reversal.IsReversalBar);
                }

                prevPos = pos;
                bars++;
                idx++;
            }

            Assert.Equal(10_000, bars);
            Assert.True(originalFlips > 0,
                "Expected at least one original pos flip in 10k bars.");
            Assert.Equal(originalFlips, matchedFlips);
            Assert.Equal(productionReversals, originalFlips);
        }
    }
}

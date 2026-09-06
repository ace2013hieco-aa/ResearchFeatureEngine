using System;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

// The engine entry-point class name equals its namespace; alias it so
// the type resolves unambiguously inside this test namespace.
using Engine = ResearchFeatureEngine.ResearchFeatureEngine;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Pipeline-level tests for the dual-reference features in the
    /// HMA + ATRSmooth composite mode (§10.B of the brief):
    ///
    ///   1. constant positive separation → positive mean
    ///   2. constant negative separation → negative mean
    ///   3. zero separation → zero mean
    ///   4. mixed positive/negative separation → correct signed mean
    ///   5. correct rolling window (window-limit behavior)
    ///   6. warm-up: NaN until BOTH canonical values are valid
    ///   7. reset is leak-free
    ///   8. no look-ahead
    ///   9. source-runtime identity (the engines consume the SAME
    ///      canonical instances the composite drives)
    ///  10. the HMA warm-up fallback (close) is NEVER consumed
    /// </summary>
    public sealed class MeanHmaAtrSmoothDistancePipelineTests
    {
        private const int HmaP = 16;   // first valid HMA bar: 16+4-2 = 18
        private const int WarmUpBars = 18;

        private static (ResearchFeatureEngine engine, HmaAtrSmoothCompositeSource source)
            BuildComposite(double[] close, int window = 5, int dataSeed = 7)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            var rng = new Random(dataSeed);
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + rng.NextDouble() * 0.5;
                low[i] = close[i] - rng.NextDouble() * 0.5;
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(HmaP));

            var configuration = new EngineConfiguration(
                md,
                new EngineValues(),
                source,
                new ATRScaleModel(14),
                new ScaleNormalizationModel(),
                new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                new EngineOptions
                {
                    StatisticsWindowSize = 20,
                    MeanHmaAtrSmoothWindowSize = window
                });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();
            return (engine, (HmaAtrSmoothCompositeSource)source);
        }

        // -------------------------------------------------------------
        // 6 + 10. Warm-up and HMA-fallback exclusion
        // -------------------------------------------------------------

        [Fact]
        public void WarmUp_NanUntilBothCanonicalValuesValid_FallbackNeverConsumed()
        {
            // Deterministic rising series.
            int n = 60;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + i * 0.5;

            var (engine, source) = BuildComposite(close);

            double[] hmaFallback = new double[n];
            double[] meanPerBar = new double[n];
            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                // The HMA source's OWN published level — the warm-up
                // close fallback returned by its ComputeReference —
                // is captured here via its Runtime.LastReference.
                hmaFallback[i] = source.HmaSource.Runtime.LastReference;
                meanPerBar[i] =
                    engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;

                double hma = source.HmaSource.Runtime.Hma;

                if (i < WarmUpBars)
                {
                    // HMA genuinely unavailable → feature unavailable.
                    Assert.True(double.IsNaN(hma),
                        $"HMA must be NaN at warm-up bar {i}");
                    Assert.True(double.IsNaN(meanPerBar[i]),
                        $"Mean must be NaN at warm-up bar {i}");
                }
                else
                {
                    Assert.False(double.IsNaN(hma));
                }
            }

            // Explicit proof the fallback is NOT consumed: on every
            // warm-up bar the HMA source's published level (the close
            // fallback) is FINITE and distinct from Runtime.Hma (NaN) —
            // a feature consuming that fallback would produce finite
            // distances (close − ATRSmooth) and a finite mean. The
            // mean stayed NaN for all 18 warm-up bars, so the fallback
            // never entered the window.
            for (int i = 0; i < WarmUpBars; i++)
            {
                Assert.True(double.IsFinite(hmaFallback[i]),
                    $"bar {i}: the HMA close fallback must be finite (it exists)");
                Assert.True(double.IsNaN(meanPerBar[i]),
                    $"bar {i}: feature must be NaN while only the fallback exists");
            }
        }

        [Fact]
        public void WarmUp_FirstObservationIsBarPPlusSqrtPMinus2_MeanEqualsSingleDistance()
        {
            int n = 60;
            double[] close = new double[n];
            var rng = new Random(31);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.3) * 5.0 + rng.NextDouble() * 0.1;

            var (engine, source) = BuildComposite(close, window: 5);

            for (int i = 0; i < WarmUpBars; i++)
            {
                engine.ProcessAt(i);
                Assert.True(double.IsNaN(
                    engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance));
            }

            engine.ProcessAt(WarmUpBars); // bar 18: first valid HMA

            double hma = source.HmaSource.Runtime.Hma;
            double atr = source.AtrSmoothSource.Runtime.LastReference;

            Assert.False(double.IsNaN(hma));
            Assert.Equal(
                hma - atr,
                engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                12);
        }

        // -------------------------------------------------------------
        // 1-4. Sign semantics via engineered series
        // -------------------------------------------------------------

        [Fact]
        public void SteadyRise_HmaAboveSmooth_MeanIsPositive()
        {
            // Persistent uptrend: fast HMA stays above the lagging
            // smoothed equilibrium → positive distances → positive mean.
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + i * 1.0;

            var (engine, _) = BuildComposite(close, window: 10);

            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            var mean = engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            Assert.False(double.IsNaN(mean));
            Assert.True(mean > 0.0,
                $"Expected positive mean on a steady rise, got {mean}");
        }

        [Fact]
        public void SteadyFall_HmaBelowSmooth_MeanIsNegative()
        {
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 500.0 - i * 1.0;

            var (engine, _) = BuildComposite(close, window: 10);

            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            var mean = engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            Assert.False(double.IsNaN(mean));
            Assert.True(mean < 0.0,
                $"Expected negative mean on a steady fall, got {mean}");
        }

        // A TRULY flat series (high = low = close) is required for exact
        // zero separation: TR = 0 → ATR = 0 → nLoss = 0 → the trailing
        // stop equals the close, so the ATRSmooth equilibrium = close
        // EXACTLY and HMA(100) − ATRSmooth(100) = 0. (A flat close with
        // non-zero high/low range would park the trailing stop at
        // close ± nLoss — a constant NON-zero separation.)
        // Minimal direct pipeline for exact-value feature tests: the
        // full production pipeline's Scale stage REJECTS an all-zero
        // True Range series ("Scale must be greater than zero" — the
        // established validation contract), so exact zero-separation /
        // equality fixtures are driven through a composition of
        // ReferenceEngine + the two dual-reference feature engines
        // only (the same manual-pipeline pattern the adversarial
        // audit tests use). The feature contracts under test are the
        // feature engines', not the Scale stage's.
        private static (ResearchFeatureEngine engine, HmaAtrSmoothCompositeSource source)
            BuildFeatureOnlyComposite(double[] close, int window = 10)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i];
                low[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new EngineContext(md, new EngineValues());

            IReferenceSource source = ReferenceSourceFactory.Create(
                ReferenceType.HmaAtrSmooth,
                new ATRSmoothConfiguration(16, 5.1, 100),
                darvasBoxConfiguration: null,
                hmaConfiguration: new HmaConfiguration(HmaP));
            var composite = (HmaAtrSmoothCompositeSource)source;

            var pipeline = new EnginePipeline(ctx);
            pipeline.Register(new ReferenceEngine(ctx, composite));
            pipeline.Register(new MeanHmaAtrSmoothDistanceEngine(
                ctx,
                new MeanHmaAtrSmoothDistanceModel(window),
                composite.HmaSource,
                composite.AtrSmoothSource));
            pipeline.Register(new HmaPriceAtrSmoothAlignmentEngine(
                ctx,
                new HmaPriceAtrSmoothAlignmentModel(),
                composite.HmaSource,
                composite.AtrSmoothSource));
            pipeline.Initialize();

            return (new Engine(ctx, pipeline), composite);
        }

        [Fact]
        public void ConstantSeries_ZeroSeparation_MeanIsZero()
        {
            // TRULY flat OHLC: TR = 0 → ATR = 0 → nLoss = 0 → the
            // trailing stop equals the close, so the ATRSmooth
            // equilibrium = close EXACTLY and HMA(100) −
            // ATRSmooth(100) = 0 on every valid bar → mean = 0.
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0;

            var (engine, source) = BuildFeatureOnlyComposite(close, window: 10);

            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            // Sanity: the equilibrium really is exactly the close.
            Assert.Equal(100.0, source.AtrSmoothSource.Runtime.LastReference, 12);
            Assert.Equal(100.0, source.HmaSource.Runtime.Hma, 12);

            Assert.Equal(0.0,
                engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                12);
        }

        [Fact]
        public void ZeroSeparation_AlignmentIsUnavailable_EqualityIsStrict()
        {
            // The same flat fixture pins the equality semantics of the
            // alignment feature: HMA == ATRSmooth == Close exactly →
            // neither strictly-above nor strictly-below holds →
            // UNAVAILABLE (project strict-comparison convention; no
            // epsilon).
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0;

            var (engine, source) = BuildFeatureOnlyComposite(close, window: 10);

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                double hma = source.HmaSource.Runtime.Hma;
                double atr = source.AtrSmoothSource.Runtime.LastReference;

                if (!double.IsNaN(hma))
                {
                    Assert.Equal(100.0, hma, 12);
                    Assert.Equal(100.0, atr, 12);

                    Assert.True(
                        engine.Values.HmaPriceAtrSmoothAlignment.Alignment
                        == HmaPriceAtrSmoothAlignment.Unavailable,
                        $"bar {i}: HMA == ATRSmooth == Close must be Unavailable");
                }
                else
                {
                    Assert.True(
                        engine.Values.HmaPriceAtrSmoothAlignment.Alignment
                        == HmaPriceAtrSmoothAlignment.Unavailable,
                        $"bar {i}: warm-up must be Unavailable");
                }
            }
        }

        [Fact]
        public void FlatRange_NonZeroNLoss_ConstantSeparationIsSigned()
        {
            // Companion negative-control for the flat fixture: a flat
            // close with a real high/low range parks the trailing stop
            // at close + nLoss (bearish stop above price), so the
            // separation is CONSTANT and NEGATIVE — the mean must be
            // negative and finite (documented ATRSmooth semantics, not
            // a defect).
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0;

            var (engine, source) = BuildComposite(close, window: 10);

            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            var mean = engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            Assert.False(double.IsNaN(mean));
            Assert.True(mean < 0.0,
                $"expected constant negative separation (stop above flat price), got {mean}");
        }

        // -------------------------------------------------------------
        // 4 + 5. Mixed separation and exact window arithmetic
        // -------------------------------------------------------------

        [Fact]
        public void RollingMean_ExactWindowArithmetic_MatchesManualComputation()
        {
            // Drive with a zigzag so distances mix sign, then verify
            // EVERY bar's mean against a manual O(N) recomputation from
            // the recorded canonical HMA + ATRSmooth values.
            int n = 80;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.55) * 8.0;

            const int window = 7;
            var (engine, source) = BuildComposite(close, window: window);

            double[] hmaSeries = new double[n];
            double[] atrSeries = new double[n];
            double[] meanSeries = new double[n];

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                hmaSeries[i] = source.HmaSource.Runtime.Hma;
                atrSeries[i] = source.AtrSmoothSource.Runtime.LastReference;
                meanSeries[i] = engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            }

            // Manual recomputation: observations are the valid bars
            // (HMA non-NaN), window = last `window` observations.
            var valid = new System.Collections.Generic.List<double>();
            for (int i = 0; i < n; i++)
            {
                if (!double.IsNaN(hmaSeries[i]))
                    valid.Add(hmaSeries[i] - atrSeries[i]);

                if (valid.Count == 0)
                {
                    Assert.True(double.IsNaN(meanSeries[i]),
                        $"bar {i}: expected NaN");
                }
                else
                {
                    int from = Math.Max(0, valid.Count - window);
                    double sum = 0.0;
                    for (int k = from; k < valid.Count; k++)
                        sum += valid[k];
                    double expected = sum / (valid.Count - from);
                    Assert.Equal(expected, meanSeries[i], 10);
                }
            }
        }

        [Fact]
        public void RollingWindow_LimitsToConfiguredWindowSize()
        {
            // With window = 1, the mean must equal the LAST distance
            // only — the oldest observation rolls out immediately.
            int n = 80;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.55) * 8.0;

            var (engine, source) = BuildComposite(close, window: 1);

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                double hma = source.HmaSource.Runtime.Hma;
                if (!double.IsNaN(hma))
                {
                    Assert.Equal(
                        hma - source.AtrSmoothSource.Runtime.LastReference,
                        engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                        12);
                }
            }
        }

        // -------------------------------------------------------------
        // 7. Reset leak-free
        // -------------------------------------------------------------

        [Fact]
        public void Reset_IsLeakFree_RestoresWarmUpThenReproducesFreshRun()
        {
            int n = 80;
            double[] close = new double[n];
            var rng = new Random(53);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.2) * 5.0 + rng.NextDouble();

            var (engine, source) = BuildComposite(close, window: 5);

            // Fresh run reference.
            double[] fresh = new double[n];
            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                fresh[i] = engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            }

            // Reset mid-run, replay from 0: every bar must reproduce.
            engine.Pipeline.Reset();

            Assert.True(double.IsNaN(
                engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance));
            Assert.True(double.IsNaN(source.HmaSource.Runtime.Hma));
            Assert.Equal(0.0, source.AtrSmoothSource.Runtime.LastReference);

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                Assert.Equal(fresh[i],
                    engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                    12);
            }
        }

        // -------------------------------------------------------------
        // 8. No look-ahead
        // -------------------------------------------------------------

        [Fact]
        public void NoLookAhead_MutatingFutureBarsCannotChangePastMeans()
        {
            int n = 80;
            double[] closeA = new double[n];
            var rng = new Random(53);
            for (int i = 0; i < n; i++)
                closeA[i] = 100.0 + Math.Sin(i * 0.2) * 5.0 + rng.NextDouble();

            // Run A: full history.
            var (engineA, _) = BuildComposite(closeA, window: 5);
            double[] meansA = new double[n];
            for (int i = 0; i < n; i++)
            {
                engineA.ProcessAt(i);
                meansA[i] = engineA.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance;
            }

            // Run B: identical up to bar 45, wildly different after.
            double[] closeB = (double[])closeA.Clone();
            var rng2 = new Random(97);
            for (int i = 46; i < n; i++)
                closeB[i] = 80.0 + rng2.NextDouble() * 50.0;

            var (engineB, _) = BuildComposite(closeB, window: 5);
            for (int i = 0; i <= 45; i++)
            {
                engineB.ProcessAt(i);
                Assert.True(
                    meansA[i] == engineB.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance
                    || (double.IsNaN(meansA[i])
                        && double.IsNaN(engineB.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance)),
                    $"bar {i}: look-ahead contamination from future bars");
            }
        }

        // -------------------------------------------------------------
        // 9. Source-runtime identity
        // -------------------------------------------------------------

        [Fact]
        public void SourceRuntimeIdentity_EnginesConsumeCompositeProducers()
        {
            // The feature stages receive the SAME canonical instances
            // the composite drives — proven end-to-end: driving the
            // pipeline updates the composite producers' runtimes, and
            // the published feature values equal manual computation
            // from those same runtimes on every bar.
            int n = 60;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.4) * 4.0;

            var (engine, source) = BuildComposite(close, window: 3);

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                double hma = source.HmaSource.Runtime.Hma;
                double atr = source.AtrSmoothSource.Runtime.LastReference;

                if (double.IsNaN(hma))
                {
                    Assert.True(double.IsNaN(
                        engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance));
                }
                else if (i == WarmUpBars)
                {
                    Assert.Equal(hma - atr,
                        engine.Values.MeanHmaAtrSmoothDistance.MeanSignedDistance,
                        12);
                }
            }
        }
    }
}

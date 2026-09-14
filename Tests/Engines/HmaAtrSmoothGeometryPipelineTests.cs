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
    /// M11.1 pipeline-level tests for the two HMA/ATRSmooth geometry
    /// measurements in the composite mode: warm-up, the constant-price
    /// degenerate fixture, determinism / reset / re-tick idempotence,
    /// mode gating, no-lookahead, and canonical-value equality against
    /// the composite's own producer runtimes.
    /// </summary>
    public sealed class HmaAtrSmoothGeometryPipelineTests
    {
        private const int HmaP = 16;   // first valid HMA bar: 16+4-2 = 18
        private const int WarmUpBars = 18;

        private static (ResearchFeatureEngine engine, HmaAtrSmoothCompositeSource source)
            BuildComposite(double[] close, int dataSeed = 7)
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
                new EngineOptions { StatisticsWindowSize = 20 });

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();
            return (engine, (HmaAtrSmoothCompositeSource)source);
        }

        // A TRULY flat OHLC fixture (high = low = close) for the
        // degenerate geometry test. The full production pipeline's
        // Scale stage legitimately rejects an all-zero True Range
        // series ("Scale must be greater than zero" — the established
        // contract), so the degenerate fixture drives a minimal
        // composition containing every stage EXCEPT Scale/
        // Normalization, with the M11.1 separation engine fed a
        // constant positive Scale directly through the same builder
        // pattern the adversarial audit tests use (manual pipeline
        // registration).
        private static ResearchFeatureEngine BuildFlatFeatureOnly(double[] close, double scale)
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
            pipeline.Register(new HmaAtrSmoothSeparationEngine(
                ctx,
                new HmaAtrSmoothSeparationModel(),
                composite.HmaSource,
                composite.AtrSmoothSource));
            pipeline.Register(new HmaAtrSmoothRelativeClosePositionEngine(
                ctx,
                new HmaAtrSmoothRelativeClosePositionModel(),
                composite.HmaSource,
                composite.AtrSmoothSource));
            pipeline.Initialize();

            // Publish the constant positive Scale the separation
            // stage consumes (the production Scale stage cannot run
            // on this fixture; the geometry contracts under test are
            // the M11.1 stages', not the Scale stage's).
            ctx.Values.Scale.Scale = scale;

            return new Engine(ctx, pipeline);
        }

        // ---------------------------------------------------------
        // Warm-up: bars 0–17 NaN, bar 18 available
        // ---------------------------------------------------------

        [Fact]
        public void WarmUp_BothNanUntilBar18_FirstValidAtBar18()
        {
            int n = 60;
            double[] close = new double[n];
            var rng = new Random(11);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + (rng.NextDouble() - 0.5) * 10.0;

            var (engine, source) = BuildComposite(close);

            int firstValidSeparation = -1;
            int firstValidPosition = -1;

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                double hma = source.HmaSource.Runtime.Hma;
                double atr = source.AtrSmoothSource.Runtime.LastReference;
                double scale = engine.Values.Scale.Scale;

                double separation = engine.Values.HmaAtrSmoothSeparation.Separation;
                double position = engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;

                if (i < WarmUpBars)
                {
                    // Bars 0–17: HMA genuinely unavailable → both NaN.
                    Assert.True(double.IsNaN(hma),
                        $"bar {i}: HMA must be NaN during warm-up");
                    Assert.True(double.IsNaN(separation),
                        $"bar {i}: separation must be NaN during warm-up");
                    Assert.True(double.IsNaN(position),
                        $"bar {i}: relative close position must be NaN during warm-up");
                }
                else
                {
                    // After warm-up: HMA finite. Separation always
                    // finite (Scale > 0); position finite unless the
                    // exact-zero denominator holds (not on this
                    // fixture's noisy walk).
                    Assert.False(double.IsNaN(hma));
                    Assert.False(double.IsNaN(separation));
                    Assert.False(double.IsNaN(position));

                    if (firstValidSeparation < 0)
                    {
                        firstValidSeparation = i;
                        firstValidPosition = i;
                        Assert.Equal(WarmUpBars, i);
                    }

                    // Canonical-value equality: the published values
                    // equal direct recomputation from the canonical
                    // producer runtimes and the published Scale.
                    Assert.Equal((hma - atr) / scale, separation, 12);
                    Assert.Equal(
                        (close[i] - atr) / (hma - atr),
                        position,
                        12);
                }
            }

            Assert.Equal(WarmUpBars, firstValidSeparation);
            Assert.Equal(WarmUpBars, firstValidPosition);
        }

        [Fact]
        public void WarmUp_FallbackCloseIsNeverConsumedAsHma()
        {
            // During warm-up the HMA source's published measurement
            // level falls back to the current close. A feature
            // consuming that fallback would publish FINITE values on
            // warm-up bars; both measurements stay NaN for all 18
            // warm-up bars, proving the fallback never enters.
            int n = 60;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + i * 0.5;

            var (engine, source) = BuildComposite(close);

            for (int i = 0; i < WarmUpBars; i++)
            {
                engine.ProcessAt(i);

                // The fallback exists and is finite...
                double fallback = source.HmaSource.Runtime.LastReference;
                Assert.True(double.IsFinite(fallback),
                    $"bar {i}: the HMA close fallback must exist (finite)");

                // ...but neither measurement consumes it.
                Assert.True(double.IsNaN(
                    engine.Values.HmaAtrSmoothSeparation.Separation));
                Assert.True(double.IsNaN(
                    engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition));
            }
        }

        // ---------------------------------------------------------
        // Degenerate constant-price fixture
        // ---------------------------------------------------------

        [Fact]
        public void ConstantPrice_SeparationExactlyZero_PositionNaN_NoException()
        {
            // HMA == ATRSmooth == Close == 100 exactly on every
            // post-warm-up bar (TR = 0 → ATR = 0 → nLoss = 0 → the
            // trailing stop equals the close → the equilibrium is
            // exactly the close; the HMA of a constant series is the
            // constant). Expected: Separation = 0 exactly (zero
            // divided by the positive Scale), RelativeClosePosition
            // = NaN (exact zero denominator), and NO exception.
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0;

            const double scale = 2.0;
            var engine = BuildFlatFeatureOnly(close, scale);

            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                if (i < WarmUpBars)
                {
                    Assert.True(double.IsNaN(
                        engine.Values.HmaAtrSmoothSeparation.Separation),
                        $"bar {i}: warm-up must be NaN");
                    Assert.True(double.IsNaN(
                        engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition),
                        $"bar {i}: warm-up must be NaN");
                }
                else
                {
                    // 0 / 2 = 0 exactly: coincident references are a
                    // VALID zero separation, not an unavailable state.
                    Assert.Equal(0.0,
                        engine.Values.HmaAtrSmoothSeparation.Separation,
                        12);
                    // Degenerate denominator → NaN, no exception.
                    Assert.True(double.IsNaN(
                        engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition),
                        $"bar {i}: exact-zero denominator must be NaN");
                }
            }
        }

        [Fact]
        public void ConstantPrice_NoEpsilonLogicChangesDegenerateBehavior()
        {
            // The degenerate fixture held for 102 consecutive bars
            // above. Re-process the final bar repeatedly (re-tick):
            // the exact-equality state is stable — an epsilon/floor
            // would flip NaN into a huge finite value here.
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0;

            var engine = BuildFlatFeatureOnly(close, 2.0);
            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            for (int tick = 0; tick < 5; tick++)
            {
                engine.ProcessAt(n - 1);
                Assert.Equal(0.0,
                    engine.Values.HmaAtrSmoothSeparation.Separation, 12);
                Assert.True(double.IsNaN(
                    engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition));
            }
        }

        // ---------------------------------------------------------
        // Determinism / reset / re-tick
        // ---------------------------------------------------------

        [Fact]
        public void Determinism_RepeatedRunsProduceIdenticalValues()
        {
            int n = 200;
            double[] close = new double[n];
            var rng = new Random(29);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.3) * 6.0 + rng.NextDouble() * 0.5;

            var (engineA, _) = BuildComposite(close);
            var sepA = new double[n];
            var posA = new double[n];
            for (int i = 0; i < n; i++)
            {
                engineA.ProcessAt(i);
                sepA[i] = engineA.Values.HmaAtrSmoothSeparation.Separation;
                posA[i] = engineA.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;
            }

            var (engineB, _) = BuildComposite(close);
            for (int i = 0; i < n; i++)
            {
                engineB.ProcessAt(i);
                Assert.Equal(sepA[i],
                    engineB.Values.HmaAtrSmoothSeparation.Separation, 12);
                Assert.Equal(posA[i],
                    engineB.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition,
                    12);
            }
        }

        [Fact]
        public void ReTick_SameBarUpdatesAreIdempotent_NoStateAccumulates()
        {
            int n = 120;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.35) * 6.0;

            var (engine, _) = BuildComposite(close);
            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            double expectedSep = engine.Values.HmaAtrSmoothSeparation.Separation;
            double expectedPos = engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;

            for (int tick = 0; tick < 10; tick++)
            {
                engine.ProcessAt(n - 1);
                Assert.Equal(expectedSep,
                    engine.Values.HmaAtrSmoothSeparation.Separation, 12);
                Assert.Equal(expectedPos,
                    engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition,
                    12);
            }
        }

        [Fact]
        public void Reset_RestoresNaNThenReplayIsIdentical()
        {
            int n = 120;
            double[] close = new double[n];
            var rng = new Random(61);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.2) * 5.0 + rng.NextDouble();

            var (engine, _) = BuildComposite(close);

            var sepFirst = new double[n];
            var posFirst = new double[n];
            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                sepFirst[i] = engine.Values.HmaAtrSmoothSeparation.Separation;
                posFirst[i] = engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;
            }

            engine.Pipeline.Reset();

            // Reset restores the unavailable defaults.
            Assert.True(double.IsNaN(
                engine.Values.HmaAtrSmoothSeparation.Separation));
            Assert.True(double.IsNaN(
                engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition));

            // Replay reproduces every bar exactly.
            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);
                Assert.Equal(sepFirst[i],
                    engine.Values.HmaAtrSmoothSeparation.Separation, 12);
                Assert.Equal(posFirst[i],
                    engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition,
                    12);
            }
        }

        // ---------------------------------------------------------
        // Mode gating: non-composite modes never publish
        // ---------------------------------------------------------

        [Fact]
        public void ModeGating_NonCompositeModes_ValuesStayNaN()
        {
            int n = 60;
            double[] close = new double[n];
            var rng = new Random(23);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + (rng.NextDouble() - 0.5) * 8.0;

            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + 0.3;
                low[i] = close[i] - 0.3;
                volume[i] = 100.0;
            }

            foreach (ReferenceType type in new[]
                { ReferenceType.ATRSmooth2, ReferenceType.DarvasBox, ReferenceType.Hma })
            {
                var md = new FlatOhlcvMarketData(open, high, low, close, volume);
                IReferenceSource source = ReferenceSourceFactory.Create(
                    type,
                    type == ReferenceType.ATRSmooth2 || type == ReferenceType.Hma
                        ? new ATRSmoothConfiguration(16, 5.1, 100)
                        : null,
                    type == ReferenceType.DarvasBox
                        ? new DarvasBoxConfiguration(5)
                        : null,
                    type == ReferenceType.Hma
                        ? new HmaConfiguration(HmaP)
                        : null);

                var configuration = new EngineConfiguration(
                    md,
                    new EngineValues(),
                    source,
                    new ATRScaleModel(14),
                    new ScaleNormalizationModel(),
                    new System.Collections.Generic.List<IStatisticModel> { new MeanModel() },
                    new EngineOptions { StatisticsWindowSize = 20 });

                var engine = new ResearchFeatureEngineBuilder(configuration).Build();

                for (int i = 0; i < n; i++)
                {
                    engine.ProcessAt(i);
                    Assert.True(double.IsNaN(
                        engine.Values.HmaAtrSmoothSeparation.Separation),
                        $"{type} at bar {i}: separation must stay NaN");
                    Assert.True(double.IsNaN(
                        engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition),
                        $"{type} at bar {i}: relative close position must stay NaN");
                }

                // Existing shared values remain unchanged by the
                // presence of the new (absent) stages: spot-check the
                // shared families on the final bar.
                Assert.True(engine.Values.Scale.Scale > 0.0);
                Assert.False(double.IsNaN(engine.Values.Reference.Price));
            }
        }

        // ---------------------------------------------------------
        // No-lookahead
        // ---------------------------------------------------------

        [Fact]
        public void NoLookAhead_FutureBarsCannotChangePastValues()
        {
            int n = 120;
            double[] closeA = new double[n];
            var rng = new Random(53);
            for (int i = 0; i < n; i++)
                closeA[i] = 100.0 + Math.Sin(i * 0.2) * 5.0 + rng.NextDouble();

            // Run A: full history.
            var (engineA, _) = BuildComposite(closeA);
            var sepA = new double[n];
            var posA = new double[n];
            for (int i = 0; i < n; i++)
            {
                engineA.ProcessAt(i);
                sepA[i] = engineA.Values.HmaAtrSmoothSeparation.Separation;
                posA[i] = engineA.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;
            }

            // Run B: identical up to bar 60, wildly different after.
            double[] closeB = (double[])closeA.Clone();
            var rng2 = new Random(97);
            for (int i = 61; i < n; i++)
                closeB[i] = 80.0 + rng2.NextDouble() * 50.0;

            var (engineB, _) = BuildComposite(closeB);
            for (int i = 0; i <= 60; i++)
            {
                engineB.ProcessAt(i);

                double sepB = engineB.Values.HmaAtrSmoothSeparation.Separation;
                double posB = engineB.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition;

                bool sepEqual = sepA[i] == sepB
                    || (double.IsNaN(sepA[i]) && double.IsNaN(sepB));
                bool posEqual = posA[i] == posB
                    || (double.IsNaN(posA[i]) && double.IsNaN(posB));

                Assert.True(sepEqual,
                    $"bar {i}: separation contaminated by future bars");
                Assert.True(posEqual,
                    $"bar {i}: relative close position contaminated by future bars");
            }
        }

        // ---------------------------------------------------------
        // Canonicality: the published values consume the composite's
        // own producer runtimes and the published Scale only
        // ---------------------------------------------------------

        [Fact]
        public void CanonicalInputs_PublishedValuesMatchProducerRuntimesAndScale()
        {
            int n = 150;
            double[] close = new double[n];
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + Math.Sin(i * 0.4) * 8.0;

            var (engine, source) = BuildComposite(close);

            int checkedBars = 0;
            for (int i = 0; i < n; i++)
            {
                engine.ProcessAt(i);

                double hma = source.HmaSource.Runtime.Hma;
                double atr = source.AtrSmoothSource.Runtime.LastReference;
                double scale = engine.Values.Scale.Scale; // the Scale stage's own output

                if (double.IsNaN(hma))
                    continue;

                // Separation consumes the canonical HMA, the canonical
                // ATRSmooth equilibrium, and the canonical Scale(14).
                Assert.Equal(
                    (hma - atr) / scale,
                    engine.Values.HmaAtrSmoothSeparation.Separation,
                    12);

                // Position consumes the current close, the canonical
                // HMA, and the canonical ATRSmooth equilibrium.
                if (hma != atr)
                {
                    Assert.Equal(
                        (close[i] - atr) / (hma - atr),
                        engine.Values.HmaAtrSmoothRelativeClosePosition.RelativeClosePosition,
                        12);
                }

                checkedBars++;
            }

            Assert.True(checkedBars > 100, "fixture must exercise 100+ valid bars");
        }

        // ---------------------------------------------------------
        // Stage ordering: the stages run AFTER Scale (they consume it)
        // ---------------------------------------------------------

        [Fact]
        public void Placement_ConsumesPublishedScale_ExactlyTheScaleStageValue()
        {
            // Independently recompute Scale(14) — the simple mean of
            // True Range over the last 14 bars (ATRScaleModel's exact
            // contract) — from the fixture's OHLC arrays, and verify
            // (a) the engine's published Scale equals it, and
            // (b) the published separation equals (HMA − ATRSmooth)
            //     divided by that same value.
            int n = 100;
            double[] close = new double[n];
            var rng = new Random(71);
            for (int i = 0; i < n; i++)
                close[i] = 100.0 + (rng.NextDouble() - 0.5) * 4.0;

            // Regenerate the exact OHLC arrays BuildComposite(dataSeed:7)
            // produces for this close series (same Random consumption
            // order: high uses one draw, low the next).
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            var rngOhlc = new Random(7);
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + rngOhlc.NextDouble() * 0.5;
                low[i] = close[i] - rngOhlc.NextDouble() * 0.5;
            }

            var (engine, source) = BuildComposite(close);
            for (int i = 0; i < n; i++)
                engine.ProcessAt(i);

            // Independent Scale(14): mean of True Range over bars
            // max(1, n-14) .. n-1 inclusive.
            double sum = 0.0;
            int count = 0;
            for (int k = Math.Max(1, n - 14); k < n; k++)
            {
                double tr = Math.Max(
                    high[k] - low[k],
                    Math.Max(
                        Math.Abs(high[k] - close[k - 1]),
                        Math.Abs(low[k] - close[k - 1])));
                sum += tr;
                count++;
            }

            double expectedScale = sum / count;

            Assert.Equal(expectedScale, engine.Values.Scale.Scale, 12);

            double hma = source.HmaSource.Runtime.Hma;
            double atr = source.AtrSmoothSource.Runtime.LastReference;
            Assert.False(double.IsNaN(hma));

            Assert.Equal(
                (hma - atr) / expectedScale,
                engine.Values.HmaAtrSmoothSeparation.Separation,
                12);
        }
    }
}

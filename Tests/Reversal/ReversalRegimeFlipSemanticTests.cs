using System.Collections.Generic;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Reversal;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reversal
{
    /// <summary>
    /// Adversarial semantic tests for the canonical reversal
    /// definition:
    ///
    ///     A reversal occurs ONLY when ATR Smooth FLIPS REGIME.
    ///     A candle crossing the ATR Smooth line while the regime
    ///     stays unchanged is NOT a reversal.
    ///
    /// The canonical ATR Smooth regime state is the trailing-stop
    /// position bias published as
    /// <see cref="ReferenceRuntimeValues.Regime"/>
    /// (1 = bullish/long bias, -1 = bearish/short bias, 0 = flat),
    /// computed by <c>ATRSmoothReferenceSource</c> and verified
    /// bar-for-bar against the original <c>AtrTrailingStopSmoothed</c>
    /// <c>pos</c> series (see ReversalVsOriginalIndicatorTests).
    ///
    /// These tests drive the <see cref="ReversalEngine"/> through its
    /// DEFAULT constructor (the semantic under test) and decouple the
    /// two relations by setting <c>Regime</c> (regime) and
    /// <c>DirectionalExtension</c> (price-vs-line) independently:
    ///
    ///   * under the INCORRECT semantics (reversal = candle crossing
    ///     the reference line) tests A, B, G and the pipeline
    ///     equivalence test FAIL — a reversal fires on every cross;
    ///   * under the CANONICAL semantics (reversal = regime flip)
    ///     all tests pass.
    /// </summary>
    public sealed class ReversalRegimeFlipSemanticTests
    {
        private static (ReversalEngine engine, EngineContext context, EngineValues values)
            CreateDefault()
        {
            var values = new EngineValues();
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);

            // DEFAULT constructor: the reversal semantic under test.
            var engine = new ReversalEngine(context);
            engine.Initialize();
            return (engine, context, values);
        }

        /// <summary>
        /// Processes one bar with the regime state
        /// (<paramref name="regime"/>) and the price-vs-line
        /// relation (<paramref name="directionalExtension"/>) set
        /// independently — the key to exposing the conflation bug.
        /// </summary>
        private static void Process(
            ReversalEngine engine,
            EngineContext context,
            EngineValues values,
            int index,
            double regime,
            double directionalExtension)
        {
            values.Reference.Regime = regime;
            values.Distance.DirectionalExtension = directionalExtension;
            context.SetIndex(index);
            engine.Update();
        }

        // regime constants
        private const double Bullish = 1.0;   // pos = +1 (long bias)
        private const double Bearish = -1.0;  // pos = -1 (short bias)

        // price-vs-line constants (sign of close - reference)
        private const double PriceAboveLine = 1.0;
        private const double PriceBelowLine = -1.0;

        // -------------------------------------------------------------
        // TEST A — PRICE CROSSES BELOW, NO ATR FLIP => NO REVERSAL
        // Regime stays bullish; the candle crosses below the line.
        // -------------------------------------------------------------

        [Fact]
        public void TestA_PriceCrossesBelow_NoAtrFlip_NoReversal()
        {
            var (engine, context, values) = CreateDefault();

            Process(engine, context, values, 0, Bullish, PriceAboveLine);
            Assert.False(values.Reversal.IsReversalBar);
            Assert.Null(values.Reversal.BarsSinceReversal);

            // Candle crosses below the ATR Smooth line, but the ATR
            // Smooth regime remains bullish (no flip).
            Process(engine, context, values, 1, Bullish, PriceBelowLine);

            Assert.False(values.Reversal.IsReversalBar);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // TEST B — PRICE CROSSES ABOVE, NO ATR FLIP => NO REVERSAL
        // Regime stays bearish; the candle crosses above the line.
        // -------------------------------------------------------------

        [Fact]
        public void TestB_PriceCrossesAbove_NoAtrFlip_NoReversal()
        {
            var (engine, context, values) = CreateDefault();

            Process(engine, context, values, 0, Bearish, PriceBelowLine);
            Assert.False(values.Reversal.IsReversalBar);

            // Candle crosses above the ATR Smooth line, but the ATR
            // Smooth regime remains bearish (no flip).
            Process(engine, context, values, 1, Bearish, PriceAboveLine);

            Assert.False(values.Reversal.IsReversalBar);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // TEST C — ACTUAL BULLISH FLIP => BULLISH REVERSAL
        // Regime: bearish -> bullish.
        // -------------------------------------------------------------

        [Fact]
        public void TestC_ActualBullishFlip_BullishReversal()
        {
            var (engine, context, values) = CreateDefault();

            Process(engine, context, values, 0, Bearish, PriceBelowLine);
            Assert.False(values.Reversal.IsReversalBar);

            // ATR Smooth flips bearish -> bullish.
            Process(engine, context, values, 1, Bullish, PriceAboveLine);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Up, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // TEST D — ACTUAL BEARISH FLIP => BEARISH REVERSAL
        // Regime: bullish -> bearish.
        // -------------------------------------------------------------

        [Fact]
        public void TestD_ActualBearishFlip_BearishReversal()
        {
            var (engine, context, values) = CreateDefault();

            Process(engine, context, values, 0, Bullish, PriceAboveLine);
            Assert.False(values.Reversal.IsReversalBar);

            // ATR Smooth flips bullish -> bearish.
            Process(engine, context, values, 1, Bearish, PriceBelowLine);

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // TEST E — CONTINUED BULLISH REGIME => NO REVERSAL
        // -------------------------------------------------------------

        [Fact]
        public void TestE_ContinuedBullishRegime_NoReversal()
        {
            var (engine, context, values) = CreateDefault();

            for (int i = 0; i < 6; i++)
            {
                Process(engine, context, values, i, Bullish, PriceAboveLine);
                Assert.False(values.Reversal.IsReversalBar);
                Assert.Null(values.Reversal.BarsSinceReversal);
                Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
            }
        }

        // -------------------------------------------------------------
        // TEST F — CONTINUED BEARISH REGIME => NO REVERSAL
        // -------------------------------------------------------------

        [Fact]
        public void TestF_ContinuedBearishRegime_NoReversal()
        {
            var (engine, context, values) = CreateDefault();

            for (int i = 0; i < 6; i++)
            {
                Process(engine, context, values, i, Bearish, PriceBelowLine);
                Assert.False(values.Reversal.IsReversalBar);
                Assert.Null(values.Reversal.BarsSinceReversal);
                Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
            }
        }

        // -------------------------------------------------------------
        // TEST G — MULTIPLE PRICE CROSSES WITHOUT FLIPS
        // => ZERO REVERSAL EVENTS
        // Price whipsaws across the line four times while the regime
        // stays bullish throughout.
        // -------------------------------------------------------------

        [Fact]
        public void TestG_MultiplePriceCrosses_NoFlips_ZeroReversals()
        {
            var (engine, context, values) = CreateDefault();

            // above, below, above, below, above — four crosses,
            // regime bullish the whole time.
            double[] priceSide =
            {
                PriceAboveLine, PriceBelowLine, PriceAboveLine,
                PriceBelowLine, PriceAboveLine
            };

            int reversalEvents = 0;

            for (int i = 0; i < priceSide.Length; i++)
            {
                Process(engine, context, values, i, Bullish, priceSide[i]);
                if (values.Reversal.IsReversalBar)
                    reversalEvents++;
            }

            Assert.Equal(0, reversalEvents);
            Assert.Null(values.Reversal.BarsSinceReversal);
            Assert.Equal(ReversalDirection.None, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // TEST H — MULTIPLE ACTUAL FLIPS
        // bullish -> bearish -> bullish -> bearish
        // => EXACTLY THREE REVERSALS WITH CORRECT DIRECTIONS
        // -------------------------------------------------------------

        [Fact]
        public void TestH_MultipleActualFlips_ExactlyThreeReversals()
        {
            var (engine, context, values) = CreateDefault();

            // regime: +1, +1, -1, -1, +1, +1, -1
            double[] regime =
            {
                Bullish, Bullish, Bearish, Bearish,
                Bullish, Bullish, Bearish
            };

            var observed = new List<ReversalDirection>();

            for (int i = 0; i < regime.Length; i++)
            {
                Process(engine, context, values, i, regime[i], regime[i]);
                if (values.Reversal.IsReversalBar)
                    observed.Add(values.Reversal.Direction);
            }

            // Flip 1 at bar 2: bullish -> bearish = Down.
            // Flip 2 at bar 4: bearish -> bullish = Up.
            // Flip 3 at bar 6: bullish -> bearish = Down.
            Assert.Equal(3, observed.Count);
            Assert.Equal(ReversalDirection.Down, observed[0]);
            Assert.Equal(ReversalDirection.Up, observed[1]);
            Assert.Equal(ReversalDirection.Down, observed[2]);

            // Final state: most recent reversal was Down.
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
            Assert.Equal(0, values.Reversal.BarsSinceReversal);
        }

        // -------------------------------------------------------------
        // FLAT-REGIME BOUNDARY — pos 0 (flat) is BELOW; a transition
        // into/out of flat is a regime change under the canonical
        // sign-change rule (existing valid-state rule preserved).
        // -------------------------------------------------------------

        [Fact]
        public void FlatRegime_Transition_IsAReversal_PerExistingRule()
        {
            var (engine, context, values) = CreateDefault();

            Process(engine, context, values, 0, Bullish, PriceAboveLine);
            Process(engine, context, values, 1, 0.0, PriceBelowLine); // flat => BELOW

            Assert.True(values.Reversal.IsReversalBar);
            Assert.Equal(ReversalDirection.Down, values.Reversal.Direction);
        }

        // -------------------------------------------------------------
        // PIPELINE EQUIVALENCE — the DEFAULT pipeline configuration
        // must behave bar-for-bar identically to an explicit
        // TrailingStopPosition configuration on real data, and every
        // reversal bar must be a Regime flip bar (never a mere
        // price-vs-reference cross).
        // -------------------------------------------------------------

        [Fact]
        public void DefaultPipeline_IsRegimeFlip_OnRealData()
        {
            string csvPath = @"TestData\EURUSD_M1_10000.csv";
            if (!System.IO.File.Exists(csvPath))
            {
                csvPath =
                    @"D:\Software\Distance\Tests\TestData\EURUSD_M1_10000.csv";
            }

            const int atrPeriod = 16;
            const double atrMult = 5.1;
            const int smoothLength = 100;

            // Engine 1: DEFAULT options (no ReversalMode specified).
            var md1 = new CsvMarketData(csvPath);
            var engine1 = new ResearchFeatureEngineBuilder(
                new EngineConfiguration(
                    marketData: md1,
                    values: new EngineValues(),
                    referenceSource: new ATRSmoothReferenceSource(
                        new ATRSmoothConfiguration(
                            atrPeriod, atrMult, smoothLength)),
                    scaleModel: new ATRScaleModel(14),
                    normalizationModel: new ScaleNormalizationModel(),
                    statisticModels: new List<IStatisticModel>
                    {
                        new MeanModel()
                    },
                    options: new EngineOptions
                    {
                        StatisticsWindowSize = 252
                    })).Build();

            // Engine 2: explicit canonical mode.
            var md2 = new CsvMarketData(csvPath);
            var engine2 = new ResearchFeatureEngineBuilder(
                new EngineConfiguration(
                    marketData: md2,
                    values: new EngineValues(),
                    referenceSource: new ATRSmoothReferenceSource(
                        new ATRSmoothConfiguration(
                            atrPeriod, atrMult, smoothLength)),
                    scaleModel: new ATRScaleModel(14),
                    normalizationModel: new ScaleNormalizationModel(),
                    statisticModels: new List<IStatisticModel>
                    {
                        new MeanModel()
                    },
                    options: new EngineOptions
                    {
                        StatisticsWindowSize = 252,
                        ReversalMode = ReversalMode.TrailingStopPosition
                    })).Build();

            int bars = 0;
            int flips = 0;
            int crossesWithoutFlip = 0;
            double prevPos = 0.0;
            bool prevAbove = false;
            bool hasPrev = false;

            int idx = 0;
            while (md1.MoveNext())
            {
                engine1.Update();
                md2.MoveNext();
                engine2.Update();

                // Bar-for-bar identity between default and explicit
                // canonical mode.
                Assert.Equal(
                    engine2.Values.Reversal.BarsSinceReversal,
                    engine1.Values.Reversal.BarsSinceReversal);
                Assert.Equal(
                    engine2.Values.Reversal.Direction,
                    engine1.Values.Reversal.Direction);
                Assert.Equal(
                    engine2.Values.Reversal.IsReversalBar,
                    engine1.Values.Reversal.IsReversalBar);

                double pos = engine1.Values.Reference.Regime;
                double close = md1.Close[idx];
                double reference = engine1.Values.Reference.Price;
                bool above = close >= reference;

                bool posFlip = hasPrev && pos != prevPos;
                bool priceCross = hasPrev && above != prevAbove;

                if (posFlip)
                    flips++;

                if (priceCross && !posFlip)
                {
                    crossesWithoutFlip++;

                    // The adversarial core: a price cross without a
                    // regime flip must NEVER be a reversal.
                    Assert.False(engine1.Values.Reversal.IsReversalBar);
                }

                if (engine1.Values.Reversal.IsReversalBar)
                {
                    // Every reversal bar must be a regime-flip bar.
                    Assert.True(posFlip);
                }

                prevPos = pos;
                prevAbove = above;
                hasPrev = true;
                bars++;
                idx++;
            }

            Assert.Equal(10_000, bars);
            Assert.True(flips > 0,
                "Expected at least one regime flip in 10k bars.");
            Assert.True(crossesWithoutFlip > 0,
                "Expected at least one price cross without a regime " +
                "flip in 10k bars — this is the exact scenario that " +
                "must NOT produce a reversal.");
        }
    }
}

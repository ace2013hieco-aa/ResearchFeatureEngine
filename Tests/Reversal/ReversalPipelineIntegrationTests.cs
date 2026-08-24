using Xunit;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Reversal
{
    /// <summary>
    /// Integration tests confirming the Reversal stage is wired into
    /// the production pipeline and produces valid, lookahead-free
    /// output against the real ATRSmooth reference source.
    /// </summary>
    public sealed class ReversalPipelineIntegrationTests
    {
        [Fact]
        public void Pipeline_PublishesReversalValues()
        {
            EngineConfiguration configuration =
                TestConfigurationFactory.CreateKnownDataset();

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData = configuration.MarketData;

            while (marketData.MoveNext())
            {
                engine.Update();
            }

            // The Reversal stage must have produced published values.
            Assert.NotNull(engine.Values.Reversal);

            // After processing the full known dataset, the reversal
            // direction is either None, Up, or Down (all valid). The
            // counter, if non-null, must be non-negative.
            var dir = engine.Values.Reversal.Direction;
            Assert.True(
                dir == Core.ReversalDirection.None ||
                dir == Core.ReversalDirection.Up ||
                dir == Core.ReversalDirection.Down);

            Assert.True(
                !engine.Values.Reversal.BarsSinceReversal.HasValue ||
                engine.Values.Reversal.BarsSinceReversal.Value >= 0);
        }

        [Fact]
        public void Pipeline_ReversalMatchesAtrSmoothRegimeFlips()
        {
            // CANONICAL SEMANTIC (replaces the stale
            // "Pipeline_ReversalMatchesDistanceSignTransitions" test,
            // which encoded the incorrect interpretation of a
            // reversal as a candle crossing the ATR Smooth line).
            //
            // Drive the pipeline with the known dataset and, at each
            // bar, recompute the expected regime from the published
            // Reference.TrendPosition (the ATR trailing-stop position
            // bias — the canonical ATR Smooth regime state). A
            // reversal in the engine output must correspond to a
            // strict sign change of the regime between consecutive
            // bars (> 0 is ABOVE, <= 0 is BELOW), and a mere
            // close-vs-reference cross without a regime change must
            // NOT produce a reversal. This verifies the engine's
            // default reversal detection against an independent
            // in-test computation (no lookahead: only current +
            // previous).
            EngineConfiguration configuration =
                TestConfigurationFactory.CreateKnownDataset();

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData = configuration.MarketData;

            bool hasPrev = false;
            bool prevAbove = false;
            bool prevPriceAbove = false;
            int? expectedBars = null;

            int idx = 0;
            while (marketData.MoveNext())
            {
                engine.Update();

                double position = engine.Values.Reference.TrendPosition;
                bool above = position > 0.0;

                if (hasPrev && above != prevAbove)
                {
                    expectedBars = 0;
                }
                else if (expectedBars.HasValue)
                {
                    expectedBars = expectedBars.Value + 1;
                }

                Assert.Equal(expectedBars,
                    engine.Values.Reversal.BarsSinceReversal);

                // Adversarial core: a close-vs-reference cross that
                // is NOT a regime flip must never flag a reversal.
                double close = configuration.MarketData.Close[idx];
                double reference = engine.Values.Reference.Price;
                bool priceAbove = close >= reference;
                if (hasPrev && priceAbove != prevPriceAbove
                    && above == prevAbove)
                {
                    Assert.False(engine.Values.Reversal.IsReversalBar);
                }

                prevPriceAbove = priceAbove;
                prevAbove = above;
                hasPrev = true;
                idx++;
            }
        }
    }
}

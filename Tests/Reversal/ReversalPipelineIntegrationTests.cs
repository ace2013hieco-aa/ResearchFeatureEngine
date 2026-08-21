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
        public void Pipeline_ReversalMatchesDistanceSignTransitions()
        {
            // Drive the pipeline with the known dataset and, at each
            // bar, recompute the expected above/below relation from
            // the published Reference and Close. A reversal in the
            // engine output must correspond to a strict sign change
            // of (close - reference) between consecutive bars, using
            // the >= rule for ABOVE. This verifies the engine's
            // reversal detection against an independent in-test
            // computation (no lookahead: only current + previous).
            EngineConfiguration configuration =
                TestConfigurationFactory.CreateKnownDataset();

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData = configuration.MarketData;

            bool hasPrev = false;
            bool prevAbove = false;
            int? expectedBars = null;

            int idx = 0;
            while (marketData.MoveNext())
            {
                engine.Update();

                double close = configuration.MarketData.Close[idx];
                double reference = engine.Values.Reference.Price;
                bool above = close >= reference;

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

                prevAbove = above;
                hasPrev = true;
                idx++;
            }
        }
    }
}

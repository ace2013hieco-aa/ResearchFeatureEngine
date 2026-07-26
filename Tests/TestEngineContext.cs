using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;

namespace ResearchFeatureEngine.Tests
{
    /// <summary>
    /// Provides a convenient factory for creating <see cref="EngineContext"/>
    /// instances with pre-initialized test doubles for unit testing.
    /// </summary>
    internal static class TestEngineContext
    {
        /// <summary>
        /// Creates a new <see cref="EngineContext"/> with the given
        /// close price data and an empty <see cref="EngineValues"/>.
        /// </summary>
        /// <param name="closePrices">
        /// The close price series to use. If not provided, defaults
        /// to an empty series.
        /// </param>
        /// <returns>
        /// A new <see cref="EngineContext"/> suitable for unit testing.
        /// </returns>
        public static EngineContext Create(params double[] closePrices)
        {
            var marketData = new TestMarketData(closePrices);
            var values = new EngineValues();

            return new EngineContext(marketData, values);
        }
    }
}

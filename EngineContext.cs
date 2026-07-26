using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Core.Engine
{
    /// <summary>
    /// Represents the shared runtime context for the
    /// Research Feature Engine.
    ///
    /// The context provides all engines with access to the
    /// common execution environment while remaining independent
    /// of any platform-specific implementation.
    /// </summary>
    public sealed class EngineContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EngineContext"/> class.
        /// </summary>
        /// <param name="marketData">
        /// Platform-independent market data provider.
        /// </param>
        /// <param name="values">
        /// Shared runtime value storage.
        /// </param>
        public EngineContext(
            IMarketData marketData,
            EngineValues values)
        {
            MarketData = marketData;
            Values = values;
            CurrentIndex = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EngineContext"/> class
        /// with no market data or values set. Intended for testing convenience.
        /// </summary>
        public EngineContext()
        {
            CurrentIndex = 0;
        }

        /// <summary>
        /// Gets the shared market data provider.
        /// </summary>
        public IMarketData? MarketData { get; }

        /// <summary>
        /// Gets the shared runtime values.
        /// </summary>
        public EngineValues? Values { get; }

        /// <summary>
        /// Gets the current processing index.
        /// </summary>
        public int CurrentIndex { get; private set; }

        /// <summary>
        /// Gets the current processing index.
        /// Alias for <see cref="CurrentIndex"/>.
        /// </summary>
        public int Index => CurrentIndex;

        /// <summary>
        /// Updates the current processing index.
        /// </summary>
        /// <param name="index">
        /// Zero-based processing index.
        /// </param>
        public void SetIndex(int index)
        {
            CurrentIndex = index;
        }

        /// <summary>
        /// Resets the runtime context.
        /// </summary>
        public void Reset()
        {
            CurrentIndex = 0;
        }
    }
}

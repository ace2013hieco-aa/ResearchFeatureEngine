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
    ///
    /// Both <see cref="MarketData"/> and <see cref="Values"/> are
    /// non-null in normal operation. A parameterless constructor
    /// is retained solely for targeted tests that exercise the
    /// null-market-data guard in <see cref="Reference.ReferenceSourceBase"/>;
    /// it leaves <see cref="MarketData"/> null on purpose, which is
    /// why <see cref="MarketData"/> is declared nullable. Production
    /// composition (<see cref="Composition.ResearchFeatureEngineBuilder"/>)
    /// always supplies a non-null market data adapter and a non-null
    /// values container, so engine code dereferences both directly
    /// without null-forgiving operators.
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
        /// with a fresh <see cref="EngineValues"/> and no market data.
        /// Intended only for tests that exercise the null-market-data
        /// guard paths; production code must use the
        /// <see cref="EngineContext(IMarketData, EngineValues)"/>
        /// constructor.
        /// </summary>
        public EngineContext()
        {
            Values = new EngineValues();
            CurrentIndex = 0;
        }

        /// <summary>
        /// Gets the shared market data provider.
        /// Null only when constructed via the parameterless
        /// constructor for null-guard testing.
        /// </summary>
        public IMarketData? MarketData { get; }

        /// <summary>
        /// Gets the shared runtime values. Always non-null.
        /// </summary>
        public EngineValues Values { get; }

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

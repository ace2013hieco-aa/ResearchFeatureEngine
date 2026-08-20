using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Runtime;

namespace ResearchFeatureEngine.Reference
{
    /// <summary>
    /// Defines the contract for a market reference source.
    ///
    /// A reference source is a stateful, platform-independent component
    /// that produces a smoothed equilibrium price for each processing
    /// index. Implementations are responsible for their own internal
    /// state (ATR, rolling sums, trailing stop, etc.) and expose that
    /// state through <see cref="Runtime"/>.
    /// </summary>
    public interface IReferenceSource
    {
        /// <summary>
        /// Gets the immutable configuration of this source.
        /// </summary>
        ReferenceSourceConfiguration Configuration { get; }

        /// <summary>
        /// Gets the runtime state of this source.
        /// </summary>
        ReferenceRuntime Runtime { get; }

        /// <summary>
        /// Resets the source to its initial state.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Advances the source to the current processing index and
        /// returns the new reference price.
        /// </summary>
        /// <param name="context">
        /// Engine context providing market data and the current index.
        /// </param>
        /// <returns>
        /// The reference price for the current index.
        /// </returns>
        double Update(EngineContext context);

        /// <summary>
        /// Clears the source's per-cycle state.
        /// </summary>
        void Reset();
    }
}

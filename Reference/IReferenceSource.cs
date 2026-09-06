using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Runtime;

namespace ResearchFeatureEngine.Reference
{
    /// <summary>
    /// Defines the contract for a market reference source.
    ///
    /// A reference source is a stateful, platform-independent component
    /// that produces a source-defined scalar measurement level for each
    /// processing index. The scalar is the quantity against which the
    /// Distance stage measures signed price deviation; it is NOT
    /// universally an "equilibrium price". ATRSmooth2 publishes the
    /// average of its VWMA and ATR trailing stop; Darvas Box publishes
    /// the midpoint of its current box.
    ///
    /// The source additionally publishes a source-defined signed
    /// directional/regime state (<see cref="Regime"/>) consumed by the
    /// generic reversal detection. The semantics of that state are
    /// defined by each reference source (e.g. ATRSmooth2 trailing-stop
    /// position bias vs. Darvas above/inside/below box position) and are
    /// opaque to the engine.
    ///
    /// Implementations are responsible for their own internal state and
    /// expose it through <see cref="Runtime"/>.
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
        /// Gets the source-defined signed regime state for the most
        /// recently processed index. The semantics are defined by the
        /// concrete source (see contract above); generic reversal
        /// detection treats a strict change of this value as a reversal.
        /// </summary>
        double Regime { get; }

        /// <summary>
        /// Resets the source to its initial state.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Advances the source to the current processing index and
        /// returns the new reference measurement level.
        /// </summary>
        /// <param name="context">
        /// Engine context providing market data and the current index.
        /// </param>
        /// <returns>
        /// The source-defined scalar measurement level for the current
        /// index (see contract above).
        /// </returns>
        double Update(EngineContext context);

        /// <summary>
        /// Clears the source's per-cycle state.
        /// </summary>
        void Reset();
    }
}

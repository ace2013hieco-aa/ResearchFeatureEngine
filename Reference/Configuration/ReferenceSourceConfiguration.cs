using System;

namespace ResearchFeatureEngine.Reference.Configuration
{
    /// <summary>
    /// Immutable configuration for a concrete <see cref="IReferenceSource"/>.
    ///
    /// A reference source configuration holds the algorithm parameters that
    /// fully describe the reference computation but are constant for the
    /// lifetime of a given pipeline. Mutable per-cycle state lives in
    /// <see cref="Reference.Runtime.ReferenceRuntime"/>.
    /// </summary>
    public abstract class ReferenceSourceConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ReferenceSourceConfiguration"/> class.
        /// </summary>
        protected ReferenceSourceConfiguration()
        {
        }

        /// <summary>
        /// Gets a human-readable identifier for the source algorithm.
        /// </summary>
        public abstract string SourceName { get; }
    }
}

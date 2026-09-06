namespace ResearchFeatureEngine.Reference.Runtime
{
    /// <summary>
    /// Per-cycle mutable state owned by an <see cref="IReferenceSource"/>.
    ///
    /// The runtime is mutated exclusively by the source. The engine reads
    /// it and publishes the validated reference price into
    /// <see cref="Core.EngineValues.Reference"/>. This keeps a single
    /// owner for the source's mutable state while still allowing the
    /// engine to remain agnostic of the specific reference algorithm.
    /// </summary>
    public sealed class ReferenceRuntime
    {
        /// <summary>
        /// Gets the last processed index, or -1 if no bar has been
        /// processed since construction or the most recent reset.
        /// </summary>
        public int CurrentIndex { get; internal set; } = -1;

        // ATRSmooth state
        /// <summary>
        /// Gets the EMA of the True Range over the ATR lookback.
        /// </summary>
        public double EmaTrueRange { get; internal set; }

        /// <summary>
        /// Gets the current ATR trailing stop value.
        /// </summary>
        public double TrailingStop { get; internal set; }

        /// <summary>
        /// Gets the current position state used by the trailing stop
        /// (1 = long bias, -1 = short bias, 0 = flat).
        /// </summary>
        public double Position { get; internal set; }

        /// <summary>
        /// Gets the rolling sum of close * volume over the VWMA window.
        /// </summary>
        public double SumPV { get; internal set; }

        /// <summary>
        /// Gets the rolling sum of volume over the VWMA window.
        /// </summary>
        public double SumV { get; internal set; }

        /// <summary>
        /// Gets the most recently published reference price.
        /// </summary>
        public double LastReference { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the runtime has been
        /// initialized for a new processing run.
        /// </summary>
        public bool IsInitialized { get; internal set; }

        /// <summary>
        /// Resets the runtime to its freshly-constructed state.
        /// </summary>
        public void Reset()
        {
            CurrentIndex = -1;
            EmaTrueRange = 0.0;
            TrailingStop = 0.0;
            Position = 0.0;
            SumPV = 0.0;
            SumV = 0.0;
            LastReference = 0.0;
            IsInitialized = false;
        }
    }
}

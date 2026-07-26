namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Represents the current execution state of a processing component.
    /// This class provides a lightweight snapshot of the component's
    /// lifecycle without recording execution history.
    /// </summary>
    public sealed class ExecutionTrace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionTrace"/> class.
        /// </summary>
        public ExecutionTrace()
        {
            Stage = ExecutionStage.Created;
        }

        /// <summary>
        /// Gets the current execution stage.
        /// </summary>
        public ExecutionStage Stage { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the component has completed initialization.
        /// </summary>
        public bool IsInitialized =>
            Stage == ExecutionStage.Ready ||
            Stage == ExecutionStage.Processing ||
            Stage == ExecutionStage.Resetting ||
            Stage == ExecutionStage.Completed;

        /// <summary>
        /// Gets a value indicating whether the component is currently processing.
        /// </summary>
        public bool IsProcessing =>
            Stage == ExecutionStage.Processing;

        /// <summary>
        /// Gets a value indicating whether the component has entered the faulted state.
        /// </summary>
        public bool HasFault =>
            Stage == ExecutionStage.Faulted;

        /// <summary>
        /// Updates the current execution stage.
        /// </summary>
        /// <param name="stage">The new execution stage.</param>
        public void SetStage(ExecutionStage stage)
        {
            Stage = stage;
        }

        /// <summary>
        /// Resets the execution trace to its initial state.
        /// </summary>
        public void Reset()
        {
            Stage = ExecutionStage.Created;
        }
    }
}

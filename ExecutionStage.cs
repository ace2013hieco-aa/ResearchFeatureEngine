namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Represents the execution lifecycle stage of the
    /// Research Feature Engine and its processing components.
    /// </summary>
    public enum ExecutionStage
    {
        /// <summary>
        /// Object has been created but not initialized.
        /// </summary>
        Created,

        /// <summary>
        /// Component is initializing internal state.
        /// </summary>
        Initializing,

        /// <summary>
        /// Component has been initialized and is ready to process.
        /// </summary>
        Ready,

        /// <summary>
        /// Component is actively processing market data.
        /// </summary>
        Processing,

        /// <summary>
        /// Component is resetting its internal runtime state.
        /// </summary>
        Resetting,

        /// <summary>
        /// Component has completed execution successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Component encountered an unrecoverable execution error.
        /// </summary>
        Faulted
    }
}

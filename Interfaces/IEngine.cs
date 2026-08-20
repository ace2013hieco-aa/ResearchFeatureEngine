using System;

namespace ResearchFeatureEngine.Interfaces
{
    /// <summary>
    /// Defines the lifecycle contract for every processing engine
    /// within the Research Feature Engine.
    /// </summary>
    public interface IEngine
    {
        /// <summary>
        /// Gets the concrete engine type.
        /// Used for diagnostics, verification and profiling.
        /// </summary>
        Type EngineType { get; }

        /// <summary>
        /// Initializes the engine before processing begins.
        /// Called once by the EnginePipeline.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Executes one processing cycle.
        /// Called once per pipeline update.
        /// </summary>
        void Update();

        /// <summary>
        /// Resets the engine to its initial state.
        /// </summary>
        void Reset();
    }
}

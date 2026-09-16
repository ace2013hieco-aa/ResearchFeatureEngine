using System;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Provides the common execution foundation for all processing engines.
    /// Implements the Template Method pattern by managing lifecycle state
    /// while delegating engine-specific behavior to derived classes.
    /// </summary>
    public abstract class EngineBase : IEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EngineBase"/> class.
        /// </summary>
        /// <param name="name">Engine name.</param>
        /// <param name="context">Shared execution context.</param>
        protected EngineBase(string name, EngineContext context)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? throw new ArgumentNullException(nameof(context));

            Trace = new ExecutionTrace();
        }

        /// <summary>
        /// Gets the engine name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the concrete engine type.
        /// Used for diagnostics, verification and profiling.
        /// </summary>
        public Type EngineType => GetType();

        /// <summary>
        /// Gets the shared execution context.
        /// </summary>
        protected EngineContext Context { get; }

        /// <summary>
        /// Gets the execution trace for this engine.
        /// </summary>
        public ExecutionTrace Trace { get; }

        /// <summary>
        /// Initializes the engine.
        /// </summary>
        public void Initialize()
        {
            Trace.SetStage(ExecutionStage.Initializing);

            OnInitialize();

            Trace.SetStage(ExecutionStage.Ready);
        }

        /// <summary>
        /// Executes one processing cycle.
        /// </summary>
        public virtual void Update()
        {
            Trace.SetStage(ExecutionStage.Processing);

            OnUpdate();

            Trace.SetStage(ExecutionStage.Ready);
        }

        /// <summary>
        /// Resets the engine.
        /// </summary>
        public virtual void Reset()
        {
            Trace.SetStage(ExecutionStage.Resetting);

            OnReset();

            Trace.SetStage(ExecutionStage.Ready);
        }

        /// <summary>
        /// Engine-specific initialization.
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Engine-specific processing.
        /// </summary>
        protected abstract void OnUpdate();

        /// <summary>
        /// Engine-specific reset.
        /// </summary>
        protected virtual void OnReset()
        {
        }
    }
}

using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Coordinates the deterministic execution of all registered engines.
    /// The pipeline owns the shared execution context and orchestrates the
    /// engine lifecycle without performing any computational work itself.
    /// </summary>
    public sealed class EnginePipeline
    {
        private readonly List<IEngine> _engines = new();

        private bool _initialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnginePipeline"/> class.
        /// </summary>
        /// <param name="context">
        /// Shared execution context.
        /// </param>
        public EnginePipeline(EngineContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Trace = new ExecutionTrace();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnginePipeline"/> class
        /// without an execution context. The context must be set before
        /// initialization.
        /// </summary>
        public EnginePipeline()
        {
            Trace = new ExecutionTrace();
        }

        /// <summary>
        /// Gets the shared execution context.
        /// </summary>
        public EngineContext? Context { get; private set; }

        /// <summary>
        /// Gets the execution trace for the pipeline.
        /// </summary>
        public ExecutionTrace Trace { get; }

        /// <summary>
        /// Registers an engine for execution.
        /// Registration is only permitted before initialization.
        /// </summary>
        /// <param name="engine">
        /// Engine to register.
        /// </param>
        public void Register(IEngine engine)
        {
            if (_initialized)
                throw new InvalidOperationException(
                    "Cannot register engines after initialization.");

            ArgumentNullException.ThrowIfNull(engine);

            _engines.Add(engine);
        }

        /// <summary>
        /// Registers an engine for execution.
        /// Alias for <see cref="Register"/>.
        /// </summary>
        /// <param name="engine">
        /// Engine to register.
        /// </param>
        public void Add(IEngine engine)
        {
            Register(engine);
        }

        /// <summary>
        /// Initializes every registered engine.
        /// </summary>
        public void Initialize()
        {
            Trace.SetStage(ExecutionStage.Initializing);

            foreach (var engine in _engines)
            {
                engine.Initialize();
            }

            _initialized = true;

            Trace.SetStage(ExecutionStage.Ready);
        }

        /// <summary>
        /// Executes one processing cycle.
        /// </summary>
        public void Update()
        {
            if (!_initialized)
                throw new InvalidOperationException(
                    "Pipeline has not been initialized.");

            Trace.SetStage(ExecutionStage.Processing);

            foreach (var engine in _engines)
            {
                engine.Update();
            }

            Trace.SetStage(ExecutionStage.Ready);
        }

        /// <summary>
        /// Resets the pipeline and every registered engine.
        /// </summary>
        public void Reset()
        {
            Trace.SetStage(ExecutionStage.Resetting);

            foreach (var engine in _engines)
            {
                engine.Reset();
            }

            Context?.Reset();

            Trace.SetStage(ExecutionStage.Ready);
        }
    }
}

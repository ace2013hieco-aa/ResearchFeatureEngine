using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;

namespace ResearchFeatureEngine
{
    /// <summary>
    /// Public entry point to the Research Feature Engine.
    /// </summary>
    public sealed class ResearchFeatureEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResearchFeatureEngine"/> class.
        /// </summary>
        /// <param name="context">
        /// Shared execution context.
        /// </param>
        /// <param name="pipeline">
        /// Engine pipeline responsible for processing updates.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a required dependency is null.
        /// </exception>
        public ResearchFeatureEngine(
            EngineContext context,
            EnginePipeline pipeline)
        {
            Context = context
                ?? throw new ArgumentNullException(nameof(context));

            Pipeline = pipeline
                ?? throw new ArgumentNullException(nameof(pipeline));
        }

        /// <summary>
        /// Gets the shared execution context.
        /// </summary>
        public EngineContext Context { get; }

        /// <summary>
        /// Gets the engine pipeline.
        /// </summary>
        public EnginePipeline Pipeline { get; }

        /// <summary>
        /// Gets the shared runtime values.
        /// </summary>
        public EngineValues Values => Context.Values;

        /// <summary>
        /// Executes one complete pipeline update.
        /// Advances the shared processing index after each cycle
        /// so that each successive call processes the next bar.
        /// </summary>
        public void Update()
        {
            Pipeline.Update();
            Context.SetIndex(Context.CurrentIndex + 1);
        }
    }
}

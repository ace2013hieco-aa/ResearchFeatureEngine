using System;
using System.Collections.Generic;

using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Composition
{
    /// <summary>
    /// Builds an <see cref="EnginePipeline"/> from an ordered collection
    /// of processing engines.
    /// </summary>
    public sealed class EnginePipelineBuilder
    {
        private readonly List<EngineBase> _engines = new();

        /// <summary>
        /// Adds an engine to the execution pipeline.
        /// </summary>
        /// <param name="engine">
        /// Engine to add.
        /// </param>
        /// <returns>
        /// The current builder.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="engine"/> is null.
        /// </exception>
        public EnginePipelineBuilder Add(EngineBase engine)
        {
            ArgumentNullException.ThrowIfNull(engine);

            _engines.Add(engine);

            return this;
        }

        /// <summary>
        /// Builds the engine pipeline.
        /// </summary>
        /// <returns>
        /// A fully configured <see cref="EnginePipeline"/>.
        /// </returns>
        public EnginePipeline Build()
        {
            var pipeline = new EnginePipeline();

            foreach (EngineBase engine in _engines)
            {
                pipeline.Add(engine);
            }

            return pipeline;
        }

    }
}

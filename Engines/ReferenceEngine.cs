using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Validation;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// Reference stage engine.
    ///
    /// Advances the configured <see cref="IReferenceSource"/> by one
    /// processing index, validates the produced reference price, and
    /// publishes it into <see cref="Core.EngineValues.Reference"/>.
    ///
    /// The engine is intentionally agnostic of the specific reference
    /// algorithm. The source owns its own runtime state
    /// (<see cref="Reference.Runtime.ReferenceRuntime"/>); the engine
    /// only validates the published value and writes it into the
    /// canonical runtime values container.
    /// </summary>
    public sealed class ReferenceEngine : EngineBase
    {
        private readonly IReferenceSource _referenceSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceEngine"/>
        /// class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="referenceSource">
        /// Source that produces the per-index reference price.
        /// </param>
        public ReferenceEngine(
            EngineContext context,
            IReferenceSource referenceSource)
            : base("ReferenceEngine", context)
        {
            _referenceSource = referenceSource
                ?? throw new ArgumentNullException(nameof(referenceSource));
        }

        /// <inheritdoc />
        protected override void OnInitialize()
        {
            _referenceSource.Initialize();
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            int index = Context.CurrentIndex;

            double price = _referenceSource.Update(Context);

            ReferenceSourceValidator.Validate(price, index);
            ReferenceValidator.Validate(price);

            Context.Values.Reference.Price = price;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            _referenceSource.Reset();
        }
    }
}

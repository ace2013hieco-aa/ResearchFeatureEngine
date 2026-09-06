using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Runtime;

namespace ResearchFeatureEngine.Reference
{
    /// <summary>
    /// Common base for all <see cref="IReferenceSource"/> implementations.
    ///
    /// Provides default lifecycle management (Initialize/Reset) and
    /// exposes the immutable <see cref="Configuration"/> together with
    /// the mutable <see cref="Runtime"/>. Derived classes implement
    /// the actual update logic by overriding
    /// <see cref="ComputeReference"/>.
    /// </summary>
    public abstract class ReferenceSourceBase : IReferenceSource
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ReferenceSourceBase"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Immutable configuration for the source.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="configuration"/> is null.
        /// </exception>
        protected ReferenceSourceBase(ReferenceSourceConfiguration configuration)
        {
            Configuration = configuration
                ?? throw new System.ArgumentNullException(nameof(configuration));

            Runtime = new ReferenceRuntime();
        }

        /// <inheritdoc />
        public ReferenceSourceConfiguration Configuration { get; }

        /// <inheritdoc />
        public ReferenceRuntime Runtime { get; }

        /// <inheritdoc />
        public abstract double Regime { get; }

        /// <inheritdoc />
        public void Initialize()
        {
            Reset();
            Runtime.IsInitialized = true;
        }

        /// <inheritdoc />
        public virtual void Reset()
        {
            Runtime.Reset();
        }

        /// <inheritdoc />
        public double Update(EngineContext context)
        {
            ArgumentGuard(context);

            int index = context.CurrentIndex;

            double reference = ComputeReference(context, index);

            Runtime.CurrentIndex = index;
            Runtime.LastReference = reference;

            return reference;
        }

        /// <summary>
        /// Source-specific computation for a given processing index.
        /// </summary>
        /// <param name="context">Engine context.</param>
        /// <param name="index">Zero-based processing index.</param>
        /// <returns>The reference price at the given index.</returns>
        protected abstract double ComputeReference(EngineContext context, int index);

        private static void ArgumentGuard(EngineContext context)
        {
            if (context is null)
                throw new System.ArgumentNullException(nameof(context));

            if (context.MarketData is null)
                throw new System.InvalidOperationException(
                    "Reference source requires a non-null market data adapter.");
        }
    }
}

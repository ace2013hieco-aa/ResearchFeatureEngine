
using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// Computes and publishes the Distance runtime values.
    /// </summary>
    public sealed class DistanceEngine : EngineBase
    {
        private readonly IDistanceModel _distanceModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistanceEngine"/> class.
        /// </summary>
        /// <param name="context">
        /// Shared engine context.
        /// </param>
        /// <param name="distanceModel">
        /// Distance computation model.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a required dependency is null.
        /// </exception>
        public DistanceEngine(
            EngineContext context,
            IDistanceModel distanceModel)
            : base("DistanceEngine", context)
        {
            _distanceModel = distanceModel
                ?? throw new ArgumentNullException(nameof(distanceModel));
        }

        protected override void OnUpdate()
        {
            int index = Context.CurrentIndex;

            double directional =
                _distanceModel.Compute(
                    index,
                    Context.Values.Reference.Price);

            double absolute = Math.Abs(directional);

            DistanceValidator.Validate(directional, absolute);

            Context.Values.Distance.DirectionalExtension = directional;
            Context.Values.Distance.AbsoluteExtension = absolute;
        }
    }
}


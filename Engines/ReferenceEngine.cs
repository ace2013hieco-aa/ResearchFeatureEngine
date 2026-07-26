using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;

namespace ResearchFeatureEngine.Engines
{
    public sealed class ReferenceEngine : EngineBase
    {
        private readonly IReferenceModel _referenceModel;

        public ReferenceEngine(
            EngineContext context,
            IReferenceModel referenceModel)
            : base("ReferenceEngine", context)
        {
            _referenceModel = referenceModel
                ?? throw new ArgumentNullException(nameof(referenceModel));
        }

        protected override void OnUpdate()
        {
            double price = _referenceModel.Compute(Context.CurrentIndex);
            ReferenceValidator.Validate(price);
            Context.Values.Reference.Price = price;

            // Future:
            //
            // Context.Values.Reference.Slope =
            //     _referenceSlopeModel.Compute(Context.CurrentIndex);
            //
            // Context.Values.Reference.Direction =
            //     _referenceDirectionModel.Compute(Context.CurrentIndex);
        }
    }
}

using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Scale.Validation;

namespace ResearchFeatureEngine.Scale
{
    public sealed class ScaleEngine : EngineBase
    {
        private readonly IScaleModel _scaleModel;

        public ScaleEngine(
            EngineContext context,
            IScaleModel scaleModel)
            : base(nameof(ScaleEngine), context)
        {
            _scaleModel = scaleModel ?? throw new ArgumentNullException(nameof(scaleModel));
        }

        protected override void OnUpdate()
        {
            double scale = _scaleModel.Compute(Context);

            ScaleValidator.Validate(scale);

            Context.Values.Scale.Scale = scale;
        }
    }
}


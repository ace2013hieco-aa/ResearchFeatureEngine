using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Normalization.Validation;

namespace ResearchFeatureEngine.Normalization
{
    public sealed class NormalizationEngine : EngineBase
    {
        private readonly INormalizationModel _model;

        public NormalizationEngine(
            EngineContext context,
            INormalizationModel model)
            : base(nameof(NormalizationEngine), context)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        protected override void OnUpdate()
        {
            var input = BuildInput();
            double normalized = _model.Compute(input);
            NormalizationValidator.Validate(normalized);
            Context.Values.Normalization.NormalizedMeasurement = normalized;
        }

        private NormalizationInput BuildInput()
        {
            return new NormalizationInput(
                Measurement: Context.Values.Distance.AbsoluteExtension,
                CharacteristicScale: Context.Values.Scale.Scale);
        }
    }
}

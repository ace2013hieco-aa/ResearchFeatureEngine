using ResearchFeatureEngine.Normalization.Models;

namespace ResearchFeatureEngine.Normalization.Models
{
    /// <summary>
    /// Normalizes a measurement using the supplied characteristic scale.
    ///
    /// NormalizedMeasurement = Measurement / CharacteristicScale
    /// </summary>
    public sealed class ScaleNormalizationModel : INormalizationModel
    {
        public double Compute(NormalizationInput input)
        {
            return input.Measurement / input.CharacteristicScale;
        }
    }
}


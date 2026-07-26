namespace ResearchFeatureEngine.Normalization.Models
{
    /// <summary>
    /// Represents the input required to perform a normalization.
    /// </summary>
    public readonly record struct NormalizationInput(
        double Measurement,
        double CharacteristicScale);
}


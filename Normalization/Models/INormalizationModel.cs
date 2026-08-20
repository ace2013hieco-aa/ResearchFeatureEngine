namespace ResearchFeatureEngine.Normalization.Models
{
    /// <summary>
    /// Defines the contract for all normalization models.
    /// </summary>
    public interface INormalizationModel
    {
        /// <summary>
        /// Transforms a measurement into a dimensionless quantity.
        /// </summary>
        /// <param name="input">
        /// The normalization input.
        /// </param>
        /// <returns>
        /// A normalized (dimensionless) measurement.
        /// </returns>
        double Compute(NormalizationInput input);
    }
}

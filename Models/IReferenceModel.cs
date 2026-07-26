namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Defines the contract for all reference models.
    ///
    /// A reference model estimates the current market equilibrium
    /// (reference price) for a specified processing index.
    /// </summary>
    public interface IReferenceModel
    {
        /// <summary>
        /// Computes the reference price for the specified index.
        /// </summary>
        /// <param name="index">
        /// Current processing index.
        /// </param>
        /// <returns>
        /// The computed reference price.
        /// </returns>
        double Compute(int index);
    }
}

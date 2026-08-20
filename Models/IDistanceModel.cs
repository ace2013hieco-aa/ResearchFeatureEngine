namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Defines the contract for all distance models.
    ///
    /// A distance model computes the signed displacement
    /// between the current market price and a supplied
    /// reference price.
    /// </summary>
    public interface IDistanceModel
    {
        /// <summary>
        /// Computes the signed market extension for the
        /// specified processing index.
        /// </summary>
        /// <param name="index">
        /// Current processing index.
        /// </param>
        /// <param name="referencePrice">
        /// Previously computed reference price.
        /// </param>
        /// <returns>
        /// Signed distance between the market price and
        /// the supplied reference price.
        /// </returns>
        double Compute(int index, double referencePrice);
    }
}


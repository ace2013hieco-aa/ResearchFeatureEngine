using ResearchFeatureEngine.Core.Engine;

namespace ResearchFeatureEngine.Scale.Models
{
    /// <summary>
    /// Defines the contract for all characteristic scale estimation models.
    /// </summary>
    public interface IScaleModel
    {
        /// <summary>
        /// Computes the characteristic scale for the current processing cycle.
        /// </summary>
        /// <param name="context">
        /// The engine context providing the information required to estimate
        /// the current characteristic scale.
        /// </param>
        /// <returns>
        /// A positive, finite characteristic scale estimate.
        /// </returns>
        double Compute(EngineContext context);
    }
}


using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Defines the contract for a statistic model that computes
    /// a specific statistical measure over a set of values.
    /// </summary>
    public interface IStatisticModel
    {
        /// <summary>
        /// Gets the type of statistic this model computes.
        /// </summary>
        StatisticType Type { get; }

        /// <summary>
        /// Gets the minimum number of observations required
        /// to compute this statistic.
        /// </summary>
        int MinimumObservationCount { get; }

        /// <summary>
        /// Computes the statistic for the given input.
        /// </summary>
        /// <param name="input">The input values and parameters.</param>
        /// <returns>The computed statistic value.</returns>
        double Compute(in StatisticsInput input);
    }
}

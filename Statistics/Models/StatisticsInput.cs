using System;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Represents the input required to compute a statistic.
    /// The observations are provided as a <see cref="ReadOnlyMemory{T}"/>
    /// to allow both array and slice-based usage without allocation.
    /// </summary>
    public readonly record struct StatisticsInput(
        ReadOnlyMemory<double> Observations);
}

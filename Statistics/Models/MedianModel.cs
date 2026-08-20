using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the statistical median of the supplied observations.
    /// </summary>
    public sealed class MedianModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.Median;

        /// <inheritdoc />
        public int MinimumObservationCount => 1;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cannot compute the median of an empty observation set.");
            }

            // Create a working copy to preserve immutability.
            double[] values = observations.ToArray();

            Array.Sort(values);

            int count = values.Length;
            int middle = count / 2;

            // Odd number of observations.
            if ((count & 1) == 1)
            {
                return values[middle];
            }

            // Even number of observations.
            return (values[middle - 1] + values[middle]) * 0.5;
        }
    }
}

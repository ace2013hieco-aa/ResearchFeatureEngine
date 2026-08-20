using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the arithmetic mean of the supplied observations.
    /// </summary>
    public sealed class MeanModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.Mean;

        /// <inheritdoc />
        public int MinimumObservationCount => 1;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length == 0)
                throw new InvalidOperationException(
                    "Cannot compute the mean of an empty observation set.");

            double sum = 0.0;

            foreach (double value in observations)
            {
                sum += value;
            }

            return sum / observations.Length;
        }
    }
}

using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the minimum value of the supplied observations.
    /// </summary>
    public sealed class MinimumModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.Minimum;

        /// <inheritdoc />
        public int MinimumObservationCount => 1;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length == 0)
            {
                throw new InvalidOperationException(
                    "Minimum requires at least one observation.");
            }

            double minimum = observations[0];

            for (int i = 1; i < observations.Length; i++)
            {
                if (observations[i] < minimum)
                {
                    minimum = observations[i];
                }
            }

            return minimum;
        }
    }
}

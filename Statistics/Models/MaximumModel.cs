using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the maximum value of the supplied observations.
    /// </summary>
    public sealed class MaximumModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.Maximum;

        /// <inheritdoc />
        public int MinimumObservationCount => 1;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length == 0)
            {
                throw new InvalidOperationException(
                    "Maximum requires at least one observation.");
            }

            double maximum = observations[0];

            for (int i = 1; i < observations.Length; i++)
            {
                if (observations[i] > maximum)
                {
                    maximum = observations[i];
                }
            }

            return maximum;
        }
    }
}

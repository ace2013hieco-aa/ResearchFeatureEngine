using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the sample standard deviation of the supplied observations
    /// using Welford's numerically stable online algorithm.
    /// </summary>
    public sealed class StandardDeviationModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.StandardDeviation;

        /// <inheritdoc />
        public int MinimumObservationCount => 2;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length < 2)
            {
                throw new InvalidOperationException(
                    "Standard deviation requires at least two observations.");
            }

            int count = 0;
            double mean = 0.0;
            double m2 = 0.0;

            foreach (double value in observations)
            {
                count++;

                double delta = value - mean;
                mean += delta / count;

                double delta2 = value - mean;
                m2 += delta * delta2;
            }

            // Sample variance (Bessel's correction)
            double variance = m2 / (count - 1);

            // Sample standard deviation
            return Math.Sqrt(variance);
        }
    }
}

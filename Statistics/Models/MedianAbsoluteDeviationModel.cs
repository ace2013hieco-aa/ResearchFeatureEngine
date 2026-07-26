using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the Median Absolute Deviation (MAD) of the supplied observations.
    /// </summary>
    public sealed class MedianAbsoluteDeviationModel : IStatisticModel
    {
        private readonly MedianModel _medianModel = new();

        /// <inheritdoc />
        public StatisticType Type => StatisticType.MedianAbsoluteDeviation;

        /// <inheritdoc />
        public int MinimumObservationCount => 1;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length == 0)
            {
                throw new InvalidOperationException(
                    "Median Absolute Deviation requires at least one observation.");
            }

            // Step 1: Compute the median of the observations.
            double median = _medianModel.Compute(input);

            // Step 2: Compute absolute deviations.
            double[] deviations = new double[observations.Length];

            for (int i = 0; i < observations.Length; i++)
            {
                deviations[i] = Math.Abs(observations[i] - median);
            }

            // Step 3: Compute the median of the deviations.
            StatisticsInput deviationInput = new StatisticsInput(deviations);

            return _medianModel.Compute(deviationInput);
        }
    }
}

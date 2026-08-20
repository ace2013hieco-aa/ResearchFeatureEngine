using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Statistics.Validation
{
    /// <summary>
    /// Validates the statistical execution environment before
    /// any statistical models are executed.
    /// </summary>
    public sealed class StatisticsValidator
    {
        /// <summary>
        /// Validates the supplied statistical input.
        /// </summary>
        /// <param name="input">
        /// Statistical input.
        /// </param>
        /// <param name="models">
        /// Registered statistic models.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input or models are null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when validation fails.
        /// </exception>
        public void Validate(
            StatisticsInput input,
            IEnumerable<IStatisticModel> models)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(models);

            ReadOnlySpan<double> observations = input.Observations.Span;

            //--------------------------------------------------
            // Observation Count
            //--------------------------------------------------

            if (observations.Length == 0)
            {
                throw new InvalidOperationException(
                    "Statistics requires at least one observation.");
            }

            //--------------------------------------------------
            // Observation Values
            //--------------------------------------------------

            foreach (double value in observations)
            {
                if (double.IsNaN(value))
                {
                    throw new InvalidOperationException(
                        "Observation contains NaN.");
                }

                if (double.IsInfinity(value))
                {
                    throw new InvalidOperationException(
                        "Observation contains Infinity.");
                }
            }
        }
    }
}

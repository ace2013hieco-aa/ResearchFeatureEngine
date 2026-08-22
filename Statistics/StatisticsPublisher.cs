using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Statistics.Runtime;

namespace ResearchFeatureEngine.Statistics
{
    /// <summary>
    /// Publishes computed statistical values into the runtime values.
    /// </summary>
    public sealed class StatisticsPublisher
    {
        private readonly StatisticsRuntimeValues _runtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatisticsPublisher"/> class.
        /// </summary>
        /// <param name="runtime">
        /// Statistics runtime values.
        /// </param>
        public StatisticsPublisher(StatisticsRuntimeValues runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            _runtime = runtime;
        }

        /// <summary>
        /// Publishes a statistic into the runtime values.
        /// </summary>
        /// <param name="type">
        /// Statistic type.
        /// </param>
        /// <param name="value">
        /// Computed statistic.
        /// </param>
        public void Publish(
            StatisticType type,
            double value)
        {
            switch (type)
            {
                //------------------------------------------
                // Location
                //------------------------------------------

                case StatisticType.Mean:
                    _runtime.Location.Mean = value;
                    break;

                case StatisticType.Median:
                    _runtime.Location.Median = value;
                    break;

                //------------------------------------------
                // Dispersion
                //------------------------------------------

                case StatisticType.Variance:
                    _runtime.Dispersion.Variance = value;
                    break;

                case StatisticType.StandardDeviation:
                    _runtime.Dispersion.StandardDeviation = value;
                    break;

                case StatisticType.MedianAbsoluteDeviation:
                    _runtime.Dispersion.MedianAbsoluteDeviation = value;
                    break;

                //------------------------------------------
                // Range
                //------------------------------------------

                case StatisticType.Minimum:
                    _runtime.Range.Minimum = value;
                    break;

                case StatisticType.Maximum:
                    _runtime.Range.Maximum = value;
                    break;

                case StatisticType.Range:
                    _runtime.Range.Range = value;
                    break;

                //------------------------------------------
                // Shape
                //------------------------------------------

                case StatisticType.Skewness:
                    _runtime.Shape.Skewness = value;
                    break;

                case StatisticType.Kurtosis:
                    _runtime.Shape.Kurtosis = value;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(type),
                        type,
                        "Unsupported statistic type.");
            }
        }
    }
}

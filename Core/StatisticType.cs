namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Defines the types of statistics that can be computed
    /// by a statistic model.
    /// </summary>
    public enum StatisticType
    {
        /// <summary>
        /// The arithmetic mean (average) of a set of values.
        /// </summary>
        Mean,

        /// <summary>
        /// The middle value separating the higher half from the lower half.
        /// </summary>
        Median,

        /// <summary>
        /// The average of the squared differences from the mean.
        /// </summary>
        Variance,

        /// <summary>
        /// The square root of the variance.
        /// </summary>
        StandardDeviation,

        /// <summary>
        /// The median of the absolute deviations from the median.
        /// </summary>
        MedianAbsoluteDeviation,

        /// <summary>
        /// The smallest value in a set.
        /// </summary>
        Minimum,

        /// <summary>
        /// The largest value in a set.
        /// </summary>
        Maximum,

        /// <summary>
        /// The difference between the maximum and minimum values.
        /// </summary>
        Range,

        /// <summary>
        /// The Fisher–Pearson bias-corrected sample skewness (G1).
        /// </summary>
        Skewness,

        /// <summary>
        /// The Fisher bias-corrected excess kurtosis (G2).
        /// </summary>
        Kurtosis
    }
}

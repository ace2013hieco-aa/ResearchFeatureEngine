namespace ResearchFeatureEngine.Statistics.Runtime
{
    /// <summary>
    /// Published runtime values for the Statistics stage.
    ///
    /// These values represent the official, validated output of the
    /// current processing cycle and are updated exclusively by
    /// StatisticsEngine.
    /// </summary>
    public sealed class StatisticsRuntimeValues
    {
        /// <summary>
        /// Gets the number of observations currently in the rolling window.
        /// </summary>
        public int ObservationCount { get; internal set; }

        /// <summary>
        /// Measures of central tendency.
        /// </summary>
        public StatisticsLocationRuntimeValues Location { get; }
            = new StatisticsLocationRuntimeValues();

        /// <summary>
        /// Measures of dispersion.
        /// </summary>
        public StatisticsDispersionRuntimeValues Dispersion { get; }
            = new StatisticsDispersionRuntimeValues();

        /// <summary>
        /// Measures describing the observed range.
        /// </summary>
        public StatisticsRangeRuntimeValues Range { get; }
            = new StatisticsRangeRuntimeValues();

        /// <summary>
        /// Measures describing the distribution shape.
        /// </summary>
        public StatisticsShapeRuntimeValues Shape { get; }
            = new StatisticsShapeRuntimeValues();
    }

    /// <summary>
    /// Runtime values for measures of central tendency.
    /// </summary>
    public sealed class StatisticsLocationRuntimeValues
    {
        public double Mean { get; internal set; }

        public double Median { get; internal set; }
    }

    /// <summary>
    /// Runtime values for measures of dispersion.
    /// </summary>
    public sealed class StatisticsDispersionRuntimeValues
    {
        public double Variance { get; internal set; }

        public double StandardDeviation { get; internal set; }

        public double MedianAbsoluteDeviation { get; internal set; }
    }

    /// <summary>
    /// Runtime values describing the observed range.
    /// </summary>
    public sealed class StatisticsRangeRuntimeValues
    {
        public double Minimum { get; internal set; }

        public double Maximum { get; internal set; }

        public double Range { get; internal set; }
    }

    /// <summary>
    /// Runtime values describing the distribution shape.
    /// </summary>
    public sealed class StatisticsShapeRuntimeValues
    {
        /// <summary>
        /// Gets the Fisher–Pearson bias-corrected sample skewness
        /// (G1) of the current observation window.
        /// </summary>
        public double Skewness { get; internal set; }

        /// <summary>
        /// Gets the Fisher bias-corrected excess kurtosis (G2) of
        /// the current observation window.
        /// </summary>
        public double Kurtosis { get; internal set; }
    }
}

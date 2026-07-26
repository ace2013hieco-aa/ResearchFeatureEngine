using ResearchFeatureEngine.Statistics.Runtime;

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Stores the runtime values produced by the engine pipeline.
    /// </summary>
    public sealed class EngineValues
    {
        public EngineValues()
        {
            Reference = new ReferenceRuntimeValues();
            Distance = new DistanceRuntimeValues();
            Scale = new ScaleRuntimeValues();
            Normalization = new NormalizationRuntimeValues();
            Statistics = new StatisticsRuntimeValues();
        }

        #region Reference

        /// <summary>
        /// Gets the reference runtime values.
        /// </summary>
        public ReferenceRuntimeValues Reference { get; }

        #endregion

        #region Distance

        /// <summary>
        /// Gets the distance runtime values.
        /// </summary>
        public DistanceRuntimeValues Distance { get; }

        #endregion

        #region Scale

        /// <summary>
        /// Gets the scale runtime values.
        /// </summary>
        public ScaleRuntimeValues Scale { get; }

        #endregion

        #region Normalization

        /// <summary>
        /// Gets the normalization runtime values.
        /// </summary>
        public NormalizationRuntimeValues Normalization { get; }

        #endregion

        #region Statistics

        /// <summary>
        /// Gets the statistics runtime values.
        /// </summary>
        public StatisticsRuntimeValues Statistics { get; }

        #endregion
    }

    /// <summary>
    /// Runtime values for the Reference stage.
    /// </summary>
    public sealed class ReferenceRuntimeValues
    {
        /// <summary>
        /// Gets the current market reference price.
        /// </summary>
        public double Price { get; internal set; }

        /// <summary>
        /// Gets the current slope of the market reference.
        /// </summary>
        public double Slope { get; internal set; }

        /// <summary>
        /// Gets the current direction of the market reference.
        /// </summary>
        public int Direction { get; internal set; }
    }

    /// <summary>
    /// Runtime values for the Distance stage.
    /// </summary>
    public sealed class DistanceRuntimeValues
    {
        /// <summary>
        /// Gets the signed distance between the current market price
        /// and the computed reference price.
        /// </summary>
        public double DirectionalExtension { get; internal set; }

        /// <summary>
        /// Gets the absolute distance between the current market price
        /// and the computed reference price.
        /// </summary>
        public double AbsoluteExtension { get; internal set; }
    }

    /// <summary>
    /// Runtime values for the Scale stage.
    /// </summary>
    public sealed class ScaleRuntimeValues
    {
        /// <summary>
        /// Gets the current characteristic scale.
        /// </summary>
        public double Scale { get; internal set; }
    }

    /// <summary>
    /// Runtime values for the Normalization stage.
    /// </summary>
    public sealed class NormalizationRuntimeValues
    {
        /// <summary>
        /// Gets the current normalized measurement.
        /// </summary>
        public double NormalizedMeasurement { get; internal set; }
    }
}

using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reversal.Runtime;
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
            DarvasBoxDistance = new DarvasBoxDistanceRuntimeValues();
            MeanDarvasClosingDistance = new MeanDarvasClosingDistanceRuntimeValues();
            MeanHmaAtrSmoothDistance = new MeanHmaAtrSmoothDistanceRuntimeValues();
            HmaPriceAtrSmoothAlignment = new HmaPriceAtrSmoothAlignmentRuntimeValues();
            HmaAtrSmoothSeparation = new HmaAtrSmoothSeparationRuntimeValues();
            HmaAtrSmoothRelativeClosePosition = new HmaAtrSmoothRelativeClosePositionRuntimeValues();
            Scale = new ScaleRuntimeValues();
            Normalization = new NormalizationRuntimeValues();
            Statistics = new StatisticsRuntimeValues();
            Reversal = new ReversalRuntimeValues();
            AtrSmoothRegimeSegment = new AtrSmoothRegimeSegmentRuntimeValues();
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

        #region Darvas Box Distance

        /// <summary>
        /// Gets the Darvas Box closing-distance runtime values
        /// (close vs. the outer boundary of the current Darvas box;
        /// NaN while no box is confirmed).
        /// </summary>
        public DarvasBoxDistanceRuntimeValues DarvasBoxDistance { get; }

        #endregion

        #region Mean Darvas Closing Distance

        /// <summary>
        /// Gets the mean Darvas closing distance runtime values
        /// (rolling mean of signed Darvas closing distances).
        /// </summary>
        public MeanDarvasClosingDistanceRuntimeValues MeanDarvasClosingDistance { get; }

        #endregion

        #region Mean HMA–ATRSmooth Distance

        /// <summary>
        /// Gets the mean HMA–ATRSmooth distance runtime values
        /// (rolling mean of signed HMA minus ATRSmooth distances).
        /// </summary>
        public MeanHmaAtrSmoothDistanceRuntimeValues MeanHmaAtrSmoothDistance { get; }

        #endregion

        #region HMA/Price vs ATRSmooth Alignment

        /// <summary>
        /// Gets the HMA/Price vs ATRSmooth alignment runtime values
        /// (current-bar alignment state).
        /// </summary>
        public HmaPriceAtrSmoothAlignmentRuntimeValues HmaPriceAtrSmoothAlignment { get; }

        #endregion

        #region HMA/ATRSmooth Separation (M11.1)

        /// <summary>
        /// Gets the HMA/ATRSmooth separation runtime values (M11.1):
        /// the current-bar normalized structural separation
        /// (HMA − ATRSmooth) / Scale(14). Published only by the
        /// HMA + ATRSmooth composite mode; the values remain at
        /// their unavailable (NaN) defaults in every other mode.
        /// </summary>
        public HmaAtrSmoothSeparationRuntimeValues HmaAtrSmoothSeparation { get; }

        #endregion

        #region HMA/ATRSmooth Relative Close Position (M11.1)

        /// <summary>
        /// Gets the HMA/ATRSmooth relative close position runtime
        /// values (M11.1): R = (Close − ATRSmooth) / (HMA −
        /// ATRSmooth), NaN on warm-up and the exact zero-denominator
        /// geometry. Published only by the HMA + ATRSmooth composite
        /// mode; the values remain at their unavailable (NaN)
        /// defaults in every other mode.
        /// </summary>
        public HmaAtrSmoothRelativeClosePositionRuntimeValues HmaAtrSmoothRelativeClosePosition { get; }

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

        #region Reversal

        /// <summary>
        /// Gets the reversal runtime values.
        /// </summary>
        public ReversalRuntimeValues Reversal { get; }

        #endregion

        #region ATRSmooth Regime Segment

        /// <summary>
        /// Gets the ATRSmooth regime segment runtime values (M9):
        /// canonical regime direction plus segment metadata —
        /// RegimeId, RegimeStartIndex, zero-based RegimeAge, and
        /// RegimeTransition. Published only by the ATRSmooth-based
        /// reference compositions; the values remain at their
        /// unavailable defaults in every other mode.
        /// </summary>
        public AtrSmoothRegimeSegmentRuntimeValues AtrSmoothRegimeSegment { get; }

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
        /// Gets the source-defined signed regime state published by the
        /// selected reference source and consumed by generic reversal
        /// detection. The semantics are defined by the reference source,
        /// not by the engine:
        /// <list type="bullet">
        /// <item><description>
        /// ATRSmooth2: +1 = bullish trailing-stop position,
        /// -1 = bearish trailing-stop position, 0 = initial/uncommitted.
        /// </description></item>
        /// <item><description>
        /// Darvas Box: +1 = close above the upper boundary,
        /// 0 = close inside the box (a real persistent state),
        /// -1 = close below the lower boundary.
        /// </description></item>
        /// </list>
        /// A strict change of this value between consecutive bars is a
        /// reversal for the selected reference.
        /// </summary>
        public double Regime { get; internal set; }
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

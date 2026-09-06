using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Composition
{
    /// <summary>
    /// Optional settings for the Research Feature Engine.
    /// </summary>
    public sealed class EngineOptions
    {
        /// <summary>
        /// Gets the default engine options.
        /// </summary>
        public static EngineOptions Default { get; } = new EngineOptions();

        /// <summary>
        /// Gets or sets the statistics window size.
        /// </summary>
        public int StatisticsWindowSize { get; set; } = 20;

        /// <summary>
        /// Gets or sets the mean Darvas closing distance window size.
        /// </summary>
        public int MeanDarvasWindowSize { get; set; } = 20;

        /// <summary>
        /// Gets or sets the quantity whose rolling statistics are
        /// computed by the Statistics stage (raw closes, simple
        /// returns, or log returns).
        /// The default is <see cref="StatisticsSource.Close"/>,
        /// which preserves the historical behavior for callers that
        /// do not specify a source.
        /// </summary>
        public StatisticsSource StatisticsSource { get; set; }
            = StatisticsSource.Close;

        /// <summary>
        /// Gets or sets the reversal detection mode used by the
        /// Reversal stage.
        ///
        /// The default is
        /// <see cref="ReversalMode.TrailingStopPosition"/>: a reversal
        /// is a FLIP of the ATR Smooth regime itself (the trailing-stop
        /// position bias), NOT a candle crossing the ATR Smooth line.
        /// <see cref="ReversalMode.CloseToReference"/> remains
        /// available as an explicit opt-in for the close-to-reference
        /// relation.
        /// </summary>
        public ReversalMode ReversalMode { get; set; }
            = ReversalMode.TrailingStopPosition;
    }
}

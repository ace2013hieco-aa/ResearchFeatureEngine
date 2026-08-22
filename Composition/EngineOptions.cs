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
        /// </summary>
        public ReversalMode ReversalMode { get; set; }
            = ReversalMode.CloseToReference;
    }
}

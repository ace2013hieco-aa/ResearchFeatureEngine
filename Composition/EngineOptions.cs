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
    }
}

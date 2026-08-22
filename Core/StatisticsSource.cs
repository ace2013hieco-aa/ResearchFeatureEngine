namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Selects the quantity whose rolling statistics are computed by
    /// the <see cref="Statistics.StatisticsEngine"/>.
    /// </summary>
    /// <remarks>
    /// The source is materialized from the committed close window at
    /// the engine's input boundary; statistic models always receive
    /// plain numeric observations and remain unaware of whether those
    /// observations represent prices or returns.
    /// </remarks>
    public enum StatisticsSource
    {
        /// <summary>
        /// Raw close prices: the observation at bar t is C_t.
        /// This is the default and preserves the historical behavior
        /// exactly.
        /// </summary>
        Close = 0,

        /// <summary>
        /// Simple (arithmetic) returns derived from adjacent closes:
        /// r_t = C_t / C_{t-1} - 1 for t &gt;= 1. There is no valid
        /// return at the first bar.
        /// </summary>
        SimpleReturn = 1,

        /// <summary>
        /// Logarithmic returns derived from adjacent closes:
        /// r_t = ln(C_t / C_{t-1}) for t &gt;= 1. There is no valid
        /// return at the first bar.
        /// </summary>
        LogReturn = 2
    }
}

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Directional state of the canonical ATRSmooth regime
    /// (the ATR trailing-stop position bias).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The regime itself is the source-declared signed state published
    /// as <see cref="EngineValues.Reference.Regime"/> by the canonical
    /// <c>ATRSmoothReferenceSource</c> (+1 bullish / -1 bearish /
    /// 0 initial-uncommitted). This enum names the three published
    /// segment-state cases; it does NOT reinterpret the regime —
    /// the numeric values match the published regime exactly.
    /// </para>
    /// </remarks>
    public enum AtrSmoothRegimeDirection
    {
        /// <summary>
        /// No established directional regime yet (warm-up): the
        /// canonical ATRSmooth regime has not committed to +1 or -1.
        /// </summary>
        Unavailable = 0,

        /// <summary>
        /// Bullish ATRSmooth regime (trailing-stop position bias +1).
        /// </summary>
        Bullish = 1,

        /// <summary>
        /// Bearish ATRSmooth regime (trailing-stop position bias -1).
        /// </summary>
        Bearish = -1
    }
}

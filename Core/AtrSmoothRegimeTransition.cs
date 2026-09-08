namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Transition state of the canonical ATRSmooth regime segment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A non-zero value marks the bar on which the canonical ATRSmooth
    /// regime flipped — a strict change of the source-declared signed
    /// regime state (<see cref="EngineValues.Reference.Regime"/>)
    /// between consecutive bars, from one established directional
    /// state to the other. It is the FLIP itself, never a candle
    /// crossing the published ATRSmooth reference line.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Up"/>: bearish → bullish ATRSmooth flip
    /// (-1 → +1).
    /// </description></item>
    /// <item><description>
    /// <see cref="Down"/>: bullish → bearish ATRSmooth flip
    /// (+1 → -1).
    /// </description></item>
    /// <item><description>
    /// <see cref="None"/>: no established-regime transition on this
    /// bar (continuation, warm-up, or unavailable).
    /// </description></item>
    /// </list>
    /// </remarks>
    public enum AtrSmoothRegimeTransition
    {
        /// <summary>
        /// No ATRSmooth regime transition on this bar: the regime is
        /// unchanged, or no established directional regime exists.
        /// </summary>
        None = 0,

        /// <summary>
        /// Bearish → bullish ATRSmooth regime flip (-1 → +1).
        /// </summary>
        Up = 1,

        /// <summary>
        /// Bullish → bearish ATRSmooth regime flip (+1 → -1).
        /// </summary>
        Down = -1
    }
}

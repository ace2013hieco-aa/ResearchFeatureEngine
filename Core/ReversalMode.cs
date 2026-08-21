namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Selects the relation used by the
    /// <see cref="Reversal.ReversalEngine"/> to detect ATRSmooth
    /// reversals.
    /// </summary>
    public enum ReversalMode
    {
        /// <summary>
        /// Reversal on a strict side change of the close-to-reference
        /// relation (close &gt;= reference is ABOVE, &lt; is BELOW).
        /// This is the default and matches the close-to-close fallback
        /// semantics.
        /// </summary>
        CloseToReference = 0,

        /// <summary>
        /// Reversal on a strict sign change of the ATR trailing-stop
        /// position bias (<see cref="ReferenceRuntimeValues.TrendPosition"/>):
        /// positive (long bias) → negative (short bias) is Down,
        /// negative → positive is Up. Flat (0) is treated as BELOW.
        /// </summary>
        TrailingStopPosition = 1
    }
}

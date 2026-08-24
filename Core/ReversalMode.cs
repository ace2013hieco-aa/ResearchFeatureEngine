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
        /// Explicit opt-in: this treats a candle crossing the ATR
        /// Smooth line as a reversal, which is NOT the canonical
        /// reversal semantic (see
        /// <see cref="TrailingStopPosition"/>).
        /// </summary>
        CloseToReference = 0,

        /// <summary>
        /// Reversal on a strict sign change of the ATR trailing-stop
        /// position bias (<see cref="ReferenceRuntimeValues.TrendPosition"/>):
        /// positive (long bias) → negative (short bias) is Down,
        /// negative → positive is Up. Flat (0) is treated as BELOW.
        /// This is the DEFAULT and the canonical reversal semantic:
        /// a reversal occurs only when the ATR Smooth regime itself
        /// flips, never on a mere candle crossing of the line.
        /// </summary>
        TrailingStopPosition = 1
    }
}

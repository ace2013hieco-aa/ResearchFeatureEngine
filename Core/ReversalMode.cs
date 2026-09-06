namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Selects the relation used by the
    /// <see cref="Reversal.ReversalEngine"/> to detect reversals of
    /// the selected reference.
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
        /// Reversal on a strict state transition of the source-defined
        /// signed regime state (<see cref="ReferenceRuntimeValues.Regime"/>)
        /// published by the selected reference source: an increase
        /// (0 → +1, -1 → 0, -1 → +1) is Up, a decrease (+1 → 0,
        /// 0 → -1, +1 → -1) is Down, equal states are never a
        /// reversal. For ATRSmooth2 the regime is the trailing-stop
        /// position bias (+1 bullish / -1 bearish / 0 initial) and
        /// this is the canonical reversal semantic: a reversal occurs
        /// only when the ATR Smooth regime itself flips, never on a
        /// mere candle crossing of the line. For Darvas Box the regime
        /// is the positional state (+1 above upper / 0 inside / -1
        /// below lower) and transitions include breakout and
        /// return-to-box events. This is the DEFAULT mode.
        /// </summary>
        TrailingStopPosition = 1
    }
}

using System;

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Selects the single active reference model for an engine
    /// instance.
    ///
    /// Exactly ONE reference source is constructed per engine: the
    /// selected source is built at composition time and injected
    /// through the existing <see cref="Reference.IReferenceSource"/>
    /// dependency-injection path. The non-selected model's parameters
    /// are inert — they are neither read nor validated.
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="ATRSmooth2"/>: equilibrium-level reference — the
    /// average of a VWMA of close and an ATR trailing stop — with a
    /// trailing-stop regime (+1 bullish / -1 bearish / 0 initial).
    /// </description></item>
    /// <item><description>
    /// <see cref="DarvasBox"/>: box-midpoint measurement level —
    /// (Upper + Lower) / 2 of the current Darvas box — with an
    /// above/inside/below structural regime
    /// (+1 above upper / 0 inside / -1 below lower).
    /// </description></item>
    /// </list>
    /// </summary>
    public enum ReferenceType
    {
        /// <summary>
        /// The ATR Smoothed reference source (VWMA + ATR trailing
        /// stop). Publishes the equilibrium measurement level and the
        /// trailing-stop position bias as its regime.
        /// </summary>
        ATRSmooth2 = 0,

        /// <summary>
        /// The Darvas Box reference source. Publishes the current
        /// box's midpoint as its measurement level and the
        /// above/inside/below positional state as its regime.
        /// </summary>
        DarvasBox = 1
    }
}

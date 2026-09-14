using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the M11.1 relative close position between the two
    /// canonical references.
    ///
    /// <code>
    ///           Close - ATRSmooth
    /// R = -------------------------
    ///        HMA - ATRSmooth
    /// </code>
    ///
    /// Dimensionless relative position of the current bar's close
    /// within the HMA–ATRSmooth reference geometry: R is expressed
    /// in units of the (signed) HMA–ATRSmooth separation, with the
    /// ATRSmooth equilibrium at the origin.
    ///
    /// Semantics:
    /// <list type="bullet">
    /// <item><description>R = 0 — Close exactly at ATRSmooth.</description></item>
    /// <item><description>R = 1 — Close exactly at HMA.</description></item>
    /// <item><description>0 &lt; R &lt; 1 — Close between ATRSmooth and HMA.</description></item>
    /// <item><description>R &gt; 1 — Close beyond HMA on the HMA side.</description></item>
    /// <item><description>R &lt; 0 — Close on the opposite side of
    /// ATRSmooth from HMA.</description></item>
    /// </list>
    ///
    /// Unavailable (NaN) while HMA or ATRSmooth is unavailable
    /// (warm-up), and on the exact degenerate denominator
    /// <c>HMA == ATRSmooth</c> (exact equality — no epsilon,
    /// no tolerance, no minimum denominator, no denominator floor;
    /// no exception is thrown).
    ///
    /// The resulting tails are NOT altered: no clamping, no
    /// transformation of R.
    ///
    /// This model does NOT recompute HMA or ATRSmooth — it consumes
    /// the canonical published producer values only.
    /// </summary>
    public sealed class HmaAtrSmoothRelativeClosePositionModel
    {
        /// <summary>
        /// Computes the relative close position.
        /// </summary>
        /// <param name="close">Current bar close.</param>
        /// <param name="hma">Current canonical HMA value.</param>
        /// <param name="atrSmooth">Current canonical ATRSmooth value.</param>
        /// <returns>
        /// (close - atrSmooth) / (hma - atrSmooth), or NaN when either
        /// reference is unavailable or the denominator is exactly zero.
        /// </returns>
        public double Compute(double close, double hma, double atrSmooth)
        {
            // Unavailable while either canonical reference is
            // unavailable (HMA warm-up governs at default parameters).
            if (double.IsNaN(hma) || double.IsNaN(atrSmooth))
                return double.NaN;

            // Degenerate geometry: HMA exactly coincident with the
            // ATRSmooth equilibrium. Exact equality only — no
            // epsilon, no tolerance, no floor; NaN, never an
            // exception, never a silent 0.
            if (hma == atrSmooth)
                return double.NaN;

            double denominator = hma - atrSmooth;

            // Exact-zero denominator is handled above; a NaN close
            // (malformed bar) propagates as NaN through the division.
            return (close - atrSmooth) / denominator;
        }
    }
}

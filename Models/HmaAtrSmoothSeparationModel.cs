using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the M11.1 HMA/ATRSmooth separation.
    ///
    /// <code>
    /// Separation = (HMA - ATRSmooth) / Scale
    /// </code>
    ///
    /// Dimensionless structural separation of the two canonical
    /// references, normalized by the canonical Scale(14) value
    /// (the Scale stage's simple mean of True Range — NOT the
    /// ATRSmooth source's internal EMA of True Range).
    ///
    /// Semantics:
    /// <list type="bullet">
    /// <item><description>&gt; 0 — HMA above ATRSmooth.</description></item>
    /// <item><description>&lt; 0 — HMA below ATRSmooth.</description></item>
    /// <item><description>= 0 — HMA exactly coincident with
    /// ATRSmooth.</description></item>
    /// </list>
    ///
    /// Unavailable (NaN) while the canonical HMA is unavailable
    /// (warm-up, default parameters: bars 0–17) or the canonical
    /// ATRSmooth is unavailable.
    ///
    /// This model does NOT recompute HMA, ATRSmooth, or Scale — it
    /// consumes the canonical published producer values only.
    /// No clipping, no epsilon, no alternative scale.
    /// </summary>
    public sealed class HmaAtrSmoothSeparationModel
    {
        /// <summary>
        /// Computes the normalized HMA–ATRSmooth separation.
        /// </summary>
        /// <param name="hma">Current canonical HMA value.</param>
        /// <param name="atrSmooth">Current canonical ATRSmooth value.</param>
        /// <param name="scale">Current canonical Scale value.</param>
        /// <returns>
        /// (hma - atrSmooth) / scale, or NaN when either canonical
        /// reference is unavailable.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Scale input is not a finite, strictly
        /// positive number — the Scale stage's own fail-closed
        /// contract (ScaleValidator) guarantees this can never be
        /// published, so a violation here is an upstream contract
        /// break and must fail closed rather than masquerade as the
        /// NaN warm-up state.
        /// </exception>
        public double Compute(double hma, double atrSmooth, double scale)
        {
            // Unavailable while either canonical reference is
            // unavailable (HMA warm-up governs at default parameters).
            if (double.IsNaN(hma) || double.IsNaN(atrSmooth))
                return double.NaN;

            // The Scale stage's fail-closed contract (ScaleValidator)
            // already guarantees a finite, strictly positive Scale at
            // every published bar; a non-finite or non-positive Scale
            // reaching this model is an upstream contract violation
            // and fails closed (the Normalization stage's convention
            // for degenerate scale inputs).
            if (!double.IsFinite(scale) || scale <= 0.0)
                throw new InvalidOperationException(
                    $"HmaAtrSmoothSeparation requires a finite, strictly positive Scale; got {scale}.");

            return (hma - atrSmooth) / scale;
        }
    }
}

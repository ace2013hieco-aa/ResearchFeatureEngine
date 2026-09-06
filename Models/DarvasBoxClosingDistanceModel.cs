using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the Darvas Box closing distance: the signed
    /// displacement of a candle's closing price from the OUTER
    /// Darvas Box boundary on the side where the close is located.
    ///
    /// <para>
    /// <b>Exact formula (the canonical research value).</b>
    /// </para>
    /// <code>
    /// if   Close &gt; Upper:  SignedClosingDistance = Close - Upper
    /// elif Close &lt; Lower:  SignedClosingDistance = Close - Lower
    /// else:                 SignedClosingDistance = 0
    /// </code>
    ///
    /// <para>
    /// <b>Sign convention.</b> Positive means the close is ABOVE
    /// the box (measured from the Upper boundary); negative means
    /// the close is BELOW the box (measured from the Lower
    /// boundary); zero means the close is inside the box. A close
    /// EXACTLY on either boundary (Close == Upper or Close ==
    /// Lower) is inside: it produces exactly 0, never a signed
    /// value.
    /// </para>
    ///
    /// <para>
    /// <b>What this is NOT.</b> The boundary used is the outer box
    /// line on the close's side — never the box midpoint, never
    /// the opposite boundary, never "whichever boundary is
    /// numerically closest", and never a high/low/body/ATR/percent
    /// measure. The companion unsigned value
    /// (<c>AbsoluteClosingDistance</c>, published by the engine) is
    /// the absolute value of this signed distance:
    /// <c>Close - Upper</c> above the box, <c>Lower - Close</c>
    /// below the box, 0 inside.
    /// </para>
    ///
    /// <para>
    /// <b>Causality.</b> The model is a pure function of the bar's
    /// close and the Darvas boundaries applicable AT that bar. The
    /// boundaries are supplied by the canonical
    /// <c>DarvasBoxReferenceSource</c> (which is itself strictly
    /// causal); this model performs no box computation of its own
    /// and introduces no look-ahead.
    /// </para>
    ///
    /// <para>
    /// <b>Warm-up.</b> The model is only invoked once a box has
    /// been confirmed. The engine publishes
    /// <see cref="double.NaN"/> while no valid box exists,
    /// preserving the distinction between "valid box + close
    /// inside → 0" and "no valid box yet → unavailable".
    /// </para>
    /// </summary>
    public sealed class DarvasBoxClosingDistanceModel
        : IDarvasBoxClosingDistanceModel
    {
        /// <inheritdoc />
        public double Compute(double close, double upper, double lower)
        {
            if (close > upper)
            {
                return close - upper;
            }

            if (close < lower)
            {
                return close - lower;
            }

            // Lower <= Close <= Upper (exact boundary equality
            // included): the close is inside the box.
            return 0.0;
        }
    }
}

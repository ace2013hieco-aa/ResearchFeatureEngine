using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the current-bar HMA/Price vs ATRSmooth alignment state.
    ///
    /// This is a CURRENT-BAR state, NOT a rolling statistic.
    ///
    /// For every eligible bar:
    ///   HmaAbove = HMA > ATRSmooth
    ///   PriceAbove = Close > ATRSmooth
    ///
    ///   Aligned = (HmaAbove == PriceAbove)
    ///
    /// Classification:
    ///   ALIGNED (GREEN):
    ///     HMA > ATRSmooth AND Close > ATRSmooth
    ///     OR
    ///     HMA < ATRSmooth AND Close < ATRSmooth
    ///
    ///   MISALIGNED (RED):
    ///     HMA > ATRSmooth AND Close < ATRSmooth
    ///     OR
    ///     HMA < ATRSmooth AND Close > ATRSmooth
    ///
    /// Do not derive alignment from any rolling mean.
    /// Do not smooth the alignment state.
    /// Equality follows project convention: > is strictly greater,
    /// < is strictly less. Exact equality (HMA == ATRSmooth or
    /// Close == ATRSmooth) falls through to Unavailable.
    ///
    /// Output unavailable until HMA, ATRSmooth, and Close are valid.
    /// </summary>
    public sealed class HmaPriceAtrSmoothAlignmentModel
    {
        /// <summary>
        /// Computes the current-bar alignment state.
        /// </summary>
        /// <param name="hma">Current HMA value.</param>
        /// <param name="atrSmooth">Current ATRSmooth reference value.</param>
        /// <param name="close">Current bar close.</param>
        /// <returns>
        /// Aligned (1), Misaligned (-1), or Unavailable (0).
        /// </returns>
        public HmaPriceAtrSmoothAlignment Compute(double hma, double atrSmooth, double close)
        {
            // Unavailable if any input is invalid
            if (double.IsNaN(hma) || double.IsNaN(atrSmooth) || double.IsNaN(close))
                return HmaPriceAtrSmoothAlignment.Unavailable;

            // Strict comparisons per spec
            bool hmaAbove = hma > atrSmooth;
            bool hmaBelow = hma < atrSmooth;
            bool priceAbove = close > atrSmooth;
            bool priceBelow = close < atrSmooth;

            // Exact equality → Unavailable (follows project convention:
            // Regime uses > and < for strict transitions; equality is no transition)
            if (!hmaAbove && !hmaBelow)
                return HmaPriceAtrSmoothAlignment.Unavailable;

            if (!priceAbove && !priceBelow)
                return HmaPriceAtrSmoothAlignment.Unavailable;

            bool aligned = hmaAbove == priceAbove;

            return aligned
                ? HmaPriceAtrSmoothAlignment.Aligned
                : HmaPriceAtrSmoothAlignment.Misaligned;
        }
    }
}
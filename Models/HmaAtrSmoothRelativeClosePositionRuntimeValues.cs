using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Runtime values for the HMA/ATRSmooth Relative Close Position
    /// feature (M11.1).
    /// </summary>
    public sealed class HmaAtrSmoothRelativeClosePositionRuntimeValues
    {
        /// <summary>
        /// Gets the current-bar relative close position for the most
        /// recently processed bar. NaN while either canonical
        /// reference is unavailable (HMA warm-up) or the HMA–ATRSmooth
        /// denominator is exactly zero.
        /// </summary>
        public double RelativeClosePosition { get; internal set; } = double.NaN;
    }
}

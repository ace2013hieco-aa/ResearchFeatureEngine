using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Runtime values for the HMA/Price vs ATRSmooth Alignment feature.
    /// </summary>
    public sealed class HmaPriceAtrSmoothAlignmentRuntimeValues
    {
        /// <summary>
        /// Gets the current-bar alignment state for the most recently
        /// processed bar. Unavailable (0) during warm-up or when any
        /// reference is invalid.
        /// </summary>
        public HmaPriceAtrSmoothAlignment Alignment { get; internal set; }
            = HmaPriceAtrSmoothAlignment.Unavailable;
    }
}
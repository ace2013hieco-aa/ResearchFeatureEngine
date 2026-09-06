using System;

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Represents the HMA/Price vs ATRSmooth alignment state.
    /// </summary>
    public enum HmaPriceAtrSmoothAlignment
    {
        /// <summary>
        /// HMA and Price are on the same side of ATRSmooth.
        /// </summary>
        Aligned = 1,

        /// <summary>
        /// HMA and Price are on opposite sides of ATRSmooth.
        /// </summary>
        Misaligned = -1,

        /// <summary>
        /// Not yet available (warm-up, invalid reference values).
        /// </summary>
        Unavailable = 0
    }
}
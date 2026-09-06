using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Engines.Validation
{
    /// <summary>
    /// Validates the output of the <see cref="HmaPriceAtrSmoothAlignmentEngine"/>.
    /// </summary>
    public static class HmaPriceAtrSmoothAlignmentValidator
    {
        /// <summary>
        /// Validates the computed alignment state.
        /// </summary>
        /// <param name="alignment">The computed alignment.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the alignment is not a valid enum value.
        /// </exception>
        public static void Validate(HmaPriceAtrSmoothAlignment alignment)
        {
            if (!Enum.IsDefined(typeof(HmaPriceAtrSmoothAlignment), alignment))
            {
                throw new InvalidOperationException(
                    $"HmaPriceAtrSmoothAlignment must be a valid enum value; got {(int)alignment}.");
            }
        }
    }
}
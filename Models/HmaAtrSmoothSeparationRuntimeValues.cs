using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Runtime values for the HMA/ATRSmooth Separation feature (M11.1).
    /// </summary>
    public sealed class HmaAtrSmoothSeparationRuntimeValues
    {
        /// <summary>
        /// Gets the current-bar normalized HMA–ATRSmooth separation
        /// for the most recently processed bar. NaN while either
        /// canonical reference is unavailable (HMA warm-up).
        /// </summary>
        public double Separation { get; internal set; } = double.NaN;
    }
}

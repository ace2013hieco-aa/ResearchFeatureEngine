using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Runtime values for the Mean Darvas Closing Distance feature.
    /// </summary>
    public sealed class MeanDarvasClosingDistanceRuntimeValues
    {
        /// <summary>
        /// Gets the rolling mean of the signed Darvas closing distance
        /// for the most recently processed bar. NaN while no valid
        /// box exists or insufficient valid observations in the window.
        /// </summary>
        public double MeanSignedDistance { get; internal set; } = double.NaN;
    }
}
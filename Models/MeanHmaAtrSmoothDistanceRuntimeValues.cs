using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Runtime values for the Mean HMA–ATRSmooth Distance feature.
    /// </summary>
    public sealed class MeanHmaAtrSmoothDistanceRuntimeValues
    {
        /// <summary>
        /// Gets the rolling mean of the signed HMA–ATRSmooth distance
        /// for the most recently processed bar. NaN while either
        /// reference is unavailable or insufficient valid observations.
        /// </summary>
        public double MeanSignedDistance { get; internal set; } = double.NaN;
    }
}
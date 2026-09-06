using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the rolling arithmetic mean of the signed HMA–ATRSmooth
    /// distance over a specified window.
    ///
    /// The per-bar signed distance is:
    ///
    /// HmaAtrSmoothDistance[t] = HMA[t] - ATRSmooth[t]
    ///
    /// The rolling mean is computed over the last N bars (including
    /// the current bar).
    ///
    /// Semantics:
    /// * Positive → HMA is, on average, above ATRSmooth.
    /// * Negative → HMA is, on average, below ATRSmooth.
    /// * Magnitude → average structural separation.
    /// * Zero → average separation is zero.
    /// * Unavailable until both HMA and ATRSmooth are valid → NaN.
    /// * No look-ahead.
    ///
    /// This model does NOT recompute HMA or ATRSmooth — it consumes
    /// the canonical reference source values.
    /// </summary>
    public sealed class MeanHmaAtrSmoothDistanceModel
    {
        private readonly int _window;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MeanHmaAtrSmoothDistanceModel"/> class.
        /// </summary>
        /// <param name="window">Rolling window size in bars. Must be >= 1.</param>
        public MeanHmaAtrSmoothDistanceModel(int window)
        {
            if (window < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(window), window, "Window must be at least 1.");
            _window = window;
        }

        /// <summary>
        /// Gets the rolling window size.
        /// </summary>
        public int Window => _window;

        /// <summary>
        /// Computes the rolling mean of HMA–ATRSmooth distances.
        /// </summary>
        /// <param name="distances">Array of HMA–ATRSmooth distances (current bar last).</param>
        /// <returns>
        /// The arithmetic mean of the last min(window, array.Length) values,
        /// or NaN if any bar in the window has invalid HMA or ATRSmooth.
        /// </returns>
        public double Compute(double[] distances)
        {
            if (distances == null || distances.Length == 0)
                return double.NaN;

            int count = Math.Min(_window, distances.Length);
            double sum = 0.0;
            int validCount = 0;

            for (int i = distances.Length - count; i < distances.Length; i++)
            {
                double v = distances[i];
                if (!double.IsNaN(v))
                {
                    sum += v;
                    validCount++;
                }
            }

            if (validCount == 0)
                return double.NaN;

            return sum / validCount;
        }
    }
}
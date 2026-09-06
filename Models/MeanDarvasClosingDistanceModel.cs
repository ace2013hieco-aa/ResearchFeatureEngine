using System;

namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Computes the rolling arithmetic mean of the signed Darvas Box
    /// closing distance over a specified window.
    ///
    /// The per-bar signed distance is the canonical Darvas closing
    /// distance (close vs. the outer box boundary on the close's side):
    ///
    /// if Close > Upper:  SignedDistance = Close - Upper
    /// elif Close < Lower: SignedDistance = Close - Lower
    /// else:               SignedDistance = 0
    ///
    /// The rolling mean is computed over the last N bars (including
    /// the current bar).
    ///
    /// Semantics:
    /// * Positive → average closing price is above the Darvas box.
    /// * Negative → average closing price is below the Darvas box.
    /// * Near zero → price is, on average, close to/inside the box.
    /// * Boundary equality remains zero.
    /// * No confirmed Darvas box → NaN (unavailable, not zero).
    /// * No look-ahead.
    ///
    /// This model reuses the canonical DarvasBoxReferenceSource and
    /// DarvasBoxClosingDistanceModel — no duplicate Darvas math.
    /// </summary>
    public sealed class MeanDarvasClosingDistanceModel
    {
        private readonly int _window;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MeanDarvasClosingDistanceModel"/> class.
        /// </summary>
        /// <param name="window">Rolling window size in bars. Must be >= 1.</param>
        public MeanDarvasClosingDistanceModel(int window)
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
        /// Computes the rolling mean of signed Darvas closing distances.
        /// </summary>
        /// <param name="signedDistances">Array of signed distances (current bar last).</param>
        /// <returns>
        /// The arithmetic mean of the last min(window, array.Length) values,
        /// or NaN if no valid box exists for any bar in the window.
        /// </returns>
        public double Compute(double[] signedDistances)
        {
            if (signedDistances == null || signedDistances.Length == 0)
                return double.NaN;

            int count = Math.Min(_window, signedDistances.Length);
            double sum = 0.0;
            int validCount = 0;

            for (int i = signedDistances.Length - count; i < signedDistances.Length; i++)
            {
                double v = signedDistances[i];
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
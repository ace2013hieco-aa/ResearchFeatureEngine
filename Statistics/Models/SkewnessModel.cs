using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the Fisher–Pearson bias-corrected sample skewness
    /// (G1) of the supplied observations.
    ///
    ///     G1 = sqrt(n(n-1)) / (n-2) * m3 / m2^(3/2)
    ///
    /// where m_k = (1/n) * SUM((x_i - mean)^k) are central moments.
    ///
    /// The computation is a pure two-pass central-moment calculation
    /// (pass 1: mean; pass 2: deviations from the mean). Naive
    /// one-pass power sums (SUM(x), SUM(x^2), SUM(x^3)) are avoided
    /// because they suffer catastrophic cancellation when the mean is
    /// much larger than the dispersion of the observations.
    ///
    /// The model is source-blind: it receives plain numeric
    /// observations and does not know whether they represent prices,
    /// simple returns, or log returns. It is intended to be
    /// interpreted as a return-distribution shape statistic, and
    /// <see cref="StatisticsSource.LogReturn"/> is the canonical
    /// research input (chosen by the caller/composition layer, never
    /// by this model).
    ///
    /// Minimum publication window: n &gt;= 3 (the (n-2) denominator and
    /// the bias-correction factor are undefined below that).
    /// Reliability guidance: exploratory use n &gt;= 50, inference-grade
    /// use n &gt;= 100.
    ///
    /// Zero-variance windows (m2 == 0) make skewness mathematically
    /// undefined: the model returns <see cref="double.NaN"/>, which
    /// the engine treats as "do not publish" (the runtime retains its
    /// last published value, exactly as when the observation count is
    /// below the model's minimum).
    /// </summary>
    public sealed class SkewnessModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.Skewness;

        /// <inheritdoc />
        public int MinimumObservationCount => 3;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length < 3)
            {
                throw new InvalidOperationException(
                    "Skewness requires at least three observations.");
            }

            int count = observations.Length;

            //--------------------------------------------------
            // Pass 1: arithmetic mean.
            //--------------------------------------------------

            double sum = 0.0;
            foreach (double value in observations)
            {
                sum += value;
            }

            double mean = sum / count;

            //--------------------------------------------------
            // Pass 2: central moments m2 and m3, accumulated from
            // deviations (x - mean), never from raw power sums.
            //--------------------------------------------------

            double m2 = 0.0;
            double m3 = 0.0;

            foreach (double value in observations)
            {
                double deviation = value - mean;
                m2 += deviation * deviation;
                m3 += deviation * deviation * deviation;
            }

            m2 /= count;
            m3 /= count;

            // Zero-variance: skewness is undefined. Signal "do not
            // publish" to the engine (see class remarks).
            if (m2 == 0.0)
            {
                return double.NaN;
            }

            // G1 = sqrt(n(n-1)) / (n-2) * m3 / m2^(3/2)
            //
            // m2^(3/2) is evaluated as m2 * sqrt(m2) (m2 is strictly
            // positive here, so this is well-defined and avoids
            // pow() overhead).
            double factor = Math.Sqrt(count * (count - 1.0))
                            / (count - 2.0);

            return factor * m3 / (m2 * Math.Sqrt(m2));
        }
    }
}

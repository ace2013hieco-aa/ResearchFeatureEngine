using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Statistics.Models
{
    /// <summary>
    /// Computes the Fisher bias-corrected EXCESS kurtosis (G2) of the
    /// supplied observations.
    ///
    ///     G2 = ((n+1)(m4/m2^2 - 3) + 6)(n-1) / ((n-2)(n-3))
    ///
    /// where m_k = (1/n) * SUM((x_i - mean)^k) are central moments.
    ///
    /// This is EXCESS kurtosis: a normal distribution produces G2 ≈ 0
    /// (not the raw kurtosis convention b2 ≈ 3, and not the
    /// uncorrected b2 - 3 estimator).
    ///
    /// The computation is a pure two-pass central-moment calculation
    /// (pass 1: mean; pass 2: deviations from the mean). Naive
    /// one-pass power sums (SUM(x), SUM(x^2), SUM(x^4)) are avoided
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
    /// Minimum publication window: n &gt;= 4 (the (n-2)(n-3) denominator
    /// and the bias correction are undefined below that).
    /// Reliability guidance: exploratory use n &gt;= 50, inference-grade
    /// use n &gt;= 100.
    ///
    /// Kurtosis is highly sensitive to extreme observations: a rolling
    /// kurtosis spike can represent either a genuine tail event or a
    /// recent regime break / mixed-volatility window.
    ///
    /// Zero-variance windows (m2 == 0) make kurtosis mathematically
    /// undefined: the model returns <see cref="double.NaN"/>, which
    /// the engine treats as "do not publish" (the runtime retains its
    /// last published value, exactly as when the observation count is
    /// below the model's minimum).
    /// </summary>
    public sealed class KurtosisModel : IStatisticModel
    {
        /// <inheritdoc />
        public StatisticType Type => StatisticType.Kurtosis;

        /// <inheritdoc />
        public int MinimumObservationCount => 4;

        /// <inheritdoc />
        public double Compute(in StatisticsInput input)
        {
            ReadOnlySpan<double> observations = input.Observations.Span;

            if (observations.Length < 4)
            {
                throw new InvalidOperationException(
                    "Kurtosis requires at least four observations.");
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
            // Pass 2: central moments m2 and m4, accumulated from
            // deviations (x - mean), never from raw power sums.
            //--------------------------------------------------

            double m2 = 0.0;
            double m4 = 0.0;

            foreach (double value in observations)
            {
                double deviation = value - mean;
                double deviationSquared = deviation * deviation;
                m2 += deviationSquared;
                m4 += deviationSquared * deviationSquared;
            }

            m2 /= count;
            m4 /= count;

            // Zero-variance: kurtosis is undefined. Signal "do not
            // publish" to the engine (see class remarks).
            if (m2 == 0.0)
            {
                return double.NaN;
            }

            // Raw (non-excess) moment ratio b2 = m4 / m2^2.
            double b2 = m4 / (m2 * m2);

            // Fisher bias-corrected EXCESS kurtosis.
            double n = count;

            return ((n + 1.0) * (b2 - 3.0) + 6.0) * (n - 1.0)
                   / ((n - 2.0) * (n - 3.0));
        }
    }
}

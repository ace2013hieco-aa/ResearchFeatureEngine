using System;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Reference.Configuration
{
    /// <summary>
    /// Configuration for the ATR Smoothed Reference Source.
    ///
    /// The reference is computed as the average of two series:
    ///  1. A volume-weighted moving average of close price (VWMA) over
    ///     <see cref="SmoothLength"/> bars.
    ///  2. A stateful ATR trailing stop, where the loss is
    ///     <see cref="AtrMultiplier"/> * ATR(<see cref="AtrPeriod"/>),
    ///     using an EMA-based Average True Range.
    ///
    /// Reference_t = ( VWMA_t(Close, SmoothLength) + TrailingStop_t ) / 2.
    ///
    /// This is the platform-independent transcription of the cTrader
    /// reference indicator <c>AtrTrailingStopSmoothed</c>. All defaults
    /// match that indicator.
    /// </summary>
    public sealed class ATRSmoothConfiguration : ReferenceSourceConfiguration
    {
        /// <summary>
        /// Default ATR period used by the reference indicator.
        /// </summary>
        public const int DefaultAtrPeriod = 16;

        /// <summary>
        /// Default ATR multiplier used by the reference indicator.
        /// </summary>
        public const double DefaultAtrMultiplier = 5.1;

        /// <summary>
        /// Default VWMA smoothing length used by the reference indicator.
        /// </summary>
        public const int DefaultSmoothLength = 100;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ATRSmoothConfiguration"/> class.
        /// </summary>
        /// <param name="atrPeriod">ATR lookback period. Must be at least 1.</param>
        /// <param name="atrMultiplier">ATR multiplier for the trailing stop.</param>
        /// <param name="smoothLength">VWMA window length. Must be at least 1.</param>
        public ATRSmoothConfiguration(
            int atrPeriod = DefaultAtrPeriod,
            double atrMultiplier = DefaultAtrMultiplier,
            int smoothLength = DefaultSmoothLength)
        {
            if (atrPeriod < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(atrPeriod),
                    atrPeriod,
                    "ATR period must be at least 1.");

            // NaN comparison is always false, so `!(x > 0.0)` is true
            // for NaN, -Inf, and any negative number. Positive infinity
            // is rejected explicitly because it would produce a
            // non-finite trailing-stop loss downstream.
            if (double.IsInfinity(atrMultiplier) || !(atrMultiplier > 0.0))
                throw new ArgumentOutOfRangeException(
                    nameof(atrMultiplier),
                    atrMultiplier,
                    "ATR multiplier must be a positive finite number.");

            if (smoothLength < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(smoothLength),
                    smoothLength,
                    "Smooth length must be at least 1.");

            AtrPeriod = atrPeriod;
            AtrMultiplier = atrMultiplier;
            SmoothLength = smoothLength;
        }

        /// <inheritdoc />
        public override string SourceName => nameof(ATRSmoothReferenceSource);

        /// <summary>
        /// Gets the ATR lookback period.
        /// </summary>
        public int AtrPeriod { get; }

        /// <summary>
        /// Gets the ATR multiplier used to compute the trailing-stop loss.
        /// </summary>
        public double AtrMultiplier { get; }

        /// <summary>
        /// Gets the VWMA smoothing length (window in bars).
        /// </summary>
        public int SmoothLength { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is ATRSmoothConfiguration other &&
            AtrPeriod == other.AtrPeriod &&
            AtrMultiplier.Equals(other.AtrMultiplier) &&
            SmoothLength == other.SmoothLength;

        /// <inheritdoc />
        public override int GetHashCode() =>
            HashCode.Combine(AtrPeriod, AtrMultiplier, SmoothLength);
    }
}

using System;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Reference.Configuration
{
    /// <summary>
    /// Configuration for the Hull Moving Average (HMA) Reference Source.
    ///
    /// The HMA is a fast, low-lag moving average developed by Alan Hull.
    /// It uses weighted moving averages to achieve smoothness with reduced lag.
    ///
    /// Algorithm (period = P):
    ///   WMA1 = WMA(close, P/2)
    ///   WMA2 = WMA(close, P)
    ///   RawHMA = 2 * WMA1 - WMA2
    ///   HMA = WMA(RawHMA, sqrt(P))
    ///
    /// The regime is the HMA slope direction:
    ///   +1 : HMA > HMA[1]  (rising)
    ///   -1 : HMA < HMA[1]  (falling)
    ///   0  : HMA == HMA[1] (flat) or warm-up
    ///
    /// This is a platform-independent transcription of the standard HMA
    /// definition. All defaults match common practice (period = 16).
    /// </summary>
    public sealed class HmaConfiguration : ReferenceSourceConfiguration
    {
        /// <summary>
        /// Default HMA period used by common practice.
        /// </summary>
        public const int DefaultPeriod = 16;

        /// <summary>
        /// Structural minimum period. WMA windows of P/2 and sqrt(P) must
        /// contain at least one bar.
        /// </summary>
        public const int MinPeriod = 2;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaConfiguration"/> class.
        /// </summary>
        /// <param name="period">
        /// HMA period in bars. Must be at least <see cref="MinPeriod"/> (2).
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="period"/> is below <see cref="MinPeriod"/>.
        /// </exception>
        public HmaConfiguration(int period = DefaultPeriod)
        {
            if (period < MinPeriod)
                throw new ArgumentOutOfRangeException(
                    nameof(period),
                    period,
                    $"HMA period must be at least {MinPeriod} (WMA(P/2) and WMA(sqrt(P)) must contain at least one bar).");

            Period = period;
        }

        /// <inheritdoc />
        public override string SourceName => nameof(HmaReferenceSource);

        /// <summary>
        /// Gets the HMA period in bars. All internal windows (WMA of P/2,
        /// WMA of P, WMA of sqrt(P)) derive from this single value.
        /// </summary>
        public int Period { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is HmaConfiguration other && Period == other.Period;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Period);
    }
}
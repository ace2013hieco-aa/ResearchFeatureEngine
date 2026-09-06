using System;

using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Reference.Configuration
{
    /// <summary>
    /// Configuration for the Darvas Box Reference Source.
    ///
    /// The source is a bar-for-bar transcription of the project's
    /// canonical Darvas specification — the "Darvas Box Buy Sell"
    /// Pine Script v4 indicator (c) ceyhun, previously verified and
    /// frozen as <c>DarvasBox5</c> in the Backtest-Engine repository
    /// (boxp = Length, default 5). The full algorithm:
    ///
    /// <code>
    /// LL       = lowest(low, boxp)
    /// k1       = highest(high, boxp)
    /// k2       = highest(high, boxp - 1)
    /// k3       = highest(high, boxp - 2)
    /// NH       = valuewhen(high > k1[1], high, 0)
    /// box1     = k3 &lt; k2
    /// TopBox    = valuewhen(barssince(high > k1[1]) == boxp - 2 and box1, NH, 0)
    /// BottomBox = valuewhen(barssince(high > k1[1]) == boxp - 2 and box1, LL, 0)
    /// </code>
    ///
    /// The box is asymmetric / long-side-anchored: the bottom is the
    /// rolling boxp-bar low at the moment the top confirms, not an
    /// independently pivot-confirmed low. Top/Bottom hold forward
    /// (Pine <c>valuewhen</c>) until the next confirmation replaces
    /// them; before the first confirmation no box exists.
    ///
    /// <see cref="Length"/> is the ONLY parameter of the canonical
    /// specification — the confirmation lag (boxp - 2 bars after the
    /// breakout) and every window are fully determined by it. No
    /// additional "confirmation" parameters are introduced; doing so
    /// would be a different Darvas interpretation than the ratified
    /// one.
    /// </summary>
    public sealed class DarvasBoxConfiguration : ReferenceSourceConfiguration
    {
        /// <summary>
        /// Default box length used by the reference Pine indicator.
        /// </summary>
        public const int DefaultLength = 5;

        /// <summary>
        /// Structural minimum box length. The k3 window
        /// (<see cref="Length"/> - 2 highest-high) must contain at
        /// least one bar, so Length must be at least 3.
        /// </summary>
        public const int MinLength = 3;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="DarvasBoxConfiguration"/> class.
        /// </summary>
        /// <param name="length">
        /// Box length (bars). Must be at least
        /// <see cref="MinLength"/> (3).
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="length"/> is below
        /// <see cref="MinLength"/> (the k3 window would be empty).
        /// </exception>
        public DarvasBoxConfiguration(int length = DefaultLength)
        {
            if (length < MinLength)
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    length,
                    $"Box length must be at least {MinLength} "
                    + "(the highest(high, length - 2) window must "
                    + "contain at least one bar).");

            Length = length;
        }

        /// <inheritdoc />
        public override string SourceName => nameof(DarvasBoxReferenceSource);

        /// <summary>
        /// Gets the box length (boxp) in bars. All rolling windows
        /// (highest-high of boxp, boxp-1, boxp-2 bars; lowest-low of
        /// boxp bars) and the confirmation lag (boxp - 2 bars after
        /// the breakout bar) derive from this single value.
        /// </summary>
        public int Length { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is DarvasBoxConfiguration other &&
            Length == other.Length;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Length);
    }
}

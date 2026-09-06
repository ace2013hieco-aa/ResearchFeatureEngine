using System;

using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Reference.Configuration
{
    /// <summary>
    /// Configuration for the composite HMA + ATRSmooth dual-reference
    /// source (<see cref="HmaAtrSmoothCompositeSource"/>).
    ///
    /// Holds exactly one immutable configuration for EACH canonical
    /// producer. The composite constructs exactly one
    /// <see cref="Sources.HmaReferenceSource"/> from
    /// <see cref="Hma"/> and exactly one
    /// <see cref="Sources.ATRSmoothReferenceSource"/> from
    /// <see cref="AtrSmooth"/> — no duplicates, no additional
    /// parameters. The pipeline measurement level and regime remain
    /// the ATRSmooth2 semantics; the HMA is exposed in parallel as a
    /// canonical runtime value.
    /// </summary>
    public sealed class HmaAtrSmoothConfiguration : ReferenceSourceConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaAtrSmoothConfiguration"/> class.
        /// </summary>
        /// <param name="atrSmooth">
        /// Configuration of the canonical ATRSmooth producer. Must not
        /// be null.
        /// </param>
        /// <param name="hma">
        /// Configuration of the canonical HMA producer. Must not be
        /// null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either configuration is null.
        /// </exception>
        public HmaAtrSmoothConfiguration(
            ATRSmoothConfiguration atrSmooth,
            HmaConfiguration hma)
        {
            AtrSmooth = atrSmooth
                ?? throw new ArgumentNullException(nameof(atrSmooth));
            Hma = hma
                ?? throw new ArgumentNullException(nameof(hma));
        }

        /// <inheritdoc />
        public override string SourceName => nameof(HmaAtrSmoothCompositeSource);

        /// <summary>
        /// Gets the configuration of the canonical ATRSmooth producer.
        /// </summary>
        public ATRSmoothConfiguration AtrSmooth { get; }

        /// <summary>
        /// Gets the configuration of the canonical HMA producer.
        /// </summary>
        public HmaConfiguration Hma { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is HmaAtrSmoothConfiguration other &&
            AtrSmooth.Equals(other.AtrSmooth) &&
            Hma.Equals(other.Hma);

        /// <inheritdoc />
        public override int GetHashCode() =>
            HashCode.Combine(AtrSmooth, Hma);
    }
}

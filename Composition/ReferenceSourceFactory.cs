using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Composition
{
    /// <summary>
    /// Constructs exactly ONE reference source for an engine
    /// instance, selected by <see cref="ReferenceType"/>.
    ///
    /// This is the single construction point for reference sources
    /// used by indicator-level selection: the switch constructs only
    /// the selected source — the non-selected model is NEVER
    /// instantiated, and its parameters are neither read nor
    /// validated (inert). There is no multi-reference collection,
    /// registry, or pipeline; the returned source is injected into
    /// the existing single-source
    /// <see cref="EngineConfiguration.ReferenceSource"/> path.
    /// </summary>
    public static class ReferenceSourceFactory
    {
        /// <summary>
        /// Constructs the selected reference source.
        /// </summary>
        /// <param name="type">
        /// The reference model to construct. Determines which
        /// configuration arguments are consumed; the other model's
        /// arguments are ignored (inert).
        /// </param>
        /// <param name="atrSmoothConfiguration">
        /// Configuration consumed when
        /// <paramref name="type"/> is
        /// <see cref="ReferenceType.ATRSmooth2"/>. Must be non-null
        /// for that selection.
        /// </param>
        /// <param name="darvasBoxConfiguration">
        /// Configuration consumed when
        /// <paramref name="type"/> is
        /// <see cref="ReferenceType.DarvasBox"/>. Must be non-null for
        /// that selection.
        /// </param>
        /// <returns>The single constructed reference source.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the selected model's configuration is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="type"/> is not a defined
        /// <see cref="ReferenceType"/> value.
        /// </exception>
        public static IReferenceSource Create(
            ReferenceType type,
            ATRSmoothConfiguration? atrSmoothConfiguration,
            DarvasBoxConfiguration? darvasBoxConfiguration)
        {
            switch (type)
            {
                case ReferenceType.ATRSmooth2:
                    return new ATRSmoothReferenceSource(
                        atrSmoothConfiguration
                            ?? throw new ArgumentNullException(
                                nameof(atrSmoothConfiguration),
                                "ATRSmooth2 selection requires a non-null "
                                + "ATRSmoothConfiguration."));

                case ReferenceType.DarvasBox:
                    return new DarvasBoxReferenceSource(
                        darvasBoxConfiguration
                            ?? throw new ArgumentNullException(
                                nameof(darvasBoxConfiguration),
                                "DarvasBox selection requires a non-null "
                                + "DarvasBoxConfiguration."));

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(type),
                        type,
                        "Unknown reference type.");
            }
        }
    }
}

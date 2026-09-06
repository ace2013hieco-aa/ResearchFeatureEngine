using System;

using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Runtime;

namespace ResearchFeatureEngine.Reference.Sources
{
    /// <summary>
    /// Composite dual-reference source exposing BOTH canonical
    /// producers — the HMA and the ATRSmooth equilibrium — to the
    /// pipeline simultaneously.
    ///
    /// <para>
    /// <b>Canonical producers.</b> The composite owns exactly ONE
    /// <see cref="HmaReferenceSource"/> and exactly ONE
    /// <see cref="ATRSmoothReferenceSource"/> (each constructed once
    /// from its own immutable configuration). It performs NO indicator
    /// mathematics of its own: every update advances both canonical
    /// sources on the shared context, so their runtimes hold the
    /// current-bar canonical values. Downstream research features
    /// consume <see cref="HmaSource"/> and <see cref="AtrSmoothSource"/>
    /// directly — the same instances the composite drives — never a
    /// re-instantiated or recomputed copy.
    /// </para>
    ///
    /// <para>
    /// <b>Pipeline semantics.</b> The composite's published
    /// measurement level and regime are the ATRSmooth2 equilibrium
    /// level and trailing-stop regime — bit-identical to the
    /// ATRSmooth2 single-reference mode. Distance, Reversal, Scale,
    /// Normalization, and Statistics therefore behave exactly as in
    /// ATRSmooth2 mode; the HMA is an ADDITIVE canonical input, not a
    /// replacement measurement level. This preserves every existing
    /// single-reference semantic: selecting
    /// <see cref="Core.ReferenceType.ATRSmooth2"/>,
    /// <see cref="Core.ReferenceType.DarvasBox"/>, or
    /// <see cref="Core.ReferenceType.Hma"/> alone never constructs the
    /// other producers.
    /// </para>
    ///
    /// <para>
    /// <b>Warm-up.</b> The ATRSmooth equilibrium is finite from the
    /// first bar. The canonical HMA value
    /// (<see cref="HmaSource"/>.Runtime.Hma) is NaN until
    /// <c>index &gt;= P + floor(sqrt(P)) - 2</c>; dual-reference
    /// features must remain unavailable until it is genuinely valid.
    /// The HMA source's close-fallback reference (its published
    /// measurement level during warm-up) is NOT a valid HMA
    /// observation.
    /// </para>
    ///
    /// <para>
    /// <b>Re-tick handling.</b> Each inner source owns its own
    /// snapshot/restore pattern keyed on its own last-seen index;
    /// both see the same shared context and index, so driving both
    /// per bar keeps live re-ticks idempotent for each producer
    /// independently.
    /// </para>
    /// </summary>
    public sealed class HmaAtrSmoothCompositeSource : ReferenceSourceBase
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaAtrSmoothCompositeSource"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Composite configuration carrying both producers'
        /// parameters. Must not be null.
        /// </param>
        public HmaAtrSmoothCompositeSource(HmaAtrSmoothConfiguration configuration)
            : base(configuration)
        {
            // Exactly one construction of each canonical producer.
            HmaSource = new HmaReferenceSource(configuration.Hma);
            AtrSmoothSource = new ATRSmoothReferenceSource(configuration.AtrSmooth);
        }

        /// <summary>
        /// Gets the typed configuration for this source.
        /// </summary>
        public new HmaAtrSmoothConfiguration Configuration =>
            (HmaAtrSmoothConfiguration)base.Configuration;

        /// <summary>
        /// Gets the canonical HMA producer owned by this composite.
        /// This is the single HMA instance for the whole engine —
        /// driven once per bar by the composite, consumed directly by
        /// the dual-reference research features.
        /// </summary>
        public HmaReferenceSource HmaSource { get; }

        /// <summary>
        /// Gets the canonical ATRSmooth producer owned by this
        /// composite. This is the single ATRSmooth instance for the
        /// whole engine — driven once per bar by the composite,
        /// consumed directly by the dual-reference research features.
        /// </summary>
        public ATRSmoothReferenceSource AtrSmoothSource { get; }

        /// <summary>
        /// Gets the composite's regime: the ATRSmooth trailing-stop
        /// position bias (identical to ATRSmooth2 mode), so generic
        /// reversal detection keeps its exact ATRSmooth2 semantics.
        /// </summary>
        public override double Regime => AtrSmoothSource.Regime;

        /// <inheritdoc />
        public override void Reset()
        {
            base.Reset();
            HmaSource.Reset();
            AtrSmoothSource.Reset();
        }

        /// <inheritdoc />
        protected override double ComputeReference(EngineContext context, int index)
        {
            // Advance BOTH canonical producers for this bar. Each
            // inner source applies its own re-tick snapshot logic
            // against the shared index and market data, so both
            // runtimes hold the current-bar canonical values when
            // this returns.
            HmaSource.Update(context);
            double atrSmooth = AtrSmoothSource.Update(context);

            // The ATRSmooth equilibrium remains the pipeline
            // measurement level (identical to ATRSmooth2 mode). The
            // canonical HMA is exposed via HmaSource.Runtime for the
            // dual-reference research features.
            return atrSmooth;
        }
    }
}

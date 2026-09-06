using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// HMA/Price vs ATRSmooth Alignment stage.
    ///
    /// Current-bar point-in-time state, NOT a rolling statistic:
    ///
    /// <code>
    /// HmaAbove   = Runtime.Hma &gt; Runtime.ATRSmooth
    /// PriceAbove = Close &gt; Runtime.ATRSmooth
    /// Aligned    = (HmaAbove == PriceAbove)
    /// </code>
    ///
    /// Classification:
    /// <list type="bullet">
    /// <item><description>+1 ALIGNED — HMA and Close on the same side
    /// of ATRSmooth.</description></item>
    /// <item><description>-1 MISALIGNED — HMA and Close on opposite
    /// sides of ATRSmooth.</description></item>
    /// <item><description>0 UNAVAILABLE — any input invalid
    /// (warm-up), or an exact equality
    /// (<c>HMA == ATRSmooth</c> or <c>Close == ATRSmooth</c>) where
    /// neither "above" nor "below" strictly holds (the project's
    /// strict-&gt; / strict-&lt; comparison convention; no epsilon
    /// is introduced).</description></item>
    /// </list>
    ///
    /// No rolling mean, no smoothing, no hysteresis, no persistence,
    /// and no dependence on MeanHmaAtrSmoothDistance: the state is a
    /// pure function of the current bar's canonical HMA, the current
    /// bar's canonical ATRSmooth equilibrium, and the current close.
    ///
    /// <para>
    /// <b>Canonical inputs.</b> <c>Runtime.Hma</c> from the canonical
    /// <see cref="HmaReferenceSource"/> (NEVER the source's warm-up
    /// close fallback — with warm-up HMA the state is UNAVAILABLE),
    /// <c>Runtime.LastReference</c> from the canonical
    /// <see cref="ATRSmoothReferenceSource"/>, and the current close
    /// from the shared market data. Both source instances are the
    /// same ones the composite dual-reference source drives; the
    /// engine recomputes neither.
    /// </para>
    ///
    /// Pipeline placement: after the Reference stage (which, in the
    /// dual-reference composition, has already advanced BOTH
    /// canonical producers). Registered only for the
    /// HMA + ATRSmooth composite mode.
    /// </summary>
    public sealed class HmaPriceAtrSmoothAlignmentEngine : EngineBase
    {
        private readonly HmaPriceAtrSmoothAlignmentModel _model;
        private readonly HmaReferenceSource _hmaSource;
        private readonly ATRSmoothReferenceSource _atrSource;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaPriceAtrSmoothAlignmentEngine"/> class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="model">The alignment model.</param>
        /// <param name="hmaSource">The canonical HMA reference source.</param>
        /// <param name="atrSource">The canonical ATRSmooth reference source.</param>
        public HmaPriceAtrSmoothAlignmentEngine(
            EngineContext context,
            HmaPriceAtrSmoothAlignmentModel model,
            HmaReferenceSource hmaSource,
            ATRSmoothReferenceSource atrSource)
            : base("HmaPriceAtrSmoothAlignmentEngine", context)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _hmaSource = hmaSource ?? throw new ArgumentNullException(nameof(hmaSource));
            _atrSource = atrSource ?? throw new ArgumentNullException(nameof(atrSource));
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            int index = Context.CurrentIndex;
            var values = Context.Values.HmaPriceAtrSmoothAlignment;

            // Canonical inputs (Runtime.Hma only — the HMA source's
            // warm-up close fallback is never consumed).
            double hma = _hmaSource.Runtime.Hma;
            double atrSmooth = _atrSource.Runtime.LastReference;
            double close = Context.MarketData!.Close[index];

            HmaPriceAtrSmoothAlignment alignment =
                _model.Compute(hma, atrSmooth, close);

            HmaPriceAtrSmoothAlignmentValidator.Validate(alignment);
            values.Alignment = alignment;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            var values = Context.Values.HmaPriceAtrSmoothAlignment;
            values.Alignment = HmaPriceAtrSmoothAlignment.Unavailable;
        }
    }
}

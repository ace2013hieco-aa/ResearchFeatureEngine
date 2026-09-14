using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// HMA/ATRSmooth Relative Close Position stage (M11.1).
    ///
    /// Current-bar point-in-time measurement, NOT a rolling
    /// statistic:
    ///
    /// <code>
    ///                  Close - Runtime.ATRSmooth
    /// R = ---------------------------------------
    ///        Runtime.Hma - Runtime.ATRSmooth
    /// </code>
    ///
    /// <para>
    /// <b>Canonical inputs.</b> The current bar's close from the
    /// shared market data, <c>Runtime.Hma</c> from the canonical
    /// <see cref="HmaReferenceSource"/> (NEVER the source's warm-up
    /// close fallback — with warm-up HMA the measurement is NaN),
    /// and <c>Runtime.LastReference</c> (the ATRSmooth equilibrium)
    /// from the canonical <see cref="ATRSmoothReferenceSource"/>.
    /// The engine recomputes neither producer: both source
    /// instances are the same ones the composite dual-reference
    /// source drives.
    /// </para>
    ///
    /// <para>
    /// <b>Degenerate geometry.</b> When <c>HMA == ATRSmooth</c>
    /// exactly (e.g. a constant-price series after warm-up), the
    /// position is NaN — exact equality only, no epsilon, no
    /// tolerance, no denominator floor, and no exception.
    /// </para>
    ///
    /// <para>
    /// <b>Pipeline placement.</b> After the Statistics stage, beside
    /// the separation stage (the two M11.1 geometry measurements
    /// are independent). Registered ONLY for the HMA + ATRSmooth
    /// composite mode (type-gated in the builder); every other mode
    /// keeps its exact previous behavior and the published values
    /// remain at their unavailable (NaN) defaults.
    /// </para>
    ///
    /// The stage is stateless (a pure function of the current bar's
    /// published values), so no re-tick snapshot is required: a
    /// repeated call for the same bar reads the same canonical
    /// inputs and publishes the identical value.
    /// </summary>
    public sealed class HmaAtrSmoothRelativeClosePositionEngine : EngineBase
    {
        private readonly HmaAtrSmoothRelativeClosePositionModel _model;
        private readonly HmaReferenceSource _hmaSource;
        private readonly ATRSmoothReferenceSource _atrSource;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaAtrSmoothRelativeClosePositionEngine"/> class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="model">The relative close position model.</param>
        /// <param name="hmaSource">The canonical HMA reference source.</param>
        /// <param name="atrSource">The canonical ATRSmooth reference source.</param>
        public HmaAtrSmoothRelativeClosePositionEngine(
            EngineContext context,
            HmaAtrSmoothRelativeClosePositionModel model,
            HmaReferenceSource hmaSource,
            ATRSmoothReferenceSource atrSource)
            : base("HmaAtrSmoothRelativeClosePositionEngine", context)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _hmaSource = hmaSource ?? throw new ArgumentNullException(nameof(hmaSource));
            _atrSource = atrSource ?? throw new ArgumentNullException(nameof(atrSource));
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            int index = Context.CurrentIndex;
            var values = Context.Values.HmaAtrSmoothRelativeClosePosition;

            // Canonical inputs from the canonical producers' runtimes
            // and the shared market data (current-bar close only).
            double close = Context.MarketData!.Close[index];
            double hma = _hmaSource.Runtime.Hma;
            double atrSmooth = _atrSource.Runtime.LastReference;

            double position = _model.Compute(close, hma, atrSmooth);

            HmaAtrSmoothRelativeClosePositionValidator.Validate(position);
            values.RelativeClosePosition = position;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            var values = Context.Values.HmaAtrSmoothRelativeClosePosition;
            values.RelativeClosePosition = double.NaN;
        }
    }
}

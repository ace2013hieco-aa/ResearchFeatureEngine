using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// HMA/ATRSmooth Separation stage (M11.1).
    ///
    /// Current-bar point-in-time measurement, NOT a rolling
    /// statistic:
    ///
    /// <code>
    /// Separation = (Runtime.Hma - Runtime.ATRSmooth) / Scale
    /// </code>
    ///
    /// <para>
    /// <b>Canonical inputs.</b> <c>Runtime.Hma</c> from the canonical
    /// <see cref="HmaReferenceSource"/> (NEVER the source's warm-up
    /// close fallback — with warm-up HMA the measurement is NaN),
    /// <c>Runtime.LastReference</c> (the ATRSmooth equilibrium) from
    /// the canonical <see cref="ATRSmoothReferenceSource"/>, and the
    /// current-bar canonical <c>Scale</c> published by the Scale
    /// stage. The engine recomputes neither producer: both source
    /// instances are the same ones the composite dual-reference
    /// source drives.
    /// </para>
    ///
    /// <para>
    /// <b>Pipeline placement.</b> After the Statistics stage, because
    /// the stage consumes the current-bar Scale value published by
    /// the Scale stage. Registered ONLY for the HMA + ATRSmooth
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
    public sealed class HmaAtrSmoothSeparationEngine : EngineBase
    {
        private readonly HmaAtrSmoothSeparationModel _model;
        private readonly HmaReferenceSource _hmaSource;
        private readonly ATRSmoothReferenceSource _atrSource;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaAtrSmoothSeparationEngine"/> class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="model">The separation model.</param>
        /// <param name="hmaSource">The canonical HMA reference source.</param>
        /// <param name="atrSource">The canonical ATRSmooth reference source.</param>
        public HmaAtrSmoothSeparationEngine(
            EngineContext context,
            HmaAtrSmoothSeparationModel model,
            HmaReferenceSource hmaSource,
            ATRSmoothReferenceSource atrSource)
            : base("HmaAtrSmoothSeparationEngine", context)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _hmaSource = hmaSource ?? throw new ArgumentNullException(nameof(hmaSource));
            _atrSource = atrSource ?? throw new ArgumentNullException(nameof(atrSource));
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            var values = Context.Values.HmaAtrSmoothSeparation;

            // Canonical inputs from the canonical producers' runtimes
            // and the Scale stage's published current-bar value.
            double hma = _hmaSource.Runtime.Hma;
            double atrSmooth = _atrSource.Runtime.LastReference;
            double scale = Context.Values.Scale.Scale;

            double separation = _model.Compute(hma, atrSmooth, scale);

            HmaAtrSmoothSeparationValidator.Validate(separation);
            values.Separation = separation;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            var values = Context.Values.HmaAtrSmoothSeparation;
            values.Separation = double.NaN;
        }
    }
}

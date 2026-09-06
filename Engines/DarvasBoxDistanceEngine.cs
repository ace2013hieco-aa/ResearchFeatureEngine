using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// Darvas Box Closing Distance stage.
    ///
    /// Publishes the signed (and unsigned) distance of the current
    /// candle's CLOSE from the OUTER boundary of the current Darvas
    /// box on the close's side:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// Close above the box → <c>Close - Upper</c> (positive).
    /// </description></item>
    /// <item><description>
    /// Close below the box → <c>Close - Lower</c> (negative).
    /// </description></item>
    /// <item><description>
    /// Close inside the box (exact boundary equality included) → 0.
    /// </description></item>
    /// </list>
    ///
    /// The stage consumes the CANONICAL Darvas source: it reads the
    /// current box boundaries directly from the
    /// <see cref="DarvasBoxReferenceSource"/> instance that the
    /// pipeline's Reference stage already drives — the same
    /// production source used by every other research feature. It
    /// performs NO box computation of its own and introduces no
    /// look-ahead: the boundaries it reads are exactly those the
    /// source holds for the current processing index (the source is
    /// strictly causal — its state at index i depends only on bars
    /// &lt;= i).
    ///
    /// <para>
    /// <b>Warm-up.</b> Before the first box confirmation the source
    /// reports no valid box (<c>HasBox == false</c>). The stage then
    /// publishes <see cref="double.NaN"/> for both distances and
    /// <c>false</c> for <see cref="Core.DarvasBoxDistanceRuntimeValues.HasBox"/>
    /// — preserving the architecture's established convention that
    /// an unavailable box is NaN, NOT a silent 0 (the source's own
    /// boundary accessors are NaN during warm-up). This keeps
    /// "valid box + close inside → 0" and "no valid box yet →
    /// unavailable" strictly distinct.
    /// </para>
    ///
    /// <para>
    /// <b>Placement.</b> Registered in the pipeline immediately
    /// after <see cref="DistanceEngine"/> and before
    /// <see cref="Reversal.ReversalEngine"/> (the builder registers
    /// it conditionally — see
    /// <see cref="Composition.ResearchFeatureEngineBuilder"/>). It
    /// runs once per bar after the Reference stage has advanced the
    /// source. It is an ADDITIVE research feature: no existing stage
    /// is modified, and the stage is absent (runtime values remain
    /// NaN) for non-Darvas reference sources.
    /// </para>
    /// </summary>
    public sealed class DarvasBoxDistanceEngine : EngineBase
    {
        private readonly IDarvasBoxClosingDistanceModel _model;
        private readonly DarvasBoxReferenceSource _darvasSource;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="DarvasBoxDistanceEngine"/> class.
        /// </summary>
        /// <param name="context">
        /// Shared engine context (provides the current close and the
        /// publish target).
        /// </param>
        /// <param name="model">
        /// The pure distance model implementing the signed formula.
        /// </param>
        /// <param name="darvasSource">
        /// The canonical Darvas reference source instance driven by
        /// the pipeline's Reference stage. The same object the
        /// factory injected — never a second construction.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any required dependency is null.
        /// </exception>
        public DarvasBoxDistanceEngine(
            EngineContext context,
            IDarvasBoxClosingDistanceModel model,
            DarvasBoxReferenceSource darvasSource)
            : base("DarvasBoxDistanceEngine", context)
        {
            _model = model
                ?? throw new ArgumentNullException(nameof(model));
            _darvasSource = darvasSource
                ?? throw new ArgumentNullException(nameof(darvasSource));
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            int index = Context.CurrentIndex;
            var values = Context.Values.DarvasBoxDistance;

            if (!_darvasSource.HasBox)
            {
                // Warm-up: no confirmed box yet. Unavailable (NaN),
                // NOT a silent 0 — "valid box + inside → 0" and
                // "no box yet → unavailable" stay distinct.
                values.HasBox = false;
                values.SignedClosingDistance = double.NaN;
                values.AbsoluteClosingDistance = double.NaN;
                return;
            }

            double upper = _darvasSource.Upper;
            double lower = _darvasSource.Lower;
            double close = Context.MarketData!.Close[index];

            double signed = _model.Compute(close, upper, lower);
            double absolute = Math.Abs(signed);

            DarvasBoxClosingDistanceValidator.Validate(signed);

            values.HasBox = true;
            values.SignedClosingDistance = signed;
            values.AbsoluteClosingDistance = absolute;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            var values = Context.Values.DarvasBoxDistance;
            values.HasBox = false;
            values.SignedClosingDistance = double.NaN;
            values.AbsoluteClosingDistance = double.NaN;
        }
    }
}

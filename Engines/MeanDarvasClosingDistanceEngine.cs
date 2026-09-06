using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// Mean Darvas Closing Distance stage.
    ///
    /// Computes the rolling arithmetic mean of the canonical signed
    /// Darvas closing distance (close vs. the outer Darvas box
    /// boundary) over a specified window:
    ///
    /// <code>
    /// MeanDarvasClosingDistance[t]
    ///     = mean(SignedClosingDistance[t-N+1 .. t])
    /// </code>
    ///
    /// Consumes the signed distance values already produced by the
    /// <see cref="DarvasBoxDistanceEngine"/> — no duplicate Darvas
    /// math.
    ///
    /// <para>
    /// <b>Rolling-NaN semantics (established engine convention).</b>
    /// Follows the Statistics stage's convention exactly
    /// (<see cref="Statistics.StatisticsEngine"/>): unavailable bars
    /// are simply NOT observations — the rolling window stores only
    /// bars with a valid confirmed box, a partial window is a valid
    /// window (mean of the observations it holds, from 1 observation
    /// up, matching <c>MeanModel.MinimumObservationCount == 1</c>),
    /// and the mean is NaN only while the window is empty (no valid
    /// observation yet). Since a confirmed Darvas box never becomes
    /// unconfirmed, a NaN can only occur during the initial warm-up
    /// (no box yet); once the first box exists every subsequent bar
    /// is a valid observation.
    /// </para>
    ///
    /// <para>
    /// <b>Complexity.</b> TRUE O(1) per bar: the rolling mean is
    /// maintained incrementally (sum += new; sum -= outgoing;
    /// mean = sum / count) with no per-bar window allocation or copy.
    /// </para>
    ///
    /// <para>
    /// <b>Re-tick handling.</b> Live streaming consumers re-call the
    /// pipeline for the same bar as ticks arrive. On the first call
    /// for a bar the engine snapshots its rolling state; on a re-tick
    /// it restores the snapshot before recomputing, so the same bar
    /// cannot double-count into the window and the mean is idempotent
    /// per bar index (same pattern as the reference sources).
    /// </para>
    ///
    /// Pipeline placement: after <see cref="DarvasBoxDistanceEngine"/>.
    /// </summary>
    public sealed class MeanDarvasClosingDistanceEngine : EngineBase
    {
        private readonly MeanDarvasClosingDistanceModel _model;
        private readonly DarvasBoxReferenceSource _darvasSource;

        // Rolling state: running sum + observation count + ring head
        // (the slot of the OLDEST observation — the same layout as
        // Statistics.StatisticsWindow). The window is bounded by
        // _model.Window; the O(1) update adds the new observation,
        // subtracts the outgoing one when full, and never allocates
        // or copies the whole window.
        private double _sum;
        private int _count;
        private int _head;

        // Ring buffer of the stored observations.
        private readonly double[] _window;

        // ---------------------------------------------------------
        // Re-tick snapshot (rolling state)
        // ---------------------------------------------------------
        private double _snapshotSum;
        private int _snapshotCount;
        private int _snapshotHead;
        private int _snapshotIndex = int.MinValue;
        private readonly double[] _snapshotWindow;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MeanDarvasClosingDistanceEngine"/> class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="model">The mean distance model.</param>
        /// <param name="darvasSource">
        /// The canonical Darvas reference source (for warm-up check).
        /// </param>
        public MeanDarvasClosingDistanceEngine(
            EngineContext context,
            MeanDarvasClosingDistanceModel model,
            DarvasBoxReferenceSource darvasSource)
            : base("MeanDarvasClosingDistanceEngine", context)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _darvasSource = darvasSource ?? throw new ArgumentNullException(nameof(darvasSource));

            _window = new double[_model.Window];
            _snapshotWindow = new double[_model.Window];
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            var values = Context.Values.MeanDarvasClosingDistance;

            //--------------------------------------------------
            // Re-tick handling: restore the rolling state captured
            // at the start of this bar so a re-tick recomputes from
            // a clean baseline (no double-count).
            //--------------------------------------------------

            if (_snapshotIndex == Context.CurrentIndex)
            {
                _sum = _snapshotSum;
                _count = _snapshotCount;
                _head = _snapshotHead;
                Array.Copy(_snapshotWindow, _window, _window.Length);
            }
            else
            {
                _snapshotSum = _sum;
                _snapshotCount = _count;
                _snapshotHead = _head;
                Array.Copy(_window, _snapshotWindow, _window.Length);
                _snapshotIndex = Context.CurrentIndex;
            }

            // Warm-up: no confirmed box yet → this bar is not an
            // observation (NaN, NOT a silent 0). The window retains
            // its prior observations (none exist during warm-up).
            if (!_darvasSource.HasBox)
            {
                values.MeanSignedDistance = _count > 0
                    ? _sum / _count
                    : double.NaN;
                return;
            }

            // The canonical signed distance for this bar, produced by
            // DarvasBoxDistanceEngine (never recomputed here).
            double signedDistance = Context.Values.DarvasBoxDistance.SignedClosingDistance;

            // Warm-up edge case: DarvasBoxDistanceEngine publishes NaN
            // exactly when the source has no box, so with HasBox true
            // the value is finite; guard defensively for the NaN
            // input path regardless (no silent NaN-as-0).
            if (double.IsNaN(signedDistance))
            {
                values.MeanSignedDistance = _count > 0
                    ? _sum / _count
                    : double.NaN;
                return;
            }

            // O(1) rolling mean update (StatisticsWindow.Add layout):
            // growing window appends after the newest observation;
            // full window overwrites the oldest slot and advances the
            // head.
            if (_count < _model.Window)
            {
                _window[(_head + _count) % _model.Window] = signedDistance;
                _sum += signedDistance;
                _count++;
            }
            else
            {
                _sum -= _window[_head];
                _window[_head] = signedDistance;
                _sum += signedDistance;
                _head = (_head + 1) % _model.Window;
            }

            double mean = _sum / _count;
            MeanDarvasClosingDistanceValidator.Validate(mean);
            values.MeanSignedDistance = mean;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            var values = Context.Values.MeanDarvasClosingDistance;
            values.MeanSignedDistance = double.NaN;
            _sum = 0.0;
            _count = 0;
            _head = 0;
            Array.Clear(_window, 0, _window.Length);
            Array.Clear(_snapshotWindow, 0, _snapshotWindow.Length);
            _snapshotIndex = int.MinValue;
        }
    }
}

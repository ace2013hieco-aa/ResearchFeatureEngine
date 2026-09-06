using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using ResearchFeatureEngine.Reference.Sources;

namespace ResearchFeatureEngine.Engines
{
    /// <summary>
    /// Mean HMA–ATRSmooth Distance stage.
    ///
    /// Computes the rolling arithmetic mean of the signed structural
    /// distance HMA − ATRSmooth over a specified window:
    ///
    /// <code>
    /// HmaAtrSmoothDistance[t]    = Runtime.Hma[t] − Runtime.ATRSmooth[t]
    /// MeanHmaAtrSmoothDistance[t] = mean(HmaAtrSmoothDistance[t-N+1 .. t])
    /// </code>
    ///
    /// where <c>Runtime.Hma</c> is the canonical HMA value published by
    /// the canonical <see cref="HmaReferenceSource"/> and
    /// <c>Runtime.ATRSmooth</c> is the canonical ATRSmooth equilibrium
    /// (<c>LastReference</c>) published by the canonical
    /// <see cref="ATRSmoothReferenceSource"/> — the same instances the
    /// composite dual-reference source drives. The engine recomputes
    /// neither source.
    ///
    /// <para>
    /// <b>Warm-up / HMA-fallback exclusion.</b> The HMA source's
    /// published measurement level falls back to the current close
    /// while its WMA windows are not full
    /// (<see cref="HmaReferenceSource"/>'s pipeline contract). This
    /// engine consumes <c>Runtime.Hma</c> ONLY — the fallback never
    /// enters the distance. The feature is NaN (unavailable) until
    /// the HMA runtime value is GENUINELY valid
    /// (<c>index &gt;= P + floor(sqrt(P)) − 2</c>); the ATRSmooth
    /// equilibrium is finite from the first bar, so the HMA governs
    /// warm-up.
    /// </para>
    ///
    /// <para>
    /// <b>Rolling-NaN semantics.</b> Same established convention as
    /// the Statistics stage and the Mean Darvas stage: bars where the
    /// distance is unavailable are NOT observations — the window
    /// stores only valid bars, a partial window is a valid window
    /// (mean of its observations, from 1 up), and the mean is NaN
    /// only while the window is empty. After warm-up the HMA stays
    /// valid, so the window fills contiguously.
    /// </para>
    ///
    /// <para>
    /// <b>Complexity.</b> TRUE O(1) per bar (sum += new; sum −=
    /// outgoing; mean = sum / count) — no per-bar window allocation
    /// or copy.
    /// </para>
    ///
    /// <para>
    /// <b>Re-tick handling.</b> Snapshot/restore of the rolling state
    /// on the first call for a bar, so live re-ticks cannot
    /// double-count and the mean is idempotent per bar index.
    /// </para>
    ///
    /// Pipeline placement: after the Reference stage (which, in the
    /// dual-reference composition, has already advanced BOTH
    /// canonical producers). Registered only for the
    /// HMA + ATRSmooth composite mode.
    /// </summary>
    public sealed class MeanHmaAtrSmoothDistanceEngine : EngineBase
    {
        private readonly MeanHmaAtrSmoothDistanceModel _model;
        private readonly HmaReferenceSource _hmaSource;
        private readonly ATRSmoothReferenceSource _atrSource;

        // Rolling state (StatisticsWindow ring layout): running sum,
        // observation count, ring head of the oldest observation.
        private double _sum;
        private int _count;
        private int _head;
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
        /// <see cref="MeanHmaAtrSmoothDistanceEngine"/> class.
        /// </summary>
        /// <param name="context">Shared engine context.</param>
        /// <param name="model">The mean distance model.</param>
        /// <param name="hmaSource">The canonical HMA reference source.</param>
        /// <param name="atrSource">The canonical ATRSmooth reference source.</param>
        public MeanHmaAtrSmoothDistanceEngine(
            EngineContext context,
            MeanHmaAtrSmoothDistanceModel model,
            HmaReferenceSource hmaSource,
            ATRSmoothReferenceSource atrSource)
            : base("MeanHmaAtrSmoothDistanceEngine", context)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _hmaSource = hmaSource ?? throw new ArgumentNullException(nameof(hmaSource));
            _atrSource = atrSource ?? throw new ArgumentNullException(nameof(atrSource));

            _window = new double[_model.Window];
            _snapshotWindow = new double[_model.Window];
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            var values = Context.Values.MeanHmaAtrSmoothDistance;

            //--------------------------------------------------
            // Re-tick handling: restore the rolling state captured
            // at the start of this bar (no double-count).
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

            // Canonical inputs from the canonical producers' runtimes.
            // Runtime.Hma is NaN during HMA warm-up — the close
            // fallback returned by ComputeReference is NEVER read
            // here, so a fallback value can never masquerade as a
            // valid HMA observation.
            double hma = _hmaSource.Runtime.Hma;
            double atrSmooth = _atrSource.Runtime.LastReference;

            // Warm-up: unavailable until BOTH canonical values are
            // genuinely valid (HMA is the governing input).
            if (double.IsNaN(hma) || double.IsNaN(atrSmooth))
            {
                values.MeanSignedDistance = _count > 0
                    ? _sum / _count
                    : double.NaN;
                return;
            }

            double distance = hma - atrSmooth;

            // O(1) rolling mean update (StatisticsWindow.Add layout).
            if (_count < _model.Window)
            {
                _window[(_head + _count) % _model.Window] = distance;
                _sum += distance;
                _count++;
            }
            else
            {
                _sum -= _window[_head];
                _window[_head] = distance;
                _sum += distance;
                _head = (_head + 1) % _model.Window;
            }

            double mean = _sum / _count;
            MeanHmaAtrSmoothDistanceValidator.Validate(mean);
            values.MeanSignedDistance = mean;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
            var values = Context.Values.MeanHmaAtrSmoothDistance;
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

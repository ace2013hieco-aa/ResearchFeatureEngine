using System;

using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Reference.Configuration;

namespace ResearchFeatureEngine.Reference.Sources
{
    /// <summary>
    /// Reference source implementing the Darvas Box lifecycle.
    ///
    /// This is a bar-for-bar transcription of the project's canonical
    /// Darvas specification — the "Darvas Box Buy Sell" Pine Script
    /// v4 indicator (c) ceyhun (boxp = Length, default 5), previously
    /// verified and frozen as <c>DarvasBox5</c> in the Backtest-Engine
    /// repository. The transcription follows the documented Pine v4
    /// builtin semantics exactly:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <c>highest(x, n)</c> / <c>lowest(x, n)</c>: rolling max/min
    /// over the last n bars INCLUDING the current bar; undefined
    /// ("na") until n bars of history exist.
    /// </description></item>
    /// <item><description>
    /// <c>series[1]</c>: the previous bar's value of that series;
    /// undefined on the first bar.
    /// </description></item>
    /// <item><description>
    /// <c>valuewhen(cond, source, 0)</c>: the source value at the
    /// most recent bar where cond was true; held forward unchanged
    /// until cond is true again; undefined until cond has been true
    /// at least once.
    /// </description></item>
    /// <item><description>
    /// <c>barssince(cond)</c>: bars elapsed since cond was last true
    /// (0 on the bar where it is true); never matches if cond has
    /// never fired.
    /// </description></item>
    /// <item><description>
    /// Undefined (na) comparisons evaluate to false, matching
    /// Pine's na-as-non-triggering behavior in boolean contexts
    /// feeding valuewhen/barssince.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Algorithm (boxp = <see cref="DarvasBoxConfiguration.Length"/>).</b>
    /// A breakout bar is one whose high exceeds the previous bar's
    /// boxp-bar highest-high (k1[1]); its high becomes the pivot-high
    /// candidate (NH, held). The box CONFIRMS exactly boxp - 2 bars
    /// after the breakout bar, provided the pivot high has not been
    /// exceeded in the interim (box1 = k3 &lt; k2, where k2/k3 are the
    /// current bar's highest-high over boxp-1 / boxp-2 bars). On
    /// confirmation: Upper = NH (the breakout high), Lower = LL (the
    /// current bar's boxp-bar lowest-low) — the box is asymmetric /
    /// long-side-anchored, exactly as the source computes; there is
    /// no separate bottom-pivot confirmation. The box holds forward
    /// (valuewhen) until the next confirmation replaces it.
    /// </para>
    ///
    /// <para>
    /// <b>Pipeline publication contract.</b>
    /// <see cref="ComputeReference"/> returns the current box's
    /// midpoint <c>(Upper + Lower) / 2</c> once a box has been
    /// confirmed; before the first confirmation (warm-up) it returns
    /// the current close, so the published measurement level is
    /// always a finite scalar. <see cref="Regime"/> is the Darvas
    /// positional state: +1 (close above the upper boundary), 0
    /// (close inside the box — a real persistent Darvas state, NOT
    /// bearish), -1 (close below the lower boundary); during
    /// warm-up it is 0 (no box yet — uncommitted).
    /// </para>
    ///
    /// <para>
    /// <b>Reversal semantics.</b> A Darvas reversal is a strict
    /// transition of the positional regime between consecutive bars;
    /// this includes breakout (0 → +1) and return-to-box
    /// (+1 → 0) transitions.
    /// </para>
    ///
    /// <para>
    /// The box midpoint may jump when the box is replaced (a new
    /// confirmation replaces the held Top/Bottom). That is an
    /// intentional structural effect of the Darvas specification; it
    /// is never smoothed, suppressed, or clamped.
    /// </para>
    ///
    /// State that persists between bars per source instance: the
    /// held NH (pivot high), the barssince-breakout counter, the
    /// held Top/Bottom box, and the current bar's computed regime.
    /// All state is reset by <see cref="Reset"/>.
    /// </summary>
    public sealed class DarvasBoxReferenceSource : ReferenceSourceBase
    {
        // ---------------------------------------------------------
        // Re-tick snapshot
        // ---------------------------------------------------------
        //
        // Live streaming consumers (cTrader indicator) re-call
        // Update() for the same bar index as ticks arrive on the
        // still-forming bar. The source is stateful (barssince
        // counter, held NH, held Top/Bottom, published regime). The
        // snapshot pattern is identical to ATRSmoothReferenceSource:
        // capture the end-of-previous-bar state on the first call
        // for a bar, restore it on re-ticks for the same bar, then
        // recompute from that clean baseline with the latest market
        // data.

        private double _snapshotNh;
        private double _snapshotTop;
        private double _snapshotBottom;
        private int _snapshotBarsSince;
        private double _snapshotRegime;
        private int _snapshotIndex = int.MinValue;

        // Held state (valuewhen semantics).
        private double _nh = double.NaN;       // valuewhen(high > k1[1], high, 0)
        private double _top = double.NaN;      // valuewhen(confirm, NH, 0)
        private double _bottom = double.NaN;   // valuewhen(confirm, LL, 0)

        // barssince(high > k1[1]) sentinel: -1 = breakout never fired.
        private int _barsSinceBreakout = -1;

        // Regime for the most recently processed bar (published
        // state, mirroring ATRSmooth's Runtime.Position pattern).
        private double _regime;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="DarvasBoxReferenceSource"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Darvas box parameters. Must not be null.
        /// </param>
        public DarvasBoxReferenceSource(DarvasBoxConfiguration configuration)
            : base(configuration)
        {
        }

        /// <summary>
        /// Gets the typed configuration for this source.
        /// </summary>
        public new DarvasBoxConfiguration Configuration =>
            (DarvasBoxConfiguration)base.Configuration;

        /// <summary>
        /// Gets the upper boundary of the current (most recently
        /// confirmed) box, or <see cref="double.NaN"/> before the
        /// first confirmation.
        /// </summary>
        public double Upper => _top;

        /// <summary>
        /// Gets the lower boundary of the current box, or
        /// <see cref="double.NaN"/> before the first confirmation.
        /// </summary>
        public double Lower => _bottom;

        /// <summary>
        /// Gets a value indicating whether a box has been confirmed
        /// (at least one confirmation event has occurred).
        /// </summary>
        public bool HasBox => !double.IsNaN(_top);

        /// <summary>
        /// Gets the Darvas positional regime state for the most
        /// recently processed bar: +1 = close above the upper
        /// boundary, 0 = close inside the box (Lower &lt;= close &lt;=
        /// Upper; a real persistent Darvas state, NOT bearish), -1 =
        /// close below the lower boundary. 0 during warm-up (no box
        /// confirmed yet — uncommitted). This is the source-defined
        /// signed regime state consumed by generic reversal
        /// detection: a strict change of this value between
        /// consecutive bars is a Darvas reversal, which includes
        /// breakout (0 → +1) and return-to-box (+1 → 0) transitions.
        /// </summary>
        public override double Regime => _regime;

        /// <inheritdoc />
        public override void Reset()
        {
            base.Reset();
            _nh = double.NaN;
            _top = double.NaN;
            _bottom = double.NaN;
            _barsSinceBreakout = -1;
            _regime = 0.0;
            _snapshotIndex = int.MinValue;
        }

        /// <inheritdoc />
        protected override double ComputeReference(EngineContext context, int index)
        {
            var cfg = Configuration;
            var md = context.MarketData!;

            //--------------------------------------------------
            // Re-tick handling (identical pattern to ATRSmooth)
            //--------------------------------------------------

            if (_snapshotIndex == index)
            {
                // Re-tick: restore the state captured at the start
                // of this bar so the barssince counter / held values
                // are recomputed from a clean baseline.
                _nh = _snapshotNh;
                _top = _snapshotTop;
                _bottom = _snapshotBottom;
                _barsSinceBreakout = _snapshotBarsSince;
                _regime = _snapshotRegime;
            }
            else
            {
                // First call for this bar: snapshot the state at the
                // end of the previous bar (or initial state when
                // index == 0).
                _snapshotNh = _nh;
                _snapshotTop = _top;
                _snapshotBottom = _bottom;
                _snapshotBarsSince = _barsSinceBreakout;
                _snapshotRegime = _regime;
                _snapshotIndex = index;
            }

            //--------------------------------------------------
            // Inputs at current index
            //--------------------------------------------------

            double high = md.High[index];
            double low = md.Low[index];
            double close = md.Close[index];

            //--------------------------------------------------
            // Rolling windows (Pine highest/lowest — current bar
            // included; na until the window is full).
            //--------------------------------------------------

            // k1 = highest(high, boxp) at the PREVIOUS bar (k1[1]).
            double k1Prev = Highest(md.High, index - 1, cfg.Length);

            // k2 = highest(high, boxp - 1), k3 = highest(high, boxp - 2)
            // at the current bar.
            double k2 = Highest(md.High, index, cfg.Length - 1);
            double k3 = Highest(md.High, index, cfg.Length - 2);

            // LL = lowest(low, boxp) at the current bar.
            double ll = Lowest(md.Low, index, cfg.Length);

            //--------------------------------------------------
            // Breakout / barssince / valuewhen state machine
            // (exact transcription of the canonical algorithm)
            //--------------------------------------------------

            // breakout = high > k1[1]; false when k1[1] is na.
            bool breakout = high > k1Prev;

            if (breakout)
            {
                _nh = high;             // valuewhen(high > k1[1], high, 0)
                _barsSinceBreakout = 0;
            }
            else if (_barsSinceBreakout >= 0)
            {
                _barsSinceBreakout++;
            }
            // else: -1 sentinel — breakout never fired; can never
            // equal boxp - 2, matching barssince(never-true).

            // box1 = k3 < k2; false when either is na.
            bool box1 = k3 < k2;

            // Confirmation: barssince == boxp - 2 AND box1.
            if (_barsSinceBreakout == cfg.Length - 2 && box1)
            {
                _top = _nh;             // valuewhen(confirm, NH, 0)
                _bottom = ll;           // valuewhen(confirm, LL, 0)
            }

            //--------------------------------------------------
            // Regime (positional state vs the current box)
            //--------------------------------------------------

            _regime = double.IsNaN(_top)
                ? 0.0                    // warm-up: uncommitted
                : ComputeRegime(_top, _bottom, close);

            //--------------------------------------------------
            // Publish the measurement level
            //--------------------------------------------------

            if (double.IsNaN(_top))
            {
                // Warm-up: no box confirmed yet. Publish the close so
                // the measurement level is always a finite scalar.
                return close;
            }

            return (_top + _bottom) * 0.5;
        }

        /// <summary>
        /// Computes the Darvas positional regime from the current box
        /// boundaries and the current close:
        /// +1 (close &gt; Upper), 0 (Lower &lt;= close &lt;= Upper,
        /// exact boundaries inside), -1 (close &lt; Lower).
        /// </summary>
        /// <param name="top">Current box upper boundary.</param>
        /// <param name="bottom">Current box lower boundary.</param>
        /// <param name="close">Current bar close.</param>
        /// <returns>+1, 0, or -1 per the Darvas regime contract.</returns>
        private static double ComputeRegime(double top, double bottom, double close)
        {
            if (close > top)
                return 1.0;

            if (close < bottom)
                return -1.0;

            return 0.0; // Lower <= close <= Upper (exact boundaries inside)
        }

        /// <summary>
        /// Pine <c>highest(series, n)</c>: rolling max over the last n
        /// bars ending at <paramref name="endIndex"/> inclusive;
        /// <see cref="double.NaN"/> when fewer than n bars exist
        /// (na until the window is full).
        /// </summary>
        private static double Highest(IPriceSeries series, int endIndex, int n)
        {
            if (n < 1 || endIndex < n - 1)
                return double.NaN;

            double max = series[endIndex - n + 1];
            for (int i = endIndex - n + 2; i <= endIndex; i++)
            {
                double v = series[i];
                if (v > max)
                    max = v;
            }

            return max;
        }

        /// <summary>
        /// Pine <c>lowest(series, n)</c>: rolling min over the last n
        /// bars ending at <paramref name="endIndex"/> inclusive;
        /// <see cref="double.NaN"/> when fewer than n bars exist.
        /// </summary>
        private static double Lowest(IPriceSeries series, int endIndex, int n)
        {
            if (n < 1 || endIndex < n - 1)
                return double.NaN;

            double min = series[endIndex - n + 1];
            for (int i = endIndex - n + 2; i <= endIndex; i++)
            {
                double v = series[i];
                if (v < min)
                    min = v;
            }

            return min;
        }
    }
}

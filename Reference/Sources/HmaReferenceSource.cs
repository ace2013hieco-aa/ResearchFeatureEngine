using System;

using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Reference.Configuration;

namespace ResearchFeatureEngine.Reference.Sources
{
    /// <summary>
    /// Reference source implementing the Hull Moving Average (HMA) of close.
    ///
    /// Canonical definition (period = P):
    /// <code>
    ///   WMA1   = WMA(close, max(1, floor(P/2)))
    ///   WMA2   = WMA(close, P)
    ///   RawHMA = 2 * WMA1 - WMA2
    ///   HMA    = WMA(RawHMA, max(1, floor(sqrt(P))))
    /// </code>
    /// where WMA(x, n) at bar t is the linearly weighted moving average
    /// over the last n bars (oldest weight 1, newest weight n):
    /// <code>
    ///   WMA = sum_{j=0..n-1} (j+1) * x[t-n+1+j] / (n*(n+1)/2)
    /// </code>
    ///
    /// <para>
    /// <b>Warm-up.</b> Every WMA stage requires a FULL window, so RawHMA
    /// is valid from index P-1 and HMA — a WMA over P3 consecutive
    /// RawHMA values — is NaN until
    /// <c>index &gt;= P + floor(sqrt(P)) - 2</c>. During warm-up
    /// <see cref="ComputeReference"/> returns the current close (the
    /// established pipeline contract keeps the published measurement
    /// level finite, matching ATRSmooth2/DarvasBox), while
    /// <see cref="Reference.Runtime.ReferenceRuntime.Hma"/> stays NaN.
    /// Consumers that need the genuine HMA value (e.g. the
    /// dual-reference research features) must read
    /// <c>Runtime.Hma</c> and remain unavailable until it is genuinely
    /// valid — the close fallback is NEVER a valid HMA observation.
    /// </para>
    ///
    /// <para>
    /// <b>Regime (slope).</b> +1 = HMA strictly above the previous
    /// HMA (rising), -1 = strictly below (falling), 0 = equal (flat) or
    /// warm-up (no previous valid HMA).
    /// </para>
    ///
    /// <para>
    /// <b>Implementation.</b> DIRECT window recomputation from market
    /// data on every bar — the same convention as
    /// <see cref="DarvasBoxReferenceSource"/>'s highest/lowest window
    /// scans. There is NO incremental WMA state: each WMA is evaluated
    /// over its exact window with a fixed oldest-to-newest summation
    /// order, so the output is deterministic in bar order, cannot
    /// drift, and matches an independent direct-definition oracle
    /// bit-for-bit (pinned by
    /// <c>Tests/Reference/HmaReferenceSourceGoldenTests.cs</c>).
    /// Per-bar cost is O(sqrt(P) * (P/2 + P)) = O(P^1.5) multiply-adds
    /// (96 ops at the default P=16) with zero allocations. The only
    /// persistent state is the previous HMA value for the slope, which
    /// is snapshot/restored so live re-ticks on the same bar are
    /// idempotent.
    /// </para>
    /// </summary>
    public sealed class HmaReferenceSource : ReferenceSourceBase
    {
        // ---------------------------------------------------------
        // Re-tick snapshot
        // ---------------------------------------------------------
        // Live streaming consumers (cTrader indicator) re-call Update()
        // for the same bar index as ticks arrive on the still-forming
        // bar. The only mutable state carried between bars is the
        // previous HMA (for the slope) and the published runtime
        // values. On the first call for a bar we snapshot them; on a
        // re-tick we restore them and recompute from the latest
        // market data. The computation itself is stateless (direct
        // window recompute), so this fully determines the output.

        private double _snapshotPreviousHma;
        private double _snapshotHma;
        private double _snapshotPosition;
        private int _snapshotIndex = int.MinValue;

        private int _p1;  // max(1, P/2)
        private int _p2;  // P
        private int _p3;  // max(1, floor(sqrt(P)))
        private double _p3WeightSum;

        private double _previousHma = double.NaN;

        /// <summary>
        /// Gets the HMA slope regime for the most recently processed
        /// index: +1 = rising (HMA &gt; previous HMA), -1 = falling
        /// (HMA &lt; previous HMA), 0 = flat (equal) or warm-up.
        /// </summary>
        public override double Regime => Runtime.Position;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="HmaReferenceSource"/> class.
        /// </summary>
        /// <param name="configuration">
        /// HMA parameters. Must not be null.
        /// </param>
        public HmaReferenceSource(HmaConfiguration configuration)
            : base(configuration)
        {
            int period = Configuration.Period;

            _p1 = Math.Max(1, period / 2);
            _p2 = period;
            _p3 = Math.Max(1, (int)Math.Sqrt(period));

            _p3WeightSum = _p3 * (_p3 + 1) / 2.0;
        }

        /// <summary>
        /// Gets the typed configuration for this source.
        /// </summary>
        public new HmaConfiguration Configuration =>
            (HmaConfiguration)base.Configuration;

        /// <inheritdoc />
        public override void Reset()
        {
            base.Reset();
            _previousHma = double.NaN;
            Runtime.Position = 0.0;
            Runtime.Hma = double.NaN;
            _snapshotIndex = int.MinValue;
        }

        /// <inheritdoc />
        protected override double ComputeReference(EngineContext context, int index)
        {
            var md = context.MarketData!;

            //--------------------------------------------------
            // Re-tick handling (identical pattern to ATRSmooth)
            //--------------------------------------------------

            if (_snapshotIndex == index)
            {
                // Re-tick: restore the state captured at the start of
                // this bar so the slope is recomputed from a clean
                // baseline.
                _previousHma = _snapshotPreviousHma;
                Runtime.Hma = _snapshotHma;
                Runtime.Position = _snapshotPosition;
            }
            else
            {
                // First call for this bar: snapshot the state at the
                // end of the previous bar (or initial state when
                // index == 0).
                _snapshotPreviousHma = _previousHma;
                _snapshotHma = Runtime.Hma;
                _snapshotPosition = Runtime.Position;
                _snapshotIndex = index;
            }

            //--------------------------------------------------
            // HMA = WMA(RawHMA, P3), direct window recompute
            //--------------------------------------------------

            double close = md.Close[index];
            double hma = ComputeHma(md.Close, index);

            //--------------------------------------------------
            // Regime (slope)
            //--------------------------------------------------

            double regime;
            if (double.IsNaN(hma) || double.IsNaN(_previousHma))
            {
                regime = 0.0; // warm-up: uncommitted
            }
            else if (hma > _previousHma)
            {
                regime = 1.0; // rising
            }
            else if (hma < _previousHma)
            {
                regime = -1.0; // falling
            }
            else
            {
                regime = 0.0; // flat
            }

            _previousHma = double.IsNaN(hma) ? _previousHma : hma;

            Runtime.Hma = hma;
            Runtime.Position = regime;

            // During warm-up, return close so the pipeline's published
            // measurement level stays finite (the established source
            // contract — matching ATRSmooth2/DarvasBox). Runtime.Hma
            // remains NaN so consumers can distinguish the fallback
            // from a genuinely valid HMA value.
            if (double.IsNaN(hma))
            {
                return close;
            }

            return hma;
        }

        /// <summary>
        /// Computes the canonical HMA at <paramref name="index"/>
        /// directly from the close series:
        /// WMA over the last P3 RawHMA values, each RawHMA(k) being
        /// 2*WMA(close,k,P1) - WMA(close,k,P2).
        /// </summary>
        /// <param name="close">Close price series.</param>
        /// <param name="index">Zero-based bar index.</param>
        /// <returns>
        /// The HMA value, or <see cref="double.NaN"/> while any stage's
        /// window is not yet full (index &lt; P2 + P3 - 2).
        /// </returns>
        private double ComputeHma(IPriceSeries close, int index)
        {
            // All WMA windows must be full:
            //  * RawHMA(k) requires k >= P2 - 1 (WMA2 window full;
            //    P1 <= P2 implies the WMA1 window is full too).
            //  * the outer WMA requires index - P3 + 1 >= P2 - 1.
            // Combined: index >= P2 + P3 - 2.
            if (index < _p2 + _p3 - 2)
                return double.NaN;

            // Outer WMA over RawHMA, oldest-to-newest summation order
            // (weight 1 for the oldest RawHMA, P3 for the newest).
            double weighted = 0.0;
            for (int j = 0; j < _p3; j++)
            {
                int k = index - _p3 + 1 + j;

                double wma1 = Wma(close, k, _p1);
                double wma2 = Wma(close, k, _p2);
                double raw = 2.0 * wma1 - wma2;

                weighted += raw * (j + 1);
            }

            return weighted / _p3WeightSum;
        }

        /// <summary>
        /// Direct linearly weighted moving average over the last
        /// <paramref name="period"/> values ending at
        /// <paramref name="endIndex"/> inclusive (oldest weight 1,
        /// newest weight period). NaN when the window is not full.
        /// </summary>
        private static double Wma(
            IPriceSeries series,
            int endIndex,
            int period)
        {
            if (endIndex < period - 1)
                return double.NaN;

            double weighted = 0.0;
            for (int j = 0; j < period; j++)
            {
                weighted += series[endIndex - period + 1 + j] * (j + 1);
            }

            return weighted / (period * (period + 1) / 2.0);
        }
    }
}

using System;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference.Configuration;

namespace ResearchFeatureEngine.Reference.Sources
{
    /// <summary>
    /// Reference source that produces a smoothed equilibrium price as
    /// the average of:
    ///
    ///   1. A volume-weighted moving average of close price (VWMA)
    ///      over <see cref="ATRSmoothConfiguration.SmoothLength"/> bars.
    ///   2. A stateful ATR trailing stop using
    ///      <see cref="ATRSmoothConfiguration.AtrMultiplier"/> *
    ///      ATR(<see cref="ATRSmoothConfiguration.AtrPeriod"/>), where
    ///      ATR is an EMA of the True Range.
    ///
    /// Reference_t = ( VWMA_t + TrailingStop_t ) / 2.
    ///
    /// This is a platform-independent transcription of the cTrader
    /// reference indicator <c>AtrTrailingStopSmoothed</c>:
    ///
    ///   * ATR uses an EMA (alpha = 2 / (period + 1)) matching cTrader's
    ///     <c>MovingAverageType.Exponential</c>, not Wilder's RMA.
    ///   * True Range is the standard: max(H - L, |H - prevClose|, |L - prevClose|).
    ///   * The trailing stop is initialized at index 0 with
    ///     <c>prevStop = close[0]</c>; the position state is also
    ///     initialized to 0.
    ///   * VWMA falls back to close when the rolling volume sum is zero.
    ///   * The first bar of the trailing stop uses the "else" branch
    ///     (since prevClose == prevStop == close), so the first stop is
    ///     <c>close + nLoss</c>.
    ///
    /// State that persists between bars (per source instance):
    /// EMA of True Range, trailing stop, position, rolling VWMA sums.
    /// All state is reset by <see cref="Reset"/>.
    /// </summary>
    public sealed class ATRSmoothReferenceSource : ReferenceSourceBase
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ATRSmoothReferenceSource"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Algorithm parameters. Must not be null.
        /// </param>
        public ATRSmoothReferenceSource(ATRSmoothConfiguration configuration)
            : base(configuration)
        {
        }

        /// <summary>
        /// Gets the typed configuration for this source.
        /// </summary>
        public new ATRSmoothConfiguration Configuration =>
            (ATRSmoothConfiguration)base.Configuration;

        /// <inheritdoc />
        protected override double ComputeReference(EngineContext context, int index)
        {
            var cfg = Configuration;
            var md = context.MarketData!;

            //--------------------------------------------------
            // Inputs at current index
            //--------------------------------------------------

            double close = md.Close[index];
            double high = md.High[index];
            double low = md.Low[index];

            // Volume may be missing on some instruments / adapters.
            // A volume of zero falls through to the SumV==0 path below,
            // which produces vwma == close.
            double volume = md.Volume.Count > index ? md.Volume[index] : 0.0;

            //--------------------------------------------------
            // Step 1 — EMA of True Range (ATR)
            //--------------------------------------------------

            double trueRange;

            if (index == 0)
            {
                // First bar: no previous close; use H - L.
                trueRange = high - low;
            }
            else
            {
                double previousClose = md.Close[index - 1];

                trueRange = Math.Max(
                    high - low,
                    Math.Max(
                        Math.Abs(high - previousClose),
                        Math.Abs(low - previousClose)));
            }

            if (index == 0)
            {
                // Seed the EMA with the first True Range.
                Runtime.EmaTrueRange = trueRange;
            }
            else
            {
                double alpha = 2.0 / (cfg.AtrPeriod + 1.0);
                Runtime.EmaTrueRange =
                    alpha * trueRange + (1.0 - alpha) * Runtime.EmaTrueRange;
            }

            double atr = Runtime.EmaTrueRange;
            double nLoss = cfg.AtrMultiplier * atr;

            //--------------------------------------------------
            // Step 2 — ATR Trailing Stop
            //--------------------------------------------------

            double prevStop;
            double prevClose;
            double prevPos;

            if (index == 0)
            {
                // First bar: prime the trailing stop with the close.
                prevStop = close;
                prevClose = close;
                prevPos = 0.0;
            }
            else
            {
                prevStop = Runtime.TrailingStop;
                prevClose = md.Close[index - 1];
                prevPos = Runtime.Position;
            }

            double currentStop;

            if (close > prevStop && prevClose > prevStop)
                currentStop = Math.Max(prevStop, close - nLoss);
            else if (close < prevStop && prevClose < prevStop)
                currentStop = Math.Min(prevStop, close + nLoss);
            else if (close > prevStop)
                currentStop = close - nLoss;
            else
                currentStop = close + nLoss;

            //--------------------------------------------------
            // Step 3 — Position state
            //--------------------------------------------------

            double currentPos;

            if (prevClose < prevStop && close > prevStop)
                currentPos = 1.0;
            else if (prevClose > prevStop && close < prevStop)
                currentPos = -1.0;
            else
                currentPos = prevPos;

            //--------------------------------------------------
            // Step 4 — Rolling VWMA(close, SmoothLength)
            //--------------------------------------------------

            double pv = close * volume;

            if (index == 0)
            {
                Runtime.SumPV = pv;
                Runtime.SumV = volume;
            }
            else
            {
                if (index - cfg.SmoothLength >= 0)
                {
                    double oldClose = md.Close[index - cfg.SmoothLength];
                    double oldVolume = md.Volume.Count > index - cfg.SmoothLength
                        ? md.Volume[index - cfg.SmoothLength]
                        : 0.0;
                    double oldPV = oldClose * oldVolume;

                    Runtime.SumPV = Runtime.SumPV + pv - oldPV;
                    Runtime.SumV = Runtime.SumV + volume - oldVolume;
                }
                else
                {
                    Runtime.SumPV += pv;
                    Runtime.SumV += volume;
                }
            }

            double vwma = Runtime.SumV > 0.0
                ? Runtime.SumPV / Runtime.SumV
                : close;

            //--------------------------------------------------
            // Publish state to runtime for diagnostics
            //--------------------------------------------------

            Runtime.TrailingStop = currentStop;
            Runtime.Position = currentPos;

            //--------------------------------------------------
            // Step 5 — Final reference = average of the two series
            //--------------------------------------------------

            return (vwma + currentStop) * 0.5;
        }
    }
}

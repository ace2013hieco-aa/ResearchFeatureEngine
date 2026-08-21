using System;
using System.Collections.Generic;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Statistics.Models;
using ResearchFeatureEngine.Statistics.Runtime;
using ResearchFeatureEngine.Statistics.Validation;

namespace ResearchFeatureEngine.Statistics
{
    /// <summary>
    /// Executes the Statistics stage.
    ///
    /// Computes rolling statistics on raw close price (not normalized
    /// measurement) by design — the distribution of the normalized
    /// feature is a distinct concern for a downstream stage.
    ///
    /// Current-bar-inclusive semantics: the rolling window holds the
    /// last N CLOSED bars, and the live (still-forming) bar's latest
    /// close is appended to the observations on every tick so the
    /// published mean / std dev respond to the live bar exactly like
    /// every other stage of the pipeline (Reference, Distance, Scale,
    /// Normalization) and like the transcribed reference indicator
    /// <c>AtrTrailingStopSmoothed</c>, which include the current bar
    /// in all of their rolling sums.
    ///
    /// Live re-ticks: the committed window is never mutated by a
    /// re-tick on the same bar; only the held-aside live close is
    /// refreshed, so there is no double-counting and the statistics
    /// are recomputed from a stable closed-bar base + the latest live
    /// close. When the bar closes and a new bar opens, the final live
    /// close is committed to the window (rolling out the oldest
    /// closed bar), and the new bar becomes the live bar.
    /// </summary>
    public sealed class StatisticsEngine : EngineBase
    {
        private readonly StatisticsWindow _window;
        private readonly StatisticsValidator _validator;
        private readonly StatisticsPublisher _publisher;

        private readonly IReadOnlyList<IStatisticModel> _models;

        // Live-bar tracking. _lastSeenIndex is the highest bar index
        // processed so far; the bar at _lastSeenIndex is the current
        // live (still-forming) bar. _liveBarClose is its latest close
        // and is appended to (not committed into) the closed-bar
        // window for per-tick computation.
        private int _lastSeenIndex = -1;
        private double _liveBarClose;


        /// <summary>
        /// Initializes a new instance of the <see cref="StatisticsEngine"/> class.
        /// </summary>
        /// <param name="context">Shared execution context.</param>
        /// <param name="window">Rolling statistics window.</param>
        /// <param name="models">Registered statistic models.</param>
        public StatisticsEngine(
            EngineContext context,
            StatisticsWindow window,
            IEnumerable<IStatisticModel> models)
            : base("StatisticsEngine", context)
        {
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(models);

            _window = window;

            _validator = new StatisticsValidator();

            _publisher = new StatisticsPublisher(
                Context.Values.Statistics);

            _models = models as IReadOnlyList<IStatisticModel>
                      ?? new List<IStatisticModel>(models);
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();
            _lastSeenIndex = -1;
            _liveBarClose = 0.0;
            _window.Reset();

            // Reset published values to a clean state so the
            // engine matches a fresh build (no stale statistics
            // leaking across runs).
            var s = Context.Values.Statistics;
            {
                s.ObservationCount = 0;
                s.Location.Mean = 0.0;
                s.Location.Median = 0.0;
                s.Dispersion.Variance = 0.0;
                s.Dispersion.StandardDeviation = 0.0;
                s.Dispersion.MedianAbsoluteDeviation = 0.0;
                s.Range.Minimum = 0.0;
                s.Range.Maximum = 0.0;
                s.Range.Range = 0.0;
                s.Shape.Skewness = 0.0;
                s.Shape.Kurtosis = 0.0;
            }
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            int currentIndex = Context.CurrentIndex;

            //--------------------------------------------------
            // Live-bar handling
            //--------------------------------------------------
            // The committed rolling window holds CLOSED bars only.
            // When a new bar is observed (currentIndex > _lastSeenIndex),
            // the previously-live bar just closed; commit its FINAL close
            // (the last tick value held in _liveBarClose) to the window.
            // Re-ticks on the same bar (currentIndex == _lastSeenIndex)
            // do not touch the committed window — they only refresh
            // _liveBarClose below, and the live close is appended to the
            // observations at computation time. This gives responsive,
            // current-bar-inclusive statistics without double-counting.

            bool isNewBar = currentIndex > _lastSeenIndex;

            if (isNewBar && _lastSeenIndex >= 0)
            {
                // Commit the previously-live bar's final close to the
                // committed window. This is the value we held in
                // _liveBarClose; the most recent tick on that bar.
                _window.Add(_liveBarClose);
            }

            // Refresh the held-aside live-bar close (re-ticks
            // overwrite the previously-stored value; new bars
            // initialize it from the current tick). The market
            // data adapter is null only in null-guard tests.
            var md = Context.MarketData;
            if (md is not null &&
                currentIndex < md.Close.Count)
            {
                _liveBarClose = md.Close[currentIndex];
            }

            _lastSeenIndex = currentIndex;

            //--------------------------------------------------
            // Build the current observation set: committed closed
            // bars plus the live bar's latest close.
            //--------------------------------------------------
            // We avoid mutating the committed window for the live bar
            // by copying its contents and appending _liveBarClose.
            // Allocation is bounded by the window size and happens
            // once per bar (twice on a bar-open tick).

            double[] closed = _window.GetOrderedArray();

            int liveCount = closed.Length + 1;
            double[] observations = new double[liveCount];
            Array.Copy(closed, observations, closed.Length);
            observations[closed.Length] = _liveBarClose;

            Context.Values.Statistics.ObservationCount = liveCount;

            StatisticsInput input = new StatisticsInput(observations);

            _validator.Validate(input, _models);

            ReadOnlySpan<double> span = input.Observations.Span;

            foreach (IStatisticModel model in _models)
            {
                if (span.Length < model.MinimumObservationCount)
                    continue;

                double value = model.Compute(input);

                _publisher.Publish(
                    model.Type,
                    value);
            }
        }
    }
}

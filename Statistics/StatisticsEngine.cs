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
    /// Live-bar handling: the rolling window is kept over CLOSED
    /// bars only. When a new bar is seen, the previously-live bar's
    /// close is committed to the window. Re-ticks on the live bar
    /// update a held-aside value but do not modify the window, so
    /// the mean / std dev for the most-recent bar stays stable
    /// across ticks instead of flickering.
    /// </summary>
    public sealed class StatisticsEngine : EngineBase
    {
        private readonly StatisticsWindow _window;
        private readonly StatisticsValidator _validator;
        private readonly StatisticsPublisher _publisher;

        private readonly IReadOnlyList<IStatisticModel> _models;

        // Live-bar tracking. _lastSeenIndex is the highest bar index
        // processed so far; the bar at _lastSeenIndex is the current
        // live (still-forming) bar. _liveBarClose is its latest close.
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
                Context.Values!.Statistics);

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
            var s = Context.Values?.Statistics;
            if (s is not null)
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
            // The rolling window holds CLOSED bars only. When a new
            // bar is observed (currentIndex > _lastSeenIndex), the
            // previously-live bar (at _lastSeenIndex) just closed;
            // commit its close to the window. Re-ticks on the same
            // bar (currentIndex == _lastSeenIndex) only refresh
            // _liveBarClose and do not touch the window, so the
            // published mean / std dev stay stable across ticks.

            bool isNewBar = currentIndex > _lastSeenIndex;

            if (isNewBar && _lastSeenIndex >= 0)
            {
                // Commit the previously-live bar's close to the
                // window. This is the value we held in
                // _liveBarClose; the most recent tick on that bar.
                _window.Add(_liveBarClose);
            }

            // Update the held-aside live-bar close (re-ticks
            // overwrite the previously-stored value; new bars
            // initialize it from the current tick).
            if (Context.MarketData is not null &&
                currentIndex < Context.MarketData.Close.Count)
            {
                _liveBarClose = Context.MarketData.Close[currentIndex];
            }

            _lastSeenIndex = currentIndex;

            Context.Values!.Statistics.ObservationCount = _window.Count;

            //--------------------------------------------------
            // Not-enough-data guard
            //--------------------------------------------------
            // On the very first bar of a live stream there are no
            // closed bars yet, so the rolling window is empty.
            // Publishing statistics in that state is meaningless
            // (and would require the validator to accept empty
            // input, weakening a useful invariant). We leave the
            // statistic values at their default and skip model
            // execution. Once a second bar opens, the first bar's
            // close gets committed and the window starts filling.

            if (_window.Count == 0)
            {
                return;
            }

            //--------------------------------------------------
            // Build immutable input
            //--------------------------------------------------

            StatisticsInput input =
                new StatisticsInput(_window.GetOrderedArray());

            //--------------------------------------------------
            // Validate
            //--------------------------------------------------

            _validator.Validate(input, _models);

            //--------------------------------------------------
            // Execute models
            //--------------------------------------------------

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

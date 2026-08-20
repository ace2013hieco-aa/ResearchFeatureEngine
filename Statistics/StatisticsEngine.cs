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
    /// </summary>
    public sealed class StatisticsEngine : EngineBase
    {
        private readonly StatisticsWindow _window;
        private readonly StatisticsValidator _validator;
        private readonly StatisticsPublisher _publisher;

        private readonly IReadOnlyList<IStatisticModel> _models;

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
        protected override void OnUpdate()
        {
            //--------------------------------------------------
            // Update rolling window
            //--------------------------------------------------

            if (Context.MarketData is not null &&
                Context.CurrentIndex < Context.MarketData.Close.Count)
            {
                _window.Add(Context.MarketData.Close[Context.CurrentIndex]);
                Context.Values!.Statistics.ObservationCount = _window.Count;
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

using System.Collections.Generic;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Statistics;
using ResearchFeatureEngine.Statistics.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Statistics
{
    /// <summary>
    /// Source-dependent skewness/kurtosis integration tests
    /// (LogReturn / SimpleReturn).
    ///
    /// These tests exercise the <see cref="StatisticsSource"/>
    /// materialization plumbing in the Statistics stage and are
    /// therefore kept OUT of the skewness/kurtosis commit, which is
    /// self-contained and does not include the StatisticsSource work.
    /// They live in the working tree and travel with the pending
    /// StatisticsSource commit.
    ///
    /// Expected G1/G2 values are hard-coded from the INDEPENDENT
    /// Python two-pass reference (see SkewnessKurtosisModelTests).
    /// </summary>
    public sealed class SkewnessKurtosisSourceTests
    {
        private static (EngineContext context, StatisticsEngine engine) Build(
            double[] closes,
            StatisticsSource source)
        {
            var context = TestEngineContext.Create(closes);

            var models = new List<IStatisticModel>
            {
                new SkewnessModel(),
                new KurtosisModel()
            };

            var engine = new StatisticsEngine(
                context,
                new StatisticsWindow(5),
                models,
                source);

            return (context, engine);
        }

        private static void Step(
            EngineContext context, StatisticsEngine engine, int index)
        {
            context.SetIndex(index);
            engine.Update();
        }

        // ---------------------------------------------------------
        // J. LogReturn integration: independently calculated
        //    log(C_t / C_{t-1}) as the reference.
        // ---------------------------------------------------------

        [Fact]
        public void LogReturnSource_FeedsShapeModels_Correctly()
        {
            var closes = new[] { 100.0, 105.0, 98.0, 112.0, 104.0, 109.0 };
            var (context, engine) = Build(closes, StatisticsSource.LogReturn);

            // bar 0: no return exists (first bar).
            Step(context, engine, 0);
            Assert.Equal(0, context.Values.Statistics.ObservationCount);

            // bar 1: [ln(105/100)] n=1 ... bar 2: n=2 -> nothing.
            Step(context, engine, 1);
            Step(context, engine, 2);
            Assert.Equal(2, context.Values.Statistics.ObservationCount);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Skewness);

            // bar 3: n=3. Log returns
            //   [ln(105/100), ln(98/105), ln(112/98)]
            //   G1 = -0.4815785439725818
            Step(context, engine, 3);
            Assert.Equal(-0.4815785439725818,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(0.0, context.Values.Statistics.Shape.Kurtosis);

            // bar 4: n=4. Log returns
            //   [ln(105/100), ln(98/105), ln(112/98), ln(104/112)]
            //   G1 = 0.5798132793158912, G2 = -2.730307325353534
            Step(context, engine, 4);
            Assert.Equal(0.5798132793158912,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-2.730307325353534,
                context.Values.Statistics.Shape.Kurtosis, 11);

            // bar 5: window full (closed [100,105,98,112,104]) +
            // live 109 -> 5 log returns
            //   [ln(105/100), ln(98/105), ln(112/98), ln(104/112), ln(109/104)]
            //   G1 = 0.13757713981338093, G2 = -1.6322135096411294
            Step(context, engine, 5);
            Assert.Equal(5, context.Values.Statistics.ObservationCount);
            Assert.Equal(0.13757713981338093,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-1.6322135096411294,
                context.Values.Statistics.Shape.Kurtosis, 11);
        }

        // ---------------------------------------------------------
        // L. SimpleReturn compatibility: models remain source-blind.
        // ---------------------------------------------------------

        [Fact]
        public void SimpleReturnSource_FeedsShapeModels_Correctly()
        {
            var closes = new[] { 100.0, 101.0, 104.0, 103.0, 106.0, 105.0 };
            var (context, engine) = Build(closes, StatisticsSource.SimpleReturn);

            // bar 5: simple returns
            //   [1/100, 3/101, -1/104, 3/103, -1/106]
            //   G1 = -0.0023327159520130066, G2 = -2.997599347393115
            for (int i = 0; i <= 5; i++)
                Step(context, engine, i);

            Assert.Equal(5, context.Values.Statistics.ObservationCount);
            Assert.Equal(-0.0023327159520130066,
                context.Values.Statistics.Shape.Skewness, 11);
            Assert.Equal(-2.997599347393115,
                context.Values.Statistics.Shape.Kurtosis, 11);

            // The two return sources differ: the same closes produce
            // different shape values under SimpleReturn vs LogReturn,
            // proving the models receive the materialized observations
            // and never select the source themselves.
            var (contextLog, engineLog) = Build(closes, StatisticsSource.LogReturn);
            for (int i = 0; i <= 5; i++)
                Step(contextLog, engineLog, i);

            Assert.NotEqual(
                context.Values.Statistics.Shape.Skewness,
                contextLog.Values.Statistics.Shape.Skewness);
        }
    }
}

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Tests;
using Xunit;

namespace ResearchFeatureEngine.Tests.Composition
{
    /// <summary>
    /// Verifies that <see cref="ResearchFeatureEngine.ProcessAt(int)"/>
    /// processes the supplied index without advancing the shared
    /// index. This is the property the live cTrader indicator
    /// depends on: re-ticks on the same bar re-process that bar
    /// instead of drifting past it.
    /// </summary>
    public sealed class ProcessAtTests
    {
        [Fact]
        public void ProcessAt_DoesNotAdvanceIndex_SoReTicksStayOnBar()
        {
            // Use the 20-bar known dataset so ProcessAt can be called
            // for any index 0..19.
            EngineConfiguration cfg = TestConfigurationFactory.CreateKnownDataset();
            ResearchFeatureEngine engine = new ResearchFeatureEngineBuilder(cfg).Build();

            // Simulate cTrader calling Calculate(0) for the first bar.
            engine.ProcessAt(0);
            Assert.Equal(0, engine.Context.CurrentIndex);

            // cTrader calls Calculate(0) again on the next tick.
            engine.ProcessAt(0);
            Assert.Equal(0, engine.Context.CurrentIndex);

            // cTrader calls Calculate(0) once more (third tick on the same bar).
            engine.ProcessAt(0);
            Assert.Equal(0, engine.Context.CurrentIndex);

            // Bar closes, new bar opens, cTrader calls Calculate(1).
            engine.ProcessAt(1);
            Assert.Equal(1, engine.Context.CurrentIndex);

            // Then Calculate(2).
            engine.ProcessAt(2);
            Assert.Equal(2, engine.Context.CurrentIndex);
        }

        [Fact]
        public void Update_AutoAdvances_ForBacktestHarnessPattern()
        {
            // The backtest/harness pattern relies on Update() advancing
            // the index by 1 each call. Preserve that behavior.
            EngineConfiguration cfg = TestConfigurationFactory.CreateKnownDataset();
            ResearchFeatureEngine engine = new ResearchFeatureEngineBuilder(cfg).Build();

            Assert.Equal(0, engine.Context.CurrentIndex);
            engine.Update();
            Assert.Equal(1, engine.Context.CurrentIndex);
            engine.Update();
            Assert.Equal(2, engine.Context.CurrentIndex);
            engine.Update();
            Assert.Equal(3, engine.Context.CurrentIndex);
        }

        [Fact]
        public void ProcessAt_RepeatedCalls_ProduceIdenticalOutput()
        {
            // Re-ticks on the same bar with identical data should
            // produce identical outputs (determinism).
            EngineConfiguration cfg = TestConfigurationFactory.CreateKnownDataset();
            ResearchFeatureEngine engine = new ResearchFeatureEngineBuilder(cfg).Build();

            engine.ProcessAt(5);
            double ref5_first  = engine.Values.Reference.Price;
            double dir5_first  = engine.Values.Distance.DirectionalExtension;
            double abs5_first  = engine.Values.Distance.AbsoluteExtension;
            double scale5_first = engine.Values.Scale.Scale;

            // Re-tick on bar 5 (same data).
            engine.ProcessAt(5);
            Assert.Equal(ref5_first,  engine.Values.Reference.Price, 12);
            Assert.Equal(dir5_first,  engine.Values.Distance.DirectionalExtension, 12);
            Assert.Equal(abs5_first,  engine.Values.Distance.AbsoluteExtension, 12);
            Assert.Equal(scale5_first, engine.Values.Scale.Scale, 12);

            // Confirm we did NOT drift to bar 6.
            Assert.Equal(5, engine.Context.CurrentIndex);
        }
    }
}

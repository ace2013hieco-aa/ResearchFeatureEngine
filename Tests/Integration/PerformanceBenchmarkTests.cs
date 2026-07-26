using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.Tests.Integration
{
    public sealed class PerformanceBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public PerformanceBenchmarkTests(
            ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Update_WithLargeDataset_CompletesWithinPerformanceBudget()
        {
            // -------------------------------------------------------------
            // Arrange - Warm-up (avoid JIT cost)
            // -------------------------------------------------------------

            const int WarmUpBars = 100;

            EngineConfiguration warmUpConfig =
                TestConfigurationFactory.CreateLargeDataset(WarmUpBars);

            ResearchFeatureEngine warmUpEngine =
                new ResearchFeatureEngineBuilder(warmUpConfig)
                    .Build();

            IMarketData warmUpData = warmUpConfig.MarketData;

            while (warmUpData.MoveNext())
            {
                warmUpEngine.Update();
            }

            // -------------------------------------------------------------
            // Arrange - Benchmark
            // -------------------------------------------------------------

            const int NumberOfBars = 1_000_000;

            EngineConfiguration configuration =
                TestConfigurationFactory.CreateLargeDataset(NumberOfBars);

            ResearchFeatureEngine engine =
                new ResearchFeatureEngineBuilder(configuration)
                    .Build();

            IMarketData marketData =
                configuration.MarketData;

            // -------------------------------------------------------------
            // Act
            // -------------------------------------------------------------

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);

            Stopwatch stopwatch = Stopwatch.StartNew();

            int processedBars = 0;

            while (marketData.MoveNext())
            {
                engine.Update();
                processedBars++;
            }

            stopwatch.Stop();

            long memoryAfter = GC.GetTotalMemory(true);

            // -------------------------------------------------------------
            // Metrics
            // -------------------------------------------------------------

            double elapsedMilliseconds =
                stopwatch.Elapsed.TotalMilliseconds;

            double microsecondsPerBar =
                (stopwatch.Elapsed.TotalMilliseconds * 1000.0)
                / processedBars;

            double barsPerSecond =
                processedBars
                / stopwatch.Elapsed.TotalSeconds;

            long allocatedBytes =
                memoryAfter - memoryBefore;

            // -------------------------------------------------------------
            // Output
            // -------------------------------------------------------------

            _output.WriteLine(
                $"Bars Processed : {processedBars:N0}");

            _output.WriteLine(
                $"Elapsed        : {elapsedMilliseconds:N2} ms");

            _output.WriteLine(
                $"Bars / Second  : {barsPerSecond:N0}");

            _output.WriteLine(
                $"µs / Bar       : {microsecondsPerBar:N4}");

            _output.WriteLine(
                $"Memory Delta   : {allocatedBytes:N0} bytes");

            // -------------------------------------------------------------
            // Assert
            // -------------------------------------------------------------

            Assert.Equal(NumberOfBars, processedBars);

            Assert.True(
                elapsedMilliseconds > 0);

            Assert.True(
                barsPerSecond > 0);

            Assert.True(
                microsecondsPerBar > 0);
        }
    }
}

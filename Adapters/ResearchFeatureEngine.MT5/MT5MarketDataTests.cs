// Static validation test for the MT5 adapter.
//
// Validates that MT5MarketData (the IMarketData adapter) correctly:
// 1. Constructs from MqlRates[]
// 2. Exposes all IMarketData properties (Open, High, Low, Close, Volume, Time, Count)
// 3. Advances through bars via MoveNext()
// 4. Produces deterministic output
//
// For runtime MT5 validation: install MetaTrader 5 terminal and run
// RFEMT5Indicator.mq5 in Strategy Tester with EURUSD M1 data.

using System;
using Xunit;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Adapters;

#if MT5_STUB
using MetaTrader5;
#endif

namespace ResearchFeatureEngine.Tests.Adapters.MT5
{
    public class MT5MarketDataTests
    {
        private static MqlRates[] CreateTestData(int n)
        {
            var rates = new MqlRates[n];
            for (int i = 0; i < n; i++)
            {
                rates[i] = new MqlRates
                {
                    time = 1609459200 + i * 60,
                    open = 1.1000 + i * 0.0001,
                    high = 1.1010 + i * 0.0001,
                    low = 1.0990 + i * 0.0001,
                    close = 1.1005 + i * 0.0001,
                    tick_volume = 100 + i
                };
            }
            return rates;
        }

        [Fact]
        public void Construct_ValidRates_SetsAllProperties()
        {
            var rates = CreateTestData(10);
            var md = new MT5MarketData(rates);

            Assert.Equal(10, md.Count);
            Assert.Equal(10, md.Open.Count);
            Assert.Equal(10, md.High.Count);
            Assert.Equal(10, md.Low.Count);
            Assert.Equal(10, md.Close.Count);
            Assert.Equal(10, md.Volume.Count);
            Assert.Equal(10, md.Time.Count);
        }

        [Fact]
        public void Construct_ValidRates_OpensMatchInput()
        {
            var rates = CreateTestData(5);
            var md = new MT5MarketData(rates);

            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(rates[i].open, md.Open[i]);
                Assert.Equal(rates[i].high, md.High[i]);
                Assert.Equal(rates[i].low, md.Low[i]);
                Assert.Equal(rates[i].close, md.Close[i]);
                Assert.Equal((double)rates[i].tick_volume, md.Volume[i]);
            }
        }

        [Fact]
        public void Construct_ValidRates_TimesAreCorrect()
        {
            var rates = CreateTestData(3);
            var md = new MT5MarketData(rates);

            for (int i = 0; i < 3; i++)
            {
                var expected = DateTimeOffset.FromUnixTimeSeconds(
                    rates[i].time).DateTime;
                Assert.Equal(expected, md.Time[i]);
            }
        }

        [Fact]
        public void Construct_EmptyRates_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new MT5MarketData(new MqlRates[0]));
        }

        [Fact]
        public void Construct_NullRates_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new MT5MarketData((MqlRates[])null!));
        }

        [Fact]
        public void MoveNext_AdvancesThroughAllBars()
        {
            var rates = CreateTestData(5);
            var md = new MT5MarketData(rates);

            int count = 0;
            while (md.MoveNext())
            {
                count++;
            }

            Assert.Equal(5, count);
        }

        [Fact]
        public void MoveNext_AfterExhausted_ReturnsFalse()
        {
            var rates = CreateTestData(3);
            var md = new MT5MarketData(rates);

            for (int i = 0; i < 3; i++)
                Assert.True(md.MoveNext());

            Assert.False(md.MoveNext());
        }

        [Fact]
        public void Construct_DeterministicOutput_SameInputSameOutput()
        {
            var rates = CreateTestData(20);

            var md1 = new MT5MarketData(rates);
            var md2 = new MT5MarketData(rates);

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(md1.Close[i], md2.Close[i]);
                Assert.Equal(md1.Open[i], md2.Open[i]);
            }
        }

        [Fact]
        public void Construct_IMarketData_ConformsToContract()
        {
            var rates = CreateTestData(10);
            IMarketData md = new MT5MarketData(rates);

            Assert.Equal(10, md.Count);
            Assert.Equal(10, md.Open.Count);
            Assert.True(md.MoveNext());
        }
    }
}

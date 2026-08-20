using System;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    public sealed class ATRSmoothConfigurationTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ExposesThem()
        {
            var cfg = new ATRSmoothConfiguration(
                atrPeriod: 16,
                atrMultiplier: 5.1,
                smoothLength: 100);

            Assert.Equal(16, cfg.AtrPeriod);
            Assert.Equal(5.1, cfg.AtrMultiplier);
            Assert.Equal(100, cfg.SmoothLength);
            Assert.Equal("ATRSmoothReferenceSource", cfg.SourceName);
        }

        [Fact]
        public void Constructor_Defaults_MatchReferenceIndicator()
        {
            var cfg = new ATRSmoothConfiguration();

            Assert.Equal(ATRSmoothConfiguration.DefaultAtrPeriod, cfg.AtrPeriod);
            Assert.Equal(ATRSmoothConfiguration.DefaultAtrMultiplier, cfg.AtrMultiplier);
            Assert.Equal(ATRSmoothConfiguration.DefaultSmoothLength, cfg.SmoothLength);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Constructor_WithNonPositiveAtrPeriod_Throws(int period)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ATRSmoothConfiguration(atrPeriod: period));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1.0)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NaN)]
        public void Constructor_WithNonPositiveOrNonFiniteAtrMultiplier_Throws(double multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ATRSmoothConfiguration(atrMultiplier: multiplier));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Constructor_WithNonPositiveSmoothLength_Throws(int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ATRSmoothConfiguration(smoothLength: length));
        }

        [Fact]
        public void Equals_SameValues_TrueAndHashEqual()
        {
            var a = new ATRSmoothConfiguration(16, 5.1, 100);
            var b = new ATRSmoothConfiguration(16, 5.1, 100);

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentPeriod_False()
        {
            var a = new ATRSmoothConfiguration(16, 5.1, 100);
            var b = new ATRSmoothConfiguration(14, 5.1, 100);

            Assert.False(a.Equals(b));
        }
    }
}

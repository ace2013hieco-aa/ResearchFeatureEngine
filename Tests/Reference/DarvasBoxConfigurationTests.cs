using System;

using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Validation tests for <see cref="DarvasBoxConfiguration"/>.
    /// Structural constraint: length >= 3 (the k3 window
    /// highest(high, length-2) must contain at least one bar) — the
    /// same constraint as the canonical Backtest-Engine DarvasBox5.
    /// </summary>
    public sealed class DarvasBoxConfigurationTests
    {
        [Fact]
        public void Constructor_WithValidLength_ExposesIt()
        {
            var cfg = new DarvasBoxConfiguration(length: 5);

            Assert.Equal(5, cfg.Length);
            Assert.Equal("DarvasBoxReferenceSource", cfg.SourceName);
        }

        [Fact]
        public void Constructor_Default_IsCanonicalPineDefault()
        {
            var cfg = new DarvasBoxConfiguration();

            Assert.Equal(5, cfg.Length);
            Assert.Equal(DarvasBoxConfiguration.DefaultLength, cfg.Length);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Constructor_WithLengthBelowMinimum_Throws(int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DarvasBoxConfiguration(length: length));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(500)]
        public void Constructor_WithStructurallyValidLength_DoesNotThrow(int length)
        {
            var cfg = new DarvasBoxConfiguration(length: length);
            Assert.Equal(length, cfg.Length);
        }

        [Fact]
        public void Equals_SameLength_TrueAndHashEqual()
        {
            var a = new DarvasBoxConfiguration(5);
            var b = new DarvasBoxConfiguration(5);

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentLength_False()
        {
            var a = new DarvasBoxConfiguration(5);
            var b = new DarvasBoxConfiguration(7);

            Assert.False(a.Equals(b));
        }

        [Fact]
        public void SourceName_IsDarvasBoxReferenceSource()
        {
            Assert.Equal(
                nameof(DarvasBoxReferenceSource),
                new DarvasBoxConfiguration().SourceName);
        }
    }
}

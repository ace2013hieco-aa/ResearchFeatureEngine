using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Tests for <see cref="HmaPriceAtrSmoothAlignmentModel"/>.
    /// </summary>
    public sealed class HmaPriceAtrSmoothAlignmentModelTests
    {
        private readonly HmaPriceAtrSmoothAlignmentModel _model = new HmaPriceAtrSmoothAlignmentModel();

        [Fact]
        public void Aligned_HmaAbovePriceAbove_ReturnsAligned()
        {
            // HMA > ATRSmooth AND Close > ATRSmooth
            var alignment = _model.Compute(hma: 105.0, atrSmooth: 100.0, close: 102.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Aligned, alignment);
        }

        [Fact]
        public void Aligned_HmaBelowPriceBelow_ReturnsAligned()
        {
            // HMA < ATRSmooth AND Close < ATRSmooth
            var alignment = _model.Compute(hma: 95.0, atrSmooth: 100.0, close: 98.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Aligned, alignment);
        }

        [Fact]
        public void Misaligned_HmaAbovePriceBelow_ReturnsMisaligned()
        {
            // HMA > ATRSmooth AND Close < ATRSmooth
            var alignment = _model.Compute(hma: 105.0, atrSmooth: 100.0, close: 98.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Misaligned, alignment);
        }

        [Fact]
        public void Misaligned_HmaBelowPriceAbove_ReturnsMisaligned()
        {
            // HMA < ATRSmooth AND Close > ATRSmooth
            var alignment = _model.Compute(hma: 95.0, atrSmooth: 100.0, close: 102.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Misaligned, alignment);
        }

        [Fact]
        public void Equality_HmaEqualsAtrSmooth_ReturnsUnavailable()
        {
            // HMA == ATRSmooth (exact equality)
            var alignment = _model.Compute(hma: 100.0, atrSmooth: 100.0, close: 102.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, alignment);
        }

        [Fact]
        public void Equality_CloseEqualsAtrSmooth_ReturnsUnavailable()
        {
            // Close == ATRSmooth (exact equality)
            var alignment = _model.Compute(hma: 105.0, atrSmooth: 100.0, close: 100.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, alignment);
        }

        [Fact]
        public void WarmUp_NaNHma_ReturnsUnavailable()
        {
            var alignment = _model.Compute(hma: double.NaN, atrSmooth: 100.0, close: 102.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, alignment);
        }

        [Fact]
        public void WarmUp_NaNAtrSmooth_ReturnsUnavailable()
        {
            var alignment = _model.Compute(hma: 105.0, atrSmooth: double.NaN, close: 102.0);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, alignment);
        }

        [Fact]
        public void WarmUp_NaNClose_ReturnsUnavailable()
        {
            var alignment = _model.Compute(hma: 105.0, atrSmooth: 100.0, close: double.NaN);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, alignment);
        }

        [Fact]
        public void WarmUp_AllNaN_ReturnsUnavailable()
        {
            var alignment = _model.Compute(hma: double.NaN, atrSmooth: double.NaN, close: double.NaN);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, alignment);
        }

        [Fact]
        public void StateTransitions_BarByBar_AllQuadrants()
        {
            // Simulate a sequence of bars with different HMA/Price/ATRSmooth relations
            var bar1 = _model.Compute(105.0, 100.0, 102.0); // Aligned (both above)
            var bar2 = _model.Compute(105.0, 100.0, 98.0);  // Misaligned (HMA above, Price below)
            var bar3 = _model.Compute(95.0, 100.0, 98.0);   // Aligned (both below)
            var bar4 = _model.Compute(95.0, 100.0, 102.0);  // Misaligned (HMA below, Price above)
            var bar5 = _model.Compute(100.0, 100.0, 102.0); // Unavailable (HMA == ATRSmooth)
            var bar6 = _model.Compute(105.0, 100.0, 100.0); // Unavailable (Close == ATRSmooth)

            Assert.Equal(HmaPriceAtrSmoothAlignment.Aligned, bar1);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Misaligned, bar2);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Aligned, bar3);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Misaligned, bar4);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, bar5);
            Assert.Equal(HmaPriceAtrSmoothAlignment.Unavailable, bar6);
        }

        [Fact]
        public void NotDerivedFromRollingMean_CurrentBarOnly()
        {
            // Alignment is computed per-bar independently, no rolling window
            // Verify by calling with same inputs multiple times
            var a1 = _model.Compute(105.0, 100.0, 102.0);
            var a2 = _model.Compute(105.0, 100.0, 102.0);
            var a3 = _model.Compute(105.0, 100.0, 102.0);

            Assert.Equal(a1, a2);
            Assert.Equal(a2, a3);
        }
    }
}
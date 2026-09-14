using System;

using ResearchFeatureEngine.Engines.Validation;
using ResearchFeatureEngine.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// M11.1 model-level golden oracle tests for the two HMA/ATRSmooth
    /// geometry measurements, hand-computed per the M11.0-ratified
    /// contracts.
    /// </summary>
    public sealed class HmaAtrSmoothGeometryModelTests
    {
        private readonly HmaAtrSmoothSeparationModel _separation =
            new HmaAtrSmoothSeparationModel();

        private readonly HmaAtrSmoothRelativeClosePositionModel _position =
            new HmaAtrSmoothRelativeClosePositionModel();

        // ---------------------------------------------------------
        // Separation: mathematical oracle
        // ---------------------------------------------------------

        [Fact]
        public void Separation_NormalPositive_Returns2Point5()
        {
            // HMA = 105, ATRSmooth = 100, Scale = 2 → S = 2.5
            double s = _separation.Compute(105.0, 100.0, 2.0);
            Assert.Equal(2.5, s, 12);
        }

        [Fact]
        public void Separation_Negative_InvertsSign()
        {
            // HMA below ATRSmooth: (95 - 100) / 2 = -2.5 — the exact
            // sign mirror of the positive oracle above.
            double s = _separation.Compute(95.0, 100.0, 2.0);
            Assert.Equal(-2.5, s, 12);
        }

        [Fact]
        public void Separation_HmaEqualsAtrSmooth_IsExactlyZero()
        {
            double s = _separation.Compute(100.0, 100.0, 2.0);
            Assert.Equal(0.0, s, 12);
        }

        [Fact]
        public void Separation_NaNHma_IsNaN()
        {
            Assert.True(double.IsNaN(_separation.Compute(double.NaN, 100.0, 2.0)));
        }

        [Fact]
        public void Separation_NaNAtrSmooth_IsNaN()
        {
            Assert.True(double.IsNaN(_separation.Compute(105.0, double.NaN, 2.0)));
        }

        [Fact]
        public void Separation_BothNaN_IsNaN()
        {
            Assert.True(double.IsNaN(_separation.Compute(double.NaN, double.NaN, 2.0)));
        }

        [Fact]
        public void Separation_NonPositiveScale_FailsClosed()
        {
            // scale <= 0 is never accepted as a valid input: the
            // Scale stage's contract guarantees it can never be
            // published, so the model fails closed (no division, no
            // epsilon, no alternative scale).
            Assert.Throws<InvalidOperationException>(
                () => _separation.Compute(105.0, 100.0, 0.0));
            Assert.Throws<InvalidOperationException>(
                () => _separation.Compute(105.0, 100.0, -2.0));
            Assert.Throws<InvalidOperationException>(
                () => _separation.Compute(105.0, 100.0, double.PositiveInfinity));
            Assert.Throws<InvalidOperationException>(
                () => _separation.Compute(105.0, 100.0, double.NaN));
        }

        [Fact]
        public void Separation_NoClippingOnLargeValues()
        {
            // No clipping: a large finite separation stays large.
            double s = _separation.Compute(1e12 + 1e6, 1e12, 1.0);
            Assert.Equal(1e6, s, 6);
        }

        // ---------------------------------------------------------
        // Relative close position: mathematical oracle
        // ---------------------------------------------------------

        [Fact]
        public void Position_CloseEqualsAtrSmooth_RIsExactlyZero()
        {
            double r = _position.Compute(close: 100.0, hma: 105.0, atrSmooth: 100.0);
            Assert.Equal(0.0, r, 12);
        }

        [Fact]
        public void Position_CloseEqualsHma_RIsExactlyOne()
        {
            double r = _position.Compute(close: 105.0, hma: 105.0, atrSmooth: 100.0);
            Assert.Equal(1.0, r, 12);
        }

        [Fact]
        public void Position_CloseBetweenReferences_ZeroLtRLtOne()
        {
            // HMA above: (102 - 100) / (105 - 100) = 0.4
            double r = _position.Compute(102.0, 105.0, 100.0);
            Assert.Equal(0.4, r, 12);
            Assert.True(r > 0.0 && r < 1.0);

            // HMA below: (98 - 100) / (95 - 100) = 0.4 — reflection
            // symmetry: the same interior fraction from the other side.
            double rMirror = _position.Compute(98.0, 95.0, 100.0);
            Assert.Equal(0.4, rMirror, 12);
            Assert.True(rMirror > 0.0 && rMirror < 1.0);
        }

        [Fact]
        public void Position_CloseBeyondHma_RGreaterThanOne()
        {
            // HMA above: (110 - 100) / (105 - 100) = 2
            double r = _position.Compute(110.0, 105.0, 100.0);
            Assert.Equal(2.0, r, 12);
            Assert.True(r > 1.0);

            // HMA below: (90 - 100) / (95 - 100) = 2 — reflection
            double rMirror = _position.Compute(90.0, 95.0, 100.0);
            Assert.Equal(2.0, rMirror, 12);
            Assert.True(rMirror > 1.0);
        }

        [Fact]
        public void Position_CloseOppositeSideOfAtrSmooth_RIsNegative()
        {
            // HMA above, close below ATRSmooth: (98 - 100)/(105 - 100) = -0.4
            double r = _position.Compute(98.0, 105.0, 100.0);
            Assert.Equal(-0.4, r, 12);
            Assert.True(r < 0.0);

            // HMA below, close above ATRSmooth: (102 - 100)/(95 - 100) = -0.4
            double rMirror = _position.Compute(102.0, 95.0, 100.0);
            Assert.Equal(-0.4, rMirror, 12);
            Assert.True(rMirror < 0.0);
        }

        [Fact]
        public void Position_ReflectionSymmetry_SignOfROneToBothSides()
        {
            // For mirrored geometries R takes the same value: R is
            // invariant under (HMA, Close, ATRSmooth) →
            // (2·ATRSmooth − HMA, 2·ATRSmooth − Close, ATRSmooth).
            double atr = 100.0;
            foreach (double hma in new[] { 105.0, 95.0, 130.0, 70.0 })
            {
                foreach (double close in new[] { 101.0, 110.0, 99.0, 90.0, hma, atr })
                {
                    double r = _position.Compute(close, hma, atr);
                    double rMirror = _position.Compute(
                        2 * atr - close, 2 * atr - hma, atr);
                    Assert.Equal(r, rMirror, 12);
                }
            }
        }

        [Fact]
        public void Position_HmaEqualsAtrSmooth_DegenerateIsNaN_NoException()
        {
            // Exact equality only — no epsilon, no tolerance, no
            // minimum denominator, no floor, and NO exception.
            double r = _position.Compute(close: 100.0, hma: 100.0, atrSmooth: 100.0);
            Assert.True(double.IsNaN(r));

            // The degenerate state holds for ANY close, on either side.
            Assert.True(double.IsNaN(_position.Compute(101.0, 100.0, 100.0)));
            Assert.True(double.IsNaN(_position.Compute(99.0, 100.0, 100.0)));
        }

        [Fact]
        public void Position_NoEpsilon_OneUlpDenominatorDividesExactly()
        {
            // Exact-representation fixture at 1.0 (double spacing in
            // [1,2) is exactly 2^-52): the denominator is a SINGLE
            // ULP — one representable step above exact equality —
            // and must DIVIDE, not NaN. An epsilon, tolerance,
            // minimum-denominator, or absolute-gap rejection would
            // refuse this division; exact equality does not.
            double oneUlp = Math.Pow(2.0, -52.0); // exactly representable
            double r = _position.Compute(1.0 + 2 * oneUlp, 1.0 + oneUlp, 1.0);
            Assert.Equal(2.0, r, 12);

            // One representable step BELOW exact equality mirrors it.
            double rBelow = _position.Compute(1.0, 1.0 - oneUlp / 2, 1.0 - oneUlp);
            Assert.Equal(2.0, rBelow, 12);
        }

        [Fact]
        public void Position_NaNHmaOrAtrSmooth_IsNaN()
        {
            Assert.True(double.IsNaN(_position.Compute(102.0, double.NaN, 100.0)));
            Assert.True(double.IsNaN(_position.Compute(102.0, 105.0, double.NaN)));
            Assert.True(double.IsNaN(_position.Compute(double.NaN, double.NaN, double.NaN)));
        }

        [Fact]
        public void Position_TailsAreNotClampedOrTransformed()
        {
            // Large finite R values stay large and exact: (1000-100)/(105-100) = 180
            double r = _position.Compute(1000.0, 105.0, 100.0);
            Assert.Equal(180.0, r, 9);

            // Deep negative: (0-100)/(105-100) = -20
            double rNeg = _position.Compute(0.0, 105.0, 100.0);
            Assert.Equal(-20.0, rNeg, 9);
        }

        // ---------------------------------------------------------
        // Validators (fail-closed conventions)
        // ---------------------------------------------------------

        [Fact]
        public void Validators_AcceptNanAndFinite_RejectNonFinite()
        {
            // NaN is the unavailable state; finite values pass; ±∞ fail.
            HmaAtrSmoothSeparationValidator.Validate(double.NaN);
            HmaAtrSmoothSeparationValidator.Validate(2.5);
            HmaAtrSmoothSeparationValidator.Validate(-2.5);
            HmaAtrSmoothSeparationValidator.Validate(1e308);
            Assert.Throws<InvalidOperationException>(
                () => HmaAtrSmoothSeparationValidator.Validate(double.PositiveInfinity));
            Assert.Throws<InvalidOperationException>(
                () => HmaAtrSmoothSeparationValidator.Validate(double.NegativeInfinity));

            HmaAtrSmoothRelativeClosePositionValidator.Validate(double.NaN);
            HmaAtrSmoothRelativeClosePositionValidator.Validate(0.0);
            HmaAtrSmoothRelativeClosePositionValidator.Validate(1.0);
            HmaAtrSmoothRelativeClosePositionValidator.Validate(180.0);
            HmaAtrSmoothRelativeClosePositionValidator.Validate(-20.0);
            HmaAtrSmoothRelativeClosePositionValidator.Validate(1e308);
            Assert.Throws<InvalidOperationException>(
                () => HmaAtrSmoothRelativeClosePositionValidator.Validate(double.PositiveInfinity));
            Assert.Throws<InvalidOperationException>(
                () => HmaAtrSmoothRelativeClosePositionValidator.Validate(double.NegativeInfinity));
        }

        [Fact]
        public void Validators_NoMagnitudeBoundOnPosition()
        {
            // No magnitude bound on R: arbitrarily large finite values
            // are valid measurements, not defects.
            HmaAtrSmoothRelativeClosePositionValidator.Validate(1e300);
            HmaAtrSmoothRelativeClosePositionValidator.Validate(-1e300);
        }
    }
}

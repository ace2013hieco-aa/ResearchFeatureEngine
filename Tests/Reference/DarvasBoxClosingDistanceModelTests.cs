using System;

using ResearchFeatureEngine.Models;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Unit tests for the pure
    /// <see cref="DarvasBoxClosingDistanceModel"/> — the exact
    /// mathematical contract of the Darvas Box closing-distance
    /// research feature (cases A–G of the specification, plus
    /// sign-convention and warm-up-adjacent boundary probes).
    ///
    /// Invariant under test (for every valid box):
    /// <code>
    /// Close &gt; Upper        → Signed = Close - Upper   (&gt; 0)
    /// Lower &lt;= Close &lt;= Upper → Signed = 0
    /// Close &lt; Lower        → Signed = Close - Lower   (&lt; 0)
    /// </code>
    /// The boundary used is the OUTER box line on the close's side —
    /// never the midpoint, never the opposite boundary, never the
    /// numerically closest one.
    /// </summary>
    public sealed class DarvasBoxClosingDistanceModelTests
    {
        private readonly DarvasBoxClosingDistanceModel _model = new();

        // -------------------------------------------------------------
        // Case A — above the box
        // -------------------------------------------------------------

        [Fact]
        public void CaseA_AboveBox_MeasuresFromUpper()
        {
            // Upper = 100, Lower = 90, Close = 107 → +7
            double signed = _model.Compute(close: 107.0, upper: 100.0, lower: 90.0);

            Assert.Equal(7.0, signed, 12);
        }

        [Fact]
        public void CaseA_AboveBox_SignedValueIsPositive()
        {
            double signed = _model.Compute(close: 107.0, upper: 100.0, lower: 90.0);
            Assert.True(signed > 0.0);
        }

        // -------------------------------------------------------------
        // Case B — below the box
        // -------------------------------------------------------------

        [Fact]
        public void CaseB_BelowBox_MeasuresFromLower()
        {
            // Upper = 100, Lower = 90, Close = 83 → -7
            double signed = _model.Compute(close: 83.0, upper: 100.0, lower: 90.0);

            Assert.Equal(-7.0, signed, 12);
        }

        [Fact]
        public void CaseB_BelowBox_SignedValueIsNegative()
        {
            double signed = _model.Compute(close: 83.0, upper: 100.0, lower: 90.0);
            Assert.True(signed < 0.0);
        }

        // -------------------------------------------------------------
        // Case C — inside the box
        // -------------------------------------------------------------

        [Fact]
        public void CaseC_InsideBox_IsExactlyZero()
        {
            // Upper = 100, Lower = 90, Close = 95 → 0
            double signed = _model.Compute(close: 95.0, upper: 100.0, lower: 90.0);

            Assert.Equal(0.0, signed, 12);
        }

        // -------------------------------------------------------------
        // Cases D & E — exact boundary equality is INSIDE (0)
        // -------------------------------------------------------------

        [Theory]
        [InlineData(100.0)]   // Close == Upper
        [InlineData(90.0)]    // Close == Lower
        public void CasesDE_ExactBoundaryCloses_AreInsideZero(double close)
        {
            double signed = _model.Compute(close, upper: 100.0, lower: 90.0);

            // Boundary equality is NOT outside: exactly 0, never a
            // signed value.
            Assert.Equal(0.0, signed, 12);
            Assert.Equal(0.0, Math.Sign(signed));
        }

        // -------------------------------------------------------------
        // Spec examples (5200/5180 box)
        // -------------------------------------------------------------

        [Theory]
        [InlineData(5212.0, 12.0)]    // above → +12 (from Upper)
        [InlineData(5200.0, 0.0)]      // == Upper → 0
        [InlineData(5190.0, 0.0)]      // inside → 0
        [InlineData(5180.0, 0.0)]      // == Lower → 0
        [InlineData(5172.0, -8.0)]      // below → -8 (from Lower)
        public void SpecExamples_5200Box(double close, double expected)
        {
            double signed = _model.Compute(close, upper: 5200.0, lower: 5180.0);

            Assert.Equal(expected, signed, 12);
        }

        // -------------------------------------------------------------
        // Case F — directional transition (switching reference lines)
        // Close: 95 → 103 → 106 → 98 → 87 over Upper=100/Lower=90
        // -------------------------------------------------------------

        [Fact]
        public void CaseF_DirectionalTransition_SwitchesBoundaryCorrectly()
        {
            double upper = 100.0;
            double lower = 90.0;
            double[] closes = { 95.0, 103.0, 106.0, 98.0, 87.0 };
            double[] expected = { 0.0, 3.0, 6.0, 0.0, -3.0 };

            for (int i = 0; i < closes.Length; i++)
            {
                double signed = _model.Compute(closes[i], upper, lower);
                Assert.Equal(expected[i], signed, 12);
            }
        }

        // -------------------------------------------------------------
        // Case G — box-boundary transition
        // Close: 101 → 99 → 89 → 91 over Upper=100/Lower=90
        // -------------------------------------------------------------

        [Fact]
        public void CaseG_BoundaryTransition_ProducesPlusOneZeroMinusOneZero()
        {
            double upper = 100.0;
            double lower = 90.0;
            double[] closes = { 101.0, 99.0, 89.0, 91.0 };
            double[] expected = { 1.0, 0.0, -1.0, 0.0 };

            for (int i = 0; i < closes.Length; i++)
            {
                double signed = _model.Compute(closes[i], upper, lower);
                Assert.Equal(expected[i], signed, 12);
            }
        }

        // -------------------------------------------------------------
        // Negative-convention guards: NOT closest-boundary,
        // NOT midpoint, NOT opposite boundary
        // -------------------------------------------------------------

        [Fact]
        public void Guard_AboveBox_DoesNotUseLowerOrMidpoint()
        {
            // Close = 107 is numerically closer to Lower? No: |107-100|=7
            // vs |107-90|=17 — but the assertion that matters is that
            // the model measures from UPPER (100): +7, not from the
            // midpoint 95 (+12) and not from LOWER (+17 or -17).
            double signed = _model.Compute(close: 107.0, upper: 100.0, lower: 90.0);

            Assert.Equal(7.0, signed, 12);          // from Upper
            Assert.NotEqual(12.0, signed);          // not midpoint
        }

        [Fact]
        public void Guard_BelowBox_DoesNotUseUpperOrMidpoint()
        {
            double signed = _model.Compute(close: 83.0, upper: 100.0, lower: 90.0);

            Assert.Equal(-7.0, signed, 12);         // from Lower
            Assert.NotEqual(-17.0, signed);         // not from Upper
            Assert.NotEqual(-12.0, signed);         // not midpoint
        }

        // -------------------------------------------------------------
        // Precision (spec §11): no rounding
        // -------------------------------------------------------------

        [Fact]
        public void Precision_XAUUSDStyleValues_PreserveFullPrecision()
        {
            // Below the box: Close = 5172.43, Lower = 5175.0 → the
            // exact IEEE-754 value of Close - Lower, no rounding.
            double signed = _model.Compute(close: 5172.43, upper: 5200.0, lower: 5175.0);

            Assert.Equal(5172.43 - 5175.0, signed, 15);
            Assert.True(signed < 0.0);
        }

        [Fact]
        public void Precision_SignedValue_IsNotRoundedToPipsOrIntegers()
        {
            double signed = _model.Compute(close: 100.07, upper: 100.0, lower: 90.0);

            // Must equal the exact IEEE-754 subtraction — no integer
            // rounding, no pip rounding, no clipping to 0.07 exactly.
            Assert.Equal(100.07 - 100.0, signed, 15);

            // And it is NOT the "clean" decimal value that a
            // rounded implementation would produce.
            Assert.NotEqual(0.07, signed);
        }
    }
}

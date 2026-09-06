using System;

using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Golden-dataset correctness tests for the Darvas Box reference
    /// source.
    ///
    /// The expected Top/Bottom/Regime/Price sequences were produced
    /// by the INDEPENDENT oracle in the Backtest-Engine repository
    /// (<c>backtest_audit.darvas_indicators.DarvasBox5</c>, the
    /// verified bar-for-bar port of the user's "Darvas Box Buy Sell"
    /// Pine v4 indicator) run over these exact fixtures via
    /// <c>C:\Users\Ali Zoghi\AppData\Local\Temp\darvas_golden\
    /// generate_golden.py</c>. The oracle shares NO code with the
    /// implementation under test. These tests guard against any
    /// accidental algorithm change that would silently alter the
    /// Darvas box lifecycle.
    ///
    /// Fixture 1 exercises the full required lifecycle (§17 of the
    /// implementation brief): box formation, midpoint, inside-box
    /// regime, bullish breakout, return to box, bearish breakout,
    /// and two box replacements.
    /// </summary>
    public sealed class DarvasBoxReferenceSourceGoldenTests
    {
        // ---------------------------------------------------------
        // Fixture 1: main lifecycle (boxp = 5, n = 30)
        //
        // Confirmations at bars 9, 16, 23:
        //   box 1 = [110.0, 96.0]  (mid 103.0)
        //   box 2 = [113.0, 94.0]  (mid 103.5)
        //   box 3 = [120.0, 102.0] (mid 111.0)
        // ---------------------------------------------------------

        private static readonly double[] F1High =
        {
            102.0, 101.0, 100.5, 101.0, 100.0,
            100.5, 110.0, 105.0, 104.0, 103.5,
            104.0, 103.0, 102.0, 113.0, 106.0,
            95.0, 101.0, 103.0, 104.0, 105.0,
            120.0, 115.0, 114.0, 113.5, 112.0,
            111.0, 110.5, 109.0, 108.0, 107.0
        };

        private static readonly double[] F1Low =
        {
            99.0, 98.5, 98.0, 98.5, 99.0,
            96.0, 97.0, 97.5, 98.0, 98.5,
            99.0, 100.0, 101.0, 108.0, 100.0,
            94.0, 99.0, 100.0, 101.0, 102.0,
            103.0, 104.0, 105.0, 106.0, 107.0,
            108.0, 109.0, 106.5, 105.5, 104.5
        };

        private static readonly double[] F1Close =
        {
            100.0, 99.5, 99.0, 99.5, 100.0,
            100.2, 108.0, 103.0, 102.5, 103.0,
            103.5, 102.5, 101.5, 112.0, 105.0,
            94.0, 100.0, 101.0, 102.0, 103.0,
            104.0, 105.0, 106.0, 107.0, 108.0,
            109.0, 110.0, 108.5, 107.5, 106.0
        };

        // Oracle expected values (DarvasBox5, boxp = 5).
        private static readonly double?[] F1Top =
        {
            null, null, null, null, null, null, null, null, null,
            110.0, 110.0, 110.0, 110.0, 110.0, 110.0, 110.0,
            113.0, 113.0, 113.0, 113.0, 113.0, 113.0, 113.0,
            120.0, 120.0, 120.0, 120.0, 120.0, 120.0, 120.0
        };

        private static readonly double?[] F1Bottom =
        {
            null, null, null, null, null, null, null, null, null,
            96.0, 96.0, 96.0, 96.0, 96.0, 96.0, 96.0,
            94.0, 94.0, 94.0, 94.0, 94.0, 94.0, 94.0,
            102.0, 102.0, 102.0, 102.0, 102.0, 102.0, 102.0
        };

        // Warm-up bars publish close; boxed bars publish the midpoint.
        private static readonly double[] F1ExpectedPrice =
        {
            100.0, 99.5, 99.0, 99.5, 100.0, 100.2, 108.0, 103.0, 102.5,
            103.0, 103.0, 103.0, 103.0, 103.0, 103.0, 103.0,
            103.5, 103.5, 103.5, 103.5, 103.5, 103.5, 103.5,
            111.0, 111.0, 111.0, 111.0, 111.0, 111.0, 111.0
        };

        private static readonly double[] F1ExpectedRegime =
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0,
            1,   // bar 13: close 112 > upper 110 -> bullish breakout
            0,   // bar 14: close 105 -> return to box
            -1,  // bar 15: close 94 < lower 96 -> bearish breakout
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0
        };

        private static (EngineContext ctx, DarvasBoxReferenceSource source)
            BuildSource(
                double[] high, double[] low, double[] close,
                int length = 5)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new EngineContext(md, new Core.EngineValues());
            var source = new DarvasBoxReferenceSource(
                new DarvasBoxConfiguration(length));
            source.Initialize();
            return (ctx, source);
        }

        [Fact]
        public void Fixture1_BarForBar_TopBottomRegimePrice_MatchOracle()
        {
            var (ctx, source) = BuildSource(F1High, F1Low, F1Close);

            for (int i = 0; i < F1Close.Length; i++)
            {
                ctx.SetIndex(i);
                double price = source.Update(ctx);

                // Top / Bottom hold forward exactly as the oracle's
                // valuewhen() state.
                Assert.Equal(F1Top[i] ?? double.NaN, source.Upper, 10);
                Assert.Equal(F1Bottom[i] ?? double.NaN, source.Lower, 10);

                // Published measurement level.
                Assert.Equal(F1ExpectedPrice[i], price, 10);

                // Published regime (source-declared signed state).
                Assert.Equal(F1ExpectedRegime[i], source.Regime, 10);

                // Price == (Upper + Lower) / 2 whenever a box exists.
                if (!double.IsNaN(source.Upper))
                {
                    Assert.Equal(
                        (source.Upper + source.Lower) * 0.5,
                        price, 12);
                }
            }
        }

        [Fact]
        public void Fixture1_BoxFormation_UpperIsBreakoutHigh_LowerIsRollingLow()
        {
            // Bar 6 is the breakout bar (high 110 > k1[5] = 101).
            // Confirmation at bar 9 (boxp - 2 = 3 bars later):
            //   Upper = NH = high[6] = 110.0
            //   Lower = LL[9] = min(low[5..9]) = 96.0
            var (ctx, source) = BuildSource(F1High, F1Low, F1Close);

            for (int i = 0; i <= 9; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (i < 9)
                {
                    Assert.False(source.HasBox); // warm-up
                }
                else
                {
                    Assert.True(source.HasBox);  // confirmation bar
                }
            }

            Assert.Equal(110.0, source.Upper, 10);
            Assert.Equal(96.0, source.Lower, 10);
            Assert.Equal(103.0, (source.Upper + source.Lower) * 0.5, 10);
        }

        [Fact]
        public void Fixture1_BoxReplacement_ConfirmsAtBar16And23()
        {
            var (ctx, source) = BuildSource(F1High, F1Low, F1Close);

            for (int i = 0; i < F1Close.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (i < 9)
                {
                    Assert.False(source.HasBox); // warm-up
                }
                else if (i < 16)
                {
                    Assert.Equal(110.0, source.Upper, 10);
                }
                else if (i < 23)
                {
                    // Replacement 1: breakout at 13 (high 113), LL[16] = 94.
                    Assert.Equal(113.0, source.Upper, 10);
                    Assert.Equal(94.0, source.Lower, 10);
                }
                else
                {
                    // Replacement 2: breakout at 20 (high 120), LL[23] = 102.
                    Assert.Equal(120.0, source.Upper, 10);
                    Assert.Equal(102.0, source.Lower, 10);
                }
            }
        }

        [Fact]
        public void Fixture1_MidpointJumpsOnReplacement_AreNotSmoothed()
        {
            // The midpoint jumps 103.0 -> 103.5 -> 111.0 at the
            // replacement bars. That is the intentional structural
            // effect of box replacement; it must not be smoothed,
            // suppressed, or clamped.
            var (ctx, source) = BuildSource(F1High, F1Low, F1Close);

            double prevPrice = double.NaN;
            double midAt15 = double.NaN;
            double midAt16 = double.NaN;
            double midAt22 = double.NaN;
            double midAt23 = double.NaN;

            for (int i = 0; i < F1Close.Length; i++)
            {
                ctx.SetIndex(i);
                double price = source.Update(ctx);
                prevPrice = price;

                if (i == 15) midAt15 = price;
                if (i == 16) midAt16 = price;
                if (i == 22) midAt22 = price;
                if (i == 23) midAt23 = price;
            }

            Assert.Equal(103.0, midAt15, 10);
            Assert.Equal(103.5, midAt16, 10); // structural jump
            Assert.Equal(103.5, midAt22, 10);
            Assert.Equal(111.0, midAt23, 10); // structural jump
        }

        // ---------------------------------------------------------
        // Fixture 2: exact-boundary regime (boxp = 5, n = 17)
        //
        // Box confirms at 8: [110.0, 101.0] (mid 105.5).
        //   bar  9: close 110 == Upper -> inside (0)  [exact upper]
        //   bar 10: close 101 == Lower -> inside (0)  [exact lower]
        //   bar 11: close 100 < Lower -> -1
        //   bar 12: close 111 > Upper -> +1          [direct -1 -> +1]
        //   bar 13: close 110 == Upper -> 0          [+1 -> 0 return]
        // Replacement at 15: [111.5, 99.0] (mid 105.25).
        //   bar 16: close 90 < 99 -> -1
        // ---------------------------------------------------------

        private static readonly double[] F2High =
        {
            102.0, 101.5, 101.0, 101.5, 101.5,
            110.0, 105.0, 104.0, 103.5,
            110.0, 106.0, 104.0, 111.5, 110.5,
            107.0, 105.0, 102.0
        };

        private static readonly double[] F2Low =
        {
            100.0, 100.2, 100.5, 100.8, 101.0,
            102.0, 103.0, 103.5, 103.0,
            109.0, 100.5, 99.0, 110.5, 109.5,
            105.5, 103.5, 89.0
        };

        private static readonly double[] F2Close =
        {
            101.0, 100.8, 100.7, 101.2, 101.2,
            108.0, 104.0, 103.8, 103.2,
            110.0, 101.0, 100.0, 111.0, 110.0,
            106.5, 104.0, 90.0
        };

        private static readonly double[] F2ExpectedRegime =
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0,   // bar 8: box forms, close 103.2 inside
            0,   // bar 9: close 110 == Upper -> INSIDE (exact boundary)
            0,   // bar 10: close 101 == Lower -> INSIDE (exact boundary)
            -1,  // bar 11: close 100 < Lower 101
            1,   // bar 12: close 111 > Upper 110
            0,   // bar 13: close 110 == Upper -> INSIDE (return)
            0,   // bar 14: close 106.5
            0,   // bar 15: replacement box [111.5, 99.0], close 104 inside
            -1   // bar 16: close 90 < 99
        };

        [Fact]
        public void Fixture2_ExactBoundaryCloses_AreInsideNotOut()
        {
            var (ctx, source) = BuildSource(F2High, F2Low, F2Close);

            for (int i = 0; i < F2Close.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
                Assert.Equal(F2ExpectedRegime[i], source.Regime, 10);
            }
        }

        [Fact]
        public void Fixture2_Box_TopBottomMatchOracle()
        {
            var (ctx, source) = BuildSource(F2High, F2Low, F2Close);

            for (int i = 0; i < F2Close.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (i < 8)
                {
                    Assert.False(source.HasBox);
                }
                else if (i < 15)
                {
                    Assert.Equal(110.0, source.Upper, 10);
                    Assert.Equal(101.0, source.Lower, 10);
                }
                else
                {
                    Assert.Equal(111.5, source.Upper, 10);
                    Assert.Equal(99.0, source.Lower, 10);
                }
            }
        }

        // ---------------------------------------------------------
        // Fixture 3: pivot-exceeded replacement (boxp = 5, n = 17)
        //
        // Box A [110, 96] confirms at 8. Bar 9 (high 111 > 110)
        // starts a NEW cycle: replacement [111, 99] confirms at 12.
        // Bar 15: close 113 > 111 -> +1 against the NEW box (proves
        // the replacement took effect); bar 16: close 110 -> 0.
        // ---------------------------------------------------------

        private static readonly double[] F3High =
        {
            102.0, 101.5, 101.0, 101.5, 102.0,
            110.0, 105.0, 104.0, 103.0, 111.0,
            106.0, 105.0, 104.0, 105.0, 104.5,
            114.0, 112.0
        };

        private static readonly double[] F3Low =
        {
            99.0, 98.5, 98.0, 98.5, 99.0,
            96.0, 97.0, 98.0, 99.0, 100.0,
            101.0, 102.0, 103.0, 104.0, 104.0,
            103.5, 102.0
        };

        private static readonly double[] F3Close =
        {
            100.0, 99.5, 99.0, 99.5, 100.0,
            108.0, 103.0, 102.0, 101.0, 105.0,
            104.0, 103.5, 103.0, 104.0, 104.5,
            113.0, 110.0
        };

        [Fact]
        public void Fixture3_PivotExceeded_ReplacementBoxIsAgainstTheNewBox()
        {
            var (ctx, source) = BuildSource(F3High, F3Low, F3Close);

            double regimeAt15 = double.NaN;
            double regimeAt16 = double.NaN;

            for (int i = 0; i < F3Close.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (i == 15) regimeAt15 = source.Regime;
                if (i == 16) regimeAt16 = source.Regime;
            }

            // Replacement box [111, 99]: close 113 > 111 -> +1;
            // close 110 -> inside (0). If the source had kept the
            // OLD box [110, 96], close 113 -> +1 as well, but close
            // 110 == Upper would also be 0; the distinguishing check
            // is the box boundaries themselves (verified below).
            Assert.Equal(1.0, regimeAt15, 10);
            Assert.Equal(0.0, regimeAt16, 10);
            Assert.Equal(111.0, source.Upper, 10);
            Assert.Equal(99.0, source.Lower, 10);
        }

        // ---------------------------------------------------------
        // Fixture 4: failed confirmation / box persistence (boxp = 5,
        // n = 21). Box [110, 96] confirms at 8 and PERSISTS to the
        // end: the would-be replacement cycle (breakout at 12,
        // high 115) never confirms because bar 15's high ties the
        // pivot (k3 == k2 -> box1 false). Closes exercise the OLD
        // box: +1 at 16 (close 112 > 110), 0 at 17, -1 at 19
        // (close 95 < 96), 0 at 20.
        // ---------------------------------------------------------

        private static readonly double[] F4High =
        {
            102.0, 101.5, 101.0, 101.5, 102.0,
            110.0, 105.0, 104.0, 105.5,
            104.5, 103.5, 102.5, 115.0,
            112.0, 111.0, 115.0,
            113.0, 112.0, 111.0, 110.0, 109.0
        };

        private static readonly double[] F4Low =
        {
            99.0, 98.5, 98.0,  98.5, 99.0,
            96.0, 97.0,  98.0,  99.0,
            100.0, 100.5, 100.8, 103.0,
            104.0, 105.0, 106.0,
            111.0, 107.0, 104.0, 94.0, 99.0
        };

        private static readonly double[] F4Close =
        {
            100.0, 99.5, 99.0, 99.5, 100.0,
            108.0, 103.0, 102.0, 102.5,
            104.0, 103.0, 102.0, 110.0,
            108.0, 107.0, 108.0,
            112.0, 108.0, 105.0, 95.0, 100.0
        };

        [Fact]
        public void Fixture4_FailedConfirmation_OldBoxPersistsAndGovernsRegime()
        {
            var (ctx, source) = BuildSource(F4High, F4Low, F4Close);

            for (int i = 0; i < F4Close.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                // The box NEVER changes: the replacement cycle is
                // dead (pivot tie at bar 15 -> box1 false).
                if (i >= 8)
                {
                    Assert.Equal(110.0, source.Upper, 10);
                    Assert.Equal(96.0, source.Lower, 10);
                }
            }

            // Regime sequence against the OLD box is verified in
            // Fixture4_RegimeSequence_AgainstThePersistentOldBox.
        }

        [Fact]
        public void Fixture4_RegimeSequence_AgainstThePersistentOldBox()
        {
            var (ctx, source) = BuildSource(F4High, F4Low, F4Close);

            double r12 = 0, r16 = 0, r17 = 0, r19 = 0, r20 = 0;

            for (int i = 0; i < F4Close.Length; i++)
            Fixture4RegimeLoopBody(ctx, source, i,
                ref r12, ref r16, ref r17, ref r19, ref r20);

            Assert.Equal(0.0, r12, 10);  // == Upper -> inside
            Assert.Equal(1.0, r16, 10);  // > Upper -> breakout up
            Assert.Equal(0.0, r17, 10);  // return to box
            Assert.Equal(-1.0, r19, 10); // < Lower -> breakout down
            Assert.Equal(0.0, r20, 10);  // return to box
        }

        private static void Fixture4RegimeLoopBody(
            EngineContext ctx, DarvasBoxReferenceSource source, int i,
            ref double r12, ref double r16, ref double r17, ref double r19,
            ref double r20)
        {
            ctx.SetIndex(i);
            source.Update(ctx);
            if (i == 12) r12 = source.Regime;
            if (i == 16) r16 = source.Regime;
            if (i == 17) r17 = source.Regime;
            if (i == 19) r19 = source.Regime;
            if (i == 20) r20 = source.Regime;
        }
    }
}

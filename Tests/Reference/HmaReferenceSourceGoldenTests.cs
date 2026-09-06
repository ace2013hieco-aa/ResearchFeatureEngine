using System;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using Xunit;

namespace ResearchFeatureEngine.Tests.Reference
{
    /// <summary>
    /// Golden-dataset correctness tests for the HMA reference source.
    ///
    /// The expected HMA sequences were produced by an INDEPENDENT
    /// direct-definition oracle (literal transcription of the canonical
    /// Alan Hull formulas — WMA windows evaluated directly, no
    /// incremental state) implemented in Python at
    /// <c>C:\Users\Ali Zoghi\AppData\Local\Temp\hermes_hma_golden_gen.py</c>.
    /// The oracle shares NO code with the implementation under test.
    ///
    /// These tests pin:
    ///   * the exact warm-up boundary (first valid HMA at
    ///     P + floor(sqrt(P)) - 2 = bar 18 for P = 16);
    ///   * the exact per-bar values on ramp / random / constant
    ///     fixtures (the constant fixture must produce EXACTLY the
    ///     constant — any drift is a failure);
    ///   * the pipeline warm-up contract (published reference =
    ///     close fallback while Runtime.Hma is NaN).
    /// </summary>
    public sealed class HmaReferenceSourceGoldenTests
    {
        private static (EngineContext ctx, HmaReferenceSource source)
            MakeSource(double[] close)
        {
            int n = close.Length;
            double[] open = new double[n];
            double[] high = new double[n];
            double[] low = new double[n];
            double[] volume = new double[n];
            for (int i = 0; i < n; i++)
            {
                open[i] = close[i];
                high[i] = close[i] + 0.5;
                low[i] = close[i] - 0.5;
                volume[i] = 100.0;
            }

            var md = new FlatOhlcvMarketData(open, high, low, close, volume);
            var ctx = new EngineContext(md, new EngineValues());
            var source = new HmaReferenceSource(new HmaConfiguration(16));
            source.Initialize();
            return (ctx, source);
        }

        // Fixture: linear ramp 1..40 (P = 16).
        private static readonly double[] RampClose =
        {
            1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0,
            9.0, 10.0, 11.0, 12.0, 13.0, 14.0, 15.0, 16.0,
            17.0, 18.0, 19.0, 20.0, 21.0, 22.0, 23.0, 24.0,
            25.0, 26.0, 27.0, 28.0, 29.0, 30.0, 31.0, 32.0,
            33.0, 34.0, 35.0, 36.0, 37.0, 38.0, 39.0, 40.0
        };

        // Oracle: NaN through bar 17, then HMA = bar + 2.333333333333336
        // (ramp slope 1 → HMA lags by a constant).
        private static readonly double[] RampExpected =
        {
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN,
            18.333333333333336, 19.333333333333336, 20.333333333333336,
            21.333333333333336, 22.333333333333336, 23.333333333333336,
            24.333333333333336, 25.333333333333336, 26.333333333333336,
            27.333333333333336, 28.333333333333336, 29.333333333333336,
            30.333333333333336, 31.333333333333336, 32.333333333333336,
            33.333333333333336, 34.33333333333333, 35.33333333333333,
            36.33333333333333, 37.33333333333333, 38.33333333333333,
            39.33333333333333
        };

        // Fixture: deterministic random walk (seed 9).
        private static readonly double[] RandomClose =
        {
            99.63007357815022, 98.73311931395043, 96.38539412514456,
            103.66561849986341, 95.06435054081123, 100.02782080052208,
            103.98297970031938, 95.80814647183001, 100.54270468178287,
            101.16650042683618, 95.40895765484811, 98.79019604395435,
            102.03480392293747, 99.52020920450026, 102.25065368582209,
            96.57157161596626, 97.38012202466533, 96.10947527978014,
            100.06269051668983, 104.23829786412296, 100.90428457135913,
            102.74209467235511, 98.83664844852647, 102.46095216924428,
            96.01669437579478, 97.91178078986407, 101.74236001255372,
            102.25706352241305, 99.21755395006954, 95.87712383065971,
            97.6673357122674, 97.09890130136482, 97.81184415084569,
            103.0951070072647, 96.99483221089845, 103.86399731079277,
            103.79373188400177, 95.54789356111084, 98.7881640229975,
            99.91711736762097
        };

        private static readonly double[] RandomExpected =
        {
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN,
            98.35091993787104, 98.93475027915292, 99.58840212247256,
            100.49015766046266, 100.8653906380312, 101.34698865985577,
            101.0593325888821, 100.44483113152162, 100.13674436250476,
            100.10950127592399, 100.06732841655439, 99.49743976603062,
            98.8384072827367, 98.10996027716931, 97.63512761567789,
            98.03578546132114, 98.12488403243546, 98.918820516581,
            100.12953688221054, 100.40886340015511, 100.4452042467669,
            100.31432217931022
        };

        // Fixture: constant 100.0 — the HMA must be EXACTLY 100 (any
        // incremental-state drift fails here; this is the fixture that
        // exposed the original broken WMA scheme).
        private static readonly double[] ConstantClose =
        {
            100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0,
            100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0,
            100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0,
            100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0,
            100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0
        };

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(17)]
        public void WarmUp_BeforeP2PlusSqrtPMinus2_RuntimeHmaIsNaN(int stopAt)
        {
            var (ctx, source) = MakeSource(RandomClose);

            for (int i = 0; i <= stopAt; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                Assert.True(double.IsNaN(source.Runtime.Hma),
                    $"Runtime.Hma must be NaN during warm-up at bar {i}");
            }
        }

        [Fact]
        public void WarmUp_PublishedReferenceFallsBackToClose_MeasurementLevelStaysFinite()
        {
            var (ctx, source) = MakeSource(RandomClose);

            for (int i = 0; i <= 17; i++)
            {
                ctx.SetIndex(i);
                double reference = source.Update(ctx);

                // Pipeline contract: measurement level is finite (close
                // fallback) while Runtime.Hma is NaN — consumers must
                // distinguish the two.
                Assert.Equal(RandomClose[i], reference);
                Assert.True(double.IsNaN(source.Runtime.Hma));
            }
        }

        [Fact]
        public void WarmUp_FirstValidBarIsExactlyP2PlusSqrtPMinus2()
        {
            var (ctx, source) = MakeSource(RandomClose);

            ctx.SetIndex(17);
            source.Update(ctx);
            Assert.True(double.IsNaN(source.Runtime.Hma));

            ctx.SetIndex(18);
            source.Update(ctx);
            Assert.False(double.IsNaN(source.Runtime.Hma),
                "First valid HMA bar for P=16 is 16 + 4 - 2 = 18");
        }

        [Fact]
        public void Golden_RampFixture_MatchesOracleExactly()
        {
            Assert.Equal(RampClose.Length, RampExpected.Length);
            var (ctx, source) = MakeSource(RampClose);

            for (int i = 0; i < RampClose.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (double.IsNaN(RampExpected[i]))
                {
                    Assert.True(double.IsNaN(source.Runtime.Hma),
                        $"bar {i}: expected NaN");
                }
                else
                {
                    Assert.Equal(RampExpected[i], source.Runtime.Hma, 12);
                }
            }
        }

        [Fact]
        public void Golden_RandomFixture_MatchesOracleExactly()
        {
            var (ctx, source) = MakeSource(RandomClose);

            for (int i = 0; i < RandomClose.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (double.IsNaN(RandomExpected[i]))
                {
                    Assert.True(double.IsNaN(source.Runtime.Hma),
                        $"bar {i}: expected NaN");
                }
                else
                {
                    Assert.Equal(RandomExpected[i], source.Runtime.Hma, 12);
                }
            }
        }

        [Fact]
        public void Golden_ConstantFixture_HmaIsExactlyTheConstant()
        {
            var (ctx, source) = MakeSource(ConstantClose);

            for (int i = 0; i < ConstantClose.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                if (i < 18)
                {
                    Assert.True(double.IsNaN(source.Runtime.Hma),
                        $"bar {i}: warm-up NaN");
                }
                else
                {
                    Assert.Equal(100.0, source.Runtime.Hma, 12);
                }
            }
        }

        [Fact]
        public void Regime_SlopeSemantics_AreValid()
        {
            var (ctx, source) = MakeSource(RampClose);

            double? previous = null;
            for (int i = 0; i < RampClose.Length; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);

                double hma = source.Runtime.Hma;
                if (!double.IsNaN(hma) && previous.HasValue)
                {
                    double expected = hma > previous.Value
                        ? 1.0
                        : hma < previous.Value ? -1.0 : 0.0;
                    Assert.Equal(expected, source.Runtime.Position);
                }

                if (!double.IsNaN(hma))
                    previous = hma;
            }
        }

        [Fact]
        public void ReTick_SameBarIsIdempotent()
        {
            var (ctx, source) = MakeSource(RandomClose);

            for (int i = 0; i < 25; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }

            double hmaOnce = source.Runtime.Hma;
            double regimeOnce = source.Runtime.Position;

            // Re-tick the same bar three times: identical state.
            for (int tick = 0; tick < 3; tick++)
            {
                source.Update(ctx);
                Assert.Equal(hmaOnce, source.Runtime.Hma);
                Assert.Equal(regimeOnce, source.Runtime.Position);
            }
        }

        [Fact]
        public void Reset_ClearsStateAndIsLeakFree()
        {
            var (ctx, source) = MakeSource(RandomClose);

            for (int i = 0; i < 30; i++)
            {
                ctx.SetIndex(i);
                source.Update(ctx);
            }
            Assert.False(double.IsNaN(source.Runtime.Hma));

            source.Reset();

            Assert.True(double.IsNaN(source.Runtime.Hma));
            Assert.Equal(0.0, source.Runtime.Position);

            // Replay reproduces the golden values exactly.
            ctx.SetIndex(18);
            source.Update(ctx);
            Assert.Equal(RandomExpected[18], source.Runtime.Hma, 12);
        }
    }
}

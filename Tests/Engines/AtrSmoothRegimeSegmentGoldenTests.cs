using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Golden oracle test for the ATRSmooth Regime Segment state
    /// machine: a fully hand-computed 13-bar fixture, independent
    /// of the implementation's own internal calculations (§17 of
    /// the M9 brief).
    ///
    /// Fixture (the brief's worked example):
    ///
    /// <code>
    /// bar:         0  1  2  3  4  5  6  7  8  9 10 11 12
    /// regime:      0  0  +  +  +  +  -  -  -  -  +  +  +
    /// RegimeId:    -  -  0  0  0  0  1  1  1  1  2  2  2
    /// StartIndex:  -  -  2  2  2  2  6  6  6  6 10 10 10
    /// Age:         -  -  0  1  2  3  0  1  2  3  0  1  2
    /// Transition:  0  0  0  0  0  0 -1  0  0  0 +1  0  0
    /// </code>
    ///
    /// The unavailable representation follows the repository
    /// convention (int? = null), and the transition/direction values
    /// follow the published enums (Up = +1, Down = -1).
    /// </summary>
    public sealed class AtrSmoothRegimeSegmentGoldenTests
    {
        // Canonical ATRSmooth regime inputs (trailing-stop
        // position bias): 0 warm-up, +1 bullish, -1 bearish.
        private static readonly double[] RegimeInput =
        {
            0.0, 0.0, 1.0, 1.0, 1.0, 1.0,
            -1.0, -1.0, -1.0, -1.0,
            1.0, 1.0, 1.0
        };

        // Hand-computed expected outputs.
        private static readonly AtrSmoothRegimeDirection[] ExpectedRegime =
        {
            AtrSmoothRegimeDirection.Unavailable,
            AtrSmoothRegimeDirection.Unavailable,
            AtrSmoothRegimeDirection.Bullish,
            AtrSmoothRegimeDirection.Bullish,
            AtrSmoothRegimeDirection.Bullish,
            AtrSmoothRegimeDirection.Bullish,
            AtrSmoothRegimeDirection.Bearish,
            AtrSmoothRegimeDirection.Bearish,
            AtrSmoothRegimeDirection.Bearish,
            AtrSmoothRegimeDirection.Bearish,
            AtrSmoothRegimeDirection.Bullish,
            AtrSmoothRegimeDirection.Bullish,
            AtrSmoothRegimeDirection.Bullish
        };

        private static readonly int?[] ExpectedRegimeId =
        {
            null, null, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2
        };

        private static readonly int?[] ExpectedStartIndex =
        {
            null, null, 2, 2, 2, 2, 6, 6, 6, 6, 10, 10, 10
        };

        private static readonly int?[] ExpectedAge =
        {
            null, null, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2
        };

        private static readonly AtrSmoothRegimeTransition[] ExpectedTransition =
        {
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.Down,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.Up,
            AtrSmoothRegimeTransition.None,
            AtrSmoothRegimeTransition.None
        };

        [Fact]
        public void GoldenOracle_HandComputed13BarFixture_MatchesExactly()
        {
            var values = new EngineValues();
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            var engine = new AtrSmoothRegimeSegmentEngine(context);
            engine.Initialize();

            for (int i = 0; i < RegimeInput.Length; i++)
            {
                values.Reference.Regime = RegimeInput[i];
                context.SetIndex(i);
                engine.Update();

                var s = values.AtrSmoothRegimeSegment;

                Assert.True(ExpectedRegime[i] == s.Regime,
                    $"bar {i}: Regime expected {ExpectedRegime[i]}, got {s.Regime}");
                Assert.True(NullableIntEquals(ExpectedRegimeId[i], s.RegimeId),
                    $"bar {i}: RegimeId expected {NullablePrint(ExpectedRegimeId[i])}, got {NullablePrint(s.RegimeId)}");
                Assert.True(NullableIntEquals(ExpectedStartIndex[i], s.RegimeStartIndex),
                    $"bar {i}: RegimeStartIndex expected {NullablePrint(ExpectedStartIndex[i])}, got {NullablePrint(s.RegimeStartIndex)}");
                Assert.True(NullableIntEquals(ExpectedAge[i], s.RegimeAge),
                    $"bar {i}: RegimeAge expected {NullablePrint(ExpectedAge[i])}, got {NullablePrint(s.RegimeAge)}");
                Assert.True(ExpectedTransition[i] == s.RegimeTransition,
                    $"bar {i}: RegimeTransition expected {ExpectedTransition[i]}, got {s.RegimeTransition}");
            }
        }

        [Fact]
        public void GoldenOracle_SecondRun_IsBitConsistent()
        {
            // The fixture replayed after Reset must reproduce every
            // value bit-for-bit (deterministic state machine).
            var values = new EngineValues();
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            var engine = new AtrSmoothRegimeSegmentEngine(context);
            engine.Initialize();

            var firstRun = new (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[RegimeInput.Length];
            for (int i = 0; i < RegimeInput.Length; i++)
            {
                values.Reference.Regime = RegimeInput[i];
                context.SetIndex(i);
                engine.Update();
                var s = values.AtrSmoothRegimeSegment;
                firstRun[i] = (s.Regime, s.RegimeId, s.RegimeStartIndex, s.RegimeAge, s.RegimeTransition);
            }

            engine.Reset();

            for (int i = 0; i < RegimeInput.Length; i++)
            {
                values.Reference.Regime = RegimeInput[i];
                context.SetIndex(i);
                engine.Update();
                var s = values.AtrSmoothRegimeSegment;

                Assert.Equal(firstRun[i].Item1, s.Regime);
                Assert.Equal(firstRun[i].Item2, s.RegimeId);
                Assert.Equal(firstRun[i].Item3, s.RegimeStartIndex);
                Assert.Equal(firstRun[i].Item4, s.RegimeAge);
                Assert.Equal(firstRun[i].Item5, s.RegimeTransition);
            }
        }

        private static bool NullableIntEquals(int? a, int? b)
        {
            if (a.HasValue != b.HasValue)
                return false;
            if (!a.HasValue)
                return true;
            return a.GetValueOrDefault() == b.GetValueOrDefault();
        }

        private static string NullablePrint(int? v) =>
            v.HasValue ? v.Value.ToString() : "null";
    }
}

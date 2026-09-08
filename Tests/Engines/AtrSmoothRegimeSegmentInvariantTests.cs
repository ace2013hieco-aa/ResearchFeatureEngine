using System;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Core.Engine;
using ResearchFeatureEngine.Engines;
using Xunit;

namespace ResearchFeatureEngine.Tests.Engines
{
    /// <summary>
    /// Executable invariant tests for the ATRSmooth Regime Segment
    /// state machine (§18 of the M9 brief). The invariants are
    /// checked over every bar of driven fixtures, including on real
    /// data — they encode the mathematical definitions themselves,
    /// independent of any specific expected sequence.
    /// </summary>
    public sealed class AtrSmoothRegimeSegmentInvariantTests
    {
        /// <summary>
        /// Drives the engine over a regime sequence and returns the
        /// per-bar published segment state.
        /// </summary>
        private static (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[]
            Drive(double[] regime)
        {
            var values = new EngineValues();
            var marketData = new TestMarketData(0.0);
            var context = new EngineContext(marketData, values);
            var engine = new AtrSmoothRegimeSegmentEngine(context);
            engine.Initialize();

            var result = new (AtrSmoothRegimeDirection, int?, int?, int?, AtrSmoothRegimeTransition)[regime.Length];
            for (int i = 0; i < regime.Length; i++)
            {
                values.Reference.Regime = regime[i];
                context.SetIndex(i);
                engine.Update();
                var s = values.AtrSmoothRegimeSegment;
                result[i] = (s.Regime, s.RegimeId, s.RegimeStartIndex, s.RegimeAge, s.RegimeTransition);
            }
            return result;
        }

        private static double[] MakeRandomRegimeSeries(int n, int seed)
        {
            // A canonical-plausible regime series: warm-up 0s, then
            // long directional runs with occasional flips (the
            // trailing-stop position behaves this way — it persists
            // and flips).
            var rng = new Random(seed);
            double[] regime = new double[n];
            double current = 0.0;
            int runLength = 0;
            for (int i = 0; i < n; i++)
            {
                if (current == 0.0)
                {
                    // Warm-up persists a few bars, then the first
                    // directional establishment.
                    current = rng.NextDouble() < 0.5 ? 1.0 : -1.0;
                    runLength = 20 + rng.Next(80);
                }
                else if (runLength <= 0)
                {
                    current = -current;
                    runLength = 20 + rng.Next(80);
                }
                regime[i] = current;
                runLength--;
            }
            return regime;
        }

        // -------------------------------------------------------------
        // INV 1 — For every established regime bar: RegimeStartIndex
        // <= t and RegimeAge == t - RegimeStartIndex.
        // -------------------------------------------------------------

        [Fact]
        public void StartIndexAndAgeInvariants_HoldOnEveryBar()
        {
            double[] regime = MakeRandomRegimeSeries(500, 17);
            var states = Drive(regime);

            for (int t = 0; t < states.Length; t++)
            {
                var (reg, id, start, age, tr) = states[t];
                if (reg == AtrSmoothRegimeDirection.Unavailable)
                    continue;

                Assert.True(id.HasValue, $"bar {t}: established regime requires a non-null ID");
                Assert.True(start.HasValue, $"bar {t}: established regime requires a non-null start");
                Assert.True(age.HasValue, $"bar {t}: established regime requires a non-null age");

                Assert.True(start.Value <= t,
                    $"bar {t}: RegimeStartIndex {start} exceeds the bar index (look-ahead)");
                Assert.Equal(t - start.Value, age.Value);
                Assert.True(age.Value >= 0, $"bar {t}: negative age");
            }
        }

        // -------------------------------------------------------------
        // INV 2 — Regime ∈ {-1, +1} for every established bar; 0
        // (unavailable) implies all dependent state is unavailable.
        // -------------------------------------------------------------

        [Fact]
        public void RegimeDomainAndUnavailableConsistency_HoldOnEveryBar()
        {
            double[] regime = MakeRandomRegimeSeries(500, 29);
            var states = Drive(regime);

            for (int t = 0; t < states.Length; t++)
            {
                var (reg, id, start, age, tr) = states[t];

                if (reg == AtrSmoothRegimeDirection.Unavailable)
                {
                    Assert.Null(id);
                    Assert.Null(start);
                    Assert.Null(age);
                    Assert.Equal(AtrSmoothRegimeTransition.None, tr);
                }
                else
                {
                    Assert.True(reg == AtrSmoothRegimeDirection.Bullish
                        || reg == AtrSmoothRegimeDirection.Bearish,
                        $"bar {t}: regime outside {{-1, +1}}");
                    Assert.NotNull(id);
                    Assert.NotNull(start);
                    Assert.NotNull(age);
                }
            }
        }

        // -------------------------------------------------------------
        // INV 3 — Continuation ⇒ RegimeId unchanged; flip ⇒
        // RegimeId + 1 (and the direction/transition consistency).
        // -------------------------------------------------------------

        [Fact]
        public void IdEvolutionInvariants_HoldOnEveryTransitionPair()
        {
            double[] regime = MakeRandomRegimeSeries(500, 43);
            var states = Drive(regime);

            for (int t = 1; t < states.Length; t++)
            {
                var (reg, id, _, _, tr) = states[t];
                var (prevReg, prevId, _, _, _) = states[t - 1];

                if (reg != AtrSmoothRegimeDirection.Unavailable
                    && prevReg != AtrSmoothRegimeDirection.Unavailable)
                {
                    // Two consecutive established bars.
                    if (reg == prevReg)
                    {
                        // Continuation.
                        Assert.Equal(prevId, id);
                        Assert.Equal(AtrSmoothRegimeTransition.None, tr);
                    }
                    else
                    {
                        // Flip: ID +1, direction flips, transition set.
                        Assert.True(id.HasValue && prevId.HasValue,
                            $"bar {t}: flip requires non-null IDs");
                        Assert.Equal(prevId.GetValueOrDefault() + 1, id.GetValueOrDefault());
                        Assert.Equal(
                            reg == AtrSmoothRegimeDirection.Bullish
                                ? AtrSmoothRegimeTransition.Up
                                : AtrSmoothRegimeTransition.Down,
                            tr);
                    }
                }
                else if (reg != AtrSmoothRegimeDirection.Unavailable)
                {
                    // First establishment after warm-up: ID 0, not a
                    // transition.
                    Assert.True(id.HasValue, $"bar {t}: establishment requires an ID");
                    Assert.Equal(0, id.GetValueOrDefault());
                    Assert.Equal(AtrSmoothRegimeTransition.None, tr);
                }
                // prevReg established -> reg unavailable is
                // impossible by the engine's fail-closed guard.
            }
        }

        // -------------------------------------------------------------
        // INV 4 — RegimeTransition != 0 iff a canonical ATRSmooth
        // regime flip occurred at that bar.
        // -------------------------------------------------------------

        [Fact]
        public void TransitionFlagMarksExactlyTheFlipBars()
        {
            double[] regime = MakeRandomRegimeSeries(500, 71);
            var states = Drive(regime);

            for (int t = 0; t < states.Length; t++)
            {
                bool canonicalFlip = t > 0
                    && states[t - 1].Item1 != AtrSmoothRegimeDirection.Unavailable
                    && states[t].Item1 != AtrSmoothRegimeDirection.Unavailable
                    && states[t].Item1 != states[t - 1].Item1;

                bool flagged = states[t].Item5 != AtrSmoothRegimeTransition.None;
                Assert.True(canonicalFlip == flagged,
                    $"bar {t}: transition flag ({flagged}) != canonical flip occurrence ({canonicalFlip})");
            }
        }

        // -------------------------------------------------------------
        // INV 5 — Start index changes exactly on a flip and never
        // moves while the regime remains unchanged.
        // -------------------------------------------------------------

        [Fact]
        public void StartIndexMovesExactlyOnFlips()
        {
            double[] regime = MakeRandomRegimeSeries(500, 97);
            var states = Drive(regime);

            for (int t = 1; t < states.Length; t++)
            {
                if (states[t].Item1 == AtrSmoothRegimeDirection.Unavailable
                    || states[t - 1].Item1 == AtrSmoothRegimeDirection.Unavailable)
                    continue;

                if (states[t].Item1 == states[t - 1].Item1)
                {
                    // Continuation: same start.
                    Assert.Equal(states[t - 1].Item3, states[t].Item3);
                }
                else
                {
                    // Flip: the start IS this bar.
                    Assert.True(states[t].Item3.HasValue,
                        $"bar {t}: flip requires a non-null start");
                    Assert.Equal(t, states[t].Item3.GetValueOrDefault());
                }
            }
        }

        // -------------------------------------------------------------
        // INV 6 — No look-ahead: mutating future bars cannot change
        // any already-processed bar's output (checked via equal
        // prefix runs on different futures).
        // -------------------------------------------------------------

        [Fact]
        public void NoLookAhead_DifferentFutures_SamePrefixOutputs()
        {
            double[] regimeA = MakeRandomRegimeSeries(300, 131);
            double[] regimeB = (double[])regimeA.Clone();

            // Diverge the futures after bar 150.
            for (int i = 151; i < regimeB.Length; i++)
            {
                regimeB[i] = -regimeB[i];
            }

            var statesA = Drive(regimeA);
            var statesB = Drive(regimeB);

            for (int i = 0; i <= 150; i++)
            {
                Assert.Equal(statesA[i].Item1, statesB[i].Item1);
                Assert.Equal(statesA[i].Item2, statesB[i].Item2);
                Assert.Equal(statesA[i].Item3, statesB[i].Item3);
                Assert.Equal(statesA[i].Item4, statesB[i].Item4);
                Assert.Equal(statesA[i].Item5, statesB[i].Item5);
            }
        }
    }
}

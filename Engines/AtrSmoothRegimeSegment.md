# ATRSmooth Regime Segment Foundation (M9)

## 1. What this is

A canonical, deterministic description of the **current ATRSmooth
regime as a bounded temporal segment** — direction, identity, start,
age, and transition. It is a **state/segmentation primitive**, not a
trading feature: no score, no probability, no prediction, no entry or
exit logic. Later research features consume these values; this stage
defines the mathematically correct temporal primitive they build on.

## 2. What constitutes an ATRSmooth regime

The canonical ATRSmooth regime is the **source-declared signed
trailing-stop position bias** published by `ATRSmoothReferenceSource`
as `Reference.Regime`:

- `+1` — bullish (long) trailing-stop position bias,
- `-1` — bearish (short) trailing-stop position bias,
- `0` — initial / uncommitted (before the first bias is assigned).

The same value drives the Reversal stage's canonical
`TrailingStopPosition` mode and is verified bar-for-bar against the
original `AtrTrailingStopSmoothed` indicator. In the dual-reference
`HmaAtrSmooth` mode the composite publishes the identical value
(`HmaAtrSmoothCompositeSource.Regime => AtrSmoothSource.Regime`).

The segment stage is a **pure consumer** of this published value. It
performs no indicator mathematics, recomputes no ATRSmooth series,
and defines no second reversal semantic — there is exactly one
ATRSmooth calculation and one regime definition in the engine.

## 3. What constitutes a regime flip

A flip is a **strict change of the published canonical regime between
consecutive bars, from one established directional state to the
other**:

- Bullish flip: `-1 → +1`
- Bearish flip: `+1 → -1`

## 4. Why a price crossing ATRSmooth is NOT a regime flip

The ATRSmooth equilibrium line is the average of a VWMA and the ATR
trailing stop. A candle can cross that published line freely while
the trailing-stop position bias — the regime — stays unchanged: the
regime changes only when the stop itself flips side, which requires
price to move through the trailing stop (with the previous close on
the same side), not merely through the published average.

Concretely, in `ATRSmoothReferenceSource` the position becomes +1
only when `prevClose < prevStop && close > prevStop` and -1 only when
`prevClose > prevStop && close < prevStop`; otherwise it carries
forward. A close merely crossing the (VWMA + stop)/2 line can leave
the position untouched — that bar is **not** a transition.

This is enforced by construction: the segment stage reads only
`Values.Reference.Regime` and never reads `Reference.Price`,
`Distance.DirectionalExtension`, or the market data. A price
crossing is invisible to it. The 10 000-bar EURUSD fixture counts
real price crossings without regime flips and asserts every one of
them produces `RegimeTransition = None`.

## 5. Published outputs

All values live in `EngineValues.AtrSmoothRegimeSegment`
(`AtrSmoothRegimeSegmentRuntimeValues`).

| Output | Type | Meaning |
| --- | --- | --- |
| `Regime` | `AtrSmoothRegimeDirection` | `Bullish` / `Bearish` / `Unavailable` (warm-up). |
| `RegimeId` | `int?` | Monotonic segment identifier. First established regime = 0; every flip increments by exactly 1; all bars of one segment share it. Never a timestamp, never a bar index, never recycled. `null` during warm-up. |
| `RegimeStartIndex` | `int?` | Index of the first bar of the current segment. Changes exactly on a flip; never moves while the regime persists. `null` during warm-up. |
| `RegimeAge` | `int?` | **Zero-based** bars since the segment began: `RegimeAge = t − RegimeStartIndex`. First bar of a segment = 0, second = 1, … `null` during warm-up. |
| `RegimeTransition` | `AtrSmoothRegimeTransition` | `Up` (+1) bearish→bullish flip; `Down` (−1) bullish→bearish flip; `None` (0) continuation / warm-up / unavailable. The flip itself, never a price cross. |

### Unavailable representation

The repository's existing convention for unavailable integers is
`int? = null` (as `ReversalRuntimeValues.BarsSinceReversal`); the
segment stage uses exactly that, plus `Unavailable`/`None` enum
values for the two enum outputs. During warm-up:

```
Regime           = Unavailable
RegimeId         = null
RegimeStartIndex = null
RegimeAge        = null
RegimeTransition = None
```

## 6. Regime ID semantics

- The first **established directional** regime receives ID **0**.
  Zero is never assigned during warm-up merely because it is
  convenient — the first valid ID belongs to the first actual
  directional regime.
- Every subsequent flip increments the ID by **exactly 1** — one
  increment per flip, never on a continuation bar.
- IDs are never timestamps, never bar indexes, and never recycled
  during normal processing.
- The first establishment (0 → ±1) is **not** a transition and does
  not consume an ID increment: the ID is 0 at establishment.

## 7. Regime start semantics

`RegimeStartIndex` is the index of the first bar of the current
segment. It is set at establishment and re-set at each flip bar; on
every continuation bar it republishes the same value. It can never
move forward while the regime remains unchanged, and it is never
ahead of the current bar for any contract-conforming processing
sequence.

## 8. Zero-based age semantics

```
RegimeAge_t = t − RegimeStartIndex_t
```

The first bar of a segment has age 0, the second 1, the third 2.
Age is never one-based and is never defined as "number of completed
bars before the current bar."

## 9. Warm-up behavior

Before the first established directional regime exists (published
regime 0), the stage publishes the unavailable state (§5) and **does
not manufacture a regime**. Price action during warm-up — including
whipsaws across the ATRSmooth line — has no effect. The canonical
ATRSmooth trailing-stop position is assigned only ±1 once it leaves
0 and can never return to 0; if a published 0 ever follows an
established regime the stage fails closed with an
`InvalidOperationException` (upstream contract violation) rather
than inventing a state-machine edge that does not exist.

## 10. Reset / re-tick behavior

**Re-tick.** Live consumers re-call the pipeline for the same bar as
ticks arrive. The stage snapshots the committed end-of-previous-bar
segment state on the first call for a bar and restores it on every
re-tick, then recomputes the bar from that clean baseline. Re-ticks
never double-increment the age, never double-increment the ID,
never move the start index, and never duplicate a transition;
re-processing a bar with changed data applies the change exactly
once.

**Reset.** `Reset()` returns the stage to the initial unavailable
state (`Regime = Unavailable`, all metadata `null`, `Transition =
None`) and clears the re-tick snapshot. Replaying the same bars
after a reset reproduces every output exactly — no state leaks
across a reset.

## 11. No-look-ahead guarantee

Every value at bar `t` depends only on information available through
bar `t`: the canonical regime published for bar `t` and the committed
end-of-previous-bar segment state. Future ATRSmooth values, future
flips, future segment duration/end, and any retroactive rewrite of
prior bars are impossible by construction — the stage stores no
history to rewrite, and a later flip begins a new segment without
touching already-published bars. The engine never waits for a
regime's eventual end to assign its identity; the ID, start, and age
of a segment are final on the bar they describe.

## 12. State machine

```
UNAVAILABLE
    |  first established directional regime (published ±1)
    v
REGIME(id = 0, start = t, age = 0, transition = None)
    |  regime unchanged
    v
same REGIME(id, start unchanged, age + 1, transition = None)
    |  canonical flip (±1 → ∓1)
    v
REGIME(id + 1, start = t, age = 0, transition = ±1)
```

On establishment: `RegimeId = 0`, `RegimeStartIndex = t`,
`RegimeAge = 0`, `RegimeTransition = None`.
On continuation: same id, same start, `RegimeAge = t − start`,
`RegimeTransition = None`.
On flip: `RegimeId = previous + 1`, `RegimeStartIndex = t`,
`RegimeAge = 0`, `RegimeTransition = resulting regime's direction`
(`Up` for bearish→bullish, `Down` for bullish→bearish).

## 13. Worked example

The hand-computed golden fixture (independent of any internal
calculation — `AtrSmoothRegimeSegmentGoldenTests`):

```
bar:          0  1  2  3  4  5  6  7  8  9 10 11 12
regime:       0  0  +  +  +  +  -  -  -  -  +  +  +
RegimeId:     ·  ·  0  0  0  0  1  1  1  1  2  2  2
StartIndex:   ·  ·  2  2  2  2  6  6  6  6 10 10 10
Age:          ·  ·  0  1  2  3  0  1  2  3  0  1  2
Transition:   0  0  0  0  0  0 -1  0  0  0 +1  0  0
```

Bars 0–1: warm-up — everything unavailable. Bar 2: first
established regime (bullish) — ID 0, start 2, age 0, **no**
transition (the predecessor is not an established directional
state). Bars 3–5: continuation — age 1, 2, 3. Bar 6: bullish→bearish
flip — new segment ID 1, start 6, age 0, transition Down. Bars 7–9:
continuation. Bar 10: bearish→bullish flip — ID 2, start 10, age 0,
transition Up. Bars 11–12: continuation.

## 14. Pipeline placement & gating

Registered by `ResearchFeatureEngineBuilder` **after the Reference
stage and before the Reversal stage**, only when the composed
reference source is ATRSmooth-based:

- `ATRSmoothReferenceSource` (the ATRSmooth2 single-reference mode), or
- `HmaAtrSmoothCompositeSource` (the dual-reference mode, whose
  published regime IS the canonical ATRSmooth trailing-stop regime).

Every other reference mode (DarvasBox, Hma alone) never constructs
the stage; the segment values simply remain at their unavailable
defaults. The stage adds no parameters and no options.

## 15. Validation

- `AtrSmoothRegimeSegmentValidator.ValidateRegime` — the consumed
  regime must be exactly −1, 0, or +1 (never NaN/∞/other).
- `AtrSmoothRegimeSegmentValidator.Validate` — published-state
  consistency: unavailable ⇒ all metadata null and no transition;
  established ⇒ all metadata present, `RegimeAge == index − start`,
  and a flagged transition matches the resulting regime's direction.

## 16. Complexity

True **O(1) per bar**: a fixed number of field reads/writes per
update, no per-bar allocation, no historical series scan, no
windowed materialization. The 1 000 000-bar performance benchmark
runs with the stage active.

## 17. What this is NOT

Not a trend-sustainability score, not an exhaustion measure, not a
continuation or reversal probability, not a predictor of future
returns, and not evidence of profitability. It carries no statement
about the market's future behavior — only the mathematically exact
segmentation of the ATRSmooth regime up to the current bar.

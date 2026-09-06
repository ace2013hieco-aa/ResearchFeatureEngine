# Reversal Feature

## Definition

A **reversal** is a **strict state transition of the source-declared
signed regime state** between consecutive bars — a REGIME event, not a
line crossing.

The regime state is published by the selected reference source as
`Reference.Regime`. The semantics are defined by each reference source
and are opaque to the reversal engine:

- **ATRSmooth2**: the trailing-stop position bias
  (+1 bullish/long bias, -1 bearish/short bias, 0 initial/uncommitted),
  computed by `ATRSmoothReferenceSource` and verified bar-for-bar
  against the original `AtrTrailingStopSmoothed` `pos` series on
  10 000 real EURUSD M1 bars.
- **Darvas Box**: the positional state
  (+1 close above the upper boundary, 0 close inside the box — a real
  persistent Darvas state, NOT bearish, -1 close below the lower
  boundary), computed by `DarvasBoxReferenceSource`.

A reversal is a strict change of the regime value:

| Transition | Direction |
| --- | --- |
| 0 → +1 (inside → above) | `Up` (breakout) |
| 0 → -1 (inside → below) | `Down` |
| +1 → 0 (above → inside) | `Down` (return to box) |
| -1 → 0 (below → inside) | `Up` (return to box) |
| -1 → +1 | `Up` |
| +1 → -1 | `Down` |
| equal states | never a reversal |

**A candle crossing the ATR Smooth line while the regime stays
unchanged is NOT a reversal.** Darvas reversal semantics are strict
transitions of the Darvas positional regime and therefore include
breakout and return-to-box transitions. Only the current and the
previous bar are consulted — no future bar is read (no lookahead), so
historical replay and live/incremental processing produce identical
results.

## Outputs

All three are published by the `ReversalEngine` stage into
`EngineValues.Reversal` (`ReversalRuntimeValues`) and exposed on the
cTrader indicator as output series.

### `BarsSinceReversal` (`int?`)

Bar distance from the most recent reversal event (not elapsed time):

- `0` on the reversal bar,
- `1` on the next completed bar,
- `2`, `3`, ... incrementing on each same-side continuation bar,
- reset to `0` on the next reversal,
- `null` until the first reversal has occurred.

The indicator plots `double.NaN` (gap) while the value is `null`.

### `Direction` (`ReversalDirection`)

The direction of the **most recent** reversal (`Up` / `Down`), which
**persists until the next reversal** — it is not the current
price-vs-reference side. `None` until the first reversal.

The indicator plots `double.NaN` while `None`, then `+1` (`Up`) or
`-1` (`Down`).

### `IsReversalBar` (`bool`)

Step function: `true` only on the reversal bar itself, `false` on
every continuation bar. Useful for alerting/signal logic. `false`
until the first reversal. The indicator plots `1.0` on the reversal
bar, `0.0` otherwise, `NaN` (gap) before the first reversal.

## Reversal mode (`ReversalMode`)

Selectable via the cTrader **Reversal Mode** parameter and
`EngineOptions.ReversalMode`:

| Mode | Relation | Reversal |
| --- | --- | --- |
| `TrailingStopPosition` (default) | source-declared signed regime (`Reference.Regime`) | strict state transition — increase (0 → +1, -1 → 0, -1 → +1) is Up, decrease (+1 → 0, 0 → -1, +1 → -1) is Down, equal states never. For ATRSmooth2 this reproduces the original `AtrTrailingStopSmoothed` `pos` flips; for Darvas Box it covers breakout and return-to-box transitions. |
| `CloseToReference` (explicit opt-in) | sign of `close − reference` (`DirectionalExtension`); `>= reference` is ABOVE, `<` is BELOW | strict side change — treats a candle crossing the line as a reversal |

`TrailingStopPosition` mode is generic: it operates on the
source-declared regime without interpreting its meaning. For
ATRSmooth2 it reproduces the original `AtrTrailingStopSmoothed`
indicator's `pos` flips (verified bar-for-bar against an independent
reimplementation on 10 000 real EURUSD M1 bars); for Darvas Box it
covers breakout and return-to-box transitions of the positional
regime. It is the DEFAULT because the research specification defines
a reversal as a REGIME transition of the selected reference, not as a
candle crossing the reference line.

## Equality behavior

`close == reference` (`DirectionalExtension == 0`) is resolved
asymmetrically as **ABOVE** (`>=`). This is deterministic and avoids
oscillation: a reversal requires a strict sign change, so a bar that
sits exactly on the reference does not flip-flop the state.

## Initialization / uninitialized behavior

- `BarsSinceReversal = null`
- `Direction = None`

Both remain in this state until the first strict side change between
consecutive bars. The first bar never produces a reversal (no previous
bar).

## Live re-tick handling

Live streaming consumers (cTrader indicator) re-call `Update()` for
the same bar as ticks arrive. The engine snapshots the committed
end-of-previous-bar state on the first call for a bar and restores it
on re-ticks, so re-processing the same bar recomputes that bar's output
from a clean baseline without double-incrementing the counter or
manufacturing spurious reversals.

## Architecture

`ReversalEngine` extends `EngineBase` and runs as a pipeline stage
immediately after `DistanceEngine` (it consumes
`Distance.DirectionalExtension`). It owns the `ReversalRuntimeValues`
sub-object of `EngineValues` (single ownership), validates its input
via `ReversalValidator`, and publishes deterministically — matching
the existing stage pattern (Reference, Distance, Scale, Normalization,
Statistics). No ATRSmooth logic is duplicated; the reference price
comes from the existing `ATRSmoothReferenceSource`.

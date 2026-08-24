# ATRSmooth Reversal Feature

## Definition

A **reversal** is a transition of the **ATR Smooth regime itself**
between consecutive bars — a FLIP EVENT, not a line crossing.

The canonical ATR Smooth regime state is the trailing-stop position
bias published as `Reference.TrendPosition` (1 = bullish/long bias,
-1 = bearish/short bias, 0 = flat), computed by
`ATRSmoothReferenceSource` and verified bar-for-bar against the
original `AtrTrailingStopSmoothed` `pos` series on 10 000 real
EURUSD M1 bars.

| Regime | Condition |
| --- | --- |
| Bullish (ABOVE) | `TrendPosition > 0` (long bias) |
| Bearish (BELOW) | `TrendPosition <= 0` (short/flat bias) |

A reversal is a **strict regime change**:

| Reversal | Direction |
| --- | --- |
| Bullish → Bearish | `Down` (bearish) |
| Bearish → Bullish | `Up` (bullish) |

**A candle crossing the ATR Smooth line while the regime stays
unchanged is NOT a reversal.** Only the current and the previous bar
are consulted — no future bar is read (no lookahead), so historical
replay and live/incremental processing produce identical results.

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
| `TrailingStopPosition` (default) | sign of the ATR trailing-stop position bias (`Reference.TrendPosition`); `> 0` (long bias) is ABOVE, `<= 0` (short/flat) is BELOW | strict sign change (a flip in the trailing stop's own bias) — the canonical reversal semantic |
| `CloseToReference` (explicit opt-in) | sign of `close − reference` (`DirectionalExtension`); `>= reference` is ABOVE, `<` is BELOW | strict side change — treats a candle crossing the line as a reversal |

`TrailingStopPosition` mode reproduces the original
`AtrTrailingStopSmoothed` indicator's `pos` flips; verified
bar-for-bar against an independent reimplementation on 10 000 real
EURUSD M1 bars. It is the DEFAULT because the research specification
defines a reversal as an ATR Smooth REGIME FLIP, not as a candle
crossing the ATR Smooth line.

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

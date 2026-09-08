# ResearchFeatureEngine

A platform-independent quantitative research feature engine that transforms market data into deterministic, mathematically defined research features through a composable pipeline.

The core engine has **no cTrader dependencies** — cTrader lives only in the adapter/indicator layer. The same production pipeline drives cTrader, historical backtesting, replay, and research tooling.

[![Release](https://img.shields.io/badge/release-v1.1-blue)](https://github.com/ace2013hieco-aa/ResearchFeatureEngine/releases/tag/v1.1)
[![Tests](https://img.shields.io/badge/tests-214%2F214-brightgreen)](#testing)

---

## Pipeline

```
Market Data
    ↓
Reference      ← selected reference source (ATRSmooth2 equilibrium level
                 or Darvas Box midpoint — one active model per engine)
    ↓
Distance       ← close vs reference (directional + absolute)
    ↓
Regime Segment ← ATRSmooth regime segment metadata (M9; ATRSmooth-based modes)
    ↓
Reversal       ← strict regime-transition state machine (v1.1)
    ↓
Scale          ← characteristic scale (ATR)
    ↓
Normalization ← distance / scale (dimensionless feature)
    ↓    Statistics    ← rolling mean / std dev / median / MAD / range /
                   skewness / kurtosis
    ↓
EngineValues  → Consumer / cTrader
```

Each stage is an `EngineBase` with the Template-Method lifecycle (`Processing → OnUpdate → Ready`), owns its own runtime sub-object inside `EngineValues` (single ownership), validates its output before publishing, and is composable — adding a new model/source doesn't require modifying downstream engines.

See [`Project Vision and Architecture.md`](Project%20Vision%20and%20Architecture.md) for the full architecture.

---

## What's new in v1.1

### ATRSmooth reversal feature

A new `ReversalEngine` pipeline stage (after Distance) tracks the close-to-ATRSmooth relation as a deterministic state machine and publishes three values into `EngineValues.Reversal`:

| Output | Type | Description |
| --- | --- | --- |
| `BarsSinceReversal` | `int?` | Bar distance from the most recent reversal (`0` on the reversal bar, `1` on the next, …), reset to `0` on the next reversal. `null` until the first reversal. |
| `Direction` | `ReversalDirection` | Direction of the **most recent** reversal (`Up` / `Down`), persisting until the next reversal. `None` until the first reversal. |
| `IsReversalBar` | `bool` | Step function: `true` only on the reversal bar itself — for alerting/signal logic. `false` until the first reversal. |

### Two detection modes

Selectable via `EngineOptions.ReversalMode` / the cTrader **Reversal Mode** parameter:

| Mode | Relation | Reversal |
| --- | --- | --- |
| `TrailingStopPosition` (default) | source-declared signed regime (`Reference.Regime`) | strict state transition — increase (0 → +1, -1 → 0, -1 → +1) is Up, decrease (+1 → 0, 0 → -1, +1 → -1) is Down, equal states never. For ATRSmooth2 the regime is the trailing-stop position bias — **the canonical semantic: a reversal is an ATR Smooth REGIME FLIP, not a candle crossing the line.** For Darvas Box the regime is the positional state (+1 above upper / 0 inside / -1 below lower); transitions include breakout and return-to-box events. |
| `CloseToReference` (explicit opt-in) | sign of `close − reference`; `>= reference` is ABOVE, `<` is BELOW | strict side change |

**Verified bar-for-bar** against an independent reimplementation of the original `AtrTrailingStopSmoothed` `pos` series on 10 000 real EURUSD M1 bars — `Regime` equals `pos` and every reversal bar/direction matches.

No lookahead (only current + previous bar); live re-tick idempotent via state snapshotting. See [`Reversal/Reversal.md`](Reversal/Reversal.md) for full semantics, equality behavior, and initialization.

### Distribution shape statistics

The Statistics stage now also publishes Fisher–Pearson bias-corrected skewness (G1, n ≥ 3) and Fisher bias-corrected **excess** kurtosis (G2, n ≥ 4), computed with a numerically stable two-pass central-moment pass over the same observations as the other statistics (source-blind models; `LogReturn` is the canonical research interpretation). Flat (zero-variance) windows publish nothing — the last published value is retained. See [`Statistics/StatisticsShape.md`](Statistics/StatisticsShape.md) for formulas, zero-variance semantics, and reliability guidance.

### Live-bar statistics fix

The rolling mean / std dev were previously computed over **closed bars only**, which made them freeze on the live bar while every other stage included the current bar — the "distorted on live bars, smooth on history" symptom. Statistics now appends the live bar's latest close to the observations on every tick, so the mean/std respond smoothly to the live bar like the rest of the pipeline and the original reference indicator.

### Nullability cleanup

`EngineContext.Values` is now non-nullable; all `Context.Values!` / `MarketData!` null-forgiving operators removed. The engine and indicator build **warning-free** (the 9 `CS8602` warnings are resolved).

---

## cTrader indicator

The indicator (`Indicators/ResearchFeatureEngineIndicator/`) is a **thin adapter** — it contains no math. It wires cTrader's bar stream to the production pipeline via `CTraderMarketData` and publishes `EngineValues` to output series.

### Outputs

| Output | Color | Notes |
| --- | --- | --- |
| Reference | DodgerBlue | ATR-smoothed equilibrium |
| Directional Distance | Orange | `close − reference` (signed) |
| Absolute Distance | Magenta | `|close − reference|` |
| Scale (ATR) | Gray | characteristic scale |
| Normalized | Lime | dimensionless feature |
| Mean (Rolling) | Aqua | rolling mean of close |
| Std Dev (Rolling) | Yellow | rolling std dev of close |
| Skewness (Rolling) | Pink | Fisher–Pearson G1; retained value on flat windows |
| Kurtosis (Rolling) | Cyan | Fisher G2 **excess**; retained value on flat windows |
| **Bars Since Reversal** | White | `0` on reversal, increments; gap before first reversal (v1.1) |
| **Reversal Direction** | Red | `+1` Up / `-1` Down; gap before first reversal (v1.1) |
| **Reversal Bar** | White (histogram) | `1` on the reversal bar, `0` otherwise; gap before first reversal (v1.1) |

### Parameters

| Parameter | Group | Default |
| --- | --- | --- |
| **Reference Type** | Reference | `ATRSmooth2` (v1.2; selects the single active reference model) |
| ATR Period | Reference: ATRSmooth2 | 16 |
| ATR Multiplier | Reference: ATRSmooth2 | 5.1 |
| VWMA Smooth Length | Reference: ATRSmooth2 | 100 |
| **Box Length** | Reference: Darvas Box | 5 (v1.2; min 3) |
| Scale ATR Period | Scale | 14 |
| Statistics Window | Statistics | 252 |
| **Reversal Mode** | Reversal | `TrailingStopPosition` (v1.1; regime-transition semantic) |

The indicator renders in a dedicated sub-pane (`IsOverlay = false`), so it does not obscure the price chart.

### Reference models (v1.2)

Exactly ONE reference model is active per engine instance, selected by the **Reference Type** parameter and constructed exclusively at initialization (`ReferenceSourceFactory` — the non-selected model is never instantiated and its parameters are inert):

| Type | Reference.Price | Reference.Regime | Reversal |
| --- | --- | --- | --- |
| `ATRSmooth2` | `(VWMA(close, smoothLength) + ATRTrailingStop) / 2` | trailing-stop position bias: +1 bullish / -1 bearish / 0 initial | strict transition of the regime — a trailing-stop flip. **Crossing the ATRSmooth published reference line is not itself an ATRSmooth reversal.** |
| `DarvasBox` | `(Upper + Lower) / 2` of the current box | positional state: +1 close above Upper / 0 inside (real persistent state) / -1 close below Lower | strict transitions of the positional regime — includes breakout and return-to-box transitions. The box midpoint jumps on box replacement; that structural effect is intentional and is never smoothed. |
| `HmaAtrSmooth` | ATRSmooth2 equilibrium (the composite keeps the ATRSmooth2 pipeline semantics bit-identically) | trailing-stop position bias (ATRSmooth2) | identical to `ATRSmooth2` — the HMA is an additive parallel canonical value, not a replacement measurement level. |

`Reference.Price` is the source-defined scalar measurement level against which Distance measures signed price deviation — it is not universally an "equilibrium price" (the Darvas box midpoint is a measurement level, not an equilibrium).

### Dual-reference mode (v1.3)

`HmaAtrSmooth` is the explicit composite dual-reference mode: the engine owns exactly one canonical `HmaReferenceSource` (HMA of close, period `HMA Period`; direct window recompute — O(P^1.5)/bar, no incremental state) and one canonical `ATRSmoothReferenceSource`, both advanced once per bar by `HmaAtrSmoothCompositeSource`. Selecting `Hma` alone constructs only the HMA source — ATRSmooth is never silently instantiated. The canonical HMA value is published in `ReferenceRuntime.Hma` (NaN until index `P + floor(sqrt(P)) - 2`; e.g. bar 18 at P=16). The HMA source's warm-up close fallback (`ComputeReference` return) is a pipeline-contract value only — the research features below never consume it.

Two research stages are registered only in this mode (after Reference, before Reversal):

| Stage | Formula | Notes |
| --- | --- | --- |
| **Mean HMA–ATRSmooth Distance** | `mean(HMA − ATRSmooth)` over `Mean HMA-ATRSmooth Window` | signed; + = HMA above the smoothed equilibrium. True O(1)/bar (running sum, no per-bar allocation). NaN until `Runtime.Hma` is genuinely valid. |
| **HMA/Price–ATRSmooth Alignment** | `Aligned(+1) iff (HMA > ATRSmooth) == (Close > ATRSmooth)`, else `Misaligned(−1)`; `Unavailable(0)` during warm-up or exact equality | current-bar state only — no rolling mean, no smoothing, no hysteresis, no epsilon (strict `>` / `<`). |

Both stages consume the canonical producer runtimes directly — no duplicate indicator calculations. The HMA implementation is pinned by oracle-first golden tests (`Tests/Reference/HmaReferenceSourceGoldenTests.cs`): constant series produce exactly the constant, ramp/random fixtures match an independent direct-definition oracle bit-for-bit, and the warm-up boundary is exact.

---

### ATRSmooth Regime Segment foundation (M9)

A new `AtrSmoothRegimeSegmentEngine` pipeline stage (after Reference, before Reversal) layers **bounded temporal-segment metadata** on the canonical ATRSmooth regime — the trailing-stop position bias (`Reference.Regime`) — and publishes into `EngineValues.AtrSmoothRegimeSegment`:

| Output | Type | Description |
| --- | --- | --- |
| `Regime` | `AtrSmoothRegimeDirection` | `Bullish` / `Bearish` / `Unavailable` (warm-up). |
| `RegimeId` | `int?` | Monotonic segment ID: first established regime = 0, every flip +1, shared by all bars of a segment. `null` during warm-up. |
| `RegimeStartIndex` | `int?` | First bar of the current segment; changes exactly on a flip. `null` during warm-up. |
| `RegimeAge` | `int?` | **Zero-based** bars since the segment began (`t − start`; first bar = 0). `null` during warm-up. |
| `RegimeTransition` | `AtrSmoothRegimeTransition` | `Up` (+1) bearish→bullish flip, `Down` (−1) bullish→bearish flip, `None` (0) otherwise. The flip itself — never a price crossing of the ATRSmooth line. |

The stage is a pure consumer of the canonical published regime (no second ATRSmooth calculation, no second reversal definition). It is registered only for the ATRSmooth-based compositions (`ATRSmooth2`, `HmaAtrSmooth`); every other mode leaves the values at their unavailable defaults. True O(1) per bar; re-tick idempotent and reset/replay deterministic (snapshot/restore, same pattern as the Reversal stage). Pinned by a hand-computed 13-bar golden oracle, adversarial price-crossing fixtures (10 000 real EURUSD M1 bars), and executable invariant tests. See [`Engines/AtrSmoothRegimeSegment.md`](Engines/AtrSmoothRegimeSegment.md) for the full semantics, the worked example, and the warm-up/reset/re-tick contracts.

---

## Build & test

```bash
# Build the platform-independent engine (net6.0)
dotnet build ResearchFeatureEngine.csproj

# Build the cTrader indicator
dotnet build Indicators/ResearchFeatureEngineIndicator/ResearchFeatureEngineIndicator.csproj

# Run the full test suite (xUnit, net10.0)
dotnet test Tests/ResearchFeatureEngine.Tests.csproj
```

Both .NET SDK 6 and 10 are supported (6 for the engine/indicator, 10 for the test project).

## <a name="testing"></a>Testing

- **Full suite: 427/427 passing.**
- Mathematical correctness, determinism, long-run stability (100k bars), performance benchmarks, real-market-data validation (10k EURUSD M1 bars), and cross-platform consistency.
- 28 reversal-specific tests: all 7 required cases, equality boundary, multiple alternating reversals, re-tick idempotency, lookahead, both modes, the step signal, and the real-data comparison against the original indicator.
- 32 skewness/kurtosis tests: independent golden references (Python two-pass computation), symmetric/asymmetric/heavy-tailed samples, the excess-kurtosis convention, minimum-n and zero-variance publication semantics, numerical stability at a 1e6 baseline, outlier sensitivity, all three sources, live-tick, fresh-pass, discontinuity recovery, and bit-for-bit determinism.
- No regressions in the existing ATRSmooth / statistics / determinism / long-run suites.

---

## Project layout

```
Core/                EngineContext, EngineBase, EngineValues, StatisticType,
                     ReversalDirection, ReversalMode, ReferenceType
Reference/           IReferenceSource, ATRSmoothReferenceSource,
                     DarvasBoxReferenceSource, ReferenceRuntime,
                     configuration, validation
Engines/             ReferenceEngine, DistanceEngine, validators
Reversal/            ReversalEngine, ReversalRuntimeValues, validator, docs (v1.1)
Scale/               ScaleEngine, ATRScaleModel
Normalization/       NormalizationEngine, ScaleNormalizationModel
Statistics/          StatisticsEngine, StatisticsWindow, models, publisher
Composition/         ResearchFeatureEngine, builder, configuration, options,
                     ReferenceSourceFactory (exclusive selection)
Adapters/            CTraderMarketData, CTraderPriceSeries (cTrader only)
Indicators/          ResearchFeatureEngineIndicator (cTrader only)
ATRsmooth2/          Original AtrTrailingStopSmoothed reference indicator
tools/               CTraderHarness (CSV runner), ComputeExpected
Tests/               xUnit tests + test data (EURUSD_M1_10000.csv)
```

## License

See the repository for license details.

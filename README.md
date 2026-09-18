# ResearchFeatureEngine

A platform-independent quantitative research feature engine that transforms market data into deterministic, mathematically defined research features through a composable pipeline.

The core engine assembly has **no cTrader dependencies** — the cTrader API is referenced only by the adapter assembly and the cTrader applications (see [Architecture boundary](#architecture-boundary)). The same production pipeline drives cTrader, historical backtesting, replay, and research tooling.

[![Tests](https://img.shields.io/badge/tests-517%2F517-brightgreen)](#testing)
[![CI](https://github.com/ace2013hieco-aa/ResearchFeatureEngine/actions/workflows/ci.yml/badge.svg)](https://github.com/ace2013hieco-aa/ResearchFeatureEngine/actions/workflows/ci.yml)

Total tests: 496 .NET (CI runs the 491 portable tests; 5 source-identity tests `V11_10`–`V11_12` pin private local captures and run only on the author's machine) + 26 Python (`tests_python/`).<br>
M10.1: 34 BulkExport golden tests (G1–G19 + V11_1–V11_9).<br>
M11.1: 354 reference/engine tests + 2 HmaAtrSmooth geometry tests.<br>
M11.2A: 4 BulkExport source-admission tests (V11_11, V11_12) — 38 golden tests total.

---

## Quick Start (Python)

```bash
pip install research-feature-engine
```

```python
from research_feature_engine import HmaAtrSmooth, MarketData

md = MarketData.from_csv("Tests/TestData/EURUSD_M1_10000.csv")
engine = HmaAtrSmooth(md, atr_period=14, atr_multiplier=2.0, smooth_length=100)
df = engine.run()
print(df[["reference_price", "scale", "mean", "std_dev"]].head())
```

> **Note:** This package uses pythonnet to load the .NET 6 core engine. The `PYTHONNET_RUNTIME` environment variable is automatically set to `coreclr` at import time — no manual configuration needed. On Linux/macOS, install .NET 6 runtime first.

---

## Architecture

```
Market Data → Reference → Distance → (Darvas / HMA composites) →
  Regime Segment → Reversal → Scale → Normalization → Statistics → EngineValues
```

A single published-value surface (`EngineValues`) reaches every consumer — indicators, BulkExport, tests, tooling — with no duplicated mathematics. See [`docs/architecture.svg`](docs/architecture.svg) for the layered diagram (applications → adapters → core with the boundary line), and [`Project Vision and Architecture.md`](Project%20Vision%20and%20Architecture.md) for the full architecture write-up.

---

## Adapters

| Platform | Status | Surface |
|---|---|---|
| **cTrader indicator** | ✅ Shipped | `Adapters/ResearchFeatureEngine.CTrader` |
| **Python (CSV harness)** | ✅ Shipped | `tools/CTraderHarness` |
| **Python adapter (`pip install research-feature-engine`)** | ✅ Shipped | pythonnet wrapper + PyPI release |
| **MT5 adapter** | 🚧 Planned | C# wrapper DLL + MQL5 indicator |
| **MT4 indicator** | ❌ Out of scope | — |

---

## Pipeline

The canonical measurement pipeline, in registration order:

```
Market Data (IMarketData)
    ↓
Reference      ← selected reference source (ATRSmooth2 equilibrium level,
                 Darvas Box midpoint, HMA, or the HmaAtrSmooth composite —
                 exactly one active model per engine)
    ↓
Distance       ← close vs reference (directional + absolute)
    ↓
Darvas / HMA composites ← mode-gated research stages
                 (Darvas closing distances; HMA–ATRSmooth distance
                 and alignment)
    ↓
Regime Segment ← ATRSmooth regime segment metadata (M9; ATRSmooth-based
                 modes only)
    ↓
Reversal       ← strict regime-transition state machine
    ↓
Scale          ← characteristic scale (ATR)
    ↓
Normalization ← distance / scale (dimensionless feature)
    ↓
Statistics     ← rolling mean / std dev / median / MAD / range /
                 skewness / kurtosis
    ↓
EngineValues   → single published-value surface for every consumer
                 (indicator, BulkExport, tests, tooling)
```

Each stage is an `EngineBase` with the Template-Method lifecycle (`Processing → OnUpdate → Ready`), owns its own runtime sub-object inside `EngineValues` (single ownership), validates its output before publishing, and is composed by `ResearchFeatureEngineBuilder` with type-gated registration — stages for one reference mode are never constructed in another mode.

See [`Project Vision and Architecture.md`](Project%20Vision%20and%20Architecture.md) for the full architecture.

---

## Reversal vs. regime-segment semantics

Two distinct regime-related quantities are published; they are **not** the same event counter:

- **`EngineValues.Reversal`** (ReversalEngine) counts **strict published regime changes, including the first establishment**. The first `0 → ±1` establishment of the regime IS a reversal bar (`IsReversalBar = true`, `BarsSinceReversal = 0`, direction set). Every later strict regime change (a trailing-stop position flip in ATRSmooth modes) is another reversal.
- **`EngineValues.AtrSmoothRegimeSegment`** (M9 segment stage) tracks bounded temporal segments of the same canonical regime. A **segment transition represents only established directional flips** (`−1 ↔ +1`). The first `0 → ±1` establishment **starts segment 0 but does not constitute a segment transition** — `RegimeTransition` stays `None` on the establishment bar, and the segment ID counter does not increment.

In short: establishment counts as a *reversal* but not as a *segment transition*; after establishment, every regime flip is both. Crossing the published reference line is neither — for ATRSmooth the regime is the trailing-stop position bias, so only the trailing-stop position flip changes the regime.

See [`Reversal/Reversal.md`](Reversal/Reversal.md) and [`Engines/AtrSmoothRegimeSegment.md`](Engines/AtrSmoothRegimeSegment.md) for full semantics.

---

## Reference models

Exactly ONE reference model is active per engine instance, selected by the **Reference Type** parameter and constructed exclusively at initialization (`ReferenceSourceFactory` — the non-selected model is never instantiated and its parameters are inert):

| Type | Reference.Price | Reference.Regime | Reversal |
| --- | --- | --- | --- |
| `ATRSmooth2` | `(VWMA(close, smoothLength) + ATRTrailingStop) / 2` | trailing-stop position bias: +1 bullish / -1 bearish / 0 initial | strict transition of the regime — a trailing-stop flip. **Crossing the ATRSmooth published reference line is not itself an ATRSmooth reversal.** |
| `DarvasBox` | `(Upper + Lower) / 2` of the current box | positional state: +1 close above Upper / 0 inside (real persistent state) / -1 close below Lower | strict transitions of the positional regime — includes breakout and return-to-box transitions. The box midpoint jumps on box replacement; that structural effect is intentional and is never smoothed. |
| `Hma` | canonical HMA of close (direct window recompute) | initial 0 | strict transitions |
| `HmaAtrSmooth` | ATRSmooth2 equilibrium (the composite keeps the ATRSmooth2 pipeline semantics bit-identically) | trailing-stop position bias (ATRSmooth2) | identical to `ATRSmooth2` — the HMA is an additive parallel canonical value, not a replacement measurement level. |

`Reference.Price` is the source-defined scalar measurement level against which Distance measures signed price deviation — it is not universally an "equilibrium price" (the Darvas box midpoint is a measurement level, not an equilibrium).

### Dual-reference mode

`HmaAtrSmooth` is the explicit composite dual-reference mode: the engine owns exactly one canonical `HmaReferenceSource` (HMA of close, period `HMA Period`; direct window recompute — O(P^1.5)/bar, no incremental state) and one canonical `ATRSmoothReferenceSource`, both advanced once per bar by `HmaAtrSmoothCompositeSource`. Selecting `Hma` alone constructs only the HMA source — ATRSmooth is never silently instantiated. The canonical HMA value is published in `ReferenceRuntime.Hma` (NaN until index `P + floor(sqrt(P)) - 2`; e.g. bar 18 at P=16).

Two research stages are registered only in this mode (after Reference, before Reversal):

| Stage | Formula | Notes |
| --- | --- | --- |
| **Mean HMA–ATRSmooth Distance** | `mean(HMA − ATRSmooth)` over `Mean HMA-ATRSmooth Window` | signed; + = HMA above the smoothed equilibrium. True O(1)/bar (running sum, no per-bar allocation). NaN until `Runtime.Hma` is genuinely valid. |
| **HMA/Price–ATRSmooth Alignment** | `Aligned(+1) iff (HMA > ATRSmooth) == (Close > ATRSmooth)`, else `Misaligned(−1)`; `Unavailable(0)` during warm-up or exact equality | current-bar state only — no rolling mean, no smoothing, no hysteresis, no epsilon (strict `>` / `<`). |

Both stages consume the canonical producer runtimes directly — no duplicate indicator calculations. The HMA implementation is pinned by oracle-first golden tests (`Tests/Reference/HmaReferenceSourceGoldenTests.cs`): constant series produce exactly the constant, ramp/random fixtures match an independent direct-definition oracle bit-for-bit, and the warm-up boundary is exact.

### ATRSmooth regime segment metadata (M9)

A `AtrSmoothRegimeSegmentEngine` pipeline stage (after Reference, before Reversal) layers **bounded temporal-segment metadata** on the canonical ATRSmooth regime — the trailing-stop position bias (`Reference.Regime`) — and publishes into `EngineValues.AtrSmoothRegimeSegment`:

| Output | Type | Description |
| --- | --- | --- |
| `Regime` | `AtrSmoothRegimeDirection` | `Bullish` / `Bearish` / `Unavailable` (warm-up). |
| `RegimeId` | `int?` | Monotonic segment ID: first established regime = 0, every flip +1, shared by all bars of a segment. `null` during warm-up. |
| `RegimeStartIndex` | `int?` | First bar of the current segment; changes exactly on a flip. `null` during warm-up. |
| `RegimeAge` | `int?` | **Zero-based** bars since the segment began (`t − start`; first bar = 0). `null` during warm-up. |
| `RegimeTransition` | `AtrSmoothRegimeTransition` | `Up` (+1) bearish→bullish flip, `Down` (−1) bullish→bearish flip, `None` (0) otherwise. The flip itself — never a price crossing of the ATRSmooth line. |

The stage is a pure consumer of the canonical published regime (no second ATRSmooth calculation, no second reversal definition). It is registered only for the ATRSmooth-based compositions (`ATRSmooth2`, `HmaAtrSmooth`); every other mode leaves the values at their unavailable defaults. See [`Engines/AtrSmoothRegimeSegment.md`](Engines/AtrSmoothRegimeSegment.md).

---

## Measurement export (BulkExport)

[`tools/BulkExport`](tools/BulkExport/README.md) is the **canonical measurement extraction boundary**: it turns the engine's published runtime values into deterministic, hash-pinned CSV research artifacts. It contains zero engine mathematics — every exported token is read directly from `EngineValues` after the engine's own `Update()`, on a pipeline composed by the production `ResearchFeatureEngineBuilder`.

Guarantees:

- **deterministic output** — fixed column order, invariant-culture formatting, byte-identical on rerun;
- **frozen source identity** — the capture must be registered and SHA-256-pinned before processing (live data has no path at all);
- **SHA-256 verification** — source hash checked before and after the run; every artifact ships with a manifest carrying the artifact hash;
- **first-calendar-year research partition** — per dataset, leap-correct calendar-year boundary;
- **holdout firewall** — everything after the research partition is sealed from research use;
- **atomic finalization** — output is written to a `.partial` file and atomically renamed; an existing artifact is never overwritten;
- **no duplicated engine mathematics** — extraction only.

### M10 status

Production Year-1 measurement artifacts were certified for **EURUSD Tick100** and **XAUUSD Tick50** at M10.1 (four reference modes each), produced from owner-ratified frozen recorder captures. **M11.2A** extended the Distance certification chain to admit **XAUUSD Tick25** and **XAUUSD Tick100** — both registered with SHA-256 pins and verified Year-1 row-count invariants (2,234,316 and 68,255 respectively; Tick50 at 130,665 is the unchanged control). Artifacts live outside the repository under `out/` (gitignored) and are the handoff surface for downstream research programs.

---

## Architecture boundary

```
Applications (cTrader indicators, tools)
    ↓ reference
Adapters (ResearchFeatureEngine.CTrader assembly — cAlgo.API)
    ↓ reference
Core (ResearchFeatureEngine assembly — pure engine abstractions,
      models, algorithms; NO cTrader API, NO adapter reference)
```

- The **core engine** (`ResearchFeatureEngine.csproj`) is platform-independent: its project references contain no cTrader API package and its sources contain no cTrader-specific dependency — a future accidental cTrader dependency inside Core is a compile error, not a silent linkage.
- The **adapter assembly** (`Adapters/ResearchFeatureEngine.CTrader.csproj`) references Core and `cAlgo.API`, and owns `CTraderMarketData` / `CTraderPriceSeries` (adapting cTrader `Bars`/`DataSeries` to the platform-independent `IMarketData`/`IPriceSeries`).
- **Applications** (the indicators under `Indicators/`) reference Core and the adapter; the adapter is a thin layer with no engine mathematics.
- The dependency direction is one-way: Core never references Adapters or Applications.

Adding a new measurement family follows the documented canonical protocol in [`Composition/MeasurementFamilyAddition.md`](Composition/MeasurementFamilyAddition.md) — explicit, type-gated, multi-location wiring by design.

---

## cTrader indicator

The indicator (`Indicators/ResearchFeatureEngineIndicator/`) is a **thin adapter** — it contains no math. It wires cTrader's bar stream to the production pipeline via `CTraderMarketData` and publishes `EngineValues` to output series.

### Outputs

| Output | Color | Notes |
| --- | --- | --- |
| Reference | DodgerBlue | selected reference model's measurement level |
| Directional Distance | Orange | `close − reference` (signed) |
| Absolute Distance | Magenta | `|close − reference|` |
| Scale (ATR) | Gray | characteristic scale |
| Normalized | Lime | dimensionless feature |
| Mean (Rolling) | Aqua | rolling mean of close |
| Std Dev (Rolling) | Yellow | rolling std dev of close |
| Skewness (Rolling) | Pink | Fisher–Pearson G1; retained value on flat windows |
| Kurtosis (Rolling) | Cyan | Fisher G2 **excess**; retained value on flat windows |
| **Bars Since Reversal** | White | `0` on reversal, increments; gap before first reversal |
| **Reversal Direction** | Red | `+1` Up / `-1` Down; gap before first reversal |
| **Reversal Bar** | White (histogram) | `1` on the reversal bar, `0` otherwise; gap before first reversal |

### Parameters

| Parameter | Group | Default |
| --- | --- | --- |
| **Reference Type** | Reference | `ATRSmooth2` (selects the single active reference model) |
| ATR Period | Reference: ATRSmooth2 | 16 |
| ATR Multiplier | Reference: ATRSmooth2 | 5.1 |
| VWMA Smooth Length | Reference: ATRSmooth2 | 100 |
| **Box Length** | Reference: Darvas Box | 5 (min 3) |
| **HMA Period** | Reference: HMA | 16 (min 2) |
| Mean HMA-ATRSmooth Window | Research Features | 20 |
| Mean Darvas Window | Research Features | 20 |
| Scale ATR Period | Scale | 14 |
| Statistics Window | Statistics | 252 |
| **Reversal Mode** | Reversal | `TrailingStopPosition` (regime-transition semantic; `CloseToReference` is the explicit opt-in) |

The indicator renders in a dedicated sub-pane (`IsOverlay = false`), so it does not obscure the price chart. Statistics are current-bar-inclusive (the live bar's latest close is in the rolling window on every tick), so the rolling statistics respond to the live bar like the rest of the pipeline.

---

## Python adapter

The `research-feature-engine` package provides a pythonnet-based Python binding to the .NET 6 core engine. It ships prebuilt DLLs as package data, so no .NET build step is needed after install.

```bash
pip install research-feature-engine
```

```python
from research_feature_engine import HmaAtrSmooth, MarketData

md = MarketData.from_csv("EURUSD_M1_10000.csv")
engine = HmaAtrSmooth(md)
df = engine.run()
print(df[["reference_price", "directional_distance", "scale"]].head())
```

The four reference modes are available as `ATRSmooth2`, `DarvasBox`, `Hma`, and `HmaAtrSmooth`. Each accepts an optional `EngineOptions` for statistics window, reversal mode, and statistics source.

### Quick build & test (development)

```bash
# Build the .NET DLLs into research_feature_engine/data/
python build_dlls.py

# Install in editable mode
pip install -e ".[dev]"

# Run Python tests
pytest tests_python/

# Run the example scripts
python examples/basic_indicator.py
python examples/multi_engine_pipeline.py
python examples/pandas_integration.py
```

---

## Build & test

```bash
# Build the platform-independent core engine (net6.0)
dotnet build ResearchFeatureEngine.csproj

# Build the cTrader adapter assembly
dotnet build Adapters/ResearchFeatureEngine.CTrader.csproj

# Build the cTrader indicator (application)
dotnet build Indicators/ResearchFeatureEngineIndicator/ResearchFeatureEngineIndicator.csproj

# Run the full test suite (xUnit, net10.0)
dotnet test Tests/ResearchFeatureEngine.Tests.csproj -c Release
```

Both .NET SDK 6 and 10 are supported (6 for the engine/adapter/indicator, 10 for the test project).

## <a name="testing"></a>Testing

- **Full Release suite: 517/517 passing** (496 .NET + 26 Python).
- **M10.1:** 34 BulkExport golden tests (G1–G19 + V11_1–V11_9). Certified production Year-1 artifacts: EURUSD Tick100, XAUUSD Tick50 (4 reference modes each).
- **M11.2A:** 38 BulkExport golden tests total (adds V11_11 source-identity pins and V11_12 Year-1 row-count invariants). XAUUSD Tick25 and XAUUSD Tick100 are admitted to the Distance certification chain — source identity + Year-1 invariants only. Their M11.2 production artifacts are **not yet generated**.
- **M11.1:** 354 reference/engine tests + 2 HmaAtrSmooth geometry tests (HmaAtrSmoothDistance, Alignment).
- Mathematical correctness (hand-computed golden oracles per reference source), determinism, long-run stability (100k bars), performance benchmarks, real-market-data validation (10k EURUSD M1 bars), and cross-platform consistency.
- Reversal semantics (regime-transition and close-to-reference modes), re-tick idempotency, lookahead, and real-data comparison against the original indicator.
- M9 segment stage: hand-computed golden oracle, adversarial price-crossing fixtures (10 000 real EURUSD M1 bars), executable invariants, reset/replay determinism.
- Skewness/kurtosis: independent golden references (Python two-pass computation), convention and minimum-n semantics, numerical stability at a 1e6 baseline.
- BulkExport (M10): golden export tests — schema/column gating per mode, manifest determinism, hash-pinned source identity, refuse-overwrite, atomic finalization, Year-1 partition and holdout firewall, recorder schema validation (6- and 7-column captures).
- No regressions in the existing ATRSmooth / statistics / determinism / long-run suites; the suite count only grows across milestones.

---

## Project layout

```
Core/                EngineContext, EngineBase, EngineValues, enums
Reference/          IReferenceSource, ATRSmoothReferenceSource,
                     DarvasBoxReferenceSource, HmaReferenceSource,
                     HmaAtrSmoothCompositeSource, ReferenceRuntime,
                     configuration, validation
Engines/            ReferenceEngine, DistanceEngine, research-stage
                     engines, validators
Reversal/           ReversalEngine, ReversalRuntimeValues, validator, docs
Scale/              ScaleEngine, ATRScaleModel
Normalization/       NormalizationEngine, ScaleNormalizationModel
Statistics/         StatisticsEngine, StatisticsWindow, models, publisher
Composition/         ResearchFeatureEngine, builder, configuration, options,
                     ReferenceSourceFactory (exclusive selection),
                     MeasurementFamilyAddition.md (family protocol)
Models/             EngineValues + per-stage runtime values and models
Interfaces/         IMarketData, IPriceSeries, IEngine
Adapters/           CTrader adapter ASSEMBLY (ResearchFeatureEngine.CTrader):
                    CTraderMarketData, CTraderPriceSeries (cAlgo.API here)
                    ResearchFeatureEngine.Python — pythonnet adapter (PythonMarketData,
                    PythonEngineFactory)
Indicators/          cTrader applications (thin adapters, no math)
pyproject.toml       Python package build config (hatchling)
build_dlls.py        Build .NET DLLs into research_feature_engine/data/
research_feature_engine/  Python package (engine.py, market_data.py, data/)
tests_python/        Python pytest suite (26 tests)
examples/            Three example scripts (basic_indicator, multi_engine_pipeline,
                    pandas_integration)
tools/               BulkExport (canonical measurement export),
                    CTraderHarness (CSV runner)
Tests/               xUnit tests + test data (EURUSD_M1_10000.csv)
```

## License

[MIT](LICENSE) — Copyright (c) 2026 Osat Zoghi.

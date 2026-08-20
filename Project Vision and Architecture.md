# ResearchFeatureEngine — Project Vision & Target Architecture

## 1. Project Vision

ResearchFeatureEngine is a platform-independent quantitative research feature engine.

Its purpose is to transform market data into deterministic, mathematically defined research features through a composable pipeline.

The core mathematical engine must not depend on cTrader APIs. cTrader belongs exclusively in the adapter/host/indicator layer.

Target environments include cTrader, historical backtesting, replay, future data adapters, research tooling, and real-time execution.

## 2. Canonical Pipeline

    Market Data
        ↓
    Reference
        ↓
    Distance
        ↓
    Scale
        ↓
    Normalization
        ↓
    Statistics
        ↓
    EngineValues
        ↓
    Consumer / cTrader

Reference defines market equilibrium.

Distance measures current price relative to the reference and produces directional and absolute distance.

Scale produces the characteristic scale used to express distance relative to market volatility/scale.

Normalization combines measurement and characteristic scale to produce a dimensionless feature.

Statistics maintains statistical information over the configured rolling observation window.

## 3. Architectural Principles

### Platform isolation
Core code must not reference cAlgo/cTrader types. Only adapters and cTrader host/indicator code may reference cAlgo APIs.

### Dependency direction
Engines depend on model/source interfaces rather than concrete algorithms.

### Single ownership
Each engine owns its corresponding runtime sub-object inside EngineValues. No duplicate runtime-state objects may represent the same output.

### Determinism
Identical market data, configuration, and execution sequence must produce identical results.

### Template Method lifecycle
EngineBase owns the common lifecycle:

    Update()
      → Processing
      → OnUpdate()
      → Ready

Concrete engines implement OnUpdate(), not their own lifecycle.

### Validation before publication
A stage validates its output before publishing it into runtime state.

### Configuration vs runtime
Configuration contains immutable algorithm parameters. Runtime objects contain mutable per-cycle state.

### Composability
Adding a new model/reference source should not require modifying downstream engines.

## 4. Reference Source Architecture

The reference subsystem is being evolved from the original ReferenceModel/IPriceSeries approach into a reusable Reference Source architecture.

Target conceptual structure:

    IReferenceSource
          ↓
    ReferenceSourceBase
          ↓
    Concrete Reference Source
          ↓
    ReferenceRuntime
          ↓
    ReferenceEngine
          ↓
    DistanceEngine

Supporting components:

- ReferenceSourceConfiguration
- ReferenceSourceValidator
- Reference Source Library
- Reference Source Factory/Builder or equivalent registration mechanism

The Reference Source produces the equilibrium/reference value. ReferenceEngine integrates it into the pipeline. DistanceEngine must not know how the reference was generated.

## 5. First Concrete Reference Source

The first reference source is intended to be an ATR-smoothed reference.

The reference must not simply expose raw ClosePrices.

A previous cTrader skeleton supplied Bars.ClosePrices to ATRSmoothReferenceModel, causing:

    Reference = Close
    Distance = Close - Reference = 0

The replacement architecture must ensure the reference is genuinely computed from the intended reference algorithm.

## 6. Runtime State

EngineValues is the canonical published output container.

Runtime state should:
- use internal setters
- have clear ownership
- be overwritten deterministically each update
- avoid competing state representations

Future freshness tracking such as LastUpdatedIndex may be added if useful, but is not a prerequisite for current completion.

## 7. Validation

Reference, Distance, Scale, Normalization, and Statistics stages should have clearly separated validation responsibilities.

Reference validation should reject NaN, Infinity, and invalid/unpublished values.

Distance validation should reject NaN, Infinity, and invalid absolute distance.

Scale validation should enforce a positive finite scale.

Normalization validation should enforce valid finite normalized measurements and scale assumptions.

Statistics validation should distinguish legitimate warm-up from actual invalid input.

## 8. Extensibility Target

The architecture should eventually support sources such as SMA, EMA, WMA, HMA, RMA, KAMA, VIDYA, FRAMA, VWAP, Regression, Median, Kalman/state-space, robust statistical, and composite references.

These are future possibilities, not requirements for current completion. First deliver a correct ATR-smoothed reference path.

## 9. Performance Requirements

The engine should be suitable for historical research and capable of real-time optimization.

Avoid unnecessary per-bar allocations, duplicate rolling-window copies, unbounded memory growth, and hidden O(N²) operations.

Performance must be measured rather than assumed.

## 10. cTrader Integration

cTrader integration must be thin:

    cTrader Bars
        ↓
    CTraderMarketData / CTraderPriceSeries
        ↓
    ResearchFeatureEngine
        ↓
    EngineValues
        ↓
    cTrader indicator outputs

The indicator must not reimplement the mathematical pipeline.

## 11. Definition of Done

The current Distance/Normalized feature is complete only when:

1. Reference Source architecture is implemented.
2. ATRSmoothReferenceSource is implemented correctly.
3. ReferenceEngine consumes it correctly.
4. DistanceEngine receives a meaningful reference.
5. Distance output is mathematically correct.
6. Scale output is correct.
7. Normalized output is correct.
8. Statistics remain correct.
9. Warm-up behavior is explicit and correct.
10. Validation prevents invalid values from propagating.
11. Existing integration and mathematical tests pass.
12. Reference Source tests are added.
13. cTrader uses the production pipeline, not a duplicate implementation.
14. cTrader displays meaningful output where expected.
15. Core engine and cTrader results agree for the same input.
16. Determinism is demonstrated.
17. Long-run stability is demonstrated.
18. Performance is measured.
19. No unresolved critical correctness defects remain.

## 12. Prohibited Shortcuts

Do not rewrite Distance merely to make the cTrader line move.

Do not put cTrader dependencies into Core.

Do not silently substitute raw close for the ATR-smoothed reference.

Do not create duplicate runtime-state ownership.

Do not bypass the production pipeline in the indicator.

Do not hide invalid values by arbitrary clamping.

Do not declare completion because the indicator compiles or draws a non-zero line.

The objective is a correct research engine, not merely a visually working indicator.

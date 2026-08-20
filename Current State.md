# ResearchFeatureEngine — Work Completed & Current State

## 1. Existing Pipeline

The project contains the five-stage pipeline:

    Reference → Distance → Scale → Normalization → Statistics

The major stages are implemented and integrated.

## 2. Major Fixes Already Completed

### Engine lifecycle
EngineBase owns the Template Method lifecycle:

    Processing → OnUpdate() → Ready

ReferenceEngine, DistanceEngine, ScaleEngine, and NormalizationEngine were changed to implement OnUpdate() rather than bypass EngineBase.Update().

### CurrentIndex
A defect where CurrentIndex remained at zero was found and fixed so successive updates process successive data points.

### Runtime ownership
A previous critical problem involving disconnected/duplicated runtime objects was fixed. Runtime ownership was consolidated into EngineValues, with each engine writing to its own runtime sub-object.

### Statistics publisher
StatisticsPublisher was missing the Range case even though Range existed and RangeModel was implemented. The Range case was added.

This revealed a longer-term scaling risk: a hand-maintained switch can miss future statistics.

### Statistics observations
ObservationCount was added to StatisticsRuntimeValues and populated by StatisticsEngine after the rolling window update.

### Statistics allocation
StatisticsWindow gained GetOrderedArray(), and StatisticsEngine was changed to use it. This removed the previous AsSpan() → ToArray() double-allocation path.

Current performance should be measured before further optimization.

### Validation
ReferenceValidator was added to reject NaN/Infinity reference values.

DistanceValidator was added to validate finite directional distance, finite absolute distance, and non-negative absolute distance.

### Dead code
The following unused feature abstractions were removed:

- IFeatureModel.cs
- IFeature.cs
- FeatureBase.cs
- Direction.cs

FeatureModels was removed from EngineConfiguration.

EnginePipelineBuilder.Clear() was removed because it had no consumers.

## 3. Engineering Audit Findings

The detailed audit found:

- clean layer separation
- cTrader isolation
- correct dependency direction
- low engine coupling
- high cohesion
- deterministic mathematical behavior
- correct Welford variance
- correct ATR true-range calculation
- correct standard deviation
- correct median
- correct MAD
- correct mean/min/max/range
- bounded statistics window
- no unbounded memory growth
- no hidden O(N²) issue identified
- constructor dependency injection suitable for testing

## 4. Existing Test Coverage

The audit identified:

- PipelineExecutionTests
- MathematicalCorrectnessTests
- DeterminismTests
- LongRunStabilityTests
- PerformanceBenchmarkTests
- RealMarketDataValidationTests
- CrossPlatformConsistencyTests
- StatisticsEngineIntegrationTests

StatisticsEngineIntegrationTests cover statistics models, rolling-window behavior, validation failure, and sequential updates.

The audit reported 13 integration tests across 8 test files plus 4 StatisticsEngine unit tests.

Do not assume these counts are still current. Verify the repository.

## 5. Known Weaknesses

### StatisticsPublisher switch
A hand-maintained switch over StatisticType is fragile. A future improvement is registry/dictionary-based routing plus an automated completeness test.

Do not implement this blindly; inspect the current repository first.

### Statistics performance
The previous double-allocation was addressed. Measure current allocations before making additional changes.

### EngineConfiguration.PriceSeries
The audit identified PriceSeries as unused in production. Removing it is an API change; verify current usage first.

### ExecutionTrace
ExecutionTrace/ExecutionStage existed with no meaningful consumer in the audit snapshot. Verify current usage before changing it.

### Freshness tracking
LastUpdatedIndex/IsPublished was proposed as an optional improvement. It is not required for current completion.

## 6. Critical Current Problem: cTrader Zero Line

The cTrader indicator was compiled, but its output was a straight line at zero.

The cause was traced to the reference setup.

The indicator registered:

    var priceSeries = new CTraderPriceSeries(Bars.ClosePrices);
    IReferenceModel referenceModel =
        new ATRSmoothReferenceModel(priceSeries);

ATRSmoothReferenceModel is documented as a consumer of a precomputed ATR Smooth series. It does not calculate ATR Smooth itself.

Therefore:

    Reference.Price = Bars.ClosePrices[index]

and:

    Distance = Close - Reference
             = 0

This does not prove DistanceEngine is wrong. It exposed a missing reference-source implementation.

## 7. Reference Source Architecture Already Designed

The project discussion designed:

- IReferenceSource
- ReferenceRuntime
- ReferenceSourceBase
- ReferenceSourceValidator
- ReferenceSourceConfiguration
- Reference Source Library
- registration/discovery strategy
- library standards
- architecture audit/finalization
- implementation framework
- verification/certification

Target:

    IReferenceSource
          ↓
    ReferenceSourceBase
          ↓
    ATRSmoothReferenceSource
          ↓
    ReferenceEngine
          ↓
    DistanceEngine

The exact repository implementation must be checked before creating these classes, because pieces may already have been implemented since this document was prepared.

## 8. Intended ATRSmoothReferenceSource

The first concrete source must generate a genuinely smoothed equilibrium.

Do not simply pass Bars.ClosePrices into ATRSmoothReferenceModel.

Do not modify ATRSmoothReferenceModel arbitrarily just to create a non-zero result.

The source should:
- consume appropriate market data
- initialize state
- update for each index
- expose a valid reference value
- expose readiness/warm-up state
- support reset
- validate output
- remain platform independent

The exact ATR smoothing definition must be established from the existing project/specification and current code before implementation. Do not invent a different ATR algorithm merely because it produces movement.

## 9. cTrader Indicator Work

A ResearchFeatureEngineIndicator project exists in the user's cAlgo Sources area.

Earlier build problems included:
- config.json conflicting with code attributes
- stale obj/assembly artifacts
- missing ResearchFeatureEngineHost
- test files being compiled by the indicator project
- obsolete Features namespace reference

These were investigated with cleanup proposals:
- remove config.json when code attributes are used
- clean obj/Debug/Release artifacts
- create host only if still required by the current design
- exclude Tests from indicator compilation
- remove obsolete Features using

Inspect the current repository before applying any of these blindly.

## 10. cTrader Principle

The indicator must be a thin adapter.

It must not contain duplicate implementations of:
- Reference
- Distance
- Scale
- Normalization
- Statistics

It should instantiate the production ResearchFeatureEngine and expose EngineValues.

## 11. Intended Final Flow

    cTrader Bars
        ↓
    CTraderMarketData
        ↓
    ReferenceSource
        ↓
    ReferenceEngine
        ↓
    DistanceEngine
        ↓
    ScaleEngine
        ↓
    NormalizationEngine
        ↓
    StatisticsEngine
        ↓
    EngineValues
        ↓
    cTrader outputs

## 12. Preserve Stable Components

Do not rewrite stable components without evidence.

Preserve unless repository review proves otherwise:
- EngineBase Template Method lifecycle
- EngineValues ownership model
- CurrentIndex advancement
- Distance mathematical implementation
- Scale mathematical implementation
- Normalization mathematical implementation
- Statistics mathematical implementations
- adapter isolation
- constructor-based dependency injection
- valid existing tests

## 13. Remaining Work

1. Inspect the repository and reconcile these documents with actual code.
2. Complete/fix the Reference Source subsystem.
3. Implement the first correct ATR-smoothed reference source.
4. Integrate it with ReferenceEngine.
5. Verify Distance receives a meaningful reference.
6. Verify Scale.
7. Verify Normalization.
8. Verify Statistics.
9. Add missing tests.
10. Fix regressions.
11. Finish cTrader host/indicator integration.
12. Run the indicator on real cTrader data.
13. Compare cTrader output with a controlled reference dataset.
14. Run long-run, determinism, and performance validation.
15. Perform a final adversarial audit.
16. Report remaining issues instead of hiding them.

## 14. Completion Definition

The project is not complete merely because:
- it compiles
- superficial tests pass
- the cTrader indicator draws
- the Distance line is non-zero

It is complete when the cTrader indicator demonstrably executes the same production pipeline as the tested core engine and its outputs are mathematically verified.

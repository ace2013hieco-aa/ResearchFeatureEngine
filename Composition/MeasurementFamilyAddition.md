# Measurement-Family Addition Protocol (M10.3 F4)

How to add a new measurement family to the Distance /
ResearchFeatureEngine pipeline — the canonical, explicitly wired
procedure.

This multi-touch process is **intentional**. Adding a family touches
approximately five to six coordinated locations; the explicitness is a
safety feature, not a debt. There is deliberately **no** dynamic
registration, plugin system, or generic dictionary of values: every
family is a compile-time, type-gated construction with its own
`RuntimeValues` sub-object, so an accidental wiring mistake is a
compile error, not a silent runtime gap.

Do NOT "simplify" this protocol into reflection/registration
machinery, and do NOT weaken the type gating. If a change proposal
requires that, it is a redesign and needs its own milestone.

---

## Step 1 — Define semantics first

Before writing any code, write down:

- **Mathematical definition** — the exact formula, with the exact
  input series it consumes (and WHICH of the repo's two ATRs, if an
  ATR is involved: the ATRSmooth source's internal EMA of True Range,
  or the Scale stage's simple mean of True Range).
- **Units / dimensionality** — registered features are dimensionless
  or must declare their denominator explicitly; quantities in raw
  price units are not research features.
- **Sign convention** — which direction is positive.
- **Unavailable / warm-up semantics** — the canonical convention is
  NaN (or `null` / an `Unavailable` enum member) during warm-up or
  below-minimum-n, per the Darvas precedent. (The Statistics stage's
  retain-last-published behavior is a Statistics-specific convention
  and must NOT be copied into a new segment-reset-aware family —
  retaining across a segment boundary leaks the previous segment's
  value.)
- **Denominator-zero semantics** — where a division is involved, what
  is published when the denominator is zero (usually: unavailable, or
  the source-defined fallback; never an exception, never a silent 0).
- **No-lookahead requirements** — inputs must be computable from bars
  `0..t` only; within-segment windows are `[segment-start .. t]`,
  inclusive of the current bar, never the completed segment.
- **Minimum valid sample count** — if statistics are involved, the
  minimum n below which the value is unavailable (repo precedent:
  skewness n ≥ 3, kurtosis n ≥ 4; reliability guidance n ≥ 50
  exploratory / n ≥ 100 inference).

Check the published-values inventory and the exact-duplicate ledger
in the project's governance references before registering: a third
copy of an existing quantity (e.g. something equal to `RegimeAge` or
to `NormalizedMeasurement`) is a defect, not a feature.

## Step 2 — Implement the canonical source

Add the measurement to the appropriate **Core pipeline stage**
(typically a new `EngineBase` stage in `Engines/`, plus its model and
runtime-values classes in `Models/` or the stage's folder).

One authoritative implementation only. No duplicate implementation may
be created in:

- BulkExport
- applications / indicators
- tests
- tooling

Tests may contain independent oracles (hand-computed or
direct-definition recomputation) — that is legitimate verification
material, not a second source of truth. Production consumers consume
the canonical source. (Historical example: `tools/ComputeExpected`
mirrored the ATRSmooth math to produce golden values once; it was
removed in M10.3 F3 precisely because a second apparent source of
truth is a liability. The golden literals it produced remain pinned
in the tests.)

Placement is load-bearing: the M9 segment stage runs before
Reversal/Scale/Normalization/Statistics, so a stage consuming
current-bar Scale/Normalization/Statistics values must be appended
**after** Statistics. Ask which published values the new family reads,
and place it after every producer it consumes.

## Step 3 — Builder wiring

Register the stage in `Composition/ResearchFeatureEngineBuilder.cs`
through the existing type-gated composition mechanism — an
`is`-pattern on the composed reference-source type, exactly like the
Darvas stages (`ResearchFeatureEngineBuilder.cs:70-87`), the
HmaAtrSmooth dual stages (`:101-116`), and the M9 segment stage
(`:129-135`):

```csharp
if (_configuration.ReferenceSource is Reference.Sources.XxxSource xxx)
{
    builder.Add(new XxxEngine(context, model, xxx));
}
```

Rules:

- The type gate exists so every NON-matching composition keeps its
  exact previous behavior (Delta=0 for all other modes) — never
  register unconditionally.
- Preserve the dependency direction: the stage consumes
  `EngineContext`/`EngineValues` and the canonical source instance
  passed to it; it must not reach into the adapter or application
  layer.
- Do not duplicate engine logic in the gate; the gate only constructs.

## Step 4 — Runtime publication

- Add a dedicated `XxxRuntimeValues` class (single ownership), and
  expose it as a property on `EngineValues`
  (`Models/EngineValues.cs`), following the existing sub-objects
  (`Reversal`, `AtrSmoothRegimeSegment`, `MeanHmaAtrSmoothDistance`,
  …).
- `EngineValues` exposes the published canonical result; consumers
  read the published value — they must NOT reconstruct it from other
  published values or from source internals.
- Validators live in the stage's `Validation/` folder and fail
  closed.

## Step 5 — Export / schema wiring (BulkExport)

When the family must be exported, update in lockstep:

- `tools/BulkExport/Schema.cs` — `Columns(ExportMode)` adds the
  column names for exactly the modes whose composition registers the
  stage; `RenderRow(...)` adds the token rendering in the SAME
  position/order. Column lists and row tokens must stay in lockstep
  or column counts shift silently (mode gating is load-bearing —
  segment columns exist only in ATRSmooth-based modes).
- `tools/BulkExport/EngineConfigurationJson.cs` — extend the
  deterministic configuration description with any new composition
  parameter (fixed key order, invariant culture).
- Manifest / `ExportManifest` fields as required.
- **Schema version** (`Schema.Version`, currently
  `measurement-export/1.0.0`): additive columns bump the minor
  version; removals/renames/semantic changes bump the major version.
- Maintain deterministic ordering — the row tokens are produced in
  the fixed column order; every change to `Columns()` must be mirrored
  in `RenderRow()`.

BulkExport is an extraction boundary: it reads `EngineValues` only
and must contain zero engine mathematics. If an export change
requires recomputing something, the computation belongs in the
engine stage, not in the exporter.

## Step 6 — Tests

A new family requires tests for:

- mathematical correctness (hand-computed golden oracle; constant
  series produce exactly the constant for stateful incremental math —
  any drift means broken weights)
- warm-up / unavailable semantics
- boundary conditions (exact-representation fixtures at gate
  boundaries)
- denominator-zero behavior
- determinism (identical inputs → bit-identical outputs)
- reset / re-tick behavior (snapshot/restore idempotence; fresh-pass
  equivalence)
- no-lookahead
- mode gating (non-matching compositions leave the values at their
  unavailable defaults and produce byte-identical prior artifacts)
- export serialization where applicable (column counts per mode,
  token round-trip)
- configuration / manifest consistency (schema version bump,
  `EngineConfigurationJson` matches the composed preset)

Suite count only grows — the baseline is monotonically increasing
across milestones; a shrinking count is a defect.

## Step 7 — Certification

Run, and record the results of:

- the full Release test suite (exact pass count)
- the relevant targeted tests for the new family
- a deterministic rerun where applicable (byte-identical artifacts
  on a second run)
- artifact integrity checks if production export is involved
  (SHA-256 manifest verification, refuse-overwrite, Year-1
  partition + holdout firewall unchanged)
- a rebuild of the applications (indicator) and, when the adapter
  surface changed, the adapter assembly

## Summary table

| # | Location | File |
| --- | --- | --- |
| 1 | Semantics definition | design notes / stage `.md` doc |
| 2 | Canonical stage implementation | `Engines/`, `Models/` |
| 3 | Builder wiring (type-gated) | `Composition/ResearchFeatureEngineBuilder.cs` |
| 4 | Runtime values publication | `Models/EngineValues.cs` (+ stage runtime class) |
| 5 | Export schema / manifest | `tools/BulkExport/Schema.cs`, `EngineConfigurationJson.cs`, `ExportManifest.cs` |
| 6 | Tests | `Tests/…` |
| 7 | Certification run | Release suite + targeted + determinism |

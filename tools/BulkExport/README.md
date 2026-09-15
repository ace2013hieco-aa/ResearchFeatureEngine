# BulkExport — Canonical Measurement Export (M10)

Schema `measurement-export/1.1.0` · Exporter `1.0.0` · Milestone M11.2A

## What this tool is

BulkExport turns the frozen Distance engine's published runtime
measurements into a deterministic, hash-pinned CSV artifact plus a
SHA-256 manifest. It is an **extraction boundary only**: it contains
zero engine mathematics. Every exported measurement token is read
directly from `EngineValues` after the engine's own `Update()`, on a
pipeline composed by the production `ResearchFeatureEngineBuilder`
with the ratified default configuration (ATRSmooth 16/5.1/100, scale
14, statistics window 252, source Close, reversal
TrailingStopPosition).

## Usage

```
dotnet exec BulkExport.dll \
  --registry <registry.json> \
  --dataset  <dataset_id> \
  --mode     atrsmooth2|darvasbox|hma|hmaatrsmooth \
  --capture  <frozen capture.csv> \
  --out      <artifact.csv> \
  [--engine-commit <40-hex sha>]
```

Exit codes: `0` success · `1` usage error · `2` fail-closed export
error · `3` unexpected failure.

## Supported modes and column sets

| Mode | Columns | Extra families |
|| --- | --- | --- |
|| `atrsmooth2` | 32 | M9 segment (RegimeId/StartIndex/Age/Transition) |
|| `hmaatrsmooth` | 36 | segment + mean HMA–ATRSmooth distance + alignment + M11.1 separation/relative-close-position |
|| `darvasbox` | 31 | Darvas box distances (no segment family — the M9 stage is not registered for Darvas compositions) |
|| `hma` | 27 | base families only |

Base families (all modes): identity/source bar (verbatim tokens),
reference (price + regime), distance/scale/normalized, statistics
(11 columns, retain-last-published), reversal (3 columns — canonical
regime-change semantics; the 0→±1 establishment counts as a reversal
bar but is NOT a segment transition, per M9).

No TSI variables, no survival/hazard/exhaustion columns. Downstream
research constructs its own variables from these canonical
measurements.

## Frozen-input policy (hard gate)

```
LIVE DATA    → NOT ALLOWED (there is no live path at all)
FROZEN CAPTURE → ALLOWED only if registered + hash-pinned
```

A capture is accepted only when its `dataset_id` is in the registry
and its SHA-256 matches the registry pin, checked before processing
and re-checked after. Unregistered files, hash mismatches, timestamp
regressions, unknown headers, malformed rows, or forbidden characters
(comma/quote/CR/LF in a field) fail closed — no partial artifact
survives. An existing target artifact is never overwritten.

### Source schemas (M10.1.x)

| Schema | Exact header | Fields |
| --- | --- | --- |
| `recorder_v1_1` | `OpenTimeUtc,Open,High,Low,Close,TickVolume,Spread` | 7 |
| `recorder_v1` | `OpenTimeUtc,Open,High,Low,Close,TickVolume` | 6 |
| `fixture_v1` | `DateTime,Open,High,Low,Close,Volume` | 6 |

Every owner-ratified production capture is `recorder_v1_1` (recorder
spread-recording V1.1, commit `2795dd0`, 2026-09-05). The three
schemas remain distinct: exact header match, exact field count, no
optionality inside a schema. In `recorder_v1_1`, `Spread` is validated
source-field provenance ONLY — the recorder contract (finite,
non-negative; zero = unavailable-backfill, never fabricated) is
enforced and a malformed Spread fails closed — but Spread is never an
engine input (the engine consumes OHLCV only) and never appears in the
measurement artifact (`measurement-export/1.0.0` columns unchanged).
Identical OHLCV with different valid spreads produces byte-identical
measurement artifacts (golden-tested, V11_9).

Registry entries may declare `source_schema` as an exact pin
(`recorder_v1` / `recorder_v1_1` / `fixture_v1`); a declared value
that mismatches the actual capture header is a hard failure. Absent
declaration keeps M10.1 behavior (header detection alone).

## Research firewall — first calendar year only

**Every registered dataset is partitioned chronologically:**

- RESEARCH window = `[first_timestamp, first_timestamp + 1 calendar
  year)` — the only rows the research export ever writes.
- HOLDOUT = everything from `first_timestamp + 1 calendar year`
  onward — reserved for out-of-sample backtesting. It remains in the
  source capture but is never exported by this tool.

The boundary uses true calendar arithmetic (`AddYears`, leap-year
correct) computed independently per dataset from its own first bar —
never `365*24h`, never a global cutoff. The partition is recorded in
every manifest (`partition_policy`, `research_start`, `research_end`,
`holdout_start`).

**Research protocol (binding):** only the first chronological
calendar year of each dataset is available for feature discovery and
tuning. All subsequent observations are reserved for out-of-sample
validation/backtesting. The holdout must never enter the research
feedback loop — no distribution analysis, no parameter tuning, no
hypothesis selection on it. There is no parameter that extends the
research window; the registry's identity fields are verified against
the capture bytes, so a shifted boundary fails the identity gate.

## Deterministic serialization

- Invariant culture everywhere; doubles in round-trip `"R"` format
  (`NaN`/`Infinity`/`-Infinity` tokens for non-finite).
- Source-bar fields (`timestamp`, `open`, `high`, `low`, `close`,
  `tick_volume`) are copied as **verbatim source tokens**.
- UTF-8 without BOM, LF line endings, one final LF, no quoting.
- Same capture + engine commit + exporter version + schema version +
  configuration → byte-identical artifact and manifest (verified by
  test; no wall-clock or path data inside the artifact).

## Manifest

`<artifact>.manifest.json` — UTF-8/no BOM, LF, 2-space indent, fixed
key order, trailing LF. Records schema/exporter versions, engine
commit, full engine configuration, dataset id/version, source file +
SHA-256, source schema, row count, first/last timestamps, duplicate
timestamp count, the full partition record, artifact file + SHA-256,
and the complete column list. An independent researcher can answer
"exactly which data, engine, schema, and exporter produced this
artifact?" from the manifest alone.

## Registry

`registry.json` (shipped empty; production datasets are added by\nowner action at M10.2). Fields: `dataset_id`, `dataset_version`,\n`filename`, `source_sha256` (64 lowercase hex), `source_schema`\n(optional exact pin: `recorder_v1` / `recorder_v1_1` / `fixture_v1`),\n`first_timestamp`,\n`last_timestamp`, `partition_policy`\n(only `first_calendar_year_only` is accepted).\n\nCurrently registered production datasets (M11.2A):\n\n| dataset_id | source | Year-1 rows |\n| --- | --- | --- |\n| `XAUUSD_Tick25` | 42f17379… | 2,234,316 |\n| `XAUUSD_Tick50` | 8712b720… | 130,665 (control) |\n| `XAUUSD_Tick100` | 5f9e5fbb… | 68,255 |\n| `EURUSD_Tick100` | d7afb803… | (M10.1 certified) |

## Limitations (documented honestly)

- The engine's `IMarketData` contract is index-based (stages read
  e.g. `Close[index - SmoothLength]`), so the research window's bars
  are materialized into memory arrays once per run. The holdout is
  streamed past during validation only, never materialized. The
  pipeline is two-pass streaming for reads/writes/hashing, but NOT
  fully streaming end-to-end — by engine contract, not by exporter
  choice.
- Statistics below their minimum-n publish the retained
  (last-published or default) runtime value, exactly as the engine
  publishes it; consumers gate validity on
  `statistics_observation_count >= min_n` (min_n: 1 for
  mean/median/min/max/range/MAD, 2 for variance/stddev, 3 skewness,
  4 kurtosis).
- Duplicate source timestamps are preserved and counted in the
  manifest (never repaired — known recorder artifacts).

## M10.2 / M11.2A status

**M11.2A (current milestone):** The Distance certification chain now admits
**XAUUSD Tick25**, **XAUUSD Tick50**, and **XAUUSD Tick100** — all three
registered with SHA-256 pins and verified Year-1 row-count invariants.
Tick50 (130,665) serves as the unchanged control; Tick25 yields 2,234,316
research rows and Tick100 yields 68,255. EURUSD Tick100 was certified at
M10.1. The first calendar year of each dataset is the research window;
all subsequent bars are the sealed holdout.

Production runs will export the first-year research windows of the
ratified frozen captures. Targets (empirical acceptance, not assumptions):
6.6M-bar capture ≤ 30 min, < 2 GB. Measured M10.1 benchmark: 527,040
research rows exported from a 1,000,000-bar capture in 24.3 s at ~65 MB
peak RSS (Release, single process).

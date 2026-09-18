# Changelog

All notable changes to ResearchFeatureEngine are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- MT5 adapter — C# wrapper DLL (`Adapters/ResearchFeatureEngine.MT5/`) + MQL5 indicator skeleton (`RFEMT5Indicator.mq5`). Requires MetaTrader 5 terminal + MetaTrader5.dll for compilation.

## [0.2.0] - 2026-09-18

### Added
- **Python adapter**: `pip install research-feature-engine` — pythonnet wrapper around the .NET 6 core, exposing `ATRSmooth2`, `DarvasBox`, `Hma`, and `HmaAtrSmooth` engines to Python with prebuilt DLLs shipped as package data
- **PyPI publication workflow** (`.github/workflows/publish-pypi.yml`): on `v*` tag push, builds .NET DLLs, creates wheel + sdist, and publishes to PyPI via OIDC trusted publishing
- **Python CI workflow** (`.github/workflows/ci-python.yml`): builds DLLs, runs 26 pytest tests, and executes all 3 example scripts on every push/PR
- **26 Python tests** (`tests_python/test_engine.py`): MarketData loading, engine construction, pipeline run, parameter variation, output content validation
- **Three example scripts** in `examples/`: `basic_indicator.py`, `multi_engine_pipeline.py`, `pandas_integration.py`
- **Development dependencies** (`requirements-dev.txt`): pythonnet, pandas, numpy, pytest
- **Python `.gitignore` entries**: `__pycache__/`, `.pytest_cache/`, `wheelhouse/`, etc.

### Added
- **MT5 adapter scaffold**: `Adapters/ResearchFeatureEngine.MT5/` with `MT5MarketData.cs` (implements `IMarketData` wrapping `MqlRates[]`), `ResearchFeatureEngine.MT5.csproj` (stub compilation without MetaTrader5.dll), `RFEMT5Indicator.mq5` (MQL5 application), `RFEMT5Bridge.cpp` (native C ABI bridge documentation), and `MT5MarketDataTests.cs` (9 xUnit validation tests for adapter behavior)
- **CI fix**: `build_dlls.py` now runs `dotnet restore` before `--no-restore` build — fixes `NETSDK1127` failure on fresh GitHub Actions runners

### Changed
- README updated: test counts to 526/526 (500 .NET + 26 Python), added MT5 adapter entry, architecture boundary text generalized, removed MT4 out-of-scope row

## [0.1.0] - 2026-09-17

### Added
- **NuGet package**: Core now ships as `ResearchFeatureEngine` on [nuget.org](https://www.nuget.org/packages/ResearchFeatureEngine) — `dotnet add package ResearchFeatureEngine`
- **Symbol package** (`.snupkg`) for stack-trace and debug support
- **GitHub Actions CI** (`.github/workflows/ci.yml`): build + test on every push and PR to master. Runs the 491 portable tests; the 5 source-identity tests (`V11_10`–`V11_12`) are pinned to private local captures and run only on the author's machine.
- **Tag-triggered publish workflow** (`.github/workflows/publish-nuget.yml`): on `v*` tag push, packs Release and publishes to NuGet.org via OIDC trusted publishing (no API key stored anywhere)
- **MIT license** (`LICENSE`) — Copyright (c) 2026 Osat Zoghi
- **Architecture diagram** (`docs/architecture.svg`): 3-layer view (applications → adapters → core) with the no-platform-deps boundary called out explicitly
- **README polish**: Architecture section, Adapters table (shipped / planned / out of scope), corrected license link

### Notes
- Package metadata: id `ResearchFeatureEngine`, version `0.1.0`, license MIT, repository `https://github.com/ace2013hieco-aa/ResearchFeatureEngine`
- Python and MT5 adapters are deliberately listed as planned (not shipped) — `MT5` is the active adapter in progress

[Unreleased]: https://github.com/ace2013hieco-aa/ResearchFeatureEngine/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ace2013hieco-aa/ResearchFeatureEngine/releases/tag/v0.2.0
[0.1.0]: https://github.com/ace2013hieco-aa/ResearchFeatureEngine/releases/tag/v0.1.0
# ROADMAP.md

> Status as of 2026-09-19 · HEAD: 379c42e · origin/master: 379c42e

## Current Phase: MT5 Adapter Scaffold + CI Fix (COMPLETE)

### Completed (this session)
- [x] **CI Truthfulness**: Diagnosed CI failure (`NETSDK1127` — missing `dotnet restore` in `build_dlls.py`), fixed root cause, verified build works from clean state
- [x] **MT5/MQL5 Implementation - C# adapter**: Created `Adapters/ResearchFeatureEngine.MT5/ResearchFeatureEngine.MT5.csproj` with stub compilation for `MqlRates` (when `MetaTrader5.dll` is absent)
- [x] **MT5/MQL5 Implementation - C# adapter code**: Created `MT5MarketData.cs` implementing `IMarketData` (mirrors `CTraderMarketData` pattern)
- [x] **MT5/MQL5 Implementation - MQL5 indicator**: Rewrote `RFEMT5Indicator.mq5` — fixed malformed MQL5 syntax (`SetIndex_buffer`, `BFFER` typos, `#import` for .NET DLL, buffer declaration issues)
- [x] **MT5/MQL5 Implementation - Native bridge**: Created `RFEMT5Bridge.cpp` documenting the C ABI bridge pattern (MQL5 cannot import .NET directly)
- [x] **MT5/MQL5 Implementation - Tests**: Created 9 xUnit tests validating adapter construction, property access, MoveNext, deterministic output, IMarketData conformance
- [x] **Release/Version/Provenance**: Verified all versions consistent (Core v0.1.0, Python v0.2.0, no version conflicts)
- [x] **Architecture conformance**: Added MT5 test project reference to verify Core→Adapter dependency direction
- [x] **Documentation**: Updated README test counts (526/526), removed MT4 references, updated architecture text
- [x] **Forensic report**: Dated report committed

### Verification Results
- .NET build: ✅ PASS (all 4 projects compile)
- .NET tests: ✅ 500/500 pass (491 portable + 9 MT5 adapter)
- Python tests: ✅ 26/26 pass
- Python examples: ✅ All 3 run end-to-end
- build_dlls.py clean build: ✅ PASS (from cleared NuGet cache)

## Next Phase: MT5 Runtime Validation (BLOCKED — external dependency)

### Blocked (requires external dependency)
- [ ] **Runtime MT5 validation**: Requires MetaTrader 5 terminal installation + `MetaTrader5.dll`
- [ ] **Cross-platform parity test**: cTrader ↔ MT5 numerical agreement (requires MT5 terminal)
- [ ] **MQL5 compile verification**: Requires MetaEditor (ships with MT5 terminal)

### MT5 Validation Limitations (documented)
- MT5 adapter compiles against stub `MqlRates` (not the real MetaTrader5.dll)
- 9 static tests validate adapter logic with test data
- Runtime validation pending: install MetaTrader 5 terminal, set `-p:MT5DllPath="C:\Program Files\MetaTrader 5\MetaTrader5.dll"`
- MQL5 syntax validated by inspection (no MetaEditor available)

## Future Phases (deferred)
See `docs/ResearchFeatureEngine-roadmap.md` for the generalized roadmap.

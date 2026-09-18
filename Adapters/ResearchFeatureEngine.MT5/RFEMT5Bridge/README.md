# RFEMT5Bridge — Native C ABI Bridge (stub)

**Status:** Stub for compile-validation. Replaced at runtime with the full bridge documented in `RFEMT5Bridge.cpp`.

## Purpose

MetaTrader 5's MQL5 language can only import native Win32 DLLs via `#import`. It
cannot reference .NET assemblies directly. This project produces a native
`RFEMT5Bridge.dll` that exports the C ABI functions expected by
`RFEMT5Indicator.mq5`, allowing the MQL5 indicator to compile in MetaEditor.

## Architecture

```
MQL5 (RFEMT5Indicator.mq5)
  → #import "RFEMT5Bridge.dll"  (native C ABI, __stdcall)
  → RfeInitialize / RfeProcessBar / RfeGet* / RfeShutdown
  → [full bridge would host .NET Core via nethost and delegate to Core]
  → EngineValues pipeline (ResearchFeatureEngine.dll)
```

The full bridge (documented in `RFEMT5Bridge.cpp`) uses `nethost` + `hostfxr`
to load the .NET 6 runtime and invoke the EngineValues pipeline. This is a
**stub** implementation that returns NaN/0 sentinels so the MQL5 file compiles.

## Build

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:NativeLib=True
```

Output: `bin/Release/net8.0/win-x64/publish/RFEMT5Bridge.dll`

## Deployment

Copy the compiled DLL to the MT5 MQL5 Libraries directory:

```
%APPDATA%\MetaQuotes\Terminal\<terminal-id>\MQL5\Libraries\RFEMT5Bridge.dll
```

## Compile status of RFEMT5Indicator.mq5

The MQL5 file has **not yet been compiled to `.ex5`** in this environment.
The broker-branded MetaEditor (Pepperstone, v5.0.0.6182) does not support
headless compilation (`/compile:` exits with code 0 but produces no output),
and the GUI build (F7 / Ctrl+F7) does not respond to external input mechanisms
(SendInput, PostMessage, SendMessage, AutoHotkey, `computer_use`).

**Validation status:**
- ✅ C# bridge compiles and produces valid native DLL
- ✅ C# adapter `MT5MarketData.cs` — 9/9 xUnit tests pass
- ⏳ MQL5 `.ex5` compilation — not achieved (MetaEditor limitation)
- ⏳ MT5 runtime validation — pending .ex5 compilation
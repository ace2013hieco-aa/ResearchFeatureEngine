//+------------------------------------------------------------------+
//| RFEMT5Bridge.cpp — Native C ABI bridge for MQL5 → .NET Core engine |
//+------------------------------------------------------------------+
// This file is NOT compiled directly by MSBuild. It is a template for
// the native bridge DLL that MQL5 #imports. The actual build is done by
// a separate C++/CLI or C# UnmanagedCallersOnly project that exports
// C ABI functions callable from MQL5 via #import.
//
// MQL5 CANNOT import .NET assemblies directly via #import. The bridge
// must be a native Win32 DLL that internally hosts the .NET runtime
// (via nethost + coreclr hosting API) or uses C++/CLI on Windows.
//
// Build (when MT5 terminal is installed):
//   cl /LD /EHsc RFEMT5Bridge.cpp /link /OUT:RFEMT5Bridge.dll
//
// Or via C# with UnmanagedCallersOnly (net8.0+):
//   dotnet publish -r win-x64 -p:NativeLib=True
//
// Exported functions (C ABI, __stdcall calling convention):
//
// int RfeInitialize(
//     const char* symbol,         // _Symbol as ANSI string
//     int timeframe,              // (int)_Period (ENUM_TIMEFRAMES)
//     int ref_type,               // ENUM_REFERENCE_TYPE
//     int atr_period,
//     double atr_multiplier,
//     int smooth_length,
//     int box_length,
//     int hma_period,
//     int scale_atr_period,
//     int stats_window,
//     int reversal_mode
// );
//
//   Returns: 0 on success, non-zero error code on failure.
//
// bool RfeProcessBar(
//     double open, double high, double low,
//     double close, long volume, long time
// );
//
//   Returns: true if the engine produced valid output for this bar,
//   false if still in warm-up.
//
// double RfeGetReference();  // EngineValues.Reference.Price
// double RfeGetDistance();   // DirectionalDistance (close − reference)
// double RfeGetScale();      // Scale (ATR)
// double RfeGetNormalized();  // Normalization (distance / scale)
//
// void RfeShutdown();
//
// Architecture:
//   MQL5 → #import RFEMT5Bridge.dll (native C ABI)
//        → RfeInitialize creates a .NET Core host (via nethost)
//        → Loads ResearchFeatureEngine.dll + ResearchFeatureEngine.Python.dll
//        → Creates PythonEngineFactory.RunAll or equivalent
//        → Each RfeProcessBar feeds one bar through engine.Update()
//        → RfeGet* retrieve values from EngineValues
//
// No engine math is duplicated — all values originate from the canonical
// EngineValues pipeline in the Core assembly.
//
// Dependencies:
//   - nethost.dll (ships with .NET 6+ runtime)
//   - hostfxr.dll (ships with .NET 6+ runtime)
//   - ResearchFeatureEngine.dll (Core)
//   - ResearchFeatureEngine.Python.dll (adapter for type resolution)
//       (NOT ResearchFeatureEngine.MT5.dll — the bridge calls Core directly)
//
// The MT5 adapter project (Adapters/ResearchFeatureEngine.MT5/) compiles
// MT5MarketData.cs as a .NET assembly that is used by the bridge via
// reflection or direct reference. The bridge itself is a separate native
// project not included in this repository (requires C++/CLI toolchain).

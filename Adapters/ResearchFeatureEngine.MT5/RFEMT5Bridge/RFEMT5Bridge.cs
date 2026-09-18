//+------------------------------------------------------------------+
//| RFEMT5Bridge.cs — Native C ABI bridge for MQL5 -> .NET Core       |
//+------------------------------------------------------------------+
// This is a STUB implementation for COMPILE-VALIDATION purposes.
// It exports the same C ABI functions that RFEMT5Bridge.cpp documents,
// but returns NaN/0 sentinel values. The real bridge (documented in
// RFEMT5Bridge.cpp) would host the .NET Core engine via nethost and
// delegate to the canonical EngineValues pipeline.
//
// Build: dotnet publish -c Release -r win-x64 --self-contained false -p:NativeLib=true
//   Output: bin/Release/net8.0/win-x64/publish/RFEMT5Bridge.dll
//
// This stub allows the MQL5 indicator (RFEMT5Indicator.mq5) to compile
// in MetaEditor. At runtime, replace this stub with the full bridge
// documented in RFEMT5Bridge.cpp.
//
// Default calling convention is StdCall on Windows (MQL5 #import expects
// __stdcall, which is the default for UnmanagedCallersOnly in .NET 8
// Native AOT on Windows).

using System;
using System.Runtime.InteropServices;

namespace RFEMT5Bridge
{
    internal static class NativeExports
    {
        // EngineValues for the current bar
        private static double s_reference = double.NaN;
        private static double s_distance = double.NaN;
        private static double s_scale = double.NaN;
        private static double s_normalized = double.NaN;
        private static int s_initialised = 0;

        /// <summary>
        /// Initializes the ResearchFeatureEngine MT5 bridge.
        /// </summary>
        /// <returns>0 on success, non-zero error code on failure.</returns>
        [UnmanagedCallersOnly]
        public static int RfeInitialize(
            IntPtr symbol,           // ANSI string (_Symbol)
            int timeframe,           // (int)_Period
            int ref_type,            // ENUM_REFERENCE_TYPE
            int atr_period,
            double atr_multiplier,
            int smooth_length,
            int box_length,
            int hma_period,
            int scale_atr_period,
            int stats_window,
            int reversal_mode)
        {
            // Stub: always succeed
            s_initialised = 1;
            s_reference = double.NaN;
            s_distance = double.NaN;
            s_scale = double.NaN;
            s_normalized = double.NaN;
            return 0;
        }

        /// <summary>
        /// Processes one bar through the engine pipeline.
        /// Returns true if valid output was produced, false during warm-up.
        /// </summary>
        [UnmanagedCallersOnly]
        [return: MarshalAs(UnmanagedType.I1)]
        public static bool RfeProcessBar(
            double open,
            double high,
            double low,
            double close,
            long volume,
            long time)
        {
            // Stub: always return false (warm-up) so indicator doesn't plot garbage
            return false;
        }

        [UnmanagedCallersOnly]
        public static double RfeGetReference() => s_reference;

        [UnmanagedCallersOnly]
        public static double RfeGetDistance() => s_distance;

        [UnmanagedCallersOnly]
        public static double RfeGetScale() => s_scale;

        [UnmanagedCallersOnly]
        public static double RfeGetNormalized() => s_normalized;

        [UnmanagedCallersOnly]
        public static void RfeShutdown()
        {
            s_initialised = 0;
        }
    }
}

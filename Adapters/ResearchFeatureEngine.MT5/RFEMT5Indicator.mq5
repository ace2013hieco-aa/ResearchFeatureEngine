//+------------------------------------------------------------------+
//|                                                RFEMT5Indicator.mq5 |
//|                        MT5 adapter for ResearchFeatureEngine      |
//|  This indicator is a THIN ADAPTER — it contains no engine math.  |
//|  It wires MetaTrader 5's bar stream to the .NET Core pipeline     |
//|  via MT5MarketData and publishes EngineValues to output buffers.  |
//|  Requires: RFEMT5Bridge.dll (managed C++/CLI or C# DLL) loaded    |
//|            via MetaTrader5.NET bridge or #import of a native ABI.  |
//+------------------------------------------------------------------+
#property indicator_chart_window
#property indicator_buffers 4
#property indicator_plots   4
#property indicator_type1   DRAW_LINE
#property indicator_color1  clrDodgerBlue
#property indicator_label1  "Reference"
#property indicator_type2   DRAW_LINE
#property indicator_color2  clrOrange
#property indicator_label2  "Distance"
#property indicator_type3   DRAW_LINE
#property indicator_color3  clrGray
#property indicator_label3  "Scale"
#property indicator_type4   DRAW_LINE
#property indicator_color4  clrLime
#property indicator_label4  "Normalized"
#property indicator_width1  1
#property indicator_width2  1
#property indicator_width3  1
#property indicator_width4  1

//--- Indicator short name
#property indicator_shortname "RFE MT5 v1.0"

//--- Input parameters (match cTrader indicator parameters)
input ENUM_REFERENCE_TYPE  InpReferenceType=REF_ATRSMOOTH2;   // Reference model
input int                  InpAtrPeriod=16;                    // ATR Period
input double               InpAtrMultiplier=5.1;               // ATR Multiplier
input int                  InpSmoothLength=100;                // VWMA Smooth Length
input int                  InpBoxLength=5;                     // Darvas Box Length
input int                  InpHmaPeriod=16;                    // HMA Period
input int                  InpScaleAtrPeriod=14;              // Scale ATR Period
input int                  InpStatsWindow=20;                 // Statistics Window
input ENUM_REVERSAL_MODE   InpReversalMode=REVERSAL_TRAILING_STOP;  // Reversal mode

//--- Indicator buffers
double ExtReferenceBuffer[];
double ExtDistanceBuffer[];
double ExtScaleBuffer[];
double ExtNormalizedBuffer[];

//--- DLL imports (native ABI bridge — not a direct .NET import)
//    MetaTrader 5 cannot import .NET assemblies directly via #import.
//    The bridge is a native C ABI DLL (RFEMT5Bridge.dll) that hosts
//    the .NET Core engine internally and exposes C-exported functions.
#import "RFEMT5Bridge.dll"
   int    RfeInitialize(string symbol, int timeframe, int ref_type,
                        int atr_period, double atr_multiplier,
                        int smooth_length, int box_length,
                        int hma_period, int scale_atr_period,
                        int stats_window, int reversal_mode);
   bool   RfeProcessBar(double open, double high, double low,
                        double close, long volume, long time);
   double RfeGetReference();
   double RfeGetDistance();
   double RfeGetScale();
   double RfeGetNormalized();
   void   RfeShutdown();
#import

//--- Reference type enum (matches Core ReferenceType)
enum ENUM_REFERENCE_TYPE
  {
   REF_ATRSMOOTH2=0,
   REF_DARVAS_BOX,
   REF_HMA,
   REF_HMA_ATRSMOOTH
  };

//--- Reversal mode enum (matches Core ReversalMode)
enum ENUM_REVERSAL_MODE
  {
   REVERSAL_TRAILING_STOP=0,
   REVERSAL_CLOSE_TO_REFERENCE
  };

//+------------------------------------------------------------------+
//| Custom indicator initialization function                         |
//+------------------------------------------------------------------+
int OnInit()
  {
   //--- Set up indicator buffers
   SetIndexBuffer(0,ExtReferenceBuffer);
   SetIndexBuffer(1,ExtDistanceBuffer);
   SetIndexBuffer(2,ExtScaleBuffer);
   SetIndexBuffer(3,ExtNormalizedBuffer);

   //--- Set buffer labels
   IndicatorSetString(0,INDICATOR_LABEL,"Reference");
   IndicatorSetString(1,INDICATOR_LABEL,"Distance");
   IndicatorSetString(2,INDICATOR_LABEL,"Scale");
   IndicatorSetString(3,INDICATOR_LABEL,"Normalized");

   //--- Initialize the .NET engine bridge
   int result = RfeInitialize(_Symbol, (int)_Period, (int)InpReferenceType,
                               (int)InpAtrPeriod, InpAtrMultiplier,
                               (int)InpSmoothLength, (int)InpBoxLength,
                               (int)InpHmaPeriod, (int)InpScaleAtrPeriod,
                               (int)InpStatsWindow, (int)InpReversalMode);
   if(result != 0)
     {
      Print("Failed to initialize ResearchFeatureEngine MT5 bridge (error: ",result,").");
      return(INIT_FAILED);
     }

   return(INIT_SUCCEEDED);
  }

//+------------------------------------------------------------------+
//| Custom indicator deinitialization function                       |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
  {
   RfeShutdown();
  }

//+------------------------------------------------------------------+
//| Custom indicator iteration function                              |
//+------------------------------------------------------------------+
int OnCalculate(const int rates_total,
                const int prev_calculated,
                const int begin,
                const MqlRates& rates[])
  {
   int start=MathMax(1,prev_calculated);
   if(prev_calculated==0)
      start=1;  // skip first bar (no history for indicators)

   //--- Process bars (MQL5 passes rates oldest-first by default)
   for(int i=start; i<rates_total; i++)
     {
      // Feed each bar to the Core pipeline
      if(!RfeProcessBar(rates[i].open,
                        rates[i].high,
                        rates[i].low,
                        rates[i].close,
                        rates[i].tick_volume,
                        rates[i].time))
        {
         // Engine not ready yet (warm-up) — skip plotting
         continue;
        }

      //--- Publish canonical EngineValues to indicator buffers
      ExtReferenceBuffer[i]  = RfeGetReference();
      ExtDistanceBuffer[i]   = RfeGetDistance();
      ExtScaleBuffer[i]      = RfeGetScale();
      ExtNormalizedBuffer[i] = RfeGetNormalized();
     }

   return(rates_total);
  }

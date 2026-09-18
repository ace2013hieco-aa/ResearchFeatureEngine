//+------------------------------------------------------------------+
//|                                                RFEMT5Indicator.mq5 |
//|                        MT5 adapter for ResearchFeatureEngine      |
//|  This indicator is a THIN ADAPTER — it contains no engine math.  |
//|  It wires MetaTrader 5's bar stream to the .NET Core pipeline     |
//|  via MT5MarketData and publishes EngineValues to output buffers.  |
//+------------------------------------------------------------------+
#property indicator_chart_window
#property indicator_buffers 4
#property indicator_plots   4

//--- Indicator buffers indices
#define BUFFER_REFERENCE  0
#define BUFFER_DISTANCE   1
#define BUFFER_SCALE      2
#define BUFFER_NORMALIZED 3

//--- Indicator short name
#property indicator_shortname "RFE MT5 v1.0"

//--- Input parameters
input ENUM_REFERENCE_TYPE  InpReferenceType=REF_ATRSMOOTH2;  // Reference model
input int                  InpAtrPeriod=16;                 // ATR Period
input double               InpAtrMultiplier=5.1;              // ATR Multiplier
input int                  InpSmoothLength=100;              // VWMA Smooth Length
input int                  InpBoxLength=5;                   // Darvas Box Length
input int                  InpHmaPeriod=16;                  // HMA Period
input int                  InpScaleAtrPeriod=14;            // Scale ATR Period
input int                  InpStatsWindow=20;               // Statistics Window
input ENUM_REVERSAL_MODE   InpReversalMode=REVERSAL_TRAILING_STOP; // Reversal mode

//--- Indicator buffers
double ExtReferenceBuffer[];
double ExtDistanceBuffer[];
double ExtScaleBuffer[];
double ExtNormalizedBuffer[];

//--- DLL imports
#import "MetaTrader5.dll"
   // The .NET assembly is loaded via the MetaTrader5.NET bridge.
   // Actual engine construction happens in managed code via RFEMT5Bridge.dll.
   bool    MT5Engine_Initialize(string symbol,int timeframe);
   double  MT5Engine_Update(double open,double high,double low,double close,long volume,long time);
   double  MT5Engine_GetValue(int engine_value_index);
   void    MT5Engine_Shutdown();
#import

//--- Reference type enum (matches Core ReferenceType)
enum ENUM_REFERENCE_TYPE
  {
   REF_ATRSMOOTH2,
   REF_DARVAS_BOX,
   REF_HMA,
   REF_HMA_ATRSMOOTH
  };

//--- Reversal mode enum (matches Core ReversalMode)
enum ENUM_REVERSAL_MODE
  {
   REVERSAL_TRAILING_STOP,
   REVERSAL_CLOSE_TO_REFERENCE
  };

//+------------------------------------------------------------------+
//| Custom indicator initialization function                         |
//+------------------------------------------------------------------+
int OnInit()
  {
   //--- Set up indicator buffers
   SetIndexBuffer(BFFER_REFERENCE,ExtReferenceBuffer);
   SetIndexBuffer(BFFER_DISTANCE,ExtDistanceBuffer);
   Set Index_buffer(BFFER_SCALE,ExtScaleBuffer);
   Set Index_buffer(BFFER_NORMALIZED,ExtNormalizedBuffer);

   //--- Set labels
   IndicatorSetString(BFFER_REFERENCE,LABEL,"Reference");
   IndicatorSetString(BFFER_DISTANCE,LABEL,"Distance");
   IndicatorSetString(BFFER_SCALE,LABEL,"Scale");
   IndicatorSetString(BFFER_NORMALIZED,LABEL,"Normalized");

   //--- Initialize the .NET engine bridge
   if(!MT5Engine_Initialize(_Symbol,_Period))
     {
      Print("Failed to initialize ResearchFeatureEngine MT5 bridge.");
      return(INIT_FAILED);
     }

   EventSetTimer(1); // Refresh on new bar
   return(INIT_SUCCEEDED);
  }

//+------------------------------------------------------------------+
//| Custom indicator deinitialization function                       |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
  {
   MT5Engine_Shutdown();
   EventKillTimer();
  }

//+------------------------------------------------------------------+
//| Custom indicator iteration function                              |
//+------------------------------------------------------------------+
int OnCalculate(const int rates_total,
                const int prev_calculated,
                const int begin,
                const MqlRates& rates[])
  {
   int start=MathMax(1,prev_calculated-1);

   //--- Process bars
   for(int i=start; i<rates_total; i++)
     {
      double current_ref = MT5Engine_Update(
         rates[i].open,
         rates[i].high,
         rates[i].low,
         rates[i].close,
         rates[i].tick_volume,
         rates[i].time);

      ExtReferenceBuffer[i]  = MT5Engine_GetValue(0);  // Reference.Price
      ExtDistanceBuffer[i]   = MT5Engine_GetValue(1);  // Distance (signed)
      ExtScaleBuffer[i]      = MT5Engine_GetValue(2);  // Scale (ATR)
      ExtNormalizedBuffer[i] = MT5Engine_GetValue(3);  // Normalized
     }

   return(rates_total);
  }

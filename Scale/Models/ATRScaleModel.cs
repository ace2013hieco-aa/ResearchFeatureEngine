using System;
using ResearchFeatureEngine.Core.Engine;

namespace ResearchFeatureEngine.Scale.Models
{
    /// <summary>
    /// Estimates the characteristic scale using the Average True Range (ATR).
    /// </summary>
    public sealed class ATRScaleModel : IScaleModel
    {
        private readonly int _period;

        public ATRScaleModel(int period)
        {
            if (period < 1)
                throw new ArgumentOutOfRangeException(nameof(period));

            _period = period;
        }

        public double Compute(EngineContext context)
        {
            if (context.Index < 1)
                return context.MarketData.Close[0];

            int start = Math.Max(1, context.Index - _period + 1);

            double sum = 0.0;
            int count = 0;

            for (int i = start; i <= context.Index; i++)
            {
                double high = context.MarketData.High[i];
                double low = context.MarketData.Low[i];
                double previousClose = context.MarketData.Close[i - 1];

                double trueRange = Math.Max(
                    high - low,
                    Math.Max(
                        Math.Abs(high - previousClose),
                        Math.Abs(low - previousClose)));

                sum += trueRange;
                count++;
            }

            return count > 0 ? sum / count : context.MarketData.Close[0];
        }
    }
}


using System;

// Standalone reference computation for the Known Dataset.
// Mirrors the algorithm in ATRSmoothReferenceSource and prints the
// per-bar reference price plus the final reference/distance/scale/normalized.
internal static class Program
{
    public static void Main()
    {
        const int barCount = 20;
        const double startPrice = 100.0;
        const double step = 2.0;
        const int atrPeriod = 14;
        const double atrMultiplier = 5.1;
        const int smoothLength = 20;

        double[] close = new double[barCount];
        double[] high = new double[barCount];
        double[] low = new double[barCount];
        double[] volume = new double[barCount];
        for (int i = 0; i < barCount; i++)
        {
            close[i] = startPrice + i * step;
            high[i] = close[i] + 0.5;
            low[i] = close[i] - 0.5;
            volume[i] = 100.0;
        }

        // ATR EMA state
        double emaTr = 0.0;
        double alpha = 2.0 / (atrPeriod + 1.0);

        // Trailing stop state
        double trailingStop = 0.0;
        double pos = 0.0;

        // VWMA state
        double sumPV = 0.0;
        double sumV = 0.0;

        double[] reference = new double[barCount];
        double[] tr = new double[barCount];

        for (int i = 0; i < barCount; i++)
        {
            // True Range
            if (i == 0)
            {
                tr[i] = high[i] - low[i];
            }
            else
            {
                double prevCloseForTr = close[i - 1];
                tr[i] = System.Math.Max(
                    high[i] - low[i],
                    System.Math.Max(
                        System.Math.Abs(high[i] - prevCloseForTr),
                        System.Math.Abs(low[i] - prevCloseForTr)));
            }

            if (i == 0)
            {
                emaTr = tr[i];
            }
            else
            {
                emaTr = alpha * tr[i] + (1.0 - alpha) * emaTr;
            }

            double atr = emaTr;
            double nLoss = atrMultiplier * atr;

            double prevStop;
            double prevClose;
            double prevPos;
            if (i == 0)
            {
                prevStop = close[i];
                prevClose = close[i];
                prevPos = 0.0;
            }
            else
            {
                prevStop = trailingStop;
                prevClose = close[i - 1];
                prevPos = pos;
            }

            double currentStop;
            if (close[i] > prevStop && prevClose > prevStop)
                currentStop = System.Math.Max(prevStop, close[i] - nLoss);
            else if (close[i] < prevStop && prevClose < prevStop)
                currentStop = System.Math.Min(prevStop, close[i] + nLoss);
            else if (close[i] > prevStop)
                currentStop = close[i] - nLoss;
            else
                currentStop = close[i] + nLoss;

            double currentPos;
            if (prevClose < prevStop && close[i] > prevStop)
                currentPos = 1.0;
            else if (prevClose > prevStop && close[i] < prevStop)
                currentPos = -1.0;
            else
                currentPos = prevPos;

            double pv = close[i] * volume[i];
            if (i == 0)
            {
                sumPV = pv;
                sumV = volume[i];
            }
            else
            {
                if (i - smoothLength >= 0)
                {
                    double oldClose = close[i - smoothLength];
                    double oldVolume = volume[i - smoothLength];
                    double oldPV = oldClose * oldVolume;
                    sumPV = sumPV + pv - oldPV;
                    sumV = sumV + volume[i] - oldVolume;
                }
                else
                {
                    sumPV += pv;
                    sumV += volume[i];
                }
            }

            double vwma = sumV > 0.0 ? sumPV / sumV : close[i];
            reference[i] = (vwma + currentStop) * 0.5;

            trailingStop = currentStop;
            pos = currentPos;
        }

        // Per-bar output
        for (int i = 0; i < barCount; i++)
        {
            // Re-derive per-bar emaTR for printing only.
            double emaTrI;
            if (i == 0) emaTrI = tr[0];
            else
            {
                emaTrI = tr[0];
                for (int j = 1; j <= i; j++)
                {
                    emaTrI = alpha * tr[j] + (1.0 - alpha) * emaTrI;
                }
            }
            Console.WriteLine($"i={i,2} close={close[i],7:F4} tr={tr[i],6:F4} emaTR={emaTrI,7:F6} ref={reference[i],12:F8}");
        }

        // Final reference price
        double ref19 = reference[barCount - 1];
        double directional = close[barCount - 1] - ref19;
        double absolute = System.Math.Abs(directional);

        // ATR Scale (the project's ATRScaleModel is a simple average of TR)
        // sum of TR from 1..19 (max(start, index-period+1) = max(1, 6) = 6)
        // Actually: period=14, index=19, start = max(1, 19-14+1) = 6
        int start = System.Math.Max(1, 19 - 14 + 1);
        double atrSum = 0.0;
        int atrCount = 0;
        for (int i = start; i <= 19; i++)
        {
            atrSum += tr[i];
            atrCount++;
        }
        double scale = atrSum / atrCount;
        double normalized = absolute / scale;

        // Statistics
        double mean = 0.0;
        for (int i = 0; i < barCount; i++) mean += close[i];
        mean /= barCount;
        double variance = 0.0;
        for (int i = 0; i < barCount; i++) variance += (close[i] - mean) * (close[i] - mean);
        variance /= (barCount - 1);
        double stddev = System.Math.Sqrt(variance);

        // Median
        double[] sorted = (double[])close.Clone();
        System.Array.Sort(sorted);
        double median = (sorted[9] + sorted[10]) * 0.5;

        // MAD
        double[] dev = new double[barCount];
        for (int i = 0; i < barCount; i++) dev[i] = System.Math.Abs(close[i] - median);
        System.Array.Sort(dev);
        double mad = (dev[9] + dev[10]) * 0.5;

        Console.WriteLine();
        Console.WriteLine($"Reference[19]   = {ref19:F10}");
        Console.WriteLine($"Directional     = {directional:F10}");
        Console.WriteLine($"Absolute        = {absolute:F10}");
        Console.WriteLine($"Scale           = {scale:F10}");
        Console.WriteLine($"Normalized      = {normalized:F10}");
        Console.WriteLine($"Mean            = {mean:F10}");
        Console.WriteLine($"Median          = {median:F10}");
        Console.WriteLine($"Variance        = {variance:F10}");
        Console.WriteLine($"StdDev          = {stddev:F10}");
        Console.WriteLine($"MAD             = {mad:F10}");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Interfaces;
using ResearchFeatureEngine.Normalization.Models;
using ResearchFeatureEngine.Reference;
using ResearchFeatureEngine.Reference.Configuration;
using ResearchFeatureEngine.Reference.Sources;
using ResearchFeatureEngine.Scale.Models;
using ResearchFeatureEngine.Statistics.Models;

namespace ResearchFeatureEngine.Harness
{
    /// <summary>
    /// Console harness that runs the SAME production pipeline as the
    /// cTrader indicator on a CSV file and prints per-bar values. The
    /// outputs can be compared directly with what the indicator shows
    /// on the cTrader chart, ensuring both share identical math.
    /// </summary>
    internal static class CTraderHarness
    {
        public static int Main(string[] args)
        {
            string csvPath = args.Length > 0
                ? args[0]
                : @"D:\Software\Distance\Tests\TestData\EURUSD_M1_10000.csv";

            int atrPeriod = 16;
            double atrMultiplier = 5.1;
            int smoothLength = 100;
            int scalePeriod = 14;
            int statisticsWindowSize = 252;
            int printEvery = 1;
            int fromIndex = 0;
            int toIndex = int.MaxValue;

            if (!File.Exists(csvPath))
            {
                Console.Error.WriteLine($"CSV not found: {csvPath}");
                return 1;
            }

            // ---------------------------------------------------------
            // Market data
            // ---------------------------------------------------------
            var marketData = new CsvMarketData(csvPath);

            // ---------------------------------------------------------
            // Build the EXACT same configuration the cTrader indicator
            // uses.
            // ---------------------------------------------------------
            var referenceSource = new ATRSmoothReferenceSource(
                new ATRSmoothConfiguration(
                    atrPeriod: atrPeriod,
                    atrMultiplier: atrMultiplier,
                    smoothLength: smoothLength));

            var statisticModels = new List<IStatisticModel>
            {
                new MeanModel(),
                new StandardDeviationModel(),
                new MinimumModel(),
                new MaximumModel(),
                new MedianModel(),
                new VarianceModel(),
                new MedianAbsoluteDeviationModel(),
                new RangeModel()
            };

            var options = new EngineOptions
            {
                StatisticsWindowSize = statisticsWindowSize
            };

            var configuration = new EngineConfiguration(
                marketData: marketData,
                values: new EngineValues(),
                referenceSource: referenceSource,
                scaleModel: new ATRScaleModel(scalePeriod),
                normalizationModel: new ScaleNormalizationModel(),
                statisticModels: statisticModels,
                options: options);

            var engine = new ResearchFeatureEngineBuilder(configuration).Build();

            // ---------------------------------------------------------
            // Header
            // ---------------------------------------------------------
            Console.WriteLine(
                "bar,close,reference,dir,abs,scale,norm,mean,stddev,min,max,median,mad");

            int processedBars = 0;

            while (marketData.MoveNext())
            {
                engine.Update();

                int idx = engine.Context.CurrentIndex - 1;

                if (idx < fromIndex || idx > toIndex)
                {
                    processedBars++;
                    continue;
                }

                if (processedBars % printEvery == 0)
                {
                    var v = engine.Values;
                    Console.WriteLine(
                        string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0},{1:F5},{2:F5},{3:F5},{4:F5},{5:F5},{6:F5},{7:F5},{8:F5},{9:F5},{10:F5},{11:F5},{12:F5}",
                            idx,
                            marketData.Close[idx],
                            v.Reference.Price,
                            v.Distance.DirectionalExtension,
                            v.Distance.AbsoluteExtension,
                            v.Scale.Scale,
                            v.Normalization.NormalizedMeasurement,
                            v.Statistics.Location.Mean,
                            v.Statistics.Dispersion.StandardDeviation,
                            v.Statistics.Range.Minimum,
                            v.Statistics.Range.Maximum,
                            v.Statistics.Location.Median,
                            v.Statistics.Dispersion.MedianAbsoluteDeviation));
                }

                processedBars++;
            }

            Console.Error.WriteLine($"Processed {processedBars} bars.");
            return 0;
        }
    }
}

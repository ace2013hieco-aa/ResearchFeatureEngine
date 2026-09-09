using System;
using System.Collections.Generic;
using System.Globalization;

using ResearchFeatureEngine.Core;
using ResearchFeatureEngine.Models;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Export schema version (M10.0 §15). A schema version never
    /// rewrites the meaning of an existing artifact: additive column
    /// changes bump the minor version; removals/renames/semantic
    /// changes bump the major version.
    /// </summary>
    public static class Schema
    {
        public const string Version = "measurement-export/1.0.0";
        public const string ExporterVersion = "1.0.0";

        public static string[] Columns(ExportMode mode)
        {
            var cols = new List<string>
            {
                // Identity + source bar (verbatim tokens)
                "bar_index",
                "timestamp",
                "open",
                "high",
                "low",
                "close",
                "tick_volume",

                // Reference
                "reference_price",
                "reference_regime",

                // Distance / Scale / Normalization
                "distance_signed",
                "distance_absolute",
                "scale",
                "normalized_measurement",

                // Statistics (retain-last-published convention;
                // validity = observation_count >= min_n per column)
                "statistics_observation_count",
                "statistics_mean",
                "statistics_median",
                "statistics_minimum",
                "statistics_maximum",
                "statistics_range",
                "statistics_mad",
                "statistics_variance",
                "statistics_standard_deviation",
                "statistics_skewness",
                "statistics_kurtosis",

                // Reversal (canonical regime-change semantics)
                "reversal_bars_since",
                "reversal_direction",
                "reversal_is_bar"
            };

            // The M9 segment stage is registered ONLY for the
            // ATRSmooth-based compositions (ResearchFeatureEngineBuilder
            // :129-135); in Darvas/Hma modes the segment values remain
            // at their unavailable defaults and the columns are absent
            // from the schema (M10.0 ratified column counts:
            // ATRSmooth2 32 / HmaAtrSmooth 34 / DarvasBox 31 / Hma 27).
            if (mode == ExportMode.ATRSmooth2 || mode == ExportMode.HmaAtrSmooth)
            {
                cols.AddRange(new[]
                {
                    "segment_regime",
                    "segment_id",
                    "segment_start_index",
                    "segment_age",
                    "segment_transition"
                });
            }

            if (mode == ExportMode.DarvasBox)
            {
                cols.AddRange(new[]
                {
                    "darvas_has_box",
                    "darvas_signed_closing_distance",
                    "darvas_absolute_closing_distance",
                    "darvas_mean_signed_distance"
                });
            }

            if (mode == ExportMode.HmaAtrSmooth)
            {
                cols.AddRange(new[]
                {
                    "mean_hma_atrsmooth_distance",
                    "hma_price_atrsmooth_alignment"
                });
            }

            return cols.ToArray();
        }

        /// <summary>
        /// Minimum observation counts for statistics validity, keyed by
        /// column name — schema metadata (M10.0 §3), not a transform.
        /// Source of truth: Statistics/Models/* (Mean/Median/Min/Max/
        /// Range/MAD = 1, Variance/StdDev = 2, Skewness = 3,
        /// Kurtosis = 4).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> StatisticsMinimumN =
            new Dictionary<string, int>
            {
                ["statistics_mean"] = 1,
                ["statistics_median"] = 1,
                ["statistics_minimum"] = 1,
                ["statistics_maximum"] = 1,
                ["statistics_range"] = 1,
                ["statistics_mad"] = 1,
                ["statistics_variance"] = 2,
                ["statistics_standard_deviation"] = 2,
                ["statistics_skewness"] = 3,
                ["statistics_kurtosis"] = 4
            };

        /// <summary>
        /// Serialize one bar's runtime values into the deterministic
        /// row token array, in column order. Every measurement token
        /// is produced from the CURRENT runtime values — the engine's
        /// own computation — with zero recomputation by the exporter.
        /// </summary>
        public static string[] RenderRow(
            int barIndex,
            ParsedBar bar,
            EngineValues values,
            ExportMode mode)
        {
            var row = new List<string>(40)
            {
                barIndex.ToString(CultureInfo.InvariantCulture),
                bar.Tokens[0],
                bar.Tokens[1],
                bar.Tokens[2],
                bar.Tokens[3],
                bar.Tokens[4],
                bar.Tokens[5],
                DoubleToToken(values.Reference.Price),
                DoubleToToken(values.Reference.Regime),
                DoubleToToken(values.Distance.DirectionalExtension),
                DoubleToToken(values.Distance.AbsoluteExtension),
                DoubleToToken(values.Scale.Scale),
                DoubleToToken(values.Normalization.NormalizedMeasurement),
                values.Statistics.ObservationCount.ToString(CultureInfo.InvariantCulture),
                DoubleToToken(values.Statistics.Location.Mean),
                DoubleToToken(values.Statistics.Location.Median),
                DoubleToToken(values.Statistics.Range.Minimum),
                DoubleToToken(values.Statistics.Range.Maximum),
                DoubleToToken(values.Statistics.Range.Range),
                DoubleToToken(values.Statistics.Dispersion.MedianAbsoluteDeviation),
                DoubleToToken(values.Statistics.Dispersion.Variance),
                DoubleToToken(values.Statistics.Dispersion.StandardDeviation),
                DoubleToToken(values.Statistics.Shape.Skewness),
                DoubleToToken(values.Statistics.Shape.Kurtosis),
                IntNullToToken(values.Reversal.BarsSinceReversal),
                ((int)values.Reversal.Direction).ToString(CultureInfo.InvariantCulture),
                values.Reversal.IsReversalBar ? "1" : "0"
            };

            // Segment family: rendered ONLY for ATRSmooth-based modes,
            // mirroring Columns() exactly (the M9 stage is registered
            // only for those compositions).
            if (mode == ExportMode.ATRSmooth2 || mode == ExportMode.HmaAtrSmooth)
            {
                row.Add(((int)values.AtrSmoothRegimeSegment.Regime).ToString(CultureInfo.InvariantCulture));
                row.Add(IntNullToToken(values.AtrSmoothRegimeSegment.RegimeId));
                row.Add(IntNullToToken(values.AtrSmoothRegimeSegment.RegimeStartIndex));
                row.Add(IntNullToToken(values.AtrSmoothRegimeSegment.RegimeAge));
                row.Add(((int)values.AtrSmoothRegimeSegment.RegimeTransition).ToString(CultureInfo.InvariantCulture));
            }

            if (mode == ExportMode.DarvasBox)
            {
                row.Add(values.DarvasBoxDistance.HasBox ? "1" : "0");
                row.Add(DoubleToToken(values.DarvasBoxDistance.SignedClosingDistance));
                row.Add(DoubleToToken(values.DarvasBoxDistance.AbsoluteClosingDistance));
                row.Add(DoubleToToken(values.MeanDarvasClosingDistance.MeanSignedDistance));
            }

            if (mode == ExportMode.HmaAtrSmooth)
            {
                row.Add(DoubleToToken(values.MeanHmaAtrSmoothDistance.MeanSignedDistance));
                row.Add(((int)values.HmaPriceAtrSmoothAlignment.Alignment).ToString(CultureInfo.InvariantCulture));
            }

            return row.ToArray();
        }

        /// <summary>
        /// "R" round-trip double formatting, invariant culture. NaN →
        /// "NaN", +∞ → "Infinity", -∞ → "-Infinity" (all parse back
        /// under the invariant culture). Sign of zero is preserved as
        /// produced by the engine.
        /// </summary>
        public static string DoubleToToken(double value)
        {
            if (double.IsNaN(value))
            {
                return "NaN";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "Infinity";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "-Infinity";
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string IntNullToToken(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "";
        }
    }
}

using System;
using System.Globalization;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// First-calendar-year research/holdout partition (M10.1 §6).
    ///
    /// For a dataset whose first chronological bar opens at
    /// first_timestamp, the RESEARCH window is
    ///
    ///     [first_timestamp, first_timestamp + 1 calendar year)
    ///
    /// and the HOLDOUT begins at first_timestamp + 1 calendar year —
    /// computed on the calendar (AddYears), never 365*24h. AddYears
    /// is leap-year correct: 2024-02-29 + 1y = 2025-02-28 23:59...
    /// boundary lands on 2025-02-28T00:00, and 2023-03-01 + 1y =
    /// 2024-03-01 (the leap day 2024-02-29 falls INSIDE the window).
    ///
    /// Per-dataset independence: every dataset computes its own
    /// boundary from its OWN first bar. No global cutoff exists.
    /// </summary>
    public sealed class Partition
    {
        public DateTime ResearchStart { get; }
        public DateTime ResearchEndExclusive { get; }

        public Partition(DateTime firstUtc)
        {
            ResearchStart = firstUtc;
            ResearchEndExclusive = firstUtc.AddYears(1);
        }

        public DateTime HoldoutStart => ResearchEndExclusive;

        /// <summary>
        /// ISO-8601 UTC boundary rendering for the manifest
        /// ("yyyy-MM-ddTHH:mm:ss.fffffffZ", invariant culture) — the
        /// DateTime kind is Utc by construction.
        /// </summary>
        public string ResearchStartText =>
            ResearchStart.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

        public string ResearchEndText =>
            ResearchEndExclusive.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

        public string HoldoutStartText =>
            HoldoutStart.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

        /// <summary>
        /// True if the given parsed timestamp belongs to the research
        /// window (strictly before the exclusive end).
        /// </summary>
        public bool IsResearch(DateTime utc)
        {
            return utc < ResearchEndExclusive;
        }

        /// <summary>
        /// True if the given parsed timestamp belongs to the holdout.
        /// </summary>
        public bool IsHoldout(DateTime utc)
        {
            return utc >= ResearchEndExclusive;
        }
    }
}

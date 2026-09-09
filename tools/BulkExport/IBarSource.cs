using System;
using System.Collections.Generic;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// One parsed source bar, retaining the VERBATIM source tokens so
    /// the exporter can re-emit the source fields byte-identically
    /// (no numeric re-formatting of input fields).
    /// </summary>
    public struct ParsedBar
    {
        public string[] Tokens;
        public DateTime Utc;
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public double Volume;
    }

    /// <summary>
    /// Full-file validated scan result (pass 1).
    /// </summary>
    public struct ScanResult
    {
        public SourceSchemaKind Kind;
        public long RowCount;
        public DateTime FirstUtc;
        public string FirstToken;
        public DateTime LastUtc;
        public string LastToken;
        public long DuplicateTimestampCount;
    }

    /// <summary>
    /// Abstraction over the frozen capture file. The production
    /// implementation reads CSV; tests inject wrappers to exercise
    /// failure paths deterministically. All methods re-read the file
    /// from disk — no token retention, bounded memory.
    /// </summary>
    public interface IBarSource
    {
        /// <summary>Schema recognized from the capture header.</summary>
        SourceSchemaKind Kind { get; }

        /// <summary>
        /// Pass 1: full streaming validation — header, field count,
        /// token character set, timestamp parseability, chronological
        /// non-decreasing order, duplicate count, first/last tokens.
        /// </summary>
        ScanResult Scan();

        /// <summary>
        /// Count of the leading bars whose timestamp is strictly
        /// before <paramref name="exclusiveUpperUtc"/>.
        /// </summary>
        int CountBarsBefore(DateTime exclusiveUpperUtc);

        /// <summary>
        /// Count of the leading bars whose timestamp is at or before
        /// <paramref name="inclusiveUpperUtc"/>.
        /// </summary>
        int CountBarsUpTo(DateTime inclusiveUpperUtc);

        /// <summary>
        /// Materialize exactly <paramref name="count"/> leading bars
        /// into an indexed market-data adapter (the engine interface
        /// requires index-based arrays — this is the documented,
        /// engine-imposed materialization step).
        /// </summary>
        ArraysMarketData Materialize(int count);

        /// <summary>
        /// Stream the data rows (header skipped), row k = bar k.
        /// Used by the write phase to re-emit verbatim source tokens.
        /// </summary>
        IEnumerable<ParsedBar> Stream();
    }
}

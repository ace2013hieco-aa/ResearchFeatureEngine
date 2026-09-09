using System;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// The supported frozen-capture source schemas.
    /// </summary>
    public enum SourceSchemaKind
    {
        /// <summary>
        /// MarketDataRecorder V1 export: OpenTimeUtc,Open,High,Low,Close,TickVolume.
        /// </summary>
        RecorderV1,

        /// <summary>
        /// Test-fixture schema: DateTime,Open,High,Low,Close,Volume.
        /// </summary>
        FixtureV1,

        /// <summary>
        /// MarketDataRecorder V1.1 export (spread recording, recorder
        /// commit 2795dd0, 2026-09-05): the V1 columns plus a trailing
        /// Spread field. Every owner-ratified production capture is
        /// V1.1. Spread is validated source-field provenance only —
        /// never an engine input (M10.1.x §4).
        /// </summary>
        RecorderV1_1
    }

    /// <summary>
    /// Exact-match recognition and naming of the supported capture
    /// headers. The exporter never accepts an unknown header.
    /// </summary>
    public static class SourceSchema
    {
        public const string RecorderHeader = "OpenTimeUtc,Open,High,Low,Close,TickVolume";
        public const string RecorderV1_1Header = "OpenTimeUtc,Open,High,Low,Close,TickVolume,Spread";
        public const string FixtureHeader = "DateTime,Open,High,Low,Close,Volume";

        public static SourceSchemaKind? Detect(string headerLine)
        {
            if (string.Equals(headerLine, RecorderV1_1Header, StringComparison.Ordinal))
            {
                return SourceSchemaKind.RecorderV1_1;
            }

            if (string.Equals(headerLine, RecorderHeader, StringComparison.Ordinal))
            {
                return SourceSchemaKind.RecorderV1;
            }

            if (string.Equals(headerLine, FixtureHeader, StringComparison.Ordinal))
            {
                return SourceSchemaKind.FixtureV1;
            }

            return null;
        }

        public static string Name(SourceSchemaKind kind)
        {
            return kind switch
            {
                SourceSchemaKind.RecorderV1_1 => "recorder_v1_1",
                SourceSchemaKind.RecorderV1 => "recorder_v1",
                _ => "fixture_v1"
            };
        }

        /// <summary>Exact field count per schema (header-inclusive row split).</summary>
        public static int FieldCount(SourceSchemaKind kind)
        {
            return kind == SourceSchemaKind.RecorderV1_1 ? 7 : 6;
        }
    }
}

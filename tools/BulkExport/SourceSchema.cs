using System;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// The two supported frozen-capture source schemas.
    /// </summary>
    public enum SourceSchemaKind
    {
        /// <summary>
        /// MarketDataRecorder export: OpenTimeUtc,Open,High,Low,Close,TickVolume.
        /// </summary>
        RecorderV1,

        /// <summary>
        /// Test-fixture schema: DateTime,Open,High,Low,Close,Volume.
        /// </summary>
        FixtureV1
    }

    /// <summary>
    /// Exact-match recognition and naming of the supported capture
    /// headers. The exporter never accepts an unknown header.
    /// </summary>
    public static class SourceSchema
    {
        public const string RecorderHeader = "OpenTimeUtc,Open,High,Low,Close,TickVolume";
        public const string FixtureHeader = "DateTime,Open,High,Low,Close,Volume";

        public static SourceSchemaKind? Detect(string headerLine)
        {
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
            return kind == SourceSchemaKind.RecorderV1
                ? "recorder_v1"
                : "fixture_v1";
        }
    }
}

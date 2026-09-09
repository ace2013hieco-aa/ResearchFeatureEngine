using System.Collections.Generic;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Manifest data (M10.0 §8). All timestamps are verbatim source
    /// tokens or deterministic ISO-8601 boundary renderings — never
    /// wall clock.
    /// </summary>
    public sealed class ExportManifest
    {
        public string SchemaVersion { get; set; } = "";
        public string ExporterVersion { get; set; } = "";
        public string EngineCommit { get; set; } = "";
        public bool EngineTreeDirty { get; set; }
        public string EngineConfigurationJson { get; set; } = "";
        public string DatasetId { get; set; } = "";
        public string DatasetVersion { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public string SourceSchema { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public string SourcePreprocessing { get; set; } = "none";
        public long RowCount { get; set; }
        public string FirstTimestamp { get; set; } = "";
        public string LastTimestamp { get; set; } = "";
        public long DuplicateTimestampCount { get; set; }
        public string PartitionPolicy { get; set; } = "";
        public string ResearchStart { get; set; } = "";
        public string ResearchEnd { get; set; } = "";
        public string HoldoutStart { get; set; } = "";
        public string ArtifactFile { get; set; } = "";
        public string ArtifactSha256 { get; set; } = "";
        public IReadOnlyList<string> Columns { get; set; } = new List<string>();
    }
}

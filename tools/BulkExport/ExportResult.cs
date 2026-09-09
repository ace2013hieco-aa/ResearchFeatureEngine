using System;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>Post-run facts for the summary/audit report.</summary>
    public sealed class ExportResult
    {
        public string ArtifactPath { get; set; } = "";
        public string ManifestPath { get; set; } = "";
        public string ArtifactSha256 { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public long ResearchRows { get; set; }
        public long TotalSourceRows { get; set; }
        public bool HasHoldout { get; set; }
        public DateTime ResearchEndUtc { get; set; }
        public string FirstTimestamp { get; set; } = "";
        public string LastResearchTimestamp { get; set; } = "";
        public TimeSpan Elapsed { get; set; }
    }
}

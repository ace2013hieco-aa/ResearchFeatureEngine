using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Frozen-input registry entry (M10.0 §9). A dataset NOT in this
    /// registry, or one whose SHA-256 mismatches, fails closed before
    /// any processing. The registry is committed alongside the tool;
    /// production datasets are added by owner action (M10.2).
    /// </summary>
    public sealed class DatasetRegistryEntry
    {
        /// <summary>Owner-registered unique dataset identifier.</summary>
        [JsonPropertyName("dataset_id")]
        public string DatasetId { get; set; } = "";

        /// <summary>Dataset version (semver-like, e.g. "v1").</summary>
        [JsonPropertyName("dataset_version")]
        public string DatasetVersion { get; set; } = "";

        /// <summary>File name of the frozen capture (registry-relative or absolute).</summary>
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";

        /// <summary>SHA-256 of the frozen capture bytes (hex, lowercase).</summary>
        [JsonPropertyName("source_sha256")]
        public string SourceSha256 { get; set; } = "";

        /// <summary>First bar timestamp, verbatim source token.</summary>
        [JsonPropertyName("first_timestamp")]
        public string FirstTimestamp { get; set; } = "";

        /// <summary>Last bar timestamp, verbatim source token.</summary>
        [JsonPropertyName("last_timestamp")]
        public string LastTimestamp { get; set; } = "";

        /// <summary>
        /// Declared source schema (M10.1.x §8): one of
        /// "recorder_v1", "recorder_v1_1", "fixture_v1". Optional —
        /// absent means the schema is established by exact header
        /// detection (M10.1 behavior). When declared, it is an exact
        /// pin: a mismatch with the actual capture header fails
        /// closed. No wildcard exists.
        /// </summary>
        [JsonPropertyName("source_schema")]
        public string SourceSchema { get; set; } = "";

        /// <summary>
        /// Partition policy; the only supported value is
        /// "first_calendar_year_only". Any other value fails closed.
        /// </summary>
        [JsonPropertyName("partition_policy")]
        public string PartitionPolicy { get; set; } = "first_calendar_year_only";
    }

    /// <summary>
    /// Registry loader. Deterministic ordering by dataset_id; strict
    /// field validation; the empty dataset list is legal (the tool is
    /// shipped with zero production entries — M10.2 adds them).
    /// </summary>
    public static class DatasetRegistry
    {
        public const string PolicyName = "first_calendar_year_only";

        public static IReadOnlyList<DatasetRegistryEntry> Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new ExportException($"Dataset registry not found: {path}");
            }

            List<DatasetRegistryEntry>? entries;
            try
            {
                entries = JsonSerializer.Deserialize<List<DatasetRegistryEntry>>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                throw new ExportException($"Dataset registry is not valid JSON: {ex.Message}");
            }

            var result = new List<DatasetRegistryEntry>();
            if (entries is null || entries.Count == 0)
            {
                return result;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DatasetRegistryEntry e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.DatasetId))
                {
                    throw new ExportException("Registry entry with empty dataset_id.");
                }

                if (!seenIds.Add(e.DatasetId))
                {
                    throw new ExportException($"Duplicate dataset_id in registry: {e.DatasetId}");
                }

                if (string.IsNullOrWhiteSpace(e.DatasetVersion))
                {
                    throw new ExportException($"Registry entry '{e.DatasetId}' missing dataset_version.");
                }

                if (string.IsNullOrWhiteSpace(e.Filename))
                {
                    throw new ExportException($"Registry entry '{e.DatasetId}' missing filename.");
                }

                if (string.IsNullOrWhiteSpace(e.SourceSha256))
                {
                    throw new ExportException($"Registry entry '{e.DatasetId}' missing source_sha256.");
                }

                if (e.SourceSha256.Length != 64 || !IsLowerHex(e.SourceSha256))
                {
                    throw new ExportException(
                        $"Registry entry '{e.DatasetId}' source_sha256 must be 64 lowercase hex chars.");
                }

                if (!string.IsNullOrWhiteSpace(e.SourceSchema)
                    && e.SourceSchema != "recorder_v1"
                    && e.SourceSchema != "recorder_v1_1"
                    && e.SourceSchema != "fixture_v1")
                {
                    throw new ExportException(
                        $"Registry entry '{e.DatasetId}' has unsupported source_schema " +
                        $"'{e.SourceSchema}' (valid: recorder_v1, recorder_v1_1, fixture_v1).");
                }

                if (!string.Equals(e.PartitionPolicy, PolicyName, StringComparison.Ordinal))
                {
                    throw new ExportException(
                        $"Registry entry '{e.DatasetId}' has unsupported partition_policy "
                        + $"'{e.PartitionPolicy}' (only '{PolicyName}' is supported).");
                }

                result.Add(e);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.DatasetId, b.DatasetId));
            return result;
        }

        public static DatasetRegistryEntry Find(
            IReadOnlyList<DatasetRegistryEntry> entries,
            string datasetId)
        {
            foreach (DatasetRegistryEntry e in entries)
            {
                if (string.Equals(e.DatasetId, datasetId, StringComparison.Ordinal))
                {
                    return e;
                }
            }

            throw new ExportException(
                $"dataset_id '{datasetId}' is not registered. Only registered frozen "
                + "captures may be exported; live or arbitrary files fail closed.");
        }

        private static bool IsLowerHex(string value)
        {
            foreach (char c in value)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

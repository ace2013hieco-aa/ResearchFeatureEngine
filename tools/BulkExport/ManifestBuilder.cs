using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Manifest builder producing the deterministic JSON manifest
    /// (M10.0 §8, M10.1 §14): UTF-8 no BOM, LF endings, 2-space
    /// indentation, fixed key order, trailing LF, no wall-clock
    /// timestamps. Key order is the contract — the builder emits
    /// fields strictly in the registered order.
    /// </summary>
    public static class ManifestBuilder
    {
        /// <summary>
        /// Render the manifest body (without trailing LF) from the
        /// completed export facts.
        /// </summary>
        public static string Render(ExportManifest m)
        {
            var sb = new StringBuilder(2048);

            sb.Append("{\n");
            AppendKeyValue(sb, "schema_version", m.SchemaVersion);
            AppendKeyValue(sb, "exporter_version", m.ExporterVersion);
            AppendKeyValue(sb, "engine_commit", m.EngineCommit);
            AppendKeyValue(sb, "engine_tree_dirty", m.EngineTreeDirty);
            AppendKeyValue(sb, "engine_configuration", m.EngineConfigurationJson);
            AppendKeyValue(sb, "dataset_id", m.DatasetId);
            AppendKeyValue(sb, "dataset_version", m.DatasetVersion);
            AppendKeyValue(sb, "source_file", m.SourceFile);
            AppendKeyValue(sb, "source_schema", m.SourceSchema);
            AppendKeyValue(sb, "source_sha256", m.SourceSha256);
            AppendKeyValue(sb, "source_preprocessing", m.SourcePreprocessing);
            AppendKeyValue(sb, "row_count", m.RowCount);
            AppendKeyValue(sb, "first_timestamp", m.FirstTimestamp);
            AppendKeyValue(sb, "last_timestamp", m.LastTimestamp);
            AppendKeyValue(sb, "duplicate_timestamp_count", m.DuplicateTimestampCount);
            AppendKeyValue(sb, "partition_policy", m.PartitionPolicy);
            AppendKeyValue(sb, "research_start", m.ResearchStart);
            AppendKeyValue(sb, "research_end", m.ResearchEnd);
            AppendKeyValue(sb, "holdout_start", m.HoldoutStart);
            AppendKeyValue(sb, "artifact_file", m.ArtifactFile);
            AppendKeyValue(sb, "artifact_sha256", m.ArtifactSha256);

            sb.Append("  \"columns\": [\n");
            for (int i = 0; i < m.Columns.Count; i++)
            {
                sb.Append("    ");
                AppendJsonString(sb, m.Columns[i]);
                sb.Append(i < m.Columns.Count - 1 ? ",\n" : "\n");
            }

            sb.Append("  ]\n");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Write manifest bytes: UTF-8 no BOM, LF, trailing LF.</summary>
        public static void Write(string path, ExportManifest m)
        {
            string body = Render(m);
            byte[] bytes = Encoding.UTF8.GetBytes(body + "\n");
            File.WriteAllBytes(path, bytes);
        }

        private static void AppendKeyValue(StringBuilder sb, string key, string value)
        {
            sb.Append("  ");
            AppendJsonString(sb, key);
            sb.Append(": ");
            AppendJsonString(sb, value);
            sb.Append(",\n");
        }

        private static void AppendKeyValue(StringBuilder sb, string key, bool value)
        {
            sb.Append("  ");
            AppendJsonString(sb, key);
            sb.Append(": ");
            sb.Append(value ? "true" : "false");
            sb.Append(",\n");
        }

        private static void AppendKeyValue(StringBuilder sb, string key, long value)
        {
            sb.Append("  ");
            AppendJsonString(sb, key);
            sb.Append(": ");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\n");
        }

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
        }
    }
}

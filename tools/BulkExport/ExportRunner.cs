using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using ResearchFeatureEngine.Composition;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// The export pipeline (M10.0 §10 / M10.1 §3, §15–§18):
    ///
    ///   registered frozen input
    ///     → pre-run hash gate (registry + SHA-256)
    ///     → pass 1: scan/validate/order/duplicate/identity checks
    ///     → chronological partition (first calendar year only)
    ///     → materialize research window (engine needs indexed arrays)
    ///     → engine construction via ResearchFeatureEngineBuilder
    ///     → pass 2: Update() per bar, runtime-value extraction,
    ///       deterministic CSV, incremental SHA-256
    ///     → post-run source re-hash (mutation detection)
    ///     → manifest + atomic finalization
    ///
    /// The runner contains ZERO engine mathematics: every exported
    /// measurement token is read from EngineValues after the engine's
    /// own Update(). The research window is the FIRST CALENDAR YEAR
    /// of the dataset ([first, first+1y)); every later bar is the
    /// sealed holdout and is NEVER written by this tool. Memory is
    /// bounded by the engine's own index-based input contract: the
    /// research window's six arrays plus engine state — the holdout
    /// is streamed past in pass 1 only, never materialized.
    /// </summary>
    public sealed class ExportRunner
    {
        private readonly string _sourcePath;
        private readonly string _outputPath;
        private readonly string _datasetId;
        private readonly ExportMode _mode;
        private readonly DatasetRegistryEntry _entry;
        private readonly string _engineCommit;
        private readonly IBarSource _source;

        private ExportRunner(
            string sourcePath,
            string outputPath,
            string datasetId,
            ExportMode mode,
            DatasetRegistryEntry entry,
            string engineCommit,
            IBarSource source)
        {
            _sourcePath = sourcePath;
            _outputPath = outputPath;
            _datasetId = datasetId;
            _mode = mode;
            _entry = entry;
            _engineCommit = engineCommit;
            _source = source;
        }

        /// <summary>
        /// Resolve registry entry + capture path. The capture must be
        /// registered (dataset_id) and its pre-run SHA-256 must match
        /// the registry pin. The capture path is either the explicit
        /// --capture override or the registry entry's filename
        /// resolved against the registry file's directory.
        /// </summary>
        public static ExportRunner Create(
            IReadOnlyList<DatasetRegistryEntry> registry,
            string registryPath,
            string datasetId,
            string mode,
            string outputPath,
            string capturePathOverride,
            string? engineCommit = null)
        {
            DatasetRegistryEntry entry = DatasetRegistry.Find(registry, datasetId);

            string capturePath = capturePathOverride;
            if (string.IsNullOrWhiteSpace(capturePath))
            {
                string? dir = Path.GetDirectoryName(Path.GetFullPath(registryPath));
                capturePath = Path.Combine(dir ?? ".", entry.Filename);
            }

            if (!File.Exists(capturePath))
            {
                throw new ExportException($"Capture file not found: {capturePath}");
            }

            if (!TryParseMode(mode, out ExportMode parsed))
            {
                throw new ExportException(
                    $"Unknown mode '{mode}'. Valid: atrsmooth2 | darvasbox | hma | hmaatrsmooth.");
            }

            string commit = engineCommit is { Length: 40 } && IsLowerHex(engineCommit)
                ? engineCommit
                : ResolveBuiltEngineCommit();

            return new ExportRunner(
                capturePath,
                outputPath,
                datasetId,
                parsed,
                entry,
                commit,
                new CsvBarSource(capturePath));
        }

        /// <summary>
        /// Test hook: inject a custom IBarSource (failure-path and
        /// mutation-path wrappers) while keeping the real capture
        /// file as the hash target.
        /// </summary>
        internal static ExportRunner CreateWithSource(
            IBarSource source,
            string sourcePathForHashing,
            string datasetId,
            ExportMode mode,
            string outputPath,
            DatasetRegistryEntry entry,
            string engineCommit)
        {
            return new ExportRunner(
                sourcePathForHashing,
                outputPath,
                datasetId,
                mode,
                entry,
                engineCommit,
                source);
        }

        public ExportResult Run()
        {
            // ---------------------------------------------------------
            // Pre-run hash gate
            // ---------------------------------------------------------
            string sourceHash = Hashing.Sha256File(_sourcePath);

            if (!string.Equals(sourceHash, _entry.SourceSha256, StringComparison.Ordinal))
            {
                throw new ExportException(
                    $"Pre-run hash gate FAILED for dataset '{_datasetId}': registry pin "
                    + $"{_entry.SourceSha256} != actual {sourceHash}. The capture is not "
                    + "the registered frozen bytes; export refuses to proceed.");
            }

            // ---------------------------------------------------------
            // Pass 1 — validation, identity, partition
            // ---------------------------------------------------------
            ScanResult scan = _source.Scan();

            if (!string.Equals(scan.FirstToken, _entry.FirstTimestamp, StringComparison.Ordinal))
            {
                throw new ExportException(
                    $"Dataset identity mismatch for '{_datasetId}': registry first_timestamp "
                    + $"'{_entry.FirstTimestamp}' != capture '{scan.FirstToken}'.");
            }

            if (!string.Equals(scan.LastToken, _entry.LastTimestamp, StringComparison.Ordinal))
            {
                throw new ExportException(
                    $"Dataset identity mismatch for '{_datasetId}': registry last_timestamp "
                    + $"'{_entry.LastTimestamp}' != capture '{scan.LastToken}'.");
            }

            var partition = new Partition(scan.FirstUtc);
            int researchRows = _source.CountBarsBefore(partition.ResearchEndExclusive);
            bool hasHoldout = researchRows < scan.RowCount;

            if (researchRows == 0)
            {
                throw new ExportException(
                    $"Dataset '{_datasetId}' has no bars in the research window "
                    + $"[{partition.ResearchStartText}, {partition.ResearchEndText}).");
            }

            // ---------------------------------------------------------
            // Write phase — everything under atomic cleanup
            // ---------------------------------------------------------
            string finalArtifact = _outputPath;
            string finalManifest = finalArtifact + ".manifest.json";
            string partialArtifact = finalArtifact + ".partial";
            string partialManifest = finalManifest + ".partial";

            if (File.Exists(finalArtifact) || File.Exists(finalManifest))
            {
                throw new ExportException(
                    $"Target artifact or manifest already exists (refuse overwrite): "
                    + $"{finalArtifact}");
            }

            DeleteIfPresent(partialArtifact);
            DeleteIfPresent(partialManifest);

            string artifactHash;
            long writtenRows = 0;
            string? lastResearchToken = null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ArraysMarketData window = _source.Materialize(researchRows);

                EngineConfiguration configuration = PresetFactory.Create(_mode, window);
                var engine = new ResearchFeatureEngineBuilder(configuration).Build();

                string[] columns = Schema.Columns(_mode);

                using (var writer = new CsvWriter(partialArtifact))
                {
                    writer.WriteRow(columns);

                    int barIndex = 0;
                    foreach (ParsedBar bar in _source.Stream())
                    {
                        if (barIndex >= researchRows)
                        {
                            break;
                        }

                        engine.Update();

                        string[] row = Schema.RenderRow(
                            barIndex,
                            bar,
                            engine.Values,
                            _mode);

                        writer.WriteRow(row);

                        lastResearchToken = bar.Tokens[0];
                        writtenRows++;
                        barIndex++;

                        if (writtenRows % 500_000 == 0)
                        {
                            Console.Error.WriteLine(
                                "  progress: "
                                + writtenRows.ToString("N0", CultureInfo.InvariantCulture)
                                + " rows");
                        }
                    }

                    artifactHash = writer.HashHexFinal();
                }
            }
            catch
            {
                // §17: any failure deletes both partials — no partial
                // artifact may ever masquerade as a valid dataset.
                DeleteQuiet(partialArtifact);
                DeleteQuiet(partialManifest);
                throw;
            }

            stopwatch.Stop();

            if (writtenRows != researchRows)
            {
                DeleteQuiet(partialArtifact);
                DeleteQuiet(partialManifest);
                throw new ExportException(
                    $"Row-count mismatch: expected {researchRows} research rows, "
                    + $"wrote {writtenRows}.");
            }

            // ---------------------------------------------------------
            // §18 post-run source re-hash (mutation-during-run gate)
            // ---------------------------------------------------------
            string postHash = Hashing.Sha256File(_sourcePath);
            if (!string.Equals(postHash, sourceHash, StringComparison.Ordinal))
            {
                DeleteQuiet(partialArtifact);
                DeleteQuiet(partialManifest);
                throw new ExportException(
                    "Source capture mutated during the export (pre-hash != post-hash). "
                    + "Artifact deleted; export failed closed.");
            }

            // ---------------------------------------------------------
            // Manifest + atomic finalization
            // ---------------------------------------------------------
            var manifest = new ExportManifest
            {
                SchemaVersion = Schema.Version,
                ExporterVersion = Schema.ExporterVersion,
                EngineCommit = _engineCommit,
                EngineTreeDirty = false,
                EngineConfigurationJson = EngineConfigurationJson.Render(_mode),
                DatasetId = _datasetId,
                DatasetVersion = _entry.DatasetVersion,
                SourceFile = _entry.Filename,
                SourceSchema = SourceSchema.Name(scan.Kind),
                SourceSha256 = sourceHash,
                SourcePreprocessing = "none",
                RowCount = writtenRows,
                FirstTimestamp = scan.FirstToken,
                LastTimestamp = lastResearchToken ?? scan.FirstToken,
                DuplicateTimestampCount = scan.DuplicateTimestampCount,
                PartitionPolicy = DatasetRegistry.PolicyName,
                ResearchStart = partition.ResearchStartText,
                ResearchEnd = partition.ResearchEndText,
                HoldoutStart = partition.HoldoutStartText,
                ArtifactFile = Path.GetFileName(finalArtifact),
                ArtifactSha256 = artifactHash,
                Columns = columns_list(_mode)
            };

            try
            {
                ManifestBuilder.Write(partialManifest, manifest);

                // Atomic finalization: partials swap into their final
                // names only after every check above has passed.
                File.Move(partialArtifact, finalArtifact);
                File.Move(partialManifest, finalManifest);
            }
            catch
            {
                DeleteQuiet(partialArtifact);
                DeleteQuiet(partialManifest);
                DeleteQuiet(finalArtifact);
                DeleteQuiet(finalManifest);
                throw;
            }

            return new ExportResult
            {
                ArtifactPath = finalArtifact,
                ManifestPath = finalManifest,
                ArtifactSha256 = artifactHash,
                SourceSha256 = sourceHash,
                ResearchRows = writtenRows,
                TotalSourceRows = scan.RowCount,
                HasHoldout = hasHoldout,
                ResearchEndUtc = partition.ResearchEndExclusive,
                FirstTimestamp = scan.FirstToken,
                LastResearchTimestamp = lastResearchToken ?? "",
                Elapsed = stopwatch.Elapsed
            };
        }

        internal static IReadOnlyList<string> columns_list(ExportMode mode)
        {
            return Schema.Columns(mode);
        }

        private static string ResolveBuiltEngineCommit()
        {
            // Engine identity baked into the build via the EngineCommit
            // MSBuild property (BulkExport.csproj). 'unresolved' is the
            // visible, never-silent default for unpinned builds.
            string? commit = typeof(ExportRunner).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(m => string.Equals(m.Key, "EngineCommit", StringComparison.Ordinal))
                ?.Value;

            return string.IsNullOrWhiteSpace(commit) ? "unresolved" : commit!;
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

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteQuiet(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup during failure handling
            }
        }

        internal static bool TryParseMode(string mode, out ExportMode parsed)
        {
            switch (mode?.ToLowerInvariant())
            {
                case "atrsmooth2": parsed = ExportMode.ATRSmooth2; return true;
                case "darvasbox": parsed = ExportMode.DarvasBox; return true;
                case "hma": parsed = ExportMode.Hma; return true;
                case "hmaatrsmooth": parsed = ExportMode.HmaAtrSmooth; return true;
                default: parsed = ExportMode.ATRSmooth2; return false;
            }
        }
    }
}

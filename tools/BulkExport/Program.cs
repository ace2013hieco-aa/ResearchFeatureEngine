using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// BulkExport CLI. Deterministic interface:
    ///
    ///   BulkExport --registry &lt;path&gt; --dataset &lt;id&gt; --mode &lt;m&gt;
    ///              --capture &lt;path&gt; --out &lt;file.csv&gt;
    ///              [--engine-commit &lt;sha&gt;]
    ///
    /// Exit codes: 0 success; 1 usage; 2 fail-closed export error;
    /// 3 unexpected exception. The tool NEVER deletes pre-existing
    /// artifacts (refuse overwrite) and writes everything through
    /// .partial files that are finalized atomically.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            Dictionary<string, string> opts;
            try
            {
                opts = ParseArgs(args);
            }
            catch (ExportException ex)
            {
                Console.Error.WriteLine($"argument error: {ex.Message}");
                PrintUsage();
                return 1;
            }

            try
            {
                string registryPath = Require(opts, "registry");
                string datasetId = Require(opts, "dataset");
                string mode = Require(opts, "mode");
                string outputPath = Require(opts, "out");
                string capturePath = opts.TryGetValue("capture", out string? cp) && cp != null
                    ? cp
                    : "";

                IReadOnlyList<DatasetRegistryEntry> registry =
                    DatasetRegistry.Load(registryPath);

                ExportRunner runner = ExportRunner.Create(
                    registry,
                    registryPath,
                    datasetId,
                    mode,
                    outputPath,
                    capturePath,
                    opts.TryGetValue("engine-commit", out string? ec) ? ec : null);

                ExportResult result = runner.Run();

                Console.WriteLine(
                    "dataset=" + result.DatasetSummary());

                return 0;
            }
            catch (ExportException ex)
            {
                Console.Error.WriteLine("EXPORT FAILED (fail-closed): " + ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("UNEXPECTED FAILURE: " + ex.Message);
                return 3;
            }
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var opts = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ExportException($"Unexpected positional argument: {arg}");
                }

                int eq = arg.IndexOf('=', StringComparison.Ordinal);
                string key;
                string value;

                if (eq > 2)
                {
                    key = arg.Substring(2, eq - 2);
                    value = arg.Substring(eq + 1);
                }
                else
                {
                    key = arg.Substring(2);
                    if (i + 1 >= args.Length)
                    {
                        throw new ExportException($"Missing value for --{key}");
                    }

                    value = args[++i];
                }

                if (opts.ContainsKey(key))
                {
                    throw new ExportException($"Duplicate argument --{key}");
                }

                opts[key] = value;
            }

            return opts;
        }

        private static string Require(Dictionary<string, string> opts, string key)
        {
            if (opts.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            throw new ExportException($"Missing required argument --{key}");
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(
                "usage: BulkExport --registry <registry.json> --dataset <dataset_id> " +
                "--mode <atrsmooth2|darvasbox|hma|hmaatrsmooth> --capture <capture.csv> " +
                "--out <artifact.csv> [--engine-commit <sha40>]");
        }
    }

    /// <summary>Summary line helper used by the CLI output.</summary>
    internal static class ExportResultExtensions
    {
        public static string DatasetSummary(this ExportResult r)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|rows={1}|total={2}|has_holdout={3}|research_end={4}|artifact_sha256={5}",
                r.ArtifactPath,
                r.ResearchRows,
                r.TotalSourceRows,
                r.HasHoldout ? "true" : "false",
                r.ResearchEndUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                r.ArtifactSha256);
        }
    }
}

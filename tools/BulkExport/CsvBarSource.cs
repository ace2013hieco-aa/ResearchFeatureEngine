using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// CSV-backed frozen-capture reader for the two registered source
    /// schemas. Fail-closed validation:
    ///  - exact header match (no unknown schema);
    ///  - exact field count per row;
    ///  - no comma, quote, CR, LF in any token (the artifact is
    ///    quote-free CSV; a capture containing such characters is
    ///    rejected, not escaped);
    ///  - timestamps parse as UTC and never regress;
    ///  - duplicates preserved and counted, never repaired.
    ///
    /// All reads use File.ReadLines — the file is never loaded into a
    /// single string or array of lines.
    /// </summary>
    public sealed class CsvBarSource : IBarSource
    {
        private readonly string _path;

        public CsvBarSource(string path)
        {
            _path = path;
        }

        public SourceSchemaKind Kind
        {
            get
            {
                using var reader = OpenReader();
                string? header = reader.ReadLine();
                if (header is null)
                {
                    throw new ExportException($"Capture is empty: {_path}");
                }

                SourceSchemaKind? kind = SourceSchema.Detect(header);
                if (kind is null)
                {
                    throw new ExportException(
                        "Unrecognized capture header (expected recorder_v1 "
                        + $"'{SourceSchema.RecorderHeader}', recorder_v1_1 "
                        + $"'{SourceSchema.RecorderV1_1Header}', or fixture_v1 "
                        + $"'{SourceSchema.FixtureHeader}'): {header}");
                }

                return kind.Value;
            }
        }

        public ScanResult Scan()
        {
            long rowCount = 0;
            long duplicates = 0;
            DateTime firstUtc = default;
            string firstToken = "";
            DateTime lastUtc = default;
            string lastToken = "";

            foreach (ParsedBar bar in Stream())
            {
                if (rowCount == 0)
                {
                    firstUtc = bar.Utc;
                    firstToken = bar.Tokens[0];
                }

                if (bar.Utc < lastUtc)
                {
                    throw new ExportException(
                        $"Timestamp regression at data row {rowCount}: "
                        + $"'{bar.Tokens[0]}' follows '{lastToken}'.");
                }

                if (rowCount > 0 && bar.Utc == lastUtc)
                {
                    duplicates++;
                }

                lastUtc = bar.Utc;
                lastToken = bar.Tokens[0];
                rowCount++;
            }

            if (rowCount == 0)
            {
                throw new ExportException($"Capture contains no data rows: {_path}");
            }

            return new ScanResult
            {
                Kind = Kind,
                RowCount = rowCount,
                FirstUtc = firstUtc,
                FirstToken = firstToken,
                LastUtc = lastUtc,
                LastToken = lastToken,
                DuplicateTimestampCount = duplicates
            };
        }

        public int CountBarsBefore(DateTime exclusiveUpperUtc)
        {
            int count = 0;
            foreach (ParsedBar bar in Stream())
            {
                if (bar.Utc >= exclusiveUpperUtc)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        public int CountBarsUpTo(DateTime inclusiveUpperUtc)
        {
            int count = 0;
            foreach (ParsedBar bar in Stream())
            {
                if (bar.Utc > inclusiveUpperUtc)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        public ArraysMarketData Materialize(int count)
        {
            var open = new double[count];
            var high = new double[count];
            var low = new double[count];
            var close = new double[count];
            var volume = new double[count];
            var time = new DateTime[count];

            int i = 0;
            foreach (ParsedBar bar in Stream())
            {
                if (i >= count)
                {
                    break;
                }

                open[i] = bar.Open;
                high[i] = bar.High;
                low[i] = bar.Low;
                close[i] = bar.Close;
                volume[i] = bar.Volume;
                time[i] = bar.Utc;
                i++;
            }

            if (i != count)
            {
                throw new ExportException(
                    $"Capture has {i} bars available but {count} were required.");
            }

            return new ArraysMarketData(open, high, low, close, volume, time);
        }

        public IEnumerable<ParsedBar> Stream()
        {
            using var reader = OpenReader();
            string? header = reader.ReadLine();
            if (header is null)
            {
                throw new ExportException($"Capture is empty: {_path}");
            }

            SourceSchemaKind? kind = SourceSchema.Detect(header);
            if (kind is null)
            {
                throw new ExportException(
                    "Unrecognized capture header (expected recorder_v1 "
                    + $"'{SourceSchema.RecorderHeader}', recorder_v1_1 "
                    + $"'{SourceSchema.RecorderV1_1Header}', or fixture_v1 "
                    + $"'{SourceSchema.FixtureHeader}'): {header}");
            }

            int fieldCount = SourceSchema.FieldCount(kind.Value);
            long rowNumber = 1; // data row ordinal for diagnostics
            string? line = reader.ReadLine();
            while (line is not null)
            {
                if (line.Length == 0)
                {
                    throw new ExportException(
                        $"Empty data row at data row {rowNumber}: {_path}");
                }

                string[] tokens = line.Split(',');

                if (tokens.Length != fieldCount)
                {
                    throw new ExportException(
                        $"Expected {fieldCount} comma-separated fields, got {tokens.Length} "
                        + $"at data row {rowNumber}: {_path}");
                }

                foreach (string token in tokens)
                {
                    if (token.Length == 0 || token.Contains(',') || token.Contains('"'))
                    {
                        throw new ExportException(
                            $"Empty or forbidden character (comma/quote) in field "
                            + $"at data row {rowNumber}: {_path}");
                    }
                }

                if (line.Contains('\r') || line.Contains('\n'))
                {
                    // Unreachable: File.ReadLines strips the terminators.
                    throw new ExportException(
                        $"Embedded line break at data row {rowNumber}: {_path}");
                }

                DateTime utc;
                try
                {
                    // DateTimeStyles.AssumeUniversal|AdjustToUniversal maps a
                    // trailing 'Z' (recorder) to UTC and a naive local-style
                    // token (fixture 'yyyy-MM-dd HH:mm:ss') to UTC-as-stated,
                    // never to the machine timezone.
                    utc = DateTime.Parse(
                        tokens[0],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                }
                catch (FormatException)
                {
                    throw new ExportException(
                        $"Unparseable timestamp '{tokens[0]}' at data row "
                        + $"{rowNumber}: {_path}");
                }

                double open, high, low, close, volume;
                try
                {
                    open = double.Parse(tokens[1], CultureInfo.InvariantCulture);
                    high = double.Parse(tokens[2], CultureInfo.InvariantCulture);
                    low = double.Parse(tokens[3], CultureInfo.InvariantCulture);
                    close = double.Parse(tokens[4], CultureInfo.InvariantCulture);
                    volume = double.Parse(tokens[5], CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    throw new ExportException(
                        $"Unparseable numeric field at data row {rowNumber}: " +
                        $"{_path}");
                }

                // recorder_v1_1 Spread: the recorder contract (BarRecord.cs
                // IsSpreadValid, commit 2795dd0) is finite and >= 0; zero is
                // VALID (spread unavailable — historical backfill; never
                // fabricated). Anything else fails closed: the source record
                // itself is malformed. Spread is provenance only; it never
                // reaches the engine (which consumes OHLCV only) and never
                // enters the measurement artifact.
                double spread = 0;
                if (kind.Value == SourceSchemaKind.RecorderV1_1)
                {
                    if (!double.TryParse(
                            tokens[6],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out spread)
                        || double.IsNaN(spread)
                        || double.IsInfinity(spread)
                        || spread < 0)
                    {
                        throw new ExportException(
                            $"Invalid Spread field '{tokens[6]}' at data row " +
                            $"{rowNumber} (recorder_v1_1 requires finite non-negative): {_path}");
                    }
                }

                yield return new ParsedBar
                {
                    Tokens = tokens,
                    Utc = utc,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                };

                rowNumber++;
                line = reader.ReadLine();
            }
        }

        private StreamReader OpenReader()
        {
            return new StreamReader(
                _path,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: false);
        }
    }
}

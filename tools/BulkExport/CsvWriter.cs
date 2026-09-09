using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// UTF-8 (no BOM) / LF CSV writer with incremental SHA-256 over
    /// the artifact bytes as they are written. Each row is assembled
    /// into a byte buffer, fed to the hash, then written to the
    /// stream — one code path, no flush state machine, no way to
    /// double-count bytes. No quoting, no locale, no wall clock.
    /// </summary>
    public sealed class CsvWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly SHA256 _sha;
        private readonly StringBuilder _rowBuilder = new StringBuilder(256);

        public CsvWriter(string path)
        {
            _stream = new FileStream(
                path,
                FileMode.CreateNew, // refuse overwrite: fail closed
                FileAccess.Write,
                FileShare.None);
            _sha = SHA256.Create();
        }

        public void WriteRow(string[] tokens)
        {
            _rowBuilder.Clear();

            for (int i = 0; i < tokens.Length; i++)
            {
                if (i > 0)
                {
                    _rowBuilder.Append(',');
                }

                _rowBuilder.Append(tokens[i]);
            }

            _rowBuilder.Append('\n');

            byte[] bytes = Encoding.UTF8.GetBytes(_rowBuilder.ToString());
            _sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            _stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Finalize the hash over everything written and return it as
        /// 64 lowercase hex chars. Must be called exactly once, after
        /// the last row.
        ///
        /// NOTE: on .NET 6, TransformFinalBlock's RETURN VALUE is the
        /// transformed output buffer — empty for a hash algorithm
        /// (proven empirically: length 0). The digest is exposed via
        /// the .Hash property after finalization. This was the source
        /// of a real bug caught by G11: manifests carried an empty
        /// artifact_sha256 while the artifact hashed correctly.
        /// </summary>
        public string HashHexFinal()
        {
            _sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return FormatHex(_sha.Hash ?? throw new ExportException("SHA-256 digest unavailable after finalization."));
        }

        private static string FormatHex(byte[] hash)
        {
            var sb = new StringBuilder(64);
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            _sha.Dispose();
            _stream.Dispose();
        }
    }
}

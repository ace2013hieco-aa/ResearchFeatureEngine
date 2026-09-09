using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// SHA-256 utilities for the frozen-input gate and the post-run
    /// source re-hash (M10.1 §18).
    /// </summary>
    public static class Hashing
    {
        /// <summary>
        /// Stream a file through SHA-256 without loading it into
        /// memory. Returns 64 lowercase hex chars.
        /// </summary>
        public static string Sha256File(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            return FormatHex(hash);
        }

        public static string FormatHex(byte[] hash)
        {
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}

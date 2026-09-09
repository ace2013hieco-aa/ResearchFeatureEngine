using System;

namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Deterministic, user-facing export failure. Carries a precise
    /// message describing the fail-closed condition that stopped the
    /// export. Program maps this to exit code 2.
    /// </summary>
    public sealed class ExportException : Exception
    {
        public ExportException(string message)
            : base(message)
        {
        }
    }
}

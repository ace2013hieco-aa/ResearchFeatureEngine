namespace ResearchFeatureEngine.BulkExport
{
    /// <summary>
    /// Export modes, mirroring Core.ReferenceType. The mode selects
    /// the composed engine configuration AND the column set.
    /// </summary>
    public enum ExportMode
    {
        /// <summary>ATRSmooth2 single reference (27 canonical columns).</summary>
        ATRSmooth2 = 0,

        /// <summary>Darvas Box reference (31 columns).</summary>
        DarvasBox = 1,

        /// <summary>HMA single reference (27 columns).</summary>
        Hma = 2,

        /// <summary>Composite HMA + ATRSmooth dual reference (34 columns).</summary>
        HmaAtrSmooth = 3
    }
}

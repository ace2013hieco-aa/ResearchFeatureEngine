using System;

namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Runtime values for the Darvas Box Closing Distance feature.
    ///
    /// Distinct from the general <see cref="DistanceRuntimeValues"/>
    /// (close vs. the scalar Reference.Price): this feature measures
    /// the close against the OUTER boundary of the current Darvas
    /// box on the close's side.
    /// </summary>
    public sealed class DarvasBoxDistanceRuntimeValues
    {
        /// <summary>
        /// Gets a value indicating whether a Darvas box was
        /// confirmed and applicable for the most recently processed
        /// bar. <c>false</c> during warm-up (before the first box
        /// confirmation) — the published distances are NaN in that
        /// case, preserving the distinction between "valid box +
        /// close inside → 0" and "no valid box yet → unavailable".
        /// </summary>
        public bool HasBox { get; internal set; }

        /// <summary>
        /// Gets the SIGNED closing distance for the most recently
        /// processed bar: <c>Close - Upper</c> when the close is
        /// above the box (positive), <c>Close - Lower</c> when below
        /// (negative), 0 when inside the box (exact boundary
        /// equality included). <see cref="double.NaN"/> while no
        /// valid box exists (warm-up) — the DEFAULT state of a
        /// freshly constructed values object, so pipelines without
        /// the Darvas stage never report a misleading 0.
        /// </summary>
        public double SignedClosingDistance { get; internal set; }
            = double.NaN;

        /// <summary>
        /// Gets the UNSIGNED closing distance: the absolute value of
        /// the signed distance — <c>Close - Upper</c> above the box,
        /// <c>Lower - Close</c> below the box, 0 inside.
        /// <see cref="double.NaN"/> while no valid box exists.
        /// </summary>
        public double AbsoluteClosingDistance { get; internal set; }
            = double.NaN;
    }
}

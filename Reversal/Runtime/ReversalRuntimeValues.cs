using System;
using ResearchFeatureEngine.Core;

namespace ResearchFeatureEngine.Reversal.Runtime
{
    /// <summary>
    /// Runtime values for the Reversal stage, describing the most
    /// recent ATRSmooth reversal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BarsSinceReversal"/> is the bar distance from the
    /// most recent reversal event (0 on the reversal bar, 1 on the
    /// next completed bar, and so on). It is <c>null</c> until the
    /// first reversal occurs.
    /// </para>
    /// <para>
    /// <see cref="Direction"/> is the direction of the most recent
    /// reversal and persists until another reversal occurs. It is
    /// <see cref="Core.ReversalDirection.None"/> until the first
    /// reversal occurs.
    /// </para>
    /// </remarks>
    public sealed class ReversalRuntimeValues
    {
        /// <summary>
        /// Gets the number of bars since the most recent ATRSmooth
        /// reversal, or <c>null</c> if no reversal has occurred yet.
        /// </summary>
        public int? BarsSinceReversal { get; internal set; }

        /// <summary>
        /// Gets the direction of the most recent ATRSmooth reversal,
        /// or <see cref="Core.ReversalDirection.None"/> if no
        /// reversal has occurred yet.
        /// </summary>
        public ReversalDirection Direction { get; internal set; }
            = ReversalDirection.None;

        /// <summary>
        /// Gets a value indicating whether the current bar is itself
        /// a reversal bar (the bar on which the most recent reversal
        /// occurred). This is a step function: <c>true</c> on the
        /// reversal bar, <c>false</c> on every continuation bar,
        /// useful for alerting/signal logic. <c>false</c> until the
        /// first reversal.
        /// </summary>
        public bool IsReversalBar { get; internal set; }
    }
}

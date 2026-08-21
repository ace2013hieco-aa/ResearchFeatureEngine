namespace ResearchFeatureEngine.Core
{
    /// <summary>
    /// Direction of the most recent ATRSmooth reversal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reversal is a transition in the close-to-reference relation:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Up"/>: the close moved from BELOW the ATRSmooth
    /// reference to ABOVE it (bullish).
    /// </description></item>
    /// <item><description>
    /// <see cref="Down"/>: the close moved from ABOVE the ATRSmooth
    /// reference to BELOW it (bearish).
    /// </description></item>
    /// <item><description>
    /// <see cref="None"/>: no reversal has occurred yet since the
    /// engine was initialized or reset.
    /// </description></item>
    /// </list>
    /// </remarks>
    public enum ReversalDirection
    {
        /// <summary>
        /// No reversal has occurred yet.
        /// </summary>
        None = 0,

        /// <summary>
        /// Below → Above (bullish) reversal.
        /// </summary>
        Up = 1,

        /// <summary>
        /// Above → Below (bearish) reversal.
        /// </summary>
        Down = -1
    }
}

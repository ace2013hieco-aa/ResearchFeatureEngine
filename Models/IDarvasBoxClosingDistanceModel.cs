namespace ResearchFeatureEngine.Models
{
    /// <summary>
    /// Defines the contract for the Darvas Box closing-distance
    /// research feature.
    ///
    /// A distance model of this contract computes the SIGNED
    /// displacement of a candle's closing price from the OUTER
    /// Darvas Box boundary on the side where the close is located:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// Close above the box → <c>Close - Upper</c> (positive).
    /// </description></item>
    /// <item><description>
    /// Close below the box → <c>Close - Lower</c> (negative).
    /// </description></item>
    /// <item><description>
    /// Close inside the box (including closing EXACTLY on either
    /// boundary) → <c>0</c>.
    /// </description></item>
    /// </list>
    ///
    /// The reference boundary is the outer box line on the close's
    /// side — NOT the box midpoint, NOT the opposite boundary, and
    /// NOT whichever boundary is numerically closest regardless of
    /// the close's position.
    /// </summary>
    public interface IDarvasBoxClosingDistanceModel
    {
        /// <summary>
        /// Computes the signed closing distance for one bar.
        /// </summary>
        /// <param name="close">Closing price of the bar.</param>
        /// <param name="upper">
        /// Upper Darvas Box boundary applicable to the bar.
        /// </param>
        /// <param name="lower">
        /// Lower Darvas Box boundary applicable to the bar.
        /// </param>
        /// <returns>
        /// The signed closing distance:
        /// positive when the close is above the box
        /// (<c>Close - Upper</c>), negative when below
        /// (<c>Close - Lower</c>), zero when inside the box
        /// (<c>Lower &lt;= Close &lt;= Upper</c>, exact boundary
        /// equality included).
        /// </returns>
        double Compute(double close, double upper, double lower);
    }
}

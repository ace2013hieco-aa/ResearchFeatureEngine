namespace ResearchFeatureEngine.Interfaces
{
    /// <summary>
    /// Represents a read-only indexed numerical series.
    /// Implementations may wrap price data, indicator values,
    /// calculated features, or any other ordered numeric sequence.
    /// </summary>
    public interface IPriceSeries
    {
        /// <summary>
        /// Gets the number of elements in the series.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        /// <returns>The value at the specified index.</returns>
        double this[int index] { get; }
    }
}

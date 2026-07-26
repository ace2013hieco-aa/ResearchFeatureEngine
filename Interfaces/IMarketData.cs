using System;
using System.Collections.Generic;

namespace ResearchFeatureEngine.Interfaces
{
    public interface IMarketData
    {
        IPriceSeries Open { get; }

        IPriceSeries High { get; }

        IPriceSeries Low { get; }

        IPriceSeries Close { get; }

        IPriceSeries Volume { get; }

        IReadOnlyList<DateTime> Time { get; }

        int Count { get; }

        /// <summary>
        /// Advances the enumerator to the next data point.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator was successfully advanced
        /// to the next data point; <c>false</c> if the enumerator
        /// has passed the end of the collection.
        /// </returns>
        bool MoveNext();
    }
}

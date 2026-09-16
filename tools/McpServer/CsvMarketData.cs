using System;
using System.Collections.Generic;
using ResearchFeatureEngine.Interfaces;

namespace ResearchFeatureEngine.McpServer;

/// <summary>
/// Indexed market-data adapter over materialized arrays (mirrors
/// tools/BulkExport/ArraysMarketData: the engine reads by index).
/// </summary>
public sealed class CsvMarketData : IMarketData
{
    private readonly double[] _open;
    private readonly double[] _high;
    private readonly double[] _low;
    private readonly double[] _close;
    private readonly double[] _volume;
    private readonly DateTime[] _time;

    public CsvMarketData(
        double[] open, double[] high, double[] low,
        double[] close, double[] volume, DateTime[] time)
    {
        _open = open;
        _high = high;
        _low = low;
        _close = close;
        _volume = volume;
        _time = time;
    }

    public int Count => _close.Length;
    public IPriceSeries Open => new ArraySeries(_open);
    public IPriceSeries High => new ArraySeries(_high);
    public IPriceSeries Low => new ArraySeries(_low);
    public IPriceSeries Close => new ArraySeries(_close);
    public IPriceSeries Volume => new ArraySeries(_volume);
    public IReadOnlyList<DateTime> Time => _time;
    public bool MoveNext() => false;

    private sealed class ArraySeries : IPriceSeries
    {
        private readonly double[] _values;
        public ArraySeries(double[] values) { _values = values; }
        public int Count => _values.Length;
        public double this[int index] => _values[index];
    }
}

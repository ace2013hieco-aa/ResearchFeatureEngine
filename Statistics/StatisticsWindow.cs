using System;

namespace ResearchFeatureEngine.Statistics
{
    /// <summary>
    /// Maintains a rolling window of recent market data values
    /// for statistical computation.
    /// </summary>
    public sealed class StatisticsWindow
    {
        private readonly double[] _buffer;
        private int _count;
        private int _head;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatisticsWindow"/> class.
        /// </summary>
        /// <param name="capacity">
        /// Maximum number of values to retain in the window.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when capacity is less than 1.
        /// </exception>
        public StatisticsWindow(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity),
                    "Window capacity must be at least 1.");

            _buffer = new double[capacity];
            _count = 0;
            _head = 0;
        }

        /// <summary>
        /// Gets the number of values currently in the window.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the maximum capacity of the window.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Adds a value to the window.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(double value)
        {
            _buffer[_head] = value;
            _head = (_head + 1) % _buffer.Length;

            if (_count < _buffer.Length)
                _count++;
        }

        /// <summary>
        /// Returns a span of the current values in insertion order
        /// (oldest to newest).
        /// </summary>
        /// <returns>
        /// A span containing the current window values.
        /// </returns>
        public ReadOnlySpan<double> AsSpan()
        {
            // If not yet full, values are at the start of the buffer.
            if (_count < _buffer.Length)
            {
                return new ReadOnlySpan<double>(_buffer, 0, _count);
            }

            // Full buffer: head points to the oldest value.
            // Copy to maintain chronological order.
            double[] ordered = new double[_buffer.Length];
            int tail = _head;
            for (int i = 0; i < _buffer.Length; i++)
            {
                ordered[i] = _buffer[tail];
                tail = (tail + 1) % _buffer.Length;
            }

            return new ReadOnlySpan<double>(ordered);
        }

        /// <summary>
        /// Returns a copy of the current values in insertion order
        /// (oldest to newest).
        /// </summary>
        public double[] GetOrderedArray()
        {
            if (_count < _buffer.Length)
            {
                var copy = new double[_count];
                Array.Copy(_buffer, 0, copy, 0, _count);
                return copy;
            }

            double[] ordered = new double[_buffer.Length];
            int tail = _head;
            for (int i = 0; i < _buffer.Length; i++)
            {
                ordered[i] = _buffer[tail];
                tail = (tail + 1) % _buffer.Length;
            }
            return ordered;
        }

        /// <summary>
        /// Resets the window, clearing all values.
        /// </summary>
        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _count = 0;
            _head = 0;
        }
    }
}

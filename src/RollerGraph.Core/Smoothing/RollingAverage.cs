namespace RollerGraph.Core.Smoothing;

/// <summary>
/// Simple fixed-window rolling average. Thread-affine; not safe for concurrent use.
/// </summary>
public sealed class RollingAverage
{
    private readonly double[] _buffer;
    private int _count;
    private int _head;
    private double _sum;

    /// <summary>
    /// Creates a rolling average with the given window size.
    /// Window of 1 effectively disables smoothing (output == input).
    /// </summary>
    public RollingAverage(int windowSize)
    {
        if (windowSize < 1)
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be >= 1.");

        _buffer = new double[windowSize];
    }

    /// <summary>Number of samples currently in the window (0..WindowSize).</summary>
    public int Count => _count;

    /// <summary>Configured window size.</summary>
    public int WindowSize => _buffer.Length;

    /// <summary>
    /// Adds a new value and returns the current rolling average.
    /// While the window is not full, returns the average of values seen so far.
    /// </summary>
    public double Push(double value)
    {
        if (_count < _buffer.Length)
        {
            _buffer[_head] = value;
            _sum += value;
            _count++;
        }
        else
        {
            _sum -= _buffer[_head];
            _buffer[_head] = value;
            _sum += value;
        }

        _head = (_head + 1) % _buffer.Length;
        return _sum / _count;
    }

    /// <summary>Resets the rolling window to empty.</summary>
    public void Reset()
    {
        Array.Clear(_buffer);
        _count = 0;
        _head = 0;
        _sum = 0;
    }
}

namespace RollerGraph.Core.Smoothing;

/// <summary>
/// Fixed-window rolling maximum. Thread-affine; not safe for concurrent use.
/// </summary>
public sealed class RollingMaximum
{
    private readonly double[] _buffer;
    private int _count;
    private int _head;

    public RollingMaximum(int windowSize)
    {
        if (windowSize < 1)
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be >= 1.");

        _buffer = new double[windowSize];
    }

    /// <summary>Number of samples currently in the window (0..WindowSize).</summary>
    public int Count => _count;

    /// <summary>Configured window size.</summary>
    public int WindowSize => _buffer.Length;

    /// <summary>Adds a value and returns the highest value in the current window.</summary>
    public double Push(double value)
    {
        _buffer[_head] = value;
        if (_count < _buffer.Length)
            _count++;

        _head = (_head + 1) % _buffer.Length;

        var max = _buffer[0];
        for (var i = 1; i < _count; i++)
        {
            if (_buffer[i] > max)
                max = _buffer[i];
        }

        return max;
    }

    /// <summary>Resets the rolling window to empty.</summary>
    public void Reset()
    {
        Array.Clear(_buffer);
        _count = 0;
        _head = 0;
    }
}

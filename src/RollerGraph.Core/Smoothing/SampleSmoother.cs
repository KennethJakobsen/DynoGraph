using RollerGraph.Core.Models;

namespace RollerGraph.Core.Smoothing;

/// <summary>
/// Applies peak-preserving smoothing to a stream of samples.
/// Designed to be unit-testable in isolation from the UI.
/// </summary>
public sealed class SampleSmoother
{
    private readonly RollingMaximum _hp;
    private readonly RollingMaximum _nm;
    private readonly RollingAverage _speed;

    public SampleSmoother(int windowSize)
    {
        _hp = new RollingMaximum(windowSize);
        _nm = new RollingMaximum(windowSize);
        _speed = new RollingAverage(windowSize);
        WindowSize = windowSize;
    }

    public int WindowSize { get; }

    /// <summary>
    /// Smooths the sample. When <see cref="WindowSize"/> &lt;= 1, returns it unchanged.
    /// </summary>
    public Sample Smooth(Sample sample)
    {
        if (WindowSize <= 1)
            return sample;

        return sample with
        {
            Hp = _hp.Push(sample.Hp),
            Nm = _nm.Push(sample.Nm),
            SpeedKmh = _speed.Push(sample.SpeedKmh),
        };
    }

    public void Reset()
    {
        _hp.Reset();
        _nm.Reset();
        _speed.Reset();
    }
}

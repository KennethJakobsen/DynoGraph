using RollerGraph.Core.Serial;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>
/// Fake <see cref="ISerialSource"/> whose events are driven manually from
/// the test body. Tracks how many times Start/Stop/Dispose have been called.
/// </summary>
internal sealed class FakeSerialSource : ISerialSource
{
    public event EventHandler<LineReceivedEventArgs>? LineReceived;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsRunning { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public int DisposeCount { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        StartCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
        StopCount++;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposeCount++;
        IsRunning = false;
    }

    /// <summary>Test helper: simulate a line arriving on the wire.</summary>
    public void EmitLine(string line, DateTime? at = null) =>
        LineReceived?.Invoke(this, new LineReceivedEventArgs(line, at ?? DateTime.UtcNow));

    /// <summary>Test helper: simulate the source raising an error.</summary>
    public void EmitError(Exception ex) =>
        ErrorOccurred?.Invoke(this, ex);
}

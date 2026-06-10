namespace RollerGraph.Core.Serial;

/// <summary>
/// Abstraction over a line-producing data source (real serial port or CSV replay).
/// Implementations raise <see cref="LineReceived"/> for each complete line on a background thread.
/// </summary>
public interface ISerialSource : IDisposable
{
    /// <summary>Raised when a complete line has been received.</summary>
    event EventHandler<LineReceivedEventArgs>? LineReceived;

    /// <summary>Raised when the source has unexpectedly disconnected (e.g. cable unplugged).</summary>
    event EventHandler<Exception>? ErrorOccurred;

    /// <summary>True while the source is actively producing lines.</summary>
    bool IsRunning { get; }

    /// <summary>Starts producing lines asynchronously.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops producing lines and releases resources.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

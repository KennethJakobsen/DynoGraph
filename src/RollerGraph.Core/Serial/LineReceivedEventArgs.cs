namespace RollerGraph.Core.Serial;

/// <summary>
/// Event payload raised when a complete line has been received from the source.
/// </summary>
public sealed class LineReceivedEventArgs : EventArgs
{
    public LineReceivedEventArgs(string line, DateTime receivedAt)
    {
        Line = line;
        ReceivedAt = receivedAt;
    }

    /// <summary>The raw line (excluding any trailing newline characters).</summary>
    public string Line { get; }

    /// <summary>UTC timestamp the line was received / produced.</summary>
    public DateTime ReceivedAt { get; }
}

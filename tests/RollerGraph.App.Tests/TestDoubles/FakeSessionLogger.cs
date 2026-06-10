using RollerGraph.Core.Logging;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>In-memory <see cref="ISessionLogger"/> for tests.</summary>
internal sealed class FakeSessionLogger : ISessionLogger
{
    private readonly List<(DateTime, string)> _lines = new();
    public string? CurrentFilePath { get; private set; }
    public bool IsActive => CurrentFilePath is not null;
    public int SessionsStarted { get; private set; }
    public int SessionsEnded { get; private set; }
    public IReadOnlyList<(DateTime At, string Line)> AppendedLines => _lines;

    public string BeginSession(DateTime? utcNow = null)
    {
        SessionsStarted++;
        CurrentFilePath = $"session-{SessionsStarted}.csv";
        return CurrentFilePath;
    }

    public void Append(string rawLine, DateTime receivedAtUtc)
    {
        if (!IsActive) return;
        _lines.Add((receivedAtUtc, rawLine));
    }

    public void Flush() { }

    public void EndSession()
    {
        if (!IsActive) return;
        SessionsEnded++;
        CurrentFilePath = null;
    }

    public void Dispose() => EndSession();
}

namespace RollerGraph.Core.Logging;

/// <summary>
/// Abstraction over a per-session raw-line logger. Implementations decide
/// where the data goes (CSV file on disk, in-memory buffer, no-op, etc.).
/// </summary>
public interface ISessionLogger : IDisposable
{
    /// <summary>Path of the current session, or null if no session is open.</summary>
    string? CurrentFilePath { get; }

    /// <summary>True while a session is open and accepting <see cref="Append"/>.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Closes any open session and starts a new one. Returns an identifier
    /// (typically a file path) for the new session that the UI can display.
    /// </summary>
    string BeginSession(DateTime? utcNow = null);

    /// <summary>Appends one raw line to the current session. No-op when inactive.</summary>
    void Append(string rawLine, DateTime receivedAtUtc);

    /// <summary>Flushes buffered data so it survives a crash.</summary>
    void Flush();

    /// <summary>Closes the current session (if any).</summary>
    void EndSession();
}

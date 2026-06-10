using System.Globalization;
using System.Text;

namespace RollerGraph.Core.Logging;

/// <summary>
/// Writes incoming raw lines plus their UTC timestamps to a CSV file.
/// Each session creates a new file in the form:
///   {root}/session-yyyyMMdd-HHmmss.csv
/// </summary>
public sealed class CsvSessionLogger : IDisposable
{
    private const string Header = "timestamp_utc,raw_line";

    private readonly object _lock = new();
    private readonly string _root;
    private StreamWriter? _writer;

    /// <summary>Path to the currently open file, or null when not logging.</summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>True while a file is open.</summary>
    public bool IsActive => _writer is not null;

    /// <summary>
    /// Creates a logger that places files under <paramref name="rootDirectory"/>.
    /// The directory is created on first use.
    /// </summary>
    public CsvSessionLogger(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        _root = rootDirectory;
    }

    /// <summary>
    /// Returns the OS-appropriate default log directory for the app.
    /// </summary>
    public static string DefaultLogDirectory()
    {
        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.Combine(appData, "RollerGraph", "logs");
    }

    /// <summary>
    /// Closes any current file and opens a new one stamped with the current UTC time.
    /// </summary>
    /// <returns>The path of the newly opened file.</returns>
    public string BeginSession(DateTime? utcNow = null)
    {
        lock (_lock)
        {
            CloseInternal();
            Directory.CreateDirectory(_root);
            var stamp = (utcNow ?? DateTime.UtcNow).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(_root, $"session-{stamp}.csv");
            // Disambiguate if a file already exists (e.g. rapid reset within one second).
            var n = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(_root, $"session-{stamp}-{n}.csv");
                n++;
            }
            _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), Encoding.UTF8);
            _writer.WriteLine(Header);
            _writer.Flush();
            CurrentFilePath = path;
            return path;
        }
    }

    /// <summary>
    /// Appends a raw line + its timestamp to the current session file.
    /// No-op when no session is active.
    /// </summary>
    public void Append(string rawLine, DateTime receivedAtUtc)
    {
        if (rawLine is null) return;
        lock (_lock)
        {
            if (_writer is null) return;
            _writer.Write(receivedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            _writer.Write(',');
            _writer.WriteLine(Escape(rawLine));
        }
    }

    /// <summary>Flushes buffered data to disk.</summary>
    public void Flush()
    {
        lock (_lock)
        {
            _writer?.Flush();
        }
    }

    /// <summary>Closes the current session file (if any).</summary>
    public void EndSession()
    {
        lock (_lock)
        {
            CloseInternal();
        }
    }

    private void CloseInternal()
    {
        if (_writer is null) return;
        try { _writer.Flush(); } catch { /* ignore */ }
        try { _writer.Dispose(); } catch { /* ignore */ }
        _writer = null;
        CurrentFilePath = null;
    }

    private static string Escape(string line)
    {
        // Wrap in quotes if the line contains comma/quote/newline; double-up internal quotes.
        if (line.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            return line;
        return "\"" + line.Replace("\"", "\"\"") + "\"";
    }

    public void Dispose() => EndSession();
}

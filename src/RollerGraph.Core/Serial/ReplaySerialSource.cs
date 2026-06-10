using System.Globalization;

namespace RollerGraph.Core.Serial;

/// <summary>
/// <see cref="ISerialSource"/> that replays lines from a CSV file at a configurable rate.
/// Useful for development and offline review without dyno hardware.
/// </summary>
public sealed class ReplaySerialSource : ISerialSource
{
    private readonly IReadOnlyList<string> _lines;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _replayTask;
    private readonly object _lock = new();
    private bool _running;

    /// <summary>
    /// Creates a replay source from a list of raw lines.
    /// </summary>
    /// <param name="lines">CSV lines to replay in order.</param>
    /// <param name="interval">Delay between line emissions. Defaults to 100 ms (10 Hz).</param>
    public ReplaySerialSource(IEnumerable<string> lines, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _lines = lines.ToArray();
        _interval = interval ?? TimeSpan.FromMilliseconds(100);
    }

    public event EventHandler<LineReceivedEventArgs>? LineReceived;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsRunning
    {
        get { lock (_lock) return _running; }
    }

    /// <summary>
    /// Loads a CSV file from disk, stripping commented (#) and blank lines.
    /// </summary>
    public static ReplaySerialSource FromFile(string path, TimeSpan? interval = null)
    {
        var raw = File.ReadAllLines(path);
        var lines = raw
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();
        return new ReplaySerialSource(lines, interval);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_running)
                return Task.CompletedTask;
            _running = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _replayTask = Task.Run(() => ReplayLoopAsync(_cts.Token), _cts.Token);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_lock)
        {
            cts = _cts;
            task = _replayTask;
            _cts = null;
            _replayTask = null;
            _running = false;
        }

        if (cts is not null)
        {
            try { cts.Cancel(); } catch { /* ignore */ }
        }
        if (task is not null)
        {
            try { await task.WaitAsync(cancellationToken).ConfigureAwait(false); } catch { /* ignore */ }
        }
        cts?.Dispose();
    }

    private async Task ReplayLoopAsync(CancellationToken token)
    {
        try
        {
            foreach (var line in _lines)
            {
                token.ThrowIfCancellationRequested();
                LineReceived?.Invoke(this, new LineReceivedEventArgs(line, DateTime.UtcNow));
                if (_interval > TimeSpan.Zero)
                    await Task.Delay(_interval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            lock (_lock) _running = false;
        }
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }
    }

    /// <summary>
    /// Test helper: parse the sample-number delta between two CSV lines.
    /// Returns null if either line cannot be parsed.
    /// </summary>
    public static int? SampleDelta(string previous, string current)
    {
        var p = ExtractSample(previous);
        var c = ExtractSample(current);
        if (p is null || c is null) return null;
        return c.Value - p.Value;
    }

    private static int? ExtractSample(string line)
    {
        var comma = line.IndexOf(',');
        if (comma < 0) return null;
        return int.TryParse(line.AsSpan(0, comma).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

using RollerGraph.App.Services;
using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Logging;
using RollerGraph.Core.Models;
using RollerGraph.Core.Pipeline;
using RollerGraph.Core.Serial;

namespace RollerGraph.App.Connection;

/// <summary>
/// Event payload describing an accepted sample, already adjusted/smoothed.
/// </summary>
public sealed class SampleAcceptedEventArgs : EventArgs
{
    public Sample Sample { get; }
    public Sample MeasurementSample { get; }

    public SampleAcceptedEventArgs(Sample sample, Sample measurementSample)
    {
        Sample = sample;
        MeasurementSample = measurementSample;
    }
}

/// <summary>
/// Event payload describing a serial-side error.
/// </summary>
public sealed class ConnectionErrorEventArgs : EventArgs
{
    public Exception Error { get; }
    public ConnectionErrorEventArgs(Exception error) { Error = error; }
}

/// <summary>
/// Owns the live <see cref="ISerialSource"/>, the per-session log file, and
/// the <see cref="SamplePipeline"/>. Exposes start / stop / replay operations
/// and raises events when samples are accepted, lines are dropped, or the
/// source fails. All UI-thread marshalling is funnelled through an
/// <see cref="IUiDispatcher"/> so subscribers can update view-model state
/// safely.
///
/// Single responsibility: manage the lifecycle of one serial connection or
/// replay session at a time, and feed its samples through the pipeline.
/// </summary>
public sealed class ConnectionController : IDisposable
{
    private enum ActiveSourceKind
    {
        None,
        Live,
        Replay,
    }

    private readonly record struct ActiveSource(
        ActiveSourceKind Kind,
        string? PortName = null,
        string? ReplayPath = null,
        TimeSpan? ReplayInterval = null);

    private readonly IUiDispatcher _dispatcher;
    private readonly ISerialSourceFactory _sourceFactory;
    private readonly ISessionLogger _logger;
    private ISerialSource? _source;
    private SamplePipeline? _pipeline;
    private ActiveSource _activeSource;
    private bool _hasMeasurementSpeed;
    private bool _isRunStopped;
    private double _lastMeasurementSpeed;

    public ConnectionController(
        IUiDispatcher dispatcher,
        ISerialSourceFactory sourceFactory,
        ISessionLogger logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _sourceFactory = sourceFactory;
        _logger = logger;
    }

    /// <summary>Fired on the UI thread when a sample passes the full pipeline.</summary>
    public event EventHandler<SampleAcceptedEventArgs>? SampleAccepted;

    /// <summary>Fired on the UI thread when a raw line could not be parsed.</summary>
    public event EventHandler? BadLineReceived;

    /// <summary>Fired on the UI thread when the underlying source raises an error.</summary>
    public event EventHandler<ConnectionErrorEventArgs>? ErrorOccurred;

    /// <summary>Fired on the UI thread when a speed drop closes the current run.</summary>
    public event EventHandler? RunStopped;

    /// <summary>Fired on the UI thread before the first sample of an auto-detected new run.</summary>
    public event EventHandler? RunStarted;

    /// <summary>True when a source is active (live or replay).</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Current session log file path, or null when no session is open.</summary>
    public string? LogFilePath { get; private set; }

    /// <summary>True when the pipeline should apply peak-preserving smoothing.</summary>
    public bool SmoothingEnabled
    {
        get => _pipeline?.SmoothingEnabled ?? false;
        set
        {
            if (_pipeline is not null)
            {
                _pipeline.SmoothingEnabled = value;
                _pipeline.ResetSmoother();
            }
        }
    }

    /// <summary>Opens a live serial port and starts streaming samples.</summary>
    public async Task<string> ConnectAsync(string portName, Settings settings, bool smoothingEnabled)
    {
        if (IsConnected) return "Already connected";
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name is required.", nameof(portName));

        var src = _sourceFactory.CreateForPort(portName, settings.BaudRate);
        AttachSource(src, settings, smoothingEnabled);
        await src.StartAsync();
        LogFilePath = _logger.BeginSession();
        IsConnected = true;
        RememberLiveSource(portName);
        return $"Connected to {portName} @ {settings.BaudRate} baud";
    }

    /// <summary>Starts replaying a CSV file through the pipeline.</summary>
    public async Task<string> ReplayAsync(string filePath, Settings settings, bool smoothingEnabled, TimeSpan? interval = null)
    {
        if (IsConnected)
            await DisconnectAsync();

        var src = _sourceFactory.CreateFromCsvFile(filePath, interval);
        AttachSource(src, settings, smoothingEnabled);
        await src.StartAsync();
        // Replay also gets a log file so the parsed stream is captured.
        LogFilePath = _logger.BeginSession();
        IsConnected = true;
        RememberReplaySource(filePath, interval);
        return $"Replaying {Path.GetFileName(filePath)}";
    }

    /// <summary>Stops the current source and closes the log file.</summary>
    public Task DisconnectAsync() => DisconnectAsync(clearActiveSource: true);

    private async Task DisconnectAsync(bool clearActiveSource)
    {
        if (_source is null)
        {
            LogFilePath = null;
            IsConnected = false;
            ResetRunTracking();
            if (clearActiveSource) ClearActiveSource();
            return;
        }
        try { await _source.StopAsync(); }
        finally
        {
            DetachSource();
            _logger.EndSession();
            LogFilePath = null;
            IsConnected = false;
            ResetRunTracking();
            if (clearActiveSource) ClearActiveSource();
        }
    }

    /// <summary>
    /// Restarts the active live/replay source and opens a fresh log session.
    /// This gives Reset the same serial-port boundary as a manual
    /// disconnect/connect without losing which source mode is active.
    /// </summary>
    public async Task<string> RestartAsync(Settings settings, bool smoothingEnabled)
    {
        if (!IsConnected)
            return "Disconnected";

        var activeSource = _activeSource;

        await DisconnectAsync(clearActiveSource: false);

        return activeSource.Kind switch
        {
            ActiveSourceKind.Live when !string.IsNullOrWhiteSpace(activeSource.PortName) =>
                await ConnectAsync(activeSource.PortName, settings, smoothingEnabled),
            ActiveSourceKind.Replay when !string.IsNullOrWhiteSpace(activeSource.ReplayPath) =>
                await ReplayAsync(activeSource.ReplayPath, settings, smoothingEnabled, activeSource.ReplayInterval),
            _ => "Disconnected",
        };
    }

    /// <summary>
    /// Closes and reopens the log file without touching the source. Safe to
    /// call when no session is active.
    /// </summary>
    public void StartNewLogSession()
    {
        if (!IsConnected) return;
        _logger.EndSession();
        LogFilePath = _logger.BeginSession();
    }

    /// <summary>Clears the smoothing window without disrupting the source.</summary>
    public void ResetSmoother() => _pipeline?.ResetSmoother();

    /// <summary>
    /// Rebuilds the internal pipeline from new settings + adjuster. Call from
    /// the view-model after a settings change so the next sample uses the
    /// new MinSpeed/SmoothingWindow values.
    /// </summary>
    public void ApplySettings(Settings settings, SampleAdjuster adjuster, bool smoothingEnabled)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(adjuster);
        _pipeline = new SamplePipeline(settings, adjuster) { SmoothingEnabled = smoothingEnabled };
    }

    private void AttachSource(ISerialSource source, Settings settings, bool smoothingEnabled)
    {
        DetachSource();
        // Always start with a fresh smoother for a new connection/replay.
        var adjuster = SampleAdjuster.FromSettingsOrIdentity(settings);
        _pipeline = new SamplePipeline(settings, adjuster) { SmoothingEnabled = smoothingEnabled };
        ResetRunTracking();
        _source = source;
        source.LineReceived += OnLineReceived;
        source.ErrorOccurred += OnErrorOccurred;
    }

    private void DetachSource()
    {
        if (_source is null) return;
        _source.LineReceived -= OnLineReceived;
        _source.ErrorOccurred -= OnErrorOccurred;
        try { _source.Dispose(); } catch { /* ignore */ }
        _source = null;
    }

    private void OnLineReceived(object? sender, LineReceivedEventArgs e)
    {
        if (_pipeline is null) return;
        var result = _pipeline.Process(e.Line, e.ReceivedAt);
        switch (result.Outcome)
        {
            case SamplePipelineOutcome.Accepted:
                var sample = result.Sample!.Value;
                var measurementSample = result.MeasurementSample!.Value;
                if (HandleAcceptedMeasurement(measurementSample))
                    return;

                _logger.Append(e.Line, e.ReceivedAt);
                _dispatcher.Post(() => SampleAccepted?.Invoke(this, new SampleAcceptedEventArgs(sample, measurementSample)));
                break;
            case SamplePipelineOutcome.BadLine:
                if (_isRunStopped) return;
                _logger.Append(e.Line, e.ReceivedAt);
                _dispatcher.Post(() => BadLineReceived?.Invoke(this, EventArgs.Empty));
                break;
            case SamplePipelineOutcome.FilteredOut:
                if (!_isRunStopped)
                    _logger.Append(e.Line, e.ReceivedAt);
                // Silent drop - intentional.
                break;
        }
    }

    private bool HandleAcceptedMeasurement(Sample measurementSample)
    {
        if (!_hasMeasurementSpeed)
        {
            _hasMeasurementSpeed = true;
            _lastMeasurementSpeed = measurementSample.SpeedKmh;
            return false;
        }

        if (_isRunStopped)
        {
            if (measurementSample.SpeedKmh <= _lastMeasurementSpeed)
            {
                _lastMeasurementSpeed = measurementSample.SpeedKmh;
                return true;
            }

            _isRunStopped = false;
            _lastMeasurementSpeed = measurementSample.SpeedKmh;
            _pipeline?.ResetSmoother();
            LogFilePath = _logger.BeginSession();
            _dispatcher.Post(() => RunStarted?.Invoke(this, EventArgs.Empty));
            return false;
        }

        if (measurementSample.SpeedKmh < _lastMeasurementSpeed)
        {
            _isRunStopped = true;
            _lastMeasurementSpeed = measurementSample.SpeedKmh;
            _logger.EndSession();
            LogFilePath = null;
            _dispatcher.Post(() => RunStopped?.Invoke(this, EventArgs.Empty));
            return true;
        }

        _lastMeasurementSpeed = measurementSample.SpeedKmh;
        return false;
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        _dispatcher.Post(() =>
        {
            DetachSource();
            _logger.EndSession();
            LogFilePath = null;
            IsConnected = false;
            ResetRunTracking();
            ClearActiveSource();
            ErrorOccurred?.Invoke(this, new ConnectionErrorEventArgs(ex));
        });
    }

    private void ResetRunTracking()
    {
        _hasMeasurementSpeed = false;
        _isRunStopped = false;
        _lastMeasurementSpeed = 0;
    }

    private void RememberLiveSource(string portName)
    {
        _activeSource = new ActiveSource(ActiveSourceKind.Live, PortName: portName);
    }

    private void RememberReplaySource(string filePath, TimeSpan? interval)
    {
        _activeSource = new ActiveSource(ActiveSourceKind.Replay, ReplayPath: filePath, ReplayInterval: interval);
    }

    private void ClearActiveSource()
    {
        _activeSource = default;
    }

    public void Dispose()
    {
        try { DisconnectAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }
    }
}

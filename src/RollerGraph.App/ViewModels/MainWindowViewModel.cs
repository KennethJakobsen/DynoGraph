using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RollerGraph.App.Services;
using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Logging;
using RollerGraph.Core.Models;
using RollerGraph.Core.Parsing;
using RollerGraph.Core.Serial;
using RollerGraph.Core.Smoothing;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Top-level view-model for the main window. Owns the serial source,
/// the CSV logger, and the chart view-model. Coordinates the connect /
/// disconnect / reset / replay lifecycle.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IUiDispatcher _dispatcher;
    private readonly CsvSessionLogger _logger;
    private readonly SettingsStore? _settingsStore;
    private ISerialSource? _source;
    private SampleSmoother? _smoother;
    private SampleAdjuster _adjuster;

    public MainWindowViewModel(IUiDispatcher dispatcher, Settings? settings = null, SettingsStore? settingsStore = null)
    {
        _dispatcher = dispatcher;
        _settingsStore = settingsStore;
        Settings = settings ?? settingsStore?.Load() ?? new Settings();
        Chart = new ChartViewModel(Settings);
        _logger = new CsvSessionLogger(CsvSessionLogger.DefaultLogDirectory());
        _adjuster = BuildAdjuster(Settings);
        AvailablePorts = new ObservableCollection<string>();
        RefreshPorts();
        if (!string.IsNullOrWhiteSpace(Settings.LastPortName) && AvailablePorts.Contains(Settings.LastPortName))
            SelectedPort = Settings.LastPortName;
        else if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    public Settings Settings { get; private set; }
    public ChartViewModel Chart { get; }

    public ObservableCollection<string> AvailablePorts { get; }

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    [ObservableProperty]
    private int _badLineCount;

    [ObservableProperty]
    private bool _smoothingEnabled;

    [ObservableProperty]
    private string? _logFilePath;

    [RelayCommand]
    private void RefreshPorts()
    {
        var ports = RjcpSerialSource.EnumeratePorts();
        AvailablePorts.Clear();
        foreach (var p in ports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            AvailablePorts.Add(p);
        if (SelectedPort is not null && !AvailablePorts.Contains(SelectedPort))
            SelectedPort = AvailablePorts.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (IsConnected || string.IsNullOrWhiteSpace(SelectedPort))
            return;

        try
        {
            var src = new RjcpSerialSource(SelectedPort, Settings.BaudRate);
            AttachSource(src);
            await src.StartAsync();
            LogFilePath = _logger.BeginSession();
            IsConnected = true;
            StatusMessage = $"Connected to {SelectedPort} @ {Settings.BaudRate} baud";
            ResetSmoother();
            PersistLastPort(SelectedPort);
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connect failed: {ex.Message}";
            DetachSource();
        }
    }

    private bool CanConnect() => !IsConnected && !string.IsNullOrWhiteSpace(SelectedPort);

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        if (_source is null)
            return;
        try
        {
            await _source.StopAsync();
        }
        finally
        {
            DetachSource();
            _logger.EndSession();
            LogFilePath = null;
            IsConnected = false;
            StatusMessage = "Disconnected";
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDisconnect() => IsConnected;

    [RelayCommand]
    private void Reset()
    {
        Chart.Reset();
        BadLineCount = 0;
        ResetSmoother();
        // Start a new log file if currently connected.
        if (IsConnected)
        {
            _logger.EndSession();
            LogFilePath = _logger.BeginSession();
        }
    }

    /// <summary>
    /// Starts replay of a CSV file. Disconnects any current source first.
    /// </summary>
    public async Task ReplayAsync(string filePath, TimeSpan? interval = null)
    {
        if (IsConnected)
            await DisconnectCommand.ExecuteAsync(null);

        try
        {
            var src = ReplaySerialSource.FromFile(filePath, interval);
            AttachSource(src);
            await src.StartAsync();
            // Replay also gets a log file so the parsed stream is captured.
            LogFilePath = _logger.BeginSession();
            IsConnected = true;
            StatusMessage = $"Replaying {Path.GetFileName(filePath)}";
            ResetSmoother();
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Replay failed: {ex.Message}";
            DetachSource();
        }
    }

    /// <summary>
    /// Replaces the active settings, persists them (if a store is attached),
    /// applies new axis defaults to the chart (only when no data is plotted yet),
    /// and rebuilds the smoother with the new window size.
    /// </summary>
    public void ApplySettings(Settings newSettings)
    {
        ArgumentNullException.ThrowIfNull(newSettings);
        // Preserve the last-port selection (so changing axes doesn't lose it).
        var withPort = newSettings with { LastPortName = SelectedPort ?? newSettings.LastPortName };
        Settings = withPort;
        Chart.UpdateDefaults(withPort);
        _adjuster = BuildAdjuster(withPort);
        ResetSmoother();
        try { _settingsStore?.Save(withPort); } catch { /* surface in status later if needed */ }
    }

    private static SampleAdjuster BuildAdjuster(Settings s)
    {
        try
        {
            return new SampleAdjuster(s.SpeedAdjustment, s.NmAdjustment, s.HpAdjustment);
        }
        catch (ExpressionException)
        {
            // Bad expression in saved settings - fall back to identity rather than crashing.
            return SampleAdjuster.Identity;
        }
    }

    private void AttachSource(ISerialSource source)
    {
        DetachSource();
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
        // Log raw line on the background thread (logger is thread-safe).
        _logger.Append(e.Line, e.ReceivedAt);

        var sample = CsvLineParser.Parse(e.Line, e.ReceivedAt);
        if (sample is null)
        {
            _dispatcher.Post(() => BadLineCount++);
            return;
        }

        // Apply per-channel adjustments (factor/offset or expression) before
        // any downstream filtering so MinSpeed / smoothing operate on the
        // user-corrected values.
        var s = _adjuster.Adjust(sample.Value);

        if (s.SpeedKmh < Settings.MinSpeedKmh)
            return;

        if (SmoothingEnabled && Settings.SmoothingWindow > 1)
        {
            _smoother ??= new SampleSmoother(Settings.SmoothingWindow);
            s = _smoother.Smooth(s);
        }

        var capturedSample = s;
        _dispatcher.Post(() => Chart.AppendSample(capturedSample));
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        _dispatcher.Post(() =>
        {
            StatusMessage = $"Error: {ex.Message}";
            IsConnected = false;
            DetachSource();
            _logger.EndSession();
            LogFilePath = null;
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
    }

    partial void OnSelectedPortChanged(string? value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        if (!string.IsNullOrWhiteSpace(value)) PersistLastPort(value);
    }

    partial void OnSmoothingEnabledChanged(bool value)
    {
        // Toggling smoothing resets the smoother so the next sample starts a fresh window.
        ResetSmoother();
    }

    private void PersistLastPort(string portName)
    {
        if (_settingsStore is null) return;
        if (Settings.LastPortName == portName) return;
        var updated = Settings with { LastPortName = portName };
        Settings = updated;
        try { _settingsStore.Save(updated); } catch { /* ignore */ }
    }

    private void ResetSmoother() => _smoother = null;
}

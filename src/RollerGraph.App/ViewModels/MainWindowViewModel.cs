using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RollerGraph.App.Connection;
using RollerGraph.App.Services;
using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Logging;
using RollerGraph.Core.Models;
using RollerGraph.Core.Serial;
using RollerGraph.Core.Storage;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Top-level view-model for the main window. Acts as a thin coordinator
/// between three focused collaborators:
///
///   - <see cref="ConnectionController"/> owns the serial / replay lifecycle
///     and produces accepted samples + status events.
///   - <see cref="ChartViewModel"/> owns the chart's observable state.
///   - <see cref="SavedRunsViewModel"/> owns the saved-runs collection and
///     all CRUD operations against the on-disk store.
///
/// This class is responsible for: top-level commands, port enumeration, the
/// observable surface the window binds to (status, IsConnected, etc.), and
/// pushing settings changes down to the collaborators.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IPortEnumerator _portEnumerator;
    private readonly ISettingsStore? _settingsStore;
    private readonly ConnectionController _connection;
    private readonly List<Sample> _liveSamples = new();

    public MainWindowViewModel(
        IUiDispatcher dispatcher,
        Settings? settings = null,
        ISettingsStore? settingsStore = null,
        ISavedRunStore? runStore = null,
        ISessionLogger? logger = null,
        IPortEnumerator? portEnumerator = null,
        ISerialSourceFactory? sourceFactory = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _settingsStore = settingsStore;
        Settings = settings ?? settingsStore?.Load() ?? new Settings();

        // Default the two serial seams from a single concrete factory.
        var defaultFactory = new SystemSerialSourceFactory();
        _portEnumerator = portEnumerator ?? defaultFactory;
        var factory = sourceFactory ?? defaultFactory;

        var sessionLogger = logger ?? new CsvSessionLogger(CsvSessionLogger.DefaultLogDirectory());
        var savedRunStore = runStore ?? FileSavedRunStore.Default();

        Chart = new ChartViewModel(Settings);
        SavedRuns = new SavedRunsViewModel(savedRunStore, Chart, () => _liveSamples)
        {
            StatusReporter = msg => StatusMessage = msg,
        };

        _connection = new ConnectionController(dispatcher, factory, sessionLogger);
        _connection.ApplySettings(Settings, SampleAdjuster.FromSettingsOrIdentity(Settings), smoothingEnabled: false);
        _connection.SampleAccepted += OnSampleAccepted;
        _connection.BadLineReceived += OnBadLineReceived;
        _connection.ErrorOccurred += OnConnectionError;
        _connection.RunStopped += OnRunStopped;
        _connection.RunStarted += OnRunStarted;

        AvailablePorts = new ObservableCollection<string>();
        RefreshPorts();
        if (!string.IsNullOrWhiteSpace(Settings.LastPortName) && AvailablePorts.Contains(Settings.LastPortName))
            SelectedPort = Settings.LastPortName;
        else if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
        SavedRuns.LoadAllFromDisk();
    }

    public Settings Settings { get; private set; }
    public ChartViewModel Chart { get; }
    public SavedRunsViewModel SavedRuns { get; }

    public ObservableCollection<string> AvailablePorts { get; }

    // Property-injected view services. Each one is a small ISP-friendly interface.
    public ICsvFilePicker? FilePicker { get; set; }
    public IRunNamePrompt? RunNamePrompt { get; set; }
    public IConfirmPrompt? ConfirmPrompt { get; set; }
    public ISettingsDialog? SettingsDialog { get; set; }
    public IChartExporter? ChartExporter { get; set; }
    public IChartPrinter? ChartPrinter { get; set; }

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

    // ---- Commands ----

    [RelayCommand]
    private void RefreshPorts()
    {
        var result = _portEnumerator.EnumeratePorts();
        AvailablePorts.Clear();
        foreach (var p in result.Ports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            AvailablePorts.Add(p);
        if (SelectedPort is not null && !AvailablePorts.Contains(SelectedPort))
            SelectedPort = AvailablePorts.FirstOrDefault();

        // Surface driver-level failures so the user is not left staring at
        // an empty dropdown wondering why nothing is listed.
        if (!result.Succeeded)
            StatusMessage = $"Could not enumerate serial ports: {result.ErrorMessage}";
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (IsConnected || string.IsNullOrWhiteSpace(SelectedPort))
            return;

        try
        {
            StatusMessage = await _connection.ConnectAsync(SelectedPort, Settings, SmoothingEnabled);
            LogFilePath = _connection.LogFilePath;
            IsConnected = true;
            PersistLastPort(SelectedPort);
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connect failed: {ex.Message}";
        }
    }

    private bool CanConnect() => !IsConnected && !string.IsNullOrWhiteSpace(SelectedPort);

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        await _connection.DisconnectAsync();
        LogFilePath = _connection.LogFilePath;
        IsConnected = _connection.IsConnected;
        StatusMessage = "Disconnected";
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    private bool CanDisconnect() => IsConnected;

    [RelayCommand]
    private async Task ResetAsync()
    {
        Chart.Reset();
        BadLineCount = 0;
        _liveSamples.Clear();

        if (IsConnected)
        {
            try
            {
                StatusMessage = await _connection.RestartAsync(Settings, SmoothingEnabled);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reset failed: {ex.Message}";
            }
            finally
            {
                IsConnected = _connection.IsConnected;
                LogFilePath = _connection.LogFilePath;
                ConnectCommand.NotifyCanExecuteChanged();
                DisconnectCommand.NotifyCanExecuteChanged();
            }
            return;
        }

        _connection.ResetSmoother();
        LogFilePath = _connection.LogFilePath;
    }

    /// <summary>
    /// Starts replay of a CSV file. Disconnects any current source first.
    /// </summary>
    public async Task ReplayAsync(string filePath, TimeSpan? interval = null)
    {
        try
        {
            StatusMessage = await _connection.ReplayAsync(filePath, Settings, SmoothingEnabled, interval);
            LogFilePath = _connection.LogFilePath;
            IsConnected = true;
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Replay failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Replaces the active settings, persists them (if a store is attached),
    /// applies new axis defaults to the chart (only when no data is plotted yet),
    /// and rebuilds the pipeline with the new window size and adjustments.
    /// </summary>
    public void ApplySettings(Settings newSettings)
    {
        ArgumentNullException.ThrowIfNull(newSettings);
        // Preserve the last-port selection (so changing axes doesn't lose it).
        var withPort = newSettings with { LastPortName = SelectedPort ?? newSettings.LastPortName };
        Settings = withPort;
        Chart.UpdateDefaults(withPort);
        _connection.ApplySettings(withPort, SampleAdjuster.FromSettingsOrIdentity(withPort), SmoothingEnabled);
        TrySaveSettings(withPort);
    }

    // ---- View-service-bridged commands ----

    /// <summary>Cmd/Ctrl+K - connect if disconnected, disconnect if connected.</summary>
    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (IsConnected) await DisconnectCommand.ExecuteAsync(null);
        else await ConnectCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ReplayCsv()
    {
        if (FilePicker is null) return;
        var path = await FilePicker.PickReplayCsvAsync();
        if (path is null) return;
        await ReplayAsync(path);
    }

    [RelayCommand]
    private async Task ExportPng()
    {
        if (ChartExporter is null) return;
        var result = await ChartExporter.ExportPngAsync();
        StatusMessage = result.Outcome switch
        {
            ChartExportOutcome.Saved => $"Exported {Path.GetFileName(result.FilePath)}",
            ChartExportOutcome.Failed => $"Export failed: {result.ErrorMessage}",
            _ => StatusMessage,
        };
    }

    [RelayCommand]
    private async Task Print()
    {
        if (ChartPrinter is null) return;
        var result = await ChartPrinter.PrintAsync();
        StatusMessage = result.Outcome == ChartPrintOutcome.Failed
            ? $"Print failed: {result.ErrorMessage}"
            : result.StatusMessage;
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (SettingsDialog is null) return;
        var result = await SettingsDialog.ShowSettingsAsync(Settings);
        if (result is not null) ApplySettings(result);
    }

    [RelayCommand]
    private async Task LoadSavedRun()
    {
        if (FilePicker is null) return;
        var path = await FilePicker.PickSavedRunCsvAsync();
        if (path is null) return;
        SavedRuns.LoadFromFile(path);
    }

    [RelayCommand]
    private async Task SaveCurrentRun()
    {
        if (RunNamePrompt is null) return;
        if (!SavedRuns.CanSaveCurrentRun)
        {
            StatusMessage = "Nothing to save - capture some data first.";
            return;
        }
        var suggested = $"Run {SavedRuns.Items.Count + 1}";
        var name = await RunNamePrompt.AskForRunNameAsync(suggested);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (SavedRuns.Exists(name))
        {
            if (ConfirmPrompt is null) return;
            var ok = await ConfirmPrompt.ConfirmOverwriteAsync(name);
            if (!ok) return;
            SavedRuns.CaptureFromLive(name, overwrite: true);
        }
        else
        {
            SavedRuns.CaptureFromLive(name);
        }
    }

    /// <summary>True when there is current live data that can be saved.</summary>
    public bool CanSaveCurrentRun => SavedRuns.CanSaveCurrentRun;

    // ---- Public passthroughs for the View's code-behind row handlers ----

    public bool SavedRunExists(string name) => SavedRuns.Exists(name);
    public SavedRunViewModel? CaptureSavedRun(string name, bool overwrite = false) => SavedRuns.CaptureFromLive(name, overwrite);
    public SavedRunViewModel? LoadSavedRunFromFile(string path) => SavedRuns.LoadFromFile(path);
    public void DeleteSavedRun(SavedRunViewModel run) => SavedRuns.Delete(run);
    public bool RenameSavedRun(SavedRunViewModel run, string newName) => SavedRuns.Rename(run, newName);
    public void ToggleSavedRunVisibility(SavedRunViewModel run) => SavedRuns.ToggleVisibility(run);

    [RelayCommand]
    private void ClearSavedRuns() => SavedRuns.ClearAll();

    // ---- Event handlers wired to ConnectionController ----

    private void OnSampleAccepted(object? sender, SampleAcceptedEventArgs e)
    {
        _liveSamples.Add(e.MeasurementSample);
        Chart.AppendSample(e.Sample, e.MeasurementSample);
    }

    private void OnBadLineReceived(object? sender, EventArgs e) => BadLineCount++;

    private void OnRunStopped(object? sender, EventArgs e)
    {
        LogFilePath = _connection.LogFilePath;
        StatusMessage = "Run stopped - waiting for next measurement.";
    }

    private void OnRunStarted(object? sender, EventArgs e)
    {
        Chart.Reset();
        BadLineCount = 0;
        _liveSamples.Clear();
        LogFilePath = _connection.LogFilePath;
        StatusMessage = "New run started";
    }

    private void OnConnectionError(object? sender, ConnectionErrorEventArgs e)
    {
        StatusMessage = $"Error: {e.Error.Message}";
        IsConnected = false;
        LogFilePath = null;
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPortChanged(string? value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        if (!string.IsNullOrWhiteSpace(value)) PersistLastPort(value);
    }

    partial void OnSmoothingEnabledChanged(bool value)
    {
        _connection.SmoothingEnabled = value;
    }

    private void PersistLastPort(string portName)
    {
        if (_settingsStore is null) return;
        if (Settings.LastPortName == portName) return;
        var updated = Settings with { LastPortName = portName };
        Settings = updated;
        TrySaveSettings(updated);
    }

    private void TrySaveSettings(Settings settings)
    {
        if (_settingsStore is null) return;
        try
        {
            _settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            // Persistence failures must not crash the app, but the user
            // should know their changes did not stick.
            StatusMessage = $"Settings save failed: {ex.Message}";
        }
    }
}

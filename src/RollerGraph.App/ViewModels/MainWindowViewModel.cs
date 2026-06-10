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
using RollerGraph.Core.Storage;

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
    private readonly SavedRunStore _runStore;
    private readonly List<Sample> _liveSamples = new();
    private ISerialSource? _source;
    private SampleSmoother? _smoother;
    private SampleAdjuster _adjuster;

    public MainWindowViewModel(IUiDispatcher dispatcher, Settings? settings = null, SettingsStore? settingsStore = null, SavedRunStore? runStore = null)
    {
        _dispatcher = dispatcher;
        _settingsStore = settingsStore;
        _runStore = runStore ?? SavedRunStore.Default();
        Settings = settings ?? settingsStore?.Load() ?? new Settings();
        Chart = new ChartViewModel(Settings);
        _logger = new CsvSessionLogger(CsvSessionLogger.DefaultLogDirectory());
        _adjuster = BuildAdjuster(Settings);
        AvailablePorts = new ObservableCollection<string>();
        SavedRuns = new ObservableCollection<SavedRunViewModel>();
        RefreshPorts();
        if (!string.IsNullOrWhiteSpace(Settings.LastPortName) && AvailablePorts.Contains(Settings.LastPortName))
            SelectedPort = Settings.LastPortName;
        else if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
        LoadSavedRunsFromDisk();
    }

    public Settings Settings { get; private set; }
    public ChartViewModel Chart { get; }

    public ObservableCollection<string> AvailablePorts { get; }
    public ObservableCollection<SavedRunViewModel> SavedRuns { get; }

    /// <summary>Hook set by the View to provide file pickers, dialogs, printing.</summary>
    public IMainWindowInteractor? Interactor { get; set; }

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
        _liveSamples.Clear();
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
        _dispatcher.Post(() =>
        {
            _liveSamples.Add(capturedSample);
            Chart.AppendSample(capturedSample);
        });
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

    // -------- Interaction-bridged commands (View provides the UI surface) --------

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
        if (Interactor is null) return;
        var path = await Interactor.PickReplayCsvAsync();
        if (path is null) return;
        await ReplayAsync(path);
    }

    [RelayCommand]
    private async Task ExportPng()
    {
        if (Interactor is null) return;
        await Interactor.ExportPngAsync();
    }

    [RelayCommand]
    private async Task Print()
    {
        if (Interactor is null) return;
        await Interactor.PrintAsync();
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (Interactor is null) return;
        var result = await Interactor.ShowSettingsAsync(Settings);
        if (result is not null) ApplySettings(result);
    }

    [RelayCommand]
    private async Task LoadSavedRun()
    {
        if (Interactor is null) return;
        var path = await Interactor.PickSavedRunCsvAsync();
        if (path is null) return;
        LoadSavedRunFromFile(path);
    }

    [RelayCommand]
    private async Task SaveCurrentRun()
    {
        if (Interactor is null) return;
        if (!CanSaveCurrentRun)
        {
            StatusMessage = "Nothing to save - capture some data first.";
            return;
        }
        var suggested = $"Run {SavedRuns.Count + 1}";
        var name = await Interactor.AskForRunNameAsync(suggested);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (SavedRunExists(name))
        {
            var ok = await Interactor.ConfirmOverwriteAsync(name);
            if (!ok) return;
            CaptureSavedRun(name, overwrite: true);
        }
        else
        {
            CaptureSavedRun(name);
        }
    }

    // -------- Saved Runs --------

    /// <summary>True when there is current data that can be promoted to a saved run.</summary>
    public bool CanSaveCurrentRun => _liveSamples.Count > 0;

    private void LoadSavedRunsFromDisk()
    {
        SavedRuns.Clear();
        Chart.ClearSavedRuns();
        foreach (var run in _runStore.LoadAll())
        {
            var vm = new SavedRunViewModel(run);
            SavedRuns.Add(vm);
            Chart.AddSavedRun(run);
        }
    }

    /// <summary>
    /// Captures the current live data as a new <see cref="SavedRun"/> with the
    /// given name. Returns the resulting view-model, or null if there is no
    /// data to save.
    /// </summary>
    /// <param name="name">Display name supplied by the user.</param>
    /// <param name="overwrite">When false and a run with the same slug exists,
    /// returns null without saving.</param>
    public SavedRunViewModel? CaptureSavedRun(string name, bool overwrite = false)
    {
        if (_liveSamples.Count == 0) return null;
        var slug = SavedRunStore.Slugify(name);
        var existing = SavedRuns.FirstOrDefault(r => string.Equals(SavedRunStore.Slugify(r.Name), slug, StringComparison.Ordinal));
        if (existing is not null && !overwrite)
            return null;

        var color = RunColorPalette.Pick(SavedRuns.Count);
        var run = new SavedRun
        {
            Name = name,
            CreatedUtc = DateTime.UtcNow,
            Color = color,
            IsVisible = true,
            Samples = _liveSamples.ToArray(),
        };
        _runStore.Save(run);

        if (existing is not null)
        {
            existing.Update(run);
            Chart.AddSavedRun(run); // re-add overwrites
            StatusMessage = $"Updated saved run \"{name}\"";
            return existing;
        }

        var vm = new SavedRunViewModel(run);
        SavedRuns.Add(vm);
        Chart.AddSavedRun(run);
        StatusMessage = $"Saved run \"{name}\"";
        return vm;
    }

    /// <summary>Returns true if a saved run with the same slug already exists.</summary>
    public bool SavedRunExists(string name)
    {
        var slug = SavedRunStore.Slugify(name);
        return SavedRuns.Any(r => string.Equals(SavedRunStore.Slugify(r.Name), slug, StringComparison.Ordinal));
    }

    /// <summary>
    /// Loads a CSV file as a new saved run. The file is parsed using the same
    /// pipeline as Replay (no adjustments applied - they're assumed to already
    /// reflect the desired calibration of the source file).
    /// </summary>
    public SavedRunViewModel? LoadSavedRunFromFile(string filePath)
    {
        var samples = new List<Sample>();
        foreach (var raw in File.ReadAllLines(filePath))
        {
            var trimmed = raw.TrimEnd();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            // Skip header rows from previously-saved files.
            if (trimmed.StartsWith("sample_number", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("timestamp_utc", StringComparison.OrdinalIgnoreCase)) continue;

            // Try the saved-run CSV format first (5+ fields, fields are [n,sp,nm,hp,utc]).
            var s = TryParseSavedRunLine(trimmed) ?? CsvLineParser.Parse(trimmed, DateTime.UtcNow);
            if (s is not null) samples.Add(s.Value);
        }
        if (samples.Count == 0)
        {
            StatusMessage = $"No samples found in {Path.GetFileName(filePath)}";
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(filePath);
        var color = RunColorPalette.Pick(SavedRuns.Count);
        var run = new SavedRun
        {
            Name = name,
            CreatedUtc = DateTime.UtcNow,
            Color = color,
            IsVisible = true,
            Samples = samples,
        };
        _runStore.Save(run);

        var vm = new SavedRunViewModel(run);
        SavedRuns.Add(vm);
        Chart.AddSavedRun(run);
        StatusMessage = $"Loaded {samples.Count} samples as \"{name}\"";
        return vm;
    }

    private static Sample? TryParseSavedRunLine(string line)
    {
        var parts = line.Split(',');
        if (parts.Length < 5) return null;
        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)) return null;
        if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sp)) return null;
        if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var nm)) return null;
        if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hp)) return null;
        DateTime ra = DateTime.UtcNow;
        DateTime.TryParse(parts[4], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out ra);
        return new Sample(n, sp, nm, hp, ra);
    }

    /// <summary>Permanently removes a saved run from disk and from the chart.</summary>
    public void DeleteSavedRun(SavedRunViewModel run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runStore.Delete(run.Name);
        Chart.RemoveSavedRun(run.Name);
        SavedRuns.Remove(run);
        StatusMessage = $"Deleted run \"{run.Name}\"";
    }

    /// <summary>
    /// Renames a saved run. Deletes the old file, writes a new one. Returns
    /// false if the new name slugs to an existing run that isn't this one.
    /// </summary>
    public bool RenameSavedRun(SavedRunViewModel run, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var newSlug = SavedRunStore.Slugify(newName);
        var oldSlug = SavedRunStore.Slugify(run.Name);
        if (!string.Equals(newSlug, oldSlug, StringComparison.Ordinal))
        {
            var clash = SavedRuns.Any(r => r != run && string.Equals(SavedRunStore.Slugify(r.Name), newSlug, StringComparison.Ordinal));
            if (clash) return false;
            _runStore.Delete(run.Name);
        }
        var renamed = run.Source with { Name = newName };
        _runStore.Save(renamed);
        run.Update(renamed);
        Chart.RemoveSavedRun(run.Name); // chart key is old name - actually we already updated VM
        // Re-add under the new name.
        Chart.RemoveSavedRun(renamed.Name);
        Chart.AddSavedRun(renamed);
        return true;
    }

    /// <summary>Pushes a visibility change for a saved run through to disk and chart.</summary>
    public void ToggleSavedRunVisibility(SavedRunViewModel run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var updated = run.Source with { IsVisible = run.IsVisible };
        _runStore.Save(updated);
        run.Update(updated);
        Chart.SetSavedRunVisible(updated.Name, updated.IsVisible);
    }

    /// <summary>Removes every saved run from disk and from the chart.</summary>
    [RelayCommand]
    private void ClearSavedRuns()
    {
        foreach (var r in SavedRuns.ToList())
            DeleteSavedRun(r);
    }
}

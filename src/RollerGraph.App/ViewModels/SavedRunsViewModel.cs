using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RollerGraph.App.Services;
using RollerGraph.Core.Logging;
using RollerGraph.Core.Models;
using RollerGraph.Core.Storage;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Owns the on-screen <see cref="SavedRunViewModel"/> collection and CRUD
/// operations against an <see cref="ISavedRunStore"/>. Pushes overlay
/// add/remove/visibility changes through to the supplied
/// <see cref="ChartViewModel"/>. Loading from disk is delegated to the store.
///
/// Single responsibility: keep the in-memory saved-runs collection in sync
/// with the underlying store and with the chart's overlays.
/// </summary>
public sealed partial class SavedRunsViewModel : ObservableObject
{
    private readonly ISavedRunStore _store;
    private readonly ChartViewModel _chart;
    private readonly Func<IReadOnlyList<Sample>> _liveSamplesProvider;

    public SavedRunsViewModel(
        ISavedRunStore store,
        ChartViewModel chart,
        Func<IReadOnlyList<Sample>> liveSamplesProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(liveSamplesProvider);
        _store = store;
        _chart = chart;
        _liveSamplesProvider = liveSamplesProvider;
        Items = new ObservableCollection<SavedRunViewModel>();
    }

    /// <summary>The displayed list of saved runs.</summary>
    public ObservableCollection<SavedRunViewModel> Items { get; }

    /// <summary>Hook set by the parent VM so this VM can publish status text.</summary>
    public Action<string>? StatusReporter { get; set; }

    /// <summary>True when there is current data that can be promoted to a saved run.</summary>
    public bool CanSaveCurrentRun => _liveSamplesProvider().Count > 0;

    /// <summary>Loads every persisted run, replaces the in-memory collection and chart overlays.</summary>
    public void LoadAllFromDisk()
    {
        Items.Clear();
        _chart.ClearSavedRuns();
        foreach (var run in _store.LoadAll())
        {
            var vm = new SavedRunViewModel(run);
            Items.Add(vm);
            _chart.AddSavedRun(run);
        }
    }

    /// <summary>True when a run with a slug-equivalent name already exists in this collection.</summary>
    public bool Exists(string name) =>
        Items.Any(r => RunSlugger.AreSameSlug(r.Name, name));

    /// <summary>
    /// Promotes the current live samples to a new saved run. Returns null
    /// when there is no live data, or when the name already exists and
    /// <paramref name="overwrite"/> is false.
    /// </summary>
    public SavedRunViewModel? CaptureFromLive(string name, bool overwrite = false)
    {
        var liveSamples = _liveSamplesProvider();
        if (liveSamples.Count == 0) return null;

        var existing = Items.FirstOrDefault(r => RunSlugger.AreSameSlug(r.Name, name));
        if (existing is not null && !overwrite)
            return null;

        var color = RunColorPalette.Pick(Items.Count);
        var run = new SavedRun
        {
            Name = name,
            CreatedUtc = DateTime.UtcNow,
            Color = color,
            IsVisible = true,
            Samples = liveSamples.ToArray(),
        };
        _store.Save(run);

        if (existing is not null)
        {
            existing.Update(run);
            _chart.AddSavedRun(run);
            Report($"Updated saved run \"{name}\"");
            return existing;
        }

        var vm = new SavedRunViewModel(run);
        Items.Add(vm);
        _chart.AddSavedRun(run);
        Report($"Saved run \"{name}\"");
        return vm;
    }

    /// <summary>
    /// Loads a CSV file (in either live or saved-run format) as a new saved
    /// run. Uses <see cref="SavedRunCsvSerializer"/> first; falls back to the
    /// live <see cref="Core.Parsing.CsvLineParser"/> for raw replay files.
    /// </summary>
    public SavedRunViewModel? LoadFromFile(string filePath)
    {
        var samples = new List<Sample>();
        foreach (var raw in File.ReadAllLines(filePath))
        {
            var trimmed = raw.TrimEnd();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            // Skip header rows produced by either serializer; matching on the
            // exported constants keeps this in lock-step with format changes.
            if (IsKnownHeader(trimmed)) continue;

            // Try the saved-run CSV format first (5+ fields), fall back to live format.
            var s = SavedRunCsvSerializer.ParseSampleLine(trimmed)
                    ?? Core.Parsing.CsvLineParser.Parse(trimmed, DateTime.UtcNow);
            if (s is not null) samples.Add(s.Value);
        }
        if (samples.Count == 0)
        {
            Report($"No samples found in {Path.GetFileName(filePath)}");
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(filePath);
        var color = RunColorPalette.Pick(Items.Count);
        var run = new SavedRun
        {
            Name = name,
            CreatedUtc = DateTime.UtcNow,
            Color = color,
            IsVisible = true,
            Samples = samples,
        };
        _store.Save(run);

        var vm = new SavedRunViewModel(run);
        Items.Add(vm);
        _chart.AddSavedRun(run);
        Report($"Loaded {samples.Count} samples as \"{name}\"");
        return vm;
    }

    private static bool IsKnownHeader(string line) =>
        line.StartsWith(SavedRunCsvSerializer.Header, StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith(CsvSessionLogger.Header, StringComparison.OrdinalIgnoreCase);

    /// <summary>Removes a saved run from disk, the chart, and the collection.</summary>
    public void Delete(SavedRunViewModel run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _store.Delete(run.Name);
        _chart.RemoveSavedRun(run.Name);
        Items.Remove(run);
        Report($"Deleted run \"{run.Name}\"");
    }

    /// <summary>
    /// Renames a saved run. Returns false if the new name collides with
    /// another existing run (slug-equivalent).
    /// </summary>
    public bool Rename(SavedRunViewModel run, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var oldSlug = RunSlugger.Slugify(run.Name);
        var newSlug = RunSlugger.Slugify(newName);
        if (!string.Equals(newSlug, oldSlug, StringComparison.Ordinal))
        {
            var clash = Items.Any(r => r != run && string.Equals(RunSlugger.Slugify(r.Name), newSlug, StringComparison.Ordinal));
            if (clash) return false;
            _store.Delete(run.Name);
        }
        var renamed = run.Source with { Name = newName };
        _store.Save(renamed);
        run.Update(renamed);
        _chart.RemoveSavedRun(run.Name);
        _chart.RemoveSavedRun(renamed.Name);
        _chart.AddSavedRun(renamed);
        return true;
    }

    /// <summary>Pushes a visibility change for a saved run through to disk and chart.</summary>
    public void ToggleVisibility(SavedRunViewModel run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var updated = run.Source with { IsVisible = run.IsVisible };
        _store.Save(updated);
        run.Update(updated);
        _chart.SetSavedRunVisible(updated.Name, updated.IsVisible);
    }

    /// <summary>Removes every saved run from disk and from the chart.</summary>
    [RelayCommand]
    public void ClearAll()
    {
        foreach (var r in Items.ToList())
            Delete(r);
    }

    private void Report(string message) => StatusReporter?.Invoke(message);
}

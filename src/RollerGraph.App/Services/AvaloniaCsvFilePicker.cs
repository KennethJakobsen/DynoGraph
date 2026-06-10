using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace RollerGraph.App.Services;

/// <summary>
/// Avalonia-backed <see cref="ICsvFilePicker"/>. Wraps the owning window's
/// storage provider so the view-model never sees an Avalonia type.
/// </summary>
public sealed class AvaloniaCsvFilePicker : ICsvFilePicker
{
    private readonly Window _owner;

    public AvaloniaCsvFilePicker(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<string?> PickReplayCsvAsync()
    {
        var storage = _owner.StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open RollerGraph CSV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV files") { Patterns = new[] { "*.csv" } },
                FilePickerFileTypes.All,
            },
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> PickSavedRunCsvAsync()
    {
        var storage = _owner.StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load CSV as Saved Run",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV files") { Patterns = new[] { "*.csv" } },
                FilePickerFileTypes.All,
            },
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }
}

using Avalonia.Controls;
using RollerGraph.App.ViewModels;
using RollerGraph.App.Views;
using RollerGraph.Core.Models;

namespace RollerGraph.App.Services;

/// <summary>Avalonia-backed <see cref="ISettingsDialog"/>.</summary>
public sealed class AvaloniaSettingsDialog : ISettingsDialog
{
    private readonly Window _owner;

    public AvaloniaSettingsDialog(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<Settings?> ShowSettingsAsync(Settings current)
    {
        var dialog = new SettingsWindow { DataContext = new SettingsViewModel(current) };
        await dialog.ShowDialog(_owner);
        return dialog.Result;
    }
}

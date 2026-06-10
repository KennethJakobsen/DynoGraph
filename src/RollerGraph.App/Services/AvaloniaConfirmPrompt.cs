using Avalonia.Controls;
using RollerGraph.App.Views;

namespace RollerGraph.App.Services;

/// <summary>Avalonia-backed <see cref="IConfirmPrompt"/>.</summary>
public sealed class AvaloniaConfirmPrompt : IConfirmPrompt
{
    private readonly Window _owner;

    public AvaloniaConfirmPrompt(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<bool> ConfirmOverwriteAsync(string runName)
    {
        var dlg = new ConfirmDialog
        {
            Title = "Overwrite saved run?",
            Message = $"A run named \"{runName}\" already exists. Overwrite it with the current data?",
            ConfirmText = "Overwrite",
            CancelText = "Cancel",
        };
        await dlg.ShowDialog(_owner);
        return dlg.Confirmed;
    }
}

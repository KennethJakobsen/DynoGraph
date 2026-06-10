using Avalonia.Controls;
using RollerGraph.App.Views;

namespace RollerGraph.App.Services;

/// <summary>Avalonia-backed <see cref="IRunNamePrompt"/>.</summary>
public sealed class AvaloniaRunNamePrompt : IRunNamePrompt
{
    private readonly Window _owner;

    public AvaloniaRunNamePrompt(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<string?> AskForRunNameAsync(string suggested)
    {
        var dlg = new RunNameDialog { SuggestedName = suggested };
        await dlg.ShowDialog(_owner);
        return dlg.Result;
    }
}

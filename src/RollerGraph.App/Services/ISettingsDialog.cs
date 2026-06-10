using RollerGraph.Core.Models;

namespace RollerGraph.App.Services;

/// <summary>Presents the modal Settings dialog and returns the edited result.</summary>
public interface ISettingsDialog
{
    /// <summary>
    /// Show the settings dialog populated from <paramref name="current"/>.
    /// Returns the new settings if the user confirmed, or null if cancelled.
    /// </summary>
    Task<Settings?> ShowSettingsAsync(Settings current);
}

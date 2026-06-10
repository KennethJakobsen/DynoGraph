namespace RollerGraph.App.Services;

/// <summary>
/// Provides UI-only operations (file pickers, dialogs, printing) so the
/// main view-model can stay testable without a Window reference.
/// </summary>
public interface IMainWindowInteractor
{
    /// <summary>Shows the OS file picker for a CSV to replay; returns the chosen path or null.</summary>
    Task<string?> PickReplayCsvAsync();

    /// <summary>Shows the OS file picker for a CSV to load as a saved run.</summary>
    Task<string?> PickSavedRunCsvAsync();

    /// <summary>Asks the user for a run name; returns null if cancelled.</summary>
    Task<string?> AskForRunNameAsync(string suggested);

    /// <summary>Confirm-overwrite prompt; returns true to overwrite.</summary>
    Task<bool> ConfirmOverwriteAsync(string runName);

    /// <summary>Shows the settings dialog; returns the new settings, or null if cancelled.</summary>
    Task<RollerGraph.Core.Models.Settings?> ShowSettingsAsync(RollerGraph.Core.Models.Settings current);

    /// <summary>Picks a path and writes the current chart to it as PNG.</summary>
    Task ExportPngAsync();

    /// <summary>Triggers the OS print flow for the current chart + stats.</summary>
    Task PrintAsync();
}

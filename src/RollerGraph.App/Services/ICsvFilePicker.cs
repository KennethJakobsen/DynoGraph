namespace RollerGraph.App.Services;

/// <summary>Shows the OS file open dialog for a CSV file.</summary>
public interface ICsvFilePicker
{
    /// <summary>Pick a CSV file to replay through the live chart.</summary>
    /// <returns>Absolute path to the chosen file, or null if the user cancelled.</returns>
    Task<string?> PickReplayCsvAsync();

    /// <summary>Pick a CSV file to load as a saved run overlay.</summary>
    /// <returns>Absolute path to the chosen file, or null if the user cancelled.</returns>
    Task<string?> PickSavedRunCsvAsync();
}

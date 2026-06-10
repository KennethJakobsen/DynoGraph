namespace RollerGraph.Core.Storage;

/// <summary>
/// Centralises the OS-appropriate locations RollerGraph uses inside
/// <see cref="Environment.SpecialFolder.LocalApplicationData"/>. Keeps the
/// application's brand name ("RollerGraph") and the subfolder layout in a
/// single place so a rename or relocation is a one-file change.
/// </summary>
public static class AppDataPaths
{
    /// <summary>Top-level folder name used under LocalApplicationData.</summary>
    public const string AppFolderName = "RollerGraph";

    /// <summary>
    /// Returns <c>{LocalAppData}/RollerGraph</c>, creating
    /// <c>LocalAppData</c> if the platform requires it.
    /// </summary>
    public static string AppRoot()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.Combine(localAppData, AppFolderName);
    }

    /// <summary>Returns <c>{LocalAppData}/RollerGraph/logs</c>.</summary>
    public static string LogsDirectory() => Path.Combine(AppRoot(), "logs");

    /// <summary>Returns <c>{LocalAppData}/RollerGraph/runs</c>.</summary>
    public static string SavedRunsDirectory() => Path.Combine(AppRoot(), "runs");

    /// <summary>Returns <c>{LocalAppData}/RollerGraph/settings.json</c>.</summary>
    public static string SettingsFilePath() => Path.Combine(AppRoot(), "settings.json");
}

using System.Text.Json;
using System.Text.Json.Serialization;
using RollerGraph.Core.Storage;

namespace RollerGraph.Core.Models;

/// <summary>
/// Loads and persists <see cref="Settings"/> as JSON in a per-OS app data folder.
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public SettingsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        _filePath = filePath;
    }

    /// <summary>Full path to the settings file.</summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Returns the OS-appropriate default settings file path:
    ///   {LocalAppData}/RollerGraph/settings.json
    /// </summary>
    public static string DefaultFilePath() => AppDataPaths.SettingsFilePath();

    /// <summary>
    /// Returns the default-located store.
    /// </summary>
    public static SettingsStore Default() => new(DefaultFilePath());

    /// <summary>
    /// Loads settings from disk. Returns defaults if the file is missing,
    /// empty, or malformed. Never throws for IO/parse errors.
    /// </summary>
    public Settings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new Settings();

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new Settings();

            var loaded = JsonSerializer.Deserialize<Settings>(json, Options);
            return loaded ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    /// <summary>
    /// Persists settings atomically (write-and-rename). Creates the parent
    /// directory if necessary. Throws on permission or IO errors.
    /// </summary>
    public void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, Options);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        // Atomic replace where supported; otherwise fall back to delete+move.
        if (File.Exists(_filePath))
        {
            File.Replace(tmp, _filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, _filePath);
        }
    }
}

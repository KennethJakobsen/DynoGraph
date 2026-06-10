using System.Text;
using RollerGraph.Core.Models;

namespace RollerGraph.Core.Storage;

/// <summary>
/// File-system-backed <see cref="ISavedRunStore"/>. One file per run; the
/// file name is the slugified run name. Serialisation is delegated to
/// <see cref="SavedRunCsvSerializer"/> and naming to <see cref="RunSlugger"/>,
/// keeping this class responsible only for path resolution and atomic IO.
/// </summary>
public sealed class FileSavedRunStore : ISavedRunStore
{
    private readonly SavedRunCsvSerializer _serializer;

    public string RootDirectory { get; }

    public FileSavedRunStore(string rootDirectory)
        : this(rootDirectory, new SavedRunCsvSerializer())
    {
    }

    public FileSavedRunStore(string rootDirectory, SavedRunCsvSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        ArgumentNullException.ThrowIfNull(serializer);
        RootDirectory = rootDirectory;
        _serializer = serializer;
    }

    /// <summary>
    /// Returns the OS-appropriate default folder for saved runs:
    /// <c>{LocalAppData}/RollerGraph/runs/</c>.
    /// </summary>
    public static string DefaultRootDirectory() => AppDataPaths.SavedRunsDirectory();

    /// <summary>Returns a store rooted at the default folder.</summary>
    public static FileSavedRunStore Default() => new(DefaultRootDirectory());

    public IReadOnlyList<SavedRun> LoadAll()
    {
        if (!Directory.Exists(RootDirectory))
            return Array.Empty<SavedRun>();

        var results = new List<SavedRun>();
        foreach (var file in Directory.EnumerateFiles(RootDirectory, "*.csv"))
        {
            try
            {
                using var reader = new StreamReader(file, Encoding.UTF8);
                var run = _serializer.Read(
                    reader,
                    fallbackName: Path.GetFileNameWithoutExtension(file),
                    fallbackCreatedUtc: File.GetCreationTimeUtc(file));
                if (run is not null)
                    results.Add(run);
            }
            catch
            {
                // Corrupt files are silently skipped.
            }
        }
        return results.OrderBy(r => r.CreatedUtc).ToList();
    }

    public string Save(SavedRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Directory.CreateDirectory(RootDirectory);
        var path = PathFor(run.Name);
        var tmp = path + ".tmp";
        using (var writer = new StreamWriter(
            new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None),
            Encoding.UTF8))
        {
            _serializer.Write(run, writer);
        }

        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
        return path;
    }

    public bool Delete(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return false;
        try { File.Delete(path); return true; }
        catch { return false; }
    }

    public bool Exists(string name) => File.Exists(PathFor(name));

    /// <summary>Returns the absolute path that would be used for a given run name.</summary>
    public string PathFor(string name) => Path.Combine(RootDirectory, RunSlugger.Slugify(name) + ".csv");
}

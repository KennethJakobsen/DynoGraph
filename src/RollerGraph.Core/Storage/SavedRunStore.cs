using System.Globalization;
using System.Text;
using RollerGraph.Core.Models;

namespace RollerGraph.Core.Storage;

/// <summary>
/// Reads and writes <see cref="SavedRun"/> records as individual CSV files
/// under a root directory. One run per file; the file name is the slug of
/// the run's <see cref="SavedRun.Name"/>.
/// </summary>
public sealed class SavedRunStore
{
    private const string Header = "sample_number,speed_kmh,nm,hp,received_utc";

    public string RootDirectory { get; }

    public SavedRunStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        RootDirectory = rootDirectory;
    }

    /// <summary>
    /// Returns the OS-appropriate default folder for saved runs:
    /// <c>{LocalAppData}/RollerGraph/runs/</c>.
    /// </summary>
    public static string DefaultRootDirectory()
    {
        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.Combine(appData, "RollerGraph", "runs");
    }

    /// <summary>Returns a store rooted at the default folder.</summary>
    public static SavedRunStore Default() => new(DefaultRootDirectory());

    /// <summary>
    /// Loads every saved run in the root directory, ordered by
    /// <see cref="SavedRun.CreatedUtc"/> ascending. Bad files are skipped.
    /// </summary>
    public IReadOnlyList<SavedRun> LoadAll()
    {
        if (!Directory.Exists(RootDirectory))
            return Array.Empty<SavedRun>();

        var results = new List<SavedRun>();
        foreach (var file in Directory.EnumerateFiles(RootDirectory, "*.csv"))
        {
            try
            {
                var run = ReadFromFile(file);
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

    /// <summary>
    /// Writes (or overwrites) the file for this run. The file name is the
    /// slugified <see cref="SavedRun.Name"/>.
    /// </summary>
    public string Save(SavedRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Directory.CreateDirectory(RootDirectory);
        var path = PathFor(run.Name);
        var tmp = path + ".tmp";
        using (var writer = new StreamWriter(new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None), Encoding.UTF8))
        {
            writer.WriteLine("# name=" + EscapeMeta(run.Name));
            writer.WriteLine("# created=" + run.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteLine("# color=" + run.Color);
            writer.WriteLine("# visible=" + (run.IsVisible ? "true" : "false"));
            writer.WriteLine(Header);
            foreach (var s in run.Samples)
            {
                writer.Write(s.SampleNumber.ToString(CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(s.SpeedKmh.ToString("R", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(s.Nm.ToString("R", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(s.Hp.ToString("R", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.WriteLine(s.ReceivedAt.ToString("O", CultureInfo.InvariantCulture));
            }
        }

        if (File.Exists(path))
        {
            File.Replace(tmp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, path);
        }
        return path;
    }

    /// <summary>Deletes the saved run with the given name (no-op if missing).</summary>
    public bool Delete(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return false;
        try { File.Delete(path); return true; }
        catch { return false; }
    }

    /// <summary>Returns the absolute path that would be used for a given run name.</summary>
    public string PathFor(string name) => Path.Combine(RootDirectory, Slugify(name) + ".csv");

    /// <summary>
    /// Converts a free-form name into a filesystem-safe slug.
    /// Lowercase, [a-z0-9] preserved, everything else becomes '-', consecutive
    /// hyphens collapsed, leading/trailing hyphens trimmed.
    /// Empty input returns "run".
    /// </summary>
    public static string Slugify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "run";

        var sb = new StringBuilder(input.Length);
        bool lastHyphen = false;
        foreach (var ch in input.Trim())
        {
            char lower = char.ToLowerInvariant(ch);
            if ((lower >= 'a' && lower <= 'z') || (lower >= '0' && lower <= '9'))
            {
                sb.Append(lower);
                lastHyphen = false;
            }
            else
            {
                if (!lastHyphen && sb.Length > 0)
                {
                    sb.Append('-');
                    lastHyphen = true;
                }
            }
        }
        while (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;
        return sb.Length == 0 ? "run" : sb.ToString();
    }

    private SavedRun? ReadFromFile(string path)
    {
        string? name = null;
        DateTime created = File.GetCreationTimeUtc(path);
        string color = "#3B82F6";
        bool visible = true;
        var samples = new List<Sample>();

        using var reader = new StreamReader(path, Encoding.UTF8);
        string? line;
        bool sawHeader = false;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;

            if (line.StartsWith('#'))
            {
                var trimmed = line.AsSpan(1).Trim();
                var eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;
                var key = trimmed[..eq].ToString().Trim().ToLowerInvariant();
                var val = trimmed[(eq + 1)..].ToString().Trim();
                switch (key)
                {
                    case "name": name = UnescapeMeta(val); break;
                    case "created":
                        if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var c))
                            created = c.ToUniversalTime();
                        break;
                    case "color": color = val; break;
                    case "visible": visible = val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                }
                continue;
            }

            if (!sawHeader)
            {
                sawHeader = true;
                if (line.StartsWith("sample_number", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sn)) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)) continue;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var nm)) continue;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var hp)) continue;
            DateTime ra = DateTime.UtcNow;
            DateTime.TryParse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out ra);
            samples.Add(new Sample(sn, sp, nm, hp, ra));
        }

        if (name is null && samples.Count == 0)
            return null;

        // Default name from filename when metadata is missing.
        name ??= Path.GetFileNameWithoutExtension(path);

        return new SavedRun
        {
            Name = name,
            CreatedUtc = created,
            Color = color,
            IsVisible = visible,
            Samples = samples,
        };
    }

    private static string EscapeMeta(string value)
    {
        // Encode newlines so metadata always fits on one line.
        return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string UnescapeMeta(string value)
    {
        return value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
    }
}

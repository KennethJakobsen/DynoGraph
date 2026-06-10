using System.Globalization;
using RollerGraph.Core.Models;

namespace RollerGraph.Core.Storage;

/// <summary>
/// Serialises a <see cref="SavedRun"/> to and from the RollerGraph CSV format.
/// Format:
///   # name=...        (metadata header, escaped)
///   # created=ISO     (UTC, round-trip)
///   # color=#RRGGBB
///   # visible=true|false
///   sample_number,speed_kmh,nm,hp,received_utc
///   1,30.0,60.0,40.0,2025-06-01T10:00:01.0000000Z
///   ...
///
/// This class is stream-only: no file-system access, no path handling.
/// </summary>
public sealed class SavedRunCsvSerializer
{
    /// <summary>Column header written by <see cref="Write"/> and recognised by <see cref="Read"/>.</summary>
    public const string Header = "sample_number,speed_kmh,nm,hp,received_utc";

    /// <summary>
    /// Writes the run to <paramref name="writer"/>. The writer is not closed.
    /// </summary>
    public void Write(SavedRun run, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(writer);

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

    /// <summary>
    /// Reads a run from <paramref name="reader"/>. Returns null when the
    /// stream contains neither metadata nor sample rows.
    /// </summary>
    /// <param name="reader">Open text reader positioned at the start of the run.</param>
    /// <param name="fallbackName">Name to assign if no <c>name</c> metadata is present.</param>
    /// <param name="fallbackCreatedUtc">Creation timestamp to assign if no <c>created</c> metadata is present.</param>
    public SavedRun? Read(TextReader reader, string fallbackName, DateTime fallbackCreatedUtc)
    {
        ArgumentNullException.ThrowIfNull(reader);

        string? name = null;
        DateTime created = fallbackCreatedUtc;
        string color = "#3B82F6";
        bool visible = true;
        var samples = new List<Sample>();

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

            var sample = ParseSampleLine(line);
            if (sample is not null) samples.Add(sample.Value);
        }

        if (name is null && samples.Count == 0)
            return null;

        name ??= fallbackName;

        return new SavedRun
        {
            Name = name,
            CreatedUtc = created,
            Color = color,
            IsVisible = visible,
            Samples = samples,
        };
    }

    /// <summary>
    /// Parses a single sample row in this serializer's format. Returns null
    /// on any malformed input. Exposed so loaders can reuse the same logic
    /// for headerless input.
    /// </summary>
    public static Sample? ParseSampleLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var parts = line.Split(',');
        if (parts.Length < 5) return null;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sn)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)) return null;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var nm)) return null;
        if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var hp)) return null;

        DateTime ra = DateTime.UtcNow;
        DateTime.TryParse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out ra);
        return new Sample(sn, sp, nm, hp, ra);
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

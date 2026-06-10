using System.Globalization;
using RollerGraph.Core.Models;

namespace RollerGraph.Core.Parsing;

/// <summary>
/// Parses a single CSV line in the format:
///   samplenum,speed,nm,hp*10,NA,NA,NA,NA,NA
/// into a <see cref="Sample"/>. Returns null for malformed input.
/// </summary>
public static class CsvLineParser
{
    /// <summary>
    /// Attempts to parse a single CSV line.
    /// </summary>
    /// <param name="line">Raw line (no trailing newline required).</param>
    /// <param name="receivedAt">Timestamp to stamp on the produced sample.</param>
    /// <returns>A <see cref="Sample"/> when parsing succeeds, otherwise null.</returns>
    public static Sample? Parse(string? line, DateTime receivedAt)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var trimmed = line.Trim();
        var parts = trimmed.Split(',');

        // Need at least the first 4 fields: samplenum, speed, nm, hp*10
        if (parts.Length < 4)
            return null;

        if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sampleNumber))
            return null;

        if (!TryParseDouble(parts[1], out var speedKmh))
            return null;

        if (!TryParseDouble(parts[2], out var nm))
            return null;

        if (!TryParseDouble(parts[3], out var hpRaw))
            return null;

        var hp = hpRaw / 10.0;

        return new Sample(sampleNumber, speedKmh, nm, hp, receivedAt);
    }

    private static bool TryParseDouble(string? field, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(field))
            return false;

        var trimmed = field.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

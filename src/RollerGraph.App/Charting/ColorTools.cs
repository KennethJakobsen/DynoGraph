using System.Globalization;
using SkiaSharp;

namespace RollerGraph.App.Charting;

/// <summary>
/// Small helpers for working with #RRGGBB hex colour strings and producing
/// shaded variants. Kept separate from chart/view-model code so the colour
/// logic can be unit-tested in isolation.
/// </summary>
public static class ColorTools
{
    /// <summary>RollerGraph's fallback brand blue, used when a hex string can't be parsed.</summary>
    public static readonly SKColor FallbackBlue = new(0x3B, 0x82, 0xF6);

    /// <summary>Parses a #RRGGBB (or RRGGBB) string. Falls back to <see cref="FallbackBlue"/>.</summary>
    public static SKColor ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return FallbackBlue;
        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 6 &&
            byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
            byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
            byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return new SKColor(r, g, b);
        }
        return FallbackBlue;
    }

    /// <summary>
    /// Scales R/G/B by <paramref name="factor"/>. <c>factor &lt; 1</c> darkens,
    /// <c>&gt; 1</c> lightens. Channels are clamped to [0, 255].
    /// </summary>
    public static SKColor AdjustBrightness(SKColor c, double factor)
    {
        byte Clamp(double v) => (byte)Math.Min(255, Math.Max(0, (int)Math.Round(v)));
        return new SKColor(Clamp(c.Red * factor), Clamp(c.Green * factor), Clamp(c.Blue * factor));
    }
}

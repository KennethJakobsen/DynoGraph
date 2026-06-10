namespace RollerGraph.Core.Storage;

/// <summary>
/// Cycles through a fixed palette of distinct hex colors for saved runs.
/// </summary>
public static class RunColorPalette
{
    public static readonly IReadOnlyList<string> Colors = new[]
    {
        "#3B82F6", // blue
        "#10B981", // green
        "#A855F7", // purple
        "#F59E0B", // amber
        "#EF4444", // red
        "#06B6D4", // cyan
        "#EC4899", // pink
        "#84CC16", // lime
    };

    /// <summary>
    /// Returns a color from the palette indexed by <paramref name="i"/>
    /// (wraps with modulo).
    /// </summary>
    public static string Pick(int i)
    {
        var idx = ((i % Colors.Count) + Colors.Count) % Colors.Count;
        return Colors[idx];
    }
}

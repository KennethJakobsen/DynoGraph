namespace RollerGraph.Core.Models;

/// <summary>
/// A captured dyno run that can be displayed alongside the live run for comparison.
/// Persisted as a single CSV file under <c>{LocalAppData}/RollerGraph/runs/</c>.
/// </summary>
public sealed record SavedRun
{
    /// <summary>User-supplied display name (e.g. "86 nozzle").</summary>
    public string Name { get; init; } = "";

    /// <summary>UTC timestamp when this run was captured.</summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Hex color (e.g. "#3B82F6") used to draw this run on the chart.</summary>
    public string Color { get; init; } = "#3B82F6";

    /// <summary>Whether this run is currently rendered on the chart.</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Captured samples (adjustments already applied, smoothing not applied).</summary>
    public IReadOnlyList<Sample> Samples { get; init; } = Array.Empty<Sample>();
}

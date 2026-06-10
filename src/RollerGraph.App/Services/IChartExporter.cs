namespace RollerGraph.App.Services;

/// <summary>Result returned by <see cref="IChartExporter.ExportPngAsync"/>.</summary>
public enum ChartExportOutcome
{
    /// <summary>The user cancelled the export.</summary>
    Cancelled,
    /// <summary>The PNG was written successfully.</summary>
    Saved,
    /// <summary>The export failed; <see cref="ChartExportResult.ErrorMessage"/> describes why.</summary>
    Failed,
}

/// <summary>Outcome of a chart export attempt.</summary>
/// <param name="Outcome">High-level success/cancel/failure result.</param>
/// <param name="FilePath">Path of the saved file when <see cref="Outcome"/> is <see cref="ChartExportOutcome.Saved"/>.</param>
/// <param name="ErrorMessage">Reason for the failure when <see cref="Outcome"/> is <see cref="ChartExportOutcome.Failed"/>.</param>
public readonly record struct ChartExportResult(
    ChartExportOutcome Outcome,
    string? FilePath = null,
    string? ErrorMessage = null);

/// <summary>Exports the current chart to a PNG file chosen by the user.</summary>
public interface IChartExporter
{
    /// <summary>
    /// Prompt for a destination and write the current chart there as PNG.
    /// Implementations should not touch the view-model's status text directly;
    /// the caller decides what to do with the result.
    /// </summary>
    Task<ChartExportResult> ExportPngAsync();
}

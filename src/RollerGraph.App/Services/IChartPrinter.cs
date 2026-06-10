namespace RollerGraph.App.Services;

/// <summary>Result returned by <see cref="IChartPrinter.PrintAsync"/>.</summary>
public enum ChartPrintOutcome
{
    /// <summary>The print job was handed off to the OS successfully.</summary>
    Sent,
    /// <summary>The image was rendered but viewing/printing requires user action (e.g. macOS Preview).</summary>
    OpenedInViewer,
    /// <summary>Print is not supported on this platform; the snapshot is saved instead.</summary>
    SnapshotOnly,
    /// <summary>Print failed; <see cref="ChartPrintResult.ErrorMessage"/> describes why.</summary>
    Failed,
}

/// <summary>Outcome of a chart print attempt.</summary>
/// <param name="Outcome">High-level status code.</param>
/// <param name="StatusMessage">Human-readable description suitable for the status bar.</param>
/// <param name="ErrorMessage">Reason for the failure when <see cref="Outcome"/> is <see cref="ChartPrintOutcome.Failed"/>.</param>
public readonly record struct ChartPrintResult(
    ChartPrintOutcome Outcome,
    string StatusMessage,
    string? ErrorMessage = null);

/// <summary>Sends the current chart to the OS print pipeline.</summary>
public interface IChartPrinter
{
    /// <summary>
    /// Renders the chart, then hands the result to the OS print pipeline.
    /// Returns a structured result so the caller can update status/UI.
    /// </summary>
    Task<ChartPrintResult> PrintAsync();
}

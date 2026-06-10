using RollerGraph.App.Charting;
using RollerGraph.App.Printing;

namespace RollerGraph.App.Services;

/// <summary>
/// <see cref="IChartPrinter"/> implementation that snapshots the chart to a
/// temp PNG then delegates to an <see cref="IPrintLauncher"/> for the
/// OS-specific print handoff. Composition over hard-coded branching.
/// </summary>
public sealed class ChartPrinter : IChartPrinter
{
    private const int PrintWidth = 1600;
    private const int PrintHeight = 900;

    private readonly IChartSnapshotter _snapshotter;
    private readonly IPrintLauncher _launcher;

    public ChartPrinter(IChartSnapshotter snapshotter, IPrintLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(snapshotter);
        ArgumentNullException.ThrowIfNull(launcher);
        _snapshotter = snapshotter;
        _launcher = launcher;
    }

    public async Task<ChartPrintResult> PrintAsync()
    {
        try
        {
            var tmpPng = Path.Combine(Path.GetTempPath(),
                $"rollergraph-print-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            _snapshotter.SaveAsPng(tmpPng, PrintWidth, PrintHeight);

            if (!_launcher.IsSupported)
            {
                return new ChartPrintResult(ChartPrintOutcome.SnapshotOnly, $"Saved snapshot to {tmpPng}");
            }
            return await _launcher.LaunchAsync(tmpPng);
        }
        catch (Exception ex)
        {
            return new ChartPrintResult(ChartPrintOutcome.Failed, "Print failed", ex.Message);
        }
    }
}

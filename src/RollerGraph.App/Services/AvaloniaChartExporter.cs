using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RollerGraph.App.Charting;

namespace RollerGraph.App.Services;

/// <summary>
/// <see cref="IChartExporter"/> implementation backed by Avalonia's file
/// picker. Delegates the actual PNG rendering to an
/// <see cref="IChartSnapshotter"/>, keeping the file-picker UI separate
/// from the chart library.
/// </summary>
public sealed class AvaloniaChartExporter : IChartExporter
{
    private readonly Window _owner;
    private readonly IChartSnapshotter _snapshotter;
    private readonly Func<(int Width, int Height)> _sizeProvider;
    private readonly Func<ChartSnapshotStats?>? _statsProvider;

    /// <param name="owner">Owner window used to host the file-save picker.</param>
    /// <param name="snapshotter">Renders the actual PNG bytes.</param>
    /// <param name="sizeProvider">Returns the pixel dimensions to render with.</param>
    public AvaloniaChartExporter(
        Window owner,
        IChartSnapshotter snapshotter,
        Func<(int Width, int Height)> sizeProvider,
        Func<ChartSnapshotStats?>? statsProvider = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(snapshotter);
        ArgumentNullException.ThrowIfNull(sizeProvider);
        _owner = owner;
        _snapshotter = snapshotter;
        _sizeProvider = sizeProvider;
        _statsProvider = statsProvider;
    }

    public async Task<ChartExportResult> ExportPngAsync()
    {
        var storage = _owner.StorageProvider;
        if (storage is null) return new ChartExportResult(ChartExportOutcome.Cancelled);

        var suggested = $"rollergraph-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export chart as PNG",
            SuggestedFileName = suggested,
            DefaultExtension = "png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } },
            },
        });
        if (file is null) return new ChartExportResult(ChartExportOutcome.Cancelled);

        try
        {
            var (w, h) = _sizeProvider();
            _snapshotter.SaveAsPng(file.Path.LocalPath, w, h, _statsProvider?.Invoke());
            return new ChartExportResult(ChartExportOutcome.Saved, FilePath: file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            return new ChartExportResult(ChartExportOutcome.Failed, ErrorMessage: ex.Message);
        }
    }
}

using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.SKCharts;

namespace RollerGraph.App.Charting;

/// <summary>
/// <see cref="IChartSnapshotter"/> implementation backed by LiveChartsCore's
/// <see cref="SKCartesianChart"/>. The single place in the app that knows
/// how to turn a live <see cref="CartesianChart"/> into a PNG file.
/// </summary>
public sealed class LiveChartsChartSnapshotter : IChartSnapshotter
{
    private readonly CartesianChart _chart;

    public LiveChartsChartSnapshotter(CartesianChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        _chart = chart;
    }

    public void SaveAsPng(string destinationPath, int width, int height)
    {
        var skChart = new SKCartesianChart(_chart)
        {
            Width = width,
            Height = height,
        };
        skChart.SaveImage(destinationPath);
    }
}

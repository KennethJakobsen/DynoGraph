namespace RollerGraph.App.Charting;

/// <summary>
/// Renders the current on-screen chart to a PNG file. Implementations are
/// allowed to depend on the concrete chart library (e.g. LiveChartsCore's
/// SK chart renderer); consumers depend only on this interface.
/// </summary>
public interface IChartSnapshotter
{
    /// <summary>
    /// Render the chart to a PNG at <paramref name="destinationPath"/>.
    /// </summary>
    /// <param name="destinationPath">Absolute file path to write to.</param>
    /// <param name="width">Pixel width of the rendered image.</param>
    /// <param name="height">Pixel height of the rendered image.</param>
    void SaveAsPng(string destinationPath, int width, int height);
}

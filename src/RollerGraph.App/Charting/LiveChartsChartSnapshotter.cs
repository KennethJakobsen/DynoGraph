using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;
using System.Globalization;

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

    public void SaveAsPng(string destinationPath, int width, int height, ChartSnapshotStats? stats = null)
    {
        if (stats is not { } peakStats)
        {
            SaveChartImage(destinationPath, width, height);
            return;
        }

        var statsWidth = Math.Min(280, Math.Max(220, width / 4));
        var chartWidth = Math.Max(1, width - statsWidth);
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"rollergraph-chart-{Guid.NewGuid():N}.png");

        try
        {
            SaveChartImage(tempPath, chartWidth, height);
            using var chartBitmap = SKBitmap.Decode(tempPath);
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(chartBitmap, 0, 0);
            DrawPeakStats(canvas, peakStats, chartWidth, 0, statsWidth, height);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Open(destinationPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore cleanup */ }
        }
    }

    private void SaveChartImage(string destinationPath, int width, int height)
    {
        var skChart = new SKCartesianChart(_chart)
        {
            Width = width,
            Height = height,
        };
        skChart.SaveImage(destinationPath);
    }

    private static void DrawPeakStats(SKCanvas canvas, ChartSnapshotStats stats, int x, int y, int width, int height)
    {
        using var background = new SKPaint { Color = new SKColor(0xF2, 0xF2, 0xF2), IsAntialias = true };
        using var border = new SKPaint { Color = new SKColor(0xC8, 0xC8, 0xC8), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        var rect = new SKRect(x + 8, y + 8, x + width - 8, y + height - 8);
        canvas.DrawRoundRect(rect, 6, 6, background);
        canvas.DrawRoundRect(rect, 6, 6, border);

        using var regularTypeface = SKTypeface.FromFamilyName("Arial");
        using var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var labelFont = new SKFont(boldTypeface, 18);
        using var smallFont = new SKFont(regularTypeface, 16);
        using var hpFont = new SKFont(boldTypeface, 34);
        using var nmFont = new SKFont(boldTypeface, 30);
        using var valueFont = new SKFont(boldTypeface, 30);
        using var label = new SKPaint { Color = new SKColor(0x55, 0x55, 0x55), IsAntialias = true };
        using var small = new SKPaint { Color = new SKColor(0x55, 0x55, 0x55), IsAntialias = true };
        using var hp = new SKPaint { Color = new SKColor(0xFF, 0x8A, 0x00), IsAntialias = true };
        using var nm = new SKPaint { Color = new SKColor(0x00, 0xB8, 0xA9), IsAntialias = true };
        using var value = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var line = new SKPaint { Color = new SKColor(0xD0, 0xD0, 0xD0), StrokeWidth = 1 };

        var culture = CultureInfo.CurrentCulture;
        var left = rect.Left + 22;
        var top = rect.Top + 34;
        DrawText(canvas, "PEAK STATS", left, top, labelFont, label);

        top += 50;
        DrawStat(canvas, "Peak HP", stats.PeakHp.ToString("F1", culture), "HP", $"@ {stats.PeakHpSpeed:F0} km/h", left, ref top, hpFont, hp, smallFont, small);
        canvas.DrawLine(left, top + 8, rect.Right - 22, top + 8, line);

        top += 44;
        DrawStat(canvas, "Peak NM", stats.PeakNm.ToString("F1", culture), "NM", $"@ {stats.PeakNmSpeed:F0} km/h", left, ref top, nmFont, nm, smallFont, small);
        canvas.DrawLine(left, top + 8, rect.Right - 22, top + 8, line);

        top += 44;
        DrawStat(canvas, "Peak Speed", stats.PeakSpeed.ToString("F1", culture), "km/h", "", left, ref top, valueFont, value, smallFont, small);
    }

    private static void DrawStat(
        SKCanvas canvas,
        string title,
        string primary,
        string unit,
        string sub,
        float left,
        ref float top,
        SKFont primaryFont,
        SKPaint primaryPaint,
        SKFont smallFont,
        SKPaint smallPaint)
    {
        DrawText(canvas, title, left, top, smallFont, smallPaint);
        top += 34;
        DrawText(canvas, primary, left, top, primaryFont, primaryPaint);
        var unitX = left + primaryFont.MeasureText(primary) + 8;
        DrawText(canvas, unit, unitX, top - 2, smallFont, smallPaint);
        if (!string.IsNullOrWhiteSpace(sub))
        {
            top += 24;
            DrawText(canvas, sub, left, top, smallFont, smallPaint);
        }
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }
}

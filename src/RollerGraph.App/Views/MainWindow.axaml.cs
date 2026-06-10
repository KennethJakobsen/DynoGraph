using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.SKCharts;
using RollerGraph.App.ViewModels;

namespace RollerGraph.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnReplayClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var storage = StorageProvider;
        if (storage is null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open RollerGraph CSV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV files") { Patterns = new[] { "*.csv" } },
                FilePickerFileTypes.All,
            },
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        await vm.ReplayAsync(file.Path.LocalPath);
    }

    private async void OnExportPngClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var storage = StorageProvider;
        if (storage is null) return;

        var chart = this.FindControl<CartesianChart>("ChartHost");
        if (chart is null) return;

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

        if (file is null) return;

        try
        {
            var skChart = new SKCartesianChart(chart)
            {
                Width = (int)Math.Max(800, chart.Bounds.Width),
                Height = (int)Math.Max(450, chart.Bounds.Height),
            };
            skChart.SaveImage(file.Path.LocalPath);
            vm.StatusMessage = $"Exported {System.IO.Path.GetFileName(file.Path.LocalPath)}";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private async void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var dialog = new SettingsWindow
        {
            DataContext = new SettingsViewModel(vm.Settings),
        };
        await dialog.ShowDialog(this);
        if (dialog.Result is not null)
            vm.ApplySettings(dialog.Result);
    }
}

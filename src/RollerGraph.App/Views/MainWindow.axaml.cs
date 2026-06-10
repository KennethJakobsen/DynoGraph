using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.SKCharts;
using RollerGraph.App.Services;
using RollerGraph.App.ViewModels;
using RollerGraph.Core.Models;

namespace RollerGraph.App.Views;

public partial class MainWindow : Window, IMainWindowInteractor
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Interactor = this;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Focus-aware Space/Enter handling: only fire when no text input has focus.
        if (e.KeyModifiers != KeyModifiers.None) return;

        var focused = FocusManager?.GetFocusedElement();
        var inTextInput = focused is TextBox or NumericUpDown;
        if (inTextInput) return;

        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Space)
        {
            vm.SmoothingEnabled = !vm.SmoothingEnabled;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _ = PrintAsync();
            e.Handled = true;
        }
    }

    // -------- Saved Run row handlers --------

    private async void OnSavedRunVisibilityToggled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is CheckBox cb && cb.DataContext is SavedRunViewModel run)
        {
            // The CheckBox has already updated run.IsVisible via two-way binding.
            vm.ToggleSavedRunVisibility(run);
        }
        await Task.CompletedTask;
    }

    private async void OnSavedRunRenameClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is Button btn && btn.DataContext is SavedRunViewModel run)
        {
            var dialog = new RunNameDialog
            {
                Title = $"Rename \"{run.Name}\"",
                SuggestedName = run.Name,
            };
            await dialog.ShowDialog(this);
            if (!string.IsNullOrWhiteSpace(dialog.Result))
            {
                if (!vm.RenameSavedRun(run, dialog.Result))
                    vm.StatusMessage = "Rename failed - that name is already used.";
            }
        }
    }

    private void OnSavedRunDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is Button btn && btn.DataContext is SavedRunViewModel run)
        {
            vm.DeleteSavedRun(run);
        }
    }

    // -------- IMainWindowInteractor --------

    public async Task<string?> PickReplayCsvAsync()
    {
        var storage = StorageProvider;
        if (storage is null) return null;
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
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> PickSavedRunCsvAsync()
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load CSV as Saved Run",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV files") { Patterns = new[] { "*.csv" } },
                FilePickerFileTypes.All,
            },
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> AskForRunNameAsync(string suggested)
    {
        var dlg = new RunNameDialog { SuggestedName = suggested };
        await dlg.ShowDialog(this);
        return dlg.Result;
    }

    public async Task<bool> ConfirmOverwriteAsync(string runName)
    {
        var dlg = new ConfirmDialog
        {
            Title = "Overwrite saved run?",
            Message = $"A run named \"{runName}\" already exists. Overwrite it with the current data?",
            ConfirmText = "Overwrite",
            CancelText = "Cancel",
        };
        await dlg.ShowDialog(this);
        return dlg.Confirmed;
    }

    public async Task<Settings?> ShowSettingsAsync(Settings current)
    {
        var dialog = new SettingsWindow { DataContext = new SettingsViewModel(current) };
        await dialog.ShowDialog(this);
        return dialog.Result;
    }

    public async Task ExportPngAsync()
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
            vm.StatusMessage = $"Exported {Path.GetFileName(file.Path.LocalPath)}";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    public async Task PrintAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var chart = this.FindControl<CartesianChart>("ChartHost");
        if (chart is null) return;

        try
        {
            // 1) Render the current chart + peak-stats panel to a temp PNG.
            var tmpPng = Path.Combine(Path.GetTempPath(),
                $"rollergraph-print-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            var skChart = new SKCartesianChart(chart)
            {
                Width = 1600,
                Height = 900,
            };
            skChart.SaveImage(tmpPng);

            // 2) Hand off to the OS print pipeline.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"-a Preview \"{tmpPng}\"");
                vm.StatusMessage = "Opened in Preview - use Cmd+P to print";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var psi = new ProcessStartInfo("xdg-open", tmpPng) { UseShellExecute = false };
                Process.Start(psi);
                vm.StatusMessage = "Opened in default viewer - use Ctrl+P to print";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Try direct print via shell verb; fall back to opening the file.
                var psi = new ProcessStartInfo
                {
                    FileName = tmpPng,
                    Verb = "print",
                    UseShellExecute = true,
                };
                try { Process.Start(psi); vm.StatusMessage = "Sent to default printer"; }
                catch
                {
                    Process.Start(new ProcessStartInfo { FileName = tmpPng, UseShellExecute = true });
                    vm.StatusMessage = "Opened in default viewer - use Ctrl+P to print";
                }
            }
            else
            {
                vm.StatusMessage = $"Saved snapshot to {tmpPng}";
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Print failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LiveChartsCore.SkiaSharpView.Avalonia;
using RollerGraph.App.Charting;
using RollerGraph.App.Printing;
using RollerGraph.App.Services;
using RollerGraph.App.ViewModels;

namespace RollerGraph.App.Views;

/// <summary>
/// Main window code-behind. Responsibilities are intentionally narrow:
///   - construct the small, ISP-friendly view services and inject them
///     into the view-model;
///   - translate keyboard shortcuts to VM commands;
///   - dispatch saved-run row clicks to the right VM method.
/// All file-picker, dialog, chart-snapshot and OS print logic lives in
/// dedicated classes under <c>Services/</c>, <c>Charting/</c> and
/// <c>Printing/</c>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // File pickers and dialogs need a host window; wire them now that we have one.
        vm.FilePicker = new AvaloniaCsvFilePicker(this);
        vm.RunNamePrompt = new AvaloniaRunNamePrompt(this);
        vm.ConfirmPrompt = new AvaloniaConfirmPrompt(this);
        vm.SettingsDialog = new AvaloniaSettingsDialog(this);

        // Chart export + print are composed from a snapshotter + a launcher.
        var chartControl = this.FindControl<CartesianChart>("ChartHost");
        if (chartControl is not null)
        {
            var snapshotter = new LiveChartsChartSnapshotter(chartControl);
            vm.ChartExporter = new AvaloniaChartExporter(
                this,
                snapshotter,
                () => (
                    (int)Math.Max(800, chartControl.Bounds.Width),
                    (int)Math.Max(450, chartControl.Bounds.Height)));
            vm.ChartPrinter = new ChartPrinter(snapshotter, PlatformPrintLauncher.Default());
        }
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
            if (vm.PrintCommand.CanExecute(null))
                _ = vm.PrintCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    // -------- Saved Run row handlers --------

    private void OnSavedRunVisibilityToggled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is CheckBox cb && cb.DataContext is SavedRunViewModel run)
        {
            // The CheckBox has already updated run.IsVisible via two-way binding.
            vm.ToggleSavedRunVisibility(run);
        }
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
}

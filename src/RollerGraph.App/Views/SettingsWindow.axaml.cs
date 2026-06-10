using Avalonia.Controls;
using Avalonia.Interactivity;
using RollerGraph.App.ViewModels;
using RollerGraph.Core.Models;

namespace RollerGraph.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// When the user clicks Save, this is populated with the new settings;
    /// null if the dialog was cancelled or closed.
    /// </summary>
    public Settings? Result { get; private set; }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            Result = vm.ToSettings();
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RollerGraph.App.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() { InitializeComponent(); }

    public string Message
    {
        get => MessageText.Text ?? "";
        set => MessageText.Text = value;
    }

    public string ConfirmText
    {
        get => (ConfirmButton.Content as string) ?? "OK";
        set => ConfirmButton.Content = value;
    }

    public string CancelText
    {
        get => (CancelButton.Content as string) ?? "Cancel";
        set => CancelButton.Content = value;
    }

    public bool Confirmed { get; private set; }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}

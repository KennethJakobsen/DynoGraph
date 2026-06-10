using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RollerGraph.App.Views;

public partial class RunNameDialog : Window
{
    public RunNameDialog()
    {
        InitializeComponent();
    }

    /// <summary>Final result; null if cancelled.</summary>
    public string? Result { get; private set; }

    public string SuggestedName
    {
        get => NameBox.Text ?? "";
        set => NameBox.Text = value;
    }

    public string? ErrorMessage
    {
        set
        {
            ErrorText.Text = value;
            ErrorText.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var t = NameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "Please enter a name.";
            return;
        }
        Result = t;
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using RollerGraph.Core.Models;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// UI wrapper around a <see cref="SavedRun"/>. Mutations made here are pushed
/// back to the on-disk store + the chart by <see cref="MainWindowViewModel"/>.
/// </summary>
public sealed partial class SavedRunViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _color;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private int _sampleCount;

    public SavedRun Source { get; private set; }

    public SavedRunViewModel(SavedRun source)
    {
        Source = source;
        _name = source.Name;
        _color = source.Color;
        _isVisible = source.IsVisible;
        _sampleCount = source.Samples.Count;
    }

    /// <summary>Replaces the underlying <see cref="SavedRun"/> and re-syncs observable props.</summary>
    public void Update(SavedRun source)
    {
        Source = source;
        Name = source.Name;
        Color = source.Color;
        IsVisible = source.IsVisible;
        SampleCount = source.Samples.Count;
    }
}

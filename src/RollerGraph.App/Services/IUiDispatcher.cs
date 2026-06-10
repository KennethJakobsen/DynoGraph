using Avalonia.Threading;

namespace RollerGraph.App.Services;

/// <summary>
/// Abstraction over Avalonia's UI dispatcher so view-models stay testable.
/// </summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

/// <summary>Avalonia-backed implementation that marshals to the UI thread.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}

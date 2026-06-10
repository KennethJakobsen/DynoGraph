using RollerGraph.App.Services;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>
/// Synchronous test dispatcher: invokes posted actions inline so tests don't
/// have to deal with thread marshalling.
/// </summary>
internal sealed class SyncUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

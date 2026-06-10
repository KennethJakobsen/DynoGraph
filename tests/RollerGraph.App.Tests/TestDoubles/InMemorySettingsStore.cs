using RollerGraph.Core.Models;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>In-memory <see cref="ISettingsStore"/> for tests.</summary>
internal sealed class InMemorySettingsStore : ISettingsStore
{
    public Settings Current { get; set; } = new();

    public int LoadCount { get; private set; }
    public int SaveCount { get; private set; }

    public Settings Load()
    {
        LoadCount++;
        return Current;
    }

    public void Save(Settings settings)
    {
        SaveCount++;
        Current = settings;
    }
}

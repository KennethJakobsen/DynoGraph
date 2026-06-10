using RollerGraph.Core.Models;
using RollerGraph.Core.Storage;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>In-memory <see cref="ISavedRunStore"/> for tests.</summary>
internal sealed class FakeSavedRunStore : ISavedRunStore
{
    private readonly Dictionary<string, SavedRun> _runs = new();

    public IReadOnlyList<SavedRun> LoadAll() =>
        _runs.Values.OrderBy(r => r.CreatedUtc).ToList();

    public string Save(SavedRun run)
    {
        var key = RunSlugger.Slugify(run.Name);
        _runs[key] = run;
        return key;
    }

    public bool Delete(string name)
    {
        var key = RunSlugger.Slugify(name);
        return _runs.Remove(key);
    }

    public bool Exists(string name) => _runs.ContainsKey(RunSlugger.Slugify(name));
}

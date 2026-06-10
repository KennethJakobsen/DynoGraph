using RollerGraph.Core.Models;

namespace RollerGraph.Core.Storage;

/// <summary>
/// Abstraction over a persistent <see cref="SavedRun"/> store. Allows the App
/// layer to depend on a stable contract and lets tests substitute an
/// in-memory implementation without touching the file system.
/// </summary>
public interface ISavedRunStore
{
    /// <summary>
    /// Loads every saved run, ordered by <see cref="SavedRun.CreatedUtc"/>.
    /// Corrupt or unreadable entries are silently skipped.
    /// </summary>
    IReadOnlyList<SavedRun> LoadAll();

    /// <summary>Persists (or replaces) the given run. Returns a store-defined identifier.</summary>
    string Save(SavedRun run);

    /// <summary>Deletes the run with the given name. Returns false if it was not found.</summary>
    bool Delete(string name);

    /// <summary>Returns true when a run with a name slug-equivalent to <paramref name="name"/> exists.</summary>
    bool Exists(string name);
}

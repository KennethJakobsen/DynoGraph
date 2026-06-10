namespace RollerGraph.Core.Models;

/// <summary>
/// Abstraction over a persistent <see cref="Settings"/> store. Consumers in
/// the App layer depend on this rather than a concrete file-backed store so
/// they can be unit-tested with an in-memory implementation.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the currently-persisted settings. Implementations should never
    /// throw for IO/parse errors; they should return defaults instead so the
    /// app can always start.
    /// </summary>
    Settings Load();

    /// <summary>
    /// Persists the given settings. Implementations may throw on hard IO
    /// failures so the caller can surface the problem.
    /// </summary>
    void Save(Settings settings);
}

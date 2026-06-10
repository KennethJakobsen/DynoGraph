namespace RollerGraph.Core.Serial;

/// <summary>
/// Outcome of an <see cref="IPortEnumerator.EnumeratePorts"/> call.
/// </summary>
public sealed record PortEnumerationResult
{
    /// <summary>Empty list when enumeration failed.</summary>
    public IReadOnlyList<string> Ports { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Non-null when the underlying driver threw - usually a missing native
    /// library or a permission problem. Consumers should surface this so
    /// users are not staring at an empty dropdown wondering why.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when ports could be enumerated (even if zero ports exist).</summary>
    public bool Succeeded => ErrorMessage is null;

    /// <summary>Convenience: a successful empty enumeration.</summary>
    public static PortEnumerationResult Empty { get; } = new();

    /// <summary>Convenience constructor for a successful enumeration.</summary>
    public static PortEnumerationResult Success(IReadOnlyList<string> ports) =>
        new() { Ports = ports };

    /// <summary>Convenience constructor for a failed enumeration.</summary>
    public static PortEnumerationResult Failure(string message) =>
        new() { ErrorMessage = message };
}

/// <summary>
/// Enumerates serial ports visible on the current system. Exists so consumers
/// can be unit-tested without depending on a concrete driver implementation.
/// </summary>
public interface IPortEnumerator
{
    /// <summary>
    /// Returns the names of all serial ports the OS reports, or an error
    /// when the underlying driver could not be loaded / queried.
    /// </summary>
    PortEnumerationResult EnumeratePorts();
}

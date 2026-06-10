namespace RollerGraph.Core.Serial;

/// <summary>
/// Enumerates serial ports visible on the current system. Exists so consumers
/// can be unit-tested without depending on a concrete driver implementation.
/// </summary>
public interface IPortEnumerator
{
    /// <summary>Returns the names of all serial ports the OS reports.</summary>
    IReadOnlyList<string> EnumeratePorts();
}

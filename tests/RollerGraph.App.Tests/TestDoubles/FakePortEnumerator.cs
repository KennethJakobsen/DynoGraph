using RollerGraph.Core.Serial;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>Static <see cref="IPortEnumerator"/> for tests.</summary>
internal sealed class FakePortEnumerator : IPortEnumerator
{
    private readonly PortEnumerationResult _result;

    public FakePortEnumerator(params string[] ports)
        : this(PortEnumerationResult.Success(ports)) { }

    public FakePortEnumerator(PortEnumerationResult result)
    {
        _result = result;
    }

    /// <summary>Convenience: a fake that simulates a driver failure.</summary>
    public static FakePortEnumerator Failing(string message) =>
        new(PortEnumerationResult.Failure(message));

    public PortEnumerationResult EnumeratePorts() => _result;
}

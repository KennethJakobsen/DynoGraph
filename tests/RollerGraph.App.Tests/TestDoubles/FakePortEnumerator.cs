using RollerGraph.Core.Serial;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>Static <see cref="IPortEnumerator"/> for tests.</summary>
internal sealed class FakePortEnumerator : IPortEnumerator
{
    private readonly IReadOnlyList<string> _ports;

    public FakePortEnumerator(params string[] ports) { _ports = ports; }

    public IReadOnlyList<string> EnumeratePorts() => _ports;
}

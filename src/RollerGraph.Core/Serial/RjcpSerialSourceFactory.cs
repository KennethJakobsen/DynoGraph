namespace RollerGraph.Core.Serial;

/// <summary>
/// Default <see cref="IPortEnumerator"/> + <see cref="ISerialSourceFactory"/>
/// backed by <see cref="RjcpSerialSource"/> for live ports and
/// <see cref="ReplaySerialSource"/> for CSV replay.
/// </summary>
public sealed class RjcpSerialSourceFactory : ISerialSourceFactory, IPortEnumerator
{
    public ISerialSource CreateForPort(string portName, int baudRate) =>
        new RjcpSerialSource(portName, baudRate);

    public ISerialSource CreateFromCsvFile(string filePath, TimeSpan? interval = null) =>
        ReplaySerialSource.FromFile(filePath, interval);

    public IReadOnlyList<string> EnumeratePorts() => RjcpSerialSource.EnumeratePorts();
}

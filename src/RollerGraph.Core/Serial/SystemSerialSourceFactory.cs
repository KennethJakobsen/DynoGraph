namespace RollerGraph.Core.Serial;

/// <summary>
/// Default <see cref="IPortEnumerator"/> + <see cref="ISerialSourceFactory"/>
/// backed by <see cref="SystemSerialSource"/> for live ports and
/// <see cref="ReplaySerialSource"/> for CSV replay.
/// </summary>
public sealed class SystemSerialSourceFactory : ISerialSourceFactory, IPortEnumerator
{
    public ISerialSource CreateForPort(string portName, int baudRate) =>
        new SystemSerialSource(portName, baudRate);

    public ISerialSource CreateFromCsvFile(string filePath, TimeSpan? interval = null) =>
        ReplaySerialSource.FromFile(filePath, interval);

    public PortEnumerationResult EnumeratePorts() => SystemSerialSource.EnumeratePorts();
}

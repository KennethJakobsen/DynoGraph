using RollerGraph.Core.Serial;

namespace RollerGraph.App.Tests.TestDoubles;

/// <summary>
/// <see cref="ISerialSourceFactory"/> that returns a pre-created fake source.
/// Records the most recent call so tests can assert on the parameters used.
/// </summary>
internal sealed class FakeSerialSourceFactory : ISerialSourceFactory
{
    private readonly FakeSerialSource _liveSource;
    private readonly FakeSerialSource _replaySource;

    public string? LastPortName { get; private set; }
    public int? LastBaudRate { get; private set; }
    public string? LastReplayPath { get; private set; }

    public FakeSerialSourceFactory()
    {
        _liveSource = new FakeSerialSource();
        _replaySource = new FakeSerialSource();
    }

    public FakeSerialSource LiveSource => _liveSource;
    public FakeSerialSource ReplaySource => _replaySource;

    public ISerialSource CreateForPort(string portName, int baudRate)
    {
        LastPortName = portName;
        LastBaudRate = baudRate;
        return _liveSource;
    }

    public ISerialSource CreateFromCsvFile(string filePath, TimeSpan? interval = null)
    {
        LastReplayPath = filePath;
        return _replaySource;
    }
}

namespace RollerGraph.Core.Serial;

/// <summary>
/// Creates <see cref="ISerialSource"/> instances. The factory pattern lets
/// consumers swap in a fake (e.g. <see cref="ReplaySerialSource"/> for
/// tests) without depending on the concrete driver type.
/// </summary>
public interface ISerialSourceFactory
{
    /// <summary>Creates a live serial source bound to the named port.</summary>
    /// <param name="portName">OS-specific port name (e.g. "COM3" or "/dev/cu.usbserial-1410").</param>
    /// <param name="baudRate">Baud rate to open the port with.</param>
    ISerialSource CreateForPort(string portName, int baudRate);

    /// <summary>Creates a replay source from a CSV file on disk.</summary>
    /// <param name="filePath">Path to the CSV file to replay.</param>
    /// <param name="interval">Delay between emitted lines; null lets the implementation pick a default.</param>
    ISerialSource CreateFromCsvFile(string filePath, TimeSpan? interval = null);
}

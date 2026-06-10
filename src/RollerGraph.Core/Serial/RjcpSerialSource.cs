using System.Text;
using RJCP.IO.Ports;

namespace RollerGraph.Core.Serial;

/// <summary>
/// <see cref="ISerialSource"/> backed by RJCP.IO.Ports.SerialPortStream.
/// Cross-platform USB serial implementation (Windows COM*, macOS /dev/cu.*, Linux /dev/ttyUSB*).
/// </summary>
public sealed class RjcpSerialSource : ISerialSource
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly LineBuffer _buffer = new();
    private SerialPortStream? _port;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;
    private readonly object _lock = new();

    public RjcpSerialSource(string portName, int baudRate = 19200)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name is required.", nameof(portName));
        if (baudRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(baudRate));

        _portName = portName;
        _baudRate = baudRate;
    }

    public event EventHandler<LineReceivedEventArgs>? LineReceived;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _port?.IsOpen == true;
            }
        }
    }

    /// <summary>
    /// Enumerates serial ports detected on the current system.
    /// </summary>
    public static IReadOnlyList<string> EnumeratePorts()
    {
        try
        {
            return SerialPortStream.GetPortNames();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_port?.IsOpen == true)
                return Task.CompletedTask;

            _port = new SerialPortStream(_portName, _baudRate, 8, Parity.None, StopBits.One)
            {
                Encoding = Encoding.ASCII,
                ReadTimeout = 500,
                NewLine = "\n",
            };

            _port.Open();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        Task? loop;
        SerialPortStream? port;
        lock (_lock)
        {
            cts = _cts;
            loop = _readLoop;
            port = _port;
            _cts = null;
            _readLoop = null;
            _port = null;
        }

        if (cts is not null)
        {
            try { cts.Cancel(); } catch { /* ignore */ }
        }

        if (loop is not null)
        {
            try { await loop.WaitAsync(cancellationToken).ConfigureAwait(false); } catch { /* ignore */ }
        }

        if (port is not null)
        {
            try { if (port.IsOpen) port.Close(); } catch { /* ignore */ }
            port.Dispose();
        }

        cts?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var read = new char[1024];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int n;
                try
                {
                    n = _port!.Read(read, 0, read.Length);
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (n <= 0)
                {
                    await Task.Delay(10, token).ConfigureAwait(false);
                    continue;
                }

                var now = DateTime.UtcNow;
                foreach (var line in _buffer.Append(read.AsSpan(0, n)))
                {
                    LineReceived?.Invoke(this, new LineReceivedEventArgs(line, now));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }
    }
}

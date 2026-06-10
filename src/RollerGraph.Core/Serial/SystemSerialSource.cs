using System.IO.Ports;
using System.Text;

namespace RollerGraph.Core.Serial;

/// <summary>
/// <see cref="ISerialSource"/> backed by <see cref="SerialPort"/> from
/// <c>System.IO.Ports</c>. Cross-platform: Windows (<c>COM*</c>),
/// macOS (<c>/dev/cu.*</c>), and Linux (<c>/dev/ttyUSB*</c>, <c>/dev/ttyACM*</c>).
/// </summary>
public sealed class SystemSerialSource : ISerialSource
{
    // Tuneables. Kept as named constants so the rationale lives next to the
    // value instead of inline magic numbers in the read loop.
    private const int ReadTimeoutMs = 500;        // Per-Read timeout - balance between responsiveness and CPU.
    private const int ReadBufferSize = 1024;      // Bytes pulled per Read call.
    private const int IdlePollDelayMs = 10;       // Pause when no bytes available; avoids tight-loop CPU burn.

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly LineBuffer _buffer = new();
    private readonly object _lock = new();
    private SerialPort? _port;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    public SystemSerialSource(string portName, int baudRate = 19200)
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
    /// Enumerates serial ports detected on the current system. Returns a
    /// <see cref="PortEnumerationResult"/> so callers can distinguish
    /// "no ports plugged in" from "driver failed to load".
    /// </summary>
    public static PortEnumerationResult EnumeratePorts()
    {
        try
        {
            return PortEnumerationResult.Success(SerialPort.GetPortNames());
        }
        catch (Exception ex)
        {
            return PortEnumerationResult.Failure($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_port?.IsOpen == true)
                return Task.CompletedTask;

            _port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
            {
                Encoding = Encoding.ASCII,
                ReadTimeout = ReadTimeoutMs,
                NewLine = "\n",
                DtrEnable = true,   // Some USB-serial chips need DTR asserted to start data flow.
                RtsEnable = true,
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
        SerialPort? port;
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
            try { cts.Cancel(); } catch { /* CTS already disposed elsewhere */ }
        }

        if (loop is not null)
        {
            try { await loop.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch { /* read loop cancelled or errored - both handled inside */ }
        }

        if (port is not null)
        {
            try { if (port.IsOpen) port.Close(); }
            catch { /* port already gone (cable yanked, etc.) - cleanup is best-effort */ }
            port.Dispose();
        }

        cts?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        // System.IO.Ports.SerialPort exposes a BaseStream that we can read
        // bytes from. We read into a byte buffer, decode ASCII into a char
        // buffer, then feed the line splitter.
        var byteBuffer = new byte[ReadBufferSize];
        var charBuffer = new char[ReadBufferSize];
        var decoder = Encoding.ASCII.GetDecoder();

        try
        {
            while (!token.IsCancellationRequested)
            {
                int byteCount;
                try
                {
                    byteCount = _port!.BaseStream.Read(byteBuffer, 0, byteBuffer.Length);
                }
                catch (TimeoutException)
                {
                    // Expected on quiet lines - just loop.
                    continue;
                }

                if (byteCount <= 0)
                {
                    await Task.Delay(IdlePollDelayMs, token).ConfigureAwait(false);
                    continue;
                }

                var charCount = decoder.GetChars(byteBuffer, 0, byteCount, charBuffer, 0);
                if (charCount <= 0) continue;

                var now = DateTime.UtcNow;
                foreach (var line in _buffer.Append(charBuffer.AsSpan(0, charCount)))
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
        try { StopAsync().GetAwaiter().GetResult(); }
        catch { /* dispose paths must not throw */ }
    }
}

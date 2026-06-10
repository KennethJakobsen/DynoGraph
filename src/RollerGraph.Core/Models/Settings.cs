namespace RollerGraph.Core.Models;

using RollerGraph.Core.Adjustments;

/// <summary>
/// User-configurable application settings.
/// </summary>
public sealed record Settings
{
    /// <summary>Last-used serial port name (e.g. /dev/cu.usbserial-1410 or COM3).</summary>
    public string? LastPortName { get; init; }

    /// <summary>Baud rate. Default 19200 per the dyno spec.</summary>
    public int BaudRate { get; init; } = 19200;

    /// <summary>Samples with speed below this value are dropped before plotting.</summary>
    public double MinSpeedKmh { get; init; } = 5.0;

    /// <summary>
    /// Rolling-average window size applied when the smoothing toggle is on.
    /// 1 disables smoothing entirely; the UI only takes effect when &gt;= 2.
    /// </summary>
    public int SmoothingWindow { get; init; } = 5;

    /// <summary>Initial maximum for the HP (left Y) axis before any growth.</summary>
    public double DefaultHpMax { get; init; } = 10.0;

    /// <summary>Initial maximum for the NM (right Y) axis before any growth.</summary>
    public double DefaultNmMax { get; init; } = 10.0;

    /// <summary>Initial maximum for the speed (X) axis before any growth.</summary>
    public double DefaultSpeedMax { get; init; } = 50.0;

    /// <summary>Adjustment applied to the parsed speed value (default: identity).</summary>
    public ChannelAdjustment SpeedAdjustment { get; init; } = new();

    /// <summary>Adjustment applied to the parsed NM value (default: identity).</summary>
    public ChannelAdjustment NmAdjustment { get; init; } = new();

    /// <summary>
    /// Adjustment applied to the parsed HP value. The parser passes the wire
    /// value through unchanged, so this is also the right place to apply any
    /// unit conversion - e.g. <c>Factor = 0.1</c> if your dyno emits HP*10,
    /// or <c>Expression = "x / 0.92"</c> for a drivetrain-loss correction.
    /// </summary>
    public ChannelAdjustment HpAdjustment { get; init; } = new();
}

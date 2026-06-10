namespace RollerGraph.Core.Models;

/// <summary>
/// A single decoded sample from the dyno.
/// </summary>
/// <param name="SampleNumber">Integer counter per line, incrementing from dyno power-on.</param>
/// <param name="SpeedKmh">Speed in km/h.</param>
/// <param name="Nm">Torque in newton-meters (raw).</param>
/// <param name="Hp">Horsepower as reported by the dyno. Any unit conversion (e.g. an HP*10 wire format) is applied via per-channel adjustments, not the parser.</param>
/// <param name="ReceivedAt">UTC timestamp when the sample was received.</param>
public readonly record struct Sample(
    int SampleNumber,
    double SpeedKmh,
    double Nm,
    double Hp,
    DateTime ReceivedAt);

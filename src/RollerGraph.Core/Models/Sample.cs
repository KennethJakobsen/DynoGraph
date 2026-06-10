namespace RollerGraph.Core.Models;

/// <summary>
/// A single decoded sample from the dyno.
/// </summary>
/// <param name="SampleNumber">Integer counter per line, incrementing from dyno power-on.</param>
/// <param name="SpeedKmh">Speed in km/h.</param>
/// <param name="Nm">Torque in newton-meters (raw).</param>
/// <param name="Hp">Horsepower (already divided by 10 from raw HP*10 field).</param>
/// <param name="ReceivedAt">UTC timestamp when the sample was received.</param>
public readonly record struct Sample(
    int SampleNumber,
    double SpeedKmh,
    double Nm,
    double Hp,
    DateTime ReceivedAt);

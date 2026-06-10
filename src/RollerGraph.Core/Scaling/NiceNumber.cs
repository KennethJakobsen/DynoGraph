namespace RollerGraph.Core.Scaling;

/// <summary>
/// Helpers for snapping axis maxima to "nice" round numbers using the
/// 1 / 2 / 2.5 / 5 step family across powers of ten.
/// </summary>
public static class NiceNumber
{
    private static readonly double[] Steps = { 1.0, 2.0, 2.5, 5.0, 10.0 };

    /// <summary>
    /// Returns the smallest nice number greater than or equal to <paramref name="value"/>.
    /// The result is always positive and snapped to 1/2/2.5/5 * 10^n.
    /// </summary>
    public static double Ceil(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 1.0;

        if (value <= 0)
            return 1.0;

        var exponent = Math.Floor(Math.Log10(value));
        var pow = Math.Pow(10, exponent);
        var normalized = value / pow;

        foreach (var step in Steps)
        {
            // Allow tiny tolerance so e.g. 9.999999 -> 10 doesn't jump to 20.
            if (normalized <= step + 1e-9)
                return step * pow;
        }

        // Fallback (shouldn't be reachable because Steps ends in 10).
        return 10.0 * pow;
    }

    /// <summary>
    /// Computes the next axis maximum given the current maximum and the latest observed value.
    /// Grow-only: never returns less than <paramref name="currentMax"/>.
    /// Adds ~10% headroom and snaps to a nice number when the value comes within 90% of the max.
    /// </summary>
    /// <param name="currentMax">Current axis maximum.</param>
    /// <param name="observedValue">New observed value.</param>
    /// <returns>The next axis maximum.</returns>
    public static double NextAxisMax(double currentMax, double observedValue)
    {
        if (observedValue <= currentMax * 0.9)
            return currentMax;

        var target = observedValue * 1.1;
        var next = Ceil(target);
        return next > currentMax ? next : currentMax;
    }
}

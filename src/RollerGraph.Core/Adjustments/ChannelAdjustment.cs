namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Per-channel value transform.
///
/// When <see cref="Expression"/> is non-empty, the value is passed through the
/// expression as the variable <c>x</c>. Otherwise the linear transform
/// <c>value * Factor + Offset</c> is applied.
///
/// Identity is <c>Factor = 1.0, Offset = 0.0, Expression = null</c>.
/// </summary>
public sealed record ChannelAdjustment
{
    /// <summary>Multiplier applied first (default 1.0 - no scaling).</summary>
    public double Factor { get; init; } = 1.0;

    /// <summary>Added after the factor (default 0.0).</summary>
    public double Offset { get; init; } = 0.0;

    /// <summary>
    /// Optional expression evaluated with <c>x</c> bound to the input value.
    /// When non-empty, overrides <see cref="Factor"/> and <see cref="Offset"/>.
    /// Examples: <c>x * 1.05</c>, <c>x / 0.92 + 1.5</c>, <c>x * 1.85</c> (kW->HP).
    /// </summary>
    public string? Expression { get; init; }

    /// <summary>True when this adjustment is the identity transform.</summary>
    public bool IsIdentity =>
        string.IsNullOrWhiteSpace(Expression) && Factor == 1.0 && Offset == 0.0;

    public static ChannelAdjustment Identity { get; } = new();

    /// <summary>
    /// Builds a fast delegate that applies this adjustment. Throws
    /// <see cref="ExpressionException"/> if the expression is malformed.
    /// </summary>
    public Func<double, double> Compile()
    {
        if (!string.IsNullOrWhiteSpace(Expression))
        {
            var compiled = ExpressionParser.Compile(Expression!);
            return compiled;
        }
        var f = Factor;
        var o = Offset;
        return x => x * f + o;
    }
}

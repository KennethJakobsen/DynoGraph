namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Backward-compatible static facade over <see cref="ExpressionCompiler"/>.
/// Uses the standard math function registry. Prefer constructing an
/// <see cref="ExpressionCompiler"/> directly when you need to extend the
/// supported function set.
/// </summary>
public static class ExpressionParser
{
    private static readonly ExpressionCompiler Default = new();

    /// <summary>Compiles the expression with the default function registry.</summary>
    public static Func<double, double> Compile(string expression) => Default.Compile(expression);

    /// <summary>Validates the expression with the default function registry.</summary>
    public static string? Validate(string? expression) => Default.Validate(expression);
}

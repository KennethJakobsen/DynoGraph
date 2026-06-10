namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Compiles a single-variable arithmetic expression (variable name <c>x</c>)
/// into a <see cref="Func{Double,Double}"/>. Implementations may support
/// different operator/function sets; consumers depend on this abstraction so
/// they remain testable and substitutable.
/// </summary>
public interface IExpressionCompiler
{
    /// <summary>
    /// Compiles the expression. Throws <see cref="ExpressionException"/> on
    /// any tokenisation, parse, or semantic error.
    /// </summary>
    Func<double, double> Compile(string expression);

    /// <summary>
    /// Returns null if the expression is valid (or empty), or a human-readable
    /// error message otherwise. Must not throw.
    /// </summary>
    string? Validate(string? expression);
}

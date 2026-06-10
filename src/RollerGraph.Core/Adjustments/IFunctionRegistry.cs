namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Read-only view over the function set an <see cref="IExpressionCompiler"/>
/// will accept. Exposing this lets the parser stay open for extension
/// (register a new function) but closed for modification (no need to edit
/// the parser source).
/// </summary>
public interface IFunctionRegistry
{
    /// <summary>True when <paramref name="name"/> is a registered function.</summary>
    bool IsFunction(string name);

    /// <summary>Number of arguments the named function expects.</summary>
    int GetArity(string name);

    /// <summary>Invokes the function with the given evaluated argument values.</summary>
    double Invoke(string name, ReadOnlySpan<double> args);
}

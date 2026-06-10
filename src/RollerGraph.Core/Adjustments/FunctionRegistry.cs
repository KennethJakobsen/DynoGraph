namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Delegate signature for a user-registrable math function. Receives its
/// arguments as a <see cref="ReadOnlySpan{T}"/> so that callers may evaluate
/// them into a stack-allocated buffer without forcing a heap allocation.
/// </summary>
public delegate double MathFunction(ReadOnlySpan<double> args);

/// <summary>
/// Mutable <see cref="IFunctionRegistry"/>. Allows callers to register new
/// functions or replace existing ones without modifying the expression
/// parser. The companion <see cref="StandardMath"/> registry seeds the
/// default function set.
/// </summary>
public sealed class FunctionRegistry : IFunctionRegistry
{
    private readonly Dictionary<string, (MathFunction Fn, int Arity)> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers <paramref name="function"/> under <paramref name="name"/>.
    /// If the name is already present it is replaced.
    /// </summary>
    public FunctionRegistry Register(string name, int arity, MathFunction function)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (arity < 0)
            throw new ArgumentOutOfRangeException(nameof(arity), "Arity must be non-negative.");
        ArgumentNullException.ThrowIfNull(function);
        _entries[name] = (function, arity);
        return this;
    }

    public bool IsFunction(string name) => _entries.ContainsKey(name);

    public int GetArity(string name) =>
        _entries.TryGetValue(name, out var entry)
            ? entry.Arity
            : throw new ExpressionException($"Unknown function '{name}'.");

    public double Invoke(string name, ReadOnlySpan<double> args)
    {
        if (!_entries.TryGetValue(name, out var entry))
            throw new ExpressionException($"Unknown function '{name}'.");
        // The compiler validates arity at parse time; assert here for safety.
        if (args.Length != entry.Arity)
            throw new ExpressionException($"Function '{name}' expects {entry.Arity} argument(s), got {args.Length}.");
        return entry.Fn(args);
    }

    /// <summary>
    /// Returns the registry seeded with RollerGraph's default math functions
    /// (<c>abs sqrt log log10 exp sin cos tan min max pow</c>).
    /// </summary>
    public static FunctionRegistry StandardMath()
    {
        return new FunctionRegistry()
            .Register("abs", 1, a => Math.Abs(a[0]))
            .Register("sqrt", 1, a => Math.Sqrt(a[0]))
            .Register("log", 1, a => Math.Log(a[0]))
            .Register("log10", 1, a => Math.Log10(a[0]))
            .Register("exp", 1, a => Math.Exp(a[0]))
            .Register("sin", 1, a => Math.Sin(a[0]))
            .Register("cos", 1, a => Math.Cos(a[0]))
            .Register("tan", 1, a => Math.Tan(a[0]))
            .Register("min", 2, a => Math.Min(a[0], a[1]))
            .Register("max", 2, a => Math.Max(a[0], a[1]))
            .Register("pow", 2, a => Math.Pow(a[0], a[1]));
    }
}

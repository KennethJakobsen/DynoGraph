namespace RollerGraph.Core.Adjustments;

/// <summary>Thrown when an adjustment expression is malformed.</summary>
public sealed class ExpressionException : Exception
{
    public ExpressionException(string message) : base(message) { }
    public ExpressionException(string message, Exception inner) : base(message, inner) { }
}

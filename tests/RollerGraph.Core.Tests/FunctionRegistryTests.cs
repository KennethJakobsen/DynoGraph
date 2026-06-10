using RollerGraph.Core.Adjustments;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class FunctionRegistryTests
{
    [Fact]
    public void StandardMath_KnowsAllDocumentedFunctions()
    {
        var r = FunctionRegistry.StandardMath();
        foreach (var name in new[] { "abs", "sqrt", "log", "log10", "exp", "sin", "cos", "tan", "min", "max", "pow" })
            r.IsFunction(name).ShouldBeTrue($"expected {name} to be registered");
    }

    [Fact]
    public void Register_AddsNewFunctionWithoutModifyingExistingOnes()
    {
        var r = FunctionRegistry.StandardMath().Register("double", 1, a => a[0] * 2);

        r.IsFunction("double").ShouldBeTrue();
        r.GetArity("double").ShouldBe(1);
        r.IsFunction("abs").ShouldBeTrue();         // existing still present
    }

    [Fact]
    public void Register_OverridesExistingFunction()
    {
        var r = new FunctionRegistry()
            .Register("custom", 1, a => 1.0)
            .Register("custom", 2, a => 2.0);

        r.GetArity("custom").ShouldBe(2);
        var v = r.Invoke("custom", new double[] { 0, 0 });
        v.ShouldBe(2.0);
    }

    [Fact]
    public void Invoke_UnknownFunction_ThrowsExpressionException()
    {
        var r = new FunctionRegistry();
        Should.Throw<ExpressionException>(() => r.Invoke("nope", new double[] { 1 }));
    }

    [Fact]
    public void Invoke_WrongArity_ThrowsExpressionException()
    {
        var r = new FunctionRegistry().Register("one", 1, a => a[0]);
        Should.Throw<ExpressionException>(() => r.Invoke("one", new double[] { 1, 2 }));
    }

    [Fact]
    public void Register_NullOrEmptyName_Throws()
    {
        var r = new FunctionRegistry();
        Should.Throw<ArgumentException>(() => r.Register("", 1, a => 0));
        Should.Throw<ArgumentException>(() => r.Register("  ", 1, a => 0));
    }

    [Fact]
    public void Register_NegativeArity_Throws()
    {
        var r = new FunctionRegistry();
        Should.Throw<ArgumentOutOfRangeException>(() => r.Register("bad", -1, a => 0));
    }
}

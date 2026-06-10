using RollerGraph.Core.Adjustments;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class ExpressionCompilerTests
{
    [Fact]
    public void Compile_WithCustomFunction_CanCallIt()
    {
        var registry = FunctionRegistry.StandardMath()
            .Register("triple", 1, a => a[0] * 3);
        var compiler = new ExpressionCompiler(registry);

        var f = compiler.Compile("triple(x) + 1");
        f(2).ShouldBe(7);
    }

    [Fact]
    public void Compile_WithoutCustomFunction_StillRecognisesStandardOnes()
    {
        var compiler = new ExpressionCompiler();
        compiler.Compile("sqrt(x)")(16).ShouldBe(4);
        compiler.Compile("min(x, 10)")(5).ShouldBe(5);
        compiler.Compile("min(x, 10)")(20).ShouldBe(10);
    }

    [Fact]
    public void Compile_UnknownFunction_GivesParseError()
    {
        var compiler = new ExpressionCompiler();
        Should.Throw<ExpressionException>(() => compiler.Compile("nope(x)"));
    }

    [Fact]
    public void Compile_OverriddenFunction_UsesNewBehaviour()
    {
        // Replace `abs` with a no-op to prove the registry drives dispatch.
        var registry = FunctionRegistry.StandardMath()
            .Register("abs", 1, a => a[0]);
        var compiler = new ExpressionCompiler(registry);

        compiler.Compile("abs(x)")(-5).ShouldBe(-5);
    }

    [Fact]
    public void Compile_TwoArgFunction_PassesBothArgsInOrder()
    {
        var registry = FunctionRegistry.StandardMath()
            .Register("clamp_lower", 2, a => Math.Max(a[0], a[1]));
        var compiler = new ExpressionCompiler(registry);

        compiler.Compile("clamp_lower(x, 10)")(5).ShouldBe(10);
        compiler.Compile("clamp_lower(x, 10)")(20).ShouldBe(20);
    }

    [Fact]
    public void Validate_ReturnsNullOnValidExpression()
    {
        new ExpressionCompiler().Validate("x * 2 + 1").ShouldBeNull();
    }

    [Fact]
    public void Validate_ReturnsErrorMessageOnInvalidExpression()
    {
        new ExpressionCompiler().Validate("x * +").ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ExpressionCompiler(null!));
    }

    [Fact]
    public void Compile_OperatorsAndConstants_StillWork()
    {
        var c = new ExpressionCompiler();
        c.Compile("2 * pi")(0).ShouldBe(2 * Math.PI, 1e-12);
        c.Compile("e ^ x")(1).ShouldBe(Math.E, 1e-12);
        c.Compile("-x")(5).ShouldBe(-5);
        c.Compile("2 ^ x")(8).ShouldBe(256);
    }
}

using RollerGraph.Core.Adjustments;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class ExpressionParserTests
{
    [Theory]
    [InlineData("0", 0, 0)]
    [InlineData("1", 0, 1)]
    [InlineData("1+2", 0, 3)]
    [InlineData("2*3", 0, 6)]
    [InlineData("10/4", 0, 2.5)]
    [InlineData("10 - 3 - 1", 0, 6)] // left-associative subtraction
    [InlineData("2 + 3 * 4", 0, 14)] // precedence
    [InlineData("(2 + 3) * 4", 0, 20)]
    [InlineData("2 ^ 3", 0, 8)]
    [InlineData("2 ^ 3 ^ 2", 0, 512)] // right-assoc: 2^(3^2)=2^9=512
    [InlineData("-3 + 5", 0, 2)]
    [InlineData("- -2", 0, 2)]
    [InlineData("x", 0, 0)]
    [InlineData("x", 7.5, 7.5)]
    [InlineData("x * 2 + 1", 5, 11)]
    [InlineData("x / 0.92", 92, 100)]
    [InlineData("x * 1.05", 100, 105)]
    [InlineData("pow(x, 2)", 4, 16)]
    [InlineData("abs(-x)", 5, 5)]
    [InlineData("sqrt(x)", 16, 4)]
    [InlineData("min(x, 10)", 5, 5)]
    [InlineData("max(x, 10)", 5, 10)]
    [InlineData("pi", 0, Math.PI)]
    [InlineData("e", 0, Math.E)]
    [InlineData("1.5e2", 0, 150)]
    [InlineData("1.5E-1", 0, 0.15)]
    public void Compile_EvaluatesExpressions(string expr, double x, double expected)
    {
        var fn = ExpressionParser.Compile(expr);
        fn(x).ShouldBe(expected, 1e-9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1 +")]
    [InlineData("* 2")]
    [InlineData("(1 + 2")]
    [InlineData("1 + 2)")]
    [InlineData("foo(1)")]
    [InlineData("min(1)")]
    [InlineData("min(1, 2, 3)")]
    [InlineData("y + 1")]
    [InlineData("@")]
    [InlineData("1 1")]
    public void Compile_ThrowsOnMalformedInput(string expr)
    {
        Should.Throw<ExpressionException>(() => ExpressionParser.Compile(expr));
    }

    [Fact]
    public void Validate_ReturnsNullForGoodExpressionOrEmpty()
    {
        ExpressionParser.Validate(null).ShouldBeNull();
        ExpressionParser.Validate("").ShouldBeNull();
        ExpressionParser.Validate("   ").ShouldBeNull();
        ExpressionParser.Validate("x + 1").ShouldBeNull();
    }

    [Fact]
    public void Validate_ReturnsErrorForBadExpression()
    {
        var err = ExpressionParser.Validate("(1 + 2");
        err.ShouldNotBeNull();
    }

    [Fact]
    public void Compile_DivisionByZero_YieldsInfinity()
    {
        var fn = ExpressionParser.Compile("1 / x");
        var r = fn(0);
        double.IsInfinity(r).ShouldBeTrue();
    }

    [Fact]
    public void Compile_CaseInsensitiveFunctionNames()
    {
        ExpressionParser.Compile("ABS(-3)")(0).ShouldBe(3);
        ExpressionParser.Compile("Sqrt(9)")(0).ShouldBe(3);
        ExpressionParser.Compile("PI")(0).ShouldBe(Math.PI);
    }

    [Fact]
    public void Compile_ScientificNotation()
    {
        ExpressionParser.Compile("1e3")(0).ShouldBe(1000);
        ExpressionParser.Compile("2.5E-2")(0).ShouldBe(0.025);
    }
}

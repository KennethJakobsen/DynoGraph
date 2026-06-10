using RollerGraph.Core.Storage;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class RunSluggerTests
{
    [Theory]
    [InlineData("Hello World!", "hello-world")]
    [InlineData("86 nozzle", "86-nozzle")]
    [InlineData("  86  ---  nozzle  ", "86-nozzle")]
    [InlineData("ALL CAPS", "all-caps")]
    [InlineData("trailing!!!", "trailing")]
    [InlineData("!!!leading", "leading")]
    [InlineData("a/b\\c|d", "a-b-c-d")]
    [InlineData("café", "caf")]              // non-ascii letters dropped
    [InlineData("", "run")]
    [InlineData("   ", "run")]
    [InlineData("!!!", "run")]
    [InlineData("---", "run")]
    [InlineData(null, "run")]
    public void Slugify_ProducesExpected(string? input, string expected)
    {
        RunSlugger.Slugify(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("86 Nozzle", "86 nozzle", true)]
    [InlineData("86-NOZZLE", "  86 nozzle  ", true)]
    [InlineData("86", "87", false)]
    [InlineData("", "   ", true)]                  // both fall back to "run"
    [InlineData("only-symbols!", "@@@@", false)]   // first slugs to "only-symbols", second to "run"
    public void AreSameSlug_TrueOnlyForSlugEquivalence(string? a, string? b, bool expected)
    {
        RunSlugger.AreSameSlug(a, b).ShouldBe(expected);
    }

    [Fact]
    public void FallbackSlug_IsConstantExposedForCallers()
    {
        RunSlugger.FallbackSlug.ShouldBe("run");
    }
}

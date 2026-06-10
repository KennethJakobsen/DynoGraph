using RollerGraph.App.Charting;
using Shouldly;
using SkiaSharp;

namespace RollerGraph.App.Tests;

public class ColorToolsTests
{
    [Theory]
    [InlineData("#FF8A00", 0xFF, 0x8A, 0x00)]
    [InlineData("#00B8A9", 0x00, 0xB8, 0xA9)]
    [InlineData("FF0000", 0xFF, 0x00, 0x00)]      // no leading hash
    [InlineData("  #3B82F6  ", 0x3B, 0x82, 0xF6)] // surrounding whitespace
    public void ParseHex_ValidString_ReturnsExpectedColor(string input, byte r, byte g, byte b)
    {
        var c = ColorTools.ParseHex(input);
        c.Red.ShouldBe(r);
        c.Green.ShouldBe(g);
        c.Blue.ShouldBe(b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nothex")]
    [InlineData("#GGGGGG")]
    [InlineData("#FFF")]       // 3-digit shorthand not supported
    [InlineData("#FF8A0000")]  // too long
    public void ParseHex_Invalid_ReturnsFallback(string? input)
    {
        ColorTools.ParseHex(input).ShouldBe(ColorTools.FallbackBlue);
    }

    [Fact]
    public void AdjustBrightness_FactorLessThanOne_Darkens()
    {
        var c = new SKColor(200, 100, 50);
        var darker = ColorTools.AdjustBrightness(c, 0.5);
        darker.Red.ShouldBe((byte)100);
        darker.Green.ShouldBe((byte)50);
        darker.Blue.ShouldBe((byte)25);
    }

    [Fact]
    public void AdjustBrightness_FactorGreaterThanOne_LightensWithClamping()
    {
        var c = new SKColor(200, 100, 50);
        var brighter = ColorTools.AdjustBrightness(c, 2.0);
        brighter.Red.ShouldBe((byte)255);    // clamped
        brighter.Green.ShouldBe((byte)200);
        brighter.Blue.ShouldBe((byte)100);
    }

    [Fact]
    public void AdjustBrightness_NegativeFactor_ClampsToZero()
    {
        var c = new SKColor(100, 100, 100);
        var result = ColorTools.AdjustBrightness(c, -1.0);
        result.Red.ShouldBe((byte)0);
        result.Green.ShouldBe((byte)0);
        result.Blue.ShouldBe((byte)0);
    }
}

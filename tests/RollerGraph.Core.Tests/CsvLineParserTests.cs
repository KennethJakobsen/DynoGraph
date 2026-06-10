using RollerGraph.Core.Parsing;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class CsvLineParserTests
{
    private static readonly DateTime FixedTime = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_ValidLine_ReturnsSampleWithHpDividedByTen()
    {
        // samplenum=123, speed=45.5, nm=78.2, hp*10=1423 -> hp=142.3
        var sample = CsvLineParser.Parse("123,45.5,78.2,1423,NA,NA,NA,NA,NA", FixedTime);

        sample.ShouldNotBeNull();
        sample!.Value.SampleNumber.ShouldBe(123);
        sample.Value.SpeedKmh.ShouldBe(45.5);
        sample.Value.Nm.ShouldBe(78.2);
        sample.Value.Hp.ShouldBe(142.3, 0.0001);
        sample.Value.ReceivedAt.ShouldBe(FixedTime);
    }

    [Fact]
    public void Parse_IntegerHpField_DividesByTen()
    {
        var sample = CsvLineParser.Parse("1,10,20,500,NA,NA,NA,NA,NA", FixedTime);

        sample.ShouldNotBeNull();
        sample!.Value.Hp.ShouldBe(50.0);
    }

    [Fact]
    public void Parse_MinimumFourFields_Succeeds()
    {
        // Only the first 4 fields are required.
        var sample = CsvLineParser.Parse("5,12,34,100", FixedTime);

        sample.ShouldNotBeNull();
        sample!.Value.SampleNumber.ShouldBe(5);
        sample.Value.Hp.ShouldBe(10.0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1,2,3")]              // Only 3 fields
    [InlineData("not_an_int,1,2,3")]   // Bad sample number
    [InlineData("1,bad,2,3")]          // Bad speed
    [InlineData("1,2,bad,3")]          // Bad NM
    [InlineData("1,2,3,bad")]          // Bad HP
    [InlineData("1,,2,3")]             // Empty speed field
    public void Parse_MalformedLine_ReturnsNull(string? line)
    {
        var sample = CsvLineParser.Parse(line, FixedTime);
        sample.ShouldBeNull();
    }

    [Fact]
    public void Parse_LineWithTrailingWhitespace_Succeeds()
    {
        var sample = CsvLineParser.Parse("  10,20,30,400  ", FixedTime);

        sample.ShouldNotBeNull();
        sample!.Value.SampleNumber.ShouldBe(10);
        sample.Value.SpeedKmh.ShouldBe(20.0);
        sample.Value.Nm.ShouldBe(30.0);
        sample.Value.Hp.ShouldBe(40.0);
    }

    [Fact]
    public void Parse_NaInTrailingFields_IsIgnored()
    {
        // NA appears only in fields we don't read.
        var sample = CsvLineParser.Parse("1,2,3,4,NA,NA,NA,NA,NA", FixedTime);
        sample.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_InvariantCulture_AcceptsDotDecimalRegardlessOfHost()
    {
        // Even in a locale that uses comma decimals, parser must accept dot.
        var sample = CsvLineParser.Parse("1,2.5,3.5,45", FixedTime);
        sample.ShouldNotBeNull();
        sample!.Value.SpeedKmh.ShouldBe(2.5);
        sample.Value.Nm.ShouldBe(3.5);
        sample.Value.Hp.ShouldBe(4.5);
    }
}

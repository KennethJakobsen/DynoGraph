using RollerGraph.Core.Models;
using RollerGraph.Core.Storage;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class SavedRunCsvSerializerTests
{
    private static Sample S(int n, double sp, double nm, double hp) =>
        new(n, sp, nm, hp, new DateTime(2025, 1, 1, 12, 0, n, DateTimeKind.Utc));

    [Fact]
    public void Write_Then_Read_RoundTripsAllFields()
    {
        var s = new SavedRunCsvSerializer();
        var run = new SavedRun
        {
            Name = "86 nozzle",
            CreatedUtc = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Color = "#FF8A00",
            IsVisible = false,
            Samples = new[] { S(1, 30, 60, 40), S(2, 40, 70, 55) },
        };

        var sw = new StringWriter();
        s.Write(run, sw);

        using var sr = new StringReader(sw.ToString());
        var loaded = s.Read(sr, fallbackName: "x", fallbackCreatedUtc: DateTime.UnixEpoch);

        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldBe("86 nozzle");
        loaded.CreatedUtc.ShouldBe(run.CreatedUtc);
        loaded.Color.ShouldBe("#FF8A00");
        loaded.IsVisible.ShouldBeFalse();
        loaded.Samples.Count.ShouldBe(2);
        loaded.Samples[0].SampleNumber.ShouldBe(1);
        loaded.Samples[1].Hp.ShouldBe(55);
    }

    [Fact]
    public void Write_EmitsExpectedHeaderConstant()
    {
        var s = new SavedRunCsvSerializer();
        var sw = new StringWriter();
        s.Write(new SavedRun { Name = "x" }, sw);
        sw.ToString().ShouldContain(SavedRunCsvSerializer.Header);
    }

    [Fact]
    public void Read_OnEmptyStream_ReturnsNull()
    {
        var s = new SavedRunCsvSerializer();
        using var sr = new StringReader(string.Empty);
        s.Read(sr, "fallback", DateTime.UtcNow).ShouldBeNull();
    }

    [Fact]
    public void Read_FallsBackToProvidedNameAndDate_WhenMetadataMissing()
    {
        var s = new SavedRunCsvSerializer();
        var csv = $"{SavedRunCsvSerializer.Header}\n1,10,20,30,2025-01-01T00:00:00Z\n";
        using var sr = new StringReader(csv);
        var created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var run = s.Read(sr, "the-name", created);

        run.ShouldNotBeNull();
        run!.Name.ShouldBe("the-name");
        run.CreatedUtc.ShouldBe(created);
        run.Samples.Count.ShouldBe(1);
    }

    [Fact]
    public void Read_PreservesEscapedMetadataNewlines()
    {
        var s = new SavedRunCsvSerializer();
        var run = new SavedRun
        {
            Name = "line1\nline2",
            Samples = new[] { S(1, 10, 20, 30) },
        };
        var sw = new StringWriter();
        s.Write(run, sw);

        using var sr = new StringReader(sw.ToString());
        var loaded = s.Read(sr, "x", DateTime.UtcNow);
        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldBe("line1\nline2");
    }

    [Fact]
    public void ParseSampleLine_HandlesValidAndInvalid()
    {
        SavedRunCsvSerializer.ParseSampleLine("1,30.5,60.0,40.0,2025-01-01T00:00:00Z").ShouldNotBeNull();
        SavedRunCsvSerializer.ParseSampleLine("not,enough").ShouldBeNull();
        SavedRunCsvSerializer.ParseSampleLine("a,b,c,d,e").ShouldBeNull();      // non-numeric
        SavedRunCsvSerializer.ParseSampleLine(null).ShouldBeNull();
        SavedRunCsvSerializer.ParseSampleLine("").ShouldBeNull();
    }
}

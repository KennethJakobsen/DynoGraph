using RollerGraph.App.Printing;
using RollerGraph.App.Services;
using Shouldly;

namespace RollerGraph.App.Tests;

public class PlatformPrintLauncherTests
{
    /// <summary>Test launcher that records the path it was asked to launch.</summary>
    private sealed class RecordingLauncher : IPrintLauncher
    {
        public bool IsSupported { get; init; }
        public ChartPrintResult ResultToReturn { get; init; }
        public string? LaunchedPath { get; private set; }

        public Task<ChartPrintResult> LaunchAsync(string pngFilePath)
        {
            LaunchedPath = pngFilePath;
            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public async Task LaunchAsync_PicksFirstSupportedLauncher()
    {
        var skipped = new RecordingLauncher { IsSupported = false };
        var winner = new RecordingLauncher
        {
            IsSupported = true,
            ResultToReturn = new ChartPrintResult(ChartPrintOutcome.Sent, "ok"),
        };
        var afterWinner = new RecordingLauncher { IsSupported = true };
        var dispatcher = new PlatformPrintLauncher(new IPrintLauncher[] { skipped, winner, afterWinner });

        var r = await dispatcher.LaunchAsync("/tmp/x.png");

        winner.LaunchedPath.ShouldBe("/tmp/x.png");
        afterWinner.LaunchedPath.ShouldBeNull();
        r.Outcome.ShouldBe(ChartPrintOutcome.Sent);
    }

    [Fact]
    public async Task LaunchAsync_NoSupportedLauncher_ReturnsSnapshotOnly()
    {
        var none = new RecordingLauncher { IsSupported = false };
        var dispatcher = new PlatformPrintLauncher(new IPrintLauncher[] { none });

        var r = await dispatcher.LaunchAsync("/tmp/x.png");

        r.Outcome.ShouldBe(ChartPrintOutcome.SnapshotOnly);
        r.StatusMessage.ShouldContain("/tmp/x.png");
    }

    [Fact]
    public void IsSupported_TrueIfAnyChildLauncherIsSupported()
    {
        new PlatformPrintLauncher(new IPrintLauncher[]
        {
            new RecordingLauncher { IsSupported = false },
            new RecordingLauncher { IsSupported = true },
        }).IsSupported.ShouldBeTrue();

        new PlatformPrintLauncher(new IPrintLauncher[]
        {
            new RecordingLauncher { IsSupported = false },
        }).IsSupported.ShouldBeFalse();
    }

    [Fact]
    public void Default_ReturnsConfiguredDispatcher()
    {
        // We can't assert the OS reliably, but we can verify the call works.
        var d = PlatformPrintLauncher.Default();
        d.ShouldNotBeNull();
    }
}

using RollerGraph.App.Charting;
using RollerGraph.App.Printing;
using RollerGraph.App.Services;
using Shouldly;

namespace RollerGraph.App.Tests;

public class ChartPrinterTests
{
    private sealed class RecordingSnapshotter : IChartSnapshotter
    {
        public string? Path { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public ChartSnapshotStats? Stats { get; private set; }
        public Exception? Throw { get; init; }

        public void SaveAsPng(string destinationPath, int width, int height, ChartSnapshotStats? stats = null)
        {
            if (Throw is not null) throw Throw;
            Path = destinationPath;
            Width = width;
            Height = height;
            Stats = stats;
        }
    }

    private sealed class StubLauncher : IPrintLauncher
    {
        public bool IsSupported { get; init; } = true;
        public ChartPrintResult ResultToReturn { get; init; } = new(ChartPrintOutcome.Sent, "ok");
        public string? Launched { get; private set; }

        public Task<ChartPrintResult> LaunchAsync(string pngFilePath)
        {
            Launched = pngFilePath;
            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public async Task PrintAsync_HappyPath_SnapshotsThenDelegatesToLauncher()
    {
        var snap = new RecordingSnapshotter();
        var launcher = new StubLauncher();
        var printer = new ChartPrinter(snap, launcher);

        var r = await printer.PrintAsync();

        snap.Path.ShouldNotBeNull();
        snap.Width.ShouldBe(1600);
        snap.Height.ShouldBe(900);
        launcher.Launched.ShouldBe(snap.Path);
        r.Outcome.ShouldBe(ChartPrintOutcome.Sent);
    }

    [Fact]
    public async Task PrintAsync_LauncherUnsupported_ReturnsSnapshotOnly()
    {
        var snap = new RecordingSnapshotter();
        var launcher = new StubLauncher { IsSupported = false };
        var printer = new ChartPrinter(snap, launcher);

        var r = await printer.PrintAsync();

        r.Outcome.ShouldBe(ChartPrintOutcome.SnapshotOnly);
        launcher.Launched.ShouldBeNull();   // launcher should not have been invoked
    }

    [Fact]
    public async Task PrintAsync_SnapshotThrows_ReturnsFailedWithMessage()
    {
        var snap = new RecordingSnapshotter { Throw = new InvalidOperationException("no gpu") };
        var launcher = new StubLauncher();
        var printer = new ChartPrinter(snap, launcher);

        var r = await printer.PrintAsync();

        r.Outcome.ShouldBe(ChartPrintOutcome.Failed);
        r.ErrorMessage.ShouldBe("no gpu");
        launcher.Launched.ShouldBeNull();
    }

    [Fact]
    public async Task PrintAsync_PassesPeakStatsToSnapshotter()
    {
        var snap = new RecordingSnapshotter();
        var launcher = new StubLauncher();
        var stats = new ChartSnapshotStats(10, 20, 30, 40, 50);
        var printer = new ChartPrinter(snap, launcher, () => stats);

        await printer.PrintAsync();

        snap.Stats.ShouldBe(stats);
    }
}

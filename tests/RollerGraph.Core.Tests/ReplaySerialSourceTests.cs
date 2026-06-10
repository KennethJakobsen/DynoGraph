using RollerGraph.Core.Serial;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class ReplaySerialSourceTests
{
    [Fact]
    public async Task Replay_EmitsAllLinesInOrder()
    {
        var src = new ReplaySerialSource(
            new[] { "1,10,20,300", "2,11,21,310", "3,12,22,320" },
            interval: TimeSpan.Zero);

        var received = new List<string>();
        var tcs = new TaskCompletionSource();
        src.LineReceived += (_, e) =>
        {
            received.Add(e.Line);
            if (received.Count == 3) tcs.TrySetResult();
        };

        await src.StartAsync();
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await src.StopAsync();

        received.ShouldBe(new[] { "1,10,20,300", "2,11,21,310", "3,12,22,320" });
    }

    [Fact]
    public async Task Stop_CancelsReplayPromptly()
    {
        // 10 lines paced at 200 ms each = ~2 seconds if not cancelled.
        var src = new ReplaySerialSource(
            Enumerable.Range(0, 10).Select(i => $"{i},10,20,300"),
            interval: TimeSpan.FromMilliseconds(200));

        await src.StartAsync();
        await Task.Delay(50);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await src.StopAsync();
        sw.Stop();

        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        src.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task IsRunning_TrueAfterStart_FalseAfterStop()
    {
        var src = new ReplaySerialSource(new[] { "1,1,1,1" }, interval: TimeSpan.FromSeconds(10));
        src.IsRunning.ShouldBeFalse();
        await src.StartAsync();
        src.IsRunning.ShouldBeTrue();
        await src.StopAsync();
        src.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task FromFile_StripsBlanksAndComments()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, "# header comment\n1,2,3,4\n\n# another\n5,6,7,8\n");
            var src = ReplaySerialSource.FromFile(tmp, TimeSpan.Zero);
            // Use the public start/event surface to verify line content.
            var lines = new List<string>();
            var tcs = new TaskCompletionSource();
            src.LineReceived += (_, e) =>
            {
                lines.Add(e.Line);
                if (lines.Count == 2) tcs.TrySetResult();
            };
            await src.StartAsync();
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await src.StopAsync();
            lines.ShouldBe(new[] { "1,2,3,4", "5,6,7,8" });
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void SampleDelta_ComputesDifference()
    {
        ReplaySerialSource.SampleDelta("10,1,2,3", "12,4,5,6").ShouldBe(2);
        ReplaySerialSource.SampleDelta("bad,1,2,3", "12,4,5,6").ShouldBeNull();
    }
}

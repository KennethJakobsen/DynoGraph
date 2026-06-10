using RollerGraph.Core.Smoothing;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class RollingAverageTests
{
    [Fact]
    public void Constructor_ZeroOrNegative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingAverage(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingAverage(-1));
    }

    [Fact]
    public void Push_WindowOfOne_ReturnsLatestValue()
    {
        var avg = new RollingAverage(1);
        avg.Push(10).ShouldBe(10);
        avg.Push(20).ShouldBe(20);
        avg.Push(5).ShouldBe(5);
    }

    [Fact]
    public void Push_BeforeWindowFull_AveragesPartial()
    {
        var avg = new RollingAverage(3);
        avg.Push(10).ShouldBe(10);
        avg.Push(20).ShouldBe(15);
        avg.Push(30).ShouldBe(20);
    }

    [Fact]
    public void Push_WhenFull_SlidesOldestOut()
    {
        var avg = new RollingAverage(3);
        avg.Push(10);
        avg.Push(20);
        avg.Push(30); // window [10, 20, 30] avg = 20
        avg.Push(40).ShouldBe((20 + 30 + 40) / 3.0, 1e-9); // window [20, 30, 40]
        avg.Push(50).ShouldBe((30 + 40 + 50) / 3.0, 1e-9); // window [30, 40, 50]
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var avg = new RollingAverage(3);
        avg.Push(100);
        avg.Push(200);
        avg.Reset();
        avg.Count.ShouldBe(0);
        avg.Push(10).ShouldBe(10);
    }

    [Fact]
    public void Count_TracksFillUpToWindowSize()
    {
        var avg = new RollingAverage(3);
        avg.Count.ShouldBe(0);
        avg.Push(1);
        avg.Count.ShouldBe(1);
        avg.Push(2);
        avg.Count.ShouldBe(2);
        avg.Push(3);
        avg.Count.ShouldBe(3);
        avg.Push(4);
        avg.Count.ShouldBe(3); // capped at window size
    }
}

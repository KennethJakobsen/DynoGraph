using RollerGraph.Core.Smoothing;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class RollingMaximumTests
{
    [Fact]
    public void Constructor_ZeroOrNegative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingMaximum(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingMaximum(-1));
    }

    [Fact]
    public void Push_WindowOfOne_ReturnsLatestValue()
    {
        var max = new RollingMaximum(1);
        max.Push(10).ShouldBe(10);
        max.Push(20).ShouldBe(20);
        max.Push(5).ShouldBe(5);
    }

    [Fact]
    public void Push_BeforeWindowFull_ReturnsHighestSeen()
    {
        var max = new RollingMaximum(3);
        max.Push(10).ShouldBe(10);
        max.Push(20).ShouldBe(20);
        max.Push(15).ShouldBe(20);
    }

    [Fact]
    public void Push_WhenFull_SlidesOldestOut()
    {
        var max = new RollingMaximum(3);
        max.Push(10);
        max.Push(50);
        max.Push(30); // window [10, 50, 30]

        max.Push(20).ShouldBe(50); // window [20, 50, 30]
        max.Push(25).ShouldBe(30); // window [20, 25, 30]
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var max = new RollingMaximum(3);
        max.Push(100);
        max.Push(200);
        max.Reset();
        max.Count.ShouldBe(0);
        max.Push(10).ShouldBe(10);
    }
}

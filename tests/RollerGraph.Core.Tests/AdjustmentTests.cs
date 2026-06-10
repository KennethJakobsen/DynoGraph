using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Models;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class ChannelAdjustmentTests
{
    [Fact]
    public void DefaultAdjustment_IsIdentity()
    {
        var a = new ChannelAdjustment();
        a.IsIdentity.ShouldBeTrue();
        a.Compile()(42.0).ShouldBe(42.0);
    }

    [Fact]
    public void LinearTransform_AppliesFactorThenOffset()
    {
        var a = new ChannelAdjustment { Factor = 2.0, Offset = 3.0 };
        a.Compile()(10.0).ShouldBe(23.0);
        a.IsIdentity.ShouldBeFalse();
    }

    [Fact]
    public void Expression_OverridesFactorAndOffset()
    {
        // Factor/Offset present but Expression also set -> expression wins.
        var a = new ChannelAdjustment { Factor = 99, Offset = 99, Expression = "x + 1" };
        a.Compile()(10).ShouldBe(11);
    }

    [Fact]
    public void Compile_PropagatesExpressionException()
    {
        var a = new ChannelAdjustment { Expression = "(bad" };
        Should.Throw<ExpressionException>(() => a.Compile());
    }

    [Fact]
    public void IsIdentity_IsFalseWhenExpressionIsPresent()
    {
        var a = new ChannelAdjustment { Expression = "x" };
        a.IsIdentity.ShouldBeFalse();
    }
}

public class SampleAdjusterTests
{
    private static Sample S(double speed, double nm, double hp) =>
        new(1, speed, nm, hp, DateTime.UnixEpoch);

    [Fact]
    public void Identity_LeavesSampleUnchanged()
    {
        var s = S(50, 100, 80);
        SampleAdjuster.Identity.Adjust(s).ShouldBe(s);
    }

    [Fact]
    public void Adjust_AppliesEachChannelIndependently()
    {
        var adj = new SampleAdjuster(
            speed: new ChannelAdjustment { Factor = 2.0 },
            nm:    new ChannelAdjustment { Offset = 10.0 },
            hp:    new ChannelAdjustment { Expression = "x / 0.92" });

        var r = adj.Adjust(S(50, 100, 92));
        r.SpeedKmh.ShouldBe(100);
        r.Nm.ShouldBe(110);
        r.Hp.ShouldBe(100, 1e-9);
    }

    [Fact]
    public void Adjust_PreservesSampleNumberAndTimestamp()
    {
        var adj = new SampleAdjuster(
            new ChannelAdjustment { Factor = 2 },
            new ChannelAdjustment { Factor = 2 },
            new ChannelAdjustment { Factor = 2 });

        var t = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var input = new Sample(SampleNumber: 99, SpeedKmh: 10, Nm: 20, Hp: 30, ReceivedAt: t);
        var r = adj.Adjust(input);
        r.SampleNumber.ShouldBe(99);
        r.ReceivedAt.ShouldBe(t);
    }
}

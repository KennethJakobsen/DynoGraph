using RollerGraph.Core.Models;
using RollerGraph.Core.Smoothing;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class SampleSmootherTests
{
    private static Sample S(int n, double speed, double nm, double hp) =>
        new(n, speed, nm, hp, DateTime.UnixEpoch);

    [Fact]
    public void Smooth_WithWindowOne_PassesThrough()
    {
        var sm = new SampleSmoother(1);
        var s = S(1, 50, 100, 80);
        sm.Smooth(s).ShouldBe(s);
    }

    [Fact]
    public void Smooth_UsesTopHpAndNmWhileAveragingSpeed()
    {
        var sm = new SampleSmoother(3);
        sm.Smooth(S(1, 30, 60, 40));
        sm.Smooth(S(2, 60, 120, 80));
        var r = sm.Smooth(S(3, 90, 90, 60));
        r.SpeedKmh.ShouldBe(60, 1e-9);
        r.Nm.ShouldBe(120, 1e-9);
        r.Hp.ShouldBe(80, 1e-9);
        r.SampleNumber.ShouldBe(3); // SampleNumber is preserved
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var sm = new SampleSmoother(3);
        sm.Smooth(S(1, 100, 100, 100));
        sm.Smooth(S(2, 100, 100, 100));
        sm.Reset();
        var r = sm.Smooth(S(3, 10, 20, 30));
        r.SpeedKmh.ShouldBe(10, 1e-9);
        r.Nm.ShouldBe(20, 1e-9);
        r.Hp.ShouldBe(30, 1e-9);
    }
}

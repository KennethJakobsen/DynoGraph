using RollerGraph.Core.Scaling;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class NiceNumberTests
{
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(-5.0, 1.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 2.0)]
    [InlineData(2.0, 2.0)]
    [InlineData(2.3, 2.5)]
    [InlineData(2.5, 2.5)]
    [InlineData(3.0, 5.0)]
    [InlineData(5.0, 5.0)]
    [InlineData(7.0, 10.0)]
    [InlineData(9.9, 10.0)]
    [InlineData(10.0, 10.0)]
    [InlineData(11.0, 20.0)]
    [InlineData(20.0, 20.0)]
    [InlineData(22.0, 25.0)]
    [InlineData(25.0, 25.0)]
    [InlineData(26.0, 50.0)]
    [InlineData(50.0, 50.0)]
    [InlineData(51.0, 100.0)]
    [InlineData(100.0, 100.0)]
    [InlineData(140.0, 200.0)]
    [InlineData(200.0, 200.0)]
    [InlineData(220.0, 250.0)]
    [InlineData(251.0, 500.0)]
    [InlineData(500.0, 500.0)]
    [InlineData(501.0, 1000.0)]
    [InlineData(1234.0, 2000.0)]
    public void Ceil_SnapsToNiceNumbers(double value, double expected)
    {
        NiceNumber.Ceil(value).ShouldBe(expected, 1e-6);
    }

    [Fact]
    public void NextAxisMax_WhenValueWellBelowMax_ReturnsCurrentMax()
    {
        // Value is at 50% of current max -> no growth.
        NiceNumber.NextAxisMax(currentMax: 100, observedValue: 50).ShouldBe(100);
    }

    [Fact]
    public void NextAxisMax_WhenValueAt89Percent_ReturnsCurrentMax()
    {
        NiceNumber.NextAxisMax(currentMax: 100, observedValue: 89).ShouldBe(100);
    }

    [Fact]
    public void NextAxisMax_WhenValueAt90Percent_DoesNotGrow()
    {
        // The trigger is strictly greater than 90%, so exactly 90% should not grow.
        NiceNumber.NextAxisMax(currentMax: 100, observedValue: 90).ShouldBe(100);
    }

    [Fact]
    public void NextAxisMax_WhenValueAbove90Percent_GrowsWithHeadroomSnapped()
    {
        // 95 * 1.1 = 104.5 -> nice ceil -> 200 (since 1/2/2.5/5 family of 10^2 = 100/200/250/500)
        NiceNumber.NextAxisMax(currentMax: 100, observedValue: 95).ShouldBe(200);
    }

    [Fact]
    public void NextAxisMax_WhenValueExceedsMax_AlwaysGrows()
    {
        // 150 * 1.1 = 165 -> nice ceil -> 200
        NiceNumber.NextAxisMax(currentMax: 100, observedValue: 150).ShouldBe(200);
    }

    [Fact]
    public void NextAxisMax_NeverShrinks_EvenIfNiceCeilWouldBeLower()
    {
        // If currentMax is already 500 and observed = 10, we must not shrink to 20.
        NiceNumber.NextAxisMax(currentMax: 500, observedValue: 10).ShouldBe(500);
    }
}

using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Models;
using RollerGraph.Core.Pipeline;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class SamplePipelineTests
{
    private static Settings NewSettings(double minSpeed = 0, int smoothingWindow = 1) => new()
    {
        MinSpeedKmh = minSpeed,
        SmoothingWindow = smoothingWindow,
    };

    [Fact]
    public void Process_ValidLine_ReturnsAccepted()
    {
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        var result = pipeline.Process("1,30,60,400", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        result.Outcome.ShouldBe(SamplePipelineOutcome.Accepted);
        result.Sample.ShouldNotBeNull();
        result.Sample!.Value.SpeedKmh.ShouldBe(30);
        result.Sample.Value.Nm.ShouldBe(60);
        result.Sample.Value.Hp.ShouldBe(40);    // hp_x10 divided by 10
    }

    [Fact]
    public void Process_UnparseableLine_ReturnsBadLine()
    {
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        var result = pipeline.Process("this is not csv", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.BadLine);
        result.Sample.ShouldBeNull();
    }

    [Fact]
    public void Process_NullOrEmptyLine_ReturnsBadLine()
    {
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        pipeline.Process(null, DateTime.UtcNow).Outcome.ShouldBe(SamplePipelineOutcome.BadLine);
        pipeline.Process("", DateTime.UtcNow).Outcome.ShouldBe(SamplePipelineOutcome.BadLine);
        pipeline.Process("   ", DateTime.UtcNow).Outcome.ShouldBe(SamplePipelineOutcome.BadLine);
    }

    [Fact]
    public void Process_SpeedBelowMinSpeed_ReturnsFilteredOut()
    {
        var pipeline = new SamplePipeline(NewSettings(minSpeed: 10), SampleAdjuster.Identity);
        var result = pipeline.Process("1,5,60,400", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.FilteredOut);
        result.Sample.ShouldBeNull();
    }

    [Fact]
    public void Process_SpeedAtMinSpeed_IsAccepted()
    {
        var pipeline = new SamplePipeline(NewSettings(minSpeed: 10), SampleAdjuster.Identity);
        var result = pipeline.Process("1,10,60,400", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.Accepted);
    }

    [Fact]
    public void Process_AppliesAdjusterToAllChannels()
    {
        // Double the HP value before plotting.
        var adjuster = new SampleAdjuster(
            ChannelAdjustment.Identity,
            ChannelAdjustment.Identity,
            new ChannelAdjustment { Factor = 2.0 });
        var pipeline = new SamplePipeline(NewSettings(), adjuster);

        var result = pipeline.Process("1,30,60,400", DateTime.UtcNow);
        result.Sample!.Value.Hp.ShouldBe(80);   // 400/10 * 2.0
    }

    [Fact]
    public void Process_WithSmoothing_AveragesOverWindow()
    {
        var pipeline = new SamplePipeline(NewSettings(smoothingWindow: 3), SampleAdjuster.Identity)
        {
            SmoothingEnabled = true,
        };
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Feed three samples with hp = 10, 20, 30 -> smoothed hp = (10+20+30)/3 = 20
        pipeline.Process("1,30,60,100", t).Sample!.Value.Hp.ShouldBe(10);   // window size 1
        pipeline.Process("2,30,60,200", t).Sample!.Value.Hp.ShouldBe(15);   // window size 2
        pipeline.Process("3,30,60,300", t).Sample!.Value.Hp.ShouldBe(20);   // window size 3
    }

    [Fact]
    public void Process_WithSmoothingDisabled_ReturnsRawValues()
    {
        var pipeline = new SamplePipeline(NewSettings(smoothingWindow: 3), SampleAdjuster.Identity)
        {
            SmoothingEnabled = false,
        };

        var s1 = pipeline.Process("1,30,60,100", DateTime.UtcNow);
        var s2 = pipeline.Process("2,30,60,200", DateTime.UtcNow);
        s1.Sample!.Value.Hp.ShouldBe(10);
        s2.Sample!.Value.Hp.ShouldBe(20);
    }

    [Fact]
    public void ResetSmoother_DiscardsInFlightWindow()
    {
        var pipeline = new SamplePipeline(NewSettings(smoothingWindow: 3), SampleAdjuster.Identity)
        {
            SmoothingEnabled = true,
        };
        pipeline.Process("1,30,60,100", DateTime.UtcNow);   // hp 10 buffered
        pipeline.Process("2,30,60,200", DateTime.UtcNow);   // hp 20 buffered

        pipeline.ResetSmoother();

        // First sample after reset must produce its own raw value (window size 1).
        var s = pipeline.Process("3,30,60,300", DateTime.UtcNow);
        s.Sample!.Value.Hp.ShouldBe(30);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new SamplePipeline(null!, SampleAdjuster.Identity));
        Should.Throw<ArgumentNullException>(() => new SamplePipeline(new Settings(), null!));
    }

    // -------- Negative-value filter --------

    [Fact]
    public void Process_NegativeNm_ReturnsFilteredOut()
    {
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        var result = pipeline.Process("1,30,-5,400", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.FilteredOut);
        result.Sample.ShouldBeNull();
    }

    [Fact]
    public void Process_NegativeHp_ReturnsFilteredOut()
    {
        // hp_x10 of -10 -> hp = -1.0 after the parser /10 step.
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        var result = pipeline.Process("1,30,60,-10", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.FilteredOut);
        result.Sample.ShouldBeNull();
    }

    [Fact]
    public void Process_NegativeSpeed_ReturnsFilteredOut()
    {
        // MinSpeedKmh defaults to 0 in NewSettings, so the -5 hits the
        // negative filter rather than the MinSpeed filter.
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        var result = pipeline.Process("1,-5,60,400", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.FilteredOut);
        result.Sample.ShouldBeNull();
    }

    [Fact]
    public void Process_AllChannelsZero_IsAccepted()
    {
        // Zero is on the boundary - explicitly NOT negative, so it must pass.
        var pipeline = new SamplePipeline(NewSettings(), SampleAdjuster.Identity);
        var result = pipeline.Process("1,0,0,0", DateTime.UtcNow);

        result.Outcome.ShouldBe(SamplePipelineOutcome.Accepted);
        result.Sample!.Value.SpeedKmh.ShouldBe(0);
        result.Sample.Value.Nm.ShouldBe(0);
        result.Sample.Value.Hp.ShouldBe(0);
    }

    [Fact]
    public void Process_AdjusterTurnsPositiveIntoNegative_ReturnsFilteredOut()
    {
        // Filter runs AFTER adjustment - if a user expression like
        // 'x - 100' produces a negative HP, that's the value we reject.
        var adjuster = new SampleAdjuster(
            ChannelAdjustment.Identity,
            ChannelAdjustment.Identity,
            new ChannelAdjustment { Offset = -100 });   // hp 40 -> -60
        var pipeline = new SamplePipeline(NewSettings(), adjuster);

        var result = pipeline.Process("1,30,60,400", DateTime.UtcNow);
        result.Outcome.ShouldBe(SamplePipelineOutcome.FilteredOut);
    }

    [Fact]
    public void Process_AdjusterTurnsNegativeIntoPositive_IsAccepted()
    {
        // Conversely, if the adjuster (e.g. abs-equivalent factor) flips a
        // raw negative to a positive, the pipeline must accept it.
        var adjuster = new SampleAdjuster(
            ChannelAdjustment.Identity,
            new ChannelAdjustment { Factor = -1.0 },    // nm -5 -> 5
            ChannelAdjustment.Identity);
        var pipeline = new SamplePipeline(NewSettings(), adjuster);

        var result = pipeline.Process("1,30,-5,400", DateTime.UtcNow);
        result.Outcome.ShouldBe(SamplePipelineOutcome.Accepted);
        result.Sample!.Value.Nm.ShouldBe(5);
    }
}

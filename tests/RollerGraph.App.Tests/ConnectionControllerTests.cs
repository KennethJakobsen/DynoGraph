using RollerGraph.App.Connection;
using RollerGraph.App.Tests.TestDoubles;
using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Models;
using Shouldly;

namespace RollerGraph.App.Tests;

public class ConnectionControllerTests
{
    private static (ConnectionController c, FakeSerialSourceFactory f, FakeSessionLogger l) NewController()
    {
        var factory = new FakeSerialSourceFactory();
        var logger = new FakeSessionLogger();
        var controller = new ConnectionController(new SyncUiDispatcher(), factory, logger);
        return (controller, factory, logger);
    }

    private static Settings Settings(double minSpeed = 0) => new() { BaudRate = 19200, MinSpeedKmh = minSpeed };

    [Fact]
    public async Task ConnectAsync_StartsSourceAndOpensLog()
    {
        var (controller, factory, logger) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, smoothingEnabled: false);

        var status = await controller.ConnectAsync("COM3", Settings(), smoothingEnabled: false);

        status.ShouldContain("COM3");
        controller.IsConnected.ShouldBeTrue();
        factory.LastPortName.ShouldBe("COM3");
        factory.LastBaudRate.ShouldBe(19200);
        factory.LiveSource.StartCount.ShouldBe(1);
        logger.SessionsStarted.ShouldBe(1);
        controller.LogFilePath.ShouldNotBeNull();
    }

    [Fact]
    public async Task ConnectAsync_AlreadyConnected_IsNoOp()
    {
        var (controller, factory, _) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);
        var startCountBefore = factory.LiveSource.StartCount;

        await controller.ConnectAsync("COM4", Settings(), false);

        factory.LiveSource.StartCount.ShouldBe(startCountBefore);
        factory.LastPortName.ShouldBe("COM3");      // unchanged
    }

    [Fact]
    public async Task DisconnectAsync_StopsSourceAndClosesLog()
    {
        var (controller, factory, logger) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        await controller.DisconnectAsync();

        controller.IsConnected.ShouldBeFalse();
        controller.LogFilePath.ShouldBeNull();
        factory.LiveSource.StopCount.ShouldBe(1);
        logger.SessionsEnded.ShouldBe(1);
    }

    [Fact]
    public async Task LineReceived_ValidLine_RaisesSampleAccepted()
    {
        var (controller, factory, _) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        Sample? captured = null;
        controller.SampleAccepted += (_, args) => captured = args.Sample;

        factory.LiveSource.EmitLine("1,30,60,400");

        captured.ShouldNotBeNull();
        captured!.Value.SpeedKmh.ShouldBe(30);
    }

    [Fact]
    public async Task LineReceived_BadLine_RaisesBadLineReceived()
    {
        var (controller, factory, _) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        int bad = 0;
        controller.BadLineReceived += (_, _) => bad++;

        factory.LiveSource.EmitLine("not a csv line");

        bad.ShouldBe(1);
    }

    [Fact]
    public async Task LineReceived_FilteredBySpeed_RaisesNothing()
    {
        var (controller, factory, _) = NewController();
        var settings = Settings(minSpeed: 10);
        controller.ApplySettings(settings, SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", settings, false);

        int samples = 0, bad = 0;
        controller.SampleAccepted += (_, _) => samples++;
        controller.BadLineReceived += (_, _) => bad++;

        factory.LiveSource.EmitLine("1,5,60,400");     // speed 5 < min 10

        samples.ShouldBe(0);
        bad.ShouldBe(0);
    }

    [Fact]
    public async Task LineReceived_LogsRawLineToSessionLogger()
    {
        var (controller, factory, logger) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        factory.LiveSource.EmitLine("1,30,60,400");
        factory.LiveSource.EmitLine("nope");

        logger.AppendedLines.Count.ShouldBe(2);     // both raw lines logged
    }

    [Fact]
    public async Task ErrorOccurred_StopsAndRaisesEvent()
    {
        var (controller, factory, logger) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        Exception? captured = null;
        controller.ErrorOccurred += (_, args) => captured = args.Error;

        factory.LiveSource.EmitError(new InvalidOperationException("port lost"));

        captured.ShouldNotBeNull();
        captured!.Message.ShouldBe("port lost");
        controller.IsConnected.ShouldBeFalse();
        controller.LogFilePath.ShouldBeNull();
        logger.SessionsEnded.ShouldBe(1);
    }

    [Fact]
    public async Task ReplayAsync_FromDisconnected_StartsReplaySource()
    {
        var (controller, factory, logger) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);

        var status = await controller.ReplayAsync("/tmp/fake.csv", Settings(), false);

        status.ShouldContain("fake");
        controller.IsConnected.ShouldBeTrue();
        factory.ReplaySource.StartCount.ShouldBe(1);
        factory.LastReplayPath.ShouldBe("/tmp/fake.csv");
        logger.SessionsStarted.ShouldBe(1);
    }

    [Fact]
    public async Task ReplayAsync_FromConnected_StopsLiveSourceFirst()
    {
        var (controller, factory, _) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        await controller.ReplayAsync("/tmp/fake.csv", Settings(), false);

        factory.LiveSource.StopCount.ShouldBe(1);
        factory.ReplaySource.StartCount.ShouldBe(1);
        controller.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task StartNewLogSession_WhileConnected_RotatesTheLogFile()
    {
        var (controller, _, logger) = NewController();
        controller.ApplySettings(Settings(), SampleAdjuster.Identity, false);
        await controller.ConnectAsync("COM3", Settings(), false);

        var firstPath = controller.LogFilePath;
        controller.StartNewLogSession();

        logger.SessionsEnded.ShouldBe(1);
        logger.SessionsStarted.ShouldBe(2);
        controller.LogFilePath.ShouldNotBe(firstPath);
    }

    [Fact]
    public void StartNewLogSession_WhenDisconnected_IsNoOp()
    {
        var (controller, _, logger) = NewController();

        controller.StartNewLogSession();
        logger.SessionsStarted.ShouldBe(0);
        logger.SessionsEnded.ShouldBe(0);
    }

    [Fact]
    public async Task SmoothingEnabled_ResetsPipelineWindow()
    {
        var (controller, factory, _) = NewController();
        var settings = new Settings { MinSpeedKmh = 0, SmoothingWindow = 3 };
        controller.ApplySettings(settings, SampleAdjuster.Identity, smoothingEnabled: true);
        await controller.ConnectAsync("COM3", settings, smoothingEnabled: true);

        var samples = new List<Sample>();
        controller.SampleAccepted += (_, args) => samples.Add(args.Sample);

        factory.LiveSource.EmitLine("1,30,60,100");   // hp 10 buffered
        factory.LiveSource.EmitLine("2,30,60,200");   // hp 20 buffered
        controller.SmoothingEnabled = false;           // disables + resets
        controller.SmoothingEnabled = true;            // re-enable for fresh window
        factory.LiveSource.EmitLine("3,30,60,300");   // fresh window: hp 30

        samples[^1].Hp.ShouldBe(30);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var dispatcher = new SyncUiDispatcher();
        var factory = new FakeSerialSourceFactory();
        var logger = new FakeSessionLogger();
        Should.Throw<ArgumentNullException>(() => new ConnectionController(null!, factory, logger));
        Should.Throw<ArgumentNullException>(() => new ConnectionController(dispatcher, null!, logger));
        Should.Throw<ArgumentNullException>(() => new ConnectionController(dispatcher, factory, null!));
    }
}

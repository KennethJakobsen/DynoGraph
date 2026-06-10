using RollerGraph.App.Tests.TestDoubles;
using RollerGraph.App.ViewModels;
using RollerGraph.Core.Models;
using Shouldly;

namespace RollerGraph.App.Tests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel NewVm(
        out FakePortEnumerator ports,
        out FakeSerialSourceFactory factory,
        out FakeSessionLogger logger,
        out InMemorySettingsStore settingsStore,
        out FakeSavedRunStore runStore,
        Settings? settings = null,
        string[]? availablePorts = null)
    {
        ports = new FakePortEnumerator(availablePorts ?? new[] { "COM1", "COM2" });
        factory = new FakeSerialSourceFactory();
        logger = new FakeSessionLogger();
        settingsStore = new InMemorySettingsStore { Current = settings ?? new Settings() };
        runStore = new FakeSavedRunStore();
        return new MainWindowViewModel(
            dispatcher: new SyncUiDispatcher(),
            settings: settings,
            settingsStore: settingsStore,
            runStore: runStore,
            logger: logger,
            portEnumerator: ports,
            sourceFactory: factory);
    }

    [Fact]
    public void RefreshPorts_PopulatesAvailablePortsSorted()
    {
        var vm = NewVm(out _, out _, out _, out _, out _, availablePorts: new[] { "COM3", "COM1", "COM2" });
        vm.AvailablePorts.ShouldBe(new[] { "COM1", "COM2", "COM3" });
    }

    [Fact]
    public void Construction_WithLastPortInSettings_PrefersIt()
    {
        var vm = NewVm(out _, out _, out _, out _, out _,
            settings: new Settings { LastPortName = "COM2" });
        vm.SelectedPort.ShouldBe("COM2");
    }

    [Fact]
    public void Construction_LastPortNotAvailable_FallsBackToFirst()
    {
        var vm = NewVm(out _, out _, out _, out _, out _,
            settings: new Settings { LastPortName = "COMghost" },
            availablePorts: new[] { "COM1" });
        vm.SelectedPort.ShouldBe("COM1");
    }

    [Fact]
    public async Task ConnectCommand_StartsConnectionAndPersistsLastPort()
    {
        var vm = NewVm(out _, out var factory, out var logger, out var store, out _);
        vm.SelectedPort = "COM1";

        await vm.ConnectCommand.ExecuteAsync(null);

        vm.IsConnected.ShouldBeTrue();
        vm.StatusMessage.ShouldContain("COM1");
        factory.LastPortName.ShouldBe("COM1");
        logger.SessionsStarted.ShouldBe(1);
        store.Current.LastPortName.ShouldBe("COM1");
        vm.LogFilePath.ShouldNotBeNull();
    }

    [Fact]
    public async Task DisconnectCommand_StopsConnection()
    {
        var vm = NewVm(out _, out var factory, out _, out _, out _);
        vm.SelectedPort = "COM1";
        await vm.ConnectCommand.ExecuteAsync(null);

        await vm.DisconnectCommand.ExecuteAsync(null);

        vm.IsConnected.ShouldBeFalse();
        factory.LiveSource.StopCount.ShouldBe(1);
        vm.LogFilePath.ShouldBeNull();
    }

    [Fact]
    public async Task BadLine_IncrementsBadLineCounter()
    {
        var vm = NewVm(out _, out var factory, out _, out _, out _);
        vm.SelectedPort = "COM1";
        await vm.ConnectCommand.ExecuteAsync(null);

        factory.LiveSource.EmitLine("not csv");
        factory.LiveSource.EmitLine("also bad");

        vm.BadLineCount.ShouldBe(2);
    }

    [Fact]
    public async Task GoodLine_AppendsToChartAndUpdatesPeaks()
    {
        var vm = NewVm(out _, out var factory, out _, out _, out _);
        vm.SelectedPort = "COM1";
        await vm.ConnectCommand.ExecuteAsync(null);

        factory.LiveSource.EmitLine("1,30,60,400");
        factory.LiveSource.EmitLine("2,40,70,550");

        vm.Chart.SampleCount.ShouldBe(2);
        vm.Chart.PeakHp.ShouldBe(55);
        vm.Chart.PeakHpSpeed.ShouldBe(40);
        vm.Chart.PeakNm.ShouldBe(70);
    }

    [Fact]
    public async Task Reset_ClearsSamplesAndStartsNewLogSession()
    {
        var vm = NewVm(out _, out var factory, out var logger, out _, out _);
        vm.SelectedPort = "COM1";
        await vm.ConnectCommand.ExecuteAsync(null);
        factory.LiveSource.EmitLine("1,30,60,400");
        var firstLog = vm.LogFilePath;

        vm.ResetCommand.Execute(null);

        vm.Chart.SampleCount.ShouldBe(0);
        vm.BadLineCount.ShouldBe(0);
        logger.SessionsStarted.ShouldBe(2);     // initial + after reset
        vm.LogFilePath.ShouldNotBe(firstLog);
    }

    [Fact]
    public async Task Error_FromSource_UpdatesStatusAndDisconnects()
    {
        var vm = NewVm(out _, out var factory, out _, out _, out _);
        vm.SelectedPort = "COM1";
        await vm.ConnectCommand.ExecuteAsync(null);

        factory.LiveSource.EmitError(new InvalidOperationException("oops"));

        vm.IsConnected.ShouldBeFalse();
        vm.StatusMessage.ShouldContain("oops");
        vm.LogFilePath.ShouldBeNull();
    }

    [Fact]
    public void ApplySettings_PersistsAndUpdatesChartDefaults()
    {
        var vm = NewVm(out _, out _, out _, out var store, out _);

        vm.ApplySettings(new Settings { DefaultHpMax = 200, DefaultNmMax = 200, DefaultSpeedMax = 200 });

        store.SaveCount.ShouldBeGreaterThan(0);
        store.Current.DefaultHpMax.ShouldBe(200);
        vm.Settings.DefaultHpMax.ShouldBe(200);
    }

    [Fact]
    public void ApplySettings_PreservesCurrentlySelectedPort()
    {
        var vm = NewVm(out _, out _, out _, out _, out _);
        vm.SelectedPort = "COM2";

        vm.ApplySettings(new Settings { DefaultHpMax = 100, LastPortName = "COM1" });

        vm.Settings.LastPortName.ShouldBe("COM2");
    }

    [Fact]
    public async Task SmoothingEnabled_TogglesPipelineWithoutRestart()
    {
        var vm = NewVm(out _, out var factory, out _, out _, out _,
            settings: new Settings { SmoothingWindow = 3 });
        vm.SelectedPort = "COM1";
        await vm.ConnectCommand.ExecuteAsync(null);

        vm.SmoothingEnabled = true;
        factory.LiveSource.EmitLine("1,30,60,100");   // hp 10
        factory.LiveSource.EmitLine("2,30,60,300");   // hp 30

        // With smoothing window 3, after 2 samples the average is (10+30)/2 = 20
        vm.Chart.SampleCount.ShouldBe(2);
        vm.Chart.PeakHp.ShouldBe(20);
    }
}

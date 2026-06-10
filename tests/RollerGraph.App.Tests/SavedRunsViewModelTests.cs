using RollerGraph.App.Charting;
using RollerGraph.App.Tests.TestDoubles;
using RollerGraph.App.ViewModels;
using RollerGraph.Core.Models;
using Shouldly;

namespace RollerGraph.App.Tests;

public class SavedRunsViewModelTests
{
    private static Sample S(int n, double sp, double nm, double hp) =>
        new(n, sp, nm, hp, new DateTime(2025, 1, 1, 12, 0, n, DateTimeKind.Utc));

    private static (SavedRunsViewModel vm, FakeSavedRunStore store, IChartRenderer renderer, List<Sample> live) NewVm()
    {
        var store = new FakeSavedRunStore();
        var renderer = new LiveChartsChartRenderer(new Settings());
        var chart = new ChartViewModel(renderer);
        var live = new List<Sample>();
        var vm = new SavedRunsViewModel(store, chart, () => live);
        return (vm, store, renderer, live);
    }

    [Fact]
    public void LoadAllFromDisk_PopulatesItemsAndChartOverlays()
    {
        var (vm, store, renderer, _) = NewVm();
        store.Save(new SavedRun { Name = "A", Samples = new[] { S(1, 10, 20, 30) } });
        store.Save(new SavedRun { Name = "B", CreatedUtc = DateTime.UtcNow.AddMinutes(1), Samples = new[] { S(1, 11, 21, 31) } });

        vm.LoadAllFromDisk();

        vm.Items.Count.ShouldBe(2);
        vm.Items.Select(r => r.Name).ShouldContain("A");
        vm.Items.Select(r => r.Name).ShouldContain("B");
        // Each saved run adds 2 series (HP + NM) to the chart.
        renderer.Series.Count.ShouldBe(2 /*live HP+NM*/ + 4);
    }

    [Fact]
    public void Exists_RecognisesSlugEquivalentNames()
    {
        var (vm, store, _, _) = NewVm();
        store.Save(new SavedRun { Name = "86 nozzle", Samples = Array.Empty<Sample>() });
        vm.LoadAllFromDisk();

        vm.Exists("86 nozzle").ShouldBeTrue();
        vm.Exists("86 Nozzle").ShouldBeTrue();
        vm.Exists("86-nozzle").ShouldBeTrue();
        vm.Exists("totally different").ShouldBeFalse();
    }

    [Fact]
    public void CanSaveCurrentRun_TrueOnlyWhenLiveSamplesPresent()
    {
        var (vm, _, _, live) = NewVm();
        vm.CanSaveCurrentRun.ShouldBeFalse();
        live.Add(S(1, 10, 20, 30));
        vm.CanSaveCurrentRun.ShouldBeTrue();
    }

    [Fact]
    public void CaptureFromLive_NoSamples_ReturnsNull()
    {
        var (vm, _, _, _) = NewVm();
        vm.CaptureFromLive("anything").ShouldBeNull();
        vm.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void CaptureFromLive_HappyPath_AddsItemAndPersists()
    {
        var (vm, store, renderer, live) = NewVm();
        live.Add(S(1, 10, 20, 30));

        var captured = vm.CaptureFromLive("Run 1");

        captured.ShouldNotBeNull();
        vm.Items.Count.ShouldBe(1);
        store.Exists("Run 1").ShouldBeTrue();
        renderer.Series.Count.ShouldBe(2 + 2);          // live + 1 overlay
    }

    [Fact]
    public void CaptureFromLive_DuplicateNameWithoutOverwrite_ReturnsNull()
    {
        var (vm, store, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        vm.CaptureFromLive("Run 1");

        var second = vm.CaptureFromLive("Run 1", overwrite: false);
        second.ShouldBeNull();
        vm.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void CaptureFromLive_DuplicateNameWithOverwrite_UpdatesExisting()
    {
        var (vm, store, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        var first = vm.CaptureFromLive("Run 1");
        live.Add(S(2, 11, 21, 31));

        var second = vm.CaptureFromLive("Run 1", overwrite: true);
        second.ShouldBeSameAs(first);
        vm.Items.Count.ShouldBe(1);
        first!.SampleCount.ShouldBe(2);
    }

    [Fact]
    public void Delete_RemovesFromItemsChartAndStore()
    {
        var (vm, store, renderer, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        var v = vm.CaptureFromLive("Run 1");

        vm.Delete(v!);

        vm.Items.ShouldBeEmpty();
        store.Exists("Run 1").ShouldBeFalse();
        renderer.Series.Count.ShouldBe(2);              // only live series remain
    }

    [Fact]
    public void Rename_HappyPath_UpdatesName()
    {
        var (vm, store, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        var v = vm.CaptureFromLive("Run 1");

        var ok = vm.Rename(v!, "Run One");

        ok.ShouldBeTrue();
        v!.Name.ShouldBe("Run One");
        store.Exists("Run One").ShouldBeTrue();
        store.Exists("Run 1").ShouldBeFalse();
    }

    [Fact]
    public void Rename_CollidesWithExisting_ReturnsFalse()
    {
        var (vm, _, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        var a = vm.CaptureFromLive("Run 1");
        vm.CaptureFromLive("Run 2");

        var ok = vm.Rename(a!, "Run 2");
        ok.ShouldBeFalse();
        a!.Name.ShouldBe("Run 1");
    }

    [Fact]
    public void Rename_BlankName_ReturnsFalse()
    {
        var (vm, _, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        var v = vm.CaptureFromLive("Run 1");

        vm.Rename(v!, "").ShouldBeFalse();
        vm.Rename(v!, "   ").ShouldBeFalse();
    }

    [Fact]
    public void ToggleVisibility_PushesThroughToStore()
    {
        var (vm, store, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        var v = vm.CaptureFromLive("Run 1");
        v!.IsVisible = false;

        vm.ToggleVisibility(v);

        var fromStore = store.LoadAll().Single();
        fromStore.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void ClearAll_DeletesEverything()
    {
        var (vm, store, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        vm.CaptureFromLive("A");
        live.Add(S(2, 11, 21, 31));
        vm.CaptureFromLive("B");

        vm.ClearAll();

        vm.Items.ShouldBeEmpty();
        store.LoadAll().ShouldBeEmpty();
    }

    [Fact]
    public void StatusReporter_IsInvokedOnSave()
    {
        var (vm, _, _, live) = NewVm();
        live.Add(S(1, 10, 20, 30));
        string? msg = null;
        vm.StatusReporter = m => msg = m;

        vm.CaptureFromLive("Run 1");
        msg.ShouldNotBeNull();
        msg!.ShouldContain("Saved");
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var chart = new ChartViewModel(new LiveChartsChartRenderer(new Settings()));
        Should.Throw<ArgumentNullException>(() => new SavedRunsViewModel(null!, chart, () => Array.Empty<Sample>()));
        Should.Throw<ArgumentNullException>(() => new SavedRunsViewModel(new FakeSavedRunStore(), null!, () => Array.Empty<Sample>()));
        Should.Throw<ArgumentNullException>(() => new SavedRunsViewModel(new FakeSavedRunStore(), chart, null!));
    }
}

using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Models;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "rollergraph-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new SettingsStore(_path);
        var s = store.Load();
        s.BaudRate.ShouldBe(19200);
        s.MinSpeedKmh.ShouldBe(5.0);
        s.SmoothingWindow.ShouldBe(5);
        s.DefaultHpMax.ShouldBe(10.0);
        s.DefaultNmMax.ShouldBe(10.0);
        s.DefaultSpeedMax.ShouldBe(50.0);
        s.LastPortName.ShouldBeNull();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllProperties()
    {
        var store = new SettingsStore(_path);
        var original = new Settings
        {
            LastPortName = "/dev/cu.usbserial-foo",
            BaudRate = 38400,
            MinSpeedKmh = 7.5,
            SmoothingWindow = 11,
            DefaultHpMax = 25.0,
            DefaultNmMax = 25.0,
            DefaultSpeedMax = 100.0,
        };
        store.Save(original);

        var loaded = store.Load();
        loaded.LastPortName.ShouldBe(original.LastPortName);
        loaded.BaudRate.ShouldBe(original.BaudRate);
        loaded.MinSpeedKmh.ShouldBe(original.MinSpeedKmh);
        loaded.SmoothingWindow.ShouldBe(original.SmoothingWindow);
        loaded.DefaultHpMax.ShouldBe(original.DefaultHpMax);
        loaded.DefaultNmMax.ShouldBe(original.DefaultNmMax);
        loaded.DefaultSpeedMax.ShouldBe(original.DefaultSpeedMax);
    }

    [Fact]
    public void Save_CreatesParentDirectory()
    {
        var nestedPath = Path.Combine(_dir, "nested", "deeper", "settings.json");
        var store = new SettingsStore(nestedPath);
        store.Save(new Settings());
        File.Exists(nestedPath).ShouldBeTrue();
    }

    [Fact]
    public void Save_TwiceOverwritesAtomically()
    {
        var store = new SettingsStore(_path);
        store.Save(new Settings { BaudRate = 9600 });
        store.Save(new Settings { BaudRate = 115200 });
        var loaded = store.Load();
        loaded.BaudRate.ShouldBe(115200);
        File.Exists(_path + ".tmp").ShouldBeFalse();
    }

    [Fact]
    public void Load_OnMalformedJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ this is not json");
        var store = new SettingsStore(_path);
        var s = store.Load();
        s.BaudRate.ShouldBe(19200);
    }

    [Fact]
    public void Load_OnEmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "");
        var store = new SettingsStore(_path);
        store.Load().BaudRate.ShouldBe(19200);
    }

    [Fact]
    public void DefaultFilePath_PointsUnderRollerGraph()
    {
        var p = SettingsStore.DefaultFilePath();
        p.ShouldContain("RollerGraph");
        p.ShouldEndWith("settings.json");
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsChannelAdjustments()
    {
        var store = new SettingsStore(_path);
        var original = new Settings
        {
            SpeedAdjustment = new ChannelAdjustment { Factor = 1.0, Offset = 0.0 },
            NmAdjustment = new ChannelAdjustment { Factor = 1.02, Offset = -1.5 },
            HpAdjustment = new ChannelAdjustment { Expression = "x / 0.92" },
        };
        store.Save(original);

        var loaded = store.Load();
        loaded.SpeedAdjustment.IsIdentity.ShouldBeTrue();
        loaded.NmAdjustment.Factor.ShouldBe(1.02);
        loaded.NmAdjustment.Offset.ShouldBe(-1.5);
        loaded.HpAdjustment.Expression.ShouldBe("x / 0.92");
        loaded.HpAdjustment.Compile()(92.0).ShouldBe(100.0, 1e-9);
    }
}

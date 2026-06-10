using RollerGraph.Core.Models;
using RollerGraph.Core.Storage;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class FileSavedRunStoreTests : IDisposable
{
    private readonly string _root;

    public FileSavedRunStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rollergraph-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private static Sample S(int n, double sp, double nm, double hp) =>
        new(n, sp, nm, hp, new DateTime(2025, 1, 1, 12, 0, n, DateTimeKind.Utc));

    [Fact]
    public void LoadAll_OnEmptyOrMissingDirectory_ReturnsEmpty()
    {
        var store = new FileSavedRunStore(_root);
        store.LoadAll().ShouldBeEmpty();
    }

    [Fact]
    public void Save_ThenLoadAll_RoundTripsAllFields()
    {
        var store = new FileSavedRunStore(_root);
        var run = new SavedRun
        {
            Name = "86 nozzle",
            CreatedUtc = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Color = "#FF8A00",
            IsVisible = false,
            Samples = new[] { S(1, 30, 60, 40), S(2, 40, 70, 55), S(3, 50, 80, 70) },
        };
        store.Save(run);

        var loaded = store.LoadAll();
        loaded.Count.ShouldBe(1);
        var l = loaded[0];
        l.Name.ShouldBe("86 nozzle");
        l.CreatedUtc.ShouldBe(run.CreatedUtc);
        l.Color.ShouldBe("#FF8A00");
        l.IsVisible.ShouldBeFalse();
        l.Samples.Count.ShouldBe(3);
        l.Samples[0].SpeedKmh.ShouldBe(30);
        l.Samples[^1].Hp.ShouldBe(70);
    }

    [Fact]
    public void Save_MultipleRuns_LoadAllOrderedByCreatedUtc()
    {
        var store = new FileSavedRunStore(_root);
        store.Save(new SavedRun { Name = "later", CreatedUtc = new DateTime(2025, 6, 3, 10, 0, 0, DateTimeKind.Utc) });
        store.Save(new SavedRun { Name = "earlier", CreatedUtc = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc) });
        store.Save(new SavedRun { Name = "middle", CreatedUtc = new DateTime(2025, 6, 2, 10, 0, 0, DateTimeKind.Utc) });

        var ordered = store.LoadAll().Select(r => r.Name).ToArray();
        ordered.ShouldBe(new[] { "earlier", "middle", "later" });
    }

    [Fact]
    public void Save_WithExistingSlug_OverwritesInPlace()
    {
        var store = new FileSavedRunStore(_root);
        store.Save(new SavedRun { Name = "86 nozzle", Color = "#111111" });
        store.Save(new SavedRun { Name = "86 Nozzle", Color = "#222222" });

        var all = store.LoadAll();
        all.Count.ShouldBe(1);
        all[0].Color.ShouldBe("#222222");

        // Only one file on disk for both saves.
        Directory.GetFiles(_root, "*.csv").Length.ShouldBe(1);
    }

    [Fact]
    public void Delete_RemovesFileAndDropsFromLoadAll()
    {
        var store = new FileSavedRunStore(_root);
        store.Save(new SavedRun { Name = "Keeper" });
        store.Save(new SavedRun { Name = "Goner" });
        store.Delete("Goner").ShouldBeTrue();
        var names = store.LoadAll().Select(r => r.Name).ToArray();
        names.ShouldBe(new[] { "Keeper" });
    }

    [Fact]
    public void Delete_MissingName_ReturnsFalse()
    {
        var store = new FileSavedRunStore(_root);
        store.Delete("nothing").ShouldBeFalse();
    }

    [Fact]
    public void Exists_ReflectsWhetherSlugFileIsOnDisk()
    {
        var store = new FileSavedRunStore(_root);
        store.Exists("anything").ShouldBeFalse();
        store.Save(new SavedRun { Name = "86 nozzle" });
        store.Exists("86 Nozzle").ShouldBeTrue();           // slug-equivalent
        store.Exists("totally different").ShouldBeFalse();
    }

    [Fact]
    public void LoadAll_SkipsCorruptFiles()
    {
        Directory.CreateDirectory(_root);
        // A valid run + a junk file.
        var store = new FileSavedRunStore(_root);
        store.Save(new SavedRun { Name = "Good", Samples = new[] { S(1, 10, 20, 30) } });
        File.WriteAllText(Path.Combine(_root, "broken.csv"), "this is not a valid run file\n");

        var loaded = store.LoadAll();
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("Good");
    }

    [Fact]
    public void PathFor_UsesSlug()
    {
        var store = new FileSavedRunStore(_root);
        store.PathFor("86 Nozzle").ShouldEndWith("86-nozzle.csv");
    }

    [Fact]
    public void Save_WithFloatingPointSampleData_PreservesPrecision()
    {
        var store = new FileSavedRunStore(_root);
        var run = new SavedRun
        {
            Name = "precision",
            Samples = new[] { S(1, 42.12345, 78.91234, 123.456) },
        };
        store.Save(run);
        var loaded = store.LoadAll()[0];
        loaded.Samples[0].SpeedKmh.ShouldBe(42.12345, 1e-9);
        loaded.Samples[0].Nm.ShouldBe(78.91234, 1e-9);
        loaded.Samples[0].Hp.ShouldBe(123.456, 1e-9);
    }
}

using RollerGraph.Core.Logging;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class CsvSessionLoggerTests : IDisposable
{
    private readonly string _root;

    public CsvSessionLoggerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rollergraph-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void BeginSession_CreatesDirectoryAndFileWithHeader()
    {
        var logger = new CsvSessionLogger(_root);
        var path = logger.BeginSession(new DateTime(2025, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        File.Exists(path).ShouldBeTrue();
        logger.IsActive.ShouldBeTrue();
        logger.CurrentFilePath.ShouldBe(path);

        // Close the writer before reading so this test works on Windows,
        // where the writer's exclusive write handle prevents File.ReadAllText
        // from opening the file even though FileShare.Read is set.
        logger.EndSession();

        var contents = File.ReadAllText(path);
        contents.ShouldStartWith("timestamp_utc,raw_line");
    }

    [Fact]
    public void Append_WhenActive_WritesTimestampAndLine()
    {
        using var logger = new CsvSessionLogger(_root);
        var path = logger.BeginSession();
        logger.Append("1,2,3,400", new DateTime(2025, 6, 10, 12, 0, 0, DateTimeKind.Utc));
        logger.EndSession();

        var lines = File.ReadAllLines(path);
        lines.Length.ShouldBe(2);
        lines[1].ShouldContain("2025-06-10T12:00:00");
        // Raw line contains commas so the CSV field is quoted.
        lines[1].ShouldEndWith("\"1,2,3,400\"");
    }

    [Fact]
    public void Append_LineWithCommasAndQuotes_IsCsvEscaped()
    {
        using var logger = new CsvSessionLogger(_root);
        var path = logger.BeginSession();
        logger.Append("a,b,\"c\"", DateTime.UtcNow);
        logger.EndSession();

        var dataLine = File.ReadAllLines(path)[1];
        dataLine.ShouldEndWith("\"a,b,\"\"c\"\"\"");
    }

    [Fact]
    public void Append_WhenNotActive_IsNoOp()
    {
        using var logger = new CsvSessionLogger(_root);
        Should.NotThrow(() => logger.Append("anything", DateTime.UtcNow));
        logger.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void BeginSession_TwiceWithSameTimestamp_GeneratesDistinctFile()
    {
        using var logger = new CsvSessionLogger(_root);
        var t = new DateTime(2025, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var p1 = logger.BeginSession(t);
        var p2 = logger.BeginSession(t);
        p1.ShouldNotBe(p2);
        File.Exists(p1).ShouldBeTrue();
        File.Exists(p2).ShouldBeTrue();
    }

    [Fact]
    public void EndSession_ClosesFile()
    {
        using var logger = new CsvSessionLogger(_root);
        var path = logger.BeginSession();
        logger.EndSession();
        logger.IsActive.ShouldBeFalse();
        logger.CurrentFilePath.ShouldBeNull();
        // The file should be readable and not locked.
        Should.NotThrow(() => File.ReadAllText(path));
    }

    [Fact]
    public void DefaultLogDirectory_ReturnsRollerGraphPathUnderLocalAppData()
    {
        var p = CsvSessionLogger.DefaultLogDirectory();
        p.ShouldContain("RollerGraph");
        p.ShouldEndWith("logs");
    }
}

using RollerGraph.Core.Serial;
using Shouldly;

namespace RollerGraph.Core.Tests;

public class LineBufferTests
{
    [Fact]
    public void Append_SingleCompleteLine_EmitsOne()
    {
        var buf = new LineBuffer();
        var lines = buf.Append("hello\n").ToList();
        lines.ShouldHaveSingleItem();
        lines[0].ShouldBe("hello");
    }

    [Fact]
    public void Append_NoNewline_BuffersWithoutEmitting()
    {
        var buf = new LineBuffer();
        buf.Append("partial").ToList().ShouldBeEmpty();
        buf.Append(" more\n").ToList().ShouldBe(new[] { "partial more" });
    }

    [Fact]
    public void Append_MultipleLinesInOneCall_EmitsAll()
    {
        var buf = new LineBuffer();
        var lines = buf.Append("a\nb\nc\n").ToList();
        lines.ShouldBe(new[] { "a", "b", "c" });
    }

    [Fact]
    public void Append_CrLf_HandledCorrectly()
    {
        var buf = new LineBuffer();
        var lines = buf.Append("one\r\ntwo\r\n").ToList();
        lines.ShouldBe(new[] { "one", "two" });
    }

    [Fact]
    public void Append_EmptyLines_AreSkipped()
    {
        var buf = new LineBuffer();
        var lines = buf.Append("\n\nfoo\n\nbar\n").ToList();
        lines.ShouldBe(new[] { "foo", "bar" });
    }

    [Fact]
    public void Append_LineSplitAcrossCalls_ReassembledCorrectly()
    {
        var buf = new LineBuffer();
        buf.Append("part").ToList().ShouldBeEmpty();
        buf.Append("ial").ToList().ShouldBeEmpty();
        var lines = buf.Append("\nnext\n").ToList();
        lines.ShouldBe(new[] { "partial", "next" });
    }

    [Fact]
    public void Clear_DropsPartialLine()
    {
        var buf = new LineBuffer();
        buf.Append("incomplete");
        buf.Clear();
        var lines = buf.Append("\n").ToList();
        lines.ShouldBeEmpty();
    }
}

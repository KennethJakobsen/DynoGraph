using System.Text;

namespace RollerGraph.Core.Serial;

/// <summary>
/// Buffers incoming bytes/text and emits complete lines split on CR/LF.
/// Handles arbitrary read boundaries (a line may arrive across multiple Append calls).
/// </summary>
public sealed class LineBuffer
{
    private readonly StringBuilder _buffer = new();

    /// <summary>
    /// Appends new data and yields any complete lines that resulted.
    /// Lines are returned without their terminating CR/LF characters.
    /// Empty lines (consecutive newlines) are skipped.
    /// </summary>
    public IEnumerable<string> Append(ReadOnlySpan<char> data)
    {
        _buffer.Append(data);
        return ExtractLines();
    }

    /// <summary>
    /// Convenience overload for string input.
    /// </summary>
    public IEnumerable<string> Append(string data) => Append(data.AsSpan());

    private IEnumerable<string> ExtractLines()
    {
        var result = new List<string>();
        int start = 0;
        for (int i = 0; i < _buffer.Length; i++)
        {
            var c = _buffer[i];
            if (c == '\n' || c == '\r')
            {
                if (i > start)
                {
                    var line = _buffer.ToString(start, i - start);
                    result.Add(line);
                }
                start = i + 1;
            }
        }

        if (start > 0)
        {
            _buffer.Remove(0, start);
        }

        return result;
    }

    /// <summary>Discards any buffered partial line.</summary>
    public void Clear() => _buffer.Clear();
}

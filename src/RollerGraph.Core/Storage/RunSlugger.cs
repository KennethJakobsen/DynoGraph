using System.Text;

namespace RollerGraph.Core.Storage;

/// <summary>
/// Converts free-form names into filesystem-safe identifiers.
/// Lowercase, [a-z0-9] preserved, everything else becomes '-', consecutive
/// hyphens collapsed, leading/trailing hyphens trimmed. Empty input maps to
/// the literal "run" so a file always has a name.
/// </summary>
public static class RunSlugger
{
    /// <summary>The slug used when the input is empty or contains no slug-safe characters.</summary>
    public const string FallbackSlug = "run";

    /// <summary>Returns the canonical slug for the given name.</summary>
    public static string Slugify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return FallbackSlug;

        var sb = new StringBuilder(input.Length);
        bool lastHyphen = false;
        foreach (var ch in input.Trim())
        {
            char lower = char.ToLowerInvariant(ch);
            if ((lower >= 'a' && lower <= 'z') || (lower >= '0' && lower <= '9'))
            {
                sb.Append(lower);
                lastHyphen = false;
            }
            else
            {
                if (!lastHyphen && sb.Length > 0)
                {
                    sb.Append('-');
                    lastHyphen = true;
                }
            }
        }
        while (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;
        return sb.Length == 0 ? FallbackSlug : sb.ToString();
    }

    /// <summary>True when two names map to the same slug (case- and punctuation-insensitive).</summary>
    public static bool AreSameSlug(string? a, string? b) =>
        string.Equals(Slugify(a), Slugify(b), StringComparison.Ordinal);
}

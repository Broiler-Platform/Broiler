using System;

namespace Broiler.Code.Workspaces.Text;

public readonly record struct TextMatch(int Start, int Length)
{
    public int End => Start + Length;
}

public readonly record struct SearchOptions(
    bool MatchCase = false,
    bool WholeWord = false)
{
    public static SearchOptions Default => new();
}

/// <summary>
/// Find over a snapshot, scanning in bounded windows.
///
/// The naive implementation calls <c>ToString()</c> and uses
/// <c>string.IndexOf</c>. On the 10 MiB fixture that allocates the document per
/// keystroke in an incremental-search box, which is the cost this whole layer
/// exists to avoid.
/// </summary>
public static class TextSearch
{
    private const int WindowLength = 8192;

    public static TextMatch? FindNext(
        TextSnapshot snapshot, string pattern, int from, SearchOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0 || pattern.Length > snapshot.Length)
            return null;

        from = Math.Clamp(from, 0, snapshot.Length);
        StringComparison comparison = options.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        // Windows overlap by pattern.Length - 1 so a match straddling a window
        // boundary is still found.
        int overlap = pattern.Length - 1;
        int start = from;
        while (start < snapshot.Length)
        {
            int length = Math.Min(WindowLength, snapshot.Length - start);
            string window = snapshot.GetText(start, length);
            int index = 0;
            while (index <= window.Length - pattern.Length)
            {
                int found = window.IndexOf(pattern, index, comparison);
                if (found < 0)
                    break;

                int absolute = start + found;
                if (!options.WholeWord || IsWholeWord(snapshot, absolute, pattern.Length))
                    return new TextMatch(absolute, pattern.Length);
                index = found + 1;
            }

            if (start + length >= snapshot.Length)
                break;
            start += length - overlap;
        }

        return null;
    }

    public static TextMatch? FindPrevious(
        TextSnapshot snapshot, string pattern, int before, SearchOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0 || pattern.Length > snapshot.Length)
            return null;

        before = Math.Clamp(before, 0, snapshot.Length);
        StringComparison comparison = options.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        int overlap = pattern.Length - 1;
        int end = before;
        while (end > 0)
        {
            int start = Math.Max(0, end - WindowLength);
            string window = snapshot.GetText(start, end - start);
            int limit = window.Length - pattern.Length;
            for (int i = limit; i >= 0; i--)
            {
                if (!window.AsSpan(i, pattern.Length).Equals(pattern, comparison))
                    continue;
                int absolute = start + i;
                if (absolute + pattern.Length > before)
                    continue;
                if (!options.WholeWord || IsWholeWord(snapshot, absolute, pattern.Length))
                    return new TextMatch(absolute, pattern.Length);
            }

            if (start == 0)
                break;
            end = start + overlap;
        }

        return null;
    }

    private static bool IsWholeWord(TextSnapshot snapshot, int start, int length)
    {
        if (start > 0 && IsWordCharacter(snapshot[start - 1]))
            return false;
        int end = start + length;
        return end >= snapshot.Length || !IsWordCharacter(snapshot[end]);
    }

    private static bool IsWordCharacter(char c) => c == '_' || char.IsLetterOrDigit(c);
}

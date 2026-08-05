using System;

namespace Broiler.UI.CodeEditor.Standard;

/// <summary>
/// Monospace column arithmetic with tab expansion.
///
/// A source editor cannot treat one character as one column: a tab advances to
/// the next tab stop, so the same character index sits at a different column
/// depending on what precedes it. Hit testing, caret movement, and selection
/// rectangles all go through here so they cannot disagree.
/// </summary>
internal static class CodeLineLayout
{
    /// <summary>Visual column of a character index within a line.</summary>
    public static int VisualColumn(ReadOnlySpan<char> line, int index, int tabSize)
    {
        int column = 0;
        int limit = Math.Min(index, line.Length);
        for (int i = 0; i < limit; i++)
        {
            if (line[i] == '\t')
                column += tabSize - (column % tabSize);
            else if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
                continue;  // The pair advances once, on the low surrogate.
            else
                column++;
        }

        return column;
    }

    /// <summary>
    /// Character index nearest a visual column. Ties round to the closer edge so
    /// clicking the right half of a glyph puts the caret after it.
    /// </summary>
    public static int IndexFromVisualColumn(ReadOnlySpan<char> line, int column, int tabSize)
    {
        if (column <= 0)
            return 0;

        int current = 0;
        for (int i = 0; i < line.Length; i++)
        {
            int width;
            if (line[i] == '\t')
            {
                width = tabSize - (current % tabSize);
            }
            else if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
            {
                // A surrogate pair is one column and two indices.
                if (column < current + 1)
                    return column - current >= 1 ? i + 2 : i;
                current++;
                i++;
                continue;
            }
            else
            {
                width = 1;
            }

            if (column < current + width)
                return column - current >= (width + 1) / 2 ? i + 1 : i;
            current += width;
        }

        return line.Length;
    }

    /// <summary>Total columns a line occupies, for horizontal extent.</summary>
    public static int ColumnWidth(ReadOnlySpan<char> line, int tabSize) =>
        VisualColumn(line, line.Length, tabSize);
}

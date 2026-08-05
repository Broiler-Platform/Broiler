using System;
using System.Collections.Generic;
using Broiler.UI.CodeEditor;
namespace Broiler.Code.Language.CSharp.Syntax;

/// <summary>
/// The line-oriented C# lexer behind the portable classifier. It carries no
/// state between calls: cancellation is cooperative, so a superseded run keeps
/// going until it next checks its token and two runs overlap routinely. Shared
/// mutable state here would corrupt both.
///
/// Known approximations, all deliberate. The semantic service is authoritative
/// where they differ:
///
/// <list type="bullet">
/// <item>Contextual keywords are limited to an unambiguous subset. A local
/// variable named <c>value</c> or <c>from</c> stays an identifier rather than
/// being miscolored as a keyword.</item>
/// <item>Interpolation holes are classified as part of their string literal;
/// the expressions inside them are not lexed as code.</item>
/// <item>Identifiers get no span at all, so the editor paints them in the
/// default foreground. Distinguishing types from locals needs binding.</item>
/// <item><c>#if</c> regions are not dimmed: which branch is live depends on the
/// evaluated graph's defines, which this classifier deliberately cannot see.</item>
/// </list>
/// </summary>
internal static class CSharpLineLexer
{
    internal static LineState Lex(
        ReadOnlySpan<char> line,
        LineState state,
        List<CodeClassificationSpan> output)
    {
        int i = 0;

        switch (state.Kind)
        {
            case LineStateKind.BlockComment:
            case LineStateKind.DocumentationBlockComment:
            {
                CodeClassificationKind kind = state.Kind == LineStateKind.BlockComment
                    ? CodeClassificationKind.Comment
                    : CodeClassificationKind.DocumentationComment;
                int end = IndexOfBlockCommentEnd(line);
                if (end < 0)
                {
                    Emit(output, 0, line.Length, kind);
                    return state;
                }

                Emit(output, 0, end, kind);
                i = end;
                state = LineState.Default;
                break;
            }

            case LineStateKind.VerbatimString:
            {
                i = ContinueVerbatimString(line, 0, state.DollarCount, output, out bool closed);
                if (!closed)
                    return state;
                state = LineState.Default;
                break;
            }

            case LineStateKind.RawString:
            {
                i = ContinueRawString(line, 0, state.QuoteCount, state.DollarCount, output, out bool closed);
                if (!closed)
                    return state;
                state = LineState.Default;
                break;
            }
        }

        // A directive is only a directive when '#' is the first non-whitespace
        // character of the line and nothing has been lexed on it yet.
        if (output.Count == 0)
        {
            int probe = i;
            while (probe < line.Length && (line[probe] == ' ' || line[probe] == '\t'))
                probe++;
            if (probe < line.Length && line[probe] == '#')
            {
                LexDirective(line, probe, output);
                return LineState.Default;
            }
        }

        while (i < line.Length)
        {
            char c = line[i];

            if (c is ' ' or '\t')
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < line.Length)
            {
                char n = line[i + 1];
                if (n == '/')
                {
                    bool doc = i + 2 < line.Length && line[i + 2] == '/' &&
                        (i + 3 >= line.Length || line[i + 3] != '/');
                    Emit(output, i, line.Length - i,
                        doc ? CodeClassificationKind.DocumentationComment : CodeClassificationKind.Comment);
                    return LineState.Default;
                }

                if (n == '*')
                {
                    bool doc = i + 2 < line.Length && line[i + 2] == '*' &&
                        (i + 3 >= line.Length || line[i + 3] != '/');
                    CodeClassificationKind kind = doc
                        ? CodeClassificationKind.DocumentationComment
                        : CodeClassificationKind.Comment;
                    int end = IndexOfBlockCommentEnd(line[(i + 2)..]);
                    if (end < 0)
                    {
                        Emit(output, i, line.Length - i, kind);
                        return new LineState(
                            doc ? LineStateKind.DocumentationBlockComment : LineStateKind.BlockComment,
                            0,
                            0);
                    }

                    Emit(output, i, end + 2, kind);
                    i += end + 2;
                    continue;
                }
            }

            if (c is '"' or '\'' or '@' or '$')
            {
                int consumed = LexLiteral(line, i, output, out LineState pending, out bool isLiteral);
                if (isLiteral)
                {
                    if (pending.Kind != LineStateKind.Default)
                        return pending;
                    i = consumed;
                    continue;
                }
            }

            if (char.IsAsciiDigit(c) ||
                (c == '.' && i + 1 < line.Length && char.IsAsciiDigit(line[i + 1])))
            {
                int start = i;
                i = ScanNumber(line, i);
                Emit(output, start, i - start, CodeClassificationKind.NumericLiteral);
                continue;
            }

            if (IsIdentifierStart(c))
            {
                int start = i;
                bool verbatim = c == '@';
                if (verbatim)
                    i++;
                while (i < line.Length && IsIdentifierPart(line[i]))
                    i++;
                if (i == start)
                {
                    i++;
                    continue;
                }

                if (!verbatim)
                {
                    CodeClassificationKind kind = KeywordTable.Lookup(line[start..i]);
                    if (kind != CodeClassificationKind.None)
                        Emit(output, start, i - start, kind);
                }

                continue;
            }

            if (IsPunctuation(c))
            {
                Emit(output, i, 1, CodeClassificationKind.Punctuation);
                i++;
                continue;
            }

            if (IsOperator(c))
            {
                int start = i;
                while (i < line.Length && IsOperator(line[i]))
                    i++;
                Emit(output, start, i - start, CodeClassificationKind.Operator);
                continue;
            }

            i++;
        }

        return LineState.Default;
    }

    private static void LexDirective(ReadOnlySpan<char> line, int hash, List<CodeClassificationSpan> output)
    {
        int i = hash + 1;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;
        int nameStart = i;
        while (i < line.Length && char.IsAsciiLetter(line[i]))
            i++;

        // '#' and the directive name read as one keyword, matching how the
        // directive is written and spoken.
        Emit(output, hash, i - hash, CodeClassificationKind.PreprocessorKeyword);
        if (i > nameStart && i < line.Length)
            Emit(output, i, line.Length - i, CodeClassificationKind.PreprocessorText);
    }

    /// <summary>
    /// Lexes a string, character, or raw-string literal starting at
    /// <paramref name="i"/>. Sets <paramref name="isLiteral"/> to false when the
    /// character turned out to be an ordinary <c>@</c> or <c>$</c>.
    /// </summary>
    private static int LexLiteral(
        ReadOnlySpan<char> line,
        int i,
        List<CodeClassificationSpan> output,
        out LineState pending,
        out bool isLiteral)
    {
        pending = LineState.Default;
        isLiteral = true;

        int start = i;
        byte dollars = 0;
        bool verbatim = false;

        while (i < line.Length && (line[i] == '$' || line[i] == '@'))
        {
            if (line[i] == '@')
                verbatim = true;
            else
                dollars++;
            i++;
        }

        if (i >= line.Length)
        {
            isLiteral = false;
            return start;
        }

        char quote = line[i];
        if (quote == '\'' && !verbatim && dollars == 0)
            return LexCharacter(line, i, output);

        if (quote != '"')
        {
            isLiteral = false;
            return start;
        }

        int quoteRun = 0;
        while (i + quoteRun < line.Length && line[i + quoteRun] == '"')
            quoteRun++;

        if (quoteRun >= 3 && !verbatim)
        {
            Emit(output, start, (i - start) + quoteRun, CodeClassificationKind.StringLiteral);
            int after = ContinueRawString(
                line, i + quoteRun, (byte)quoteRun, dollars, output, out bool rawClosed);
            if (!rawClosed)
            {
                pending = new LineState(LineStateKind.RawString, (byte)quoteRun, dollars);
                return line.Length;
            }

            return after;
        }

        Emit(output, start, (i - start) + 1, CodeClassificationKind.StringLiteral);
        i++;

        if (verbatim)
        {
            int after = ContinueVerbatimString(line, i, dollars, output, out bool closed);
            if (!closed)
            {
                pending = new LineState(LineStateKind.VerbatimString, 0, dollars);
                return line.Length;
            }

            return after;
        }

        // A regular string never crosses a line: an unterminated one is
        // classified to end of line and the next line starts clean.
        int run = i;
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '\\' && i + 1 < line.Length)
            {
                Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                Emit(output, i, 2, CodeClassificationKind.EscapeSequence);
                i += 2;
                run = i;
                continue;
            }

            if (dollars > 0 && (c == '{' || c == '}') && i + 1 < line.Length && line[i + 1] == c)
            {
                Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                Emit(output, i, 2, CodeClassificationKind.EscapeSequence);
                i += 2;
                run = i;
                continue;
            }

            if (c == '"')
            {
                i++;
                Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                return i;
            }

            i++;
        }

        Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
        return i;
    }

    private static int LexCharacter(ReadOnlySpan<char> line, int i, List<CodeClassificationSpan> output)
    {
        int start = i;
        i++;
        while (i < line.Length)
        {
            if (line[i] == '\\' && i + 1 < line.Length)
            {
                i += 2;
                continue;
            }

            if (line[i] == '\'')
            {
                i++;
                break;
            }

            i++;
        }

        Emit(output, start, i - start, CodeClassificationKind.CharacterLiteral);
        return i;
    }

    private static int ContinueVerbatimString(
        ReadOnlySpan<char> line,
        int i,
        byte dollars,
        List<CodeClassificationSpan> output,
        out bool closed)
    {
        int run = i;
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                    Emit(output, i, 2, CodeClassificationKind.EscapeSequence);
                    i += 2;
                    run = i;
                    continue;
                }

                i++;
                Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                closed = true;
                return i;
            }

            if (dollars > 0 && (c == '{' || c == '}') && i + 1 < line.Length && line[i + 1] == c)
            {
                Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                Emit(output, i, 2, CodeClassificationKind.EscapeSequence);
                i += 2;
                run = i;
                continue;
            }

            i++;
        }

        Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
        closed = false;
        return i;
    }

    private static int ContinueRawString(
        ReadOnlySpan<char> line,
        int i,
        byte quoteCount,
        byte dollars,
        List<CodeClassificationSpan> output,
        out bool closed)
    {
        int run = i;
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '"')
            {
                int quotes = 0;
                while (i + quotes < line.Length && line[i + quotes] == '"')
                    quotes++;
                if (quotes >= quoteCount)
                {
                    i += quotes;
                    Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                    closed = true;
                    return i;
                }

                i += quotes;
                continue;
            }

            if (dollars > 0 && (c == '{' || c == '}') && i + 1 < line.Length && line[i + 1] == c)
            {
                Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
                Emit(output, i, 2, CodeClassificationKind.EscapeSequence);
                i += 2;
                run = i;
                continue;
            }

            i++;
        }

        Flush(output, ref run, i, CodeClassificationKind.StringLiteral);
        closed = false;
        return i;
    }

    private static int ScanNumber(ReadOnlySpan<char> line, int i)
    {
        if (line[i] == '0' && i + 1 < line.Length && (line[i + 1] is 'x' or 'X' or 'b' or 'B'))
        {
            i += 2;
            while (i < line.Length && (char.IsAsciiLetterOrDigit(line[i]) || line[i] == '_'))
                i++;
            return i;
        }

        bool seenDot = false;
        while (i < line.Length)
        {
            char c = line[i];
            if (char.IsAsciiDigit(c) || c == '_')
            {
                i++;
                continue;
            }

            if (c == '.' && !seenDot && i + 1 < line.Length && char.IsAsciiDigit(line[i + 1]))
            {
                seenDot = true;
                i++;
                continue;
            }

            if ((c is 'e' or 'E') && i + 1 < line.Length &&
                (char.IsAsciiDigit(line[i + 1]) || line[i + 1] is '+' or '-'))
            {
                i += 2;
                continue;
            }

            if (c is 'f' or 'F' or 'd' or 'D' or 'm' or 'M' or 'u' or 'U' or 'l' or 'L')
            {
                i++;
                continue;
            }

            break;
        }

        return i;
    }

    private static int IndexOfBlockCommentEnd(ReadOnlySpan<char> span)
    {
        int index = span.IndexOf("*/", StringComparison.Ordinal);
        return index < 0 ? -1 : index + 2;
    }

    private static void Emit(List<CodeClassificationSpan> output, int start, int length, CodeClassificationKind kind)
    {
        if (length > 0)
            output.Add(new CodeClassificationSpan(start, length, kind));
    }

    private static void Flush(
        List<CodeClassificationSpan> output,
        ref int runStart,
        int end,
        CodeClassificationKind kind)
    {
        if (end > runStart)
            output.Add(new CodeClassificationSpan(runStart, end - runStart, kind));
        runStart = end;
    }

    private static bool IsIdentifierStart(char c) =>
        c == '_' || c == '@' || char.IsLetter(c);

    private static bool IsIdentifierPart(char c) =>
        c == '_' || char.IsLetterOrDigit(c);

    private static bool IsPunctuation(char c) =>
        c is '(' or ')' or '[' or ']' or '{' or '}' or ';' or ',' or '.' or ':';

    private static bool IsOperator(char c) =>
        c is '+' or '-' or '*' or '/' or '%' or '=' or '<' or '>' or '!' or
            '&' or '|' or '^' or '~' or '?';
}

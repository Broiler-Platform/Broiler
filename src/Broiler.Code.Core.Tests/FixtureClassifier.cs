using Broiler.UI.CodeEditor;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// A deterministic, incremental classifier for Phase 1. Real C# classification
/// arrives in Phase 3; the control's tests must not depend on a language
/// service, and a fixture grammar makes every expectation checkable by hand.
///
/// The grammar is small but has the property that matters: a line's meaning
/// depends on the state the previous line ended in. <c>/*</c> opens a comment
/// that runs until <c>*/</c>, so an edit near the top can change how every
/// following line is classified — which is what the incremental path has to get
/// right.
/// </summary>
public sealed class FixtureClassifier : ICodeClassifier
{
    /// <summary>Lines the last call actually re-lexed, for the incrementality assertions.</summary>
    public int LastLinesLexed { get; private set; }

    public CodeClassificationResult Classify(
        ICodeTextSnapshot snapshot,
        CodeClassificationResult? previous,
        CodeTextChange? change,
        CancellationToken cancellationToken)
    {
        if (previous is Result prior && change is { } delta &&
            prior.Snapshot.Version + 1 == snapshot.Version)
        {
            return Incremental(snapshot, prior, delta, cancellationToken);
        }

        return Full(snapshot, cancellationToken);
    }

    private Result Full(ICodeTextSnapshot snapshot, CancellationToken cancellationToken)
    {
        var lines = new LineResult[snapshot.LineCount];
        bool inComment = false;
        for (int i = 0; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines[i] = ClassifyLine(snapshot, i, inComment);
            inComment = lines[i].EndsInComment;
        }

        LastLinesLexed = lines.Length;
        return new Result(snapshot, lines);
    }

    private Result Incremental(
        ICodeTextSnapshot snapshot, Result previous, CodeTextChange change, CancellationToken cancellationToken)
    {
        int firstLine = previous.Snapshot.GetLineFromPosition(
            Math.Clamp(change.Start, 0, previous.Snapshot.Length));
        int oldLastLine = previous.Snapshot.GetLineFromPosition(
            Math.Clamp(change.OldEnd, 0, previous.Snapshot.Length));
        int newLastLine = snapshot.GetLineFromPosition(Math.Clamp(change.NewEnd, 0, snapshot.Length));
        int lineDelta = snapshot.LineCount - previous.Snapshot.LineCount;

        var lines = new LineResult[snapshot.LineCount];
        Array.Copy(previous.Lines, lines, Math.Min(firstLine, lines.Length));

        bool inComment = firstLine > 0 && previous.Lines[firstLine - 1].EndsInComment;
        int lexed = 0;
        int i = firstLine;
        for (; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Past the edit, a matching start state means this line and every
            // line after it classify exactly as they did before.
            if (i > newLastLine)
            {
                int oldIndex = i - lineDelta;
                if (oldIndex > oldLastLine && oldIndex < previous.Lines.Length &&
                    (oldIndex == 0 ? false : previous.Lines[oldIndex - 1].EndsInComment) == inComment)
                {
                    break;
                }
            }

            lines[i] = ClassifyLine(snapshot, i, inComment);
            inComment = lines[i].EndsInComment;
            lexed++;
        }

        if (i < lines.Length)
            Array.Copy(previous.Lines, i - lineDelta, lines, i, lines.Length - i);

        LastLinesLexed = lexed;
        return new Result(snapshot, lines);
    }

    private static LineResult ClassifyLine(ICodeTextSnapshot snapshot, int line, bool inComment)
    {
        int length = snapshot.GetLineLength(line);
        if (length == 0)
            return new LineResult([], inComment);

        string text = snapshot.GetText(snapshot.GetLineStart(line), length);
        var spans = new List<CodeClassificationSpan>();
        int i = 0;

        while (i < text.Length)
        {
            if (inComment)
            {
                int end = text.IndexOf("*/", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    spans.Add(new CodeClassificationSpan(i, text.Length - i, CodeClassificationKind.Comment));
                    return new LineResult([.. spans], true);
                }

                spans.Add(new CodeClassificationSpan(i, end + 2 - i, CodeClassificationKind.Comment));
                i = end + 2;
                inComment = false;
                continue;
            }

            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                inComment = true;
                continue;
            }

            if (char.IsAsciiDigit(text[i]))
            {
                int start = i;
                while (i < text.Length && char.IsAsciiDigit(text[i]))
                    i++;
                spans.Add(new CodeClassificationSpan(start, i - start, CodeClassificationKind.NumericLiteral));
                continue;
            }

            if (char.IsAsciiLetter(text[i]))
            {
                int start = i;
                while (i < text.Length && char.IsAsciiLetterOrDigit(text[i]))
                    i++;
                if (text.AsSpan(start, i - start) is "int" or "return" or "public")
                    spans.Add(new CodeClassificationSpan(start, i - start, CodeClassificationKind.Keyword));
                continue;
            }

            i++;
        }

        return new LineResult([.. spans], inComment);
    }

    private readonly record struct LineResult(CodeClassificationSpan[] Spans, bool EndsInComment);

    private sealed class Result(ICodeTextSnapshot snapshot, LineResult[] lines) : CodeClassificationResult
    {
        public LineResult[] Lines { get; } = lines;

        public override ICodeTextSnapshot Snapshot { get; } = snapshot;

        public override int LineCount => Lines.Length;

        public override ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line) =>
            line >= 0 && line < Lines.Length ? Lines[line].Spans : default;
    }
}

/// <summary>Drains posted work on demand, so a test states when a completion lands.</summary>
public sealed class DeferredDispatcher : Broiler.UI.IUiDispatcher
{
    private readonly Queue<Action> _pending = new();

    public int PendingCount
    {
        get
        {
            lock (_pending)
                return _pending.Count;
        }
    }

    public bool CheckAccess() => true;

    public void Post(Action action)
    {
        lock (_pending)
            _pending.Enqueue(action);
    }

    public int Drain()
    {
        int count = 0;
        while (true)
        {
            Action next;
            lock (_pending)
            {
                if (_pending.Count == 0)
                    return count;
                next = _pending.Dequeue();
            }

            next();
            count++;
        }
    }
}

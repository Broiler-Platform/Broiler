using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Language.CSharp.Syntax;

/// <summary>
/// The Roslyn-free portable C# classifier, composed into every host — including
/// the ones that cannot afford the semantic service.
///
/// A host running on this alone reports classification-only mode. That is a
/// visible state rather than an absence of one: the absence of squiggles here
/// means nothing has looked for errors, not that there are none.
///
/// Its approximations are documented on <see cref="CSharpLineLexer"/>. Where
/// they differ from the semantic service, the semantic service is right.
/// </summary>
public sealed class PortableCSharpClassifier : ICodeClassifier
{
    /// <summary>
    /// How many lines the last call actually re-lexed. The Phase 0 full-reparse
    /// budget is stated against this rather than against elapsed time, because
    /// it is the only figure that is identical on every machine.
    /// </summary>
    public int LastLinesLexed { get; private set; }

    public CodeClassificationResult Classify(
        ICodeTextSnapshot snapshot,
        CodeClassificationResult? previous,
        CodeTextChange? change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return previous is Result prior && change is { } delta &&
            prior.Snapshot.Version + 1 == snapshot.Version
            ? Incremental(snapshot, prior, delta, cancellationToken)
            : Full(snapshot, cancellationToken);
    }

    private Result Full(ICodeTextSnapshot snapshot, CancellationToken cancellationToken)
    {
        var lines = new Line[snapshot.LineCount];
        var scratch = new List<CodeClassificationSpan>(64);
        var reader = new LineReader();
        LineState state = LineState.Default;

        for (int i = 0; i < lines.Length; i++)
        {
            if ((i & 0x3ff) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            lines[i] = ClassifyLine(snapshot, i, state, scratch, ref reader, out state);
        }

        reader.Dispose();
        LastLinesLexed = lines.Length;
        return new Result(snapshot, lines);
    }

    /// <summary>
    /// Reuses <paramref name="previous"/> for every line the change cannot have
    /// affected: lines before it directly, and lines after it as soon as the
    /// recomputed start state matches what the old result recorded.
    /// </summary>
    private Result Incremental(
        ICodeTextSnapshot snapshot,
        Result previous,
        CodeTextChange change,
        CancellationToken cancellationToken)
    {
        ICodeTextSnapshot old = previous.Snapshot;
        int firstLine = old.GetLineFromPosition(Math.Clamp(change.Start, 0, old.Length));
        int oldLastLine = old.GetLineFromPosition(Math.Clamp(change.OldEnd, 0, old.Length));
        int newLastLine = snapshot.GetLineFromPosition(Math.Clamp(change.NewEnd, 0, snapshot.Length));
        int lineDelta = snapshot.LineCount - old.LineCount;

        var lines = new Line[snapshot.LineCount];
        Array.Copy(previous.Lines, lines, Math.Min(firstLine, lines.Length));

        var scratch = new List<CodeClassificationSpan>(64);
        var reader = new LineReader();
        LineState state = firstLine == 0 ? LineState.Default : previous.Lines[firstLine].StartState;

        int lexed = 0;
        int i = firstLine;
        for (; i < lines.Length; i++)
        {
            if ((lexed & 0x3ff) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            // Past the edit, a matching start state means this line and every
            // line after it classify exactly as they did before.
            if (i > newLastLine)
            {
                int oldIndex = i - lineDelta;
                if (oldIndex > oldLastLine && oldIndex < previous.Lines.Length &&
                    previous.Lines[oldIndex].StartState == state)
                {
                    break;
                }
            }

            lines[i] = ClassifyLine(snapshot, i, state, scratch, ref reader, out state);
            lexed++;
        }

        if (i < lines.Length)
            Array.Copy(previous.Lines, i - lineDelta, lines, i, lines.Length - i);

        reader.Dispose();
        LastLinesLexed = lexed;
        return new Result(snapshot, lines);
    }

    private static Line ClassifyLine(
        ICodeTextSnapshot snapshot,
        int line,
        LineState startState,
        List<CodeClassificationSpan> scratch,
        ref LineReader reader,
        out LineState endState)
    {
        scratch.Clear();
        endState = CSharpLineLexer.Lex(reader.Read(snapshot, line), startState, scratch);
        return new Line(startState, scratch.Count == 0 ? [] : [.. scratch]);
    }

    /// <summary>
    /// Copies a line into a pooled buffer. The control-facing snapshot has no
    /// span accessor — it cannot promise contiguous storage — so the lexer is
    /// fed a copy. The buffer is reused across lines and returned to the pool,
    /// so classifying a hundred thousand lines allocates one buffer, not a
    /// hundred thousand strings.
    /// </summary>
    private struct LineReader
    {
        private char[]? _buffer;

        public ReadOnlySpan<char> Read(ICodeTextSnapshot snapshot, int line)
        {
            int length = snapshot.GetLineLength(line);
            if (length == 0)
                return default;

            if (_buffer is null || _buffer.Length < length)
            {
                if (_buffer is not null)
                    ArrayPool<char>.Shared.Return(_buffer);
                _buffer = ArrayPool<char>.Shared.Rent(length);
            }

            snapshot.CopyTo(snapshot.GetLineStart(line), length, _buffer);
            return _buffer.AsSpan(0, length);
        }

        public void Dispose()
        {
            if (_buffer is not null)
                ArrayPool<char>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

    private readonly record struct Line(LineState StartState, CodeClassificationSpan[] Spans);

    private sealed class Result(ICodeTextSnapshot snapshot, PortableCSharpClassifier.Line[] lines)
        : CodeClassificationResult
    {
        internal Line[] Lines { get; } = lines;

        public override ICodeTextSnapshot Snapshot { get; } = snapshot;

        public override int LineCount => Lines.Length;

        public override ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line) =>
            line >= 0 && line < Lines.Length ? Lines[line].Spans : default;
    }
}

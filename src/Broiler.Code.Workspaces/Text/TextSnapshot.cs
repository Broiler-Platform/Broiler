using System;
using System.Collections.Generic;
using System.Globalization;

namespace Broiler.Code.Workspaces.Text;

/// <summary>
/// An immutable view of one document at one version. Every analysis result names
/// the snapshot it came from, and may be applied only while that exact snapshot
/// is current.
///
/// The text is held as a shared balanced tree, so a successor snapshot shares
/// almost all of its predecessor's storage. Nothing here materializes the whole
/// document unless the caller explicitly asks for it with
/// <see cref="ToString"/> — which the editor never does.
/// </summary>
public sealed class TextSnapshot
{
    private readonly RopeNode? _root;

    private TextSnapshot(RopeNode? root, int version)
    {
        _root = root;
        Version = version;
    }

    public static TextSnapshot Empty { get; } = new(null, 0);

    /// <summary>
    /// Monotonic per-document version. Never reused, never decremented, and not
    /// comparable across documents — identity comparison, not this number, is
    /// what decides whether a result may be applied.
    /// </summary>
    public int Version { get; }

    public int Length => _root?.Length ?? 0;

    /// <summary>
    /// Number of lines. A document always has at least one, and a trailing
    /// terminator produces a final empty line — which is what an editor shows
    /// and what line arithmetic needs.
    /// </summary>
    public int LineCount => (_root?.LineBreaks ?? 0) + 1;

    public static TextSnapshot Create(string text, int version = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TextSnapshot(RopeNode.Create(text), version);
    }

    public char this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Length);
            return _root!.CharAt(index);
        }
    }

    /// <summary>Zero-based line containing <paramref name="position"/>.</summary>
    public int GetLineFromPosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Length);
        return _root?.LineBreaksBefore(position) ?? 0;
    }

    public int GetLineStart(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, LineCount);
        return line == 0 ? 0 : _root!.PositionAfterLineBreak(line - 1);
    }

    /// <summary>Length of the line excluding its terminator.</summary>
    public int GetLineLength(int line)
    {
        int start = GetLineStart(line);
        int end = line + 1 < LineCount ? GetLineStart(line + 1) : Length;
        if (end > start && this[end - 1] == '\n')
        {
            end--;
            if (end > start && this[end - 1] == '\r')
                end--;
        }
        else if (end > start && this[end - 1] == '\r')
        {
            end--;
        }

        return end - start;
    }

    /// <summary>Zero-based line and column, for the normalized diagnostic model.</summary>
    public (int Line, int Column) GetLineAndColumn(int position)
    {
        int line = GetLineFromPosition(position);
        return (line, position - GetLineStart(line));
    }

    public int GetPosition(int line, int column)
    {
        int start = GetLineStart(line);
        return Math.Min(start + Math.Max(0, column), start + GetLineLength(line));
    }

    /// <summary>
    /// Copies a range into <paramref name="destination"/> without allocating.
    /// This is what the renderer and the classifier use; both work a visible
    /// range at a time and neither may pull the document into a string.
    /// </summary>
    public void CopyTo(int start, int length, Span<char> destination)
    {
        ValidateRange(start, length);
        if (length == 0)
            return;
        if (destination.Length < length)
            throw new ArgumentException("The destination is too short.", nameof(destination));
        _root!.CopyTo(start, destination[..length]);
    }

    /// <summary>Materializes one range. Bounded by the caller, never by the document.</summary>
    public string GetText(int start, int length)
    {
        ValidateRange(start, length);
        if (length == 0)
            return string.Empty;
        return string.Create(length, (this, start), static (span, state) =>
            state.Item1._root!.CopyTo(state.start, span));
    }

    public string GetLineText(int line) => GetText(GetLineStart(line), GetLineLength(line));

    /// <summary>
    /// Materializes the entire document. Used for saving and for tests; the
    /// editor path never calls it, which is the point of every other member
    /// taking a range.
    /// </summary>
    public override string ToString() => GetText(0, Length);

    internal TextSnapshot WithChange(TextChange change, int version)
    {
        (RopeNode? prefix, RopeNode? rest) = RopeNode.Split(_root, change.Start);
        (_, RopeNode? suffix) = RopeNode.Split(rest, change.OldLength);
        RopeNode? inserted = RopeNode.Create(change.NewText);
        return new TextSnapshot(RopeNode.Concat(RopeNode.Concat(prefix, inserted), suffix), version);
    }

    /// <summary>
    /// The next caret position after <paramref name="position"/>, respecting
    /// surrogate pairs and combining marks. A caret that moves by one char
    /// splits an emoji; a caret that moves by one text element does not.
    /// </summary>
    public int GetNextCaretPosition(int position)
    {
        if (position >= Length)
            return Length;

        // A bounded window is enough: no grapheme cluster in practice is longer
        // than this, and reading the whole document to move the caret one step
        // is exactly what this type exists to avoid.
        int windowLength = Math.Min(Length - position, 64);
        string window = GetText(position, windowLength);
        var enumerator = StringInfo.GetTextElementEnumerator(window);
        return enumerator.MoveNext() ? position + enumerator.GetTextElement().Length : position + 1;
    }

    public int GetPreviousCaretPosition(int position)
    {
        if (position <= 0)
            return 0;

        int windowStart = Math.Max(0, position - 64);
        string window = GetText(windowStart, position - windowStart);
        var enumerator = StringInfo.GetTextElementEnumerator(window);
        int last = 0;
        while (enumerator.MoveNext())
        {
            int elementStart = enumerator.ElementIndex;
            if (windowStart + elementStart >= position)
                break;
            last = elementStart;
        }

        return windowStart + last;
    }

    /// <summary>
    /// Enumerates the leaf spans covering a range, so a consumer can scan text
    /// without copying it. Chunk boundaries are an artifact of the tree and
    /// carry no meaning; a consumer that cares about boundaries must buffer.
    /// </summary>
    public void ForEachChunk(int start, int length, ChunkVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateRange(start, length);
        if (length > 0)
            VisitChunks(_root!, start, length, visitor);
    }

    public delegate void ChunkVisitor(ReadOnlySpan<char> chunk, int position);

    private static void VisitChunks(RopeNode node, int start, int length, ChunkVisitor visitor)
    {
        var pending = new Stack<(RopeNode Node, int Offset)>();
        pending.Push((node, 0));
        int consumed = 0;

        while (pending.Count > 0 && consumed < length)
        {
            (RopeNode current, int offset) = pending.Pop();
            int nodeStart = offset;
            int nodeEnd = offset + current.Length;
            if (nodeEnd <= start || nodeStart >= start + length)
                continue;

            if (current.IsLeaf)
            {
                int from = Math.Max(0, start - nodeStart);
                int to = Math.Min(current.Length, start + length - nodeStart);
                visitor(current.Span[from..to], nodeStart + from);
                consumed += to - from;
                continue;
            }

            // Right first: the stack reverses order, so this visits left to right.
            pending.Push((current.Right!, offset + current.Left!.Length));
            pending.Push((current.Left, offset));
        }
    }

    private void ValidateRange(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (start + length > Length)
            throw new ArgumentOutOfRangeException(nameof(length), "The range extends past the end of the snapshot.");
    }
}

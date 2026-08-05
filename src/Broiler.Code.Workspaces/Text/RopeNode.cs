using System;
using System.Diagnostics;

namespace Broiler.Code.Workspaces.Text;

/// <summary>
/// A node of the immutable balanced tree that backs <see cref="TextSnapshot"/>.
///
/// Phase 0 measured the alternative: a snapshot that holds the whole document as
/// one string costs 17.63 ms and 21.4 MB per keystroke on a 10.5 MB file, and
/// rebuilding its line-start array costs a further 400 KB. Both scale with the
/// document. This tree replaces both. An edit copies only the O(log n) nodes on
/// one root-to-leaf path; every other node is shared with the previous
/// snapshot, and the line index is the per-node line-break counts rather than a
/// separate array.
///
/// A node is either a leaf, which is a slice of a backing string, or a branch.
/// Leaves slice rather than copy, so splitting a document in half allocates two
/// small nodes and no characters.
/// </summary>
internal sealed class RopeNode
{
    /// <summary>
    /// Leaves above this size are split and leaves below it are merged on
    /// concatenation. Small enough that copying one on a merge is cheap, large
    /// enough that a big document does not become millions of nodes.
    /// </summary>
    internal const int MaxLeafLength = 1024;

    private readonly string? _text;
    private readonly int _start;

    private RopeNode(string text, int start, int length, int lineBreaks, bool startsWithLf, bool endsWithCr)
    {
        _text = text;
        _start = start;
        Length = length;
        LineBreaks = lineBreaks;
        StartsWithLf = startsWithLf;
        EndsWithCr = endsWithCr;
        Height = 1;
    }

    private RopeNode(RopeNode left, RopeNode right)
    {
        Left = left;
        Right = right;
        Length = left.Length + right.Length;

        // A CR ending the left side and an LF starting the right side are one
        // line break, not two. Each side counted its own, so one is removed
        // here. Getting this wrong shifts every line number after the boundary,
        // and the boundary moves as the tree rebalances.
        LineBreaks = left.LineBreaks + right.LineBreaks - (SpansCrLf(left, right) ? 1 : 0);
        StartsWithLf = left.StartsWithLf;
        EndsWithCr = right.EndsWithCr;
        Height = (byte)(Math.Max(left.Height, right.Height) + 1);
    }

    public int Length { get; }

    /// <summary>
    /// Line terminators wholly inside this node. A CRLF counts once. A CR at
    /// the very end counts as a break here and is corrected by the parent if an
    /// LF follows.
    /// </summary>
    public int LineBreaks { get; }

    public bool StartsWithLf { get; }

    public bool EndsWithCr { get; }

    public byte Height { get; }

    public RopeNode? Left { get; }

    public RopeNode? Right { get; }

    public bool IsLeaf => _text is not null;

    public ReadOnlySpan<char> Span
    {
        get
        {
            Debug.Assert(_text is not null, "Span is only valid on a leaf.");
            return _text.AsSpan(_start, Length);
        }
    }

    public static bool SpansCrLf(RopeNode left, RopeNode right) =>
        left.EndsWithCr && right.StartsWithLf;

    public static RopeNode? Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? null : Build(text, 0, text.Length);
    }

    public static RopeNode Leaf(string text, int start, int length)
    {
        ReadOnlySpan<char> span = text.AsSpan(start, length);
        return new RopeNode(
            text,
            start,
            length,
            CountLineBreaks(span),
            length > 0 && span[0] == '\n',
            length > 0 && span[^1] == '\r');
    }

    /// <summary>Concatenates two subtrees, rebalancing and merging small leaves.</summary>
    public static RopeNode? Concat(RopeNode? left, RopeNode? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;

        if (left.IsLeaf && right.IsLeaf && left.Length + right.Length <= MaxLeafLength)
        {
            // Merging is the only place characters are copied, and it is bounded
            // by MaxLeafLength. Without it, a long typing burst leaves a tree of
            // one-character leaves whose overhead dwarfs the text.
            string merged = string.Concat(left.Span, right.Span);
            return Leaf(merged, 0, merged.Length);
        }

        if (left.Height > right.Height + 1)
            return Join(left.Left!, Concat(left.Right, right)!);
        if (right.Height > left.Height + 1)
            return Join(Concat(left, right.Left)!, right.Right!);
        return new RopeNode(left, right);
    }

    /// <summary>
    /// Splits at <paramref name="index"/>. Leaves are sliced, so neither side
    /// copies characters.
    /// </summary>
    public static (RopeNode? Left, RopeNode? Right) Split(RopeNode? node, int index)
    {
        if (node is null)
            return (null, null);
        if (index <= 0)
            return (null, node);
        if (index >= node.Length)
            return (node, null);

        if (node.IsLeaf)
        {
            return (
                Leaf(node._text!, node._start, index),
                Leaf(node._text!, node._start + index, node.Length - index));
        }

        int leftLength = node.Left!.Length;
        if (index < leftLength)
        {
            (RopeNode? innerLeft, RopeNode? innerRight) = Split(node.Left, index);
            return (innerLeft, Concat(innerRight, node.Right));
        }

        if (index > leftLength)
        {
            (RopeNode? innerLeft, RopeNode? innerRight) = Split(node.Right, index - leftLength);
            return (Concat(node.Left, innerLeft), innerRight);
        }

        return (node.Left, node.Right);
    }

    public void CopyTo(int start, Span<char> destination)
    {
        if (destination.Length == 0)
            return;

        if (IsLeaf)
        {
            Span.Slice(start, destination.Length).CopyTo(destination);
            return;
        }

        int leftLength = Left!.Length;
        if (start < leftLength)
        {
            int fromLeft = Math.Min(destination.Length, leftLength - start);
            Left.CopyTo(start, destination[..fromLeft]);
            if (fromLeft < destination.Length)
                Right!.CopyTo(0, destination[fromLeft..]);
            return;
        }

        Right!.CopyTo(start - leftLength, destination);
    }

    public char CharAt(int index)
    {
        RopeNode node = this;
        while (!node.IsLeaf)
        {
            int leftLength = node.Left!.Length;
            if (index < leftLength)
            {
                node = node.Left;
            }
            else
            {
                index -= leftLength;
                node = node.Right!;
            }
        }

        return node.Span[index];
    }

    /// <summary>Line breaks strictly before <paramref name="position"/>.</summary>
    public int LineBreaksBefore(int position)
    {
        if (position <= 0)
            return 0;
        if (position >= Length)
            return LineBreaks;

        if (IsLeaf)
            return CountLineBreaks(Span[..position]) - (Span[position - 1] == '\r' && Span[position] == '\n' ? 1 : 0);

        int leftLength = Left!.Length;
        if (position <= leftLength)
            return Left.LineBreaksBefore(position);

        // Everything in Left, minus the CR whose break actually happens at the
        // LF starting Right, plus what Right contributes up to the position.
        int before = Left.LineBreaks - (SpansCrLf(Left, Right!) ? 1 : 0);
        return before + Right!.LineBreaksBefore(position - leftLength);
    }

    /// <summary>
    /// Offset of the first character after line break number
    /// <paramref name="breakIndex"/>, counting from zero.
    /// </summary>
    public int PositionAfterLineBreak(int breakIndex)
    {
        if (IsLeaf)
            return PositionAfterLineBreakInLeaf(Span, breakIndex);

        int leftBreaks = Left!.LineBreaks - (SpansCrLf(Left, Right!) ? 1 : 0);
        if (breakIndex < leftBreaks)
            return Left.PositionAfterLineBreak(breakIndex);

        return Left.Length + Right!.PositionAfterLineBreak(breakIndex - leftBreaks);
    }

    private static int PositionAfterLineBreakInLeaf(ReadOnlySpan<char> span, int breakIndex)
    {
        int seen = 0;
        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (c == '\n')
            {
                if (seen++ == breakIndex)
                    return i + 1;
            }
            else if (c == '\r')
            {
                if (i + 1 < span.Length && span[i + 1] == '\n')
                {
                    if (seen++ == breakIndex)
                        return i + 2;
                    i++;
                }
                else if (seen++ == breakIndex)
                {
                    return i + 1;
                }
            }
        }

        return span.Length;
    }

    private static RopeNode Build(string text, int start, int length)
    {
        if (length <= MaxLeafLength)
            return Leaf(text, start, length);

        int half = length / 2;
        return new RopeNode(Build(text, start, half), Build(text, start + half, length - half));
    }

    /// <summary>
    /// Combines two subtrees whose heights differ by at most two, rotating when
    /// they differ by exactly two. This is the AVL join used by
    /// <see cref="Concat"/>; without it, repeated appends degenerate into a
    /// list and lookups become linear.
    /// </summary>
    private static RopeNode Join(RopeNode left, RopeNode right)
    {
        if (left.Height > right.Height + 1)
        {
            RopeNode innerLeft = left.Left!;
            RopeNode innerRight = left.Right!;
            if (innerLeft.Height >= innerRight.Height)
                return new RopeNode(innerLeft, new RopeNode(innerRight, right));
            return new RopeNode(
                new RopeNode(innerLeft, innerRight.Left!),
                new RopeNode(innerRight.Right!, right));
        }

        if (right.Height > left.Height + 1)
        {
            RopeNode innerLeft = right.Left!;
            RopeNode innerRight = right.Right!;
            if (innerRight.Height >= innerLeft.Height)
                return new RopeNode(new RopeNode(left, innerLeft), innerRight);
            return new RopeNode(
                new RopeNode(left, innerLeft.Left!),
                new RopeNode(innerLeft.Right!, innerRight));
        }

        return new RopeNode(left, right);
    }

    private static int CountLineBreaks(ReadOnlySpan<char> span)
    {
        int breaks = 0;
        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (c == '\n')
            {
                breaks++;
            }
            else if (c == '\r')
            {
                breaks++;
                if (i + 1 < span.Length && span[i + 1] == '\n')
                    i++;
            }
        }

        return breaks;
    }
}

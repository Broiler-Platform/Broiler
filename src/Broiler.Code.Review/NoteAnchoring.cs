using System;
using System.Collections.Generic;

namespace Broiler.Code.Review;

/// <summary>
/// Places recorded notes onto the file as it is now.
///
/// Line numbers are not identity. Inserting one line at the top of a file moves
/// every note below it, and a review tool that stored only line numbers would
/// report all of them against the wrong code after the first edit — which is the
/// failure that makes people stop trusting such a tool and then stop using it.
///
/// So a note stores the text it was written against, and this finds that text
/// again. The rules are deliberately conservative, because a note shown against
/// the wrong code is worse than a note that admits it is lost:
///
/// * the recorded line still holds the recorded text — Anchored, no search;
/// * the text occurs exactly once elsewhere — Moved, the note follows it;
/// * the text occurs several times — Ambiguous, the note stays put and says so;
/// * the text is gone — Orphaned, the note stays put and says so.
///
/// The nearest-occurrence tie-break that a fuzzier tool would use is refused on
/// purpose. Duplicated code is exactly where a reviewer's question matters most,
/// and picking the closest of four identical blocks would answer a question
/// about one of them with a note about another.
/// </summary>
public static class NoteAnchoring
{
    /// <summary>
    /// Places every note in <paramref name="review"/> onto
    /// <paramref name="content"/>, in the record's order.
    /// </summary>
    public static IReadOnlyList<AnchoredNote> Place(FileReview review, string content)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(content);

        if (review.Notes.Count == 0)
            return [];

        string[] lines = SplitLines(ReviewContentHash.Normalize(content));
        var placed = new List<AnchoredNote>(review.Notes.Count);
        foreach (ReviewNote note in review.Notes)
            placed.Add(Place(note, lines));

        return placed;
    }

    /// <summary>Places one note against already-split, already-normalized lines.</summary>
    public static AnchoredNote Place(ReviewNote note, string[] lines)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(lines);

        ReviewAnchor anchor = note.Anchor;
        if (anchor.IsFileLevel)
            return new AnchoredNote(note, ReviewAnchorStatus.FileLevel, -1, -1);

        string[] wanted = SplitLines(ReviewContentHash.Normalize(anchor.AnchorText));
        if (wanted.Length == 0)
        {
            // A blank line, or a record whose anchor carries no text. There is
            // nothing to search for — a blank line matches everywhere — so the
            // note keeps its recorded line and is reported as ambiguous, which
            // is both literally true and the status that tells the reviewer not
            // to trust the line beside it.
            return new AnchoredNote(note, ReviewAnchorStatus.Ambiguous, anchor.StartLine, anchor.EndLine);
        }

        // The recorded position first, so the overwhelmingly common case — a file
        // that has not changed, or has changed below the note — costs a compare
        // rather than a scan of the document.
        if (MatchesAt(lines, wanted, anchor.StartLine))
            return new AnchoredNote(note, ReviewAnchorStatus.Anchored, anchor.StartLine, anchor.StartLine + wanted.Length - 1);

        int found = -1;
        for (int start = 0; start + wanted.Length <= lines.Length; start++)
        {
            if (!MatchesAt(lines, wanted, start))
                continue;

            if (found >= 0)
                return new AnchoredNote(note, ReviewAnchorStatus.Ambiguous, anchor.StartLine, anchor.EndLine);

            found = start;
        }

        return found >= 0
            ? new AnchoredNote(note, ReviewAnchorStatus.Moved, found, found + wanted.Length - 1)
            : new AnchoredNote(note, ReviewAnchorStatus.Orphaned, anchor.StartLine, anchor.EndLine);
    }

    /// <summary>
    /// Builds the anchor for a new note over a line range of
    /// <paramref name="content"/>.
    ///
    /// The captured text is what makes the note findable later, so it is taken
    /// from the same normalized form the hash uses. A range outside the file is
    /// clamped rather than rejected: a caret at the end of a document is a
    /// legitimate place to ask a question from.
    /// </summary>
    public static ReviewAnchor CreateAnchor(string content, int startLine, int endLine, string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        string[] lines = SplitLines(ReviewContentHash.Normalize(content));
        if (lines.Length == 0)
            return ReviewAnchor.File;

        int start = Math.Clamp(startLine, 0, lines.Length - 1);
        int end = Math.Clamp(endLine, start, lines.Length - 1);

        return new ReviewAnchor(start, end, string.Join('\n', lines[start..(end + 1)]), symbol);
    }

    private static bool MatchesAt(string[] lines, string[] wanted, int start)
    {
        if (start < 0 || start + wanted.Length > lines.Length)
            return false;

        for (int i = 0; i < wanted.Length; i++)
        {
            if (!string.Equals(lines[start + i], wanted[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Splits normalized text into lines. A trailing newline does not produce a
    /// final empty line, so a file that gains or loses one does not shift every
    /// note's match by an index.
    /// </summary>
    private static string[] SplitLines(string normalized)
    {
        if (normalized.Length == 0)
            return [];

        string[] lines = normalized.Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}

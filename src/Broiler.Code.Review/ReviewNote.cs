using System;
using System.Collections.Generic;

namespace Broiler.Code.Review;

/// <summary>
/// What a note is for.
///
/// The kinds separate things a reviewer means differently. A Question is
/// answerable and blocks approval; an Observation is neither. Collapsing them
/// would make the count of open notes meaningless, and that count is what keeps
/// a file visible after somebody marks it reviewed.
/// </summary>
public enum ReviewNoteKind
{
    /// <summary>Something the reviewer does not understand and needs answered.</summary>
    Question = 0,

    /// <summary>Something the reviewer believes is wrong or risky.</summary>
    Concern,

    /// <summary>Work the reviewer identified but is not doing now.</summary>
    Todo,

    /// <summary>
    /// Something worth recording that asks for nothing. Never counts as open, so
    /// a reviewer can leave context behind without blocking the file.
    /// </summary>
    Observation,
}

/// <summary>
/// How a note relates to the file as it is now.
///
/// A note is attached to code, not to a line number, because line numbers move
/// under every edit above them. What is stored is the text the note was written
/// against; where that text is found now is recomputed on load.
/// </summary>
public enum ReviewAnchorStatus
{
    /// <summary>The recorded text is still at the recorded line.</summary>
    Anchored = 0,

    /// <summary>
    /// The recorded text was found elsewhere in the file and the note followed it.
    /// The ordinary result of inserting or deleting lines above a note.
    /// </summary>
    Moved,

    /// <summary>
    /// The recorded text appears more than once, so which occurrence the note
    /// meant cannot be decided. The note keeps its recorded line and says so
    /// rather than picking one, because a note silently moved to the wrong
    /// identical line is worse than a note that admits it is unsure.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// The recorded text is gone. The note is kept and shown at its recorded
    /// line: the code a reviewer questioned having disappeared is exactly the
    /// case where the question still needs an answer.
    /// </summary>
    Orphaned,

    /// <summary>The note records no anchor text, so it belongs to the file as a whole.</summary>
    FileLevel,
}

/// <summary>
/// Where a note is attached.
///
/// <see cref="AnchorText"/> is the identity and the lines are a UI hint, which
/// is the reverse of how a diff comment works and is the reason a note survives
/// an edit elsewhere in the file. <see cref="Symbol"/> is carried but never
/// required: the review store must work on a host composing only the portable
/// classifier, and on JSON, Markdown, and shell scripts that have no symbols at
/// all. When a semantic service is present it fills this in, and it is then used
/// for display and search — never as the thing that decides where the note goes.
/// </summary>
/// <param name="StartLine">Zero-based first line, as recorded.</param>
/// <param name="EndLine">Zero-based last line, inclusive, as recorded.</param>
/// <param name="AnchorText">
/// The normalized text of the anchored lines when the note was written. Empty
/// for a note about the whole file.
/// </param>
/// <param name="Symbol">
/// A fully-qualified declaration name, when something could supply one.
/// Display and search only.
/// </param>
public sealed record ReviewAnchor(
    int StartLine,
    int EndLine,
    string AnchorText = "",
    string? Symbol = null)
{
    /// <summary>An anchor for the file as a whole.</summary>
    public static ReviewAnchor File => new(-1, -1);

    /// <summary>
    /// True when this note is about the file rather than a place in it.
    ///
    /// Decided by the line alone. Folding an empty
    /// <see cref="AnchorText"/> in here as well looked equivalent and was not:
    /// a caret on a blank line — ubiquitous between C# members, and exactly
    /// where a reviewer often stops to write something — produces an empty
    /// anchor text with a perfectly good line number, and the note would then
    /// report itself as file-level, be written to disk with no anchor at all,
    /// and lose the reviewer's placement silently. An anchor that cannot be
    /// matched by content is a matching problem, and
    /// <see cref="NoteAnchoring"/> says so; it is not a different kind of note.
    /// </summary>
    public bool IsFileLevel => StartLine < 0;

    /// <summary>The lines this anchor covers, for a caller sizing a selection.</summary>
    public int LineCount => IsFileLevel ? 0 : Math.Max(1, (EndLine - StartLine) + 1);
}

/// <summary>How a note was closed.</summary>
/// <param name="ResolvedAt">When it was closed.</param>
/// <param name="ResolvedBy">Who closed it.</param>
/// <param name="Text">
/// The answer. Required, because "resolved" with no answer records that somebody
/// clicked a button, not that anybody found out.
/// </param>
public sealed record ReviewResolution(
    DateTimeOffset ResolvedAt,
    string ResolvedBy,
    string Text);

/// <summary>
/// One thing a human needs to check, or checked.
///
/// This is the half of the tool that does not belong in the source file. A code
/// comment answers "why does this work this way?" and is part of the
/// implementation; a note answers "what does a human still have to verify here?"
/// and is part of the review. Putting the second kind in the source turns an open
/// question into something that reads like documentation, and leaves it there
/// long after it is answered.
/// </summary>
public sealed record ReviewNote
{
    /// <summary>
    /// Identity within one record, stable across edits and rewrites of it.
    ///
    /// Unique among the notes the record currently holds — <see cref="ReviewJson"/>
    /// mints one for a hand-written note that has none without colliding with
    /// the ids already spelled out. It is <i>not</i> unique across the record's
    /// history: <see cref="FileReview.NextNoteId"/> derives the next id from the
    /// surviving notes, so removing the highest-numbered note frees its id for
    /// the next one written. Keeping a high-water mark would need a field in the
    /// on-disk format, and nothing cross-references note ids between revisions.
    /// </summary>
    public required string Id { get; init; }

    public required ReviewNoteKind Kind { get; init; }

    public required string Text { get; init; }

    public required string Author { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public ReviewAnchor Anchor { get; init; } = ReviewAnchor.File;

    /// <summary>Null while the note is open.</summary>
    public ReviewResolution? Resolution { get; init; }

    /// <summary>
    /// True when this note still wants something from a human. An Observation
    /// never does, which is what lets a reviewer leave one behind on a file they
    /// are approving.
    /// </summary>
    public bool IsOpen => Resolution is null && Kind != ReviewNoteKind.Observation;
}

/// <summary>
/// A note together with where it currently sits, which is recomputed rather than
/// stored. Returned by <see cref="NoteAnchoring"/> and consumed by the review
/// pane and the report.
/// </summary>
/// <param name="Note">The note as recorded.</param>
/// <param name="Status">How its anchor fared against the current content.</param>
/// <param name="StartLine">Where to show it now, zero-based; the recorded line when it could not be placed.</param>
/// <param name="EndLine">Inclusive end of the same.</param>
public readonly record struct AnchoredNote(
    ReviewNote Note,
    ReviewAnchorStatus Status,
    int StartLine,
    int EndLine)
{
    /// <summary>
    /// True when the note could not be placed with confidence and the line shown
    /// beside it is not to be trusted.
    ///
    /// A note that merely <see cref="ReviewAnchorStatus.Moved"/> is deliberately
    /// excluded: following its code down the file is the anchoring working, not
    /// something to warn about.
    /// </summary>
    public bool NeedsAttention =>
        Status is ReviewAnchorStatus.Orphaned or ReviewAnchorStatus.Ambiguous;
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Broiler.Code.Review;

/// <summary>
/// The review record for one file: what a human said about it, against which
/// exact content, and what is still open.
///
/// It lives beside the repository rather than inside the source file, and it is
/// meant to be committed. That is the point of the whole design: once these
/// records are in the history, the platform's claim stops being a sentence in a
/// README and becomes something a machine can check — every source file has a
/// known review state relative to a concrete revision of its content.
///
/// The record is immutable. Every mutation returns a new instance, so a coverage
/// report computed over a set of records can never be changed underneath by an
/// edit happening in the editor at the same time.
/// </summary>
public sealed record FileReview
{
    /// <summary>
    /// The version of the record format this build writes.
    ///
    /// A record carrying a higher number was written by a newer build and may
    /// hold fields this one does not know about. Such a record is read as far as
    /// it parses, and its version is preserved rather than stamped down, so a
    /// round trip through an older build does not silently claim the record is
    /// something it is not.
    ///
    /// It does not, today, stop this build from rewriting such a record and
    /// dropping the fields it did not understand. Nothing here can: the
    /// unrecognized fields are not carried in memory. What the version buys is
    /// that the downgrade is visible in the file afterwards instead of
    /// invisible.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>The reviewed file, relative to the workspace root, forward slashes.</summary>
    public required string Path { get; init; }

    public ReviewStatus Status { get; init; } = ReviewStatus.Unreviewed;

    /// <summary>
    /// Who recorded <see cref="Status"/>. Empty only for a record that carries
    /// notes but no status.
    /// </summary>
    public string Reviewer { get; init; } = string.Empty;

    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>
    /// The hash of the content that was reviewed, from
    /// <see cref="ReviewContentHash"/>. This is what staleness is decided
    /// against, and the only field that must not be edited by hand.
    ///
    /// Settable only inside this assembly, so the two places that may produce
    /// one are <see cref="WithDecision"/>, which hashes the content it is given,
    /// and <see cref="ReviewJson"/>, which reads one back. A consumer cannot
    /// write <c>review with { ReviewedContentHash = "…" }</c> and mint an
    /// approval for content nothing read.
    ///
    /// What this cannot enforce is that the content handed to
    /// <see cref="WithDecision"/> is really the file's. That is the caller's to
    /// get right, and why <see cref="Core.Review.ReviewController"/> takes it
    /// from the buffer and refuses a dirty document.
    /// </summary>
    public string? ReviewedContentHash { get; internal init; }

    /// <summary>
    /// The source revision the file was at when it was reviewed — a git commit
    /// SHA where one was available.
    ///
    /// Provenance, not a check. It answers "which revision was this read at?" for
    /// an auditor and lets a reviewer pull up the exact diff; it never decides
    /// whether a review is current, because a commit is a property of the
    /// repository's history and a review is a statement about content. Null when
    /// nothing could supply one, which is normal for an untracked file, a
    /// detached worktree, or a host with no git.
    /// </summary>
    public string? ReviewedRevision { get; init; }

    public IReadOnlyList<ReviewNote> Notes { get; init; } = [];

    /// <summary>
    /// The record-format version this record was read at, preserved on write so
    /// a newer record's number is not stamped down to this build's.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Notes still waiting on a human.</summary>
    public int OpenNoteCount => Notes.Count(note => note.IsOpen);

    /// <summary>An empty record for a file nobody has touched.</summary>
    public static FileReview Empty(string path) => new() { Path = path };

    /// <summary>
    /// Records a review decision against <paramref name="content"/>.
    ///
    /// The content is hashed here rather than taken as a parameter so that a
    /// caller cannot record an approval against content it never saw. That is a
    /// deliberate narrowing: this record is the evidence for the platform's
    /// human-review claim, and the one way to make it worthless is to let
    /// something mark a file reviewed without reading it.
    /// </summary>
    public FileReview WithDecision(
        ReviewStatus status,
        string reviewer,
        string content,
        DateTimeOffset at,
        string? revision = null)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        ArgumentNullException.ThrowIfNull(content);

        // Enforced here rather than only in the shell. An approval with nobody's
        // name on it is not evidence of anything, and a rule that lives only in
        // the UI is a rule the CI tool, a script, or the next caller does not
        // have. Whitespace counts as absent: " " passes a length check and names
        // no one.
        if (status != ReviewStatus.Unreviewed && string.IsNullOrWhiteSpace(reviewer))
        {
            throw new ArgumentException(
                "A review must record who made it.", nameof(reviewer));
        }

        if (status == ReviewStatus.Unreviewed)
            return this with { Status = status, Reviewer = string.Empty, ReviewedAt = null, ReviewedContentHash = null, ReviewedRevision = null };

        return this with
        {
            Status = status,
            Reviewer = reviewer,
            ReviewedAt = at,
            ReviewedContentHash = ReviewContentHash.Compute(content),
            ReviewedRevision = revision,
        };
    }

    /// <summary>
    /// Appends a note as given. The caller assigns the id — see
    /// <see cref="NextNoteId"/>, which derives one from the record's existing
    /// notes rather than from a clock or a random source, so writing the same
    /// review twice produces the same file rather than a spurious diff.
    /// </summary>
    public FileReview AddNote(ReviewNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return this with { Notes = [.. Notes, note] };
    }

    /// <summary>
    /// The next note id for this record: one above the highest currently in it.
    ///
    /// Sequential within a file, which keeps it short enough to say out loud in
    /// a review conversation. Derived from the surviving notes, so removing the
    /// highest-numbered note frees its id again — see <see cref="ReviewNote.Id"/>
    /// for why that is accepted.
    /// </summary>
    public string NextNoteId()
    {
        int highest = 0;
        foreach (ReviewNote note in Notes)
        {
            if (note.Id.Length > 1 && note.Id[0] == 'n' &&
                int.TryParse(note.Id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                highest = Math.Max(highest, value);
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"n{highest + 1}");
    }

    /// <summary>Replaces a note by ID, or returns the record unchanged when there is no such note.</summary>
    public FileReview ReplaceNote(string id, Func<ReviewNote, ReviewNote> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        int index = -1;
        for (int i = 0; i < Notes.Count; i++)
        {
            if (string.Equals(Notes[i].Id, id, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return this;

        var notes = new List<ReviewNote>(Notes);
        notes[index] = update(notes[index]);
        return this with { Notes = notes };
    }

    /// <summary>
    /// Removes every note with this id.
    ///
    /// "Every" rather than "the first", because ids are unique within a record
    /// and a duplicate therefore means the record is already damaged; leaving
    /// one of a colliding pair behind would leave an addressable note the
    /// reviewer cannot remove.
    /// </summary>
    public FileReview RemoveNote(string id) =>
        this with { Notes = [.. Notes.Where(note => !string.Equals(note.Id, id, StringComparison.Ordinal))] };

    /// <summary>
    /// True when the record holds nothing worth writing. A record that has been
    /// reset to Unreviewed with no notes left is deleted rather than written as
    /// an empty file, so the review directory mirrors what has actually been
    /// looked at instead of accumulating placeholders for every file ever opened.
    /// </summary>
    public bool IsEmpty => Status == ReviewStatus.Unreviewed && Notes.Count == 0;
}

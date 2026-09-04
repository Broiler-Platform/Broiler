using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Code.Workspaces.Storage;

namespace Broiler.Code.Review;

/// <summary>
/// Where review records live, and the only thing that reads or writes them.
///
/// The records sit in a directory beside the source rather than inside it, and
/// the split is the design decision the whole tool turns on. A code comment
/// answers "why does this work this way?" and belongs to the implementation
/// forever. A review note answers "what does a human still have to check here?"
/// and belongs to the review — it is true for a while and then it is answered.
/// Putting the second kind in the source leaves stale questions reading like
/// documentation, and makes every review action a source diff.
///
/// Keeping them beside the source rather than in a database is just as
/// deliberate. They are committed, they diff, they travel with a branch, they
/// merge, and they are as reviewable as the code they describe.
///
/// Every path goes through <see cref="IWorkspaceStorage"/>, so a review can only
/// touch the roots the user granted, and the same store works on a desktop
/// filesystem, on Android's document tree, and over a browser directory handle.
/// </summary>
public sealed class ReviewStore
{
    /// <summary>
    /// The directory review records live in, relative to the workspace root.
    ///
    /// Dot-prefixed so it sorts out of the way and reads as metadata, and named
    /// for the product so it cannot collide with another tool's <c>.review</c>.
    /// It is explicitly <b>not</b> ignored by git — see the note in
    /// <c>docs/architecture/broiler-code-review.md</c>. A review record that is
    /// not committed proves nothing to anyone but its author.
    /// </summary>
    public const string ReviewDirectory = ".broiler-review";

    /// <summary>The suffix appended to a source path to name its record.</summary>
    public const string RecordSuffix = ".review.json";

    private readonly IWorkspaceStorage _storage;

    public ReviewStore(IWorkspaceStorage storage) =>
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    /// <summary>
    /// The record path for a source path.
    ///
    /// The source tree is mirrored rather than flattened into one file per hash
    /// or one big index. Two reviewers working on different components then
    /// touch different files and their branches merge without a conflict, which
    /// a single <c>reviews.json</c> would make impossible on any repository with
    /// more than one reviewer.
    ///
    /// Returns null for a path that does not normalize — including a path
    /// already inside the review directory, so a record can never acquire a
    /// record of its own.
    /// </summary>
    public static string? RecordPathFor(string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);

        string? normalized = WorkspacePath.Normalize(sourcePath);
        if (string.IsNullOrEmpty(normalized) || IsRecordPath(normalized))
            return null;

        return $"{ReviewDirectory}/{normalized}{RecordSuffix}";
    }

    /// <summary>The source path a record path describes, or null when it is not a record path.</summary>
    public static string? SourcePathFor(string recordPath)
    {
        ArgumentNullException.ThrowIfNull(recordPath);

        string? normalized = WorkspacePath.Normalize(recordPath);
        if (normalized is null ||
            !normalized.StartsWith(ReviewDirectory + "/", StringComparison.Ordinal) ||
            !normalized.EndsWith(RecordSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        return normalized[(ReviewDirectory.Length + 1)..^RecordSuffix.Length];
    }

    /// <summary>True when a path is inside the review directory.</summary>
    public static bool IsRecordPath(string path) =>
        path.Equals(ReviewDirectory, StringComparison.Ordinal) ||
        path.StartsWith(ReviewDirectory + "/", StringComparison.Ordinal);

    /// <summary>
    /// Reads the record for a source file.
    ///
    /// A file with no record is not an error — it is the normal state of an
    /// unreviewed file, and there are far more of those than reviewed ones — so
    /// it returns an empty record rather than a failure. A record that exists
    /// but cannot be parsed <i>is</i> reported, because silently treating a
    /// corrupt record as "unreviewed" would erase a reviewer's work without
    /// telling anyone.
    /// </summary>
    public async ValueTask<StorageResult<FileReview>> ReadAsync(
        string sourcePath, CancellationToken cancellationToken = default)
    {
        if (RecordPathFor(sourcePath) is not { } recordPath)
        {
            return StorageResult<FileReview>.Fail(
                StorageFailureKind.OutsideGrant, $"'{sourcePath}' is not a reviewable workspace path.");
        }

        StorageResult<StorageTextContent> read = await _storage
            .ReadTextAsync(recordPath, cancellationToken).ConfigureAwait(false);

        if (!read.Succeeded)
        {
            return read.Failure?.Kind == StorageFailureKind.NotFound
                ? StorageResult<FileReview>.Ok(FileReview.Empty(NormalizeOrSelf(sourcePath)))
                : StorageResult<FileReview>.Fail(
                    read.Failure?.Kind ?? StorageFailureKind.Unknown,
                    read.Failure?.Message ?? $"'{recordPath}' could not be read.");
        }

        FileReview? parsed = ReviewJson.Read(read.Value!.Text, NormalizeOrSelf(sourcePath));
        return parsed is null
            ? StorageResult<FileReview>.Fail(
                StorageFailureKind.Unknown,
                $"'{recordPath}' is not a valid review record. It was left untouched.")
            : StorageResult<FileReview>.Ok(parsed);
    }

    /// <summary>
    /// Writes a record, or deletes it when the record has become empty.
    ///
    /// Deleting rather than writing an empty record keeps the review directory a
    /// mirror of what has actually been looked at. A placeholder for every file
    /// anybody ever opened would make the directory as large as the source tree
    /// and tell a reader nothing.
    /// </summary>
    public async ValueTask<StorageResult<bool>> WriteAsync(
        FileReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (RecordPathFor(review.Path) is not { } recordPath)
        {
            return StorageResult<bool>.Fail(
                StorageFailureKind.OutsideGrant, $"'{review.Path}' is not a reviewable workspace path.");
        }

        if (review.IsEmpty)
        {
            StorageResult<bool> deleted = await _storage
                .DeleteAsync(recordPath, cancellationToken).ConfigureAwait(false);

            // Deleting a record that was never written is the expected outcome
            // of resetting a file nobody had reviewed, not a failure.
            return !deleted.Succeeded && deleted.Failure?.Kind == StorageFailureKind.NotFound
                ? StorageResult<bool>.Ok(true)
                : deleted;
        }

        StorageResult<string> written = await _storage.WriteTextAsync(
            recordPath,
            ReviewJson.Write(review),
            Workspaces.Model.TextEncodingInfo.Utf8NoBom,

            // Unconditional. The alternative is to carry the record's revision
            // through every caller so a concurrent write can be detected, and
            // the thing being protected is a file only this user's editor
            // writes. A conflict here is a merge conflict in git, where it
            // belongs and where a human can actually resolve it.
            expectedRevision: null,
            cancellationToken).ConfigureAwait(false);

        return written.Succeeded
            ? StorageResult<bool>.Ok(true)
            : StorageResult<bool>.Fail(
                written.Failure?.Kind ?? StorageFailureKind.Unknown,
                written.Failure?.Message ?? $"'{recordPath}' could not be written.");
    }

    /// <summary>
    /// Every record in the workspace, plus the ones that could not be read.
    /// </summary>
    /// <param name="Reviews">The records, keyed by the source path each describes.</param>
    /// <param name="Unreadable">
    /// Record paths that exist and could not be parsed, with the reason.
    ///
    /// Reported rather than skipped. A record that fails to parse — most often
    /// because a merge left conflict markers in it — would otherwise make its
    /// file look unreviewed, silently turning somebody's approval into "needs
    /// review" and quietly lowering the published coverage number. That is the
    /// one failure mode this whole store exists to prevent, so it is a value the
    /// caller has to look at rather than a log line.
    /// </param>
    public readonly record struct ReviewRecordSet(
        IReadOnlyDictionary<string, FileReview> Reviews,
        IReadOnlyList<StorageFailure> Unreadable);

    /// <summary>
    /// Reads every record in the workspace, keyed by the source path each
    /// describes.
    ///
    /// Used by coverage reporting and by the explorer, both of which need the
    /// whole set at once and neither of which should read a record per row. A
    /// record whose source file no longer exists is still returned: a review of
    /// a deleted file is exactly the thing a report should be able to point at.
    ///
    /// Records that could not be read are reported separately rather than
    /// dropped — see <see cref="ReviewRecordSet.Unreadable"/>.
    /// </summary>
    public async ValueTask<ReviewRecordSet> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var reviews = new Dictionary<string, FileReview>(StringComparer.Ordinal);
        var unreadable = new List<StorageFailure>();
        await CollectAsync(ReviewDirectory, reviews, unreadable, cancellationToken).ConfigureAwait(false);
        return new ReviewRecordSet(reviews, unreadable);
    }

    private async ValueTask CollectAsync(
        string directory,
        Dictionary<string, FileReview> into,
        List<StorageFailure> unreadable,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StorageResult<IReadOnlyList<StorageEntry>> listed = await _storage
            .ListAsync(directory, cancellationToken).ConfigureAwait(false);

        // A workspace with no review directory yet is the starting state of
        // every repository that adopts this, not an error to report.
        if (!listed.Succeeded)
            return;

        foreach (StorageEntry entry in listed.Value!)
        {
            if (entry.IsDirectory)
            {
                await CollectAsync(entry.RelativePath, into, unreadable, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (SourcePathFor(entry.RelativePath) is not { } sourcePath)
                continue;

            StorageResult<StorageTextContent> read = await _storage
                .ReadTextAsync(entry.RelativePath, cancellationToken).ConfigureAwait(false);
            if (!read.Succeeded)
            {
                unreadable.Add(read.Failure ?? new StorageFailure(
                    StorageFailureKind.Unknown, $"'{entry.RelativePath}' could not be read."));
                continue;
            }

            if (ReviewJson.Read(read.Value!.Text, sourcePath) is { } review)
                into[sourcePath] = review;
            else
                unreadable.Add(new StorageFailure(
                    StorageFailureKind.Unknown,
                    $"'{entry.RelativePath}' is not a valid review record, so '{sourcePath}' " +
                    "is reported as unreviewed. It was left untouched."));
        }
    }

    private static string NormalizeOrSelf(string path) => WorkspacePath.Normalize(path) ?? path;
}

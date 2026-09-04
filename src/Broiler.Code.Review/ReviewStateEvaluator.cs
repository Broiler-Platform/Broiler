using System;

namespace Broiler.Code.Review;

/// <summary>
/// Turns a recorded review and the file's current content into the state the
/// product actually shows.
///
/// The whole value of the record is here. "Somebody looked at this file once" is
/// worth nothing on a codebase that moves; "this exact content was read by this
/// person on this date, and here is whether it has changed since" is a claim
/// that survives contact with development. Everything else in this assembly
/// exists to make this function answerable.
/// </summary>
public static class ReviewStateEvaluator
{
    /// <summary>
    /// Evaluates a record against the content on disk.
    ///
    /// <paramref name="content"/> may be null when the file could not be read —
    /// it was deleted, or the record names a path that no longer exists. That is
    /// reported as <see cref="ReviewFreshness.Unknown"/> rather than being
    /// treated as unchanged, because a deleted-and-restored file must not keep
    /// an approval it never earned.
    /// </summary>
    public static ReviewState Evaluate(FileReview? review, string? content)
    {
        if (review is null || review.Status == ReviewStatus.Unreviewed)
        {
            // A file nobody approved can still carry open questions — somebody
            // read it, wrote down what they did not understand, and did not
            // record a status. Reporting that as an untouched file would
            // understate the work done and hide the question.
            int pending = review?.OpenNoteCount ?? 0;
            return new ReviewState(ReviewStatus.Unreviewed, ReviewFreshness.NotReviewed, pending);
        }

        return new ReviewState(review.Status, FreshnessOf(review, content), review.OpenNoteCount);
    }

    private static ReviewFreshness FreshnessOf(FileReview review, string? content)
    {
        if (content is null)
            return ReviewFreshness.Unknown;

        // A record written by a build that hashed differently cannot be
        // compared. Saying "unknown" keeps the reviewer's name and date visible
        // and asks for a re-confirmation; saying "stale" would blame the
        // reviewer for a tool change and teach them to ignore the warning.
        if (!ReviewContentHash.IsKnownAlgorithm(review.ReviewedContentHash))
            return ReviewFreshness.Unknown;

        return ReviewContentHash.Matches(review.ReviewedContentHash, content)
            ? ReviewFreshness.Current
            : ReviewFreshness.Stale;
    }
}

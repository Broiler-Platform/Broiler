using System.Globalization;
using Broiler.Code.Review;

namespace Broiler.Code.Review.Tests;

/// <summary>
/// What the review record promises, asserted rather than described.
///
/// The claim these tests protect is the platform's, not the tool's: "every
/// source file has a known review state relative to a concrete revision of its
/// content". Each behaviour below is one way that claim could quietly become
/// false — a review that survives an edit, a hash that changes when nothing did,
/// a record that rewrites itself on read.
/// </summary>
public sealed class ReviewRecordTests
{
    private const string Source = "class A\n{\n    void M() { }\n}\n";

    [Fact(Timeout = 600000)]
    public void A_File_With_No_Record_Is_Unreviewed()
    {
        ReviewState state = ReviewStateEvaluator.Evaluate(null, Source);

        Assert.Equal(ReviewStatus.Unreviewed, state.Status);
        Assert.Equal(ReviewFreshness.NotReviewed, state.Freshness);
        Assert.False(state.IsVerified);
        Assert.Equal("needs review", state.ToDisplayString());
    }

    [Fact(Timeout = 600000)]
    public void A_Review_Of_The_Current_Content_Is_Verified()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source);

        Assert.True(state.IsVerified);
        Assert.Equal(ReviewFreshness.Current, state.Freshness);
        Assert.Equal("reviewed", state.ToDisplayString());
    }

    /// <summary>
    /// The behaviour the whole feature exists for: an approval does not survive
    /// a change to what was approved.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Edit_After_A_Review_Makes_It_Stale()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source.Replace("void M()", "int M()"));

        Assert.Equal(ReviewFreshness.Stale, state.Freshness);
        Assert.False(state.IsVerified);
        Assert.True(state.IsStaleApproval);
        Assert.Equal("reviewed, then modified", state.ToDisplayString());

        // The reviewer's name and date survive. "Reviewed, then modified" is a
        // more useful thing to tell somebody than "unreviewed", and losing the
        // attribution would throw away the only evidence that anyone ever read
        // the file.
        Assert.Equal("Enrico", review.Reviewer);
        Assert.NotNull(review.ReviewedAt);
    }

    /// <summary>
    /// Reverting an edit restores the review, because the reviewed content is
    /// back. A commit-based rule would get this wrong in the direction that
    /// matters: it would keep calling the file reviewed after the first edit and
    /// only notice at the next commit.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Reverting_An_Edit_Makes_The_Review_Current_Again()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        Assert.Equal(
            ReviewFreshness.Stale,
            ReviewStateEvaluator.Evaluate(review, Source + "\n// changed\n").Freshness);

        Assert.Equal(ReviewFreshness.Current, ReviewStateEvaluator.Evaluate(review, Source).Freshness);
    }

    /// <summary>
    /// Line endings are not content. This repository holds CRLF files, LF files,
    /// and files mixed within themselves; an editor that rewrites one whole file
    /// normalizes them all, changing every byte and not one token.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("a\r\nb\r\n")]
    [InlineData("a\rb\r")]
    [InlineData("﻿a\nb\n")]
    public void Line_Endings_And_A_Byte_Order_Mark_Do_Not_Invalidate_A_Review(string variant)
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "a\nb\n", Stamp);

        Assert.Equal(ReviewFreshness.Current, ReviewStateEvaluator.Evaluate(review, variant).Freshness);
    }

    /// <summary>
    /// Whitespace is content, deliberately. Trailing whitespace can end a raw
    /// string literal, and re-indented code is code a reviewer has not read in
    /// that form.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Reindenting_Does_Invalidate_A_Review()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", "class A\n{\n    void M();\n}\n", Stamp);

        Assert.Equal(
            ReviewFreshness.Stale,
            ReviewStateEvaluator.Evaluate(review, "class A\n{\n\tvoid M();\n}\n").Freshness);
    }

    /// <summary>
    /// A record this build cannot verify reports Unknown, never Stale. Telling a
    /// reviewer their approval expired because the tool was upgraded is how a
    /// warning gets trained out of people.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Unrecognized_Hash_Algorithm_Is_Unknown_Not_Stale()
    {
        var review = new FileReview
        {
            Path = "a.cs",
            Status = ReviewStatus.Reviewed,
            Reviewer = "Enrico",
            ReviewedAt = Stamp,
            ReviewedContentHash = "blake3:deadbeef",
        };

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source);

        Assert.Equal(ReviewFreshness.Unknown, state.Freshness);
        Assert.False(state.IsVerified);
    }

    /// <summary>A deleted file does not keep an approval it can no longer be checked against.</summary>
    [Fact(Timeout = 600000)]
    public void A_Missing_File_Is_Unknown()
    {
        FileReview review = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp);

        Assert.Equal(ReviewFreshness.Unknown, ReviewStateEvaluator.Evaluate(review, null).Freshness);
    }

    /// <summary>
    /// A file nobody approved but somebody questioned has been looked at.
    /// Reporting it as untouched would hide the question and understate the work.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Open_Notes_Are_Reported_On_An_Unreviewed_File()
    {
        FileReview review = FileReview.Empty("a.cs").AddNote(new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Question,
            Text = "Is ToPrimitive required here?",
            Author = "Enrico",
            CreatedAt = Stamp,
        });

        ReviewState state = ReviewStateEvaluator.Evaluate(review, Source);

        Assert.Equal(ReviewStatus.Unreviewed, state.Status);
        Assert.Equal(1, state.OpenNotes);
    }

    /// <summary>An observation asks for nothing, so it never blocks a file.</summary>
    [Fact(Timeout = 600000)]
    public void An_Observation_Is_Never_Open()
    {
        var note = new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Observation,
            Text = "Mirrors the spec's wording.",
            Author = "Enrico",
            CreatedAt = Stamp,
        };

        Assert.False(note.IsOpen);
        Assert.True((note with { Kind = ReviewNoteKind.Question }).IsOpen);
    }

    [Fact(Timeout = 600000)]
    public void Note_Ids_Do_Not_Repeat()
    {
        FileReview review = FileReview.Empty("a.cs");

        for (int i = 1; i <= 3; i++)
        {
            string id = review.NextNoteId();
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"n{i}"), id);
            review = review.AddNote(new ReviewNote
            {
                Id = id,
                Kind = ReviewNoteKind.Question,
                Text = "?",
                Author = "Enrico",
                CreatedAt = Stamp,
            });
        }

        // Removing the middle note must not let its ID be minted again: the
        // history would then have two different notes called n2.
        review = review.RemoveNote("n2");
        Assert.Equal("n4", review.NextNoteId());
    }

    /// <summary>Clearing a review removes the attestation rather than leaving a hash behind.</summary>
    [Fact(Timeout = 600000)]
    public void Clearing_A_Review_Drops_The_Reviewer_And_The_Hash()
    {
        FileReview cleared = FileReview.Empty("a.cs")
            .WithDecision(ReviewStatus.Reviewed, "Enrico", Source, Stamp)
            .WithDecision(ReviewStatus.Unreviewed, "Enrico", Source, Stamp);

        Assert.Equal(ReviewStatus.Unreviewed, cleared.Status);
        Assert.Equal(string.Empty, cleared.Reviewer);
        Assert.Null(cleared.ReviewedContentHash);
        Assert.Null(cleared.ReviewedAt);
        Assert.True(cleared.IsEmpty);
    }

    private static DateTimeOffset Stamp => new(2026, 9, 4, 9, 14, 0, TimeSpan.Zero);
}

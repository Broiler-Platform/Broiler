using Broiler.Code.Review;

namespace Broiler.Code.Review.Tests;

/// <summary>
/// Notes are attached to code, not to line numbers.
///
/// A tool that stored only line numbers would report every note against the
/// wrong code after the first insertion above it — which is the failure that
/// makes people stop trusting a review tool and then stop using it. These tests
/// are the guarantee that it does not happen, including the cases where the
/// honest answer is "I cannot tell".
/// </summary>
public sealed class NoteAnchoringTests
{
    private const string Source =
        """
        class A
        {
            void First() { }

            void Second() { }
        }
        """;

    [Fact(Timeout = 600000)]
    public void A_Note_On_Unchanged_Code_Stays_Where_It_Was()
    {
        AnchoredNote placed = Place(Source, Source, line: 2);

        Assert.Equal(ReviewAnchorStatus.Anchored, placed.Status);
        Assert.Equal(2, placed.StartLine);
    }

    /// <summary>
    /// The ordinary case: something is inserted above the note and the note
    /// follows its code down the file.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Note_Follows_Its_Code_When_Lines_Are_Inserted_Above()
    {
        string edited = "// a new header comment\n// and another\n" + Source;
        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(4, placed.StartLine);
    }

    [Fact(Timeout = 600000)]
    public void A_Note_Follows_Its_Code_When_Lines_Are_Deleted_Above()
    {
        string edited = Source.Replace("class A\n", string.Empty);
        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(1, placed.StartLine);
    }

    /// <summary>
    /// The code a reviewer questioned having disappeared is exactly when the
    /// question still needs answering, so the note is kept and says so.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Note_Whose_Code_Is_Deleted_Is_Orphaned_Not_Dropped()
    {
        string edited = Source.Replace("    void First() { }\n", string.Empty);
        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Orphaned, placed.Status);
        Assert.True(placed.NeedsAttention);
        Assert.Equal("Why?", placed.Note.Text);
    }

    /// <summary>
    /// Duplicated code is where a reviewer's question matters most, and picking
    /// the nearest of several identical blocks would answer a question about one
    /// of them with a note about another. The tool refuses to guess.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Note_Whose_Code_Now_Appears_Twice_Is_Ambiguous()
    {
        // The anchored line is duplicated verbatim, indentation included — an
        // extracted-then-copied method is exactly how this arises in practice.
        // A near-copy at a different indentation would not be ambiguous, because
        // whitespace is part of the anchor.
        string edited =
            """
            // a new header
            class A
            {
                void First() { }

                void Second() { }

                void First() { }
            }
            """;

        AnchoredNote placed = Place(Source, edited, line: 2);

        Assert.Equal(ReviewAnchorStatus.Ambiguous, placed.Status);
        Assert.True(placed.NeedsAttention);
    }

    [Fact(Timeout = 600000)]
    public void A_File_Level_Note_Has_No_Line()
    {
        var note = new ReviewNote
        {
            Id = "n1",
            Kind = ReviewNoteKind.Observation,
            Text = "This whole file mirrors ECMA-262 §7.1.",
            Author = "Enrico",
            CreatedAt = default,
            Anchor = ReviewAnchor.File,
        };

        AnchoredNote placed = NoteAnchoring.Place(note, Source.Split('\n'));

        Assert.Equal(ReviewAnchorStatus.FileLevel, placed.Status);
        Assert.Equal(-1, placed.StartLine);
    }

    /// <summary>
    /// A multi-line anchor moves as a unit and reports the range it now covers,
    /// so a note about a method body does not collapse onto its first line.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Multi_Line_Anchor_Keeps_Its_Range()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 2, 4);
        Assert.Equal(3, anchor.LineCount);

        var note = NewNote(anchor);
        AnchoredNote placed = NoteAnchoring.Place(note, ("// header\n" + Source).Split('\n'));

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(3, placed.StartLine);
        Assert.Equal(5, placed.EndLine);
    }

    /// <summary>
    /// A caret past the end of the file is a legitimate place to ask a question
    /// from, so the range is clamped rather than rejected.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Out_Of_Range_Anchor_Is_Clamped()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 500, 900);

        Assert.Equal(5, anchor.StartLine);
        Assert.Equal(5, anchor.EndLine);
        Assert.Equal("}", anchor.AnchorText);
    }

    /// <summary>
    /// Anchoring normalizes line endings the same way hashing does. Two
    /// normalizations that drifted apart would re-anchor notes onto lines the
    /// hash considered unchanged.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Anchoring_Agrees_With_Hashing_About_Line_Endings()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 2, 2);
        AnchoredNote placed = NoteAnchoring.Place(
            NewNote(anchor), ReviewContentHash.Normalize(Source.Replace("\n", "\r\n")).Split('\n'));

        Assert.Equal(ReviewAnchorStatus.Anchored, placed.Status);
        Assert.Equal(2, placed.StartLine);
    }

    /// <summary>
    /// A symbol is carried for display and never decides placement — the review
    /// store has to work on a host with no semantic service at all, and on JSON
    /// and Markdown files that have no symbols.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Symbol_Is_Recorded_But_Does_Not_Decide_Placement()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 2, 2, "A.First");
        Assert.Equal("A.First", anchor.Symbol);

        // The symbol still says A.First; the placement came from the text.
        AnchoredNote placed = NoteAnchoring.Place(NewNote(anchor), ("// header\n" + Source).Split('\n'));
        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.Equal(3, placed.StartLine);
    }

    /// <summary>
    /// A caret on a blank line is a perfectly ordinary place to write a note
    /// from — blank lines between members are everywhere in C#. Such an anchor
    /// carries no text to search for, so it must keep its line and say it cannot
    /// be trusted, rather than quietly becoming a note about the whole file and
    /// being written to disk with no line at all.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Note_On_A_Blank_Line_Keeps_Its_Line_And_Reports_It_Cannot_Be_Placed()
    {
        ReviewAnchor anchor = NoteAnchoring.CreateAnchor(Source, 3, 3);

        Assert.False(anchor.IsFileLevel);
        Assert.Equal(3, anchor.StartLine);
        Assert.Equal(string.Empty, anchor.AnchorText);

        AnchoredNote placed = NoteAnchoring.Place(NewNote(anchor), Source.Split('\n'));
        Assert.Equal(ReviewAnchorStatus.Ambiguous, placed.Status);
        Assert.Equal(3, placed.StartLine);
        Assert.True(placed.NeedsAttention);
    }

    /// <summary>A note that merely followed its code is working, not something to warn about.</summary>
    [Fact(Timeout = 600000)]
    public void A_Moved_Note_Does_Not_Need_Attention()
    {
        AnchoredNote placed = Place(Source, "// header\n" + Source, line: 2);

        Assert.Equal(ReviewAnchorStatus.Moved, placed.Status);
        Assert.False(placed.NeedsAttention);
    }

    private static AnchoredNote Place(string original, string edited, int line)
    {
        var review = FileReview.Empty("a.cs")
            .AddNote(NewNote(NoteAnchoring.CreateAnchor(original, line, line)));

        return NoteAnchoring.Place(review, edited)[0];
    }

    private static ReviewNote NewNote(ReviewAnchor anchor) => new()
    {
        Id = "n1",
        Kind = ReviewNoteKind.Question,
        Text = "Why?",
        Author = "Enrico",
        CreatedAt = default,
        Anchor = anchor,
    };
}

namespace Broiler.UI.RichEdit.Tests;

public sealed class RichTextDocumentTests
{
    [Fact]
    public void Empty_Document_Has_One_Empty_Paragraph()
    {
        RichTextDocument document = RichTextDocument.Empty;

        Assert.Equal(1, document.ParagraphCount);
        Assert.Equal(string.Empty, document.PlainText);
        Assert.Equal(Doc.Pos(0, 0), document.Start);
        Assert.Equal(Doc.Pos(0, 0), document.End);
    }

    [Fact]
    public void InsertText_In_Middle_Keeps_Runs_Covering_Text()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");

        RichTextEditResult result = document.InsertText(Doc.Pos(0, 2), "XY");

        Assert.Equal("HeXYllo", result.Document.PlainText);
        Assert.Equal(Doc.Pos(0, 4), result.Caret);
        Doc.AssertNormalized(result.Document);
    }

    [Fact]
    public void InsertText_With_Newlines_Splits_Into_Paragraphs()
    {
        RichTextDocument document = RichTextDocument.Empty;

        RichTextEditResult result = document.InsertText(document.Start, "one\ntwo\nthree");

        Assert.Equal(3, result.Document.ParagraphCount);
        Assert.Equal("one\ntwo\nthree", result.Document.PlainText);
        Assert.Equal(Doc.Pos(2, 5), result.Caret);
    }

    [Fact]
    public void InsertText_Normalizes_Carriage_Returns()
    {
        RichTextDocument document = RichTextDocument.Empty;

        RichTextEditResult result = document.InsertText(document.Start, "a\r\nb\rc");

        Assert.Equal(3, result.Document.ParagraphCount);
        Assert.Equal("a\nb\nc", result.Document.PlainText);
    }

    [Fact]
    public void DeleteRange_Within_Paragraph_Removes_Text()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");

        RichTextEditResult result = document.DeleteRange(Doc.Range(0, 1, 0, 4));

        Assert.Equal("Ho", result.Document.PlainText);
        Assert.Equal(Doc.Pos(0, 1), result.Caret);
        Doc.AssertNormalized(result.Document);
    }

    [Fact]
    public void DeleteRange_Across_Paragraphs_Merges_Boundary()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("abc\ndef\nghi");

        RichTextEditResult result = document.DeleteRange(Doc.Range(0, 1, 2, 1));

        Assert.Equal(1, result.Document.ParagraphCount);
        Assert.Equal("ahi", result.Document.PlainText);
        Assert.Equal(Doc.Pos(0, 1), result.Caret);
    }

    [Fact]
    public void SplitParagraph_Creates_Two_Paragraphs()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("abcd");

        RichTextEditResult result = document.SplitParagraph(Doc.Pos(0, 2));

        Assert.Equal(2, result.Document.ParagraphCount);
        Assert.Equal("ab", result.Document.Paragraphs[0].Text);
        Assert.Equal("cd", result.Document.Paragraphs[1].Text);
        Assert.Equal(Doc.Pos(1, 0), result.Caret);
    }

    [Fact]
    public void MergeParagraphs_Joins_Adjacent_Paragraphs()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("ab\ncd");

        RichTextEditResult result = document.MergeParagraphs(0);

        Assert.Equal(1, result.Document.ParagraphCount);
        Assert.Equal("abcd", result.Document.PlainText);
        Assert.Equal(Doc.Pos(0, 2), result.Caret);
    }

    [Fact]
    public void MergeParagraphs_Out_Of_Range_Is_NoOp()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("ab\ncd");

        RichTextEditResult result = document.MergeParagraphs(1);

        Assert.Equal(2, result.Document.ParagraphCount);
        Assert.Equal("ab\ncd", result.Document.PlainText);
    }

    [Fact]
    public void PlainText_Round_Trips_Through_FromPlainText()
    {
        const string text = "first\nsecond\n\nfourth";

        RichTextDocument document = RichTextDocument.FromPlainText(text);

        Assert.Equal(4, document.ParagraphCount);
        Assert.Equal(text, document.PlainText);
    }

    [Fact]
    public void ClampPosition_Constrains_To_Document()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("ab\ncd");

        Assert.Equal(Doc.Pos(1, 2), document.ClampPosition(Doc.Pos(9, 9)));
        Assert.Equal(Doc.Pos(0, 0), document.ClampPosition(Doc.Pos(-3, -3)));
        Assert.False(document.IsValid(Doc.Pos(0, 5)));
        Assert.True(document.IsValid(Doc.Pos(0, 2)));
    }

    [Fact]
    public void Position_Comparison_Orders_By_Paragraph_Then_Offset()
    {
        Assert.True(Doc.Pos(0, 5) < Doc.Pos(1, 0));
        Assert.True(Doc.Pos(1, 2) > Doc.Pos(1, 1));
        Assert.True(Doc.Pos(2, 3) >= Doc.Pos(2, 3));
        Assert.Equal(Doc.Pos(1, 1), Doc.Pos(1, 1));
    }

    [Fact]
    public void Movement_Crosses_Paragraph_Boundaries()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("ab\ncd");

        Assert.Equal(Doc.Pos(0, 2), document.PositionLeftOf(Doc.Pos(1, 0)));
        Assert.Equal(Doc.Pos(1, 0), document.PositionRightOf(Doc.Pos(0, 2)));
        Assert.Equal(Doc.Pos(0, 0), document.PositionLeftOf(Doc.Pos(0, 0)));
        Assert.Equal(Doc.Pos(1, 2), document.PositionRightOf(Doc.Pos(1, 2)));
    }

    [Fact]
    public void Movement_Steps_Over_Surrogate_Pairs()
    {
        // "a" + U+1F600 (surrogate pair) + "b": string length is 4.
        RichTextDocument document = RichTextDocument.FromPlainText("a\U0001F600b");

        Assert.Equal(Doc.Pos(0, 3), document.PositionRightOf(Doc.Pos(0, 1)));
        Assert.Equal(Doc.Pos(0, 1), document.PositionLeftOf(Doc.Pos(0, 3)));
    }
}

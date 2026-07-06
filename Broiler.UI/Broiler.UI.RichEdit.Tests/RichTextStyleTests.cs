using Broiler.Graphics;

namespace Broiler.UI.RichEdit.Tests;

public sealed class RichTextStyleTests
{
    [Fact]
    public void ApplyInlineStyle_To_Middle_Splits_Into_Three_Runs()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");

        RichTextDocument styled = document.ApplyInlineStyle(Doc.Range(0, 1, 0, 4), InlineStyleDelta.ToggleBold(true));

        RichTextParagraph paragraph = styled.Paragraphs[0];
        Assert.Equal(3, paragraph.Runs.Count);
        Assert.False(paragraph.Runs[0].Style.Bold);
        Assert.True(paragraph.Runs[1].Style.Bold);
        Assert.Equal(3, paragraph.Runs[1].Length);
        Assert.False(paragraph.Runs[2].Style.Bold);
        Doc.AssertNormalized(paragraph);
    }

    [Fact]
    public void Applying_Same_Style_To_Whole_Paragraph_Yields_Single_Run()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");

        RichTextDocument styled = document.ApplyInlineStyle(Doc.Range(0, 0, 0, 5), InlineStyleDelta.ToggleBold(true));

        Assert.Single(styled.Paragraphs[0].Runs);
        Assert.True(styled.Paragraphs[0].Runs[0].Style.Bold);
    }

    [Fact]
    public void Reverting_A_Style_Merges_Runs_Back_Together()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");

        RichTextDocument bold = document.ApplyInlineStyle(Doc.Range(0, 1, 0, 4), InlineStyleDelta.ToggleBold(true));
        Assert.Equal(3, bold.Paragraphs[0].Runs.Count);

        RichTextDocument reverted = bold.ApplyInlineStyle(Doc.Range(0, 0, 0, 5), InlineStyleDelta.ToggleBold(false));

        Assert.Single(reverted.Paragraphs[0].Runs);
        Doc.AssertNormalized(reverted);
    }

    [Fact]
    public void ClearFormatting_Resets_Runs_To_Default()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");
        RichTextDocument styled = document
            .ApplyInlineStyle(Doc.Range(0, 0, 0, 5), InlineStyleDelta.ToggleBold(true))
            .ApplyInlineStyle(Doc.Range(0, 0, 0, 5), InlineStyleDelta.WithForeground(BColor.Red));

        RichTextDocument cleared = styled.ApplyInlineStyle(Doc.Range(0, 0, 0, 5), InlineStyleDelta.Clear);

        Assert.Single(cleared.Paragraphs[0].Runs);
        Assert.Equal(InlineStyle.Default, cleared.Paragraphs[0].Runs[0].Style);
    }

    [Fact]
    public void InlineStyle_Delta_Only_Changes_Specified_Attributes()
    {
        var start = new InlineStyle { Bold = true, FontSize = 12f };

        InlineStyle result = InlineStyleDelta.WithForeground(BColor.Blue).Apply(start);

        Assert.True(result.Bold);
        Assert.Equal(12f, result.FontSize);
        Assert.Equal(BColor.Blue, result.Foreground);
    }

    [Fact]
    public void ApplyInlineStyle_Across_Paragraphs_Styles_Each_Segment()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("abc\ndef");

        RichTextDocument styled = document.ApplyInlineStyle(Doc.Range(0, 1, 1, 2), InlineStyleDelta.ToggleItalic(true));

        // First paragraph: "a" plain, "bc" italic.
        Assert.Equal(2, styled.Paragraphs[0].Runs.Count);
        Assert.True(styled.Paragraphs[0].Runs[1].Style.Italic);
        // Second paragraph: "de" italic, "f" plain.
        Assert.Equal(2, styled.Paragraphs[1].Runs.Count);
        Assert.True(styled.Paragraphs[1].Runs[0].Style.Italic);
        Assert.False(styled.Paragraphs[1].Runs[1].Style.Italic);
        Doc.AssertNormalized(styled);
    }

    [Fact]
    public void StyleBefore_Inherits_Style_To_The_Left_Of_The_Caret()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hello");
        RichTextDocument styled = document.ApplyInlineStyle(Doc.Range(0, 0, 0, 3), InlineStyleDelta.ToggleBold(true));

        Assert.True(styled.InlineStyleAt(Doc.Pos(0, 3)).Bold);
        Assert.False(styled.InlineStyleAt(Doc.Pos(0, 5)).Bold);
    }

    [Fact]
    public void ApplyParagraphStyle_Applies_To_Touched_Paragraphs_Only()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("a\nb\nc");

        RichTextDocument styled = document.ApplyParagraphStyle(Doc.Range(0, 0, 1, 1), ParagraphStyleDelta.WithAlignment(TextAlignment.Center));

        Assert.Equal(TextAlignment.Center, styled.Paragraphs[0].Style.Alignment);
        Assert.Equal(TextAlignment.Center, styled.Paragraphs[1].Style.Alignment);
        Assert.Equal(TextAlignment.Left, styled.Paragraphs[2].Style.Alignment);
    }

    [Fact]
    public void Default_Paragraph_Style_Is_Single_Spaced()
    {
        RichTextParagraph paragraph = RichTextParagraph.Plain("x");

        Assert.Equal(1f, paragraph.Style.LineSpacing);
        Assert.Equal(TextAlignment.Left, paragraph.Style.Alignment);
        Assert.Equal(ListKind.None, paragraph.Style.ListKind);
    }
}

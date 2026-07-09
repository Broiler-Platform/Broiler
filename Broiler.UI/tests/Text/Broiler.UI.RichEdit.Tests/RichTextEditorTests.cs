using Broiler.Graphics;

namespace Broiler.UI.RichEdit.Tests;

public sealed class RichTextEditorTests
{
    [Fact]
    public void New_Editor_Starts_Empty_With_Caret_At_Start()
    {
        var editor = new RichTextEditor();

        Assert.Equal(string.Empty, editor.GetPlainText());
        Assert.True(editor.Selection.IsEmpty);
        Assert.Equal(Doc.Pos(0, 0), editor.Selection.Focus);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void Typing_Inserts_At_Caret_And_Advances_It()
    {
        var editor = new RichTextEditor();

        editor.InsertText("Hello");
        editor.InsertText(" World");

        Assert.Equal("Hello World", editor.GetPlainText());
        Assert.Equal(Doc.Pos(0, 11), editor.Selection.Focus);
    }

    [Fact]
    public void Typing_Replaces_The_Selection()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SelectAll();

        editor.InsertText("Hi");

        Assert.Equal("Hi", editor.GetPlainText());
        Assert.Equal(Doc.Pos(0, 2), editor.Selection.Focus);
    }

    [Fact]
    public void Enter_Splits_The_Paragraph_At_The_Caret()
    {
        var editor = new RichTextEditor();
        editor.InsertText("abcd");
        editor.SetCaret(Doc.Pos(0, 2));

        editor.SplitParagraph();

        Assert.Equal("ab\ncd", editor.GetPlainText());
        Assert.Equal(Doc.Pos(1, 0), editor.Selection.Focus);
    }

    [Fact]
    public void Backspace_At_Paragraph_Start_Merges_With_Previous()
    {
        var editor = new RichTextEditor();
        editor.LoadPlainText("ab\ncd");
        editor.SetCaret(Doc.Pos(1, 0));

        editor.Backspace();

        Assert.Equal("abcd", editor.GetPlainText());
        Assert.Equal(Doc.Pos(0, 2), editor.Selection.Focus);
    }

    [Fact]
    public void Backspace_Removes_The_Character_Before_The_Caret()
    {
        var editor = new RichTextEditor();
        editor.InsertText("abc");

        editor.Backspace();

        Assert.Equal("ab", editor.GetPlainText());
        Assert.Equal(Doc.Pos(0, 2), editor.Selection.Focus);
    }

    [Fact]
    public void Delete_At_Paragraph_End_Merges_With_Next()
    {
        var editor = new RichTextEditor();
        editor.LoadPlainText("ab\ncd");
        editor.SetCaret(Doc.Pos(0, 2));

        editor.Delete();

        Assert.Equal("abcd", editor.GetPlainText());
        Assert.Equal(Doc.Pos(0, 2), editor.Selection.Focus);
    }

    [Fact]
    public void Backspace_Removes_A_Whole_Surrogate_Pair()
    {
        var editor = new RichTextEditor();
        editor.InsertText("a\U0001F600");
        // Caret sits after the emoji (offset 3 in a length-3 string).
        Assert.Equal(Doc.Pos(0, 3), editor.Selection.Focus);

        editor.Backspace();

        Assert.Equal("a", editor.GetPlainText());
    }

    [Fact]
    public void Selection_Extends_And_Collapses_Predictably()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SetCaret(Doc.Pos(0, 0));

        editor.MoveRight(extend: true);
        editor.MoveRight(extend: true);

        Assert.Equal(Doc.Range(0, 0, 0, 2), editor.Selection);
        Assert.False(editor.Selection.IsEmpty);

        editor.MoveRight(extend: false);
        Assert.True(editor.Selection.IsEmpty);
    }

    [Fact]
    public void Applying_Style_To_Empty_Selection_Formats_The_Next_Typed_Text()
    {
        var editor = new RichTextEditor();

        editor.SetBold(true);
        Assert.True(editor.CaretInlineStyle.Bold);

        editor.InsertText("X");

        Assert.True(editor.Document.Paragraphs[0].Runs[0].Style.Bold);
        Assert.Null(editor.PendingInlineStyle);
    }

    [Fact]
    public void Pending_Styles_Compose_Before_Typing()
    {
        var editor = new RichTextEditor();

        editor.SetBold(true);
        editor.SetItalic(true);
        editor.InsertText("X");

        InlineStyle style = editor.Document.Paragraphs[0].Runs[0].Style;
        Assert.True(style.Bold);
        Assert.True(style.Italic);
    }

    [Fact]
    public void Bold_On_A_Selection_Keeps_The_Selection()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SetSelection(Doc.Range(0, 1, 0, 4));

        editor.SetBold(true);

        Assert.Equal(Doc.Range(0, 1, 0, 4), editor.Selection);
        Assert.True(editor.Document.Paragraphs[0].Runs[1].Style.Bold);
    }

    [Fact]
    public void Foreground_Color_Applies_To_The_Selection()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SelectAll();

        editor.ApplyInlineStyle(InlineStyleDelta.WithForeground(BColor.Red));

        Assert.Equal(BColor.Red, editor.Document.Paragraphs[0].Runs[0].Style.Foreground);
    }

    [Fact]
    public void Indent_And_Outdent_Adjust_The_Caret_Paragraph()
    {
        var editor = new RichTextEditor();
        editor.InsertText("item");

        editor.Indent();
        Assert.Equal(1, editor.Document.Paragraphs[0].Style.IndentLevel);

        editor.Outdent();
        editor.Outdent();
        Assert.Equal(0, editor.Document.Paragraphs[0].Style.IndentLevel);
    }

    [Fact]
    public void Alignment_Applies_To_The_Caret_Paragraph_Without_A_Selection()
    {
        var editor = new RichTextEditor();
        editor.LoadPlainText("a\nb");
        editor.SetCaret(Doc.Pos(1, 0));

        editor.SetAlignment(TextAlignment.Right);

        Assert.Equal(TextAlignment.Left, editor.Document.Paragraphs[0].Style.Alignment);
        Assert.Equal(TextAlignment.Right, editor.Document.Paragraphs[1].Style.Alignment);
    }

    [Fact]
    public void InsertLineBreak_Stays_Within_One_Paragraph()
    {
        var editor = new RichTextEditor();
        editor.InsertText("ab");
        editor.InsertLineBreak();
        editor.InsertText("cd");

        // A soft break (U+2028) stays inside the paragraph rather than splitting it.
        Assert.Equal(1, editor.Document.ParagraphCount);
        Assert.Equal(5, editor.Document.Paragraphs[0].Length);
        Assert.Equal((char)0x2028, editor.Document.Paragraphs[0].Text[2]);
    }
}

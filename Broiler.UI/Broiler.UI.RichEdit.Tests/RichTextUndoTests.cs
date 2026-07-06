namespace Broiler.UI.RichEdit.Tests;

public sealed class RichTextUndoTests
{
    [Fact]
    public void Undo_Restores_Document_And_Selection()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SetSelection(Doc.Range(0, 1, 0, 4));

        editor.InsertText("Z");
        Assert.Equal("HZo", editor.GetPlainText());

        Assert.True(editor.Undo());
        Assert.Equal("Hello", editor.GetPlainText());
        Assert.Equal(Doc.Range(0, 1, 0, 4), editor.Selection);
    }

    [Fact]
    public void Redo_Reapplies_An_Undone_Edit()
    {
        var editor = new RichTextEditor();
        editor.InsertText("a");
        editor.InsertText("b");

        Assert.True(editor.Undo());
        Assert.Equal("a", editor.GetPlainText());

        Assert.True(editor.Redo());
        Assert.Equal("ab", editor.GetPlainText());
        Assert.Equal(Doc.Pos(0, 2), editor.Selection.Focus);
    }

    [Fact]
    public void A_New_Edit_Clears_The_Redo_Stack()
    {
        var editor = new RichTextEditor();
        editor.InsertText("a");
        editor.InsertText("b");
        editor.Undo();

        Assert.True(editor.CanRedo);

        editor.InsertText("c");

        Assert.False(editor.CanRedo);
        Assert.Equal("ac", editor.GetPlainText());
    }

    [Fact]
    public void Undo_On_Empty_History_Returns_False()
    {
        var editor = new RichTextEditor();

        Assert.False(editor.Undo());
        Assert.False(editor.Redo());
    }

    [Fact]
    public void History_Is_Bounded_By_Max_Depth()
    {
        var editor = new RichTextEditor(maxHistoryDepth: 3);

        foreach (char c in "abcde")
            editor.InsertText(c.ToString());

        Assert.Equal(3, editor.UndoStack.Count);

        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());

        Assert.False(editor.CanUndo);
        Assert.Equal("ab", editor.GetPlainText());
    }

    [Fact]
    public void Each_User_Action_Records_One_Transaction_With_Operations()
    {
        var editor = new RichTextEditor();

        editor.InsertText("Hi");

        RichTextTransaction transaction = Assert.Single(editor.UndoStack);
        RichTextOperation operation = Assert.Single(transaction.Operations);
        InsertTextOperation insert = Assert.IsType<InsertTextOperation>(operation);
        Assert.Equal("Hi", insert.Text);
        Assert.Equal(Doc.Pos(0, 0), insert.At);
    }

    [Fact]
    public void Replacing_A_Selection_Records_Delete_Then_Insert()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SelectAll();

        editor.InsertText("Hi");

        RichTextTransaction transaction = editor.UndoStack[^1];
        Assert.Equal(2, transaction.Operations.Count);
        Assert.IsType<DeleteRangeOperation>(transaction.Operations[0]);
        Assert.IsType<InsertTextOperation>(transaction.Operations[1]);
    }

    [Fact]
    public void LoadPlainText_Resets_History()
    {
        var editor = new RichTextEditor();
        editor.InsertText("something");

        editor.LoadPlainText("fresh");

        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.Equal("fresh", editor.GetPlainText());
    }

    [Fact]
    public void Undo_Then_Redo_Round_Trips_Formatting()
    {
        var editor = new RichTextEditor();
        editor.InsertText("Hello");
        editor.SelectAll();
        editor.SetBold(true);

        Assert.True(editor.Document.Paragraphs[0].Runs[0].Style.Bold);

        editor.Undo();
        Assert.False(editor.Document.Paragraphs[0].Runs[0].Style.Bold);

        editor.Redo();
        Assert.True(editor.Document.Paragraphs[0].Runs[0].Style.Bold);
    }
}

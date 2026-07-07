namespace Broiler.UI.RichEdit.Tests;

public sealed class UiRichEditCommandTests
{
    [Fact]
    public void InsertText_Command_Edits_And_Orders_Events()
    {
        var edit = new FakeRichEdit();

        bool changed = edit.ExecuteCommand(RichEditCommand.InsertText, "Hello");

        Assert.True(changed);
        Assert.Equal("Hello", edit.GetPlainText());
        Assert.Equal(new[] { "DocumentChanged", "SelectionChanged", "CommandExecuted:InsertText" }, edit.Events);
    }

    [Fact]
    public void Bold_Command_Toggles_And_Reports_State()
    {
        var edit = new FakeRichEdit();
        edit.ExecuteCommand(RichEditCommand.InsertText, "Hello");
        edit.ExecuteCommand(RichEditCommand.SelectAll);
        edit.Events.Clear();

        edit.ExecuteCommand(RichEditCommand.Bold);

        Assert.True(edit.Document.Paragraphs[0].Runs[0].Style.Bold);
        Assert.True(edit.GetCommandState(RichEditCommand.Bold).IsToggled);
        Assert.Equal(new[] { "DocumentChanged", "CommandExecuted:Bold" }, edit.Events);

        edit.ExecuteCommand(RichEditCommand.Bold);
        Assert.False(edit.Document.Paragraphs[0].Runs[0].Style.Bold);
    }

    [Fact]
    public void SelectAll_Command_Changes_Selection_Only()
    {
        var edit = new FakeRichEdit();
        edit.ExecuteCommand(RichEditCommand.InsertText, "hi");
        edit.Events.Clear();

        edit.ExecuteCommand(RichEditCommand.SelectAll);

        Assert.False(edit.Selection.IsEmpty);
        Assert.Equal(new[] { "SelectionChanged", "CommandExecuted:SelectAll" }, edit.Events);
    }

    [Fact]
    public void Alignment_Commands_Toggle_Exclusively()
    {
        var edit = new FakeRichEdit();
        edit.ExecuteCommand(RichEditCommand.InsertText, "line");

        edit.ExecuteCommand(RichEditCommand.AlignCenter);

        Assert.True(edit.GetCommandState(RichEditCommand.AlignCenter).IsToggled);
        Assert.False(edit.GetCommandState(RichEditCommand.AlignLeft).IsToggled);
        Assert.Equal(TextAlignment.Center, edit.Document.Paragraphs[0].Style.Alignment);
    }

    [Fact]
    public void BulletList_Command_Toggles_On_And_Off()
    {
        var edit = new FakeRichEdit();
        edit.ExecuteCommand(RichEditCommand.InsertText, "item");

        edit.ExecuteCommand(RichEditCommand.BulletList);
        Assert.Equal(ListKind.Bullet, edit.Document.Paragraphs[0].Style.ListKind);

        edit.ExecuteCommand(RichEditCommand.BulletList);
        Assert.Equal(ListKind.None, edit.Document.Paragraphs[0].Style.ListKind);
    }

    [Fact]
    public void Undo_And_Redo_Commands_Reflect_State()
    {
        var edit = new FakeRichEdit();
        Assert.False(edit.GetCommandState(RichEditCommand.Undo).IsEnabled);

        edit.ExecuteCommand(RichEditCommand.InsertText, "abc");
        Assert.True(edit.GetCommandState(RichEditCommand.Undo).IsEnabled);

        edit.ExecuteCommand(RichEditCommand.Undo);
        Assert.Equal(string.Empty, edit.GetPlainText());
        Assert.True(edit.GetCommandState(RichEditCommand.Redo).IsEnabled);

        edit.ExecuteCommand(RichEditCommand.Redo);
        Assert.Equal("abc", edit.GetPlainText());
    }

    [Fact]
    public void ReadOnly_Disables_Editing_Commands()
    {
        var edit = new FakeRichEdit();
        edit.ExecuteCommand(RichEditCommand.InsertText, "abc");
        edit.IsReadOnly = true;
        edit.Events.Clear();

        Assert.False(edit.GetCommandState(RichEditCommand.InsertText).IsEnabled);
        bool changed = edit.ExecuteCommand(RichEditCommand.InsertText, "x");

        Assert.False(changed);
        Assert.Equal("abc", edit.GetPlainText());
        Assert.Empty(edit.Events);
    }

    [Fact]
    public void Disabled_Command_Does_Not_Raise_CommandExecuted()
    {
        var edit = new FakeRichEdit();

        bool changed = edit.ExecuteCommand(RichEditCommand.Undo);

        Assert.False(changed);
        Assert.Empty(edit.Events);
    }

    [Fact]
    public void Copy_Requires_A_Clipboard_Host()
    {
        var edit = new FakeRichEdit();
        edit.ExecuteCommand(RichEditCommand.InsertText, "hello");
        edit.ExecuteCommand(RichEditCommand.SelectAll);

        // Not attached to a host, so no clipboard capability.
        Assert.False(edit.GetCommandState(RichEditCommand.Copy).IsEnabled);
        Assert.False(edit.ExecuteCommand(RichEditCommand.Copy));
    }

    [Fact]
    public void Copy_Cut_Paste_Use_The_Clipboard_Host()
    {
        (UiSession session, FakeRichEdit edit, FakeRichEditHost host) = RichEditHarness.Attach();
        edit.ExecuteCommand(RichEditCommand.InsertText, "hello");
        edit.ExecuteCommand(RichEditCommand.SelectAll);

        Assert.True(edit.GetCommandState(RichEditCommand.Copy).IsEnabled);
        Assert.True(edit.ExecuteCommand(RichEditCommand.Copy));
        Assert.True(host.TryGetText(out string copied));
        Assert.Equal("hello", copied);

        Assert.True(edit.ExecuteCommand(RichEditCommand.Cut));
        Assert.Equal(string.Empty, edit.GetPlainText());

        edit.ExecuteCommand(RichEditCommand.Paste);
        Assert.Equal("hello", edit.GetPlainText());

        session.Dispose();
    }

    [Fact]
    public void Submit_Raises_Submitted_With_Plain_Text()
    {
        var edit = new FakeRichEdit();
        edit.SetPlainText("done");
        edit.Events.Clear();

        edit.Submit();

        Assert.Equal(new[] { "Submitted" }, edit.Events);
    }
}

using Broiler.Graphics;

namespace Broiler.UI.RichEdit.Tests;

public sealed class UiRichEditTests
{
    [Fact]
    public void New_Control_Is_Empty_And_Editable()
    {
        var edit = new FakeRichEdit();

        Assert.Equal(string.Empty, edit.GetPlainText());
        Assert.True(edit.Selection.IsEmpty);
        Assert.True(edit.IsEnabled);
        Assert.False(edit.IsReadOnly);
        Assert.True(edit.AcceptsReturn);
    }

    [Fact]
    public void Setting_Document_Raises_Document_And_Selection_Changed()
    {
        var edit = new FakeRichEdit();

        edit.Document = RichTextDocument.FromPlainText("hello");

        Assert.Equal("hello", edit.GetPlainText());
        Assert.Equal(new[] { "DocumentChanged", "SelectionChanged" }, edit.Events);
    }

    [Fact]
    public void Setting_Null_Document_Throws()
    {
        var edit = new FakeRichEdit();

        Assert.Throws<ArgumentNullException>(() => edit.Document = null!);
    }

    [Fact]
    public void Setting_Same_Selection_Does_Not_Raise()
    {
        var edit = new FakeRichEdit();
        edit.SetPlainText("hello");
        edit.Events.Clear();

        edit.Selection = edit.Selection;

        Assert.Empty(edit.Events);
    }

    [Fact]
    public void Setting_Selection_Raises_SelectionChanged()
    {
        var edit = new FakeRichEdit();
        edit.SetPlainText("hello");
        edit.Events.Clear();

        edit.Selection = new RichTextRange(edit.Document.Start, edit.Document.End);

        Assert.Equal(new[] { "SelectionChanged" }, edit.Events);
        Assert.False(edit.Selection.IsEmpty);
    }

    [Fact]
    public void Semantic_Node_Uses_RichEdit_Role_And_Flat_Text_Metadata()
    {
        var edit = new FakeRichEdit();
        edit.SetPlainText("ab\ncd");
        edit.Selection = RichTextRange.Caret(Doc.Pos(1, 1));

        UiSemanticNode node = edit.GetSemanticNode();

        Assert.Equal(UiSemanticRole.RichEdit, node.Role);
        Assert.NotNull(node.TextInfo);
        Assert.Equal("ab\ncd", node.TextInfo!.Value);
        Assert.Equal(4, node.TextInfo.CaretIndex);
        Assert.True(node.TextInfo.IsEditable);
        Assert.False(node.TextInfo.IsPassword);
    }

    [Fact]
    public void ReadOnly_Is_Reflected_In_Semantics()
    {
        var edit = new FakeRichEdit { IsReadOnly = true };
        edit.SetPlainText("x");

        UiSemanticNode node = edit.GetSemanticNode();

        Assert.True(node.State.HasFlag(UiSemanticState.ReadOnly));
        Assert.False(node.TextInfo!.IsEditable);
    }

    [Fact]
    public void Selection_With_Content_Sets_Selected_State_And_Length()
    {
        var edit = new FakeRichEdit();
        edit.SetPlainText("hello");
        edit.Selection = new RichTextRange(Doc.Pos(0, 1), Doc.Pos(0, 4));

        UiSemanticNode node = edit.GetSemanticNode();

        Assert.True(node.State.HasFlag(UiSemanticState.Selected));
        Assert.Equal(1, node.TextInfo!.SelectionStart);
        Assert.Equal(3, node.TextInfo.SelectionLength);
    }

    [Fact]
    public void Document_Change_Invalidates_Measure_And_Semantic()
    {
        (UiSession session, FakeRichEdit edit, FakeRichEditHost host) = RichEditHarness.Attach();

        edit.ExecuteCommand(RichEditCommand.InsertText, "hi");

        Assert.Contains(host.Invalidations, i =>
            i.Kind.HasFlag(UiInvalidationKind.Measure) && i.Kind.HasFlag(UiInvalidationKind.Semantic));
        session.Dispose();
    }

    [Fact]
    public void Selection_Change_Invalidates_Render_And_Semantic_Only()
    {
        (UiSession session, FakeRichEdit edit, FakeRichEditHost host) = RichEditHarness.Attach();
        edit.SetPlainText("hello");
        host.Invalidations.Clear();

        edit.Selection = new RichTextRange(edit.Document.Start, edit.Document.End);

        Assert.Single(host.Invalidations);
        Assert.Equal(UiInvalidationKind.Render | UiInvalidationKind.Semantic, host.Invalidations[0].Kind);
        session.Dispose();
    }

    [Fact]
    public void PreferredSize_Change_Invalidates_Measure()
    {
        (UiSession session, FakeRichEdit edit, FakeRichEditHost host) = RichEditHarness.Attach();

        edit.PreferredSize = new BSize(400, 200);

        Assert.Contains(host.Invalidations, i => i.Kind.HasFlag(UiInvalidationKind.Measure));
        session.Dispose();
    }
}

using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Text;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditEditingTests
{
    private const char LineSeparator = (char)0x2028;

    private static RichEditScene Focused(string text = "", BSize? size = null)
    {
        RichEditScene scene = Create(size ?? new BSize(300, 160), text);
        scene.Session.RenderFrame();
        scene.Session.SetFocus(scene.Edit);
        return scene;
    }

    private static void Caret(RichEditScene scene, int paragraph, int offset) =>
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(paragraph, offset));

    [Fact]
    public void Typing_Committed_Text_Inserts_At_The_Caret()
    {
        RichEditScene scene = Focused("ac");
        Caret(scene, 0, 1);

        scene.Route.Dispatch(Text("b"));

        Assert.Equal("abc", scene.Edit.GetPlainText());
        Assert.Equal(new RichTextPosition(0, 2), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Typing_Replaces_The_Active_Selection()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));

        scene.Route.Dispatch(Text("bye"));

        Assert.Equal("bye", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Control_Characters_In_Committed_Text_Are_Dropped()
    {
        RichEditScene scene = Focused();

        scene.Route.Dispatch(Text("a\r\n\tb"));

        Assert.Equal("ab", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Backspace_Deletes_The_Character_Before_The_Caret()
    {
        RichEditScene scene = Focused("abc");
        Caret(scene, 0, 3);

        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));

        Assert.Equal("ab", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Delete_Removes_The_Character_After_The_Caret()
    {
        RichEditScene scene = Focused("abc");
        Caret(scene, 0, 0);

        scene.Route.Dispatch(Key("Delete", 0x2E));

        Assert.Equal("bc", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Backspace_At_A_Paragraph_Start_Merges_With_The_Previous_Paragraph()
    {
        RichEditScene scene = Focused("a\nb");
        Caret(scene, 1, 0);

        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));

        Assert.Equal("ab", scene.Edit.GetPlainText());
        Assert.Equal(1, scene.Edit.Document.ParagraphCount);
        scene.Session.Dispose();
    }

    [Fact]
    public void Enter_Splits_The_Paragraph_At_The_Caret()
    {
        RichEditScene scene = Focused("ab");
        Caret(scene, 0, 1);

        scene.Route.Dispatch(Key("Enter", BVirtualKey.Enter));

        Assert.Equal(2, scene.Edit.Document.ParagraphCount);
        Assert.Equal("a\nb", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Enter_Submits_When_AcceptsReturn_Is_False()
    {
        RichEditScene scene = Focused("ab");
        scene.Edit.AcceptsReturn = false;
        Caret(scene, 0, 1);
        string? submitted = null;
        scene.Edit.Submitted += (_, e) => submitted = e.PlainText;

        scene.Route.Dispatch(Key("Enter", BVirtualKey.Enter));

        Assert.Equal("ab", submitted);
        Assert.Equal(1, scene.Edit.Document.ParagraphCount);
        scene.Session.Dispose();
    }

    [Fact]
    public void Shift_Enter_Inserts_A_Soft_Line_Break_Within_The_Paragraph()
    {
        RichEditScene scene = Focused("ab");
        Caret(scene, 0, 1);

        scene.Route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down, KeyboardModifierState.Shift));

        Assert.Equal(1, scene.Edit.Document.ParagraphCount);
        Assert.Contains(LineSeparator, scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_Z_Undoes_And_Ctrl_Y_Redoes_A_Typed_Edit()
    {
        RichEditScene scene = Focused("a");
        Caret(scene, 0, 1);
        scene.Route.Dispatch(Text("b"));
        Assert.Equal("ab", scene.Edit.GetPlainText());

        scene.Route.Dispatch(Key("Z", 0x5A, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.Equal("a", scene.Edit.GetPlainText());

        scene.Route.Dispatch(Key("Y", 0x59, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.Equal("ab", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_C_Copies_The_Selection_To_The_Clipboard()
    {
        RichEditScene scene = Focused("hello world");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));

        scene.Route.Dispatch(Key("C", BVirtualKey.C, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.Equal("hello", scene.Host.ClipboardText);
        Assert.Equal("hello world", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_X_Cuts_The_Selection()
    {
        RichEditScene scene = Focused("hello world");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 6));

        scene.Route.Dispatch(Key("X", 0x58, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.Equal("hello ", scene.Host.ClipboardText);
        Assert.Equal("world", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_V_Pastes_Clipboard_Text_At_The_Caret()
    {
        RichEditScene scene = Focused("ac");
        scene.Host.ClipboardText = "b";
        Caret(scene, 0, 1);

        scene.Route.Dispatch(Key("V", 0x56, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.Equal("abc", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_B_With_No_Selection_Arms_A_Pending_Bold_Style()
    {
        RichEditScene scene = Focused("hi");
        Caret(scene, 0, 2);

        scene.Route.Dispatch(Key("B", 0x42, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.True(scene.Edit.GetCommandState(RichEditCommand.Bold).IsToggled);
        Assert.True(scene.Edit.CaretInlineStyle.Bold);

        scene.Route.Dispatch(Text("X"));
        Assert.True(scene.Edit.Document.Paragraphs[0].StyleAt(2).Bold);
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_B_Toggles_Bold_On_The_Selection()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));

        scene.Route.Dispatch(Key("B", 0x42, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.True(scene.Edit.Document.Paragraphs[0].StyleAt(0).Bold);
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Blocks_Typing_And_Deletion()
    {
        RichEditScene scene = Focused("abc");
        scene.Edit.IsReadOnly = true;
        Caret(scene, 0, 3);

        scene.Route.Dispatch(Text("d"));
        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));

        Assert.Equal("abc", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Ime_Composition_Previews_Without_Committing_Then_Commits()
    {
        RichEditScene scene = Focused();

        scene.Route.Dispatch(Composition("ni", TextCompositionState.Updated));
        Assert.Equal("ni", scene.Edit.CompositionText);
        Assert.True(scene.Edit.GetSemanticNode().TextInfo?.IsCompositionActive);
        Assert.Equal("", scene.Edit.GetPlainText());

        scene.Route.Dispatch(Composition("hi", TextCompositionState.Committed));
        Assert.Equal("", scene.Edit.CompositionText);
        Assert.Equal("hi", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Cancelled_Composition_Clears_The_Preview_Without_Editing()
    {
        RichEditScene scene = Focused("x");
        Caret(scene, 0, 1);

        scene.Route.Dispatch(Composition("ni", TextCompositionState.Updated));
        scene.Route.Dispatch(Composition("", TextCompositionState.Cancelled));

        Assert.Equal("", scene.Edit.CompositionText);
        Assert.Equal("x", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Editing_Raises_Document_And_Command_Events()
    {
        RichEditScene scene = Focused("a");
        Caret(scene, 0, 1);
        int documentChanges = 0;
        RichEditCommand executed = RichEditCommand.None;
        scene.Edit.DocumentChanged += (_, _) => documentChanges++;
        scene.Edit.CommandExecuted += (_, e) => executed = e.Command;

        scene.Route.Dispatch(Text("b"));

        Assert.True(documentChanges > 0);
        Assert.Equal(RichEditCommand.InsertText, executed);
        scene.Session.Dispose();
    }
}

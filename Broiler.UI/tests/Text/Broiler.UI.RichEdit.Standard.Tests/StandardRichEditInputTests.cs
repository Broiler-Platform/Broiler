using Broiler.Graphics;
using Broiler.Input.Keyboard;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditInputTests
{
    private static RichEditScene Focused(string text, BSize? size = null)
    {
        RichEditScene scene = Create(size ?? new BSize(300, 160), text);
        scene.Session.RenderFrame();
        scene.Session.SetFocus(scene.Edit);
        return scene;
    }

    [Fact]
    public void ArrowRight_And_Left_Move_The_Caret_By_A_Character()
    {
        RichEditScene scene = Focused("abc");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 0));

        scene.Route.Dispatch(Key("Right", BVirtualKey.Right));
        Assert.Equal(new RichTextPosition(0, 1), scene.Edit.Selection.Focus);

        scene.Route.Dispatch(Key("Left", BVirtualKey.Left));
        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void ArrowDown_Moves_To_The_Next_Line()
    {
        RichEditScene scene = Focused("abc\ndef");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));

        scene.Route.Dispatch(Key("Down", BVirtualKey.Down));

        Assert.Equal(1, scene.Edit.Selection.Focus.ParagraphIndex);
        scene.Session.Dispose();
    }

    [Fact]
    public void ArrowUp_From_Second_Line_Returns_To_First()
    {
        RichEditScene scene = Focused("abc\ndef");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(1, 2));

        scene.Route.Dispatch(Key("Up", BVirtualKey.Up));

        Assert.Equal(0, scene.Edit.Selection.Focus.ParagraphIndex);
        scene.Session.Dispose();
    }

    [Fact]
    public void ShiftArrow_Extends_The_Selection_From_A_Fixed_Anchor()
    {
        RichEditScene scene = Focused("abc");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 0));

        scene.Route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down, KeyboardModifierState.Shift));
        scene.Route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down, KeyboardModifierState.Shift));

        Assert.False(scene.Edit.Selection.IsEmpty);
        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Anchor);
        Assert.Equal(new RichTextPosition(0, 2), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Home_And_End_Move_To_Line_Edges()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 2));

        scene.Route.Dispatch(Key("Home", BVirtualKey.Home));
        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Focus);

        scene.Route.Dispatch(Key("End", BVirtualKey.End));
        Assert.Equal(new RichTextPosition(0, 5), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_Home_And_End_Move_To_Document_Edges()
    {
        RichEditScene scene = Focused("a\nb\nc");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(1, 0));

        scene.Route.Dispatch(Key("End", BVirtualKey.End, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.Equal(scene.Edit.Document.End, scene.Edit.Selection.Focus);

        scene.Route.Dispatch(Key("Home", BVirtualKey.Home, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.Equal(scene.Edit.Document.Start, scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_Right_Moves_By_Word()
    {
        RichEditScene scene = Focused("hello world");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 0));

        scene.Route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.Equal(new RichTextPosition(0, 6), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_A_Selects_The_Whole_Document()
    {
        RichEditScene scene = Focused("abc\ndef");

        scene.Route.Dispatch(Key("A", BVirtualKey.A, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.Equal(scene.Edit.Document.Start, scene.Edit.Selection.Anchor);
        Assert.Equal(scene.Edit.Document.End, scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Mouse_Click_Focuses_And_Places_The_Caret()
    {
        RichEditScene scene = Focused("hello world");

        scene.Route.Dispatch(MouseDown(8, 8));

        Assert.Same(scene.Edit, scene.Session.FocusedElement);
        Assert.True(scene.Edit.Selection.IsEmpty);
        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Mouse_Drag_Selects_A_Range_From_The_Press_Point()
    {
        RichEditScene scene = Focused("hello world");

        scene.Route.Dispatch(MouseDown(8, 8));
        scene.Route.Dispatch(MouseMove(260, 8));

        Assert.False(scene.Edit.Selection.IsEmpty);
        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Anchor);
        Assert.True(scene.Edit.Selection.Focus.Offset > 0);
        scene.Session.Dispose();
    }

    [Fact]
    public void Double_Click_Selects_A_Word()
    {
        RichEditScene scene = Focused("hello world");

        scene.Route.Dispatch(MouseDown(20, 8));
        scene.Route.Dispatch(MouseUp(20, 8));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(100));
        scene.Route.Dispatch(MouseDown(20, 8));

        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Start);
        Assert.Equal(new RichTextPosition(0, 5), scene.Edit.Selection.End);
        scene.Session.Dispose();
    }

    [Fact]
    public void Wheel_Scrolls_Vertically()
    {
        string text = string.Join("\n", Enumerable.Range(0, 30).Select(i => "line " + i));
        RichEditScene scene = Focused(text, new BSize(200, 100));
        double before = scene.Edit.VerticalScrollOffset;

        scene.Route.Dispatch(Wheel(50, 50, -1));

        Assert.True(scene.Edit.VerticalScrollOffset > before);
        scene.Session.Dispose();
    }

    [Fact]
    public void PageDown_Moves_The_Caret_Down_And_Scrolls()
    {
        string text = string.Join("\n", Enumerable.Range(0, 40).Select(i => "row" + i));
        RichEditScene scene = Focused(text, new BSize(200, 100));
        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.Start);

        scene.Route.Dispatch(Key("PageDown", BVirtualKey.PageDown));

        Assert.True(scene.Edit.Selection.Focus.ParagraphIndex > 0);
        Assert.True(scene.Edit.VerticalScrollOffset > 0);
        scene.Session.Dispose();
    }

    [Fact]
    public void Caret_Geometry_Is_Published_To_The_Text_Input_Host()
    {
        RichEditScene scene = Focused("abc");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 2));

        scene.Session.RenderFrame();

        Assert.NotNull(scene.Host.LastCaret);
        Assert.Equal(2, scene.Host.LastCaret!.CaretIndex);
        scene.Session.Dispose();
    }

    [Fact]
    public void Detaching_Clears_The_Published_Caret()
    {
        RichEditScene scene = Focused("abc");
        scene.Session.RenderFrame();

        scene.Session.RemoveRoot(scene.Edit);

        Assert.True(scene.Host.ClearCaretCount > 0);
        scene.Session.Dispose();
    }
}

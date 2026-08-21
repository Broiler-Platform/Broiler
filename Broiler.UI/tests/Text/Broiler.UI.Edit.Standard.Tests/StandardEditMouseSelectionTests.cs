using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

using static Broiler.UI.Edit.Standard.Tests.EditStandardHarness;

namespace Broiler.UI.Edit.Standard.Tests;

/// <summary>
/// Marking text with the mouse: press to place the caret, drag to mark, Shift to
/// extend from the existing anchor, and a double click to take the whole field.
/// </summary>
public sealed class StandardEditMouseSelectionTests
{
    [Fact(Timeout = 600000)]
    public void A_Press_Places_The_Caret_And_Clears_The_Mark()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 11);

        Assert.True(scene.Route.Dispatch(MouseDown(scene.TextX(5), scene.CenterY())));

        Assert.Equal(5, scene.Edit.CaretIndex);
        Assert.False(scene.Edit.HasSelection);
    }

    [Fact(Timeout = 600000)]
    public void Dragging_Right_Marks_From_The_Press_Point_To_The_Pointer()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(2), scene.CenterY()));
        Assert.True(scene.Route.Dispatch(MouseDrag(scene.TextX(8), scene.CenterY())));

        Assert.Equal(2, scene.Edit.SelectionStart);
        Assert.Equal(8, scene.Edit.SelectionEnd);
        Assert.Equal(8, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void Dragging_Left_Marks_Backwards_And_Leaves_The_Caret_At_The_Start()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(8), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(2), scene.CenterY()));

        Assert.Equal(2, scene.Edit.SelectionStart);
        Assert.Equal(8, scene.Edit.SelectionEnd);
        Assert.Equal(2, scene.Edit.CaretIndex);
        Assert.Equal(8, scene.Edit.SelectionAnchor);
    }

    [Fact(Timeout = 600000)]
    public void A_Drag_Back_Over_The_Anchor_Collapses_The_Mark()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(9), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(4), scene.CenterY()));

        Assert.False(scene.Edit.HasSelection);
        Assert.Equal(4, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Drag_Past_The_Right_Edge_Marks_To_The_End_Of_The_Text()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(3), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.Edit.Bounds.Right + 200, scene.CenterY()));

        Assert.Equal(3, scene.Edit.SelectionStart);
        Assert.Equal(11, scene.Edit.SelectionEnd);
    }

    [Fact(Timeout = 600000)]
    public void A_Drag_Past_The_Left_Edge_Marks_To_The_Start_Of_The_Text()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(6), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.Edit.Bounds.Left - 200, scene.CenterY()));

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(6, scene.Edit.SelectionEnd);
        Assert.Equal(0, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Move_After_The_Release_No_Longer_Marks()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(2), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(6), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(6), scene.CenterY()));
        scene.Route.Dispatch(MouseMove(scene.TextX(10), scene.CenterY()));

        Assert.Equal(2, scene.Edit.SelectionStart);
        Assert.Equal(6, scene.Edit.SelectionEnd);
    }

    [Fact(Timeout = 600000)]
    public void The_Release_Gives_Up_The_Input_Capture()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(2), scene.CenterY()));
        Assert.Same(scene.Edit, scene.Session.CapturedElement);

        scene.Route.Dispatch(MouseUp(scene.TextX(2), scene.CenterY()));
        Assert.Null(scene.Session.CapturedElement);
    }

    [Fact(Timeout = 600000)]
    public void Shift_Click_Extends_The_Mark_From_The_Existing_Anchor()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(3), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(3), scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromSeconds(2));
        scene.Route.Dispatch(MouseDown(scene.TextX(9), scene.CenterY(), modifiers: InputModifiers.Shift));

        Assert.Equal(3, scene.Edit.SelectionStart);
        Assert.Equal(9, scene.Edit.SelectionEnd);
    }

    [Fact(Timeout = 600000)]
    public void Shift_Click_Before_The_Anchor_Marks_Backwards()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(7), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(7), scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromSeconds(2));
        scene.Route.Dispatch(MouseDown(scene.TextX(1), scene.CenterY(), modifiers: InputModifiers.Shift));

        Assert.Equal(1, scene.Edit.SelectionStart);
        Assert.Equal(7, scene.Edit.SelectionEnd);
        Assert.Equal(1, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Double_Click_Selects_All_Of_The_Text()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(4), scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(120));
        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(11, scene.Edit.SelectionEnd);
        Assert.Equal(11, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Double_Click_Selects_All_Of_A_Password_Field()
    {
        EditScene scene = Create("secret");
        scene.Edit.IsPassword = true;

        scene.Route.Dispatch(MouseDown(scene.Edit.Bounds.Left + 12, scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.Edit.Bounds.Left + 12, scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(120));
        scene.Route.Dispatch(MouseDown(scene.Edit.Bounds.Left + 12, scene.CenterY()));

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(6, scene.Edit.SelectionEnd);
    }

    [Fact(Timeout = 600000)]
    public void A_Second_Click_After_The_Double_Click_Window_Only_Places_The_Caret()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(4), scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(900));
        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));

        Assert.False(scene.Edit.HasSelection);
        Assert.Equal(4, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Second_Click_Far_From_The_First_Only_Places_The_Caret()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(1), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(1), scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(120));
        scene.Route.Dispatch(MouseDown(scene.TextX(9), scene.CenterY()));

        Assert.False(scene.Edit.HasSelection);
        Assert.Equal(9, scene.Edit.CaretIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Drag_After_A_Double_Click_Keeps_The_Whole_Field_Marked()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));
        scene.Route.Dispatch(MouseUp(scene.TextX(4), scene.CenterY()));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(120));
        scene.Route.Dispatch(MouseDown(scene.TextX(4), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(6), scene.CenterY()));

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(11, scene.Edit.SelectionEnd);
    }

    [Fact(Timeout = 600000)]
    public void A_Marked_Range_Is_Painted_Behind_The_Text()
    {
        EditScene scene = Create("Hello world");

        scene.Route.Dispatch(MouseDown(scene.TextX(0), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(5), scene.CenterY()));
        BRenderList frame = scene.Render();

        BRenderCommand.FillRect highlight = Assert.Single(
            frame.Commands.OfType<BRenderCommand.FillRect>()
                .Where(command => command.Color == scene.Edit.SelectionBackground));
        Assert.Equal(scene.TextX(0), highlight.Rect.Left, 3);
        Assert.Equal(scene.TextX(5), highlight.Rect.Right, 3);
    }

    [Fact(Timeout = 600000)]
    public void A_Disabled_Edit_Does_Not_Mark_On_A_Drag()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.IsEnabled = false;

        scene.Route.Dispatch(MouseDown(scene.TextX(2), scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(scene.TextX(8), scene.CenterY()));

        Assert.False(scene.Edit.HasSelection);
    }

    [Fact(Timeout = 600000)]
    public void A_Right_To_Left_Edit_Marks_From_The_Point_The_Glyphs_Are_Drawn_At()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.Direction = UiEditTextDirection.RightToLeft;
        scene.Render();

        double width = BTextMeasurer.MeasureAdvance(scene.Edit.Text, scene.Edit.Font);
        double origin = scene.Edit.Bounds.Right - scene.Edit.PaddingX - width;
        double fifth = origin + BTextMeasurer.MeasureAdvance(scene.Edit.Text[..5], scene.Edit.Font);

        scene.Route.Dispatch(MouseDown(origin, scene.CenterY()));
        scene.Route.Dispatch(MouseDrag(fifth, scene.CenterY()));

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(5, scene.Edit.SelectionEnd);
    }
}

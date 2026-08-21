using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;

using static Broiler.UI.Edit.Standard.Tests.EditStandardHarness;

namespace Broiler.UI.Edit.Standard.Tests;

public sealed class StandardEditContextMenuTests
{
    [Fact(Timeout = 600000)]
    public void Right_Click_Opens_The_Menu_Focuses_The_Field_And_Captures_Input()
    {
        EditScene scene = Create("Hello world");

        Assert.True(scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(scene.TextX(5), scene.CenterY(), MouseButton.Right))));

        Assert.True(scene.Edit.IsContextMenuOpen);
        Assert.Same(scene.Edit, scene.Session.FocusedElement);
        Assert.Same(scene.Edit, scene.Session.CapturedElement);

        // The release of the opening click belongs to the menu, not to the field.
        Assert.True(scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseUp(scene.TextX(5), scene.CenterY(), MouseButton.Right))));
        Assert.True(scene.Edit.IsContextMenuOpen);
    }

    [Fact(Timeout = 600000)]
    public void Right_Click_Outside_The_Selection_Moves_The_Caret_And_Drops_The_Selection()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);

        scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(scene.TextX(9), scene.CenterY(), MouseButton.Right)));

        Assert.False(scene.Edit.HasSelection);
        Assert.Equal(9, scene.Edit.CaretIndex);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void Right_Click_Inside_The_Selection_Keeps_It_So_Copy_Acts_On_It()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);

        scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(scene.TextX(2), scene.CenterY(), MouseButton.Right)));

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(5, scene.Edit.SelectionLength);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void Clicking_Copy_Copies_The_Selection_And_Closes_The_Menu()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);
        OpenMenuAt(scene, scene.TextX(2));

        ClickItem(scene, StandardEditContextMenuCommand.Copy);

        Assert.Equal("Hello", scene.Host.ClipboardText);
        Assert.Equal("Hello world", scene.Edit.Text);
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(scene.Session.CapturedElement);
    }

    [Fact(Timeout = 600000)]
    public void Clicking_Cut_Then_Paste_Moves_Text_Through_The_Clipboard()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 6);
        OpenMenuAt(scene, scene.TextX(2));
        ClickItem(scene, StandardEditContextMenuCommand.Cut);

        Assert.Equal("world", scene.Edit.Text);
        Assert.Equal("Hello ", scene.Host.ClipboardText);

        scene.Edit.SetSelection(5, 0);
        OpenMenuAt(scene, scene.TextX(5));
        ClickItem(scene, StandardEditContextMenuCommand.Paste);

        Assert.Equal("worldHello ", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Clicking_Delete_Removes_The_Selection_Without_Touching_The_Clipboard()
    {
        EditScene scene = Create("Hello world", clipboardText: "untouched");
        scene.Edit.SetSelection(5, 6);
        OpenMenuAt(scene, scene.TextX(7));

        ClickItem(scene, StandardEditContextMenuCommand.Delete);

        Assert.Equal("Hello", scene.Edit.Text);
        Assert.Equal("untouched", scene.Host.ClipboardText);
    }

    [Fact(Timeout = 600000)]
    public void Clicking_Select_All_Selects_The_Whole_Field()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(3));

        ClickItem(scene, StandardEditContextMenuCommand.SelectAll);

        Assert.Equal(0, scene.Edit.SelectionStart);
        Assert.Equal(11, scene.Edit.SelectionLength);
    }

    [Fact(Timeout = 600000)]
    public void A_Disabled_Row_Neither_Runs_Nor_Closes_The_Menu()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(3));
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);

        ClickItem(scene, StandardEditContextMenuCommand.Copy);

        Assert.Null(scene.Host.ClipboardText);
        Assert.True(scene.Edit.IsContextMenuOpen);
    }

    [Fact(Timeout = 600000)]
    public void Enablement_Reflects_Selection_Clipboard_And_Undo_State()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(3));

        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Undo).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Cut).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Paste).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Delete).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.SelectAll).IsEnabled);

        scene.Edit.CloseContextMenu();
        scene.Host.ClipboardText = "ready";
        scene.Edit.SetSelection(0, 5);
        scene.Edit.InsertCommittedText("Howdy");
        scene.Edit.SetSelection(0, 5);
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Undo).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Cut).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Paste).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Delete).IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void A_Password_Field_Offers_Paste_But_Not_Cut_Or_Copy()
    {
        EditScene scene = Create("secret", clipboardText: "ready");
        scene.Edit.IsPassword = true;
        scene.Edit.SelectAll();
        OpenMenuAt(scene, scene.TextX(0));

        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Cut).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Paste).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Delete).IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void A_Read_Only_Field_Offers_Only_Copy_And_Select_All()
    {
        EditScene scene = Create("Hello world", clipboardText: "ready");
        scene.Edit.IsReadOnly = true;
        scene.Edit.SetSelection(0, 5);
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.Copy).IsEnabled);
        Assert.True(FindItem(scene.Edit, StandardEditContextMenuCommand.SelectAll).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Cut).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Paste).IsEnabled);
        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Delete).IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void Paste_Stays_Disabled_When_The_Clipboard_Holds_Nothing_Insertable()
    {
        EditScene scene = Create("Hello", clipboardText: "\r\n\t");
        OpenMenuAt(scene, scene.TextX(2));

        Assert.False(FindItem(scene.Edit, StandardEditContextMenuCommand.Paste).IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void The_Menu_Key_And_Shift_F10_Open_The_Menu_Below_The_Caret()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(5, 0);

        Assert.True(scene.Route.Dispatch(Key("F10", TestKey.F10, KeyboardModifierState.Shift)));
        Assert.True(scene.Edit.IsContextMenuOpen);
        Assert.True(scene.Edit.ContextMenuBounds.Top >= scene.Edit.Bounds.Top);

        Assert.True(scene.Route.Dispatch(Key("Escape", 0x1B)));
        Assert.False(scene.Edit.IsContextMenuOpen);

        Assert.True(scene.Route.Dispatch(Key("ContextMenu", TestKey.ContextMenu)));
        Assert.True(scene.Edit.IsContextMenuOpen);
    }

    [Fact(Timeout = 600000)]
    public void Arrow_Keys_Skip_Separators_And_Disabled_Rows_Before_Enter_Runs_The_Row()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(scene.Route.Dispatch(Key("Down", 0x28)));
        Assert.Equal(StandardEditContextMenuCommand.Cut, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);

        Assert.True(scene.Route.Dispatch(Key("Down", 0x28)));
        Assert.Equal(StandardEditContextMenuCommand.Copy, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);

        Assert.True(scene.Route.Dispatch(Key("Up", 0x26)));
        Assert.Equal(StandardEditContextMenuCommand.Cut, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);

        Assert.True(scene.Route.Dispatch(Key("Enter", 0x0D)));
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Equal("Hello", scene.Host.ClipboardText);
        Assert.Equal(" world", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void End_Highlights_The_Last_Runnable_Row()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(scene.Route.Dispatch(Key("End", 0x23)));

        Assert.Equal(StandardEditContextMenuCommand.SelectAll, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);
    }

    [Fact(Timeout = 600000)]
    public void Typing_A_Row_Letter_Cycles_Between_Rows_That_Share_It()
    {
        EditScene scene = Create("Hello world", clipboardText: "ready");
        scene.Edit.SetSelection(0, 5);
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(scene.Route.Dispatch(Text("c")));
        Assert.Equal(StandardEditContextMenuCommand.Cut, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);

        Assert.True(scene.Route.Dispatch(Text("c")));
        Assert.Equal(StandardEditContextMenuCommand.Copy, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);
    }

    [Fact(Timeout = 600000)]
    public void Hovering_A_Row_Highlights_It()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(2));
        BPoint row = RowCenter(scene.Edit, StandardEditContextMenuCommand.SelectAll);

        Assert.True(scene.Session.DispatchInput(UiInputEvent.FromMouseMove(MouseMove(row.X, row.Y))));

        Assert.Equal(StandardEditContextMenuCommand.SelectAll, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);
    }

    [Fact(Timeout = 600000)]
    public void Typing_While_The_Menu_Is_Open_Does_Not_Reach_The_Field()
    {
        EditScene scene = Create("Hello");
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(scene.Route.Dispatch(Text("z")));
        Assert.True(scene.Route.Dispatch(Key("Backspace", 0x08)));

        Assert.Equal("Hello", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void A_Click_Outside_The_Menu_Dismisses_It_Without_Editing()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);
        OpenMenuAt(scene, scene.TextX(2));

        Assert.True(scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(2, 2))));

        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(scene.Session.CapturedElement);
        Assert.Equal("Hello world", scene.Edit.Text);
        Assert.Null(scene.Host.ClipboardText);
    }

    [Fact(Timeout = 600000)]
    public void The_Menu_Stays_Inside_The_Viewport_When_Opened_At_A_Corner()
    {
        EditScene scene = Create("Hello world", viewportSize: new BSize(300, 120));

        Assert.True(scene.Edit.OpenContextMenu(new BPoint(295, 118)));

        BRect menu = scene.Edit.ContextMenuBounds;
        Assert.True(menu.Left >= 0);
        Assert.True(menu.Top >= 0);
        Assert.True(menu.Right <= 300);
        Assert.True(menu.Bottom <= 120);
    }

    [Fact(Timeout = 600000)]
    public void The_Open_Menu_Paints_Over_Later_Siblings_And_Appears_In_The_Semantic_Tree()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.ContextMenuBackground = BColor.FromArgb(255, 17, 29, 43);
        OpenMenuAt(scene, scene.TextX(2));

        BRenderList rendered = scene.Render();

        Assert.Contains(
            rendered.Commands,
            command => command is BRenderCommand.FillRoundedRect fill && fill.Color == scene.Edit.ContextMenuBackground);

        UiSemanticNode semantic = scene.Edit.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Edit, semantic.Role);
        UiSemanticNode menu = Assert.Single(semantic.Children);
        Assert.Equal(UiSemanticRole.Menu, menu.Role);
        Assert.True(menu.State.HasFlag(UiSemanticState.Expanded));

        scene.Edit.CloseContextMenu();
        Assert.Empty(scene.Edit.GetSemanticNode().Children);
    }

    [Fact(Timeout = 600000)]
    public void Detaching_The_Field_Closes_The_Menu_And_Releases_The_Capture()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(2));

        scene.Edit.Dispose();

        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(scene.Session.CapturedElement);
    }

    [Fact(Timeout = 600000)]
    public void A_Disabled_Field_Dismisses_The_Menu_And_Refuses_To_Open_One()
    {
        EditScene scene = Create("Hello world");
        OpenMenuAt(scene, scene.TextX(2));

        scene.Edit.IsEnabled = false;
        Assert.False(scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(scene.TextX(2), scene.CenterY(), MouseButton.Right))));

        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.False(scene.Edit.OpenContextMenu(new BPoint(30, 30)));
    }

    /// <summary>A host-driven menu runs the same commands as the drawn one.</summary>
    [Fact(Timeout = 600000)]
    public void Commands_Can_Be_Invoked_Without_Opening_The_Drawn_Menu()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);

        Assert.True(scene.Edit.InvokeContextMenuCommand(StandardEditContextMenuCommand.Cut));

        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Equal("Hello", scene.Host.ClipboardText);
        Assert.Equal(" world", scene.Edit.Text);
    }

    private static void OpenMenuAt(EditScene scene, double x) =>
        scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(x, scene.CenterY(), MouseButton.Right)));

    private static void ClickItem(EditScene scene, StandardEditContextMenuCommand command)
    {
        BPoint point = RowCenter(scene.Edit, command);
        scene.Session.DispatchInput(UiInputEvent.FromMouseButton(MouseDown(point.X, point.Y, MouseButton.Left)));
    }

    private static StandardEditContextMenuItem FindItem(StandardEdit edit, StandardEditContextMenuCommand command) =>
        edit.ContextMenuItems.Single(item => item.Command == command);

    /// <summary>
    /// The middle of a row, walked the same way the control lays the popup out —
    /// separators are shorter than command rows.
    /// </summary>
    private static BPoint RowCenter(StandardEdit edit, StandardEditContextMenuCommand command)
    {
        BRect menu = edit.ContextMenuBounds;
        double top = menu.Top + 4;
        foreach (StandardEditContextMenuItem item in edit.ContextMenuItems)
        {
            double height = item.IsSeparator ? 7 : edit.ContextMenuItemHeight;
            if (item.Command == command)
                return new BPoint(menu.Left + (menu.Width / 2), top + (height / 2));

            top += height;
        }

        throw new InvalidOperationException($"The context menu has no '{command}' row.");
    }
}

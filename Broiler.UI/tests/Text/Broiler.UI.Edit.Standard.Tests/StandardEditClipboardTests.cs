using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.UI.Standard;

using static Broiler.UI.Edit.Standard.Tests.EditStandardHarness;

namespace Broiler.UI.Edit.Standard.Tests;

public sealed class StandardEditClipboardTests
{
    [Fact(Timeout = 600000)]
    public void Control_C_Copies_The_Selection_Without_Changing_The_Text()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);

        Assert.True(scene.Route.Dispatch(Key("C", TestKey.C, KeyboardModifierState.Control)));

        Assert.Equal("Hello", scene.Host.ClipboardText);
        Assert.Equal("Hello world", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Control_X_Cuts_The_Selection_And_Control_Z_Restores_It()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(5, 6);

        Assert.True(scene.Route.Dispatch(Key("X", TestKey.X, KeyboardModifierState.Control)));

        Assert.Equal(" world", scene.Host.ClipboardText);
        Assert.Equal("Hello", scene.Edit.Text);

        Assert.True(scene.Route.Dispatch(Key("Z", TestKey.Z, KeyboardModifierState.Control)));
        Assert.Equal("Hello world", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Control_V_Replaces_The_Selection_And_Leaves_The_Caret_After_The_Insert()
    {
        EditScene scene = Create("Hello world", clipboardText: "brave new");
        scene.Edit.SetSelection(6, 5);

        Assert.True(scene.Route.Dispatch(Key("V", TestKey.V, KeyboardModifierState.Control)));

        Assert.Equal("Hello brave new", scene.Edit.Text);
        Assert.Equal(15, scene.Edit.CaretIndex);
        Assert.False(scene.Edit.HasSelection);
    }

    [Fact(Timeout = 600000)]
    public void Control_Insert_Copies_And_Shift_Insert_Pastes()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 5);

        Assert.True(scene.Route.Dispatch(Key("Insert", TestKey.Insert, KeyboardModifierState.Control)));
        Assert.Equal("Hello", scene.Host.ClipboardText);

        scene.Edit.SetSelection(11, 0);
        Assert.True(scene.Route.Dispatch(Key("Insert", TestKey.Insert, KeyboardModifierState.Shift)));
        Assert.Equal("Hello worldHello", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Shift_Delete_Cuts_While_Plain_Delete_Still_Deletes_Forward()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.SetSelection(0, 6);

        Assert.True(scene.Route.Dispatch(Key("Delete", TestKey.Delete, KeyboardModifierState.Shift)));
        Assert.Equal("Hello ", scene.Host.ClipboardText);
        Assert.Equal("world", scene.Edit.Text);

        scene.Edit.SetSelection(0, 0);
        Assert.True(scene.Route.Dispatch(Key("Delete", TestKey.Delete)));
        Assert.Equal("orld", scene.Edit.Text);
        Assert.Equal("Hello ", scene.Host.ClipboardText);
    }

    [Fact(Timeout = 600000)]
    public void Plain_Insert_Is_Left_To_The_Host()
    {
        EditScene scene = Create("Hello", clipboardText: "pasted");

        Assert.False(scene.Route.Dispatch(Key("Insert", TestKey.Insert)));
        Assert.Equal("Hello", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void A_Password_Field_Neither_Copies_Nor_Cuts()
    {
        EditScene scene = Create("secret");
        scene.Edit.IsPassword = true;
        scene.Edit.SelectAll();

        Assert.False(scene.Route.Dispatch(Key("C", TestKey.C, KeyboardModifierState.Control)));
        Assert.False(scene.Route.Dispatch(Key("X", TestKey.X, KeyboardModifierState.Control)));

        Assert.Null(scene.Host.ClipboardText);
        Assert.Equal("secret", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void A_Read_Only_Field_Copies_But_Neither_Cuts_Nor_Pastes()
    {
        EditScene scene = Create("Hello world", clipboardText: "replacement");
        scene.Edit.IsReadOnly = true;
        scene.Edit.SetSelection(0, 5);

        Assert.True(scene.Route.Dispatch(Key("C", TestKey.C, KeyboardModifierState.Control)));
        Assert.Equal("Hello", scene.Host.ClipboardText);

        Assert.False(scene.Route.Dispatch(Key("X", TestKey.X, KeyboardModifierState.Control)));
        Assert.False(scene.Route.Dispatch(Key("V", TestKey.V, KeyboardModifierState.Control)));
        Assert.Equal("Hello world", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Pasting_Multiple_Lines_Keeps_The_Single_Line_Field_On_One_Line()
    {
        EditScene scene = Create(string.Empty, clipboardText: "first\r\nsecond\tthird");

        Assert.True(scene.Route.Dispatch(Key("V", TestKey.V, KeyboardModifierState.Control)));

        Assert.Equal("firstsecondthird", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Pasting_Stops_At_MaxLength()
    {
        EditScene scene = Create("abc", clipboardText: "defgh");
        scene.Edit.MaxLength = 5;
        scene.Edit.SetSelection(3, 0);

        Assert.True(scene.Route.Dispatch(Key("V", TestKey.V, KeyboardModifierState.Control)));

        Assert.Equal("abcde", scene.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Copy_Without_A_Clipboard_Host_Reports_Unhandled_Rather_Than_Throwing()
    {
        var host = new TestHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var edit = new StandardEdit { Text = "Hello" };
        session.AddRoot(edit);
        session.SetFocus(edit);
        edit.SelectAll();

        Assert.False(edit.Copy());
        Assert.False(edit.Paste());
        Assert.Equal("Hello", edit.Text);
    }
}

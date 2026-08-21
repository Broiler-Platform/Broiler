using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Covers <see cref="UiListSelectionMode.Multiple"/>. The mode is opt-in and
/// <see cref="UiListSelectionMode.Single"/> is the default, so the load-bearing
/// property is that a list which never asks for it behaves exactly as before —
/// pinned by <see cref="A_Single_Selection_List_Still_Replaces_On_Every_Click"/>.
/// </summary>
public sealed class ListViewMultiSelectionTests
{
    private const double RowHeight = 20;

    private static StandardListView CreateList(UiListSelectionMode mode)
    {
        var listView = new StandardListView
        {
            ItemHeight = RowHeight,
            SelectionMode = mode,
        };
        listView.SetItems(
        [
            new UiListItem("a", "A"),
            new UiListItem("b", "B"),
            new UiListItem("c", "C"),
            new UiListItem("d", "D"),
        ]);
        listView.Arrange(new BRect(0, 0, 120, 4 * RowHeight));
        return listView;
    }

    /// <summary>Y coordinate inside the row at <paramref name="index"/>.</summary>
    private static double RowY(int index) => (index * RowHeight) + (RowHeight / 2);

    [Fact(Timeout = 600000)]
    public void Single_Is_The_Default_Mode()
    {
        Assert.Equal(UiListSelectionMode.Single, new StandardListView().SelectionMode);
    }

    [Fact(Timeout = 600000)]
    public void A_Single_Selection_List_Still_Replaces_On_Every_Click()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Single);

        listView.DispatchInput(PointerDown(5, RowY(0)));
        listView.DispatchInput(PointerDown(5, RowY(2)));

        // Clicking never accumulates a selection a single-selection list cannot hold.
        Assert.Equal("c", listView.SelectedItemId);
        Assert.Equal(["c"], listView.SelectedItemIds);
    }

    /// <summary>
    /// An unmodified click toggles rather than replacing: a touch contact arrives as
    /// a synthesized pointer press with no modifiers, so requiring Ctrl would put
    /// multi-selection out of reach of touch entirely.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Click_Adds_And_Removes_One_Item()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.DispatchInput(PointerDown(5, RowY(0)));
        listView.DispatchInput(PointerDown(5, RowY(2)));
        Assert.Equal(["a", "c"], listView.SelectedItemIds);

        listView.DispatchInput(PointerDown(5, RowY(0)));
        Assert.Equal(["c"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void Shift_Click_Selects_The_Range_From_The_Anchor()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.DispatchInput(PointerDown(5, RowY(1)));
        listView.DispatchInput(PointerDown(5, RowY(3), KeyboardModifierState.Shift));

        Assert.Equal(["b", "c", "d"], listView.SelectedItemIds);
        // The anchor does not move, so shifting again resizes one range.
        Assert.Equal("b", listView.SelectedItemId);

        listView.DispatchInput(PointerDown(5, RowY(2), KeyboardModifierState.Shift));
        Assert.Equal(["b", "c"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void Shift_Click_Ranges_Backwards_Too()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.DispatchInput(PointerDown(5, RowY(3)));
        listView.DispatchInput(PointerDown(5, RowY(1), KeyboardModifierState.Shift));

        Assert.Equal(["b", "c", "d"], listView.SelectedItemIds);
    }

    /// <summary>
    /// Ctrl-click toggles, the same result the desktop convention gives it, so
    /// muscle memory from other platforms still works.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Click_Toggles_Like_An_Unmodified_Click()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.DispatchInput(PointerDown(5, RowY(0), KeyboardModifierState.Control));
        listView.DispatchInput(PointerDown(5, RowY(2), KeyboardModifierState.Control));

        Assert.Equal(["a", "c"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void A_Single_Selection_List_Ignores_Modifiers()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Single);

        listView.DispatchInput(PointerDown(5, RowY(0)));
        listView.DispatchInput(PointerDown(5, RowY(2), KeyboardModifierState.Shift));

        // Shift cannot build a range a single-selection list cannot hold.
        Assert.Equal(["c"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void Shift_Arrow_Extends_And_A_Plain_Arrow_Replaces()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SelectItem("a");

        listView.DispatchInput(KeyDown("Down", KeyboardModifierState.Shift));
        Assert.Equal(["a", "b"], listView.SelectedItemIds);

        listView.DispatchInput(KeyDown("Down", KeyboardModifierState.None));
        Assert.Equal(["b"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void Space_Toggles_Without_Moving_So_A_Gapped_Selection_Is_Reachable_By_Keyboard()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SelectItem("a");

        listView.DispatchInput(KeyDown("Down", KeyboardModifierState.None));
        listView.DispatchInput(KeyDown("Down", KeyboardModifierState.None));
        listView.DispatchInput(KeyDown("Space", KeyboardModifierState.None));

        // a was replaced by the arrows; Space removed c, leaving nothing selected.
        Assert.Empty(listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void SetSelectedItems_Reports_In_Item_Order_Whatever_Order_It_Is_Given()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        Assert.True(listView.SetSelectedItems(["d", "a", "c"]));

        Assert.Equal(["a", "c", "d"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void SetSelectedItems_Ignores_Unknown_Ids_And_Duplicates()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.SetSelectedItems(["a", "nope", "a"]);

        Assert.Equal(["a"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void SetSelectedItems_Keeps_Only_The_First_In_Single_Mode()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Single);

        listView.SetSelectedItems(["b", "c"]);

        Assert.Equal(["b"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void Narrowing_To_Single_Keeps_The_Primary_And_Drops_The_Rest()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SelectItem("b");
        listView.ToggleItem("d");

        listView.SelectionMode = UiListSelectionMode.Single;

        // The toggle made d the anchor, so d is what survives.
        Assert.Equal(["d"], listView.SelectedItemIds);
        Assert.Equal("d", listView.SelectedItemId);
    }

    [Fact(Timeout = 600000)]
    public void Replacing_The_Items_Drops_Selections_That_No_Longer_Exist()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SetSelectedItems(["a", "c"]);

        listView.SetItems([new UiListItem("c", "C"), new UiListItem("e", "E")]);

        Assert.Equal(["c"], listView.SelectedItemIds);
    }

    [Fact(Timeout = 600000)]
    public void SelectionChanged_Reports_The_Whole_Set_On_Every_Change()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SelectItem("a");

        UiListSelectionChangedEventArgs? observed = null;
        listView.SelectionChanged += (_, e) => observed = e;

        listView.ToggleItem("c");

        Assert.NotNull(observed);
        Assert.Equal(["a"], observed.OldItemIds);
        Assert.Equal(["a", "c"], observed.NewItemIds);
    }

    [Fact(Timeout = 600000)]
    public void A_Single_Selection_Change_Still_Reports_A_One_Item_Set()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Single);
        UiListSelectionChangedEventArgs? observed = null;
        listView.SelectionChanged += (_, e) => observed = e;

        listView.SelectItem("b");

        Assert.NotNull(observed);
        Assert.Null(observed.OldItemId);
        Assert.Equal("b", observed.NewItemId);
        Assert.Equal(["b"], observed.NewItemIds);
    }

    [Fact(Timeout = 600000)]
    public void Every_Selected_Row_Is_Highlighted_Not_Just_The_Primary()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SetSelectedItems(["a", "c"]);

        Assert.True(listView.IsSelected("a"));
        Assert.True(listView.IsSelected("c"));
        Assert.False(listView.IsSelected("b"));
    }

    /// <summary>A pointer press, optionally with modifier keys held.</summary>
    private static UiInputEvent PointerDown(double x, double y, KeyboardModifierState modifiers = KeyboardModifierState.None) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", 1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic,
                (InputModifiers)modifiers));

    private static UiInputEvent KeyDown(string name, KeyboardModifierState modifiers) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 2),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                modifiers,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static InputEventHeader Header(string id, long sequence) =>
        new(
            InputDeviceId.FromOpaqueValue(id),
            new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "listview-multiselect-test"),
            sequence);
}

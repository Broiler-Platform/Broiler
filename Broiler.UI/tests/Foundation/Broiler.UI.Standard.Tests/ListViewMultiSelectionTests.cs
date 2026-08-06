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

    [Fact]
    public void Single_Is_The_Default_Mode()
    {
        Assert.Equal(UiListSelectionMode.Single, new StandardListView().SelectionMode);
    }

    [Fact]
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
    /// A plain click toggles rather than replacing, because pointer events carry no
    /// modifier state — see <c>StandardListView.ApplyClick</c>. Without this a
    /// multi-selection list would be unusable by mouse or touch.
    /// </summary>
    [Fact]
    public void A_Click_Adds_And_Removes_One_Item()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.DispatchInput(PointerDown(5, RowY(0)));
        listView.DispatchInput(PointerDown(5, RowY(2)));
        Assert.Equal(["a", "c"], listView.SelectedItemIds);

        listView.DispatchInput(PointerDown(5, RowY(0)));
        Assert.Equal(["c"], listView.SelectedItemIds);
    }

    /// <summary>
    /// Ranges are keyboard-only for now — a pointer press carries no modifier state,
    /// so Shift-click cannot be distinguished from a plain click.
    /// </summary>
    [Fact]
    public void Shift_Arrow_Extends_And_A_Plain_Arrow_Replaces()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SelectItem("a");

        listView.DispatchInput(KeyDown("Down", KeyboardModifierState.Shift));
        Assert.Equal(["a", "b"], listView.SelectedItemIds);

        listView.DispatchInput(KeyDown("Down", KeyboardModifierState.None));
        Assert.Equal(["b"], listView.SelectedItemIds);
    }

    [Fact]
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

    [Fact]
    public void SetSelectedItems_Reports_In_Item_Order_Whatever_Order_It_Is_Given()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        Assert.True(listView.SetSelectedItems(["d", "a", "c"]));

        Assert.Equal(["a", "c", "d"], listView.SelectedItemIds);
    }

    [Fact]
    public void SetSelectedItems_Ignores_Unknown_Ids_And_Duplicates()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);

        listView.SetSelectedItems(["a", "nope", "a"]);

        Assert.Equal(["a"], listView.SelectedItemIds);
    }

    [Fact]
    public void SetSelectedItems_Keeps_Only_The_First_In_Single_Mode()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Single);

        listView.SetSelectedItems(["b", "c"]);

        Assert.Equal(["b"], listView.SelectedItemIds);
    }

    [Fact]
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

    [Fact]
    public void Replacing_The_Items_Drops_Selections_That_No_Longer_Exist()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SetSelectedItems(["a", "c"]);

        listView.SetItems([new UiListItem("c", "C"), new UiListItem("e", "E")]);

        Assert.Equal(["c"], listView.SelectedItemIds);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Every_Selected_Row_Is_Highlighted_Not_Just_The_Primary()
    {
        StandardListView listView = CreateList(UiListSelectionMode.Multiple);
        listView.SetSelectedItems(["a", "c"]);

        Assert.True(listView.IsSelected("a"));
        Assert.True(listView.IsSelected("c"));
        Assert.False(listView.IsSelected("b"));
    }

    /// <summary>
    /// A pointer press. There is no modifier parameter because there is nowhere to
    /// put one: <c>MouseButtonEvent</c> has no modifier field and
    /// <c>UiInputEvent.FromMouseButton</c> reports <c>None</c> — which is exactly why
    /// a click has to toggle rather than wait for Ctrl.
    /// </summary>
    private static UiInputEvent PointerDown(double x, double y) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", 1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

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

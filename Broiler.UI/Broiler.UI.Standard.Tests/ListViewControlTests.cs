using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class ListViewControlTests
{
    [Fact]
    public void Standard_ListView_Allows_Selection_Handlers_To_Replace_Items()
    {
        var listView = new StandardListView
        {
            ItemHeight = 20,
        };
        listView.Arrange(new BRect(0, 0, 120, 80));
        listView.SelectionChanged += (_, _) => listView.SetItems([]);

        SetSampleItems(listView);
        Exception? pointerException = Record.Exception(() => listView.DispatchInput(PointerDown(5, 5)));

        SetSampleItems(listView);
        Exception? keyboardException = Record.Exception(() => listView.DispatchInput(KeyDown("Down")));

        Assert.Null(pointerException);
        Assert.Null(keyboardException);
    }

    private static void SetSampleItems(StandardListView listView) =>
        listView.SetItems(
        [
            new UiListItem("one", "One"),
            new UiListItem("two", "Two"),
        ]);

    private static UiInputEvent PointerDown(double x, double y) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", 1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private static UiInputEvent KeyDown(string name) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 2),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static InputEventHeader Header(string id, long sequence) =>
        new(
            InputDeviceId.FromOpaqueValue(id),
            new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "listview-test"),
            sequence);
}

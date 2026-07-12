using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Standard;

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

    [Fact]
    public void Standard_ListView_Reserves_Content_Bounds_And_Renders_Vertical_Scrollbar()
    {
        var listView = CreateOverflowingListView();

        using UiSession session = AttachAndRender(listView, new BSize(120, 80), out BRenderList renderList);

        Assert.True(listView.HasVerticalScrollbar);
        Assert.Equal(new BRect(0, 0, 108, 80), listView.ContentBounds);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRoundedRect>(), command => command.Rect == new BRect(108, 0, 12, 80) && command.Color == listView.ScrollbarTrack);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRoundedRect>(), command => command.Color == listView.ScrollbarThumb);
    }

    [Fact]
    public void Standard_ListView_Clicking_Scrollbar_Track_Pages_Without_Selecting_Row()
    {
        var listView = CreateOverflowingListView();
        using UiSession session = AttachAndRender(listView, new BSize(120, 80), out _);

        Assert.True(session.DispatchInput(PointerDown(114, 70)));

        Assert.Equal(68, listView.VerticalOffset);
        Assert.Null(listView.SelectedItemId);
    }

    [Fact]
    public void Standard_ListView_Dragging_Scrollbar_Thumb_Updates_Offset_And_Captures_Input()
    {
        var listView = CreateOverflowingListView();
        using UiSession session = AttachAndRender(listView, new BSize(120, 80), out _);

        Assert.True(session.DispatchInput(PointerDown(114, 5)));
        Assert.Same(listView, session.CapturedElement);

        Assert.True(session.DispatchInput(PointerMove(114, 45, 2)));

        Assert.InRange(listView.VerticalOffset, 119.999, 120.001);
        Assert.True(session.DispatchInput(PointerUp(114, 45, 3)));
        Assert.Null(session.CapturedElement);
    }

    private static void SetSampleItems(StandardListView listView) =>
        listView.SetItems(
        [
            new UiListItem("one", "One"),
            new UiListItem("two", "Two"),
        ]);

    private static StandardListView CreateOverflowingListView()
    {
        var listView = new StandardListView
        {
            ItemHeight = 20,
            ScrollbarThickness = 12,
        };

        listView.SetItems(Enumerable.Range(0, 12).Select(index => new UiListItem(index.ToString(), "Item " + index.ToString())));
        return listView;
    }

    private static UiSession AttachAndRender(StandardListView listView, BSize viewportSize, out BRenderList renderList)
    {
        var host = new TestHost(viewportSize);
        UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(listView);
        renderList = StandardRenderTraversal.Render(session);
        return session;
    }

    private static UiInputEvent PointerDown(double x, double y) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", 1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private static UiInputEvent PointerUp(double x, double y, long sequence) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", sequence),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.None,
                MouseButton.Left,
                MouseButtonTransition.Up,
                InputEventSource.Synthetic));

    private static UiInputEvent PointerMove(double x, double y, long sequence) =>
        UiInputEvent.FromMouseMove(
            new MouseMoveEvent(
                Header("mouse", sequence),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
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

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1.0;

        public List<UiInvalidation> Invalidations { get; } = [];

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);

        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }
}

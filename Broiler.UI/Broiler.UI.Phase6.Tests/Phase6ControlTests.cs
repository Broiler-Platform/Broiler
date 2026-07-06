using System.Diagnostics;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView;
using Broiler.UI.TabView.Standard;
using Broiler.UI.Tooltip.Standard;

namespace Broiler.UI.Phase6.Tests;

public sealed class Phase6ControlTests
{
    [Fact]
    public void ScrollView_Clamps_Wheel_Keyboard_And_Clips_Content()
    {
        var host = new TestHost(new BSize(120, 80));
        using UiSession session = CreateSession(host);
        var scroll = new StandardScrollView { PreferredSize = new BSize(120, 80), LineScrollAmount = 20 };
        scroll.AddChild(new FixedElement(new BSize(320, 240), BColor.FromArgb(0xFF, 0x22, 0x66, 0xAA)));
        session.AddRoot(scroll);
        BRenderList first = session.RenderFrame();

        Assert.Equal(new BSize(320, 240), scroll.ExtentSize);
        Assert.Contains(first.Commands, command => command is BRenderCommand.PushClip);

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(Wheel(10, 10, -1, MouseWheelAxis.Vertical)));
        Assert.Equal(20, scroll.VerticalOffset);

        Assert.True(route.Dispatch(Key("End", BVirtualKey.End, KeyboardKeyTransition.Down)));
        Assert.Equal(160, scroll.VerticalOffset);
        Assert.Equal(200, scroll.HorizontalOffset);

        Assert.True(route.Dispatch(Wheel(10, 10, 1, MouseWheelAxis.Horizontal)));
        Assert.Equal(180, scroll.HorizontalOffset);
    }

    [Fact]
    public void ListView_Virtualizes_OneHundredThousand_Items_Selection_And_Semantics()
    {
        var host = new TestHost(new BSize(240, 120));
        using UiSession session = CreateSession(host);
        var list = new StandardListView { ItemHeight = 20, PreferredSize = new BSize(240, 120) };
        list.SetItems(Enumerable.Range(0, 100_000).Select(index => new UiListItem("item-" + index, "Item " + index)));
        session.AddRoot(list);

        Stopwatch stopwatch = Stopwatch.StartNew();
        BRenderList rendered = session.RenderFrame();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 2000);
        Assert.True(list.VisibleItemCount <= 7);
        Assert.True(rendered.Commands.OfType<BRenderCommand.DrawText>().Count(command => command.Text.Text.StartsWith("Item ", StringComparison.Ordinal)) <= 7);
        Assert.True(list.GetSemanticNode().Children.Count <= 7);

        list.SelectItem("item-99999");
        list.ScrollIntoView("item-99999");
        session.RenderFrame();

        Assert.Equal("item-99999", list.SelectedItemId);
        Assert.True(list.FirstVisibleIndex > 99_990);
        Assert.Contains(list.GetSemanticNode().Children, node => node.Name == "Item 99999" && node.State.HasFlag(UiSemanticState.Selected));
    }

    [Fact]
    public void ComboBox_Commits_Cancels_LightDismisses_Restores_Focus_And_Relayouts_Popup()
    {
        var host = new TestHost(new BSize(220, 88));
        using UiSession session = CreateSession(host);
        var focusAnchor = new FixedElement(new BSize(20, 20), BColor.Black);
        var combo = new StandardComboBox { PreferredSize = new BSize(180, 32), ItemHeight = 24, MaxDropDownItems = 3 };
        combo.SetItems(
        [
            new UiComboBoxItem("one", "One"),
            new UiComboBoxItem("two", "Two"),
            new UiComboBoxItem("three", "Three"),
        ]);
        session.AddRoot(focusAnchor);
        session.AddRoot(combo);
        session.RenderFrame();

        var route = new StandardInputRoute(session);
        session.SetFocus(combo);
        Assert.True(route.Dispatch(Key("Down", BVirtualKey.Down, KeyboardKeyTransition.Down)));
        Assert.True(combo.IsDropDownOpen);
        Assert.Same(combo, session.CapturedElement);

        Assert.True(route.Dispatch(Key("Down", BVirtualKey.Down, KeyboardKeyTransition.Down)));
        Assert.True(route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down)));
        Assert.Equal("two", combo.SelectedItem?.Id);
        Assert.False(combo.IsDropDownOpen);
        Assert.Null(session.CapturedElement);

        session.SetFocus(focusAnchor);
        combo.OpenDropDown();
        session.CaptureInput(combo);
        host.ViewportSize = new BSize(220, 64);
        session.RenderFrame();
        Assert.True(combo.PopupBounds.Bottom <= host.ViewportSize.Height);
        Assert.True(route.Dispatch(MouseButtonInput(210, 8, MouseButtonTransition.Down)));
        Assert.False(combo.IsDropDownOpen);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void TabView_Selects_By_Pointer_And_Keyboard_And_Keeps_Inactive_Lifetime_Explicit()
    {
        var host = new TestHost(new BSize(320, 180));
        using UiSession session = CreateSession(host);
        var first = new FixedElement(new BSize(100, 80), BColor.FromArgb(0xFF, 0xAA, 0xCC, 0xFF));
        var second = new FixedElement(new BSize(100, 80), BColor.FromArgb(0xFF, 0xCC, 0xAA, 0xFF));
        var tabs = new StandardTabView { InactiveContentPolicy = UiTabContentLifetimePolicy.CollapseInactive };
        tabs.AddTab("first", "First", first);
        tabs.AddTab("second", "Second", second);
        session.AddRoot(tabs);
        session.RenderFrame();

        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(UiVisibility.Collapsed, second.Visibility);

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down)));
        session.RenderFrame();
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal(UiVisibility.Collapsed, first.Visibility);
        Assert.Equal(UiVisibility.Visible, second.Visibility);

        Assert.True(route.Dispatch(MouseButtonInput(10, 10, MouseButtonTransition.Down)));
        session.RenderFrame();
        Assert.Equal(0, tabs.SelectedIndex);
    }

    [Fact]
    public void Menu_Navigates_Nested_Items_Invokes_Commands_And_LightDismisses()
    {
        var host = new TestHost(new BSize(480, 240));
        using UiSession session = CreateSession(host);
        int invoked = 0;
        int commandCount = 0;
        var dispatcher = new StandardCommandDispatcher();
        dispatcher.Add(new StandardCommand("new", () => commandCount++));
        var file = new UiMenuItem("file", "File") { AccessKey = 'F' };
        file.Children.Add(new UiMenuItem("new", "New") { CommandName = "new", AccessKey = 'N' });
        var export = new UiMenuItem("export", "Export");
        export.Children.Add(new UiMenuItem("pdf", "PDF"));
        file.Children.Add(export);
        var menu = new StandardMenu { CommandDispatcher = dispatcher, MaxDepth = 2 };
        menu.SetItems([file, new UiMenuItem("edit", "Edit")]);
        menu.ItemInvoked += (_, _) => invoked++;
        BColor blockerColor = BColor.FromArgb(0xFF, 0xEE, 0x11, 0x22);
        session.AddRoot(menu);
        session.AddRoot(new FixedElement(new BSize(480, 240), blockerColor));
        session.RenderFrame();
        session.SetFocus(menu);
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(Key("Down", BVirtualKey.Down, KeyboardKeyTransition.Down)));
        Assert.True(menu.IsOpen);
        Assert.Same(menu, session.CapturedElement);
        BRenderList opened = session.RenderFrame();
        string[] openedText = opened.Commands.OfType<BRenderCommand.DrawText>().Select(command => command.Text.Text).ToArray();
        Assert.Contains("New", openedText);
        Assert.DoesNotContain("File >", openedText);
        int blockerIndex = Enumerable.Range(0, opened.Commands.Count)
            .Last(index => opened.Commands[index] is BRenderCommand.FillRect fill && fill.Color == blockerColor);
        int popupItemIndex = Enumerable.Range(0, opened.Commands.Count)
            .Last(index => opened.Commands[index] is BRenderCommand.DrawText text && text.Text.Text == "New");
        Assert.True(popupItemIndex > blockerIndex);
        Assert.True(route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down)));
        Assert.Equal([0, 0], menu.SelectedPath);
        Assert.True(route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Down)));
        Assert.Equal(1, invoked);
        Assert.Equal(1, commandCount);
        Assert.False(menu.IsOpen);

        menu.Open();
        menu.SetSelectedPath([0, 1, 0]);
        Assert.True(menu.SelectedPath.Count <= 2);
        session.CaptureInput(menu);
        Assert.True(route.Dispatch(MouseButtonInput(470, 230, MouseButtonTransition.Down)));
        Assert.False(menu.IsOpen);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Tooltip_Uses_SessionClock_Delays_TimesOut_And_Stays_In_Viewport()
    {
        var host = new TestHost(new BSize(160, 80));
        var clock = new TestClock();
        using UiSession session = CreateSession(host, clock);
        var tooltip = new StandardTooltip
        {
            Text = "More details",
            InitialDelay = TimeSpan.FromMilliseconds(100),
            DismissAfter = TimeSpan.FromMilliseconds(200),
        };
        session.AddRoot(tooltip);
        tooltip.Start(new BRect(120, 64, 32, 12));

        Assert.False(tooltip.IsTooltipOpen);
        Assert.DoesNotContain(session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "More details");

        clock.Advance(TimeSpan.FromMilliseconds(120));
        BRenderList opened = session.RenderFrame();
        Assert.True(tooltip.IsTooltipOpen);
        Assert.True(tooltip.TooltipBounds.Right <= host.ViewportSize.Width);
        Assert.True(tooltip.TooltipBounds.Bottom <= host.ViewportSize.Height);
        Assert.Contains(opened.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "More details");

        clock.Advance(TimeSpan.FromMilliseconds(250));
        session.RenderFrame();
        Assert.False(tooltip.IsTooltipOpen);
        Assert.Equal(UiSemanticRole.Tooltip, tooltip.GetSemanticNode().Role);
    }

    private static UiSession CreateSession(TestHost host, IUiClock? clock = null)
    {
        var builder = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher());
        if (clock is not null)
            builder.WithClock(clock);
        return builder.Build(host);
    }

    private static MouseButtonEvent MouseButtonInput(double x, double y, MouseButtonTransition transition)
    {
        MouseButtons buttons = transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None;
        return new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), buttons, MouseButton.Left, transition, InputEventSource.Synthetic);
    }

    private static MouseWheelEvent Wheel(double x, double y, double delta, MouseWheelAxis axis) =>
        new(Header("wheel"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None, axis, delta, InputEventSource.Synthetic);

    private static KeyboardKeyEvent Key(string name, int nativeKeyCode, KeyboardKeyTransition transition) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), transition, KeyboardModifierState.None, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "phase6"), 1);

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; set; }

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }

    private sealed class TestClock : IUiClock
    {
        public UiTimestamp Now { get; private set; }

        public void Advance(TimeSpan elapsed) => Now = new UiTimestamp(Now.Elapsed + elapsed);
    }

    private sealed class FixedElement : UiElement
    {
        private readonly BSize _size;
        private readonly BColor _color;

        public FixedElement(BSize size, BColor color)
        {
            _size = size;
            _color = color;
        }

        protected override BSize MeasureCore(BSize availableSize) => _size;

        protected override void RenderCore(UiRenderContext context) => context.RenderList.FillRect(Bounds, _color);
    }
}

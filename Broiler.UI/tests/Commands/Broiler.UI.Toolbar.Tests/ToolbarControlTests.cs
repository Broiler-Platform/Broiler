using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

public sealed class ToolbarControlTests
{
    [Fact]
    public void Toolbar_Arranges_Children_Separators_And_Semantics()
    {
        var host = new TestHost(new BSize(360, 64));
        using UiSession session = CreateSession(host);
        var toolbar = new StandardToolbar { Title = "Navigation", Padding = 5, Spacing = 7, PreferredSize = new BSize(300, 44) };
        var back = new StandardButton { Text = "Back", PreferredSize = new BSize(54, 30) };
        var forward = new StandardButton { Text = "Forward", PreferredSize = new BSize(76, 30) };
        var refresh = new StandardButton { Text = "Refresh", PreferredSize = new BSize(82, 30) };
        toolbar.AddChild(back);
        toolbar.AddChild(forward);
        toolbar.AddChild(refresh);
        toolbar.SetSeparatorBefore(refresh, true);
        session.AddRoot(toolbar);

        BRenderList rendered = session.RenderFrame();

        Assert.Equal(new BRect(0, 0, 360, 64), toolbar.Bounds);
        Assert.True(back.Bounds.Left >= toolbar.Bounds.Left + toolbar.Padding);
        Assert.True(forward.Bounds.Left > back.Bounds.Right);
        Assert.True(refresh.Bounds.Left > forward.Bounds.Right);
        Assert.True(toolbar.GetSeparatorBefore(refresh));
        Assert.Contains(rendered.Commands.OfType<BRenderCommand.FillRect>(), command => command.Color == toolbar.SeparatorColor);

        UiSemanticNode semantic = toolbar.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Toolbar, semantic.Role);
        Assert.Equal("Navigation", semantic.Name);
        Assert.True(semantic.State.HasFlag(UiSemanticState.Enabled));
        Assert.Equal([UiSemanticRole.Button, UiSemanticRole.Button, UiSemanticRole.Button], semantic.Children.Select(static child => child.Role));
    }

    [Fact]
    public void Toolbar_Keyboard_Navigation_Moves_Focus_Across_Visible_Children()
    {
        var host = new TestHost(new BSize(360, 64));
        using UiSession session = CreateSession(host);
        var toolbar = new StandardToolbar { Title = "Commands" };
        var first = new StandardButton { Text = "One", PreferredSize = new BSize(48, 30) };
        var second = new StandardButton { Text = "Two", PreferredSize = new BSize(48, 30) };
        var third = new StandardButton { Text = "Three", PreferredSize = new BSize(64, 30) };
        toolbar.AddChild(first);
        toolbar.AddChild(second);
        toolbar.AddChild(third);
        session.AddRoot(toolbar);
        session.RenderFrame();
        session.SetFocus(toolbar);
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(Key("Right", BVirtualKey.Right)));
        Assert.Same(first, session.FocusedElement);
        Assert.True(route.Dispatch(Key("Right", BVirtualKey.Right)));
        Assert.Same(second, session.FocusedElement);
        Assert.True(route.Dispatch(Key("Left", BVirtualKey.Left)));
        Assert.Same(first, session.FocusedElement);
        Assert.True(route.Dispatch(Key("End", BVirtualKey.End)));
        Assert.Same(third, session.FocusedElement);
        Assert.True(route.Dispatch(Key("Home", BVirtualKey.Home)));
        Assert.Same(first, session.FocusedElement);
    }

    [Fact]
    public void Toolbar_Vertical_Orientation_Stacks_Items_And_Uses_Vertical_Keys()
    {
        var host = new TestHost(new BSize(96, 180));
        using UiSession session = CreateSession(host);
        var toolbar = new StandardToolbar { Orientation = UiToolbarOrientation.Vertical, PreferredSize = new BSize(84, 160) };
        var first = new StandardButton { Text = "A", PreferredSize = new BSize(54, 30) };
        var second = new StandardButton { Text = "B", PreferredSize = new BSize(54, 30) };
        toolbar.AddChild(first);
        toolbar.AddChild(second);
        session.AddRoot(toolbar);
        session.RenderFrame();
        session.SetFocus(toolbar);

        Assert.True(second.Bounds.Top > first.Bounds.Bottom);

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(Key("Down", BVirtualKey.Down)));
        Assert.Same(first, session.FocusedElement);
        Assert.True(route.Dispatch(Key("Down", BVirtualKey.Down)));
        Assert.Same(second, session.FocusedElement);
        Assert.True(route.Dispatch(Key("Up", BVirtualKey.Up)));
        Assert.Same(first, session.FocusedElement);
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private static KeyboardKeyEvent Key(string name, int nativeKeyCode) =>
        new(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("keyboard"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "toolbar"), 1),
            KeyboardKey.FromName(name),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.None,
            nativeKeyCode,
            0,
            0,
            false,
            false,
            Source: InputEventSource.Synthetic);

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}

using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;
using Broiler.UI.Splitter.Standard;

namespace Broiler.UI.Splitter.Tests;

public sealed class StandardSplitterTests
{
    [Fact]
    public void Value_Is_Clamped_Evented_And_Semantic()
    {
        var splitter = new StandardSplitter();
        UiSplitterValueChangedEventArgs? raised = null;
        splitter.ValueChanged += (_, args) => raised = args;

        splitter.Value = 2;

        Assert.Equal(0.9, splitter.Value);
        Assert.Equal(0.5, raised?.OldValue);
        Assert.Equal(0.9, raised?.NewValue);
        UiSemanticNode node = splitter.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Splitter, node.Role);
        Assert.Contains("90", node.Name);
    }

    [Fact]
    public void Keyboard_And_Pointer_Resize_Without_Host_Specific_Logic()
    {
        using SplitterScene scene = Create();
        scene.Splitter.Orientation = UiSplitterOrientation.Horizontal;
        scene.Splitter.DragExtent = 100;
        scene.Session.SetFocus(scene.Splitter);

        scene.Route.Dispatch(Key("Down", BVirtualKey.Down));
        Assert.Equal(0.52, scene.Splitter.Value, 3);

        scene.Route.Dispatch(MouseDown(20, 5));
        scene.Route.Dispatch(MouseMove(20, 25));
        scene.Route.Dispatch(MouseUp(20, 25));
        Assert.Equal(0.72, scene.Splitter.Value, 3);
    }

    [Fact]
    public void Theme_Render_And_Factory_Are_Deterministic()
    {
        using SplitterScene scene = Create();
        scene.Splitter.ApplyTheme(StandardThemeTokens.HighContrastDark);
        scene.Session.SetFocus(scene.Splitter);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), command => command.Color == StandardThemeTokens.HighContrastDark.SurfaceAlt);
        Assert.Contains(list.Commands.OfType<BRenderCommand.StrokeRect>(), command => command.Color == StandardThemeTokens.HighContrastDark.FocusRing);
        var factory = new StandardSplitterFactory();
        Assert.Equal(typeof(UiSplitter), factory.ContractType);
        Assert.IsType<StandardSplitter>(factory.Create(default));
    }

    private static SplitterScene Create()
    {
        var host = new Host();
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var splitter = new StandardSplitter { PreferredSize = new BSize(200, 10) };
        session.AddRoot(splitter);
        session.RenderFrame();
        return new SplitterScene(session, splitter, new StandardInputRoute(session));
    }

    private static InputEventHeader Header() =>
        new(InputDeviceId.FromOpaqueValue("splitter"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "splitter"), 1);

    private static KeyboardKeyEvent Key(string name, int native) =>
        new(Header(), KeyboardKey.FromName(name), KeyboardKeyTransition.Down,
            KeyboardModifierState.None, native, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static MouseButtonEvent MouseDown(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left,
            MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic);

    private static MouseMoveEvent MouseMove(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left, InputEventSource.Synthetic);

    private static MouseButtonEvent MouseUp(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None,
            MouseButton.Left, MouseButtonTransition.Up, InputEventSource.Synthetic);

    private sealed record SplitterScene(
        UiSession Session,
        StandardSplitter Splitter,
        StandardInputRoute Route) : IDisposable
    {
        public void Dispose() => Session.Dispose();
    }

    private sealed class Host : IUiHost
    {
        public BSize ViewportSize => new(200, 10);

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

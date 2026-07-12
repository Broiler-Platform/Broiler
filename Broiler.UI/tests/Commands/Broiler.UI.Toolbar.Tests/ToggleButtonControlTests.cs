using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;

namespace Broiler.UI.Toolbar.Tests;

public sealed class ToggleButtonControlTests
{
    [Fact]
    public void Pointer_Click_Leaves_Button_Checked_Until_The_Next_Click()
    {
        var host = new TestHost(new BSize(120, 50));
        using UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var button = new StandardToggleButton
        {
            Text = "Bold",
            PreferredSize = new BSize(80, 32),
            Background = BColor.FromArgb(0xFF, 0x10, 0x10, 0x10),
            HoverBackground = BColor.FromArgb(0xFF, 0x20, 0x20, 0x20),
            CheckedBackground = BColor.FromArgb(0xFF, 0x30, 0x30, 0x30),
        };
        session.AddRoot(button);
        session.RenderFrame();
        BPoint center = new(button.Bounds.Left + button.Bounds.Width / 2, button.Bounds.Top + button.Bounds.Height / 2);

        Click(session, center);

        Assert.Equal(UiToggleState.On, button.ToggleState);
        Assert.Contains(session.RenderFrame().Commands.OfType<BRenderCommand.FillRoundedRect>(),
            command => command.Color == button.CheckedBackground);

        Click(session, center);

        Assert.Equal(UiToggleState.Off, button.ToggleState);
        Assert.Contains(session.RenderFrame().Commands.OfType<BRenderCommand.FillRoundedRect>(),
            command => command.Color == button.HoverBackground);
    }

    private static void Click(UiSession session, BPoint position)
    {
        var route = new StandardInputRoute(session);
        route.Dispatch(Pointer(position, MouseButtonTransition.Down));
        route.Dispatch(Pointer(position, MouseButtonTransition.Up));
    }

    private static MouseButtonEvent Pointer(BPoint position, MouseButtonTransition transition) =>
        new(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("mouse"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "toggle"), 1),
            InputPoint.ClientDeviceIndependentPixels(position.X, position.Y),
            transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None,
            MouseButton.Left,
            transition,
            InputEventSource.Synthetic);

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

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

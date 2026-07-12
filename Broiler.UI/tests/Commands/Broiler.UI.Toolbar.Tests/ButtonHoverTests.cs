using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

public sealed class ButtonHoverTests
{
    [Fact]
    public void Button_Hover_Background_Clears_After_Pointer_Leaves()
    {
        var host = new TestHost(new BSize(220, 60));
        using UiSession session = CreateSession(host);
        var toolbar = new StandardToolbar { Padding = 6, Spacing = 8 };
        var button = new StandardButton
        {
            Text = "Hover",
            PreferredSize = new BSize(72, 30),
            Background = BColor.FromArgb(0xFF, 0x11, 0x22, 0x33),
            SecondaryHoverBackground = BColor.FromArgb(0xFF, 0x44, 0x55, 0x66),
        };
        toolbar.AddChild(button);
        session.AddRoot(toolbar);
        session.RenderFrame();

        var route = new StandardInputRoute(session);
        route.Dispatch(MouseMove(button.Bounds.Left + 1, button.Bounds.Top + 1, 1));
        BRenderList hovered = session.RenderFrame();

        Assert.Equal(button.SecondaryHoverBackground, ButtonFillColor(hovered, button));

        route.Dispatch(MouseMove(button.Bounds.Right + 16, button.Bounds.Top + 1, 2));
        BRenderList unhovered = session.RenderFrame();

        Assert.Equal(button.Background, ButtonFillColor(unhovered, button));
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private static MouseMoveEvent MouseMove(double x, double y, long sequence) =>
        new(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("mouse:hover"),
                new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "button-hover"),
                sequence),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.None,
            InputEventSource.Synthetic);

    private static BColor ButtonFillColor(BRenderList renderList, StandardButton button) =>
        renderList.Commands
            .OfType<BRenderCommand.FillRoundedRect>()
            .Single(command => command.Rect == button.Bounds)
            .Color;

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

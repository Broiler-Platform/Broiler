using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

internal sealed class FormatCodeViewTestHost : IUiHost, IUiClipboardHost
{
    public FormatCodeViewTestHost(BSize size) => ViewportSize = size;

    public BSize ViewportSize { get; }

    public double Scale => 1;

    public string ClipboardText { get; private set; } = string.Empty;

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation)
    {
    }

    public void Present(BRenderList renderList)
    {
    }

    public bool TryGetText(out string text)
    {
        text = ClipboardText;
        return true;
    }

    public void SetText(string text) => ClipboardText = text;
}

internal sealed class FormatCodeViewScene : IDisposable
{
    public required UiSession Session { get; init; }

    public required StandardFormatCodeView View { get; init; }

    public required FormatCodeViewTestHost Host { get; init; }

    public required StandardInputRoute Route { get; init; }

    public void Dispose() => Session.Dispose();
}

internal static class FormatCodeViewStandardHarness
{
    public static FormatCodeViewScene Create(BSize size, FormatCodeProjection projection)
    {
        var host = new FormatCodeViewTestHost(size);
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var view = new StandardFormatCodeView
        {
            PreferredSize = size,
            Projection = projection,
        };
        session.AddRoot(view);
        return new FormatCodeViewScene
        {
            Session = session,
            View = view,
            Host = host,
            Route = new StandardInputRoute(session),
        };
    }

    public static FormatCodeProjection Project(string text) =>
        new FormatCodeProjector().Project(RichTextDocument.FromPlainText(text));

    public static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "format-codes"), 1);

    public static MouseButtonEvent MouseDown(double x, double y) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left,
            MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic);

    public static MouseButtonEvent MouseUp(double x, double y) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None,
            MouseButton.Left, MouseButtonTransition.Up, InputEventSource.Synthetic);

    public static MouseMoveEvent MouseMove(double x, double y) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left,
            InputEventSource.Synthetic);

    public static MouseWheelEvent Wheel(double x, double y, double notches, MouseWheelAxis axis = MouseWheelAxis.Vertical) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None,
            axis, notches, InputEventSource.Synthetic);

    public static KeyboardKeyEvent Key(
        string name,
        int nativeKeyCode,
        KeyboardModifierState modifiers = KeyboardModifierState.None) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), KeyboardKeyTransition.Down,
            modifiers, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);
}

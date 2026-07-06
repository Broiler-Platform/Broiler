using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard.Tests;

internal sealed class TestHost : IUiHost, IUiClipboardHost, IUiTextInputHost
{
    public TestHost(BSize viewportSize) => ViewportSize = viewportSize;

    public BSize ViewportSize { get; }

    public double Scale => 1;

    public string ClipboardText { get; set; } = string.Empty;

    public UiTextCaretInfo? LastCaret { get; private set; }

    public int ClearCaretCount { get; private set; }

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

    public void PublishCaret(UiTextCaretInfo caret) => LastCaret = caret;

    public void ClearCaret(UiElement owner)
    {
        LastCaret = null;
        ClearCaretCount++;
    }
}

internal sealed class ManualClock : IUiClock
{
    public UiTimestamp Now { get; set; }

    public void Advance(TimeSpan delta) => Now = new UiTimestamp(Now.Elapsed + delta);
}

internal sealed class RichEditScene
{
    public required UiSession Session { get; init; }

    public required StandardRichEdit Edit { get; init; }

    public required TestHost Host { get; init; }

    public required ManualClock Clock { get; init; }

    public required StandardInputRoute Route { get; init; }
}

internal static class RichEditStandardHarness
{
    public static UiSession CreateSession(TestHost host, ManualClock clock) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(clock)
            .Build(host);

    public static RichEditScene Create(BSize size, string text = "")
    {
        var host = new TestHost(size);
        var clock = new ManualClock();
        UiSession session = CreateSession(host, clock);
        var edit = new StandardRichEdit { PreferredSize = size };
        if (text.Length > 0)
            edit.SetPlainText(text);
        session.AddRoot(edit);
        return new RichEditScene
        {
            Session = session,
            Edit = edit,
            Host = host,
            Clock = clock,
            Route = new StandardInputRoute(session),
        };
    }

    public static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "richedit"), 1);

    public static MouseButtonEvent MouseDown(double x, double y) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left, MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic);

    public static MouseButtonEvent MouseUp(double x, double y) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None, MouseButton.Left, MouseButtonTransition.Up, InputEventSource.Synthetic);

    public static MouseMoveEvent MouseMove(double x, double y) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left, InputEventSource.Synthetic);

    public static MouseWheelEvent Wheel(double x, double y, double notches) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None, MouseWheelAxis.Vertical, notches, InputEventSource.Synthetic);

    public static KeyboardKeyEvent Key(
        string name,
        int nativeKeyCode,
        KeyboardKeyTransition transition = KeyboardKeyTransition.Down,
        KeyboardModifierState modifiers = KeyboardModifierState.None) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), transition, modifiers, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);

    public static TextInputEvent Text(string text) =>
        new(Header("text"), text, InputEventSource.Synthetic);

    public static TextCompositionEvent Composition(string text, TextCompositionState state) =>
        new(Header("text"), text, state, Source: InputEventSource.Synthetic);
}

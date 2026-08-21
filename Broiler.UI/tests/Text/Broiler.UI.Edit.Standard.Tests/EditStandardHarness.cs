using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.Edit.Standard.Tests;

/// <summary>Virtual key codes the edit's shortcuts switch on.</summary>
internal static class TestKey
{
    public const int A = 0x41;
    public const int C = 0x43;
    public const int V = 0x56;
    public const int X = 0x58;
    public const int Z = 0x5A;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int ContextMenu = 0x5D;
    public const int F10 = 0x79;
}

internal sealed class TestHost : IUiHost, IUiTextInputHost
{
    public TestHost(BSize viewportSize) => ViewportSize = viewportSize;

    public BSize ViewportSize { get; }

    public double Scale => 1;

    public UiTextCaretInfo? LastCaret { get; private set; }

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation)
    {
    }

    public void Present(BRenderList renderList)
    {
    }

    public void PublishCaret(UiTextCaretInfo caret) => LastCaret = caret;

    public void ClearCaret(UiElement owner) => LastCaret = null;
}

/// <summary>A host that also owns a clipboard, as every real desktop host does.</summary>
internal sealed class ClipboardTestHost : IUiHost, IUiClipboardHost
{
    public ClipboardTestHost(BSize viewportSize) => ViewportSize = viewportSize;

    public BSize ViewportSize { get; }

    public double Scale => 1;

    /// <summary>Null means the clipboard holds no text at all, not empty text.</summary>
    public string? ClipboardText { get; set; }

    public int SetTextCount { get; private set; }

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation)
    {
    }

    public void Present(BRenderList renderList)
    {
    }

    public bool TryGetText(out string text)
    {
        text = ClipboardText ?? string.Empty;
        return ClipboardText is not null;
    }

    public void SetText(string text)
    {
        ClipboardText = text;
        SetTextCount++;
    }
}

/// <summary>A clock the test drives, so the double-click window is deterministic.</summary>
internal sealed class ManualClock : IUiClock
{
    public UiTimestamp Now { get; set; }

    public void Advance(TimeSpan delta) => Now = new UiTimestamp(Now.Elapsed + delta);
}

internal sealed class EditScene
{
    public required UiSession Session { get; init; }

    public required StandardEdit Edit { get; init; }

    public required ClipboardTestHost Host { get; init; }

    public required ManualClock Clock { get; init; }

    public required StandardInputRoute Route { get; init; }

    public BRenderList Render() => Session.RenderFrame();

    /// <summary>The x coordinate of the caret position for <paramref name="index"/>.</summary>
    public double TextX(int index) =>
        Edit.Bounds.Left + Edit.PaddingX + BTextMeasurer.MeasureAdvance(Edit.Text[..index], Edit.Font);

    public double CenterY() => Edit.Bounds.Top + (Edit.Bounds.Height / 2);
}

internal static class EditStandardHarness
{
    public static EditScene Create(string text = "", BSize? viewportSize = null, string? clipboardText = null)
    {
        BSize size = viewportSize ?? new BSize(400, 300);
        var host = new ClipboardTestHost(size) { ClipboardText = clipboardText };
        var clock = new ManualClock();
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(clock)
            .Build(host);
        var edit = new StandardEdit { Text = text, PreferredSize = new BSize(240, 32) };
        var root = new EditRoot(edit);
        session.AddRoot(root);

        var scene = new EditScene
        {
            Session = session,
            Edit = edit,
            Host = host,
            Clock = clock,
            Route = new StandardInputRoute(session),
        };

        // One frame so bounds, and with them every hit test, are real.
        scene.Render();
        session.SetFocus(edit);
        return scene;
    }

    public static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "edit"), 1);

    public static KeyboardKeyEvent Key(
        string name,
        int nativeKeyCode,
        KeyboardModifierState modifiers = KeyboardModifierState.None,
        KeyboardKeyTransition transition = KeyboardKeyTransition.Down) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), transition, modifiers, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);

    public static TextInputEvent Text(string text) => new(Header("text"), text, InputEventSource.Synthetic);

    public static MouseButtonEvent MouseDown(
        double x,
        double y,
        MouseButton button = MouseButton.Left,
        InputModifiers modifiers = InputModifiers.None) =>
        new(
            Header("mouse"),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            button == MouseButton.Right ? MouseButtons.Right : MouseButtons.Left,
            button,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic,
            modifiers);

    public static MouseButtonEvent MouseUp(double x, double y, MouseButton button = MouseButton.Left) =>
        new(
            Header("mouse"),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.None,
            button,
            MouseButtonTransition.Up,
            InputEventSource.Synthetic);

    public static MouseMoveEvent MouseMove(double x, double y, MouseButtons buttons = MouseButtons.None) =>
        new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), buttons, InputEventSource.Synthetic);

    /// <summary>A move with the left button held, as a drag delivers it.</summary>
    public static MouseMoveEvent MouseDrag(double x, double y) => MouseMove(x, y, MouseButtons.Left);

    /// <summary>Places the edit at a fixed offset so pointer coordinates are unambiguous.</summary>
    private sealed class EditRoot : UiElement
    {
        private readonly UiElement _edit;

        public EditRoot(UiElement edit)
        {
            _edit = edit;
            AddChild(edit);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _edit.Measure(availableSize);
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect) =>
            _edit.Arrange(new BRect(20, 20, 240, 32));
    }
}

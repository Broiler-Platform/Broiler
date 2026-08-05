using System;
using System.Threading;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI;

namespace Broiler.Code.Core.Hosting;

/// <summary>
/// Turns a graphics window's events into explicit Broiler.Input events.
///
/// The Writer heads use <c>StandardLegacyGraphicsInputAdapter</c>, which is
/// marked obsolete and which they suppress the warning for. Code routes
/// explicitly instead, and the difference is not cosmetic: this assigns a real
/// device identity, a monotonic sequence, and a named clock to every event, so
/// a consumer can order events and tell devices apart. Pen and touch work later
/// in the roadmap needs exactly that.
///
/// It lives in Core rather than in each head so the two heads share one
/// translation. A copy in each is what the Phase 2 architecture test forbids.
/// </summary>
public sealed class DesktopInputRouter
{
    private readonly InputDeviceId _pointerDevice;
    private readonly InputDeviceId _keyboardDevice;
    private readonly string _clockName;
    private long _sequence;

    public DesktopInputRouter(string hostName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        _pointerDevice = InputDeviceId.FromOpaqueValue($"{hostName}:pointer");
        _keyboardDevice = InputDeviceId.FromOpaqueValue($"{hostName}:keyboard");
        _clockName = hostName;
    }

    public UiInputEvent FromPointerButton(BPointerEventArgs args, bool pressed) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            Header(_pointerDevice),
            InputPoint.ClientDeviceIndependentPixels(args.Position.X, args.Position.Y),
            MapButtons(args.Buttons),
            MapButton(args.ChangedButton),
            pressed ? MouseButtonTransition.Down : MouseButtonTransition.Up,
            InputEventSource.Raw));

    public UiInputEvent FromPointerMove(BPointerEventArgs args) =>
        UiInputEvent.FromMouseMove(new MouseMoveEvent(
            Header(_pointerDevice),
            InputPoint.ClientDeviceIndependentPixels(args.Position.X, args.Position.Y),
            MapButtons(args.Buttons),
            InputEventSource.Raw));

    public UiInputEvent FromWheel(BMouseWheelEventArgs args) =>
        UiInputEvent.FromMouseWheel(new MouseWheelEvent(
            Header(_pointerDevice),
            InputPoint.ClientDeviceIndependentPixels(args.Position.X, args.Position.Y),
            MapButtons(args.Buttons),
            MouseWheelAxis.Vertical,
            args.Delta,
            InputEventSource.Raw));

    public UiInputEvent FromKey(BKeyEventArgs args, bool pressed) =>
        UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
            Header(_keyboardDevice),
            KeyboardKey.FromName(MapVirtualKey(args.VirtualKey)),
            pressed ? KeyboardKeyTransition.Down : KeyboardKeyTransition.Up,
            MapModifiers(args),
            args.VirtualKey,
            0,
            0,
            false,
            false,
            Source: InputEventSource.Raw));

    public UiInputEvent FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return UiInputEvent.FromTextInput(
            new TextInputEvent(Header(_keyboardDevice), text, InputEventSource.Raw));
    }

    /// <summary>
    /// An IME composition update. A null <paramref name="text"/> means the
    /// composition ended without a commit, which the editor has to treat as a
    /// cancellation rather than as committing an empty string.
    /// </summary>
    public UiInputEvent FromComposition(string? text, bool committed) =>
        UiInputEvent.FromTextComposition(new TextCompositionEvent(
            Header(_keyboardDevice),
            text ?? string.Empty,
            text is null
                ? TextCompositionState.Cancelled
                : committed ? TextCompositionState.Committed : TextCompositionState.Updated,
            Source: InputEventSource.Raw));

    /// <summary>
    /// Maps a platform virtual-key code to the neutral key name the controls
    /// switch on. Explicit and small on purpose: the legacy adapter's mapping is
    /// opaque, and a key that silently arrives under an unexpected name is a
    /// shortcut that does nothing with no way to see why.
    /// </summary>
    public static string MapVirtualKey(int virtualKey) => virtualKey switch
    {
        BVirtualKey.Back => "Backspace",
        BVirtualKey.Tab => "Tab",
        BVirtualKey.Enter => "Enter",
        BVirtualKey.Escape => "Escape",
        BVirtualKey.Space => "Space",
        BVirtualKey.PageUp => "PageUp",
        BVirtualKey.PageDown => "PageDown",
        BVirtualKey.End => "End",
        BVirtualKey.Home => "Home",
        BVirtualKey.Left => "ArrowLeft",
        BVirtualKey.Up => "ArrowUp",
        BVirtualKey.Right => "ArrowRight",
        BVirtualKey.Down => "ArrowDown",
        0x2E => "Delete",
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
        _ => $"VK{virtualKey}",
    };

    /// <summary>
    /// A monotonic header per event. The sequence is what lets a consumer order
    /// two events from one device; without it they are indistinguishable.
    /// </summary>
    private InputEventHeader Header(InputDeviceId device) => new(
        device,
        new InputTimestamp(Environment.TickCount64, 1000, _clockName),
        Interlocked.Increment(ref _sequence));

    private static MouseButton MapButton(BMouseButtons button) => button switch
    {
        BMouseButtons.Right => MouseButton.Right,
        BMouseButtons.Middle => MouseButton.Middle,
        BMouseButtons.Left => MouseButton.Left,
        _ => MouseButton.None,
    };

    private static MouseButtons MapButtons(BMouseButtons buttons)
    {
        MouseButtons mapped = MouseButtons.None;
        if ((buttons & BMouseButtons.Left) != 0)
            mapped |= MouseButtons.Left;
        if ((buttons & BMouseButtons.Right) != 0)
            mapped |= MouseButtons.Right;
        if ((buttons & BMouseButtons.Middle) != 0)
            mapped |= MouseButtons.Middle;
        return mapped;
    }

    private static KeyboardModifierState MapModifiers(BKeyEventArgs args)
    {
        KeyboardModifierState modifiers = KeyboardModifierState.None;
        if (args.Shift)
            modifiers |= KeyboardModifierState.Shift;
        if (args.Control)
            modifiers |= KeyboardModifierState.Control;
        if (args.Alt)
            modifiers |= KeyboardModifierState.Alt;
        return modifiers;
    }
}

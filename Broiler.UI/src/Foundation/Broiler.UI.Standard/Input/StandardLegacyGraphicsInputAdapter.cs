using System;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;

namespace Broiler.UI.Standard;

[Obsolete("Temporary Phase 2 migration adapter for legacy Broiler.Graphics callbacks. Remove after Broiler.Input cutover.")]
public sealed class StandardLegacyGraphicsInputAdapter
{
    private readonly InputDeviceId _deviceId;
    private long _sequence;

    public StandardLegacyGraphicsInputAdapter(string deviceId = "legacy-graphics")
    {
        _deviceId = InputDeviceId.FromOpaqueValue(deviceId);
    }

    public UiInputEvent FromPointerMove(BPointerEventArgs input) =>
        UiInputEvent.FromMouseMove(new MouseMoveEvent(NextHeader(), ToInputPoint(input.Position), ToMouseButtons(input.Buttons), InputEventSource.Synthetic));

    public UiInputEvent FromPointerButton(BPointerEventArgs input) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            NextHeader(),
            ToInputPoint(input.Position),
            ToMouseButtons(input.Buttons),
            ToMouseButton(input.ChangedButton),
            IsButtonHeld(input.Buttons, input.ChangedButton) ? MouseButtonTransition.Down : MouseButtonTransition.Up,
            InputEventSource.Synthetic));

    public UiInputEvent FromMouseWheel(BMouseWheelEventArgs input) =>
        UiInputEvent.FromMouseWheel(new MouseWheelEvent(
            NextHeader(),
            ToInputPoint(input.Position),
            ToMouseButtons(input.Buttons),
            MouseWheelAxis.Vertical,
            input.Delta,
            InputEventSource.Synthetic));

    public UiInputEvent FromKey(BKeyEventArgs input, KeyboardKeyTransition transition = KeyboardKeyTransition.Down) =>
        UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
            NextHeader(),
            KeyboardKey.FromName("VirtualKey:" + input.VirtualKey.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            transition,
            ToModifiers(input),
            input.VirtualKey,
            0,
            0,
            false,
            false,
            KeyboardKeyLocation.Standard,
            InputEventSource.Synthetic));

    public UiInputEvent FromText(BTextInputEventArgs input) =>
        UiInputEvent.FromKeyboardText(new KeyboardTextEvent(NextHeader(), input.Character.ToString(), false, InputEventSource.Synthetic));

    private InputEventHeader NextHeader() =>
        new(_deviceId, new InputTimestamp(++_sequence, TimeSpan.TicksPerSecond, "legacy-graphics"), _sequence);

    private static InputPoint ToInputPoint(BPoint point) =>
        InputPoint.ClientDeviceIndependentPixels(point.X, point.Y);

    private static MouseButtons ToMouseButtons(BMouseButtons buttons)
    {
        MouseButtons result = MouseButtons.None;
        if ((buttons & BMouseButtons.Left) != 0)
            result |= MouseButtons.Left;
        if ((buttons & BMouseButtons.Right) != 0)
            result |= MouseButtons.Right;
        if ((buttons & BMouseButtons.Middle) != 0)
            result |= MouseButtons.Middle;
        return result;
    }

    private static MouseButton ToMouseButton(BMouseButtons button) =>
        button switch
        {
            BMouseButtons.Left => MouseButton.Left,
            BMouseButtons.Right => MouseButton.Right,
            BMouseButtons.Middle => MouseButton.Middle,
            _ => MouseButton.None,
        };

    private static KeyboardModifierState ToModifiers(BKeyEventArgs input)
    {
        KeyboardModifierState modifiers = KeyboardModifierState.None;
        if (input.Control)
            modifiers |= KeyboardModifierState.Control;
        if (input.Shift)
            modifiers |= KeyboardModifierState.Shift;
        if (input.Alt)
            modifiers |= KeyboardModifierState.Alt;
        return modifiers;
    }

    private static bool IsButtonHeld(BMouseButtons buttons, BMouseButtons changedButton) =>
        changedButton != BMouseButtons.None && (buttons & changedButton) != 0;
}

using System;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Pen;
using Broiler.Input.Text;
using Broiler.Input.Touch;

namespace Broiler.UI;

public sealed class UiInputEvent
{
    private UiInputEvent(
        InputEventHeader header,
        UiInputEventKind kind,
        BPoint position,
        string? text,
        string? keyName,
        MouseButton mouseButton,
        MouseButtonTransition? mouseButtonTransition,
        KeyboardKeyTransition? keyTransition,
        KeyboardModifierState keyModifiers,
        int nativeKeyCode,
        TextCompositionState? compositionState,
        MouseWheelAxis wheelAxis,
        double wheelDeltaNotches,
        InputEventSource source)
    {
        Header = header;
        Kind = kind;
        Position = position;
        Text = text;
        KeyName = keyName;
        MouseButton = mouseButton;
        MouseButtonTransition = mouseButtonTransition;
        KeyTransition = keyTransition;
        KeyModifiers = keyModifiers;
        NativeKeyCode = nativeKeyCode;
        CompositionState = compositionState;
        WheelAxis = wheelAxis;
        WheelDeltaNotches = wheelDeltaNotches;
        Source = source;
    }

    public InputEventHeader Header { get; }

    public UiInputEventKind Kind { get; }

    public BPoint Position { get; }

    public string? Text { get; }

    public string? KeyName { get; }

    public MouseButton MouseButton { get; }

    public MouseButtonTransition? MouseButtonTransition { get; }

    public KeyboardKeyTransition? KeyTransition { get; }

    public KeyboardModifierState KeyModifiers { get; }

    public int NativeKeyCode { get; }

    public TextCompositionState? CompositionState { get; }

    public MouseWheelAxis WheelAxis { get; }

    public double WheelDeltaNotches { get; }

    public InputEventSource Source { get; }

    public static UiInputEvent FromMouseMove(MouseMoveEvent input) =>
        new(input.Header, UiInputEventKind.PointerMove, ToPoint(input.Position), null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.Source);

    public static UiInputEvent FromMouseButton(MouseButtonEvent input) =>
        new(input.Header, UiInputEventKind.PointerButton, ToPoint(input.Position), null, null, input.Button, input.Transition, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.Source);

    public static UiInputEvent FromMouseWheel(MouseWheelEvent input) =>
        new(input.Header, UiInputEventKind.PointerWheel, ToPoint(input.Position), null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, input.Axis, input.DeltaNotches, input.Source);

    public static UiInputEvent FromTouchContact(TouchContactEvent input) =>
        new(input.Header, UiInputEventKind.TouchContact, ToPoint(input.Position), null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.Source);

    public static UiInputEvent FromPenContact(PenContactEvent input) =>
        new(input.Header, UiInputEventKind.PenContact, ToPoint(input.Position), null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.Source);

    public static UiInputEvent FromKeyboardKey(KeyboardKeyEvent input) =>
        new(input.Header, UiInputEventKind.KeyboardKey, new BPoint(0, 0), null, input.Key.Name, MouseButton.None, null, input.Transition, input.Modifiers, input.NativeKeyCode, null, MouseWheelAxis.Vertical, 0, input.Source);

    public static UiInputEvent FromKeyboardText(KeyboardTextEvent input)
    {
        ArgumentNullException.ThrowIfNull(input.Text);
        return new(input.Header, UiInputEventKind.TextInput, new BPoint(0, 0), input.Text, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.Source);
    }

    public static UiInputEvent FromTextInput(TextInputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input.Text);
        return new(input.Header, UiInputEventKind.TextInput, new BPoint(0, 0), input.Text, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.Source);
    }

    public static UiInputEvent FromTextComposition(TextCompositionEvent input)
    {
        ArgumentNullException.ThrowIfNull(input.Text);
        return new(input.Header, UiInputEventKind.TextComposition, new BPoint(0, 0), input.Text, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, input.State, MouseWheelAxis.Vertical, 0, input.Source);
    }

    private static BPoint ToPoint(InputPoint point) => new(point.X, point.Y);
}

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
        long contactId,
        TouchContactState? touchContactState,
        PenContactState? penContactState,
        PenButtons penButtons,
        double pressure,
        double tiltX,
        double tiltY,
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
        ContactId = contactId;
        TouchContactState = touchContactState;
        PenContactState = penContactState;
        PenButtons = penButtons;
        Pressure = pressure;
        TiltX = tiltX;
        TiltY = tiltY;
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

    /// <summary>The stable identity of a touch contact. Zero for non-touch input.</summary>
    public long ContactId { get; }

    public TouchContactState? TouchContactState { get; }

    public PenContactState? PenContactState { get; }

    public PenButtons PenButtons { get; }

    public double Pressure { get; }

    public double TiltX { get; }

    public double TiltY { get; }

    public InputEventSource Source { get; }

    /// <summary>
    /// Maps a device-neutral <see cref="InputModifiers"/> onto the keyboard flags the
    /// UI layer routes on. The two enums are declared with identical members and
    /// values — <c>Broiler.Input.Contract.Tests</c> pins that — because
    /// <c>KeyboardModifierState</c> is published API on the keyboard package and
    /// pointer events must not depend on it (Broiler.Input ADR 0001).
    /// </summary>
    private static KeyboardModifierState ToModifiers(InputModifiers modifiers) => (KeyboardModifierState)modifiers;

    public static UiInputEvent FromMouseMove(MouseMoveEvent input) =>
        new(input.Header, UiInputEventKind.PointerMove, ToPoint(input.Position), null, null, MouseButton.None, null, null, ToModifiers(input.Modifiers), 0, null, MouseWheelAxis.Vertical, 0, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);

    public static UiInputEvent FromMouseButton(MouseButtonEvent input) =>
        new(input.Header, UiInputEventKind.PointerButton, ToPoint(input.Position), null, null, input.Button, input.Transition, null, ToModifiers(input.Modifiers), 0, null, MouseWheelAxis.Vertical, 0, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);

    public static UiInputEvent FromMouseWheel(MouseWheelEvent input) =>
        new(input.Header, UiInputEventKind.PointerWheel, ToPoint(input.Position), null, null, MouseButton.None, null, null, ToModifiers(input.Modifiers), 0, null, input.Axis, input.DeltaNotches, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);

    public static UiInputEvent FromTouchContact(TouchContactEvent input) =>
        new(input.Header, UiInputEventKind.TouchContact, ToPoint(input.Position), null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, input.ContactId, input.State, null, PenButtons.None, input.Pressure, 0, 0, input.Source);

    public static UiInputEvent FromPenContact(PenContactEvent input) =>
        new(input.Header, UiInputEventKind.PenContact, ToPoint(input.Position), null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, 0, null, input.State, input.Buttons, input.Pressure, input.TiltX, input.TiltY, input.Source);

    public static UiInputEvent FromKeyboardKey(KeyboardKeyEvent input) =>
        new(input.Header, UiInputEventKind.KeyboardKey, new BPoint(0, 0), null, input.Key.Name, MouseButton.None, null, input.Transition, input.Modifiers, input.NativeKeyCode, null, MouseWheelAxis.Vertical, 0, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);

    public static UiInputEvent FromKeyboardText(KeyboardTextEvent input)
    {
        ArgumentNullException.ThrowIfNull(input.Text);
        return new(input.Header, UiInputEventKind.TextInput, new BPoint(0, 0), input.Text, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);
    }

    public static UiInputEvent FromTextInput(TextInputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input.Text);
        return new(input.Header, UiInputEventKind.TextInput, new BPoint(0, 0), input.Text, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);
    }

    public static UiInputEvent FromTextComposition(TextCompositionEvent input)
    {
        ArgumentNullException.ThrowIfNull(input.Text);
        return new(input.Header, UiInputEventKind.TextComposition, new BPoint(0, 0), input.Text, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, input.State, MouseWheelAxis.Vertical, 0, 0, null, null, PenButtons.None, 0, 0, 0, input.Source);
    }

    internal UiInputEvent AsTouchPointerFallback() => TouchContactState switch
    {
        Broiler.Input.Touch.TouchContactState.Pressed =>
            new(Header, UiInputEventKind.PointerButton, Position, null, null, MouseButton.Left, Broiler.Input.Mouse.MouseButtonTransition.Down, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, ContactId, TouchContactState, null, PenButtons.None, Pressure, 0, 0, InputEventSource.Synthetic),
        Broiler.Input.Touch.TouchContactState.Moved =>
            new(Header, UiInputEventKind.PointerMove, Position, null, null, MouseButton.None, null, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, ContactId, TouchContactState, null, PenButtons.None, Pressure, 0, 0, InputEventSource.Synthetic),
        Broiler.Input.Touch.TouchContactState.Released or Broiler.Input.Touch.TouchContactState.Cancelled =>
            new(Header, UiInputEventKind.PointerButton, Position, null, null, MouseButton.Left, Broiler.Input.Mouse.MouseButtonTransition.Up, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, ContactId, TouchContactState, null, PenButtons.None, Pressure, 0, 0, InputEventSource.Synthetic),
        _ => throw new InvalidOperationException("A touch pointer fallback requires a contact state."),
    };

    internal UiInputEvent AsTouchPointerCancellation() =>
        new(Header, UiInputEventKind.PointerButton, new BPoint(double.MinValue, double.MinValue), null, null, MouseButton.Left, Broiler.Input.Mouse.MouseButtonTransition.Up, null, KeyboardModifierState.None, 0, null, MouseWheelAxis.Vertical, 0, ContactId, TouchContactState, null, PenButtons.None, Pressure, 0, 0, InputEventSource.Synthetic);

    private static BPoint ToPoint(InputPoint point) => new(point.X, point.Y);
}

using System;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Pen;
using Broiler.Input.Text;
using Broiler.Input.Touch;
using Broiler.UI;

namespace Broiler.UI.Standard;

public sealed class StandardInputRoute
{
    private readonly UiSession _session;

    public StandardInputRoute(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public bool Dispatch(MouseMoveEvent input) =>
        _session.DispatchInput(UiInputEvent.FromMouseMove(input));

    public bool Dispatch(MouseButtonEvent input) =>
        _session.DispatchInput(UiInputEvent.FromMouseButton(input));

    public bool Dispatch(MouseWheelEvent input) =>
        _session.DispatchInput(UiInputEvent.FromMouseWheel(input));

    public bool Dispatch(TouchContactEvent input) =>
        _session.DispatchInput(UiInputEvent.FromTouchContact(input));

    public bool Dispatch(PenContactEvent input) =>
        _session.DispatchInput(UiInputEvent.FromPenContact(input));

    public bool Dispatch(KeyboardKeyEvent input) =>
        _session.DispatchInput(UiInputEvent.FromKeyboardKey(input));

    public bool Dispatch(KeyboardTextEvent input) =>
        _session.DispatchInput(UiInputEvent.FromKeyboardText(input));

    public bool Dispatch(TextInputEvent input) =>
        _session.DispatchInput(UiInputEvent.FromTextInput(input));

    public bool Dispatch(TextCompositionEvent input) =>
        _session.DispatchInput(UiInputEvent.FromTextComposition(input));
}

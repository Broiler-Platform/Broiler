using System;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI;

namespace Broiler.Code.Core.Hosting;

/// <summary>
/// Turns raw evdev device events into UI input.
///
/// evdev is not a windowing input stack. It reports what a device did — this key
/// went down, the pointer moved this far — with no notion of a window, a cursor
/// position, or a character. Three things therefore have to happen here that the
/// Windows head gets from the OS:
///
/// <list type="bullet">
/// <item>relative motion is accumulated into an absolute position and clamped to
/// the viewport, because nothing else is tracking where the pointer is;</item>
/// <item>key names are normalised to the names the controls switch on, since the
/// Linux providers spell them <c>KeyA</c> and <c>ArrowLeft</c>;</item>
/// <item>characters are derived from keys, because evdev carries no text.</item>
/// </list>
///
/// That last one is also the boundary of what this can do: a US-layout mapping
/// is not an input method. It handles typing Latin text and nothing else, which
/// is exactly why the Linux head reports its IME as unavailable rather than
/// claiming this is one.
///
/// It lives in Core rather than the head so it can be tested on any operating
/// system — none of it needs a device, a display, or Linux.
/// </summary>
public sealed class EvdevInputRouter
{
    private readonly object _gate = new();
    private BSize _viewport;
    private double _pointerX;
    private double _pointerY;
    private bool _pointerPlaced;
    private MouseButtons _buttons;

    public EvdevInputRouter(BSize viewport) => _viewport = viewport;

    public BPoint PointerPosition
    {
        get
        {
            lock (_gate)
                return new BPoint(_pointerX, _pointerY);
        }
    }

    /// <summary>
    /// The viewport the pointer is confined to. The pointer starts centred, and
    /// afterwards a resize only clamps it — recentring on every resize would
    /// move the cursor out from under the user's hand.
    /// </summary>
    public void SetViewport(BSize viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        lock (_gate)
        {
            _viewport = viewport;
            if (!_pointerPlaced)
            {
                _pointerX = viewport.Width / 2;
                _pointerY = viewport.Height / 2;
                _pointerPlaced = true;
                return;
            }

            _pointerX = Clamp(_pointerX, viewport.Width);
            _pointerY = Clamp(_pointerY, viewport.Height);
        }
    }

    /// <summary>
    /// An absolute position from the window server, which knows where the real
    /// cursor is. Preferred over accumulated motion when it is available: it
    /// keeps this pointer and the X11 cursor the user can see in agreement.
    /// </summary>
    public UiInputEvent? SetAbsolutePointer(double x, double y, InputDeviceId device, long sequence)
    {
        lock (_gate)
        {
            double nextX = Clamp(x, _viewport.Width);
            double nextY = Clamp(y, _viewport.Height);

            // A sub-pixel move is not worth an event; at 60 Hz the queue would
            // fill with motion nobody can see.
            bool moved = !_pointerPlaced ||
                Math.Abs(nextX - _pointerX) >= 0.5 ||
                Math.Abs(nextY - _pointerY) >= 0.5;

            _pointerX = nextX;
            _pointerY = nextY;
            _pointerPlaced = true;

            return moved
                ? UiInputEvent.FromMouseMove(new MouseMoveEvent(
                    new InputEventHeader(device, StopwatchInputClock.Shared.GetTimestamp(), sequence),
                    InputPoint.ClientDeviceIndependentPixels(_pointerX, _pointerY),
                    _buttons,
                    InputEventSource.Semantic))
                : null;
        }
    }

    /// <summary>Relative device motion, accumulated into the tracked position.</summary>
    public UiInputEvent FromMouseMove(MouseMoveEvent motion)
    {
        lock (_gate)
        {
            Place();
            _pointerX = Clamp(_pointerX + motion.Position.X, _viewport.Width);
            _pointerY = Clamp(_pointerY + motion.Position.Y, _viewport.Height);
            return UiInputEvent.FromMouseMove(new MouseMoveEvent(
                motion.Header,
                InputPoint.ClientDeviceIndependentPixels(_pointerX, _pointerY),
                motion.Buttons,
                InputEventSource.Semantic));
        }
    }

    public UiInputEvent FromMouseButton(MouseButtonEvent button)
    {
        lock (_gate)
        {
            Place();
            _buttons = button.Buttons;
            return UiInputEvent.FromMouseButton(new MouseButtonEvent(
                button.Header,
                InputPoint.ClientDeviceIndependentPixels(_pointerX, _pointerY),
                button.Buttons,
                button.Button,
                button.Transition,
                InputEventSource.Semantic));
        }
    }

    public UiInputEvent FromWheel(MouseWheelEvent wheel)
    {
        lock (_gate)
        {
            Place();
            return UiInputEvent.FromMouseWheel(new MouseWheelEvent(
                wheel.Header,
                InputPoint.ClientDeviceIndependentPixels(_pointerX, _pointerY),
                wheel.Buttons,
                wheel.Axis,
                wheel.DeltaNotches,
                InputEventSource.Semantic));
        }
    }

    /// <summary>
    /// A key event, and the character it produces if it produces one. Both are
    /// returned together because the controls need both: the key drives
    /// shortcuts and caret movement, the text drives insertion, and a head that
    /// sent only one of them would have either no typing or no shortcuts.
    /// </summary>
    public (UiInputEvent Key, UiInputEvent? Text) FromKey(KeyboardKeyEvent key)
    {
        KeyboardKeyEvent normalized = key with
        {
            Key = KeyboardKey.FromName(NormalizeKeyName(key.Key.Name)),
            Source = InputEventSource.Semantic,
        };

        UiInputEvent keyEvent = UiInputEvent.FromKeyboardKey(normalized);
        return TryComposeText(normalized, out string text)
            ? (keyEvent, UiInputEvent.FromTextInput(
                new TextInputEvent(normalized.Header, text, InputEventSource.Semantic)))
            : (keyEvent, null);
    }

    /// <summary>
    /// The names the Linux providers use, mapped to the ones the controls switch
    /// on. <c>DesktopInputRouter.MapVirtualKey</c> does the same job for Win32
    /// codes; a key arriving under an unexpected name is a shortcut that does
    /// nothing with no way to see why.
    /// </summary>
    public static string NormalizeKeyName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 4 && name.StartsWith("Key", StringComparison.Ordinal))
            return char.ToUpperInvariant(name[3]).ToString();
        if (name.Length == 6 && name.StartsWith("Digit", StringComparison.Ordinal))
            return name[5].ToString();

        return name;
    }

    /// <summary>
    /// The character a key produces on a US layout, or none.
    ///
    /// Deliberately narrow. It covers unmodified and shifted Latin typing so the
    /// editor is usable, and refuses anything with Control, Alt, or a Windows
    /// key held, which are shortcuts rather than text. Everything beyond that —
    /// other layouts, dead keys, composition — needs a real input method, which
    /// is the gap this head reports rather than papers over.
    /// </summary>
    public static bool TryComposeText(KeyboardKeyEvent key, out string text)
    {
        text = string.Empty;

        if (key.Transition != KeyboardKeyTransition.Down)
            return false;

        const KeyboardModifierState Blocked =
            KeyboardModifierState.Control |
            KeyboardModifierState.Alt |
            KeyboardModifierState.LeftWindows |
            KeyboardModifierState.RightWindows;
        if ((key.Modifiers & Blocked) != 0)
            return false;

        bool shift = key.Modifiers.HasFlag(KeyboardModifierState.Shift);
        string name = NormalizeKeyName(key.Key.Name);

        if (name.Length == 1)
        {
            char character = name[0];
            if (character is >= 'A' and <= 'Z')
            {
                text = shift ? character.ToString() : char.ToLowerInvariant(character).ToString();
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                text = (shift ? ShiftedDigit(character) : character).ToString();
                return true;
            }
        }

        text = name switch
        {
            "Space" => " ",
            "Tab" => "\t",
            "Minus" => shift ? "_" : "-",
            "Equal" => shift ? "+" : "=",
            "BracketLeft" => shift ? "{" : "[",
            "BracketRight" => shift ? "}" : "]",
            "Semicolon" => shift ? ":" : ";",
            "Quote" => shift ? "\"" : "'",
            "Backquote" => shift ? "~" : "`",
            "Backslash" => shift ? "|" : "\\",
            "Comma" => shift ? "<" : ",",
            "Period" => shift ? ">" : ".",
            "Slash" => shift ? "?" : "/",
            _ => string.Empty,
        };

        return text.Length > 0;
    }

    private void Place()
    {
        if (_pointerPlaced)
            return;

        _pointerX = _viewport.Width / 2;
        _pointerY = _viewport.Height / 2;
        _pointerPlaced = true;
    }

    private static double Clamp(double value, double extent)
    {
        double max = Math.Max(0, extent - 1);
        return value < 0 ? 0 : value > max ? max : value;
    }

    private static char ShiftedDigit(char digit) => digit switch
    {
        '1' => '!',
        '2' => '@',
        '3' => '#',
        '4' => '$',
        '5' => '%',
        '6' => '^',
        '7' => '&',
        '8' => '*',
        '9' => '(',
        '0' => ')',
        _ => digit,
    };
}

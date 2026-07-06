# Broiler.Input Phase 2 Keyboard And Mouse Windows Parity

**Status:** Implemented inside `Broiler.Input`  
**Date:** 2026-07-02

Phase 2 adds the extraction-ready keyboard and mouse Windows path while keeping
all edits inside the `Broiler.Input` component.

## Keyboard

`Broiler.Input.Keyboard.Windows` handles:

| Windows message | Input output |
|---|---|
| `WM_KEYDOWN` | `KeyboardKeyEvent` with semantic source |
| `WM_SYSKEYDOWN` | `KeyboardKeyEvent` with `IsSystemKey=true` |
| `WM_KEYUP` | `KeyboardKeyEvent` up transition |
| `WM_SYSKEYUP` | `KeyboardKeyEvent` up transition with `IsSystemKey=true` |
| `WM_CHAR` | `KeyboardTextEvent` |

The key event carries virtual key, scan code, repeat count, extended-key bit,
previous down state, left/right/numpad location where derivable, modifier state,
semantic source, and timestamp header.

## Mouse

`Broiler.Input.Mouse.Windows` handles:

| Windows message | Input output |
|---|---|
| `WM_MOUSEMOVE` | `MouseMoveEvent` |
| left/right/middle/X button down/up | `MouseButtonEvent` |
| `WM_MOUSEWHEEL` | vertical `MouseWheelEvent` |
| `WM_MOUSEHWHEEL` | horizontal `MouseWheelEvent` |
| `WM_MOUSELEAVE` | `MouseLeaveEvent` |
| `WM_CAPTURECHANGED` | `MouseCaptureLostEvent` |

Mouse positions carry coordinate-space labels and optional scaling. Wheel
screen coordinates can be translated through `ScreenToClient` when an HWND is
available.

## Hot-Plug

The typed Windows providers implement `IInputDeviceWatcher` and observe
`WM_INPUT_DEVICE_CHANGE`. They report arrival and removal without consuming the
shared message, allowing keyboard and mouse observers to both see the same
notification.

## Duplicate Delivery

`WindowsInputMessageDispatcher` stops dispatching when a sink handles a message.
The Phase 2 contract tests pin this behavior so semantic/raw bridge code can
avoid duplicate click/key delivery by ordering its sinks deliberately.

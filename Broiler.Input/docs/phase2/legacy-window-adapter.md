# Broiler.Input Phase 2 Legacy Window Adapter

**Status:** Implemented inside `Broiler.Input`  
**Date:** 2026-07-02

`Broiler.Input.Legacy` provides a migration adapter for the existing `BWindow`
callback categories without referencing `Broiler.Graphics`.

## Adapter Events

| Existing callback category | Adapter event |
|---|---|
| pointer down | `PointerDown` |
| pointer move | `PointerMove` |
| pointer up | `PointerUp` |
| pointer leave | `PointerLeave` |
| pointer capture lost | `PointerCaptureLost` |
| mouse wheel | `MouseWheel` |
| key down | `KeyDown` |
| key up | `KeyUp` |
| text input | `TextInput` |

The adapter accepts `KeyboardInputDevice` and `MouseInputDevice` instances and
subscribes to their typed events. A later Graphics or application migration can
map these value types to `BPointerEventArgs`, `BMouseWheelEventArgs`,
`BKeyEventArgs`, and `BTextInputEventArgs`.

## Dependency Boundary

The adapter references only:

```text
Broiler.Input.Keyboard
Broiler.Input.Mouse
```

It does not reference Graphics, Windows, Camera, Microphone, or application
projects.

# Broiler.Input Phase 0 Decisions

**Status:** Approved for Phase 1  
**Date:** 2026-07-02  
**Roadmap:** `docs/roadmap/broiler-input-component.md`, Phase 0

Phase 0 creates the root component and freezes the first buildable dependency
shape. The first platform slice is Windows keyboard and mouse.

## Approved Component Root

```text
Broiler.Input/
```

The component has its own solution file and is referenced from the aggregate
workspace solution. A future split into a standalone repository or submodule
must preserve this root-level component identity.

## Approved Runtime Assemblies For This Slice

```text
Broiler.Input
Broiler.Input.Windows
Broiler.Input.Keyboard
Broiler.Input.Keyboard.Windows
Broiler.Input.Mouse
Broiler.Input.Mouse.Windows
```

Camera, microphone, touch, pen, and gamepad projects are intentionally deferred.
The roadmap still owns those assemblies, but they are not part of this
keyboard/mouse-first Phase 0 implementation slice.

## Abstract Class Names

| Assembly | Abstract device class | Direct base |
|---|---|---|
| `Broiler.Input` | `InputDevice` | none |
| `Broiler.Input.Keyboard` | `KeyboardInputDevice` | `InputDevice` |
| `Broiler.Input.Mouse` | `MouseInputDevice` | `InputDevice` |

No intermediate public `PointerInputDevice`, `WindowsInputDevice`, or untyped
payload base class is approved.

## Native Policy

Windows implementation assemblies may call operating-system APIs only through
`DllImport` or `LibraryImport`. They must not add third-party interop packages,
WPF, Windows Forms, or WinRT dependencies for this slice.

## Current Prototype Boundaries

- `Broiler.Input.Windows` owns the Windows message envelope, QPC clock, and Raw
  Input registration lease coordinator.
- `Broiler.Input.Keyboard.Windows` translates keyboard window messages and text
  messages into typed keyboard events.
- `Broiler.Input.Mouse.Windows` translates mouse window messages, wheel
  messages, X buttons, horizontal wheel, and leave tracking into typed mouse
  events.
- Graphics continues to own the current production callback path until a later
  migration phase connects it to these contracts.

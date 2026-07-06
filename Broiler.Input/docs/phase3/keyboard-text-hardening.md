# Broiler.Input Phase 3 Keyboard And Text Hardening

**Status:** Implemented inside `Broiler.Input`  
**Date:** 2026-07-03

## Unicode Scalars

`WindowsKeyboardInputDevice` now buffers a high UTF-16 surrogate from `WM_CHAR`
until the matching low surrogate arrives. The pair is emitted as one
`KeyboardTextEvent`.

Invalid surrogate input is never emitted as invalid UTF-16. An unpaired high or
low surrogate is converted to `U+FFFD`.

## Dead Keys

`WM_DEADCHAR` and `WM_SYSDEADCHAR` produce `KeyboardDeadKeyEvent`. Dead keys are
reported separately from committed text; the following `WM_CHAR` still produces
the composed committed character when Windows sends one.

## Layout And Modifiers

`WM_INPUTLANGCHANGE` produces `KeyboardLayoutChangedEvent` and is not consumed.
Keyboard events now expose side-specific modifier locations where Windows makes
them derivable:

- left/right Shift;
- left/right Control;
- left/right Alt;
- left/right Windows keys; and
- numpad location for numpad digit keys.

## IME Milestone

Phase 3 does not decode full IME composition strings. Instead, the first IME
milestone is explicit and non-silent:

| Message | Event |
|---|---|
| `WM_IME_STARTCOMPOSITION` | `KeyboardCompositionState.Started` |
| `WM_IME_COMPOSITION` | `KeyboardCompositionState.Unsupported` |
| `WM_IME_ENDCOMPOSITION` | `KeyboardCompositionState.Cancelled` |

All IME milestone messages are observed but not consumed by default.

## System Shortcuts

`WM_SYSKEYDOWN`, `WM_SYSKEYUP`, and `WM_SYSDEADCHAR` still emit typed events, but
they return `false` from `ProcessMessage` unless `KeyboardOpenOptions` sets
`ConsumeSystemKeyMessages=true`. This keeps system shortcuts from being swallowed
by default.

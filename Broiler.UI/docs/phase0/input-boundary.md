# Broiler.UI Phase 0 Input Boundary

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

Broiler.UI consumes normalized input from the Broiler.Input family. It does not
parse Windows messages, expose HWNDs, or make public contracts around current
Graphics input callback types.

## Current Legacy Source

`Broiler.Graphics.Windows.Direct2DWindow` currently translates Win32 messages
into these platform-neutral-ish Graphics callbacks:

| Legacy callback | Source messages | Temporary UI route |
|---|---|---|
| `OnPointerDown` | `WM_LBUTTONDOWN`, `WM_RBUTTONDOWN`, `WM_MBUTTONDOWN` | Pointer pressed |
| `OnPointerMove` | `WM_MOUSEMOVE` | Pointer moved |
| `OnPointerUp` | `WM_LBUTTONUP`, `WM_RBUTTONUP`, `WM_MBUTTONUP` | Pointer released |
| `OnPointerLeave` | `WM_MOUSELEAVE` | Pointer left viewport |
| `OnMouseWheel` | `WM_MOUSEWHEEL` | Mouse wheel |
| `OnKeyDown` | `WM_KEYDOWN`, `WM_SYSKEYDOWN` | Keyboard key down |
| `OnKeyUp` | `WM_KEYUP` | Keyboard key up |
| `OnTextInput` | `WM_CHAR` | Committed text input |

The temporary adapter may consume this source only inside migration tests or a
clearly named compatibility layer. It must be removable after Broiler.Input is
available.

## Permanent Direction

| Input concern | Permanent source |
|---|---|
| Device identity | `Broiler.Input` base abstractions |
| Mouse/pointer state | `Broiler.Input.Mouse` and future pointer/touch/pen abstractions |
| Keyboard keys | `Broiler.Input.Keyboard` normalized key contracts |
| Text and composition | Broiler.Input text/composition contracts plus host text-service ports |
| Timestamps | Input event timestamps, not Windows message data |
| Capture and focus | `UiSession`, not static process state |

Broiler.UI public input events must not include Windows message numbers,
virtual-key constants, HWNDs, or `Broiler.Graphics.Windows` types.

## Graphics Use

UI may use `Broiler.Graphics` geometry, color, render-list, surface, text
measurement, and resource contracts. It must not use Graphics-owned input
callbacks as permanent public UI input contracts.


# Broiler.Input Phase 0 Current-State Inventory

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Existing Graphics Input Types

`Broiler.Graphics/Broiler.Graphics/Windowing/BInputEvents.cs` currently owns:

| Type | Responsibility |
|---|---|
| `BMouseButtons` | Left, right, and middle button flags |
| `BPointerEventArgs` | Pointer position, current buttons, changed button |
| `BMouseWheelEventArgs` | Vertical wheel delta and pointer position |
| `BKeyEventArgs` | Platform virtual key plus Control, Shift, Alt state |
| `BTextInputEventArgs` | Single translated UTF-16 code unit |
| `BVirtualKey` | Small set of Win32-shaped virtual key constants |

## Existing Windows Translation

`Broiler.Graphics/Broiler.Graphics.Windows/Direct2DWindow.cs` currently
translates:

| Message | Current route |
|---|---|
| `WM_MOUSEMOVE` | `BWindow.OnPointerMove` |
| `WM_LBUTTONDOWN`, `WM_RBUTTONDOWN`, `WM_MBUTTONDOWN` | `BWindow.OnPointerDown` |
| `WM_LBUTTONUP`, `WM_RBUTTONUP`, `WM_MBUTTONUP` | `BWindow.OnPointerUp` |
| `WM_MOUSEWHEEL` | `BWindow.OnMouseWheel` |
| `WM_MOUSELEAVE` | `BWindow.OnPointerLeave` |
| `WM_KEYDOWN`, `WM_SYSKEYDOWN` | `BWindow.OnKeyDown` |
| `WM_KEYUP` | `BWindow.OnKeyUp` |
| `WM_CHAR` | `BWindow.OnTextInput` |

The current path does not expose physical device identity, Raw Input data,
X buttons, horizontal wheel, scan-code metadata, capture-lost state, hot-plug
state, or full text composition lifecycle.

## Application Consumers

The current application input consumer inventory is:

| File | Usage |
|---|---|
| `src/Broiler.App.Graphics/BrowserWindow.cs` | Overrides Graphics pointer, wheel, key, and text callbacks for browser interaction |

This inventory is intentionally narrow. New permanent consumers should target
the Broiler.Input abstraction assemblies instead of adding new dependencies on
Graphics-owned input callback types.

## Existing Behavior Tests

No existing files under `tests/`, `src/`, or `Broiler.Graphics/` matched the
current Graphics input callback types or the native message constants as
dedicated behavior tests. Keyboard and mouse migration phases therefore need new
characterization coverage before replacing the Graphics path.

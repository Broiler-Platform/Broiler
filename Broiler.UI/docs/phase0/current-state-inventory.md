# Broiler.UI Phase 0 Current-State Inventory

**Status:** Recorded  
**Date:** 2026-07-02

This inventory covers the aggregate checkout at
`D:\Broiler-New\Broiler-Release` and the checked-out `Broiler.Graphics`
submodule at commit `94e6709fd05f8828d188d95539be54c50e93d628`.

## Graphics UI-Shaped Public Surface

| Type or member | Current owner | Migration interpretation |
|---|---|---|
| `BControl.NativeHandle` | `Broiler.Graphics` | Native handle leak. New UI controls must not expose it. |
| `BControl.Bounds`, `Text`, `Enabled`, `Visible`, `Focus`, `Dispose` | `Broiler.Graphics` | Control state to replace with retained UI element state. |
| `BButtonControl.Clicked` | `Broiler.Graphics` | Button activation compatibility event. |
| `BEditControl.TextChanged`, `Submitted` | `Broiler.Graphics` | Native edit compatibility path until managed Edit passes replacement gates. |
| `BLabelControl` | `Broiler.Graphics` | Native static label compatibility path. |
| `BControlOptions` | `Broiler.Graphics` | Native-control construction options. |
| `BWindow.NativeHandle` | `Broiler.Graphics` | Native host handle. UI must not consume or expose it. |
| `BWindow.ClientSize`, `DpiScale`, `Renderer`, `Surface` | `Broiler.Graphics` | Host/rendering facts to replace with platform-neutral viewport ports. |
| `BWindow.CreateEditControl`, `CreateButtonControl`, `CreateLabelControl` | `Broiler.Graphics` | Native child-control factory methods to retire after UI migration. |
| `BWindow.StartAnimationTimer`, `StopAnimationTimer`, `OnAnimationTick` | `Broiler.Graphics` | Timer bridge to replace with UI session clock and host dispatch. |
| `BWindow.OnPointer*`, `OnMouseWheel`, `OnKey*`, `OnTextInput` | `Broiler.Graphics` | Transitional input callbacks until Broiler.Input cutover. |

## Implementation Owners

| File | Responsibility today | Notes |
|---|---|---|
| `Broiler.Graphics/Broiler.Graphics/Windowing/BWindow.cs` | Abstract mixed window, rendering, control, input, timer contract | Platform-neutral name but includes `IntPtr NativeHandle` and native-control factories. |
| `Broiler.Graphics/Broiler.Graphics/Windowing/BInputEvents.cs` | Mouse, wheel, keyboard, text event structs and virtual-key constants | Transitional input vocabulary. `BKeyEventArgs.VirtualKey` remains platform virtual-key-shaped. |
| `Broiler.Graphics/Broiler.Graphics/Controls/*.cs` | Native control abstractions | Directly expose native handles. |
| `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DWindow.cs` | Win32 window, render host child window, message loop, Direct2D surface lifecycle, input translation, timer dispatch | Correct long-term owner is Graphics backend plus application host, not UI runtime. |
| `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DControls.cs` | Win32 `EDIT`, `BUTTON`, and `STATIC` child controls | Compatibility only. Standard UI controls will be Broiler-drawn. |

## Application Consumers

The only current app consumer found under `src` is
`src/Broiler.App.Graphics/BrowserWindow.cs`.

| Consumer area | Current dependency | Baseline behavior |
|---|---|---|
| Navigation toolbar | six `BButtonControl` instances and one `BEditControl` | Back, forward, refresh, stop, favorite toggle, address entry, Go. |
| Favorites bar | dynamic `BButtonControl` list | Rebuilt from `FavoritesManager`, hidden when row overflows. |
| Status bar | `BLabelControl` | Displays ready/loading/error/done state. |
| In-page form edit bridge | lazy `BEditControl` | Native edit is overlaid on an HTML form input rectangle and synced back to DOM state. |
| Pointer input | `OnPointerDown`, `OnPointerMove`, `OnPointerUp`, `OnPointerLeave` | Routed into `HtmlContainer` hit testing and link activation. |
| Wheel input | `OnMouseWheel` | Scrolls rendered page content. |
| Keyboard input | `OnKeyDown` | Handles selection helpers, scrolling, reload, and Alt+Left/Right history. |
| Animation | `StartAnimationTimer`, `StopAnimationTimer`, `OnAnimationTick` | Steps `InteractiveSession` and invalidates rendering. |
| Native handle use | `NativeHandle` | Context menu ownership, screen coordinate conversion, save dialog owner. |

No production UI runtime exists yet. Phase 1 must scaffold new UI contracts
without deleting or changing the compatibility surface above.

## Migration Consumers of Record

1. `src/Broiler.App.Graphics/BrowserWindow.cs` application chrome.
2. `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DControls.cs` native child
   controls.
3. `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DWindow.cs` legacy input,
   timer, invalidation, and native-window bridge.

The browser content DOM and HTML form-control semantics remain browser-layer
concerns. Broiler.UI migrates application chrome and general-purpose widgets,
not the HTML DOM.


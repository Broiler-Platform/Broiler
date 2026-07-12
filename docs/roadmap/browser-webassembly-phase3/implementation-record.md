# Browser WebAssembly Phase 3 Implementation Record

**Status:** Local implementation complete; Chromium/Firefox CI evidence pending  
**Date:** 2026-07-11  
**Roadmap:** [Browser WebAssembly Roadmap](../browser-webassembly-graphics-ui-roadmap.md), Phase 3

## 1. Outcome

The browser sample now runs a real `UiSession` through existing public APIs:

```text
DOM input / ResizeObserver
  -> queued BrowserUiDispatcher
  -> requestAnimationFrame (one pending callback maximum)
  -> UiSession.DispatchInput / UiSession.RenderFrame
  -> BrowserUiHost
  -> persistent BImageRenderer + BImageSurface
  -> one presentFrame interop call
  -> reusable ImageData + putImageData
```

No `Broiler.Graphics`, `Broiler.Input`, or `Broiler.UI` core/API change was
needed. Phase 3 is an application-host integration layer. The Phase 2 evidence
still requires a later direct-Canvas backend for production frame rate, but that
does not block validation of UI ownership, scheduling, and input semantics.

## 2. Host and scheduling

`BrowserUiHost` implements `IUiHost`, `IUiTextInputHost`, `IUiCursorHost`, and
`IUiSystemSettingsHost`. It owns a persistent renderer, resizable surface,
cached no-copy pixel view, logical viewport, DPR, frame counter, caret snapshot,
and browser-system-settings snapshot.

`BrowserUiDispatcher.Post` always queues callbacks. A managed input export never
renders directly. JavaScript maintains one RAF slot and treats invalidations
raised while draining/rendering as satisfied by the in-progress frame. This is
important because layout controls can raise derived-state invalidations during
measure/arrange. The initial local browser observation was one RAF callback for
seven scheduling requests, six coalesced, followed by no idle frames. No frame
was presented recursively from a DOM input callback.

The clock uses monotonic `Stopwatch` elapsed time. `ResizeObserver` publishes CSS
size and DPR, and the existing Phase 2 logical/backing allocation budgets are
enforced before surface resize.

## 3. Standard-control slice

The attached tree uses existing implementations of:

- `StandardWindow`, `StandardPanel`, and `StandardLabel` for the shell;
- `StandardButton`, `StandardEdit`, and `StandardSlider` for commands/text/value;
- `StandardScrollView` with overflowing content and `StandardListView`;
- `StandardMenu`; and
- `StandardImageView` with a managed in-memory image handle.

Tooltip timing/hover behavior is not claimed by this vertical slice. It has no
primary Phase 3 workflow and remains grouped with the richer text, semantic, and
interaction hardening in Phase 4. The Phase 3 tree deliberately avoids an
inactive full-window tooltip that would distort canvas hit testing.

## 4. Input and focus adapter

The JavaScript module listens to browser Pointer Events and converts client
coordinates through the canvas bounding rectangle into logical device-
independent coordinates. It forwards move, down, up, button state, and normalized
wheel deltas. DOM pointer capture keeps drag completion routed outside the
canvas.

Pointer cancel, lost DOM capture, window blur, page hiding, and managed disposal
run the compatibility cleanup defined by the roadmap: outside move, synthetic
release for every held button, explicit session-capture release, and cursor
reset. The browser test opens capture and verifies that blur leaves no captured
control.

Key down/up uses canonical Broiler names (`ArrowLeft` becomes `Left`, space
becomes `Space`), modifiers, native key code, repeat count, and key location.
The neutral `KeyboardKeyEvent` retains repeat/location. `UiInputEvent` currently
projects the canonical name/modifiers/native code but not repeat/location; the
diagnostic counters and automation make that deliberate loss visible. No core
change is justified for the selected workflows.

Tab traversal is application-owned for the selected controls. Canvas focus owns
command keys. When `StandardEdit` publishes a caret, a hidden textarea is placed
at that logical caret and receives committed browser text. Moving focus away
returns DOM keyboard ownership to the canvas. This is basic committed text only:
no IME/composition, clipboard, password, or shaping parity is claimed.

## 5. Local browser evidence

The Codex in-app Chromium run validated:

| Workflow | Observed result |
|---|---|
| Initial scheduling | 1 RAF / 7 requests / 6 coalesced; then idle |
| Button | click count `1`, focus `button`, capture released |
| Edit | `WASM` -> `WASM-Browser`, focus `edit` |
| Slider | keyboard value `35` -> `36`, focus `slider` |
| ScrollView | keyboard offset `0` -> `24`, focus `scroll` |
| ListView | selection `window` -> `panel` |
| Menu | captured while open, one item invoked, capture released |
| Input reentrancy | recursive-frame count `0` |

The tuned 720 x 405 logical demo still takes roughly 1.1-1.2 seconds per frame
in this local interpreted/trimmed browser at DPR 1.5. That is consistent with the
Phase 2 CPU performance failure and is not presented as production performance.
The direct-Canvas gate remains assigned to Phase 5.

## 6. Automation and remaining gate

The Phase 3 smoke test drives Chromium and Firefox using real browser mouse and
keyboard input. It covers idle scheduling, button activation, hidden-editor text,
slider drag, wheel scrolling, list selection, menu capture/invoke, Tab focus,
keyboard value changes, repeat/location retention, blur cancellation, recursive
render detection, console errors, and listener cleanup.

The status remains “cross-browser CI evidence pending” until the committed
workflow completes on both engines. IME, clipboard, and generalized actionable
DOM semantics remain explicit Phase 4 gates.

## 7. Records

- [Machine-readable boundary](phase3-boundary.json)
- [Sample instructions](../../../Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/README.md)
- [Cross-browser smoke test](../../../tests/browser-wasm-phase3/smoke.mjs)

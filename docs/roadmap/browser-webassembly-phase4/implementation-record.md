# Browser WebAssembly Phase 4 Implementation Record

**Status:** Local implementation complete; cross-browser CI and manual assistive-technology evidence pending  
**Date:** 2026-07-11  
**Roadmap:** [Browser WebAssembly Roadmap](../browser-webassembly-graphics-ui-roadmap.md), Phase 4

## 1. Outcome

Phase 4 remains an application-host enhancement. Existing public contracts were
sufficient; no `Broiler.Graphics`, `Broiler.Input`, or `Broiler.UI` core/API was
changed.

```text
ordinary / password / RTL native editor
  -> selection + composition lifecycle
  -> neutral TextCompositionEvent (selection retained for diagnostics)
  -> existing UiInputEvent projection
  -> focused StandardEdit

trusted copy / cut / paste event
  -> event-scoped BrowserUiHost clipboard capability
  -> existing IUiClipboardHost
  -> StandardEdit Copy / Cut / Paste

UiSemanticNode snapshots
  -> password-safe application serializer
  -> stable DOM control IDs
  -> application-owned focus and action map
```

## 2. Text and composition

The browser host owns three native text contexts:

- a normal textarea for the main Edit;
- an empty native password input whose DOM value is never synchronized from the
  managed password; and
- an RTL textarea synchronized with the right-to-left Edit.

The active context follows the managed caret bounds, selection, input purpose,
and focus. Moving focus to a non-text control leaves no active native text
context. Normal `beforeinput` committed text is delivered separately from
composition start/update/commit/cancel.

`compositionend` emits exactly one committed `TextCompositionEvent`; the
following browser `input` event is suppressed. Neutral composition selection
start/length are recorded before the current `UiInputEvent` projection. The
selected `StandardEdit` only consumes composition text/state, so an expansion of
the UI projection is not justified yet.

The automated matrix covers a CJK composition sequence, combining text, a
surrogate-pair emoji, and Arabic/Hebrew RTL content. Actual operating-system IME
candidate-window behavior remains a manual browser/OS gate.

## 3. Clipboard capability and privacy

`BrowserUiHost` now implements the existing synchronous `IUiClipboardHost`, but
only while JavaScript is inside a trusted DOM `copy`, `cut`, or `paste` event.
The event supplies paste text synchronously and receives copied text
synchronously, matching the existing neutral contract without an async API.

Untrusted/synthetic clipboard events are ignored and counted. Browser clipboard
denial leaves the document unchanged. A password Edit blocks Copy/Cut through
existing `StandardEdit` behavior. Its value is absent from:

- the native password input;
- page diagnostics;
- the semantic JSON snapshot;
- accessible names/values; and
- the last-copy diagnostic.

This executable path does not justify a new async clipboard API. One should only
be proposed for workflows that cannot operate inside trusted clipboard events.

## 4. Semantic mirror and focus

Every presented frame captures password-safe semantic nodes for nine selected
controls. The application assigns stable IDs (`menu`, `button`, `edit`,
`password`, `rtl`, `slider`, `scroll`, `list`, `image`) and mirrors them into a
DOM group with actionable elements, accessible labels, enabled/focused state,
menu expansion, slider range/value, and current list selection description.

DOM focus events update managed focus. Managed pointer or canvas-keyboard focus
updates the corresponding DOM semantic element or native text context. Native
Tab order and the existing canvas-owned Tab traversal both reach the same
application focus order. This is intentionally an application-owned baseline,
not a generalized framework tab-order or semantic-action API.

Local in-app Chromium inspection exposed all nine nodes in the accessibility
tree and activated the managed Button from its DOM semantic element. The
password node exposed only “Password field”. See the separate support statement
for what is and is not certified.

## 5. Local evidence

The local trimmed Chromium run observed:

- 9 semantic nodes and 0 password privacy leaks;
- trusted physical key input updating the ordinary Edit;
- semantic keyboard activation incrementing the managed Button exactly once;
- one four-event CJK composition sequence producing one commit with neutral
  selection diagnostic `1+1`;
- combining text plus a surrogate-pair emoji reaching the managed Edit;
- untrusted clipboard attempts denied without losing managed focus;
- initial scheduling coalesced to one frame and returned idle; and
- Phase 1/2 regression checks still passing.

The repeatable Phase 4 diagnostic buttons and cross-browser test additionally
gate one CJK commit, combining/emoji input, RTL text, trusted clipboard where
the browser grants it, denied clipboard behavior, password copy blocking,
focus-context clearing, DOM Tab synchronization, no recursive rendering, and
listener cleanup.

## 6. Remaining gates

- committed full-AOT Chromium and Firefox workflow evidence;
- manual native IME runs on representative Windows/macOS browser combinations;
- manual NVDA/Chrome and VoiceOver/Safari or approved equivalent runs; and
- broader controls/scripts only when product requirements select them.

These gates prevent the baseline from being described as broad accessibility or
IME certification.

## 7. Records

- [Machine-readable boundary](phase4-boundary.json)
- [Accessibility support statement](accessibility-support.md)
- [Cross-browser smoke test](../../../tests/browser-wasm-phase4/smoke.mjs)
- [Sample instructions](../../../Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/README.md)

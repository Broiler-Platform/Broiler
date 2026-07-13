# Formatting Codes Phase 3 implementation record

**Completed:** 2026-07-12  
**Roadmap:** [`../formatting-codes-pane-roadmap.md`](../formatting-codes-pane-roadmap.md)

Phase 3 delivers the public read-only preview milestone in both Writer hosts. The
editor remains the only mutable source of truth; the lower pane receives an
immutable projection and typed source mappings.

## Shared application binding

`Broiler.Writer.FormatCodes` contains the platform-neutral host behavior used by
desktop and WebAssembly Writer:

- `WriterFormatCodesController` subscribes to RichEdit document, selection, and
  command events and unsubscribes deterministically on disposal;
- a reentrancy guard prevents selection feedback loops;
- source and projected selections preserve anchor/focus direction;
- code activation moves the editor caret and supplies a separate affected-range
  highlight without changing the document or undo history;
- pending caret formatting is projected as a non-canonical overlay and is
  refreshed or cleared when the caret state changes;
- projections above the Phase 1 threshold use an injected scheduler, cancel old
  work, and publish only when generation and document identity still match; and
- the shared layout and shortcut policies keep both hosts behaviorally aligned.

The controller never sets focus, reparses visible bracket text, replaces the
editor document, or owns document state.

## Writer hosts

Desktop and browser Writer now provide the same original Formatting Codes UI:

- a checked **View > Formatting Codes** command;
- `Ctrl+Shift+F3` show/hide and `F6`/`Shift+F6` focus cycling;
- a keyboard- and pointer-operable horizontal splitter;
- persistent pane visibility and splitter position across New, Open, Save, and
  document replacement;
- matching palette roles, status text, sizing constraints, and monospaced view;
  and
- copy, search, click-to-source, caret follow, and selection synchronization.

Showing the pane does not move focus. Hiding it returns focus to the editor only
when focus was inside the pane or its splitter. Browser clipboard, cursor, and
text-focus publication recognize the Formatting Codes view.

`UiRichEdit.SecondarySelection` is a non-editing inspector highlight. The
Standard renderer paints it independently from the primary selection, and it is
cleared when the source document is replaced.

## Verification

Verification used .NET SDK 10.0.301 / runtime 10.0.9:

| Scope | Result |
|---|---:|
| `Broiler.Writer.FormatCodes.Tests` | 16 passed, 0 failed |
| Complete `Broiler.UI.slnx` suite | 226 passed, 0 failed |
| Complete `Broiler.Documents.slnx` suite | 256 passed, 0 failed |
| RichEdit RTF integration | 6 passed, 0 failed |
| Total verified tests | **504 passed, 0 failed** |
| Desktop Writer | Release build passed, 0 warnings |
| WebAssembly Writer | Release build passed, 0 warnings |

Focused coverage includes exact canonical output, live edits, pending-style
creation and clearing, directional mappings in both directions, event-loop
bounds, click-to-source affected ranges, focus preservation, document identity,
undo preservation, document replacement, disposal, stale background results,
small-window layout constraints, hidden-pane layout, and shared shortcuts.

## Phase 4 entry condition

Phase 4 may add typed structured edits. It must continue to route all mutations
through RichEdit transactions, keep code tokens atomic, preserve read-only and
document limits, and make each pane action one predictable undo unit.

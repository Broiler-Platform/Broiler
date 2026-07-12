# Formatting Codes Phase 2 implementation record

**Completed:** 2026-07-12  
**Roadmap:** [`../formatting-codes-pane-roadmap.md`](../formatting-codes-pane-roadmap.md)

Phase 2 delivers the reusable read-only control and splitter families. It does
not yet dock the pane in Writer or subscribe to RichEdit; that application
binding remains Phase 3.

## Control abstraction

`UiFormatCodeView` owns only platform-neutral view state and commands:

- immutable `FormatCodeProjection` assignment;
- directional projected selection and caret state;
- canonical selection copy through `IUiClipboardHost`;
- ordinal forward/backward search with optional case matching and wrapping;
- token activation through `FormatCodeMappedPosition`, never reparsing bracket
  text;
- host events for navigation, search UI, and returning focus to the editor;
- wrapping and independent scroll policies; and
- read-only semantic text information, selection, current-token descriptions,
  and a dedicated `FormatCodeView` semantic role.

Paragraph tokens are announced as **engine state; visual rendering pending**.
Pending tokens are announced as pending formatting and remain excluded from the
canonical value and clipboard text.

## Standard implementation

`StandardFormatCodeView` provides:

- monospaced virtual-line layout with wrap and no-wrap modes;
- horizontal and vertical scrolling, wheel input, scrollbar tracks/thumbs, and
  keyboard-driven caret visibility;
- visible-slice rendering rather than one control or render submission per
  source token;
- token-role colors plus semibold code text, so meaning is not carried by color
  alone;
- selection and caret painting, clipping, token-aware hit testing, drag
  selection, and typed navigation activation;
- Left/Right, Up/Down, Home/End, Page Up/Down, Ctrl+arrow token navigation,
  Shift selection, Ctrl+A, Ctrl+C, Ctrl+F, F3/Shift+F3, Enter/Space activation,
  and Escape handoff; and
- live light, dark, high-contrast, and enlarged-text theme behavior through the
  existing Standard theme roles.

The one-million-character no-wrap test verifies that a 300-unit viewport submits
fewer than 100 text characters to the render list and creates no token child
controls.

## Reusable splitter

The repository had no splitter abstraction, so Phase 2 also adds
`UiSplitter`/`StandardSplitter`. It exposes a normalized, bounded value for host
layout, pointer dragging over a supplied layout extent, arrow/Page/Home/End
keyboard resizing, focus and semantic state, theming, and a factory. Phase 3 can
therefore compose the pane without embedding resize mechanics in Writer.

## Packaging and topology

The four runtime projects and three focused test projects are registered in the
component and aggregate solutions. Both control families are included in
`Broiler.UI.All`. The meta-project build also exposed a transitive relative-path
warning in the existing RichEdit RTF integration; its three cross-component
references now resolve from `MSBuildThisFileDirectory`. A clean restored
`Broiler.UI.All` Release build completes with zero warnings.

The machine-readable boundary is
[`phase2-boundary.json`](phase2-boundary.json).

## Verification

Release verification on .NET SDK 10.0.301 / runtime 10.0.9:

| Scope | Result |
|---|---:|
| `Broiler.UI.FormatCodeView.Tests` | 7 passed, 0 failed |
| `Broiler.UI.FormatCodeView.Standard.Tests` | 14 passed, 0 failed |
| `Broiler.UI.Splitter.Tests` | 3 passed, 0 failed |
| Focused Phase 2 tests | **24 passed, 0 failed** |
| Complete `Broiler.UI.slnx` suite | 226 passed, 0 failed |
| Complete `Broiler.Documents.slnx` suite | 256 passed, 0 failed |
| RichEdit RTF integration | 6 passed, 0 failed |
| Total verified tests | **488 passed, 0 failed** |
| `Broiler.UI.All` | Release build passed, 0 warnings |
| Desktop Writer | Release build passed, 0 warnings |
| WebAssembly Writer | Release build passed, 0 warnings |

Formatting verification reports no changes for the new control and test
projects. The focused tests cover architecture boundaries, factories,
directional selection, canonical copy, search, typed mappings, semantic state,
pending formatting, wrap/no-wrap, both scroll axes, hit testing, keyboard and
pointer reachability, render-list balance, all four theme presets, enlarged
text, splitter operation, and million-character virtualization.

## Phase 3 entry condition

Phase 3 may add the shared Writer controller and host layout. It must consume
typed projection and navigation data, use the new splitter, preserve document
identity and undo history, discard stale background projections, and keep the
desktop/WebAssembly shell differences small.

# ADR 0020 - Formatting Code View and Writer Integration

**Status:** Approved for Formatting Codes Phase 1  
**Date:** 2026-07-12

## Context

The Formatting Codes roadmap adds a synchronized lower pane to the Writer
hosts. The headless projection decision is recorded by
`Broiler.Documents/docs/adr/0006-formatting-codes-projection-and-grammar.md`.
The UI decision must preserve the one-control-per-assembly rule, maintain
desktop/WebAssembly parity, and state how the pane behaves while paragraph
properties exist in the model but are not all visibly rendered by RichEdit.

## Decision

### Public identity and activation

- The public feature name is **Formatting Codes**.
- The initial toggle shortcut is `Ctrl+Shift+F3`. It is original to Broiler,
  configurable where host command configuration exists, and always accompanied
  by a menu command and an accessible name.
- Public UI and documentation may use another product's name only for concise,
  factual historical comparison under the repository's attribution guidance.
  Broiler does not copy another product's icons, wording, layout measurements,
  colors, or branded visual treatment.

### Control family and ownership

- `UiFormatCodeView` is a new Text-category abstraction in its own
  `Broiler.UI.FormatCodeView` assembly.
- `StandardFormatCodeView` and its factory live in the mirrored
  `Broiler.UI.FormatCodeView.Standard` assembly.
- The control consumes typed projection results. It does not inspect
  `RichTextDocument` internals or parse the displayed command string.
- A Writer integration/controller owns binding between one RichEdit instance and
  one Formatting Code view. The editor document, selection, caret, command
  execution, and undo/redo state remain authoritative.
- Desktop and WebAssembly Writer expose the same feature and command semantics;
  platform adapters may differ only in input and rendering mechanics.

### MVP interaction

- Phase 2 is a read-only synchronized view: display, navigation, selection,
  copy, search, and accessible inspection are permitted; token or raw-source
  editing is not.
- Selection and caret mapping use projection metadata. The view never derives
  a model position by counting characters in the rendered command text.
- Opening, closing, or resizing the pane does not replace the document, clear
  RichEdit undo history, or change focus unexpectedly.
- The pane uses virtualized text/token layout rather than one UI child per token.
  Large-document scope reduction or asynchronous projection is enabled only by
  measured thresholds.

### Paragraph renderer gap

Formatting Codes shows every supported paragraph property already stored by the
document model even when the current RichEdit renderer does not yet paint that
property. Such a token is identified in its accessible description and optional
diagnostic presentation as **engine state; visual rendering pending**. It is not
hidden and is not presented as proof of rendered appearance. Renderer parity is
tracked independently and does not block the read-only semantic projector.

### Accessibility and preferences

- The splitter and pane are keyboard reachable and resizable. Focus, selection,
  current token, and mapped document range are exposed through semantic APIs.
- Code tokens must be distinguishable without color alone. High-contrast and
  reduced-motion preferences apply.
- Pane visibility, height, wrapping, and scope may be persisted as user
  preferences; document content and projections are not telemetry.

## Consequences

- The control follows ADR 0001 and ADR 0019 rather than becoming Writer-only
  drawing code.
- Writer integration can evolve without coupling the canonical projector to UI
  or platform assemblies.
- The pane can accurately expose model features before visual renderer parity,
  provided the gap is explicit to users.
- Structured token actions and any Advanced source editor remain later,
  separately gated phases.

## Follow-up

Phase 2 must add render-list, hit-test, keyboard, accessibility, and
desktop/WebAssembly integration tests. The Writer release checklist must verify
that toggling the pane preserves document identity and undo/redo history.

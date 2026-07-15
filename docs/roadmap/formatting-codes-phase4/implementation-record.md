# Formatting Codes Phase 4 implementation record

**Completed:** 2026-07-15  
**Roadmap:** [`../formatting-codes-pane-roadmap.md`](../formatting-codes-pane-roadmap.md)

Phase 4 delivers safe structured editing without introducing a bracket parser,
a second document model, or a second undo stack. `UiRichEdit` remains the only
mutable source of truth.

## Typed edit boundary

`Broiler.Documents.FormatCodes` now owns the platform-neutral Phase 4 contracts:

- `ReplaceFormatCodeTextIntent`, `ApplyFormatCodeInlineIntent`, and
  `ApplyFormatCodeParagraphIntent` carry opaque source ranges and typed model
  deltas;
- editable projector tokens carry `FormatCodeTokenEditDescriptor` metadata, so
  code identity and removal semantics never come from parsing display text;
- `FormatCodeEditValidator` enforces source-range validity, Unicode integrity,
  insertion/document/paragraph limits, finite bounded metrics, font-family and
  URL lengths, and the inert `http`/`https`/`mailto` link policy; and
- `FormatCodeInsertPalette` supplies stable typed entries for formatting and
  structural insertion.

The canonical grammar and projector text are unchanged. After every accepted
edit, the pane is republished from a fresh canonical projection.

## One transaction path

`RichTextEditor` and `UiRichEdit` now expose explicit-range text replacement,
inline formatting, and paragraph formatting. Each document-changing call
creates one existing `RichTextTransaction`, records its before/after selection,
and participates in the same bounded undo/redo history used by the main editor.
Ordinary edits never assign `UiRichEdit.Document`.

`WriterFormatCodesController` rejects disabled, read-only, stale-token,
unsupported, malformed, and over-limit requests before dispatch. Empty-range
inline palette actions remain the existing pending-caret-style operation and
are projected as a transient overlay.

## Interaction behavior

- Ordinary typing, replacement, cut, paste, and committed IME text operate on
  mapped document text spans.
- Code tokens remain atomic. Backspace/Delete on an editable code issues its
  typed removal intent; it never deletes characters from the bracket label.
- Removing an inline pair clears that property over its recorded affected range.
  Paragraph properties reset to their model defaults, while tabs, soft breaks,
  and paragraph breaks use explicit source-range deletion.
- Ctrl+Z/Ctrl+Y work while the pane has focus and route to RichEdit history.
- The Format menu exposes a shared desktop/WebAssembly **Insert Code** palette
  and **Remove selected code** action. Existing formatting commands provide
  Clear Formatting and selection-based property changes; the controller also
  exposes validated scalar token editing for font, size, color, link, and
  paragraph values.
- Accessibility text state reports editability and IME composition state.

## Verification

Verification used .NET SDK 10.0.301 / runtime 10.0.9:

| Scope | Result |
|---|---:|
| Complete `Broiler.Documents.slnx` suite | 263 passed, 0 failed |
| Complete `Broiler.UI.slnx` suite | 231 passed, 0 failed |
| `Broiler.Writer.FormatCodes.Tests` | 21 passed, 0 failed |
| Total verified tests | **515 passed, 0 failed** |
| Desktop Writer Release build | passed, 0 warnings |
| WebAssembly Writer Release build | passed, 0 warnings |

Focused coverage includes typed metadata, safe-link and numeric validation,
document limits, explicit-range transaction shape, selection restoration,
text replacement, semantic token deletion, cross-pane undo/redo, pending style,
clipboard primitives, IME update/commit state, read-only rejection,
malformed palette values, stale tokens, canonical reprojection, and host parity.

## Phase 5 boundary

Phase 4 does not accept or parse arbitrary bracket source. Phase 5 remains
optional and still requires the roadmap's explicit product go/no-go review,
detached invalid drafts, a versioned parser, diagnostics, and atomic apply.

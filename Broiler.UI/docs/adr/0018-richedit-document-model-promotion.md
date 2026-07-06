# ADR 0018 - RichEdit Document Model Promotion

**Status:** Approved for RichEdit Phase 5 rework / Broiler.Documents Phase 1
**Date:** 2026-07-05

## Context

ADR 0013 placed the whole RichEdit family — the document model *and* the
`UiRichEdit` control — in the `Broiler.UI.RichEdit` assembly, and ADR 0014 defined
that model. In practice the model types (`RichTextDocument`, `RichTextParagraph`,
`InlineStyle`/`Delta`, `ParagraphStyle`/`Delta`, `StyleRun`, `RichTextPosition`,
`RichTextRange`, `RichTextOperation`, `RichTextTransaction`, `RichTextEditResult`,
`RichTextEditor`, `ListKind`, `TextAlignment`) use only `Broiler.Graphics`; they
have no `Broiler.UI` dependency. They sit in a UI assembly only for historical
packaging reasons.

The document-formats roadmap (`docs/roadmap/broiler-documents-component.md`) adds
RTF (then HTML/Markdown) as codecs that decode to and encode from this exact
model. RTF import/export is a headless concern — a CLI converter, server, the
clipboard, or a print path must not have to reference `Broiler.UI` to parse a
`.rtf` file. The new component's ADR 0002 chose **Path A: promote the model**.
This ADR records the mirror decision on the `Broiler.UI` side and supersedes the
model *placement* (not the design) in ADRs 0013 and 0014.

Timing matters: RichEdit Phase 5 ("DOM and HTML adapter") is not yet built.
Deciding placement now lets HTML land as a peer codec instead of a UI-side
`Broiler.UI.RichEdit.Dom` adapter that would later need moving.

## Decision

- **Model moves to `Broiler.Documents.Model`.** The pure model types listed above
  move to the new platform-neutral `Broiler.Documents.Model` assembly (depends
  only on `Broiler.Graphics`). `Broiler.UI.RichEdit` now **references**
  `Broiler.Documents.Model` instead of owning the model.
- **The control stays in `Broiler.UI.RichEdit`.** `UiRichEdit`, `RichEditCommand`,
  `RichEditCommandState`, the event-args types, and `RichEditScrollPolicy` remain,
  and the **public control API is unchanged**.
- **Design preserved.** ADR 0014's guarantees are carried over verbatim:
  immutable/copy-on-write snapshots; opaque, stable `RichTextPosition`/
  `RichTextRange` with `internal` indexing and no UTF-16 movement promise;
  transaction undo; and the inline/paragraph style subset. This is a move, not a
  redesign.
- **`InternalsVisibleTo` retargets.** The `InternalsVisibleTo` that today lets
  `Broiler.UI.RichEdit.Standard` (and the RichEdit test assemblies) read the
  internal position indexing moves onto `Broiler.Documents.Model`. Positions stay
  opaque to external consumers.
- **Phase 5 is re-pointed.** RichEdit Phase 5's HTML interop becomes the
  `Broiler.Documents.Html` codec (references `Broiler.DOM`/`Broiler.Dom.Html`);
  the `Broiler.UI.RichEdit.Dom` assembly named in ADR 0013 is **not** created.
  The DOM policy of ADR 0013 (no DOM in core UI assemblies) is *strengthened*, not
  weakened: DOM now lives entirely outside the UI tree, in the document codec.
- **Rich clipboard.** The rich-clipboard path deferred by ADR 0016 is satisfied by
  the RTF codec (`CF_RTF`); core `IUiClipboardHost` is still not widened, and
  plain-text fallback is preserved.

## Consequences

- ADR 0013's assembly-placement clause for the model and its
  `Broiler.UI.RichEdit.Dom` adapter, and ADR 0014's implicit "model lives in
  `Broiler.UI.RichEdit`" assumption, are **superseded in part** by this ADR. The
  command model (ADR 0015), clipboard/HTML sanitization policy (ADR 0016), and
  accessibility semantics (ADR 0017) are unaffected.
- The move is mechanical and guarded by the existing RichEdit suite as a
  regression baseline (129/129 green on 2026-07-05: `Broiler.UI.RichEdit.Tests`
  74, `Broiler.UI.RichEdit.Standard.Tests` 55). A green run before and after the
  move is the acceptance evidence.
- Architecture tests must assert `Broiler.Documents.Model` references only
  `Broiler.Graphics`, and that `Broiler.UI.RichEdit` keeps the same public control
  surface after the move.
- No `Broiler.UI` runtime assembly gains a DOM dependency; document interchange
  (RTF, HTML) lives entirely in `Broiler.Documents.*`.

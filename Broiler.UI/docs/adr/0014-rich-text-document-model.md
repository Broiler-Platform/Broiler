# ADR 0014 - Rich Text Document Model

**Status:** Approved for RichEdit Phase 1; **placement superseded** by
[ADR 0018](0018-richedit-document-model-promotion.md) (2026-07-05)  
**Date:** 2026-07-04

> **Update (2026-07-05).** The model *design* in this ADR stands unchanged, but
> the assembly it lives in does not: per ADR 0018 the model types move from
> `Broiler.UI.RichEdit` to the platform-neutral `Broiler.Documents.Model`
> assembly (depends only on `Broiler.Graphics`). Opaque positions, copy-on-write
> snapshots, transaction undo, and the style subset are all preserved.

## Context

RichEdit needs an editor state model independent of rendering (roadmap Phase 1)
and a text-indexing model that will not force a public commitment to UTF-16
code-unit semantics. `UiEdit` stores a single `string` with `int` caret and
selection indices (`Broiler.UI.Edit/UiEdit.cs`); that is adequate for single-line
entry but cannot express paragraphs, style runs, or positions that stay stable
across edits. The Phase 0 exit gate requires that no public API depends on an
unchosen text-indexing model and that the first-release rich-text subset is
explicit.

## Decision

- **Document model.** A `RichTextDocument` is an ordered sequence of paragraphs
  plus document metadata. A paragraph carries text content, a paragraph style,
  and non-overlapping (normalized) inline style runs. Snapshots are immutable or
  copy-on-write so document operations are deterministic and testable without
  graphics, input, or a host.
- **Positions and ranges.** Public navigation uses opaque, stable
  `RichTextPosition` and `RichTextRange` value types (anchor/focus with
  normalized start/end). Public APIs do not expose raw string indexes and do not
  promise UTF-16 code-unit movement where user-visible caret movement is
  intended. The first implementation may store per-paragraph offsets internally,
  but the public position type leaves room for grapheme clusters, bidi, and
  shaped runs. This satisfies the exit-gate item "no public API depends on an
  unchosen text-indexing model."
- **Operations and undo.** The primitive operations are: insert text, delete
  range, split paragraph, merge paragraph, apply inline style, apply paragraph
  style. Every user action is a transaction with before/after selection and an
  operation list; undo/redo replays transactions with bounded history.
  Transactions are a Phase 1 primitive, not a later feature.
- **First-release inline style subset (authoritative).**

  | Inline style | First release |
  |---|---|
  | Font family | yes |
  | Font size | yes |
  | Weight (bold) | yes |
  | Italic | yes |
  | Underline | yes |
  | Strikethrough | yes |
  | Foreground color | yes |
  | Background color | yes |
  | Link metadata (href + display text) | yes |

- **First-release paragraph style subset (authoritative).**

  | Paragraph style | First release |
  |---|---|
  | Alignment (left/center/right) | yes |
  | Line spacing | yes |
  | List kind (none/bullet/numbered) | yes |
  | Indent level | yes |
  | Spacing before/after | yes |

- **Out of scope.** Tables, images, embedded media, arbitrary CSS, and nested
  block structures beyond simple lists are excluded from the first release
  (roadmap section 3).

## Consequences

- The command model (ADR 0015) maps commands onto these operations and this style
  subset; the clipboard/HTML subset (ADR 0016) maps onto the same styles;
  accessibility (ADR 0017) derives value/caret/selection from positions.
- Model invariant tests (split, merge, delete, style normalization, selection
  adjustment, undo) run with no graphics, input, or host dependency (roadmap
  Phase 1 exit gate).
- Because positions are opaque, later adoption of grapheme- and bidi-correct
  movement does not break the public API.
- Plain-text import/export is required in Phase 1; rich import/export is deferred
  to the DOM adapter (ADR 0016).

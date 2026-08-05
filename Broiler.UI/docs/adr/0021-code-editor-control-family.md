# ADR 0021 - CodeEditor Control Family

**Status:** Approved for Broiler Code Phase 0  
**Date:** 2026-08-05

## Context

Broiler Code needs a source editor. Broiler.UI already has two controls that
look like candidates and are not:

- `StandardRichEdit` edits a `RichTextDocument` — a formatted-document model of
  paragraphs and style runs. A source buffer is a character sequence with line
  structure, and the two disagree about what a document *is*, not merely about
  how it is drawn.
- `StandardFormatCodeView` renders monospace tokens by visible line and is
  read-only over a projection it does not own.

Neither exposes a collection of versioned diagnostic adornments, and neither
takes its text from a buffer whose edit transactions belong to someone else.

The product decision this record serves is in
[Broiler Code architecture](../../../docs/architecture/broiler-code.md): the
workspace owns edit transactions, versions, and undo/redo; the control owns
view state and renders the accepted snapshot.

## Decision

### Control family

- `UiCodeEditor` is a new Text-category abstraction in its own
  `Broiler.UI.CodeEditor` assembly.
- `StandardCodeEditor` and its factory live in the mirrored
  `Broiler.UI.CodeEditor.Standard` assembly.

This follows ADR 0001 and ADR 0019 unchanged. `Broiler.UI.All` is regenerated
with `scripts/gen-metapackages.ps1` when the assemblies land.

### CodeEditor does not subclass RichEdit

`UiCodeEditor` does not derive from `UiRichEdit`, and `StandardCodeEditor` does
not derive from `StandardRichEdit`. It also never parses colored display text to
recover structure.

It *may* reuse extracted caret, selection, scrolling, IME, visible-line, and
token-rendering mechanics from `StandardRichEdit` and `StandardFormatCodeView`.
Reuse happens by extracting those mechanics into shared helpers, not by
inheriting a document model that does not fit.

### The control does not own the text

The control-facing document interface is defined by
`Broiler.UI.CodeEditor` itself and has no dependency on Roslyn, on a product
workspace, on the filesystem, or on a platform assembly. It exposes:

- an immutable snapshot with line-addressable access;
- versioned edit *intents*, submitted rather than applied; and
- an accepted-snapshot result the control renders.

The control never mutates an independent copy of the text. A host that binds it
to a workspace gets workspace-owned undo/redo for free; a host that binds it to
a trivial in-memory buffer gets a working editor for tests. Neither arrangement
changes the control.

An edit intent that names a superseded version is **rejected, not rebased**. The
control re-derives its view state from the snapshot it is given back.

### Classifications are neutral and line-relative

The control consumes classification spans, not language tokens. The
classification vocabulary is language-neutral — comment, documentation comment,
keyword, control keyword, preprocessor keyword and text, string, escape,
character, numeric, operator, punctuation — and the control maps those kinds to
theme tokens. It must not learn C# token kinds, because a second language would
then require changing the control.

Spans are stored per line with line-relative offsets, so an edit on one line
leaves every other line's spans valid unchanged.

### Results are snapshot-checked

Classification and diagnostic results name the snapshot they were produced from.
The control renders a result only while that exact snapshot is current, and
compares snapshot identity rather than a version number — a version integer is
not comparable across documents, and a stale result that happens to match one is
worse than no result.

### Diagnostics are typed

The control consumes typed diagnostics: severity, span, and an accessible
description. It never parses a message string, and never treats message text as
identity. Squiggles, gutter marks, and tooltips are driven by severity and span.

### Palette

A CodeEditor-specific classification palette maps from existing theme roles,
with compatible defaults so existing theme initializers keep working. Every
state stays distinguishable in high contrast and none is conveyed by color
alone.

## Consequences

- Broiler Code can bind the control to `Broiler.Code.Workspaces` without the
  control knowing that type exists, and Broiler.UI can test the control without
  a workspace.
- RichEdit and Writer are unaffected. Their compatibility tests remain the gate
  on any mechanics extracted for reuse.
- A stale classification cannot paint, by construction rather than by timing.
- The buffer representation behind the document interface is the host's problem,
  which matters: Phase 0 measured a whole-string snapshot costing 17.63 ms and
  21.4 MB per keystroke on a 10.5 MB document, against 0.16 ms to analyse it.
  The control does not have to change when that representation does.

## Follow-up

Phase 1 delivers the control, its rendering by visible range, and the tests.
Real C# classification arrives in Phase 3; Phase 1 uses a deterministic fixture
classifier so the control's tests do not depend on a language service.

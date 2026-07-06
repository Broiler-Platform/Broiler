# ADR 0013 - RichEdit Assembly Boundaries And DOM Adapter Policy

**Status:** Approved for RichEdit Phase 1; **superseded in part** by
[ADR 0018](0018-richedit-document-model-promotion.md) (2026-07-05)  
**Date:** 2026-07-04

> **Update (2026-07-05).** The document-model *placement* in this ADR is
> superseded by ADR 0018: the model moves to `Broiler.Documents.Model` and
> `Broiler.UI.RichEdit` references it. HTML interop becomes the
> `Broiler.Documents.Html` codec, so the `Broiler.UI.RichEdit.Dom` adapter named
> below is **not** created — DOM now lives entirely outside the UI tree, which
> strengthens (not weakens) this ADR's "no DOM in core UI assemblies" rule. The
> control names, boundaries, and multi-line placement decisions here are
> otherwise unchanged.

## Context

The RichEdit roadmap (`docs/roadmap/broiler-ui-rich-edit-control.md`) proposes a
formatted, multi-paragraph editor as a control family distinct from `UiEdit`.
Before any kernel code is written, Phase 0 must fix the name, the public classes,
the assembly set, the dependency direction, whether `Broiler.DOM` may be
referenced by core UI assemblies, and where simple multi-line plain text belongs.
ADR 0001 already fixes the UI per-type assembly rule and the component roadmap
sections 6.4-6.5 fix the OS-neutrality and forbidden-reference rules; RichEdit
must comply rather than amend them.

## Decision

- **Name and classes.** The family is `RichEdit`. The public abstract base is
  `UiRichEdit` (assembly `Broiler.UI.RichEdit`); the standard concrete
  implementation is `StandardRichEdit` (assembly `Broiler.UI.RichEdit.Standard`).
  `UiRichEdit` derives directly from `UiElement` and does **not** subclass
  `UiEdit`.
- **Assemblies and dependency direction.**

  ```text
  Broiler.UI.RichEdit          -> Broiler.UI, Broiler.Graphics
  Broiler.UI.RichEdit.Standard -> Broiler.UI.RichEdit, Broiler.UI.Standard,
                                  Broiler.Input.*, Broiler.Graphics
  Broiler.UI.RichEdit.Dom      -> Broiler.UI.RichEdit, Broiler.Dom, Broiler.Dom.Html
  ```

- **DOM policy.** `Broiler.DOM` is **not** referenced by any core RichEdit runtime
  assembly. DOM/HTML interop lives only in the optional
  `Broiler.UI.RichEdit.Dom` adapter. The Broiler.UI dependency and OS-neutrality
  rules are **not** amended; the adapter is an additive optional assembly, which
  is the safer design the roadmap recommends. A future live DOM binding that
  needs DOM types inside a core assembly requires a new ADR and is out of
  first-release scope.
- **Multi-line plain text placement.** Both controls may hold multi-line plain
  text, but the boundary is explicit. `UiEdit` stays the lightweight entry
  control and may gain simple, opt-in plain multi-line text (compact chrome and
  forms; no paragraph model, no formatting, no rich clipboard). `UiRichEdit` owns
  document-grade multi-line editing: wrapping, vertical scrolling, the
  paragraph/run document model (ADR 0014), formatting commands (ADR 0015), and
  rich clipboard (ADR 0016). Applications that need formatting, paragraphs, or
  scrolling use `UiRichEdit`; they do not extend `UiEdit`.

## Consequences

- A new `UiSemanticRole.RichEdit` (ADR 0017) and the RichEdit assemblies must
  pass the same architecture tests as existing UI assemblies: no `*.Windows`, no
  native handles in public signatures, exactly one primary abstract class per
  abstraction assembly and one primary concrete class per `.Standard` assembly.
- `Broiler.UI.RichEdit.Standard` must reach a visible, selectable editor with no
  DOM reference; DOM import/export/sanitization is a strictly later, optional
  slice (roadmap Phase 5).
- Duplicating `UiEdit`'s single-line `int`-index text state into RichEdit is
  prohibited; RichEdit uses the document model in ADR 0014.
- This ADR satisfies the Phase 0 exit-gate item "dependency graph is approved."

# ADR 0017 - RichEdit Accessibility Semantics

**Status:** Approved for RichEdit Phase 1  
**Date:** 2026-07-04

## Context

RichEdit needs platform-neutral accessibility metadata for multi-line formatted
text before its public semantic surface is frozen (roadmap risk: "accessibility
is too flat"). The UI already has a semantic bridge (ADR 0008), a
`UiSemanticRole` enum with an `Edit` role but no rich-edit role
(`Broiler.UI/UiSemanticRole.cs`), a `UiSemanticTextInfo` value used by `UiEdit`,
and a caret-publication port `IUiTextInputHost`
(`PublishCaret` / `ClearCaret`). Phase 0 must decide the role and the
first-release semantic surface.

## Decision

- **New role.** Add `UiSemanticRole.RichEdit` rather than overloading `Edit`.
  This matches the one-role-per-control-type pattern (Button, Edit, CheckBox,
  ...) and lets hosts map RichEdit to the correct native text pattern.
- **Reuse `UiSemanticTextInfo`** for the first-release surface: a multi-line
  plain-text projection of the document as the value (redacted per privacy
  rules), caret position, selection start/length, and editable/read-only state.
  Per-run formatting-in-semantics (bold/italic/link on a range) is deferred
  beyond the first release; the role and text metadata are frozen now, and
  formatting metadata can extend later without changing the role.
- **Value projection.** The paragraph/run document (ADR 0014) maps to a linear
  text value with paragraph breaks, so screen readers and text services see
  multi-line content without knowing the internal model.
- **Caret geometry** is published through the existing
  `IUiTextInputHost.PublishCaret` / `ClearCaret`. Publishing caret rectangles for
  wrapped lines and active composition is a rendering-phase concern (roadmap
  Phase 6); the port is unchanged.
- **Privacy.** RichEdit is not a password control, but its semantic value,
  clipboard payloads, and selection are excluded from default telemetry and honor
  redaction (ADR 0008; component roadmap section 7.6).

## Consequences

- Adding `UiSemanticRole.RichEdit` is an additive enum change consumed by host
  UI Automation bridges in the application layer, not in UI runtime assemblies
  (ADR 0008).
- Because the first-release semantic surface equals `Edit`'s surface plus the new
  role, host bridges can support RichEdit with minimal new mapping while
  formatting semantics mature.
- Native UIA text-pattern mapping (text-range navigation, formatting attributes)
  is validated in the host/integration layer, not in RichEdit runtime assemblies.
- This resolves the Phase 2 open question ("is `UiSemanticRole.RichEdit` a new
  role or does `Edit` gain richer metadata") in favor of a new role.

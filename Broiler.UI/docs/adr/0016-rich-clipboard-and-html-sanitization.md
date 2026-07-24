# ADR 0016 - Rich Clipboard And HTML Sanitization

**Status:** Approved for RichEdit Phase 1  
**Date:** 2026-07-04

> **Placement update:** ADR 0018 moved the document model to
> `Broiler.Documents.Model` and HTML interchange to
> `Broiler.Documents.Html`. The shipped rich clipboard integration is the
> optional `Broiler.UI.RichEdit.Rtf` adapter; core RichEdit remains codec- and
> DOM-free.

## Context

RichEdit must support copy/cut/paste with a guaranteed plain-text path and an
optional rich path, and must never accept unsafe HTML. The core clipboard host
port `IUiClipboardHost` (`Broiler.UI/IUiClipboardHost.cs`) is text-only
(`TryGetText` / `SetText`). `Broiler.DOM` must not enter core UI assemblies
(ADR 0013). Phase 0 must fix the clipboard capability model, the sanitization
pipeline, and the supported HTML subset.

## Decision

- **Plain text always works.** Copy/cut/paste use the existing
  `IUiClipboardHost` and degrade to plain text when no rich capability is
  present; editing remains fully functional. This matches the component roadmap
  section 12.4 fallback matrix.
- **Rich clipboard is optional and lives in the adapter.** Rich read/write and
  HTML (de)serialization live in the optional `Broiler.UI.RichEdit.Dom`
  assembly; core `StandardRichEdit` does not reference DOM. A host that exposes
  only text yields plain-text behavior with no loss of core function. The core
  `IUiClipboardHost` is **not** widened to carry rich formats.
- **Paste sanitization pipeline (adapter).** rich payload -> parse to a
  `Broiler.Dom.Html` fragment -> filter to the supported subset -> convert to
  ADR 0014 document operations. Unsupported tags, attributes, styles, and URLs
  are dropped or downgraded predictably; the default fallback is plain text.
- **Supported HTML subset (authoritative; one-way import/export first):**
  `p`, `br`, `strong`/`b`, `em`/`i`, `u`, `s`, `span` (style-limited to the
  ADR 0014 inline styles), `a[href]`, `ul`, `ol`, `li`. Scripts, event handlers,
  external references, CSS layout, forms, tables, media, and Shadow DOM are never
  accepted.
- **URL policy.** Only `http`, `https`, and `mailto` links survive sanitization;
  other schemes (for example `javascript:` and `data:`) are dropped.

## Consequences

- Core RichEdit and Standard assemblies stay DOM-free (ADR 0013); round-trip HTML
  tests belong to the adapter (roadmap Phase 5).
- The HTML subset is deliberately the same style set as ADR 0014, so import and
  export cannot introduce styles the document model cannot represent.
- Diagnostics must not log clipboard payloads or document text by default
  (privacy; component roadmap section 7.6).
- This ADR closes the roadmap risk "clipboard accepts unsafe HTML" by making DOM
  sanitization and plain-text fallback the default path.

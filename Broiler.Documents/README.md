# Broiler.Documents

Broiler.Documents is the planned document-format codec component for Broiler.
It reads and writes rich-text interchange formats — **RTF first**, then
HTML/Markdown/DOCX — to and from Broiler's rich-text document model.

A document format is not new data: its decoded form is the rich-text document
model that RichEdit Phases 1-4 already built. This component is therefore a
*codec* layer over that model, mirroring how `Broiler.Media` decodes binary media
formats to and from typed buffers. It is **not** part of `Broiler.Media` (a
document is not a `MediaKind`) and **not** part of `Broiler.DOM` (RTF is not a DOM
tree; DOM is the dependency of the later HTML *codec*).

This folder currently contains the **Phase 0** charter and ADRs only — no runtime
assembly yet. See the roadmap at
`../docs/roadmap/broiler-documents-component.md`.

## Component constraints

- Target .NET 10 only.
- Do not add third-party runtime dependencies.
- Keep abstraction assemblies platform-neutral, safe-code compatible,
  trimming-friendly, and AOT-friendly.
- Put any OS-dependent code in OS-specific implementation projects only (none is
  expected for RTF).
- Do not add hidden global codec registration or module-initializer side effects;
  codecs are registered explicitly into a `DocumentCodecCatalog`.
- `Broiler.Documents.Model` and `Broiler.Documents` must not reference
  `Broiler.UI`, `Broiler.DOM`, `Broiler.Input`, or any `*.Windows` assembly.

## Planned runtime projects

- `Broiler.Documents.Model` — the platform-neutral rich-text document model,
  promoted out of `Broiler.UI.RichEdit` (ADR 0002); depends only on
  `Broiler.Graphics`.
- `Broiler.Documents` — codec contract, catalog, signature probe, format
  descriptors, read/write options, limits, diagnostics (ADR 0003).
- `Broiler.Documents.Rtf` — the first codec (RTF reader + writer).
- `Broiler.Documents.Html` *(later)* — HTML fragment codec; references
  `Broiler.DOM` / `Broiler.Dom.Html`.
- `Broiler.Documents.Markdown` *(later, optional)* — CommonMark subset codec.

Matching dependency-free console test projects live beside each runtime project
(the `Broiler.Media` pattern).

## Phase 0 contents

- [Phase 0 Record](docs/phase-0.md)
- [ADR Index](docs/adr/README.md)
  - [ADR 0001: Component Topology And Consumption Policy](docs/adr/0001-component-topology-and-consumption-policy.md)
  - [ADR 0002: Document Model Ownership And Promotion (Path A)](docs/adr/0002-document-model-ownership-and-promotion.md)
  - [ADR 0003: Codec Contract And Signature Probe](docs/adr/0003-codec-contract-and-signature-probe.md)
  - [ADR 0004: Document Read Limits And RTF Sanitization Policy](docs/adr/0004-document-read-limits-and-rtf-sanitization.md)
  - [ADR 0005: RTF First-Release Subset And Text Encoding](docs/adr/0005-rtf-first-release-subset-and-text-encoding.md)

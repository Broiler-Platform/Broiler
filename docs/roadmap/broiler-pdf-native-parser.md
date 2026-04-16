# Roadmap: Broiler.Pdf Native PDF Parser

> **Status**: Active — M0 initiated 2026-04-16  
> **Tracking issue**: Initiate Milestone 0 (M0) for Native PDF Parser

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Project Scope and Objectives](#2-project-scope-and-objectives)
3. [Current State and Target Outcome](#3-current-state-and-target-outcome)
4. [Core Features and Parsing Requirements](#4-core-features-and-parsing-requirements)
5. [Proposed Architecture and Module Interfaces](#5-proposed-architecture-and-module-interfaces)
6. [Timeline and Key Milestones](#6-timeline-and-key-milestones)
7. [Integration Points Within Broiler](#7-integration-points-within-broiler)
8. [Extensibility and Maintenance Considerations](#8-extensibility-and-maintenance-considerations)
9. [Risk Assessment and Mitigation Strategies](#9-risk-assessment-and-mitigation-strategies)
10. [Success Criteria](#10-success-criteria)
11. [Open Questions](#11-open-questions)

---

## 1. Executive Summary

`Broiler.Pdf` currently converts PDFs to Word documents by delegating PDF parsing
to the external `PdfPig` package. This roadmap defines a phased plan to replace
that dependency with an in-house, read-focused PDF parser that is designed for
Broiler's needs: deterministic parsing, controllable performance, predictable
extensibility, and tighter integration with Broiler's rendering and CLI
pipelines.

The recommended strategy is **incremental and compatibility-first**:

- keep the existing PDF-to-DOCX user workflow stable while the native parser is
  developed behind internal abstractions;
- target **core PDF 1.4–1.7 document parsing** first, with explicitly deferred
  support for advanced authoring scenarios;
- build the parser as layered components so text extraction, layout extraction,
  metadata inspection, and future rendering features can reuse the same
  primitives.

The first production goal is not "support every PDF". It is to ship a
well-tested parser that reliably handles the subset of PDFs most relevant to
Broiler's document-conversion and content-extraction scenarios.

---

## 2. Project Scope and Objectives

### 2.1 Objectives

1. **Own the parsing pipeline** inside `Broiler.Pdf`, reducing dependence on
   third-party PDF parsing behavior and release cycles.
2. **Preserve existing conversion workflows** exposed by `Broiler.Pdf` and the
   `Broiler.Cli --convert-pdf` compatibility wrapper.
3. **Create reusable parsing foundations** for future Broiler features such as
   structured extraction, HTML conversion, diagnostics, and native rendering.
4. **Establish explicit compliance boundaries** so unsupported PDF features fail
   clearly instead of producing silent corruption.
5. **Design for incremental expansion** rather than a monolithic one-shot parser.

### 2.2 In Scope (Phase 1–3)

- Cross-reference table and cross-reference stream parsing
- Indirect object parsing and object resolution
- Trailer parsing and document catalog discovery
- Page tree traversal
- Stream decoding for the most common filter pipelines
- Text extraction from content streams
- Basic font handling sufficient for text decoding and layout extraction
- Geometric extraction of glyph positions, page boxes, images, and metadata
- Parser diagnostics, validation, and failure reporting

### 2.3 Explicitly Out of Scope for the Initial Release

- PDF creation or editing
- Full AcroForm authoring and JavaScript execution
- Digital signature validation
- Rich media, embedded 3D, and multimedia annotations
- Complete encryption support beyond basic detection/reporting
- Pixel-perfect PDF rendering on day one

These may be added later, but they should not delay the first native parser
release.

---

## 3. Current State and Target Outcome

### 3.1 Current State

- `Broiler.Pdf` is a standalone application that converts PDF input into DOCX
  output.
- PDF parsing currently depends on `PdfPig`.
- `Broiler.Cli` shells out to `Broiler.Pdf` for `--convert-pdf`, so the current
  integration boundary is already process-based and stable.
- Existing tests focus on conversion outcomes rather than parser internals.

### 3.2 Target Outcome

At the end of this roadmap, `Broiler.Pdf` should provide:

- a native parser that can open and validate common PDF documents;
- a stable internal API for document, page, object, and content-stream access;
- extraction services for text, layout, and resource metadata;
- compatibility with the current CLI/document-conversion experience; and
- a path to future HTML/rendering integrations without rewriting the parser core.

---

## 4. Core Features and Parsing Requirements

### 4.1 Compliance Target

The parser should initially target the **widely used read-only subset of PDF
1.4–1.7**, with selective support for later constructs when they appear in
real-world inputs.

Minimum requirements:

- file header detection and version reporting;
- incremental update awareness;
- cross-reference table and cross-reference stream support;
- indirect object parsing for dictionaries, arrays, names, strings, numbers,
  booleans, nulls, and streams;
- object stream support;
- page tree traversal with inherited resources and page boxes;
- content stream tokenization and operator dispatch;
- support for common stream filters such as `FlateDecode`, with extension points
  for additional decoders;
- text-showing operators, graphics-state tracking, and coordinate transforms.

### 4.2 Functional Capabilities

The initial native parser should support these use cases:

| Capability | Priority | Notes |
|---|---|---|
| Open document and enumerate pages | P0 | Foundation for all other scenarios |
| Extract plain page text | P0 | Needed for current conversion baseline |
| Extract positioned text fragments | P0 | Needed for layout-preserving conversion |
| Read document metadata | P1 | Title, author, producer, creation dates |
| Extract embedded image references and bounds | P1 | Improves current preserve-layout output |
| Detect unsupported/encrypted/corrupt documents | P0 | Must fail explicitly |
| Emit parser diagnostics | P1 | Useful for debugging and future tooling |
| Structured logical extraction | P2 | Tables, reading order, tagged PDF hints |

### 4.3 Non-Functional Requirements

- **Performance**: avoid full-file materialization when not needed; resolve page
  resources lazily where possible.
- **Memory efficiency**: use pooled buffers and span-based parsing in hot paths.
- **Determinism**: identical inputs should produce identical object graphs and
  diagnostics.
- **Security**: defend against malformed files, deeply nested objects, inflated
  streams, and recursion bombs.
- **Observability**: expose warnings, unsupported feature flags, and parse
  timings in a structured way.

---

## 5. Proposed Architecture and Module Interfaces

### 5.1 Architectural Principles

1. **Layered pipeline** — tokenize bytes, parse objects, resolve document
   structure, then interpret content streams.
2. **Lazy resolution by default** — do not decode every object or stream up
   front.
3. **Immutable parsed models where practical** — reduce accidental mutation and
   simplify debugging.
4. **Narrow public surface** — keep low-level parser mechanics internal until
   the API stabilizes.
5. **Feature gates for advanced support** — optional capabilities should plug in
   without distorting the core model.

### 5.2 Suggested Internal Modules

| Module | Responsibility |
|---|---|
| `Broiler.Pdf.IO` | Random-access byte source, buffering, bounds checks |
| `Broiler.Pdf.Syntax` | Tokenizer, primitive parsing, object/stream grammar |
| `Broiler.Pdf.Structure` | XRef resolution, trailers, catalog, page tree |
| `Broiler.Pdf.Filters` | Stream decoder registry (`FlateDecode`, etc.) |
| `Broiler.Pdf.Content` | Content stream parser, operators, graphics state |
| `Broiler.Pdf.Fonts` | Font dictionaries, encodings, text decoding |
| `Broiler.Pdf.Extraction` | Text/layout/image extraction services |
| `Broiler.Pdf.Diagnostics` | Warnings, validation errors, trace events |

These can remain namespaces inside one assembly at first, then split only if
size or reuse justifies it.

### 5.3 Suggested Service Interfaces

The initial design should code against a few stable abstractions:

```csharp
public interface IPdfDocumentParser
{
    PdfOpenResult Open(string path, PdfOpenOptions? options = null);
}

public interface IPdfDocument
{
    PdfVersion Version { get; }
    IReadOnlyList<IPdfPage> Pages { get; }
    PdfMetadata Metadata { get; }
    IReadOnlyList<PdfDiagnostic> Diagnostics { get; }
}

public interface IPdfPage
{
    int Number { get; }
    PdfRectangle MediaBox { get; }
    PdfTextPage ExtractText();
    PdfLayoutPage ExtractLayout();
}

public interface IContentStreamInterpreter
{
    PdfLayoutPage Interpret(IPdfPage page);
}
```

These interfaces intentionally align with Broiler's current needs:
document-level open, page iteration, text extraction, and layout extraction.

### 5.4 Parsing Strategy

Recommended implementation order:

1. **Byte-level reader**
2. **Primitive/object parser**
3. **XRef/trailer resolver**
4. **Document catalog + page tree**
5. **Content stream parser**
6. **Font/text decoding**
7. **Image/resource extraction**

This sequence keeps each milestone independently testable and reduces the risk
of jumping straight into content interpretation before the document model is
sound.

---

## 6. Timeline and Key Milestones

The timeline below assumes part-time, incremental development alongside ongoing
Broiler work. Adjust dates if the effort becomes a dedicated project.

| Milestone | Target | Deliverables |
|---|---|---|
| M0: Discovery and API design | Weeks 1-2 | Sample corpus, compliance boundary, architecture sketch, parser abstractions |
| M1: Syntax and object model | Weeks 3-6 | Tokenizer, primitive parser, indirect objects, xref/trailer parsing |
| M2: Document structure | Weeks 7-10 | Catalog loading, page tree traversal, stream decoding, diagnostics |
| M3: Text extraction parity | Weeks 11-14 | Text operators, font decoding baseline, page text extraction, parity tests against current converter |
| M4: Layout extraction parity | Weeks 15-18 | Positioned glyph extraction, image bounds/resources, preserve-layout converter integration |
| M5: Hardening and rollout | Weeks 19-22 | Corpus regression suite, malformed-file handling, performance profiling, `PdfPig` fallback removal plan |

### 6.1 Milestone Details

#### M0 — Discovery and API Design

- Assemble a representative PDF corpus: simple text PDFs, multi-page reports,
  scanned/image-heavy files, object-stream PDFs, malformed PDFs, encrypted PDFs.
- Define supported versus unsupported features.
- Add parser interfaces and internal seams so the current converter can later
  switch implementations without changing its CLI contract.

### 6.2 M0 Execution Tracker

This roadmap issue is the central coordination point for M0. Keep the checklist
and dependency notes below updated as work lands so follow-up issues can link
back here.

#### 6.2.1 Core Task Breakdown

- [x] Document the target parser layering and recommended module boundaries
  (`IO`, `Syntax`, `Structure`, `Filters`, `Content`, `Fonts`, `Extraction`,
  `Diagnostics`).
- [x] Introduce parser/document/page seams inside `Broiler.Pdf` so the current
  CLI contract stays stable while the backing parser changes.
- [ ] Create a committed sample corpus manifest covering:
  - simple text PDFs;
  - multi-page reports;
  - scanned/image-heavy PDFs;
  - object-stream PDFs;
  - malformed PDFs;
  - encrypted PDFs.
- [ ] Add syntax-level test fixtures that can exercise the future native parser
  independently of the DOCX conversion path.
- [ ] Capture baseline parity expectations between the current `PdfPig` path and
  the future native parser for text extraction and preserve-layout extraction.

#### 6.2.2 M0 Deliverables, Dependencies, and Follow-up Issues

| Workstream | M0 deliverable | Immediate dependency | Suggested follow-up issue |
|---|---|---|---|
| Parser seam | `IPdfDocumentParser` / document / page abstractions wired into `Broiler.Pdf` | None | Implement native byte reader and primitive parser |
| Corpus | Representative fixture inventory with ownership and licensing notes | Decide where PDF fixtures live in-repo | Add corpus files + regression harness |
| Support boundary | Explicit supported/deferred feature list for the first native-parser target | Corpus categories to validate against | Convert support boundary into diagnostics and docs |
| Validation | Targeted parser tests plus existing compatibility tests | Parser seam in place | Add M1 syntax/xref tests |

#### 6.2.3 Initial Supported Boundary for Native Parser Work

Treat the following as the M0 baseline contract for upcoming implementation
work:

**Initially in scope**

- common read-only PDF 1.4–1.7 documents;
- standard xref tables before xref streams/object streams;
- unencrypted text/image pages needed by current DOCX conversion modes; and
- explicit diagnostics when the parser hits unsupported structures.

**Explicitly deferred beyond M0**

- document editing/writing;
- forms, annotations, tagged PDF, signatures, and incremental-save authoring
  features;
- full CMap/font coverage beyond the first text-extraction baseline; and
- removing `PdfPig` before native parsing reaches conversion parity.

#### M1 — Syntax and Object Model

- Implement primitive lexical parsing.
- Parse indirect objects and streams.
- Resolve cross-reference tables/streams and trailers.
- Add unit tests for malformed syntax, offsets, and object reuse.

#### M2 — Document Structure

- Load the document catalog and page tree.
- Resolve inherited resources and page boxes.
- Implement filter decoding and stream-length validation.
- Produce structured diagnostics for unsupported document features.

#### M3 — Text Extraction Parity

- Interpret text operators (`BT`/`ET`, `Tf`, `Td`, `Tm`, `Tj`, `TJ`, etc.).
- Decode text using standard/simple font encodings first.
- Compare extracted text against current `PdfPig`-backed output on the corpus.
- Switch non-layout conversion to the native parser when parity is acceptable.

#### M4 — Layout Extraction Parity

- Track graphics state and coordinate transforms.
- Extract positioned glyph runs and embedded image placements.
- Update `--preserve-layout` conversion to use native extraction.
- Add regression tests for bounding boxes, reading order, and mixed text/image pages.

#### M5 — Hardening and Rollout

- Profile large-file performance and memory behavior.
- Add parse limits for recursion depth, stream inflation, and object counts.
- Remove or isolate the `PdfPig` dependency once the native path is stable.
- Document known unsupported PDF features and operational guardrails.

---

## 7. Integration Points Within Broiler

### 7.1 `Broiler.Pdf` CLI

The command-line contract should remain stable:

- `--input`
- `--output`
- `--preserve-layout`

Users should not need to know when the underlying parser implementation
switches.

### 7.2 `Broiler.Cli` Compatibility Wrapper

`Broiler.Cli --convert-pdf` already delegates to the standalone `Broiler.Pdf`
process. This is an ideal rollout boundary:

- keep `Broiler.Cli` unchanged during parser development;
- validate native-parser output inside `Broiler.Pdf.Tests`;
- switch the implementation under the existing CLI behavior once ready.

### 7.3 Future Broiler Integrations

The parser should be designed so it can later support:

- PDF-to-HTML conversion for visual inspection;
- PDF diagnostics/reporting in developer tooling;
- extraction services for search indexing or content analysis;
- potential future rendering bridges if Broiler adds native PDF display.

---

## 8. Extensibility and Maintenance Considerations

### 8.1 Extensibility

Design extension points up front for:

- additional stream decoders;
- alternate font decoders and CMap support;
- annotation/form extraction;
- tagged PDF and logical structure parsing;
- parser instrumentation hooks for benchmarks and diagnostics.

### 8.2 Test Strategy

The roadmap should be backed by three test layers:

1. **Syntax tests** for primitives, xref data, streams, and malformed files
2. **Golden extraction tests** for text/layout output on a curated PDF corpus
3. **Compatibility tests** proving current CLI/DOCX behavior remains stable

### 8.3 Maintenance Rules

- Keep the PDF specification mapping documented near the code.
- For each newly supported operator/filter/structure, add both positive and
  malformed-input tests.
- Prefer additive feature support over parser-wide rewrites.
- Track unsupported features explicitly in diagnostics and roadmap updates.

---

## 9. Risk Assessment and Mitigation Strategies

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| PDF specification complexity expands scope | High | High | Ship a defined subset first; defer advanced features explicitly |
| Text decoding is incorrect for non-trivial fonts/CMaps | High | Medium | Start with common encodings, maintain a sample corpus, add font-specific regression cases |
| Large or malicious files cause excessive memory/CPU use | High | Medium | Add parser limits, lazy decoding, pooled buffers, and decompression safeguards |
| Native parser output diverges from current conversion behavior | Medium | Medium | Run side-by-side corpus comparisons before switching each feature path |
| Architecture becomes too rigid for future rendering needs | Medium | Medium | Keep content interpretation and extraction separated from syntax/object parsing |
| Team underestimates validation needs | Medium | High | Build the sample corpus and golden tests during M0, not after feature work |

---

## 10. Success Criteria

The roadmap should be considered successfully executed when:

- `Broiler.Pdf` can parse and convert the agreed-upon sample corpus without
  relying on `PdfPig`;
- plain-text conversion output is at or above current quality for common PDFs;
- `--preserve-layout` uses native positioned extraction for the supported subset;
- unsupported PDFs fail with actionable diagnostics instead of silent data loss;
- parser performance and memory use are acceptable for typical CLI workloads; and
- the architecture is documented well enough for contributors to add filters,
  operators, and resource decoders safely.

---

## 11. Open Questions

These questions should be resolved during M0:

1. Should encrypted PDFs be rejected outright in v1, or should password-based
   opening be included in scope?
2. What percentage of the existing/future Broiler PDF corpus uses object streams
   or advanced CMap-based fonts?
3. Should the first native release keep an opt-in fallback to `PdfPig` for
   unsupported files, or should the cutover be all-or-nothing?
4. Is HTML output a near-term requirement, or should the parser optimize only
   for DOCX conversion first?
5. Do we want public parser APIs immediately, or should the parser remain
   internal until two Broiler features consume it?

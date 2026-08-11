# PDF Support Roadmap for Broiler.Documents

**Status:** Proposed  
**Component:** `Broiler.Documents`  
**Target package:** `Broiler.Documents.Pdf`  
**IP/legal review baseline:** 2026-08-11

The IP and licensing requirements below are engineering release controls, not a
legal opinion. Patent freedom-to-operate, reciprocal-license decisions, and
target-jurisdiction questions require approval by the project's qualified legal
reviewer before the affected feature ships.

## 1. Recommended end state

Create `Broiler.Documents.Pdf`, matching the existing `.Docx`, `.Rtf`, and
`.Html` naming, with:

- `PdfDocumentCodec.Read`: best-effort logical extraction into
  `RichTextDocument`;
- `PdfDocumentCodec.Write`: deterministic pagination and PDF export from
  `RichTextDocument`;
- descriptor name `PDF`, MIME type `application/pdf`, and extension `.pdf`;
- explicit registration in `DocumentCodecCatalog`; and
- no third-party runtime dependency, global registry, module initializer, UI
  dependency, DOM dependency, or platform-specific dependency.

PDF import and export are separate capability tracks. PDF is fixed-layout,
while `RichTextDocument` is a normalized paragraph/run model. V1 must therefore
not promise source-faithful round trips or "preserve layout" conversion.

### 1.1 V1 scope

Include:

- feature-based reading of the declared ISO 32000-1:2008/PDF 1.7 subset,
  including older PDF 1.x files that use those constructs;
- recognition of PDF 2.x headers while processing only explicitly supported
  ISO 32000-1:2008 constructs; this is not ISO 32000-2 conformance and includes
  no PDF 2.0-only feature or Adobe extension level;
- logical text, basic styling, safe links, metadata, and safely placeable inline
  images;
- new PDF 1.7 output for broad compatibility, subject to a qualified review that
  the planned reader/writer falls within Adobe's ISO 32000-1 public patent
  license definitions or has separate authority, and to the project's exact
  file-validity and supported-feature statements; and
- Unicode text, embedded/subset fonts, links, inline images, lists, paragraph
  formatting, deterministic pagination, and structured diagnostics.

Explicitly exclude from V1:

- native PDF viewing or page rasterization;
- source-preserving editing or incremental saving;
- OCR;
- password-encrypted input beyond detection and diagnosis;
- JavaScript, Launch actions, attachments, rich media, or external-resource
  fetching;
- AcroForm editing, signature validation, PDF/A, or PDF/UA claims;
- full tagged-PDF reconstruction, Type 3 fidelity, PDF 2.0-only constructs,
  JPX/JBIG2/CCITT support, four-component CMYK/YCCK JPEG conversion, and
  arithmetic-coded, lossless, hierarchical, JPEG-LS, or JPEG XR decoding;
- PDF-writer use or extension of the existing managed JPEG encoder; and
- HTML/CSS print-to-PDF.

## 2. Historical reset and cleanup

Two retired implementations exist, but neither is a valid baseline:

- `c45df220` through `e7f6bba0`: a third-party PdfSharp HTML-to-PDF adapter
  based on .NET Framework and System.Drawing;
- `3ed7b982` through `12d055b3`: a PdfPig/OpenXML PDF-to-DOCX application. Its
  "native parser" was only a PdfPig adapter, and its generated corpus is not an
  authoritative test oracle.

Do not restore code, APIs, tests, assets, or fixtures from either lineage. No Git
history rewrite is required merely to isolate the new implementation, but Phase
0 must separately audit the continuing redistribution of historical repository
content. Obtain authority or follow repository policy to remove/rewrite any
unlicensed, confidential, or otherwise restricted historical material. History
is not an approved implementation source. Record the lawful source of every new
implementation, table, mapping, fixture, and generated oracle; do not copy from
the retired lineages or from reference renderers merely because they remain
locally accessible.

Phase 0 must remove or rewrite these current remnants:

- external-process conversion, `BROILER_PDF_APP`, `--preserve-layout`, and the
  nonexistent source fallback in `src/Broiler.Cli/Program.cs`;
- environment-dependent
  `src/Broiler.Cli.Tests/PdfToWordConverterTests.cs`;
- the unresolved standalone-app decision in `docs/ROADMAP.md`;
- the obsolete proposal warning in `Broiler.Documents/docs/roadmap.md`; and
- standalone-app assumptions in the multithreading documentation and
  `CLAUDE.md`.

Keep unrelated current infrastructure such as PDF MIME classification,
binary-resource handling, WPT cases, PdfJS benchmarks, and HTML link-rectangle
generation. These may remain test or benchmark infrastructure, but they are not
approved sources for PDF implementation code or data.

## 3. Component ownership

| Owner | Shared work placed there | Must remain PDF-specific |
|---|---|---|
| `Broiler.Documents` | Extensible format options, replayable/random-access input, cancellation, common resource services, diagnostic locations, common byte/resource budgets, metadata sidecar, release-level IP/provenance register | Nothing involving xrefs, PDF objects, operators, CMaps, or encryption |
| `Broiler.Documents.Model` | Only cross-format semantics proven useful to DOCX/RTF/HTML too: possible page breaks, justification, direction | Page coordinates, page boxes, annotations, PDF dictionaries, positioned glyphs |
| New `Broiler.Documents.Pagination` | Headless rich-text line layout, page settings, margins, page breaks, list markers, inline images, link rectangles, paged output | PDF serialization |
| `Broiler.Graphics` | Explicit font-face resources, shaped/positioned glyph runs, deterministic font resolver, technical embedding/subsetting enforcement, generally reusable licensed font/shaping assets, neutral page-scene resources | PDF font dictionaries, PDF encodings, Standard 14 metrics, predefined CMaps, and character collections |
| `Broiler.Media.Image` | PDF-neutral image contracts and caller-composed codec/resource services | PDF filter dictionaries, predictors, masks, Decode arrays, and resource resolution |
| `Broiler.Media.Image.Managed` | The approved managed JPEG subset and its code/data provenance, SBOM, notices, tests, and human review; later shared codec implementations only under separately approved tracks | PDF filter dictionaries, predictors, masks, Decode arrays, and PDF resource resolution |
| `Broiler.DOM` | No work required | PDF objects and tagged structure are not DOM nodes |
| Existing `Broiler.Layout`/HTML/CSS | Later HTML print media, fragmentation, `@page`, link rectangles | The document codec must not depend on this DOM/CSS layout engine |
| `Broiler.Documents.Pdf` | Syntax, object store, xrefs, filters, security handler, page tree, resources, content operators, PDF encodings/CMaps/Standard 14 data, extraction heuristics, model projection, serialization, and notices for PDF-only assets | — |

Containment rule: a shared addition must have a PDF-neutral name, owner-local
tests, and a second non-PDF consumer. Otherwise it stays internal to
`Broiler.Documents.Pdf`.

Legal containment follows code containment: the implementation component that
distributes a font, CMap, mapping table, ICC profile, codec, or other external
asset owns its license record, notice file, SBOM entry, and human approval.
Graphics owns only generally reusable font/shaping assets. PDF-only encodings,
Standard 14 metrics, predefined CMaps, and character collections remain owned
and noticed by `Broiler.Documents.Pdf` unless a real non-PDF consumer justifies
promotion. PDF filter semantics remain there, while codec code/data/notices stay
with their Media implementation; legal uncertainty is not a reason to duplicate
shared codec code inside PDF.

```text
Broiler.Documents.Model ──> Broiler.Graphics
Broiler.Documents       ──> Broiler.Documents.Model

Broiler.Documents.Pagination
    ├──> Broiler.Documents.Model
    └──> Broiler.Graphics

Broiler.Documents.Pdf
    ├──> Broiler.Documents
    ├──> Broiler.Documents.Model
    ├──> Broiler.Documents.Pagination   [write track]
    ├──> Broiler.Graphics
    └──> Broiler.Media.Image            [abstraction only]
```

## 4. Phase summary

| Phase | Goal | Dependency | Estimated effort |
|---|---|---|---:|
| 0 | Reset authority, scope, IP/legal ADRs, cleanup | None | 2–3 engineer-weeks |
| 1 | Shared contracts, approved corpus, CI/license foundation | Phase 0 | 4–6 |
| 2 | PDF syntax and object store | Phase 1 | 4–6 |
| 3 | Xrefs, structure, filters, security detection | Phase 2 | 4–6 |
| 4 | Logical text/image/link import | Phase 3 | 6–10 |
| 5 | Read-preview integration | Phase 4 | 2–4 |
| 6 | Shared pagination/font/export foundation | Phase 1; parallel with 2–5 | 8–14 |
| 7 | Deterministic PDF writer | Phases 3 and 6 | 6–10 |
| 8 | Hardening, packaging, legal and stable-release evidence | Phases 5 and 7 | 5–9 |

Estimates assume one experienced contributor. With parser and export-foundation
work running in parallel, a read preview is roughly 22–35 engineer-weeks, a
read/write preview 36–59, and a hardened release 41–68. These estimates exclude
waiting time for standards acquisition, outside legal review, patent-family
research, permissions, or commercial-license negotiation.

## 5. Phase 0 — Scope, standards/IP authority, and re-baseline

### 5.1 Deliverables

- Add ADRs covering:
  - PDF product scope and dependency ownership;
  - security, active-content, resource, and encryption policy;
  - logical import versus fixed-layout export;
  - deterministic font and pagination policy;
  - IP, standards access, implementation provenance, asset licensing, patent
    declarations, target distribution jurisdictions, and reciprocal-license
    policy; and
  - conformance, trademark, certification, and non-endorsement claims.
- Establish a versioned IP/licensing register. Each feature entry records:
  - exact standard, edition, part, profile, amendment, operator/filter mapping,
    and whether Broiler reads, writes, decodes, encodes, or only preserves it;
  - the lawfully obtained specification source and its copyright/use terms;
  - code and data provenance, SPDX expression or full terms, required notices,
    asset redistribution rights, generated-document attribution/license-copy/
    naming/source obligations, and any dependency patent grant;
  - the ISO/IEC/ITU declaration-database URL and review date, licensing option,
    reciprocity, known patent-family/status review, and approved distribution
    jurisdictions;
  - responsible component, reviewer, approval date, and pending/approved/rejected
    status.
- Require worldwide clearance for an unrestricted public package feed. A
  narrower jurisdiction approval is valid only when the actual distribution
  channel is technically and contractually territory-limited and that limit is
  recorded and enforced; documentation alone is not a distribution control.
- Seed the register from the official
  [ISO patent policy](https://www.iso.org/iso-standards-and-patents.html) and
  current ISO/ITU records. A missing declaration, an old declaration, a
  royalty-free objective, widespread adoption, or an open-source copyright
  license must never be treated as proof of patent freedom.
- Record Adobe's
  [ISO 32000-1:2008 public patent license](https://www.adobe.com/pdf/pdfs/ISO32000-1PublicPatentLicense.pdf),
  including its Adobe-owned-essential-claims scope, exclusion of updated
  specifications, patent-retaliation language, and lack of non-infringement
  warranty. A qualified reviewer must determine whether the planned partial
  reader/writer satisfies the license's `Compliant Implementation` and
  `Essential Claim` coverage conditions; mere project acceptance does not
  establish coverage. If coverage is uncertain, block the affected feature or
  obtain separate authority. The license is not authority for ISO 32000-2/PDF
  2.0.
- Establish the standards-source rule: cite clauses rather than reproducing
  standard prose; do not commit ISO/ITU publications, diagrams, sample code,
  official test material, or substantial tables without redistribution rights;
  record the source and approved legal basis for unavoidable normative
  constants.
- Establish the user-content rule: opening or converting a document grants
  Broiler no reuse or republication right. Callers are responsible for authority
  to extract, copy, transform, and preserve input text, metadata, fonts, images,
  profiles, and attachments; Broiler does not automatically republish input
  assets or represent that caller authorization establishes legal ownership.
- Establish the public-surface rule:
  - initially public: `PdfDocumentCodec`, `PdfReadOptions`, `PdfWriteOptions`,
    `PdfLimits`, and documented diagnostics;
  - keep `PdfObject`, xref entries, page dictionaries, operator tokens, and
    parser internals non-public until another real consumer justifies them.
- Publish a feature matrix with behavior states `supported`,
  `detected-but-skipped`, and `rejected`, plus an independent legal-clearance
  column. `Supported` is invalid while clearance is pending.
- Create a new independent corpus manifest containing:
  - SHA-256, exact source/revision URL and acquisition date, author/rightsholder,
    provenance, SPDX expression or full license terms, and attribution;
  - redistribution, public-CI, modification/fuzzing, and generated-derivative or
    screenshot rights;
  - embedded font, image, CMap, and ICC-profile provenance/rights;
  - PII/privacy classification, malicious/CVE classification, quarantine,
    retention, and redistribution approval; and
  - feature tags, expected text/diagnostics, oracle and modification history.
- Define an approved-source and similarity-review record for new PDF code and
  data. The old PdfSharp/PdfPig lineages, PDF.js, PDFium, Poppler, MuPDF, and
  other independent implementations may be black-box test oracles under their
  terms, but their code, tables, fixtures, and generated data are not sources to
  copy.
- Remove the obsolete CLI/application authority listed above.
- Re-baseline all current Documents tests before implementation.

### 5.2 Exit gate

- Scope, dependency graph, IP/licensing ADR, register schema, and claims policy
  are approved.
- A qualified reviewer has determined and recorded Adobe ISO 32000-1 patent-
  license coverage for the planned V1 reader/writer, or the affected capability
  remains blocked under separate-authority review; PDF 2.x recognition is
  documented as construct tolerance, not ISO 32000-2 conformance.
- Old code and fixtures are formally classified as non-reusable, and no unclear
  historical artifact remains in the working tree or approved-source list.
- The repository-history redistribution audit is resolved: restricted historical
  material has documented authority or has been removed/rewritten under the
  repository's approved history-rewrite policy.
- Required standards are lawfully accessible to implementers without copying
  restricted publications into the repository.
- The intended distribution channel is worldwide-cleared or has an enforceable,
  approved territory limitation.
- No feature is described as patent-free, unconditionally royalty-free,
  certified, endorsed, or fully conforming without specific recorded authority.
- No "preserve layout" or standalone-app claim remains.
- Existing Documents and Writer behavior is unchanged.
- Architecture guards prohibit PDF types in shared assemblies.

## 6. Phase 1 — Shared contracts, approved corpus, and engineering foundation

### 6.1 `Broiler.Documents` work

- Resolve the mismatch where `DocumentReadOptions` and `DocumentWriteOptions`
  are sealed even though ADR 0003 anticipates format-specific options.
- Replace `DecodeEmbeddedObjects` with an explicit caller-composed, bounded
  image/resource service.
- Define a PDF-neutral resource-use policy contract in the shared
  Documents/resource and Graphics/font layers. For each resource and proposed
  operation it carries caller-provided identity/provenance, permitted
  extract/persist/preserve/embed/subset/modify/distribute uses, target output,
  required generated-document obligations, and a stable allow/deny decision.
  Supplying input and invoking `Read` requests only the bounded, transient
  parsing and decoding needed for that read; it neither asserts Broiler ownership
  nor authorizes durable extraction or reuse. Unknown or absent dispositions for
  extraction, persistence, output copying, or redistribution default to deny.
  Broiler enforces technical restrictions and caller policy; it does not
  determine legal ownership.
- Add a `DocumentInput`-style abstraction that:
  - defines stream ownership;
  - replays probe bytes on non-seekable streams;
  - supports bounded materialization for random access;
  - allows caller-provided spooling rather than ambient temporary-file access;
    and
  - works in memory-only WebAssembly environments.
- Add async/cancellation overloads without removing the synchronous contract.
- Add optional, format-neutral diagnostic source locations and
  diagnostic-count limits.
- Add generic encoded/decoded byte and resource budgets only where multiple
  codecs can use them.
- Put PDF-specific limits into `PdfLimits`, including:
  - object and indirect-reference count;
  - xref section and incremental-revision count;
  - page-tree and container depth;
  - per-stream and total decoded bytes;
  - filter-chain depth and expansion ratio;
  - operators per page and Form XObject recursion;
  - CMap entries and `usecmap` depth; and
  - font bytes/glyphs and image pixels.
- Add a format-neutral metadata sidecar to read/write results rather than
  placing PDF Info/XMP objects into the model.

### 6.2 Model review

Promote only features with another immediate codec consumer. Likely candidates
are explicit page breaks, justified alignment, and text direction. Absolute
coordinates and fixed-layout objects are prohibited.

### 6.3 Test, corpus, license, and CI foundation

- Add path-filtered Documents/Graphics/Media jobs on Windows and Linux.
- Run Documents xUnit tests and explicitly invoke Graphics/Media console test
  runners.
- Add trim/AOT smoke tests and clean-feed package-consumption tests.
- Populate the Phase 0 corpus manifest. Keep only Broiler-authored synthetic or
  explicitly redistributable fixtures in-tree; a document-level license does
  not automatically clear embedded fonts, images, profiles, personal data, or
  rendered golden derivatives.
- Keep larger real-world/CVE corpora access-controlled and quarantined, fetched
  only by a pinned nightly job under their source terms. Do not publish, mirror,
  package, or cache ambiguous samples or their renders; recreate minimal
  synthetic fixtures when rights cannot be established.
- Create a test-tool license manifest with exact source/release, commit or
  version, SHA-256/signature, selected license, dependency/asset SBOM, notices,
  acquisition method, exact build flags, enabled codec/font/CMap/ICC inventory,
  patent-review status, approved CI jurisdictions/use scope, and whether a
  binary is cached or redistributed. Disable unneeded codecs and assets. This
  tool inventory complements, but never substitutes for, the product feature
  register.
- Require independent tools to execute out-of-process in CI and remain absent
  from product references, NuGet packages, applications, and release containers.
  Process isolation is not a redistribution safe harbor: any distributed CI
  image or cached binary must still satisfy its license and notice obligations.
- Make the existing `Broiler.Media.Image.Managed` JPEG decoder and encoder an
  immediate affected-component release gate, even if PDF V1 uses only decode or
  byte preservation. Audit Annex-derived quantization/Huffman data, IJG quality
  scaling, `JpegOptimalHuffman` and its documented libjpeg
  `jpeg_gen_optimal_table` lineage, implementation/test-vector provenance, and
  IJG/libjpeg attribution or notice applicability before PDF depends on it or
  the Media package is approved with these assets.
- Add component-local `THIRD_PARTY_NOTICES.md` and SBOM entries whenever
  Documents, Graphics, or Media incorporates, derives from, or distributes
  third-party source or generated code, algorithms, constants, tables/data, test
  vectors, dependencies, fonts, CMaps, profiles, or other assets. Inspect the
  resulting `.nupkg`/`.snupkg` contents rather than relying only on project
  metadata.
- Add `Broiler.Documents` and affected Media/Graphics packages to the repository
  human-review and public-publish approval gate.

### 6.4 Exit gate

- Non-seekable probe/read behavior is covered.
- Cancellation and resource ownership are documented.
- Tests prove that bounded transient parsing for a requested read is distinct
  from durable extraction/reuse and that unknown durable dispositions fail
  closed.
- Format-specific options require no PDF fields in base options.
- Shared additions have non-PDF consumer tests.
- Every in-tree fixture and generated golden has explicit redistribution and
  derivative-work approval.
- The test-tool manifest proves that no oracle is a product dependency and that
  redistributed CI artifacts satisfy notices and source obligations.
- Existing managed JPEG decoder/encoder code, tables, algorithms, and test-vector
  provenance and required attribution are resolved before `DCTDecode` is marked
  supported or the affected Media release is approved.
- Documents, Media, and Graphics package notices, SBOMs, provenance records, and
  human-review coverage match all third-party or derived material they
  incorporate or distribute.
- Existing codec API and conformance suites remain green.

## 7. Phase 2 — PDF syntax and object store

### 7.1 Deliverables

- Scaffold `Broiler.Documents.Pdf` and `Broiler.Documents.Pdf.Tests`.
- Add `%PDF-` signature probing, MIME/extension hints, and deterministic
  confidence.
- Parse the header version for diagnostics and feature gating. A PDF 2.x header
  never enables ISO 32000-2-only syntax or semantics in V1; unsupported
  constructs remain detected-and-skipped or rejected according to the matrix.
- Implement bounded tokenization for:
  - whitespace and comments;
  - integers and real numbers;
  - names and `#xx` escapes;
  - literal and hexadecimal strings;
  - arrays, dictionaries, booleans, and null; and
  - indirect objects/references and streams.
- Implement checked offset, length, generation, and allocation arithmetic.
- Implement classic xref tables, xref streams, object streams,
  hybrid-reference files, trailers, `startxref`, and incremental `/Prev`
  chains.
- Resolve the latest object revision deterministically.
- Use explicit stacks and cycle detection rather than unbounded recursion.
- Permit only narrowly documented recovery; do not scan arbitrary input until
  something resembles an object.
- Record the governing ISO 32000-1 clause and approved implementation provenance
  for each syntax family without reproducing restricted standard text.

### 7.2 Exit gate

- Every syntax construct has positive, truncated, malformed, and boundary
  tests.
- Classic, streamed, hybrid, object-stream, and incrementally updated fixtures
  resolve identically to independent tools.
- Every truncation point and deterministic byte mutation terminates within
  bounds.
- Parser implementation/provenance records contain no copied renderer code,
  tables, or unclear historical material.
- No model-projection or rendering concerns exist in this layer.

## 8. Phase 3 — Document structure, filters, and safe feature detection

### 8.1 Deliverables

- Load catalog, page tree, inherited resources, MediaBox/CropBox, rotation, and
  UserUnit.
- Resolve resources lazily and guard page/resource/Form XObject cycles.
- Implement PDF-owned stream filters:
  - Flate with PNG/TIFF predictors;
  - LZW only after the focused register entry confirms the exact PDF algorithm,
    implementation provenance, historic core-patent status, and absence of an
    unapproved proprietary variant or optimization;
  - ASCIIHex;
  - ASCII85;
  - RunLength; and
  - chained filters with per-stage and total budgets.
- Classify image filters before decoding content:
  - `DCTDecode`: only one- or three-component Huffman-coded baseline sequential
    (SOF0) and progressive (SOF2) JPEG is eligible for V1 decode after the Phase
    1 provenance/IP gate;
  - four-component CMYK/YCCK conversion and arithmetic-coded, lossless,
    hierarchical, JPEG-LS, and JPEG XR data are rejected in V1; and
  - `JPXDecode`, `JBIG2Decode`, and `CCITTFaxDecode` are detected-but-skipped
    with stable diagnostics pending their separate post-V1 approvals.
- Snapshot the official
  [T.81 declaration record](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=2633)
  for the supported JPEG modes.
  CMYK/YCCK color handling is not a separate codec patent clearance, and an
  open-source JPEG implementation license does not clear third-party standards
  patents.
- Parse Info and bounded XMP metadata without introducing a DOM dependency.
- Inventory annotations, outlines, forms, actions, embedded files, signatures,
  and encryption.
- Treat URI values as inert text and never fetch them.
- Reject encrypted input in V1 with a stable diagnostic. Never expose passwords
  or document content in diagnostics.
- Detect JavaScript, Launch, rich media, and attachments but never execute or
  instantiate them.

### 8.2 Exit gate

- Page counts, boxes, rotation, and inherited resources match independent
  tools.
- Filter chains have decompression-bomb and expansion-ratio tests.
- Flate, LZW, and eligible DCT modes have approved entries in the IP/licensing
  register before their behavior state becomes `supported`.
- Unsupported JPEG, JPX, JBIG2, and CCITT inputs are distinguished accurately;
  no generic "corrupt image" result hides a policy rejection.
- Cyclic or hostile graphs terminate predictably.
- Encrypted and active-content cases produce specific diagnostics without side
  effects.

## 9. Phase 4 — Logical import into `RichTextDocument`

### 9.1 Content interpretation

- Implement the graphics and text state needed for extraction:
  - `q`/`Q` and `cm`;
  - `BT`/`ET`;
  - font selection and text matrices;
  - character/word spacing, horizontal scale, leading, rise, and render mode;
  - `Tj`, `TJ`, quote operators, and positioning operators;
  - Form XObjects with bounded recursion; and
  - marked-content `ActualText`.
- Support the initial font subset:
  - Standard/simple Type 1 fonts;
  - embedded TrueType/OpenType fonts;
  - Type 0/CID fonts;
  - Encoding Differences; and
  - ToUnicode CMaps, `bfchar`, `bfrange`, and bounded `usecmap`.
- Register the provenance and redistribution terms of every non-code font asset.
  `Broiler.Documents.Pdf` owns/notices PDF-only Standard 14 metrics, encoding
  vectors, predefined CMaps, and character collections; Graphics owns only
  generally reusable shaping and glyph-mapping data unless another consumer
  justifies promotion. None is implicitly cleared merely because an algorithm
  or identifier is standardized.
- Treat an input PDF's embedded font program as document-scoped content. Parse
  it only through bounded services for the current document; do not install it,
  persist it in a cross-document cache, expose its bytes, bundle it as a
  fallback, or automatically carry it into a newly written PDF.
- Detect Type 3 and unsupported font programs; do not fake reliable text
  without a mapping.

### 9.2 Reading order and model projection

- Prefer valid `ActualText`.
- Group glyphs deterministically into words, lines, blocks, and paragraphs
  using baselines, direction, spacing, and columns.
- Document the heuristic and emit `pdf.import.reading-order-heuristic` whenever
  geometry rather than trustworthy logical information determined order.
- Map font family, size, weight, slant, color, decoration, alignment, spacing,
  links, and lists only when evidence is reliable.
- Remove subset prefixes from font family names using PDF metadata, not
  substring-based bold/italic guessing.
- Never retain hidden coordinate side channels in the rich-text model.

### 9.3 Images and links

- Preserve compatible DCT/JPEG resources byte-for-byte only when the shared
  resource-use policy explicitly allows the operation, no decode, color
  conversion, recompression, or other transformation is required, and the
  destination model can retain the encoded resource safely.
- Decode only the Phase 3-approved one- or three-component Huffman SOF0/SOF2
  JPEG subset through the reviewed Media implementation.
- Decode/transcode raw non-DCT samples through explicit Media services; newly
  encoded V1 raster data uses Flate rather than an incidental JPEG encoder.
- Emit specific diagnostics for four-component CMYK/YCCK conversion,
  arithmetic/lossless/hierarchical JPEG, JPEG-LS/XR, JPX, JBIG2, and CCITT
  rather than delegating them to an ambient platform decoder.
- Create `InlineImage` only when an image can be placed meaningfully in reading
  order.
- Diagnose floating/background/vector artwork that cannot be represented.
- Import allowed external URI links as inert `LinkHref` values.
- Defer internal destinations until a cross-format bookmark/anchor model exists.
- Return an explicit "OCR required" diagnostic for scanned pages without text.

### 9.4 Exit gate

- Unicode text exactly matches owned goldens for the declared supported subset.
- Paragraph/run/style output is deterministic across platforms.
- Every unsupported font, image, action, or ambiguous layout emits a stable
  `pdf.*` diagnostic.
- No empty or partial extraction is silently reported as success.
- Standard 14 metrics, glyph-name mappings, predefined CMaps, and other
  distributed font data have approved provenance, license, notices, and package
  placement.
- No input font program survives beyond the document-scoped operation or is
  re-embedded by a later conversion without a new caller-supplied licensed
  resource.
- JPEG fixtures cover the exact supported/rejected mode boundary and carry
  independent rights/provenance records.
- No PDF-specific object leaks into `Broiler.Documents.Model`.

## 10. Phase 5 — Read-preview integration and artifact replacement

### 10.1 Deliverables

- Add `PdfDocumentCodec` explicitly to selected catalogs in:
  - `src/Broiler.Cli/DocumentConvertService.cs`;
  - `src/Broiler.Writer/WriterApp.cs`; and
  - `src/Broiler.Writer.WebAssembly/BrowserWriterDemo.cs`, once its memory/AOT
    gates pass.
- Add `.pdf` to appropriate open filters, but not save filters until Phase 7.
- Use the existing conversion workflow:
  - `--convert-doc input.pdf --output output.docx`;
  - `--convert-doc input.pdf --output output.txt`.
- Do not reintroduce the dedicated external `--convert-pdf` process contract.
- Report diagnostics and unsupported-feature counts in CLI/Writer status.
- Publish descriptive support wording tied to the feature matrix and exact
  ISO 32000-1:2008 subset. Do not use Adobe's PDF file icon, certification
  marks, or names of reference tools in a way that implies affiliation,
  endorsement, or full-format certification.
- Document that callers remain responsible for authority to extract, copy, and
  transform ordinary document text, metadata, and images; successful parsing is
  not a license grant and the conversion workflow does not automatically
  republish source assets.
- Add the project/tests to `Broiler.Documents.slnx`, aggregate solution
  generation, packaging, and relevant Writer/CLI solutions.

### 10.2 Exit gate

- PDF import works in-process with no external executable.
- Batch conversion is reentrant and has no mutable global parser/font/image
  registry.
- Windows and Linux tests pass; WebAssembly is enabled only after AOT and
  bounded-memory evidence.
- The read-preview support statement exactly names the supported subset,
  identifies PDF 2.x handling as header/construct tolerance only, and passes the
  claims/trademark review.

## 11. Phase 6 — Shared export foundation

This phase can run in parallel with parser Phases 2–5.

### 11.1 `Broiler.Documents.Pagination`

Create a headless, UI-free paginator supporting:

- explicit point units, page size, orientation, and margins;
- paragraph spacing and line spacing;
- wrapping, bidi/shaping, lists, indentation, alignment, and page breaks;
- inline images, highlights, underlines, strikethrough, and link rectangles;
- deterministic page and resource ordering; and
- overflow diagnostics for content too large to place.

Extract reusable line-breaking behavior from `StandardRichEdit`, then make
RichEdit print/preview or layout tests its second consumer.

### 11.2 `Broiler.Graphics`

Add the export-relevant shared capabilities:

- explicit instance-based font resolver and metrics service;
- immutable font-face resources with controlled byte ownership;
- shaped glyph runs carrying glyph IDs, positions, clusters, direction, and
  Unicode mapping;
- TrueType/OpenType-CFF sanitization and bounded table access;
- font subsetting and technical embedding-right checks, including
  [OpenType `OS/2.fsType`](https://learn.microsoft.com/en-us/typography/opentype/spec/os2)
  restricted, preview-and-print, editable, no-subsetting, and bitmap-only cases;
  and
- typed image resources rather than backend-only handles.

Do not require the full native PDF rendering vocabulary for V1 export.
Arbitrary paths, path clipping, gradients, soft masks, patterns, and faithful
affine image replay become prerequisites only for the later native-rendering
track.

### 11.3 Font and embedding-license policy

- Never select fonts through ambient installed-font discovery.
- Require caller-supplied deterministic font resources or an explicitly
  licensed shared fallback font.
- Require an explicit license disposition for each font resource covering the
  intended embedding, subsetting/modification, redistribution, commercial use,
  target platforms, and obligations attached to each generated document. The
  shared resource-use policy carries the caller decision; `fsType` is a
  technical signal and enforcement input, not a substitute for the font EULA or
  other actual grant, and Broiler does not determine the caller's legal title.
- Fail closed on restricted, invalid, ambiguous, bitmap-only-without-bitmaps, or
  legally unknown resources. Honor `no subsetting`; define and document the
  permitted output behavior for preview-and-print and editable embedding rather
  than silently choosing the least restrictive interpretation.
- Produce a stable diagnostic and caller-controlled licensed fallback decision
  when a requested font cannot be embedded. Never substitute an ambient OS font.
- Do not treat a font extracted from an input document as caller-supplied export
  authority; import-to-export conversions resolve a new approved font resource.
- Record and ship the license/attribution required by any bundled fallback font,
  including Reserved Font Name or modified-font naming obligations where
  applicable. Separately record whether each generated PDF must carry
  attribution, a license copy, modified naming, source availability, or another
  notice; the writer must fulfill that obligation or reject the resource. Do not
  assume that a freely downloadable font is redistributable.
- Ensure the same fixed font set produces byte-identical output on Windows,
  Linux, and WebAssembly.

### 11.4 Exit gate

- Pagination goldens cover long paragraphs, multiple pages, lists, RTL text,
  images, links, and explicit page breaks.
- RichEdit/print and PDF-facing tests consume the same line/pagination logic.
- Glyph runs and font subsets are deterministic and independently validated.
- Font-policy tests cover every `fsType` state, absent/invalid flags,
  no-subsetting, bitmap-only, license rejection, and deterministic fallback.
- Resource-policy tests default unknown dispositions to deny and verify that
  generated-document obligations are emitted or cause a stable rejection.
- Every bundled font, shaping table, glyph mapping, and other export asset has
  an approved license record, component notice, and package-content test.
- No UI, DOM, HTML, or platform reference enters the pagination assembly.

## 12. Phase 7 — Deterministic PDF writer

### 12.1 Deliverables

- Set `PdfDocumentCodec.CanWrite` to true only when the full preview gate passes.
- Emit new PDF 1.7 files with:
  - header, catalog, page tree, resources, content streams, xref, trailer, and
    EOF;
  - stable object numbering and resource names;
  - Flate-compressed streams;
  - page boxes and metadata;
  - Unicode Type 0 fonts, subsets, widths, and ToUnicode maps;
  - text, colors, highlights, decorations, lists, and inline raster images,
    using Flate for newly encoded samples and only approved byte-preserving DCT
    resources allowed by the shared resource-use policy; and
  - external-link annotations and alt text where representable.
- Support non-seekable output by tracking byte offsets during emission.
- Make creation/modification dates and identifiers caller-controlled;
  deterministic mode must not read the clock, machine name, locale, or installed
  fonts.
- Preserve original logical text in ToUnicode mappings where presentation-only
  capitalization changes glyph appearance.
- Emit diagnostics for unsupported model features rather than silently
  rasterizing.
- Do not add a JPEG encoder, transcode to JPEG, or infer permission to embed a
  font/image from its presence on the machine or in a previously read document.
- Generate the exact support/conformance statement from the approved feature
  matrix. Do not call Broiler an ISO 32000-1-conforming reader or processor on
  the basis of an arbitrary supported subset. Claim only that documented
  feature subset, while separately validating that every emitted file satisfies
  all applicable ISO 32000-1 requirements. Do not market output as
  Adobe-certified, patent-free, or endorsed by an oracle.
- Do not implement incremental save, linearization, encryption, or a hidden
  raster-page fallback in V1.

### 12.2 Exit gate

- Xref offsets, stream lengths, references, and resource dictionaries pass
  independent structural validation.
- Files open in at least two independent readers.
- Copy/paste text matches the source model.
- Reference renders from two independent renderers meet declared tolerances.
- The same input, options, and font resources produce byte-identical output.
- Every emitted font and preserved image has a recorded caller/resource policy
  decision. The writer fulfills or rejects every generated-document attribution,
  license-copy, naming, or source obligation; every package-bundled fallback
  asset has its separate required notice.
- Reader and writer are not each other's only oracle.

## 13. Phase 8 — Hardening, IP/licensing evidence, and release

### 13.1 Verification

- Syntax, filter, CMap, font, image, content, and writer fuzz/property tests.
- Every-truncation-point and deterministic mutation campaigns.
- Malicious corpus covering cycles, huge lengths, deep containers, xref loops,
  decompression bombs, page floods, font bombs, image pixel bombs, and action
  payloads.
- Differential text and page-geometry checks.
- Independent structural validation with pinned, test-only, out-of-process
  tools. Prefer [qpdf](https://github.com/qpdf/qpdf) as the Apache-2.0 structural
  oracle, while retaining its license/NOTICE and auditing the providers enabled
  in the exact build.
- Render comparisons using at least two independently approved and pinned
  renderers, with these candidate-specific rules:
  - [PDFium](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE)
    requires the complete dependency/asset SBOM and notices for the exact build;
    its top-level license alone is insufficient;
  - [Poppler](https://gitlab.freedesktop.org/poppler/poppler/-/blob/master/README.md)
    remains a separate GPL command-line tool, is never linked or bundled into
    Broiler, and `poppler-data` is audited separately; and
  - [MuPDF](https://mupdf.readthedocs.io/en/latest/license.html) is used only
    under one qualified-reviewer-approved compliance path: an
    organization-installed, unmodified AGPL tool that is not conveyed; an
    approved AGPL-compliant conveyance or service plan covering Corresponding
    Source and any applicable network-use duties; or a suitable commercial
    license. It is never silently introduced by a wrapper package or
    redistributed in a CI image under a notices-only assumption.
- If the [veraPDF apps](https://github.com/veraPDF/veraPDF-apps) CLI or installer
  is used for PDF/A/UA diagnostics, audit that actual distribution and its full
  dependency set, pin it as a separate CLI, and explicitly select the MPL-2.0
  option in the manifest. A redistributed CLI or CI image must retain the
  required notices and make MPL-covered source available as required. No
  conformance claim is made in V1.
- Verify tool release signatures/checksums where available. Do not copy oracle
  source, tables, generated code, or undocumented expected behavior into the
  implementation.
- Treat expected renders, extracted text, screenshots, and other goldens as
  derivatives of the corpus input; generate or retain them only when the
  manifest records the necessary rights.

### 13.2 Performance baselines

Measure:

- open and first-page discovery;
- full text extraction;
- large-object and incremental-update files;
- peak memory, allocations, decoded bytes, and cache sizes;
- pagination and writing throughput; and
- parallel per-document conversion.

No cache may grow beyond document-, font-, object-, or resource-count bounds.

### 13.3 Release and legal gates

- Windows/Linux Release builds and tests.
- WebAssembly trimming/AOT smoke where enabled.
- Package/consume from a clean local feed.
- Refresh and approve the IP/licensing register against the current ISO/ITU
  declaration records and target distribution jurisdictions; resolve every
  pending entry used by a supported feature.
- Confirm a qualified reviewer's recorded determination that every planned V1
  capability falls within Adobe's ISO 32000-1 public patent-license definitions
  and conditions, including retaliation, scope, and warranty terms, or has
  separate authority. Block capabilities whose coverage remains unresolved.
- Confirm unrestricted public feeds have worldwide clearance for every enabled
  capability, dependency, and shipped asset; otherwise use a technically and
  contractually enforced territory-limited distribution channel.
- Produce SBOMs and component-local notices covering all third-party or derived
  source/generated code, algorithms, constants, tables/data, test vectors,
  dependencies, and assets, plus API compatibility evidence, security review,
  and human approval. Inspect `.nupkg`, `.snupkg`, application, and container
  contents for undeclared code, fonts, CMaps, ICC profiles, sample files, tools,
  native binaries, and license texts.
- Confirm `Broiler.Documents`, the PDF package, and every affected Media/Graphics
  package participate in the repository publish-approval gate.
- Audit every shipped standard-derived constant, corpus item, golden, font,
  mapping table, profile, and fallback asset back to its source, rights, notices,
  and approval.
- Confirm that test tools remain absent from product artifacts; any separately
  redistributed CI binary or image carries its own license, notices, SBOM, and
  source obligations.
- Claims review prohibits unsupported `patent-free`, `royalty-free`, `certified`,
  full-conformance, affiliation, or endorsement wording and unauthorized Adobe,
  ISO, or oracle logos/marks.
- Exact conformance document at `Broiler.Documents/docs/pdf-conformance.md`.
- All existing RTF, DOCX, HTML, Markdown, RichEdit, CLI, and Writer suites remain
  green.
- No product-time external application, PdfPig/PdfSharp fallback, hidden global
  registration, environmental legacy test, or restricted standards publication
  remains in a release artifact.

## 14. Post-V1 tracks

Treat these as separately approved roadmaps:

1. Password encryption through the Standard Security Handler, preceded by a
   crypto export-control, sanctions, anti-circumvention, authorized-password,
   permissions-policy, algorithm, and target-jurisdiction review.
2. Full tagged-PDF structure, outlines, internal destinations, and
   accessibility.
3. PDF/A and PDF/UA profiles pinned to exact standards editions and levels, with
   lawful standards access, independent validation, and certification/marketing
   review.
4. AcroForm reading and attachment extraction under an explicit security and
   user-content policy.
5. Signature inspection and later cryptographic validation under an explicit
   trust-store/revocation policy. Broiler must distinguish mathematical
   integrity from identity/trust and never claim that a signature is legally
   valid.
6. Four-component CMYK/YCCK JPEG decode/transcode and advanced color/ICC support
   in Media/Graphics, with the T.81 register rechecked; the source, rights,
   provenance, and Adobe-license scope of APP14 marker handling and Adobe
   Technical Note #5116 reviewed; [ISO/IEC 10918-6](https://www.iso.org/standard/59634.html)
   reviewed only if that printing profile is selected; and every bundled ICC
   profile licensed as a separate asset.
7. `CCITTFaxDecode` as a separately scoped, decode-first T.4/T.6 track. Recheck the
   official [T.4](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=4597)
   and [T.6](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=2613)
   declaration records at approval and release; absence from a declaration
   register is not clearance. Use Flate as the writer fallback.
8. `JPXDecode` as an independent JPEG 2000 track: select the exact T.800/ISO
   15444-1 edition and profile, separate Part 1 core from Part 2 JPX extensions,
   HTJ2K, and other parts, map the official
   [T.800 declarations](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=5281),
   and approve the codec's copyright and patent posture before use. If any Part
   2/JPX extension is enabled, separately pin the
   [T.801/ISO 15444-2](https://www.itu.int/ITU-T/recommendations/rec.aspx?lang=en&rec=15653)
   edition and profile and review the official
   [T.801 declaration record](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=6123);
   Part 1 review does not clear Part 2.
9. `JBIG2Decode` as an independent T.88 patent, license, and security track.
   Start decode-only after reviewing the official
   [T.88 declarations](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=4845)
   and patent-family/status or obtaining an approved vendor license; do not add
   lossy symbol-substitution encoding by default because it can silently change
   document characters and numbers.
10. PDF-writer use or extension of the existing native managed JPEG encoder only
    under a separately justified Media roadmap with exact T.81 modes,
    implementation and data provenance, patent review, and notices; it is not an
    incidental PDF-writer task.
11. Native page rendering in a satellite such as
   `Broiler.Documents.Pdf.Rendering`. This requires canonical Graphics paths,
   fill rules, path clipping, gradients, patterns, transparency groups, soft
   masks, blend modes, and geometrically faithful affine replay.
12. HTML/CSS print-to-PDF through paged CSS/Layout output into the shared Graphics
   page representation, not through DOM code inside the PDF codec.
13. OCR through an explicitly composed external service, never silently inside
    the codec. Before document bytes leave the process, approve provider terms,
    confidentiality, data-processing/privacy, retention, and cross-border
    transfer policy.

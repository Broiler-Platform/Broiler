# Broiler Document Formats (RTF) Roadmap

**Status:** Phases 0-6 complete (2026-07-07); Path A. RTF reads/writes (lossless round-trip, hardened); rich RTF clipboard adapter + headless CLI convert shipped; HTML + Markdown codecs and packaging shipped; DOCX remains future optional scope
**Date:** 2026-07-07
**Scope:** Roadmap for reading and writing rich-text document interchange
formats — RTF first, then HTML/Markdown/DOCX — in Broiler. This document
contains no implementation.

## 1. Executive decision

Add document-format support as a **new standalone component, `Broiler.Documents`**
— a *decode-first document codec* framework modelled on `Broiler.Media` — rather
than folding RTF into `Broiler.DOM` or `Broiler.Media`.

RTF's decoded, in-memory form is **already modelled** by the rich-text document
kernel that RichEdit Phases 1-4 built (`RichTextDocument`, `RichTextParagraph`,
`InlineStyle`, `ParagraphStyle`, `StyleRun`). A document format is therefore not
new *data* — it is a new *serialization* of data Broiler can already represent.
The right shape is a codec layer that maps serialized formats **↔ the rich-text
document model**, exactly the way `Broiler.UI.RichEdit.Dom` was planned to map
HTML fragments ↔ the same model in RichEdit Phase 5.

Two things follow from that:

1. **The document model should be promoted out of the UI layer.** It currently
   lives inside `Broiler.UI.RichEdit`, a UI assembly. RTF import/export is a
   headless concern (CLI converters, servers, clipboard, print) that should not
   drag in `Broiler.UI`. Promote the pure model types into a UI-free
   `Broiler.Documents.Model` and let `Broiler.UI.RichEdit` consume it.
2. **RTF and HTML become peer codecs, not one-off adapters.** Instead of an
   HTML-only adapter bolted onto RichEdit and, later, an RTF-only adapter bolted
   on somewhere else, both live as codecs in one catalog with one probe/read/write
   contract. This is the moment to make that call — **before** RichEdit Phase 5
   writes the HTML adapter into the UI layer and we have to move it later.

| Assembly | Primary type | Purpose |
|---|---|---|
| `Broiler.Documents.Model` | `RichTextDocument` (promoted) | Platform-neutral rich-text document model; depends only on `Broiler.Graphics` |
| `Broiler.Documents` | `DocumentCodecCatalog`, `IDocumentCodec` | Codec contract, signature probe, format descriptors, read/write options, limits |
| `Broiler.Documents.Rtf` | `RtfDocumentCodec` | RTF reader + writer (first codec) |
| `Broiler.Documents.Html` *(later)* | `HtmlDocumentCodec` | HTML fragment codec; references `Broiler.DOM` / `Broiler.Dom.Html` |
| `Broiler.Documents.Markdown` *(later)* | `MarkdownDocumentCodec` | CommonMark subset codec |

## 2. Why not Broiler.Media, and why not Broiler.DOM

The user's question was explicitly *where* this belongs. The two obvious existing
homes were evaluated and rejected as **homes**, though one contributes its design.

### Broiler.Media — borrow the pattern, not the home

`Broiler.Media` is a **decode-first binary-media** component: its codecs sniff a
byte prefix and produce **pixel buffers, audio sample buffers, or video frames**
(`MediaKind` = Image/Audio/Video). RTF is a **text-structured document**; its
decoded form is a paragraph/run tree, not an `ImageBuffer` or `AudioBuffer`, and
it has no `MediaKind`. Adding `MediaKind.Document` would break the component's
"decode-first media" charter (Media ADR 0001 topology).

**Verdict: wrong home.** But `Broiler.Media`'s **catalog architecture is the
right template** and should be mirrored: signature/prefix probing (RTF's
`{\rtf` header is exactly this), confidence-ranked codec selection
(`MediaCodecCatalog.SelectAsync`), `MediaFormatDescriptor` (name + MIME types +
file extensions), explicit registration with **no hidden global codec
registration or module-initializer side effects**, `.NET 10` only, no
third-party runtime dependencies, platform-neutral abstractions with any
OS-specific code split into its own assembly. `Broiler.Documents` copies this
shape deliberately.

### Broiler.DOM — stays the HTML bridge, not the RTF home

`Broiler.DOM` is the HTML/XML DOM (nodes, `DomRange`, mutations) plus
`Broiler.Dom.Html` (HTML parse/serialize). RTF is **not** an SGML/XML tree; its
group + control-word grammar (`{... \b ...}`) has no natural DOM representation,
and routing RTF through a DOM would be a lossy, awkward intermediate. DOM's
genuine value to rich text is specifically **HTML** import/export and paste
sanitization — which is why it becomes the `Broiler.Documents.Html` codec's
dependency, not RTF's.

**Verdict: wrong layer for RTF.** RTF targets the document model directly. A
"convert RTF → HTML" feature is then just *codec composition* (RTF reader →
model → HTML writer) at the catalog level, which is a reason to keep both codecs
in one component, not a reason to house RTF inside DOM.

### The decision

| Candidate | Role for RTF | Outcome |
|---|---|---|
| `Broiler.Media` | Home | **Rejected** — binary-media charter; document is not a `MediaKind`. Pattern reused. |
| `Broiler.DOM` | Home | **Rejected** — RTF is not a DOM tree. DOM remains the HTML *codec's* dependency. |
| **`Broiler.Documents`** *(new)* | Home | **Chosen** — codec framework over the promoted rich-text model. |

## 3. Goals

1. Read RTF into, and write RTF from, the rich-text document model with a
   predictable, documented mapping.
2. Keep the document model **UI-free** so headless consumers (CLI, server,
   clipboard, print) can use RTF without referencing `Broiler.UI`.
3. Mirror `Broiler.Media`: a codec catalog with signature probing, format
   descriptors, explicit registration, and no hidden global state.
4. Make RTF safe by default: cap nesting depth and size, skip unknown
   destinations, never fetch external resources, never instantiate OLE, and
   default to *not* decoding embedded binary objects.
5. Get Unicode right from the start: `\uN`/`\ucN`, `\'hh` code-page bytes,
   `\fcharset`, and surrogate-safe emission.
6. Support lossless round-trip for the supported subset, and predictable,
   documented degradation for everything outside it.
7. Provide a rich-clipboard payload (`CF_RTF`) so RichEdit copy/paste can carry
   formatting — satisfying the rich-clipboard goal deferred by RichEdit ADR 0016.
8. Keep the codec contract format-agnostic so HTML, Markdown, and DOCX can be
   added later as peer codecs without reshaping the catalog.
9. Make everything testable headlessly: fixture corpus, round-trip property
   tests, and fuzzing, with no graphics/UI/OS dependency in the core path.

## 4. Non-goals

- Full word-processor parity: tables, floating objects, sections/columns,
  headers/footers, footnotes, fields (beyond hyperlinks), revision marks,
  style sheets, and pagination are **out of the first release**.
- Being a layout or rendering engine — `Broiler.Documents` produces/consumes the
  model; `Broiler.UI.RichEdit.Standard` renders it.
- Instantiating or executing embedded OLE objects, macros, or `\*\objdata`
  payloads. Ever.
- Fetching remote resources referenced by RTF (`INCLUDEPICTURE`, remote
  templates, `HYPERLINK` targets) during parse.
- Round-tripping content the model cannot represent losslessly (unknown
  destinations are dropped, not silently mangled).
- Replacing `Broiler.DOM` as the HTML engine — the HTML codec *uses* DOM.

## 5. Relationship to the RichEdit work (and why timing matters)

RichEdit Phases 0-4 are complete: the kernel, the `UiRichEdit` control, standard
multiline rendering, and editing/formatting commands all ship, with 129 tests.
The kernel already depends only on `Broiler.Graphics` (for `BColor`) — it does
not use `Broiler.UI` types — even though it is *packaged inside* the
`Broiler.UI.RichEdit` assembly next to `UiRichEdit : UiElement`.

RichEdit **Phase 5 is "DOM and HTML adapter"** (`Broiler.UI.RichEdit.Dom`) and is
the next unstarted phase. That makes now the cheapest possible moment to decide
whether document interchange lives in the UI layer or its own component. If we
build the HTML adapter as `Broiler.UI.RichEdit.Dom` first and then add RTF, we
will have two different homes for the same concern and a later migration. If we
stand up `Broiler.Documents` now, RichEdit Phase 5 is **re-pointed**: HTML becomes
`Broiler.Documents.Html`, RTF is `Broiler.Documents.Rtf`, and RichEdit consumes
both through the catalog. One concept, one home.

This roadmap therefore proposes a **model promotion** that supersedes the
*placement* (not the design) of the model in RichEdit ADRs 0013/0014. The opaque
`RichTextPosition` (internal ctor, `InternalsVisibleTo` → the Standard renderer)
and the copy-on-write snapshots are preserved exactly; only the assembly they sit
in changes. See §9 Phase 0 for the decision gate and the Path A / Path B fork.

## 6. Target model and RTF mapping

The first-release RTF codec targets today's model attributes and nothing more.
Anything richer is skipped safely (§8) rather than approximated.

### Inline (character) formatting

| RTF control word | Model (`InlineStyle`) | Notes |
|---|---|---|
| `\b` / `\b0` | `Bold` | |
| `\i` / `\i0` | `Italic` | |
| `\ul` / `\ulnone` | `Underline` | underline variants (`\uld`, `\ulw`…) collapse to `Underline` |
| `\strike` / `\strike0` | `Strikethrough` | |
| `\fsN` | `FontSize = N/2` | RTF font size is in half-points |
| `\fN` | `FontFamily` | resolved through the `\fonttbl` |
| `\cfN` | `Foreground` | resolved through the `\colortbl` |
| `\highlightN` / `\cbN` | `Background` | |
| `\field{\*\fldinst{HYPERLINK "url"}}{\fldrslt …}` | `LinkHref` | hyperlink fields only; other field types dropped |

### Paragraph formatting

| RTF control word | Model (`ParagraphStyle`) | Notes |
|---|---|---|
| `\pard` | reset to `ParagraphStyle.Default` | |
| `\ql` `\qc` `\qr` `\qj` | `Alignment` | justify maps to the nearest supported value |
| `\slN\slmultN` | `LineSpacing` | multiple form → `LineSpacing`; absolute twip spacing approximated |
| `\liN` | `IndentLevel` | twips → discrete levels (e.g. round `N/360` per 0.25"), capped |
| `\sbN` / `\saN` | `SpacingBefore` / `SpacingAfter` | twips → logical units |
| `\par` | paragraph break | new `RichTextParagraph` |
| `\line` | soft line break | maps to the model's `U+2028` soft break |
| list tables / `\pntext` / `{\*\pn}` | `ListKind`, `IndentLevel` | bulleted/numbered detection; deferred to a later phase |

### Document structure and encoding

- Header `{\rtf1 …}`, character set (`\ansi`/`\mac`/`\pc`/`\pca`), `\ansicpgN`,
  default font `\deffN`.
- Tables: `\fonttbl` (with `\fcharsetN` per font), `\colortbl`; `\stylesheet`
  is parsed enough to resolve `\sN` references, styles themselves deferred.
- Text encoding: `\'hh` hex bytes decoded against the active code page; `\uN`
  with `\ucN` skip-count; surrogate-safe on write. Twips constant: 1 inch =
  1440 twips; 1 point = 20 twips.
- Destinations skipped by default: `\*\…` unknown groups, `\pict`, `\object` /
  `\objdata`, `\bin`, `\info`, `\*\datastore`, and any `\field` that is not a
  hyperlink.

## 7. Assembly topology and dependency direction

```text
Broiler.Documents.Model  -> Broiler.Graphics            (BColor only; no UI, no DOM)

Broiler.Documents        -> Broiler.Documents.Model      (catalog, IDocumentCodec, probe, options, limits, errors)

Broiler.Documents.Rtf    -> Broiler.Documents
Broiler.Documents.Rtf    -> Broiler.Documents.Model

Broiler.Documents.Html   -> Broiler.Documents            (later)
Broiler.Documents.Html   -> Broiler.Documents.Model
Broiler.Documents.Html   -> Broiler.DOM, Broiler.Dom.Html

Broiler.UI.RichEdit      -> Broiler.Documents.Model       (consumes the promoted model)
Broiler.UI.RichEdit      -> Broiler.UI, Broiler.Graphics
Broiler.UI.RichEdit.Standard -> Broiler.UI.RichEdit       (renderer; InternalsVisibleTo retargeted to the Model assembly)
```

Mirroring `Broiler.Media`, each runtime project gets a dependency-free console
test project beside it, and a Windows-only implementation assembly is added
*only if* a codec ever needs OS services (none is expected for RTF).

## 8. Security and robustness (RTF is a known attack surface)

RTF is a historically heavy exploit and DoS vector; the codec is built defensively
from Phase 1, following the same spirit as RichEdit ADR 0016 (sanitize, then fall
back). `DocumentReadOptions` carries hard limits enforced by the tokenizer:

| Threat | RTF vector | Mitigation |
|---|---|---|
| Stack overflow | deeply nested `{ … }` groups | `MaxGroupDepth` cap; iterative or depth-guarded parse |
| Resource exhaustion | huge `\binN`, `\uN` floods, enormous `\'` runs | `MaxDocumentBytes`, `MaxRunLength`, bounded `\bin` skip |
| Code execution | `\object` / `\*\objdata` OLE, macros | never instantiated; destination skipped by default |
| Downstream image exploits | `\pict` payloads | not decoded by default; opt-in decode routed through `Broiler.Media.Image` with *its* limits |
| SSRF / phone-home | `INCLUDEPICTURE`, remote templates, `HYPERLINK` | no network access during parse; hyperlink stored as inert `LinkHref` text |
| Malformed input | truncated groups, bad control words, bad hex | never throw across the API boundary — return `DocumentReadResult` with diagnostics and a best-effort model |

Fuzzing (§9 Phase 4) is a first-class exit gate, not an afterthought.

## 9. Roadmap

### Phase 0 — Charter, ADRs, and the model-placement decision

**Status:** Complete (2026-07-05). **Path A (promote) chosen.** Decisions recorded
as `Broiler.Documents/docs/adr/` ADRs 0001-0005 and `Broiler.UI/docs/adr/` ADR
0018; Phase 0 record in `Broiler.Documents/docs/phase-0.md`. See the resolution
table below.

**Objective:** Approve the component boundary and resolve where the document
model lives *before* writing codec or migration code.

Tasks:

- Approve the name `Broiler.Documents` and the codec-catalog architecture
  mirrored from `Broiler.Media`.
- **Decide the model-placement fork (the one real design choice):**
  - **Path A — Promote (recommended).** Move the pure model types
    (`RichTextDocument`, `RichTextParagraph`, `InlineStyle`/`Delta`,
    `ParagraphStyle`/`Delta`, `StyleRun`, `RichTextPosition`, `RichTextRange`,
    `RichTextOperation`, `RichTextTransaction`, `RichTextEditor`,
    `RichTextEditResult`, `ListKind`, `TextAlignment`) into
    `Broiler.Documents.Model`; leave the control types (`UiRichEdit`,
    `RichEditCommand*`, events, scroll policy) in `Broiler.UI.RichEdit`, which
    now references the Model assembly. Retarget the `InternalsVisibleTo` for
    `RichTextPosition` to `Broiler.UI.RichEdit.Standard` and the codec/test
    assemblies. Re-point RichEdit Phase 5 (HTML) at `Broiler.Documents.Html`.
  - **Path B — Adapter only.** Keep the model in `Broiler.UI.RichEdit`; ship
    `Broiler.UI.RichEdit.Rtf` as a UI-side adapter paralleling the planned DOM
    adapter. Faster and lower-risk short term, but RTF then depends on
    `Broiler.UI` and the model stays UI-owned. Document as the fallback.
- Record the supported RTF subset (§6) and the safety limits (§8).
- Author the ADRs: component topology & consumption policy; document model
  ownership (promotion vs adapter) superseding the *placement* in RichEdit ADRs
  0013/0014; codec contract & probe; RTF sanitization policy (analogue of ADR
  0016). Add a UI-side ADR 0018 if Path A is chosen.

Exit gate:

- component dependency graph approved; model-placement path chosen and recorded;
- first-release RTF subset and safety limits are explicit;
- no public API commits to a code-page/indexing model that blocks Unicode work.

Resolution:

| Phase 0 item | Decision | ADR |
|---|---|---|
| Approve `Broiler.Documents` name + codec-catalog architecture | Approved; new root component mirroring `Broiler.Media`; .NET 10, no third-party deps, no hidden global registration | [Docs 0001](../../Broiler.Documents/docs/adr/0001-component-topology-and-consumption-policy.md) |
| Model-placement fork (Path A vs B) | **Path A — promote** the pure model to `Broiler.Documents.Model` (deps: `Broiler.Graphics` only); `Broiler.UI.RichEdit` references it; control stays in UI; `InternalsVisibleTo` retargets; RichEdit Phase 5 HTML re-pointed at `Broiler.Documents.Html` | [Docs 0002](../../Broiler.Documents/docs/adr/0002-document-model-ownership-and-promotion.md), [UI 0018](../../Broiler.UI/docs/adr/0018-richedit-document-model-promotion.md) |
| Codec contract & signature probe | `IDocumentCodec` + `DocumentCodecCatalog` + `DocumentFormatDescriptor`; confidence-ranked probe; explicit registration; total (non-throwing) `Read`/`Write` results | [Docs 0003](../../Broiler.Documents/docs/adr/0003-codec-contract-and-signature-probe.md) |
| Record safety limits | `DocumentLimits` (depth/size/bin/run caps); skip-by-default destinations; no network, no OLE; embedded-image decode opt-in via `Broiler.Media.Image`; best-effort + diagnostics | [Docs 0004](../../Broiler.Documents/docs/adr/0004-document-read-limits-and-rtf-sanitization.md) |
| Record first-release RTF subset + encoding | Authoritative inline/paragraph control-word map onto the ADR 0014 style set; `\uN`/`\ucN`/`\'hh` + code pages; surrogate-safe; indexing-model-neutral | [Docs 0005](../../Broiler.Documents/docs/adr/0005-rtf-first-release-subset-and-text-encoding.md) |

Exit-gate status: dependency graph approved (Docs 0001/0002); model-placement
chosen and recorded — Path A (Docs 0002 + UI 0018); first-release RTF subset
(Docs 0005) and safety limits (Docs 0004) explicit; no public API commits to a
code-page/indexing model (Docs 0005 targets the opaque `RichTextPosition`).
RichEdit regression baseline captured green (129/129) as the gate for the Phase 1
promotion.

### Phase 1 — Codec framework and RTF tokenizer

**Status:** Complete (2026-07-05). Model promoted (ADR 0002) and the codec
framework + RTF tokenizer delivered; RichEdit stayed green (129/129) and 68 new
tests were added. See the delivery table below.

**Objective:** Stand up the catalog and a safe, complete RTF token layer with no
semantic mapping yet.

Tasks:

- Scaffold `Broiler.Documents.Model` (Path A: the model move; Path B: a
  reference) and `Broiler.Documents` with `IDocumentCodec`,
  `DocumentCodecCatalog`, `DocumentFormatDescriptor` (name, MIME
  `application/rtf`/`text/rtf`, extension `.rtf`), `DocumentReadOptions`/
  `DocumentWriteOptions`, `DocumentLimits`, `DocumentReadResult`/
  `DocumentWriteResult`, and `DocumentDiagnostic`.
- Implement the RTF tokenizer: groups, control words (with optional numeric
  parameters and the delimiter space), control symbols, `\*` destinations,
  `\'hh` hex, and raw text — all under the Phase 0 limits.
- Implement signature probing (`{\rtf` prefix → confidence) analogous to
  `MediaCodecCatalog.SelectAsync`.
- Architecture tests: no `Broiler.UI`/DOM/OS dependency in Model or core; no
  hidden global registration.

Exit gate:

- the catalog selects the RTF codec by signature;
- the tokenizer survives the malformed/adversarial corpus without throwing across
  the API boundary and honours every limit;
- Model and core assemblies are UI-free and platform-neutral.

Delivery:

| Phase 1 task | Implementation |
|---|---|
| Promote the model (ADR 0002) | New `Broiler.Documents.Model` assembly (deps: `Broiler.Graphics` only) holds the 15 promoted types; `Broiler.UI.RichEdit`/`.Standard`/tests/demo re-pointed at it; `InternalsVisibleTo` for `RichTextPosition` retargeted; RichEdit suite green before and after (129/129) |
| Codec framework | `Broiler.Documents`: `DocumentCodec` + `DocumentCodecCatalog` + `DocumentFormatDescriptor`, `DocumentProbeRequest`/`Result`/`Confidence`, `DocumentReadOptions`/`WriteOptions`, `DocumentLimits`, `DocumentReadResult`/`WriteResult`, `DocumentDiagnostic`, `DocumentException` — sync, explicit registration, no hidden global state |
| RTF tokenizer | `Broiler.Documents.Rtf`: `RtfTokenizer`/`RtfToken`/`RtfTokenizeResult` — groups, control words (signed params + single-space delimiter), control symbols, `\*` destinations, `\'hh` hex, text (Latin-1 preserved, stream line-breaks dropped); total and bounded (depth/size/run limits stop-and-diagnose, never throw) |
| Signature probe | `RtfDocumentCodec.Probe`: `{\rtf1` → Certain, `{\rtf` → High, filename/MIME hint → Low, else no-match; skips a UTF-8 BOM and leading whitespace. `CanRead`/`CanWrite` are false (reader is Phase 2, writer Phase 3); `Read`/`Write` throw `NotSupportedException` |
| Architecture tests | Model references only `Broiler.Graphics`; core references only the model; no UI/DOM/Input/Windows edge; catalog has no parameterless ctor and the assembly has no `[ModuleInitializer]` (no hidden registration) |
| Test projects | `Broiler.Documents.Model.Tests` (6), `Broiler.Documents.Tests` (28), `Broiler.Documents.Rtf.Tests` (34) = **68 new tests**; all six projects registered in `Broiler.slnx` under `/Dependencies/Documents/` |

Exit-gate status: the catalog selects RTF by the `{\rtf` signature
(`RtfCatalogSelectionTests`); the tokenizer survives a malformed/adversarial +
random-byte corpus without throwing and honours the depth/size/run limits
(`RtfTokenizerTests`); Model and core are UI-/DOM-/platform-free (architecture
tests). Semantic mapping (RTF ↔ model) remains Phase 2/3.

### Phase 2 — RTF reader (RTF → model)

**Status:** Complete (2026-07-05). `RtfReader` maps the token stream to a
`RichTextDocument`; `RtfDocumentCodec.CanRead` is now true. 34 new tests (RichEdit
still 129/129). See the delivery table below.

**Objective:** Turn tokens into a `RichTextDocument`.

Tasks:

- Parse `\fonttbl`, `\colortbl`, and the header (`\ansicpg`, `\deff`, character
  set); maintain a formatting-state stack synchronized to group `{`/`}`.
- Map inline and paragraph control words per §6; emit paragraphs on `\par`,
  soft breaks on `\line`.
- Decode text correctly: `\'hh` against the active code page, `\uN` honouring
  `\ucN`, per-font `\fcharset`.
- Map hyperlink fields to `LinkHref`; skip all other destinations safely.
- Tests: fixture corpus (WordPad-, Word-, LibreOffice-emitted RTF), Unicode and
  code-page cases, surrogate pairs, and graceful degradation of unsupported
  constructs.

Exit gate:

- representative real-world RTF imports with correct text, styling, and
  paragraph structure;
- unsupported constructs degrade predictably and are reported as diagnostics.

Delivery:

| Phase 2 task | Implementation |
|---|---|
| Header + tables + group-scoped state | `RtfReader` keeps a `Stack<State>` pushed/popped on `{`/`}`; `State` carries the current `InlineStyle`, `ParagraphStyle`, destination, `\uc` skip, and code page. `\fonttbl`→`RtfFontTable` (family + `\fcharset`), `\colortbl`→`RtfColorTable`, `\ansicpg`/`\deff`/charset honoured |
| Inline + paragraph mapping | `\b`/`\i`/`\ul`/`\ulnone`/`\strike`/`\plain`, `\fsN`, `\fN`, `\cfN`, `\cbN`/`\highlightN`; `\pard`, `\ql`/`\qc`/`\qr`/`\qj`, `\liN` (twips→level), `\sbN`/`\saN`; `\par` (terminator semantics — no spurious trailing empty), `\line`→`U+2028`, `\tab`, and the common `\lquote`/`\bullet`/`\emdash`… entities |
| Encoding | `\'hh` via `RtfCodePage` (full Windows-1252 incl. smart quotes, no encoding-provider dependency; Latin-1 fallback + one diagnostic for other code pages); `\uN` with `\ucN` skip (surrogate-safe); charset→code-page map |
| Hyperlink fields + destination skipping | `\field`/`\fldinst`/`\fldrslt` → `LinkHref` (http/https/mailto only, others dropped + diagnostic); `\*` unknown destinations, `\pict`/`\object` (with a diagnostic), `\info`/`\stylesheet`/… skipped safely |
| Document assembly | New additive `RichTextDocument.FromParagraphs` (model); reader builds multi-run paragraphs through the public model API — no model internals used |
| Codec wiring | `RtfDocumentCodec.Read` reads the (bounded) stream and runs the tokenizer + reader; `CanRead` = true; `Write`/`CanWrite` remain Phase 3 |
| Tests | `RtfReaderTests` (31): text/paragraph structure, every inline/paragraph attribute, colors/fonts, `\'hh`/`\uN`/`\ucN`, alignment, `\line`/`\tab`, destination skips, hyperlinks (+bad-scheme drop), malformed-input robustness, and a representative WordPad-style document; `RichTextDocumentFactoryTests` (3) |

Exit-gate status: a representative WordPad-style document imports with correct
text, bold/color/font runs, font size, and two paragraphs
(`Reads_A_Representative_WordPad_Style_Document`); embedded pictures, disallowed
link schemes, and unsupported code pages degrade predictably and are reported as
diagnostics; malformed/unbalanced input never throws. The RTF **writer** (model →
RTF) and round-trip are Phase 3.

### Phase 3 — RTF writer (model → RTF) and round-trip

**Status:** Complete (2026-07-05). `RtfWriter` serializes the model to ASCII-safe
RTF; `RtfDocumentCodec.CanWrite` is now true. 16 new tests (round-trip is
lossless for the supported subset). See the delivery table below.

**Objective:** Serialize the model to portable, correct RTF and close the loop.

Tasks:

- Emit a conformant header, build `\fonttbl`/`\colortbl` from the styles used,
  and write inline + paragraph control words for the supported subset.
- Emit Unicode via `\uN` with an ASCII fallback char and correct `\uc`;
  surrogate-safe.
- Produce output that opens cleanly in WordPad and Word.
- Round-trip property tests: model → RTF → model is stable for the supported
  subset; RTF → model → RTF is semantically stable for known-good inputs.

Exit gate:

- round-trip is lossless for the supported subset;
- emitted RTF is accepted by WordPad and Word without repair prompts.

Delivery:

| Phase 3 task | Implementation |
|---|---|
| Header + tables from used styles | `RtfWriter` scans runs to build a `\fonttbl` (default `\f0` + one entry per family) and `\colortbl` (auto `;` + one entry per fore/background color) via an order-preserving `ResourceTable<T>`; header `{\rtf1\ansi\ansicpg1252\deff0\uc1` |
| Inline + paragraph control words | Each styled run is **group-wrapped** (`{\b\i\ul\strike\fN\fsN\cfN\highlightN text}`) so formatting never leaks; default runs emit bare text. Paragraph: `\pard\plain` + `\qc`/`\qr`, `\liN` (level×360), `\sbN`/`\saN`; `\par` after **every** paragraph (round-trips exactly with the reader's terminator semantics, preserving trailing empties) |
| Unicode + escaping | `\`/`{`/`}`→`\\`/`\{`/`\}`, tab→`\tab`, `U+2028`→`\line`; non-ASCII→`\uN?` with `\uc1` (surrogate pairs emit two `\uN`, negative-encoded >0x7FFF). Output is pure ASCII (`Encoding.ASCII`) |
| Hyperlinks | Runs with `LinkHref` emit `{\field{\*\fldinst{HYPERLINK "url"}}{\fldrslt …}}` — the symmetric inverse of the reader |
| Codec wiring | `RtfDocumentCodec.Write` delegates to `RtfWriter`; `CanWrite` = true; `RtfWriter.WriteToArray` convenience added |
| Round-trip tests | `RtfRoundTripTests` (9): plain paragraphs, all inline styles, colors/fonts/highlight, alignment/indent/spacing, Unicode incl. a supplementary (emoji) char, hyperlink, empty document, trailing-empty paragraph, and **read→write→read stability** on a WordPad-style fixture; `RtfWriterTests` (7) assert header/escaping/tables/ASCII output |

Exit-gate status: model → RTF → model is asserted equivalent (paragraph text,
paragraph style, and every run's length + full `InlineStyle`) across the supported
subset, including a supplementary Unicode character and a trailing empty paragraph
(`RtfRoundTripTests`); read→write→read on a representative fixture is stable. RTF
output is a well-formed, pure-ASCII `{\rtf1…}` group with conformant font/color
tables. (WordPad/Word acceptance is a manual check outside CI; the structure
matches WordPad's own output shape.)

### Phase 4 — Hardening, fuzzing, and conformance

**Status:** Complete (2026-07-05). Fuzz/limit/security/conformance suites added
(52 new tests; 133 in `Broiler.Documents.Rtf.Tests`), `\bin` skipping and a
paragraph-cap memory bound closed, and the control-word matrix documented. See the
delivery table below.

**Objective:** Make the codec trustworthy on hostile input.

Tasks:

- Fuzz the tokenizer and reader (random bytes, nesting bombs, huge parameters,
  truncation); assert no crash, no unbounded memory, no hang.
- Expand the conformance corpus; document exactly which control words are
  honoured, approximated, or ignored.
- Verify every §8 limit and the "never fetch / never instantiate" guarantees
  with explicit tests.

Exit gate:

- fuzzing surfaces no crash/DoS;
- the honoured/approximated/ignored control-word matrix is documented and tested.

Delivery:

| Phase 4 task | Implementation |
|---|---|
| Fuzzing | `RtfFuzzTests`: 300 random-byte + 300 RTF-flavoured-random inputs (no throw), every prefix truncation of a rich fixture, 400 single-byte flips, a 200 000-`{` nesting bomb (bounded, no stack overflow — the reader uses an explicit `Stack`, not recursion), huge/overflowing parameters, a 500 000-char run, and **200 read→write→read equivalence** checks proving the round-trip is stable even on garbage |
| `\bin` hardening | `RtfTokenizer` now skips the N raw bytes after `\binN` (bounded by remaining input; `rtf.bin` when > `MaxBinBytes`) so binary content — braces/backslashes included — cannot corrupt the token stream |
| Paragraph-cap memory bound | `RtfReader` accumulator now drops text and stops once `MaxParagraphCount` is hit (previously it kept appending after the cap and `Build` could exceed it) |
| Limit verification | `RtfLimitTests`: depth, size, paragraph-count (+ post-cap bound), run-length, `\bin`, and codec-level `DocumentReadOptions.Limits` each enforced with the expected diagnostic |
| Security guarantees | `RtfSecurityTests`: `\object`/`\*\objdata` skipped (never instantiated), `INCLUDEPICTURE`/non-hyperlink fields produce no link and no fetch, disallowed URL schemes (`javascript:`/`data:`/`file:`/`vbscript:`) dropped + `rtf.link`, allowed schemes kept inert, and reads over unresolvable hosts complete (proving no network) |
| Conformance matrix | `Broiler.Documents/docs/rtf-conformance.md` documents the honoured / approximated / ignored / skipped control words, limits, and security guarantees; `RtfConformanceTests` data-drive the honoured-entity, ignored, skipped-destination, and alignment claims |

Exit-gate status: fuzzing (random bytes, mutation, truncation, nesting bombs,
huge params) surfaces no crash, hang, or unbounded memory; the honoured /
approximated / ignored / skipped control-word matrix is documented in
`rtf-conformance.md` and asserted by `RtfConformanceTests`. Two rare, documented
round-trip caveats (line spacing / list kind not written; non-ASCII font names) are
recorded in the conformance doc.

### Phase 5 — RichEdit clipboard and CLI integration

**Status:** Complete (2026-07-05). Rich RTF clipboard delivered as an optional
adapter (`Broiler.UI.RichEdit.Rtf`) with paste sanitization, plus a headless
`Broiler.Cli --convert-doc` command (verified end-to-end). The interactive Win32
demo wiring is deferred (see below). See the delivery table.

**Objective:** Deliver the two highest-value consumers.

Tasks:

- Wire `Broiler.Documents.Rtf` into RichEdit's clipboard path so copy/paste
  carries `CF_RTF` (rich clipboard), with plain-text fallback preserved — the
  rich-clipboard capability deferred by RichEdit ADR 0016.
- Add a paste-sanitization pass (reuse the §8 policy) before RTF becomes model
  operations.
- Add a `Broiler.Cli` command to convert between formats through the catalog
  (`rtf → txt`, and once the HTML codec lands, `rtf → html`).
- Add a focused demo (or extend `Broiler.UI.RichEdit.Win32.Demo`) showing RTF
  open/save and rich paste.

Exit gate:

- RichEdit round-trips formatted content through the clipboard via RTF;
- the CLI converts RTF headlessly with no UI dependency in the conversion path.

Delivery:

| Phase 5 task | Implementation |
|---|---|
| Rich clipboard (ADR 0016/0018) | New optional adapter `Broiler.UI.RichEdit.Rtf`: `IUiRichClipboardHost` (text + RTF) and `RichEditClipboard.Copy`/`Paste`/`InsertRtf`/`SelectionToRtf`/`DocumentToRtf`. Copy serializes the selection to RTF (+ plain text); paste prefers RTF and falls back to plain text. **Core RichEdit stays codec-free** — the core `IUiClipboardHost` is unchanged and the adapter references the codec, not the reverse |
| Rich-edit primitives | `RichTextDocument.Slice` (selection → sub-document) and `.InsertDocument` + `RichTextEditor.InsertDocument` (one undo unit) in the model; one public `UiRichEdit.InsertDocument(RichTextDocument)` on the control (reuses `RunEditorEdit`; honours enabled/read-only) — the rich copy/paste hooks, usable by any host |
| Paste sanitization | `RichEditClipboard.InsertRtf` reads through `RtfReader` (default limits = the ADR 0004 safe policy), so embedded objects are skipped and disallowed link schemes dropped before content reaches the document — asserted by `Rich_Paste_Sanitizes_Malicious_Rtf` |
| CLI convert | `Broiler.Cli --convert-doc <in> --output <out.txt|.rtf>` via `DocumentConvertService` (catalog `Select` → `Read` → PlainText or `RtfWriter`); no UI dependency in the path. Verified end-to-end: `clean.rtf → clean.txt` yields the expected plain text; missing input errors cleanly |
| Tests | `RichEditClipboardTests` (6): copy→paste round-trip preserves bold/color through RTF, plain-text fallback, read-only no-op, malicious-paste sanitization, selection-only serialization; `RichTextDocumentSliceInsertTests` (8): slice within/across paragraphs, single/multi-paragraph insert, transactional editor insert + undo |
| Demo | **Deferred (optional).** The reusable adapter is complete and tested; wiring it into the interactive `Broiler.UI.RichEdit.Win32.Demo` was deferred because the demo's keyboard Ctrl+C/V path runs through core input handling (plain clipboard), so a consistent rich demo needs an input-layer hook — a follow-up. A host wires `IUiRichClipboardHost` to the OS `CF_TEXT`/`CF_RTF` clipboard |

Exit-gate status: RichEdit round-trips formatted content through RTF — a copy of a
bold/coloured selection serializes to RTF and pastes back with formatting intact
(`RichEditClipboardTests`); the CLI converts RTF headlessly with no UI dependency
in the path (`DocumentConvertService`, verified end-to-end). Paste is sanitized by
construction (reads through the safe `RtfReader` policy).

### Phase 6 — Additional formats and packaging

**Status:** Complete (2026-07-07). `Broiler.Documents.Html` was added as the
second codec over the same `DocumentCodec` contract, with no catalog changes.
Package metadata now applies to the Documents component, the new projects are in
`Broiler.slnx`, and HTML/Markdown subset and limit documentation is published.
Markdown was added after the first Phase 6 pass as the optional peer codec. See
the delivery table below.

**Objective:** Prove the catalog generalizes and make it consumable.

Tasks:

- Add `Broiler.Documents.Html` (references `Broiler.DOM`/`Broiler.Dom.Html`),
  formally absorbing RichEdit Phase 5's HTML adapter as a peer codec.
- Optionally add `Broiler.Documents.Markdown` (CommonMark subset).
- Register the projects in `Broiler.slnx`; add package metadata and architecture
  gates; freeze public names after consumer review.
- Document the supported subset, safety limits, and known limitations per format.

Exit gate:

- a second codec (HTML) works through the same contract with no catalog changes;
- packages are independently consumable; the supported subset is documented.

Delivery:

| Phase 6 task | Implementation |
|---|---|
| Add `Broiler.Documents.Html` | New runtime project references `Broiler.Documents`, `Broiler.Documents.Model`, `Broiler.Dom`, and `Broiler.Dom.Html`; `HtmlDocumentCodec` probes HTML documents/fragments, reads through `HtmlDocumentParser`, writes deterministic UTF-8 HTML through `HtmlSerializer`, and maps the current model subset (paragraphs, soft breaks, inline styles, links, paragraph spacing/alignment, list reads) |
| Add `Broiler.Documents.Markdown` | New dependency-free runtime project references only `Broiler.Documents` + `Broiler.Documents.Model`; `MarkdownDocumentCodec` probes conservatively (block markers / inline markers / `.md` hints), reads a CommonMark-oriented subset, and writes deterministic UTF-8 Markdown |
| Prove catalog generalizes | `HtmlDocumentCodecProbeTests` registers `RtfDocumentCodec` + `HtmlDocumentCodec` in the unchanged `DocumentCodecCatalog` and selects HTML by content signature; no global registration or catalog API changes |
| Prove Markdown peer codec | `MarkdownDocumentCodecProbeTests` registers RTF + HTML + Markdown in the unchanged `DocumentCodecCatalog` and selects Markdown by block syntax; no global registration or catalog API changes |
| CLI consumer | `Broiler.Cli --convert-doc` catalog now includes RTF + HTML + Markdown and writes `.txt`, `.rtf`, `.html`/`.htm`, or `.md`/`.markdown`; focused tests cover HTML -> RTF, RTF -> HTML, Markdown -> RTF, and HTML -> Markdown conversions |
| Register projects + package metadata | `Broiler.Documents.Html`/`.Tests` and `Broiler.Documents.Markdown`/`.Tests` are registered in `/Dependencies/Documents/` in `Broiler.slnx`; `Broiler.Documents/Directory.Build.props` supplies shared package authors/license/tags for runtime packages |
| Architecture gates | `HtmlArchitectureTests` and `MarkdownArchitectureTests` assert `net10.0`, no package refs, intended project references, no UI/Input/Windows references, and no module initializers |
| Supported subset docs | `Broiler.Documents/docs/html-conformance.md` and `Broiler.Documents/docs/markdown-conformance.md` document probes, reader/writer mappings, skipped or degraded constructs, security policy, enforced limits, and known limitations |
| Optional DOCX | Not implemented; left as future optional scope |

Exit-gate status: HTML and Markdown work as peer codecs through the existing
catalog contract with no catalog changes, and the component is consumable through
runtime package metadata plus solution registration. The supported HTML and
Markdown subsets are documented in `docs/html-conformance.md` and
`docs/markdown-conformance.md`.

## 10. Suggested first MVP

- `Broiler.Documents.Model` + `Broiler.Documents` catalog with signature probe;
- `Broiler.Documents.Rtf` reader and writer for the §6 subset: bold, italic,
  underline, strike, font family/size, fore/background colour, paragraph
  alignment and spacing, paragraphs and soft line breaks;
- correct `\uN`/`\ucN` and `\'hh` handling;
- the §8 safety limits and "never fetch / never instantiate" guarantees;
- lossless round-trip on the supported subset;
- headless: no `Broiler.UI`, DOM, or OS dependency in the codec path.

Lists, hyperlinks-as-fields, and the HTML/Markdown codecs follow once the subset
and contract are proven.

## 11. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Promotion touches ADR-governed placement and `InternalsVisibleTo` | Migration friction | Move types only (not design); retarget `InternalsVisibleTo`; supersede *placement* via a new ADR; keep opaque positions unchanged. Path B fallback if the move is deferred |
| Two document models drift | Duplication/bugs | Promote the *existing* kernel; never fork a parallel DTO |
| RTF security holes (OLE, nesting bombs, SSRF) | Exploit/DoS | §8 limits + skip-by-default destinations + no network/OLE; fuzzing as an exit gate |
| Encoding bugs (`\u`/`\uc`/code pages/surrogates) | Corrupt text | Dedicated encoding phase with a targeted corpus; reuse the kernel's surrogate-safe boundaries |
| Scope creep toward full Word parity | Never ships | Fixed §6 subset; unsupported constructs skipped + reported, not approximated |
| Lossy round-trip surprises users | Data loss | Document the honoured/approximated/ignored matrix; report drops as diagnostics |
| RichEdit Phase 5 built as a UI-side DOM adapter first | Rework | Decide in Phase 0 now; re-point Phase 5's HTML at `Broiler.Documents.Html` |

## 12. Immediate next steps

1. Approve the component `Broiler.Documents` and the codec-catalog architecture.
2. **Resolve the Phase 0 model-placement fork** — Path A (promote, recommended)
   vs Path B (UI-side adapter). This is the one decision that gates everything
   else and is cheapest to make now, before RichEdit Phase 5.
3. Author the Phase 0 ADRs (topology, model ownership, codec contract, RTF
   sanitization), superseding only the *placement* in RichEdit ADRs 0013/0014.
4. Prototype the Phase 1 tokenizer against the adversarial corpus before any
   semantic mapping — the safety limits and encoding questions surface cheapest
   there.
5. Confirm whether document formats are first-stable scope or a post-stable
   extension (same open question RichEdit carries in its §12).

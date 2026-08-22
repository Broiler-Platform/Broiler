# PDF Support Feature Matrix

**Version:** 0.1 (Phase 0 baseline)  
**Updated:** 2026-08-22  
**Authority:** This matrix defines claims; the roadmap defines planned work.

Status values are `Planned`, `Candidate`, `Supported`, `Rejected`, and
`Post-V1`. Only `Supported` may appear as a product capability. Advancing an
entry requires tests, corpus evidence, documentation, and any applicable legal
clearance recorded in the IP/licensing register.

The package does not yet exist, so the **current behavior of every PDF operation
is `Rejected`/unavailable**. The tables below are planning intent, not current
support claims. Within the operational table, `Plan` means targeted but not
supported, `Detect/skip` means recognize without interpreting, `Reject` means a
stable rejection is required, `Later` means post-V1, and `—` means not
applicable. No `Plan` entry may become `Supported` while its legal column is
pending.

## Operational and clearance matrix

| Feature / exact subset | V1 read | V1 write | Decode | Encode | Preserve bytes | Transform | Default exposure | Legal row / state | Required diagnostic |
|---|---|---|---|---|---|---|---|---|---|
| PDF 1.7 syntax, only subsets below | Plan | Plan | — | — | No | Yes | In-process codec after gates | IP-001 pending | `pdf.version.unsupported` outside approved subset |
| PDF 2.x declaration/header tolerance | Detect/skip | Reject | — | — | No | No | Never a conformance claim | IP-002 pending | `pdf.version.tolerated-not-supported` |
| Developer extensions | Detect/skip | Reject | — | — | No | No | None | IP-003 pending | `pdf.extension.unsupported` |
| Classic xref / cross-reference streams / object streams | Plan | Plan | — | — | No | Yes | Bounded parser only | IP-001 pending | `pdf.xref.malformed` / limit code |
| Effective incremental revision | Plan | Reject | — | — | No | Yes | Latest effective revision only | IP-001 pending | `pdf.revisions.history-dropped` |
| Standard security handler / encryption | Reject | Reject | No | No | No | No | None | IP-015 blocked V1 | `pdf.encryption.unsupported` |
| ASCIIHex / ASCII85 / RunLength filters | Plan | Plan | Plan | Plan | No | Yes | Bounded filter chain | IP-001 plus source review pending | `pdf.filter.limit` / `pdf.filter.malformed` |
| FlateDecode, PNG predictors | Plan | Plan | Plan | Plan | No | Yes | Bounded shared budget | IP-011 pending | `pdf.filter.flate.*` |
| LZWDecode | Detect/skip | Reject | Candidate | No | No | No | None until cleared | IP-010 pending | `pdf.filter.lzw.unsupported` |
| CCITTFaxDecode exact modes not yet selected | Detect/skip | Reject | Candidate | No | No | No | None until cleared | IP-009 pending | `pdf.filter.ccitt.unsupported` |
| DCT: 8-bit baseline sequential, Huffman, 1/3/4 components | Detect/skip until tuple approval; then Plan | Candidate | Candidate | No | No by default | Candidate | Caller-composed decoder | IP-005 pending | `pdf.image.dct.tuple-unsupported` |
| DCT: 8-bit progressive, Huffman, 1/3/4 components | Detect/skip | Reject | Candidate | No | No | No | None until separately cleared | IP-005 pending | `pdf.image.dct.progressive-unsupported` |
| DCT: arithmetic, lossless, 12-bit, or other tuples | Detect/skip | Reject | No | No | No | No | None | IP-005 pending | `pdf.image.dct.tuple-unsupported` |
| JPEG APP14 / `ColorTransform` 0, 1, 2, absent, or conflicting | Detect/skip | Reject | Candidate per case | No | No | Candidate | None until independently approved | IP-006 pending | `pdf.image.dct.color-transform-uncertain` |
| JPXDecode / JPEG 2000 | Detect/skip | Reject | Later | Later | No | No | None | IP-007 blocked V1 | `pdf.filter.jpx.unsupported` |
| JBIG2Decode | Detect/skip | Reject | Later | Later | No | No | None | IP-008 blocked V1 | `pdf.filter.jbig2.unsupported` |
| Standard 14 font-name/metric handling | Plan | Plan | — | — | No | Yes | Deterministic approved data only | IP-012 pending | `pdf.font.standard14.unavailable` |
| Embedded Type 1 / TrueType / OpenType / CFF font programs | Candidate | Candidate | Candidate | Candidate | No by default | Candidate | Explicit resource permission | IP-012 pending | `document.resource.permission-required` |
| Type 0/CID fonts and `ToUnicode` CMaps | Plan | Plan | — | — | No | Yes | Approved CMap/data only | IP-012/IP-013 pending | `pdf.text.mapping-missing-or-uncertain` |
| Latin, Greek, Cyrillic text export | — | Plan | — | — | No | Yes | Caller-supplied approved font | IP-012/IP-013 pending | `document.script.unsupported` |
| Complex scripts, bidi shaping, vertical writing, emoji sequences | Detect/skip | Later | — | — | No | No | None | IP-012/IP-013 pending | `document.script.unsupported` |
| Raw XMP packets | Detect/skip then drop | Reject | No | No | No | No | None | IP-004 pending | `document.metadata.raw-dropped` |
| Allowlisted normalized metadata | Plan | Plan | — | — | No | Yes | Explicit caller selection on write | IP-004/source review pending | `document.metadata.dropped` |
| URI/link values | Plan as inert values | Plan after policy admission | — | — | No | Yes | Never activated by codec | IP-014 pending | `document.uri.rejected` |
| Attachments, JavaScript, launch/remote/submit/multimedia actions | Detect/skip | Reject | No | No | No | No | None | IP-001 and security policy | `pdf.active-content.removed` |
| Tagged PDF / PDF/UA / PDF/A / PDF/X | Detect/skip | Later | — | — | No | No | No conformance claim | IP-017 blocked V1 | Profile-specific unsupported code |
| Digital signatures | Detect/skip with invalidation warning | Later | No validation | Later | No | No | No trust claim | IP-016 blocked V1 | `pdf.signature.not-validated` |

## Package and delivery

| Capability | V1 status | Evidence required |
|---|---|---|
| In-process `Broiler.Documents.Pdf` codec | Planned | Architecture tests; package tests |
| Standalone `Broiler.Pdf` process | Rejected | Phase 0 removal guard |
| PDF import to logical document | Planned | Reader corpus and semantic tests |
| PDF export from logical document | Planned | Pagination, writer, and interoperability tests |
| Layout-preserving round trip | Rejected | Not a product claim |
| Byte-preserving or incremental update | Post-V1 | Separate ADR and security review |

## Input and syntax

| Capability | V1 status | Notes / gate |
|---|---|---|
| PDF 1.7 syntax within enumerated subsets | Planned | ISO 32000-1 clearance and per-feature tests |
| PDF 2.0 tolerance | Planned | Qualified review; tolerance does not imply PDF 2.0 conformance |
| Classic cross-reference tables | Planned | Strict and bounded recovery corpus |
| Cross-reference streams | Planned | Filter and object-stream limits |
| Object streams | Planned | Shared object/decompression budgets |
| Linearized files | Planned | Read as ordinary files; no fast-web-view claim |
| Hybrid-reference files | Candidate | Must not weaken encryption or duplicate-object rules |
| Incremental revisions | Candidate | Read latest effective revision only; adversarial tests |
| Encrypted input | Rejected | Reject when `/Encrypt` is discovered |
| Digital signatures | Post-V1 | No validation, preservation, or signing claim |

## Stream filters and images

| Capability | V1 status | Ownership / gate |
|---|---|---|
| ASCIIHexDecode / ASCII85Decode / RunLengthDecode | Planned | PDF syntax layer; IP row and fuzz tests |
| FlateDecode and PNG predictors | Planned | Neutral compression/media capability where reusable |
| LZWDecode | Candidate | Legal/patent-history review and bounded decoder tests |
| DCTDecode (JPEG) | Planned | `Broiler.Media.Image`; JPEG tuple/APP14 register rows |
| JPXDecode (JPEG 2000) | Post-V1 | Separate standards, patent, decoder, and licensing review |
| CCITTFaxDecode | Candidate | Separate IP review and corpus |
| JBIG2Decode | Post-V1 | Separate high-risk security and patent review |
| Image masks / soft masks | Candidate | Compositing semantics and resource budgets |
| ICCBased color | Candidate | Color-management ownership and profile licensing |

## Text, fonts, and scripts

| Capability | V1 status | Notes / gate |
|---|---|---|
| Standard 14 font-name handling | Planned | No assumption that font programs are installed or redistributable |
| Embedded Type 1 / TrueType / OpenType data | Candidate | Embedding rights remain the content provider's responsibility |
| Type 0 and CID fonts | Planned | Unicode mapping and vertical-writing limits explicit |
| `ToUnicode` CMaps | Planned | Primary semantic extraction route |
| Fallback character inference without `ToUnicode` | Candidate | Confidence diagnostic; no silent correctness claim |
| Latin, Greek, Cyrillic export | Planned | Caller-supplied font and deterministic shaping tests |
| Complex scripts / bidi shaping / vertical writing | Post-V1 | Neutral shaping component and script corpus required |
| Emoji sequences and color fonts | Post-V1 | Font technology and rendering review required |

## Graphics and page content

| Capability | V1 status | Ownership / gate |
|---|---|---|
| Paths, fills, strokes, clipping, transforms | Planned | Reusable primitives in `Broiler.Graphics` |
| Text positioning and text state | Planned | PDF interpreter plus neutral geometry |
| Form XObjects | Planned | Recursion/resource limits |
| Transparency groups and blend modes | Candidate | Neutral graphics compositing ownership |
| Patterns and shadings | Candidate | Shared graphics capability; bounded evaluation |
| Optional content groups | Candidate | Logical visibility policy required |
| DeviceN / Separation color | Post-V1 | Color-management and conformance review |

## Semantics, metadata, and active content

| Capability | V1 status | Notes / gate |
|---|---|---|
| Normalized title/author/subject/keywords/dates | Planned | Allowlist only; privacy tests |
| Raw XMP preservation | Rejected | XMP review is separate; V1 drops raw packets |
| Links as inert semantic values | Planned | Never dereferenced by the codec |
| Annotations | Candidate | Allowlisted non-active subset only |
| AcroForm / XFA | Post-V1 | No form execution or fidelity claim |
| Attachments and embedded files | Rejected | No extraction or activation in V1 |
| JavaScript and active actions | Rejected | Diagnose and ignore without execution |
| Redaction or secure sanitization | Rejected | Conversion is not redaction |
| Tagged PDF / structure tree | Post-V1 | Separate accessibility architecture |
| PDF/UA, PDF/A, PDF/X conformance | Post-V1 | Profile-specific standards and validation required |

## Platform claims

| Platform | V1 status | Evidence required |
|---|---|---|
| .NET CLI | Candidate | Full import/export corpus and resource-limit tests |
| Windows | Candidate | Runtime, trimming, fonts, and deterministic-layout tests |
| Linux | Candidate | Runtime, fonts, globalization, and deterministic-layout tests |
| Android | Post-V1 | AOT/trimming, memory, and font provisioning |
| WebAssembly | Post-V1 | AOT/trimming, memory, streaming, and font provisioning |

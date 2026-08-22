# PDF IP, Licensing, And Standards Register

**Register version:** 0.1  
**Updated:** 2026-08-22  
**Owner:** Broiler.Documents maintainers  
**Approval authority:** Qualified legal reviewer designated by the project

This register is an engineering control, not legal advice. `Pending` is a
blocking state for implementation or public claims involving that row. A public
patent declaration or patent license is recorded as evidence, not interpreted as
worldwide freedom to operate.

## Decision fields

Each row must eventually identify the exact feature/subset, specification
edition, source and acquisition right, implementation jurisdictions, patent
evidence, copyright/license conditions, reviewer, decision date, expiry/review
date, and obligations. Changes to scope reopen the row.

| ID | Technology / exact scope | Primary evidence | Current assessment | Status / required action |
|---|---|---|---|---|
| IP-001 | ISO 32000-1:2008 / PDF 1.7 subsets enumerated in the feature matrix | Adobe [ISO 32000-1 Public Patent License](https://www.adobe.com/pdf/pdfs/ISO32000-1PublicPatentLicense.pdf); lawfully obtained standard | Adobe's license is limited to Adobe-owned essential claims for compliant implementations of the named standard and contains conditions, retaliation, revocation, and warranty limitations. It does not establish absence of third-party claims. | **Pending qualified review.** Record jurisdictions, compliant-implementation interpretation, attribution/notice obligations, and third-party search strategy. |
| IP-002 | ISO 32000-2 tolerance or features, including amendments | Lawfully obtained applicable editions; [ISO standards and patents policy](https://www.iso.org/iso-standards-and-patents.html) | IP-001 cannot be extended by inference. ISO states declaration data are informational and not verified for accuracy or relevance. | **Pending qualified review.** Do not claim PDF 2.0 conformance. Enumerate tolerated syntax separately from implemented features. |
| IP-003 | Adobe PDF extensions or implementation notes | Exact extension document and its terms | Public availability is not implementation permission and an extension may sit outside IP-001. | **Pending.** No extension is in scope until separately registered and approved. |
| IP-004 | XMP serialization or semantic preservation | [Adobe XMP Toolkit SDK](https://github.com/adobe/XMP-Toolkit-SDK); ISO 16684-1:2019 | The toolkit repository is BSD-licensed, while its specification refers to a separate XMP public patent license. Code license and specification/patent scope are distinct. | **Pending qualified review.** V1 drops raw XMP; normalized metadata must not depend on copying XMP implementation material. |
| IP-005 | JPEG baseline/progressive DCT as used by `DCTDecode` | ISO/IEC 10918-1 / ITU-T T.81; [ITU-T T.81 declarations](https://www.itu.int/ITU-T/recommendations/related_ps.aspx?id_prod=2633) | The declaration page lists statements and warns that the database is not certified accurate or complete. “JPEG” is not one indivisible clearance: process, arithmetic coding, Huffman coding, color interpretation, and container conventions differ. | **Pending qualified review.** Approve exact tuples (coding process, entropy mode, precision/components), jurisdictions, and decoder source. |
| IP-006 | JPEG APP14 Adobe marker and `ColorTransform` interpretation | Exact Adobe documentation/terms and approved interoperability evidence | APP14 behavior is separate from core JPEG decoding and may affect color correctness. | **Pending.** Do not infer permission or semantics from third-party code. |
| IP-007 | JPEG 2000 / JPXDecode | ISO/IEC 15444 editions and applicable declarations | Separate family from JPEG DCT with a distinct patent and implementation landscape. | **Blocked for V1 / Post-V1.** New review required before any decoder or sample is added. |
| IP-008 | JBIG2Decode | ISO/IEC 14492 / ITU-T T.88 and declarations | Distinct patent and security surface. | **Blocked for V1 / Post-V1.** Separate legal and threat-model approval required. |
| IP-009 | CCITT fax encodings | ITU-T T.4/T.6 and declarations | Exact modes and implementation jurisdictions are not yet reviewed. | **Pending qualified review** before inclusion. |
| IP-010 | LZWDecode | Applicable historical patents and current jurisdiction/status evidence | Common statements about patent expiry are not a project decision and may omit jurisdictions or later claims. | **Pending qualified review** before inclusion. |
| IP-011 | Flate/DEFLATE and predictor algorithms | Exact specifications and decoder implementation licenses | The implementation source and any copied tables/tests require provenance even where algorithm risk is considered low. | **Pending source/license review.** |
| IP-012 | Type 1, TrueType, OpenType, CFF/CFF2, and subsetting | Exact format editions; [Microsoft OpenType specification](https://learn.microsoft.com/en-us/typography/opentype/spec/) for selected OpenType scope | Font-format implementation and font-content embedding rights are separate. Installed or user-supplied fonts are not automatically redistributable or embeddable. | **Pending qualified review.** Record exact formats/tables and enforce font embedding permissions where technically available. No bundled font in V1. |
| IP-013 | Unicode mapping, normalization, bidi, and property data | Exact Unicode version and Unicode data/license terms | Versioned data files and generated tables require attribution/provenance review. | **Pending source/license review.** V1 script subset remains gated. |
| IP-014 | URI syntax and scheme handling | Exact RFC editions and approved implementation source | Standards text, copied test vectors, and scheme policy are separate concerns. | **Pending source review.** Codec treats URIs as inert values only. |
| IP-015 | Standard security handler / encryption | Exact ISO clauses and cryptographic specifications | Patent, export-control, security, and interoperability review not completed. | **Blocked for V1.** Encrypted inputs are rejected. |
| IP-016 | Digital signatures and certificate validation | Exact ISO clauses, cryptographic standards, trust-store behavior | Signature preservation and validation claims create substantial security and legal scope. | **Blocked for V1 / Post-V1.** Separate architecture and qualified review required. |
| IP-017 | PDF/A, PDF/UA, PDF/X or other profiles | Each exact profile edition, normative dependencies, validation and mark rules | Base-PDF work does not grant profile conformance. | **Blocked for V1 / Post-V1.** Separate standards acquisition, legal review, and conformance plan required. |
| IP-018 | “PDF” naming, Adobe references, certification or compatibility claims | Current trademark/brand guidance reviewed for target markets | Descriptive use must not imply Adobe sponsorship or certification. | **Pending qualified review** before public marketing or stable-package claims. |
| IP-019 | Old Broiler PDF source, binaries, fixtures, generated data, or output goldens | Per-artifact origin, author, license, hashes, and approval | None are approved sources merely because they are in project history or a local workspace. | **Rejected by default.** Delete/ignore; import only after independent approval and provenance record. |
| IP-020 | User/third-party PDFs, images, and fonts used as fixtures | Written grant/license, redistribution scope, privacy review | Possession or public download is not permission to commit or redistribute. | **Pending per artifact.** Prefer purpose-built generated fixtures once the generator sources are approved. |

## Review record

| Review | Reviewer | Date | Scope | Result |
|---|---|---|---|---|
| Engineering issue spotting | Codex, not legal counsel | 2026-08-22 | Phase 0 architecture and public primary-source pointers | Register created; no row legally cleared |
| Qualified legal review | _Unassigned_ | _Pending_ | Target jurisdictions and rows required by the first implementation slice | **Required for Phase 0 exit** |


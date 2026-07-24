# Broiler.Documents Roadmap

**Status:** Active preview. The codec family, RTF/DOCX/HTML/Markdown support,
Formatting Codes projection, and package projects are implemented. Only current
residual work is tracked here.

## API contract cleanup

- Resolve `DocumentReadOptions.DecodeEmbeddedObjects` before the public API is
  frozen. The option exists, but the RTF reader currently skips `\pict` and
  object destinations regardless of its value. Either implement a bounded,
  caller-composed image-import path or remove/replace the option so the public
  contract cannot imply behavior that does not exist.
- Re-review ADR 0004 and the read/write option surface after that decision.
- Freeze public names and XML documentation after a consumer review.

## Format fidelity

- Decide whether RTF list-table detection and style-sheet interpretation belong
  in the next supported subset. They are currently deliberate, documented
  limitations rather than partially supported behavior.
- Consider HTML list writing and relative-link policy only with conformance
  fixtures and explicit diagnostics.
- Extend Markdown or DOCX coverage only behind format-specific conformance tests;
  source-preserving round trips are not a goal for the normalized document
  model.

Intentional limitations in the conformance documents are not release blockers
unless they are explicitly promoted into this roadmap.

## Native PDF proposal

- Re-baseline the repository before reviving the old native-PDF parser proposal:
  there is currently no `src/Broiler.Pdf` project to extend.
- Make an explicit product scope and component-ownership decision first. Only
  then scaffold a bounded parser with corpus, limits, diagnostics, and
  differential tests; do not treat the obsolete project assumptions as a
  current implementation plan.

## Stabilization

- Add sustained fuzz/property coverage and allocation/performance baselines for
  every parser and writer that accepts untrusted input.
- Validate package consumption from a feed without the aggregate repository.
- Complete dependency, license, API-compatibility, and human review before a
  stable release.

UI host integration, RichEdit clipboard wiring, and the Writer Formatting Codes
experience are owned by their UI/application layers rather than this component.

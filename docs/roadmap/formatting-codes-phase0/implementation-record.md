# Formatting Codes Phase 0 implementation record

**Completed:** 2026-07-12  
**Roadmap:** [`../formatting-codes-pane-roadmap.md`](../formatting-codes-pane-roadmap.md)

This public record closes the roadmap's decision, provenance, deterministic
fixture, and regression-baseline gates. It is release-planning evidence, not a
legal opinion or a freedom-to-operate opinion.

## Approved decisions

| Decision | Approved outcome | Evidence |
|---|---|---|
| Public name | **Formatting Codes** | UI ADR 0020 |
| Initial shortcut | `Ctrl+Shift+F3`, plus menu/accessibility path | UI ADR 0020 |
| Meaning | Canonical semantic projection of `RichTextDocument`, not imported source bytes | Documents ADR 0006 |
| Grammar | Versioned, invariant ASCII commands with Unicode content and unambiguous escaping | Documents ADR 0006 |
| Source of truth | The editor's immutable document snapshot; no second model or undo stack | Both ADRs |
| MVP edit scope | Read-only; raw textual source editing requires a later ADR and gate | Both ADRs |
| Model assembly | Headless `Broiler.Documents.FormatCodes`, depending only on the document model | Documents ADR 0006 |
| UI assemblies | Per-type `Broiler.UI.FormatCodeView` and `.Standard` control family | UI ADR 0020 |
| Paragraph renderer gap | Show model state with **engine state; visual rendering pending** diagnostics | UI ADR 0020 |
| Canonical sample | `[Bold ON]Hello World![Bold OFF]` | Documents ADR 0006 |

ADRs:

- [`Broiler.Documents` ADR 0006](../../../Broiler.Documents/docs/adr/0006-formatting-codes-projection-and-grammar.md)
- [`Broiler.UI` ADR 0020](../../../Broiler.UI/docs/adr/0020-formatting-code-view-and-writer-integration.md)

## IP provenance and clearance boundary

Roadmap section 2 records the public prior-art and patent-marking research,
copyright/interface boundary, original-design rules, trademark attribution, and
commercial-release checklist. Its practical Phase 0 outcome is:

- clean-room implementation against Broiler's own normalized document model is
  reasonable to proceed with for engineering planning;
- **Formatting Codes** is used instead of the competitor feature name;
- no external code, assets, screenshots, manual prose, full code chart, branded
  shortcut, or pixel-identical visual treatment may be copied; and
- a claims-based patent search and live trademark/design review in actual launch
  markets remains a gate before a material commercial release.

The roadmap uses the registered-mark symbol on the first prominent reference
and includes one factual attribution. Repeating `®` or `™` on every mention is
not required. Project documentation must not imply affiliation or endorsement.

## Deterministic fixture baseline

The manifest at
[`tests/formatting-codes-phase0/fixtures.json`](../../../tests/formatting-codes-phase0/fixtures.json)
defines six generated-in-memory documents: 1K, 100K, and 1M low-density text;
100K text with 10,000 normalized runs; 10,001 empty paragraphs; and 100K
Unicode-heavy text. The runner validates exact sizes before measuring model-only
reference operations.

The captured Windows x64 result is
[`benchmark-baseline.windows-x64.json`](benchmark-baseline.windows-x64.json).
Timings are machine-specific and are not Phase 1 projector budgets. Fixture
sizes and structural counts are deterministic compatibility inputs.

## Regression baseline

All commands ran in Release configuration on .NET SDK 10.0.301 / runtime
10.0.9 on 2026-07-12.

| Scope | Result |
|---|---:|
| `Broiler.Documents.Model.Tests` | 17 passed, 0 failed |
| `Broiler.UI.RichEdit.Tests` | 76 passed, 0 failed |
| `Broiler.UI.RichEdit.Standard.Tests` | 59 passed, 0 failed |
| `Broiler.UI.RichEdit.Rtf.Tests` | 6 passed, 0 failed |
| Total automated tests | **158 passed, 0 failed** |
| Desktop `Broiler.Writer` | Release build passed |
| `Broiler.Writer.WebAssembly` | Release build passed |
| Phase 0 fixture runner | Release build passed; all six fixtures validated |

Writer currently has no dedicated test project, so successful desktop and
WebAssembly Release builds are the host baseline. A parallel first build of the
two hosts encountered an MSBuild intermediate-file lock; the WebAssembly build
passed cleanly when rerun after the concurrent builds completed. This is an
execution-concurrency artifact, not a product-code failure.

## Phase 1 entry condition

Phase 1 may scaffold the headless projector. It must publish the complete
version-1 command/escape table, add golden and property-based tests, reuse these
fixtures, and establish measured full-projector interaction thresholds before
the UI control phase begins.

# Phase 0 Record

Date: 2026-07-05

Roadmap source: `docs/roadmap/broiler-documents-component.md`, Phase 0
("Charter, ADRs, and the model-placement decision"), Path A.

Objective: approve the component boundary and resolve where the rich-text
document model lives, before any codec or model-move code is written. No runtime
assembly is added in Phase 0.

## Scope landed

- Added a new root component folder: `Broiler.Documents`, with `README.md` and
  `docs/` (charter + ADRs + this record).
- Chose **Path A (promote the model)** and recorded it as `Broiler.Documents`
  ADR 0002 and, on the UI side, `Broiler.UI` ADR 0018.
- Wrote Phase 0 ADRs: topology and consumption policy (0001), model
  ownership/promotion (0002), codec contract and signature probe (0003), read
  limits and RTF sanitization (0004), and the RTF first-release subset and text
  encoding (0005).
- Recorded the authoritative first-release RTF subset (ADR 0005) and the safety
  limits (ADR 0004).
- Captured the RichEdit test baseline that the Phase 1 promotion must preserve.
- Left all existing component source unchanged (no code moved yet; the move is
  Phase 1).

## Environment

Observed with `dotnet --info`:

| Item | Value |
| --- | --- |
| SDK | 10.0.301 |
| MSBuild | 18.6.4 |
| OS | Windows 10.0.26200 |
| RID | win-x64 |

## Model-promotion baseline

The Phase 1 promotion (ADR 0002) moves the pure model types out of
`Broiler.UI.RichEdit` into `Broiler.Documents.Model`. It is a mechanical move
guarded by the existing RichEdit test suite; a green run before and after the move
is the acceptance evidence. Baseline captured this pass:

| Test project | Command | Result |
| --- | --- | --- |
| `Broiler.UI.RichEdit.Tests` | `dotnet test Broiler.UI/Broiler.UI.RichEdit.Tests/Broiler.UI.RichEdit.Tests.csproj -c Release` | Passed: `74/74`, exit code `0` |
| `Broiler.UI.RichEdit.Standard.Tests` | `dotnet test Broiler.UI/Broiler.UI.RichEdit.Standard.Tests/Broiler.UI.RichEdit.Standard.Tests.csproj -c Release` | Passed: `55/55`, exit code `0` |

Total: `129/129` green. This is the regression gate for the ADR 0002 move.

Note: passing two project paths to a single `dotnet test` invocation fails with
`MSB1008` (MSBuild accepts one project at a time); run each project separately.

## Exit gate status

| Gate | Status |
| --- | --- |
| Component dependency graph approved | Complete — ADR 0001 (topology) and ADR 0002 (model dependency) |
| Model-placement path chosen and recorded | Complete — Path A; `Broiler.Documents` ADR 0002 + `Broiler.UI` ADR 0018 |
| First-release RTF subset explicit | Complete — ADR 0005 |
| Safety limits explicit | Complete — ADR 0004 |
| No public API commits to an unchosen code-page/indexing model | Complete — ADR 0005 targets the opaque `RichTextPosition`; no byte/UTF-16 offsets exposed |
| Codec contract and probe fixed before scaffolding | Complete — ADR 0003 |

## Follow-up before Phase 1

- Scaffold `Broiler.Documents.Model` and perform the ADR 0002 move; re-run the
  129-test baseline before and after.
- Retarget `InternalsVisibleTo` (for `RichTextPosition`) from `Broiler.UI.RichEdit`
  to `Broiler.Documents.Model`, granting `Broiler.UI.RichEdit.Standard` and the
  model test assembly.
- Scaffold `Broiler.Documents` with the codec contract, catalog, probe, options,
  limits, and diagnostics; add architecture tests (no UI/DOM/`*.Windows` in Model
  or core; no hidden global codec registration).
- Register the new projects in `Broiler.slnx`.

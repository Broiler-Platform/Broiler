# HtmlBridge complexity-reduction — Phase 0 baseline (P0.3)

Status: recorded 2026-07-13

This is the reproducible behavioral/performance baseline for the
[HtmlBridge complexity-reduction roadmap](htmlbridge-complexity-reduction-roadmap.md).
It satisfies the Phase 0 exit criterion *"Baseline test and benchmark artifacts are
committed or linked"* by pointing at the already-committed baseline artifacts, giving
the exact commands to reproduce each, and recording the status observed at this
baseline so a later phase cannot misattribute a pre-existing or environmental failure
to its own change.

Phase 0 changed only the bridge namespace/public surface (P0.1) and added guard tests
(P0.2). Neither alters runtime behavior of the product assemblies, so every behavioral
and performance baseline below is expected to hold unchanged; they are recorded here as
the reference the later phases (which *do* move behavior) gate against.

## Build / public-surface baseline (P0.1)

- The HtmlBridge scope and its consumers build with **zero errors**:
  `Broiler.HtmlBridge.{Core,Dom,Rendering,Scripting}`, `Broiler.Cli`,
  `Broiler.Cli.Tests`, `Broiler.Wpt`, `Broiler.Wpt.Tests`, and the
  `Broiler.Engines.Baseline` consumer.
- The v2 public type name `Broiler.HtmlBridge.DomBridge` compiles from a real
  (non-test) consumer fixture — `src/Broiler.Engines.Baseline/Program.cs`
  (`using Broiler.HtmlBridge; new DomBridge();`).

Reproduce:

```sh
dotnet build src/Broiler.HtmlBridge.Dom/Broiler.HtmlBridge.Dom.csproj -c Release
dotnet build src/Broiler.Wpt.Tests/Broiler.Wpt.Tests.csproj -c Release
dotnet build src/Broiler.Engines.Baseline/Broiler.Engines.Baseline.csproj -c Release
```

**Known pre-existing / environmental build failures** (present at clean `HEAD`,
unrelated to this roadmap — do not attribute to it): the full `Broiler.slnx` build
reports 6 errors in `Broiler.Input.Camera.Windows`, `Broiler.Input.Microphone.Windows`
(missing `Broiler.Input.Windows` WinRT projection) and `Broiler.Writer.FormatCodes.Tests`
(missing `Broiler.Writer.WriterUiHost` / `WriterApp`). None reference HtmlBridge.

## Public API surface baseline (P0.2)

- Committed baselines: `src/Broiler.Cli.Tests/ApiBaselines/Broiler.HtmlBridge.*.PublicApi.txt`
  (Core, Dom, Rendering, Scripting).
- Guarded by `HtmlBridgePublicApiSnapshotTests`; drift fails the test.

Reproduce / refresh after an intentional surface change:

```sh
dotnet test src/Broiler.Cli.Tests -c Release \
  --filter "FullyQualifiedName~HtmlBridgePublicApiSnapshotTests"
# regenerate approved baselines after a deliberate change:
UPDATE_API_BASELINES=1 dotnet test src/Broiler.Cli.Tests -c Release \
  --filter "FullyQualifiedName~HtmlBridgePublicApiSnapshotTests"
```

## Architecture / dependency baseline (P0.2)

- `HtmlBridgeArchitectureGuardTests` and `HtmlBridgeBoundaryGuardTests` lock the
  roadmap dependency rules — canonical `Broiler.{Dom,CSS,CSS.Dom,Dom.Html,Layout}`
  reference no bridge/JavaScript assembly; the bridge builds on the canonical engines.
- Tripwire: `Broiler.HtmlBridge.Rendering` still references `Broiler.HTML.Image`
  (the Phase 1 removal target); the test flips when the dependency is removed.

```sh
dotnet test src/Broiler.Cli.Tests -c Release \
  --filter "FullyQualifiedName~HtmlBridgeArchitectureGuardTests|FullyQualifiedName~HtmlBridgeBoundaryGuardTests"
```

## Behavioral baseline — Acid

- `Broiler.Cli.Tests` `Acid3RegressionTests` + `Acid3CssComplianceTests`.
- Observed at this baseline: **84 / 85 pass**. The single failure,
  `Acid3CssComplianceTests.Border_Shorthand_Expands_Color_To_Individual_Sides`, is
  **pre-existing at clean `HEAD`** (verified by stash), not introduced by Phase 0.

```sh
dotnet test src/Broiler.Cli.Tests -c Release \
  --filter "FullyQualifiedName~Acid3RegressionTests|FullyQualifiedName~Acid3CssComplianceTests"
```

## Behavioral baseline — WPT / pixel

WPT and its pixel comparison are gated against committed snapshots, not re-run in full
here (the corpus is ~23k tests and the raster path is environment-sensitive):

- Per-segment regression snapshots and the aggregate manifest live in
  [`tests/wpt-baseline/`](../../tests/wpt-baseline/) (`README.md`, `failed-tests.json`,
  `rf-layout-curated.json`). The gate (`scripts/check-wpt-regression.sh`,
  `.github/workflows/wpt-tests.yml`) fails only on **regression** vs the snapshot.
- Triage/status: [wpt-triage-and-diagnostics.md](wpt-triage-and-diagnostics.md).

Reproduce a subset (pixel threshold 99% match):

```sh
dotnet run --project src/Broiler.Wpt -- --wpt-dir tests/wpt \
  --reference-dir tests/wpt/references --subset <path> --failure-images <dir>
# or the segment harness used by CI:
bash scripts/run-wpt-tests.sh --subset "css/CSS2"
```

## Performance baseline — bridge.mutation and engine benchmarks

- Committed baseline: [`tests/m0-baseline/performance/engine-benchmark-baseline.json`](../../tests/m0-baseline/performance/engine-benchmark-baseline.json)
  (+ `.md`). Gated metrics: `bridge.mutation`, `html.raster`, `js.startup`, at a **+2%**
  slowdown budget.
- `bridge.mutation` baseline (repeated `textContent` mutations through the DOM bridge):
  **mean 1,099,027 ns/op**, median 1,108,983 ns/op.

Reproduce on a full dev/CI environment (writes json+markdown, compares to the baseline,
exits non-zero on a gated regression):

```sh
dotnet run --project src/Broiler.Engines.Baseline -c Release -- benchmarks \
  --baseline tests/m0-baseline/performance/engine-benchmark-baseline.json \
  --budget-percent 2
# to avoid overwriting the committed baseline while checking, redirect output:
#   --output-dir <scratch-dir>
```

**Environmental note:** the benchmark's HTML rendering metrics (`html.parse/layout/paint/raster`,
`bridge.render-handoff`) require the `Broiler.HTML.Image.Compat` provider, which is loaded
dynamically at runtime and is not deployable in the bare sandbox container (same
environmental class as the full WPT raster run and the PDF-conversion tests). The
`bridge.mutation` metric itself is pure DOM mutation with no rendering; because Phase 0
(P0.1 namespace-only, P0.2 test-only) emits identical product IL, the committed baseline
remains authoritative and is reproduced on a full environment, not in-container.

## How later phases use this baseline

Each subsequent phase runs the smallest relevant rows from the roadmap validation
matrix and compares against the artifacts above: no public-API drift without an approved
baseline refresh, no new architecture-rule violation, no Acid/WPT regression vs the
committed snapshots, and `bridge.mutation` within +2% of the committed number.

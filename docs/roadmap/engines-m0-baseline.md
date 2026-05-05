# Engines M0 baseline and instrumentation

This page is the M0 **baseline of record** for the cross-engine roadmap in
[`engines-standards-and-performance-roadmap.md`](./engines-standards-and-performance-roadmap.md).
It ties together the PR dashboard workflow, the published conformance numbers,
the benchmark harness outputs, and the current bridge-boundary map.

## Unified dashboard entry points

- PR workflow: [`.github/workflows/engines-m0-dashboard.yml`](../../.github/workflows/engines-m0-dashboard.yml)
- JavaScript subset runner: [`src/Broiler.Engines.Baseline`](../../src/Broiler.Engines.Baseline)
- Bridge boundary map: [`../architecture/htmlbridge-engine-boundaries.md`](../architecture/htmlbridge-engine-boundaries.md)
- Bridge API/spec map: [`../architecture/htmlbridge-spec-map.md`](../architecture/htmlbridge-spec-map.md)
- Published benchmark output: [`../../tests/m0-baseline/performance/engine-benchmark-baseline.md`](../../tests/m0-baseline/performance/engine-benchmark-baseline.md)
- Published Test262 subset output: [`../../tests/m0-baseline/conformance/test262-subset/test262-subset-summary.md`](../../tests/m0-baseline/conformance/test262-subset/test262-subset-summary.md)

## What runs on every PR

The M0 dashboard keeps the PR signal intentionally small and green while still
covering all three engines:

1. **Test262 subset** via `Broiler.Engines.Baseline test262`, compared against
   the committed M0 baseline so PRs fail only on regression
2. **WPT-relevant suites** via `Broiler.Cli.Tests.WptCssVariablesTests` and
   `Broiler.Cli.Tests.WptFontAndSelectorTests`
3. **Acid3 regression** via `Broiler.Cli.Tests.Acid3RegressionTests`
4. **Engine baseline benchmarks** via `Broiler.Engines.Baseline benchmarks`,
   compared against the committed JSON baseline with a **≤ 2%** slowdown budget
   on the current representative gate metrics: `js.startup`, `html.raster`, and
   `bridge.mutation`

The broader WPT runner remains available separately in
[`.github/workflows/wpt-tests.yml`](../../.github/workflows/wpt-tests.yml) for
larger slices and artifact-heavy triage.

## Conformance baseline of record

| Signal | Current baseline | Notes / source |
|---|---:|---|
| Test262 curated subset | **4 / 6 passed (66.7%)** | [`test262-subset-summary.md`](../../tests/m0-baseline/conformance/test262-subset/test262-subset-summary.md) pinned to `tc39/test262@d0c1b4555b03dd404873fd6422a4b5da00136500` |
| WPT HTML slice | **1,686 passed / 1,440 failed / 6,602 skipped** | [`tests/html/wpt-results/wpt-triage-summary.md`](../../tests/html/wpt-results/wpt-triage-summary.md); executed pass rate **53.9%**, overall pass rate **17.3%** |
| WPT CSS slice | **2,267 passed / 1,984 failed / 20,669 skipped** | [`tests/css/wpt-results/wpt-triage-summary.md`](../../tests/css/wpt-results/wpt-triage-summary.md); executed pass rate **53.3%**, overall pass rate **9.1%** |
| Acid3 JavaScript score | **100 / 100** | [`acid3-compliance.md`](./acid3-compliance.md) |
| Acid3 visual fidelity | **42.68% viewport match** | [`acid3-compliance.md`](./acid3-compliance.md) viewport-constrained full-image/content-area baseline |

### Current Test262 subset failures

The initial M0 subset records two known JavaScript gaps:

- `JSON.stringify uses toJSON return values` → Broiler currently returns `{}`
  where the Test262 case expects `[false]`
- `Array.isArray recognises proxied arrays` → proxied arrays currently report
  `false`

That failure list is intentional M0 instrumentation: the dashboard now records
these gaps on every PR instead of rediscovering them ad hoc.

## Performance baseline of record

The published benchmark harness lives in
[`src/Broiler.Engines.Baseline`](../../src/Broiler.Engines.Baseline) and emits
machine-readable JSON plus Markdown summaries under
[`tests/m0-baseline/performance/`](../../tests/m0-baseline/performance/).
The PR workflow now treats any benchmark mean slowdown above **2%** versus the
committed JSON baseline as a regression for the current representative gate
metrics `js.startup`, `html.raster`, and `bridge.mutation`; the full suite is
still published on every run for manual review and later gate expansion.

| Metric | Unit | Mean | Median |
|---|---|---:|---:|
| `js.startup` | ms | 2.914 | 2.513 |
| `js.micro` | ms | 3.560 | 3.788 |
| `html.parse` | ms | 3.861 | 1.706 |
| `html.layout` | ms | 5.048 | 3.508 |
| `html.paint` | ms | 325.048 | 264.327 |
| `html.raster` | ms | 286.414 | 285.767 |
| `bridge.dom-call` | ns/op | 2238.079 | 1763.250 |
| `bridge.mutation` | ns/op | 818681.583 | 795240.000 |

## Bridge boundaries and leaks

M0 also publishes the current bridge seam document here:

- [`../architecture/htmlbridge-engine-boundaries.md`](../architecture/htmlbridge-engine-boundaries.md)

The main leaks captured there are:

- public bridge APIs still require concrete `JSContext` / `JSValue` types
- `DomBridge` still exposes HTML-engine `DomElement` instances directly
- CLI capture still owns bridge-specific async draining details
- the bridge-to-renderer hand-off is still serialized HTML, not a typed tree

M1 now freezes those leak points in the boundary doc and maps the public
`Broiler.HtmlBridge` API surface to spec anchors in
[`../architecture/htmlbridge-spec-map.md`](../architecture/htmlbridge-spec-map.md).

## Reproducing the published baselines

```bash
dotnet run --project src/Broiler.Engines.Baseline --configuration Release -- test262
dotnet run --project src/Broiler.Engines.Baseline --configuration Release -- benchmarks
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --configuration Release --filter "FullyQualifiedName~Broiler.Cli.Tests.WptCssVariablesTests|FullyQualifiedName~Broiler.Cli.Tests.WptFontAndSelectorTests"
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj --configuration Release --filter "FullyQualifiedName~Acid3RegressionTests"
```

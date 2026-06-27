# Octane benchmark harness

Runs Google's [Octane 2.0](https://github.com/chromium/octane) JavaScript
benchmark suite under two engines and produces a side-by-side comparison:

| Engine | How it runs |
|---|---|
| **Chromium** | Real V8 in a headless browser, driven by Playwright. |
| **Broiler** | The `BroilerJS --script-host` shell (the Broiler.JS engine). |

## Running

```bash
# Full run (clones chromium/octane, builds BroilerJS, installs Chromium):
./scripts/run-octane-benchmarks.sh

# Faster local iteration against an existing checkout / build:
./scripts/run-octane-benchmarks.sh --octane-dir /path/to/octane --skip-build --engines broiler
```

In CI the [Octane Benchmarks workflow](../../.github/workflows/octane-benchmarks.yml)
(`workflow_dispatch`) runs both engines and commits the refreshed results.

## How it works

Octane registers one `BenchmarkSuite` per benchmark file. The shared runner
[`scripts/octane-runner.js`](../../scripts/octane-runner.js) runs the registered
suites and reports each score. In a JS shell (no `window`) Octane runs
synchronously and the runner prints `OCTANE_RESULT_JSON {…}`; in a browser page
it yields via `setTimeout`, so the Playwright driver awaits a Promise instead.

Each suite is executed **in isolation** — a fresh Chromium page or a fresh
Broiler process per suite — driven by the manifest
[`scripts/octane-suites.json`](../../scripts/octane-suites.json). This is
deliberate: Broiler is experimental, so a suite may score, throw a catchable
error, hang, or abort the whole process. Isolation means one bad suite never
discards the others. Each suite is classified `ok` / `error` / `timeout` /
`crash`. The orchestration lives in [`scripts/run-octane.mjs`](../../scripts/run-octane.mjs).

The overall score is the geometric mean of the per-benchmark scores an engine
completed, matching Octane's own methodology.

## Layout

```text
tests/octane/
├── package.json            # Playwright dependency (committed)
├── package-lock.json       # committed
├── checkout/               # chromium/octane clone (gitignored, runtime)
├── node_modules/           # gitignored, runtime
└── results/                # committed
    ├── chromium-results.json
    ├── broiler-results.json
    ├── comparison.json
    └── comparison.md       # human-readable table
```

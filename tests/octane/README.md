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
(`workflow_dispatch`) runs both engines and commits the refreshed results. It
also uploads the per-suite logs as an `octane-logs` artifact.

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

## Diagnosing a failing suite

A suite that reports `error` or `crash` is a Broiler bug report, so the harness
keeps the evidence rather than a one-line summary of it.

**Start with [`results/diagnostics.md`](results/diagnostics.md).** For every
suite that did not complete it gives the failing exception type, the benchmark /
phase / iteration it died in, the engine (.NET) stack, the JavaScript stack, and
a command to re-run that one suite.

Three things make that report possible:

- **Broiler's own error detail is kept in full.** When a .NET fault surfaces as
  a catchable JS error, the exception type and its entire managed stack live in
  the error's *message* — that is the diagnostic, and the runner captures it
  instead of truncating to the first line. An unhandled throw is recovered from
  the process's stderr instead.
- **Stack traces are mapped back to Octane sources.** Broiler runs one
  concatenated script per suite, so its traces cite lines in a temporary file.
  `run-octane.mjs` records where each part landed and rewrites every citation to
  the file it came from, so a frame reads `base.js:371`, not `<temp>:2109`.
- **The runner leaves breadcrumbs.** [`scripts/octane-runner.js`](../../scripts/octane-runner.js)
  prints a line on entering each `Setup` / `run` / `tearDown` phase (and on
  iterations 1, 2, 4, 8, … of the measured loop). A suite that aborts the
  process never reports a result, but its last breadcrumb still names the
  benchmark, phase, and iteration that were live when it died — and the
  `OCTANE_FILE_LOADED` markers say which file was reached, which is what
  separates a load-time failure from a run-time one.

Full per-suite output — stdout, stderr, exit code (decoded: a .NET unhandled
exception, a stack overflow, an access violation), duration, and a repro
command — is written to `logs/<engine>/<suite>.log`. The combined script for a
failing suite is kept next to it, so the failure can be re-run by hand.

```bash
# Re-run one suite with the child's output streamed live:
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only Crypto --verbose
```

`--only` writes everything under `logs/partial/`, so a debugging run never
overwrites the committed full-run results. Other flags that help:
`--keep-scripts` (keep the combined script for passing suites too), `--no-trace`
(drop the breadcrumbs for an undisturbed timing run), and `--broiler-env K=V`
to pass an engine diagnostic switch through, e.g.
`--broiler-env BROILER_GENERATE_IL_LOGS=1`.

The parsing this depends on is covered by
[`harness-selftest.mjs`](harness-selftest.mjs), which runs against recorded
BroilerJS output and needs no engine, checkout, or network:

```bash
node tests/octane/harness-selftest.mjs
```

## Layout

```text
tests/octane/
├── package.json            # Playwright dependency (committed)
├── package-lock.json       # committed
├── harness-selftest.mjs    # committed; checks the diagnostic parsing
├── checkout/               # chromium/octane clone (gitignored, runtime)
├── node_modules/           # gitignored, runtime
├── logs/                   # gitignored, runtime
│   ├── broiler/
│   │   ├── <suite>.log     # full stdout/stderr, exit code, repro command
│   │   └── scripts/        # combined script, kept for failing suites
│   ├── chromium/<suite>.log
│   └── partial/            # output of a --only run
└── results/                # committed
    ├── chromium-results.json
    ├── broiler-results.json
    ├── comparison.json
    ├── comparison.md       # human-readable table
    └── diagnostics.md      # why each failing suite failed
```

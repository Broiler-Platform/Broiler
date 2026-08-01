# Octane benchmark harness

Runs Google's [Octane 2.0](https://github.com/chromium/octane) JavaScript
benchmark suite under two engines and produces a side-by-side comparison:

| Engine | How it runs |
|---|---|
| **Chromium** | Real V8 in a headless browser, driven by Playwright. |
| **Broiler** | The `BroilerJS --script-host` shell (the Broiler.JS engine). |

This file covers *how* the harness runs. For what each benchmark actually does,
which engine subsystem it loads, and where Broiler's time goes on it, see
[`benchmarks.md`](benchmarks.md); for the plan that follows from those findings,
[`roadmap.md`](roadmap.md).

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

### Measuring, rather than just running

A single run tells you whether a suite completes. It does **not** tell you
whether a score moved — run-to-run variance is comfortably larger than most
changes worth making. To get a number a change can be judged against:

```bash
./scripts/run-octane-benchmarks.sh --repetitions 3
```

Each suite runs three times; `comparison.md` reports the **median** score per
benchmark and the observed **spread**, flagging with `⚠` anything outside the
noise band (`--noise-band`, default 7.5%, matching the baseline profile in
`Broiler.JS/eng/performance/phase0.json`). Every repetition keeps its own log
(`<suite>.rep1.log`, …), and a suite that passes some repetitions and fails
others is reported as **flaky** rather than averaged into a pass.

The comparison also leads with the three numbers the run is read for: how many
of the expected scores were reported, the geomean, and the **spread between the
best and worst suite** — the last being the one [`roadmap.md`](roadmap.md) is
organized around.

### Per-suite time budgets

`--timeout` (default 180 s) is a **floor**. A suite that genuinely needs longer
raises its own budget with `timeoutSec` in
[`scripts/octane-suites.json`](../../scripts/octane-suites.json) — currently
Mandreel (1200 s) and zlib (1800 s), which measured 313 s and 647 s under
Broiler. Raising `--timeout` still widens every suite at once for a debugging
run. The budget a suite ran under is recorded in its log and its status entry.

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

# Octane benchmark harness

Runs Google's [Octane 2.0](https://github.com/chromium/octane) JavaScript
benchmark suite under three engines and produces a side-by-side comparison:

| Engine | How it runs |
|---|---|
| **Chromium** | Real V8 in a headless browser, driven by Playwright. |
| **Broiler** | The `BroilerJS --script-host` shell (the Broiler.JS engine). |
| **Jint** | [Jint](https://github.com/sebastienros/jint), a managed ECMAScript interpreter, through the [`jint-host`](jint-host) shell. |

Broiler is the subject; the other two are reference points, and they answer
different questions. Chromium says how far Broiler is from V8 — a JIT-compiling
C++ engine, so the answer is a large number that moves slowly. **Jint says how
Broiler compares to another managed engine on the same runtime**, running the
same script in the same process shape, and that ratio sits near 1 — which makes
it the number a Broiler change can actually be judged by.

Jint is a reference, not a target: it is an AST interpreter with no compilation
tier at all, so beating it is a floor rather than a goal, and a suite where
Broiler *loses* to an interpreter is pointing at a specific defect.

This file covers *how* the harness runs. For what each benchmark actually does,
which engine subsystem it loads, and where Broiler's time goes on it, see
[`benchmarks.md`](benchmarks.md); for the plan that follows from those findings,
[`roadmap.md`](roadmap.md).

## Running

```bash
# Full run (clones chromium/octane, builds the two shells, installs Chromium):
./scripts/run-octane-benchmarks.sh

# Faster local iteration against an existing checkout / build:
./scripts/run-octane-benchmarks.sh --octane-dir /path/to/octane --skip-build --engines broiler

# The two managed engines, no browser needed:
./scripts/run-octane-benchmarks.sh --octane-dir /path/to/octane --engines broiler,jint
```

In CI the [Octane Benchmarks workflow](../../.github/workflows/octane-benchmarks.yml)
(`workflow_dispatch`) runs all three engines and commits the refreshed results.
It also uploads the per-suite logs as an `octane-logs` artifact.

Benchmarking takes hours, so the branch has usually moved by the time there is
anything to commit. The commit step goes through
[`scripts/ci-commit-generated-results.sh`](../../scripts/ci-commit-generated-results.sh),
which rebuilds its commit on the branch tip as it is *then* and pushes again
instead of failing on the non-fast-forward — merge PRs during a run, not around
it. Results are generated wholesale, so if two runs touch the same file the
later measurement wins; nothing else on the branch is disturbed.

A single-engine run still refreshes the whole comparison: any per-engine result
already in `results/` is folded back in, so `--engines jint` updates the Jint
column against the last committed Chromium and Broiler ones. That is only honest
when the runs come from the same machine — which is why the published numbers
come from the workflow rather than from a workstation.

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
organized around. Below them sits **Broiler ÷ Jint**, the geometric mean of the
per-benchmark ratios over the suites both managed engines completed. It is a
geomean of ratios rather than a ratio of geomeans because each engine's geomean
is taken over whatever suites *it* finished, and dividing those compares two
different suite sets.

### Per-suite time budgets

`--timeout` (default 180 s) is a **floor**. A suite that genuinely needs longer
raises its own budget with `timeoutSec` in
[`scripts/octane-suites.json`](../../scripts/octane-suites.json) — currently
Mandreel (1200 s) and zlib (1800 s), which measured 313 s and 647 s under
Broiler. Raising `--timeout` still widens every suite at once for a debugging
run. The budget a suite ran under is recorded in its log and its status entry.

Those budgets are set by Broiler, which is the slow one: Jint's longest suite
(Mandreel) measured 104 s, inside the 180 s floor, so no suite needs a raised
budget on its account.

## How it works

Octane registers one `BenchmarkSuite` per benchmark file. The shared runner
[`scripts/octane-runner.js`](../../scripts/octane-runner.js) runs the registered
suites and reports each score. In a JS shell (no `window`) Octane runs
synchronously and the runner prints `OCTANE_RESULT_JSON {…}`; in a browser page
it yields via `setTimeout`, so the Playwright driver awaits a Promise instead.

Each suite is executed **in isolation** — a fresh Chromium page or a fresh shell
process per suite — driven by the manifest
[`scripts/octane-suites.json`](../../scripts/octane-suites.json). This is
deliberate: Broiler is experimental, so a suite may score, throw a catchable
error, hang, or abort the whole process. Isolation means one bad suite never
discards the others. Each suite is classified `ok` / `error` / `timeout` /
`crash`. The orchestration lives in [`scripts/run-octane.mjs`](../../scripts/run-octane.mjs).

Broiler and Jint go through the same path — one `dotnet <shell> <combined script>`
process per suite — differing only in which assembly is launched. The two shells
also expose the *same* host surface: `print`, `read`, and no `window`. That is
deliberate. A benchmark that fails for want of a host function then fails the
same way under both, and the comparison stays a statement about the engine
rather than about which shell was more generous. The Jint side of it is
[`jint-host/Program.cs`](jint-host/Program.cs), which mirrors the BroilerJS
shell down to the 16 MiB stack JavaScript runs on.

The overall score is the geometric mean of the per-benchmark scores an engine
completed, matching Octane's own methodology.

## Diagnosing a failing suite

A suite that reports `error` or `crash` is a Broiler bug report, so the harness
keeps the evidence rather than a one-line summary of it.

**Start with [`results/diagnostics.md`](results/diagnostics.md).** For every
suite that did not complete it gives the failing exception type, the benchmark /
phase / iteration it died in, the engine (.NET) stack, the JavaScript stack, and
a command to re-run that one suite. It carries a section per shell engine:
a Jint failure is not a Broiler bug, but *both* managed engines failing a suite
the same way is the fastest way to tell a Broiler defect from a benchmark
expecting a host facility neither shell provides.

Three things make that report possible:

- **The engine's own error detail is kept in full.** When a .NET fault surfaces
  as a catchable JS error, the exception type and its entire managed stack live
  in the error's *message* — that is the diagnostic, and the runner captures it
  instead of truncating to the first line. An unhandled throw is recovered from
  the process's stderr instead: Broiler prints its own dump there, and Jint's
  escapes as an unhandled .NET exception whose inner exception is the JavaScript
  stack.
- **Stack traces are mapped back to Octane sources.** Each shell runs one
  concatenated script per suite, so its traces cite lines in a temporary file.
  `run-octane.mjs` records where each part landed and rewrites every citation to
  the file it came from, so a frame reads `base.js:371`, not `<temp>:2109`. Both
  frame spellings are understood — Broiler's `at <fn>:<file>:<line>,<col>` and
  Jint's V8-style `at <fn> (<file>:<line>:<col>)`.
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
BroilerJS and Jint output and needs no engine, checkout, or network:

```bash
node tests/octane/harness-selftest.mjs
```

## Layout

```text
tests/octane/
├── package.json            # Playwright dependency (committed)
├── package-lock.json       # committed
├── harness-selftest.mjs    # committed; checks the diagnostic parsing
├── jint-host/              # committed; the Jint shell — Program.cs and a csproj
│                           #   whose only dependency is the Jint package
├── checkout/               # chromium/octane clone (gitignored, runtime)
├── node_modules/           # gitignored, runtime
├── logs/                   # gitignored, runtime
│   ├── broiler/
│   │   ├── <suite>.log     # full stdout/stderr, exit code, repro command
│   │   └── scripts/        # combined script, kept for failing suites
│   ├── jint/               # same shape as broiler/
│   ├── chromium/<suite>.log
│   └── partial/            # output of a --only run
└── results/                # committed
    ├── chromium-results.json
    ├── broiler-results.json
    ├── jint-results.json
    ├── comparison.json
    ├── comparison.md       # human-readable table
    └── diagnostics.md      # why each failing suite failed, per engine
```

The Jint shell is built by `run-octane-benchmarks.sh` alongside the BroilerJS
one and belongs to no solution: it is a harness tool, not a Broiler component,
and it references no repository project.

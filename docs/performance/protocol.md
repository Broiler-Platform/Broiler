# Measurement and acceptance protocol

§3.1–§3.4 — what may be *claimed*, how to run the Octane harness and the engine probes, and the conformance gates every item passes through.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

---

## 3. Measurement and acceptance protocol

### 3.1 What may be claimed

[`Broiler.JS/docs/performance.md`](../../Broiler.JS/docs/performance.md) is unchanged
and unchallenged by this document. To *claim* a performance result:

1. same commit, idle physical machine, power plan, RID, CPU feature overrides, GC
   mode, and publish properties;
2. cold lifecycle results kept separate from warmed microbenchmarks;
3. **two runs inside the configured band** (7.5% for the `baseline` profile, 20% for
   `smoke`, which only verifies wiring);
4. time, allocation, working set, file count and publish bytes reported together;
5. the semantic owner and focused test262 manifests named in
   [`Broiler.JS/eng/performance/ownership.json`](../../Broiler.JS/eng/performance/ownership.json).

Release matrix: **win-x64, linux-x64, linux-arm64.** SIMD claims additionally require
x64 with the feature enabled and disabled, and an AdvSimd-capable Arm64 host.

For the Octane half of that matrix the
[Octane Benchmarks workflow](../../.github/workflows/octane-benchmarks.yml) takes a
`platform` input — one RID, or `all` to fan out to a job per RID on `ubuntu-latest`,
`windows-latest` and `ubuntu-24.04-arm`. Each writes and commits its own
`tests/octane/results/<platform>/`, because a score off one machine says nothing
about another; a locally driven run picks the same directory up from
`--platform`, defaulting to the host's own RID. That is the *harness* covering the
matrix, not the matrix being satisfied — §3.1's other conditions (an idle physical
machine above all) still decide whether a run may be claimed, and a GitHub-hosted
runner is not one.

> **Standing caveat on every number in §4.** The engine campaign's figures come from
> an ad-hoc in-process harness on a shared 4-core container with 10–15% run-to-run
> variance, reporting the slower of two runs. Allocation counts are deterministic and
> exact; timings are for **prioritization only**. Not one of them has been through
> the gates above.

### 3.2 Running the Octane harness

```bash
./scripts/run-octane-benchmarks.sh --repetitions 3
```

A single run tells you whether a suite completes; it does **not** tell you whether a
score moved — run-to-run variance is comfortably larger than most changes worth
making. With `--repetitions n` the harness reports the **median** per benchmark plus
the observed spread `(max − min) / median`, flagging `⚠` anything outside
`--noise-band` (default 7.5%, matching the `baseline` profile).

Three properties of that design are load-bearing:

- **A default run is unchanged byte for byte.** One repetition ⇒ the median is the
  sample, no stability data, no spread column.
- **Each repetition keeps its own log** (`<suite>.rep1.log`, …), so a flake keeps the
  evidence of the run that failed.
- **A suite is `ok` only if it was `ok` every time.** Mixed verdicts report `flaky`,
  never an average. Averaging a flake into a pass is the failure mode the harness
  exists to prevent.

Expect the two latency scores to be the noisy ones, and treat that as data — a wide
band on SplayLatency is itself a pause-distribution result.

**Per-suite budgets.** `--timeout` (default 180 s) is a **floor**; a suite that needs
longer raises its own via `timeoutSec` in
[`scripts/octane-suites.json`](../../scripts/octane-suites.json) — currently Mandreel
(1200 s, measured 313 s) and zlib (1800 s, measured 647 s). Before this, CI was
overriding the global timeout to 1800 s, which meant a genuine hang anywhere else had
thirty minutes to look like work.

**Isolation.** One fresh process or page per suite, driven by the manifest. Broiler is
experimental — a suite may score, throw, hang, or abort the process — and isolation
means one bad suite never discards the other sixteen. Failures are classified
`ok` / `error` / `timeout` / `crash` / `flaky`, with full evidence in
[`tests/octane/results/<platform>/diagnostics.md`](../../tests/octane/results/linux-x64/diagnostics.md).

Harness parsing is covered by a test that needs no engine, checkout, or network:

```bash
node tests/octane/harness-selftest.mjs
```

### 3.3 Running the engine probes

Run from the `Broiler.JS` submodule root:

```powershell
python scripts/performance/collect_phase0.py --profile baseline --include-eventpipe --include-build-baselines --include-publish --rid win-x64
```

The collector records commit/dirty state, commands, runtime, OS/RID, processor, GC and
tiering settings, lifecycle samples, BenchmarkDotNet results, package graph, managed
assembly sizes, and optional publish results. Machine-specific output belongs under the
ignored `Broiler.JS/artifacts/performance/`, never in a Markdown result log. Retain the
raw BenchmarkDotNet, EventPipe, binary-log, IL and publish artifacts with release
evidence. The probe corpus itself is Appendix A.

**Bootstrap profile matters to any startup number.** `JavaScriptBootstrap` and
`JavaScriptContextBuilder` take a `JavaScriptBootstrapProfile` — `Full` (lazy
Intl/Temporal realization), `FullEager` (the comparison/compatibility profile), or
`Minimal` (deliberately reduced and non-conformant). Say which one a measurement used:
a smaller package or faster context is not a win if required globals are absent.

### 3.4 Conformance gates

The pinned manifests are `test262-arrays`, `test262-properties-proxy`,
`test262-strict-mode`, `test262-realm-isolation`, and — added 2026-08-03, see below —
`test262-lexical-declarations`. First taken 2026-08-01 at `cdb2fd41`
(suite ref `ccaac100`), **re-run 2026-08-02 at `a6f101cc` plus 2-9 with every count
unchanged**, **re-run at `71dda1b7` plus 3-3 with every count unchanged**, and **re-run five
times on 2026-08-03 on linux-x64 at `9bf9639b` (the pin at the time) — plus `patches/0067`, plus `0067` and
`0068`, plus `0067`–`0069`, plus `0067`–`0070`, and plus all five of `0067`–`0071` — with every count
identical every time, manifest by manifest** — so the table below describes the pinned pointer as well as the commit it was first
measured at.

**Re-run 2026-08-05 on linux-x64 at `cca39b4d` (the pin at the time) plus item 3-1's order-preserving guard
placement, on both settings of its switch. On the shipping arm every count is identical to the row
below, manifest by manifest — 8 710 executed, 8 617 passed, 84 failed, 251 skipped, 9 timed out.**
This is the run that most needed taking of anything in phase 3, because the change *removes an
eligibility rule whose entire justification is observable evaluation order* — a lost `valueOf`
call, a coercion that stops running, or a throw arriving from the wrong operand would surface here
rather than in a box count. So the arms were compared **file by file** and not only by total, which
is what makes the next paragraph readable at all.

**One test moved between two non-passing buckets on the control arm, and it is worth stating
exactly rather than as "identical".** `test262-arrays` reads **17 failed / 9 timed out** on the
ordered arm — the recorded row — and **18 / 8** on the hoisting one, because
`built-ins/Array/prototype/toReversed/length-exceeding-array-length-limit.js` was killed by the
30 s timeout in one and reported as a failure in the other, with empty stderr both times. **The set
of 26 non-passing files is the same on both arms**, and that file is already tracked in
`Broiler.JS/scripts/compliance/test262-failures.txt` as one of the nine integer-limit cases CI has
carried for a while. The other four manifests agree **file for file** on both arms (38, 26, 3 and
0 non-passing). So: no test passes on one arm and fails on the other, and what moved is which side
of a wall-clock boundary a known-failing test landed on under `--max-workers 4`. *It is recorded
because a total that reads 84 against 85 would otherwise look like a regression, and because
"identical" would have been the easy and wrong word.*

`--max-workers 4`; the suite came from a `git fetch --depth 1` of the pinned `ccaac100` passed
through `--suite-root`, for the reason recorded below, and the runner's own *"Selected 3 160
runnable test(s)"* for `arrays` is what says it is the same corpus.

**Re-run 2026-08-04 on linux-x64 at `07adeb44` plus `patches/0082` (item 1-1's remaining half) —
now `0aa8a558`, an ancestor of the pin, so this run describes the pinned tree rather than a local
build: every count is identical to the row below, manifest by manifest — 8 710 executed,
8 617 passed, 84 failed, 251 skipped, 9 timed out over all five.** The failures and timeouts are
the same *files*, not merely the same totals: all **84** failures need `$262` — including the 13
`language/global-code/script-decl-*` cases, every one of which includes it — and the **9** timeouts
are lines 7–15 of `test262-failures.txt`, nine for nine. The manifests that matter here are
`strict-mode` and `lexical-declarations` rather than `arrays`, because what `0082` removes is a
repeat of the walk that decides *which bindings a nested function captures*, and a lost capture
would surface as a scoping failure rather than an arithmetic one.

**The suite came from a git checkout at the pinned ref rather than from the runner's own download,
and that is a harness change worth recording.** `codeload.github.com` and `api.github.com` both
return **403** through this session's proxy, so `run_test262.py`'s `ensure_local_suite_root` cannot
fetch at all; `git fetch --depth 1 origin ccaac100…` against `github.com` succeeds, and
`--suite-root` takes the resulting checkout. What says this is the same corpus rather than a
smaller one is the runner's own selection count printed before it runs anything — **"Selected 3 160
runnable test(s)"** for `arrays`, which is the executed count in the row below to the test, and the
same for the other four.

**Re-run 2026-08-04 on linux-x64 at `61c8cc65` (the pin at the time), plus `patches/0078` (item 3-7), plus
`0078`–`0079` (item 3-8), plus `0078`–`0080` (item 3-1) and plus `0078`–`0081` (item 3-2): every
count is identical to the row below, manifest by manifest, on every arm.** The `0080` run matters most of the five, because that
patch changes what six core operators *emit* — `&`, `|`, `^`, `<<`, `>>`, `>>>` — and
`test262-arrays` is thick with `ToUint32` edge cases. All five manifests were run on 3-7's switch-ON arm — the shipping configuration — and
all five again with `BROILER_JS_CAPTURED_NUMERIC_LOCALS=0`; `properties-proxy` was then run a third
time at `0078`–`0079` with nothing else building, and a fourth on a **pristine build of the pin**
as a control. The last two agree **file for file** on which 38 fail, which is what makes this a
control rather than a matching total.

**One run of `properties-proxy` on the switch-ON arm came back 3 949 / 39, and the extra failure
was mine, not the engine's.** The stderr the runner captured says so outright: *"The JavaScript
compiler is not available. Reference the Broiler.JavaScript.Compiler assembly to enable script
compilation."* That child process had loaded `Broiler.JavaScript.Compiler.dll` **while a
`dotnet build` of the same solution was rewriting it** — a build I started for an unrelated edit
while the manifest was still running. It is not a `$262` case, it is not an assertion failure, and
`built-ins/Object/getOwnPropertyDescriptor/15.2.3.3-3-4.js` passes three times for three when run
alone on the widened build, and answers correctly on the widened build, on the same build with the
switch off, and on a pristine build of the pin. The manifest-level controls settle it: **a pristine
build of `61c8cc65`, the switch-off arm, and a re-run at `0078`–`0079` with nothing else building
all report 3 950 / 38 and agree file for file on which 38 fail** — so the 39th file is in none of
the three and is not a property of any change here.

> *This is §3.5's "check that the thing you measured is the thing you built", arriving from the
> other side: there the binary under test was older than the source, here it was being rewritten
> underneath a running suite.* **Do not build while a suite is running against the output.** The
> first diagnosis was "a flake under `--max-workers 8`" — plausible, consistent with the test
> passing three times in isolation, and wrong; what settled it was reading the captured stderr
> instead of re-running until it went away. A failure that reproduces nowhere is not thereby a
> flake, and the runner had recorded the real reason all along.

**Re-run 2026-08-07 on linux-x64 at `e5dc2610` (the pin) plus `patches/0115` (phase 5's item 2),
with `BROILER_JS_REGEX_TIERING=1` — the arm where the mechanism actually fires. Every count is
identical to the row below, manifest by manifest: 8 710 executed, 8 617 passed, 84 failed, 251
skipped, 9 timed out.** This is the manifest set that matters for that item, because `0115`
changes which `Regex` object serves a hot pattern and `properties-proxy` is thick with
`RegExp.prototype` receiver and descriptor cases — a promotion that altered a capture layout or a
`lastIndex` progression would surface there rather than in a benchmark. **The failing set is
checked as files rather than as totals**: all **84** failures need `$262`, verified by reading
each one's source rather than by matching a count, and the **9** timeouts are nine of nine the
integer-limit cases already tracked in `Broiler.JS/scripts/compliance/test262-failures.txt`. The
suite came from a `git fetch --depth 1` of the pinned `ccaac100` passed through `--suite-root`,
and the runner's own *"Selected 3 160 runnable test(s)"* for `arrays` is what says it is the same
corpus. Nothing else was building while it ran, for the reason recorded above.

| Manifest | Executed | Passed | Failed | Skipped | Timed out | Engine failures |
|---|---:|---:|---:|---:|---:|---:|
| `test262-arrays` | 3 160 | 3 134 | 17 | 0 | 9 | **0** |
| `test262-properties-proxy` | 3 988 | 3 950 | 38 | 13 | 0 | **0** |
| `test262-strict-mode` | 1 066 | 1 040 | 26 | 27 | 0 | **0** |
| `test262-realm-isolation` | 99 | 96 | 3 | 4 | 0 | **0** |
| | **8 313** | **8 220** | **84** | **44** | **9** | **0** |
| **`test262-lexical-declarations`** *(new)* | **397** | **397** | **0** | 207 | 0 | **0** |

Every one of the 84 failures needs `$262` (`createRealm`, `detachArrayBuffer`, or a
harness include that uses one), which the raw script host does not provide. All 9
timeouts are already tracked in `Broiler.JS/scripts/compliance/test262-failures.txt` —
lines 7–15, nine for nine, the integer-limit `slice`/`unshift`/`reduceRight`/
`toReversed` cases CI has carried for a while.

**`test262-lexical-declarations` is new, and it closes a gap rather than reporting one.**
Item 3-3's `let`/`const` half changes how lexical bindings are *compiled*, and **no pinned
manifest covered `let` or `const` at all** — `test262-language-basics` is twelve entries about
`throw`, commas and relational operators. The manifest is
`language/statements/{let,const,variable}` plus `language/block-scope`, and it was run **six
times from the same tree**: at `9bf9639b` (the pin at the time), and at that commit plus each successive
prefix of `patches/0067`–`0071`. **Identical, 397 of 397 passing on each.** So it did not
*detect* anything — its value is that a future regression on those paths now fails a pinned gate
instead of passing unnoticed, and `language/statements/variable` is exactly what `0068` touches.
The 207 skips are the negative-syntax and module cases the runner excludes by design, not silent
failures.

**Still not covered:** the Annex B forbidden-extension paths that P0-3 gates on
(`test/annexB/built-ins/Function`, `forbidden-ext/b2`) are in no manifest. Adding them
changes what CI enforces, so it is an open item rather than a silent edit.

> **Check out the suite with `core.autocrlf=false` on Windows, or `strict-mode` reports 27
> failures instead of 26.** Git's Windows default rewrites every LF to CRLF on checkout, and
> `built-ins/Function/prototype/toString/line-terminator-normalisation-LF.js` asserts that a
> function containing an LF round-trips through `toString` as an LF — so converting the *test
> file* makes it assert the opposite of its name. All 37 of its lines arrive as CRLF and it
> fails; its `CR` and `CR-LF` siblings are unaffected, which is why the damage is one test and
> not a family. Found while running 3-3, where the one-count difference from the recorded row
> was the only thing standing between "unchanged" and a claim that would have been wrong.
>
> **This is the third time the same root cause has produced a fake engine failure**, after the
> two §3.4 tooling defects below, and it is worth naming the general form: *a test whose subject
> is its own bytes cannot survive any layer that normalizes bytes* — the harness writing the
> assembled script (fixed), and now the checkout that supplies the file. `git cat-file -p HEAD:<path>`
> is the check, because it prints the blob rather than the working copy; re-checking out will not
> fix it once the index has recorded the translated form.

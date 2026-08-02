# Broiler performance and benchmark roadmap

The single plan for JavaScript execution speed and the benchmark evidence that
governs it. It merges two documents that were each correct about half the
picture:

| Merged from | What it contributed |
|---|---|
| [`Broiler.JS/docs/performance-roadmap.md`](../Broiler.JS/docs/performance-roadmap.md) | The engine-internal campaign — P0–P3, phases A–F, their measurements, and the open items each deliberately left behind |
| [`tests/octane/roadmap.md`](../tests/octane/roadmap.md) | The forward plan driven by Octane 2.0 — phases 0–5, ordered by where the suite says the losses are |
| [`tests/octane/benchmarks.md`](../tests/octane/benchmarks.md) | What each benchmark exercises, and the ranked blockers B1–B8 that connect the two |

**It lives in the main repository, not in `Broiler.JS`, for a structural reason:**
the plan spans both. The harness, the suite manifest and the workflow are main-repo
(`tests/octane/`, `scripts/`, `.github/workflows/octane-benchmarks.yml`); the engine
is a submodule. A document inside `Broiler.JS` cannot link outward to the harness,
so only the parent can hold the combined view.

- **Owner assemblies:** `Broiler.JavaScript.Runtime`, `.Engine`, `.BuiltIns`,
  `.Storage`, `.Compiler`, `.Parser`, plus `Broiler.Regex` for phase 5.
- **Acceptance protocol:** unchanged and unchallenged —
  [`Broiler.JS/docs/performance.md`](../Broiler.JS/docs/performance.md) governs what
  may be *claimed*. **Nothing in this document closes on the numbers it quotes.**
- **Provenance:** the pinned submodule pointer is **`685026c0`**, checked 2026-08-01 —
  two commits past the `b3f53dcc` this document named a revision ago and nine past the
  `cdb2fd41` both source documents name; `cdb2fd41`, `7ef80c03` and `8228b0da` all remain
  ancestors, so item 0-1's substance holds. Those two commits are item 0-9's probe corpus
  (`aa2b1562`, #938), which means the corpus 0-9 describes is now *in* the pointer rather
  than pending against it. **Measurements and the test262 run in §4.1 and §3.4 were taken
  at `cdb2fd41` and have not been repeated at `685026c0`** — which also carries a
  string-allocation fix (#936). Octane code sites verified at `45f4f679`. Item rows were
  checked against the tree rather than inherited from the prose above them; doing that is
  what caught this, and this time it also caught that **item 1-2's acceptance criterion
  already passed before any work** (phase 1).

> **Path convention.** Because this document moved up a level, every path is written
> **relative to the repository root**. Paths carrying a `Broiler.JS/` prefix are inside
> the submodule — the source documents wrote those without it. Source *files* named in
> the item tables (`Runtime/ObjectShape.cs`, `BuiltIns/Function/JSFunction.cs`, …) are
> relative to `Broiler.JS/Broiler.JavaScript.*`, as they were in the original.

---

## 1. What the merge produces that neither document had

Two findings come out of putting these side by side. Both change the plan, so they
lead.

### 1.1 The engine roadmap's scope statement is wrong, and Octane is why

[`performance-roadmap.md` §9](../Broiler.JS/docs/performance-roadmap.md) declares two
areas out of scope:

> - **Parsing and compilation.** Fresh-context startup is 1.20 ms and
>   `script:evaluation` runs in 37 ms; neither showed up as a bottleneck.
> - **A real JIT / tiered compilation.** […] Everything above is achievable in the
>   current architecture.

Both exclusions are **superseded here**, and the reason is not a change of opinion —
it is that the probe corpus could not see the effect:

- **Front end.** The probes are one-liners in a fresh `JSContext`. Octane runs 15
  large real programs, and the two worst scores in the entire suite are the two that
  measure nothing but the front end: **MandreelLatency at 4646×** and **CodeLoad at
  371×**. `script:evaluation` at 37 ms was a true measurement of a corpus small
  enough that eager compilation is free. It is not free on jQuery, on the TypeScript
  compiler, or on a 152,948-line generated function. **The front end is phase 1.**
- **Speculation.** Engine §9 scoped itself to "achievable in the current
  architecture", which was an honest boundary for a bookkeeping-removal campaign. The
  remaining ~100× is not achievable inside it. **Speculation is phase 4**, and the
  scaffolding for it is already built and tested.

This is the general hazard the merge exists to close: **an in-process probe answers
"what does this operation cost", and a benchmark suite answers "what does this
program spend its time on".** Neither substitutes for the other, and the excluding
section was written with only the first.

*(Section numbers prefixed **engine** or **Octane** refer to a source document; bare
`§n.n` refers to this one. Phases are always named, never numbered as sections.)*

### 1.2 Both roadmaps are blocked on the same missing thing

They describe it differently and it is one gate:

- The Octane roadmap's phase 0 owes **a workflow run** — the committed results
  predate the engine fixes by ~15 hours, so the geomean, the coverage count and the
  spread all quote a superseded engine.
- Engine §8.1 owes **acceptance evidence** — no BenchmarkDotNet comparison, no
  RID-matrix run, and the probes that produced every number in it have no permanent
  home.

Neither list can be *judged* until both are satisfied, and they satisfy the same
requirement from two directions. They are merged into **Phase 0**, which is
consequently the only phase with no engineering in it and the only one that blocks
everything else.

---

## 2. Metrics

Track five numbers, in two families. Reporting one without the other is how the two
source documents ended up disagreeing.

### 2.1 The suite view — three numbers per Octane run

| Metric | Last committed run | Target |
|---|---|---|
| **Scores reported** out of 17 | 12 / 17 (stale) | **17 / 17** |
| **Geomean** over all 17 scores | 245 over the 12 that completed; ≈244 including the five *(0046)* measurements | — |
| **Spread** = worst ÷ best, as ×-slower-than-Chromium | 4646 / 45 ≈ **103×** | **< 5×** |

**Spread is the organizing metric.** Because the suite total is a geometric mean,
flattening the curve and raising the total are the same work: moving MandreelLatency
from 14.5 to 1000 is worth more than tripling every score already above 300. A run
where every suite is uniformly 150× off is a far healthier engine than today's at a
similar geomean, because no single subsystem is pathological.

All three are emitted by `run-octane.mjs` into `results/comparison.md` and
`comparison.json`, so the trend comes out of the run rather than being reconstructed
by hand.

### 2.2 The engine view — time *and* allocation per hot path

Wall clock alone hid the largest result of the completed campaign. The shadow-stack
change (§4.1) took an argument-less call from 80 B to **0 B** with throughput
*unchanged*; the pooled predecessor was banked as an allocation win at no speedup.
Conversely P2-2's item 3 looked like an allocation change and turned out to be worth
9–99% of wall clock once its eligibility gate was widened.

**Report time, allocation, and working set together.** This is already the rule in
`performance.md`; it is restated because the campaign twice found the interesting
half in the column it was not looking at.

### 2.3 Which number answers which question

| Question | Instrument |
|---|---|
| Did this operation get cheaper? | In-process probes, Appendix A |
| Did the cache actually start hitting? | `PropertyOptimizationDiagnostics.Snapshot()` |
| Did real programs get faster? | Octane, ≥3 repetitions, median + spread |
| Is the engine still correct? | test262 over the pinned manifests |
| May we publish the number? | `performance.md` gates only — none of the above |

---

## 3. Measurement and acceptance protocol

### 3.1 What may be claimed

[`Broiler.JS/docs/performance.md`](../Broiler.JS/docs/performance.md) is unchanged
and unchallenged by this document. To *claim* a performance result:

1. same commit, idle physical machine, power plan, RID, CPU feature overrides, GC
   mode, and publish properties;
2. cold lifecycle results kept separate from warmed microbenchmarks;
3. **two runs inside the configured band** (7.5% for the `baseline` profile, 20% for
   `smoke`, which only verifies wiring);
4. time, allocation, working set, file count and publish bytes reported together;
5. the semantic owner and focused test262 manifests named in
   [`Broiler.JS/eng/performance/ownership.json`](../Broiler.JS/eng/performance/ownership.json).

Release matrix: **win-x64, linux-x64, linux-arm64.** SIMD claims additionally require
x64 with the feature enabled and disabled, and an AdvSimd-capable Arm64 host.

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
[`scripts/octane-suites.json`](../scripts/octane-suites.json) — currently Mandreel
(1200 s, measured 313 s) and zlib (1800 s, measured 647 s). Before this, CI was
overriding the global timeout to 1800 s, which meant a genuine hang anywhere else had
thirty minutes to look like work.

**Isolation.** One fresh process or page per suite, driven by the manifest. Broiler is
experimental — a suite may score, throw, hang, or abort the process — and isolation
means one bad suite never discards the other sixteen. Failures are classified
`ok` / `error` / `timeout` / `crash` / `flaky`, with full evidence in
[`tests/octane/results/diagnostics.md`](../tests/octane/results/diagnostics.md).

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
`test262-strict-mode`, `test262-realm-isolation`. As of 2026-08-01 at `cdb2fd41`
(suite ref `ccaac100`):

| Manifest | Executed | Passed | Failed | Skipped | Timed out | Engine failures |
|---|---:|---:|---:|---:|---:|---:|
| `test262-arrays` | 3 160 | 3 134 | 17 | 0 | 9 | **0** |
| `test262-properties-proxy` | 3 988 | 3 950 | 38 | 13 | 0 | **0** |
| `test262-strict-mode` | 1 066 | 1 040 | 26 | 27 | 0 | **0** |
| `test262-realm-isolation` | 99 | 96 | 3 | 4 | 0 | **0** |
| | **8 313** | **8 220** | **84** | **44** | **9** | **0** |

Every one of the 84 failures needs `$262` (`createRealm`, `detachArrayBuffer`, or a
harness include that uses one), which the raw script host does not provide. All 9
timeouts are already tracked in `Broiler.JS/scripts/compliance/test262-failures.txt` —
lines 7–15, nine for nine, the integer-limit `slice`/`unshift`/`reduceRight`/
`toReversed` cases CI has carried for a while.

**Still not covered:** the Annex B forbidden-extension paths that P0-3 gates on
(`test/annexB/built-ins/Function`, `forbidden-ext/b2`) are in no manifest. Adding them
changes what CI enforces, so it is an open item rather than a silent edit.

### 3.5 Standing measurement lessons

These were paid for once each. They apply to every phase below.

- **A premise is not a finding.** P3 blamed the five `using` scopes around every call,
  built the fast path, measured it, and found no signal — the scopes never allocated.
  The real cost was an 80-byte activation record they were hiding. *Measure before
  implementing, and be willing to throw the implementation away.*
- **An acceptance criterion is a claim too — run it before the work.** 1-2's was "a
  generated 200k-line single-function script compiles without overflow", and it passed on
  the untouched tree: the item had inherited *size* from the line count of the function it
  was found on, when the cause was *nesting*. A criterion that passes before the change
  measures nothing and hides the real one. *Write the failing case first, and check that it
  fails.*
- **Reproduce on the platform you will close on.** 1-2's repro was a win-x64 Octane run.
  The same suite completes on linux-x64 at the same pointer, so the CI run that was meant
  to confirm it never could. *A one-platform repro dates the item to that platform.*
- **A benchmark named as an item's justification is a test that item has to pass.** 2-8 existed
  because of DeltaBlue's 601× score, was measured with a loop written to look like DeltaBlue's hot
  path, and **broke DeltaBlue** — the real suite threw before scoring. The loop reproduced the
  *reads* the item was about and none of the *writes* the item's change also affected, so it could
  not have failed. Octane was available, takes minutes, and no test in 7 347 caught it. *A
  resemblance to a benchmark is not evidence about that benchmark; run the thing you named.*
- **A conservative bug passes its own tests.** 2-0 invalidated too much, never too little,
  so every staleness test in `PropertyShapeCacheTests` was green — and green for a reason
  none of them was checking. A correctness suite cannot find an
  over-invalidation, only a hit-rate counter can, which is why 0-9's emitter found in one
  run what twenty tests had been sitting on. *Fixing an over-invalidation invalidates the
  tests that covered it: re-check each path in the condition the fix creates.*
- **Verify a premise before building on it, and separate it from its explanation.** 2-1
  named the wrong missing structure — the shape-transition cache it called absent is
  present and working — while being exactly right about the symptom it predicted. *An item
  can be worth doing and still be wrong about why; a control run tells you which half you
  have.*
- **A deferral is a claim too, and it needs a citation.** 2-4 shipped its update half with a
  written reason for not doing the compound half: an abstraction the compound path "goes
  through". It does not — that abstraction serves the *identifier* form, and the member form was
  three lines below the branch the item had just edited. *A sentence explaining why work was
  skipped will be read as a finding by whoever arrives next, so it needs the same file-and-line
  backing as one explaining why work was done.* The cost here was small; the deferral was mine
  and I returned to it. Aimed at a stranger it is a dead end with a plausible-sounding sign on it.
- **"Pure removal" is a claim about the code, and it is usually wrong.** 2-3 proposed deleting
  one of two stores. They serve different access paths, so neither can go; the item's real
  content was a storage-layer redesign, and its measured ceiling was 3% of the most favourable
  workload available. *Before writing "pure removal", delete the thing in a scratch build and
  see what breaks — here it took one probe, and the wrong answer it returned was the proof.*
- **An item can be overtaken by the items before it, and it has happened twice.** 2-3 was
  written when a store cost two key lookups; P1-3 and 2-1 removed the second, so most of its
  value was collected before anyone reached it. 2-5 was written against "an `AsyncLocal` per
  write" when P0-2 had already removed the expensive half — the ExecutionContext *write* — and
  left only a read that measures at 0%. Both were re-measured on arrival and neither survived.
  *Re-measure an item's premise when the work before it lands, not only when it is written* —
  and note that in both cases the item was inheriting a cost description from the campaign that
  had already fixed it.
- **Name the half you mean.** "The `AsyncLocal` is on the hot path" was true of 2-5 and told
  nobody that the write was the cost and the read was not. An item that says which operation,
  on which population, at what frequency can be checked in one probe; one that names a mechanism
  cannot be checked at all until someone rebuilds the reasoning.
- **Compare against the right pair.** Pooling frames measured as "no cost" against
  *allocating* them. Against an array slot it was worth 11%. The first comparison
  showed recycling costs about what allocating costs — not that either is free.
- **A failing test is a claim, not a verdict.** Five "pre-existing failures" asserted
  behaviour the engine is right to refuse; the pinned suite settled each faster than
  reasoning from spec text. Two of the five contradicted a test262 vector the engine
  passes. Separately, three *harness* defects produced five failures that looked like
  engine defects and were not.
- **Check how much of the probe the change can reach.** 2-4's compound half first measured
  0.915 with two of eleven pairs the wrong way, on a *top-level* `o.x += 1` loop that spends most
  of its time resolving a global binding the change never touches. The same change on the
  in-function shape: 0.903, seven of eight the same direction, against a control at 1.002. *A
  probe whose bulk is inert dilutes the effect and keeps all of the noise* — and it fails
  downward, so it reads as "the change barely helps" rather than as a broken measurement.
- **Interleave, at process granularity.** Sub-1.5% effects are only visible ABBA-
  interleaved across independent builds, ten runs each, medians compared.
- **The local suite will not catch a lifetime bug.** All three frame-recycling defects
  appeared as corrupted parent chains — two only as an intermittent hang. The suite
  stayed green through every one. Diagnosis was bisection to a failure *rate*
  (20–40 runs per configuration), not reading.
- **Mutation-test an invariant.** Both frame-lifetime rules pass the JavaScript-level
  tests with either rule deleted, because the corruption needs a job-queue
  interleaving the xUnit host does not reproduce. Assert the rule against the API.

---

## 4. Where the engine stands

### 4.1 Completed — phases A–F (implemented, none *closed*)

Every item below is implemented and covered by repository tests. **None is closed**,
for the reason Phase 0 exists: the acceptance evidence has not been collected.

| Phase | Items | Result |
|---|---|---|
| **A** | P0-1, P0-3 | Prototype invalidation on every value allocation removed (800 013 → 3 per 200k loop); legacy `caller`/`arguments` made lazy via a deferred *data* property, preserving the Annex B descriptor shape. **2.0–2.9× on call paths, 6× less call allocation** |
| **B** | P0-2 | The ambient strict-mode scope stores a `bool` and writes **only on a transition**, so same-strictness call chains write nothing. `[ThreadStatic]` was rejected — it loses the value across async resumption |
| **C** | P1-1, P1-4 | Property writes stopped destroying the object's shape. A monomorphic read went **0 hits / 200 000 misses → 199 999 / 1**; constructor-built three-field objects **6 595 B → 1 480 B**, against 1 328 for the literal |
| **D** | P1-2, P1-3 | Prototype and class method calls now hit the cache (**0 → ~400k hits**); constant-key stores go through a store cache (2.1×, or 3.6× when the key is not one-character early-interned) |
| **E** | P2-1, P2-2 | Descriptor-free `push`; per-thread small-integer cache; unboxed `double` locals. Plus two array defects found by measuring: **repeated `pop` was quadratic (729×)** and array fill went **1 350 B → 145 B per element** |
| **F** | P2-3, P2-4, P3 | Dense element = one reference, not a 32-byte descriptor (`new Array(1000)` −73%); string concatenation no longer quadratic (**150×** on the accumulation loop); the per-call activation record became a slot in a context-owned array addressed by a struct token — an argument-less call allocates **nothing**, and call-heavy code runs **3–15% faster** (median ≈11%) |

Headline before/after on the probes:

| Hot path | Before | After | Factor | Alloc before | Alloc after |
|---|---:|---:|---:|---:|---:|
| Plain function call (sloppy) | 945 ms | 327 ms | **2.9×** | 1 784 B | **264 B** |
| Closure call | 953 ms | 357 ms | **2.7×** | 1 816 B | **296 B** |
| Prototype method call | 861 ms | 370 ms | **2.3×** | 1 632 B | **264 B** |
| Built-in call (`Math.max`) | 443 ms | 217 ms | **2.0×** | 400 B | **176 B** |
| Empty `for` loop | 426 ms | 210 ms | **2.0×** | 96 B | 96 B |
| Own property read | 491 ms | 333 ms | **1.5×** | 128 B | 128 B |
| Integer arithmetic | 476 ms | 342 ms | **1.4×** | 128 B | 128 B |
| `s = s + x` × 20 000 | 1 604 ms / 3.20 GB | **10.7 ms / 4.4 MB** | 150× | 913 gen2 | **0 gen2** |
| `script:dromaeo-object-array` | 5 564 ms | **646 ms** | 8.6× | — | — |
| `script:stopwatch` (real script) | 976 ms | 669 ms | **1.5×** | 736 MB | **264 MB** |

Repository suite at `cdb2fd41`: **7 284 tests across 13 projects, 7 281 passing.** The
three failures are host-environment, not engine — `ReproTests.Repro` (a debugging
leftover writing to a hardcoded `D:\Broiler.JS\` path, asserting nothing) and two
`Issue838Tests` date cases that assume a UTC host. Baseline before attributing either
to a change. Note also that `Broiler.JS/BroilerJS.sln` **cannot restore** — it
references `Broiler.Regex` paths that do not exist — so `Broiler.JS.slnx` at the
repository root is the solution to run.

### 4.2 The current Octane profile

Committed scores plus the five *(0046)* measurements taken on the fixed engine. **The
committed run is stale** — see Phase 0.

| Benchmark | Chromium | Broiler | × slower | Dominant blocker |
|---|--:|--:|--:|---|
| SplayLatency | 69 725 | 1 539 | **45** | — (best axis; GC pauses are fine) |
| Typescript | 86 327 | 1 009 *(0046)* | 86 | mixed; overhead amortized by real work |
| Gameboy | 90 650 | 1 041 | 87 | B1 typed arrays, B3 exotic exclusion |
| NavierStokes | 35 432 | 341 | 104 | B1 boxed array elements |
| RegExp | 9 890 | 89.9 | 110 | B5 regex engine |
| Splay | 43 027 | 283 | 152 | B1 allocation rate |
| Box2D | 99 321 | 584 | 170 | B1 + B2 (no escape analysis, no inlining) |
| PdfJS | 58 725 | 321 *(0046)* | 183 | B1, B5, B4 |
| EarleyBoyer | 91 547 | 339 | 270 | B1 allocation rate |
| RayTrace | 117 436 | 403 | 291 | B1 + B2 escape analysis |
| Mandreel | 47 996 | 160 | 300 | B4 compile, B1 heap traffic |
| Crypto | 38 183 | 127 *(0046)* | 301 | B1 integer boxing |
| zlib | 80 514 | 237 *(0046)* | 340 | B1 integer boxing |
| CodeLoad | 30 916 | 83.4 *(0046)* | 371 | **B4 eager compilation** |
| Richards | 46 754 | 108 | 433 | **B2 call cost, B3 shape transitions** |
| DeltaBlue | 102 708 | 171 | 601 | **B2 polymorphic call cost** |
| MandreelLatency | 67 368 | 14.5 | **4 646** | **B4 compile latency** |

The shape of that list *is* the finding: the extremes are front-end and call-path,
not arithmetic. The losses are concentrated in two subsystems rather than spread
evenly — which is what makes them addressable in a defined order.

### 4.3 The blockers, ranked

The bridge between §4.1 (what the engine does) and the phases (what to do). Ordered by how
much of the gap each accounts for.

**B1 · Every JavaScript value is a heap-allocated object.** `JSValue` is
`public abstract partial class JSValue` — a CLR reference type. No Smi tagging, no
NaN-boxing. Integer arithmetic allocated **128 B/iteration**; an *empty* `for` loop,
96. Two mitigations landed (small-integer cache, unboxed `double` locals) but the
second has a deliberately narrow gate. **Object fields, array elements, parameters,
return values, and anything crossing a call boundary are still boxed.** The single
largest multiplier, and it applies to all 17 scores. → **phase 3**

**B2 · One non-speculative compile tier.** source → `FastParser` → `FastCompiler` →
LINQ expression trees → IL via `Broiler.JavaScript.ExpressionCompiler` → RyuJIT. Real
machine code comes out, so this is not "an interpreter" — but it is compiled once,
generically, with no knowledge of the types that will flow through it. Every `+` is a
runtime helper implementing the full §13.15 algorithm. **No JS-into-JS inlining is the
sharpest sub-case**: Richards and DeltaBlue are built out of one-line methods, which
is why they have the worst throughput ratios. Now with a number on it — **a call costs
~250–300 ns, about thirteen times the entire loop body it replaces**, and none of that is
callee resolution (2-6). → **phase 4**

**B3 · Shapes and inline caches cover only a slice of the object model.** The
structures work well on the sites they cover; what they do not cover maps one-to-one
onto benchmarks. → **phase 2**

| Gap | Hits |
|---|---|
| Shape eligibility is `GetType() == typeof(JSObject)` — `JSArray`, `JSFunction`, every exotic excluded | **Fixed for arrays (2-2) and functions (2-8).** The four benchmarks named here were the wrong ones — they reach arrays by element and by `length`, neither of which a shape can hold. The idiom that pays is statics on a constructor function, and **DeltaBlue at 601× was the case** — 2-8 took it from 0 to 199 999 hits |
| No shape-transition cache — *creating* a property misses every time | Richards, DeltaBlue, RayTrace, Box2D |
| `o.x++`, `o.x += 1`, computed keys, `super`, optional chains, private names keep the old lowering | Richards, Gameboy, Box2D |
| Double storage in `TrackShapeDataProperty` | everything — but **measured at ~3% of a worst-case store loop and 4.5% of an object's bytes**; see 2-3, which is re-specified, and 2-7, which is where the memory actually goes |

A fifth gap belongs on that list and is **fixed**: every `new` published a global
prototype-mutation notice, so a prototype-keyed entry could not survive a loop that
allocated — which is every loop in Richards, DeltaBlue, RayTrace and Box2D. It was worth
half the hit rate at an inherited-method site. See 2-0; the structures were fine, nothing
was allowed to stay warm.

**B4 · Compile time and latency on large machine-generated code.** Compilation is
eager; expression trees are an expensive, non-incremental intermediate; and the front end
recurses over nested source. A measured floor for the first two: compiling
`return a + <i>;` through the `Function` constructor costs **~7.5 ms**, and that is three
compilations rather than one — §20.2.1.1.1 requires the parameter text and the body text to
be validated separately, so `JSFunction` compiles each alone before the assembled source
(the `Evaluate` that follows hits the cache). So **~2.5 ms to compile one trivial
expression.** That is the number 1-3 was told to go and measure, and it is large enough
that it will still be there after 1-1 stops compiling what is never called.

The recursion is a separate, sharper problem: it aborts the process rather than costing
time, it lives in three passes across `.Parser`, `.Compiler` and `.ExpressionCompiler`,
and it follows source **nesting** rather than source **size** — a flat 200 000-statement
function is fine while ~19 400 nested operators is not. Mitigated at `685026c0` by giving
compilation a stack the engine sizes; see 1-2, which also records that the Mandreel
failure this blocker was written around does not reproduce on linux-x64. The blocker with
the clearest browser relevance: it is page load time. → **phase 1**

**B5 · The regex engine is a backtracking interpreter.** `Broiler.Regex`'s
`Matching/Matcher.cs` has no compilation to native code; V8's Irregexp JIT-compiles
each pattern. RegExp is 110× off *against Octane's lowest reference baseline*, and the
same engine sits on PdfJS's and Typescript's critical path. → **phase 5**

**B6 · Ambient state on hot paths — *the write half was the blocker, and it is gone*.**
`JSEngine` holds the current context and the strict-mode flag in `AsyncLocal<T>`. P0-2 removed
the redundant *writes*, which is where the cost was: a write allocated a fresh
`ExecutionContext`. `JSValue`'s set accessors do still **resolve** strictness through the
`AsyncLocal<bool>` on every uncached property write — and **that read measures at 0%**. Removing
all 13 resolutions moved a 30 M-write all-misses loop by nothing (median paired ratio 1.013).
So this is no longer a blocker on the write path; **2-5 is closed on that measurement.** What
remains under B6 is the *context* `AsyncLocal`, which nothing here has measured — it is read on
paths this blocker never quantified, and it should be measured before it is claimed. → **2-5
closed; the context read is unmeasured**

**B7 · GC — *not* a primary blocker.** Stated explicitly to keep it off the list.
SplayLatency at 45× is the *best* result in the suite and Splay's throughput at 152×
beats the median. The .NET collector is handling a workload it was never tuned for
well. The allocation **rate** is severe — that is B1, and it is a problem with what the
engine asks the collector to do, not with the collector.

**B8 · Correctness gates that cost whole suites.** Five of 15 suites scored nothing in
the committed results, each tracing to a small, *general* engine defect: `eval`
var-scoping (CodeLoad), `obj == null` running `ToPrimitive` (Crypto), `undefined + x`
string-concatenating (PdfJS), a dropped `for`-head comma expression (Typescript), a
missing `read` shell builtin (zlib). All five are fixed at `7ef80c03` and the pinned
pointer carries them.

Not one was exotic — four are core operator or scoping semantics any large real
program will hit. **That is the argument for keeping a retired benchmark in the loop:
Octane is 15 large real programs, and it found them.**

A structural point from the same change: the engine had no stack limit of its own,
only the CLR's probe, which fires when the stack is all but gone. .NET runs a catch
handler as a funclet *on top of* the frames it is handling, so the handler started
with no stack and its first call threw again — escaping the very `try` meant to catch
it. Octane's harness is literally `catch (e) { suite.NotifyError(e) }`, which is why
one overflowing benchmark took its entire suite down. The script host now sizes its
own thread (16 MiB) and opts into a reserve explicitly;
`JSContextOptions.MaxStackUsageBytes` correctly still defaults to 0 for embedders,
since a host that does not control its JavaScript thread's stack cannot pick a number.

---

## Phase 0 — establish the baseline

**Nothing else here can be measured until this is done**, and both source roadmaps
said so independently (§1.2). This phase contains no engineering.

### Harness readiness — **implemented**

| # | Item | State |
|---|---|---|
| **0-1** | Land the pending `Broiler.JS` patches | **Already landed, and the pointer has since advanced twice.** It is now `685026c0` — `b3f53dcc` plus 0-9's probe corpus — 9 commits past the `cdb2fd41` both source documents name; `cdb2fd41` (0048), `7ef80c03` (0046) and `8228b0da` (0047) are all still ancestors, verified with `merge-base --is-ancestor`. `patches/` held only its `README.md` when this was checked, consistent with the three patch files having been removed after the bump; it now carries `0049` for 1-2. Independently confirmed: `ff819e06` refreshed the WPT baseline right after for a **net 36 fewer failures** — those patches changed `+`, `==`, the `for` head and `eval` scoping, exactly the surface WPT exercises indirectly, and it moved the right way |
| **0-2** | Stack reserve on by default in the shell | **Already on.** `Program.cs` runs script-host JS on a 16 MiB thread it sizes itself. See B8 for why this is not a Crypto-specific fix. As of 1-2 that thread no longer carries *compilation* as well, so the 12 MiB JavaScript budget is no longer spent on compiler recursion — which is what the Mandreel signature below was |
| **0-3** | Record each suite's real time budget | **Implemented.** `timeoutSec` per suite; `--timeout` became a floor. Mandreel 1200 s, zlib 1800 s |
| **0-4** | Quantify run-to-run noise | **Implemented.** `--repetitions`, median + spread, `--noise-band`, per-repetition logs, `flaky` status (§3.2) |
| **0-5** | Check the code cache against CodeLoad's intent | **Checked; no problem.** `DictionaryCodeCache.Current = new AssemblyCodeCache();` is present but **commented out** in `Program.cs`, so `--script-host` compiles from source every time and **CodeLoad is a genuine compile-throughput measurement.** Re-check if that line is ever uncommented — the shell would then be measuring cache lookup, and this is not the kind of change that announces itself in a score |

### Evidence owed — **the actual gate**

| # | Owed | State in the tree |
|---|---|---|
| **0-6** | **Run the Octane workflow and commit refreshed results** | **Not done, and not doable outside CI.** The committed results were generated 2026-07-31 20:28, ~15 hours *before* the pointer bump, so they show five suites failing on an engine that no longer has those defects. `comparison.md` carries a hand-added stale banner that disappears the moment the results regenerate. A local run is *technically* possible — upstream `chromium/octane` is reachable and `run-octane.mjs` only loads Playwright on the Chromium path, so `--engines broiler` needs no browser — but its numbers would come from a developer workstation and **could not be committed against the CI-generated Chromium column**. The gate is the workflow; see below |
| **0-7** | A `PropertyOperationBenchmarks` / `FunctionCallBenchmarks` comparison | **Run** — `collect_phase0.py --profile baseline`, both classes plus the new probes, 2 repetitions, win-x64 at `b3f53dcc` (recorded `dirty: true`). The stale 2026-07-16 artifacts no longer stand alone. Artifacts under the gitignored `Broiler.JS/artifacts/performance/`, per `performance.md` |
| **0-8** | Two runs inside the band on **win-x64, linux-x64, linux-arm64**, reporting time, allocation and working set together | **Not satisfied on any RID.** The win-x64 leg ran and **failed the band: 25 of 62 metrics outside 7.5%**, worst 56% (`FunctionCallBenchmarks.Native`), including 5 lifecycle metrics. That is the protocol working, not a defect — the run was on a 16-core developer workstation, and `performance.md` requires an *idle physical machine*. **A workstation cannot produce this evidence**; linux-x64 and linux-arm64 are unavailable here regardless |
| **0-9** | A permanent home for the Appendix A probes under `Broiler.JS/benchmarks/Broiler.JavaScript.Engine.Benchmarks`, wired into `Broiler.JS/eng/performance/phase0.json` | **Done — and engine §8.1's "not created" was wrong.** The project existed and was *already* `phase0.json`'s `benchmark.project`; what was missing was the probe corpus inside it. Added `HotPathProbeBenchmarks` (all 14 Appendix A scenarios at Appendix A's iteration counts, plus the P2-4 accumulation probe), registered in `Program.cs` and wired into all three profiles. Phase C's hit rates got their own emitter, `--cache-metrics`, because a hit rate is not a wall-clock benchmark |
| **0-10** | Pinned test262 over the four manifests | **Done** — 8 313 tests, **zero engine failures** (§3.4). Getting there took three tooling fixes, below. The Annex B paths P0-3 names are still in no manifest |
| **0-11** | An `ownership.json` entry per item | **Done.** Fifteen added, one per item rather than one per phase since the file is item-scoped, bringing it to 36 total. The pre-existing `tiered-unboxed-locals` is the same work as `numeric-local-doubles` and should be retired when the phase 0–5 evidence is next revisited — left alone rather than silently retargeted |

#### 0-9 reproduces phase C exactly

The first thing the new emitter was used for was checking whether phase C's numbers
survive contact with a repeatable harness. They do — every figure in the P1 table
reproduces to the unit, cold-cache, at `b3f53dcc`:

| Site | Documented (after P1) | `--cache-metrics` |
|---|---|---|
| `var o = {}; o.x = 1` then read `o.x` | 199 999 / 1 | **199 999 / 1** |
| `class C { constructor(v){ this.v = v } }` then read `c.v` | 199 999 / 1 | **199 999 / 1** |
| `P.prototype.get` — inherited method call | 399 998 / 3 | **399 998 / 3** |
| class method call | 399 998 / 2 | **399 998 / 2** |
| dictionary fallbacks (inherited method loop) | 0 | **0** |
| monomorphic store (P1-3's write side) | — | **199 999 / 1** |

That is the first result in this document that is reproducible from a clean checkout
rather than from a harness that no longer exists. It does not *close* phase C — closing
still needs the RID matrix (0-8) — but it removes the specific risk engine §8.1 was
worried about, which is that the one-off observation could not be checked at all.

**And it has since earned its keep twice over.** Three sites were added to it while working
phase 2, and between them they measured a premise (2-1: 0 store hits against 600 000
misses), identified a defect nobody had filed (2-0: 200 001 prototype invalidations for
200 000 allocations) and supplied the control that pinned its cause (the same objects as
literals invalidate nothing). None of that is visible in a correctness suite or in wall
clock, which is the argument for a hit-rate emitter existing at all — and it took one run,
not a campaign.

#### What the corpus caught on its first run

Not acceptance evidence — the band failed (0-8) — but the allocation column is
deterministic and it disagrees with §4.1's table on its first two rows:

| Probe | §4.1 before | §4.1 after | Measured at `b3f53dcc` |
|---|---:|---:|---:|
| Empty `for` loop (3M) | 96 B/iter | 96 B/iter | **0 B** |
| Integer arithmetic (3M) | 128 B/iter | 128 B/iter | **0 B** |

The table records allocation as *unchanged* by P0 for both, and it has stayed that way
in the document ever since. It is no longer true: both allocate nothing. This is P2-2
item 3 landing after those rows were written — phase E's prose does say "the arithmetic
ones stopping allocation entirely", but the headline table was never updated, so the
document still shows 96 B and 128 B as the current state.

That is precisely the failure mode a permanent corpus exists to prevent, and it was
caught within one run of creating one. Per-call allocation has likewise moved below
§4.1's "after" column (which predates the shadow stack): a sloppy call now measures
120 B/iteration against the 264 B recorded there.

**Caveat on reading these.** BenchmarkDotNet reports `Allocated` as `-` on several rows
that show non-zero Gen0 activity, so its per-op allocation is less trustworthy than the
`GC.GetAllocatedBytesForCurrentThread()` deltas Appendix A used. Where the two disagree,
prefer a deliberate allocation probe. The zero rows above are corroborated by Gen0/1/2
all reading `-` as well.

#### A local Broiler-only pass answers most of 0-6 — and finds one new failure

Run at `b3f53dcc` on win-x64 with a freshly rebuilt shell, `--engines broiler`, results
kept out of `tests/octane/results`. **The scores are not comparable to the committed
Chromium column** — different hardware, 7 commits ahead — so only the pass/fail column
below is being read, and that part is hardware-independent.

**All five previously-failing suites now score.** B8's five defects are confirmed fixed
by the engine, not just by inspection of the patch:

| Suite | Committed run | Local pass |
|---|---|---|
| Crypto | `crash` — Maximum call stack size exceeded | **ok** |
| PdfJS | `error` — Malformed PDF: stream must have data | **ok** |
| CodeLoad | `error` — Cannot get property userAgent of undefined | **ok** |
| zlib | `error` — read is not defined | **ok** |
| Typescript | `error` — Cannot get property getScopedTypeNameEx of null | **ok** |

**But Mandreel now fails, and it scored 160 before.** 14 of 15 suites are `ok`, giving
**15 of 17 scores** rather than 17 — Mandreel takes MandreelLatency with it.

```text
RangeError: Maximum call stack size exceeded
  at EnsureWithinStackBudget: Broiler.JavaScript.Engine/CallFrames.cs:215
  at mandreelAppInit: mandreel.js:1460
  … 6 JavaScript frames total, in phase `Setup`, after 375.5 s (budget 1200 s)
```

That was read as **item 1-2's exact signature**: the guard fires with a JavaScript stack
only six frames deep, so what exhausted the budget is the engine recursing over
`global_init`, not the program recursing. It is **not** the old failure mode: exit code was
0, the suite was reported rather than killed, and the other 14 were unaffected. That
containment is 0-2 and B8 working as designed — the same overflow used to take a whole
suite down.

> **Superseded in part.** The same suite was re-run on **linux-x64 at `685026c0`** against
> upstream `chromium/octane` while working 1-2, and **Mandreel completes** — `status: ok`,
> 365.5 s of its 1 200 s budget, with 1-2's mitigation disabled so the engine is the one
> described here. So this is a win-x64 failure, not a property of the engine at this
> pointer, and the "15 of 17 scores" above is a win-x64 count. It still does not separate
> regression from platform difference — the two runs differ in both — but it does mean
> **0-6 will not reproduce it**, because CI is Linux. 1-2 carries the corrected diagnosis
> and a repro that exists on both platforms.

For 0-6:

```text
Actions → Octane Benchmarks → Run workflow
  engines:         chromium,broiler
  timeout_seconds: 180          # Mandreel and zlib raise their own
  repetitions:     3            # the first run that can distinguish signal
```

**Exit gate for phase 0:**

1. **17 of 17 scores reported** — the five previously failing suites complete;
2. **no `timeout`** at the default 180 s floor;
3. **a per-suite noise band on record**, and the suites exceeding it named;
4. `comparison.md` reporting coverage, geomean and spread;
5. **0-7, 0-8, 0-9 satisfied** — without them no phase A–F can *close* and nothing in
   §4.1 may be claimed under `performance.md`.

> **Three tooling defects had to be fixed before 0-10 could run**, and none was an
> engine defect. Two manifests **aborted before running a single test** — they named
> `test/built-ins/FunctionPrototype` and `test/built-ins/globalThis`, neither of which
> has ever existed in test262, and `_expand_path` raises on the first missing path. So
> those two had **never gated anything**. `_FIXTURE.js` files were executed as tests
> through `--path-file` (which CI also uses), producing phantom failures. And the
> assembled script was written with newline translation, so on Windows every `\n`
> became `\r\n` — turning an LF `Function.prototype.toString` test into CRLF and a
> CRLF test into CR-CRLF, invisible on the Linux CI. **A failing test is a claim, and
> here the claim was about the harness.**

---

## Phase 1 — the front end

**Targets: MandreelLatency (4646×), CodeLoad (371×), Mandreel (300×).** Owns the two
worst scores in the suite outright. Blocker **B4**. This is the phase the engine
roadmap had excluded (§1.1), and it is the item with the clearest value outside
Octane: **this is page-load time.**

Owner assemblies: `Broiler.JavaScript.Parser`, `.Compiler`, `.BuiltIns`.

### 1-1 · Lazy function compilation — *the single highest-leverage item*

**Target.** CodeLoad and MandreelLatency are *designed* so this is the dominant term —
jQuery defines thousands of functions and calls almost none of them. A large multiple
on both is the expected outcome; if it is not, the measurement is wrong before the
change is. Mandreel's 313 s should fall substantially, and Typescript and PdfJS should
improve on load. **Steady-state execution does not change at all** — do not expect
Richards or DeltaBlue to move.

**Where.**

| File | Role |
|---|---|
| `Broiler.JavaScript.Parser/FastParser.Function.cs` | where a body is parsed today; needs a skip-with-errors mode |
| `Broiler.JavaScript.Compiler/Declarations/FastCompiler.CreateFunction.cs` | where the body is compiled eagerly |
| `Broiler.JavaScript.BuiltIns/Function/JSFunction.cs` | already carries `source` and already recompiles from it for tiering — the raw material for deferring is present |
| `Broiler.JavaScript.Engine` code cache | keyed on whole scripts; needs to key on function spans |

**Work.** Pre-parse a body far enough to find its extent and binding structure without
generating code; record source span + captured scope on the `JSFunction`; compile on
first invocation, memoized per function-span; force eager treatment for the cases
below.

**Risk — all four are spec-visible, and the first is the bulk of the work.**

- **Early errors must stay eager.** A syntax error inside a never-called function is
  still a `SyntaxError` at parse time. The pre-parser has to be a real parser *for
  error purposes* while skipping code generation. Most likely to regress test262.
- **Scope capture.** A deferred body must compile against the scope chain as it was at
  closure creation, not at first call.
- **Direct `eval`** inside a deferred body can introduce bindings into enclosing
  scopes. The pre-parser must detect it and opt that function out.
- **Generators and async bodies** suspend mid-frame; confirm deferral composes with
  `GeneratorRewriter` before assuming it does.

**Verify.** Full test262 over the four pinned manifests with **no new failure and no
new timeout** — the local suite is not sufficient for an early-error change. Plus
`ParserCompilerBenchmarks` before/after, and a CodeLoad number taken with the code
cache confirmed off (0-5).

**Size: XL.** The only item here that is a genuine sub-project.

### 1-2 · Stop recursive compilation from overflowing — **mitigation landed**

**The diagnosis this item carried was wrong about its own cause, and its acceptance
criterion passed before any work was done.** Both were settled by measurement at
`685026c0`, and the corrected item is below. What was wrong:

| This item said | Measured |
|---|---|
| The trigger is source **size** — `global_init`'s 152,948 lines | A **flat 200 000-statement** function compiles and runs (30 s). Size is not the trigger; **nesting depth** is: a `1+1+…` chain overflows between 10 000 (passes) and 20 000 (aborts) operators |
| The failure is `EnsureWithinStackBudget` — a catchable `RangeError` | On linux-x64 it is a **hard CLR stack overflow that aborts the process**. Not catchable, no `try` reaches it, and no JavaScript stack is involved at all |
| One site: the compiler's AST visitors | **Three passes**, in three assemblies. The *parser* is one of them — upstream of the compiler entirely |
| Verify: "a generated 200k-line single-function script compiles … without overflow" | **Already passed at `685026c0` before the change.** As written the criterion certified nothing |

The three passes, each reached in turn as the one above it was given more stack:

| Pass | Assembly | Overflows on |
|---|---|---|
| `FastParser` recursive descent (via `FastScanner.Commit`) | `.Parser` | a right-nested conditional, 20 000 levels |
| `AstReduce.VisitBinaryExpression` under `SyntaxValidation.StrictModeValidator` | `.Compiler` | a left-nested `+` chain — the abort trace shows ~19 400 levels of its three-frame cycle on the stack, ~845 B per level |
| `ILCodeGenerator.VisitBinary` / `VisitCall` | `.ExpressionCompiler` | the same chain, reached only once the front end survives it |

All three measured on the script host's 16 MiB thread. The ~845 B per level is what makes
the ceiling predictable: it scales with the stack, so the same shapes abort at ~1 200 levels
on a 1 MiB Windows main thread and ~9 700 on an 8 MiB Linux one.

**Target.** Any deeply nested source, which is a *correctness* result before it is a
performance one: a syntactically valid script takes the host process down. Machine-
generated code is where nesting gets deep without anyone intending it, which is how this
reached the roadmap through Mandreel — but see the repro note below, because Mandreel is
not the demonstration this item thought it was.

**Where.** `Broiler.JavaScript.ExpressionCompiler/CompilationStack.cs` (new);
`Broiler.JavaScript.Runtime/CoreScript.cs`, `.../DictionaryCodeCache.cs`,
`Broiler.JavaScript.Compiler/DirectEvalSupport.cs`,
`Broiler.JavaScript.ExpressionCompiler/Runtime/RuntimeAssembly.cs` for the boundary;
`Broiler.JavaScript.ExpressionCompiler/StackGuard.cs` for the real fix.

**Work.**

1. **Mitigation (S) — landed as `patches/0049-js-compilation-stack.patch`.** Compilation
   runs on a thread the engine sizes (`CompilationStack`, 64 MiB, settable via
   `SizeBytes` or `BROILER_JS_COMPILE_STACK_BYTES`; `0` compiles in place). The boundary
   sits at the point each `ICodeCache` starts a compilation, so parse, validation, tree
   construction and IL emission share one crossing, with catch-alls in `CoreScript` and
   `RuntimeAssembly` for a host that brings its own cache. Two details are what make it
   affordable:
   - **Workers are parked and reused.** A thread per compilation costs ~300 µs — the
     reservation is a kernel mapping, not bookkeeping — which measured **+27%** on a
     compile-only loop. Renting leaves two semaphore handoffs.
   - **Short sources stay put.** Nesting depth cannot exceed source length, so a source
     under 512 characters cannot exhaust even a 1 MiB stack and is compiled where it
     stands. This is a bound, not a heuristic, and it is what takes the remaining cost to
     nothing: the handoff is a fixed ~180 µs, which is unmeasurable against a large
     compile and unaffordable against `eval` of one expression.

   Cost, measured ABBA-interleaved at process granularity over five pairs of a
   5 000-compile loop, on **one build with the environment variable as the only
   difference** — comparing two builds cannot separate this from anything else that
   changed: **+1.5% by median of paired ratios, +3.2% comparing medians, and one pair of
   five negative.** Inside this container's noise band, and the loop is the worst case
   (pure compilation, no execution). All four nesting shapes that aborted now compile. With
   `BROILER_JS_COMPILE_STACK_BYTES=0` the fixtures below abort the test run, which is what
   makes them decisive rather than merely green.
2. **Real fix (M) — still open, and it is a repair, not a new mechanism.**
   `Broiler.JavaScript.ExpressionCompiler/StackGuard.cs` already exists to segment the
   emitter's recursion and **cannot fire**: it tests `address - start > MaxStackSize` on a
   stack that grows *downwards*, so the difference is negative and the branch is
   unreachable. It also truncates stack addresses to `int` and would hop every 1 024
   bytes if it did fire. `CallFrames.EnsureWithinStackBudget` gets the direction right and
   says so in a comment; this one does not. Repair it, give it a threshold in megabytes,
   and hand its segments a sized thread rather than a thread-pool thread — then extend
   the same treatment to the parser and the validator. **Compiler stack depth should be a
   function of source *nesting*, not source *size*,** and it still is not.

**Verify.** `Broiler.JavaScript.Compiler.Tests/DeeplyNestedSourceTests.cs` — a nested `+`
chain, a nested conditional, a long flat statement list (kept, so the size case cannot be
re-diagnosed from length again), and a syntax error in deeply nested source reporting the
same type as one in shallow source. They assert values, not exceptions, because the
failure being pinned is the test host *aborting*. Repository suite at the patched tree:
**7 288 tests across 13 projects, 0 failures** — the three failures §4.1 attributes to the
host environment are win-x64 ones and do not reproduce on Linux.

> **The repro is win-x64 only, and that is a finding.** Phase 0's local pass lost Mandreel
> and MandreelLatency to `EnsureWithinStackBudget` on win-x64. Run here on linux-x64 at
> `685026c0` against upstream `chromium/octane`, **Mandreel completes either way**:
>
> | Mitigation | Status | Mandreel | MandreelLatency | Duration (budget 1 200 s) |
> |---|---|--:|--:|--:|
> | disabled (`…STACK_BYTES=0`) | `ok` | 138 | 12.6 | 365.5 s |
> | default (64 MiB) | `ok` | 123 | 13.3 | 355.5 s |
>
> So "this is the only thing between the suite and 17 of 17 scores" was a win-x64
> statement; on Linux the suite is not blocked on it, and the mitigation neither fixes nor
> breaks it there. **Read no score movement into those two rows** — one repetition each,
> the two metrics move in *opposite* directions, and §3.2 says a single run cannot tell a
> change from noise. This also does not separate regression from platform difference (the
> two runs differ in both commit and platform), but it does bound the question: whatever
> fires on win-x64 does not fire on linux-x64 at this pointer, so **0-6 will not reproduce
> it and CI cannot be the instrument that closes it.** The synthetic shapes above are the
> repro that exists on both, and they are what the fixtures pin.

### 1-3 · Reduce compile cost per byte — *only after 1-1*

**Do not start here.** If 1-1 lands, most source is never compiled at all and the
remaining throughput may not justify a pipeline change. Measure first with
`ParserCompilerBenchmarks`, splitting the cost three ways: parse, expression-tree
construction, IL emission. The measurement names the target; committing to one now
would be guessing. **Size: unknown by construction.**

There is now one datapoint to start from, taken while working 1-2: **~2.5 ms to compile
`return a + <i>;`** (B4). It is a whole-pipeline figure, so it does not yet split three
ways — but it does say the per-compile floor is milliseconds for a trivial body, which is
the part 1-1 cannot remove. A body that is never called costs nothing after 1-1; the one
that *is* called still pays this.

---

## Phase 2 — the call and property paths

**Targets: DeltaBlue (601×), Richards (433×), Box2D (170×).** Blocker **B3**; **B6 is closed on
the write path** — 2-5 measured its remaining half at 0%.

This phase is exactly the "engineering deliberately left behind" table from engine §8.1 — a set
of contained changes to structures that already exist and already work on the sites they cover.
**Best effort-to-value ratio on the list after phase 1**, and it has held up: five items landed,
every one of them measured, and three of the eight turned out to be mis-specified rather than
merely undone (2-2's targets, 2-3, 2-5).

Owner assemblies: `Broiler.JavaScript.Runtime`, `.Compiler`, `.Engine`.

### 2-0 · `new` retired every prototype-keyed cache entry in the process — **landed**

Numbered 2-0 because it was not on this list: it was found while measuring 2-1's premise,
and it lands before it. `OrdinaryCreateFromConstructor` installed the instance prototype by
**overwriting the one the `JSObject` constructor had just set**. The second write is what
matters: by then `prototypeChain` was no longer null, so the guard could only read it as a
`[[SetPrototypeOf]]` on a live object, and it published the global prototype-mutation
notice. Every prototype-keyed inline-cache entry in the process was retired — **once per
`new`.**

This is the defect P1-2's guard exists to prevent. Its comment in
`JSObject.BasePrototypeObject` states the failure mode precisely ("would leave any
prototype-keyed cache permanently invalid in a loop that allocates") and the guard is
correct; the construct path simply reached it in a state it cannot recognise. Measured with
`--cache-metrics` (0-9's emitter), 200 000 allocations of a three-field object:

| Site | Prototype invalidations | Cache hits / misses |
|---|--:|---|
| `constructor-field-creation` — 200 000 × `new T(a,b,c)` | **200 001** → **1** | — |
| `literal-field-creation` — the same objects as literals (control) | 0 → 0 | — |
| `inherited-method-call` — read site, allocation hoisted out of the loop | 2 → 1 | 399 998 / 3 |
| `inherited-method-call-while-allocating` — same site, allocation inside | 200 002 → **1** | **199 999 / 200 002 → 399 998 / 3** |

The control row is what identifies the cause: the same number of property creations on the
same number of objects invalidates nothing when built by a literal, so it is `new` and not
property creation. The last row is the consequence — a warm inherited-method site ran at a
**50% hit rate purely because the loop also allocated**, and now matches the hoisted
control. Wall clock on a 2 M-iteration allocate-and-call loop, interleaved four pairs:
**~11% faster** (median of paired ratios 0.89, all four pairs the same direction), which is
the weaker of the two results — the hit-rate figures are exact and deterministic.

**Fix.** Install the prototype *by construction* at the two sites that allocate an instance
— `BuiltIns/Function/JSFunction.cs` (`OrdinaryCreateFromConstructor`) and
`BuiltIns/Class/JSClass.cs` — instead of by an initializer that overwrites it. There is
already a prototype-taking `JSObject` constructor, and `Runtime` exposes internals to
`BuiltIns`, so this routes the construct path through the *existing* guard rather than
adding one. The end state of the object graph is byte-for-byte the same; only the spurious
notice is gone. `JSClass`'s null branch is kept verbatim, because assigning null through
the setter clears the chain whereas passing null to the constructor substitutes
`%Object.prototype%`.

**Why the local suite did not catch it, and what now does.** The invalidation was
*conservative* — it retired too much, never too little — so every staleness test in
`PropertyShapeCacheTests` passed for a reason unrelated to what it checked. Removing it
means those paths need re-checking with an allocation in the loop, where previously there
was nothing left to invalidate: `PropertyShapeCacheTests` gains that combination for
prototype mutation, `setPrototypeOf`, own-property shadowing and accessor redefinition,
plus `InheritedReadInAnAllocatingLoop_IsCached` as the guard for the fix itself (**501/501
hits before, ≥999 after**) and `AClassInstanceStillGetsItsNewTargetPrototype` for the
prototype the construct paths install, including the subclass, `Reflect.construct` and
primitive-`prototype` forms. Suite: **7 290 tests across 13 projects, 0 failures.**

Landed as `patches/0050-js-construct-prototype-invalidation.patch`.

### 2-1 · A store-cache entry that can describe a property *creation* — **landed**

**Measured before: 0 store-cache hits against 600 000 misses**, for 200 000 constructions of
a three-field object. A property-creating store could not hit *even once*, ever, because
`PropertyStoreInlineCache` only recorded `(shapeAfterTheWrite, slot)` and hit through
`TryWriteShapeSlot`, which requires the property to exist already — so the next object
presented the *predecessor* shape and missed. Every constructor that builds an object
field-by-field missed on every field of every object it ever built: Richards'
`TaskControlBlock`, DeltaBlue's constraints, RayTrace's `Vector`, Box2D's `b2Vec2`.

**After: 599 997 hits against 3 misses** — one cold miss per field to install the entry, and
nothing after. The read site inside an allocating loop went the same way (0 / 200 002 →
200 000 / 2). Wall clock on a 2 M-iteration constructor loop, interleaved four pairs:
**~20% faster** (median of paired ratios 0.797, every pair the same direction, spread
0.777–0.840).

**What was built.** A second entry form on the same store cache, discriminated by a null
`FromShape` exactly as the read cache discriminates own from prototype entries:

| Form | Guard | Action |
|---|---|---|
| overwrite (existing) | shape id | write `Slot` |
| **transition (new)** | `FromShape` identity, receiver-prototype identity, global prototype version, extensibility | create the property in `Slot` and advance the shape to `ToShape` |

plus `JSObject.TransitionShape` (the shape a transition may be recorded out of) and
`JSObject.TryCreateShapeSlot`, which performs the same three steps
`DefineReceiverDataProperty` does — `ownProperties.Put`, shape update, `PropertyChanged` —
with the shape advanced to the recorded successor rather than re-derived. That is where the
saving is: `TrackShapeDataProperty` would look the key up in the current shape, miss, then
look it up again in that shape's transition table to find the very shape and slot the entry
already holds.

**Three things make it safe, and none of them is the shape id alone.**

- **A concrete shape proves the key is absent.** While an object is in shape mode its tracked
  keys *are* its complete set of own named properties — every untrackable addition (private
  name, accessor, non-default attributes, deferred cell) calls `AbandonObjectShape` first.
  The entry holds the `ObjectShape` **by reference**, not by id, so the test is identity.
- **The prototype chain is walked once, at install, and required to be free of the key.** A
  creation is only what `OrdinarySetWithOwnDescriptor` would do while the chain supplies
  nothing: a setter there has to run, an inherited non-writable data property has to reject
  the write. Two guards keep that answer true at every later hit — the receiver still
  pointing at the same prototype *by reference*, and the global prototype-mutation version,
  which any addition to any object used as a prototype publishes. **These are the same two
  the read cache's prototype form uses, and they are only affordable because of 2-0**: before
  it, the version advanced once per `new`, so a transition entry retired on the very next
  object the loop built. 2-1 does not work without 2-0.
- **Extensibility is re-checked on every hit**, unlike the overwrite form which deliberately
  omits it. `preventExtensions`, `seal` and `freeze` all set `NonExtensible`, so one test
  covers all three.

Everything else falls through to the unchanged generic path, which is the property that
bounds the risk: a guard failing costs a miss, never a wrong answer. The only way to be
wrong is a **false positive**, and each guard above closes one.

**How a creation is even detected.** By the shape *changing* across the store. A shape is
immutable, so a receiver reporting a different one has gained a tracked property, and the
only one it can have gained at this site is this key. No extra pre-lookup of the key is
needed — which matters, because that lookup would be paid on every miss.

**Verify.** 17 tests in `PropertyStoreCacheTests`, each warming the site on the fast path
first: a prototype setter present from the start and added mid-loop; an inherited read-only
data property, sloppy and strict; a non-extensible receiver, sloppy and strict; a frozen
receiver; two receivers sharing a shape but not a prototype; `setPrototypeOf` mid-loop;
`__proto__` still reaching the inherited accessor; a dictionary-mode receiver; a Proxy as
receiver and in the chain; `delete` then re-create; attributes and key order; and the
hit-rate guard for the fix itself. **Removing the two hit-time prototype guards fails four
of them**, which is what makes them load-bearing rather than decorative. Suite: **7 307 tests
across 13 projects, 0 failures.**

> **test262 has not been run for this item.** §3.4's protocol and this phase's exit gate both
> require `test262-properties-proxy` and `test262-strict-mode` for anything touching
> `OrdinarySetWithOwnDescriptor`, and 2-1 touches its last step. The repository suite and the
> targeted battery above are what *has* been run. **The two manifests are owed before this
> closes** — this is the gate the phase-2 exit criterion names, not an optional extra.

Landed as `patches/0051-js-store-cache-property-creation.patch`, which applies on top of
`0050`.

| # | Item | Origin | Where | Why it matters here | Size |
|---|---|---|---|---|---|
| **2-2** | **Widen shape eligibility** past `GetType() == typeof(JSObject)` — *arrays landed (2-2), functions landed (2-8)* | P1-4 | `Runtime/JSObject.cs` — `SupportsShapeTracking`; `BuiltIns/Array/JSArray.cs` | `JSArray`, `JSFunction` and every built-in exotic were excluded wholesale — **measured 0 hits / 200 000 for every named access on one.** Arrays now opt in (**0 → 199 999**). The function half is blocked and is now 2-8 | M |
| **2-3** | **Remove the double storage** — *re-specified and re-sized; see below* | P1-4 | `Runtime/JSObject.cs:97,:188` — `TrackShapeDataProperty` | Every tracked object writes each value into `shapeSlots` *and* the `PropertySequence`. **Not a pure removal, and its throughput case is ~3% of a worst-case loop.** The dominant per-object cost is elsewhere — see 2-3 below and 2-7 | ~~S~~ **M** |
| **2-4** | **Extend the store cache** to `o.x++` ✅, `o.x += 1` ✅, computed keys, `super`, optional chains, private names | P1-3 | `.Compiler` lowering | Measured: these reached **neither** cache — 0 hits *and* 0 misses, the counters never saw them. `o.x++`/`o.x--` and `o.x op= rhs` now take both (**0 → 199 999** each side, on twelve operators); `&&=`/`||=`/`??=` stay out because their write is conditional, and computed keys, `super`, optional chains and private names stay out on purpose | M |
| **2-5** | ~~**Get strictness off the property-write path**~~ — **measured; closed, no work worth doing** | P0-2 | `Engine/Core/JSEngine.cs:225`; `JSValue` set accessors | Removing **all 13** resolutions from the write path moves a 30 M-write all-misses loop by **nothing** — median paired ratio 1.013, i.e. marginally the wrong way. P0-2 already took the expensive half (the ExecutionContext *write*); what remains is a read that does not cost. See below | ~~M~~ **closed** |
| **2-6** | ~~**Monomorphic call-site caching**~~ — **measured; folded into 4-1** | new | `BuiltIns/Function/JSFunction.cs` — `InvokeFunction`, `SelectInvocationDelegate` | "Callee resolution repeats per call" does not describe this engine: the callee is already resolved by the cached property read, and `SelectInvocationDelegate` is a volatile read plus a null check. A call costs **~250–300 ns**, and a call-site cache removes none of it. Its surviving clause — feedback for phase 4's inlining — is **4-1**. See below | ~~M~~ **folded** |

> **2-1 was named after the wrong missing thing.** It called for "a shape-transition cache
> — an `oldShapeId → (newShape, slot)` entry. Absent entirely." That cache **is present**:
> `ObjectShape.Add` memoizes each transition in a `ConcurrentDictionary<uint, ObjectShape>`,
> so adding `x` to a given shape always yields the same successor shape without rebuilding
> it. The measurement shows it working — 200 000 three-field constructions produce **3**
> shape transitions in total, one per field, not 600 000.
>
> The item's *rationale* was nevertheless exactly right, and measured: **0 store-cache hits
> against 600 000 misses.** What was absent is a **store-site** entry that can describe a
> property *creation*, which is what landed — the item above says what it took. The lesson is
> in §3.5: an item can be worth doing and still be wrong about why.

### 2-2 · Widen shape eligibility — **arrays landed; the item's own targets were wrong**

Shape eligibility was an exact `GetType() == typeof(JSObject)` test in six places, so a
`JSArray`, a `JSFunction` and every built-in exotic had no shape and therefore no
inline-cache entry. Measured first, and the exclusion is total — **0 hits out of 200 000 on
every named access to one:**

| Site | Before | After |
|---|--:|--:|
| `array-named-read` — `a.tag` in a loop | 0 / 200 000 | **199 999 / 1** |
| `array-named-store` — `a.tag = i` | 0 / 200 001 | **199 999 / 2** |
| `array-length-read` — `a.length` | 0 / 200 000 | 0 / 200 000 — *unchanged, and cannot change* |
| `array-element-read` — `a[1]` (control) | 0 / 0 | 0 / 0 — never cached, by design |
| `function-named-read`, sloppy / strict / class static | 0 / 200 000 | 0 / 200 000 — *not opted in; see 2-8* |
| `typed-array-length-read` | 0 / 200 000 | 0 / 200 000 |

Wall clock on a 10 M-iteration loop reading and writing one named property on an array, same
build with only the override differing, **eight** interleaved pairs: median of paired ratios
**0.93**, seven of eight in the same direction and one pair 11% *against*. Read that as
directional rather than as a figure — the hit-rate rows above are exact and deterministic, the
wall clock on this container is not, and eight pairs were needed before the median stopped
moving.

**What landed.** The gate became a virtual `JSObject.SupportsShapeTracking`, following the
`SupportsOrdinaryIndexedWrite` pattern the class already uses, with `JSArray` overriding it. It
is an opt-**in**, and the remarks say what a subclass has to earn: *while an object is in
shape mode, the shape's tracked keys must be its complete set of own named properties.*
`GetPrototypeLookupShapeId` reads "key absent from the shape" as "no own property shadows the
prototype's", and 2-1's `TryCreateShapeSlot` reads it as "creating this key is safe" — so a
subclass that violates it does not merely fail to help, it breaks both.

**Three things the measurement changed about the item.**

- **`a.length` can never be cached by this.** It is computed from the element store by an
  exotic override rather than held as a data property, so there is no slot for it to occupy —
  and there should not be, since its value moves whenever the array does. Unchanged at
  0 / 200 000, and now pinned by a test so its absence reads as designed.
- **"On the hot path of five benchmarks" does not survive.** The item names Crypto,
  NavierStokes, Gameboy and zlib, but what those do with arrays is *elements* and *length* —
  elements bypass the cache by design (the control row) and length cannot be cached. In the
  corpus, **NavierStokes and zlib contain no `.length` at all**; Crypto has 28 and Gameboy 21,
  none of them named data properties. A named expando on an array is a real pattern in real
  JavaScript, which is why this is worth having, but it is not what those four benchmarks do.
- **The type that would have paid is `JSFunction`, and it is blocked.** See 2-8.

**What it buys is bounded, and the bound is deliberate.**
`GetOwnProperties(create: true)` abandons the shape whenever another assembly asks for a
mutable ref to the property store, because such a ref could add a named property without
telling the tracker. `a.push(...)` goes through it, so **an array that grows through the
built-ins loses its named-property cache at the first growth** — one dictionary fallback, then
correctness unaffected and hits gone. Measured and pinned rather than discovered later.

**Verify.** 12 tests in `PropertyShapeCacheTests`: the hit rate itself; elements and named
properties staying distinct; `length` tracking `push` while a named property is tracked;
`Object.defineProperty(a, 'length', …)` materializing length without confusing the shape;
delete revealing an `Array.prototype` property; a prototype mutation mid-loop; `join`/`forEach`
/`slice`/`reverse`/`JSON.stringify`/`indexOf` after tracking; a frozen array refusing both
kinds of write; a sparse array keeping its holes; an `extends Array` instance; a typed array
staying untracked; and two arrays reaching the same shape staying distinct. Suite: **7 319
tests across 13 projects, 0 failures.**

Landed as `patches/0053-js-array-shape-eligibility.patch`, on top of `0051`.

### 2-4 · `obj.name++` and `obj.name op= rhs` through both caches — **landed, both halves**

A read-modify-write on a member reads and writes the same property, and both halves went
through one assignable index reference. Measured, that reference reaches **neither** cache —
not a poor hit rate, no counter at all:

| Site | Before | After |
|---|---|---|
| `increment-store` — `o.x++` | 0 hits, 0 misses, 0 stores | **199 999 read hits / 2**, **199 999 store hits / 1** |
| `compound-assign-store` — `o.x += 1` | 0 / 0 | **199 999 / 2**, **199 999 / 1** |
| `computed-key-read` — `o[k]` | 0 / 0 | 0 / 0 — excluded on purpose |
| `optional-chain-read` — `o?.x` | 0 / 0 | 0 / 0 — excluded on purpose |
| `monomorphic-store` — `o.x = i` (control) | 199 999 | 199 999 |

Both forms are eligible on exactly `TryCreateCachedMemberStore`'s terms — constant `KeyString`,
ordinary base, no `super`, no optional chain, no private name — because the reasons are the
same: a computed key would drive one site through every key the expression produces, and a
private name is a brand check rather than an ordinary [[Get]]/[[Set]]. Both end in the same
`JSValue` indexer on a miss, so strict-mode reporting and a refused write's silent failure are
unchanged, and the observable sequence is untouched: base once, the coercion once, getter once,
setter once. The compound form carries one further restriction, below.

Wall clock on a 20 M-iteration `o.x++` loop, same build with only the eligibility call
differing, five interleaved pairs: **median of paired ratios 0.944**, every pair the same
direction. Modest, and it should be — the cache removes the two property resolutions, not the
`ToNumeric` or the boxing around them. That remainder is B1, not this.

#### The compound form, and a correction

The 0054 note in this section said compound assignment was excluded because it "goes through
`EvalShadowBuilder`'s captured-reference abstraction". **That was wrong.** `EvalShadowBuilder`
handles the *identifier* case (`x += 1`), where a direct `eval` on the right-hand side can
redirect which binding the write lands on. The *member* case is a plain `CreateMemberExpression`
plus `Assign`, three lines below the branch 0054 had already changed — and `objectTemp` there
already evaluates the base exactly once, which is the only thing the read and the write have to
agree on. The deferral was reasoning about a neighbouring code path, not the one in front of it.

`o.x op= rhs` now emits a cached read, the operator, and a cached write, for the **twelve**
operators `CompoundAssignmentToBinaryOperator` maps. `CachedStore` takes the computed value as
its last argument, so the read stays inside it and cannot float past the right-hand side —
§13.15.2 reads the old value *before* evaluating the RHS, and a test asserts exactly that with
an RHS that overwrites the property being compounded.

**`&&=`, `||=` and `??=` keep the ordinary reference, and this is the one guard that is
load-bearing rather than defensive.** For them the write is conditional on the value read, so a
cached store would perform it unconditionally. `CompoundAssignmentToBinaryOperator` currently
throws for all three, which makes the exclusion look redundant — a probe settles it: complete
that operator table the way it reads like it wants to be completed, widen the gate to match, and
`o.a &&= 1` against a falsy getter fires the setter **300 times instead of 0**. Silent, and a
spec violation. The eligibility set is the only thing standing between those two edits and that
bug.

Wall clock, 20 M iterations of `o.x += 1` inside a function, eight interleaved pairs with only
the eligibility call differing: **median of paired ratios 0.903**, seven of eight the same
direction. The control is the point — `o.x = o.x + 1`, which does the same three operations and
already took both caches, measures **1.002** across the same builds, so the machine is not
drifting under the compound number. Stated within one build: `o.x += 1` cost **1.163×** the
spelled-out form before and **1.043×** after, closing about three quarters of the gap. Across
operator shapes the medians were 0.86 (`+= 1`), 0.91 (`+= d`), 0.89 (`-=`) and 0.93 (`|=`).

> **The first version of this measurement was worth less and did not look it.** The same change
> measured on a *top-level* `o.x += 1` loop gave 0.915 with two of eleven pairs the wrong way.
> That loop spends most of its time resolving a global binding, which the change cannot touch, so
> the signal arrived diluted and buried in noise. Moving the loop inside a function and adding a
> control the change provably cannot reach turned a soft 0.915/11 into a clean 0.903/8 at 1.002.
> *Check what fraction of the probe the change can actually reach before trusting its ratio.*

**Verify.** 15 test cases for the update form in `PropertyStoreCacheTests` (8 facts and a
7-case theory): hit rates; prefix and postfix values for `++` and `--`; string and BigInt
operands, where `ToNumeric` coercing once means a postfix update yields the *number*;
`undefined` giving NaN; an inherited getter/setter pair each running exactly once per iteration
through a warmed site; a non-writable property refused in sloppy mode and throwing in strict;
the base evaluated exactly once; a Proxy firing both traps; every excluded form still correct;
and an update interleaved with a plain store on the same property agreeing.

**37 more for the compound form** (19 facts and an 18-case theory), weighted to the order and
to the ways a write can be refused: all twelve operators' values, including `>>>=` on a negative
and the string/number asymmetry the `+= <literal>` fast paths preserve; the old value read
before the RHS, proven with an RHS that overwrites the very property being compounded; an RHS
that moves the receiver's shape every iteration; the three short-circuiting forms neither
writing nor mis-valuing;
a refused write still evaluating to the *computed* value; a nullish base throwing before the RHS
runs; a primitive base silently discarding in sloppy mode and throwing in strict; a getter-only
property likewise; nested compound assignments not sharing a base temporary; and the compound,
update and plain-store forms agreeing on one property. Suite: **7 385 tests across 13 projects,
0 failures.**

Landed as `patches/0054-js-update-expression-cache.patch` (the update form) and
`patches/0056-js-compound-assignment-cache.patch` (the compound form), on top of `0053` and
`0055` respectively.

### 2-8 · Functions track their named properties by shape — **landed**

The half of 2-2 that would pay, and the reason it needed its own item. **DeltaBlue is the worst
throughput score in the suite at 601×, and it reads `Strength.stronger`, `Strength.REQUIRED`
and `Strength.WEAKEST` in its hot path** — `deltablue.js:104` defines `Strength` as a
*function*, so every one of those was a named read on a `JSFunction`. Richards, RayTrace and
Box2D use the same statics-on-a-constructor idiom.

| Site | Before | After |
|---|--:|--:|
| `sloppy-function-static-read` — DeltaBlue's exact shape | 0 / 200 000 | **199 999 / 1** |
| `strict-function-static-read` | 0 / 200 000 | **199 999 / 1** |
| `class-static-read` | 0 / 200 000 | **199 999 / 1** |
| `function-named-read` | 0 / 200 000 | **199 999 / 1** |
| dictionary fallbacks, whole corpus | one per function | **0** |

Wall clock on a 10 M-iteration loop shaped like DeltaBlue's `satisfy()` — two static reads and
a static method call per iteration — five interleaved pairs with only the overrides differing:
**median of paired ratios 0.905**, every pair the same direction.

**Two prerequisites had to land first, and flipping the gate without them would have been a
correctness bug rather than a no-op** — a scratch build confirmed the hazard was masked only by
an accidental dictionary fallback.

1. **A function's own properties were invisible to the shape.** `length`, `name` and
   `prototype` went in through a bare `ownProperties.Put`, and four constructors additionally
   took a mutable ref through `GetOwnProperties()`, which abandons the layout on the spot. All
   are routed through `FastAddValue` now; the four refs were dead once their uses were
   converted.
2. **Every ordinary non-strict function carries the Annex B `caller`/`arguments` as deferred
   cells from birth** (P0-3), and a deferred cell abandoned the shape. Fixed by recording such a
   key **with a null slot** instead. The shape makes two claims and only one needs the value:
   *presence* — "key K is at slot N" — is what `TryReadShapeSlot` and `TryWriteShapeSlot` use;
   *absence* — "the shape does not carry K, so this object does not own K" — is what
   `GetPrototypeLookupShapeId` and `TryCreateShapeSlot` use. A key present with a null slot
   keeps **both** true: absence reasoning sees the key and declines, and all three fast paths
   already reject a null slot or a descriptor whose value is not a `JSValue`, so the read or
   write falls through to the generic path that realizes the cell. A private name still
   abandons — it is per-class-evaluation, so admitting one would mint a shape per instantiation
   instead of sharing a chain.

Without prerequisite 2 this would have helped strict functions and classes only, and the
motivating case is sloppy: neither `deltablue.js` nor `richards.js` contains `"use strict"`.

**Verify.** 13 test cases in `PropertyShapeCacheTests`, and the ones that matter are the Annex B
surface rather than the hit rates: `caller`/`arguments` keeping the non-writable,
non-enumerable, non-configurable **data** descriptor P0-3 preserved; reading `caller` while the
function is on the stack; the `Function.prototype` poison pills still throwing; and a null-slot
key still reading through the generic path after its site is warmed. Plus a function's own
`length`/`name`/`prototype` values, attributes and enumeration order unchanged, redefining them,
a bound function's name and length, a static redefined as an accessor mid-loop, and `delete`
revealing an inherited static.

**Seven more for the prototype-write gate below**, which is the half these 13 did not cover:
DeltaBlue's exact three-level `inheritsFrom` idiom; one warmed site writing 300 different
functions' prototypes with every instance landing on its own; the property and `[[Construct]]`
agreeing across 400 warmed writes; a class's non-writable `prototype` still refused once the site
is warm; constructability surviving a non-object assignment; a function's *other* statics still
taking the store cache — the assertion that stops the fix undoing 2-8 — and `f.prototype` still
being cached on **read**, because only the write paths are gated. Suite: **7 392 tests across 13
projects, 0 failures.**

> **One pre-existing test changed with it, and the change is worth reading.**
> `AnInheritedAccessorIsNotSlotCached` asserted *zero* cache hits for a script that also called
> `Object.create` — itself a named read on a function object, which this item makes cacheable,
> so it was quietly supplying one hit. The assertion was a proxy for "the accessor site does not
> hit", and the proxy went stale the moment function statics started caching. Fixed by linking
> the prototype with `__proto__` in the literal so the script performs no other cacheable read,
> which keeps the exact assertion instead of loosening it. **A test that reads a process-wide
> counter is coupled to everything else in its script** — worth remembering for the next item
> that widens what can be cached.

#### It shipped a regression, and Octane found it in one run

**2-8 broke DeltaBlue.** The item whose entire justification was DeltaBlue's 601× score, measured
with a hand-written loop shaped like DeltaBlue's hot path, made the real benchmark throw
`TypeError: undefined is not a function` before it produced a score. Found by running Octane
while setting up 2-7's measurement — not by any of the 7 347 tests, and not by the loop.

`JSFunction` keeps its `prototype` object in a **cached field**, and that field — not the
property — is what `[[Construct]]` reads. It is synced by overriding every observable write path:
the indexer, `SetValue`, `DefineProperty`. **A shape fast path is none of them.** It writes
`ownProperties` and `shapeSlots` and returns. So once functions became shape-tracked, a *cached*
store to `f.prototype` updated the observable property and left construction building instances
on the previous object.

DeltaBlue's `inheritsFrom` is precisely the shape that exposes it:

```js
Object.defineProperty(Object.prototype, "inheritsFrom", {
  value: function (shuper) {
    function Inheriter() { }
    Inheriter.prototype = shuper.prototype;
    this.prototype = new Inheriter();     // one emitted site, once per class
    this.superConstructor = shuper;
  }
});
```

One store site, called once per class. **The first call missed and was right; every call after it
hit and was wrong** — so the first level of every inheritance chain linked and the second did not,
and `this.addConstraint` was undefined two constructors down.

Fixed with a virtual `JSObject.AllowsDirectShapeWrite(key)`, checked by `TryWriteShapeSlot`,
`TryCreateShapeSlot` and `TryGetWritableShapeSlot`, which `JSFunction` overrides for exactly one
key. **Checked on the write and not only on the install**, because shapes are interned by key set:
a `JSFunction` and a plain object carrying the same keys share one shape *and one id*, so an entry
installed against the plain object would otherwise hit the function.

It is deliberately not the null-slot trick 2-8 introduced for `caller`/`arguments`. That one works
because a deferred cell's stored value is not a `JSValue`, which the write paths already reject —
`prototype` holds an ordinary `JSValue` and sails through every existing check. The comment
claiming "all three fast paths already reject a null slot" was **only true of the read path**;
corrected in place.

**Octane runs again: 17 of the 18 benchmarks pass.** The one failure — RegExp's
`Error: Wrong checksum.` — fails identically on a pristine build at the pinned pointer, so it is
not from this patch. Mandreel exceeded the 300 s smoke budget rather than failing;
`scripts/octane-suites.json` already gives it 1 200 s for that reason.

**The fix costs nothing measurable.** All 22 `--cache-metrics` rows are byte-identical before and
after, including all four function rows at 199 999 — the exclusion is one key wide and the win is
in the statics. Wall clock on a 2 M-iteration three-field constructor loop, gate against a build
with the three call sites removed, six interleaved pairs: **median paired ratio 1.0015**. Reads
are not gated: `f.prototype` still caches, because a read has no field to keep in sync.

> **The lesson is about the probe, not the bug.** 2-8's evidence was a loop I wrote to look like
> DeltaBlue. It reproduced the *reads* the item was about and none of the *writes* the item's
> change also affected, so it could not have failed. Octane was available the whole time, takes
> minutes, and would have caught this before the patch was written. **A benchmark named as an
> item's justification is a test that item has to pass** — a resemblance to it is not evidence
> about it. Now recorded in §3.5.

Landed as `patches/0055-js-function-shape-eligibility.patch`, on top of `0054`, **with the gate
folded in** — the patch is pending and unapplied, so shipping it broken and fixing it in a later
patch would leave any partial application of the series with a DeltaBlue that does not run.

> **Two pre-existing defects found alongside it, neither caused by this item and neither fixed
> here.** Both reproduce identically on a pristine build at the pinned pointer `685026c0`:
>
> 1. **A refused write to `prototype` still redirects `[[Construct]]`.** `JSFunction`'s indexer
>    calls `AssignPrototypeField` *before* the write and unconditionally, so for a non-writable
>    `prototype` — every `class`, or any function frozen with `defineProperty` — the property
>    correctly refuses the write while `new` starts producing instances on the rejected object.
>    `class C {}; C.prototype = x;` leaves `C.prototype` untouched and `new C().__proto__ === x`.
>    A spec violation (a failed `[[Set]]` must have no effect), and the reason the class test here
>    asserts only the observable property.
> 2. **Octane's RegExp suite fails its own checksum** — `Error: Wrong checksum.`, so the
>    committed score of 89.9 predates whatever changed. The checksum is computed inside a single
>    `run()` call, so it is a match-count discrepancy in the regex engine, not a harness artifact.
>
> Neither belongs to phase 2. Item 0-6's run will surface the second on its own; the first wants
> its own item, because moving that sync after the write means giving the indexer a success signal
> it does not currently have.

### 2-6 · Monomorphic call-site caching — **measured; folded into 4-1**

The item's stated reason was "callee resolution repeats per call". Read against the code, it
does not: at a method call site the callee comes from the **cached property read** (measured
199 999 hits out of 200 000 for `p.get()`), and `SelectInvocationDelegate` is a
`Volatile.Read` plus a null check on `tieringState`, which is null unless tiering is enabled.
There is no repeated resolution for a cache to remove.

**What a call actually costs, which this document did not have.** 20 M iterations, script host:

| Shape | Total | Per call |
|---|--:|--:|
| `no-call-control` — `s = s + i`, same loop, no call | 399 ms | — |
| `plain-call` — `s = s + f(i)` | 5 514 ms | **~255 ns** |
| `method-call` — `s = s + o.m(i)` | 5 059 ms | ~235 ns |
| `proto-call` — `s = s + p.m(i)` | 5 963 ms | ~280 ns |

**A call costs about thirteen times the entire loop body it replaces.** That ratio is far
outside any noise this container produces, and it is the concrete number behind B2 — the reason
Richards and DeltaBlue, which are built out of one-line methods, have the worst throughput
ratios in the suite.

**Where that quarter-microsecond is, and where it is not.** Not in resolving the callee. It is
the per-call prologue and epilogue: five `using` scopes (`EnterRealm`, `EnterStrictMode`,
`SuspendWithScopes`, `PushWithFallbackScopes`, `PushWithScopes`), the `Arguments` construction,
the frame, the delegate dispatch, and the boxing of the argument and the return. A cost probe
that removes **all five scopes** — not shippable, they carry realm, strict-mode and `with`
semantics — moves a call loop by a **single-digit** percentage, and at the load this container
reached during the run that was not cleanly separable from its own variance (one pair of four
went 26% the other way). Reported as single-digit and no more precisely than that. The
remainder is `Arguments`, the frame and the boxing, which is **B1 and phase F territory, not a
call-site cache**.

> **This refines P3's finding rather than contradicting it.** §3.5 records that P3 "blamed the
> five `using` scopes around every call, built the fast path, measured it, and found no signal —
> the scopes never allocated". That was an *allocation* result and it still stands. The probe
> here is a *time* result on an engine phase F has since changed underneath, and it finds a small
> but non-zero cost. Both readings agree on the conclusion P3 drew: the scopes are not where the
> call's cost lives.

**Folded into 4-1, not deferred.** The item's last clause is the part that survives —
"prerequisite for inlining in phase 4" — and phase 4 already carries it: **4-1 · Type feedback
collection** says "record and retain observed shapes, **callee identities**, and
numeric-vs-generic outcomes per site". Recording callee identity is feedback collection, it is
only useful once 4-2 and 4-4 can consume it, and keeping a duplicate of it in phase 2 as a
*throughput* item invites someone to build it for a win that is not there. Phase 2 keeps no
call-path item; the call path is B2, and B2 is phase 4.

### 2-5 · Get strictness off the property-write path — **measured; closed**

The item's claim was that `JSValue`'s set accessors "**resolve** an `AsyncLocal<bool>` per
write". True, and it costs nothing. Measured before starting, as this item's own note asked.

**The probe.** A build with all 13 `IsStrictModeEnabled?.Invoke()` sites replaced by `false` —
not shippable, since strict-mode error reporting goes with them, but it removes the read
entirely and so bounds the win from above. Run against a loop where **every** store is a
store-cache miss and therefore does resolve the flag: five shapes on one emitted site retires
it, so all 30 000 000 writes go through the indexer.

| | base | no resolution at all |
|---|--:|--:|
| 30 M-write all-misses loop, five interleaved pairs (median) | 16 017 ms | **16 222 ms** |

**Median of paired ratios 1.013** — the build with the work removed is *marginally slower*,
which is another way of saying the difference is container noise. The broader sweep agrees and
says something stronger: across five write shapes the deltas ranged 0–6% in **both**
directions, and the shape that should have gained most (all misses, 10 M resolutions) gained
least, while the shape that performs **no** resolutions at all — a constant-key store, which
hits the cache — showed the largest apparent delta. A causal effect does not distribute itself
inversely to its own cause.

**Why the premise was wrong, and it is worth knowing which half.** P0-2 is quoted in this
document as having removed the redundant strict-mode *writes*, and that was the whole cost: a
write allocated a fresh `ExecutionContext` on every call, which is why P0-2 made the scope write
only on a transition. What it left behind is a *read*, and an `AsyncLocal<T>.Value` read is not
a map walk — .NET keeps one to three async-locals in a specialized holder, so it is a field
access and a type check. This engine has a handful. The item inherited "AsyncLocal is
expensive" from the campaign that fixed the expensive part.

**Closed rather than deferred.** The stated fix — "threading the compiler's static knowledge
into the emitted set helpers so the hot path reads nothing" — is a compiler change, and it is
being asked to buy 0%. 2-1 also narrowed the exposure independently: a store-cache *hit* never
consults strict mode at all, so the read only survives on misses, which is the population the
probe above measured directly.

**Bounded claim.** Measured in the script host, where the engine holds few async-locals. An
embedding that stacks many `AsyncLocal`s on the same execution context could in principle push
the read into a slower path; if that is ever suspected, the reproduction is the probe above and
it takes one build to re-run. Nothing in this document should be read as saying the read is
free in every host — only that it is free in the one the roadmap measures on.

### 2-3 · Remove the double storage — **re-specified; do not start as written**

Measured before starting, and the item does not survive it. Three things are wrong.

**It is not a pure removal.** The two stores serve two different access paths. A cached read
takes `shapeSlots[slot]` — an array index; the generic path takes `ownProperties`, a radix
trie keyed by *name*. Deleting `shapeSlots` would put a trie walk back on the path phases C
and D exist to keep off it, and deleting the value from `ownProperties` would put a shape
lookup on every generic read, every descriptor query and every enumeration. Neither is a
deletion. *Demonstrated, not argued*: a cost probe that removed only the `ownProperties`
write left the store loop's own answer correct and made a later cold read of the same
property return the **stale** value, because that read resolved generically.

**Its throughput case is ~3%, of the most store-heavy workload that exists.** Same build,
one line differing, four interleaved pairs of a 20 M-iteration pure-overwrite loop: **median
of paired ratios 0.971**, every pair the same direction. That is the ceiling, not the
expected win — and it is unreachable anyway, because the write cannot simply be removed.

**Most of what it was aiming at has already been collected.** The item was written when a
store cost *two* key lookups: `ownProperties.Put` walked the trie and then
`TrackShapeDataProperty` looked the key up again in the shape. P1-3 and 2-1 removed the
second from both cached paths — `TryWriteShapeSlot` and `TryCreateShapeSlot` each do one trie
access plus a cached slot. The double *lookup* is gone from the hot paths. Only the double
*storage* remains, which is a memory question.

**So it is a memory item, and the memory is not where the item said.** Measured with the new
`--object-alloc` emitter (below), a `JSValue[4]` slot array is ~56 B of a 1 256 B
constructor-built three-field object — **4.5%**. What the same measurement found instead is
2-7.

**If someone does take it on**, the only shape that stores the value once while keeping O(1)
slot access is for a slot to hold a stable `PropertySequence` node index rather than the value
itself, so `shapeSlots` becomes a `uint[]` and the read is one more indirection. Node indices
are stable — `PropertySequence` already uses them as its `Previous`/`Next` links — but the
backing `VirtualMemory<T>` relocates on growth, so a `ref` is not. That is a storage-layer
change with real questions about node identity across trie restructuring, deletes and
deferred cells. **M, not S**, and it should be re-justified against 2-7 first.

### 2-7 · The property map's 16-node floor costs ~1 KB per object — **instrumented; awaiting its distribution**

Numbered 2-7 because it was not on this list either; it came out of measuring 2-3. Bytes per
object, warmed then measured after a forced gen2 collection, field values small integer
constants so a row difference is structure rather than contents:

| Object | B/object |
|---|--:|
| `{}` | 192 |
| `{ a: 1, b: 2, c: 3 }` | 1 168 |
| `new T()`, empty body | 216 |
| `new T()`, **one** field | **1 256** |
| `new T()`, **three** fields | **1 256** |
| `new T()`, eight fields | 2 712 |
| `class C`, three fields | 1 448 |
| `Object.create(null)` + three fields | 1 224 |

**One field costs the same as three, and both cost ~1 040 B more than no fields at all.** The
per-object cost is a fixed block, not per-field storage: `SAUint32Map` allocates its trie
nodes from a `VirtualMemory<T>` whose first allocation rounds up to **16 nodes**, and a node
is a whole `JSObjectProperty` — a descriptor plus two link fields. One property therefore
reserves sixteen descriptors' worth of memory and uses one. The block covers the first four
node groups, which is why fields two and three are free, and the step to eight fields is the
next block.

**This is a trade, not an oversight, which is why it is sized rather than started.** Two
alternative growth policies, measured:

| Policy | 1 field | 3 fields | 8 fields |
|---|--:|--:|--:|
| round up to 16 (current) | 1 256 | 1 256 | 2 712 |
| round up to 4 | **584** (−53%) | **1 056** (−16%) | 3 432 (**+27%**) |
| minimum 4, then double capacity | **584** | **1 056** | 3 880 (**+43%**) |

A smaller floor makes a one-field object less than half the cost and a three-field object 16%
cheaper, and makes an eight-field object worse by paying repeated resize-and-copy. The 16-node
floor is buying amortized growth for medium objects with memory that small objects do not use.

**What decides it is the size distribution of real objects, which no synthetic probe can
supply.** Every object phase 2 names is small — Richards' `TaskControlBlock`, DeltaBlue's
constraints, RayTrace's `Vector` (3), Box2D's `b2Vec2` (2) — which argues for the smaller
floor. But "small objects dominate" is exactly the kind of premise this document has now been
wrong about twice. **Instrument the distribution over an Octane run first**, then pick the
floor, and consider a policy that is small at the bottom and geometric only after the first
block rather than one constant for both. Related to **B1**: this is a large part of what makes
the allocation rate severe, and unlike B1 proper it needs no change to value representation.

**Size: S for the change, M for the measurement that justifies it.**

#### The blocking measurement now exists — `--property-map-distribution`

`PropertyStorageMetrics` (in `Broiler.JavaScript.Storage`, the layer that owns the floor) records
**the final node-group count of every map**, per `SAUint32Map<T>` value type. Each allocation moves
its map out of the previous bucket and into the next, so `histogram[k]` ends up holding the number
of maps whose life ended at `k` groups. A map that never allocated — an object with no named
properties — is counted nowhere, which is right: it never pays the floor. Resizes and the nodes they
copy are counted too, because that is the cost a smaller floor trades *for*.

`--property-map-distribution <octane-dir>` runs Octane's own suites, one fresh context each with the
histogram reset between them so a per-suite disagreement is visible rather than averaged away, and
simulates each candidate policy against the result. Two deliberate choices: the simulation **mirrors
`VirtualMemory.Allocate` step for step** instead of modelling it, and the node size comes from
`SAUint32Map<T>.NodeSizeBytes` instead of a hand-added field list — so the arithmetic cannot drift
from the code it is about. `BROILER_MAP_DISTRIBUTION_RUNS` sets runs per benchmark, present so the
claim that the distribution converges can be *checked* rather than assumed.

**First result confirms the model the item was built on, from the real layout rather than from
`--object-alloc`'s deltas:** a node is **56 bytes**, so the 16-node floor is **16 × 56 + 24 = 920 B**
for any object carrying a named property, and one field and three fields both land inside a single
block. It also confirmed the trie allocates in groups of four, and that the first block covers four
groups.

**Still open: the distribution itself, at a sample worth deciding on.** A 3-runs-per-benchmark smoke
produced only 65 property maps for Richards — Richards creates a handful of `TaskControlBlock`s and
then passes messages, so map *allocations* are rare even though the suite is long. The run of record
needs a larger sample and a check that the shares do not move with the run count. **Do not pick a
floor before that number exists** — this item's whole reason for being sized rather than started is
that the answer is a distribution, and a converged one is the only kind that settles a trade.

Instrumentation landed as `patches/0057-js-property-map-distribution-metrics.patch`.

#### `--object-alloc`, and why the corpus grew again

`ObjectAllocationMetrics` in `Broiler.JS/benchmarks/Broiler.JavaScript.Engine.Benchmarks`
emits the table above as JSON, by the Appendix A method — forced gen2 collection, then
`GC.GetAllocatedBytesForCurrentThread()` deltas over 50 000 objects, warmed first so
compilation, key strings, shapes and cache entries land outside the measured run. It joins
`--cache-metrics` (hit rates) and `--sparse-metrics` as a standing emitter for a quantity no
wall-clock benchmark reports, and it exists because 2-3 could not be decided without it: the
item's *only* surviving justification was memory, and there was no way to measure memory per
object from a clean checkout. Both 2-3's re-sizing and 2-7 came out of its first run.

**Sequence.** 2-0 ✅ → 2-1 ✅ → 2-2 ✅ → 2-4 ✅ (both halves) → 2-8 ✅; **2-5 closed** and
**2-6 folded into 4-1**, both on measurements. **Every listed item in this phase is now either
landed or closed.** What remains is not implementation: **2-3 is re-specified** (M, and
superseded in value by 2-7) and **2-7 needs an object-size distribution from an Octane run before
it can be picked up**. The phase's own exit gate — test262 `properties-proxy` and `strict-mode`,
now covering 2-1, 2-2, 2-4 and 2-8 — and 0-6's CI Octane run are what stand between "landed" and
"closed".

**Verify — per item, not per phase.**

- An `ownership.json` entry naming its benchmark and semantic owner. The file is
  item-scoped and already carries 36 entries; match that granularity.
- Coverage in `PropertyShapeCacheTests` / `PropertyStoreCacheTests` for **every**
  invalidation path: `setPrototypeOf`, prototype mutation, own-property shadowing,
  `delete`, freeze, accessor redefinition, polymorphic and megamorphic sites.
- **P1-1 already touches `OrdinarySetWithOwnDescriptor` — the single most
  spec-sensitive path in the engine — and 2-1 to 2-4 touch it again.** test262 over
  `test262-properties-proxy` and `test262-strict-mode` is not optional here.

**Exit criterion: DeltaBlue and Richards inside 200×.** They are the outliers on a
curve whose median is ~180×, and this phase is the reason they are.

> **A known deviation on this path**, pinned by `ReflectSetReceiverAttributesTests`:
> `Reflect.set` gives a receiver's new property the *base's* attributes instead of the
> all-true set `CreateDataProperty` mandates. **No test262 file at the pinned ref
> reaches it** — `creates-a-data-descriptor.js` uses an empty target where step 4.d
> supplies the default `ownDesc`, and `different-property-descriptors.js` covers only
> an accessor. The engine passes every file in `Reflect/set/`. Do not let phase 2 make
> it worse silently.

---

## Phase 3 — value representation

**Targets: Crypto (301×), zlib (340×), RayTrace (291×), EarleyBoyer (270×), Splay
(152×), NavierStokes (104×).** Blocker **B1** — the largest total win in the plan and
the largest change. Deliberately after phases 1 and 2 because those are contained and
this is not.

Owner assemblies: `Broiler.JavaScript.Storage`, `.Runtime`, `.Compiler`.

### 3-1 · Unboxed backing stores for dense arrays — **start here**

**Where.** `Broiler.JavaScript.Storage/ElementArray.cs` — `private IPropertyValue[] dense`.

P2-3 made each element one reference instead of a 32-byte descriptor, which was a real
win, but a dense array of a million doubles is still a million heap objects behind a
million interface references.

**Work.** A typed backing store (`double[]`, `int[]`) chosen on first store, with an
elements-kind tag on `ElementArray`, transitioning to `IPropertyValue[]` on the first
non-numeric write. Standard, well-understood machinery.

**Target.** Crypto's 28-bit digit arrays, NavierStokes' grids, and the
typed-array-shaped heaps in zlib, Mandreel and Gameboy. **The most contained item in
the phase and the one covering the most benchmarks** — which is why it goes first.

**Verify.** `test262-arrays` and `test262-binary-data`; `CompactElementStorageTests`,
`ElementDescriptorRoundTripTests`, `IndexedWriteAndLengthTests` for integrity levels,
foreign receivers, exotics and length-shrink. **Report allocation per element
alongside time.** **Size: L.**

### 3-2 · Unboxed doubles in shape slots

The object-field twin of 3-1: `shapeSlots` holds `JSValue` references, so
`vector.x = 1.5` allocates. This is what RayTrace and Box2D need, and it **composes
with 2-1** — a shape that knows a slot is a double can store it raw, so land 2-1 first
and this gets cheaper.

**Where.** `Runtime/JSObject.cs`, `Runtime/ObjectShape.cs`. **Size: L.**

### 3-3 · Widen the unboxed-locals eligibility gate

P2-2 item 3 currently covers a function-top-level `var` not named by any nested
closure. Still ineligible: **function parameters**, `let`/`const` (needs TDZ analysis),
and `var` declared inside a block or loop body (needs definite-assignment analysis).

**Parameters are the valuable one** — every numeric helper takes them, and every Octane
benchmark is full of numeric helpers. Do parameters first; treat the other two as
separate items.

**Where.** `Broiler.JavaScript.Compiler` — the P2-2 eligibility gate.

**Watch:** patch 0047 exists because this codegen path produced **invalid IL** when an
unboxed local reached value position. Widening the gate widens that exposure;
`InvalidProgramException` is the failure signature to test for. Note also that the same
work carries the `NaN <= x` bug as a precedent for how subtly numeric specialization
goes wrong. **Size: M.**

### 3-4 · A tagged value representation — *scope and cost, do not start*

The real fix, and a multi-quarter redesign of the engine's most fundamental type with
every built-in downstream of it. An `ownership.json` entry (`tagged-js-value`) already
exists from the earlier campaign.

**Write it up and cost it at the end of phase 3**, once 3-1 to 3-3 have shown how much
of the gap survives unboxed arrays, fields and locals. It is entirely possible the
answer is "less than expected", and that is worth knowing *before* committing to the
redesign rather than after. **Size: XL.**

---

## Phase 4 — speculation

**Target: everything, and it is the difference between ~100× and ~10×.** Blocker
**B2**. The second scope exclusion this document overturns (§1.1).

Two findings make it more tractable than it looks.

**The tiering scaffolding already exists and is general.** `Runtime/FunctionTiering.cs`
has `FunctionTieringController` with an invocation threshold, a per-realm budget, a
retained-code cap, delegate replacement, and `RecordDeoptimization` counters, gated
behind `JSContextOptions.FunctionTiering` (disabled by default, and it must retain the
original delegate as the semantic fallback).

**But there is no optimizing compiler behind it.** `JSFunction.RecompileForTiering`
with `numericPlan == null` re-runs `CoreScript.Compile` on `({source})` with a one-shot
cache — it recompiles *the same code the same way*, so it cannot be faster. The only
real specialization is the `NumericLoopPlan` path. **Tier-2 today is a hook, not a
tier.**

That is a good position: the bookkeeping, budget and safety-fallback policy are built
and tested; what is missing is the part that makes entering tier-2 worth anything.

| # | Item | Where | Note | Size |
|---|---|---|---|---|
| **4-3** | **Deoptimization** — **do this first** | `Runtime/FunctionTiering.cs`, `Engine/CallFrames.cs` | The safety net that makes everything else legal. Must bail out **mid-function** when a guard fails; the current model can only swap the delegate for the *next* call. The gating item for the entire phase | XL |
| **4-1** | **Type feedback collection** — *now also carries what was 2-6* | `Runtime/ObjectShape.cs`, `.Compiler` sites | The inline caches already observe shapes at property sites. Extend to record and retain observed shapes, **callee identities**, and numeric-vs-generic outcomes per site. Callee identity was phase 2's 2-6 until that item was measured: there is no repeated callee resolution to remove, so recording it is feedback and nothing else, and it pays only once 4-2 and 4-4 consume it | L |
| **4-2** | **A specializing tier-2 compile** | `BuiltIns/Function/JSFunction.cs` — replace the `numericPlan == null` branch | Consume 4-1's feedback: monomorphic property access → shape check plus direct slot read; arithmetic → raw `double`/`int` where feedback says so | XL |
| **4-4** | **Inlining of small JS callees** at monomorphic sites | `.Compiler` | What Richards and DeltaBlue actually need, and the measurement says why: **a call costs ~250 ns, about thirteen times the loop body it replaces** (2-6). Strictly downstream of 4-3, 4-1 and 4-2 — the callee-identity feedback it needs is 4-1's, not a separate phase-2 item | XL |

**Do not start 4-2 before 4-3 has a design.** Speculation without a mid-function
bailout is either unsound or restricted to functions with no observable side effect
before the guard — which excludes everything worth optimizing.

**Verify.** Deopt correctness before any speculation ships: a test that forces every
guard to fail at every point in a function body and asserts the fallback produces the
unspecialized answer. Then the full test262 matrix — **this phase can break anything.**

> **The frame work in §4.1 is a prerequisite nobody filed as one.** Mid-function
> bailout needs to reconstruct an interpreter frame from a specialized one, and the
> activation record is now a slot in `CallFrameStack` addressed by a `FrameToken`
> struct. The three invariants that redesign asserts — a suspendable frame retaking a
> slot under a different caller, unwinding refusing to grow back into abandoned slots,
> and popping past stranded callees — are exactly the surface 4-3 has to preserve.

---

## Phase 5 — RegExp

**Target: RegExp (110×), plus PdfJS and Typescript.** Blocker **B5**.

Deliberately late: it costs one score, measured against Octane's *lowest* reference
baseline. But its value is larger than that score suggests, because the same engine is
on PdfJS's and Typescript's critical path, and the component has its own roadmap at
[`Broiler.JS/Broiler.Regex/docs/roadmap.md`](../Broiler.JS/Broiler.Regex/docs/roadmap.md).

**Order.** Profile `Matching/Matcher.cs` against the Octane regex corpus **first**, to
separate backtracking *strategy* from per-step *interpretive overhead*. Then compile
the common subset — literal prefixes, character classes, bounded quantifiers — keeping
the interpreter as the fallback.

**Gate:** the corpus is profiled **before** any rewrite. Broiler.JS additionally owns
the integration gate from its own roadmap: route only features the native engine
implements and tests, compare both backends during expansion, move `Exec`, `Split` and
`Replace` to one match-data abstraction, and retire the .NET translator only after the
pinned RegExp corpus is clean.

---

## Sequencing

| Phase | Order within it | Size | Unblocks / expected effect | Exit gate |
|---|---|---|---|---|
| **0** | 0-1…0-5 ✅, 0-9…0-11 ✅ → **0-6 (CI) → 0-7, 0-8** | — | Everything. 12 → **17 scores**, known noise band, and the first evidence any phase A–F can close on | 17/17, no timeout at the 180 s floor, band on record, `comparison.md` reporting the triad, **and the BenchmarkDotNet + RID-matrix rows collected** |
| **1** | 1-2 mitigation ✅ → **1-1** → 1-2 real fix → 1-3 measure | XL | The two worst scores in the suite; page-load time generally | test262 over the four pinned manifests, no new failure **and no new timeout**; MandreelLatency and CodeLoad out of the tail |
| **2** | 2-0 ✅ → 2-1 ✅ → 2-2 ✅ → 2-4 ✅ → 2-8 ✅; 2-5 closed, 2-6 folded into 4-1; 2-3 re-specified (M), 2-7 needs its measurement. **No listed item is still open** | M each | The Richards/DeltaBlue/Box2D cluster | An ownership entry and owned tests **per item**; test262 properties/strict-mode — **owed for 2-1, 2-2, 2-4 and 2-8**; **DeltaBlue and Richards inside 200×** |
| **3** | **3-1** → 3-3 → 3-2, then *cost* 3-4 | L–XL | Uniform lift across arithmetic and allocation-heavy suites | `test262-arrays`, `test262-binary-data`; allocation reported per item alongside time |
| **4** | **4-3 design first** → 4-1 → 4-2 → 4-4 | XL | The remaining order of magnitude | Deopt correctness proven **before** any speculation ships; full test262 matrix |
| **5** | profile → compile the common subset | L | RegExp, plus PdfJS and Typescript | Octane regex corpus profiled **before** any rewrite |

**Dependencies.**

- Phase 0 gates every claim in phases 1–5 *and* retroactively gates closing A–F.
- Phases 1 and 2 are independent of each other and of phase 5, and can run in parallel.
- 3-2 is cheaper after 2-1.
- Phase 4 depends on 4-3 (for everything in the phase) and on 4-1 for 4-4's callee feedback — what was 2-6 is now inside 4-1 — and
  benefits from 3-1/3-2 having established unboxed representations to speculate into.

**The bolded item in each phase is the one to start with**, and in three of the five it
is not the one that sounds most important: 1-1 over 1-3, 2-1 over 2-2, 4-3 over 4-2.
Each ordering is argued where the item is described.

**Every phase closes under
[`Broiler.JS/docs/performance.md`](../Broiler.JS/docs/performance.md)**, unchanged: two
runs inside the configured band, on the release RID matrix, reporting time, allocation
and working set together, with an `ownership.json` entry naming each item's benchmark
and semantic owner. **Phases A–F are all *implemented* and none is *closed* for exactly
this reason.** This plan should not add to that debt.

---

## Non-goals

Stated explicitly so effort does not drift into them.

- **GC work.** SplayLatency at 45× is the *best* result in the suite (B7). The
  allocation **rate** is a severe problem — that is phase 3, and it is a problem with
  what the engine asks the collector to do, not with the collector.
- **asm.js or WebAssembly special-casing** for Mandreel and zlib. Recognizing asm.js
  type annotations would move two scores and is exactly the optimize-for-the-benchmark
  behaviour that got Octane retired in 2017. Phases 3 and 4 reach the same code through
  general mechanisms.
- **Chasing the geomean directly.** If a change raises the total without raising the
  worst scores, it has not smoothed anything (§2.1).
- **Anything that trades conformance for speed.** Every item is a
  same-observable-behaviour change. Where the spec-visible surface is genuinely at risk
  (1-1's early errors, 2-1…2-4's `OrdinarySetWithOwnDescriptor`, all of phase 4) the
  risk is called out and the gating manifest named.
- **Security.** Broiler.JS is not a sandbox, and none of this changes that. Compliance
  and performance completion must never be presented as isolation of untrusted scripts.

**Scope discipline.** Octane was retired by its authors precisely because engines began
optimizing for its shapes. Every item above is justified by a *mechanism* that matters
to real JavaScript, with the benchmark used as **evidence that the mechanism is
missing** — never as the target.

**No longer non-goals:** parsing/compilation and a speculating tier, both of which the
engine roadmap excluded. See §1.1.

---

## Appendix A — reproducing the measurements

### The engine probes

Each scenario is `ctx.Eval`'d once to warm and compile, then measured on a second
evaluation. Timing is `Stopwatch`; allocation is
`GC.GetAllocatedBytesForCurrentThread()` deltas after a forced gen2 collection. Cache
behaviour is read from `PropertyOptimizationDiagnostics.Snapshot()` after `Reset()` —
note the counters default to **off** since P0-1 and need an `Enable()` scope.

```js
// loop-empty            (3M)  var s=0; for (var i=0;i<3000000;i++) { s=i; } return s;
// arith-add             (3M)  var s=0; for (var i=0;i<3000000;i++) { s=s+i; } return s;
// prop-own-get          (3M)  var o={x:1,y:2}; ... s=s+o.x;
// prop-own-set          (3M)  var o={x:1};     ... o.x=i;
// fn-call               (1M)  function f(a){return a;}              ... s=s+f(i);
// fn-call-strict        (1M)  'use strict'; function f(a){return a;} ...
// closure-call          (1M)  var k=1; var f=function(a){return a+k;} ...
// proto-method-call     (1M)  function P(v){this.v=v;} P.prototype.get=function(){return this.v;};
// class-field           (3M)  class C { constructor(v){this.v=v;} }  ... s=s+c.v;
// builtin-call          (1M)  s = Math.max(s, i);
// array-rw              (1M)  var a=new Array(1000); ... s=s+a[i%1000];
// obj-alloc            (500k) last = {a:i, b:i+1, c:i+2};
// array-push           (500k) a.push(i);
// string-concat        (200k) s = 'x' + i;
```

Real-world scripts are the repository's own
`Broiler.JS/OtherTests/JIntPerfTests/Scripts/*.js`, each in a fresh `JSContext`.

**These probes now have a permanent home** — `HotPathProbeBenchmarks` in
`Broiler.JS/benchmarks/Broiler.JavaScript.Engine.Benchmarks`, wired into all three
`Broiler.JS/eng/performance/phase0.json` profiles, with phase C's hit rates on their own
`--cache-metrics` emitter (item 0-9). That landed in `aa2b1562` and is carried by the
pinned pointer, so this appendix is the description of the corpus rather than the only copy
of it. §4.1's figures are still one-off *observations* — they were taken by the ad-hoc
harness, and the corpus has since contradicted two of its rows (see 0-9) — but they are now
checkable from a clean checkout, which is the part that was missing.

### Octane

```bash
# Full run (clones chromium/octane, builds BroilerJS, installs Chromium):
./scripts/run-octane-benchmarks.sh --repetitions 3

# Faster local iteration against an existing checkout / build:
./scripts/run-octane-benchmarks.sh --octane-dir /path/to/octane --skip-build --engines broiler

# Re-run one suite with the child's output streamed live:
./scripts/run-octane-benchmarks.sh --engines broiler --skip-build --only Crypto --verbose
```

`--only` writes under `logs/partial/`, so a debugging run never overwrites committed
results. Also: `--keep-scripts` (keep the combined script for passing suites),
`--no-trace` (drop the breadcrumbs for an undisturbed timing run), and
`--broiler-env K=V` to pass an engine diagnostic switch through, e.g.
`--broiler-env BROILER_GENERATE_IL_LOGS=1`.

**Start a failure diagnosis at
[`tests/octane/results/diagnostics.md`](../tests/octane/results/diagnostics.md)**, not
at the logs. For every suite that did not complete it gives the failing exception type,
the benchmark / phase / iteration it died in, the .NET stack, the JavaScript stack, and
a command to re-run that one suite. Three things make that possible: Broiler's managed
stack lives in the JS error's *message* and is captured in full rather than truncated;
stack traces are rewritten from the concatenated temp file back to `base.js:371`; and
the runner prints a breadcrumb on entering each `Setup`/`run`/`tearDown` phase and on
iterations 1, 2, 4, 8, …, so a suite that aborts the process still names what was live
when it died.

### test262

From the `Broiler.JS` submodule root:

```sh
python scripts/compliance/run_test262.py --path-file scripts/compliance/test262-<name>.txt \
  --suite-root <pinned checkout> \
  --broiler-dll Broiler.JavaScript/bin/Release/net10.0/BroilerJS.dll \
  --max-workers 8
```

`Broiler.JS/scripts/compliance/test262-failures.txt` is **generated** by
`Broiler.JS/.github/workflows/test262.yml` from a run's own results — a hand-written
entry is overwritten by the next run, and an entry only appears if some file actually
fails.
Gaps that no test262 file reaches are therefore pinned by repository tests instead
(`StrictModeFlowTests.KnownGap_AsyncAndGeneratorBodiesDoNotEnterRuntimeStrictMode`,
`ReflectSetReceiverAttributesTests`).

---

## Appendix B — traceability

Where each item came from, so existing cross-references still resolve.

| This document | Engine roadmap | Octane roadmap | State |
|---|---|---|---|
| §4.1 phase A | P0-1, P0-3 | — | Implemented, not closed |
| §4.1 phase B | P0-2 | — | Implemented, not closed |
| §4.1 phase C | P1-1, P1-4 | — | Implemented, not closed |
| §4.1 phase D | P1-2, P1-3 | — | Implemented, not closed |
| §4.1 phase E | P2-1, P2-2 (+ engine §6.5 array defects) | — | Implemented, not closed |
| §4.1 phase F | P2-3, P2-4, P3 | — | Implemented, not closed |
| 0-1 … 0-5 | — | 0-1 … 0-5 | Implemented |
| 0-6 | — | Octane §2.6 | **Owed** |
| 0-7, 0-8, 0-9 | engine §8.1 acceptance evidence | — | **Owed** |
| 0-10, 0-11 | engine §8.1, §8.2 | — | Done |
| 1-1, 1-3 | *excluded by engine §9* | 1-1, 1-3 | Open — superseded, see §1.1 |
| 1-2 mitigation | *excluded by engine §9* | 1-2 | **Landed** — `patches/0049`, pending submodule push |
| 1-2 real fix | — | 1-2 | Open — repair `StackGuard`, which cannot fire today |
| 2-0 | — (P1-2's guard, reached in a state it cannot recognise) | — | **Landed** — `patches/0050`, pending submodule push |
| 2-1 | P1-3 remainder | 2-1 | **Landed** — `patches/0051`, pending submodule push; **test262 owed** |
| 2-4 | P1-3 remainder | 2-4 | **Landed, both halves** — `patches/0054` (`o.x++`) and `patches/0056` (`o.x op= rhs`), pending submodule push; computed keys, `super`, optional chains, private names and the three short-circuiting compound forms stay out on purpose |
| 2-2 | P1-4 remainder | 2-2 | **Landed for arrays** — `patches/0053`, pending submodule push; its four named benchmarks were the wrong targets |
| 2-8 | — (the blocked half of 2-2) | — | **Landed** — `patches/0055`, pending submodule push; both prerequisites fixed. **Shipped a regression that broke DeltaBlue** (a cached store to `f.prototype` bypassed `JSFunction`'s cached-field sync); the gate that fixes it is folded into the same patch |
| 2-3 | P1-4 remainder | 2-3 | **Re-specified** — not a pure removal, ~3% ceiling, S → M, superseded in value by 2-7 |
| 2-7 | — (found measuring 2-3) | — | **Instrumented** — `patches/0057` adds `--property-map-distribution`; the floor is 920 B per object with a named property. Still needs the distribution itself at a converged sample before a floor is chosen |
| 2-5 | P0-2 remainder | 2-5 | **Closed** — measured at 0%; P0-2 had already taken the cost, and 2-1 narrowed what was left |
| 2-6 | — | 2-6 | **Folded into 4-1** — no callee resolution to cache; a call costs ~250 ns and a call-site cache removes none of it |
| 3-1, 3-2 | — | 3-1, 3-2 | Open |
| 3-3 | P2-2 item 3 remainder | 3-3 | Open |
| 3-4 | — (`tagged-js-value` in ownership.json) | 3-4 | Cost, do not start |
| 4-1 … 4-4 | *excluded by engine §9* | 4-1 … 4-4 | Open — superseded, see §1.1 |
| 5 | — | Octane §7 "regex, until late" | Open |
| Lazy frame materialization | P3 remainder | — | Candidate, not a task — no measured cost to remove |

**Status of the three source documents.**
[`Broiler.JS/docs/performance-roadmap.md`](../Broiler.JS/docs/performance-roadmap.md) and
[`tests/octane/roadmap.md`](../tests/octane/roadmap.md) are **archives** — superseded plans
kept for what they contributed, carrying diagnoses this document has since corrected, and
**not back-ported**. `tests/octane/roadmap.md` now says so at the top; the engine one is
labelled only here, because it is inside the submodule and this repository cannot annotate
it without a pointer bump. `tests/octane/benchmarks.md` is different: it is a *reference*,
not a plan, and stays live as the per-benchmark description.

**Dropped in the merge, deliberately:** the engine roadmap's detailed defect
narratives (the `SAUint32Map<T>` sentinel, the Debug-build stack-trace-on-throw, the
six pre-existing test failures, the three frame-recycling defects) are history, not
plan. They stay in
[`Broiler.JS/docs/performance-roadmap.md`](../Broiler.JS/docs/performance-roadmap.md),
which remains the archive of record; only their transferable lessons were lifted into
§3.5. Likewise `tests/octane/benchmarks.md` remains the per-benchmark reference —
§4.3 carries only the ranked blockers.

---

_Merged 2026-08-01 from `tests/octane/roadmap.md`, `tests/octane/benchmarks.md` and
`Broiler.JS/docs/performance-roadmap.md`. Engine facts verified against `Broiler.JS` at
`cdb2fd41`; Octane code sites at `45f4f679`._

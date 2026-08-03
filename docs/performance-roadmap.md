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
- **Provenance:** the pinned submodule pointer is **`2ebc0c3c`**, checked 2026-08-02 against
  the gitlink rather than against the prose — **and checking it is what caught that this line
  said `71dda1b7`**, which the pointer had moved past. `71dda1b7` is an **ancestor** of the
  current pin, so nothing recorded against it is invalidated; the correction is to the prose,
  not to any measurement — which is exactly the failure mode the rule two sentences down
  already names, caught by applying it. **Phase 5's two `replace` changes are *not* in the
  pin**: their pushes to the submodule remote returned 403, so they are carried as
  [`patches/0059`](../patches/0059-js-single-match-replace-one-allocation.patch) and
  [`patches/0060`](../patches/0060-js-stream-global-replace.patch) — **in that order**, since
  `0060` builds on the function `0059` restructures — and the pointer is deliberately unbumped.
  Their figures below were measured on a local build of `2ebc0c3c` **plus** those patches,
  control and change from the same tree. **Item 2-9's follow-up measurement is
  [`patches/0061`](../patches/0061-js-measure-2-9-materialization-cause.patch)**, independent of
  those two and measured on the pin alone.
  **Item 1-4 is [`patches/0065`](../patches/0065-js-linear-closure-rewrite-scope.patch)**, on the
  same terms: its push returned 403, the pointer is deliberately unbumped, and every figure in
  its section was measured on a local build of the then-current pin `79c6ff23` with and without
  that one patch — both arms from the same tree, interleaved. It touches
  `Broiler.JavaScript.ExpressionCompiler`, which none of `0059`–`0064` do, so it applies in any
  order relative to them.
  `a6f101cc` — which this section named until 3-3
  was worked, and which every §0 and phase-2 measurement below was taken at — is an **ancestor**
  of it (`merge-base --is-ancestor`), so nothing recorded here is invalidated;
  what moved on top of it is 2-9, 3-0, 1-2's visitor stack guard and the script-host shell
  joining the solution. **A pointer written into prose goes stale silently**, which is why
  §4.1's and §3.4's figures below carry the commit they were taken at rather than "the pin".
  **The patch handoff has completed**: `patches/0049`–`0058` were applied to `Broiler.JS`,
  pushed, and the pointer bumped, so all ten are now ancestors of the pin rather than
  pending against it — verified patch-by-patch against the submodule log, not inferred from
  the prose. `685026c0`, `cdb2fd41`, `7ef80c03` and `8228b0da` all remain ancestors, so item
  0-1's substance holds, and `685026c0` carries item 0-9's probe corpus (`aa2b1562`, #938).
  **Measurements and the test262 run in §4.1 and §3.4 were taken at `cdb2fd41` and have not
  been repeated** — `685026c0` also carries a string-allocation fix (#936). Octane code
  sites verified at `45f4f679`. **Phase 2's own measurements — §0, and each 2-x section —
  were taken at `685026c0` plus the then-pending `0050`–`0058`, which is exactly the tree
  `a6f101cc` now is**, so they describe the pinned pointer directly and no longer depend on
  a patch series being applied in order. Item rows were
  checked against the tree rather than inherited from the prose above them; doing that is
  what caught this, and it also caught that **item 1-2's acceptance criterion
  already passed before any work** (phase 1).

> **Path convention.** Because this document moved up a level, every path is written
> **relative to the repository root**. Paths carrying a `Broiler.JS/` prefix are inside
> the submodule — the source documents wrote those without it. Source *files* named in
> the item tables (`Runtime/ObjectShape.cs`, `BuiltIns/Function/JSFunction.cs`, …) are
> relative to `Broiler.JS/Broiler.JavaScript.*`, as they were in the original.

---

## 0. Status

**Last updated 2026-08-02.** Snapshot of where the campaign stands; every claim is detailed in
the item's own section below, and nothing here is *closed* — see the acceptance protocol in §3.

| Phase | State |
|---|---|
| **0** — evidence | 0-1…0-5 ✅, 0-9…0-11 ✅. **0-6 (the CI Octane run) is still the critical path, but its headline question is answered**: run at the pinned pointer on linux-x64, Octane is **15 of 15 suites `ok` and 17 of 17 scores** — Mandreel included, which the previous local pass had failing. Geomean 217, spread **113×** against a same-machine Chromium reference. What 0-6 still owes is the *workflow* run plus 0-7's BenchmarkDotNet and 0-8's RID matrix, which a container cannot produce |
| **1** — compile-time | 1-2's mitigation ✅ (`43bc4230`); **1-2's real fix is now on all three recursing passes** — the validator and emitter (`StackGuard` had three defects and could not fire), and now `FastParser`, whose descent aborted the process at 25 000 nesting levels **in the default configuration** and now survives 90 000 at no measurable cost. 1-2's stated acceptance criterion **already passed before any work** — it measured size where the cause was nesting. **New: 1-4 ✅.** Measuring 1-1's premise found the phase's actual dominant cost, and it was not lazy compilation: the closure rewrite held a lambda's in-scope bindings in a `List` and asked it `Contains` per parameter reference, so **emission was quadratic in a scope's binding count** — 2 000 top-level declarations emitted in 13 865 ms against 2.5 ms of parse. A reference-keyed multiset (list-backed below 32 bindings) makes it linear: **28.5× on that shape, and 3.04× on Mandreel end-to-end**, ABBA-interleaved, six pairs. **1-1 is still open and its premise now has a number** — 92–96% of compile time is function bodies on the large real programs — but the measurement also **splits phase 1 in two and re-targets 1-1**: Mandreel was *wide*, not deep, and never was a 1-1 case, while jQuery at 96.5% deferrable is the whole of it. 1-1 is a CodeLoad and page-load item, and its remaining sub-project is a **capture mechanism** — this engine cannot bake a captured cell into compiled code (`EmitConstant` refuses reference types), so a deferred body has nothing to compile against |
| **2** — property access | **Every item landed or closed.** 2-0 ✅ 2-1 ✅ 2-2 ✅ 2-4 ✅ 2-7 ✅ 2-8 ✅ **2-9 ✅**; **2-3 and 2-5 closed on measurements**; 2-6 folded into 4-1. The phase's conformance gate is **satisfied**, and **its Octane exit criterion is now answered and splits: Richards is inside 200× at 183× (band 163–191) and DeltaBlue is not, at 576× (band 538–711)** — five repetitions per engine, same machine. **DeltaBlue is what phase 2 has left** (item **2-10**), and it is the suite 2-8 was written for. Its first pass found and fixed a real defect — `push` cost every array its shape permanently, **2 503 dictionary fallbacks → 0** — but that did **not** move DeltaBlue's read hit rate, which stays at **65.96% against Richards's 86.61%** and is the live lead. Decomposing those misses ruled out megamorphism (**0** megamorphic read sites) and, in passing, **found a live `class`-shaped instance of 2-0's defect**: `class C{}; new C()` published a global prototype invalidation **once per allocation** (2 002 for 2 000). **Fixed as 2-11** — the setter no longer invalidates when the chain did not actually change — and the effect on the real suites is far larger than the class case suggested, because the retirement was process-wide: **Richards's read hit rate 86.61% → 99.97%**, DeltaBlue's 65.96% → 69.45%, Box2D's 96.39% → 97.72%, with invalidations 37 → 10, 2 519 → 16 and 1 944 → 107. Then **2-12** found why the misses that remained could never heal: the cache's add path deduplicated on two keys while a hit checked six, so a stale entry was declined rather than refreshed and its site missed for the rest of the process — **77.7% of DeltaBlue's misses**. Refreshing in place takes **DeltaBlue's read hit rate to 93.16%** (65.96% before both fixes) and Box2D's to 98.83%. **DeltaBlue still fails the gate at 447×**, but the cache is no longer the reason, and what remains is not property-cache-shaped Also outstanding: **2-9's ~20% compile-and-first-run cost still wants a follow-up — but not the one that was written.** Its losing-side hypothesis was measured against the control it never had (a *strict* function, which carries no Annex B deferred cells) and is **wrong**: every function materializes its trie **exactly once** whether strict or not, because the `prototype` install is withheld from shape-only storage by 2-8's DeltaBlue fix. "Stop materializing for a deferred cell" would have removed a materialization that already happened. The replacement candidate — split cache-visibility from shape-only storage — is specified and **not attempted**, since it is the code whose last regression broke DeltaBlue and it needs 0-6 |
| **3** — arithmetic | Started. **3-0 landed, both halves** — an indexed access boxed its index; a read now allocates **nothing at all** and a write loses ~32 B, on reference arrays as much as numeric ones. **3-1 measured before starting and re-specified**: it trades write allocation for read allocation 1:1, so its clean half is live memory. **3-3's parameter half landed** — and the measurement re-specified it: the gap was a per-call `JSVariable` **cell**, not a box, so a three-parameter call went **230.2 → 62.2 B**. **Probing that analysis before extending it found a wrong-answer bug shipped since P2-2** — two writes it could not see, one returning NaN and one aborting the process on valid JavaScript; fixed, at no measurable cost. Its `let`/`const` half was then **built, measured (31.98 → 0.00 B/iter) and withdrawn**: it miscompiles after any earlier compilation in the same process, including for bindings the gate never admits, so the reproduction is recorded instead of the change. 3-4 is a cost, not a task |
| **4** — tiering | Open. **4-3's design is written** — and it re-specifies the item: this engine has no interpreter frame to reconstruct, so V8-style deopt has no counterpart here. Splits into 4-3a (state and enforce the restart contract the pilot already runs, S) and 4-3b (a generic fallback branch inside the specialized method, M–L), which gates 4-4 rather than all of phase 4. **4-1 can start now** |
| **5** — regex | **Gate satisfied, and it overturns the phase.** `Matcher.cs` is not the default engine — `JSRegExp` routes only semantic-gap patterns to it, and Octane's corpus has no look-behind and no `u` flag, so it barely runs. The engine that does serve them is `System.Text.RegularExpressions` built **interpreted**; `RegexOptions.Compiled` is worth ~2× on six of seven real Octane patterns and a stable **4.3× against** on the seventh — a *trim* — so a use-count policy is ruled out. Largest regex cost measured was neither: `replace` with a global flag allocated **42 859 B per match**, because an Annex B legacy static copied the subject on every successful match — **fixed, 0.048x the bytes and 0.30x the time**. Decomposing what was left **per call** then found a single-match `replace` paying two full UTF-16 copies of the subject through a `StringBuilder`; concatenating three spans instead is **4.020 → 2.020 B per subject character, exactly the predicted halving**, and **the identical defect in `String.prototype.replace`'s string-`searchValue` builtin was found by reading the neighbouring code and fixed with it**. The global case's retained result list then landed too — **2 032.8 → 478.3 B per match**, dead linear on both sides, by streaming when the receiver's `exec` is the pristine intrinsic and the replacement is a `$`-free string. **Every follow-up this phase named is now closed**; what is left is item 2's `Compiled` policy, deliberately unshipped |

**What phase 2 changed, measured.** Hit rates and byte counts are deterministic and exact; every
wall-clock figure is a median of interleaved process-granularity pairs against a control, per §3:

| Item | Result |
|---|---|
| 2-0 | `new` published a global prototype-mutation notice per allocation, retiring every prototype-keyed cache entry: **200 001 invalidations per 200 000 allocations → 3**. An inherited-method site inside an allocating loop went from a 50% hit rate to matching its hoisted control |
| 2-1 | A store that *creates* its property could never hit the store cache — **0 hits against 600 000 misses → 599 997 / 3**, and ~20% faster on a constructor loop |
| 2-2 | Named properties on a `JSArray` were a 100% miss: **0 → 199 999** |
| 2-4 | `o.x++` and `o.x op= rhs` reached **neither** cache — 0 hits *and* 0 misses. Both now take both, **0 → 199 999** on each side; the compound form went from costing 1.163x the spelled-out equivalent to 1.043x |
| 2-7 | The property map reserved 16 trie nodes for the first property of any object — **920 B unused**. 43.9% of 47 M real maps never outgrow one four-node group: **live map bytes 0.56x, allocated 0.82x**, and Typescript, the suite with the worst tail, gains most |
| 2-8 | Statics on a constructor function were a 100% miss — DeltaBlue's hot path — **0 → 199 999**, ~10% on a DeltaBlue-shaped loop. **This item also shipped a regression that broke DeltaBlue outright; the fix is folded into the same patch** |
| 2-9 | A shape-tracked property cost ~150 B of radix trie to store an 8-byte reference. The trie is no longer written at all while an object is shape-tracked — **a three-field object is 0.36x and an eight-field one 0.15x**, against **+8 B on every object** for the attribute array. Over an Octane run **six in seven property maps are never built**: 16.2 M → 2.5 M, live map bytes 0.15x. All 22 cache rows byte-identical. **Losing side, measured against a built control: ~20% on compile-and-first-run**, corroborated by Octane CodeLoad at 0.844 |

**Owed.** One thing now gates "landed" becoming "closed":

1. **0-6's CI Octane run** — the only measurement of phase 2's real exit criterion, *DeltaBlue
   and Richards inside 200x*. The committed results in `tests/octane/results/` predate the
   pointer bump and are stale.

**The conformance gate is satisfied.** All four pinned manifests were run at `a6f101cc` plus
2-9, on win-x64 against the pinned suite ref `ccaac100` — **8 220 passed, 84 failed, 9 timed
out, and every count is identical to §3.4's recorded run, manifest by manifest.** The 84 are the
same `$262`-requiring files and the 9 the same integer-limit cases already tracked in
`test262-failures.txt`. So `properties-proxy` and `strict-mode`, which phase 2's exit gate names
because 2-1, 2-2, 2-4 and 2-8 all touch `OrdinarySetWithOwnDescriptor`, are **clean — and 2-9,
which rewrites the storage underneath that path, adds no failure either.**

**The patch handoff, which was the third gate, is done.** `patches/0049`–`0058` have been
applied, pushed and the pointer bumped to `a6f101cc`; the patch files are cleared and their rows
moved to *Recently cleared* in [`patches/README.md`](../patches/README.md). What phase 2 measured
and what CI now clones are the same tree — which is the condition both remaining gates were
waiting on, and it is why the conformance one could be run at all.

**Two pre-existing defects found in passing**, both reproducing on a pristine build at the
pinned pointer, neither owned by this campaign: a refused write to a function's `prototype`
still redirects `[[Construct]]`, and Octane's RegExp suite fails its own checksum.

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

| Metric | Last committed run | **At the pin, linux-x64 (§Phase 0)** | Target |
|---|---|---|---|
| **Scores reported** out of 17 | 12 / 17 (stale) | **17 / 17** — 15 of 15 suites `ok` | **17 / 17** |
| **Geomean** over all 17 scores | 245 over the 12 that completed; ≈244 including the five *(0046)* measurements | **217** over all 17 | — |
| **Spread** = worst ÷ best, as ×-slower-than-Chromium | 4646 / 45 ≈ **103×** | 3 097 / 27 ≈ **113×** | **< 5×** |

The middle column is a single repetition on a developer container with its **own** Chromium
reference measured on the same machine, so its ratios are internally valid while its scores are
not comparable to the left column's. It is not 0-6; see Phase 0 for what it is and is not.

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
| What does an object or an element cost in bytes? | `--object-alloc`, `--element-alloc` |
| What does a local, a binding or a parameter cost? | `--local-alloc`, which reports the compiler's own eligibility counts beside the bytes |
| What does a regex cost, and which engine ran it? | `--regex-profile` |
| How much of a compile is function bodies — i.e. what can 1-1 win? | `--compile-profile <octane-dir>` |
| Which of parse / tree construction / IL emission is the cost, and is it linear? | `--compile-scaling` |
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
`test262-strict-mode`, `test262-realm-isolation`. First taken 2026-08-01 at `cdb2fd41`
(suite ref `ccaac100`), **re-run 2026-08-02 at `a6f101cc` plus 2-9 with every count
unchanged**, and **re-run again at `71dda1b7` plus 3-3, again with every count unchanged,
manifest by manifest** — so the table below describes the pinned pointer as well as the commit
it was first measured at:

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

### 3.5 Standing measurement lessons

These were paid for once each. They apply to every phase below.

- **Measuring an item's premise is how you find the item next to it.** 1-1's premise —
  "most front-end cost is function bodies" — needed a control, so one was built: the same
  source with every body replaced by `{}`. Five corpora agreed with the premise. The sixth,
  Mandreel, took **17.7 s with every body already removed**, and that residue was 1-4: an
  emitter quadratic in a scope's binding count, worth 3.04× on the suite 1-1 was written
  around. Nobody was looking for it, and no probe could have shown it — a one-liner has one
  binding, and a quadratic needs width to be visible. *A control built to size one item
  measures everything that item is not, which is the only place a cost nobody has named can
  show up.*
- **"Big input is slow" is a description, not a diagnosis, and it hides the exponent.**
  B4 said machine-generated code is expensive to compile, which was true, and everyone read
  it as being about size. It was about *width*: 2 000 bindings in one scope cost 4× what
  1 000 did, while parse and tree construction stayed flat. The tell was available from the
  start — Mandreel's ratio to Box2D was far worse than their size ratio — and it reads as
  "Mandreel is enormous" until someone divides. *Before accepting that a large input is
  slow because it is large, halve it and check that the cost halves.*
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
- **Check that the thing you measured is the thing you built.** `Broiler.JavaScript.csproj` — the
  `--script-host` shell every Octane and test262 run executes — was **not a member of
  `Broiler.JS.slnx`**, so `dotnet build Broiler.JS.slnx` left its output untouched. A full day of
  shell-driven verification therefore ran a binary from the previous session, and test262 came
  back "identical" for the trivial reason that both sides were the same executable. The unit
  suites and the `--object-alloc` / `--cache-metrics` emitters were unaffected, because those
  projects *are* in the solution — which is exactly why the discrepancy was invisible. Two
  things follow. The project is now in the solution, so a solution build refreshes the shell.
  And a run that cannot fail is not evidence: **assert something that is only true of the build
  under test before trusting a suite of it.** Here that is one command — deeply nested source
  with `BROILER_JS_COMPILE_STACK_BYTES=0` completes only with 1-2's guard present, and aborts
  without it. *The tell was there and was read as a puzzle rather than a signal: a guard that
  will not fire at a 100 KB threshold is not a subtle bug.*
- **Reproduce on the platform you will close on.** 1-2's repro was a win-x64 Octane run.
  The same suite completes on linux-x64 at the same pointer, so the CI run that was meant
  to confirm it never could. *A one-platform repro dates the item to that platform.* **And so
  does a one-platform verification matrix** — 1-2's own four-way table records "mitigation off /
  guard on completes", which is true on linux-x64 and false on win-x64, for the reason in the
  next bullet. The rule was applied to the item's repro and not to its proof.
- **A threshold larger than the resource it guards is not a guard, and it fails silently.**
  `StackSegment` segments a recursive walk after 4 MiB of stack. With the compilation mitigation
  disabled, the front end compiles in place on a win-x64 stack that measures **1 052 048 bytes** —
  so the threshold can never be reached, the guard never fires once, and the process aborts
  looking exactly as it would with no guard at all. The struct's own remarks predicted the
  shape of this ("a walk cannot know how large the stack it is standing on actually is"); what
  was missing is that the condition is reachable on a shipping platform rather than hypothetical.
  *An absolute limit on a resource whose size you cannot query is unfireable in precisely the
  cases you wrote it for — probe what is left (`RuntimeHelpers.TryEnsureSufficientExecutionStack`)
  instead of assuming how much there was.*
- **A formula's stated intent is not its behaviour.** 2-7 read the property map's 16-node floor as
  "buying amortized growth for medium objects with memory small objects do not use", and sized two
  alternatives against that reading. The rounding it describes only applies while
  `last * 2 <= max`, so past the first block the rule grew **linearly** and paid *more* copies than
  doubling. The floor bought nothing for medium objects; it overcharged small ones, and the
  replacement won on memory *and* time — including on the suite whose objects were supposed to be
  the reason for keeping it. *Trace the branch with real numbers before describing what a policy is
  for.*
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
- **Re-measuring can make an item *worse*, not just smaller.** 2-3's memory case was "the slot
  array is 4.5% of an object's bytes". 2-7 then cut 672 B out of that object — bytes 2-3 was not
  targeting — so the same proposal came back at **1.9%** for the common shape. *An item's share can
  fall because the work around it succeeded, so a share recorded before that work is not a share at
  all; and the direction is not predictable from the numerator alone.*
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
- **An item's title can hide the mechanism, and then the item asks for the wrong thing.** 3-3
  was "widen the *unboxed-locals* gate", and named parameters as its first target. That gate has
  two tiers, and a parameter was outside *both* — but the one it could actually reach is the
  **scalar** tier, because a `var` can be proved numeric by reading the function while a
  parameter's type is the caller's choice. Taken at its word the item would have delivered
  nothing; measured, the parameter gap turned out to be a per-call `JSVariable` **cell** and not a
  box at all, worth 56 B on every parameter of every call. *When an item names a category, check
  which tier of the mechanism that category is missing before accepting the tier in the title.*
- **A ranking inside an item is a claim, and it is usually the least-supported sentence in it.**
  3-3 said "parameters are the valuable one" of four ineligible categories. Measured, all four
  cost **31.98 B/iteration** — identical to the byte — so the ordering recorded the order they
  were written down. The measurement also *reversed* it: the three that were deferred can reach
  the numeric tier and the one that was promoted cannot. *An ordering with no number behind it
  will be followed anyway, because it reads like a conclusion.*
- **A comment that says "missing one here is a miscompile" is a checklist, and it has to be run
  against every member of its family.** `AstReduce` leaves `ObjectProperty`, `VariableDeclarator`
  and `Case` as leaves for its rewriting visitors. Two of the three walkers that must not accept
  that carry an override for each and a comment saying why. The third, `NameCollector` — which
  backs the *only* rejection path in `NumericLocalAnalysis` — carried none, so every name bound
  through an object pattern was invisible to every rejection at once: `var { a: s } = o` aborted
  compilation of the whole script with an unhandled `NotImplementedException`, and
  `({ a: s } = o)` returned NaN. *The hazard was known, written down and fixed twice; nobody
  grepped for the third case. When a comment explains a trap, search for every class that can
  fall into it before writing the comment again.*
- **A green single run is not a green feature when the bug needs two.** 3-3's `let`/`const` half
  passed every script-host check, every single-test run, and its own allocation measurement — and
  miscompiled the moment a second compilation happened in the same process. The script host
  evaluates one file per process, so it is structurally incapable of seeing a defect of that
  shape, and the unit tests only caught it because xUnit happens to reuse one process across test
  methods. *When a change touches state that outlives a compilation, the smallest honest test is
  two compilations — and if the harness you reach for runs one, it is not the harness.*
- **Probe the analysis you are about to extend, before extending it.** 3-3's successor widens
  `NumericLocalAnalysis` from `var` to `let`/`const`. Ten minutes of probing what the existing
  analysis does with unusual *writes* found two it could not see at all — one of them a
  process abort on valid JavaScript, shipped since P2-2. Extending first would have widened a
  wrong-answer bug to two more declaration forms rather than exposing it. *A gate is only as
  sound as the analysis behind it, and the cheapest time to audit that analysis is while you
  still think of it as someone else's.*
- **A per-unit figure cannot tell a fixed cost from a scaling one, and it reads as scaling.**
  Phase 5's profile reported `exec` at 0.22 bytes per subject character, which sounds like
  something walking the subject; measured per CALL at three subject lengths it is ~1 950 bytes
  FLAT plus 0.02 B/char. The normalization that made the first finding legible — bytes per
  character, which is how the per-match subject copy was spotted — made the second one
  invisible. *Normalize by the thing you think is driving the cost, then vary that thing to
  check.*
- **An item can be written from another engine's architecture.** 4-3 asked for a mid-function
  bailout that "reconstructs an interpreter frame from a specialized one" — the V8 model, with a
  stack map naming where each value lives. This engine has no interpreter frame to reconstruct:
  tier-1 is compiled IL and a JavaScript local is a CLR local of that method, which is what
  phases C–F were *for*. The design that fits is a fallback branch inside the specialized method,
  where the locals are shared because it is the same method — cheaper than the item, and it
  preserves the frame-stack invariants by never engaging them. *When an item names a mechanism
  rather than an outcome, check the mechanism exists here before sizing it.*
- **Eager work for a deprecated feature is still work, and it is charged to the feature that is
  not deprecated.** Annex B's `RegExp.leftContext` / `rightContext` partition the subject around
  the match, and keeping them warm copied the whole subject on every successful match — so
  `replace` with a global flag, which execs once per match, was quadratic in allocation at
  42 859 bytes a match. Nothing reads those statics in ordinary code; recording the span and
  slicing on read costs a reference. *A compatibility surface nobody calls should be paid for by
  the caller that arrives, not by every operation that might one day precede one.*
- **Check which component actually runs before profiling the one the plan names.** Phase 5 is
  written about `Broiler.Regex`'s closure matcher, and B5 ranked it as sitting on PdfJS's and
  Typescript's critical path. It does not: `JSRegExp` routes only semantic-gap patterns to it,
  and Octane's corpus has no look-behind and no `u` flag, so the suite the phase is justified by
  never reaches the component the phase is about. The engine that does serve it was one grep away
  — `new Regex(pattern, options)` with no `RegexOptions.Compiled`. *A blocker that names a file
  is making a routing claim, and routing is cheaper to check than to profile.*
- **A `StringBuilder`'s floor is two copies, and pre-sizing removes neither.** Phase 5's
  single-match `replace` assembled its answer through a builder: one copy into the chunk list,
  one back out through `ToString()`. Pre-sizing it was tried first and was worth 0.2% — .NET's
  `StringBuilder` chunks rather than doubles, so there was no reallocation waste to remove — and
  the change that worked was to not use a builder at all, `string.Concat` over the three spans,
  worth exactly half. The neighbouring `String.prototype.replace` had the same three appends into
  a builder that was *already sized exactly right*, and halved by the same amount — which is the
  cleanest statement of the point, since there was nothing left to tune. *When the final length is
  knowable in one pass, a builder is the wrong tool rather than a mis-tuned one, and tuning it
  optimizes the copy you should not be making.*
- **A defect found by profiling has siblings the profile cannot see.** The single-match `replace`
  was found in a `--regex-profile` row; the identical assembly in `String.prototype.replace`'s
  string-`searchValue` path had **no row at all**, and was found by reading the builtin next to
  the one being edited. Its before-slope then matched the profiled path's to three decimal places
  — 4.020 B/char both — which is what established them as one defect in two places rather than
  two resembling ones. *When a profile localizes a cost to a mechanism, grep for the mechanism;
  the corpus only measures what somebody thought to add to it.*
- **A fix recorded as landed covers the path it was measured on, not the feature.** 2-0 removed
  the per-allocation prototype invalidation and pinned it at "200 001 → 3" — on a `function`
  constructor. `class C{}; new C()` still publishes one per allocation, by the same mechanism the
  fix's own comment describes (a second write to the prototype that reads as `[[SetPrototypeOf]]`
  on a live object). The item was not wrong and its number was not stale; its *scope* was one
  construction path, and nothing in the record said so. *When a fix is verified through one
  syntax, name the syntax in the claim — the next reader will otherwise take the general
  statement, and a second path can carry the identical defect for as long as nobody spells it.*
- **A cache entry that cannot be replaced is worse than no entry.** The inline cache's add path
  deduplicated on `ShapeId` + `Holder`; a hit checked those plus four more guards. When one of the
  four went stale the read missed, reached the add path, was told the entry was already present,
  and returned — leaving the stale entry in place with no route back. That site then missed on
  that receiver forever, and it was **77.7% of DeltaBlue's misses**. A cold site would have
  recovered on its second read; this one could not recover at all. *Whenever a lookup and an
  insert disagree about what identifies an entry, the insert wins and the lookup starves — check
  that the dedup key is the whole guard, not a prefix of it.*
- **A process-wide invalidation makes every workload pay for the worst one.** The prototype
  version is deliberately coarse — one mutation anywhere retires every prototype-keyed entry
  everywhere — so a redundant write on one construction path held *Richards's* read cache at 86%
  when the machinery was capable of 99.97%. Richards does not construct classes; it was paying
  for someone else's writes. Every phase 2 probe measured the machinery in isolation, where the
  storm does not exist, and all of them reported it working. *A shared invalidation channel turns
  a local defect into a global one and hides it from every local measurement — the only
  instrument that sees it is a counter taken over a whole real workload.*
- **A counter that separates two workloads is a lead, not an explanation.** DeltaBlue fails
  phase 2's gate and Richards passes it, and of every inline-cache counter the sharpest split was
  dictionary fallbacks: **2 503 against 1**, three orders of magnitude. It traced to a real
  defect — `push` cost every array its shape permanently — and fixing it took DeltaBlue to **0**.
  **DeltaBlue's read hit rate then did not move by a hundredth of a percent.** The suite was
  losing shapes it never read through, because it puts no named properties on its arrays. The fix
  is worth keeping on its own merits and the investigation still has to start over. *Rank a
  counter by how well it explains the metric you care about, not by how sharply it separates the
  cases — and confirm the link by moving it, because "biggest difference" and "cause" are
  different claims and only one of them is testable cheaply.*
- **An exit criterion that has never been run is not a pending task, it is an unknown answer.**
  Phase 2's was *"DeltaBlue and Richards inside 200×"*, owed since the phase opened and carried
  through every item as one line of the sequencing table. Run at last, it **splits**: Richards is
  inside at 183× and DeltaBlue is outside at 576×, so the phase that was described as "every item
  landed or closed" has in fact failed half its own gate, on the suite item 2-8 was written for.
  Two repetitions would not have said that safely; five with a band did. *A gate carried unrun
  reads as "nearly done" for as long as nobody runs it, and the cost of running it is almost
  always less than the cost of the plan built on top of assuming it passes.*
- **A hypothesis with a plausible mechanism still needs the control that would refute it.** 2-9's
  losing side was explained by the Annex B deferred cells forcing a trie rebuild — a mechanism
  read straight off the code, correct in every step, and wrong about the cause. The control that
  settles it is one line of JavaScript: a **strict** function gets no deferred cells, so if the
  cells are the cause it must not pay. It pays exactly the same — 1.00 trie rebuilds per function
  on both — because the `prototype` install materializes first, for an unrelated correctness
  reason. *The question "what would I expect to see if this were false" has an answer that is
  usually cheaper to run than the fix the hypothesis implies, and running it first is what stops
  a fix being built for a cause that was not there.*
- **Count the thing, do not infer it from bytes.** The same hypothesis had been probed by
  allocation, where the deferred cells do show up — non-strict functions cost 4.8% more than
  strict, which reads like confirmation. A counter on the rebuild itself says 1.00 on both, and
  the 4.8% turns out to be the cells' own price and nothing to do with the trie. *An indirect
  instrument agreeing with you is weaker evidence than a direct one, and adding the direct one
  here was six lines.*
- **When an optimization skips work, the design is the observers, not the work.** Phase 5's
  streaming replace is four lines: append each replacement instead of collecting them all. Every
  hour of it went into establishing that nobody could watch the skipped result objects — and two
  of the three conditions that turned out to be needed are not in the item's description. The
  sharpest is the functional replacer: because the spec collects *all* matches before calling
  *any* replacer, the final failing `exec` has already reset `lastIndex` to 0 before user code
  runs, so a streamed replacer would see a *different value*, not merely a different order. The
  item said "changes the observable order"; the actual hazard was a changed value. *Enumerate who
  could have been watching, and check what each one would see — an item that names the hazard in
  the abstract has usually not enumerated them.*
- **"Is this builtin unpatched" can only be asked against a pristine capture.** The `exec` guard
  compares against `%RegExp.prototype.exec%` captured at realm init, before user code runs.
  Reading `RegExp.prototype.exec` at call time and comparing it to itself is circular — by then
  it may already be the patched one — and there is no property of the function object that says
  "genuine". *Identity against something captured earlier is the test; anything cheaper is
  answering a different question.*
- **A halving that lands exactly is a check on the decomposition, not just a win.** The same item
  predicted 4 B/char → 2 from "two full UTF-16 copies and nothing else". Measuring 4.02 → 2.02 at
  three subject lengths is what rules out a third copy hiding in the row; a saving of *roughly*
  half would have left the model unfalsified and untested. *Predict the number before the change,
  then treat a miss as evidence about the model rather than noise in the measurement.*
- **Interleave, at process granularity.** Sub-1.5% effects are only visible ABBA-
  interleaved across independent builds, ten runs each, medians compared.
- **Hold the call site fixed when the callee is what changed.** Sizing a parameter's cost by
  comparing `h(a)` called with one argument against `h(a, c, d)` called with three measured the
  *arguments* as much as the bindings, and reported 88 B per parameter. Passing three arguments to
  both — so the only difference left is how many the callee declares — gave **56**, which the
  before/after then confirmed exactly at 168 B for three. *Same failure as 2-4's diluted probe,
  from the opposite direction: there the probe was mostly inert, here it moved two things at once.*
- **The local suite will not catch a lifetime bug.** All three frame-recycling defects
  appeared as corrupted parent chains — two only as an intermittent hang. The suite
  stayed green through every one. Diagnosis was bisection to a failure *rate*
  (20–40 runs per configuration), not reading.
- **Mutation-test an invariant.** Both frame-lifetime rules pass the JavaScript-level
  tests with either rule deleted, because the corruption needs a job-queue
  interleaving the xUnit host does not reproduce. Assert the rule against the API.

---

## 4. Where the engine stands

### 4.1 Completed — phases A–F and 2 (implemented, none *closed*)

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
| **2** | 2-0, 2-1, 2-2, 2-4, 2-7, 2-8 | Every remaining way a constant-key property access missed its cache, closed: allocation no longer retires prototype-keyed entries, a store that *creates* a property can hit, arrays and functions track named properties by shape, and `++`/`op=` take both caches. **Six sites went 0 → 199 999 hits.** Plus 2-7, which is memory rather than hit rate: the property map's 16-node floor charged **920 B of unused trie** to every object's first property — **live map bytes 0.56x**. Plus 2-9, which finishes what 2-7 started one layer down: a shape-tracked object no longer writes the radix trie at all, so **a three-field object is 0.36x and an eight-field one 0.15x** and **six in seven property maps over an Octane run are never built** (16.2 M → 2.5 M, live map bytes 0.15x). Delivered as `2df877a0`…`a6f101cc`, all in the pinned pointer, and 2-9 on top |

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
| Double storage in `TrackShapeDataProperty` | everything — but **measured at ~3% of a worst-case store loop and, after 2-7, 1.0-4.3% of an object's per-property bytes**; 2-3 is **closed** on that, and the 67-94% that *is* the trie became 2-9, **which has landed** |

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

**This blocker named three causes and had a fourth, which was the biggest of them.** The
three-way split it asked for now exists (`--compile-scaling`) and reports **parse ≈ 0.5%,
expression-tree construction ≈ 11%, IL emission ≈ 89%** on function-dense source. Inside
that 89% sat an algorithmic defect rather than a cost: the closure rewrite's per-lambda
scope was a `List` scanned once per parameter reference, making emission **quadratic in a
scope's binding count**. "Machine-generated code is expensive to compile" was true and read
as a property of its size; it was a property of its *width*. Fixed in **1-4** — Mandreel's
whole front end 21 307 → 7 015 ms. What remains of B4 after it is genuinely the eager and
non-incremental part, which is 1-1 and 1-3.

The recursion is a separate, sharper problem: it aborts the process rather than costing
time, it lives in three passes across `.Parser`, `.Compiler` and `.ExpressionCompiler`,
and it follows source **nesting** rather than source **size** — a flat 200 000-statement
function is fine while ~19 400 nested operators is not. Mitigated at `685026c0` by giving
compilation a stack the engine sizes; see 1-2, which also records that the Mandreel
failure this blocker was written around does not reproduce on linux-x64. The blocker with
the clearest browser relevance: it is page load time. → **phase 1**

**B5 · The regex engine is a backtracking interpreter — *and it is not the engine Octane
runs*.** `Broiler.Regex`'s `Matching/Matcher.cs` has no compilation to native code; V8's
Irregexp JIT-compiles each pattern. RegExp is 110× off *against Octane's lowest reference
baseline*. **The second half of that sentence — "the same engine sits on PdfJS's and
Typescript's critical path" — is wrong, and phase 5's profile is what found it:** `JSRegExp`
keeps `System.Text.RegularExpressions` as the default engine and routes only semantic-gap
patterns to `Broiler.Regex`, and Octane's corpus contains no look-behind and no `u` flag, so
it never gets there. The engine that does serve those suites is built **interpreted** — no
`RegexOptions.Compiled` anywhere on the user-regex path. **This blocker names the wrong
component**; see phase 5 for what the measurement puts in its place. → **phase 5**

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
| **0-11** | An `ownership.json` entry per item | **Done.** Fifteen added, one per item rather than one per phase since the file is item-scoped, bringing it to 36 total, and 2-9 has since added `shape-only-property-storage` for 37. The pre-existing `tiered-unboxed-locals` is the same work as `numeric-local-doubles` and should be retired when the phase 0–5 evidence is next revisited — left alone rather than silently retargeted |

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

#### At the pinned pointer it is **17 of 17**, and phase 2's exit criterion finally has an answer

Run 2026-08-03 at the pinned `2ebc0c3c` on **linux-x64**, against upstream `chromium/octane`
`570ad1cc` (Octane v9), with the shell rebuilt by the harness rather than reused — and this
time **both engines on the same machine**, so the ×-slower column is internally valid in a way
no previous run's was.

| | Committed run (2026-07-31, CI) | Local pass (`b3f53dcc`, win-x64) | **This run (pin, linux-x64)** |
|---|---|---|---|
| Suites `ok` | 10 of 15 | 14 of 15 | **15 of 15** |
| Scores reported | 12 / 17 | 15 / 17 | **17 / 17** |

**Mandreel passes now, and it is the difference between 15 and 17 scores.** The local pass
above had it failing with 1-2's signature after 375 s of `global_init`; at the pin it completes,
which is what **1-2's real fix landing on all three recursing passes** predicts and is the first
run to show it. Every one of B8's five original failures stays fixed. **So 0-6's headline gate —
17/17 with nothing timing out — is met at the pinned pointer**, which is the tree CI clones.

**Read the timeout half of that gate precisely.** Nothing timed out, but two suites finish only
because `scripts/octane-suites.json` raises their budgets above the run-wide **180 s floor**:
**zlib at 644 s and Mandreel at 428 s**. The floor is a floor, not a bound, and it always was —
the config file records the earlier 647 s / 313 s measurements that set those budgets. Mandreel
is 37% slower here than the figure that budget was chosen from, which is the same
this-machine-is-slower effect the scores show, and it leaves the budget comfortable rather than
marginal. *"No timeout at the 180 s floor" would be the wrong sentence to carry forward: two of
the fifteen exceed 180 s by design.*

**The triad, with a same-machine Chromium reference:**

| Metric | Value |
|---|---|
| Scores reported | **17 / 17** |
| Broiler geomean over all 17 | **217** |
| **Spread** = worst ÷ best, as ×-slower-than-Chromium | **113×** — MandreelLatency **3 097×** worst, SplayLatency **27×** best |

MandreelLatency is still the tail by a wide margin, which is **phase 1's justification measured
rather than quoted**: the two worst rows in the suite remain the two that measure only the front
end. Nothing here changes that ordering.

**Phase 2's exit criterion — "DeltaBlue and Richards inside 200×" — has been owed since the
phase began. It is now answered, and it splits.** Re-run with **5 repetitions per engine**, so
the verdict carries a band rather than a point:

| Suite | Broiler median (band) | Chromium median (band) | ×-slower | Across the whole band | Gate |
|---|--:|--:|--:|---|---|
| **Richards** | 143 (7.0%) | 26 173 (8.6%) | **183×** | 163× – 191× | **PASS** |
| **DeltaBlue** | 116 (16.7%) | 66 759 (10.1%) | **576×** | 538× – 711× | **FAIL** |

Richards is inside 200× at *every* combination of the two bands, and DeltaBlue is outside it at
every combination — worst-case-to-best-case it misses by between 2.7× and 3.6×. Neither verdict
is a coin-flip against noise, which is the point of running five and reporting the band instead
of one and reporting a number. **DeltaBlue is the item phase 2 has left**, and it is the suite
2-8 was written for.

> **What this is not.** It is not 0-6. That gate is *the workflow*, and it also carries 0-7's
> BenchmarkDotNet comparison and 0-8's RID matrix, neither of which a container can produce —
> §0-8 already records why a non-idle machine cannot. These results are therefore **not committed
> to `tests/octane/results/`**, which stays CI's to write; the stale banner there still stands.
> The full-suite numbers are **one repetition** and the harness says so in its own output. What
> is claimed here is the pass/fail column, which is hardware-independent, and the two gate
> ratios, which are same-machine, five-repetition and banded. The geomean and the spread are
> recorded as this machine's, not as the campaign's.

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
  engines:         chromium,broiler,jint
  timeout_seconds: 180          # Mandreel and zlib raise their own
  repetitions:     3            # the first run that can distinguish signal
```

`jint` is the third engine the harness gained after this section was written:
[Jint](https://github.com/sebastienros/jint) run through `tests/octane/jint-host`,
with the same shell surface and stack budget as `BroilerJS --script-host`. It
costs about ten minutes of the run and adds the managed-versus-managed ratio —
another AST-walking managed engine on the same runtime, so unlike the Chromium
column it lands near 1 and moves with a Broiler change rather than dwarfing it.
It is a reference, not a target: Jint has no compilation tier, so a suite where
Broiler loses to it names a specific defect rather than a general deficit.

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

Owner assemblies: `Broiler.JavaScript.Parser`, `.Compiler`, `.BuiltIns` — **and
`.ExpressionCompiler`, which 1-4 adds and which is where the phase's cost turned out to
be.** The three-way split the phase was told to take (B4) now exists as `--compile-scaling`,
and on function-dense source it reads **parse ≈ 0.5%, expression-tree construction ≈ 11%,
IL emission ≈ 89%**. Every item here should be read against that: an item that does not
reduce what reaches the emitter, or what the emitter does per unit reaching it, is working
on the 11%.

**The phase splits in two, and the split is not the one the items are numbered by.**
Mandreel and CodeLoad were paired throughout as "the front end", and they fail for
unrelated reasons: Mandreel is **wide** (1 364 top-level declarations in one scope, which
was quadratic — 1-4, landed, 3.04×) and jQuery is **deep** (532 functions nested in one
IIFE, 96.5% of its compile in bodies that are never called — 1-1, open).

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

**Its premise is now measured, and it holds — but not on the suite the item leads with.**
`--compile-profile`'s control compiles each corpus with every outermost function body
replaced by `{}`, which is the floor an ideal deferral converges to. The saving is *not*
that difference: a body that is never called must still be **parsed**, because a syntax
error inside it is still a `SyntaxError` at script-compile time (this item's first named
risk), and the stub source has no bodies to parse. Charging that pre-parse back gives
`ceiling = full − stub − (parseFull − parseStub)`. Taken **after 1-4**, so it is what is
left for 1-1 to win rather than what was there before the phase started:

| Corpus | Functions | Compile | Ceiling | Share |
|---|--:|--:|--:|--:|
| codeload-jquery | 532 | 767 ms | 741 ms | **96.5%** |
| box2d | 982 | 413 ms | 390 ms | 94.4% |
| typescript | 1 763 | 947 ms | 892 ms | 94.2% |
| pdfjs | 949 | 991 ms | 914 ms | 92.3% |
| mandreel | 1 476 | 7 815 ms | 6 646 ms | 85.1% |
| codeload-closure | 57 | 72 ms | 45 ms | 62.9% |

So the item is right that most front-end cost is function bodies — 92–96% on the large real
programs — and it is right that a *parse* is all a deferral has to keep. **Two corrections
to how it is stated.** First, the ceiling is a **lower** bound, not an upper one: the
control still emits an empty lambda per function, and a deferred function emits none, so
the floor is below the stub. Second, the item's headline pairing of CodeLoad with
MandreelLatency does not survive: Mandreel's cost was never mostly bodies (see **1-4**,
which took it 3.04× without deferring anything), while **jQuery's is 96.5% bodies and
essentially all of it is deferrable** — CodeLoad evaluates it and calls almost none of it.
1-1 is a CodeLoad item and a page-load item. It is not the Mandreel item it was written as.

**And its cost side is now known too**, which is what makes it startable: the deferred body
has to compile against the scope it was created in, and this engine has no closure object to
hang that on. Its only mechanism for "compile later against captured bindings" is direct
eval's `JSVariable[]` capture list, and identifiers compiled that way resolve through
`JSContextBuilder.ResolveIdentifier` at **run time** rather than binding to a cell at compile
time — fine for CodeLoad, whose payload is eval'd code either way, and a steady-state
regression for anything hot. Baking the cells in as constants is not available:
`ILGeneratorExtensions.EmitConstant` throws `NotSupportedException` for any reference type
that is not a `string`, `Type` or `MethodInfo`. **So the sub-project inside this item is a
capture mechanism, not a pre-parser** — the pre-parser is the part that already exists.

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

1. **Mitigation (S) — landed as `43bc4230`.** Compilation
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
2. **Real fix (M) — landed for the two visitor passes; the parser is still open.**
   `StackGuard` existed to segment the emitter's recursion and **could not fire**, for three
   independent reasons any one of which was fatal: it tested `address - start > MaxStackSize`
   on a stack that grows *downwards*, so the difference was negative and the branch
   unreachable; it truncated 64-bit stack addresses to `int`; and its threshold was **1 024
   bytes**, so with the sign corrected it would have hopped threads every few frames.

   The rules now live in `ExpressionCompiler/StackSegment.cs` and are shared, because they are
   exactly what a copy-per-pass got wrong: anchor high, measure `anchor - current` the way
   `CallFrames.EnsureWithinStackBudget` does, read it unsigned so an upward-growing stack
   degrades to *never segments* rather than *always*, and hold the threshold in megabytes
   (4 MiB, `BROILER_JS_VISITOR_SEGMENT_BYTES`, `0` to disable).

   **Two things had to be discovered rather than designed.** Segmentation goes through a new
   `CompilationStack.RunOnFreshStack`, not `Run`, because `Run` returns *inline* whenever a
   compilation boundary is already established on the thread — and a segmenter only ever fires
   from inside one, so routing it through `Run` would have segmented nothing. And the pass that
   actually overflows first is **not** the emitter: it is `AstReduce.VisitBinaryExpression`
   under `SyntaxValidation.StrictModeValidator`, whose base `AstMapVisitor<T>` never derived
   from `StackGuard` at all. Repairing `StackGuard` alone therefore did not move the repro;
   the same guard had to be put on `AstMapVisitor.Visit`. **`FastParser`'s recursive descent is
   the third pass, and it is now guarded too** — see below.

   **Verified by turning each mechanism off independently**, which is the only way to tell which
   one is doing the work. A 20 000-operator chain through the script host: mitigation on / guard
   on completes, **mitigation off / guard on completes** — the guard alone — mitigation on /
   guard off completes, and with **both off the process aborts**. That last row is what makes
   the other three mean anything. Cost, interleaved on one build with only the environment
   variable differing: **median paired ratio 1.0027**, i.e. nothing.

   > **That matrix is a linux-x64 result, and the second row does not hold on win-x64.** Re-run
   > there while guarding the parser, *this same 20 000-operator chain* completes with the
   > mitigation on and **aborts with it off**, guard or no guard. The reason was measured rather
   > than guessed: with the mitigation disabled the front end compiles in place on a stack that
   > tops out at **1 052 048 bytes** — about 1 MiB — while `StackSegment.SegmentAtBytes` is
   > **4 MiB**, so the threshold is larger than the whole stack and the guard cannot fire once.
   > `StackSegment`'s own remarks anticipate exactly this ("a walk cannot know how large the
   > stack it is standing on actually is, and a threshold above it would never be reached before
   > the CLR aborted the process"); what is new is that the condition is *reachable on a shipping
   > platform* with the mitigation off. It costs nothing in the default configuration, where the
   > worker is 64 MiB and the guard fires freely — 223 times on a 10 000-level parse. **The fix
   > is an adaptive threshold** (`RuntimeHelpers.TryEnsureSufficientExecutionStack` probes what
   > is actually left, rather than assuming), and it belongs to `StackSegment`, so it would move
   > all three passes at once. Not done here. *A one-platform matrix dates its rows to that
   > platform — the same lesson this item already learned from Mandreel, applied to its own
   > verification instead of to its repro.*

   *No unit test pins the guard-alone row.* Doing so means setting `CompilationStack.SizeBytes`
   to 0, which is a process-wide static that xUnit's parallel classes would race on, so it needs
   process isolation the fixtures do not have. The four-way matrix above is a manual result, and
   saying so is better than a test that appears to cover it and does not.
3. **The parser pass (S) — landed.** `FastParser`'s recursive descent was the last of the three
   and the one that overflows *first*, before the validator or the emitter ever see a tree.

   **The failing case was written first and watched to fail, in the configuration that ships.**
   A right-nested conditional through the script host, mitigation at its default 64 MiB:
   20 000 levels returns its answer, **25 000 aborts the process** — no exception, nothing to
   catch. So this was not a defect that needed a diagnostic switch flipped to see; it was
   reachable by a syntactically valid script on the default build.

   **Where.** `Broiler.JavaScript.Parser/FastParser.Expression.cs`. The abort trace's repeating
   cycle is seven frames — `Expression` → `SinglePrefixPostfixExpression` →
   `SingleMemberExpression` → `SingleExpression` → `BracketExpression` → `ExpressionList` →
   `Expression` — and `Expression` appears in it twice, so guarding that one entry covers every
   nested construct. `.Parser` already references `.ExpressionCompiler`, so it shares
   `StackSegment` rather than copying it, which is the whole point of that struct existing.

   **After: 25 000, 40 000 and 90 000 levels all complete.** With the guard alone disabled
   (`BROILER_JS_VISITOR_SEGMENT_BYTES=0`) 25 000 aborts again, which is what makes the pass
   attributable to the guard rather than to anything else in the build.

   **Cost: none measurable.** The guard sits on the parser's hottest entry, so it was measured
   rather than assumed — 3 000 *distinct* `new Function` compilations (distinct so the code cache
   cannot answer them), six interleaved pairs on one build with only the environment variable
   differing: **median of paired ratios 0.9993**, three pairs above 1 and three below.

   **Verify.** A 25 000-level fixture in `DeeplyNestedSourceTests` — the smallest depth that was
   fatal, and decisive without touching `CompilationStack.SizeBytes`. A syntax error at the
   deepest point of one still reports the same exception type and the right offset (1, 552 809),
   which is the path that crosses the worker handoff. Repository suite **7 561 tests across 13
   projects, 3 failures**, the pre-existing win-x64 host ones. **test262 unchanged across all four
   pinned manifests** — 8 220 passed, 84 failed, 9 timed out, identical manifest by manifest,
   which is the gate that matters most for a change to the parser. **Octane 14 of 15 `ok`**, the
   same set as before it, with Mandreel's failure record **byte-identical** to the previous run —
   so this does not fix Mandreel, and does not pretend to: that one aborts on the *JavaScript*
   stack budget during execution, not in parsing.

   > **The first version of this guard was wrong, and the `off/on` row is what exposed it.**
   > It inferred "this is the outermost call" from `!segment.IsAnchored`, which reads correctly
   > and is false: `StackSegment.Continue` *deliberately* clears the anchor so the continuation
   > measures against its fresh stack — so the first call on a segmented continuation calls
   > itself outermost and releases the anchor again the moment that one sub-expression finishes.
   > The accounting then restarts from whatever depth the next call happens to sit at, so the
   > guard fires on an interval it did not choose. Fixed with an explicit recursion counter,
   > which says what the anchor cannot: *this is the top of the recursion, not the top of the
   > current stack.* **Recorded as a structural defect, not as a measured regression** — it was
   > found while chasing the `off/on` failure above, which turned out to have a different cause,
   > and the two were never separated. The fix is cheap and obviously right, so it stayed; what
   > it is worth was not established.
   >
   > **`StackGuard<T,TIn>` makes the same inference** for the validator and emitter passes. Not
   > changed here — those two are verified working and this item is the parser — but it is the
   > first place to look if either is ever found segmenting less than it should.

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

**The three-way split this item asks for now exists** — `--compile-scaling` times parse,
expression-tree construction and IL emission separately — and it was 1-4 that used it. Its
answer is lopsided enough to be the starting point 1-3 was told to go and find: on 2 000
top-level function declarations the three phases are **2.5 ms / 63 ms / 486 ms**. Parse is
noise, tree construction is 11%, **emission is 89%**, and that ordering held on every shape
measured. Whatever 1-3 becomes, it is an emitter item.

### 1-4 · The closure rewrite was quadratic in a scope's binding count — **landed**

**Not an item either source roadmap had**, because neither could see it: it is invisible to
a probe (one-liners have one binding) and it does not look like a bottleneck in a score
(it looks like "Mandreel is big"). It was found by measuring 1-1's premise, and it is
larger than 1-1's premise.

**What it was.** `LambdaRewriter.Scope` held the variables in scope for one lambda in a
`List<BParameterExpression>` and performed exactly the two operations a list is worst at:
`Contains`, run by `CheckForClosure` for **every parameter reference in the tree**, and
`Remove`, run once per variable as each block scope ends. Both are linear scans, so the
cost of emitting a lambda grew as the **square of the number of bindings in it**. A
script's top level is one lambda, which makes the count of top-level declarations the term
that squares — and machine-generated JavaScript is nothing but top-level declarations.

**The measurement that found it.** `--compile-profile` compiles each corpus twice: as
written, and with every outermost function body replaced by `{}`. The second is the floor
1-1 converges to, so the difference sizes 1-1. Five of six corpora behaved as 1-1 predicts.
Mandreel did not: with **every function body removed**, its remaining 248 KB still took
**17.7 s** to compile, against ~5 ms for the same control on PdfJS, Typescript and Box2D.
Per byte that residue was ~70× more expensive than Box2D's entire source — a difference no
deferral can explain, because there was nothing left to defer.

`--compile-scaling` then varied one thing at a time. Emission on N top-level function
declarations:

| N | parse | tree | **emit** | ms per declaration |
|--:|--:|--:|--:|--:|
| 500 | 0.8 | 12.7 | **796.9** | 1.62 |
| 1 000 | 1.3 | 24.0 | **2 980.8** | 3.01 |
| 2 000 | 2.5 | 70.2 | **13 864.5** | 6.97 |

Just under 4× per doubling with parse and tree construction flat: quadratic, in emission,
in the declaration count. Name length was ruled out in the same run (200-character mangled
names cost the same as `f1`), and plain `var`s were quadratic too — so it is bindings, not
functions.

**The fix.** The scope became a reference-keyed multiset. Three details are load-bearing:

- **A multiset, not a set.** The list held *duplicates* and both operations depended on it:
  a variable registered by two nested block scopes is added twice, and the inner scope's
  exit has to leave the outer registration behind. A `HashSet` would have collapsed the
  pair and taken a still-live binding out of scope — a miscompile, not a lost optimization.
- **The list is kept for small scopes.** A dictionary is not free — hashing a reference is
  a runtime call — and most scopes are small. Dictionary-only bought Mandreel 3.6× and cost
  jQuery, one large IIFE full of small function scopes, ~20%. Promotion above 32 bindings
  takes both ends; the list is abandoned once the index exists, so nothing is maintained twice.
- **The comparison is spelled out.** `List.Contains` resolves through
  `EqualityComparer<T>.Default`, i.e. a virtual `Equals` per element, and
  `BParameterExpression` overrides neither `Equals` nor `GetHashCode`. An explicit
  `ReferenceEquals` loop is the same answer without the dispatch — and it makes the identity
  semantics the rewrite depends on unbreakable by a later `Equals` override.

**Result.** Synthetic, same probe, before and after — emission on 2 000 top-level function
declarations **13 864.5 → 485.7 ms (28.5×)**, and cost per declaration went from climbing
(1.62 → 3.01 → 6.97) to flat (0.38 → 0.25 → 0.28). The speedup factor doubles as N doubles,
which is what tells quadratic-made-linear from a constant-factor win.

Real corpora, **ABBA-interleaved at process granularity, six pairs per arm**:

| Corpus | HEAD | Changed | Ratio |
|---|--:|--:|--:|
| **mandreel** | 21 307 ms | **7 015 ms** | **0.329×** — every pair 0.29–0.40 |
| pdfjs | 1 005 ms | 878 ms | 0.874× |
| typescript | 1 028 ms | 920 ms | 0.894× |
| box2d | 396 ms | 367 ms | 0.927× |
| codeload-closure | 48.9 ms | 50.8 ms | 1.039× — ratios straddle 1 |
| codeload-jquery | 540 ms | 600 ms | 1.112× — ratios 0.669–1.437, no signal |

A second A/B on **one build**, with `BROILER_JS_REWRITER_INDEX_THRESHOLD` as the only
difference, isolates the promotion from the devirtualized comparison: the index alone is
**0.536× on Mandreel** and inside noise everywhere else. So both halves are real and the
larger one is the index.

**What it does not do.** CodeLoad is untouched, and that is the honest reading rather than a
disappointment: its two payloads are 57 and 532 functions *nested inside one IIFE*, so no
scope in them is wide. This item is about width, 1-1 is about depth of work per function,
and Mandreel needed the first while jQuery needs the second.

**Verify.** `DeclarationDenseSourceTests` — a scale-free ratio bound (4× the declarations
must cost under 8×; it was 17.4× before and 2.6× after, so the bound is near neither), plus
two semantic fixtures for the duplicate-registration case a set would have broken. Full
repository suite: **7 630 tests across 13 projects, 0 failures.**

**Size: S.** One class, and it was found by measuring a different item.

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

Landed as `2df877a0`.

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

> **test262 has since been run, and it is clean.** §3.4's protocol and this phase's exit gate
> both require `test262-properties-proxy` and `test262-strict-mode` for anything touching
> `OrdinarySetWithOwnDescriptor`, and 2-1 touches its last step. Both manifests were run at
> `a6f101cc` plus 2-9 and match §3.4's recorded counts exactly — 3 950 / 38 and 1 040 / 26 — so
> this item's conformance debt is paid. See §0.

Landed as `5d31617a`, which builds on `2df877a0`.

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

Landed as `641241af`, on top of `5d31617a`.

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

Landed as `f9c2193f` (the update form) and `c5842c9d` (the compound form), on top of
`641241af` and `850121a0` respectively.

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
not from this patch. Mandreel passes too — it exceeded a 300 s smoke budget rather than
failing, and completes on both this build and a pristine one when given the 900 s that
`scripts/octane-suites.json` already budgets it.

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

Landed as `850121a0`, on top of `f9c2193f`, **with the gate folded in** — the patch was still
pending when this was written, so shipping it broken and fixing it in a later patch would have
left any partial application of the series with a DeltaBlue that does not run.

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

### 2-3 · Remove the double storage — **measured twice; closed, superseded by 2-9**

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

#### Re-justified against 2-7, and it does not survive that either — **closed**

2-7 has landed, so the re-justification this item was waiting on is now possible. Measured, not
modelled: the group count for each shape comes from `--property-map-distribution` and the bytes
from `--object-alloc`, both against the shipped build, and the trie figure is
`VirtualMemory.Allocate` replayed over the measured group count.

| Object | Trie nodes | Nodes **per property** | Bytes over empty | Of which trie | Trie share |
|---|--:|--:|--:|--:|--:|
| `new T()`, 1 field | 4 | 4.00 | 368 | 248 | **67%** |
| `new T()`, 3 fields | 8 | 2.67 | 840 | 720 | **86%** |
| `new T()`, 8 fields | 20 | 2.50 | 3 216 | 3 008 | **94%** |

**The item is aimed at the small side, and 2-7 made it smaller.** Its own proposal — slots holding
a `uint` node index instead of a `JSValue`, saving 4 bytes a slot — is worth **1.9%** of a
three-field object's per-property bytes, 4.3% at one field and 1.0% at eight. For a storage-layer
change with open questions about node identity across trie restructuring, deletes and deferred
cells, that is not a trade worth making. **Closed.** The 4.5% figure recorded before 2-7 was
against a denominator 2-7 has since cut; the share moved the *wrong* way for the item, because
2-7 removed bytes the item was not targeting.

**And its central premise is wrong in a way worth writing down.** "Store the value once" cannot be
done by dropping the `ownProperties` copy for shape-tracked objects, because a shape is *shared* by
every object that reaches it and `IsShapeTrackableData` admits **any** plain data property —
writable, enumerable and configurable in any combination. That widening was deliberate (without it
no prototype object could keep a shape, so no inherited method could be cached), and it means
per-property attributes are per-*object* data the shape cannot hold. Enumeration order the shape
*could* supply, since slot order is insertion order; attributes it cannot.

#### What the measurement actually points at — new item 2-9

**A property costs ~150 B of radix trie to store an 8-byte reference.** The trie allocates 2.5–4.0
nodes per property — a `JSObjectProperty` node is 56 B and only ~37% of the nodes a three-field
object allocates hold a property at all; the rest are branch structure. That, not the duplicated
8-byte value, is where a tracked object's memory is, and it is the same finding 2-7 made one layer
up: the storage is sized for a shape the workload does not have.

So the correctly-aimed item is **"shape-tracked properties should not live in a radix trie"** — for
an object in shape mode, key to slot is already in the shape, order is already slot order, and the
only genuinely per-object extras are the value (already in `shapeSlots`) and the attributes, which
are a *byte*. A parallel `byte[]` costs 24 + n against the trie's 150 B per property.

**L**, with a measured prize of 67–94% of per-property object bytes — the first version of this item
that has one, and the reason it was worth starting. It touches the same storage
`OrdinarySetWithOwnDescriptor` writes through, so it was sequenced with that path's conformance
gate rather than after it: **test262 and Octane were run as part of the change, not once it
looked finished.** Landed; see below.

#### Design spike — three questions answered, so nobody re-derives them

**1. Is there a single choke point?** Yes. `GetOwnProperties()` returns a **mutable
`ref PropertySequence`** to about 25 files across `BuiltIns`, `Extensions`, `Modules`, `Debugger`
and `Engine`, and `ownProperties` is otherwise private. A caller holding that ref can mutate the
trie directly, so lazy materialization is viable *because* every such caller has to go through the
accessor — but it also means the boundary must materialize unconditionally and then never
un-materialize. Design: shape mode holds nothing in the trie; the first `GetOwnProperties()` rebuilds
it and sets a flag; after that the object behaves exactly as it does today. Worst case is today's
behaviour, which is the property that makes this safe to land incrementally.

**2. Can a property be rebuilt from the shape?** **Yes, and with no change to `ObjectShape`.** This
was the question the item looked most likely to die on: the shape stores `Dictionary<uint, int>` —
key *hashes* — while a trie node needs a full `KeyString`. It turns out a `KeyString` **is** that
uint (`public readonly uint Key`, and `KeyStrings.GetName(uint) => new(id)` reconstructs it, with
`GetMetadata`/`GetNameString` alongside). So the shape already carries everything materialization
needs: iterate its keys in slot order — slot order *is* insertion order, since `ObjectShape.Add`
assigns `slots[key] = slots.Count` — and take the value from `shapeSlots[slot]`.

**3. Can the value be reconstructed without a per-object attribute array?** **No.** Two independent
reasons, both verified rather than assumed:
- `IsShapeTrackableData` admits any plain data property, so writable/enumerable/configurable vary
  per object at the same shape (see 2-3 above). One `byte` per slot, so a parallel array costs
  24 + n against the trie's ~150 B per property — still overwhelmingly worth it.
- **`JSProperty.get` is not derivable from `value`**, which kills the tempting cheap alternative of
  shrinking the node instead of replacing it. The accessor factory sets `value = get` and the data
  factory sets `get = value as IPropertyAccessor`, which together *look* like a redundant field —
  but four five-argument call sites pass them independently:
  `new JSProperty(key, getter, setter, existing.value, attributes)` in `JSObjectExtensions` and
  `JSObject.PropertyStorage` install an accessor pair while retaining the old **data** value, and
  `new JSProperty(key, null, null, deferred, attributes)` deliberately holds a null `get` beside a
  non-null deferred cell. So the 56-byte node does not shrink by 8 bytes for free; the prize is only
  reachable by not allocating the node at all.

#### Landed — the trie is not written at all while an object is shape-tracked

Built exactly as the spike specified, and the spike held: the choke point was where it said, the
shape did supply the keys and their order with no change to `ObjectShape`, and the attributes did
need a per-object array.

**Bytes per object**, `--object-alloc`, same method as 2-7 (forced gen2 collection, then
`GC.GetAllocatedBytesForCurrentThread()` deltas over 50 000 objects, warmed first):

| Object | Before | After | Ratio | Delta |
|---|--:|--:|--:|--:|
| `{}` | 192 | 200 | 1.04 | **+8** |
| `{ a, b, c }` literal | 968 | 288 | **0.30** | −680 |
| `{ …8 fields }` literal | 3 344 | 408 | **0.12** | −2 936 |
| `new T()`, empty body | 216 | 224 | 1.04 | **+8** |
| `new T()`, 1 field | 584 | 376 | **0.64** | −208 |
| `new T()`, 3 fields | 1 056 | 376 | **0.36** | −680 |
| `new T()`, 8 fields | 3 432 | 496 | **0.15** | −2 936 |
| `class C`, 3 fields | 1 248 | 568 | **0.46** | −680 |
| `Object.create(null)` + 3 | 1 024 | 344 | **0.34** | −680 |

**A three-field object is 0.36x and an eight-field one 0.15x**, and the shape of the win is the
shape of the finding: the saving is per *property*, so it grows with the object, where 2-7's was a
fixed block. One field and three fields no longer cost the same — the fixed block 2-7 shrank is
now gone entirely for these objects, and what remains is a slot and a byte each.

**The losing side is +8 bytes on every object**, including one with no named properties at all,
for the `shapeAttributes` reference. That is the whole cost, and it is smaller than it first
measured: carrying the materialization flag as its own `bool` field cost another 8 bytes on every
object — a fresh alignment group — so it moved into a spare bit of the existing `ObjectStatus`
word. **An empty object pays 8 bytes; a three-field one saves 680.**

**The inline caches are untouched, and that is asserted rather than assumed.** All 22
`--cache-metrics` rows are byte-identical to the pre-change build — every hit, miss, dictionary
fallback and shape transition — so nothing phase 2 landed is paid for here.

#### It has a losing side, and it is compile throughput

**Measured against a freshly built control at `a6f101cc`, on an idle machine, alternating between
the two builds so that machine drift could not masquerade as a result:**

| Workload | Control | With 2-9 | Ratio |
|---|--:|--:|--:|
| 4 000 × `new Function(…)` **and call each once** | 20 943 ms | 25 780 ms | **1.23** |
| 2 000 × `new Function(…)`, never called | 5 137 ms | 5 307 ms | 1.03 |
| 500 000 closure creations | 967 ms | 1 044 ms | 1.05 |

**So the cost is not in compiling — it is in compiling and then running the result once**, which
is the shape of a define-many-call-few workload. Octane agrees from the other direction:
**CodeLoad is 0.844**, and CodeLoad is the suite built to measure exactly that (jQuery defines
thousands of functions and calls almost none of them). Isolated by bisection: the same loop on
the 2-9-only commit measures 27 358 ms, so this is 2-9 and not 3-0 or 1-2 — 3-0 wins part of it
back.

**The likely mechanism, stated as a hypothesis because it is not yet proven.** Every ordinary
non-strict function carries the Annex B `caller`/`arguments` as deferred cells from birth (P0-3),
a deferred cell cannot be described from a slot, and `TrackShapeKeyWithoutSlotValue` therefore
materializes. So a function does the shape work *and* the trie rebuild where before it did the
trie work alone. That predicts the cost lands on functions and on whatever they drag in on first
call, which is where it is — but the +3% on function creation measured alone is smaller than the
hypothesis wants, so something on the first-call path is carrying the rest and has not been
identified. **Do not treat the mechanism as settled.**

**Taken anyway, on the same terms 2-7 was.** A real trade with a real losing side: six in seven
property maps never built and a three-field object at 0.36x, against ~20% on compile-and-first-run.
Octane's own verdict at 14 of 15 suites is mixed-to-positive on a single run — Splay 1.86 and
EarleyBoyer 1.27, the two suites whose map counts fell furthest, against CodeLoad 0.844 — but
**a single run per side cannot separate a change from noise (§3.2), so none of those score
movements is claimed here.** What is claimed is the allocation result, which is deterministic,
and the compile-throughput cost, which reproduced across three separate measurement rounds.
**The right follow-up is to stop materializing for a deferred cell** — the null-slot key it
records needs its descriptor somewhere other than the trie — which would test the hypothesis and,
if it holds, remove the loss. ***It does not hold. See below: the hypothesis was measured against
its own control and is wrong, and that follow-up would not have removed the loss.***

#### The losing-side hypothesis was measured, and it is wrong — **`prototype` is what materializes**

> **Delivered as a patch, not in the pin**, for the same 403 as phase 5's:
> [`patches/0061`](../patches/0061-js-measure-2-9-materialization-cause.patch), which is
> **measurement and instrumentation only — no behaviour change** — and is independent of
> `0059`/`0060`, touching a disjoint set of files.

The mechanism above was recorded as a hypothesis and flagged *"do not treat as settled"*. It is
now settled, and against it. **A strict function is the control it never had**:
`AddLegacyCallerAndArguments` runs for non-strict functions only, so if the Annex B deferred cells
are what forces the trie, a strict function must not pay it.

`--deferred-cell-cost` (new; `DeferredCellCostMetrics`) builds 4 000 functions each way, and a
new `RecordNamedPropertiesMaterialized` counter reports trie rebuilds directly rather than
inferring them from bytes:

| Site | B/function | ns/function | **materializations/function** |
|---|--:|--:|--:|
| `nonstrict-create` | 356 421 | 4 667 190 | **1.00** |
| `strict-create` | 340 161 | 4 900 524 | **1.00** |
| `nonstrict-create-and-call` | 356 226 | 8 693 125 | **1.00** |
| `strict-create-and-call` | 340 426 | 9 245 164 | **1.00** |

**Exactly one materialization per function, on all four rows.** A strict function — which has no
deferred cells at all — rebuilds its trie just as surely as a non-strict one, so the deferred
cells cannot be what causes it. The wall clock says the same thing from the other side: strict is
*marginally slower*, not faster, on both halves.

**What actually materializes is the `prototype` install, and it is a correctness rule doing it.**
Traced to `JSFunction..ctor`, whose three own-key writes are `length`, `name`, `prototype` — the
first two stay shape-only and the third does not, because
`JSFunction.AllowsDirectShapeWrite(uint key) => key != KeyStrings.prototype.Key` withholds that
one key. `FastAddValue` then falls off `TryShapeOnlySetDataProperty` to `OwnProperties()`, which
materializes. **That withhold is 2-8's DeltaBlue fix** — a cached prototype write left the second
level of every inheritance chain unlinked — so it is load-bearing, not an oversight.

**So the item is re-specified, and the planned fix is withdrawn before it was built.** Stopping
the deferred-cell materialization would remove a materialization that has already happened: every
function with a `prototype` has materialized before either Annex B cell is installed. The
non-strict rows do cost **4.8% more bytes** than strict, which is the cells' real price — but it
is a per-function 4.8%, not the compile-and-first-run loss the item is trying to explain.

**The candidate that replaces it, not started and deliberately not attempted here.** The withhold
exists so an *inline cache* cannot answer for `prototype`; shape-only *storage* is a different
question, and `AllowsDirectShapeWrite` is currently the single answer to both — it is consulted at
five sites, two of which are storage (`TryShapeOnlyOverwrite`, `TryShapeOnlySetDataProperty`) and
three of which are cache paths. Splitting the two would let `prototype` live in a slot while
staying invisible to the cache. **It is not attempted here because this is exactly the code whose
last regression broke DeltaBlue outright**, and §3.5's own rule is that a change justified by a
benchmark has to be run against that benchmark — which needs 0-6. The measurement is the
deliverable; the fix is specified and left.

**The first call is still unexplained, and it is not allocation.** The call roughly doubles wall
clock (4.7 M → 8.7 M ns per function) while adding **no** managed bytes at all — the
create-and-call rows are within 200 B of the create-only rows, on both halves. Whatever the
first-call cost is, it allocates nothing and does not split on strictness, which rules out both
the deferred cells and the trie. That narrows the item's open question rather than closing it.

#### It holds on real programs — `--property-map-distribution`, before and after

The rows above are synthetic sites, and the open question they cannot answer is how much of a
real workload *stays* shape-only rather than materializing on its first enumeration. 2-7's
emitter answers it directly, because a map that is never allocated is counted nowhere: run
Octane and count the property maps. Both runs on this machine, 13 suites (Mandreel skipped, as
2-7's own run of record did), **10 runs per benchmark on each side**, the second on a pristine
build at `a6f101cc`:

| | Before | After | Ratio |
|---|--:|--:|--:|
| **Property maps allocated** | 16 246 854 | 2 501 706 | **0.154** |
| Node-group allocations | 36 634 448 | 6 371 061 | 0.174 |
| Nodes copied by resizes | 110 622 968 | 14 175 868 | **0.128** |
| **Live map bytes** (shipped policy) | 9.47 GB | 1.39 GB | **0.147** |
| Allocated map bytes | 16.75 GB | 2.24 GB | 0.134 |

**Six in seven property maps are never built at all.** The per-suite spread is what makes it a
finding rather than an average — Splay **591 324 → 60**, EarleyBoyer 0.036x, Typescript 0.100x,
DeltaBlue 0.144x, PdfJS 0.173x, while RayTrace (0.500x) and Box2D (0.510x) keep half of theirs
because they materialize more. Nothing regressed; the worst suite still halves.

**The before-run is also a check on the harness, and it passes.** It reproduces 2-7's recorded
16.2 M-map sample to four digits — one-group share **0.4386** against the 43.86% §2-7 records —
so the two sides are being measured the same way that run was. Afterwards the surviving maps sit
at a one-group share of 0.114 and **98.97% within four groups**: what still materializes is
overwhelmingly small, which is consistent with the survivors being objects that took a
descriptor path rather than objects with many properties.

*This is the measurement 2-3 was closed on, pointed at 2-3's successor, and it is the first
real-workload number this item has* — the byte table above is 50 000 objects in a loop; this is
fifteen large real programs.

**How it works, in one paragraph.** An object starts *shape-only*: the shape holds key-to-slot,
`shapeSlots` holds the values, a parallel `JSPropertyAttributes[]` holds the attributes, and the
radix trie is never written. Anything that needs a real descriptor — an accessor, a deferred
cell, a `delete`, a private name, or the mutable `ref PropertySequence` that `GetOwnProperties()`
hands to another assembly — calls `MaterializeNamedProperties()`, which replays the shape's keys
in slot order into the trie and sets a status bit for good. **After that the object behaves
exactly as it did before this item existed, so the worst case is the old behaviour** — which is
what made it safe to convert the paths one at a time. Slot order is insertion order
(`ObjectShape.Add` assigns `slots[key] = slots.Count`), so the rebuilt chain is the one the eager
path would have built and `OrdinaryOwnPropertyKeys` reports the order it always did.

The rule that makes a shape-only object answerable without a descriptor: **every key in its shape
has a non-null slot and a plain data attribute set.** Everything that would violate it
materializes *before* it is recorded — which is why `TrackShapeKeyWithoutSlotValue`, 2-8's
null-slot mechanism for the Annex B `caller`/`arguments` cells, now materializes first.

**Where the trie writes were removed.** Six paths, and they are the ones that create or overwrite
a property: `FastAddValue`, `DefineReceiverDataProperty` (both overloads), the `[[Set]]`
overwrite fast path, `TryCreateShapeSlot` (2-1's transition entry), `TryWriteShapeSlot` (the store
cache's overwrite) and `CopyDataProperties`. Five read paths answer from the shape rather than
materializing, because materializing on a read would hand the trie straight back to every object
that is ever read without a warm cache: `GetValue`, `GetInternalProperty`, `GetOwnProperty`,
`HasOwnProperty`/`TryGetOrdinaryOwnProperty` and `GetMethod`. Everything else materializes, on
purpose.

**Verify — the boundary, not the hit rates.** 25 test cases in `ShapeOnlyPropertyStorageTests`,
and what they pin is what a shape cannot carry. Order: insertion order through a rebuild for five
construction forms, order continuing rather than restarting for properties added *after*
materialization, and a deleted-then-recreated property still moving to the tail. Attributes: each
of `writable`/`enumerable`/`configurable` set to a non-default while shape-only and read back
through a descriptor query, plus **two objects at the same shape keeping different flags** —
the reason the parallel array exists, stated as a test. Refusals through a *warmed* store site:
a frozen receiver, and a property made non-writable after 300 cached writes. The descriptor kinds
a slot cannot hold: an accessor redefined mid-loop taking over both directions, a function's
Annex B `caller` keeping the non-writable/non-enumerable/non-configurable **data** descriptor P0-3
preserved, and a private field staying out of the own keys. And the boundary as other assemblies
cross it: `Object.assign`, spread, a Proxy over a shape-only target, `for`-in over a chain, and
40 properties across every growth step with **every value and every position checked**, because a
resize that copied the slots and not the attributes would surface as a wrong flag rather than a
crash. Repository suite: **7 401 tests across 13 projects, 3 failures**, all three the
pre-existing win-x64 host-environment ones §4.1 names.

**test262 and Octane were run as part of the item, which is what 2-8 established they have to
be.** All four pinned manifests are unchanged — 8 220 passed, 84 failed, 9 timed out, identical
counts manifest by manifest (§0). Octane: **14 of 15 suites `ok`, DeltaBlue included**, which is
the specific check 2-8 skipped and paid for.

**The fifteenth is Mandreel, and it is not this item — confirmed against a control rather than
assumed.** It fails in phase `Setup` with `RangeError: Maximum call stack size exceeded` at
`EnsureWithinStackBudget` (`CallFrames.cs:215`) from `mandreelAppInit` (`mandreel.js:1460`),
which is the win-x64 signature phase 0 recorded and item 1-2 diagnosed. Re-run on a **pristine
build at `a6f101cc` with 2-9 absent**, on the same machine and the same harness, it fails
**identically** — same guard, same frame, same phase, same eleven-frame stack. So the one
non-`ok` suite is pre-existing at the pinned pointer on this platform, exactly as 1-2 says, and
1-2's note that it does not reproduce on linux-x64 is why 0-6 will not see it. *A failing suite
is a claim; the control is what turns it into a verdict, and it cost one 387 s run.*

### 2-7 · The property map's 16-node floor costs ~1 KB per object — **landed**

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

Instrumentation landed as `55c6b1fb`.

#### The distribution, and what it decided — **landed**

14 Octane suites, 30 runs per benchmark, **47 482 058 property maps** (Mandreel set aside for run
length, not for cause):

| A map's life ends at | Share |
|---|--:|
| **1 group** — 4 nodes needed, 16 reserved | **43.9%** |
| 2 groups | 38.1% |
| ≤ 4 groups — *inside the old floor* | **87.3%** |

**The reservation was almost never used.** Per-suite spread is wide and worth keeping: EarleyBoyer
96.4% at one group, Splay 48.6%, PdfJS 47.2%, Typescript 24.8%, RayTrace 4.9%, RegExp 0.1%. The
aggregate is dominated by Typescript, PdfJS and EarleyBoyer, which supply 41 M of the 47 M maps.

**Converged, checked rather than assumed.** Tripling the sample (16.2 M → 47.5 M maps) moves the
one-group share from 43.86% to 43.87% and the within-floor share from 87.66% to 87.34%. Only suites
with a few hundred maps move at all.

| Policy | Live bytes | vs current | Allocated | vs current |
|---|--:|--:|--:|--:|
| `round-up-16` — as written | 17.0 GB | 1.000 | 20.7 GB | 1.000 |
| `round-up-8` | 13.8 GB | 0.813 | 21.8 GB | 1.053 |
| `round-up-4` | 12.5 GB | 0.733 | 22.4 GB | 1.087 |
| **`min-4-then-double`** | **9.5 GB** | **0.560** | **16.8 GB** | **0.815** |

**This overturns the table above it, and the reasoning that produced it.** That table predicted
`min-4-then-double` would be the *worst* option for eight fields (+43%) and read the 16-node floor as
"buying amortized growth for medium objects". It is not: `((max / N) + 1) * N` only applies while
`last * 2 <= max`, so past the first block the old rule grew by a **fixed increment — linearly** —
and paid more copies than doubling, not fewer. The floor was buying nothing for medium objects; it
was overcharging small ones. Against the real distribution the smaller floor wins on **both** axes.

**The model was validated against reality, not trusted.** Changing the floor for real and re-running
`--object-alloc`: `ctor-1` goes **1 256.5 → 584.4 B**, a 672.1 B saving against a predicted
920 → 248 = 672 B, with the 120 B of non-map overhead identical in both builds. The per-shape trade
the item predicted is confirmed exactly: 1 field **−53%**, 3 fields **−16%**, 8 fields **+27%**.

**Wall clock, four interleaved rounds** (first discarded as warm-up — one `min4` round ran 4× long):

| Workload | Ratio |
|---|--:|
| 3-field object literal, 3 M | **0.729** |
| 3-field constructor, 3 M | **0.800** |
| 1-field constructor, 3 M | **0.847** |
| hot property read, 20 M (control) | 1.013 |
| local arithmetic, 20 M (control) | 1.007 |
| **8-field constructor, 1.5 M** | **1.193** |

**And on the real suites, which is what settles the tail.** The four suites that build the most
maps, two interleaved rounds each, whole-process: Typescript **0.916**, Box2D **0.937**, PdfJS
1.013, EarleyBoyer 1.020. **Typescript has by far the worst tail — a third of its maps outgrow the
old floor — and it is the suite that gains most.** That is the geometric-growth half paying for the
smaller-floor half. Nothing among them regresses worth the name. The Octane *correctness* smoke was
re-run on this build too, not only the unit suite — the lesson 2-8 paid for — and returns **exactly
the set it returned before the change**: 17 of 18 benchmarks pass, Mandreel included once given a
real budget, with RegExp's pre-existing checksum failure the only one out.

So the trade is real, its losing side is real, and it is worth taking: an 8-field object pays ~27%
more bytes and ~19% more time, against 43.9% of all maps costing 248 B instead of 920.

**Verify.** Five test cases in `StorageTests`: the first allocation reserving exactly what was asked
(was 16), a sub-group request still getting a whole group, growth being geometric rather than a fixed
increment, every slot's contents surviving 50 growths — the policy is only safe because a resize
copies — and `SAUint32Map` keeping all 2 000 entries across the many resizes the new policy forces. Suite:
**7 397 tests across 13 projects, 0 failures.**

Landed as `a6f101cc`.

#### `--object-alloc`, and why the corpus grew again

`ObjectAllocationMetrics` in `Broiler.JS/benchmarks/Broiler.JavaScript.Engine.Benchmarks`
emits the table above as JSON, by the Appendix A method — forced gen2 collection, then
`GC.GetAllocatedBytesForCurrentThread()` deltas over 50 000 objects, warmed first so
compilation, key strings, shapes and cache entries land outside the measured run. It joins
`--cache-metrics` (hit rates) and `--sparse-metrics` as a standing emitter for a quantity no
wall-clock benchmark reports, and it exists because 2-3 could not be decided without it: the
item's *only* surviving justification was memory, and there was no way to measure memory per
object from a clean checkout. Both 2-3's re-sizing and 2-7 came out of its first run.

**Sequence.** 2-0 ✅ → 2-1 ✅ → 2-2 ✅ → 2-4 ✅ (both halves) → 2-8 ✅ → 2-7 ✅ → **2-9 ✅**;
**2-5 closed** and **2-6 folded into 4-1**, both on measurements. **Every item in this phase is now
landed or closed.** **2-3 is closed** — re-measured against the shipped 2-7 build, its own proposal
is worth 1.0-4.3% of an object's per-property bytes while the radix trie it does not target is
67-94%; that became **2-9**, which has since landed and taken it. The phase's own exit gate —
test262 `properties-proxy` and `strict-mode`, covering 2-1, 2-2, 2-4, 2-8 and now 2-9 — is
**satisfied**; 0-6's CI Octane run is what still stands between "landed" and "closed".

**Verify — per item, not per phase.**

- An `ownership.json` entry naming its benchmark and semantic owner. The file is
  item-scoped and carries 37 entries; match that granularity.
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
### 2-10 · DeltaBlue's dictionary fallbacks — **found, fixed, and it is not the explanation**

> **Delivered as a patch, not in the pin**, for the same 403 as the rest:
> [`patches/0062`](../patches/0062-js-array-length-keeps-shape.patch). It applies cleanly on its
> own but **will not compile without [`patches/0061`](../patches/0061-js-measure-2-9-materialization-cause.patch)**,
> whose counter the new emitter reads — a textual apply-check is not enough, build after applying.

Phase 2's exit criterion split (Richards 183× passes, DeltaBlue 576× fails), and every phase 2
item was sized on a probe rather than on the suite. §3.5 already records what that costs: 2-8 was
justified by DeltaBlue's score, measured with a loop *shaped like* DeltaBlue, and broke DeltaBlue
outright. So the first move was to run the suite itself.

**`--suite-cache-metrics` (new; `SuiteCacheMetrics`) runs the real Octane suites of §0's phase 2
cluster under the inline-cache counters.** Richards is the control — same phase, same items, and
it *passes* — so a counter that separates the two is a lead:

| | Richards (183×, passes) | **DeltaBlue (576×, fails)** | Box2D (144×) |
|---|--:|--:|--:|
| read cache hit rate | 86.61% | **65.96%** | 96.39% |
| store cache hit rate | 99.74% | 80.65% | 92.57% |
| **dictionary fallbacks** | **1** | **2 503** | 9 |
| prototype invalidations | 37 | 2 519 | 1 944 |
| materializations | 82 | 2 638 | 70 264 |

**Three orders of magnitude on one counter.** A dictionary fallback is permanent — the object
drops its shape and no inline cache can reach its named properties again — so 2 503 of them is
not a tuning difference.

**Traced, and the cause is `push`.** 2 507 of DeltaBlue's 2 512 array fallbacks come from
`JSArray.SetLengthWritable`, which reaches the property store through `GetOwnProperties()`, and
that hands out a mutable trie ref by *abandoning the shape*. Isolated one operation at a time on
a fresh process, counting fallbacks against an empty-script baseline:

| Operation | Fallbacks (before) | (after) |
|---|--:|--:|
| `a[i] = i`, array literal, `new Array(n)`, `a.slice()` | 0 | 0 |
| **`a.push(i)`** | **1** | **0** |
| **`a.pop()`** | **1** | **0** |
| **`a.concat([3])`** | **1** | **0** |
| `a.length = 2` | 1 | 0 |
| `Object.defineProperty(a,'length',{writable:false})` | 1 | **1** |
| `Object.freeze(a)` | 1 | **1** |

One per array, not one per call — the first drops the shape and the rest find it already gone.
**`push` is the most common array operation in the language**, and DeltaBlue's
`OrderedCollection.add` is `this.elms.push(elm)`.

**The fix is that a writable `length` needs no descriptor at all.** `IsLengthReadOnly` reads an
*absent* entry as writable — the default — and the stored value is never read back, because
`GetOwnPropertyDescriptor` builds it from `_length`. So the entry was pure write-only
bookkeeping, and writing it cost the array its shape. `SetLengthWritable` now writes only when
the length is non-writable or an entry already exists; the last two rows above show `freeze` and
an explicit non-writable `length` still recording one, which is what keeps `IsLengthReadOnly`
answerable.

**It also closes a hole in this class's own stated invariant.** `JSArray.SupportsShapeTracking`
documents that it is *"earned by not writing any named property of its own with a bare
`ownProperties.Put`"* — and `length` was the one place that did.

**What it is worth, stated honestly: the defect is real and the metric it was found by did not
move.**

- **Dictionary fallbacks: DeltaBlue 2 503 → 0**, Box2D 9 → 4, Richards 1 → 0.
- **A named property on an array that grows now keeps hitting its cache.**
  `GrowingAnArrayThroughABuiltInKeepsItsNamedShape` — which until now asserted the opposite, and
  whose own comment called the fallback *"the part worth pinning, because it bounds what item 2-2
  buys"* — now pins ≥499 hits across 500 reads after five pushes. That bound is gone.
- **DeltaBlue's read hit rate did not change by a single hundredth: 65.96% before, 65.96%
  after.** Nor did its prototype invalidations or materializations.

- **And the score does not move either.** Re-run at five repetitions per engine, the same way the
  gate was measured: **DeltaBlue 116 → 122 broiler-side, 576× → 581×**, against its own 16.4%
  band — noise, in both directions. Richards is 183× → 179×. Recorded because §3.5's rule from
  2-8 is that a change justified by a benchmark has to be *run* against that benchmark, and this
  one was: it neither helps nor harms it.

**So this is not the explanation for 576×.** DeltaBlue does not put named properties on its
arrays, so the shapes it was losing were shapes it never read through. The counter that separated
the two suites most sharply turned out to separate them for a reason unrelated to the gap being
investigated — which is worth stating plainly, because the fix looked like the answer right up
until it was measured.

**Verify.** Repository suite **7 563 tests across 13 projects, 0 failures**. **test262 unchanged
across all four pinned manifests — 8 313 executed, 8 220 passed, 84 failed, 44 skipped, 9 timed
out, identical manifest by manifest**, `test262-arrays` among them, which is the manifest that
covers this change most directly. Octane still runs 15 of 15 suites `ok`.

**The live lead is the read hit rate itself: 65.96% against Richards's 86.61%**, one in three
reads missing on a suite whose whole shape is polymorphic constraint objects. That is where 2-10's
successor should start, and it should start by decomposing *which sites* miss rather than by
assuming, which is the mistake this item just made and caught.

#### Decomposing the misses: not megamorphism, and a **`class`-shaped 2-0 regression found on the way**

Two things are already ruled out, and one new defect fell out.

**It is not megamorphism.** DeltaBlue records **0 megamorphic read sites** — every read site stays
within the four-entry polymorphic budget. Whatever misses, misses while the site still has room.

**The counter that tracks the gap is prototype invalidation: 2 519 for DeltaBlue against
Richards's 37**, and each one retires *every* prototype-keyed entry in the process — the guard is
deliberately coarse ("one prototype mutation anywhere"). DeltaBlue's reads are overwhelmingly
inherited-method lookups, which are exactly the entries that retires.

**Tagging the two publish sites splits them 4 543 / 115** across the cluster: almost everything
comes from `NotifyPrototypeChainMutation` (a real `[[SetPrototypeOf]]`, or a mutation on an object
already used as a prototype), not from `MarkUsedAsPrototype` (which is correctly guarded and fires
once per object).

**And isolating that by construct found a live defect — in `class`, not in DeltaBlue.** Counting
invalidations against an empty-script baseline, per *n* allocations:

| Construct | n = 100 | n = 500 | n = 2 000 |
|---|--:|--:|--:|
| `function F(){…}; new F()` | **1** | **1** | **1** |
| **`class C{…}; new C()`** | **102** | **502** | **2 002** |
| object literal | 0 | 0 | 0 |
| DeltaBlue's `inheritsFrom` + `new` | 2 | 2 | — |

**Dead linear at one per allocation**, and it is *precisely* item 2-0's signature — 2-0 recorded
"200 001 invalidations per 200 000 allocations → 3". Traced, a class instantiation reaches
`JSValue.SetPrototypeOf` → `set_BasePrototypeObject`, where `prototypeChain` is already non-null,
so the write reads as a `[[SetPrototypeOf]]` on a live object and publishes. That is the same
second write `JSFunction.CreateInstance` documents having removed: *"Installed by the constructor
rather than by an initializer that overwrites what the constructor just set. … the second write
looked like a `[[SetPrototypeOf]]` on a live object … Once per `new`."` **2-0 fixed the function
path and the class path still does it.**

**It does not explain DeltaBlue, and that is the second time in this item.** Octane's DeltaBlue is
ES5 — its only occurrences of the word "class" are in comments — so it never constructs one. The
defect is real, dead-linear, and reaches every `class` in modern JavaScript; it is simply not this
suite's problem. Recorded as its own item rather than folded into 2-10, and **not fixed here**:
the fix is the constructor-installs-the-prototype change 2-0 already made once, but this is the
code whose last two regressions (2-0's own, and 2-8's DeltaBlue break) both came from this area,
and it wants the Octane cluster run against it — which is now possible.

#### 2-11 · The redundant prototype write — **landed, and it is the largest cache win since 2-0**

> **Delivered as a patch, not in the pin**:
> [`patches/0063`](../patches/0063-js-prototype-rewrite-no-invalidate.patch). Independent of
> `0059`–`0062` — one file, one condition.

The class path was tracked to `JSClass.CreateInstance`, and the two obvious sites were already
correct: the instance is built with `new JSObject(instancePrototype)`, carrying 2-0's own comment.
What publishes is the **re-apply afterwards** — `@this.BasePrototypeObject = instancePrototype`,
writing the prototype the constructor had *already installed*. `prototypeChain` is non-null by
then, so the setter reads it as a `[[SetPrototypeOf]]` on a live object.

**The fix is to notice that the chain did not change.** Every assumption the prototype version
guards is about *which chain this object has*; after a redundant write it has the same one, so
nothing cached is stale. The setter now compares the resulting chain with the previous one and
publishes only on a real change — which fixes the class path, the derived-class path and any
other redundant assignment at once, rather than patching call sites one at a time.

| Construct, per *n* allocations | Before | After |
|---|--:|--:|
| `class C{…}; new C()`, n = 2 000 | 2 002 | **0** |
| `function F(){…}; new F()`, n = 2 000 | 1 | **0** |
| `class B extends A{…}; new B()`, any n | — | **2**, flat |

**On the real suites the effect is much larger than the class case suggested**, because the
retirement was process-wide — one redundant write anywhere retired every prototype-keyed entry
everywhere. These are exact counts, not timings:

| | Prototype invalidations | Read cache hit rate | Store hit rate |
|---|--:|--:|--:|
| **Richards** | 37 → **10** | 86.61% → **99.97%** | 99.74% → 99.75% |
| **DeltaBlue** | 2 519 → **16** | 65.96% → **69.45%** | 80.65% → **83.92%** |
| **Box2D** | 1 944 → **107** | 96.39% → **97.72%** | 92.57% → 92.98% |

**Richards's read cache goes from missing one read in seven to missing one in three thousand.**
That is the phase 2 machinery finally doing on a real suite what its probes always said it did,
and it had been masked since the phase began by an invalidation storm none of the probes
allocated their way into.

> **The scores moved the right way and are *not* claimed.** Five repetitions per engine:
> Richards 143 → 168 (178.7× → 155.4×) and DeltaBlue 122 → 125 (581× → 516×). Richards's **+17%
> sits inside its own 15.5% band**, so a five-run median cannot separate it from noise — §3.2.
> What is claimed is the hit-rate and invalidation columns, which are deterministic counts.

**DeltaBlue still fails phase 2's exit criterion**, at 516× against the 200× gate, and its read
hit rate is still 69% against Richards's 99.97%. So the gap narrowed and did not close, and the
suite remains the phase's open item.

**Verify.** Repository suite **7 563 tests across 13 projects, 0 failures**. **test262 unchanged
across all four pinned manifests — 8 313 / 8 220 / 84 / 44 / 9, identical manifest by manifest**;
`test262-arrays` matters doubly here because the same version gates `JSArray`'s dense-element fast
path, and `properties-proxy` and `realm-isolation` are where a wrongly-skipped invalidation would
surface. Octane still runs 15 of 15 suites `ok`.

#### 2-12 · The stale cache entry that could never be replaced — **DeltaBlue 69% → 93%**

> **Delivered as a patch, not in the pin**:
> [`patches/0064`](../patches/0064-js-refresh-stale-cache-entry.patch). Needs `0061` (the counter
> infrastructure) and `0062` (the emitter) to compile.

2-10 owed a per-site attribution, and this is it. The bare miss counter cannot say *why* a read
missed, so the lookup's exits are now counted separately:

| | Richards (99.97%) | **DeltaBlue (69.45%)** | Box2D (97.72%) |
|---|--:|--:|--:|
| total read misses | 208 | 306 004 | 592 263 |
| cold — first touch of a site | 84.1% | **0.1%** | 0.9% |
| **shape — site had room, receiver's shape not among its entries** | 15.9% | **99.9%** | 99.1% |
| megamorphic / key mismatch / non-object | 0 | **0** | ~0 |

**Effectively every DeltaBlue miss is the same exit**, and it is the one that should be
self-correcting: a site with room that meets a new shape is supposed to *add* it and hit from
then on. Splitting that exit further found why it does not:

| | Richards | **DeltaBlue** | Box2D |
|---|--:|--:|--:|
| entry could not be described at all | 11 | 67 874 | 297 786 |
| **entry ALREADY PRESENT — declined, not refreshed** | 28 | **237 738 (77.7% of all misses)** | 288 980 |

**The add path deduplicates on `ShapeId` and `Holder`. A hit checks four more guards** — the
prototype version, the receiver's prototype identity, and the holder's shape and slot. Any of
those can go stale while the two dedup keys stay equal, and when they do the read misses, reaches
the add path, finds an entry it considers "already present", and **returns without replacing it**.
The entry can never be re-described, so that site misses on that receiver **for the rest of the
process**.

**The fix is one line: refresh in place instead of declining.** `entryToAdd` was just built from
the live receiver, so it is by construction the correct replacement.

| | Read hit rate | Read misses |
|---|--:|--:|
| **DeltaBlue** | 69.45% → **93.16%** | 306 004 → **68 534** |
| **Box2D** | 97.72% → **98.83%** | 592 263 → **303 612** |
| Richards | 99.97% → 99.97% | 208 → 192 |

**Taken with 2-11, DeltaBlue's read cache goes 65.96% → 93.16%** and its misses fall by 78%.
Richards was already at the ceiling and stays there, which is the control working: it had almost
no stale entries to refresh.

> **Scores moved and are still not claimed.** Five repetitions per engine: DeltaBlue **125 → 145**
> (516× → **447×**) and Richards 168 → 170 (155× → 150×). DeltaBlue's +16% against a 13.8% band is
> at the edge, not clear of it. Across this session the suite has gone 116 → 145 and 576× → 447×,
> which is the direction the deterministic counters predict — but §3.2's rule stands, and what is
> claimed here is the hit-rate column.

**DeltaBlue still fails phase 2's exit criterion**, at 447× against 200×. The cache is no longer
the reason: at 93% it is closer to Box2D (98.8%, and 144×) than to its own former self, yet its
ratio is still three times Box2D's. **Whatever is left is not property-cache-shaped**, and that is
a genuinely new state for this item — three explanations eliminated, two defects fixed, and the
remaining gap now has to be looked for somewhere other than phase 2's subject.

**Verify.** Repository suite **7 627 tests across 13 projects, 0 failures**. **test262 unchanged
across all four pinned manifests — 8 313 / 8 220 / 84 / 44 / 9, identical manifest by manifest.**
The change cannot return a wrong value: a refreshed entry is still checked by every guard on the
next read, and the entry it replaces was one the guards had just rejected.

---


## Phase 3 — value representation

**Targets: Crypto (301×), zlib (340×), RayTrace (291×), EarleyBoyer (270×), Splay
(152×), NavierStokes (104×).** Blocker **B1** — the largest total win in the plan and
the largest change. Deliberately after phases 1 and 2 because those are contained and
this is not.

Owner assemblies: `Broiler.JavaScript.Storage`, `.Runtime`, `.Compiler`.

> **The order changed on a measurement.** 3-1 was written as the phase's opener because it looked
> like the most contained item covering the most benchmarks. Measured before starting (below), it
> is a 1:1 trade of write allocation for read allocation whose unambiguous half is live memory —
> and the same probe found a larger, strictly-cheaper target it was standing in front of: **an
> indexed access boxes its index**, ~32 B on every array read and write in the engine, with no
> read-side cost to removing it. That became **3-0**, and it goes first.

### 3-1 · Unboxed backing stores for dense arrays — **measured; re-specified, and no longer first**

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

#### Measured before starting — and it re-specifies the item

The item's premise is a claim about the **write** side, and it is true. What it does not say
is what the change costs on the **read** side, which is the half that decides it: the dense
store is `IPropertyValue[]` and every read hands back an `IPropertyValue`, so a raw `double[]`
cannot answer one without boxing a fresh `JSNumber`. 3-1 therefore *trades* a write allocation
for a read allocation, and the exchange rate had to be measured before anything was built.

`--element-alloc` (new; `ElementAllocationMetrics`), 100 000 elements, warmed then measured
after a forced gen2 collection, every row net of an inert no-array loop control:

| Site | Write B/element | Read B/element |
|---|--:|--:|
| loop control, no array access | 0.00 | 0.00 |
| `a[0] = t` — constant index, hoisted reference | **0.00** | **0.00** |
| `a[0] = i + 0.5` — constant index, fresh number | **32.00** | 0.00 |
| `a[i] = t` — variable index, hoisted reference | 52.65 | 31.67 |
| `a[i] = i + 0.5` — variable index, fresh number | **84.69** | **31.67** |
| `a[i] = i & 1023` — small integers | 84.32 | 31.67 |

**The 84.69 decomposes exactly, and only one third of it is what 3-1 removes:**

| Component | B/element | How the rows show it |
|---|--:|---|
| Boxing the **index** | ~32 | a constant index costs **0.00**; a variable one costs 32 more |
| Boxing the **value** | **32.00** | the constant-index-number row *is* this number, alone |
| Amortized backing growth | ~21 | 100 000 slots doubling from four ≈ 21 B/element |

**Three findings, and two of them change the plan.**

- **3-1's prize is 32 of 85 bytes on write, and it costs 32 bytes on every read.** Reads are
  free today — the value is already a heap object, so a read is a reference copy — and after a
  typed store each one boxes. On allocation the item is a **wash at a 1:1 read/write ratio, a
  win only when writes dominate, and a loss on read-heavy code.** Its named targets —
  NavierStokes' grids, Crypto's digit arrays — read each element many times per write, which
  is the unfavourable direction. **What survives unambiguously is live memory**: a resident
  `double[1e6]` is 8 MB against 8 MB of references plus 32 MB of `JSNumber`, so ~0.2x, and that
  is a real win for exactly those long-lived numeric heaps. **Re-specify 3-1 as a live-memory
  item whose throughput case is contingent on 3-4**, rather than as the phase's throughput
  opener.
- **The bigger contained win on array access is not the element store at all — it is that a
  variable index is boxed.** It costs ~32 B on every indexed read and every indexed write,
  whatever the array holds, and unlike a typed backing store removing it has **no read-side
  penalty**: it is pure removal, and it applies to reference arrays too. On the read path it is
  *the entire cost*. That deserves its own item ahead of 3-1.
- **The per-thread small-integer cache does not reach this path.** `a[i] = i & 1023` costs
  84.32 against 84.65 for large integers — a 0.33 B difference, i.e. none. So 3-1 buys the same
  32 B for small integers as for doubles, and P2-1's cache is not already collecting it here.
  Worth knowing before anyone sizes the integer case as already-solved.

*Neither the read-side cost nor the index boxing was visible from the item's text, and both
came out of one probe run. §3.5's rule about a premise not being a finding, applied to an item
that was right about its premise and wrong about its consequence.*

### 3-0 · Stop boxing the index of an indexed access — **landed, both halves**

**Measured, not proposed.** `a[0] = t` allocates **0.00 B/element** and `a[i] = t` allocates
**52.65**; the read side is **0.00** against **31.67**. The array, the element and the value are
identical across each pair — only the index expression differs — so ~32 B per access is the
index, and on the read path it is *the whole cost*. It is charged to every indexed access the
engine performs, on reference arrays as much as numeric ones, which is why it is worth more than
3-1 and costs less to take.

**Why it happens — confirmed at the source, and the code already said so.**
`FastFunctionScope` builds a numeric local's readable expression as `JSNumberBuilder.New(pe)`
over its raw `double` storage, under a comment that reads *"A numeric local's readable Expression
BOXES its storage, so every consumer that expects a JSValue keeps working"*. An index expression
was one of those consumers, so `a[i]` allocated a `JSNumber` purely to name a slot. The literal
form never did: `a[0]` lowers to a constant `uint` key, which is exactly why it measured at
0.00 B.

**What landed.** `JSValue.GetElementByNumber(double)` reads the element straight from the raw
double and `SetElementByNumber(double, JSValue)` writes it, emitted by `VisitMemberExpression`
and by a new `TryCreateNumericIndexStore` lowering for a computed key that resolves to a numeric
local. `--element-alloc`'s constant-index rows were already the floor both had to reach, and both
reach it:

| Site | Read before | Read after | Write before | Write after |
|---|--:|--:|--:|--:|
| `a[i] = i + 0.5` — numeric | 31.67 | **0.00** | 84.69 | **52.98** |
| `a[i] = t` — reference | 31.67 | **0.00** | 52.65 | **20.98** |
| `a[0] = t` — constant index (floor) | 0.00 | 0.00 | 0.00 | 0.00 |

**Indexed reads now allocate nothing at all**, and a write loses ~32 B. A write-once-read-once
numeric element goes **116.36 → 52.98 B (0.46x)** and a reference one **84.32 → 20.98 (0.25x)**.
What is left is exactly the two things 3-0 is not about: the value's own `JSNumber` (32 B, which
is 3-1's territory) and the amortized backing growth (~21 B). It applies to **reference arrays as
much as numeric ones**, which is the half a typed backing store could never have served.

**The guard is the item.** Only a non-negative integral double at most **2^32-2** names an array
index; everything else is an ordinary string-keyed property, so `a[1.5]` is the property `"1.5"`,
`a[-1]` is `"-1"`, and `a[4294967295]` is a string key — 2^32-1 being the one canonical numeric
string above the range. `-0` is deliberately admitted, because `ToString(-0)` is `"0"` and slot 0
is the right answer; NaN fails the lower comparison and every infinity fails the upper. **Each
rejection falls back to exactly the boxed path that ran before, so a guard that is too strict
costs an allocation and never a wrong answer** — which is what bounds the risk of the whole item.

**A guarded access is a CALL, and that is what shapes the item.** Three places need an index
*node* and therefore cannot have one: `CreateMemberAssignmentTarget` is assigned through,
`InternalUpdateExpression` switches on `right.NodeType` and takes a different branch for anything
that is not `BExpressionType.Index`, and a **compound** assignment reads and writes through a
single reference. So the fast path is offered to exactly two lowerings — the plain read and the
plain write — and `a[i] += v` keeps its boxed index; splitting it would evaluate the base twice
unless the whole form were rebuilt around a temp, which is more than this item. A test pins that
the base is still evaluated once.

*The first attempt hooked `CreateMemberExpression`, which is on the assignable path.* It compiles
cleanly either way — an expression tree only rejects the assignment later — so the callers had to
be read rather than the build trusted. Two of the three pass `computed: false` and so could not
have reached it, **but destructuring passes `property.Computed` straight through and would have.**

**The write path goes through `SetValue`, not the `uint` indexer, and that is deliberate.**
Measuring first turned up a pre-existing split in the error messages: `null[0] = 1` reports
*"Cannot get property 0 of null"* through `JSUndefined`'s `this[uint]` override, while
`null[i] = 1` reports *"Cannot set properties of null"* through the `JSValue` setter — because a
constant index has always lowered to a `uint` key and a variable one never did. Routing integer
indices to the `uint` indexer would have silently moved every variable index onto the other
message. Copying the `JSValue` setter's failure handling keeps both exactly where they were; a
test pins them, so reconciling that split later has to be a deliberate act rather than a side
effect.

**Verify.** 42 test cases in `NumericIndexKeyTests`, weighted to the keys the fast path must
*refuse*. Reads: the nine index values above, a hole still reaching the prototype chain, an index
accessor still running, string and typed-array receivers, a Proxy still seeing the key as a string
through its `get` trap, `'0'` and `'00'` staying distinct properties, and an optional-chain read
still taking the excluded path. Writes: each of the eight keys **read back through its string
form** rather than through the numeric local, so the two halves cannot agree on a shared bug; an
index setter running; a frozen array refusing silently in sloppy mode and throwing in strict; a
Proxy `set` trap seeing a string key; typed-array writes discarding an out-of-range index rather
than landing elsewhere; the null and undefined messages above; the assignment evaluating to its
right-hand side; and §13.15.2 ordering, proven with a right-hand side that reassigns the index
mid-assignment. Repository suite: **7 463 tests across 13 projects, 3 failures**, all three the
pre-existing win-x64 host-environment ones. **test262 over all four pinned manifests is unchanged
— 8 220 / 84 / 9, no test on a different side than before the item.**

**Size: M**, and it landed at that size.

### 3-2 · Unboxed doubles in shape slots

The object-field twin of 3-1: `shapeSlots` holds `JSValue` references, so
`vector.x = 1.5` allocates. This is what RayTrace and Box2D need, and it **composes
with 2-1** — a shape that knows a slot is a double can store it raw, so land 2-1 first
and this gets cheaper.

**Where.** `Runtime/JSObject.cs`, `Runtime/ObjectShape.cs`. **Size: L.**

### 3-3 · Widen the unboxed-locals eligibility gate — **measured; the parameter half landed, and it is not the half the item described**

P2-2 item 3 covers a function-top-level `var` not named by any nested closure. Still
ineligible when this item was written: **function parameters**, `let`/`const` (needs TDZ
analysis), and `var` declared inside a block or loop body (needs definite-assignment
analysis).

The item then asserted an ordering: *"**Parameters are the valuable one** — every numeric
helper takes them, and every Octane benchmark is full of numeric helpers. Do parameters
first; treat the other two as separate items."* That is a claim about where the bytes are,
and it had never been measured. Measured now, it is **right about the target and wrong
about the tier**, which changes both what landed and what comes next.

**Where.** `Broiler.JavaScript.Compiler` — `Declarations/FastCompiler.CreateFunction.cs`
(the parameter-binding site and `TryPlanScalarReplacement`), `Scope/FastFunctionScope.cs`.

#### Measured before starting — `--local-alloc`, and it re-specifies the item

`LocalAllocationMetrics` (new; `--local-alloc`) reports bytes per iteration for every place
a number can live in a function, alongside the compiler's own count of how many bindings it
kept scalar. Two instruments on purpose: the counter is exact and settles *whether a shape is
eligible at all*, the bytes settle *what that eligibility is worth*. Every row is net of a
loop control carrying no value under test.

| Site | B/iteration | Numeric locals |
|---|--:|--:|
| `loop-control` | 0.00 | 2 |
| **`top-level-var`** — the only eligible category today | **0.00** | **3** |
| `parameter` | 31.98 | 1 |
| `let-binding` | 31.98 | 1 |
| `const-binding` | 31.98 | 1 |
| `block-var` | 31.98 | 1 |

**Three findings, and two of them change the plan.**

- **All four ineligible categories cost exactly the same.** A `let`, a `const` and a
  block-scoped `var` each cost what a parameter costs, to the byte. So "parameters are the
  valuable one" is not a statement about cost per site, and nothing in the item ever
  established it — the four were ranked by how they were written down, not by what they
  charge.
- **An ineligible binding does not merely fail to help; it de-optimizes the locals
  downstream of it.** The eligible row keeps **3** locals in raw doubles and every
  ineligible row keeps **1**. The accumulator `s` in `s = s + v * 2` stops being provably
  numeric the moment `v` is not, so one ineligible binding costs the specialization of
  everything that reads it. That is a multiplier the item did not have, and it is the
  strongest argument for finishing the gate.
- **The parameter gap is not a box. It is a cell** — and that is the finding the item's own
  title hid. Every parameter was created with `CreateVariable(name, null, …)`, whose default
  type is `JSVariable`, so **every parameter allocated a heap cell on every call**, while a
  `var` in the same function had been scalar-replaced since P2-2. It is not the numeric tier
  of the gate that parameters were missing, it is the *scalar* one.

| Helper called in a loop | B/call | What the pair isolates |
|---|--:|---|
| `h(a) { return a * 2 + 1; }` | 119.99 | binds and reads a parameter |
| `h(a) { return a; }` | 120.01 | **the same cost with no arithmetic at all** — so the cost is the binding |
| `h(a) { var b = 3.5; return b * 2 + 1; }` | 95.99 | a parameter nothing reads, which is elided outright |
| `h() { var b = 3.5; return b * 2 + 1; }` | 95.99 | no parameter — identical, which is what proves the row above |

**And the numeric tier cannot be widened to parameters at all.** A `var` can be proved to
hold only numbers by reading the function; a parameter's value is the caller's choice, and no
analysis of the callee can constrain it. Holding one in a raw `double` needs an entry guard
and a generic fallback — that is speculation, and speculation is **phase 4**. So the item as
written asked for the one thing in its list that this phase cannot deliver, and would have
delivered nothing had it been taken at its word.

#### Landed — a parameter no longer costs a cell

The gate now admits parameters at the scalar tier, on four conditions, and the
simple-parameter-list one is doing more work than it looks:

| Condition | Why |
|---|---|
| `CanScalarReplaceLocals` | the same hazards that stop a `var`: direct `eval`, `with`, `debugger`, a dynamic nested function, async, generator |
| `arguments` named **nowhere** in the function or anything nested in it | a sloppy simple-parameter-list function gets a **mapped** `arguments` object, and the mapping is built out of the parameters' cells. Refused on any mention, because `arguments` is materialized lazily on first reference — long after the parameters are created |
| a **simple** parameter list | rules out defaults, rest and destructuring, and with them every *expression* in the parameter list. Without it, a closure in a default (`function f(a, b = () => a)`) would capture a scalarized parameter, because the hazard detector scans the body and never the parameter list. **A bound, not a heuristic** — it is what lets this reuse the existing analysis instead of extending it |
| not named by any nested function | capturing a binding requires naming it — the same rule, and the same set, `VisitBlock` already applies to `var`s |

**Bytes per call**, from the item's own acceptance test, on a three-parameter helper called
20 000 times:

| | Before | After | Ratio |
|---|--:|--:|--:|
| three-parameter call | 230.2 B | **62.2 B** | **0.27** |
| of which parameter cells | 168.0 | **0** | — |

**56.0 bytes per parameter per call, and it is now nothing.** The `--local-alloc` rows agree
and identify it as per-*binding* rather than per-call: a one-parameter helper drops 56.00, a
three-parameter one 168.00, and **three rows that provably cannot move — the two
no-parameter controls and the numeric-var control — do not move by a byte.** So a one-line
helper's call allocation falls **47% at one parameter and 73% at three**, on every helper in
every program, whatever its parameters hold.

Note what is *not* claimed: the in-loop rows above are unchanged, because a cell is charged
once per call and those loops call once. This item removes an allocation per call, not per
use, and the 31.98 B/iteration those rows report is still owed to the numeric tier.

**Patch 0047's hazard is untouched, and deliberately so.** This item never puts a value in a
raw `double`, so the codegen path that produced **invalid IL** when an unboxed local reached
value position is not on it — `InvalidProgramException` is the signature for the *numeric*
half of this gate, which is the half that did not land. The `NaN <= x` precedent applies
there too, not here.

**Verify.** 57 test cases in `ScalarParameterTests`, weighted to what a cell exists *for*
rather than to hit rates, because a miss there is a miscompilation and shows up as a stale
read rather than a crash: a mapped `arguments` aliasing both directions and a strict one not;
a closure over a parameter seeing a later write and writing back through it; a direct `eval`
reading and assigning one; `with` shadowing one and stopping at the closing brace; and eleven
refusal cases asserting **zero** scalar bindings for the shapes that must keep cells
(defaults, rest, destructuring, generators, async, `debugger`, `var arguments`, an
`arguments` mention reaching in from a nested arrow). Plus duplicate parameter names, a `var`
redeclaring a parameter, a body function declaration overriding one, arity mismatches both
ways, every write form, `typeof`/`delete`, recursion, class and accessor parameters, and a
catch parameter.

**The counter assertions are the ones that pin the gate**, and they were checked against the
build without the item: `ScalarLocals` is **0** for every parameter before and 2 for a
two-parameter function after, and the allocation test fails at 230.2 B/call against its
100 B/call bound. *A criterion that passes before the change measures nothing (§3.5), so this
one was run before the change and watched to fail.* Repository suite: **7 525 tests across 13
projects, 3 failures**, all three the pre-existing win-x64 host-environment ones §4.1 names.

**test262 over all four pinned manifests is unchanged — 8 220 passed, 84 failed, 9 timed out,
identical counts manifest by manifest** (§3.4), which is the gate this item most needed:
`FunctionDeclarationInstantiation` and the Annex B `arguments` mapping are the spec surface it
edits.

**Octane was run too, because this item's justification names it.** 2-8 established that a
benchmark quoted as an item's reason is a test that item has to pass, and 3-3's reason is
"every Octane benchmark is full of numeric helpers" — a claim about calls with parameters,
which is exactly what this changes. **14 of 15 suites `ok`, DeltaBlue and Richards included**,
on win-x64 with results kept out of `tests/octane/results`.

**The fifteenth is Mandreel, and it is not this item — confirmed against a control rather than
assumed.** It fails in phase `Setup` with `RangeError: Maximum call stack size exceeded` at
`EnsureWithinStackBudget` (`CallFrames.cs:215`) from `mandreelAppInit` (`mandreel.js:1460`),
which is the win-x64 signature phase 0 recorded, item 1-2 diagnosed and 2-9 already controlled
for one pointer earlier. Re-run with **only the compiler reverted to `71dda1b7`**, same machine
and same harness, it fails **byte-identically** — the two `OCTANE_ERROR` records compare equal,
so it is the same guard, frame, phase and eleven-frame stack, not merely a similar-looking one.
*A failing suite is a claim; the control is what turns it into a verdict, and the pointer had
moved since the last one was taken.*

> **`RegExp` scored rather than failing its checksum**, which is a change from what 2-8
> recorded as a pre-existing defect. Not investigated here and not claimed as fixed — a single
> run on a different platform is not evidence either way — but it is worth someone confirming
> before that note is relied on again.

**Size: M**, and the half that landed came in at that size. What did not land is below.

> **One pre-existing defect found in passing, neither caused nor fixed here.** A parameter
> named `undefined` does not shadow the global: `(function (undefined) { return undefined; })(1)`
> answers `undefined`, and `typeof` on it answers `"undefined"` rather than `"number"`. It
> reproduces identically with this item reverted, so it is not a regression, and it is pinned
> by `KnownGap_AParameterNamedUndefinedDoesNotShadowTheGlobal` rather than left to be
> rediscovered — a fix flips a failing assertion instead of passing unnoticed.

#### A correctness fix the successor needed first — two writes the analysis could not see

**Found by probing `NumericLocalAnalysis` before extending it, and it is a wrong-answer bug in
shipped code** — present since `a746f82d` landed P2-2 item 3, on every platform, in ordinary
JavaScript. The analysis proves a `var` only ever holds a number and then the compiler keeps it
in a raw CLR `double`. Two ways of writing that binding were invisible to the proof:

| Invisible write | Why | What happened |
|---|---|---|
| A `var` **re-declared** below the function body's own statement list — inside a block, `if`, loop, `while`, `try`, `switch` | it names the same function-scoped binding, but only the *top-level* declarations were recorded as stores; the collector's `VisitVariableDeclarator` visited the initializer as a read and recorded nothing | `var s = 0; { var s = 'x'; } return s` → **NaN** |
| Any name bound through an **object destructuring pattern**, in a declaration *or* an assignment | `AstReduce` treats `ObjectProperty` as a leaf, and `NameCollector` — the walker behind every `RejectEveryNameIn` call — never overrode it | `({ a: s } = { a: 'x' })` → **NaN**; `var { a: s } = …` → **the process aborts** |

**The second failure mode is the serious one, and it is not a wrong answer — it is an unhandled
`System.NotImplementedException`** (*"Assignment target Call (BCallExpression) is not
supported"*) out of `ILCodeGenerator.VisitAssign`, which kills compilation of the whole script
and cannot be caught from JavaScript. That is precisely what the numeric local's own remarks
predict: its readable `Expression` is a **boxing read**, so writing through it is an assignment
to a method call. Three shapes reach it — `var { a: s } = o`, the same nested in a block, and
`for (var { a: s } of …)`. A fourth, `[...s] = ['a']`, threw a bogus `undefined is not a
function`.

**One root cause is shared and it is worth naming.** `ScalarReplacementHazardDetector` and
`NestedFunctionScanner` both carry a comment explaining that `AstReduce` leaves `ObjectProperty`,
`VariableDeclarator` and `Case` as leaves, and both override all three — *"Missing one here is
not a missed optimization but a miscompile."* `NameCollector` is the third walker in the same
family and had none of them. The comment was right, was written twice, and was not applied to
the class that needed it most: `RejectEveryNameIn` is the analysis's only *rejection* path, so a
name it cannot see is a name nothing else will reject either.

**The fix costs nothing measurable**, which is the expected result and worth stating because it
is checkable: all fourteen `--local-alloc` rows are byte-identical before and after and every
numeric-local count is unchanged, because the names now rejected are exactly the ones that were
being compiled wrongly. Ordinary code loses no specialization — `a[i] = v` and `o.x = i` are
asserted by count, not just by answer, since the over-broad version of the pattern rule would
have silently undone 3-0's unboxed index while still computing the right values.

**Verify.** 35 test cases in `NumericLocalWriteVisibilityTests`, written as ordinary JavaScript
answers because every one of them is a value the engine got wrong or refused to compile.
**18 of the 35 fail on the build without the fix**, four of those by aborting the test host —
which is what makes them a pin rather than a description. Repository suite: **7 560 tests across
13 projects, 3 failures**, the pre-existing win-x64 host ones.

*This is why the successor could not start first.* Extending the same analysis to `let`/`const`
without this would have widened a silent-NaN miscompilation to two more declaration forms.

#### What is left of 3-3, and it now outranks what landed

`let`/`const` and the block-scoped `var` are still ineligible, and the measurement moves them
**ahead** of where the item put them, for a reason the item could not have known:

- They cost the same per site as a parameter did — **31.98 B/iteration**, charged per
  *assignment* rather than once per call, so on a loop they dominate what a cell ever cost.
- They can reach the **numeric** tier, which parameters cannot: a `const v = 3.5` at function
  top level is exactly as provable as the `var` beside it, and the TDZ condition is already
  satisfied by the dominance argument `NumericLocalAnalysis` uses today — the declaration
  must be a direct statement of the function body with no textual reference before it.
- And they carry the multiplier: re-qualifying one binding re-qualifies every local
  downstream of it, which is the 1 → 3 in the table above.

So the successor item is **`let`/`const` at the numeric tier first**, then the block-scoped
`var` (which does need the definite-assignment analysis the item names).

#### `let`/`const` was attempted and **withdrawn**, and the reproduction is the deliverable

It was built, it measured exactly as predicted, and it miscompiles. Recorded here rather than
left as a branch, because the next attempt should start from the evidence.

**What worked.** Offering a function-body-top-level `let`/`const` to `NumericLocalAnalysis` and
admitting a lexical name in the function body block only:

| Site | Before | After |
|---|--:|--:|
| `let-binding` | 31.98 B/iter, 1 numeric local | **0.00 B/iter, 3** |
| `const-binding` | 31.98 B/iter, 1 numeric local | **0.00 B/iter, 3** |

— identical to `top-level-var`, the eligible floor, with **every other `--local-alloc` row
unchanged**. The multiplier the section above predicts is visible in the second column: one
binding re-qualified, and the accumulator and counter that read it came with it. Semantics held
in single-compilation runs: the `const` reassignment `TypeError`, the `let` TDZ
`ReferenceError`, and the nested-shadowing dead zone all still fired, byte-identical to the
baseline.

**Two obligations it had to discharge, and both were fine.** The **TDZ** is discharged by the
dominance argument the analysis already makes — a name with any reference before its
declaration is rejected, so the throw is unreachable rather than removed. **Const-ness** needed
one addition: a write to a const is a `TypeError` raised by the binding's *cell*, so a const
written anywhere was rejected outright rather than specialized into a silent store.

**What is wrong.** After **any** earlier compilation in the same process — a different
`JSContext`, a different source — a `let` declared in a *nested block* reads back as an
uninitialized double:

```js
// First, in one JSContext:
(function () { let v = 3.5; v = v + 1; return v; })()      // → 4.5, correct

// Then, in a fresh JSContext in the same process:
(function () { let v = 1; { let v = 2; return v; } })()    // → 2.0000000074796844
(function () { { let v = 2; return v; } })()               // → 2.0000000074796844
(function () { let v = 1; { let w = 2; return w; } })()    // → 2.0000000074796844
```

**The tell is the third line: none of those nested bindings is one the gate admits.** A lexical
name is applied in the function body block only, so `{ let v = 2; }` must get a cell — and it
does not. **So a lexical binding's storage is decided somewhere other than that gate**, and
until that is found no amount of tightening the gate is a fix. Three hypotheses were eliminated:
it is not a specific predecessor (any one will do), not a compile count (64 preceding
compilations in a fresh context each are harmless), and not repetition of the same source. The
value's shape is a clue worth keeping — the high bits read as the right integer and the low
mantissa bits are garbage, which is a slot written narrower than it is read.

**One real bug was found and fixed on the way there**, which is why the attempt was worth
making even though it did not land: the lexical declaration path assigns through the binding's
value setter, and for a numeric local that setter is a **boxing read** — so the first build of
this threw `System.NotImplementedException: Assignment target Call (BCallExpression) is not
supported` out of `ILCodeGenerator.VisitAssign`. That is patch 0047's hazard family, exactly
where this item's own *Watch* note said to look, and the fix is to test `NumericStorage` before
the lexical branch rather than after it.

**For the next attempt**, in order: find what else decides a lexical binding's storage (start at
`VisitBlock`'s `CreateVariable` and `FastFunctionScope.variableScopeList` — the block scope is
constructed fresh, so the leak is below it); keep the `NumericStorage`-before-lexical ordering
in `VisitVariableDeclaration`; keep the const-write rejection; and re-run the reproduction above
**as two evaluations in one process**, because a single one is green and the script host is
therefore not an instrument that can see this.

`const` remains the cheaper half and worth separating once the storage question is answered: it
cannot be reassigned at all, so its analysis reduces to checking the initializer.

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
| **4-3** | **Deoptimization** — **designed; see below** | `Runtime/FunctionTiering.cs`, `Engine/CallFrames.cs`, and for 4-3b `.Compiler` / `.ExpressionCompiler` | The safety net that makes everything else legal. "Bail out mid-function by reconstructing an interpreter frame" is **not expressible here** — there is no interpreter frame. Splits into **4-3a** (S, the restart contract the pilot already implements) and **4-3b** (M–L, a generic fallback branch inside the specialized method), and only 4-3b gates 4-4 | ~~XL~~ **S + M–L** |
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

### 4-3 · Deoptimization — **design spike; the item is mis-specified and the fix is cheaper**

Written before 4-2 as the phase requires. Four questions, answered from the code so nobody
re-derives them.

**1. What does a mid-function bailout have to reconstruct? — Nothing, because there is nothing
to reconstruct *into*.** 4-3's brief says "reconstruct an interpreter frame from a specialized
one". **This engine has no interpreter frame.** §4.3's own B2 says so: source → `FastParser` →
`FastCompiler` → expression trees → IL → RyuJIT, and "real machine code comes out, so this is not
'an interpreter'". Tier-1 is a compiled `JSFunctionDelegate`, and a JavaScript local in it is a
**CLR local of that IL method** — that is exactly what phases C–F achieved. `CallFrame` carries
`FileName`, `Function`, `Line`, `Column`, `NewTarget`, `DirectEvalBindings` and the `Escaped`
marker, and **no JavaScript values at all**.

So the V8 model — a stack map naming where each value lives, replayed into an interpreter frame —
has no counterpart here, and could not have one: the CLR does not let one method materialize
another's locals. *The item was written from V8's architecture, not from this one.*

**2. What transfer IS expressible? Two, and the pilot already runs the first.**
`NumericLoopPlan.Compile(baseline, deoptimize)` takes the **baseline delegate** and, on a failed
guard, does:

```csharp
if (!guard) { deoptimize(); return baseline(in arguments); }
```

That is **restart, not resume** — re-enter the unoptimized function with the original arguments —
and it is soundly limited to guards that fire *before any observable effect*. The pilot's fire on
entry, on argument count and argument type.

The general mechanism is the other one: **compile the specialized and generic forms into one
method and make a failed guard a branch.** Then the CLR locals are shared because it is the same
method, no transfer exists to get wrong, and speculation is legal *after* effects have begun —
which is what 4-2 and 4-4 need and what restart cannot give them. It costs code size, and the
generic path can never be dropped.

**3. How does each interact with `CallFrameStack`'s three invariants?**

| | Entry-guard restart (A) | In-method branch (B) |
|---|---|---|
| suspendable frame retaking a slot | **illegal** — a generator or async body may already have yielded, so re-entering it re-runs effects. Never speculate this way on one | untouched: one method, one `FrameToken`, no re-entry |
| unwinding never growing back | safe only if the guard fires **before** the frame is pushed; otherwise the optimized frame must be popped, and `RestoreDepth` deliberately refuses to grow, so a bailout can never resurrect an abandoned slot | no frame transition at all |
| popping past stranded callees | the restart must not leave the optimized call's frame behind — `Pop(token)` clears from the target to the current depth | not reachable |

**(B) is the design that preserves all three by not engaging them.** That is the strongest
argument for it, and it is an argument the item could not have made before the frame redesign
landed.

**4. Is the item still XL? No — it is two items, and neither is XL.**

- **4-3a, S:** state the restart contract the pilot already implements, and enforce it — guards
  before any effect, no suspendable bodies, frame popped on the bailout path. Mostly a rule and
  a test, since the mechanism ships today.
- **4-3b, M–L:** teach the compiler to emit a generic fallback path inside a specialized method
  and branch to it. This is the real prerequisite for 4-2 and 4-4, and it is a codegen change in
  `.Compiler` / `.ExpressionCompiler` rather than a runtime redesign.

**What this changes about the phase.** "Do not start 4-2 before 4-3 has a design" stands, and the
design now exists. But the sentence under it — *"speculation without a mid-function bailout is
either unsound or restricted to functions with no observable side effect before the guard, which
excludes everything worth optimizing"* — is **half wrong**: restart is exactly that restricted
form, and it is not worthless (it is what the shipping pilot uses). What it excludes is
speculation *inside* a body, which is what inlining needs. **4-3b is therefore the gate on 4-4,
not on all of phase 4**, and 4-1's feedback collection can start immediately — it consumes
neither.

**Verify, when built.** Deopt correctness before any speculation ships, as the phase already
says, and for (B) specifically: a test that forces every guard to fail at every point in a body
and asserts the generic path produces the unspecialized answer *with the same observable effect
sequence* — the effects before the guard have already happened and must not be repeated, which
is the one thing a branch gets right for free and a restart cannot.

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

### The gate is satisfied, and it overturns the phase — **`Matcher.cs` is not on this path**

Profiled with `--regex-profile` (new; `RegexProfileMetrics`), and the first thing the profile
established is that **the engine this phase is written about barely runs**.

**`Broiler.Regex` is not the default engine, by design.** `JSRegExp.Broiler.cs` says so in its
own header — *"JSRegExp keeps the mature .NET translator as the default engine and routes ONLY
gap-feature patterns that Broiler.Regex can fully handle through it"* — and `GapScan` defines
"gap" precisely: an astral or lone-surrogate atom under `u`, a back-reference inside a
look-behind or in Unicode mode, a capturing group inside a look-behind, or a nullable quantifier
that can repeat. **Octane's `regexp.js` contains no look-behind and no `u` flag at all**, so
essentially none of the suite reaches `Matching/Matcher.cs`. B5's sentence — *"`Broiler.Regex`'s
`Matching/Matcher.cs` has no compilation to native code … the same engine sits on PdfJS's and
Typescript's critical path"* — is describing a component those workloads route around.

**What does serve them is `System.Text.RegularExpressions`, built INTERPRETED.**
`JSRegExp.ParseFlags` starts from `RegexOptions.ECMAScript` and the pattern is constructed as
`new Regex(pattern, options)`; **`RegexOptions.Compiled` appears nowhere on the user-regex
path**, though the engine does use it for `Intl` and `DateParser`. So the phase's own plan —
"compile the common subset, keeping the interpreter as the fallback" — would be building a
compiler for a path the benchmark never takes, while the path it *does* take has compilation
available behind one flag.

**Measured, on seven patterns lifted from `regexp.js` itself**, 200 000 matches each, the same
`RegexOptions.ECMAScript` the engine ships against that plus `Compiled`:

| Pattern | Interpreted | Compiled | Speedup | Build (interp → compiled) |
|---|--:|--:|--:|--:|
| `^ba` | 10.19 ms | 4.72 ms | **2.16×** | 1.2 µs → 6.8 µs |
| `,` | 9.53 | 4.67 | **2.04×** | 1.0 → 6.3 |
| `(-[a-z])` | 14.38 | 7.69 | **1.87×** | 2.4 → 13.4 |
| `[+, ]` | 9.48 | 5.01 | **1.89×** | 1.3 → 6.6 |
| `TNQP=([^;]*)` | 16.98 | 8.41 | **2.02×** | 1.6 → 12.1 |
| `[<>]` | 9.27 | 5.01 | **1.85×** | 1.4 → 6.7 |
| **`^[\s\xa0]+\|[\s\xa0]+$`** | 17.95 | **71.54** | **0.25× — four times SLOWER** | 4.2 → 25.9 |

**Six of seven are worth about 2×, and the seventh is a 4× regression** — which is exactly why
this is a measurement and not a flag to set globally. Construction costs 5–6× more compiled, but
in absolute terms 7–26 µs against a pattern Octane builds once and matches hundreds of thousands
of times, so the trade is not close *where it wins*. A per-pattern decision — compile on the
second or third use, the way tiering already reasons about functions — is the shape this wants,
not a blanket option.

**And the largest regex-shaped cost in the engine is not matching at all.** Nine JS-level shapes
over a 20 000-character subject, net of an inert loop:

| Shape | ns/char | B/char |
|---|--:|--:|
| `re.test` miss — literal, class, alternation | 0.13 – 0.35 | ~0.02 |
| `re.exec` hit with eight captures | 0.80 | 2.23 |
| `/a*b/` quantifier walk, fails at every position | 0.95 | 0.02 |
| `String.indexOf` for the same literal *(floor)* | 15.94 | 0.00 |
| **`subject.replace(/[aeiou]/g, 'x')`** | **1 318** | **10 522** |

The miss rows cost *less than `indexOf`*, which is the clearest possible statement that matching
is not where the time goes. `replace` with a global flag is **~3 800× the next row in time and
~4 700× in bytes**: 20 calls allocated **4.21 GB**, i.e. **210 MB per call and ~42 KB per match**
on a 40 KB subject with 5 000 matches. Time scales only mildly superlinearly (2.3× for a doubled
subject), so this is **allocation per match**, not an algorithmic blow-up — a match-result object
built per match, which is the same shape as phase E's quadratic string concatenation and wants
the same treatment.

**So phase 5 is re-specified, and re-ordered against itself:**

1. **Stop allocating per match on the `replace`/`exec` result path — landed, see below.**
   Largest measured cost by three orders of magnitude, and it is the one that reaches PdfJS and
   Typescript — which is what this phase claimed to care about.
2. **Decide `RegexOptions.Compiled` per pattern** — measured further, and **a use count is not
   enough to decide it**. See below.
3. **Only then consider compiling `Broiler.Regex`.** It is correctness-critical for the gap
   cases and it should stay, but no measurement here puts it on a hot path, and B5's ranking of
   it was never checked against the routing.

**RegExp's 110× is therefore not evidence about `Matcher.cs`**, and the score should not be
quoted as if it were until something establishes which engine produced it.

#### Item 1 landed — an Annex B legacy static was copying the subject on every match

The profile said `replace` with a global flag cost **10 522 bytes per subject character**. The
cause is one line, and it is not in either regex engine.

`RegExpBuiltinExec` calls `LegacyRegExpState.Update` on every **successful** match, to keep
Annex B §B.2.4's deprecated statics warm — `RegExp.lastMatch`, `RegExp.leftContext`,
`RegExp.rightContext` and friends. `LeftContext` and `RightContext` **partition the subject
around the match**, and they were built eagerly:

```csharp
LastMatch    = input.Substring(startIndex, endIndex - startIndex);
LeftContext  = input.Substring(0, startIndex);      // O(startIndex)
RightContext = input.Substring(endIndex);           // O(length - endIndex)
```

Together those copy the **entire subject, once per successful match**. And
`RegExp.prototype[@@replace]` is the generic spec path — it calls `exec` once per match — so a
global replace was **quadratic in allocation**: measured at **42 859 bytes per match**, 204 MB
for one call over a 40 KB subject with 5 000 matches.

**The fix is to record the span and slice on read.** Nothing needs those substrings until
somebody reads one, and almost nothing ever does — they are a deprecated compatibility surface.

| | Before | After | |
|---|--:|--:|--:|
| `replace(/[aeiou]/g, 'x')` | 10 522 B/char | **504 B/char** | **0.048x** |
| the same, time | 1 318 ns/char | **397 ns/char** | **0.30x** |
| `exec` with eight captures | 2.23 B/char | **0.23 B/char** | 0.10x |
| `test`, matching | 2.22 B/char | **0.22 B/char** | 0.10x |
| every **miss** row | 0.01 – 0.02 | **unchanged** | — |

The miss rows are the control and they do not move by a byte, which is what identifies the cost
as *per successful match* rather than per scan.

**What remains, decomposed — and the per-character framing was hiding it.** A `--regex-profile`
scaling section now reports bytes **per call** at three subject lengths, because 4 400 bytes on a
20 000-character subject reads as 0.22 B/char whether it scales with the subject or not:

| Operation | 5 000 | 20 000 | 80 000 | Reading |
|---|--:|--:|--:|---|
| `test`, matching | 2 051 | 2 351 | 3 551 | **~1 950 B fixed** + 0.02 B/char |
| `exec`, matching | 1 995 | 2 295 | 3 495 | the same, and the result array is nearly all of it |
| `replace`, **one** match | 22 635 | 82 935 | 324 135 | **~4 B per subject character** |

So `exec` is **not** proportional to the subject — it is a flat ~2 KB per call, which is the
result array plus its `index` / `input` / `groups` properties. And a *single* non-global
`replace` costs four bytes per subject character, which is **two full UTF-16 copies**: the
`StringBuilder`'s chunks and then its `ToString()`.

Two follow-ups, both sized by those rows. **The first has landed** — see below; the second has
not started:

- **The single-match replace should not use a builder at all.** `input[0..pos] + replacement +
  input[end..]` is one `string.Concat` over three spans and one allocation, halving 4 B/char
  to 2. *Pre-sizing the builder was tried first and is worth 0.2%* — .NET's `StringBuilder`
  is a chunk list, not a doubling array, so there was no reallocation waste to remove. The
  change was reverted rather than kept for a rationale that turned out to be wrong.
  **Landed, and the halving is exact — in both builtins that had the pattern.**
- **A global replace retains every result before it builds anything.** §22.2.6.11 collects all
  matches in step 14 and reads their properties in step 16, so 5 000 matches means 5 000 live
  result arrays — 5 000 × ~2 KB, which is exactly the ~10 MB per call still measured. Streaming
  them would change the observable order of `exec` calls against capture reads, so it is only
  available on a fast path where nothing is patched. **Landed — and the estimate was right to
  three digits: 2 033 bytes per match, dead linear.**

**One hazard the change introduced and closed.** Deferring the slice means `Update` publishes a
subject and two indices that must agree; three separate field writes would let a reader on
another thread pair a new subject with the previous match's indices and slice outside it. The
eager version could not do that — its fields were independent, already-built strings, so a torn
read returned something stale but valid. They are now one immutable record published by a single
reference write.

**Verify.** `LegacyRegExpStaticsAllocationTests` asserts the bytes, because nothing about the
*answers* changes and `Issue845RegExpAndWithTests`' twenty existing cases pass either way — on
the build without this, the allocation test reports **204.4 MB (42 859 B/match)** against its
50 MB bound, so it fails by a factor of four. A second test pins that the statics still describe
the last match, so the allocation test cannot be satisfied by simply not recording them.
Repository suite **7 563 tests across 13 projects, 3 failures**, the pre-existing win-x64 host
ones. **test262 unchanged across all four pinned manifests** — 8 220 passed, 84 failed, 9 timed
out, identical manifest by manifest. **Octane 14 of 15 `ok`**, the same set, with Mandreel's
failure record byte-identical to the earlier runs.

> **Octane's scores moved the right way and are not claimed.** Across this session's four
> broiler-only runs RegExp went 131, 126, 132 → **140** and Typescript 2 935, 2 951, 2 998 →
> **3 257**, so both landed above the spread of the three runs that preceded the change — which
> is the direction a per-match subject copy disappearing should push the two suites that use
> regexes most. **One repetition per side cannot separate a change from noise (§3.2)**, and
> these are single runs on a developer workstation, so what is claimed here is the allocation
> figure, which is deterministic and exact. The scores are recorded as corroboration and as a
> reason for 0-6 to look at them.

#### The single-match follow-up landed — the halving is exact

> **Delivered as a patch, not in the pin.** The push to `Broiler-Platform/Broiler.JS` returned
> **403** — the submodule remote is outside this session's GitHub scope — so per the patch
> workflow the pointer is **not** bumped and the change ships as
> [`patches/0059`](../patches/0059-js-single-match-replace-one-allocation.patch) for a maintainer
> to apply. Every figure below was measured on a local build of the pinned `2ebc0c3c` **plus**
> that patch, with the control built from the same tree minus it. No main-repo fallback is
> needed: this is an allocation reduction with no behaviour difference, so CI is correct without
> it and only more allocating.

`RegExp.prototype[@@replace]` accumulated into a `StringBuilder` whatever the match count. For a
single match the answer is exactly `prefix + replacement + suffix`, so `string.Concat` over three
spans writes it into **one** allocation of the final length, and the builder's two copies — into
its chunk list, then back out through `ToString()` — become one.

Measured with the same `--regex-profile` scaling rows, both sides built from the same tree rather
than compared against the figures recorded above:

| `replace`, one match | 5 000 | 20 000 | 80 000 | Slope |
|---|--:|--:|--:|--:|
| Before | 22 635 | 82 935 | 324 190 | **4.020 B/char** |
| After | 12 483 | 42 783 | 164 030 | **2.020 B/char** |
| | 0.55x | 0.52x | 0.51x | **exactly half** |

**The predicted halving is realized to two decimal places, and that is the load-bearing part.**
The decomposition claimed the 4 B/char was two copies of the subject *and nothing else*; removing
one copy and getting exactly half is what confirms there was no third. The `test` and `exec` rows
are byte-identical on both sides — 2 051 / 2 351 / 3 551 and 1 995 / 2 295 / 3 495 — so they are
the control, and they place the saving on the replace path rather than in the profile.

*(The before row's 80 000 figure is 324 190 here against the 324 135 recorded in the table above.
Both are this build's own control, taken a pointer apart; 55 bytes on 324 KB is 0.02% and changes
no slope. It is written as measured rather than reconciled to the earlier row.)*

**The same assembly was one file over, and it is fixed too.** `String.prototype.replace` with a
**string** `searchValue` never reaches `@@replace` — it is a separate builtin — and it replaces
only the first occurrence, so it is single-match *by construction*. It built its answer from three
appends into a **pre-sized** `StringBuilder`: the same two copies, and the clearest case of why
pre-sizing does not help, since that builder was already sized exactly right. `--regex-profile`
now carries a `replace-one-string` row beside `replace-one` so the two cannot drift apart:

| `replace`, one match, **string** searchValue | 5 000 | 20 000 | 80 000 | Slope |
|---|--:|--:|--:|--:|
| Before | 20 406 | 80 706 | 321 965 | **4.020 B/char** |
| After | 10 326 | 40 626 | 161 877 | **2.020 B/char** |
| | 0.51x | 0.50x | 0.50x | **exactly half** |

Its slope was already identical to the regexp path's to three decimal places before the change,
which is what identifies the two as the same defect rather than two similar ones — and it lands
on the same 2.020 after. **It was found by reading the neighbouring builtin, not by the profile**,
which had no row for it; the row exists now because the fix needed one.

**A global regexp that matches once takes the fast path too.** The gate is `results.Count == 1`,
not the `g` flag: `global` decides how many results were collected, not how they are assembled,
so `'abc'.replace(/b/g, 'X')` gets it. And step 16.p's backwards-position guard cannot apply to a
single result — `nextSourcePosition` is still 0 and `position` is clamped to [0, lengthS], so
`position >= 0` holds — which makes the fast path the loop's behaviour rather than an
approximation of it.

**The per-result work is shared, not duplicated.** Step 16 reads the result's properties in an
order a Proxy can observe (`length` → `0` → `index` → captures → `groups`), and test262
`sm/RegExp/replace-trace` pins it. Both paths call one local function rather than each carrying a
copy, so the order cannot drift between them; it is called directly and never as a delegate, so
the capture is a struct closure and costs no allocation.

**Verify.** `SingleMatchReplaceAllocationTests` asserts the bytes and **fails on the build
without the change** — 95 881 B/call against its 60 000 bound — while every one of its answer
cases passes on *both* builds, which is what identifies them as regression guards rather than
change detectors. They cover the edges the fast path now owns: a match at position 0 and at the
end of the subject, an empty match, an empty replacement, the global-matching-once case, every
`$` substitution form, functional replacers, surrogate pairs, an ill-behaving `exec` reporting an
index past the subject, the Annex B statics, and the property read order — plus the same edges
again through the string-`searchValue` builtin.

**test262 is unchanged across all four pinned manifests — 8 313 executed, 8 220 passed, 84
failed, 44 skipped, 9 timed out, identical manifest by manifest** to §3.4's recorded run, at
suite ref `ccaac100`. And because the four pinned manifests contain no `replace` coverage at all,
**the paths this change actually touches were run separately, control against change**:
`RegExp/prototype/Symbol.replace`, `String/prototype/replace`, `replaceAll`, `RegExp/prototype/exec`,
`Symbol.split`, `Symbol.match`, `annexB/built-ins/RegExp` and `staging/sm/RegExp` — **499 tests,
484 passed, 13 failed, 2 timed out, and the failing set is the same file for file on both
builds.** All 13 are cross-realm cases needing `$262.createRealm`, which the raw script host does
not provide; **not one failure is in a `replace` directory.** Repository suite
**7 604 tests across 13 projects, 0 failures** — 41 of them new here.

#### The retained-result-list follow-up landed — 2 033 bytes a match, and the guard is the change

> **Delivered as a patch, not in the pin**, for the same 403 as the section above, and it must be
> applied *after* it: [`patches/0060`](../patches/0060-js-stream-global-replace.patch) builds on
> the function [`patches/0059`](../patches/0059-js-single-match-replace-one-allocation.patch)
> restructures. Figures below were measured on a local build of the pinned `2ebc0c3c` plus both.

`RegExp.prototype[@@replace]` collects every match in step 14 and only reads their properties in
step 16, so a global replace held one result array per match live before it assembled anything.
**Measured with the subject held fixed and only the match count varying** — which is the
discriminator the earlier scaling rows could not be, since they vary the subject and hold the
match count at one:

| Global replace, 40 000-char subject | 500 matches | 2 500 | 5 000 | Slope |
|---|--:|--:|--:|--:|
| Before | 1 181 612 | 5 247 695 | 10 329 158 | **2 032.8 B/match** |
| After | 409 588 | 1 363 919 | 2 561 870 | **478.3 B/match** |
| | 0.35x | 0.26x | 0.25x | **0.235x** |

Dead linear on both sides — 2 033.0 and 2 032.6 across the two independent intervals before the
change — so the retained list was the whole of it, and the previous section's "5 000 × ~2 KB ≈
10 MB" estimate was right to three digits rather than approximately. What is left at 478 B/match
is the match data itself: the `RegexMatchData`, its capture array and the matched string.

**The optimization is four lines; the guard is the item.** Appending each replacement as it is
produced, instead of collecting first, is trivial. Establishing that nobody can *watch* the
results being skipped is the entire design, and it needs three conditions at once:

| Condition | Why, and what breaks without it |
|---|---|
| The receiver's `exec` **is** the pristine `%RegExp.prototype.exec%` | Every result is then a fresh array this function is the only holder of. A patched `exec` — own property or on the prototype — must run, and its results are the user's |
| The replacement is a **string**, not a function | A functional replacer is user code running *between* matches |
| That string contains **no `$`** | `$&`, `` $` ``, `$'`, `$n` and `$<name>` all read back through the result object |

**Two of those three are not obvious from the item's own description, and one is a real trap.**
The item says streaming "would change the observable order of `exec` calls against capture reads",
which is true but understates the functional-replacer case: because the spec collects *all*
matches before calling *any* replacer, the final failing `exec` has already reset `lastIndex` to
**0** by the time user code first runs. A streamed replacer would instead see `lastIndex` sitting
mid-subject, at a different value for every call. That is not a reordering, it is a different
observable value, and `AFunctionalReplacerIsNotStreamed` pins it at `0,0`.

**Identity against a pristine capture is the only sound form of the `exec` test.** `%RegExp.prototype.exec%`
is captured into `JSContext.IntrinsicRegExpExec` at realm init, before any user code can run —
the same mechanism `IntrinsicArrayValues` and `IntrinsicPromisePrototype` already use. Reading
`RegExp.prototype.exec` later and comparing it to itself would be circular: by then it may
already be the patched one.

**Matching still goes through one shared code path.** `Exec` is split into `ExecMatch` — the
`lastIndex` read and write, the match, the sticky re-check and the Annex B statics — and
`BuildExecResult`, which is the part the fast path skips. Both callers use `ExecMatch`, so
`lastIndex` progression, engine routing and the statics cannot drift between the two paths,
because they are the same code rather than the same intention written twice. This is the same
device 0059 used for step 16's property read order, and for the same reason.

**Verify.** `GlobalReplaceStreamingAllocationTests` asserts the bytes and **fails on the build
without the change** — 2 610 B/match against its 1 000 bound — while all 22 guard cases pass on
*both* builds, which is what makes them regression guards rather than change detectors. They
cover each exclusion (patched own `exec`, patched prototype `exec`, an `exec` returning null, a
functional replacer, and every `$` form), the empty-match advance under `/u` and without it
(asserted as code units, because a C# literal for a lone surrogate is its own trap), sticky with
global, `lastIndex` after the call, and the Annex B statics.

Repository suite **7 627 tests across 13 projects, 0 failures**. **test262 unchanged across all
four pinned manifests — 8 313 executed, 8 220 passed, 84 failed, 44 skipped, 9 timed out,
identical manifest by manifest**, at suite ref `ccaac100`. The replace-path manifest was re-run
too: **499 tests, 484 passed, 13 failed, 2 timed out, and the failing set is identical file for
file to the *pre-0059* control** — so both of this phase's follow-ups together move no test262
file in either direction.


#### Item 2 measured — and the obvious policy is the wrong one

The single-run table above had one pattern losing badly under `Compiled`. Repeated three times
it is **stable, not noise**: `/^[\s ]+|[\s ]+$/` — an ordinary *trim* — measures
**0.236, 0.225, 0.237**, consistently about **4.3× slower compiled**, while the other six sit
between 1.7× and 2.3× faster.

That kills "compile after N uses", which is the policy this item was about to specify: a trim is
exactly the kind of pattern a program runs hundreds of thousands of times, so a use counter would
find it first and make it four times worse.

**So the loss was characterized rather than guessed at**, with four probes decomposing the
pattern (they are kept in the emitter, since the next attempt needs them):

| Probe | Speedup | |
|---|--:|---|
| `^[\s ]+` — anchor + class quantifier | 0.366, 0.365 | **loss** |
| `[\s ]+$` — the other anchor | 0.464, 0.419 | **loss** |
| `[\s ]+\|zzz` — **same class, no anchor** | 2.758, 2.938 | big win |
| `^a+\|b+$` — **anchored alternation, literals** | 3.425, 2.765 | big win |

**It is neither alternation nor anchoring**, which were the two obvious readings — an anchored
alternation of literal quantifiers is one of the *best* rows in the set. What loses is
specifically an **anchored character-class quantifier**, and the `trim` pattern is that shape
twice over.

**No policy is shipped, on purpose.** The rule above is drawn from eleven patterns on one
runtime, and turning it into "compile unless the pattern begins with an anchored class
quantifier" would be exactly the kind of heuristic §3.5 warns about — a branch described from
its intent rather than traced with real numbers. What the next attempt needs, in order: the same
comparison over a corpus far wider than Octane's, an explanation of *why* the compiled path
loses that shape (it is .NET's codegen, not this engine's), and only then a predicate. A
per-pattern decision made by measuring both forms once on the real subject — the way tiering
already reasons about functions — is the design most likely to survive that, because it needs no
predicate at all.

> **The RegExp checksum failure 2-8 recorded did not reproduce.** All three Octane runs this
> session scored it (131, 126, 132) rather than failing `Error: Wrong checksum.`. Left as an
> observation, not a claim: those are single runs on a different platform from the one that
> recorded the failure, and nothing here was aimed at it.

---

## Sequencing

| Phase | Order within it | Size | Unblocks / expected effect | Exit gate |
|---|---|---|---|---|
| **0** | 0-1…0-5 ✅, 0-9…0-11 ✅ → **0-6 (CI — 17/17 established at the pin locally; the workflow run is still owed) → 0-7, 0-8** | — | Everything. 12 → **17 scores**, known noise band, and the first evidence any phase A–F can close on | 17/17, no timeout at the 180 s floor, band on record, `comparison.md` reporting the triad, **and the BenchmarkDotNet + RID-matrix rows collected** |
| **1** | 1-2 mitigation ✅ → 1-2 real fix ✅ (all three passes) → **1-4 ✅** → **1-1** → 1-3 measure | 1-4 S, 1-1 XL | The two worst scores in the suite; page-load time generally. **1-4 took the Mandreel half (3.04×); 1-1 owns the CodeLoad half** | test262 over the four pinned manifests, no new failure **and no new timeout**; MandreelLatency and CodeLoad out of the tail |
| **2** | 2-0 ✅ → 2-1 ✅ → 2-2 ✅ → 2-4 ✅ → 2-7 ✅ → 2-8 ✅ → **2-9 ✅** (2-3's successor, L); 2-5 and **2-3 closed on measurements**, 2-6 folded into 4-1. **Every item is landed or closed** | M each, 2-9 L | The Richards/DeltaBlue/Box2D cluster | An ownership entry and owned tests **per item**; test262 properties/strict-mode **satisfied** — unchanged at `a6f101cc` plus 2-9; **DeltaBlue and Richards inside 200×** — **measured: Richards PASSES (183× → 150× after 2-11/2-12), DeltaBlue FAILS (576× → 447×)**, five repetitions per engine on one machine |
| **3** | 3-0 ✅ (both halves) → 3-3 parameters ✅ → **3-3 `let`/`const`** → 3-1 → 3-2, then *cost* 3-4 | M, then L–XL | Uniform lift across arithmetic and allocation-heavy suites | `test262-arrays`, `test262-binary-data`; allocation reported per item alongside time |
| **4** | 4-3 design ✅ → **4-1** (unblocked) → 4-3a (S) → 4-3b (M–L) → 4-2 → 4-4 | XL | The remaining order of magnitude | Deopt correctness proven **before** any speculation ships; full test262 matrix |
| **5** | profile ✅ → per-match subject copy on `replace`/`exec` ✅ → single-match `replace` without a builder ✅ (both builtins) → the global case's retained result list ✅ → `Compiled` per pattern **measured, no policy shipped** → *then* consider compiling `Broiler.Regex` | L | RegExp, plus PdfJS and Typescript | Octane regex corpus profiled **before** any rewrite — **satisfied**, and it re-ordered the phase |

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

### The front-end probes (phase 1)

Both take the same benchmarks host as every other emitter above. `--compile-profile` needs
an Octane checkout, because the shapes that matter here — hundreds of sibling declarations,
one IIFE holding hundreds of nested functions — do not occur in hand-written test sources;
`--compile-scaling` generates its own, since its job is to vary one property at a time.

```bash
cd Broiler.JS/Broiler.JS
DLL=benchmarks/Broiler.JavaScript.Engine.Benchmarks/bin/Release/net10.0/Broiler.JavaScript.Engine.Benchmarks.dll

# How much of each corpus's compile is function bodies (sizes item 1-1).
# Third argument is repetitions; the report is a median. Mandreel dominates the runtime.
dotnet $DLL --compile-profile /path/to/octane 3

# Parse / expression-tree / IL-emission split, against declaration count and name length
# (this is what found item 1-4). Streams a row per shape to stderr as it completes.
dotnet $DLL --compile-scaling
```

`--compile-profile` builds its control by replacing every outermost function body with `{}`
and **re-parses it before timing anything** — a control the parser rejects would measure
failing early rather than compiling less. Set `BROILER_COMPILE_PROFILE_DUMP=<dir>` to write
each control out; that is how Mandreel's residue was read.

To A/B item 1-4 on a single build, `BROILER_JS_REWRITER_INDEX_THRESHOLD` sets the scope size
above which the closure rewrite indexes instead of scanning (default 32). Any value larger
than a real scope restores the pre-1-4 linear scan, so the two arms differ in nothing else:

```bash
BROILER_JS_REWRITER_INDEX_THRESHOLD=1000000000 dotnet $DLL --compile-profile /path/to/octane 1
```

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
| §4.1 phase E | P2-1, P2-2 (+ engine §6.5 array defects) | — | Implemented, not closed. **P2-2 item 3 shipped a wrong-answer bug**, found and fixed while working 3-3's successor: two writes to a numeric local were invisible to the analysis proving it numeric — a `var` re-declared in a nested statement, and any name bound through an object destructuring pattern. The first returned NaN; the second aborted compilation of the whole script with an unhandled `NotImplementedException`. See 3-3 |
| §4.1 phase F | P2-3, P2-4, P3 | — | Implemented, not closed |
| 0-1 … 0-5 | — | 0-1 … 0-5 | Implemented |
| 0-6 | — | Octane §2.6 | **Owed** |
| 0-7, 0-8, 0-9 | engine §8.1 acceptance evidence | — | **Owed** |
| 0-10, 0-11 | engine §8.1, §8.2 | — | Done |
| 1-1, 1-3 | *excluded by engine §9* | 1-1, 1-3 | Open — superseded, see §1.1. **Both now have their measurement**: 1-1's prize is 92-96% of compile time on the large real programs but its stated Mandreel target was never a 1-1 case, and 1-3's three-way split reads parse 0.5% / tree 11% / **emission 89%**, so 1-3 is an emitter item |
| 1-4 | — (found measuring 1-1's premise) | — | **Landed** — the closure rewrite's per-lambda scope was a `List` asked `Contains` per parameter reference, so IL emission was **quadratic in a scope's binding count**. A reference-keyed multiset, list-backed below 32 bindings: **28.5×** on 2 000 top-level declarations, **3.04× on Mandreel** end-to-end (ABBA, six pairs), inside noise on the narrow-scope corpora. Carried as a patch, not a pointer bump — see [`patches/README.md`](../patches/README.md) |
| 1-2 mitigation | *excluded by engine §9* | 1-2 | **Landed** — `43bc4230`, in the pinned pointer |
| 1-2 real fix | — | 1-2 | **Landed on all three recursing passes.** `StackGuard` was repaired and put on `AstMapVisitor.Visit`; `FastParser.Expression` is now guarded too, which was the last one — its descent aborted the process at 25 000 nesting levels in the DEFAULT configuration and now survives 90 000, median paired ratio 0.9993. The four-way matrix's "mitigation off / guard on" row is a **linux-x64** statement: on win-x64 the front end compiles in place on ~1 MiB while the threshold is 4 MiB, so no segmenter can fire there |
| 2-0 | — (P1-2's guard, reached in a state it cannot recognise) | — | **Landed** — `2df877a0`, in the pinned pointer |
| 2-1 | P1-3 remainder | 2-1 | **Landed** — `5d31617a`, in the pinned pointer; **test262 owed** |
| 2-4 | P1-3 remainder | 2-4 | **Landed, both halves** — `f9c2193f` (`o.x++`) and `c5842c9d` (`o.x op= rhs`), both in the pinned pointer; computed keys, `super`, optional chains, private names and the three short-circuiting compound forms stay out on purpose |
| 2-2 | P1-4 remainder | 2-2 | **Landed for arrays** — `641241af`, in the pinned pointer; its four named benchmarks were the wrong targets |
| 2-8 | — (the blocked half of 2-2) | — | **Landed** — `850121a0`, in the pinned pointer; both prerequisites fixed. **Shipped a regression that broke DeltaBlue** (a cached store to `f.prototype` bypassed `JSFunction`'s cached-field sync); the gate that fixes it is folded into the same commit |
| 2-3 | P1-4 remainder | 2-3 | **Closed** — measured twice. Not a pure removal, ~3% throughput ceiling, and after 2-7 its own proposal is worth 1.0-4.3% of per-property object bytes. Its premise is also wrong: shape slots admit non-default attributes, which are per-object data a shared shape cannot hold |
| 2-9 | — (found closing 2-3) | — | **Landed** — shape-tracked properties no longer live in the radix trie; it is written only when something needs a real descriptor. A three-field object is **0.36x**, an eight-field one **0.15x**, against +8 B on every object; over an Octane run **16.2 M property maps become 2.5 M**. All 22 cache rows byte-identical; test262 unchanged across all four manifests; Octane 14/15 with the fifteenth confirmed pre-existing against a control |
| 2-7 | — (found measuring 2-3) | — | **Landed** — `55c6b1fb` (the measurement) and `a6f101cc` (the policy), both in the pinned pointer. 43.9% of 47 M real maps never outgrow one four-node group; live map bytes 0.56x, allocated 0.82x, Typescript 0.92x |
| 2-5 | P0-2 remainder | 2-5 | **Closed** — measured at 0%; P0-2 had already taken the cost, and 2-1 narrowed what was left |
| 2-6 | — | 2-6 | **Folded into 4-1** — no callee resolution to cache; a call costs ~250 ns and a call-site cache removes none of it |
| 3-0 | — (found measuring 3-1) | — | **Landed, both halves** — an indexed access boxed its index. A read now allocates **0.00 B/element** against 31.67 and a write loses ~32 B; write-once-read-once goes 0.46x for a numeric element and 0.25x for a reference one. Compound assignment keeps its boxed index, on purpose |
| 3-1 | — | 3-1 | **Open, re-specified on a measurement** — trades 32 B of write allocation for 32 B of read allocation, so it is a live-memory item (a resident `double[1e6]` ~0.2x) whose throughput case is contingent on 3-4 |
| 3-2 | — | 3-2 | Open |
| 3-3 | P2-2 item 3 remainder | 3-3 | **Parameters landed; `let`/`const` and block `var` open and re-ranked ahead of them.** Measured before starting, and the item was right about the target and wrong about the tier: a parameter was excluded from the *scalar* gate, not the numeric one, so it allocated a `JSVariable` cell on every call — **56 B per parameter, a three-parameter call 230.2 → 62.2 B**. The numeric tier cannot be widened to parameters at all, because the caller picks the type; that is phase 4. All four ineligible categories cost the same per site, so the item's ordering was never a cost claim |
| 3-4 | — (`tagged-js-value` in ownership.json) | 3-4 | Cost, do not start |
| 4-1 … 4-4 | *excluded by engine §9* | 4-1 … 4-4 | Open — superseded, see §1.1. **4-3's design is written**: the item asked for V8-style frame reconstruction, which this engine cannot express (tier-1 locals are CLR locals of an IL method, and `CallFrame` carries no JavaScript values). Re-specified as restart (shipping in the pilot) plus an in-method fallback branch |
| 5 | — | Octane §7 "regex, until late" | **Profiled — gate satisfied, phase re-specified.** `Matching/Matcher.cs` is not on the Octane path at all (only semantic-gap patterns route to it); the default engine is .NET's, built without `RegexOptions.Compiled`. B5's ranking of the closure matcher was never checked against the routing |
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
`cdb2fd41`; Octane code sites at `45f4f679`. Phase 2 worked and measured 2026-08-01/02 at
pointer `685026c0` plus the then-pending `0050`–`0058`, since applied and pinned as
`a6f101cc`; status summary in §0._

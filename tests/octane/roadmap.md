# Roadmap: smoothing the Octane profile

> **Superseded — this is an archive, not the plan.** It was merged into
> [`docs/performance-roadmap.md`](../../docs/performance-roadmap.md) on 2026-08-01,
> which is the plan of record and carries the engine-internal campaign alongside it.
> Items here have since been corrected there and **the corrections are not
> back-ported**: item 1-2 below, for instance, still names source *size* as the cause
> of the compiler's stack overflow and still cites a Mandreel failure that does not
> reproduce on linux-x64. Read this only for what the Octane suite contributed to the
> merge; take the plan, the diagnoses and the acceptance criteria from the merged
> document. [`benchmarks.md`](benchmarks.md) remains the live per-benchmark reference.

Companion to [`benchmarks.md`](benchmarks.md), which describes what each
benchmark does and where Broiler's time goes. This document is the plan that
follows from it.

**"Smooth" is used in two senses here, and both are goals:**

1. **A smooth run** — all 17 scores, every run, no crashes, no timeouts, and a
   known noise band. **Phase 0 (§2) is implemented**; it owes only a workflow run
   to produce the baseline everything else is measured against.
2. **A smooth curve** — the per-suite deficit currently spans **45× to 4646×**, a
   ~100× spread. That spread is the finding: it says the losses are concentrated
   in two subsystems rather than spread evenly, and therefore that they are
   addressable in a defined order.

The second point is the whole argument for this plan. Because the suite total is
a **geometric mean**, flattening the curve and raising the total are the same
work: moving MandreelLatency from 14.5 to 1000 is worth more to the total than
tripling every score that is already above 300.

> **Scope discipline.** Octane was retired by its authors in 2017 precisely
> because engines began optimizing for its shapes. Every item below is justified
> by a *mechanism* that matters to real JavaScript, with the benchmark used as
> evidence that the mechanism is missing — never as the target. Items that would
> only move Octane are called out and excluded in [§7](#7-non-goals).

---

## 1. The metric

Track three numbers per run, not one:

| Metric | Last committed run | Target |
|---|---|---|
| **Geomean** over all 17 scores | 244 (est. with `0046`; 245 over the 12 that complete) | — |
| **Scores reported** | 12 / 17 | **17 / 17** |
| **Spread** = worst suite ÷ best suite, measured as ×-slower-than-Chromium | 4646 / 45 ≈ **103×** | **< 5×** |

The spread is the "smoothness" number and it is the one this roadmap is
organized around. A run where every suite is uniformly 150× off is a far
healthier engine than today's, at a similar geomean, because it means no single
subsystem is pathological.

All three are now emitted by `run-octane.mjs` into `results/comparison.md` and
`comparison.json` (§2.4), so the trend comes out of the run rather than being
reconstructed by hand. The "last committed run" column above is stale in the way
§2.1 describes and will be superseded by the first Phase 0 gate run.

---

## 2. Phase 0 — make the run complete and repeatable — **implemented**

**Nothing else on this list can be measured until this is done.** Phase 0 was
mostly not engineering: most of the code already existed, and the item that
looked like the biggest blocker turned out to be already finished.

Status at 2026-08-01: **0-1 to 0-5 are done.** What Phase 0 still owes is the
one thing it cannot do from a checkout — a workflow run (§2.6).

### 0-1 · Land the pending `Broiler.JS` patches — **already landed**

An earlier draft of this roadmap claimed the three pending patches were blocked
on egress scope and that the pinned pointer did not carry them. **That was
wrong**, and the correction matters because it changes what the committed
results mean.

The pinned pointer **is** `cdb2fd41`, which *is* patch 0048's commit, and both
`7ef80c03` (0046) and `8228b0da` (0047) are its ancestors:

```sh
git ls-tree HEAD Broiler.JS                                  # → cdb2fd41
git -C Broiler.JS merge-base --is-ancestor 7ef80c03 cdb2fd41  # → yes
git -C Broiler.JS merge-base --is-ancestor 8228b0da cdb2fd41  # → yes
```

The pointer was bumped in `2d9f39ca` on **2026-08-01 11:45**. The committed
Octane results were generated **2026-07-31 20:28** — about 15 hours earlier. So
the five failures in `results/` are a **stale result set, not a stale pointer**.

The three patch files and their index rows have been deleted from `patches/`,
per that directory's own instruction to remove a patch once its pointer is
bumped; a short "recently cleared" table records what landed where.

Independent confirmation that the landing was clean: `ff819e06` refreshed
`tests/wpt-baseline/failed-tests.json` right after the bump, for a **net 36
fewer WPT failures** (50 removed, 14 added). Those patches changed `+`, `==`,
the `for` head and `eval` scoping, which is exactly the surface WPT exercises
indirectly — and the surface moved the right way.

**Remaining action: none in the tree.** See §2.6.

### 0-2 · Make the stack reserve the default in the shell — **already on**

Verified rather than changed. `Broiler.JavaScript/Program.cs` runs script-host
JavaScript on a thread it sizes itself and opts into the budget explicitly:

```csharp
private const int ScriptHostStackBytes = 16 * 1024 * 1024;
…
MaxStackUsageBytes = ScriptHostStackBytes - ScriptHostStackReserveBytes,
```

So the reserve is active in exactly the configuration the Octane workflow builds.
`JSContextOptions.MaxStackUsageBytes` still defaults to **0 (disabled)** for
embedders, which is correct: a host that does not control its JavaScript thread's
stack size cannot pick a number.

Why this matters beyond Crypto: Octane's harness is literally
`catch (e) { suite.NotifyError(e) }`, and .NET runs a catch handler as a funclet
*on top of* the frames it is handling. Without a reserve the handler has no
stack, its first call throws again, and the second throw escapes the `try` — so
**any** benchmark that overflows takes its whole suite's other benchmarks with
it.

### 0-3 · Record each suite's real time budget — **implemented**

`scripts/octane-suites.json` entries now accept an optional `timeoutSec`, and
`--timeout` became a **floor** rather than an override
(`suiteTimeoutSec()` in `run-octane.mjs` returns `max(global, suite)`), so a
debugging run can still widen everything at once without editing the manifest.

Set only where a measured duration needs it, at roughly 3× the observed time:

| Suite | Measured under Broiler | Budget |
|---|--:|--:|
| Mandreel | 313 s | 1200 s |
| zlib | 647 s | 1800 s |

Every other suite fits inside the 180 s default, and Chromium finishes all of
them in about 2 s. Before this, a local `--only Mandreel` run at defaults
reported a spurious `timeout`, and the full run only passed because CI was
overriding the global timeout to 1800 s — which also meant a genuine hang
anywhere else had 30 minutes to look like work.

The budget a suite ran under is now written into its log and its status record,
so a `timeout` verdict can be read without reconstructing the invocation.

### 0-4 · Quantify run-to-run noise — **implemented**

Every score in `results/` came from a **single run**, and the phases below will
be judged on 20–50% deltas. There was no basis for calling any delta real.

`--repetitions <n>` (default 1) now runs each suite n times and reports the
**median** score per benchmark plus the observed **spread**
(`(max − min) / median`, as a percentage). `--noise-band <pct>` (default 7.5,
matching the baseline profile in `eng/performance/phase0.json`) sets the
threshold above which a benchmark is flagged `⚠` in `comparison.md`. Both are
plumbed through `run-octane-benchmarks.sh` and exposed as a workflow input.

Three decisions worth knowing:

- **A default run is unchanged, byte for byte.** With one repetition the median
  is the sample, no stability data is emitted, no spread column appears, and the
  log keeps its `<suite>.log` name.
- **Each repetition keeps its own log** (`<suite>.rep1.log`, …), so a flake keeps
  the evidence of the run that failed instead of having it overwritten by the run
  that passed.
- **A suite is `ok` only if it was `ok` every time.** Anything else reports the
  first bad run and records `statusPerRepetition`; a suite that mixes verdicts is
  marked `flaky`. Averaging a flake into a pass is the failure mode this whole
  harness exists to avoid.

`comparison.md` now also leads with the three numbers from §1 — scores reported
out of the expected total, geomean, and the **spread** between the best and worst
suite — so the smoothness metric is produced by the run rather than computed by
hand afterwards.

Expect the two latency scores to be the noisy ones, and treat that as data: a
wide band on SplayLatency is itself a pause-distribution result.

### 0-5 · Check the code cache against CodeLoad's intent — **checked; no problem**

CodeLoad `eval`s the same jQuery and Closure source repeatedly, and the engine
has a `DictionaryCodeCache`. If that cache were hit across iterations the score
would be measuring cache lookup rather than compilation, and every Phase 1 number
taken from it would be meaningless.

It is not installed. In `Broiler.JavaScript/Program.cs` the line is present but
commented out:

```csharp
// DictionaryCodeCache.Current = new AssemblyCodeCache();
```

So `--script-host` compiles from source every time and **CodeLoad is a genuine
compile-throughput measurement**. Phase 1 can be judged on it directly.

Worth re-checking if that line is ever uncommented — the shell would then be
measuring something else, and this is not the kind of change that announces
itself in a benchmark score.

### 2.6 · What Phase 0 still owes: a run

Everything above is in the tree. The gate is not, because it cannot be produced
from a checkout:

**Run the Octane workflow and commit the refreshed results.** Until then the
committed numbers describe an engine that no longer exists, and the geomean, the
coverage count and the spread in §1 are all quoting a superseded run.

```text
Actions → Octane Benchmarks → Run workflow
  engines:         chromium,broiler
  timeout_seconds: 180          # Mandreel and zlib now raise their own
  repetitions:     3            # the first run that can distinguish signal
```

**Exit gate for Phase 0:**

1. **17 of 17 scores reported** — the five previously failing suites complete.
2. **No `timeout` status** at the default 180 s floor.
3. **A per-suite noise band on record**, and the suites that exceed it named.
4. `comparison.md` reporting coverage, geomean and spread.

Only then is there a baseline that Phases 1–4 can be measured against.

## 3. Phase 1 — the front end

**Targets: MandreelLatency (4646×), CodeLoad (371×), Mandreel (300×).** Owns the
two worst scores in the suite outright, and is the item with the clearest value
outside Octane: this is page-load time.

Owner assemblies: `Broiler.JavaScript.Parser`, `.Compiler`, `.BuiltIns`.

### 1-1 · Lazy function compilation — *the single highest-leverage item*

**Target.** CodeLoad and MandreelLatency are *designed* so this is the dominant
term — jQuery defines thousands of functions and calls almost none of them. A
large multiple on both is the expected outcome; if it is not, the measurement is
wrong before the change is. Mandreel's 313 s should fall substantially, and
Typescript and PdfJS should improve on load. **Steady-state execution does not
change at all** — do not expect Richards or DeltaBlue to move.

**Where.**

| File | Role |
|---|---|
| `Broiler.JavaScript.Parser/FastParser.Function.cs` | where a body is parsed today; needs a skip-with-errors mode |
| `Broiler.JavaScript.Compiler/Declarations/FastCompiler.CreateFunction.cs` | where the body is compiled eagerly |
| `Broiler.JavaScript.BuiltIns/Function/JSFunction.cs` | already carries `source` and already recompiles from it for tiering — the raw material for deferring is present |
| `Broiler.JavaScript.Engine` code cache | keyed on whole scripts; needs to key on function spans |

**Work.**

1. Pre-parse a function body far enough to find its extent and binding structure,
   without generating code.
2. Record source span + captured scope on the `JSFunction`.
3. Compile on first invocation, memoized per function-span.
4. Force eager treatment for the cases in *Risk* below.

**Risk — all four are spec-visible, and the first is the bulk of the work.**

- **Early errors must stay eager.** A syntax error inside a never-called function
  is still a `SyntaxError` at parse time. The pre-parser has to be a real parser
  for error purposes while skipping code generation. This is the part most
  likely to regress test262.
- **Scope capture.** A deferred body must compile against the scope chain as it
  was at closure creation, not at first call.
- **Direct `eval`** inside a deferred body can introduce bindings into enclosing
  scopes. The pre-parser must detect it and opt that function out.
- **Generators and async bodies** suspend mid-frame; confirm deferral composes
  with the `GeneratorRewriter` before assuming it does.

**Verify.** Full test262 over the four pinned manifests with **no new failure and
no new timeout** — the local suite is not sufficient for an early-error change.
Plus `ParserCompilerBenchmarks` before/after, and a CodeLoad number taken with
the code cache confirmed off (§2.5).

**Size: XL.** The only item here that is a genuine sub-project.

### 1-2 · Stop AST-recursive compilation from overflowing

**Target.** Mandreel, which today can die outright: `global_init` is one
generated function of **152,948 lines**, and compiling it has been observed to
overflow the CLR stack with a JavaScript stack only eight frames deep — the
compiler recursing over the AST, not the program recursing.

**Where.** `Broiler.JavaScript.Compiler/FastCompiler*.cs` visitors;
`Broiler.JavaScript/Program.cs` for the mitigation.

**Work.** Two steps, and the first is worth landing on its own:

1. **Mitigation (S).** Compile on a thread with a chosen stack size, exactly as
   the shell already does for *execution* (`ScriptHostStackBytes`, 16 MiB). Turns
   a crash into a slow success.
2. **Real fix (M).** An explicit worklist in the visitor for the shapes that nest
   without bound — long statement lists, deep binary-expression chains, giant
   `switch`. Compiler stack depth should be a function of source *nesting*, not
   source *size*.

**Verify.** A generated 200k-line single-function script compiles at the default
shell stack size without overflow. Add it as a compiler test fixture — this is
exactly the kind of thing that silently regresses.

### 1-3 · Reduce compile cost per byte — *only after 1-1*

**Do not start here.** If 1-1 lands, most source is never compiled at all and
the remaining throughput may not justify a pipeline change.

**Work.** Measure first, with the existing `ParserCompilerBenchmarks`, splitting
the cost three ways: parse, expression-tree construction, IL emission. The
measurement names the target; committing to one now would be guessing.

**Size: unknown by construction.** Re-scope after 1-1's numbers land.

---

## 4. Phase 2 — the call and property paths

**Targets: DeltaBlue (601×), Richards (433×), Box2D (170×).** These three are
dominated by the cost of making a call and reading a property. Every item below
is already named as open in
[`Broiler.JS/docs/performance-roadmap.md` §8.1](../../Broiler.JS/docs/performance-roadmap.md)
— this phase is a set of contained changes to structures that already exist and
already work on the sites they cover. **Best effort-to-value ratio on the list
after Phase 1.**

Owner assemblies: `Broiler.JavaScript.Runtime`, `.Compiler`, `.Engine`.

| # | Item | Where | Why it matters here | Size |
|---|---|---|---|---|
| **2-1** | **Shape-transition cache** — an `oldShapeId → (newShape, slot)` entry. Absent entirely: there is no such map anywhere in `Runtime` | `Runtime/ObjectShape.cs`, `Runtime/JSObject.PropertyStorage.cs` | *Creating* a property misses every time, so every constructor that builds an object field-by-field misses on **every field**. Richards' `TaskControlBlock`, DeltaBlue's constraints, RayTrace's `Vector`, Box2D's `b2Vec2` are all exactly this shape | M |
| **2-2** | **Widen shape eligibility** past `GetType() == typeof(JSObject)` | `Runtime/JSObject.cs` — `TryGetShapeSlot` | `JSArray`, `JSFunction` and every built-in exotic are excluded from shape tracking wholesale. **Start with `JSArray`** — it is on the hot path of five benchmarks | M |
| **2-3** | **Remove the double storage** | `Runtime/JSObject.cs` — `TrackShapeDataProperty` | Every tracked object writes each value into `shapeSlots` *and* the `PropertySequence`, storing twice and paying to keep them in sync. Pure removal | S |
| **2-4** | **Extend the store cache** to `o.x++`, `o.x += 1`, computed keys, `super`, optional chains, private names | `.Compiler` lowering; `Runtime/ObjectShape.cs` | All keep the old uncached lowering. `o.x++` measured the most expensive of them and is pervasive in Gameboy and Box2D | M |
| **2-5** | **Get strictness off the property-write path** | `Engine/Core/JSEngine.cs:223`; `JSValue` set accessors | P0-2 removed the redundant *writes*, but set accessors still **resolve** an `AsyncLocal<bool>` per write. The preferred fix — thread the compiler's static knowledge into the emitted set helpers so the hot path reads nothing — is not started | M |
| **2-6** | **Monomorphic call-site caching** | `BuiltIns/Function/JSFunction.cs` — `InvokeFunction`, `SelectInvocationDelegate` | Callee resolution repeats per call. **Prerequisite for inlining in Phase 4** | M |

**Sequence.** 2-1 first (largest single win, and it is the missing half of a
structure that otherwise works), then 2-3 (pure removal, near-zero risk), then
2-2, 2-4, 2-5, 2-6.

**Verify — per item, not per phase.**

- An `eng/performance/ownership.json` entry naming its benchmark and semantic
  owner. The file is item-scoped and already has fifteen such entries; match that
  granularity.
- Coverage in `PropertyShapeCacheTests` / `PropertyStoreCacheTests` for every
  invalidation path: `setPrototypeOf`, prototype mutation, own-property
  shadowing, `delete`, freeze, accessor redefinition, polymorphic and megamorphic
  sites.
- **P1-1 already touches `OrdinarySetWithOwnDescriptor`, the single most
  spec-sensitive path in the engine, and 2-1 to 2-4 touch it again.** test262
  over `test262-properties-proxy` and `test262-strict-mode` is not optional here.

**Exit criterion: DeltaBlue and Richards inside 200×.** They are the outliers on
a curve whose median is ~180×, and this phase is the reason they are.

---

## 5. Phase 3 — value representation

**Targets: Crypto (301×), zlib (340×), RayTrace (291×), EarleyBoyer (270×),
Splay (152×), NavierStokes (104×).** The largest total win in the plan and the
largest change. Deliberately after Phases 1 and 2 because those are contained
and this is not.

The root fact: `JSValue` is `public abstract partial class JSValue` — a CLR
reference type. There is no tagged-value representation, so a number that leaves
a local becomes a heap allocation. Baseline: integer arithmetic allocated
**128 bytes per iteration**; an empty `for` loop, 96.

Owner assemblies: `Broiler.JavaScript.Storage`, `.Runtime`, `.Compiler`.

### 3-1 · Unboxed backing stores for dense arrays — **start here**

**Where.** `Broiler.JavaScript.Storage/ElementArray.cs` — `private IPropertyValue[] dense`.

P2-3 made each element one reference instead of a 32-byte descriptor, which was
a real win, but a dense array of a million doubles is still a million heap
objects behind a million interface references.

**Work.** A typed backing store (`double[]`, `int[]`) chosen on first store, with
an elements-kind tag on `ElementArray`, transitioning to `IPropertyValue[]` on
the first non-numeric write. Standard, well-understood machinery.

**Target.** Crypto's 28-bit digit arrays, NavierStokes' grids, and the
typed-array-shaped heaps in zlib, Mandreel and Gameboy. **The most contained item
in the phase and the one covering the most benchmarks** — which is why it goes
first.

**Verify.** `test262-arrays` and `test262-binary-data`;
`CompactElementStorageTests`, `ElementDescriptorRoundTripTests`,
`IndexedWriteAndLengthTests` for integrity levels, foreign receivers, exotics and
length-shrink. Report allocation per element alongside time.

**Size: L.**

### 3-2 · Unboxed doubles in shape slots

The object-field twin of 3-1: `shapeSlots` holds `JSValue` references, so
`vector.x = 1.5` allocates. This is what RayTrace and Box2D need, and it
**composes with 2-1** — a shape that knows a slot is a double can store it raw,
so land 2-1 first and this gets cheaper.

**Where.** `Runtime/JSObject.cs`, `Runtime/ObjectShape.cs`. **Size: L.**

### 3-3 · Widen the unboxed-locals eligibility gate

P2-2 item 3 currently covers a function-top-level `var` not named by any nested
closure. Named as open in §8.1: **function parameters**, `let`/`const` (needs TDZ
analysis), and `var` declared inside a block or loop body (needs
definite-assignment analysis).

**Parameters are the valuable one** — every numeric helper takes them, and every
Octane benchmark is full of numeric helpers. Do parameters first and treat the
other two as separate items.

**Where.** `Broiler.JavaScript.Compiler` — the P2-2 eligibility gate.
**Watch:** patch 0047 exists because this codegen path produced invalid IL when
an unboxed local reached value position. Widening the gate widens that exposure;
`InvalidProgramException` is the failure signature to test for. **Size: M.**

### 3-4 · A tagged value representation — *scope and cost, do not start*

The real fix, and a multi-quarter redesign of the engine's most fundamental type
with every built-in downstream of it.

**Write it up and cost it at the end of Phase 3**, once 3-1 to 3-3 have shown how
much of the gap survives unboxed arrays, fields and locals. It is entirely
possible the answer is "less than expected", and that is worth knowing *before*
committing to the redesign rather than after. **Size: XL.**

---

## 6. Phase 4 — speculation

**Target: everything, and it is the difference between ~100× and ~10×.**

The most speculative part of the plan in both senses. Two findings make it more
tractable than it looks.

**The tiering scaffolding already exists and is general.**
`Runtime/FunctionTiering.cs` has `FunctionTieringController` with an invocation
threshold, a per-realm budget, a retained-code cap, delegate replacement, and
`RecordDeoptimization` counters, gated behind `JSContextOptions.FunctionTiering`
(disabled by default).

**But there is no optimizing compiler behind it.**
`JSFunction.RecompileForTiering` with `numericPlan == null` re-runs
`CoreScript.Compile` on `({source})` with a one-shot cache — it recompiles *the
same code the same way*, so it cannot be faster. The only real specialization is
the `NumericLoopPlan` path. **Tier-2 today is a hook, not a tier.**

That is a good position: the bookkeeping, budget and safety-fallback policy are
built and tested; what is missing is the part that makes entering tier-2 worth
anything.

| # | Item | Where | Note | Size |
|---|---|---|---|---|
| **4-3** | **Deoptimization** — **do this first** | `Runtime/FunctionTiering.cs`, `Engine/CallFrames.cs` | The safety net that makes everything else legal. Must bail out **mid-function** when a guard fails; the current model can only swap the delegate for the *next* call. This is the gating item for the entire phase | XL |
| **4-1** | **Type feedback collection** | `Runtime/ObjectShape.cs`, `.Compiler` sites | The inline caches already observe shapes at property sites. Extend to record and retain observed shapes, callee identities, and numeric-vs-generic outcomes per site | L |
| **4-2** | **A specializing tier-2 compile** | `BuiltIns/Function/JSFunction.cs` — replace the `numericPlan == null` branch | Consume 4-1's feedback: monomorphic property access → shape check plus direct slot read; arithmetic → raw `double`/`int` where feedback says so | XL |
| **4-4** | **Inlining of small JS callees** at monomorphic sites | `.Compiler` | What Richards and DeltaBlue actually need. Strictly downstream of 4-3, 4-1, 4-2, and of **2-6** | XL |

**Do not start 4-2 before 4-3 has a design.** Speculation without a mid-function
bailout is either unsound or restricted to functions with no observable side
effect before the guard — which excludes everything worth optimizing.

**Verify.** Deopt correctness before any speculation ships: a test that forces
every guard to fail at every point in a function body and asserts the fallback
produces the interpreter's answer. Then the full test262 matrix — this phase can
break anything.

---

## 7. Non-goals

Stated explicitly so effort does not drift into them.

- **GC work.** SplayLatency at 45× is the *best* result in the suite and Splay's
  throughput at 152× beats the median. The .NET collector is handling a workload
  it was never tuned for well. The allocation **rate** is a severe problem — that
  is Phase 3, and it is a problem with what the engine asks the collector to do,
  not with the collector.
- **asm.js or WebAssembly special-casing** for Mandreel and zlib. Recognizing
  asm.js type annotations would move two scores and is exactly the
  optimize-for-the-benchmark behaviour that got Octane retired. Phases 3 and 4
  reach the same code through general mechanisms.
- **Regex, until late.** `Broiler.Regex`'s backtracking interpreter costs one
  score, measured against Octane's *lowest* reference baseline. When it is
  reached: profile `Matching/Matcher.cs` against the Octane corpus first to
  separate backtracking strategy from per-step interpretive overhead, then
  compile the common subset (literal prefixes, character classes, bounded
  quantifiers) with the interpreter as fallback. It also sits on PdfJS's and
  Typescript's paths, so its value is larger than its one score suggests.
- **Chasing the geomean directly.** If a change raises the total without raising
  the worst scores, it has not smoothed anything.

---

## 8. Sequencing

| Phase | Order within it | Size | Unblocks / expected effect | Exit gate |
|---|---|---|---|---|
| **0** ✅ | 0-1 … 0-5 **implemented** | — | Everything. 12 → **17 scores**, known noise band | Owes only a workflow run: all 17 scores, no timeout at the 180 s floor, per-suite band on record (§2.6) |
| **1** | 1-2 mitigation → **1-1** → 1-2 real fix → 1-3 measure | XL | The two worst scores in the suite; page-load time generally | test262 over the four pinned manifests, no new failure **and no new timeout**; MandreelLatency and CodeLoad out of the tail |
| **2** | **2-1** → 2-3 → 2-2 → 2-4 → 2-5 → 2-6 | M each | The Richards/DeltaBlue/Box2D cluster | An ownership entry and owned tests **per item**; test262 properties/strict-mode; **DeltaBlue and Richards inside 200×** |
| **3** | **3-1** → 3-3 → 3-2, then *cost* 3-4 | L–XL | Uniform lift across arithmetic and allocation-heavy suites | `test262-arrays`, `test262-binary-data`; allocation reported per item alongside time |
| **4** | **4-3 design first** → 4-1 → 4-2 → 4-4 | XL | The remaining order of magnitude | Deopt correctness proven before any speculation ships; full test262 matrix |
| **5** | profile → compile the common subset | L | RegExp, plus PdfJS and Typescript | Octane regex corpus profiled **before** any rewrite |

**Dependencies.** Phases 1 and 2 are independent of each other and of Phase 5,
and can run in parallel. 3-2 is cheaper after 2-1. Phase 4 depends on 2-6
(4-4), on 4-3 (everything else in the phase), and benefits from 3-1/3-2 having
established unboxed representations for it to speculate into.

**The bolded item in each phase is the one to start with**, and in three of the
four it is not the one that sounds most important: 1-1 over 1-3, 2-1 over 2-6,
4-3 over 4-2. Each of those orderings is argued where the item is described.

**Every phase closes under [`Broiler.JS/docs/performance.md`](../../Broiler.JS/docs/performance.md)**,
unchanged: two runs inside the configured band, on the release RID matrix
(win-x64, linux-x64, linux-arm64), reporting time, allocation and working set
together, with an `eng/performance/ownership.json` entry naming each item's
benchmark and semantic owner. Note that the existing roadmap's phases A–F are all
*implemented* and none is *closed* for exactly this reason — the RID-matrix and
BenchmarkDotNet rows are still owed there, and this plan should not add to that
debt.

**A standing warning from the existing roadmap, which applies to every phase
here:** P3's premise — that the scope machinery around every call was the cost —
was built, measured, and disproved; the real cost was an 80-byte activation
record it was hiding. Measure before implementing, and be willing to throw the
implementation away.

---

_Sources: [`benchmarks.md`](benchmarks.md), [`results/`](results/),
[`Broiler.JS/docs/performance-roadmap.md`](../../Broiler.JS/docs/performance-roadmap.md),
[`Broiler.JS/docs/performance.md`](../../Broiler.JS/docs/performance.md),
[`patches/README.md`](../../patches/README.md). Code sites verified against the
`Broiler.JS` checkout at `45f4f679`._

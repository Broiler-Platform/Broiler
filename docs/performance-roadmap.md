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
- **Provenance:** the pinned submodule pointer is **`cca39b4d`**, checked 2026-08-05 against
  the gitlink rather than against the prose — **and checking it is what caught that this line
  said `07adeb44`**, which the pointer had moved five commits past. That is the *fifth*
  consecutive time this line was stale when read (`2ebc0c3c`, `71dda1b7`, `9bf9639b` and
  `61c8cc65` before that), which is no longer a coincidence: **a pointer written into prose goes
  stale silently**, and the only reliable reading of it is `git submodule status`. Five readings,
  five staleness findings, is a rate rather than an anecdote — the line is wrong *by default*, so
  the sentence to write next to any pointer is the command that reads it.
  It is why §4.1's and §3.4's figures below carry the
  commit they were taken at rather than "the pin". `2ebc0c3c`, `a6f101cc`, `71dda1b7`,
  `685026c0`, `cdb2fd41`, `7ef80c03` and `8228b0da` are all **ancestors** of the current pin
  (`merge-base --is-ancestor`), so nothing recorded against any of them is invalidated and item
  0-1's substance holds; `685026c0` carries item 0-9's probe corpus (`aa2b1562`, #938).
  **The patch handoff has completed again, twice over.** The five files pending at the last
  reading (`0067`–`0071`, items 3-3's two halves, 4-1, 4-3a and 4-3b) **and the six written after
  them** (`0072`–`0077`, items 4-2a, 4-2b, 4-4's premise, 4-5, 3-5 and 3-6) have all been applied,
  pushed and the pointer bumped. The six were checked **patch by patch against the submodule log
  rather than inferred from this prose** — each patch's `Subject` matched to a commit, that
  commit's `format-patch` output diffed against the patch file (identical modulo line endings and
  the `[PATCH n/m]` numbering), and each commit confirmed an ancestor of the pin: `3f8d5db4`,
  `34270c76`, `af5b8b78`, `53690423`, `8073e4fb`, `61c8cc65` in patch order. **So every figure
  recorded for items 4-2, 4-4, 4-5, 3-5 and 3-6 now describes the pinned pointer directly**,
  rather than a local build plus a patch series applied in order, which is what their sections
  used to have to say.
  **The four patches pending at the last reading have been applied and the pointer bumped.**
  `0078`–`0081` (items 3-7, 3-8, 3-1 and 3-2) are now `37905aeb`, `14ac195f`, `cb2e63c6` and
  `07adeb44` — matched patch by patch to the submodule log rather than inferred from this prose,
  and `61c8cc65` is an ancestor of the pin, so every figure recorded for those four items now
  describes the pinned pointer directly rather than a local build plus a patch series.
  **The patch handoff has completed a third time, and `patches/` is empty.** The five files pending
  at the last reading — `0082`–`0086`, item 1-1's remaining half and item 3-1's four — have all been
  applied, pushed and the pointer bumped, which is what moved it off `07adeb44`. They were checked
  **patch by patch against the submodule log rather than inferred from this prose**: each `Subject`
  resolved to a commit, that commit's `format-patch` output diffed against the patch file (identical
  once the `From <sha>` line, the blob `index` lines and the trailing git version are set aside —
  that is the whole of the difference on all five), and `07adeb44` confirmed an ancestor of the new
  pin. In patch order they are **`0aa8a558`, `9e5b57d3`, `0dda32b2`, `23fc8fb9`, `cca39b4d`**.
  **So every figure recorded for items 1-1's remaining half and 3-1's census, guarded tree,
  boxing-source census and `ToNumeric` reuse now describes the pinned pointer directly**, rather
  than a local build of `07adeb44` plus a patch series applied in order, which is what their
  sections used to have to say.
  **Seven patches are pending again:
  [`patches/0087`](../patches/0087-js-numeric-tree-order.patch)** — item 3-1's order-preserving
  guard placement, the refusal waterfall that specified it and the 54.0% of the corpus's allocation
  it removes — **[`0088`](../patches/0088-js-gc-pause-accounting.patch)** — the GC-pause
  denominator that prices it, and phase 3, for the first time — and
  **[`0089`](../patches/0089-js-update-target-census.patch)** — where the `++`/`--` step's operands
  live, which re-opens item 3-8 — and
  **[`0090`](../patches/0090-js-numeric-local-defeat-tests.patch)** — the eight shapes that scope it
  to 3-8a — and **[`0091`](../patches/0091-js-3-8a-defeat-ab.patch)** — the A/B that survived an
  attempt to build 3-8a, which is not built — and
  **[`0092`](../patches/0092-js-speculative-numeric-population.patch)** — 3-8a's population, counted
  at 26 names by an instrument made to discriminate first — and
  **[`0093`](../patches/0093-js-speculative-numeric-storage.patch)** — 3-8a's dual-representation
  local, built and off by default — and
  **[`0094`](../patches/0094-js-speculative-numeric-read-paths.patch)** — its three consumers, the
  counter that prices them, and the measurement that closes the item as a 1.2% regression — and
  **[`0095`](../patches/0095-js-imported-outer-numeric-population.patch)** — item 3-9's population,
  counted at **zero** by an instrument proven to discriminate first, closing that item for one
  instrument and no mechanism — and
  **[`0096`](../patches/0096-js-async-job-scheduling.patch)**, which is a **correctness** fix rather
  than a performance one: a promise job could run JavaScript beside the JavaScript that queued it,
  and the gates for `0095` are what found it — and
  **[`0097`](../patches/0097-js-execution-exclusion.patch)**, which closes the residual `0096` wrote
  down (measured at 172 overlaps in 200 rounds) and states the embedding contract that the engine
  cannot enforce alone — and
  **[`0098`](../patches/0098-js-blocking-host-wait.patch)**, which fixes the two deadlocks those two
  introduced between them, one each, and gives a host a supported way to wait on a `Task` from
  inside JavaScript. *Each of the three was found by measuring what the one before it claimed.*
  `0090` and `0091` are tests only. Usual terms: the push to the
  submodule remote returned 403, so the pointer is deliberately unbumped and every figure in their
  sections was measured on a local build of `cca39b4d` plus those patches. They are independent of
  everything cleared above, **`0087` → `0088` → `0089` → `0090` → `0091` → `0092` → `0093` is the
  required order**, and all seven were verified by applying them in sequence to a clean checkout with
  **`git am --keep-cr`** and diffing the result against the branch they were generated from —
  identical. Neither needs a main-repo fallback: `BROILER_JS_NUMERIC_TREE_ORDER=0` restores the
  previous emission exactly and is the bisection, `0087`'s refusal counters are touched once per
  compiled site rather than per call, and `0088` is four `GC` reads per suite in a benchmark host.
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

**Last updated 2026-08-04.** Snapshot of where the campaign stands; every claim is detailed in
the item's own section below, and nothing here is *closed* — see the acceptance protocol in §3.

| Phase | State |
|---|---|
| **0** — evidence | 0-1…0-5 ✅, 0-9…0-11 ✅. **0-6's workflow run has happened and its results are committed** — 2026-08-03 at the pinned pointer, **15 of 15 suites `ok` and 17 of 17 scores** for Broiler, Jint and a same-machine Chromium alike, nothing errored or timed out. Geomean **351** against Chromium's 57 080 (**163×**) and Jint's 616 (**0.569×**), spread **139.8×**. That answers the coverage question and phase 2's exit criterion (below); **what 0-6 still owes is the noise band** — the run is one repetition per suite and says so, so magnitudes and pass/fail may be read from it and deltas may not — plus 0-7's BenchmarkDotNet and 0-8's RID matrix, which a container cannot produce |
| **1** — compile-time | 1-2's mitigation ✅ (`43bc4230`); **1-2's real fix is now on all three recursing passes** — the validator and emitter (`StackGuard` had three defects and could not fire), and now `FastParser`, whose descent aborted the process at 25 000 nesting levels **in the default configuration** and now survives 90 000 at no measurable cost. 1-2's stated acceptance criterion **already passed before any work** — it measured size where the cause was nesting. **New: 1-4 ✅.** Measuring 1-1's premise found the phase's actual dominant cost, and it was not lazy compilation: the closure rewrite held a lambda's in-scope bindings in a `List` and asked it `Contains` per parameter reference, so **emission was quadratic in a scope's binding count** — 2 000 top-level declarations emitted in 13 865 ms against 2.5 ms of parse. A reference-keyed multiset (list-backed below 32 bindings) makes it linear: **28.5× on that shape, and 3.04× on Mandreel end-to-end**, ABBA-interleaved, six pairs. **1-1 is still open and its premise now has a number** — 92–96% of compile time is function bodies on the large real programs — but the measurement also **splits phase 1 in two and re-targets 1-1**: Mandreel was *wide*, not deep, and never was a 1-1 case, while jQuery at 96.5% deferrable is the whole of it. **1-1's emission half then landed without needing the capture mechanism at all**: every risk the item names is settled by the front end, so deferring *IL generation* to first invocation is the same prize with none of them — **jQuery 0.661×, Box2D 0.636×, PdfJS 0.689× on compile, allocation ~0.52× across the board, and 1.0009× steady state**. **Octane CodeLoad, the benchmark the item names, was run and passes: 94.6 → 104.0, 1.099×, 24 samples an arm, 93% pairwise dominance** — and it took 24 because the first three-sample pair and its reverse disagreed. That ratio also re-frames the item: compilation is only ~27% of what CodeLoad measures, not the whole of it. Two mistakes were caught by measuring: a stack handoff per deferred function took the suite from 3.5 to 20 minutes, and a thunk that *called* its resolve cost 1.0247% on call-heavy code until the warm path was written in IL. Typescript is 1.034× slower and unexplained. **The Mandreel suite was then run too, and it overturns the phase's headline target**: a 3.04× faster compile of `mandreel.js` moves Mandreel 0.993× and MandreelLatency 0.992× — Octane compiles that file at script load and times only the run function, so MandreelLatency measures execution pauses and belongs to phase 3. The saving is real and outside every score: suite wall clock **358.2 → 350.0 s**, non-overlapping. **What remains of 1-1 has now been measured before being built, and both halves of its premise hold.** The three-way split was only ever taken on synthetic declaration walls; taken on the real corpora it reads **parse 9.4–13.5%, expression-tree construction 33.6–63.9%, emission 25–57%** — tree construction is the single largest phase on five of six, parse and tree together are **43–75%** of compile, and the parse, the part an early-error rule forbids deferring, is a tenth of it. The population was never counted at all, and it is **84–99.7% of a script's functions never invoked once it has been evaluated** (jQuery 347 of 415, Mandreel 2 689 of 2 697). So the remaining half is over half the compile across a population that is almost entirely never needed. **It also corrects this item's own ceiling table**: `--compile-profile` stubs *outermost* bodies and jQuery has exactly one — the IIFE the library is written inside, 99.91% of its bytes — so the "96.5% of its compile in bodies that are never called" is everything except the parse, and that body is called first. **And measuring the phases found a repeat inside the half that already landed**: `LambdaRewriter.Rewrite` descends through nested lambdas, and `Relay` called it *again* per relayed site, so a lambda at depth *d* was walked *d+1* times and jQuery's whole tree was walked twice by a compile that emits almost nothing. Counted, the second walk finds nothing on any site — **0 of 415, 0 of 978, 0 of 1 574** — and a second counter says the repeat, left to run, creates **0 captures** the first walk had not. It is now skipped for any lambda a descending walk has already entered, with `RewriteRootOnly`'s pass deliberately marking nothing so async and generator bodies are unaffected. **All five pinned test262 manifests were run against it and every count is identical to §3.4's row, manifest by manifest** — 8 710 / 8 617 / 84 / 251 / 9, same files not just same totals. Whole compile **0.782× on jQuery and 0.867× on Typescript, six of six pairs each**; **Box2D does not separate** and its control arm's own spread is 55.6%, so the phase was measured directly instead — **its emission phase 0.549× and its whole compile 0.775×**, in the round where `--compile-phases`' parse control held |
| **2** — property access | **Every item landed or closed.** 2-0 ✅ 2-1 ✅ 2-2 ✅ 2-4 ✅ 2-7 ✅ 2-8 ✅ **2-9 ✅**; **2-3 and 2-5 closed on measurements**; 2-6 folded into 4-1. The phase's conformance gate is **satisfied**, and **its Octane exit criterion is now answered and splits: Richards is inside 200× at 183× (band 163–191) and DeltaBlue is not, at 576× (band 538–711)** — five repetitions per engine, same machine. **DeltaBlue is what phase 2 has left** (item **2-10**), and it is the suite 2-8 was written for. Its first pass found and fixed a real defect — `push` cost every array its shape permanently, **2 503 dictionary fallbacks → 0** — but that did **not** move DeltaBlue's read hit rate, which stays at **65.96% against Richards's 86.61%** and is the live lead. Decomposing those misses ruled out megamorphism (**0** megamorphic read sites) and, in passing, **found a live `class`-shaped instance of 2-0's defect**: `class C{}; new C()` published a global prototype invalidation **once per allocation** (2 002 for 2 000). **Fixed as 2-11** — the setter no longer invalidates when the chain did not actually change — and the effect on the real suites is far larger than the class case suggested, because the retirement was process-wide: **Richards's read hit rate 86.61% → 99.97%**, DeltaBlue's 65.96% → 69.45%, Box2D's 96.39% → 97.72%, with invalidations 37 → 10, 2 519 → 16 and 1 944 → 107. Then **2-12** found why the misses that remained could never heal: the cache's add path deduplicated on two keys while a hit checked six, so a stale entry was declined rather than refreshed and its site missed for the rest of the process — **77.7% of DeltaBlue's misses**. Refreshing in place takes **DeltaBlue's read hit rate to 93.16%** (65.96% before both fixes) and Box2D's to 98.83%. **DeltaBlue still fails the gate at 447×**, but the cache is no longer the reason, and what remains is not property-cache-shaped. **0-6's CI run has since confirmed the split independently — Richards 144.9×, DeltaBlue 460×** — so the phase's exit criterion is answered by two measurements on different machines that agree on which side of 200× each benchmark falls, rather than by one. Also outstanding: **2-9's ~20% compile-and-first-run cost still wants a follow-up — but not the one that was written.** Its losing-side hypothesis was measured against the control it never had (a *strict* function, which carries no Annex B deferred cells) and is **wrong**: every function materializes its trie **exactly once** whether strict or not, because the `prototype` install is withheld from shape-only storage by 2-8's DeltaBlue fix. "Stop materializing for a deferred cell" would have removed a materialization that already happened. The replacement candidate — split cache-visibility from shape-only storage — is specified and **not attempted**, since it is the code whose last regression broke DeltaBlue and it needs 0-6 |
| **3** — arithmetic | Started. **3-0 landed, both halves** — an indexed access boxed its index; a read now allocates **nothing at all** and a write loses ~32 B, on reference arrays as much as numeric ones. **3-1 measured before starting and re-specified**: it trades write allocation for read allocation 1:1, so its clean half is live memory. **3-3's parameter half landed** — and the measurement re-specified it: the gap was a per-call `JSVariable` **cell**, not a box, so a three-parameter call went **230.2 → 62.2 B**. **Probing that analysis before extending it found a wrong-answer bug shipped since P2-2** — two writes it could not see, one returning NaN and one aborting the process on valid JavaScript; fixed, at no measurable cost. **Its `let`/`const` half is now landed**, on the second attempt: the first was withdrawn on a miscompile, and re-built scoped to the *numeric* tier alone it reproduces the predicted number and not the defect — **`let` and `const` both 31.98 → 0.00 B/iter and 1 → 3 numeric locals, identical to the eligible `var` floor, with all twelve other `--local-alloc` rows byte-identical**, both arms from one tree. The recorded reproduction was re-run against it and is green, including under the switches that restore the pre-1-4 and pre-1-1 front end — so the withdrawn attempt's defect is **not explained**, only not reproduced; what the second attempt does differently is leave the JSValue tier closed to lexical names, since a TDZ and const-ness live in the cell that tier removes while the numeric gate proves both unobservable. **The block-scoped `var` then landed too, and 3-3 is complete**: the "definite-assignment analysis" it asked for is the function body's own dominance argument applied one level down — an unconditional block is *transparent* (entered whenever reached, exits only via `return`/`throw`), and any other block *confines* its declaration, which then needs every reference inside it. **`block-var` 31.98 → 0.00 B/iter and 1 → 3, one row moved and twelve byte-identical.** Two defects were caught on the way, neither shipped: a non-dominating declaration could mark a name readable and mask a read that would see `undefined`, and the fix for that over-corrected into rejecting a benign numeric re-declaration — caught by a pre-existing test written as "the guard against over-fixing". All four of the item's categories are now at the eligible floor except `parameter`, which cannot reach the numeric tier at all. 3-4 is a cost, not a task. **New: 3-5 ✅, and it measured the ceiling on this whole phase.** 4-5's probe found that the control loop every measurement here treats as a *floor* was itself paying a box per iteration — and the cause is not the parameter: `i` is a raw double, `n` is a `JSValue`, and `<` had a native form only when **both** sides were doubles, so the raw side was boxed to meet the generic operator. Unboxing the *other* side instead needs no entry guard and covers more (`i < a.length` is a property read, boxed for the same reason), and is sound because ToPrimitive of a Number is that Number. **33.77 → 10.03 ns and 32 → 0 B an iteration, 3.4× on its shape**; 33 semantics tests, every one of which also passes on the unmodified compiler. **On the Octane corpus it is invisible — 0.997× bytes, 0.995× time — and the reason is the number this phase never had: only 5.0% of scalar locals (203 of 4 029) reach the numeric tier at all.** The emission is not the problem (390 comparisons take the new form, 59% of those that could); what is on the other side is. That is the ceiling on 3-0, 3-3 and 3-5 alike, it is the same `CanScalarReplaceLocals` gate that bounds phase 4's tiering candidates, and widening it became **new item 3-6**. It also answers what 3-4 was told to wait for: the gap largely survives unboxed locals, because the unboxing reaches 5% of them. **3-6 has since done its count, and it retired its own design — and 3-5's explanation with it.** Of 2 695 hoisted names, `CanScalarReplaceLocals` — the gate 3-5 blamed — rejects **2, 0.1%**; the causes are *not proven numeric* (2 012, 74.7%) and *captured by a nested function* (478, 17.7%). Counted again inside the analysis, the first is not "most locals are not numbers" either: only **~170 names are never offered**, while the optimistic fixed point **offers 2 335 and drops 1 842 (78.9%)**, because something assigned to them comes from a parameter, a property read, an element or a call — none knowable statically. The two counts reconcile exactly, and the residue is **290 names the analysis proved numeric that the hoist site refused for being captured**. So the work splits: **3-7** gives a captured numeric local a raw-`double` cell (290 names, **203 → ~493, 2.4×**, entirely static), and **3-8** guards a local's numeric-ness at run time — which is **4-3b's in-method branch pointed at a representation**, and means *the largest single obstacle in phase 3 is shaped like phase 4*. Nothing was built for 3-6: its own text said to count first, and the count retired the design, for the fourth item running. **New: 3-7 ✅, and its premise was wrong in both directions.** The cell it asked for already existed — the expression compiler rewrites any CLR local a nested lambda references into a `Box<T>`, and **`Box<double>` *is* the shared cell**, so a captured numeric local costs *one* allocation where the `JSVariable` form costs two. The population, though, is **36× smaller than 3-6 said**: of its 478 captured names, **247 (51.7%) are named by a hoisted function declaration** and can never be widened, 223 more are not proven numeric, and the widening is worth **eight names, 224 → 232, 1.036×**. 3-6's 290 was **inferred rather than counted**, from *offered minus dropped* — and `Resolve` removes a third population between those two counters that had no counter at all, so the real reconciliation is **offered 2 295 = rejected 133 + dropped 1 916 + surviving 246**, and only **22** provably-numeric names are refused at the hoist site for any reason. Lifting the conjunct exposed **two wrong answers and one compile failure that had been hiding behind it**: a hoisted `function g(){ return s; }` can read `s` before `var s = 0` runs while sitting textually after it (`"0"` for `"undefined"`); a nested function's own parameter could mark the outer name initialized and mask a read that really sees `undefined` (`"0,5"` for `"undefined,5"`); and a function declaration stores a function object into the binding being typed, which no assignment-expression walk sees (`let f = 5; { function f(){} }` died on *"Assignment target Call is not supported"*). The first is fixed by a conjunct that is **not** behind the switch, because it is correctness. On its shape the result is exact — **63.97 → 0.01 B/iter, −112 B an activation, and shape ÷ control 7.19× → 1.0000×**, i.e. a captured numeric local now runs at the speed of the same loop with no closure at all — against an equally exact **losing side of +32 B and 1.111× when the value is read *through* the closure**. On the corpus it is **1.0001×**, invisible for the third item running, and the count says why: 2 439 names are not proven numeric and 247 are held by a hoisting rule. **Nothing left in phase 3 is a matter of loosening a conjunction** — and **3-8 then said the conjunctions were never where the prize was**. Two numbers, neither previously taken: **number boxing is 41.89% of everything the corpus allocates** (2.05 GB of 4.88 GB; 66.96% on NavierStokes, 55.16% on Crypto, 35.98% on Box2D, against 0.31% on DeltaBlue — a spread that buries the prize in any corpus average), and the **entire** numeric-local tier, measured for the first time against a build with it switched off, removes **311 187 boxes of 85.6 M — 0.36%, and 0.41% of total allocation**. So four "invisible on the corpus" readings were never evidence that the mechanism does not matter; they were evidence that eight more names do not. A box is minted by the **operator**, whose operands arrive boxed from array elements and object fields, so the local is one link carrying 0.36% of the traffic. Counting what defeats each proof says the same: of 1 916 drops, **894 (46.7%) are a property read and 570 (29.7%) a call's return — 76.4% values produced elsewhere** — against **47 (2.5%) parameters**, the category 3-3 deferred to phase 4 as the one that mattered. **3-8 as written should not be started; 3-1 and 3-2 move to the front of the phase.** Writing the classifier's tests also found the analysis offering a nested function's block-scoped `var`s to its *enclosing* function too, so each was dropped and counted once per level — no answer changes and every downstream figure is identical, but 3-7's `offered`/`rejected` pair is corrected from 2 521/359 to **2 295/133**. **New: 3-1 is started, and its first count re-specifies it off storage.** Nobody had measured what the generic arithmetic operators are *handed* — only what the compiler could prove about them. Counted: **73 817 515 of 73 818 646 invocations across the corpus arrive with both operands already Numbers, every one but 1 131**, and that population is **86.6% of all 85.2 M boxes**, while the compiler's `both are native` gate reaches **556 053, 0.75%** — and even that counts `+` alone, the only operator with a raw-double overload. *Compile-time provability reaches 0.75% of the arithmetic and run-time truth reaches 100.00% of it*, which is the sharpest statement this phase has of why six correct items are invisible. The consequence: the operator already gets two Numbers whatever they are stored in, so a typed backing store is not the precondition — what it cannot do is **hand one back**, because the consumer is a `JSValue`. The shared half is a **run-time-guarded specialization of an arithmetic tree**, boxing only the root, and the per-shape rows already say what it is worth (96 B and three boxes for `s = s + a[0] * 1.5`, of which two are intermediates). It also partly reverses 3-8's "do not start as written": 3-8 priced that guard at the **local** and was right that it is worth 0.36%; at the **operator** the same speculation reaches 86.6%. **And the shared half is now built and measured**: a guarded arithmetic tree — leaves evaluated once into temporaries, tested for Number, computed on raw doubles, boxed only at the root — removes **10 401 782 boxes of 85 249 783, 12.2% of everything the corpus allocates, from 862 compiled sites**, where 3-0, 3-3, 3-5, 3-7 and 3-1's bitwise half moved **0.36% between them**. Crypto 0.786× boxes and 0.583× generic invocations; Box2D 0.933×; Richards 0.787×. **Eligibility is bounded by evaluation order, not by the census**: a coercion runs between two leaf evaluations in a nested tree and is observable, so a leaf evaluated after the first internal node must be a literal or a proven-numeric local — which is why `s + a[0] * 1.5` qualifies and `(a[0] * 2) + p.v` is refused. **The gap to the 86.6% ceiling is itself the next finding**: NavierStokes loses 10.1% of its generic invocations and **1.8%** of its boxes, EarleyBoyer **99.7%** and **none**, so most of those two suites' boxes are minted somewhere that is not a binary arithmetic operator. **And the wall clock is measured too**, ABBA-interleaved, six pairs, with the corpus's own control: DeltaBlue and EarleyBoyer remove zero boxes between the arms and sit at **1.005× and 1.006×**, while the driver total is **0.981× on six of six pairs** and Crypto **0.912× on six of six**. No suite is slower. *12.2% of the corpus's allocation buys 1.9% of its execution time* — which bounds the rest of phase 3 the way 4-2b's 0.83% bounded phase 4. **And the gap to the ceiling has now been chased to its source, which is not where this item has been looking.** Giving the compiler's boxing conversion its own factory entry refutes the obvious reading — only **5.0%** of NavierStokes' requests are a raw double crossing into a `JSValue`, so a typed backing store cannot be why its boxes survive, while the conversion-heavy suite is **Crypto at 31.0%**, the one the guarded tree already served best. That first pass left **40.5% of the corpus's requests attributed to nothing**, and two counters took it to **1.0%**: `BitwiseXor` was the one generic binary operator the census never hooked, and the rest is **the unary operators, which no census had looked at**. **`++` and `--` are 30.9% of all boxing on the corpus, 51.6% on NavierStokes and 80.4% on EarleyBoyer** — more than the compiler conversion and the numeric literal together — and **exactly half of it is a `ToNumeric` copying a `JSNumber` into an equal `JSNumber`**, because it mints unconditionally to hand back the old value and a Number has no observable identity. **17 281 232 requests, 15.4% of the corpus's boxing, for a value the engine is already holding**. **That is now built, and it is nine lines.** Reuse is sound because a Number has no observable identity — the argument the small-integer cache has rested on since P2-2 — and the guard is `IsNumber`, not `!IsBigInt`, because a String or `null` still has to be coerced. Measured: **17 285 913 requests removed against a prediction of 17 281 232, the thing built matching the thing measured to 0.03%**, and **7 050 834 real allocations, 9.4%** — the gap between the two being the small-integer cache, which had already been answering Crypto's loop counters for free while NavierStokes' indices run past its bound. **NavierStokes loses 23.0% of its boxes and 0.906× of its time on six of six ABBA pairs**; with `0084` the corpus goes **85 255 034 → 67 798 222 boxes, 0.795×**. **And the run's sharpest reading is what did not move**: EarleyBoyer cut **50.0%** of its boxes — the largest proportional cut — for **1.002×**, because that is 82 000 boxes a second against NavierStokes' 4 240 000. *A share of a suite's own allocation forecasts nothing; the absolute rate forecasts everything*, which retires a habit this document has had since phase 3 opened. **And then the largest single result phase 3 has produced, from removing one eligibility rule rather than building a mechanism.** `0084` reached 12.2% against a census ceiling of 86.6% and never said which of its six conditions was refusing the rest; counted, **862 of 5 396 candidate arithmetic nodes specialize — 16.0%** — and the two rules that turn down the rest are one finding, not two: `+` is left-associative, so `a[0]+a[1]+a[2]+a[3]` refuses at the root as **order-unsafe** (1 762), refuses again at each left child, and its bottom node is then a single operator with **no saving to make** (2 718). *A chain of k operators produces k−1 order-unsafe rows and one no-saving row and specializes nothing.* **The sub-census then said the rule is not refusing what this phase assumed**: the blocking leaf is a property read **1 028** times and a computed element read **34** — 1.9% — so after six items written around array-resident data, the leaf that blocks the order rule is an object field, and Box2D alone contributes 984. **The fix is that nothing required the leaves to move.** Emitting each leaf at its own postorder position and putting the type test *where the coercion it stands in for would have run* preserves the reference order exactly, and the purity rule then has nothing left to protect — the same soundness argument `0084` makes, read from the other end. Each node carries a `bool`, a raw `double` and a `JSValue`, so a failure part-way up boxes the accumulated double once and lets the rest run generically, which the hoisting form cannot do. **Measured, one build, the switch the only difference: 53 353 957 → 6 626 052 generic invocations (0.124×) and 67 795 858 → 31 162 330 boxes — 36 633 528 removed, 54.0% of everything the corpus allocates**, against 12.2% for `0084`, 9.4% for `0086` and 0.36% for the five locals items combined; from the pre-`0084` baseline the corpus is **85 255 034 → 31 162 330, 0.366×**. OrderUnsafe goes **1 762 → 0** and NoSavingToMake **2 718 → 1 181** without that rule being touched, which is the chain-residue prediction coming out. The leaf cap had to be re-measured too — it was 8 and had *never fired*, because the order rule refused those trees first; at 16 it turns down 8 instead of 85 and the corpus loses a further **664 338 boxes, 2.1%**. **Wall clock, six ABBA pairs, counters off: driver 0.969× on six of six, NavierStokes 0.834× and Crypto 0.893× both on six of six**, against the two zero-box controls at **1.002× and 0.999×**. **And `0086`'s lesson predicts the row that looks wrong**: Box2D removes 51% of its own boxes and reads 1.003×, because that is 861 000 a second against NavierStokes' 6 500 000 — the two suites that move are exactly the two above ~6 M/s. *54.0% of the allocation buys 3.1% of the time*, which with `0084`'s 12.2% → 1.9% is the third reading of the same constant and the number to size the rest of the phase from **And the phase finally has a denominator.** Eight items priced in boxes, three of them measuring an allocation cut against wall clock and getting a sixth of the share back with no explanation — four lines of `GC.GetTotalPauseDuration()` say why. **Collection is 1.8–2.0% of the driver**, and of the 768 ms this item removed **54 ms was collection and 714 ms was the mutator** (pointer bump, zeroing, write barriers, cache traffic). *A box costs about fourteen times more to create than to collect here*, which turns §Non-goals' "the collector is not the problem" from an assertion into a measurement. Allocated bytes fall **4.00 → 2.92 GB**, corroborating the box counters from outside them. At **711 ms per GB** the **0.70 GB of number boxes still standing is worth ~495 ms, 2.6% of the driver** — so everything left in phase 3 is an XL bidding for under 2%, and the `++`/`--` step (33.2% of what remains, concentrated on the corpus's highest-rate boxer) should be counted before the typed store is built. **A sampling profiler was tried and does not help**: `dotnet-trace` inflates the driver ~29% and puts 28% of self time in `PollGCWorker`, its own rendezvous point, while compiled JavaScript lives in `DynamicMethod`s that do not symbolicate — 47.8% of the run lands on `InvokeFunction` and 2.4% on a named body. *The biggest frame in the profile is the profiler*, and 4-5's "blocked on a profiler" needs a different tool, not an afternoon **And the `++`/`--` count that re-specification asked for is taken, with a clean answer.** Of 17 282 144 steps: **Element 0, Property 0.3%, LocalCell 0.0%, LocalSlot 98.1%, Other 0** — *not one of the corpus's increments is on an array element*, so the step shares no mechanism with a typed store, and 98.1% are on a local or parameter the numeric analysis did not prove numeric. Weighted by each suite's request-to-allocation ratio that is **≈7.05 M real boxes, 22.6% of the 31.16 M the corpus still allocates**, and **6.76 M of it is NavierStokes' alone** — the corpus's highest-rate boxer, which is where §3.5's rate lesson says an allocation item pays. Reading the source names the cascade exactly: `++currentRow` in `lin_solve`, where `currentRow = j * rowSize` and `rowSize` is a `FluidField`-scope var written from a sibling closure, so the analysis cannot type it and 3-6's waterfall shows NavierStokes at **24 numeric locals of 141 hoisted names**. ***One closure variable the analysis will not type costs 6.76 M boxes.*** **This re-opens 3-8 on the terms `0083` already used once**: 3-8 priced a run-time guard at the *local* and measured the static tier at 0.36%, which was a measurement of what the mechanism catches; this measures what it **lets through**, and the two differ sixty-fold **Then scoped, by asking which RULE defeats the shape the traffic is in rather than which names were dropped.** Eight shapes, one per conjunct, with the update-target census as the oracle (a numeric local contributes no row, a slot contributes `LocalSlot`, a captured one `LocalCell`). Three suspects are innocent: a nested function **declaration** does not defeat the enclosing local, 3-7's hoisting rule produces a `LocalCell` — and NavierStokes has **9 461 760 `LocalSlot` steps against six `LocalCell`** — and passing the value in as an argument only trades `OtherName` for `Parameter`. **What is left is one conjunct: the analysis is per-function and will not type a name from outside it**, and the sharp fixture is that this holds *even when the enclosing name is already proven numeric* — a conclusion is not carried across a closure boundary. That splits the work: **3-9** (new, S–M, static) imports the enclosing scope's proven-numeric set, which is pure analysis reach with no soundness argument — but **does not reach NavierStokes**, whose root is held by 3-7's correctness rule, so its population must be counted before it is built; and **3-8a**, the run-time half, which is the only thing that reaches the cascade: where a local's *only* defeat is `OtherName`, one `IsNumber` test where the value enters decides the name for the whole function — 4-3b's in-method branch pointed at a representation, no longer general, and so no longer an XL. **Sized: 6.76 M of the 7.05 M real update boxes (96%), ≈0.16 GB, ≈115 ms — 0.6% of the driver.** *The best-founded item left in the phase, and still half a percent; phase 3 is not short of ideas, it is bounded by the exchange rate `0088` measured* **3-8a was then taken to the build and stopped, on its own instrument.** Two findings. **The mechanism is an XL after all**: narrowing *which names* it speculates on does not narrow *what has to change to hold one* — a speculative raw double is a double only while a flag holds, and every fast path (3-0's index, 3-5's comparison, the raw store, the native step, `ToNativeExpression`) keys off the single `NumericStorage` field, so each must become guard-aware or read a dead value, and a missed site is a **wrong answer**. *Size an item by the surface that changes, not by the population that uses it.* **And the population could not be measured**: the optimistic-minus-real instrument read **0 on all seven suites and on the shape it was built for**, so by §3.5 it is unusable — a counter never shown to read non-zero is a claim about the counter. One real defect fell out of it, and it is `0083`'s a second time: **the enable for a compile-time counter was placed among the run-time censuses, which switch on after the corpus has compiled.** Fixing that changed nothing, so the instrument was **reverted rather than shipped**. **What is kept is the A/B the item rests on**, reduced to one identifier: `var c = 2 * rowSize; c++` is a `LocalSlot` that boxes every step, `var c = 2 * 10; c++` is numeric and costs nothing — same nesting, same body, one name different, so the enclosing-scope read *is* the defeat **The count 3-8a was missing has now been taken, on the second attempt, and the discipline is the finding as much as the number**: the instrument was made to *discriminate on constructed shapes* before being pointed at the corpus — seven fixtures, of which the two that matter are negatives (a `Parameter`-defeated local and a never-offered `var a = []` must both stay out, or the count is a tally of every drop). **26 names across the corpus, 232 → 258 numeric locals, 1.11×** — and the distribution is the result: six suites gain one to three each while **NavierStokes gains fifteen and goes 24 → 39, 1.62×**, the largest single-suite widening this phase has produced, landing on exactly the suite that carries **9.46 M of the 16.95 M `LocalSlot` steps and 6.76 M of the 7.05 M real update boxes**. *The population and the traffic are concentrated in the same place, which is the condition every earlier phase-3 widening failed* — 3-7 moved 8 names at 1.036× and was worth 1.0001× because its eight were scattered where nothing hot lived. The prize is still `0088`'s ≈115 ms, **0.6% of the driver**: what changed is confidence, not size. **The count does not license the build** — the mechanism is still the XL above, since every fast path keys off `NumericStorage` and a speculative local is a double only while a flag holds; what it settles is that the work would have something to reach **The XL's storage half is then built, off by default, and it is a measured regression.** A speculative local is held as a raw `double`, a `bool` saying the double is live, and the ordinary `JSValue` slot, with `Expression` a conditional over the two — so **every existing read site is correct untouched** and a write through it is rejected loudly, the numeric tier's own safety argument reused. It deliberately does **not** get `NumericStorage`, the field five fast paths read as "this binding IS a double". Writes derive the flag from the slot branch-free; the `++`/`--` step branches on it and, while it holds, is a native double add that writes nothing back. **All three consumers that can take a raw double are now built too** — the guarded tree's leaf (`OrderedNode` already *is* a raw double, a flag and a fallback), the element read and the element write, over 3-0's `GetElementByNumber`/`SetElementByNumber`. **Each moved the number and none moved it enough: 1.021× storage alone, 1.017× with the tree leaf and the element read, 1.012× with the element write.** Only then was a counter added at the *read* — `JSNumber.CreateSpeculativeRead`, a fourth factory entry — and it closed the item in one line: **NavierStokes mints 393 705 boxes reading a speculative local and the whole item removes ≈5 300.** The 835 584 steps it genuinely takes off `Increment` mostly save no allocation at all, because they are `x[++i]` and the result is boxed to be an index either way. ***Closed as measured, not deferred***: the mechanism is correct and left in the tree behind a switch that defaults off, because what makes it lose is the read/write ratio of the code it targets — `currentRow` is read four ways and incremented once — which is a property of the workload, not of how many consumers the compiler grows. **Every premise the item was scoped on survived and the item still lost.** Building it also found a real defect the fixtures initially failed to catch — a tree leaf that offered a slot the raw half had left three increments stale, answering `"0!"` for `"3!"` — and the repair to the *test* discipline is §3.5's new rule: after writing a fixture for a new fast path, break the emitter deliberately and confirm it fails. **And the arm corrects the count's own reading**: the 15 names carry **835 584 of NavierStokes' 9.46 M steps, 8.8%**, not the whole of it — *the suite holding the names being the suite holding the traffic is not the claim that the names hold the traffic*. 1 191 + 4 571 + 2 103 tests green **on both settings**, with the four shape fixtures now Theories over the switch **3-9, the static half of the same split, is then closed by its own precondition count — and it cost one instrument and no mechanism.** Its population is **0 on all seven suites**, against 3-8a's 26 reported from the same call site in the same run, so the zero is the corpus rather than the harness. A second counter says *why*, because a single zero cannot separate "nested functions never read an enclosing numeric local" from "they read them constantly and never anywhere typable": the enclosing scope chain answers *"that name is already a raw double"* **0 times on the whole corpus**. The reads do not exist. That reconciles with item 3-7 exactly — 3-9 can only import from a name that is both proven numeric AND still a raw double despite being captured, which is 3-7's population of **eight names**, and not one of the eight is read from an assignment inside the function that captures it. **The instrument was made to discriminate first** — nine constructed fixtures, three reading non-zero, each re-checked by disabling the probe and confirming it fails, which is `0094`'s own new §3.5 rule applied to the thing that decides this item. It also settles a design question by flipping two fixtures: the probe must ask what the compiler **built** (`NumericStorage`) and not what the analysis **proved**, or a name 3-7 leaves in a cell for correctness reads as a win. *A good mechanism with nothing to point it at* — no guard, no fallback, 3-8a's failure mode structurally absent — declined because building it would buy an analysis pass and a scope-chain probe per compiled function for zero names |
| **4** — tiering | Started. **4-3a is landed and it found a real hazard**: restart is only sound if the body is not suspendable, and nothing said so — the property held by two unrelated accidents (the `EnableTiering` call sitting inside the ordinary-function `else` branch, and the tiering gate borrowing `CanScalarReplaceLocals`, which refuses generators for its own reasons). Defeating both, a legal `async function` whose body matches the planner's shape returns **`number` instead of a Promise** from its second call on — measured, not argued. One condition at the decision point fixes it, and 16 tests pin all three conditions. **4-3b is landed too**: `SpeculationBuilder.Guarded` compiles the specialized and generic forms into one method so a failed guard is a *branch*, with the subject evaluated exactly once (the hand-rolled spelling fails 12 of its 15 tests) and per-site poisoning after four misses. **It emits no JavaScript-level speculation, and that is structural rather than scope-trimming** — a guard needs a shape or a callee to speculate on and a tier-1 method knows neither, so the branch only has meaning inside a tier-2 recompile, which is 4-2. The mechanism lands before its consumer because it has to. **4-3's design is written** — and it re-specifies the item: this engine has no interpreter frame to reconstruct, so V8-style deopt has no counterpart here. Splits into 4-3a (state and enforce the restart contract the pilot already runs, S) and 4-3b (a generic fallback branch inside the specialized method, M–L), which gates 4-4 rather than all of phase 4. **4-1 has landed and it settles the phase's premise.** Per-site feedback now *retains* what the inline caches only observe — receiver shapes at reads, callee identities at calls — and over seven Octane suites, weighted by executed operations, **93.54% of 37.9 M property reads and 96.70% of 4.24 M calls happen at a site that only ever saw one shape or one callee**. 4-2 and 4-4 are an XL each and both are worth their cost only in proportion to that number; nothing in the engine could report it until now, and it comes out high. **Megamorphism is essentially absent** — 18 sites in total, five of seven suites have none — corroborating 2-10's independent finding of zero megamorphic read sites; the fallback path 4-3b must still be correct, it just will not be hot. **DeltaBlue is the worst read case at 77.10%**, with 43 polymorphic read sites against Richards's 1, which is a lead on the suite still outside phase 2's gate. Collection is off by default, costs nothing on the call path *by construction*, and the item's third signal — numeric-vs-generic per site — is deliberately left uncollected rather than half-built. **4-2 has now landed too, and it splits the same way 4-3 did.** Measuring the branch it was told to replace found that it does **not** "recompile the same code the same way": a fresh top-level compilation builds a *second* function object and loses inherited strictness, so **DeltaBlue died on the shipping tier-2 hook** — `TypeError: Cannot get property call of undefined`, 0 of 1 benchmarks against 1 of 1 untiered, because its constructors read `X.superConstructor` off their own name and got the copy. Four of thirteen probes disagreed between tiered and untiered. **4-2a** states the recompile contract, refuses the identity cases and repairs strictness, at a cost of ~5% of promotions. **4-2b** then makes tier-2 re-emit tier-1's *own* site indices — which carries the warm caches across promotion and makes 4-1's feedback addressable — and emits a monomorphic read as a shape guard plus a direct slot load through 4-3b's in-method branch, whose **first JavaScript-level consumer** this is. **44.74% of the corpus's 37.9 M executed reads leave the inline-cache path** (counted exactly: cache misses are identical, so they were removed, not converted), carried by **1 130 sites**, with 156 guard misses and 30 poisoned — the monomorphism holds past the promotion point. **Each such read is 0.818× (46.83 → 37.12 ns, six pairs, 0.778–0.879), and the suite wall clock does not move: 0.9947 against a feedback-on control.** That is arithmetic, not failure — 16.9 M × 9.7 ns is 164 ms of a 19.7 s run, **0.83%**, under a ±2% floor. It also bounds the phase: the whole read path is ≤ ~9% of Octane's execution time here and the whole call path ≤ ~5.5%, so **the two paths phase 4 is built around are together at most ~15%**, which 4-4 should know before it starts. The item's arithmetic half is still not built, for 4-1's reason. **4-4's premise has now been measured too, and it re-specifies the item before any of it was built.** Counting at the call rather than through 4-1's compile-time gate, the corpus makes **6 194 758** invocations, and **37% of them are to native builtins** — an emitted call site with no body to inline, which any ceiling counting them inflates by more than a third. That correction came out of writing the counter's tests, which also found that a builtin runs a JavaScript callback on a *different and much shorter* entry (`InvokeCallback`, one `using` scope against five); callback invocations turn out to be **zero** on all seven suites, so an earlier guess that they explained the gap to 4-1's figure was wrong and is recorded rather than quietly deleted. Of the 3 902 620 calls with a JavaScript callee, **64.0% are made from a promoted function** — inlining's whole surface, and an upper bound. Against a hand-inlined control in one run set, **inlining saves 149 ns a call (0.37×)** — so the ceiling is **372 ms of a 19 694 ms driver, 1.89%**. Inlining is **expressible** here, unlike 4-3's deopt: labels and goto exist, a real function scope handles the callee's names, and 4-1 retains the callee — so the blocker is value, plus one semantics decision nothing can undo (an inlined callee has no frame, so it leaves `Error().stack`, and this engine has nothing to reconstruct one from). **New item 4-5 beats it**: a call costs **142 ns before it carries any argument**, plus 17.1 ns each, so ~90% of the overhead is fixed — and reducing that reaches all 6.19 M calls with no speculation, no guard and no fallback. The same probe also bounds the phase: reads are **9.16%** of execution time and the fixed call prologue **4.47%** (paid by every invocation, native callee included), so **the two paths phases 2 and 4 are built around are together under 14%** — while the arithmetic-only control loop is 16.98 ns an iteration, which points at **3-4**, not at phase 4. **4-5's ablation has since run, and it falsifies most of its own premise**: five nested `using` scopes cost **0.011 ns**, EH 0.73 ns and dispatch 0.68 ns, so the prologue is not where a call's cost is. The one real cost is an **`AsyncLocal<bool>` read at 7.0 ns against a `[ThreadStatic]` at 0.31 ns** — read on every call, and documented in `JSEngine` as *"reads are cheap"*, **wrong by 24×**. Mirrored into a ThreadStatic with the AsyncLocal kept as the carrier (0.22% of the corpus; 9 tests, which also pass on the unmodified engine). **~85% of a call's fixed cost is still unattributable from outside the engine**, so the rest of 4-5 is blocked on a profiler rather than a design. **And the control every probe here has used turned out not to be a floor**: the same counted loop with a *literal* bound instead of a parameter one runs at **8.36 ns and 0 B an iteration** against **33.77 ns and 32 B** — same answer, **4.0× and a box per iteration**, because a parameter cannot reach the numeric tier (3-3's one acknowledged gap) so `i < n` boxes. `for (var i = 0; i < n; i++)` is the corpus's commonest shape; that is **new item 3-5**, and on this evidence it is worth more than anything left in phase 4 |
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

**Owed.** **0-6's CI Octane run has happened**, at the pinned pointer on 2026-08-03, and it is
the first committed result that describes the engine as it now is. What it settles and what it
does not:

- **Coverage: 17 of 17 scores, all 15 suites `ok`, for all three engines** — Broiler, Jint and
  a same-machine Chromium. Nothing errored, crashed or timed out (`diagnostics.md`: *"All 15
  suites completed. Nothing to diagnose."*). This is the headline half of the gate and it is
  met.
- **Phase 2's real exit criterion — *DeltaBlue and Richards inside 200×* — is answered, and it
  splits exactly as the local run predicted: Richards PASSES at 144.9×, DeltaBlue FAILS at
  460×.** The local five-repetition run had them at 150× and 447×; CI's single-repetition run
  says 144.9× and 460×. Two independent measurements agreeing on which side of 200× each
  benchmark falls is what makes the split a finding rather than a reading.
- **The noise band is still not on record**, and that is the half still owed: the run is
  `"Repetitions per suite: 1"`, and `comparison.md` says so itself — *"a single run; deltas
  against it cannot be distinguished from noise"*. So the per-suite numbers below may be
  quoted for **magnitude and for pass/fail**, and **no delta against them may be claimed**.
  0-7's BenchmarkDotNet comparison and 0-8's RID matrix remain owed too, and a container
  cannot produce either.

**The conformance gate is satisfied, and was re-run five times for items 3-3, 4-1, 4-3a and 4-3b.** All
four pinned manifests were run **2026-08-03 on linux-x64 at the pinned `9bf9639b`** — plus
`patches/0067`, and then plus each successive prefix through all five of `0067`–`0071` —
against the pinned suite ref `ccaac100`: **8 220 passed, 84 failed, 44 skipped, 9 timed out, and
every count is identical to §3.4's recorded run on all five, manifest by manifest.** The 84 are the same
`$262`-requiring files and the 9 the same integer-limit cases already tracked in
`test262-failures.txt`. So `properties-proxy` and `strict-mode`, which phase 2's exit gate names
because 2-1, 2-2, 2-4 and 2-8 all touch `OrdinarySetWithOwnDescriptor`, are **clean; 2-9, which
rewrites the storage underneath that path, adds no failure; and neither does 3-3's `let`/`const`
half.** A **fifth manifest** was added with that item — `test262-lexical-declarations`, because
none of the four covered `let` or `const` at all — and it is clean on both arms (§3.4).
**Re-run again 2026-08-04 at the pinned `61c8cc65` plus `patches/0078` for item 3-7, on both
settings of that patch's switch**: every count is identical, manifest by manifest. One run of
`properties-proxy` reported an extra failure whose captured stderr reads *"The JavaScript compiler
is not available"* — **a `dotnet build` rewriting the assembly under a running suite, which was
mine**, not an engine result; re-run with nothing else building it is clean (§3.4).

**The patch handoff, which was the third gate, is done and has stayed done.** `patches/0049`–`0058`
were applied, pushed and the pointer bumped to `a6f101cc`, and `0059`–`0086` have followed it in
six further bumps, most recently to **`cca39b4d`**; the patch files are cleared and
[`patches/README.md`](../patches/README.md)'s index is **empty for the first time since it was
written**. What phase 2 measured and what CI now clones are the same tree — which is the condition
both remaining gates were waiting on, and it is why the conformance one could be run at all. **The
handoff has now cleared three times running**, so a 403 has come to mean *deferred* rather than
*stranded*; what makes that safe is that the pointer is never bumped locally, so nothing here can
name a commit CI cannot clone. **Seven patches are open again** (`0087`, item 3-1's order-preserving
guard placement; `0088`, the GC-pause denominator that prices it; `0089`, the update-target
census that re-opens item 3-8; `0090`, the eight shapes that scope it to 3-8a; `0091`, the A/B
that survived the attempt to build it; `0092`, its population counted at 26 names; and `0093`, its
dual-representation local built and measured), on the usual 403 terms.

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
  large real programs, and when this was written the two worst scores in the entire suite
  were **MandreelLatency at 4646×** and **CodeLoad at 371×**. `script:evaluation` at 37 ms was
  a true measurement of a corpus small enough that eager compilation is free. It is not
  free on jQuery, on the TypeScript compiler, or on a 152,948-line generated function.
  **The front end is phase 1.**
  > **Correction, from running both suites (phase 1).** This bullet used to call those two
  > scores "the two that measure nothing but the front end", and used them as the phase's
  > success metric. Measured, **CodeLoad is ~27% compilation and MandreelLatency is 0%** —
  > Octane compiles `mandreel.js` at script load and starts its timer afterwards. The
  > argument above is unaffected, because it rests on the *probe corpus being too small to
  > see eager compilation*, which is still true and is why phase 1 exists. What it cost was
  > the phase's target list: see Phase 1's header.
  >
  > **The two scores it named are no longer the two worst** (§4.2, 2026-08-03): CodeLoad is
  > 228× and three suites are now behind it — DeltaBlue 460×, Mandreel 290×, RayTrace 256×.
  > MandreelLatency at 4 584× is still the tail by an order of magnitude. Neither correction
  > touches the argument, which was never about *which* scores were worst.
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

| Metric | Superseded run (2026-07-31) | **Committed run at the pin (2026-08-03)** | Target |
|---|---|---|---|
| **Scores reported** out of 17 | 12 / 17 | **17 / 17** — 15 of 15 suites `ok` | **17 / 17** |
| **Geomean** over all 17 scores | 245 over the 12 that completed | **351** over all 17 | — |
| **Spread** = worst ÷ best, as ×-slower-than-Chromium | 4 646 / 45 ≈ **103×** | 4 584 / 32.8 ≈ **139.8×** | **< 5×** |
| **Against Jint**, geomean of per-benchmark ratios | not measured | **0.569×** | > 1 |

The right column is the workflow's own run, so its Chromium and Jint columns were measured on
the same machine at the same time and the ratios are directly comparable. It is **one
repetition per suite**, which is enough for coverage and for which side of a threshold a
benchmark falls on, and not enough for a delta — the band is what 0-6 still owes.

**The spread went up, and that is not a regression.** It is 139.8× against the superseded run's
103× because the superseded run had *five suites scoring nothing at all*: a suite that fails
contributes no ratio, and the four of those five that now score (Crypto, PdfJS, zlib,
Typescript) landed across the middle of the range while the best axis improved from 45× to
32.8×. Spread is a ratio of two suites, so widening the denominator widens it. Compare the
column honestly or not at all — which is the reason this table names both dates rather than
saying "before" and "after".

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
`test262-strict-mode`, `test262-realm-isolation`, and — added 2026-08-03, see below —
`test262-lexical-declarations`. First taken 2026-08-01 at `cdb2fd41`
(suite ref `ccaac100`), **re-run 2026-08-02 at `a6f101cc` plus 2-9 with every count
unchanged**, **re-run at `71dda1b7` plus 3-3 with every count unchanged**, and **re-run five
times on 2026-08-03 on linux-x64 at the pinned `9bf9639b` — plus `patches/0067`, plus `0067` and
`0068`, plus `0067`–`0069`, plus `0067`–`0070`, and plus all five of `0067`–`0071` — with every count
identical every time, manifest by manifest** — so the table below describes the pinned pointer as well as the commit it was first
measured at.

**Re-run 2026-08-05 on linux-x64 at the pinned `cca39b4d` plus item 3-1's order-preserving guard
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

**Re-run 2026-08-04 on linux-x64 at the pinned `61c8cc65`, plus `patches/0078` (item 3-7), plus
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
times from the same tree**: at the pinned `9bf9639b`, and at that commit plus each successive
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

### 3.5 Standing measurement lessons

These were paid for once each. They apply to every phase below.

- **Measuring an item's premise is how you find the item next to it.** 1-1's premise —
  "most front-end cost is function bodies" — needed a control, so one was built: the same
  source with every body replaced by `{}`. Five corpora agreed with the premise. The sixth,
  Mandreel, took **17.7 s with every body already removed**, and that residue was 1-4: an
  emitter quadratic in a scope's binding count, worth 3.04× on the *compile* of the suite 1-1
  was written around — though running that suite later showed its two scores do not measure
  compilation at all. Nobody was looking for it, and no probe could have shown it — a one-liner has one
  binding, and a quadratic needs width to be visible. *A control built to size one item
  measures everything that item is not, which is the only place a cost nobody has named can
  show up.*
- **A benchmark's name is not its contents — read what it times before aiming a phase at it.**
  Phase 1 was aimed at MandreelLatency, the worst score in the suite, on the strength of the
  word *latency* and a 5 MB machine-generated file. Octane compiles that file at script load
  and starts the timer afterwards; `MandreelLatency` is the RMS of pauses between 20 render
  frames over already-compiled code. Making the compile **3.04× faster moved it 0.992×** — and
  the saving is genuinely there, in the suite's wall clock (358.2 → 350.0 s), where no score
  looks. The same reading error, smaller, put CodeLoad at 100% compilation when it is ~27%.
  *Twenty lines of the benchmark's own source would have said so at any point in the last three
  phases, and nobody opened it.*
- **Two arms, three samples each, is a coin toss dressed as a measurement.** 1-1's CodeLoad
  run separated cleanly on its first pair — 94.3 eager against 105 deferred, no overlap — and
  then failed to separate at all on the reversed pair, 99.2 against 99.4. Both pairs were three
  repetitions an arm, on a suite whose own declared noise band is 7.5%, chasing an effect near
  10%. Twenty-four samples an arm settled it at 1.099× with 93% pairwise dominance, but *either
  early pair would have been reported as the answer*, and they disagreed. **Interleaving is not
  enough when the effect and the noise are the same size — the sample count has to grow until
  the arms separate by rank, not by median.**
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
- **A count you inferred is not a count, however well it reconciles.** 3-6 sized its successor at
  290 names by reading survivors as *offered minus dropped*, and said its two figures "reconcile
  exactly" — they did, to **each other**, while both omitted the same third population: everything
  a rejection path removes before the fixed point runs, which had no counter at all. Counted
  directly, `offered 2 295 = rejected 133 + dropped 1 916 + surviving 246`, and the item's real
  population was **22 names, of which 8 were reachable** — off by 36×. *Two derived numbers
  agreeing is evidence about the arithmetic between them and about nothing else; adding the direct
  counter was four lines, and the campaign's own rule about indirect instruments (2-9) had already
  been written down.*
- **A conjunct that is doing two jobs hides the second one until you remove it.** Every numeric
  local a nested function mentions was refused, and the stated reason was that a closure captures
  through a cell. It was also, silently, the only thing preventing three defects: a hoisted
  function declaration reading the binding before its initializer, a nested function's parameter
  marking the outer name initialized, and a function declaration storing a function object into
  the binding being typed. All three were reachable the moment the refusal was lifted, and all
  three produce wrong answers rather than lost optimizations. *Before widening a gate, ask what
  else the conjunct you are deleting happens to be enforcing — the answer is not in its comment,
  because whoever wrote the comment did not know either.*
- **A static argument that rests on text order is defeated by hoisting, not by closures.** The
  numeric tier is sound because a name referenced before its declaration is refused, so text order
  implies execution order. A function *expression* preserves that — it does not exist until its
  statement runs — while a function *declaration* at body top level breaks it, existing at entry
  with its body textually anywhere. The distinction is worth 247 of 478 captured names on the
  Octane corpus, i.e. it is the majority case and not a corner. *When an item is described as
  "entirely static", check which of its conditions are about text and which are about time.*
- **Never build while a suite is running against the output, and read the failure before calling
  it a flake.** One `properties-proxy` run reported an extra failure on the arm under test and not
  on its control — the shape of a regression. It was neither: the runner's captured stderr said
  *"The JavaScript compiler is not available"*, because a `dotnet build` I had started for an
  unrelated edit was rewriting `Broiler.JavaScript.Compiler.dll` under the running children. The
  first diagnosis was "a flake under `--max-workers 8`", which fitted the evidence then available
  (the test passes three times in isolation, needs no `$262`, and answers correctly on every
  build) and was still wrong. *A failure that reproduces nowhere is not thereby a flake — the
  runner had recorded the actual reason, and reading it was cheaper than the three re-runs that
  did not settle it.* This is §3.5's "check that the thing you measured is the thing you built"
  from the other side: there the binary was older than the source, here it was being rewritten
  mid-suite.

- **A run of deltas is not a measurement of the mechanism, and the difference is a switch.**
  Items 3-0, 3-3, 3-5 and 3-7 each measured their own increment to the numeric-local tier against
  the tier as it stood, and each came out invisible on the corpus — 0.997×, 1.0001×. Four such
  readings look like a verdict on the mechanism and are a verdict on *eight more names*. Turning
  the whole tier off for the first time put a number on it: **0.36% of the engine's number boxing,
  0.41% of allocation**, from every raw-double local the campaign has ever produced. *When several
  items in a row report "no effect", the missing control is the one that removes all of them at
  once — and it is usually one conjunct and an environment variable.*
- **A per-unit figure repeated by every item is a description of the unit, not of the problem.**
  Phase 3 has now reported **31.98 bytes an iteration** for four ineligible categories (3-3), a
  parameter-bound comparison (3-5), a captured local (3-7) and all three provability causes (3-8).
  It is the same box, and it was never the question. The question is what share of a real
  workload's allocation is boxing at all — **41.89%**, and 66.96% on NavierStokes against 0.31% on
  DeltaBlue. *A number that comes out identical no matter which item measures it is measuring the
  representation, and the corpus share has to be measured separately or the phase will keep
  producing shapes that are 7× faster and suites that do not move.*
- **A corpus average can bury the very thing the phase is for.** The boxing share across the seven
  Octane suites is 41.89%, and reading only that average would have been almost as misleading as
  reading none: it is 0.31% on DeltaBlue and 66.96% on NavierStokes. Four suites where phase 3 has
  nothing to win outvote three where it has almost everything. *Report the spread before the
  aggregate whenever the items are representation changes, because those are exactly the changes
  whose value is concentrated in a workload shape rather than spread across one.*

- **A one-sentence premise is a cause claim, and it is the sentence least likely to have been
  checked.** Item 3-2 stood for the whole campaign as *"`shapeSlots` holds `JSValue` references, so
  `vector.x = 1.5` allocates"*. The line does allocate. The slot does not: `o.x = 2` costs **0.00
  bytes an iteration**, because storing an already-boxed value into a slot is a reference copy.
  What that example pays for is the **literal**, which is a different item worth 1.2% of the
  corpus's boxing — so for as long as the sentence stood unmeasured it aimed the item at the wrong
  half of its own mechanism. *The shorter an item's justification, the more of it is inference;
  probe the example it gives you before the mechanism it names.*
- **Two items described as twins should be measured against each other before either is built.**
  3-1 and 3-2 have been separate L's since the phase opened. Measured, their per-iteration figures
  are identical to the hundredth — 31.98 for a read into an addition and 96.00 for a
  read-modify-write, in an array and in an object field alike — because both are one mechanism (a
  value that stays unboxed from producer to consumer) with two storage backends. Their *populations*
  then turn out to be disjoint: **98% of the corpus's numeric property reads are Box2D's**, while
  NavierStokes performs **388** property reads and mints **30 M** boxes. *The shared half should be
  built once and the storage halves ranked by population — neither of which is visible from either
  item's own text.*

- **Check a corpus counter is deterministic before reading a delta out of it.** Item 3-1's
  bitwise change came back **+3 126 boxes on Crypto** — the wrong direction, on the suite it was
  aimed at. Running the *same arm twice* gave 42 418 727 and 42 421 217: Crypto generates RSA keys
  and its work is not fixed across runs, so its own variation is larger than any gap between the
  arms. Six of the seven suites are identical to the digit and only that one is not. *"Allocation
  is deterministic" is a property of most counters here and not of all of them, and the check
  costs one extra run of the arm you already have.*

- **An emitter that cannot be fed is not an optimization, and it will pass every test you write
  for it.** The bitwise operators were given a native form that takes `s = i & 1023` from 31.84
  bytes an iteration to **0.00**, is correct on 15 semantics cases, and removes **exactly zero
  boxes on the whole Octane corpus** — including on Crypto, a BigInteger implementation built on
  `&`, `|` and `>>` that mints 42.4 M boxes. The native form requires both operands to be numeric
  locals, and Crypto's digits live in `this.array[i]`. That is the same shape as 3-5's finding a
  phase earlier, and by now it is a rule: *before adding a fast path, count how many of its
  operands can actually reach it — the population feeding a specialization is a different
  measurement from the specialization's own speed, and only the first one predicts the corpus.*

- **A control built by deleting a syntactic category deletes the program when the program is one
  of them.** `--compile-profile` sizes item 1-1 by replacing every *outermost* function body with
  `{}`. jQuery has exactly one outermost function — the IIFE the library is written inside — so
  its control is an empty file (`bodyByteShare` **0.9991**), `full − stub` is the whole compile,
  and the resulting "96.5% ceiling" is *everything except the parse*. It is also unreachable, for
  a reason the same table cannot see: CodeLoad evaluates jQuery, so that body is the first thing
  called. The instrument that answers the question is a **count of what is never invoked**, and it
  says 83.6% rather than 96.5% — a different measurement, not a corrected one. *A differencing
  control is only a ceiling while the thing it removes is the thing that is optional; check the
  share it removes before quoting the difference.*
- **A phase that is deferred can still be walked, and the counter is one line.** Item 1-1 defers a
  nested function's IL to first invocation, and the relay that registers the deferral then ran the
  closure rewrite over that function's whole subtree — so deferring jQuery's single IIFE walked
  the entire program. The rewrite descends through nested lambdas already, which makes the relay's
  call a repeat at every level: a lambda at depth *d* was walked *d+1* times. Two counters on the
  relay say so exactly — **0 rewrites needed against 415, 978 and 1 574 skips** on three corpora.
  *After deferring a phase, count what the deferral still touches: the work that moves is easy to
  measure and the work that stays is what nobody looks at.*
- **A counter reading zero is a claim about the counter, and "turn it on" has a location.** The
  arithmetic-operand census read **0 invocations on all seven suites** against 85 M boxes, which
  would have been a finding — the generic operators are never called — and was an instrument
  switched on in the wrong method: the enable was inserted next to the first of two identical
  `NumberBoxingDiagnostics.Reset()` pairs, one in a call probe and one in the driver. The *boxing*
  counter next to it read correctly throughout, which is what made the zero look like data.
  *Before reporting an extreme count, make the instrument produce a non-extreme one on a case you
  constructed to move it* — here five test fixtures, three of which have to make the counter
  discriminate rather than merely fire.
- **Compile-time provability and run-time truth are different measurements, and this phase had only
  ever taken the first.** Every phase-3 item widens what the compiler can *prove* numeric, and the
  gate they widen reaches **0.75%** of the corpus's arithmetic invocations. What those operators are
  actually *handed* is two Numbers **100.00%** of the time — 73 817 515 of 73 818 646, every one
  but 1 131. Six correct, invisible items sit in the gap between those two numbers. *When a
  static analysis is the thing being widened, count what the dynamic answer would have been before
  widening it again; the two counts are usually available from the same probe run and only one of
  them predicts the corpus.*
- **Interleave, at process granularity.** Sub-1.5% effects are only visible ABBA-
  interleaved across independent builds, ten runs each, medians compared.
- **Two shapes that allocate at different rates cannot share a process, and the control is what
  says so.** 3-7's first timing run put the change at 0.1327× — and its *control*, the same code
  compiled the same way on both arms, at 1.2857×. A control that moves is a broken measurement,
  full stop: the winning arm allocated 192 MB over its loop and the collections landed on whatever
  ran next in the same process. Re-run one shape per process the control came back to 0.9535× and
  the answer held. *That is the `--compile-profile` corpus artifact one level down, and the general
  form is that a control exists to be checked, not to be quoted alongside the result.*
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
- **A share of a suite's own allocation forecasts nothing; the absolute rate forecasts everything.**
  3-1's `ToNumeric` reuse removed **50.0% of EarleyBoyer's boxes and moved it 1.002×**, and **23.0%
  of NavierStokes' and moved it 0.906× on six of six pairs**. The percentages say the opposite of
  the result; the rates say it exactly — 82 000 boxes a second removed against **4 240 000**. This
  document had quoted per-suite percentages as though they forecast time since phase 3 opened, and
  they never did: NavierStokes mints 18.5 M boxes a second and EarleyBoyer 165 000, two orders of
  magnitude apart, so no single corpus figure describes both. *Before predicting time from an
  allocation change, divide by the elapsed time — a proportion is a statement about the suite, and
  only a rate is a statement about the machine.* The corollary is that a driver total can be silent
  while the item works: 9.4% off a suite that is 8.7% of the corpus is 0.82% of the total, which is
  under the total's own noise before anything is built.
- **An unattributed residue is a claim about the census, and chasing it is where the item is.**
  3-1's boxing-source census named 59.5% of the corpus's requests and left **40.5% coming from
  nowhere**, which reads like builtins and rounding and was nearly written up that way. Two
  counters took it to **1.0%**, and what came out was not a scattering: **`++` and `--` are 30.9%
  of all boxing on the corpus and 51.6% on its biggest boxer** — larger than the compiler
  conversion the section had been written to measure, and invisible because the census was built
  around *binary* arithmetic and no one had counted a unary operator. Half of it is a `ToNumeric`
  copying a `JSNumber` into an equal `JSNumber`. *A residue is the part of the measurement that is
  not yet a measurement; the size of the thing hiding in it is bounded only by how big the residue
  is.* Corollary, from the same afternoon: `BitwiseXor` was the one generic binary operator the
  census never hooked, and nothing failed — **an unhooked operator is silent, not wrong**, which is
  the same failure mode as the counter that read zero, one level up.
- **An optimization with a numerator and no denominator is half a measurement, and the missing
  half is usually the item.** `0084` reported *"10 401 782 boxes removed, 12.2%, from 862 sites"*
  and explained the gap to its own 86.6% ceiling with a per-suite table. What it never reported is
  **how many sites it was offered** — and the answer is 5 396, so it was specializing 16.0% of the
  arithmetic and the other 84% had no attribution at all. Adding the waterfall took one enum and
  about thirty lines, and the largest result in phase 3 fell straight out of it: one rule
  (`OrderUnsafe`, 1 762) was manufacturing a second (`NoSavingToMake`, 2 718) by refusing chains
  from the top down until only a lone operator was left. *"X% removed" is a statement about the
  successes. The refusals are a population too, and until they are attributed the item does not
  know what it is next.*
- **A threshold nothing has ever hit is untested, not safe.** `MaximumSpeculativeLeaves` was 8 and
  read as a harmless code-size bound because it fired **zero** times on the corpus — but only
  because a rule above it refused those trees first. The moment that rule went, the cap turned down
  85 trees and cost **664 338 boxes, 2.1%** of what the change otherwise removes. *A constant's
  measured cost is only valid for the configuration it was measured in; changing anything upstream
  of a limit re-opens it.* This is the same shape as `0084`'s "two operators" rule, which was
  reasoned rather than measured and lost half that item's prize.
- **Price the thing you are optimizing before optimizing it, not after — and "allocation" is not
  one cost.** Phase 3 ran for eight items on box counts, and three of them measured an allocation
  cut against wall clock and got about a sixth of the share back, three times, with no explanation
  offered. Four lines of `GC.GetTotalPauseDuration()` say why: **collection is 1.8% of the driver**,
  so the collector was never the thing being bought back. Of the 768 ms the order-preserving
  emission removed, **54 ms was collection and 714 ms was the mutator** — the pointer bump, the
  zeroing, the write barriers and the cache traffic. *A box costs about fourteen times more to
  create than to collect on this corpus*, which is the number that makes "GC work is a non-goal"
  a measurement rather than an opinion, and which gives every future allocation item a rate to bid
  with — **711 ms per GB** — instead of a percentage.
- **Price a mechanism by what it lets through, not by what it catches.** Item 3-8 was shelved on
  `BROILER_JS_NUMERIC_LOCALS=0`: the whole raw-double local tier removes **0.36%** of the corpus's
  boxing, so widening it looked like an XL for nothing. That number is real and it answers a
  different question — it measures the population the analysis *can already prove*, which is small
  exactly because the proof is hard. Counting the same mechanism from the other side, at the
  `++`/`--` operator, the names it **fails** to type carry **22.6% of everything the corpus still
  allocates**. *An ablation switch prices the built thing; only a census of the misses prices the
  thing that was not built,* and the two differed sixty-fold here. This is the second time the same
  correction has been needed — `0083` found compile-time provability reaching 0.75% of the
  arithmetic against run-time truth's 100.00% — so it is a pattern rather than an accident:
  **whenever an item is turned down on an ablation, ask what the ablated mechanism never saw.**
- **Narrowing an item's population does not narrow its mechanism, and only one of the two decides
  the size.** 3-8a was re-sized from XL to M on the strength of its population: a run-time numeric
  guard aimed at one cascade instead of at every local. Taken to the build, the mechanism was
  unchanged — a speculative raw double is a double *only while a flag holds*, and every fast path
  in the compiler keys off the single `NumericStorage` field that means "this is a double", so all
  of them become guard-aware or read a dead value. *Size an item by the surface that has to change,
  which is a property of the representation, not by the number of names that would use it.* The
  tell was available before any code: the item's own sentence said "pointed at a representation".
- **A counter that has never read non-zero is not evidence of a zero.** 3-8a's population
  instrument read 0 on all seven suites *and* on the shape it was built for, and was reverted
  rather than reported. §3.5 already had the rule from `0083` — where the enable went next to the
  wrong one of two identical lines — and the same failure recurred here in a new form: **the enable
  for a COMPILE-time counter was placed among the run-time censuses, which are switched on after
  the corpus has finished compiling.** Fixing the placement changed nothing, which is what said the
  problem was the instrument and not the placement. *Before believing a zero, make the instrument
  produce a non-zero on a shape you constructed to produce one.*
- **"The suite that has the names is the suite that has the traffic" is not the same claim as "the
  names have the traffic", and only the arm tells them apart.** Item 3-8a's population came out as
  15 names in NavierStokes, which is also the suite carrying 9.46 M `LocalSlot` update steps, and
  the scoping treated the alignment as read. Built and measured, **those 15 names carry 835 584 of
  the 9.46 M — 8.8%.** The count was right and the inference on top of it was wrong, which is the
  same shape as item 3-6's 290 names being *inferred* from offered-minus-dropped rather than
  counted. *A population and a traffic figure that live in the same suite still need multiplying,
  and the multiplication is an A/B, not an argument.*
- **A cost you write down as the price of a change should be measured before it is written down,
  because it may not be that change's price at all.** `0097` recorded one deadlock as what the
  execution lock cost. Measured, there were **two**, and the first belonged to `0096`'s job queue —
  a change earlier than the note blaming the lock for it. The control row is what separates them: a
  host wait on unrelated work completes on both builds, so the two failures are mechanisms rather
  than one symptom seen twice. *A named cost is a claim; run it against each build that could have
  caused it before attributing it to the newest one.*
- **A concurrency counter measures the wrong thing by default, and the default is plausible enough
  to ship.** The detector built to check "one thread runs JavaScript in a context at a time" counted
  threads inside JavaScript **process-wide** on its first version. That is not the invariant: two
  independent contexts running in parallel is exactly what an embedder is supposed to be able to do,
  so the counter would have reported legitimate concurrency as a violation and fired on any
  full-suite run, where xUnit evaluates several test classes at once. *Before trusting a counter that
  checks an invariant, state the invariant's scope and check the counter has the same one.*
- **"In principle" in a written-up residual is a measurement not taken.** `0096` recorded that a job
  posted with nothing running "could in principle land during a later execution". Measured, it did
  so in **172 of 200 rounds**. The honesty of naming the gap was worth something; the estimate inside
  it was worth nothing, and the two are easy to mistake for each other in a document that otherwise
  insists on numbers.
- **A test that fails only under load is a race, and the race is more likely in the engine than in
  the test.** `SuspendingNestedFunctionsCaptureThroughTheSameBox` had passed every full-suite run in
  this phase; a saturated container made it fail three times in four, and what it was reporting was
  the engine running **user JavaScript on two threads in one context at once**. Both dispatch paths
  for a promise job were wrong — the thread pool when no `SynchronizationContext` was present, and
  `SynchronizationContext.Current` when one was, because a test host's context is not a JavaScript
  thread — and *each covered for the other's absence*, which is why a fix for one of them measured
  clean on a console harness and still failed the suite. **A rate measured on a loaded machine and
  re-measured on a quiet one is not an A/B**; what settled it was a fixture built to lose the race
  deterministically. *When a flake is timing-dependent, make the timing lopsided on purpose before
  believing any fix for it.*
- **A precondition count can close an item for the price of an instrument, and it is the cheapest
  outcome available.** Item 3-9's specification said to count first; the count came back **0 names
  and 0 offers on all seven suites**, so a mechanism that was sound, guard-free and genuinely
  attractive was declined without being written. Set that beside 3-8a directly above, whose
  population *was* real and which was built and lost anyway: *the count does not always say build,
  and the item that gets counted is the one that can be closed cheaply either way.* The counter
  stays in the tree with the condition that would re-open it written down — 3-9's supply is bounded
  by item 3-7's eight captured numeric locals, so widening 3-7 is the only thing that changes the
  answer.
- **A representation change is priced by the read/write ratio of the population, and that ratio
  has to be counted before the representation is built.** 3-8a's storage half does exactly what it
  was built to do — 835 584 update steps take a native double add and box nothing — and the corpus
  got **2.1% MORE boxes**. Three consumers were then built to close the gap, each a reasonable guess
  at where the remaining boxes were: the guarded tree's leaf and the element read took it to 1.7%,
  the element write to 1.2%. Only then was a counter added **at the read** (`CreateSpeculativeRead`,
  a fourth factory entry beside `CreateLiteral` and `CreateConversion`), and it settled the item in
  one line: **394 000 boxes minted reading, ≈5 300 removed.** The steps it takes off `Increment`
  mostly do not save an allocation at all, because they are `x[++i]` and the result is boxed to be
  an index either way. *Count the losing side at its own site before building the winning one — four
  builds and a measured regression is what it costs to count it afterwards.* Note the symmetry with
  item 3-1's bitwise operators, where the rule was *count how many of a fast path's operands can
  reach it*: here the operands reached it, and the **other** side of the trade was the uncounted one.
- **Every premise can survive and the item can still lose.** 3-8a's scoping A/B held exactly as
  measured — the enclosing-scope read is the defeat, testing it at run time removes the row, the
  population is real. *An item is not validated by its premises being true; it is validated by the
  number at the end, and the two can point opposite ways.*
- **A fixture written against a broken emitter can pass, and passing is not evidence.** Three of
  3-8a's read-path fixtures passed against the *bug they were written to catch*, for two different
  reasons: the trees they built were refused by an eligibility gate before the new leaf ran, and the
  ordering fixture's `i = "2"` defeated the local's candidacy at compile time, so the path under test
  was never emitted. *After writing a fixture for a new fast path, break the emitter deliberately and
  confirm the fixture fails* — the same discipline §3.5 already demands of a counter, applied to
  tests. It caught a stale-slot read (`"0!"` for `"3!"`) that no amount of re-reading the test had.
- **A sampling profiler is not automatically an instrument.** `dotnet-trace`'s sample profiler
  inflates this driver by ~29% and attributes 28% of self time to `Thread.PollGCWorker`, the
  rendezvous point its own stack walks force threads to — *the biggest frame in the profile is the
  profiler*. Independently, compiled JavaScript lives in `DynamicMethod`s that do not symbolicate,
  so 47.8% of the run lands on `JSFunction.InvokeFunction` and 2.4% on a named function body. Both
  facts were cheap to establish and neither was guessable. *Check what a new instrument costs and
  what it can name before believing its largest row* — the counter it displaced (an exact GC pause
  duration) was four lines and had none of these problems.
- **A fixture that asserts an eligibility *refusal* is an alarm for the next item, and should be
  written to go off.** `0085`'s `AnUpdateOnAPropertyCostsTwoBoxesNotOne` failed when `0086` landed
  under it; `0084`'s `ATreeWhoseOrderCannotBePreservedIsRefused` failed when the order-preserving
  emission landed under it. Both times the failing test was the correct and cheapest notification
  that a successor had changed the mechanism, and both times the repair was the same: **restate it
  as the invariant on both settings of the new switch** — the answer is unchanged, only which form
  computes it moves. *Assert the count as well as the answer, because an answer-only fixture passes
  silently when the mechanism underneath it is replaced.*

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

**Regenerated by the workflow on 2026-08-03 at the pinned pointer** — `tests/octane/results/`,
Octane version 9, Chromium 149.0.7827.55 and Jint 4.15.3 on the same machine. This replaces the
2026-07-31 table, which quoted an engine five suites of which did not score and which is now
twenty-odd commits behind. Ordered by ratio, best first.

> **One repetition per suite.** `comparison.md` says so on its own face — *"a single run;
> deltas against it cannot be distinguished from noise"*. Read this table for **magnitude and
> for which side of a threshold a benchmark falls**, which is what phase 2's exit gate asks;
> do **not** read a delta out of it against any earlier table, including the one it replaces.
> A band needs `--repetitions` and is still owed (0-6).

| Benchmark | Chromium | Broiler | × slower | Jint | Dominant blocker |
|---|--:|--:|--:|--:|---|
| SplayLatency | 72 859 | 2 220 | **33** | 3 164 | — (best axis; GC pauses are fine) |
| Typescript | 90 558 | 2 492 | 36 | 2 556 | mixed; overhead amortized by real work |
| Splay | 44 461 | 626 | 71 | 839 | B1 allocation rate |
| Gameboy | 91 279 | 1 096 | 83 | 936 | B1 typed arrays, B3 exotic exclusion |
| RegExp | 10 263 | 122 | 84 | 210 | B5 — **and B5 names the wrong component**; see phase 5 |
| NavierStokes | 35 474 | 384 | 92 | 255 | B1 boxed array elements |
| PdfJS | 56 417 | 508 | 111 | 890 | B1, B5, B4 |
| Richards | 38 257 | 264 | **145** | 188 | B2 call cost, B3 shape transitions — **now inside 200×** |
| Box2D | 101 050 | 675 | 150 | 622 | B1 + B2 (no escape analysis, no inlining) |
| Crypto | 38 431 | 225 | 171 | 148 | B1 integer boxing |
| zlib | 80 429 | 390 | 206 | 5 339 | B1 integer boxing |
| CodeLoad | 31 180 | 137 | 228 | 3 659 | B4 eager compilation — **~27% of what it measures**, see 1-1 |
| EarleyBoyer | 93 399 | 404 | 231 | 416 | B1 allocation rate |
| RayTrace | 118 472 | 463 | 256 | 448 | B1 + B2 escape analysis |
| Mandreel | 48 186 | 166 | 290 | 92.8 | B1 heap traffic — **not B4 compile**, see 1-4 |
| DeltaBlue | 99 812 | 217 | **460** | 188 | **B2 polymorphic call cost — the one suite still outside 200×** |
| MandreelLatency | 66 469 | 14.5 | **4 584** | 789 | ~~B4 compile latency~~ — **measured: not compilation.** Pauses between render frames over already-compiled code; a 3.04× faster compile of `mandreel.js` moves it 0.992×. Points at B1 allocation rate / B7 |
| **Overall (geomean)** | **57 080** | **351** | **163** | **616** | spread (worst ÷ best suite) **139.8×** |

The shape of that list *is* the finding, and it has not changed: the extremes are front-end and
call-path, not arithmetic. The losses are concentrated in two subsystems rather than spread
evenly — which is what makes them addressable in a defined order.

**Three things this run says that the stale one could not.**

- **Richards is inside 200× and DeltaBlue is not** — 145× against 460×, the same split the
  local run found at 150× and 447×. Phase 2 moved Richards across the line and did not move
  DeltaBlue across it. That is the phase's exit criterion, answered.
- **Jint is the more informative column.** Against a managed interpreter on the same runtime
  Broiler is **0.569× overall** — behind, on a geometric mean of the 17 per-benchmark ratios.
  It is *ahead* on the call- and object-heavy suites this campaign has been working (Mandreel
  1.79×, Crypto 1.52×, NavierStokes 1.51×, Richards 1.40×, Gameboy 1.17×, DeltaBlue 1.15×,
  Box2D 1.09×) and far behind on three: **CodeLoad 0.037×, zlib 0.073×, MandreelLatency
  0.018×**. Two of those three are the front end and the third is latency, so the column
  agrees with §1.1 about where this engine's remaining structural gap is — and it does it
  against a reference that is not a JIT, which Chromium's column cannot.
- **The worst score is no longer a compilation problem.** MandreelLatency at 4 584× is still
  the tail, and 1-4 and 1-1 between them made compiling `mandreel.js` 3.04× faster and moved
  it 0.992×. It belongs to phase 3.

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
| **0-6** | **Run the Octane workflow and commit refreshed results** | **Done, and the results in `tests/octane/results/` are current** — regenerated by the workflow 2026-08-03 at the pinned pointer, against a same-machine Chromium 149 and Jint 4.15.3. **17 of 17 scores, all 15 suites `ok` for all three engines, nothing errored or timed out.** Geomean 351 against Chromium's 57 080 (163×) and Jint's 616 (0.569×); spread 139.8×. See §4.2 for the table and what may be read out of it. **The half still owed is the noise band**: the run is one repetition per suite, which cannot distinguish a delta from noise — it needs `--repetitions` (0-4 built the flag; nothing has used it in CI yet) |
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
> to `tests/octane/results/`**, which stays CI's to write. *(CI has since written them — the
> committed results are the 2026-08-03 workflow run at the pin, §4.2, and they agree with this
> table's verdicts: Richards 144.9× passes, DeltaBlue 460× fails.)*
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

**Targets — and two of the three were wrong, measured.** This phase was aimed at
MandreelLatency (4646×), CodeLoad (371×) and Mandreel (300×), on the reading that they
measure the front end. Both Octane suites have now been *run* against a phase-1 build rather
than reasoned about:

- **MandreelLatency measures no compilation at all.** Octane compiles `mandreel.js` at script
  load and times only the benchmark's `run` function; `runMandreel` renders 20 frames over
  already-compiled code and `MandreelLatency` is the RMS of the pauses between them. Tripling
  the speed of compiling that file moves **neither** score: Mandreel 138.0 → 137.0 (0.993×),
  MandreelLatency 12.70 → 12.60 (0.992×), four samples an arm, ABBA on one build. **It is an
  execution-pause benchmark, so it belongs to B1/B7 and phase 3, not to B4 and this phase.**
- **CodeLoad is about a quarter compilation**, not the whole of it — see 1-1, which did move
  it, 1.099×.

What *did* move on Mandreel is real and outside every score: the suite's wall clock went
**358.2 → 350.0 s**, non-overlapping over four runs an arm. **So phase 1's value is page-load
time, exactly as the sentence below always said — and Octane is a poor instrument for it,
because Octane deliberately excludes load from what it times.** Blocker **B4**. This is the
phase the engine roadmap had excluded (§1.1), and it is the item with the clearest value
outside Octane: **this is page-load time.**

Owner assemblies: `Broiler.JavaScript.Parser`, `.Compiler`, `.BuiltIns` — **and
`.ExpressionCompiler`, which 1-4 adds and which is where the phase's cost turned out to
be.** The three-way split the phase was told to take (B4) now exists as `--compile-scaling`,
and on **2 000 synthetic top-level declarations** it reads **parse ≈ 0.5%, expression-tree
construction ≈ 11%, IL emission ≈ 89%**.

**That split is a fact about that shape, and 1-1 established it does not carry over.** On the
real corpora, removing every nested function body's IL generation removes only **17–36%** of
compile time, so tree construction is a far larger share of a real program than of a wall of
stubs — the synthetic shape has almost no tree to build. Both numbers are useful and neither
generalizes: use the synthetic one for machine-generated declaration walls (Mandreel) and the
corpus one for everything else.

**The phase splits in two, and the split is not the one the items are numbered by.**
Mandreel and CodeLoad were paired throughout as "the front end", and they fail for
unrelated reasons: Mandreel is **wide** (1 364 top-level declarations in one scope, which
was quadratic — 1-4, landed, 3.04×) and jQuery is **deep** (532 functions nested in one
IIFE, 96.5% of its compile in bodies that are never called — 1-1, open).

### 1-1 · Lazy function compilation — **the emission half landed; re-specified by measurement**

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

#### What landed: defer the *emission*, not the compilation

**The capture mechanism was not built, and the item's win was taken without it.** The four
risks above are all properties of the **front end**, and they are all settled before a
function reaches the emitter: the source is parsed, early errors have thrown,
`LambdaRewriter` has decided which variables are captured and boxed them, and
`GeneratorRewriter` has run. What is left at that point is generating machine code for a tree
that is already correct. **Deferring that is the same prize with none of the risk** — a
deferred site cannot observe anything, cannot fail differently on any input the eager path
accepts, and produces byte-identical IL, because it is the same call on the same tree, later.

**Where.** `RuntimeMethodBuilder.Relay` generated each nested lambda's `DynamicMethod` while
emitting its enclosing lambda. It now registers the site instead
(`MethodRepository.RegisterDeferred`), and `Create` — which runs once per *closure
instantiation* — hands back a thunk over that instance's boxes. The first invocation
generates the method, memoized on the shared site; a function defined once and instantiated a
million times generates once, and one instantiated once and never called generates never.
`LambdaRewriter.Rewrite` stays eager and must: it decides the boxes the creation site
references.

**Two things had to be got right, and both were found by measuring rather than by reading.**

- **No stack handoff.** Wrapping generation in `CompilationStack.RunOnFreshStack` — matching
  what the eager path was inside — took the repository suite from 3.5 minutes to **over 20, at
  12% CPU**: blocked, not working. It is 1-2's own lesson about short sources, applied at the
  wrong granularity: a fixed ~180 µs handoff is nothing against one whole-script compilation
  and unaffordable once per function. It is also unnecessary, because `ILCodeGenerator` derives
  from `StackGuard` and segments onto a fresh stack only when a tree is actually deep enough.
- **The thunk's warm path is written in IL, not called.** A `DynamicMethod` does not inline
  what it calls, so a thunk that called `Instance.Resolve()` unconditionally cost **1.0247×**
  on a call-heavy script — a real regression on every deferred function that is ever invoked.
  Reading the field inline and branching to the slow path only when it is null takes that to
  **1.0009×**, with pairs straddling 1 in both directions.

**Result.** Compile only (the corpus is compiled and never invoked, which is CodeLoad's shape),
ABBA-interleaved on one build with `BROILER_JS_DEFER_IL` as the only difference, **one corpus
per process**, six pairs per arm:

| Corpus | Functions | Eager | Deferred | Time | Allocation |
|---|--:|--:|--:|--:|--:|
| **codeload-jquery** | 532 | 587 ms | **398 ms** | **0.661×** | **0.518×** |
| box2d | 982 | 914 ms | 581 ms | 0.636× | 0.553× |
| pdfjs | 949 | 1 607 ms | 1 107 ms | 0.689× | 0.549× |
| codeload-closure | 57 | 51 ms | 37 ms | 0.714× | 0.657× |
| mandreel | 1 476 | 7 115 ms | 5 923 ms | 0.832× | 0.544× |
| typescript | 1 763 | 1 657 ms | 1 805 ms | **1.034×** | 0.516× |

Steady state — a script that compiles *and* runs 6.3 M calls across three shapes — is
**1.0009×** by median paired ratio. So the change is free where it does not help.

**The benchmark this item names was run, and it passes** (§3.5: *a benchmark named as an
item's justification is a test that item has to pass*). Octane **CodeLoad**, ABBA-interleaved
on one build with `BROILER_JS_DEFER_IL` as the only difference, 24 samples per arm:

| Arm | n | Median | Range |
|---|--:|--:|--:|
| eager | 24 | 94.6 | 89.1–103.0 |
| **deferred** | 24 | **104.0** | 98.0–110.0 |

**1.099× (higher is better), with 93.1% pairwise dominance** — 536 of 576 deferred-vs-eager
sample pairs favour deferral. Worth noting how nearly this was mis-called: the first pair of
runs separated cleanly (94.3 vs 105) and the *reversed* pair did not (99.2 vs 99.4). At three
samples an arm, against a suite whose own noise band is 7.5%, either pair alone would have
been read as a verdict. It took 24.

**And it re-frames the item, because 1.099× is not what 1-1 predicts.** The item says "a large
multiple on both is the expected outcome; if it is not, the measurement is wrong before the
change is." The measurement is not wrong — the same payload's isolated *compile* moved 1.51×
(0.661× time). Solving for the share: if compilation were fraction *f* of CodeLoad's measured
time, `f × 0.661 + (1 − f) = 1/1.099`, giving **f ≈ 0.27**. So **compilation is about a
quarter of what CodeLoad measures**, not the dominant term this document has assumed since
§1.1 called it "the two that measure nothing but the front end". The rest is executing the
payload — jQuery's initialization runs a great deal of code, and the functions it calls have
their generation forced. That estimate is derived from two measured ratios rather than
observed directly, and it is the first thing 1-3 should check.

**Three honest qualifications, and the first corrects something this document said.**

1. **"Emission is 89%" is true of the synthetic shape and not of these corpora.** That figure
   comes from `--compile-scaling`'s top-level declarations, where a lambda is a stub and almost
   all the work is emitting it. On the real corpora, deferring every nested body removes
   **17–36%** of total compile time (jQuery 32%, Box2D 36%, PdfJS 31%, Closure 29%, Mandreel
   17%) — so on real source, expression-tree construction is a far larger share than 11%. The
   phase-level claim above has been qualified accordingly; *a split measured on one shape is a
   fact about that shape.*
2. **Typescript is consistently slower and it is not explained.** 1.034× on the final build,
   every one of six pairs above 1 (1.004–1.077), while its allocation halves like everything
   else's. It is not GC — allocation moves the same 0.52× as the corpora that gain. It is
   recorded rather than explained, because a plausible story with no measurement behind it is
   what §3.5 exists to stop.
3. **A deferred site retains its expression tree.** Sites are registered under a `GCHandle`
   that is never freed (pre-existing), so an un-generated site holds its tree for the life of
   the process where a generated one holds only its `DynamicMethod`. The tree is released the
   moment a site is forced. This is also what made the *first* measurement of this change
   wrong: with all six corpora in one process, the ones measured last paid for the ones before
   them and read 1.6× and 2.6× **slower**, with bimodal ratios — the tell that it was ordering
   and not the change.

**The three-engine comparison was run over both named suites.** Not `--only` — that makes the
harness skip the comparison by design ("its suite set is a subset, so folding it into the full
comparison would silently drop every suite it skipped") — but a **two-suite manifest**, which
makes it a full run over exactly CodeLoad and Mandreel and emits `comparison.md` honestly.
Chromium 3 reps, Broiler 3 reps, Jint 1:

| Benchmark | Chromium | Broiler | Jint | Broiler × slower | Broiler / Jint |
|---|--:|--:|--:|--:|--:|
| CodeLoad | 19 334 | 82.4 ⚠ | 1 997 | 234.6× | 0.041 |
| Mandreel | 28 359 | 104 | 58.5 | 272.7× | **1.778** |
| MandreelLatency | 35 865 | 9.8 ⚠ | 499 | **3 659.7×** | 0.020 |

⚠ = spread over 3 repetitions exceeded the 7.5% noise band (CodeLoad 15.9%, MandreelLatency
9.5%). All six suite runs completed `ok`; nothing to diagnose.

**Four things this table is not.** *(1)* Its geomean (44) and spread (15.6×) are over **three
scores**, not the suite's seventeen — they are not comparable to the committed run's 354 and
249×, and must never be quoted as if they were. *(2)* Broiler's absolute scores here are well
below the same build measured alone earlier the same day (CodeLoad 82.4 against 104, Mandreel
104 against 137): a three-engine session is a busier machine than a one-suite run, which is
§3.2's rule about comparing only within a run, showing up. *(3)* **Jint ran one repetition, and
`comparison.md`'s header said "Repetitions per suite: 3"** — the header took that number from a
single engine and presented it as global. **Found by running the thing, and fixed**: the summary
now records repetitions per engine, reports one number only while they agree, and otherwise names
each — the same comparison now reads *"Chromium 3, Broiler 3, Jint 1 — the engines differ, so each
column is that engine's own median, and Jint is a single run whose deltas cannot be distinguished
from noise"*. The ⚠ spread column follows Broiler's own count too, rather than whichever engine
came first, since it describes Broiler's samples. Eight checks added to
`tests/octane/harness-selftest.mjs`, five of which fail against the old harness. *(4)* Jint ran in a separate, later, quieter process after
the first attempt was killed, so its column is the weakest of the three.

**What it does show.** Broiler is **1.778× ahead of Jint on Mandreel** — the suite whose compile
1-4 tripled — and far behind it on the two that are not compile-bound: 0.041× on CodeLoad, whose
payload Jint evaluates far faster, and 0.020× on MandreelLatency. *A managed interpreter beating
this engine by 25× and 50× on those two is the sharper statement of where phase 1 and phase 3
have left to go, and it is a comparison no Chromium column makes visible.*

#### The remaining half, measured before it was built

**"On real source that is the larger half" was an inference, and it is now a reading.** It had
been derived — the ceiling table says 92–96% of compile is bodies, the deferral A/B says emission
is 17–36% of compile, and the remainder was read off as tree construction — which is the move
§3.5 has a rule about and item 3-6 paid for once already. `--compile-phases` takes the split
directly, on the real corpora rather than on `--compile-scaling`'s declaration walls, one corpus
per process, five repetitions (three for Mandreel), medians per column. Deferral on, i.e. the
engine as it ships:

| Corpus | parse | tree construction | emission | total | tree share | parse share |
|---|--:|--:|--:|--:|--:|--:|
| codeload-jquery | 14.7 ms | **79.9 ms** | 54.8 ms | 149.4 ms | **53.5%** | 9.8% |
| codeload-closure | 4.2 ms | 15.0 ms | 25.4 ms | 44.7 ms | 33.6% | 9.4% |
| box2d | 25.8 ms | **116.0 ms** | 99.2 ms | 240.9 ms | **48.2%** | 10.7% |
| pdfjs | 93.4 ms | **427.0 ms** | 173.0 ms | 693.3 ms | **61.6%** | 13.5% |
| typescript | 67.5 ms | **452.0 ms** | 188.3 ms | 707.7 ms | **63.9%** | 9.5% |
| mandreel | 650.5 ms | **3 037.2 ms** | 2 724.5 ms | 6 412.3 ms | **47.4%** | 10.1% |

**The claim holds and is sharper than it was stated.** Expression-tree construction is the single
largest phase on five of six corpora, parse and tree together are 43–75% of what the engine spends
compiling, and the parse — the part 1-1 may *never* defer, because a syntax error in a function
that is never called is still a `SyntaxError` — is only **9.4–13.5%**. So what is left of the item
is almost entirely tree construction, and the early-error risk that dominates the item's text
costs a tenth of it.

It also corrects the phase's own headline once more. `--compile-scaling`'s synthetic split reads
parse 0.5% / tree 11% / **emit 89%**; on real source, with the emission half landed, emission is
the *middle* term on five of six and the largest only on Closure, the smallest corpus here. The
qualification already recorded — "a split measured on one shape is a fact about that shape" — is
right, and the distance between the two shapes is larger than it was described.

**And the population is now counted, which the ceiling table never did.** `--defer-population`
reads item 1-1's own registration and forcing counters — both touched once per *site*, never per
call — after evaluating each corpus the way its harness does. Evaluating and stopping is not an
approximation of CodeLoad's shape, it *is* CodeLoad's shape:

| Corpus | sites registered | forced (invoked) | never forced | share |
|---|--:|--:|--:|--:|
| codeload-jquery | 415 | 68 | 347 | **83.6%** |
| codeload-closure | 51 | 5 | 46 | 90.2% |
| box2d | 978 | 101 | 877 | 89.7% |
| pdfjs | 804 | 105 | 699 | 86.9% |
| typescript | 1 574 | 235 | 1 339 | 85.1% |
| mandreel | 2 697 | 8 | 2 689 | **99.7%** |

**84–99.7% of a real script's functions are never invoked at load**, so the population the
remaining half would serve is nearly all of them. Between the two tables the item is well
founded: the work is over half the compile and almost none of it is needed.

**One correction the population count forces, and it is to this item's own ceiling table.**
`--compile-profile` stubs every *outermost* function body, and **jQuery has exactly one** — the
IIFE the library is written inside, 99.91% of the source by bytes. Its control is therefore an
empty file, and the **96.5%** in the table above is *everything except the parse*, not anything a
deferral can take: CodeLoad evaluates jQuery, which runs that IIFE. The sentence this document
has carried since — "532 functions nested in one IIFE, 96.5% of its compile in bodies that are
never called" — is wrong about the outermost body, which is called first and calls 67 more. The
right figure for jQuery is the population one, **83.6%**, and it is a different measurement rather
than a correction of the same one.

#### What landed for it: the closure rewrite is no longer walked once per level

**Measuring the phases found a repeat, in the phase 1-1 had already deferred.** With deferral on,
emission is 25–57% of compile and the closure rewrite is about half of *that* — which is odd for a
compile that generates IL for one lambda. It is not odd: `LambdaRewriter.Rewrite` descends through
nested lambdas (that is how `CheckForClosure` threads a capture up the whole chain), and
`RuntimeMethodBuilder.Relay` then called it **again** with the relayed lambda as its own root.
A lambda at depth *d* was walked *d+1* times, and jQuery's single IIFE means its whole tree was
walked twice by a compile that emitted almost nothing.

**Counted rather than argued, and then counted again for the claim that matters.** Two counters on
the relay, one per site: how many relayed sites needed a rewrite of their own, and how many had
already had one from the walk that emitted their parent. On jQuery, Box2D and Typescript the first
is **0 — 0 of 415, 0 of 978, 0 of 1 574.** But *"every relay is a repeat"* and *"the repeat does
nothing"* are two claims and only the first is what that counter measures — §3.5's rule about
indirect instruments, which this campaign has now paid for twice. A third counter answers the
second directly: with the switch **off**, so the repeat still runs, count the captures it creates
that the first walk had not. It is **0 on every corpus** — 0 against
415, 51, 978, 804 and 1 574 repeats respectively — which is what makes the skip a removal of
repeated work rather than of work.

`Relay` now skips a lambda a descending walk has already entered, marked on the lambda itself.
The mark is set only by a walk that rewrites nested lambdas, so `RewriteRootOnly` — the async
pre-rewrite, which stops at each nested lambda *by design* — leaves it clear, and anything built
after the walk (a generator or async body rewritten into a state machine) is rewritten at relay
exactly as before. That is why the skip is a fact about this tree rather than a guess about which
lambdas need rewriting. `BROILER_JS_RELAY_REWRITE_ONCE=0` restores the repeat.

**Measured, and one corpus of three does not separate.** `--compile-profile`'s whole-compile
number, ABBA-interleaved on one build with the switch as the only difference, one corpus per
process, six pairs an arm:

| Corpus | repeat (median) | skipped (median) | median pair ratio | pairs favouring the skip | control-arm spread |
|---|--:|--:|--:|--:|--:|
| codeload-jquery | 502.5 ms | 394.5 ms | **0.782×** | **6 of 6** | 13.0% |
| typescript | 1 940.6 ms | 1 711.3 ms | **0.867×** | **6 of 6** | 15.9% |
| box2d | 793.2 ms | 882.0 ms | 1.055× | 2 of 6 | **55.6%** |

Box2D is reported rather than dropped, and the last column is why it cannot be read either way:
its own *control* arm ranges 662–1 103 ms, a 55.6% spread against an effect near 10%. So the
whole-compile instrument answers on two corpora and is silent on the third.

**The phase that changed was then measured directly, and `--compile-phases` carries its own
control**: `parse` cannot be affected by this change, so a round whose parse column moves is a
round that measured the machine. Two rounds an arm; the first moved parse by 1.49× and 1.86× and
is discarded on that basis alone. In the round where the control holds to 6%, **Box2D's emission
phase goes 99.9 → 54.8 ms, 0.549×, and its whole compile 267.4 → 207.2 ms, 0.775×** — the same
ratio jQuery's whole compile shows on the other instrument. jQuery's own phase round has its
control drift 27%, so it is quoted only for direction: 59.1 → 28.4 ms.

*Two instruments, three corpora, one discarded round and one non-separating corpus is a weaker
result than a single clean table would be, and it is what the machine gave.* What is not
weak is the mechanism: the walk removed is provably a repeat on every site of every corpus, and
provably creates nothing.

**What is still open.** Tree construction itself is still eager, and the two tables above say what
it is worth: **43–75% of compile, over a population that is 84–99.7% never invoked.** Closing it
needs the capture mechanism, and **one sentence of this item's cost side needs correcting before
anyone starts.** "Baking the cells in as constants is not available — `EmitConstant` throws for
any reference type that is not a `string`, `Type` or `MethodInfo`" is true and is not the
obstacle: the engine already carries per-instance reference state into a generated method, through
the `Box[]` the creation site passes and the `Closures` the delegate is bound to, and a compiler
that knows a name's *index* in that array binds it with an array load rather than a name lookup.
So the capture mechanism is not missing, it is **unaddressable**: the index is decided by
`LambdaRewriter` from the tree, and a deferred body has no tree. What the remaining half has to
build is the eager step that makes the array addressable without one — a free-name walk per
deferred function, resolved against the enclosing scopes and recorded as a name → index map, which
`--compile-phases` charges back as `scanMs` and measures at **0.9–164 ms against 15–3 037 ms of
tree construction, 5.4–9.9% of it** — a lower bound, since a real scanner also has to resolve each
name and tell a free reference from a locally bound one. **Size of what remains: L**, and the sub-project inside it is
that map, not a pre-parser and not `EmitConstant`.

**Verify.** `DeferredCompilationTests` — ten fixtures covering the item's four named risks
against the implementation (a syntax error in a never-called function still throws at compile
time; per-instance loop captures; writes through a captured cell after first call; self- and
mutual recursion through a thunk; direct `eval` inside a deferred body; a deferred generator;
100 instances of one site keeping distinct state; concurrent first calls). Full repository
suite **7 640 tests, 0 failures**.

**Verify (the relay-rewrite half).** `RelayRewriteTests` — 19 cases, and the point of every one is
that a capture has to be threaded through levels that never mention it: a read three levels down,
a write back through two, per-instance loop cells two levels down, a generator and an `async` body
nested in a closure (the two rewrites that build lambdas *after* the descending walk), `this`
through an arrow, a named function expression's own name, a direct `eval` two levels down, and
five levels each reading and writing every binding above them. **Each is asserted on both settings
of `BROILER_JS_RELAY_REWRITE_ONCE`**, so they are a statement about closure semantics rather than a
description of the skip — and with the switch off they all still pass, which is what says the
second walk was not what they were relying on. Plus the counter invariant, as a test rather than a
corpus reading. Full repository suite **7 944 tests, 0 failures**, all thirteen projects.

**And the conformance gate was run, which the first version of this section said had been skipped.**
All five pinned manifests at `07adeb44` plus `0082` — the tree `0aa8a558` now is, an ancestor of
the pin — on linux-x64: **8 710 executed, 8 617 passed,
84 failed, 251 skipped, 9 timed out — every count identical to §3.4's recorded row, manifest by
manifest**, and the same *files* rather than the same totals (all 84 failures need `$262`; the 9
timeouts are lines 7–15 of `test262-failures.txt`). The reason to run it despite the change having
no semantic surface is that "no semantic surface" was the claim under test: what `0082` removes is
a walk that decides which bindings a nested function captures, and `strict-mode` and
`lexical-declarations` are where a lost capture would show.

**A second unreproduced failure is on the record, and it is not the one above.** One full-suite
run reported `CapturedNumericLocalTests.SuspendingNestedFunctionsCaptureThroughTheSameBox(captured:
False)` — item 3-7's fixture — returning `"2,12"` for `"2,2"`. That case runs an `async` body with
an `await 0` and asserts the continuation has *not* run when `Eval` returns, so what it observed is
a scheduling order, not a capture: `out` was correct on both sides and only the post-`await`
statement had run. It did not reproduce in **six further runs of that assembly, three on each
setting of the switch**, nor twelve times in isolation on both settings, nor in the final full-suite
run. Recorded rather than dismissed, on the same terms as the `ModuleExtensions` flake above: if it
recurs, it is a test that asserts a microtask has not been drained, and that is what to look at
first.

**One unreproduced failure is on the record.** A single run of the full suite reported
`Broiler.JavaScript.ModuleExtensions.Tests` 1 of 5 failed. It did not reproduce in **nine
subsequent full-suite runs** — four with `BROILER_JS_DEFER_IL=0` and five with it on, same
build — nor in isolation. That project's first test guards a *module-initializer ordering*
bug by its own comment ("before the BuiltIns `[ModuleInitializer]` that wires it had run"), so
it is order-dependent by construction and a plausible pre-existing flake. **Eight runs cannot
separate a 1-in-10 flake from a 1-in-10 regression**, so this is recorded as unresolved rather
than dismissed: if it recurs, start here.

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
expression-tree construction and IL emission separately — and it was 1-4 that used it. On
2 000 top-level function declarations the three phases are **2.5 ms / 63 ms / 486 ms**: parse
noise, tree construction 11%, emission 89%.

**But that is the synthetic shape, and on real source the answer is the other way round.**
1-1's deferral removes *all* nested-body IL generation, and on the real corpora that is only
17–36% of compile time — so on a real program most of what is left, after parse, is
expression-tree construction. **1-3 is therefore a front-end item, not the emitter item the
synthetic split implied**, and the first thing it should do is take the same three-way split
on the corpora rather than on generated declarations.

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

> **In the pin.** Shipped as `patches/0061` while its push was blocked by a 403; since applied
> and pushed, and it is now **`e6222df3`**, an ancestor of the pinned `61c8cc65`. Measurement
> and instrumentation only — no behaviour change.

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

> **In the pin.** Shipped as `patches/0062` while its push was blocked by a 403; since applied
> and pushed, and it is now **`0812d80d`**, an ancestor of the pinned `61c8cc65`.

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

> **In the pin.** Shipped as `patches/0063` while its push was blocked by a 403; since applied
> and pushed, and it is now **`4d1c4796`**, an ancestor of the pinned `61c8cc65`.

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

> **In the pin.** Shipped as `patches/0064` while its push was blocked by a 403; since applied
> and pushed, and it is now **`fb1e2f4c`**, an ancestor of the pinned `61c8cc65`.

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

### 3-1 · Unboxed backing stores for dense arrays — **re-measured; it is the precondition for the six items already built**

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

#### Re-measured for the promotion 3-8 gave it — and the two findings say it is a *precondition*, not an option

3-8 moved this item to the front of the phase on a census: **42.01% of the corpus's allocation is
number boxing** (corrected from 41.89% once the constructor was counted as well as the factory —
a builtin writing `new JSNumber(x)` directly turns out to be **0.3%** of all boxes, so the earlier
figure was a lower bound and barely one). That promotion sat against this item's own 2026 finding
that a typed backing store is *a wash*. Both are right, and reconciling them is what this item
needed before anything was built.

**The element chain decomposes exactly, and every term is a box the operators mint.** New
`provability` and `element` rows in `--local-alloc`, each running the identical arithmetic the
raw-double control runs at **0.00**:

| Site | B/iter | |
|---|--:|---|
| `local-read-only` — `s = s + v`, `v` a raw double | **0.00** | the floor |
| `element-read-only` — `s = s + a[0]` | 31.98 | one box: the add's result |
| `literal-static-operand` — `a[0] * 2` | 32.00 | one box |
| `literal-fresh-operand` — `a[0] * 1.5` | **64.00** | **two** — and the second one is the literal |
| `element-multiply-only` — `s = a[0] * 1.5` | 64.00 | |
| `element-read-constant-index` — `s = s + a[0] * 1.5` | 95.99 | = 64 + 32, exactly |
| `element-read-variable-index` — `a[i & 1023]` | 128.08 | + one box for `i & 1023` |
| `element-read-write-chain` — read, arithmetic, store back | 159.67 | five boxes, the NavierStokes kernel |

Every figure is an exact multiple of 32 and the composition checks out to the hundredth, which is
what says the model is right rather than approximately right. **The element store is not in any of
them.** The boxes are minted by the *operators*, and the element read is free today precisely
because the value it hands back is already a box. So 3-1's own verdict holds — a typed store alone
trades a write allocation for a read allocation — while 3-8's promotion also holds, for a reason
3-1 never stated: **the operators cannot stay unboxed while their operands come out of an array.**

**Two things fell out of that decomposition, and both were measured rather than argued.**

- **A numeric literal is re-boxed on every evaluation.** `VisitLiteral` has shared statics for
  NaN, 0, 1 and 2 and emits a factory call for everything else, so `a[0] * 1.5` allocates *two*
  boxes where `a[0] * 2` allocates one. Counted over the corpus through a separate factory entry,
  literals are **1 671 331 of 133 936 952 requests — 1.2%, and at most 2.0% of fresh boxes**. Real,
  exactly demonstrated, and too small to justify either a thread-shared constant (the small-integer
  cache is `[ThreadStatic]` for a stated reason) or a per-activation local per literal. **Recorded
  and not built**, with the number that says why.
- **The bitwise and shift operators had no native form, and the analysis had been proving them
  numeric all along.** `NumericLocalAnalysis.IsNumericBinary` lists `&`, `|`, `^`, `<<`, `>>` and
  `>>>`, so a local assigned `i & 1023` stays numeric — while `TryCreateNativeNumericValue` did
  not, so the value went out to a `JSValue` operator and came back. Measured:
  **`s = i + 1023` is 0.00 B/iter and `s = i & 1023` is 31.84**, with both operands raw doubles and
  the result stored straight into one. *The analysis proved something the emitter could not use.*

#### The bitwise half is built, and its corpus result is the finding

The exclusion had a real reason, and it is why the operators live in `JSNumericOperators` rather
than as `BExpression` nodes: a bitwise operand is not the double but `ToInt32`/`ToUint32` of it
(§7.1.5/§7.1.6) — truncated toward zero, reduced modulo 2^32, NaN and the infinities mapping to 0
— and that reduction is **not** a CLR cast, which is undefined on overflow rather than wrapping.
Routing all six through `JSValue.ToUint32`, the same helper `IntValue` uses, makes them identical
to the boxed operators by construction. On its shape it removes the box completely:

| Site | native | generic |
|---|--:|--:|
| `bitwise-on-numeric-locals` — `s = i & 1023` | **0.00** | 31.84 |
| `element-read-variable-index` | **96.25** | 128.08 |
| `element-read-write-chain` | **96.00** | 159.67 |

**On the corpus it removes nothing.** Six of the seven suites come back with the box count
identical to the digit — a difference of exactly zero — and the seventh is **Crypto**, a
BigInteger implementation built on `&`, `|` and `>>` that mints 42.4 M boxes, 55% of its own
allocation, where the two arms differ by 3 126 in the *wrong* direction. That is not a result
either: running the **same arm twice** gives 42 418 727 and 42 421 217, so Crypto's own
run-to-run variation is larger than the gap between the arms. (It generates RSA keys, so its work
is not fixed across runs — worth knowing before quoting any Crypto delta, and the reason the
census figures elsewhere in this item are quoted per suite rather than to the digit.) The reason is the whole point of this item: the native form is chosen when **both**
operands are native, and Crypto's digits live in `this.array[i]`. An element read is not a numeric
local, so the operator it feeds is never eligible, however good its native form is.

*That is item 3-5's finding — "the emission is fine; what is on the other side is not" — arriving
for the second time from a different direction, and it is the sharpest evidence this phase has
produced about its own ordering.* Six items have now built machinery that array-resident data
cannot reach: unboxed indices (3-0), unboxed locals in four categories (3-3), an unboxed
comparison (3-5), a captured raw cell (3-7), and now unboxed bitwise operators. Every one of them
is correct, every one is invisible on the corpus, and **every one of them is waiting on the same
thing.**

**Shipped on by default, with `BROILER_JS_NATIVE_BITWISE=0` to restore the generic operators**, on
3-5's terms: 15 test cases pinning ToInt32 wrapping, NaN and both infinities, shift-count masking,
`>>>`'s unsigned result, the coercions a non-numeric operand must still get and a getter that must
run exactly once — **every one asserted on both settings of the switch**.

#### What the operators are handed at run time — counted, and it moves the item off storage

Everything above says the boxes are minted by the **operators**, whose operands arrive boxed from
array elements and object fields. What nobody had counted is the half that decides whether any
fast path can be fed: **how often a generic operator's two operands are already Numbers.** A native
form guarded on that test reaches exactly those invocations and no others — and this item has paid
for skipping that count once already, with a bitwise emission that is correct on 15 semantics cases
and removes zero boxes.

`ArithmeticOperandDiagnostics` (new, off by default, one counter pair on each generic
arithmetic and bitwise operator) over the seven-suite driver:

| Suite | generic invocations | both operands Numbers | share | not both | boxes allocated |
|---|--:|--:|--:|--:|--:|
| Richards | 55 198 | 55 197 | 100.00% | 1 | 13 659 |
| DeltaBlue | 14 533 | 14 532 | 99.99% | 1 | 6 765 |
| RayTrace | 844 051 | 844 044 | 100.00% | 7 | 841 017 |
| Box2D | 15 916 294 | 15 916 279 | 100.00% | 15 | 11 434 706 |
| EarleyBoyer | 26 116 | 26 112 | 99.98% | 4 | 563 997 |
| Crypto | 39 867 896 | 39 866 794 | 100.00% | 1 102 | 42 410 739 |
| NavierStokes | 17 094 558 | 17 094 557 | 100.00% | 1 | 29 977 465 |
| **Total** | **73 818 646** | **73 817 515** | **100.00%** | **1 131** | **85 248 348** |

**Every generic arithmetic invocation on the corpus but 1 131 arrives with two Numbers**, and that
population is **86.6% of every box the corpus allocates**. Nine hundred and seventy-four of the
1 131 exceptions are one suite's. So the guard a speculating native form needs is not a
coin-toss — it is a branch that predicts perfectly, and the type test costs a compare.

**The number next to it is the one that re-specifies the item.** The compiler's own proof —
`isLeftNumber && isRightNumber`, the gate every phase-3 item so far has widened — reaches
**556 053 of 73 818 646 invocations, 0.75%**, and even that figure is generous: it counts the
`AddValue(double)` overload, and **`+` is the only operator that has one.** `-`, `*`, `/` and `%`
re-box a raw double they already hold in order to meet the `JSValue` operator. *Compile-time
provability reaches 0.75% of the arithmetic; run-time truth reaches 100.00% of it.* Six landed
items have widened the first number.

**So the shared half is not a storage change, and 3-1's own re-specification above needs one more
correction.** "Storage plus an unboxed element read" assumes the problem is where the value is
kept. It is not: the operator already receives two Numbers whatever they are stored in. What it
cannot do is **hand one back** — its consumer is a `JSValue` local, slot or element, so the result
is boxed at the root of every expression. The shared half is therefore a **run-time-guarded
specialization of an arithmetic expression tree**: evaluate each leaf once, test the leaves for
Number, compute the whole tree in raw doubles, and box only the root. The per-shape figures already
in this item say what that is worth — `s = s + a[0] * 1.5` is **96 B, three boxes**, of which two
are intermediates, and the read-modify-write chain is **159.67 B, five boxes**, of which four are.
A typed backing store then becomes what it always measured as — a live-memory item — rather than
the precondition.

**And it partly reverses item 3-8's "do not start as written", without contradicting it.** 3-8
priced a run-time numeric guard at the **local** and found the whole local tier worth 0.36% of the
corpus's boxing. That verdict stands, because it is about the local. The same speculation applied
at the **operator** reaches 86.6% of the boxes. The two measurements are of different things and
this document had only ever taken the first: *3-8 measured where the guard was proposed; this
counts where the boxes are minted.*

**One smaller finding, and it corrects a reading of this item's own table.** A numeric literal is
**already** a native double — `ToNativeExpression` returns one — so the second box in
`a[0] * 1.5` is not "the literal being re-boxed by `VisitLiteral`" as an operand of that
multiply. It is the compiler boxing a raw double it already holds, because `*` has no
`JSValue × double` overload. The literal-re-boxing finding stands on its own count (1.2% of
requests) and is a different site.

*The counter's first version read **zero** on all seven suites, against 85 M boxes. The enable had
been inserted next to the wrong one of two identical `NumberBoxingDiagnostics.Reset()` lines — one
in a call probe, one in the driver — so the driver never turned it on. §3.5's "check that the
thing you measured is the thing you built", from the third direction: not a stale binary, not a
binary being rewritten, but an instrument switched on in the wrong method. A counter reading zero
is a claim about the counter first.*

#### The shared half is built: a guarded numeric tree

The census says the operators are handed two Numbers essentially always and cannot hand one back.
So the build is not a storage change and not a widening of the compile-time proof — it is a
**run-time-guarded specialization of an arithmetic expression tree**: evaluate each leaf once into a
temporary, test the leaves for Number, compute the whole tree on raw doubles, and box only the
root. `NumericSpeculation` / `FastCompiler.NumericSpeculation.cs`, on by default, with
`BROILER_JS_NUMERIC_SPECULATION=0` restoring the unguarded emission.

**Evaluation order is the whole correctness argument, and it is what makes the rule narrower than
the census.** The ordinary emission evaluates a node's two operands and *then* coerces them, so in
a nested tree a coercion runs between two leaf evaluations — and a coercion is observable, because
`ToPrimitive` on an object runs `valueOf`. Hoisting every leaf ahead of the test would move later
leaves in front of that coercion. A tree is therefore eligible only when **every leaf evaluated
after the first internal node in postorder is one that can neither cause nor observe anything** — a
numeric literal or a proven-numeric local. Leaves *before* the first coercion are unrestricted, and
that is not a corner: JavaScript's precedence makes `s + a[0] * 1.5` parse right-leaning, so all
three of its leaves precede the multiply. `(a[0] * 2) + p.v` does not, and is refused.

**When the guard holds nothing is skipped**, which is what makes the two arms the same program:
every operator here applies ToNumeric (for `+`, ToPrimitive then ToNumeric) to both operands, and
on a Number each is the identity. The native forms are `TryCreateNativeNumericValue`'s — the same
ones the all-native path emits — so the arms are identical by construction rather than by
inspection, the argument this item's bitwise half already used for `ToUint32`.

**Measured on the corpus**, one build, `BROILER_JS_NUMERIC_SPECULATION` the only difference:

| Suite | generic invocations off → on | | boxes allocated off → on | | trees |
|---|--:|--:|--:|--:|--:|
| Richards | 55 198 → 49 204 | 0.891 | 13 659 → 10 743 | **0.787** | 12 |
| DeltaBlue | 14 533 → 13 933 | 0.959 | 6 765 → 6 765 | 1.000 | 6 |
| RayTrace | 844 051 → 796 759 | 0.944 | 841 017 → 823 293 | 0.979 | 27 |
| Box2D | 15 916 294 → 13 870 770 | 0.871 | 11 434 706 → 10 663 471 | **0.933** | 424 |
| EarleyBoyer | 26 116 → 79 | **0.003** | 563 997 → 563 997 | 1.000 | 129 |
| Crypto | 39 872 917 → 23 249 552 | **0.583** | 42 412 174 → 33 356 341 | **0.786** | 191 |
| NavierStokes | 17 094 558 → 15 373 914 | 0.899 | 29 977 465 → 29 423 391 | 0.982 | 73 |
| **Total** | **73 823 667 → 53 354 211** | **0.723** | **85 249 783 → 74 848 001** | **0.878** | **862** |

**10 401 782 boxes removed — 12.2% of everything the corpus allocates, from 862 compiled sites.**
That is the first corpus-visible allocation result phase 3 has produced: 3-0, 3-3, 3-5, 3-7 and the
bitwise half of 3-1 moved **0.36% between them**, and this is thirty times that from one change.

**And it is well short of the 86.6% the census set as the ceiling, which the per-suite column
explains rather than excuses.** Crypto is the case the mechanism was built for — 0.583× of its
generic invocations, 0.786× of its boxes. **NavierStokes is not**: it loses 10.1% of its generic
invocations and **1.8%** of its boxes, so the great majority of its 30 M boxes are minted somewhere
that is not a binary arithmetic operator. EarleyBoyer is the sharpest version of the same thing —
**99.7% of its generic invocations removed and not one box** — because what it was doing there was
not allocating in the first place. *The census bounded what the operators could reach; it did not
say the boxes were all at the operators, and two suites now say plainly that they are not. That is
the next count, and it should be taken before anything else in this item is built.*

**One mistake, caught by measuring, and it is the item's own rule about populations arriving from
the other side.** The first eligibility condition required **two operators**, on the argument that a
single node mints one box either way — true of the *result*, and wrong, because it forgets the
*operand*: `a[0] * 2` costs two boxes today, the literal and the result, since only `+` has a
`JSValue × double` overload. Measured, that condition took the corpus from 10.4 M boxes removed to
**5.6 M — Crypto alone lost 4.7 M**. The condition that ships counts what the guard actually buys:
`(operators − 1) + native leaves ≥ 1`, i.e. one intermediate that never becomes a `JSValue`, or one
already-unboxed operand that no longer has to be boxed to meet a generic operator. *A savings rule
is a claim about the code and has to be measured like one; this one was reasoned and lost half the
prize.*

**And the wall clock, measured — with a control that comes free.** ABBA-interleaved at process
granularity, six pairs, one build, the switch the only difference, and the diagnostics counters
**off** for the timing pass (they had been enabled around the driver unconditionally; leaving them
would have charged the slower arm for 20.5 M interlocked increments it does not otherwise pay — a
bias pointing the same way as the result, which is the worst kind). The control is the corpus's
own: **DeltaBlue and EarleyBoyer remove exactly zero boxes** between the arms, so their time must
not move, and their spread is the noise floor.

| Suite | off (median) | on (median) | ratio | pairs favouring | |
|---|--:|--:|--:|--:|---|
| **Crypto** | 3 554 ms | 3 241 ms | **0.912×** | **6 of 6** | 0.857–0.961, entirely below 1 |
| Box2D | 6 652 ms | 6 596 ms | 0.991× | 5 of 6 | |
| NavierStokes | 1 929 ms | 1 890 ms | 0.982× | 4 of 6 | inside the noise |
| RayTrace | 2 327 ms | 2 260 ms | 0.972× | 3 of 6 | inside the noise |
| Richards | 689 ms | 690 ms | 0.995× | 3 of 6 | |
| DeltaBlue | 1 392 ms | 1 388 ms | 1.005× | 3 of 6 | **control** |
| EarleyBoyer | 3 805 ms | 3 808 ms | 1.006× | 2 of 6 | **control** |
| **Driver total** | **20 360 ms** | **19 916 ms** | **0.981×** | **6 of 6** | 0.946–0.994 |

**0.981× on the driver with six of six pairs, carried by Crypto at 0.912× with six of six**, and
the two control suites sit at 1.005× and 1.006× on 3-of-6 and 2-of-6 — they do not move, which is
what makes the rest readable. Their pair spread is ~11%, so **no per-suite effect under about 5%
can be called from this run**: Box2D, NavierStokes, RayTrace and Richards are all directionally
right and individually unproven. **No suite is slower.** The guard's losing side — a tree whose
operands turn out not to be Numbers pays a type test and then does what it did before — does not
show up anywhere, which is what the census predicted when it found 1 131 non-Number invocations in
73.8 M.

*The ratio between the two measurements is worth more than either.* **12.2% of the corpus's
allocation removed buys 1.9% of its execution time.** Allocation is not the dominant term in what
this engine spends, and that bounds the rest of phase 3 the way item 4-2b's 0.83% bounded phase 4:
the remaining boxes are worth having, and nobody should expect the next 12% of them to be worth
more than another ~2%.

**Verify.** `NumericSpeculationTests` — 33 cases, **each asserted on both settings of the switch**,
so each is a statement about JavaScript semantics rather than a description of the fast path. Values
(NaN, both infinities, −0 through `1/x`, `%` on infinity, ToInt32 wrapping and shift masking for all
six bitwise operators); types (a string leaf still concatenates under `+` and still coerces under
`*`, an object leaf still runs `valueOf`, a BigInt still throws on mixing, `null`/`undefined`/
booleans coerce as before); and **order** — a getter read exactly once and left to right, a `valueOf`
that mutates a later leaf, a getter that mutates a later leaf, and a throwing leaf that must stop
the next one being read. Plus two counter assertions, because every one of the 33 also passes when
the specialization never fires: one that the shape the item is written around *does* specialize, and
one that `(a[0] * 2 * 3) + p.v` is refused **by guard count** — one guarded leaf, not two — which
distinguishes "the root was refused" from "nothing was eligible", since the inner tree specializes
on its own. Full repository suite **7 963 tests, 0 failures**.

#### Where the remaining boxes are minted — every one of them, and it is not what this item assumed

The guarded tree left **74 835 575 boxes from 111 997 550 factory requests**, and the reading that
suggests itself is that they are **root** boxes: the value of a tree on its way into a `JSValue`
slot or element, which is exactly what a typed backing store — this item as originally written —
would remove. That is a hypothesis, and it is cheap to test: give the compiler's boxing conversion
its own factory entry (`JSNumber.CreateConversion`, counted apart from `Create` and `CreateLiteral`
exactly as the literal entry already is) and ask what share of a run's requests it is.

It is **18.4%**, and the first version of this section stopped there and called the hypothesis
falsified. That was half an answer: it left **40.5% of the corpus's requests attributed to nothing
at all**, which by §3.5 is a claim about the census, not about the engine. Two counters closed it.
`JSValue.BitwiseXor` turned out to be the one generic binary operator `0083` never hooked — a real
gap, though the corpus says a small one, below the run-to-run spread. The rest was **the unary
operators, which no census had ever looked at**: `-x` and `~x`, the `++`/`--` step, and the
`ToNumeric` that coerces the operand of `++`/`--`. That takes the unattributed share from **40.5%
to 1.0%**, measured in the arm that is *built* — the guarded tree on:

| Source | requests | share | what it is |
|---|--:|--:|---|
| Binary operators | 53 351 878 | 47.6% | what `0083` counted and `0084` consumes |
| **`++` and `--`** | **34 562 464** | **30.9%** | 17 281 232 steps and 17 281 232 `ToNumeric` coercions |
| Compiler conversion | 20 601 685 | 18.4% | a raw double crossing into a `JSValue` |
| Numeric literal | 1 671 314 | 1.5% | already native; re-boxed to meet an operator |
| Unary `-` and `~` | 702 031 | 0.6% | |
| **Unnamed** | **1 108 178** | **1.0%** | builtins reaching the factory directly |

**The root-box hypothesis is wrong, and most clearly wrong on the suite it was invented for.**

| Suite | requests | conversion | | binary | | `++`/`--` | | unnamed |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| **NavierStokes** | 36 669 153 | 1 827 793 | **5.0%** | 15 373 914 | 41.9% | **18 923 532** | **51.6%** | 1.0% |
| **Crypto** | 55 322 471 | 17 126 896 | **31.0%** | 23 247 219 | 42.0% | 14 417 806 | 26.1% | 0.3% |
| Box2D | 17 382 495 | 1 407 419 | 8.1% | 13 870 770 | 79.8% | 574 746 | 3.3% | 1.5% |
| EarleyBoyer | 756 617 | 114 468 | 15.1% | 79 | 0.0% | **608 502** | **80.4%** | 4.4% |
| RayTrace | 1 628 284 | 70 991 | 4.4% | 796 759 | 48.9% | 0 | 0.0% | 15.2% |
| Richards | 90 589 | 8 789 | 9.7% | 49 204 | 54.3% | 31 116 | 34.3% | 0.0% |
| DeltaBlue | 147 941 | 45 329 | 30.6% | 13 933 | 9.4% | 6 762 | 4.6% | 51.5% |

Only **5.0%** of NavierStokes' requests are the compiler carrying a raw double across into a
`JSValue`, so a typed backing store — which is what would remove those — cannot be why its boxes
survive. The suite where conversions *are* a large share is **Crypto at 31.0%**, and Crypto is the
one the guarded tree already served best. *The two suites are the opposite way round from the way
this item has assumed since it was written.* The conversion column is in fact the **ceiling on what
a typed store can remove without further operator work**, because where the tree already computes
natively the root is counted there: 5.0% on NavierStokes against 31.0% on Crypto.

**And the largest single source on the corpus's biggest boxer is `++`.** NavierStokes spends
**51.6%** of its boxing on increments and decrements, EarleyBoyer **80.4%**, the corpus **30.9%** —
more than the compiler conversion and the numeric literal together, and two thirds of what the
binary operators cost. **Exactly half of it is waste that is visible in four lines of source.**
`ToNumeric` ends `primitive.IsBigInt ? primitive : CreateNumber(primitive.DoubleValue)`, so an
operand that is *already* a `JSNumber` is copied into a second, equal `JSNumber` to be handed back
as the old value — and a JavaScript Number has no observable identity, so the copy can never be
detected. **17 281 232 requests, 15.4% of the corpus's boxing, for a value the engine is already
holding.** That is the next build, and it is the cheapest one this phase has surfaced: it is a
guard, not a mechanism.

*This is the third time in one item that a plausible mechanism has been checked and come back
wrong — the ceiling table, the "two operators" savings rule, and now the root-box hypothesis — and
the first time a residue was chased instead of rounded off. The 40.5% that "came from nowhere" was
not noise and not builtins; it was the operator every one of these suites runs most often, sitting
outside the census because the census was written around binary arithmetic. Each correction took
one counter and about ten minutes.*

#### The `ToNumeric` copy, removed — built straight off the census

`ToNumeric` coerces the operand of `++`/`--` and hands back the coerced old value, and it minted
unconditionally. So `n++` on a Number copied the Number into a second, equal `JSNumber`. **Reusing
it is sound because a JavaScript Number has no observable identity** — it compares by value, it
cannot carry a property, and `Object.is` on two Numbers is a value comparison — which is the same
argument the small-integer cache has rested on since P2-2, where unrelated call sites are already
handed the same instance. The guard is `primitive.IsNumber`, not `!primitive.IsBigInt`: a String,
a Boolean, `null` and `undefined` all reach this line and all still have to be coerced, which is
the whole reason `ToNumeric` exists (`"1"++` yields the Number 1, not the String).

Measured on the corpus, one build, `BROILER_JS_NUMERIC_UPDATE_REUSE` the only difference:

| Suite | requests | | boxes allocated | | of the removed requests, real |
|---|--:|--:|--:|--:|--:|
| **NavierStokes** | 36 669 153 → 27 207 387 | **0.742×** | 29 423 391 → 22 665 084 | **0.770×** | 6 758 307 of 9 461 766 — 71.4% |
| **EarleyBoyer** | 756 617 → 452 366 | **0.598×** | 563 997 → 282 000 | **0.500×** | 281 997 of 304 251 — 92.7% |
| Richards | 90 589 → 75 031 | 0.828× | 10 743 → 6 852 | 0.638× | 3 891 of 15 558 — 25.0% |
| Crypto | 55 327 970 → 48 114 381 | 0.870× | 33 357 396 → 33 352 279 | 1.000× | 5 117 of 7 213 589 — **0.1%** |
| Box2D | 17 382 495 → 17 095 127 | 0.983× | 10 663 471 → 10 661 949 | 1.000× | 1 522 of 287 368 — 0.5% |
| DeltaBlue | 147 941 → 144 560 | 0.977× | 6 765 → 6 765 | 1.000× | 0 of 3 381 — 0.0% |
| RayTrace | 1 628 284 → 1 628 284 | 1.000× | 823 293 → 823 293 | 1.000× | no updates at all |
| **Total** | **112 003 049 → 94 717 136** | **0.846×** | **74 849 056 → 67 798 222** | **0.906×** | 7 050 834 of 17 285 913 — 40.8% |

**17 285 913 requests removed, 15.4% — the census predicted 17 281 232, so the thing built is the
thing measured to 0.03%.** In allocations it is **7 050 834, 9.4%**, and *the gap between those two
numbers is the small-integer cache, which is the most useful thing in the table*: Crypto removes
7.2 M requests and **5 117 boxes**, because its updates are loop counters inside `[-128, 1024]`
where P2-2 was already answering them for free. NavierStokes' indices run past that bound, so
**71.4% of its removed requests were real allocations — 6.76 M boxes, 23.0% of everything it
allocates.** *A `++` on a small integer was already free; a `++` on anything larger was not, and
nothing before this said which suites were which.*

Set against the guarded tree's 10 401 782, this is **7 050 834 from a nine-line guard** — and it
lands on the suite the tree could not reach, NavierStokes, which the tree moved 1.8% and this moves
23.0%. **Together the two take the corpus from 85 255 034 boxes with neither switched on to
67 798 222 with both, 0.795×.** Five
coercions still mint on the reuse arm, which is the guard discriminating rather than a leak: those
operands are not Numbers.

**Wall clock, ABBA-interleaved at process granularity, six pairs, counters off for the timing pass
— and it is the sharpest reading phase 3 has produced, because of what it says about the suites
that did *not* move:**

| Suite | boxes removed | of its own | removed per second | median | pairs won |
|---|--:|--:|--:|--:|--:|
| **NavierStokes** | 6 758 307 | 23.0% | **4 240 469/s** | **0.906×** | **6 of 6** |
| EarleyBoyer | 281 997 | **50.0%** | 82 504/s | 1.002× | 3 of 6 |
| Richards | 3 891 | 36.2% | 5 842/s | 1.121× | 1 of 6 |
| Crypto | 5 117 | 0.0% | 1 767/s | 0.984× | 3 of 6 |
| Box2D | 1 522 | 0.0% | 255/s | 1.030× | 2 of 6 |
| RayTrace | 0 | 0.0% | 0 | 0.997× | 3 of 6 |
| DeltaBlue | 0 | 0.0% | 0 | 1.029× | 3 of 6 |
| **Driver total** | 7 050 834 | 9.4% | | **1.013×** | 2 of 6 |

**One suite moves: NavierStokes, 0.906× on six of six pairs, every pair between 0.862 and 0.928.**
The controls hold — RayTrace removes nothing and reads 0.997× — and **the driver total does not
move at all**, which the arithmetic predicts rather than contradicts: NavierStokes is 8.7% of the
driver, so 9.4% of it is **0.82% of the total**, under the total's own spread. Richards' 1.121× is
**not callable in either direction**: its own off-arm spread is 11.2% against a 12.1% effect, and
believing it would price 3 891 boxes at 18 µs each.

***The share of a suite's own boxes predicts nothing; the absolute rate predicts everything.***
EarleyBoyer **halves** its boxing — the largest proportional cut in the table — and reads 1.002×,
because 282 000 boxes over 3.4 s is 82 000/s. NavierStokes removes a smaller *share*, 23.0%, at
**fifty times the rate**, and is the only suite that moves. Every row in the table orders by rate
and none of them orders by percentage. That retires a habit this document has had since phase 3
opened — quoting a per-suite percentage of boxes as though it forecast time — and it sharpens
3-5's ceiling and 0084's *"12.2% of allocation buys 1.9% of time"* into something usable: **an
allocation item pays where the allocation rate is high in absolute terms, and nowhere else.**
NavierStokes mints 18.5 M boxes a second; EarleyBoyer mints 165 000. They are not the same kind of
problem and no single corpus figure can describe both.

`NumericUpdateReuseTests` — 9 fixtures, **each on both settings of the switch**, so every one is a
statement about JavaScript semantics rather than a description of the fast path: postfix and prefix
results, the non-Number operands that must still be coerced, NaN and the infinities, `-0` asserted
through both `1/x` and `Object.is` (it cannot survive the increment — `-0 + 1` is `1` — so the half
that matters is the old value), a `valueOf` that must run exactly once, a getter read once with the
setter seeing the increment, BigInt, the `Symbol` TypeError, and an element update. Plus **the
identity argument asserted rather than assumed** — `===`, `==`, `Object.is` and a property write
against a reused old value — and a counter invariant, because "the box count went down" would
otherwise be consistent with the coercion having stopped happening: `UnaryToNumeric +
UnaryToNumericReused` is equal on both arms and only the split moves.

**This build also broke one of `0085`'s own fixtures, which is the fixture working rather than a
cost.** `AnUpdateOnAPropertyCostsTwoBoxesNotOne` asserted the two-box cost, so it failed in all
three of the first suite runs the moment the reuse landed. It is now a Theory on both settings
asserting the invariant instead of the total — the **coercion count stays 1 either way**, and only
which side of the split it falls on moves. *A census fixture that survives the change it measured
would not have been measuring it.* Full repository suite **7 988 tests, 0 failures, on three
consecutive runs**.

**One caveat recorded rather than rounded off.** `Broiler.JavaScript.Integration.Tests` **stalled
once** on an earlier build of this stack — its host measured at *one jiffy of CPU over eight
seconds*, which is a hang and not slow progress. It did **not recur in six subsequent full-suite
runs**, three of them under `--blame-hang --blame-hang-timeout 300s` with no sequence file
produced, nor when that assembly was run alone, where it passed 4 571 in 47 s. It was not resource
exhaustion (27 GB disk and 13.7 GB RAM free, no stray processes). **Unexplained, not reproduced,
and not attributed to this change**; if it returns, `BROILER_JS_NUMERIC_UPDATE_REUSE=0` restores
the previous behaviour exactly and is the bisection. Separately,
`CapturedNumericLocalTests.SuspendingNestedFunctionsCaptureThroughTheSameBox` — the async-scheduling
intermittent 3-7 already records as unresolved — failed **once in those six runs**, which is the
first rate this document has for it.

#### Why the guarded tree reached a third of the census's ceiling — counted, not guessed

`0084` removed 12.2% of the corpus's boxes against a census ceiling of 86.6%, and its own section
said the per-suite column explained the gap: NavierStokes lost 10.1% of its generic invocations,
EarleyBoyer 99.7% and no boxes. What it did **not** say is *which of the six eligibility conditions
was doing the refusing* — the item had a numerator and no denominator, and by §3.5 that is a claim
about the instrument.

The waterfall it needed is the one item 3-6 already uses for hoisted names: attribute each
candidate to the **first** condition it fails, so the counts add up and each reads as *"widen this
and that many sites move"*. A candidate is a binary node whose operator has a native form —
counting anything else would put every `===` and `&&` in the denominator. One caveat the counter
has to be read with: a refused root **re-offers its children**, because `VisitBinaryExpression`
falls through to visiting the operands, so a refused chain contributes several rows.

| Suite | Specialized | AlreadyNative | NoSaving | **OrderUnsafe** | StringLeaf | With/eval |
|---|--:|--:|--:|--:|--:|--:|
| Richards | 12 | 1 | 22 | 11 | 1 | 0 |
| DeltaBlue | 6 | 1 | 13 | 9 | 4 | 0 |
| RayTrace | 27 | 1 | 126 | 77 | 1 | 0 |
| Box2D | 424 | 11 | 2 258 | 1 470 | 2 | 0 |
| EarleyBoyer | 129 | 16 | 111 | 36 | 2 | 2 |
| Crypto | 191 | 1 | 141 | 97 | 1 | 0 |
| NavierStokes | 73 | 9 | 47 | 62 | 1 | 0 |
| **Total** | **862** | **40** | **2 718** | **1 762** | **12** | **2** |

**862 of 5 396 candidate nodes specialize — 16.0%.** The two rules that turn down the rest are
`NoSavingToMake` (50.4%) and `OrderUnsafe` (32.7%), and *they are one finding rather than two*.
`+` is left-associative, so `a[0] + a[1] + a[2] + a[3]` parses left-leaning: the root is refused as
order-unsafe, its left child is refused as order-unsafe, and the bottom node — a single operator
over two unprovable leaves — is refused for having no saving to make. **A chain of *k* operators
produces *k−1* OrderUnsafe rows and one NoSaving row and specializes nothing.** The savings rule is
correct wherever it fires on a genuinely standalone `x op y`; most of the time it is firing on the
residue of a chain the order rule already declined.

**And the sub-census says the order rule is not refusing what this phase assumed it was.** Recorded
at the first blocking leaf, one-for-one against the OrderUnsafe row:

| Blocking leaf | count | share |
|---|--:|--:|
| A named property read, `o.x` | **1 028** | **58.3%** |
| An identifier that is not a proven-numeric local | 593 | 33.7% |
| Anything else | 83 | 4.7% |
| **A computed element read, `a[i]`** | **34** | **1.9%** |
| A call's return value | 24 | 1.4% |

*The element read is 1.9%.* Phase 3 has spent six items on the premise that array-resident data is
what its machinery cannot reach, and on this rule the blocked leaf is an **object field** — Box2D
alone contributes 984 of the 1 028. NavierStokes, the suite whose arrays the premise was written
about, is blocked 18 times by an element and 39 times by a plain name. **The order rule is not an
array problem and widening it is not a storage change**, which is the third time this item has had
a mechanism checked and come back pointing somewhere else.

#### The guard moves to where the coercion was — and that is the whole fix

The hoisting form is bounded by the fact that it *hoists*: every leaf is evaluated into a temporary
ahead of one combined test, so a leaf that moves in front of a coercion has to be one that can
neither cause nor observe anything. Nothing requires the leaves to move. Emitting each leaf at its
own postorder position and putting the test **where the coercion it stands in for would have run**
preserves the reference order exactly, and then the purity rule has nothing left to protect.

`NumericTreeOrdering` / `CreateOrderedNumericTree`, on by default, with
`BROILER_JS_NUMERIC_TREE_ORDER=0` restoring the hoisting form gate and all — two emitters rather
than one because the difference has to be attributable, and comparing against
`BROILER_JS_NUMERIC_SPECULATION=0` would charge this change for everything `0084` does.

**The soundness argument is `0084`'s, read from the other end.** The reference emission evaluates a
node's two operands and then coerces them, so the left operand's coercion runs *after* the right
operand is evaluated and *before* anything above the node is. This emits the leaves in that same
order and tests at that same point. When the test holds, the coercion it replaces was the identity
— ToNumeric of a Number is that Number — so nothing observable is skipped. When it fails, the same
generic operator runs at the same point over the values already in hand.

**What it costs is a two-armed node instead of a two-armed tree.** Each internal node carries a
`bool` saying its subtree stayed numeric, a raw `double` holding the value when it did and a
`JSValue` holding it when it did not, and one branch. So a failure part-way up no longer discards
the native work below it: the accumulated double is boxed once, at the node that failed, and the
rest of the tree proceeds generically — which the hoisting form cannot do, since its fallback is
the whole generic tree.

**Measured on the corpus**, one build, `BROILER_JS_NUMERIC_TREE_ORDER` the only difference — so the
`off` column is `0084`+`0086` as they ship and every removal below is this change alone:

| Suite | generic invocations off → on | | boxes allocated off → on | | trees |
|---|--:|--:|--:|--:|--:|
| Richards | 49 204 → 49 204 | 1.000 | 6 852 → 6 852 | **1.000** | 12 → 12 |
| DeltaBlue | 13 933 → 1 | 0.000 | 6 765 → 6 732 | 0.995 | 6 → 10 |
| RayTrace | 796 759 → 347 614 | 0.436 | 823 293 → 481 887 | **0.585** | 27 → 40 |
| Box2D | 13 870 770 → 4 152 413 | 0.299 | 10 661 949 → 5 225 033 | **0.490** | 424 → 1 090 |
| EarleyBoyer | 79 → 79 | 1.000 | 282 000 → 282 000 | **1.000** | 129 → 128 |
| Crypto | 23 249 298 → 338 328 | **0.015** | 33 349 915 → 13 412 191 | **0.402** | 191 → 208 |
| NavierStokes | 15 373 914 → 1 738 413 | 0.113 | 22 665 084 → 11 747 635 | **0.518** | 73 → 75 |
| **Total** | **53 353 957 → 6 626 052** | **0.124** | **67 795 858 → 31 162 330** | **0.460** | **862 → 1 563** |

**36 633 528 boxes removed — 54.0% of everything the corpus allocates — and 87.6% of the generic
arithmetic invocations that were left.** Set against the rest of the phase: `0084` removed 12.2%,
`0086` 9.4%, and 3-0, 3-3, 3-5, 3-7 and the bitwise half **0.36% between them**. Taken from the
baseline before any of the three, the corpus goes **85 255 034 → 31 162 330, 0.366×**.

**The refusal waterfall is the check that it happened for the stated reason** rather than by some
other route: OrderUnsafe **1 762 → 0**, and NoSavingToMake **2 718 → 1 181** without that rule being
touched at all — which is the chain-residue prediction above coming out, since a root that
specializes no longer offers its bottom node as a separate candidate. Specialized goes 862 → 1 563.
Two rows grow because trees now reach conditions they used to be refused before: StringLeaf 12 →
123, and TooManyLeaves 0 → 8 (below).

**The leaf cap had to be re-measured, because the order rule had been hiding it.**
`MaximumSpeculativeLeaves` was 8 and *never fired on the corpus* — the order rule refused those
trees first. The ordered form accepts whole chains, and at 8 it turned 85 of them down, 80 of them
Box2D's. At 16 that is 8, and the corpus loses a further **664 338 boxes, 2.1%** (Box2D 0.954×,
NavierStokes 0.983×, Crypto 0.985×) while the *tree count falls* — Box2D 1 109 → 1 090 — because a
longer chain absorbs sub-trees that were separately specialized. *That is `0084`'s "two operators"
mistake avoided rather than repeated: a threshold is a claim about the code and this one moved the
answer by 2.1% the first time it was measured.*

**And the wall clock, ABBA-interleaved at process granularity, six pairs, counters off**, with the
corpus's own controls — Richards and EarleyBoyer remove **exactly zero** boxes between the arms, so
their time must not move:

| Suite | off (median) | on (median) | ratio | pairs won | boxes removed per second |
|---|--:|--:|--:|--:|--:|
| **NavierStokes** | 1 680 ms | 1 406 ms | **0.834×** | **6 of 6** | **6 500 000/s** |
| **Crypto** | 3 098 ms | 2 790 ms | **0.893×** | **6 of 6** | **6 437 000/s** |
| RayTrace | 2 284 ms | 2 224 ms | 0.959× | 5 of 6 | 149 000/s |
| Box2D | 6 315 ms | 6 358 ms | 1.003× | 3 of 6 | 861 000/s |
| DeltaBlue | 1 310 ms | 1 324 ms | 0.966× | 4 of 6 | 25/s |
| Richards | 704 ms | 732 ms | 1.002× | 3 of 6 | **0 — control** |
| EarleyBoyer | 3 713 ms | 3 793 ms | 0.999× | 3 of 6 | **0 — control** |
| **Driver total** | **19 080 ms** | **18 634 ms** | **0.969×** | **6 of 6** | 1 920 000/s |

**0.969× on the driver with six of six pairs**, carried by NavierStokes at 0.834× and Crypto at
0.893× — both six of six, both entirely below 1 (0.793–0.899 and 0.866–0.926). **The two zero-box
controls read 1.002× and 0.999×**, which is what makes the rest readable; their own pair spread is
~12% and ~14%, so no per-suite effect under about 5% is callable and RayTrace and DeltaBlue are
directionally right and individually unproven. **No suite is slower** by more than its control's
noise.

***And the standing lesson from `0086` predicts every row of that table, including the one that
looks wrong.*** Box2D removes **5 436 916 boxes, 51% of its own** — proportionally more than Crypto
— and reads 1.003×, because that is 861 000 boxes a second against NavierStokes' 6 500 000. The
two suites that move are exactly the two above ~6 M/s; nothing between 25/s and 861 000/s moves at
all. *The share of a suite's own allocation still forecasts nothing; the absolute rate still
forecasts everything*, and this is the second independent run to say so.

**The exchange rate is also worth stating, because it is the third reading of the same constant.**
`0084` bought 1.9% of execution time with 12.2% of the allocation; this buys **3.1% with 54.0%**.
Allocation is simply not the dominant term in what this engine spends — three measurements now
agree on that within a factor of about two, and it is the number anyone sizing the rest of phase 3
should start from rather than the box counts.

**Verify.** `NumericTreeOrderTests` — 11 fixtures, **every value case on both settings of
`BROILER_JS_NUMERIC_TREE_ORDER`**, so each is a statement about JavaScript rather than a
description of the fast path: the hoisting arm reaches these answers by refusing to specialize and
the ordered arm by specializing correctly, and a disagreement is the bug the file exists to catch.
Left-leaning chains of elements and of fields; **a valueOf that mutates a later leaf of a
three-node tree**; **a throwing coercion that must beat a later leaf that would also throw**, which
is the sharpest one in the file because both arms throw and only the *message* says whether the
order held; four getters logging that every leaf is read once and left to right; a failure
half-way up that must leave the rest generic with `valueOf` run exactly once; a String defeating
the guard mid-chain so `+` becomes concatenation from that node up; BigInt mixing from the middle
of a chain; NaN, the infinities and −0 carried through several nodes as raw doubles; ToInt32
wrapping at every node; and a thousand-iteration element kernel. Plus three counter assertions,
because all eleven also pass when nothing specializes: that the tree `NumericSpeculationTests` pins
as refused now takes **two** guarded leaves instead of one, that a four-element chain moves from
`OrderUnsafe` to `Specialized` **by refusal reason** rather than merely by count, and that the
order-blocker sub-census discriminates a property read from an element read and reports nothing at
all once the conjunct that consults it is gone.

**This change also broke one of `0084`'s own fixtures, and that is the fixture working.**
`ATreeWhoseOrderCannotBePreservedIsRefused` asserted the refusal this removes, so it failed the
moment the ordered emission landed — the same way `0085`'s `AnUpdateOnAPropertyCostsTwoBoxesNotOne`
failed when `0086` landed under it. It is now a Theory on both settings asserting the invariant
instead of the refusal: **the answer is 25 either way**, and only which form computes it moves
(one guarded leaf on the hoisting arm, two on the ordered one). *That is twice in three items that
an eligibility fixture has caught its own item's successor, which is the argument for asserting
counts and not only answers.*

Full repository suite **8 063 tests, 0 failures** across 14 assemblies.

#### The denominator this phase never had — collection is 1.8% of the driver, and it is not where an allocation item pays

Three items have now measured an allocation cut against wall clock and got roughly a sixth of the
share back — `0084` 12.2% → 1.9%, this 54.0% → 3.1% — and the document has recorded the ratio three
times without ever asking *what the collector was costing in the first place*. The whole of phase 3
is priced in boxes; nothing said what a box is worth.

`GC.GetTotalPauseDuration()` answers it exactly rather than by sampling — it is the runtime's own
accounting of how long execution was suspended — and it is four lines in the driver. Taken per
suite on both arms of this item, three runs each, medians:

| Suite | elapsed off → on | GC pause off → on | pause share | gen0 off → on |
|---|--:|--:|--:|--:|
| Richards | 677 → 725 | 5 → 5 | 0.8% | 1 → 1 |
| DeltaBlue | 1 311 → 1 320 | 25 → 27 | 2.0% | 1 → 1 |
| RayTrace | 2 322 → 2 345 | 55 → 55 | 2.4% | 48 → 43 |
| Box2D | 6 895 → 6 802 | 108 → 86 | 1.5% | 9 → 8 |
| EarleyBoyer | 3 955 → 3 894 | 78 → 92 | 2.2% | 5 → 5 |
| Crypto | 3 217 → 2 844 | 66 → 44 | 1.8% | 81 → 33 |
| **NavierStokes** | 1 732 → 1 411 | 67 → 39 | **3.3%** | 16 → 7 |
| **Total** | **20 109 → 19 341** | **404 → 350** | **2.0% → 1.8%** | |

**Collection is 1.8–2.0% of the driver.** And the decomposition is sharper than the level:
**768 ms of wall clock came off and 54 ms of it was collection — 7%.** *The other 93% is the
mutator's own allocation work* — the pointer bump, the zeroing, the write barriers and the cache
traffic of touching a gigabyte of fresh memory — which no GC counter reports and which is where an
allocation item actually pays.

The same run corroborates the box counters from outside them: **allocated bytes fall 4.00 GB →
2.92 GB**, against a prediction of ~1.0 GB from 36 633 528 boxes at 24–32 B each. Two independent
instruments, one counting objects at the factory and one counting bytes at the allocator, agree.

**So the ceiling on everything left in this phase can now be stated rather than guessed.** At the
measured rate — **711 ms per GB removed**, or **12–21 ns a box** depending on whether the six-pair
ABBA total or this run's is used — the **0.70 GB of number boxes still standing (24% of the 2.92 GB
that remains) is worth about 495 ms, 2.6% of the driver**, and a typed backing store reaches part of
that rather than all of it.

***This retires an assumption the phase has run on since it opened, and confirms a non-goal that
was until now asserted rather than measured.*** §Non-goals says GC work is out of scope because
"the allocation **rate** is a severe problem […] not […] the collector". That is now a measurement:
the collector costs 1.8% and the allocation costs about fourteen times what the collection of it
does. Aiming at the collector would have been aiming at a fourteenth of the problem.

#### And a sampling profiler cannot decompose this engine, which is a finding about item 4-5

Item 4-5 stopped at *"~85% of a call's fixed cost is still unattributable from outside the engine,
so the rest of 4-5 is blocked on a profiler rather than a design"*. A profiler was tried here —
`dotnet-trace` with `Microsoft-DotNETCore-SampleProfiler`, converted to speedscope and aggregated
by self time — and it does not lift the block. Two reasons, both worth recording so nobody spends
the afternoon again:

- **The JavaScript does not symbolicate.** Compiled JavaScript runs in `DynamicMethod`s, and the
  stack walker resolves almost none of them: **47.8% of the profiled run sits under
  `JSFunction.InvokeFunction` with a JavaScript frame below it on the stack that has no name**,
  against **2.4%** that reaches a named `dynamicClass.<function>-<file>` body. So the profile can
  say *"this is JavaScript executing"* and essentially nothing else about which JavaScript.
- **The largest single frame in the profile is the profiler.** `Thread.PollGCWorker` takes
  **28.0%** of self time — which is not collection, since the exact counter above says collection
  is 1.8% — it is threads rendezvousing at GC poll points so the sampler can walk their stacks.
  The profiled run takes **25.4 s against 19.3–20.1 s unprofiled**, and that ~29% inflation is the
  same 28%. *A profile whose biggest frame is its own suspension mechanism is measuring itself.*

**Neither is a reason to distrust the GC number**, which comes from a counter and not from the
sampler, and which the two arms' allocated-bytes agree with. It is a reason to stop treating "get a
profiler on it" as an available next step for phase 4: it needs one that can name a `DynamicMethod`,
and that is a different tool.

#### The census re-taken on the far side, and it hands the item back its original premise

`0085` gave the compiler's boxing conversion its own factory entry and used it to refute the
root-box hypothesis: **18.4% of the corpus's requests, and only 5.0% of NavierStokes'** — so a
typed backing store could not be why its boxes survived. That was right about the engine as it then
was. Re-taken on the arm that ships now, the same counters say something different, and the
difference is what this change did:

| Source | hoisting arm | | order-preserving arm | |
|---|--:|--:|--:|--:|
| **Compiler conversion** — a raw double crossing into a `JSValue` | 20 603 254 | 21.8% | **24 649 016** | **47.4%** |
| **`++` / `--` step** | 17 281 964 | 18.2% | 17 281 954 | **33.2%** |
| Binary operators | 53 353 957 | **56.3%** | 6 626 052 | 12.7% |
| Numeric literal | 1 671 332 | 1.8% | 1 671 332 | 3.2% |
| Unary `-` and `~` | 702 031 | 0.7% | 702 031 | 1.3% |
| Unnamed | 1 108 187 | 1.2% | 1 107 019 | 2.1% |
| **Total requests** | **94 720 730** | | **52 037 409** | |

**The conversion column did not grow much — 20.6 M to 24.6 M — the rest collapsed underneath it.**
It is now the largest single source of boxing on the corpus, and it grew *because* the guarded tree
works: a tree that computes natively boxes once, at the root, and a root box is a conversion. Per
suite the concentration is sharper still — Crypto **17.06 M** conversions against 13.41 M
allocations (the gap is the small-integer cache), NavierStokes 1.83 M → **4.04 M**, Box2D 3.21 M.

***So the root-box hypothesis was false when `0085` tested it and is true now, and this change is
what made it true.*** Everything a typed store cannot reach has been taken out from in front of it.

**And the second survivor points the same way.** The `++`/`--` step is untouched at 17.28 M and is
now **33.2%** — 9.46 M of it NavierStokes', 7.21 M Crypto's. `0086` removed the *coercion* half,
which was a copy; what is left is the **new value being stored back**, and `a[i]++` has to box it
because the element is a `JSValue` slot. That is a storage cost wearing an operator's name.

**Conversion plus update step is 80.6% of everything the corpus still boxes, and both are the same
sentence: a raw double crossing into a `JSValue` slot or element.**

#### Where the `++`/`--` step's operands live — counted, and not one of them is an element

The re-specification below named this as the count to take before a typed backing store is built,
on a stated disjunction: *if the operand is an element or a field the step shares that mechanism,
and if it is a local the analysis merely failed to type, it is a much smaller change aimed
somewhere else entirely.* It is the second, and not marginally.

The compiler already knows where the operand lives — it emits a different branch for each — so the
census is that knowledge carried into the step as a compile-time constant. The rows are recorded by
an overload while the total stays with `Increment` itself, so **the rows sum to `UnaryUpdate` by
construction** and a call site the emitter forgot shows as a shortfall rather than vanishing. Every
suite balances.

| Suite | step | Element | Property | LocalCell | **LocalSlot** | GlobalOrWith | Other |
|---|--:|--:|--:|--:|--:|--:|--:|
| Richards | 15 558 | 0 | 15 558 | 0 | 0 | 0 | 0 |
| DeltaBlue | 3 381 | 0 | 933 | 0 | 2 448 | 0 | 0 |
| RayTrace | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Box2D | 287 373 | 0 | 15 051 | 0 | 272 322 | 0 | 0 |
| EarleyBoyer | 304 251 | 0 | 0 | 93 | 19 149 | 285 009 | 0 |
| Crypto | 7 209 815 | 0 | 16 453 | 260 | **7 193 102** | 0 | 0 |
| NavierStokes | 9 461 766 | 0 | 0 | 6 | **9 461 760** | 0 | 0 |
| **Total** | **17 282 144** | **0** | 47 995 | 359 | **16 948 781** | 285 009 | **0** |

**Not one of the corpus's 17.28 M `++`/`--` steps is on an array element. 0.3% are on an object
field. 98.1% are on a local or parameter the numeric analysis did not prove numeric** — a
`LocalSlot`, meaning a name that resolved statically, so it never took the dynamic path, and stayed
a `JSValue` because nothing could type it.

***So the step is not a storage problem at all, and the disjunction resolves against the typed
store.*** Weighted by each suite's own request-to-allocation ratio, the step is **≈7.05 M real
boxes — 22.6% of the 31.16 M the corpus still allocates** — and **6.76 M of that is NavierStokes'
`LocalSlot` alone**, on the suite with the highest absolute boxing rate in the corpus, which is
where §3.5's rate lesson says an allocation item pays and nowhere else.

**Reading NavierStokes' source says exactly which locals, and it is one cascade.** The hot updates
are `++currentRow;` and `x[++currentRow]` in `lin_solve`, `advect` and `project` — never
`a[i]++`, which is why the Element column is a clean zero. `currentRow` is initialized
`var currentRow = j * rowSize`, and `rowSize` is a `FluidField`-scope `var` assigned inside
`reset()` from `width + 2`, where `width` is assigned inside `setResolution()` from a parameter.
So the analysis cannot type `rowSize`, therefore cannot type `currentRow`, therefore every
`++currentRow` boxes. Item 3-6's waterfall on the same run agrees to the name: NavierStokes has
**141 hoisted names and 24 numeric locals**, with the drops attributed to `OtherName` (17, the
outer-scope bindings) and `DroppedCandidate` (18, the cascade from them).

***One closure variable the analysis will not type costs 6.76 M boxes.***

**This re-opens item 3-8 on the same terms `0083` re-opened it once already.** 3-8 was told not to
start "as written" because it priced a run-time numeric guard at **the local** and measured the
whole static tier at 0.36% of the corpus's boxing. That verdict was about *the tier as built*.
Priced at the *update operator* — the same move that took a compile-time proof reaching 0.75% of
the arithmetic and replaced it with a run-time test reaching 100.00% — the population the tier
**misses** is 22.6% of what the corpus still allocates. *3-8 measured what the mechanism catches;
this measures what it lets through, and the two numbers differ by sixty-fold.*

**One adjacent gap, found by reading the emitter and worth naming rather than building.**
`++currentRow;` in statement position throws its value away, and the compiler has that concept —
`FastCompiler`'s `assignmentInStatementPosition` sets it so `n = 5;` on a numeric local stores an
unboxed double. It is set for **assignments only**; an update expression gets `discardResult` from
the `for`-update clause and from nowhere else, so a bare `++x;` statement boxes even when `x` is a
raw double. On this corpus that is worth **nothing measurable**, because the locals it would serve
are not numeric in the first place — which is the honest reason to record it next to the item that
would make it pay rather than to build it now.

#### Re-specification

**3-1 returns to the storage change it was written as — and for the first time the objection that
took it off storage does not apply.**

- **The wash is gone, because the consumer now exists.** The item's original measurement said a
  typed store trades a write allocation for a read allocation, since the dense store is
  `IPropertyValue[]` and every read has to hand back an `IPropertyValue`. That was true of a
  compiler with nowhere to put a raw double. The guarded tree's leaf slot **is** that place: it
  saves each leaf and immediately reads `DoubleValue` off it, so an element read that could answer
  in a raw double would feed it directly and box nothing. The item is still **storage plus an
  unboxed element READ the numeric operators can consume** — joint `Broiler.JavaScript.Storage` and
  `Broiler.JavaScript.Compiler`, still an **XL** — but the second half now has a caller.
- **The ceiling is measured rather than assumed.** `boxingConversionRequests` is exactly what a
  typed store can remove without further operator work: **24 649 016 requests, 47.4%**, against the
  18.4% `0085` measured before the operators were cleared.
- **3-2 is the same argument for object fields and is now the larger half on two suites**, not one:
  Box2D 3.21 M conversions and Crypto 17.06 M. They remain one mechanism with two backends.
- **The `++`/`--` step is a third item's worth and it belongs with them**, since what it boxes is
  a value going into a slot or an element. 17.28 M requests, and it is NavierStokes' largest
  remaining source.
- **The bitwise emission is still waiting, and its wait is now shorter.** It cost one file and 15
  tests, it is correct today, and the day an element read yields a raw double it starts collecting
  on Crypto without another line being written.

**What the wall clock says about all of it should be read first, and it now has a mechanism behind
it rather than an observed ratio.** Collection is **1.8% of the driver**, and of the 768 ms this
item removed only **54 ms — 7% — was collection**; the rest is the mutator's own allocation work.
At the measured **711 ms per GB**, the **0.70 GB of number boxes still standing is worth about
495 ms, 2.6% of the driver**, and a typed backing store reaches a part of that rather than all of
it — the conversion column is 47.4% of *requests*, and requests are not allocations, because the
small-integer cache answers a large share of Crypto's for free.

**So the honest statement of what is left in phase 3 is: an XL for something under 2%.** That is
worth having and it is not worth doing ahead of anything with a better ratio. Two consequences for
sequencing:

- **The `++`/`--` step is the cheaper bid, and the count is now taken: it belongs to the numeric
  local, not to storage.** **Element 0, Property 0.3%, LocalSlot 98.1%** of 17.28 M steps — ≈7.05 M
  real boxes, **22.6% of everything the corpus still allocates**, 6.76 M of it NavierStokes' alone.
  So it shares no mechanism with a typed store and must be sequenced against it rather than after
  it. What it wants is **item 3-8's run-time guard, re-priced at the operator** — 3-8 measured the
  static tier's yield (0.36%) and concluded "do not start"; this measures what that tier lets
  through, and the two differ sixty-fold. *That is the same correction `0083` made when it moved
  the guard from the local to the arithmetic operator, arriving a second time by a different
  route.*
- **Nothing in phase 3 should be started on a box count again.** Every item from here is bidding
  against 2.6%, and the number to beat it with is a rate — ms per GB, or ns per box — not a share.

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

### 3-2 · Unboxed doubles in shape slots — **measured; its premise sentence is wrong and its population is one suite**

The object-field twin of 3-1: `shapeSlots` holds `JSValue` references, so
`vector.x = 1.5` allocates. This is what RayTrace and Box2D need, and it **composes
with 2-1** — a shape that knows a slot is a double can store it raw, so land 2-1 first
and this gets cheaper.

**Where.** `Runtime/JSObject.cs`, `Runtime/ObjectShape.cs`. **Size: L.**

#### `vector.x = 1.5` does allocate, and not for the reason the item gives

The item's premise is one sentence, and it names a cause. Taken literally as a probe — the item's
own example, then the same store varying only where the value came from:

| Site | B/iter | |
|---|--:|---|
| `local-write-control` — the same arithmetic into a raw-double local | **0.00** | the floor |
| **`o.x = 2`** | **0.00** | **the slot store allocates nothing** |
| `o.x = 1.5` | 32.00 | one box — and it is the **literal**, not the slot |
| `o.x = v * 1.5`, `v` a raw double | 32.00 | one box — and *this* is the slot |
| `field-read-only` — `s = s + o.x` | 31.98 | |
| `field-read-write-chain` — read, arithmetic, store back | 96.00 | |

**`o.x = 2` allocates nothing at all**, because storing an already-boxed `JSValue` into a slot is a
reference copy. So `shapeSlots holds JSValue references, so vector.x = 1.5 allocates` is right that
the line allocates and wrong about why: what it pays for is the **literal**, which `VisitLiteral`
re-boxes on every evaluation (item 3-1 measured that at 1.2% of the corpus's boxing requests). The
slot's own cost appears only in the row the item does not write — `o.x = v * 1.5`, where the value
*is* a raw double at the point of the store and the slot cannot hold it. That is 32 bytes, the same
32 bytes this phase has now measured eleven times.

**And the field rows match the element rows to the hundredth**: `field-read-only` 31.98 against
`element-read-only` 31.98, `field-read-write-chain` 96.00 against `element-read-write-chain` 96.00.
*3-1 and 3-2 are not twins by analogy, they are the same numbers.* One mechanism — a value that
stays unboxed from its producer to its consumer — with two storage backends.

#### Which suite each item can actually reach, counted

The signal that settles it is the one **4-1 deliberately left uncollected** ("numeric-vs-generic
per site") and that 3-8 then named as the thing nobody could answer. It is one branch on the
inline cache's two hit returns: of the reads the cache answers, how many hand back a number.

| Suite | cache-answered reads | of them numeric | | fresh boxes | boxing share |
|---|--:|--:|--:|--:|--:|
| **Box2D** | 18 241 436 | **9 853 002** | **54.0%** | 11 629 732 | 36.60% |
| DeltaBlue | 470 291 | 64 274 | 13.7% | 13 794 | 0.64% |
| Crypto | 651 289 | 74 457 | 11.4% | **42 423 644** | **55.17%** |
| EarleyBoyer | 76 599 | 7 802 | 10.2% | 564 024 | 1.92% |
| RayTrace | 353 058 | 31 267 | 8.9% | 872 934 | 4.98% |
| Richards | 272 432 | 21 380 | 7.8% | 14 671 | 1.51% |
| **NavierStokes** | **388** | **0** | **0.0%** | **29 977 471** | **66.96%** |
| **Total** | **20 065 493** | **10 052 182** | **50.1%** | | |

**Half of every property read the cache answers hands back a number**, which is the number 3-2 was
missing — and the per-suite column is the one that decides the plan:

- **3-2 is a Box2D item.** Of the corpus's 10.05 M numeric reads, **98% are Box2D's**, against its
  11.6 M boxes — so an unboxed slot could serve most of what that suite mints. RayTrace, the other
  suite the item names, does 353 058 reads in total and is 4.98% boxing; it is not a target.
- **3-2 cannot touch 3-1's suites, at all.** NavierStokes mints **29 977 471** boxes and performs
  **388** property reads, **zero** of them numeric. Crypto mints 42 423 644 and reads 651 289.
  Together they are **85% of the corpus's boxes and essentially no property traffic**: their
  numbers live in `new Array` read by index. *No amount of work on shape slots reaches them.*

#### Re-specification

**3-1 first, then 3-2, and the split is now quantitative rather than a guess.**

- **3-1 carries 85% of the corpus's boxes** (NavierStokes' 30.0 M plus Crypto's 42.4 M) and is
  reachable by nothing else. **3-2 carries Box2D's 11.6 M**, where 54% of reads are already
  numeric. Both are worth doing; only one of them is most of the phase.
- **They are one mechanism.** The identical per-iteration figures say the compiler half — a value
  that stays unboxed from producer to consumer — is shared, and only the storage differs. Building
  either one without that half reproduces 3-1's measured wash, and building the half twice would be
  the waste this measurement exists to prevent.
- **The item's stated composition with 2-1 still holds and is now cheaper than written**: 4-2b's
  specialized read already resolves a monomorphic read to a **literal slot index** on 44.7% of the
  corpus's executed reads, so the site that would consume a raw slot largely exists.
- **`vector.x = 1.5` should be struck from the item's rationale.** It allocates for a reason that
  belongs to `VisitLiteral`, is worth 1.2% of requests, and pointed the item at the wrong half of
  its own mechanism for as long as it stood unmeasured.

### 3-3 · Widen the unboxed-locals eligibility gate — **complete: all three halves landed**

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

#### What was left of 3-3, and it outranked what landed first

`let`/`const` and the block-scoped `var` were still ineligible, and the measurement moved them
**ahead** of where the item put them, for a reason the item could not have known:

- They cost the same per site as a parameter did — **31.98 B/iteration**, charged per
  *assignment* rather than once per call, so on a loop they dominate what a cell ever cost.
- They can reach the **numeric** tier, which parameters cannot: a `const v = 3.5` at function
  top level is exactly as provable as the `var` beside it, and the TDZ condition is already
  satisfied by the dominance argument `NumericLocalAnalysis` uses today — the declaration
  must be a direct statement of the function body with no textual reference before it.
- And they carry the multiplier: re-qualifying one binding re-qualifies every local
  downstream of it, which is the 1 → 3 in the table above.

So the successor item was **`let`/`const` at the numeric tier first**, then the block-scoped
`var` (which does need the definite-assignment analysis the item names). **Both have now
landed, and item 3-3 is complete**: all four categories the item named are at the eligible
floor except `parameter`, which cannot reach the numeric tier at all — the value arrives as a
`JSValue` and nothing proves it is a number, which is why its half landed at the *scalar* tier
instead.

| Site | Before 3-3 | After all three halves |
|---|--:|--:|
| `top-level-var` — the eligible floor | 0.00 B/iter, 3 | 0.00, 3 |
| `let-binding` | 31.98, 1 | **0.00, 3** |
| `const-binding` | 31.98, 1 | **0.00, 3** |
| `block-var` | 31.98, 1 | **0.00, 3** |
| `parameter` | 31.98, 1 | 31.98, 1 — *scalar* tier only; see the parameter section above |

#### `let`/`const` — **landed on the second attempt**

The first attempt is recorded below, because it was withdrawn on a miscompile and the
instruction it left behind ("find what else decides a lexical binding's storage") is the reason
the second attempt was scoped the way it was. **The second attempt reproduces the number and
not the defect.**

**What it does.** `NumericLocalAnalysis` offers a function-body-top-level `let` or `const` on
the same terms as a `var`; `VisitBlock`'s **numeric** gate admits a lexical name when the block
is the function's own body; and `VisitVariableDeclaration` tests `NumericStorage` *before* the
lexical branch rather than after it.

**What it deliberately does not do, and this is the whole difference from the first attempt.**
The **JSValue tier stays closed to lexical names.** The two tiers are not interchangeable:

| | JSValue tier (`useScalarLocal`) | Numeric tier (`useNumericLocal`) |
|---|---|---|
| admits a name because | it is an ordinary local nothing captures | the analysis **proved** it only ever holds a number |
| TDZ | nothing proves the dead zone unobservable | the dominance argument does — any name referenced before its declaration is rejected, so the throw is **unreachable**, not removed |
| const-ness | nothing proves no write happens | a const written anywhere is rejected outright, so there is no assignment whose `TypeError` could go missing |

A `let`'s dead zone and a `const`'s read-only-ness are both properties of the `JSVariable`
**cell** that either tier removes. Only the numeric tier's gate discharges them, so only it may
admit a lexical name.

*Whether the first attempt relaxed the shared condition instead is an **inference**, not
something checked — the branch was not kept, which is precisely why this section exists. It is
offered as the most likely reading of its recorded symptom ("none of those nested bindings is
one the gate admits") and should not be repeated as fact.*

**Measured, both arms from one tree, `--local-alloc`:**

| Site | Before | After |
|---|--:|--:|
| `let-binding` | 31.98 B/iter, 1 numeric local | **0.00 B/iter, 3** |
| `const-binding` | 31.98 B/iter, 1 numeric local | **0.00 B/iter, 3** |
| every other row (12 of them) | — | **byte-identical, numeric-local count unchanged** |

— identical to `top-level-var`, the eligible floor. The multiplier the section above predicts
is the second column: one binding re-qualified, and the accumulator and counter that read it
came with it.

**On the withdrawn attempt's defect: it did not reproduce, and it is not explained.** The
recorded reproduction was re-run against this implementation — two evaluations in one process,
fresh `JSContext` each, which is the only configuration that could ever see it — and all three
lines answer correctly. It was then re-run with `BROILER_JS_REWRITER_INDEX_THRESHOLD` set above
any real scope and with `BROILER_JS_DEFER_IL=0`, which between them restore the pre-1-4 and
pre-1-1 front end, since both landed *after* the withdrawal and `LambdaRewriter` was the one
plausible place a binding's storage is decided outside the gate. Green under all four
configurations. The three compiler files this item touches are **byte-identical between
`2ebc0c3c` (where the attempt was made) and the current pin**, so the tree did not fix it
either. Widening the JSValue tier as a deliberate experiment did not reproduce it.
**So the honest statement is that the second attempt avoids the defect rather than fixes it**,
and the reproduction below is kept as a pinned test rather than retired — it costs one test and
it is the only thing that would catch a recurrence.

#### The first attempt, **withdrawn** — kept because the next one started from it

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

**What the next attempt did with this.** Three of the four instructions this section left were
followed and one was overtaken. Kept: the `NumericStorage`-before-lexical ordering in
`VisitVariableDeclaration`, the const-write rejection, and re-running the reproduction **as two
evaluations in one process** — which is exactly why the landed change has
`ALexicalBindingIsUnaffectedByAnEarlierCompilationInTheSameProcess` rather than a single-eval
test that would have been green either way. Overtaken: *"find what else decides a lexical
binding's storage"*. It was looked for at the two named places and not found, and the four
configurations above rule out the front end as well; what the second attempt changed instead
was **which tier** may admit a lexical name at all. See the landing section above for why that
distinction is the load-bearing one, and for the plain statement that the defect is avoided
rather than explained.

`const` did turn out to be the cheaper half, as this section predicted — it cannot be
reassigned, so its analysis reduces to checking the initializer plus rejecting any write — but
it was cheap enough that separating it from `let` bought nothing, and the two landed together.

**Verify.** `LexicalNumericLocalTests`, 58 cases in eight groups: that the gate admits `let` and
`const` *to the same numeric-local count as `var`* (without which every other case here passes
vacuously); arithmetic over the values doubles make awkward (NaN, both zeroes, the infinities,
`2**53`); the refusals — TDZ reads, every form of writing a `const`, a binding that later holds
a string/object/null/undefined/BigInt, and a captured one; the nested-block shapes, including
all three reproduction lines and a nested block's own dead zone; `for (let i …)` binding per
iteration, which a single raw double cannot represent; and the sharpest shadowing case, a
`for`-head `let` sharing its name with an eligible body-level one, where **both halves are
numeric so nothing about the type distinguishes them** and conflating them returns the loop's
final value instead of the outer binding's; and the two function kinds whose locals are not
ordinary CLR locals — a **generator**, where a lexical value has to survive a `yield` because
the body is rewritten into a state machine, and an **arrow**, whose concise form has no body
block at all, so the body-block test must fail to match rather than misfire. Repository suite:
**7 698 tests across 13 projects, 0 failures** on linux-x64, with the patch applied to a
clean checkout of the pin.

**And it exposed a gap in the conformance gate, which is closed here rather than noted.** No
pinned manifest covered `let`/`const` at all — `test262-language-basics` is twelve entries about
`throw`, commas and relational operators — so a change to how lexical bindings are *compiled*
had nothing in §3.4 that could fail. `scripts/compliance/test262-lexical-declarations.txt` adds
`language/statements/{let,const,variable}` and `language/block-scope`; it is **397 of 397
passing on both arms** (§3.4), so it reports nothing today and guards those paths from here on.

#### The block-scoped `var` — **landed, and it completes item 3-3**

> **In the pin.** Shipped as `patches/0068` while its push was blocked by a 403; since applied
> and pushed, and the pointer bumped — it is commit `f566b30d`, an ancestor of `61c8cc65`. The
> figures below were taken on a local build of `9bf9639b` plus `0067` with and without `0068`,
> both arms from the same tree, and they describe the pinned pointer directly now that both have
> landed.

This is the half the item said needs "definite-assignment analysis", and what it actually needs
is the **dominance argument the function body already gets, applied one level down**. The hazard
is exact: a `var` is hoisted to the function but its initializer sits inside a block, so between
function entry and that block the binding is observably `undefined` — and a raw double hoisted
to 0 answers `0` instead. That is a silent wrong answer, not a lost optimization.

**Two admissions, and they are different arguments.**

| | Transparent | Confined |
|---|---|---|
| shape | an unlabelled `{ … }` that is a direct statement of the function body, or of another transparent block | a `var` that is a direct statement of any other block |
| why the initializer has run | the block is entered whenever control reaches it, and the only ways out are `return`/`throw`, which leave the function — so it does not weaken the body's dominance at all | entering the block is itself the proof |
| extra condition | none; the name behaves exactly like a body-level `var` | **every reference must be inside that block**, and after the declaration |
| what it buys | the item's own probe shape — `{ var v = 3.5; }` then a loop that reads `v` | the case that matters in real code: a temporary declared and consumed inside a loop body |

**Measured, both arms from one tree, `--local-alloc`: `block-var` 31.98 → 0.00 B/iteration and
1 → 3 numeric locals**, identical to `top-level-var`, with **all twelve other rows byte-identical
and every other numeric-local count unchanged** — one row moved, which is the whole diff.

**Only a *direct* statement of the block qualifies, and that is load-bearing.** Keying on the
innermost *enclosing* block instead would admit `if (c) var t = 1; return t;` — whose enclosing
block is the function body, which does not dominate the declaration — and answer `0` where the
program sees `undefined`. A label is excluded for the same reason: `break` can leave a labelled
block before the declaration runs, and a labelled block is an `AstLabeledStatement` rather than
an `AstBlock`, so the transparency test does not match it. A `catch` is excluded because it is a
sibling of its `try`'s block, not inside it.

**One hazard was found by testing and would have shipped otherwise.** The first cut marked a
name readable at *whichever* declaration the walk reached first, which is how the existing
analysis has always worked — sound while every declaration dominates. It stops being sound once
a name can have both a dominating and a non-dominating declaration:

```js
if (c) { var t = 1; }      // non-dominating, but reached FIRST
var r = String(t);         // → "undefined", and a raw double answers "0"
{ var t = 2; }             // dominating, and what made the name a candidate
```

The fix is one line of principle — **a name becomes readable at its dominating declaration, not
at any other declaration of the same name** — and it is why the analysis now records which
initializer nodes are the dominating ones rather than only which names are.

**And the fix for that immediately over-corrected, which a pre-existing test caught.** Making
transparent blocks offer their declarations turned `var s = 0; { var s = 5; }` into a
"declared twice" rejection, failing
`NumericLocalWriteVisibilityTests.ANumericReDeclarationKeepsTheLocalSpecialized` — a test written
for exactly this, whose comment reads *"this is the guard against over-fixing"*. It was right:
two declarations that **both** dominate are not a hazard, because each dominates everything
after itself and the type proof still runs over both values. That rejection was over-conservative
even before this item — its stated reason ("the second may sit somewhere the first does not
dominate") never applied to its own call site — and it is now gone.

**Verify.** `BlockScopedVarNumericLocalTests`, 43 cases: the admissions with their numeric-local
counts asserted (a value-only assertion passes vacuously here, since the right answer and the
wrong storage often agree); and the refusals, each written as a value the program can observe —
`String(t)` answering `"undefined"` where a raw double answers `"0"`. The refusals are the
block that may not run, the declaration with no block of its own, the labelled block, the
`catch` reading its `try`'s declaration, the reference before the declaration, two declarations
in incomparable blocks, and a confined name reached from outside its block through `+=`, `++`
and `typeof` — the three forms that never reach an ordinary identifier read. Repository suite:
**7 741 tests across 13 projects, 0 failures** on linux-x64.

### 3-4 · A tagged value representation — *scope and cost, do not start*

The real fix, and a multi-quarter redesign of the engine's most fundamental type with
every built-in downstream of it. An `ownership.json` entry (`tagged-js-value`) already
exists from the earlier campaign.

**Write it up and cost it at the end of phase 3**, once 3-1 to 3-3 have shown how much
of the gap survives unboxed arrays, fields and locals. It is entirely possible the
answer is "less than expected", and that is worth knowing *before* committing to the
redesign rather than after. **Size: XL.**

### 3-5 · A numeric local compared against a JSValue — **landed, 3.4× on its shape; and it measured the ceiling on all of phase 3**

Item 4-5's probe produced this item by accident: the control loop every measurement in this
document has used as a *floor* was itself paying a box per iteration, and the same loop with a
literal bound ran at **8.36 ns and 0 B** against **33.77 ns and 32 B**.

#### The cause is not the parameter, and that changes the fix

3-3 recorded the gap as a property of parameters: *"All four of the item's categories are now at
the eligible floor except `parameter`, which cannot reach the numeric tier at all."* True, and it
is not what costs the box. `i` **is** a numeric local — a raw CLR double. `n` is a `JSValue`. The
compiler had a native form for `<` only when **both** operands were already doubles, so the mixed
case fell through to the generic operator and **boxed the raw `i`** to meet it.

So the fix is to unbox the *other* side rather than to make the parameter numeric: test the value
side, compare two doubles when it is a number, and take the ordinary operator when it is not. That
needs no entry guard and no second body — and it covers strictly more, because
`for (var i = 0; i < a.length; i++)` is a property read, not a parameter, and was boxed for exactly
the same reason.

**Sound because ToPrimitive of a Number is that Number.** Relational comparison runs ToPrimitive on
both operands first; when the value side is already a primitive number that step calls no
`valueOf`, no `toString`, and has no observable effect. So the guarded path is the same path with
the same answer, and everything else reaches the operator it reached before. **Only `<` and `>`**,
for the reason the neighbouring code already records: the backend emits an ORDERED compare for
`<=`/`>=`, which answers true on NaN where JavaScript answers false.

**Block-declared locals, not pooled temporaries, and that is a correctness point.** Both operands
are spilled — the value side is read twice (test and unbox) and the native side is read in both
arms — and the temporaries are needed *after* the operands have already been compiled. A pooled
temp could therefore be one a sub-expression released while being built, and the second spill would
clobber the first operand. `i < obj.m()` is enough to reach it. Declaring locals in the block cannot
collide with anything.

**Verify.** `MixedNumericComparisonTests`, 33 cases, all about semantics and none about speed:
every relation in both directions and with the native side on either side of the operator; NaN on
each side; ±0; ±Infinity; strings, `null`, `undefined`, booleans, arrays, objects, a `Number`
wrapper (not a primitive, so it must take the fallback), a BigInt, and a Symbol that must still
throw; `valueOf` called **exactly once** per comparison and only on the fallback; source-order
evaluation with each operand evaluated once; a throwing `valueOf`; a loop bound that is not a
number; and a bound whose type **changes mid-loop**, since the guard is per evaluation and not per
site. **All 33 pass on the unmodified compiler too** — they pin the existing semantics, they do not
describe the change. Repository suite: **7 872 tests across 13 projects, 0 failures**.

**On its shape it is large.** The counted loop with a parameter bound: **33.77 → 10.03 ns and
32 → 0 B per iteration, 3.4×.** Every probe shape in this document drops the same 32 B.

#### On the corpus it is invisible, and *why* is the finding

Paired Octane runs, four rounds, allocation exact: **0.997× bytes and 0.995× time.** 15.7 MB saved
of 4 487 MB — about 490 000 boxes avoided, against a corpus that performs 37.9 M property reads.

The compile-time counts say the sites exist. **390 relational comparisons take the new form, 59% of
those that could** — so the emission is not the problem. The problem is what is on the other side:

| Suite | Scalar locals | Numeric locals | Share |
|---|--:|--:|--:|
| Richards | 117 | 10 | 8.5% |
| DeltaBlue | 176 | 19 | 10.8% |
| RayTrace | 233 | 17 | 7.3% |
| Box2D | 1 774 | 66 | 3.7% |
| EarleyBoyer | 1 011 | 44 | 4.4% |
| Crypto | 521 | 24 | 4.6% |
| NavierStokes | 197 | 23 | 11.7% |
| **All seven** | **4 029** | **203** | **5.0%** |

> **Five per cent of scalar locals in the Octane corpus reach the numeric tier.**

That is the ceiling on **all** of phase 3's local work — 3-0, 3-3 and 3-5 alike — and nothing in
this document had measured it. Every one of those items is correct, tested, and demonstrably large
on the shape it targets; each one then meets the same gate.

> **Correction.** This section first named `CanScalarReplaceLocals` as the gate that costs the
> coverage — "no nested functions, no captured names, no `eval` and no `with`, and real code has
> those nearly everywhere". **Item 3-6 counted it and that is wrong: it rejects 2 names out of
> 2 695.** The real causes are below, in 3-6. The claim is left here struck rather than deleted
> because it is exactly the kind of plausible reading-of-the-code that this document keeps having
> to correct with a count.

#### Re-specification

- **New 3-6 (L): widen numeric-local eligibility.** At 5.0% coverage this is the **multiplier** on
  every local-representation item already landed, and it is worth more than any of them
  individually. The gate is a conjunction inherited from scalar replacement; the question is which
  conjunct actually costs the coverage, and that is a measurement — count the locals each conjunct
  rejects — not a design. **Do that count first**, on the evidence of the last three items that
  measuring a premise keeps changing what gets built.
- **3-4 stays "do not start", and now for a stated reason.** Its own instruction is to cost it
  *"once 3-1 to 3-3 have shown how much of the gap survives unboxed locals"*. The answer is that
  the gap largely survives, because the unboxing reaches 5% of locals — so the question 3-4 was
  told to wait for is answered, and it points at 3-6 rather than at the XL redesign.

### 3-6 · Which conjunct costs the coverage — **counted, and it is none of the ones the item named**

3-5 measured numeric-local coverage at 5.0% of scalar locals and blamed the scalar-replacement
gate. 3-6's whole instruction was to **count before designing**, because the last three items had
each been re-specified by their own premise. It was right to insist: the count says the item was
looking in the wrong place, and then says so a second time one level down.

#### The waterfall

Every hoisted name in the seven suites, attributed to the **first** conjunct of the numeric-local
gate it fails — a waterfall rather than overlapping tallies, so the numbers add up and each one
reads as "widen this and at most that many names become eligible":

| | Names | Share |
|---|--:|--:|
| **Accepted — became a raw `double`** | **203** | **7.5%** |
| Not proven numeric | 2 012 | 74.7% |
| Captured by a nested function | 478 | 17.7% |
| Function not scalar-replaceable | **2** | **0.1%** |
| Direct-eval root | 0 | — |
| Not in a function | 0 | — |
| Named `arguments` or `eval` | 0 | — |
| `let`/`const` outside the function body | 0 | — |
| **Total hoisted** | **2 695** | |

**`CanScalarReplaceLocals` rejects two names.** Async, generator, `eval`, `with`, `debugger` and
dynamic nested functions — the conjunction 3-5 named, and the same one that bounds phase 4's
tiering candidates — cost **0.1%** of the coverage. That claim is now corrected where it was made.

#### And "not proven numeric" is not what it sounds like either

The obvious reading of 74.7% is that most locals simply are not numbers, and that there is nothing
to fix because no analysis makes a string a double. Counted inside the analysis, that reading is
also wrong:

| | Names |
|---|--:|
| Offered as numeric candidates | **2 335** |
| **Dropped by the optimistic fixed point** | **1 842** |
| Surviving the analysis | 493 |
| Never offered at all | ~170 |

**Only ~170 names of 2 695 — 6.3% — are rejected because their declaration is not numeric.** The
analysis *offers* 2 335 and then drops **1 842 of them, 78.9%**, in the fixed point: a candidate is
dropped as soon as any assignment to it cannot be proved numeric under the current assumption.

The two counts also reconcile, which is what says neither is measuring the wrong thing: 1 842
dropped plus ~170 never offered is the 2 012 the waterfall attributes to *not proven numeric*, and
the 493 survivors minus the 203 accepted is **290 names that the analysis proved numeric and the
hoist site then refused** — all of them to the captured-by-a-nested-function conjunct.

> **Both of those numbers are wrong, and 3-7 found out how.** "493 survivors" is *offered minus
> dropped*, and `Resolve` removes a **third** population between those two counters — every name a
> rejection path named — which had no counter at all, so the subtraction silently counted it as
> zero. With the counter added, the same corpus reads **offered 2 295 = rejected 133 + dropped
> 1 916 + surviving 246** (as corrected by 3-8, which found the offer double-counted across
> nested functions): the survivors are 246, not 493, and the residue refused at the hoist site is
> **22 names, not 290**. The reconciliation this paragraph claims is real but circular —
> both figures were derived from the same two counters, so agreeing with each other told nobody
> that a third term was missing. See 3-7, and §3.5's *"a count you inferred is not a count"*.

#### What that leaves, and it is two different problems

- **The fixed point's 1 842 (68% of all hoisted names) is a *provability* wall, not a gate.** A
  candidate is dropped because something assigned to it comes from a parameter, a property read, an
  element or a call — values whose type is not knowable statically. That is precisely the wall 3-5
  hit from the other side, and no amount of widening a conjunction reaches it. **Making those
  numeric needs a runtime guard, which is a phase 4 mechanism applied to a phase 3 representation**
  — and 4-3b's in-method branch is exactly the facility for it. Worth stating plainly: **the
  largest single obstacle in phase 3 is shaped like phase 4.**
- **The 290 provably-numeric names refused for being captured is a bounded, purely static
  opportunity.** A closure captures through a cell, so a numeric local that any nested function
  mentions keeps its `JSVariable`. Giving those a raw-`double` cell instead would take numeric
  locals from **203 to ~493 — 2.4×** — with no speculation and no guard. That is the only part of
  3-6 that is a widening in the sense the item meant.

#### Re-specification

**3-6 as written is answered and closed**: the conjunction it proposed to widen costs 0.1%. What it
found splits into two successors, and the count is what says which is which:

- **3-7 (L): a raw-`double` cell for a captured numeric local.** 290 names, 2.4× numeric coverage,
  entirely static. The obvious first item, and the one to size next. **Built: it is 8 names, and
  "entirely static" was the part that was wrong** — half the captured names are held by a *hoisting*
  rule that no static widening can touch, and lifting the conjunct exposed two wrong answers. See
  3-7.
- **3-8 (XL, and it belongs to phase 4's machinery): guard a local's numeric-ness at run time.**
  The 1 842 dropped candidates are dropped for want of a *type*, not for want of a rule. This is
  4-3b's in-method branch pointed at a local's representation rather than at a property read, and
  it should not start before 3-7 says how much of the gap the static half closes.

**Nothing is built for this item**, deliberately. Its own text said to count first, and the count
retired the design it was going to justify — for the fourth item running, which is now less a run
of luck than a description of how this campaign works.

---

### 3-7 · A raw-`double` cell for a captured numeric local — **landed, and its own premise was wrong twice**

3-6 handed this item a number and a claim: **290 names, `203 → ~493`, 2.4×, "entirely static"**,
and called it "the obvious first item". Built and measured, the widening is worth **eight names —
`224 → 232`, 1.036×** — and getting there found **two wrong answers** that the item's "entirely
static" reading had no room for. Both halves of the premise failed, in opposite directions: the
mechanism is *cheaper* than the item thought (nothing had to be built for the cell at all) and the
population is **36× smaller**.

**Where.** `Broiler.JavaScript.Compiler` — `Statements/FastCompiler.VisitBlock.cs` (the gate),
`Declarations/FastCompiler.CreateFunction.cs` (the new hoisted-capture set),
`Declarations/NumericLocalAnalysis.cs` (both correctness fixes), `Scope/FastFunctionScope.cs`,
`CapturedNumericLocals.cs` (the A/B switch).

#### The cell already existed, which is the one part that was easier than written

The item is titled "give a captured numeric local a raw-`double` **cell**", and no cell had to be
written. The expression compiler already rewrites any CLR local a nested lambda references into a
`Box<T>` (`ClosureSeparator/Box.cs`, `LambdaRewriter.CheckForClosure`), and **`Box<double>` *is*
the shared cell a closure needs** — allocated once per activation, read and written through by
every closure over it. So a captured numeric local is *one* allocation where the `JSVariable` form
is two (the cell, plus the box the closure reads the cell through), and the change at the gate is
the removal of one conjunct.

That also answers a question the gate never stated: the JSValue tier refuses captured names too,
and **not** because sharing would break — `Box<JSValue>` would share just as well. It refuses them
because that tier has no cell at all, and a cell is what a TDZ, a `const`'s TypeError and a
`delete`d eval binding *are*. The numeric tier's gate proves each of those unreachable, which is
why the widening applies there and only there.

#### Two wrong answers, both found by running the widening rather than by reading it

The numeric tier's soundness rests on a **textual** argument: a name with any reference before its
declaration is refused, so the initializer has always run by the time anything reads the binding,
and a raw double hoisted to `0` is never observed where `undefined` belongs. Capture breaks the
link between text order and execution order, and 3-6's "entirely static" description is exactly
the reading that misses it.

- **A hoisted function declaration exists before the body runs.** Its body is textually *after*
  the declaration — so the analysis accepts it — and its function object exists at function entry,
  so it can run *before* the declaration:

  ```js
  function f() { var r = g(); var s = 0; function g() { return s; } return String(r); }
  ```

  `f()` is `"undefined"`. With only the gate widened it returned **`"0"`**. The fix is one more
  conjunct — a name mentioned by a function declaration at *body top level* keeps its cell — and
  it is deliberately **not** behind the switch, because it is correctness rather than policy. Only
  body-top-level declarations qualify: one inside a block, `if`, loop, `try` or `switch` has its
  *binding* hoisted (Annex B B.3.3.1) but not its *value*, so calling it early is a `TypeError` on
  `undefined` and never a read. A declaration textually *before* the numeric one is already
  refused by the analysis, so no position comparison is needed.

- **A declaration inside a nested function is a different binding.** `NumericLocalAnalysis`
  deliberately conflates names across nested functions — that is what makes a closure's
  `s = 'x'` drop an outer numeric `s` — but the conflation ran in the *initializing* direction
  too, so a nested function's own parameter opened `declared` for the outer name:

  ```js
  function f() { var r; { var g = function (t) { return t; }; r = String(t); var t = 5; } return r + ',' + t; }
  ```

  `"undefined,5"`. With only the gate widened, **`"0,5"`**. Fixed by suppressing `declared` at
  nested-function depth; writes are still recorded at every depth, which is the half that has to
  stay conflated.

A third defect was a *compile* failure rather than a wrong answer, and it is the same shape as the
first two: **a function declaration stores a function object into the very binding being typed**,
and a declaration is not an assignment expression, so the walk never saw the store.
`let f = 5; { function f() {} }` reaches that binding through Annex B's copy-out and died on
*"Assignment target Call (BCallExpression) is not supported"* — the write had been aimed at a
numeric local's *reading* expression, which boxes. It was covered by accident until now: a
declaration mentions its own name, so the name counted as captured and was refused for that.
**All three had been sitting behind the capture conjunct, and lifting it is what exposed them** —
which is the sharpest form of §3.5's rule about a conservative bug passing its own tests.

#### The count, and why 3-6's 290 was not there

Same waterfall as 3-6, over the same seven suites, with the capture row split by whether the
mention is hoisted. The **off** column reproduces the pinned pointer — `224` numeric locals,
`4 521` scalar, `2 920` hoisted names, and every conjunct identical except the capture row, which
the new counter splits (the pin reports its 478 undivided) — so the two correctness fixes above
cost **nothing** in coverage:

| Conjunct | off | on |
|---|--:|--:|
| **Accepted** | **224** | **232** |
| Not proven numeric | 2 216 | 2 439 |
| Captured by a **hoisted** function declaration | **247** | **247** |
| Captured by a nested function (other) | **231** | 0 |
| Function not scalar-replaceable | 2 | 2 |
| **Total hoisted** | **2 920** | **2 920** |

3-6's 478 captured names **split almost in half: 247 of them (51.7%) are named by a hoisted
function declaration** and can never be widened, because that conjunct is correctness. Of the 231
that remain, **223 are not proven numeric** and **8 become raw doubles**.

**3-6's 290 was inferred, not counted, and the inference had a missing term.** It read survivors as
*offered minus dropped* — 2 335 − 1 842 = 493 — and then 493 − 203 = 290. But `Resolve` removes a
third population between those two counters: every name a rejection path named (read before its
initializer, bound through a pattern, `delete`d, a written `const`, a for-in head) leaves in
`ExceptWith(rejected)`, which **had no counter at all**. Counted directly, on the same corpus:

```
offered 2 295  =  rejected 133  +  dropped by the fixed point 1 916  +  surviving 246
```

It reconciles exactly, and the survivor count is **246, not 493**. (Those `offered` and `rejected`
figures were first published as 2 521 and 359; item 3-8 found the analysis was offering a nested
function's block-scoped `var`s to its *enclosing* function as well as to its own, so both were
inflated. `dropped`, `surviving` and every figure this item rests on are unchanged — the
double-counted names were all rejected anyway.) Since 224 of those are already
accepted, **only 22 provably-numeric names are refused at the hoist site for any reason at all**,
and 14 of the 22 are the hoisted-capture ones. So the item's population was never 290; it was 22,
of which 8 are reachable. *An inferred count and a measured one are different kinds of number, and
3-6 said its two counts "reconcile exactly" — they did, to each other, while both omitted the same
term.*

#### What it is worth, and it has an exact losing side

`--local-alloc` gains a `capture` category — four spellings of `top-level-var` differing only in
how a closure names the value. Deterministic, exact, net of the loop control:

| Site | off | on | |
|---|--:|--:|---|
| `captured-var` | 63.97 | **0.01** | the value used by the enclosing function's own arithmetic |
| `captured-var-written-in-closure` | 127.93 | **0.02** | ...and written through the closure each iteration |
| `captured-var-read-in-closure` | 31.99 | **63.99** | **the losing side**: read *through* the closure each iteration |
| `captured-var-hoisted-fn` | 63.97 | 63.97 | what the correctness conjunct costs — the whole win, on that shape |
| `call-captured-var` (per **call**) | 3 135.99 | **3 023.99** | −112 B an activation: the `JSVariable` and the boxing of its arithmetic |

Every **per-iteration** delta is an exact multiple of 32 B, which is what says the model is right
rather than approximately right: two boxes an iteration removed, four when the closure writes, and
**one box added per read made through a closure** — a raw double has to box to hand a JSValue back
where a `JSVariable` returns the one it already holds. The per-activation row is the one that is
not, and for the expected reason: its −112 B is two of those boxes plus the `JSVariable` object
itself, which is not 32 bytes.

Timed the same way, one shape per process (ten rotations, four samples a rotation):

| | off | on | |
|---|--:|--:|---|
| the winning shape | 309.0 ms | **41.0 ms** | **0.1327×** |
| the same loop with no closure (control) | 43.0 ms | 41.0 ms | 0.9535× — same code both arms, the noise floor |
| **shape ÷ control** | **7.19×** | **1.0000×** | capture cost 7.2× on this shape and now costs nothing |
| the losing shape | 554.0 ms | 615.5 ms | **1.1110×** |
| its control (closure reads a literal) | 608.0 ms | 606.5 ms | 0.9975× |

**`shape ÷ control = 1.0000` is the result**: a captured numeric local now runs at exactly the
speed of the same loop with no closure at all, which is the floor this whole family aims at. The
losing side is real, bounded and priced — 11% and one box per closure read — and it bites only a
closure whose body hands the raw value straight back out; a closure that *computes* with it
resolves through the same `NumericStorage` and never boxes.

**The first measurement of this had to be thrown away**, and the tell was in the control: run in
one process, the shape and its control moved *together* (control 1.2857×), because the off arm
allocates 192 MB over the loop and its collections are charged to whichever function runs next.
That is §3.5's `--compile-profile` artifact one level down, and the rule is the same — **one shape
per process**.

#### On the corpus it is invisible, for the third item running

Seven Octane suites, driver run, allocation deterministic: **1.0001×**. Eight names of 2 920.
That is not a failure of the change, it is the same ceiling 3-5 and 3-6 measured from two other
directions, and the count now says where the rest of it is: **2 439 names are not proven numeric
and 247 are held by a hoisting rule no analysis can widen**. Nothing left in phase 3 is a matter
of loosening a conjunction.

The suites were **run, not merely compiled** — 2-8's lesson, which broke DeltaBlue by measuring a
loop that resembled it. All seven load and all nine benchmarks complete with no failures on the
pinned build, the off arm and the on arm alike.

**Shipped on by default, with `BROILER_JS_CAPTURED_NUMERIC_LOCALS=0` to restore the cell**, on the
same terms as `BROILER_JS_DEFER_IL`: the change has a losing side, so it has to be measurable
against a build that differs in nothing else, and every figure above is a pair from one tree.
`CapturedNumericLocalTests` is 24 cases — the three defects above, sharing across two closures,
one box per activation, a `var` a loop closes over, generators and `async` bodies (rewritten by a
different path), `try`/`catch`/`finally`, recursion, NaN / ±0 / ±Infinity, and a closure that
stores a string, an object, `undefined` or a destructured value — **each asserted on both settings
of the switch**, so they are a regression guard and not a description of the optimization. The
probe scripts they were written from answer **identically on a pristine build of the pinned
pointer**.

#### Re-specification

**3-7 is answered and closed.** What it leaves:

- **The 247 hoisted-capture names are closed, not deferred.** A raw double cannot represent
  `undefined`, and a hoisted declaration can observe the binding before its initializer runs. The
  only way to reach them is a representation that carries an *uninitialized* state — which is a
  tagged value, i.e. **3-4**, and 3-4 is a cost rather than a task.
- **3-8 is now the whole of what is left in phase 3**, and its size grew: the 2 439 names not
  proven numeric are 83.5% of every hoisted name, against the 8 this item moved. Its mechanism is
  4-3b's in-method branch pointed at a local's representation, and this item is the evidence that
  nothing static gets there: the last three attempts on phase 3's static coverage have moved the
  corpus by nothing — 3-5 at 0.997×, 3-6 which counted first and found its own design had nothing
  to widen, and 3-7 at 1.0001×.

---

### 3-8 · Guard a local's numeric-ness at run time — **3-8a is built complete, measured, and closed as a regression**

3-6 specified this from one sentence: *"the 1 842 dropped candidates are dropped for want of a
**type**, not for want of a rule"*, sized it XL, and named 4-3b's in-method branch as the
mechanism. 3-7 then made it "the whole of what is left in phase 3". Counted before building any of
it — the sixth item running to be re-specified by its own premise — the item is **well-founded
mechanically and aimed at almost none of the prize**, and the two measurements that say so are
ones nobody had taken.

**Where.** `Broiler.JavaScript.Compiler` — `Declarations/NumericLocalAnalysis.cs` (the drop-cause
classifier and one bookkeeping fix), `CompilerSpecializationDiagnostics.cs`,
`NumericLocalSpecialization.cs` (the whole-tier switch); `Broiler.JavaScript.BuiltIns` —
`Number/NumberBoxingDiagnostics.cs`, `Number/JSNumber.cs`.

#### The first number nobody had: how much of a real run is number boxing at all

Every phase 3 item so far was sized by a per-shape figure — **31.98 bytes an iteration**, reported
by 3-3 for four categories, by 3-5 for its comparison, by 3-7 for capture, and now by 3-8 for all
three of its own causes. The same number, over and over, and every item then moved the whole corpus
by nothing. The figure that would have explained that is what share of a real workload's allocation
is number boxing, and no counter existed for it. `NumberBoxingDiagnostics` (off by default) counts
every call to `JSNumber.Create`, split by whether the small-integer table answered it:

| Suite | allocated | boxing requests | cached | fresh boxes | **boxes as share of allocation** |
|---|--:|--:|--:|--:|--:|
| NavierStokes | 1 074 MB | 38 153 253 | 8 175 788 | 29 977 465 | **66.96%** |
| Crypto | 1 845 MB | 74 249 073 | 31 839 549 | 42 409 524 | **55.16%** |
| Box2D | 763 MB | 18 854 548 | 7 419 842 | 11 434 706 | **35.98%** |
| RayTrace | 420 MB | 1 658 272 | 817 255 | 841 017 | 4.80% |
| EarleyBoyer | 706 MB | 782 654 | 218 657 | 563 997 | 1.92% |
| Richards | 23 MB | 99 565 | 85 906 | 13 659 | 1.41% |
| DeltaBlue | 52 MB | 148 541 | 141 776 | 6 765 | 0.31% |
| **Total** | **4 884 MB** | **133 945 906** | **48 698 773** | **85 247 133** | **41.89%** |

**Number boxing is 41.89% of everything the corpus allocates** — 2.05 GB of 4.88 GB — and the
small-integer cache absorbs 36.4% of the requests before they allocate anything, which is P2-2's
table still earning its keep. So the prize phase 3 is aimed at is not small and never was; the
per-suite spread is what hid it, because a corpus average over four suites at 0.3–4.8% buries three
at 36–67%.

#### The second number nobody had: what the numeric-local tier is worth

Every phase 3 item was measured as a **delta** against the tier as it stood — 3-5 at 0.997×, 3-7 at
1.0001× — and four such readings look like evidence that the mechanism does not matter. They are
evidence that *eight more names* do not matter. Nobody had measured the tier itself, because there
was no way to turn it off. `BROILER_JS_NUMERIC_LOCALS=0` is that control, and it is the whole
cumulative product of P2-2 item 3, 3-0, 3-3, 3-5 and 3-7:

| | tier on | tier off | |
|---|--:|--:|---|
| fresh number boxes | 85 250 178 | 85 561 365 | **311 187 removed — 0.36%** |
| allocated bytes | 4 884.1 MB | 4 904.3 MB | **0.9959×, 0.41%** |
| numeric locals | 232 | 0 | |

Timed the same way — six rotations of the driver run per arm, interleaved — the wall clock does
**not separate at all**: 21 261 ms with the tier against 21 024 ms without it, a 1.0113 ratio
against a per-arm spread of 4–5% whose ranges *overlap end to end* (20 718–21 793 off,
20 980–21 823 on). That is not "the tier costs 1%", it is "six samples an arm cannot tell the two
apart", which is what §3.5 says to expect when the effect and the noise are the same size — and at
0.41% of allocation the effect is far smaller than the noise. **The honest reading is that the
whole mechanism is not measurable on this corpus's wall clock**, which is a much stronger statement
than any single item's 0.997× and is the one that should have been available before four of them
were built.

**The entire raw-double local tier removes 0.36% of the boxes the engine allocates**, and 3-8
proposes to widen that tier by roughly ten times. Even a perfect widening that scaled linearly in
names — which it will not, because the 232 already include the hottest loop counters — reaches a
few per cent of the 41.89%.

*The reason is structural and the drop-cause table below says it out loud.* A box is minted by the
**operator**, not by the local: `a[i] = b[i] * 2` boxes because the multiply's operands arrived
boxed from element reads, and it would box whether or not either end were held in a local. A local
is one link in that chain, and phase 3 has spent five items unboxing the link that carries 0.36% of
the traffic.

#### What defeats the proof, and it is mostly not the local

Every candidate the fixed point drops, attributed to the first leaf of the assigned expression the
analysis will not type — so `s = a.x * 2 + 1` is charged to the property read rather than to the
operator or the literal:

| | Names | Share |
|---|--:|--:|
| **A named property read** | **894** | **46.7%** |
| **A call's or `new`'s return** | **570** | **29.7%** |
| Another dropped candidate — a *cascade* | 132 | 6.9% |
| A computed element read | 101 | 5.3% |
| A literal that is not a number | 95 | 5.0% |
| Any other name (global, outer binding, catch) | 55 | 2.9% |
| **A parameter** | **47** | **2.5%** |
| An operator the analysis will not type | 22 | 1.1% |
| **Total dropped** | **1 916** | |

Three things follow, and none of them is in the item as written.

- **76.4% of the population is a value arriving from somewhere else** — a property read or a call
  return. The guard 3-8 proposes does belong at those points and would work there; what it does
  *not* do is what the item's title says, which is guard "a local's numeric-ness". The type is not
  unknown because the local is untyped; it is unknown because the **producing site** hands back a
  `JSValue`. Making those sites produce unboxed doubles is **3-1** (array backing stores) and
  **3-2** (shape slots) — and the boxing table says exactly the same thing, since the three suites
  where boxing dominates are the three that stream numbers through arrays and object fields.
- **A parameter is 2.5%.** 3-3 recorded the parameter gap as the one the numeric tier "cannot be
  widened to at all, because the caller picks the type; that is phase 4", and phase 4 is where it
  has sat since. It is 47 names of 1 916. *The category an item defers is not thereby the category
  that costs.*
- **93.1% of drops are roots, not cascades.** That is good news for any design — fixing a root
  frees its dependents — and it also means the 1 916 cannot be collapsed to a handful of causes by
  chasing chains.

#### The per-shape ceiling, which is the same number this phase always gets

`--local-alloc` gains a `provability` category: the three dominant causes, each with the value
hoisted out of the loop so the loop body is identical to `top-level-var`'s and the delta is the
cost of the local not being *provable* rather than the cost of the read.

| Site | net B/iter | numeric locals |
|---|--:|--:|
| `top-level-var` (provable) | **0.00** | 3 |
| `property-sourced-var` | 31.98 | 1 |
| `call-sourced-var` | 31.99 | 1 |
| `parameter-sourced-var` | 31.98 | 1 |

All three cost **31.98 bytes an iteration** — to the hundredth, the same figure 3-3 measured for
its four ineligible categories and 3-5 for its parameter-bound loop. Timed one shape per process,
ten rotations of four: the property-sourced loop is **280.5 ms against 41.0 ms**, **6.84×**, which
is the same order as 3-7's 7.19×. *Every route to "not provably numeric" costs exactly the same as
every other, and the shape-level prize has never been the question.*

#### A bookkeeping defect found while counting, and it corrects 3-7's published figures

Writing the classifier's tests turned up a drop being counted twice. `Collector` descends into
nested functions on purpose — that is what makes a closure's `s = 'x'` drop an outer numeric `s` —
but `VisitBlock` was also **offering the blocks it met there**, so a nested function's
block-scoped `var` became a candidate of every enclosing function as well as of its own, and was
dropped and counted once per level. Suppressed at nested-function depth. It changed **no answer**:
the enclosing function's hoisting scope never contains the name, so it never reached the hoist
site, and the corrected run leaves `dropped`, `surviving`, `numericLocals`, `hoistedNames` and
every drop cause **identical**. What it moves is the pair 3-7 published:
**`offered 2 521 → 2 295` and `rejected 359 → 133`**, corrected where 3-7 states them. *The
double-counted names were all names the enclosing analysis rejected anyway, which is why nothing
downstream moved — and why nobody would have found it except by writing a test that asserted an
exact count.*

#### Re-specification

**3-8 as written should not be started.** It is not wrong about its mechanism — a guard at the
value's source, branching into 4-3b's in-method fallback, is the right shape and would win the
31.98 B/iter its shapes cost. It is wrong about its target: the tier it widens carries **0.36%** of
the engine's number boxing, and the item's own population is **76.4% values produced elsewhere**.

- **3-1 and 3-2 move to the front of phase 3, and the boxing table is why.** 41.89% of the corpus's
  allocation is number boxes, and the three suites that carry it split cleanly between the two
  items — checked in their sources rather than assumed: **NavierStokes** (66.96%) and **Crypto**
  (55.16%) hold their numbers in `new Array` and read them by index, with no `.x`/`.y` field access
  anywhere, which is **3-1**; **Box2D** (35.98%) allocates no arrays at all and has 240 `.x`/`.y`
  accesses, which is **3-2**. Both items have been ranked behind the locals work since the phase
  opened, on no measurement at all.
- **What 3-8 keeps** is the 2.5% parameter case, which is small, and the observation that a guard
  at a *property read* is 4-2b's specialized read — already built, already knows the shape at the
  site, and already carries 44.7% of the corpus's executed reads. If unboxing is ever wired into a
  specialized read, the local half follows for free.
- **The instrument outlives the item.** `NumberBoxingDiagnostics` and
  `BROILER_JS_NUMERIC_LOCALS` are what turn "phase 3 is invisible on the corpus" from a repeated
  observation into a measured share, and any future item here should be sized against them before
  it is written.

#### Re-opened, on the terms `0083` already used once — the 0.36% was the wrong denominator

**"Do not start as written" stands; "aimed at almost none of the prize" does not.** Item 3-1's
update-target census counted where the `++`/`--` step's operand lives, and the answer re-prices
this item without contradicting a number in it: **Element 0, Property 0.3%, LocalCell 0.0%,
LocalSlot 98.1%** of 17 282 144 steps — **≈7.05 M real boxes, 22.6% of the 31.16 M the corpus
still allocates**, and 6.76 M of that on NavierStokes alone.

*The 0.36% and the 22.6% are measurements of different things.* `BROILER_JS_NUMERIC_LOCALS=0`
prices **what the tier catches** — every raw-double local the analysis can prove, which is a small
population precisely because the proof is hard. The update census prices **what the tier lets
through**: the names that would have been raw doubles had anything typed them. An item is worth its
second number, not its first, and this section reasoned from the first.

**It is the same correction `0083` made, arriving by a different route.** There, compile-time
provability reached 0.75% of the arithmetic while run-time truth reached 100.00%, which moved the
guard from the compiler's proof to the operator. Here the static tier reaches 5.0% of scalar locals
while the update operator hands 98.1% of its steps to a local that merely was not proved. *Twice
now, a mechanism priced by what the compiler can prove has been under-priced by two orders of
magnitude against what a run-time test can reach.*

**And the population has a named shape rather than being a long tail.** NavierStokes' 9.46 M steps
are `++currentRow` in three functions, where `currentRow = j * rowSize` and `rowSize` is a
`FluidField`-scope var written from a sibling closure — so **one untypable closure variable
cascades into 6.76 M boxes**, and 3-6's waterfall confirms it at the name (24 numeric locals of 141
hoisted). That suggests the guard does not have to be general to pay: a run-time numeric test on
the *initializer* of a local whose only defeat is an outer-scope name would reach this whole
population, which is a much smaller item than "guard every local".

**What still has to be answered before it is built** is the exchange rate, which is now known and
is not kind: `0088` puts collection at 1.8% of the driver and the measured cost of allocation at
**711 ms per GB**, so 7.05 M boxes at ~24 B is **≈0.17 GB, about 120 ms — 0.6% of the driver**.
That is worth having and it is not an XL's worth. *The re-opening is of the item's ranking, not of
its size: it should be re-scoped to the cascade it actually serves, and it should be argued in
milliseconds.*

#### 3-8a · Scoped to the cascade — one conjunct, one test, and the reason no static fix reaches it

The waterfall counts *which names* were dropped. Scoping needs the next thing down — **which rule
defeats the shape the traffic is actually in** — because the rules want different fixes and two of
them can never be widened at all. The update-target census is itself the oracle for that, and this
is the discrimination it was built for: a numeric local compiles `c++` to a native add and
contributes **no row**, a local that stayed a `JSValue` contributes `LocalSlot`, a captured one
contributes `LocalCell`. Eight shapes, one per conjunct (`NumericLocalDefeatTests`):

| Shape | Row | Defeated by |
|---|---|---|
| `var c = 10; c++` | *none — numeric* | — (control) |
| …with a nested function **declaration** present | *none — numeric* | **not** `CanScalarReplaceLocals` |
| a hoisted `function g(){ return c; }` names it | `LocalCell` | `CapturedByHoistedFunction` (3-7, correctness) |
| **`var c = 2 * rowSize`, `rowSize` one scope out** | **`LocalSlot`** | **`OtherName`** |
| …with `rowSize` written from a sibling closure | `LocalSlot` | `OtherName` |
| …with `rowSize` **already proven numeric** | `LocalSlot` | `OtherName` |
| the value passed in as a parameter instead | `LocalSlot` | `Parameter` (3-3's gap) |

**Three of these rule things out, and that is most of the work.** A nested function declaration is
innocent — `CanScalarReplaceLocals` tolerates it, and `FluidField` is built out of them. The
hoisting rule is innocent *of this traffic*: it produces a `LocalCell`, and NavierStokes reports
**9 461 760 `LocalSlot` steps against six `LocalCell`**, so the conjunct 3-7 proved is correctness
is not what is costing the boxes. And "just pass it in as an argument" trades `OtherName` for
`Parameter` and lands in the same row.

**What is left is one conjunct: the analysis is per-function and will not type a name from outside
it.** The sixth row is the sharp one — `rowSize` is *already proven numeric* by its own scope's
analysis, and the local one level down that reads it is still dropped as `OtherName`. **A
conclusion is not carried across a closure boundary.** That is pure analysis reach with no
soundness argument attached, and it splits the work in two:

- **3-9 (new, S–M, static, count first) — counted, and closed at a population of zero; see below.**
  Import the enclosing function's proven-numeric set into
  `IsNumeric`, so an identifier resolving to a numeric local one scope out is typed rather than
  classified `OtherName`. No run-time machinery, no guard, no fallback. **It does not reach
  NavierStokes** — the seventh fixture is why: there the readers of `rowSize` are hoisted
  *declarations*, so the root is held by 3-7's correctness conjunct and is untypable no matter how
  far the analysis reaches. So 3-9's population is names whose enclosing binding is captured only
  by function *expressions*, and **nobody has counted how many of those the corpus has**. That
  count is the item's own precondition, on the pattern that has now retired five designs here.
- **3-8a — the run-time half, and the only thing that reaches the cascade.** When a local's *only*
  defeat is `OtherName` or `DroppedCandidate` — every other conjunct already passes — one
  `IsNumber` test where the value enters decides the name for the whole function. That is 4-3b's
  in-method branch pointed at a representation, which is what 3-8 always said; what is new is that
  it no longer needs to be general. It does not need to guard a parameter (3-3's gap, a different
  entry point), a property read or a call result (a guard per *read*, not per name), which is the
  76.4% of drops 3-8 was originally sized around and the reason it was an XL.

**Sizing 3-8a honestly.** Its population is the names NavierStokes' `++currentRow` family lives in:
**6.76 M of the 7.05 M real update boxes, 96%** — the rest of the corpus's steps are either
answered by the small-integer cache already (Crypto: 7.19 M steps, 7 210 real boxes) or too few to
matter. At `0088`'s **711 ms per GB** that is **≈0.16 GB, ≈115 ms, 0.6% of the driver**, and the
*reads* of those same locals add nothing to it — `0084`'s guarded tree already computes on them
natively at the operator, and an index read hands back a box that already exists rather than
minting one.

#### Attempted, and stopped — the population narrowed but the mechanism did not

3-8a was taken to the build and **is not built**. Two things came out of the attempt, and the
second is the reason it stopped.

**The mechanism is an XL after all, and the scoping above was wrong to call it an M.** Narrowing
*which names* the item speculates on does not narrow *what has to change to hold one*. A local that
is a raw double today advertises itself through `VariableScope.NumericStorage`, and **every fast
path in the compiler keys off that one field** — item 3-0's `GetElementByNumber(double)` index,
item 3-5's mixed comparison, `AssignToVariable`'s raw store, the update emitter's native step, and
`ToNativeExpression`'s "this leaf is already a double". A speculative local is a double *only while
a flag holds*, so every one of those sites has to become guard-aware or read a dead double — and a
site that is missed produces a **wrong answer**, not a slow one. Holding the value in both
representations is what makes the reads correct, and then a read outside the guarded set costs a
box it does not cost today. *The population is small; the surface is the whole numeric tier.*

**And the population could not be measured, which is what actually stopped it.** The instrument was
built the honest way — take the same optimistic fixed point a second time with a name from an
enclosing scope assumed numeric, and subtract the real survivors, so the set comes out of the
existing analysis by difference rather than out of a new rule — and it read **0 on all seven
suites**. It also read **0 on the shape it was built for**. By §3.5's own rule that reading is
unusable: *a counter that has never been shown to read non-zero is a claim about the counter*, and
this one never discriminated. One real defect was found inside it on the way and is worth recording
because it is `0083`'s failure mode a second time — **the enable for a compile-time counter was
placed next to the run-time censuses, which run after the corpus has already been compiled**, so
the first reading was of a counter that was switched on too late. Fixing that changed nothing, and
the instrument was **reverted rather than shipped**: a zero nobody can vouch for is worse than no
number.

#### The count, on the second attempt — 26 names, and 15 of them are NavierStokes'

The instrument was rebuilt, and this time **made to discriminate before it was pointed at
anything** — which is the whole of what went wrong the first time. `AnalyzeSpeculative` still works
by difference against the real fixed point rather than by a new rule (run the same resolution a
second time with an identifier the function neither declares nor takes as a parameter assumed
numeric, and subtract the real survivors), and it now carries seven fixtures that make it *fail*
if it stops separating the populations:

| Shape | Drop cause | In the population? |
|---|---|---|
| `var c = 2 * gg` | `OtherName` | **yes** — 1 |
| `var r = gg; var c = 2 * r` | `OtherName` + `DroppedCandidate` | **yes** — 2, the cascade resolves |
| `var c = 2 * 10` | *(proven numeric)* | no |
| **`function f(n){ var c = 2 * n }`** | **`Parameter`** | **no** |
| `var c = 2 * o.x` | `PropertyRead` | no |
| `var a = []; var c = 2 * a` | *(never offered)* | no |

The last two rows are what make it a measurement rather than a tally. A **parameter** is one slot
away in the same enum and is *not* a name from outside the function — it is a value the caller
picks per call, so no test at an initializer decides it — and an instrument that could not separate
them would report 3-8a's population as everything item 3-3 already deferred. The final row is the
error that would have inflated rather than zeroed the figure: a local that was never *offered*
(`var a = []`) is not in `candidates` either, so an instrument asking only "is it a candidate?"
would classify it as coming from outside the function and assume it numeric. Telling *outside the
function* from *inside and unqualified* needs its own set of declared names.

**Counted on the corpus:**

| Suite | hoisted | numeric today | **+3-8a** | would be | | `OtherName` drops | `LocalSlot` steps |
|---|--:|--:|--:|--:|--:|--:|--:|
| Richards | 70 | 12 | 1 | 13 | 1.08× | 1 | 0 |
| DeltaBlue | 126 | 22 | 1 | 23 | 1.05× | 1 | 2 448 |
| RayTrace | 182 | 21 | 1 | 22 | 1.05× | 5 | 0 |
| Box2D | 1 446 | 80 | 3 | 83 | 1.04× | 8 | 272 322 |
| EarleyBoyer | 597 | 47 | 3 | 50 | 1.06× | 7 | 19 149 |
| Crypto | 358 | 26 | 2 | 28 | 1.08× | 16 | 7 191 452 |
| **NavierStokes** | 141 | 24 | **15** | **39** | **1.62×** | 17 | **9 461 760** |
| **Total** | **2 920** | **232** | **26** | **258** | **1.11×** | | 16 947 131 |

**26 names, and the distribution is the result rather than the total.** Six suites gain one to
three names each; **NavierStokes gains fifteen and its numeric-local count goes 24 → 39, 1.62×** —
by far the largest widening any item in this phase has produced on a single suite, and it lands on
exactly the suite the update-target census says carries **9.46 M of the 16.95 M `LocalSlot` steps
and 6.76 M of the 7.05 M real update boxes**. *The population and the traffic are concentrated in
the same place, which is the condition every other phase-3 widening failed.*

**Against the item it most resembles**: 3-7 widened the tier by **8 names, 224 → 232, 1.036×**, and
was worth 1.0001× on the corpus because its eight names were scattered where nothing hot lived.
This is 26 names at 1.11× with fifteen of them in the hottest boxing loop in the corpus. **The
prize is still bounded by `0088`'s exchange rate** — 6.76 M boxes is ≈0.16 GB, ≈115 ms, **0.6% of
the driver** — so what has changed is confidence, not size: the item now has a counted population
in the right place instead of an estimate.

**And the count does not license the build.** The mechanism is still the XL described above: every
fast path keys off `NumericStorage`, and a speculative local is a double only while a flag holds.
What the count settles is that if that work is ever done, there is something for it to reach.

#### Built, complete, measured — and it does not pay: **`0094`**

The whole mechanism is built: the dual representation, the writes, the `++`/`--` step, and all
three consumers that can take a raw `double`. It is **off by default**
(`BROILER_JS_SPECULATIVE_NUMERIC_LOCALS=1`), and it stays off, because the finished item is a
**1.2% regression** on the corpus's boxing and the counter that says why also says no fourth
consumer would change it.

**The storage half.** A speculative local is held as a raw `double`, a `bool` saying the double is
live, and the ordinary `JSValue` slot; `Expression` becomes a conditional over the two, so **every
existing read site is correct without being touched** and a write through it is an assignment to a
conditional, which the backend rejects loudly. That is the numeric tier's own safety argument
reused — the field it does *not* get is `NumericStorage`, because five fast paths read that on the
understanding that the binding **is** a double, and a speculative one is a double only sometimes.
Writes route through `AssignToVariable`, which lands the value in the slot, derives the flag from it
and mirrors the raw half — branch-free, and reading the flag and the double **off the slot** rather
than off the expression, so a value with a side effect cannot run twice. The `++`/`--` step branches
on the flag: while it holds, the increment is a native double add that **writes nothing back to the
slot**, which is the box the census priced.

**The three consumers.** The guarded arithmetic tree offers a speculative local **as a leaf** —
`OrderedNode` already *is* a raw double, a flag and a fallback, so the shape needed nothing invented
— snapshotted into three CLR locals at the leaf's own postorder position. The element **read**
(`x[currentRow]`) and the element **write** (`x[currentRow] = v`) each emit two arms over item 3-0's
`GetElementByNumber(double)` / `SetElementByNumber(double, JSValue)` and the ordinary indexer.

**Three things the build got wrong, and how each was caught.**

**A leaf that offered a stale slot.** `OrderedNode.IsLeaf` means *"the saved operand is the value
whichever way the test went"* — true of an ordinary guarded leaf, which saved the `JSValue` it was
handed. It is **false** of a speculative leaf, whose slot is deliberately stale exactly while the
flag is up, so a tree that fell to its generic arm read a value several increments old. `x++` three
times then `x + tail` answered `"0!"` instead of `"3!"` — no exception, no NaN, just an old number.
Fixed by building the leaf `IsLeaf: false` so `AsJSValue` re-materializes from the flag, which costs
a box on the arm that was going to box anyway.

**A leaf that was nearly unreachable.** Eligibility is
`CountOperators - 1 + CountNativeLeaves ≥ 1`, and a speculative local counts as neither — so
`c + p.v`, **the shape the whole population is made of**, was refused for having no saving to make
and the new leaf never ran. Counted as its own term (`CountSpeculativeLeaves`, self-gating: with the
switch off no variable carries a flag, so the control arm's rule is byte-for-byte unchanged).

**The first three fixtures proved nothing, and the file records why.** They passed against the
*broken* emitter. Two distinct causes, both worth keeping: the tree fixtures never built a tree at
all (the eligibility gate above), and the ordering fixture wrote `i = "2"` — **provably** non-numeric,
so it defeated the local's candidacy at *compile* time and the path under test was never emitted.
Each fixture was then re-checked by deliberately breaking the emitter and confirming it failed:
forcing the slot arm turns `60` into `30` and `11,22,33` into `33,2,3`.

*And the ordering fixture could not be repaired, which is the more interesting outcome.* `a[i]`
evaluates the receiver before it reads `i`, so a receiver that disturbed `i` would make the order
observable — but **to write `i` from inside a getter the getter must close over `i`, and a captured
binding is a `JSVariable` cell, which is not a candidate for either numeric tier.** The two
properties are mutually exclusive by construction. The fixture became a pair asserting exactly that,
and the receiver temp is justified by what it really buys — the compiled receiver emitted once,
behind one inline-cache site — rather than by an ordering rule that cannot be violated.

**Measured on the corpus, one build, the switch the only difference:**

| Suite | boxes off → on | | speculative locals | `LocalSlot` steps off → on |
|---|--:|--:|--:|--:|
| Richards / DeltaBlue / RayTrace / Box2D / EarleyBoyer | unchanged | 1.000 | 0 / 0 / 0 / 0 / 2 | unchanged |
| Crypto | 13 415 650 → 13 414 358 | 1.000 | 1 | 7 192 736 → 7 192 166 |
| **NavierStokes** | **11 747 641 → 12 136 012** | **1.033** | **14** | **9 461 760 → 8 626 176** |
| **Total** | **31 400 805 → 31 787 884** | **1.012** | **17** | |

Each consumer moved it, and none of them moved it enough: storage alone **1.021×**, plus the tree
leaf and the element read **1.017×**, plus the element write **1.012×**.

**Two things about that table are worth stating rather than smoothing.** The control arm was run
twice and **six of the seven suites are bit-identical**; only Crypto moves, by **5 668 boxes
(0.04%)**, which is the run-to-run variability `0084` recorded for it and which is larger than
anything this item does to that suite — so Crypto's `1.000×` row means *below its own noise*, not
*exactly zero*. And the control total here (**31 400 805**) does not match the one recorded against
`0093` (**31 162 965**), which is 635 from `0085`'s corpus baseline: since the four unaffected
suites are bit-exact across runs, that difference cannot be drift, and the earlier figure was a
**carried total rather than a re-sum of its own run**. Both arms in the table above come from one
build and one pair of runs, which is the property the ratio needs.

#### The counter that should have existed first, and what it settles

Three consumers were built by *guessing* where the remaining boxes were and checking afterwards
whether the total moved. `JSNumber.CreateSpeculativeRead` — a fourth factory entry beside
`CreateLiteral` and `CreateConversion`, on the same pattern — attributes a box **at the read**, and
answers directly:

| | NavierStokes |
|---|--:|
| boxes minted **reading** a speculative local | **393 705** |
| net change in boxes | **+388 371** |
| ⇒ boxes the whole item **removes** | **≈ 5 300** |

**The dual representation costs 394 000 boxes to save 5 300.** That is the item, and it is not a
matter of one more consumer: the 835 584 steps it genuinely takes off `Increment` mostly **do not
save an allocation**, because NavierStokes' steps are `x[++currentRow]` — the result is used as an
index, so the fast arm does a native add and then boxes the result anyway. Only a step whose value
is discarded (a `for` update clause) saves a box, and NavierStokes has almost none.

***The item is closed as measured, not deferred.*** The mechanism is correct, tested on both arms,
and left in the tree behind a switch that defaults off, because the thing that makes it lose is the
read/write ratio of the code it targets — `currentRow` is read four ways and incremented once — and
that ratio is a property of the workload, not of how many consumers the compiler grows.

**§3.5 gains the rule this cost most of an item to learn:** *a representation change is priced by
the ratio of reads to writes on the population, and that ratio has to be counted before the
representation is built.* The three consumers were each a reasonable guess and each of them was
worth less than the read it displaced; one counter at the read would have said so before any of
them existed. The same mistake, in the same shape, as the bitwise operators in item 3-1 — *count how
many of a fast path's operands can actually reach it* — except that this time the operands reached
it and the **other** side of the trade had not been counted.

**And the measurement corrects the count's own reading.** The population is 15 names in
NavierStokes, and the scoping above took the alignment between that and NavierStokes' 9.46 M steps
as read. Measured, **those 15 names carry 835 584 of the 9.46 M — 8.8%, not the whole of it.**
*The suite that holds the names and the suite that holds the traffic being the same suite is not
the same claim as the names holding the traffic*, and only running the arm distinguishes them.

**Wall clock was deliberately not measured, and that is a scoping call rather than an omission.**
Every landing item in this phase reports time beside allocation, because a box count is a proxy and
the exchange rate has to be checked. This item does not land: the switch defaults off, so the
shipping arm's time is unchanged *by construction*, and the only thing a driver run could price is
how much the losing arm costs in milliseconds — a number that changes no decision, since the
decision is already made by a counter that is exact rather than sampled. Six ABBA pairs on an arm
nobody will ship is an hour spent confirming the sign of something already counted. **If the item is
ever re-opened for a workload with a different read/write ratio, the timing run belongs to that
attempt, not to this one.**

**Gates.** 1 191 compiler tests, 4 571 integration, 2 103 built-ins, plus runtime, core, parser,
modules, storage and CLR — all green **on both settings of the switch**. 20 of the compiler tests
are `SpeculativeNumericReadPathTests`, every one asserted on both arms so a disagreement between
them *is* the bug, and `NumericLocalDefeatTests`' four shape fixtures are Theories over the switch —
the answer is unchanged and only the row moves, `LocalSlot` when off and nothing when on, which is
what says the speculation fires on exactly the shapes it was scoped from.

**The A/B the item was scoped from still holds, and that is the point.**
`NumericLocalDefeatTests` carries it reduced to one difference:

| Inner function | Result |
|---|---|
| `var c = 2 * rowSize; c++` — `rowSize` one scope out | `LocalSlot`, and every `c++` boxes |
| `var c = 2 * 10; c++` — literal | **numeric**, and `c++` costs nothing |

Same nesting, same body, same update; one identifier different. The enclosing-scope read really is
**the** defeat on this shape, and testing it at run time really does remove the row. *Every premise
the item was scoped on survived; the item still lost.* That is the shape of the finding — not a
mechanism that failed to work, but a correct mechanism whose cost was on the side nobody counted.

***So the item closes at a measured −1.2%, and phase 3's largest remaining candidate closes with
it.*** 3-8 was sized XL on the strength of 1 842 dropped candidates; scoped by measurement it became
26 names, then 15 names carrying 8.8% of their suite's steps, and finally a representation whose
reads cost seventy times what its writes save. *Phase 3's remaining work is not blocked on a missing
idea. It is bounded by the exchange rate `0088` measured, and this is what that bound looks like
when an item is followed all the way to a number instead of stopped at a plausible one.*

#### 3-9 · Counted, and closed by its own precondition — **`0095`**

3-9's specification made the count its precondition and predicted where the answer would come from:
*"it does not reach NavierStokes"*, because there the readers of `rowSize` are hoisted function
**declarations** and item 3-7 proved those must keep their `JSVariable` cell. So the population is
names whose enclosing binding is captured only by function *expressions* — and nobody had counted
how many of those the corpus has.

**It has none. Zero on all seven suites.**

| Suite | numeric locals | **3-9 population** | outer-numeric offers | 3-8a population |
|---|--:|--:|--:|--:|
| Richards / DeltaBlue / RayTrace | 12 / 22 / 21 | **0** | 0 | 1 / 1 / 1 |
| Box2D / EarleyBoyer / Crypto | 80 / 47 / 26 | **0** | 0 | 3 / 3 / 2 |
| NavierStokes | 24 | **0** | 0 | 15 |
| **Total** | **232** | **0** | **0** | **26** |

**A zero is exactly the reading this phase has learned not to trust, so it was earned before it was
taken.** Item 3-8a's first population instrument read zero on all seven suites and was nearly
published as a finding before anyone had shown it could read anything else; §3.5 gained the rule
that *a counter never shown to read non-zero is a claim about the counter.* This one was built the
other way round — **nine constructed fixtures first, and only then the corpus** — and three of them
read non-zero. Each was then re-checked by **disabling the probe and confirming it fails**, which is
the discipline `0094` added to §3.5 one item ago, applied to the instrument that decides this one.

**And the zero is not the harness.** 3-8a's 26 is reported from the same call site in
`CreateFunction`, two lines away, behind the same `CanScalarReplaceLocals` gate and the same
compile-time switch, in the same run that reports 3-9's zero. A harness that could not reach the
code would have zeroed both.

**Why the population is empty, counted rather than argued.** A single candidate count cannot tell
two very different worlds apart — nested functions never read an enclosing numeric local, or they
read them constantly and never anywhere typable — and the follow-up differs completely between them.
So a second counter records **how often the enclosing scope chain answers "that name is already a
raw `double`"** while 3-9's pass resolves a function. **It answers never: 0 offers on the whole
corpus.** The reads do not exist. There is nothing to import, rather than something that cannot be
used.

That reconciles exactly with the item this one sits behind. 3-9 can only import from a name that is
both *proven numeric* and *still a raw double despite being captured* — and that second condition is
precisely item 3-7's population, which measured **eight names in the entire corpus** (224 → 232).
Not one of those eight is read from an assignment inside the function that captures it, which is
what the offer counter says directly.

**The probe asks what the compiler BUILT, not what the analysis PROVED, and the difference is the
item's own prediction.** Pointed at the enclosing analysis's conclusion instead of at
`NumericStorage`, the hoisted-declaration fixture flips from 0 to 1 — a name 3-7 leaves in a cell
for correctness would be reported as a win — and the two-levels-out fixture flips from 1 to 0,
because a per-frame set is not what a lexical reference resolves through. Both flips were run.

***So 3-9 is closed without being built, and the mechanism is the cheapest thing in phase 3 to have
declined.*** Unlike 3-8a it needs no run-time test, no flag, no fallback representation, and its
failure mode is structurally absent — a name it typed would be an ordinary numeric local that every
fast path reads unchanged. **It is a good mechanism with nothing to point it at**, and building it
would buy an extra analysis pass and a scope-chain probe per compiled function in exchange for zero
names. *The count cost one instrument and no mechanism, which is the whole argument for taking it
first.*

**What would re-open it** is stated because the counter is left in the tree to answer it: 3-9's
population is bounded above by the number of captured numeric locals, so **widening item 3-7 is its
only supply**. If a future change moves 3-7's eight, re-run this counter before re-reading this
section — it is off by default (`BROILER_JS_OUTER_NUMERIC_COUNT=1`) and costs nothing while it is.

**Gates.** Full engine suite green — 1 200 compiler, 4 571 integration, 2 103 built-ins, plus
runtime, core, parser, modules, storage and CLR. The counter is off by default and changes no
emission on any setting, which is what makes this a measurement rather than a change.

**One pre-existing flake was found on the way. It is a real engine race, and it is now fixed —
see the section that follows.**
`CapturedNumericLocalTests.SuspendingNestedFunctionsCaptureThroughTheSameBox` fails
intermittently **under CPU load** — three of four runs while the test262 matrix was saturating the
container at load average ~14 on four cores, and not at all on an idle one. The failure is always
the same assertion and always the same way:

```js
var out = 'no'; var v = 1;
var f = async function () { v = v + 1; out = v; await 0; v = v + 10; };
f();
String(out) + ',' + v      // "2,2" required; "2,12" observed
```

`await 0` must queue a microtask, so `v = v + 10` cannot have run before the synchronous caller
returns. **`"2,12"` says the continuation resumed early**, which is a real scheduling violation
rather than a slow test — there is no timeout or drain in the fixture to be racing. **It reproduces
on the unmodified baseline**: the same four runs with this item's changes stashed fail three times,
so it is neither 3-9's nor 3-8a's. It is noted here because it is load-dependent and therefore
invisible on a quiet machine, which is how it survived every full-suite run in this phase until a
saturated container made it visible.


#### The async resumption race — found by the gates, and it was two threads running JavaScript at once: **`0096`**

The flake above is not a slow test. `await 0` queues a job, so the statements after `f()` belong to
the job already running and `v = v + 10` cannot have happened when they read `v`. **`"2,12"` says
the continuation ran anyway** — and it ran on a different thread.

**Both dispatch paths were wrong, for opposite reasons, and each covered the other's absence.**

| Ambient `SynchronizationContext` | What the engine did | Why it is wrong |
|---|---|---|
| none — every plain `Eval` | `ThreadPool.QueueUserWorkItem` | runs the job on a pool thread |
| present — every xUnit test | `SynchronizationContext.Current.Post` | xUnit's `AsyncTestSyncContext` dispatches through the pool too |

A job resumes a generator, and resuming runs user JavaScript, so **the engine let two threads
execute JavaScript in one context simultaneously.** ECMAScript's agent is single-threaded by
construction and this engine is written throughout on that assumption, so a wrong arithmetic answer
was the visible corner of an unsynchronized heap. *The reason it read as a rare flake rather than as
corruption is that the racing job only incremented a number.*

**The second row is the one that matters for how this was nearly missed.** The first fix addressed
only the thread-pool fallback, and the console harness agreed — **18 of 3 000 wrong before, 0 of
3 000 after.** Then the new fixtures failed anyway, because a test host is never the no-context
case: xUnit installs `AsyncTestSyncContext` on every test thread, so the suite had always been
taking the *other* branch. **The rate had also been measured on a loaded machine and re-measured on
a quiet one**, which on its own would have been enough to believe a fix that fixed the wrong half.
What settled it was a deterministic fixture, not a rate.

**The rule now lives in one place** (`JSContext.PostJob`), and the order of its cases is the whole
of it:

1. **We are on the engine's own pump** (`AsyncPump`, marked `IJSJobPump`) — post there.
2. **The pump this work belongs to**, captured when the promise or the `await` was created.
3. **This context's queue**, while it is executing JavaScript — the new case, and the fix.
4. **A host context**, with nothing executing.
5. **The thread pool**, with neither.

**Case 2 is not defensive and dropping it deadlocks `Execute`**, which is how the first attempt at
this rule failed: a promise created on the pump thread can be settled from a pool thread, where
`SynchronizationContext.Current` is null and case 1 cannot see the pump. Without case 2 the job took
the queue, the task `AsyncPump.Run` was blocking on never completed, and the pump spun forever
waiting for work that had gone somewhere else. `Issue814ForAwaitUsingTests.ForAwaitWithAwaitUsingHead`
hung for twelve minutes at load average 0.10 before `--blame-hang` named it. *Only a pump is
trusted, whether current or captured: an arbitrary captured context is no more the JavaScript thread
than an arbitrary current one.*

**The queue cannot strand a job, and that is a property of when it is taken rather than of a
fallback.** It accepts only while a JavaScript execution is in progress — exactly when there is
something for the job to race — so anything posted with nothing running keeps its old dispatch. That
is what lets a host `Task`-backed promise settle long after `EvalWithTopLevelAwaitAsync` returned.
The depth stays at one for the whole drain, so a job that queues another job takes the queue too, and
the return to zero is made under the same lock as the final dequeue, which is the only window in
which an enqueue could be lost. A nested `Eval` — a host callback evaluating more source while
JavaScript is on the stack — does not drain, or a job would run in the middle of another job and
reintroduce the interleaving on one thread.

**What is fixed and what is not.** The race is gone whenever JavaScript is executing when the job is
posted, which is every in-script `await` and every reaction queued by a running script. A job posted
while *nothing* is executing still takes the host context or the pool, and could in principle land
during a later execution. Closing that too means the embedding contract has to name a JavaScript
thread the engine can serialize against, which is a change to the API rather than to a dispatch
site. **That residual was then measured and closed — see below; "in principle" turned out to be 172
overlaps in 200 rounds.**

**Gates.** The eight new fixtures are written to **lose the race deterministically** — a spin after
the call, so a racing thread reliably wins — which is the difference between them and the test that
found this: that one asserts the same value and caught the bug **0.6% of the time**. Measured
against the unmodified pin they fail **5 of 8**; with the fix, 8 of 8. The whole engine suite is
green, and the two job-ordering test262 manifests — `test262-promise-jobs` and `test262-for-await`,
whose `ticks-with-*` cases assert exact microtask tick counts and are the sharpest check available
on this change — pass **5/5 and 2/2**. The five pinned manifests were re-run against it as well;
their counts are recorded in §3.4.

#### The embedding contract — the residual, measured at 86% and closed: **`0097`**

`0096` fixed where a job is *dispatched* and named what that could not reach: **a job posted while
nothing is executing** takes a host context or the thread pool, because the queue is deliberately
refused at depth zero — that refusal is what makes stranding a job impossible. Such a job could then
run JavaScript while a later `Eval` was running JavaScript.

**"In principle" was doing a lot of work in that sentence.** Reaching the case needs a JavaScript
entry point that is not `Eval`, and a host invoking a `JSValue` directly is exactly one — arm a
promise, settle it from a host thread with nothing running, and evaluate meanwhile. Measured:

| | peak threads in one context | overlaps / 200 rounds |
|---|--:|--:|
| `0096` — dispatch fixed, no lock | **2** | **172 (86%)** |
| `0097` — with the execution lock | **1** | **0** |

**The counter had to be built twice, and the first version is the more instructive one.** It counted
threads inside JavaScript *process-wide*, which is not the invariant: **two independent contexts
running in parallel is exactly what an embedder is supposed to be able to do**, so a process-wide
count reports legitimate concurrency as a violation and would fire on any full-suite run, where
xUnit evaluates several test classes at once. Detection is per context; only the aggregate is static,
because an overlap is a real violation whichever context produced it. *A concurrency counter measures
the wrong thing by default, and the default is plausible enough to ship.*

**The fix is one lock and one contract.**

A per-context `Monitor` is taken by every execution the engine owns — `Eval`, `Execute`,
`ExecuteAsync`, the queue drain, and **every job wherever it was dispatched** — so an evaluation and
a job are mutually exclusive even when the job runs on a pool thread. Re-entrancy is required rather
than convenient: a host callback that evaluates more source is the same agent going deeper and must
not deadlock against itself.

What the engine cannot see is a host reaching in by another route — invoking a `JSValue`, reading a
property whose getter is a JavaScript function, calling back from a thread of its own. Those are
ordinary calls on ordinary objects and **guarding each one would put a mutex on the engine's hottest
path**. So the rule is stated and given an API instead:

```csharp
using (context.EnterExecution())
    callback.InvokeFunction(new Arguments(JSUndefined.Value, value));
```

*Wrap host-initiated entry into JavaScript, unless it is already inside one.* A call made from within
an engine-owned execution needs nothing, and wrapping it anyway is harmless.

**What the lock costs is a bounded wait, and one pattern it cannot support: JavaScript blocking on a
host task whose completion has to re-enter the same context.** That was written here as one thing
the lock broke, and measuring it found the attribution wrong and the problem twice as large — see
the section after next. **Measured, it costs nothing on the suite**: the integration suite runs in 46–47 s
against a 43–57 s baseline, and the earlier reading of 3 m 4 s was a concurrently-running sweep
rather than the lock, which is worth recording because it was nearly believed. One allocation was
found and removed on the way — the public handle is a class, so the per-job path uses the struct
scopes directly rather than putting an allocation on every microtask.

**Gates.** Five new fixtures asserting the *property* rather than a value, which is the point: a
value assertion catches an overlap only when the overlap happens to change that value, and that is
how the original defect survived a phase of full-suite runs at a 0.6% hit rate. They cover the
residual shape, two host threads evaluating on one context, the contract API, re-entrancy, and that
jobs queued under a host scope drain when it is released. Whole engine suite green — **8 098 tests
across 13 projects, no deadlock** — with `test262-promise-jobs` and `test262-for-await` at 5/5 and
2/2 and the five pinned manifests recorded in §3.4.


#### The blocking host wait — one deadlock from each of the last two changes: **`0098`**

`0097` recorded that the lock cost "one pattern it cannot support". Measured, **the attribution was
wrong and there are two patterns, one contributed by each change**:

| Host function called from a script, waiting on a `Task` completed by… | `0096` queue | `0097` + lock |
|---|:--:|:--:|
| **a promise reaction on this context** | **hangs** | hangs |
| **host work that must enter this context** | completes | **hangs** |
| unrelated host work (control) | completes | completes |

The first is the *queue's*, not the lock's, and it arrived a change earlier than the note that
blamed the lock for it: a host frame called from a script is inside an execution, so a reaction it
waits for is **queued** and cannot run until that execution ends — which it never does. The second is
the lock's: the execution lock the frame holds keeps out the host work that would complete the task.
*Two mechanisms, two deadlocks, and writing one of them down as the other's cost is what the control
row exists to prevent.*

**`JSContext.WaitFor(task)` is the supported wait, and it answers one shape each.** It **drains** the
queue — which releases the first — and then **releases the context** while it blocks, retaking it
afterwards — which releases the second. The drain happens *before* the release, so a job queued by
the execution being suspended runs on the thread that queued it and in the order it was queued,
rather than being handed to whichever thread takes the context next.

```csharp
context["readFile"] = JSValue.CreateFunction((in Arguments a) =>
    (JSValue)context.WaitFor(File.ReadAllTextAsync(a.Get1().ToString())));
```

**The hazard it trades for is real, and is why the API is explicit rather than automatic.** While
the context is released, other JavaScript can run on it — queued jobs, another host thread — so
state the waiting frame read before the wait may differ after it. That is inherent in blocking a
single-threaded agent: *the alternative is not "safe", it is "hangs".* `task.Wait()` and `task.Result`
still deadlock, and are still the wrong thing to write; nothing detects them automatically, because
"this thread is blocked waiting for something that needs the context" is not a question a lock can
be asked.

**Two details that are easy to get wrong and are pinned by their own fixtures.** The depth released
is also the number of `Monitor` entries the thread holds, so a wait made two levels deep has to give
up both and take both back — getting that wrong leaks the lock and the *next* entry deadlocks, which
no value assertion would catch. And the fault is re-observed **after** the context is retaken and
through the awaiter rather than through `Wait`, so a host function sees the exception the task
actually carried instead of the `AggregateException` wrapping it, with the context held so it can
turn that into a JavaScript throw.

**Gates.** Seven fixtures, **each on a worker with a 15-second budget**, so a regression fails in
seconds instead of hanging the suite — which is not caution: the last deadlock met here took twelve
minutes and `--blame-hang` to identify. Against a plain blocking wait they fail **4 of 7**, three of
them reporting `deadlocked` explicitly; with `WaitFor`, 7 of 7. Whole engine suite green, and the
conformance manifests are recorded in §3.4.

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
| **4-3** | **Deoptimization** — **designed; 4-3a and 4-3b both landed** | `Runtime/FunctionTiering.cs`, `Engine/CallFrames.cs`, and for 4-3b `.Compiler` / `.ExpressionCompiler` | The safety net that makes everything else legal. "Bail out mid-function by reconstructing an interpreter frame" is **not expressible here** — there is no interpreter frame. Splits into **4-3a** (S, the restart contract the pilot already implements) and **4-3b** (M–L, a generic fallback branch inside the specialized method), and only 4-3b gates 4-4 | ~~XL~~ **S + M–L** |
| **4-1** | **Type feedback collection** — **shapes and callees landed; numeric-vs-generic outstanding** | `Runtime/TypeFeedback.cs`, `Runtime/ObjectShape.cs`, `LinqExpressions/JSFunctionBuilder.cs` | The inline caches already observe shapes at property sites but do not *retain* them. Now recorded per site and kept: receiver shapes at reads, callee identities at calls. **And it answers the question the rest of the phase rests on — see below.** Callee identity was phase 2's 2-6 until that item was measured: there is no repeated callee resolution to remove, so recording it is feedback and nothing else, and it pays only once 4-2 and 4-4 consume it | L |
| **4-2** | **A specializing tier-2 compile** — **split by measurement; 4-2a and 4-2b both landed, arithmetic half outstanding** | `BuiltIns/Function/JSFunction.cs` — replace the `numericPlan == null` branch, plus `Runtime/TypeFeedback.cs` and `.LinqExpressions` for 4-2b | Consume 4-1's feedback: monomorphic property access → shape check plus direct slot read; arithmetic → raw `double`/`int` where feedback says so. **Measuring the branch first found it unsound** — it does not recompile the same code the same way, and DeltaBlue died on it — so the item splits into **4-2a** (S, the recompile contract) and **4-2b** (L, the specializing emission). 4-2b specializes **44.7% of the corpus's executed reads at 0.818× each**, which is **0.83% of suite time**: real, and below the noise floor | ~~XL~~ **S + L** |
| **4-4** | **Inlining of small JS callees** at monomorphic sites — **premise measured; re-specified, do not start as written** | `.Compiler` | What Richards and DeltaBlue actually need, and the measurement says why: **a call costs ~250 ns, about thirteen times the loop body it replaces** (2-6). Strictly downstream of 4-3, 4-1 and 4-2 — the callee-identity feedback it needs is 4-1's, not a separate phase-2 item. **Measured before starting, and the ceiling is 1.89%**: 6 194 758 invocations of which **37% are to native builtins with no body to inline**, 3 902 620 with a JavaScript callee, 64.0% of those from a promoted function, and a hand-inlined control says inlining saves 149 ns each. Inlining is *expressible* here — unlike 4-3's deopt, the mechanism exists — so the blocker is value, and it splits into **4-4a** (the stack-trace question) and **4-4b** (AST-level inlining). **New 4-5** — make the fixed 142 ns call prologue cheaper — addresses more calls for less risk | ~~XL~~ **deferred; 4-5 first** |
| **4-5** | **The fixed cost of a call** — **ablation done; premise mostly falsified, one cost fixed** | `Engine/Core/JSEngine.cs` | A call costs **142 ns before any argument** plus 17.1 ns each. The ablation prices every piece: **five nested `using` scopes cost 0.011 ns**, EH 0.73 ns, dispatch 0.68 ns, ThreadStatic reads free — so the prologue is *not* where the cost is, and 2-6 is confirmed directly. The one real cost is an **`AsyncLocal<bool>` read at 7.0 ns against a `[ThreadStatic]` at 0.31 ns**, read on every call, and documented in `JSEngine` as *"reads are cheap"* — **wrong by 24x**. Mirrored into a ThreadStatic, keeping the AsyncLocal as the carrier: **0.22% of the corpus**, pinned by 9 tests that also pass on the unmodified engine. **~85% of a call's fixed cost remains unattributable from outside the engine** — the rest of the item is blocked on a profiler, not on a design | ~~M–L~~ **S landed; rest blocked** |
| **3-5** | **A numeric local compared against a `JSValue`** — **landed** | `.Compiler` — `FastCompiler.VisitBinaryExpression` | The control loop every probe here used as a floor was paying a box per iteration: `i` is a raw double, `n` is a `JSValue`, and `<` had a native form only when **both** sides were doubles, so the raw side was boxed to meet the generic operator. The cause is not the parameter — unboxing the *other* side needs no entry guard and covers more (`i < a.length` is a property read). Sound because ToPrimitive of a Number is that Number; `<`/`>` only, as NaN makes `<=`/`>=` unsafe. **33.77 → 10.03 ns and 32 → 0 B per iteration, 3.4× on its shape**, 33 semantics tests that all pass on the unmodified compiler too. **On the corpus it is invisible — 0.997× bytes — and why is the finding: only 5.0% of scalar locals (203 of 4 029) reach the numeric tier at all** | M |
| **3-6** | **Which conjunct costs the coverage** — **counted; answered and closed** | `.Compiler` — `FastCompiler.VisitBlock`, `NumericLocalAnalysis` | Its own instruction was to count before designing, and the count retired the design. Of **2 695 hoisted names**: 203 accepted (7.5%), 2 012 not proven numeric (74.7%), 478 captured by a nested function (17.7%), and `CanScalarReplaceLocals` — the conjunction 3-5 blamed — rejects **2 (0.1%)**. Counted again inside the analysis, *not proven numeric* is not what it sounds like either: only **~170 names are never offered**, while the optimistic fixed point **offers 2 335 and drops 1 842 (78.9%)**, because something assigned to them comes from a parameter, a property read, an element or a call. The counts reconcile, and the residue is **290 names the analysis proved numeric that the hoist site refused for being captured**. Splits into **3-7** and **3-8** | L |
| **3-7** | **A raw-`double` cell for a captured numeric local** — *new, from 3-6's count* | `.Compiler` | A closure captures through a cell, so a numeric local any nested function mentions keeps its `JSVariable`. **290 names are provably numeric and refused for exactly that**; giving them a raw-`double` cell takes numeric locals **203 → ~493, 2.4×**, with no speculation and no guard. The only part of 3-6 that is a widening in the sense the item meant, and the one to size next | L |
| **3-8** | **Guard a local's numeric-ness at run time** — **3-8a built complete and closed as a measured regression** | `.Compiler` + 4-3b's `SpeculationBuilder` | The fixed point's **1 842 dropped candidates — 68% of all hoisted names** — are dropped for want of a *type*, not for want of a rule: the values come from parameters, property reads, elements and calls, none knowable statically. No widening of a conjunction reaches them. Scoped by measurement the XL became **3-8a**, an M for 0.6%: one conjunct, 26 names, 15 of them in NavierStokes. **Built — the dual representation, the writes, the `++`/`--` step, and all three consumers that can take a raw double — and it costs more than it saves.** Each consumer moved the number and none moved it enough (1.021× → 1.017× → 1.012×), and a counter added **at the read** settled it: **393 705 boxes minted reading against ≈5 300 removed**, because the 835 584 steps it takes off `Increment` are mostly `x[++i]`, whose result is boxed to be an index anyway. *Every premise survived and the item still lost.* Off by default and staying off; the mechanism stays in the tree behind its switch, correct and tested on both settings | ~~XL~~ **M, built, −1.2%** |bailout is either unsound or restricted to functions with no observable side effect
before the guard — which excludes everything worth optimizing. *(Satisfied: 4-3's design
spike, then 4-3a and 4-3b, all landed before 4-2 began — and 4-2b is 4-3b's first
consumer.)*

**Verify.** Deopt correctness before any speculation ships: a test that forces every
guard to fail at every point in a function body and asserts the fallback produces the
unspecialized answer. Then the full test262 matrix — **this phase can break anything.**

> **The frame work in §4.1 is a prerequisite nobody filed as one.** Mid-function
> bailout needs to reconstruct an interpreter frame from a specialized one, and the
> activation record is now a slot in `CallFrameStack` addressed by a `FrameToken`
> struct. The three invariants that redesign asserts — a suspendable frame retaking a
> slot under a different caller, unwinding refusing to grow back into abandoned slots,
> and popping past stranded callees — are exactly the surface 4-3 has to preserve.

### 4-1 · Type feedback collection — **landed, and it settles the phase's premise**

> **In the pin.** Shipped as `patches/0069` while its push was blocked by a 403; since applied
> and pushed, and the pointer bumped — it is commit `0932cae6`, an ancestor of `61c8cc65`.

**What it is, and why the inline cache is not already it.** The property cache observes shapes,
but it observes them *to answer the current read*: it replaces entries when they go stale
(item 2-12) and drops everything once a site passes four shapes. Feedback has to **retain**,
because "this site only ever saw one shape" is a claim about history, and a structure designed
to be overwritten cannot make it. `Runtime/TypeFeedback.cs` keeps, per site, the distinct
receiver shapes at a read and the distinct callee identities at a call, plus the observation
count and whether the site overflowed the four-entry cap — the same threshold the cache calls
megamorphic, so the two words mean the same thing about the same site.

**Two gates, deliberately, and they are not the same gate.** Property feedback is a runtime flag
tested inside the site helper, which already pays a predictable branch per read for the
cache-hit counter. Call feedback is gated at **compile** time: with the flag clear the compiler
returns the call's target expression untouched, so the emitted call is the one emitted before
this item existed — no extra hop, no extra branch, no extra argument. **A call costs ~255 ns
(2-6) and is the path phase 4 exists to fix; instrumenting it unconditionally in order to
measure it would be self-defeating.** The cost of that choice is that enabling the flag does not
retrofit already-compiled code, which is pinned by a test rather than left to be discovered.

#### What the feedback says, which is the actual deliverable

4-1 buys no throughput — the item says so, and that is the reason to be careful about what it
*is* for. **4-2 ("monomorphic property access → shape check plus direct slot read") and 4-4
("inlining of small JS callees at monomorphic sites") are an XL each, and both are worth their
cost only in proportion to how much real work happens at monomorphic sites. Nothing in this
engine could report that number until now.** Seven Octane suites, three runs per benchmark,
weighted by **executed operations** rather than by site count — because a tier only pays where
the work is, and ten thousand cold monomorphic sites are worth nothing:

| Suite | Reads | Monomorphic | Calls | Monomorphic | Megamorphic sites |
|---|--:|--:|--:|--:|---|
| Richards | 605 672 | **96.74%** | 121 404 | **83.76%** | none |
| DeltaBlue | 1 001 675 | **77.10%** | 346 333 | **83.12%** | none |
| RayTrace | 2 919 249 | **94.06%** | 476 934 | **95.56%** | none |
| Box2D | 25 963 010 | **94.12%** | 1 501 362 | **99.67%** | 1 read, 3 call |
| EarleyBoyer | 5 490 829 | **100%** | 1 537 115 | **97.68%** | 14 call |
| Crypto | 1 891 024 | **73.82%** | 255 454 | **100%** | none |
| NavierStokes | 428 | — | 630 | — | none |
| **All seven** | **37 871 887** | **93.54%** | **4 239 232** | **96.70%** | 18 sites total |

**The premise holds, and now it is measured rather than assumed: 93.5% of executed property
reads and 96.7% of executed calls happen at a site that only ever saw one shape, or one
callee.** Phase 4's two XL items are well-founded on this corpus. Three things worth keeping:

- **Megamorphism is essentially absent** — 18 sites across 37.9 M reads and 4.2 M calls, and
  five of seven suites have none at all. This corroborates 2-10, which found **0** megamorphic
  read sites while decomposing DeltaBlue's misses, and it means the fallback path a
  speculating tier needs will be cold in practice. It does not make the fallback optional:
  4-3b still has to be correct, it just will not be hot.
- **DeltaBlue is the worst read case at 77.10%**, and it is the suite that fails phase 2's
  200× gate at 460×. Its 359 live read sites include 43 polymorphic ones against Richards's 1
  — so what is left of DeltaBlue has a polymorphic-read component that phase 2's cache work
  could not reach and 4-2 could. That is a lead, not a conclusion.
- **NavierStokes exercises neither path**: 428 reads and 630 calls for a whole suite, against
  Box2D's 26 M. Its work is typed-array *elements*, which no property site serves and no shape
  can hold — the same observation §4.3's B3 table already makes about arrays. Its 100% is
  arithmetically true and evidentially empty, and is reported as `—` rather than as a win.

**Cost when off.** For calls it is **zero by construction**, not by measurement: the emitted
expression is the same object when the flag is clear. For reads it is one static bool test per
read, probed with six ABBA-interleaved process pairs over a 60 M-read loop — **median paired
ratio 0.9835, spread 0.961–1.019**, i.e. the change arm came out nominally *faster*, which is
this container's noise and not an effect. **The honest statement is that the probe bounds the
cost at roughly ±2% and cannot resolve anything smaller**; a 1 ns-per-read cost would be 0.55%
and would not be visible here.

**Verify.** `TypeFeedbackTests`, 16 cases: that nothing is recorded while disabled; that a call
compiled before enabling is *not* retrofitted (the compile-time gate's observable half); that a
site seeing one shape is monomorphic, two is polymorphic and five is megamorphic, with the same
three for callees; that cold sites are counted apart and excluded from the shares; and that six
call and property shapes — including `new`, a prototype method, and an optional call — compute
the same answer with feedback on as off. **`--cache-metrics` is byte-identical with and without
the change**, which is what says the feedback does not perturb the caches it observes.

**What is not done.** The item names a third signal, **numeric-vs-generic outcomes per site**,
and it is not collected. Reads and callees are what 4-2 and 4-4 consume first, and the numeric
signal has a complication the other two do not: the compiler already proves numeric-ness
statically for locals (P2-2 item 3, item 3-3), so a runtime numeric counter would have to be
defined against *that* to say anything new rather than re-reporting it. Left open rather than
half-built.

> **Partly collected since, by item 3-2, and deliberately not per site.** Sizing 3-1 against 3-2
> needed the numeric share of *reads*, so `PropertyOptimizationDiagnostics` now records whether a
> cache-answered property read handed back a number — **50.1% over the corpus, and 98% of those in
> one suite**. That is an aggregate over reads, not the per-site signal this item names, and it
> says nothing about calls; what it settles is a phase 3 ranking rather than a phase 4 one. The
> item's own complication stands: a per-site numeric counter still has to be defined against what
> the compiler already proves statically, and that is why it is still not built.

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

#### 4-3a · The restart contract — **landed, and one of its three conditions was held only by accident**

> **In the pin.** Shipped as `patches/0070` while its push was blocked by a 403; since applied
> and pushed, and the pointer bumped — it is commit `2821f421`, an ancestor of `61c8cc65`.

The item was sized **S** and described as "mostly a rule and a test, since the mechanism ships
today". That is what it turned out to be, and the interesting part is *why the rule was worth
writing down*. The three conditions restart is sound under:

| # | Condition | Held before? | Held **because**? |
|---|---|---|---|
| 1 | every guard fires before any observable effect | yes | yes — the specialized body reads its arguments and touches nothing but its own locals, so there is no effect to repeat |
| 2 | the bailout leaves no `CallFrameStack` slot behind | yes | yes — the specialized delegate never pushes one (the push lives inside the compiled baseline), so on bailout the baseline pushes exactly once, as it would have without tiering |
| 3 | the body is not suspendable | yes | **no — by accident, twice over** |

**Condition 3 is the finding.** Nothing in the engine said a generator or async body must never
be tiered. It was true anyway, for two unrelated reasons, *neither of which is about
speculation*:

- `EnableTiering` is called inside `FastCompiler.CreateFunction`'s **ordinary-function `else`
  branch**; generators and async functions take earlier branches. That is branch placement, not
  a rule.
- `TryPlanScalarReplacement` returns `false` for `Async || Generator`, and the tiering gate
  happens to require `CanScalarReplaceLocals`. That is a rule about *scalar-replacing locals*
  that the tiering gate borrows by coincidence.

**Both are the kind of thing a reasonable refactor removes.** Hoisting the `EnableTiering` call
out of the branch is an ordinary tidy-up; teaching scalar replacement about state-machine fields
is a plausible future optimization. So the hazard was measured rather than argued: with **both**
accidental exclusions defeated and no explicit guard,

```js
async function sum(n) { var s = 0; for (var i = 0; i < n; i++) { s += i; } return s; }
```

— a legal async function whose body matches the planner's counted-reduction shape exactly and
contains no `await` — starts returning **`number` instead of a Promise** from its second call
onward — the first call returns `object`, every later one `number`. The specialized delegate replaces the one
that builds the promise. That is a silent wrong answer, and it is two ordinary refactors away.

**The fix is one condition at the decision point**: `NumericLoopPlanner.TryCreate` refuses a
suspendable function outright, so the property survives the branch structure changing. Restoring
just that guard, with both accidental exclusions still defeated, restores correct answers — the
function keeps returning a Promise. What remains in that configuration is a *generic* re-compile
(`numericPlan == null`), which re-runs the same code the same way and speculates on nothing, so
it cannot violate a restart contract; §4's own header already calls that path "a hook, not a
tier".

**Verify.** `RestartContractTests`, 16 cases across the three conditions: a generator, an async
function and an async generator with the exact matching shape are never tiered and keep
returning their objects, with **the same body as an ordinary function tiered as the control** —
without which the refusal could just be the shape failing to match; a yielding generator still
iterates; the deoptimizing call produces **the same number of observable effects as the untiered
engine** (counted through a `valueOf` on the argument, because adding a statement to the body
would stop it being tiered and the test would pass vacuously); every guard — argument count,
argument type, fractional, negative, NaN and `-0` limits — answers exactly what an untiered
`JSContext` answers; and the bailout unwinds correctly from 200 frames deep and stays catchable.
Repository suite: **7 773 tests across 13 projects, 0 failures**.

**What this does not do.** It states and enforces the contract the pilot *already* runs under;
it does not widen what may be speculated on. Speculation *inside* a body still needs **4-3b**,
below.

#### 4-3b · The in-method fallback — **the mechanism landed; it has no JavaScript-level consumer yet, and that is a finding**

> **In the pin.** Shipped as `patches/0071` while its push was blocked by a 403; since applied
> and pushed, and the pointer bumped — it is commit `72494502`, an ancestor of `61c8cc65`.

The transfer 4-3's design spike identified as the one restart cannot give: compile the
specialized and generic forms into **one method** and make a failed guard a **branch**. The CLR
locals are shared because it is the same method, so no transfer exists to get wrong; nothing is
re-entered, so effects already performed are never repeated; and no `CallFrameStack` slot changes
hands, so the three invariants 4-3a preserves are not engaged at all.

`SpeculationBuilder.Guarded` emits it, and `Runtime/Speculation.cs` carries the site table.

**The guarantee that justifies a facility rather than a hand-rolled conditional.** The subject is
evaluated **exactly once**, into a temporary the guard and both arms share. The obvious
hand-rolled spelling evaluates it in the guard *and again* in whichever arm runs — so a receiver
with an effect (`f().x`) would run `f()` twice. That is a wrong answer visible only on effectful
receivers, which is to say the ones nobody tests by hand. Swapping the facility for that spelling
fails **12 of the 15 tests**, which is how it is known the tests can see it.

**Poisoning is part of the mechanism, not a nicety.** A guard that keeps failing costs its own
evaluation on every execution *plus* the generic path — strictly worse than never speculating.
After four misses (the same threshold the inline cache and 4-1's tracking use) a site
short-circuits straight to generic. **This is deliberately a stand-in**: the right answer once
4-2 exists is to *re-emit the method without the guard*, because a poisoned site still pays one
static array read here. Recorded so the successor knows it is owed.

**What is NOT here, and why it is a finding rather than an omission.** No JavaScript-level
speculation is emitted. That is not scope-trimming — **it is structural, and it sharpens the
sequencing.** A guard needs something to speculate *on*: a shape, a callee, a numeric type. In a
tier-1 method, compiled before anything has run, none of those is known — the compiler has no
observations yet, which is precisely why 4-1 exists. So **the in-method branch only has meaning
inside a tier-2 recompile**, and tier-2 emission *is* item 4-2. The mechanism therefore has to
land before its first consumer, and its first consumer is the next item rather than this one.
The roadmap's ordering (4-3b gates 4-4, and 4-2 consumes both) is right; what was not written
down is that 4-3b cannot demonstrate itself on JavaScript until 4-2 emits something.

**Verify.** `InMethodFallbackTests`, 15 cases, built as expression trees and compiled through
the engine's own IL generator — testing it through JavaScript would test whatever chose to emit
it instead. The phase's own stated verification is the centre of the file: *"forces every guard
to fail at every point in a function body and asserts the generic path produces the unspecialized
answer with the same observable effect sequence"*. Bodies of 1, 2, 3 and 5 guarded operations
with effects before, between and after them are run against an **unspeculated control compiled
from the same shape**, and the effect logs must match entry for entry; then each guard is failed
individually while the rest hold, asserting that every prior effect happened exactly once and
only the failing operation took the generic path. Plus the evaluate-once contract on both arms,
poisoning after four misses (visible as the guard disappearing from the log while the answer
holds), a never-missing site never poisoning, and a refused site index emitting the generic form
alone. Repository suite: **7 788 tests across 13 projects, 0 failures**.

### 4-2 · A specializing tier-2 compile — **split by measurement; both halves landed**

Written after 4-3's design, as the phase requires. The item said "replace the `numericPlan == null`
branch", and the first thing to establish is whether that branch is reached by anything worth an
XL. **`--specializing-tier` answers it on the same seven suites 4-1 used**, with a budget generous
enough not to bound the answer (100 000 recompilations, 512 MiB of retained code — a cap that bound
the result would be reporting the cap):

| Suite | Tiering candidates | Promoted |
|---|--:|--:|
| Richards | 82 | 16 |
| DeltaBlue | 123 | 9 — **and the suite died** |
| RayTrace | 126 | 32 |
| Box2D | 665 | 100 |
| EarleyBoyer | 716 | 33 |
| Crypto | 299 | 14 |
| NavierStokes | 30 | 0 |

**The branch is reached by real code** — the gate is narrow (no nested functions, no outer-function
captures, scalar-replaceable locals, not a class, not an arrow) but a few hundred functions per
suite survive it and tens get hot. NavierStokes's 0 is the same observation 4-1 made about it from
the other side: its work is typed-array elements inside a handful of long-running calls, so there is
nothing to promote.

**And DeltaBlue reported a failure**, which is not something a "hook that recompiles the same code
the same way" should be able to do. That is item **4-2a**, below, and it had to be fixed before
anything speculative could be built on top. The specializing emission is **4-2b**.

#### 4-2a · The recompile contract — **it was not recompiling the same code the same way**

> **In the pin.** Shipped as `patches/0072` while its push was blocked by a 403; since applied
> and pushed, and the pointer bumped — it is commit `3f8d5db4`, an ancestor of `61c8cc65`.

§4's header says the `numericPlan == null` path "re-runs `CoreScript.Compile` on `({source})` — it
recompiles *the same code the same way*, so it cannot be faster". The first half of that is wrong.
A fresh top-level compilation does not reproduce the scope the function was written in, and **two
consequences of that were producing wrong answers on real programs**.

**The recompile builds a second function object.** Only its *delegate* is installed on the original,
so a body that can observe its own function object observes the copy — while every other reference
in the program still reaches the original, and the two differ in every own property the program
installed. DeltaBlue's constructors are written

```js
UnaryConstraint.superConstructor.call(this, strength);
```

so after promotion `UnaryConstraint` names the copy, the copy has no `superConstructor`, and the
suite dies with **`TypeError: Cannot get property call of undefined` — 0 of 1 benchmarks run,
against 1 of 1 with tiering off**. Minimally: a function reading `f.step` off its own name answers
`6|NaN|NaN|NaN`, correct on the first call and wrong on every one after it.

**Strictness is inherited rather than written.** A function inside a `'use strict'` script carries
no directive of its own, so re-parsing its text at the top level of a fresh script makes the copy
sloppy: `undeclaredGlobal = t` **threw a `ReferenceError` before promotion and silently created a
global after it**.

Thirteen probes, each run through a tiered and an untiered context, and **four disagreed** — the
two above plus `arguments.callee` and `f === original`, which are the same identity defect by two
more routes. The nine that agreed are kept as pins, because each is a way the fresh compilation
could have failed to reproduce the original scope and did not: a top-level `const`, a `class`
binding, `this` in a strict function, a default-parameter initializer resolving an outer name.

**Identity is refused; strictness is repaired.** The two halves are not symmetrical and it is worth
saying why. Strictness is something a re-parse *can* reproduce — the wrapper re-states the directive
when the original was strict — so nothing is lost. Identity is not: the copy is a different object,
and no wrapper makes it the same one. `TieringRecompileContract` therefore declines a function whose
body mentions **its own name** or **`arguments`** — the second because `arguments.callee` is the
function object by a route no name check can see, and can be reached through an alias
(`var a = arguments; a.callee`), so the narrow check is the unsound one.

**Asked at the decision point**, for exactly the reason 4-3a records about its own condition 3. The
tiering gate is a conjunction of conditions that exist for unrelated reasons; a property that holds
because of where a call happens to sit is one refactor from being gone.

**What the refusal costs, measured rather than asserted.** Candidates 2 041 → 1 940 and, setting
DeltaBlue aside because it stops dying and so promotes far more (9 → 44), **promotions 195 → 186 —
about 5%**. Cheap, and the 5% were producing whatever the copy produced.

**Recursion by name is refused too, and that is the cost of the rule rather than an oversight.**
`fact(n - 1)` inside the copy calls the copy, which computes the same answer — a self-call is only
wrong when the identity is *observed* rather than invoked. Telling those apart needs a use analysis
the contract deliberately does not do, so the conservative side is taken and pinned by a test that
says so.

**The detector had the bug the item is about, one level down.** `AstReduce` treats three compact
structs — `VariableDeclarator`, `ObjectProperty`, `Case` — as leaves, because most rewriting
visitors handle them explicitly. Inheriting that, the first draft admitted
`function fact(n) { var t = n <= 1 ? 1 : n * fact(n - 1); return t; }` while refusing the same
reference written as an assignment statement: the self-reference was hidden in a declarator's
initializer and the detector never looked. "Did not look" reading as "did not find" is the failure
this whole item exists to close. One test per leaf kind pins it.

**Verify.** `RecompileContractTests`, 19 cases. Every one runs the same source through a tiered and
an untiered context and requires the two to agree — the untiered answer is the specification — with
**a control that is still promoted**, without which a refusal could just be the gate rejecting the
shape for some unrelated reason and every test would pass vacuously. DeltaBlue completes again:
1 of 1 benchmarks, no failures.

#### 4-2b · The specializing emission — **44.7% of executed reads specialized, 18% cheaper each, and that is 0.8% of the suite**

> **In the pin.** Shipped as `patches/0073` while its push was blocked by a 403; since applied
> and pushed, and the pointer bumped — it is commit `34270c76`, an ancestor of `61c8cc65`.

The item's own brief: *"monomorphic property access → shape check plus direct slot read"*. What was
missing was not the codegen — 4-3b built the in-method branch — but a way for the tier-2 compile to
**address** tier-1's feedback.

**The site map, and a defect it closes on the way.** A tier-2 recompile re-parses the source, and
every property read it emits allocates a *fresh* inline-cache site. So promoting a function silently
threw away every warm cache it had **and** there was no way to ask what the original sites had seen,
because their indices were nowhere. Tier-1 now records the half-open range of read sites its body
compile allocated, and tier-2 hands those same indices back out in emission order — which carries
the warm caches across promotion and makes 4-1's per-site feedback addressable.

**The mapping is ordinal, and it is deliberately not trusted.** The site counter is process-wide, so
two threads compiling at once is enough to slide the range. The emitted guard therefore compares the
key the specialization was built for against the key actually being read — **one integer compare** —
so a slipped mapping fails its guard, poisons, and falls back. *The mapping is a performance
heuristic and never a correctness dependency*, which is the only thing that makes an ordinal mapping
acceptable at all.

**What is emitted.** For a site whose whole history is one shape resolving one key to one own slot,
through `SpeculationBuilder.Guarded`:

```
receiver evaluated once
  → key == K && receiver is JSObject && shape.Id == S && slots[N] != null
      ? slots[N]
      : PropertyInlineCacheSite.Get(site, receiver, key)
```

`S` and `N` are literals. The cache's own monomorphic hit ends in the same shape compare and slot
load, but reaches it through a static call taking a `KeyString`, a bounds test, a side-table read, a
megamorphic flag, a receiver type test, a key compare, an entry loop and a holder test — and reads
the shape id and slot *out of a cache entry* rather than having them as constants.

**This is 4-3b's first JavaScript-level consumer**, and it needs the guarantee that facility was
built for: the receiver is evaluated **exactly once**. Hand-rolled, `f().x` would run `f()` twice.
4-3b recorded that it had no consumer and that the reason was structural — a guard needs an
observation, and only a tier-2 recompile has one. That is now discharged.

**What it declines**, each with a test: a prototype-resolved read (a method — no own slot describes
it), an indexed read (an element, which no shape tracks), and a site the feedback classifies as
polymorphic. The last is the half 4-1 exists to answer and the only one a guard cannot recover from
on its own without paying for a speculation that was never going to hold.

##### The addressable surface, counted rather than argued

A read that takes the specialized path never calls `PropertyInlineCacheSite.Get`, so it records no
cache hit. **`cacheHits(tiered) − cacheHits(specializing)` is therefore an exact count of the
executed reads the specialization took off the cache path**, with the two arms differing in nothing
else. Cache *misses* come out identical in six of seven suites and eleven reads apart in Crypto, so
the reads were removed rather than converted:

| Suite | Executed reads | Removed from the cache path | Share | Specialized sites | Guard misses | Poisoned |
|---|--:|--:|--:|--:|--:|--:|
| Richards | 605 672 | 333 048 | **54.99%** | 70 | 0 | 0 |
| DeltaBlue | 1 001 675 | 462 850 | **46.21%** | 94 | 52 | 13 |
| RayTrace | 2 919 249 | 2 119 050 | **72.59%** | 276 | 16 | 4 |
| Box2D | 25 963 010 | 7 417 962 | **28.57%** | 585 | 48 | 6 |
| EarleyBoyer | 5 490 829 | 5 383 113 | **98.04%** | 54 | 0 | 0 |
| Crypto | 1 891 047 | 1 227 467 | **64.91%** | 51 | 40 | 7 |
| NavierStokes | 428 | 0 | — | 0 | 0 | 0 |
| **All seven** | **37 871 908** | **16 943 490** | **44.74%** | **1 130** | **156** | **30** |

Two things worth keeping. **A thousand sites carry nearly half the corpus's reads**, which is the
promoted functions being the hot ones and is the strongest form of 4-1's premise holding. And
**the monomorphism holds through the rest of the run**: 156 guard misses against 16.9 M taken
speculations, 30 poisoned sites of 1 130 — so "only ever saw one shape" was not merely true up to
the promotion point.

##### The throughput, and the control that changes what it means

Three arms, rotated across six rounds, separate processes, driver time only (source loading is
outside the stopwatch): **tiered** (4-2a's engine), **feedback** (recording on, consuming off) and
**specializing**. The middle arm is the one that matters — without it the two arms differ in *two*
things, and a first two-arm run read DeltaBlue's 1.232 as the specialization's cost when it is not.

| | median paired ratio | spread over six rounds |
|---|--:|---|
| feedback ÷ tiered — the cost of **collecting** | **1.0249** | 0.941 – 1.095 |
| specializing ÷ feedback — the effect of **consuming** | **0.9947** | 0.931 – 1.106 |

**Removing 44.7% of the corpus's executed reads from the inline-cache path does not move the wall
clock.** NavierStokes, which specializes nothing at all, comes out at 0.982 on the same probe, which
is the noise floor stating itself.

##### Why not, and it is arithmetic rather than a mystery

Two explanations fit — the specialized path is not actually cheaper, or reads are too small a share
of the time for any change to them to show — and they call for opposite follow-ups. So they were
separated with `--specializing-read-probe`: one promoted function whose body is a monomorphic read
in a loop, so essentially all of the measured time *is* the read path, timed with the specialization
on and off and feedback recording on in both.

| | ns per iteration (median of 6) | spread |
|---|--:|---|
| cached get | **46.83** | 44.52 – 48.59 |
| shape guard + slot load | **37.12** | 35.97 – 41.72 |
| **paired ratio** | **0.818** | 0.778 – 0.879 |

**The specialized read is ~18% cheaper — about 9.7 ns — and every one of six pairs agrees.** The
absolute is a loop *iteration*, not a read alone, so 9.7 ns is the attributable difference and 46.83
is an upper bound on what a read costs. Then:

- **16 943 490 specialized reads × 9.7 ns = 164 ms.**
- The seven suites' driver time is **19 694 ms**.
- **0.83%** — against a suite probe whose noise floor is ±2%.

**The effect is real, measured, and arithmetically invisible at suite level.** That also puts a
number on something the phase had not asked: at 46.83 ns an iteration, the *entire* property-read
path is an upper bound of **~9%** of Octane's execution time here, and at 2-6's ~255 ns a call the
*entire* call path is an upper bound of **~5.5%**. Both are upper bounds because both figures
include their loop's overhead. **So the two paths phase 4 is built around are together at most ~15%
of the time**, which is a lead worth having before 4-4 is started rather than after: an XL that
inlines calls perfectly cannot buy more than that ceiling, and where the other ~85% goes is not
answered by anything in this document.

**Cost when off.** Nothing is emitted and nothing is consulted: `SpecializeFromTypeFeedback`
defaults to `false`, and with it clear a tier-2 recompile emits exactly what 4-2a left behind. The
specialization is gated on the *plan*, not on whether feedback happens to be recording, so the two
are independently controllable — which is what made the three-arm measurement expressible.

**What is not done.**

- **The item's arithmetic half.** *"Arithmetic → raw `double`/`int` where feedback says so"* is not
  built, and the blocker is 4-1's, restated: the numeric-vs-generic signal was deliberately left
  uncollected, because the compiler already proves numeric-ness statically for locals (3-3), so a
  runtime counter has to be defined against *that* to say anything new. Left open rather than
  half-built, for the second time and for the same reason.
- **A poisoned site still pays its guard**, which 4-3b predicted and recorded as owed to this item.
  The right answer is to re-emit the method without the guard once a site poisons; 30 sites of 1 130
  is small enough that it was not worth building before this item had a throughput number, and now
  that it has one, 0.83% is not the place to spend it.
- **Prototype-resolved reads are not specialized.** A method read — which is most of what Richards
  and DeltaBlue do — needs the receiver shape, the receiver's prototype identity, the global
  prototype version and the holder's shape and slot, all four of which the cache already guards.
  That is a strictly larger guard and it is the same set 4-4's inlining needs, so it belongs with
  4-4 rather than here.

### 4-4 · Inlining of small JS callees — **premise measured; the item is re-specified and should not be started as written**

Written the way 4-3 was: the premise first, from the code and a probe, before an XL is started
against it. §4's own ordering makes this the last item, and 4-2b's closing arithmetic already
flagged that its ceiling looked smaller than the phase assumed. Measured directly, it is.

#### The two numbers the item rests on, and one of them was not what the phase had

**How many calls there are.** `--specializing-tier`'s counting pass counts at the invocation rather
than at an instrumented site, which is deliberately **not** 4-1's count: 4-1's call feedback is
gated at *compile* time, which is right for feedback and wrong for a denominator.

**Building the counter is where the first correction came from.** Its tests — written because
4-4's whole conclusion is arithmetic over its output — found two things a plausible-looking counter
had wrong. A call to a **native builtin** reaches the same entry as a call to a JavaScript function
and has an emitted call site, so 4-1 counts it, but it has **no body to inline**; counting the two
together puts every `Math.floor` into 4-4's ceiling. And a builtin running a JavaScript **callback**
does not use that entry at all — `Array.prototype.forEach` and friends call
`JSFunction.InvokeCallback`, which takes *one* `using` scope where the emitted-call entry takes
five, and skips the executing-function and legacy-caller bookkeeping entirely. Merging them prices
a call at the average of two paths that differ by most of their cost. Split three ways:

| Suite | All invocations | Native callee | **JS callee** | 4-1 recorded | From a promoted caller | Share of JS calls |
|---|--:|--:|--:|--:|--:|--:|
| Richards | 121 404 | 2 | 121 402 | 121 404 | 68 954 | 56.8% |
| DeltaBlue | 348 772 | 13 146 | 335 626 | 346 333 | 290 060 | 86.4% |
| RayTrace | 676 718 | 231 697 | 445 021 | 476 934 | 237 168 | 53.3% |
| Box2D | 1 749 666 | 527 164 | 1 222 502 | 1 501 362 | 239 745 | 19.6% |
| EarleyBoyer | 3 042 092 | 1 505 024 | 1 537 068 | 1 537 115 | 1 443 753 | 93.9% |
| Crypto | 255 476 | 15 097 | 240 379 | 255 454 | 217 080 | 90.3% |
| NavierStokes | 630 | 8 | 622 | 630 | 0 | — |
| **All seven** | **6 194 758** | **2 292 138** | **3 902 620** | **4 239 232** | **2 496 760** | **64.0%** |

**Callback invocations are zero on all seven suites** — so the earlier guess that they explained
the gap to 4-1 was wrong, and it is recorded here rather than quietly deleted. **37% of all
invocations are to a native builtin**, which is the number that actually matters: those are calls
4-4 can never address, and any ceiling that includes them is inflated by more than a third.

**4-1's figure sits between the two populations and matches neither** — above the JS-callee count
on Box2D and RayTrace, equal to it on EarleyBoyer, equal to the total on Richards. It is a count of
instrumented sites, which is what it says it is; the gap is **not decomposed here**, and the two
plausible causes (4-1's 65 536-site cap, and call forms its wrapper does not reach) are left named
rather than asserted.

**Where inlining could be emitted.** 4-3b established that a guard needs an observation and a
tier-1 method has none, so inlining only has meaning inside a tier-2 recompile — which makes the
calls with a JavaScript callee made *from* a promoted function the whole surface: **2 496 760, or
64.0% of JavaScript calls and 40.3% of all invocations**. Still an upper bound: the caller comes
from `JSEngine.ExecutingFunction`, so a call made from inside a builtin is attributed to the
JavaScript function that called the builtin.

#### What inlining would save, measured against a hand-inlined control

`--inlining-call-probe`, six rotated repetitions, 20 M iterations, all shapes in one run set
against **one** control — which is what lets the read path and the call path finally be compared
without crossing two probes. Each `-inlined` arm writes the callee's body out by hand with its work
held identical, so it is what a perfect inliner would produce:

| Shape | ns per iteration |
|---|--:|
| `no-call-control` — `s = s + (i + 1)` | **16.98** |
| `plain-inlined` | 17.03 |
| `property-read` — `s = s + o.x` | 64.62 |
| `method-inlined` — `s = s + (i + box.k)` | 87.38 |
| `call-0-args` — `callee()` | 159.06 |
| `call-1-arg` | 174.60 |
| `call-2-args` | 189.49 |
| `call-3-args` | 210.33 |
| `plain-call` — `s = s + callee(i)` | 186.94 |
| `method-call` — `s = s + box.add(i)` | 236.47 |

- **A call costs 142 ns before it carries anything**, plus **17.1 ns per argument**. So ~90% of a
  one-argument call's overhead is *fixed*: `Arguments` and the per-argument boxing are the small
  half, which corrects the natural reading of 2-6's list.
- **Inlining saves 149 ns (method shape) to 170 ns (plain shape)** — the ratio is 0.37× and 0.09×,
  which is the largest per-operation win anywhere in this document.
- A marginal cached property read is **47.6 ns**, against 2-6's ~250 ns call. The call is **three
  times** the read, not thirteen — 2-6's "thirteen times" was against a loop body, not a read, and
  both statements are true of different things.

#### The ceiling, and it is the finding

> **2 496 760 inlinable calls × 149 ns = 372 ms, against a 19 694 ms driver: 1.89%.**
> Inlining every call with a JavaScript callee, which nothing can do, would be **582 ms — 2.95%**.

That is the whole prize, before anything is lost to a callee that is not monomorphic, a callee too
large to inline, a guard that has to be paid on every execution, and the generic path 4-3b requires
to be kept forever. **An XL whose perfect execution is 1.89%** — against 4-2b's 0.83%, so about
twice it, and against the campaign's 163× gap, not the item that closes it.

**And the same probe says where the time actually is.** Reads are 37 871 908 × 47.6 ns = **1 804 ms
(9.16%)**. The call *prologue* is paid by all 6 194 758 invocations — a native callee takes the same
entry as a JavaScript one — so at 142 ns fixed that is **880 ms (4.47%)**. The two paths phases 2
and 4 are built around are together **under 14% of Octane's execution time in this engine**,
measured directly rather than as the pair of upper bounds 4-2b could give. §4's header says phase 4
is "the difference between ~100× and ~10×"; **that is not what these numbers say**, and the sentence
should not survive them unqualified.

The other ~86% has a visible candidate in the same table and it is the *control*: `s = s + (i + 1)`
costs **16.98 ns an iteration** for three JSValue operations and a compare. A loop that touches no
property and calls nothing is already tens of times slower than the engines Octane is scored
against. That is item **3-4**'s territory — a tagged value representation — which this document
currently marks *"scope and cost, do not start"*. **That marking is now the one worth revisiting**,
and it is a phase 3 question rather than a phase 4 one.

#### Is inlining even expressible here? Yes — and the blocker is value, not mechanism

Answered from the code so the successor does not re-derive it. 4-3's spike had to conclude that
V8's deopt model has no counterpart in this engine; this one concludes the opposite, which is worth
being explicit about.

1. **`return` is expressible.** The tree layer has `BExpression.Label`/`Goto`, and a function body
   already compiles against `FastFunctionScope.ReturnLabel`. An inlined body gets its own label and
   its `return` becomes a jump to the end of the inlined block rather than out of the caller.
2. **Scope is expressible — at the *tree* level, and only there.** Splicing the callee's *source
   text* into the caller resolves every free identifier in the caller's scope, which is item 4-2a's
   defect generalized from the function's own name to all of its names. Pushing a real
   `FastFunctionScope` for the inlined body instead gives it its own locals, its own return label
   and its own `this` (the scope already takes a `previousThis`, for arrows), and leaves free names
   resolving as they do for a top-level callee — globals, the same in both. So the condition is
   **the callee must be a top-level function whose free names are global**, which is checkable.
3. **The callee's body is reachable.** 4-1 retains callee identities, and a `JSFunction` carries
   its `SourceSpan`, so the tier-2 compile can parse the callee and inline its AST. 4-1's retained
   callees are currently private to `TypeFeedback`; exposing them is small.
4. **The guard is cheap.** Reference equality against the recorded callee, through 4-3b's
   `Guarded` — one compare, and the receiver is already evaluated once.

**What it costs that is not code, and this is the part to decide first.** An inlined callee has no
frame, so it does not appear in `Error().stack`, and `f.caller` cannot see it. 4-3's spike
established that this engine has nothing to reconstruct a frame *from* — so unlike V8, there is no
mechanism that could restore the missing frame on demand. Keeping the frame preserves the traces
and gives back a share of the cost the item came for; dropping it is an observable semantic change
that no guard can undo. **Neither is wrong, and the item cannot be sized until it is chosen.**

#### Re-specification

**4-4 as written should not be started.** Not because it does not work — it does, and the mechanism
is available — but because its ceiling is 1.89% and two cheaper things address the same or more:

- **4-5 (new, M–L): make the call prologue cheaper.** The measurement says 142 ns of every call is
  fixed, and 2-6 already ruled out the five `using` scopes (removing all of them moved a call loop
  by a single-digit percentage). What is left is `ExecutingFunction`, the legacy-caller check,
  `SelectInvocationDelegate`, the sloppy-mode `this` coercion, the delegate dispatch and the frame.
  **This applies to all 6 194 758 invocations rather than the 2 496 760 inlinable ones — 2.5× the
  calls — needs no speculation, no guard, no tier and no fallback path, and cannot change a stack
  trace.** Halving the fixed cost would be ~2.2%, more than 4-4's *ceiling*, at a fraction of the
  risk.
  **And there is already a shipping proof that most of the prologue is optional.**
  `JSFunction.InvokeCallback` — the entry every native callback site uses — takes one `using` scope
  against five and does none of the executing-function or legacy-caller bookkeeping. Two call paths
  exist in this engine and one is much shorter; **pricing the difference between them is the first
  thing 4-5 should do**, because it converts "the fixed cost could perhaps be reduced" into a
  measured number, and it may also be a semantic question worth asking (the shorter path omits
  `EnterStrictMode`).
- **3-4, re-examined.** The 16.98 ns arithmetic-only loop is the larger number by a wide margin and
  nothing in phase 4 touches it.

**If 4-4 is built anyway**, it splits the way 4-3 and 4-2 did: **4-4a** — decide and pin the
stack-trace question, with tests; **4-4b** — the AST-level inlining under the conditions in (2)
above. Neither is XL on its own. The order matters: 4-4a is a semantics decision that changes what
4-4b is allowed to emit.

**Nothing is landed for this item.** The probe (`--inlining-call-probe`) and the call counting
(`CallPathDiagnostics`, off by default) are, because the successor needs them and because a
measurement nobody can re-run is not evidence.

### 4-5 · The fixed cost of a call — **one real cost found and fixed; the item's own premise is mostly wrong**

4-4's measurement produced this item and told it what to do first: *"it wants an ablation pass of
its own before it is built"*. That pass has now happened, and it falsifies most of what the item
was written to attack — which is the point of doing it before an M–L rather than after.

#### Every piece of the prologue, priced

`--call-prologue-probe`, 200 M iterations, six rotated repetitions, medians, each shape the same
loop with one mechanism added. The framework mechanisms are replicated locally rather than reached
through the engine, because the claim under test is about the mechanism:

| Piece | ns per iteration | over the empty loop |
|---|--:|--:|
| `control-empty-loop` | 0.556 | — |
| plain `static bool` read | 0.309 | — |
| `[ThreadStatic] bool` read | 0.314 | — |
| **`AsyncLocal<bool>` read** | **7.481** | **+6.92** |
| one `using` over a no-op scope | 0.560 | +0.004 |
| **five nested `using`s** | **0.567** | **+0.011** |
| `try`/`catch`/`finally` | 1.282 | +0.73 |
| delegate invoke | 1.235 | +0.68 |

*(The two static reads come out below the control because the JIT compiles `acc += flag ? 1 : 0`
better than the control's `acc += i & 1`. Both are free; the point is that neither is measurable.)*

**Five nested `using` scopes cost 0.011 ns.** The EH regions are free, the dispatch is free, the
ThreadStatic bookkeeping is free. 2-6 said the scopes are not where a call's cost lives and was
right; this says so directly rather than by subtraction, and it disposes of the natural reading of
4-5 in one line.

#### The one real cost, and it was documented as the opposite

`JSEngine`'s own comment about the strict-mode flag says: *"An AsyncLocal SET is expensive though …
**Reads are cheap**, so the scope below only writes on an actual strict/sloppy TRANSITION"* (P0-2).
The set half is right and the write-only-on-transition design follows from it. **The read half was
asserted, never measured, and is wrong by 24×** — and it is the half that runs on every call,
because `StrictModeScope` has to save the previous value before it can decide whether anything
changed.

**Fixed with the pattern the same file already uses.** `JSEngine.Current` keeps an `AsyncLocal` as
the mechanism that carries a value across a suspension and a `[ThreadStatic]` **mirror** that
answers the reads, with the AsyncLocal's change handler keeping them in step. Strict mode now does
the same: the AsyncLocal stays — its comment's reason for existing is correct, an async body
resumes on whatever thread pumps the microtask queue — and reads go to the mirror. **7.0 ns → 0.31
ns, once per call.**

**Verify.** `StrictModeMirrorTests`, 9 cases, and the ones that matter are the suspensions: a
strict async body must still throw on an undeclared assignment *after* its `await`, a sloppy one
must still not, two async bodies of opposite strictness must interleave without leaking into each
other, and a strict generator must stay strict across a `yield`. Those are exactly what a bare
ThreadStatic would get wrong, and they are the reason the AsyncLocal stays. Plus both transition
directions, restoration on return, five-deep nesting, and strict `this`. **Every one of them also
passes on the unmodified engine** — they are a regression guard, not a fit to the change. Repository
suite: **7 839 tests across 13 projects, 0 failures**.

**What it is worth, and it is small.** 7 ns × 6 194 758 invocations = **43 ms of a 19 694 ms
driver, 0.22%** — a fifth of 4-2b's, and below anything this container can resolve directly. The
component measurement is where the evidence is (spread 7.35–7.66 against 0.305–0.337, which is
about as tight as this machine gets); the suite-level arithmetic follows from it.

#### So where is a call's 142 ns? Not anywhere this can see

Everything priced above sums to about **10 ns of the ~142 ns** a zero-argument call costs. The
allocation half is deterministic and says a little more — `GC.GetAllocatedBytesForCurrentThread`
around each shape, exact to the byte:

| Shape | bytes per iteration |
|---|--:|
| arithmetic loop (parameter bound) | 32 |
| cached property read | 64 |
| call, 0 arguments | 64 |
| call, 1 argument | 96 |
| call, 2 arguments | 128 |
| call, 3 arguments | 160 |

**Exactly 32 bytes per argument and 32 for the return** — one boxed number each. That accounts for
the 17.1 ns-per-argument slope 4-4 measured, and for roughly 17 ns of the fixed cost. **It does not
account for the rest.** After the scopes, the EH, the dispatch, the ThreadStatics, the AsyncLocal
and the boxing, **~85% of a call's fixed cost is unexplained by any component that can be priced
from outside the engine.** That is the honest state of this item, and the successor's first move is
a sampling profiler rather than another reading of the code — which this container does not have.

#### The larger thing the control turned out to be hiding

Every probe in this document has used the same control loop —
`function hot(n) { var s = 0; for (var i = 0; i < n; i++) { s = s + (i + 1); } return s; }` — on
the assumption that it is a floor. **It is not.** The same loop with a *literal* bound instead of
the parameter, computing the identical answer:

| | ns per iteration | bytes per iteration |
|---|--:|--:|
| bound is a **parameter** (`i < n`) | **33.77** | **32** |
| bound is a **literal** (`i < 5000000`) | **8.36** | **0** |

**4.0× and 32 bytes an iteration, for the bound alone.** Item 3-3 records the cause and calls it
finished business: *"All four of the item's categories are now at the eligible floor except
`parameter`, which cannot reach the numeric tier at all."* So `i` is a raw double, `n` is a
`JSValue`, and `i < n` boxes `i` on every iteration. Copying the parameter into a local first does
**not** help — the local inherits its unknown type, and it measures identically.

**The allocation difference is the solid half of that claim.** A literal bound also gives the JIT a
constant trip count, so part of the 4.0× could be unrolling rather than unboxing; 32 B → 0 B cannot
be. The boxing is real and priced; the 4.0× is an upper bound on the parameter's own share.

**`for (var i = 0; i < n; i++)` is the single most common shape in the Octane corpus**, and it is
paying a box per iteration. That is a phase 3 item — 3-3's one acknowledged gap, which has never
had a number — and on this evidence it is worth more than anything left in phase 4.

#### Re-specification

- **The prologue work 4-5 was created to do is mostly not there.** What remains of the item is the
  ~85% that nothing here can attribute, and it should not be attempted without a profiler. The
  AsyncLocal fix ships; the rest of the item is **blocked on a tool, not on a design**.
- **New 3-5 (M): give a parameter a numeric local.** 3-3 excluded parameters and said so; the price
  is now measured at a box per iteration on the corpus's commonest loop. It needs the same
  dominance argument 3-3 already built for `var`, `let`/`const` and block-scoped `var`, plus a
  guard for the arguments object's mapping. **This is where the call-path budget should go next**,
  ahead of 4-4 and ahead of what is left of 4-5.

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
> **403** — the submodule remote was outside that session's GitHub scope — so the change shipped
> as `patches/0059` with the pointer unbumped. **It has since been applied and pushed, and is now
> `962ca06a`, an ancestor of the pinned `61c8cc65`.** Every figure below was measured on a local
> build of the then-pinned `2ebc0c3c` **plus** that patch, with the control built from the same
> tree minus it — so they describe the tree the pin now contains.

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

> **In the pin.** Shipped as `patches/0060` for the same 403 as the section above; since applied
> and pushed, and it is now **`6f56d24f`**, an ancestor of the pinned `61c8cc65`. Figures below
> were measured on a local build of the then-pinned `2ebc0c3c` plus it and `0059`.

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
| **0** | 0-1…0-5 ✅, 0-9…0-11 ✅ → 0-6 workflow run ✅ (17/17 committed at the pin) → **0-6's noise band (`--repetitions` in CI), then 0-7, 0-8** | — | Everything. 12 → **17 scores** ✅, known noise band, and the first evidence any phase A–F can close on | 17/17 ✅, no timeout at the 180 s floor ✅, `comparison.md` reporting the triad ✅, **band on record** and **the BenchmarkDotNet + RID-matrix rows collected** — the three still open |
| **1** | 1-2 mitigation ✅ → 1-2 real fix ✅ (all three passes) → **1-4 ✅** → **1-1 emission half ✅** → **1-1's remaining half measured, and the repeated closure rewrite it found is fixed ✅**; the capture mechanism itself is still open → 1-3 measure | 1-4 S, 1-1 remainder L | The two worst scores in the suite; page-load time generally. **1-4 took the Mandreel half (3.04×); 1-1's deferred emission takes 0.64–0.69× off jQuery, PdfJS and Box2D at 1.0009× steady state, and CodeLoad 94.6 → 104.0 (1.099×)**. **The remaining half is now sized rather than inferred**: parse 9.4–13.5% / tree construction 33.6–63.9% / emission 25–57% on the real corpora, over a population that is **84–99.7% never invoked**. What blocks it is not a pre-parser and not `EmitConstant` — the `Box[]` a creation site passes *is* the capture mechanism — but that its indices are decided by `LambdaRewriter` from a tree the deferred body does not have; the eager free-name map that would make it addressable measures at 5.4–9.9% of tree construction. **The repeated closure rewrite the measurement found is fixed and is worth 0.782× on jQuery's whole compile and 0.867× on Typescript's, six of six pairs each** | test262 over the four pinned manifests, no new failure **and no new timeout**; MandreelLatency and CodeLoad out of the tail |
| **2** | 2-0 ✅ → 2-1 ✅ → 2-2 ✅ → 2-4 ✅ → 2-7 ✅ → 2-8 ✅ → **2-9 ✅** (2-3's successor, L); 2-5 and **2-3 closed on measurements**, 2-6 folded into 4-1. **Every item is landed or closed** | M each, 2-9 L | The Richards/DeltaBlue/Box2D cluster | An ownership entry and owned tests **per item**; test262 properties/strict-mode **satisfied** — unchanged at `a6f101cc` plus 2-9; **DeltaBlue and Richards inside 200×** — **measured twice, agreeing: Richards PASSES (183× → 150× after 2-11/2-12 locally; 144.9× in CI), DeltaBlue FAILS (576× → 447× locally; 460× in CI)**, five repetitions per engine on one machine and the committed CI run on another |
| **3** | 3-0 ✅ → **3-3 ✅** → 3-5 ✅ → 3-6 ✅ → 3-7 ✅ → 3-8 ✅ (counted, do not start as written) → **3-1 (85% of the corpus's boxes) → 3-2 (Box2D's 11.6 M), sharing one compiler half, and nothing else until they land** → then *cost* 3-4 | L–XL, 3-8 XL | Uniform lift across arithmetic and allocation-heavy suites. **3-7 closes the static half of the coverage question and 3-8 is what is left**: the widening reached 8 names of 2 920 (224 → 232), because 247 of 3-6's 478 captured names are held by a *hoisting* rule that is correctness rather than policy, and 2 439 are not proven numeric. **3-8 then measured the two numbers this phase never had, and they re-order it.** Number boxing is **41.89% of the corpus's allocation** (2.05 GB of 4.88, and 66.96% of NavierStokes) — so the prize was always large — while the **whole** raw-double local tier, every item from P2-2 onward, removes **0.36% of those boxes**. A box is minted by the operator, not by the local, and 76.4% of the names 3-8 would guard take their value from a property read or a call. **3-1 and 3-2 move to the front**: they unbox the sites that mint the boxes, and they have been ranked behind the locals work since the phase opened on no measurement at all. **Started, and the first count moves the item off storage.** What the generic arithmetic operators are handed at run time had never been measured: **73 817 515 of 73 818 646 invocations arrive with both operands already Numbers — every one but 1 131 — and that population is 86.6% of every box the corpus allocates**, while the compiler's own `both are native` proof reaches **0.75%** of the same invocations. *Compile-time provability reaches 0.75% of the arithmetic; run-time truth reaches 100.00%.* So the shared half is a **run-time-guarded specialization of an arithmetic expression tree** — box only the root — and not the typed backing store the item is written around; a typed store returns to being the live-memory item it always measured as. It also partly reverses 3-8's "do not start as written" without contradicting it: 3-8 priced the guard at the **local** (0.36%), this counts it at the **operator** (86.6%). **The shared half is then built** — evaluate each leaf once, test for Number, compute on raw doubles, box only the root — and removes **10 401 782 boxes of 85 249 783, 12.2% of the corpus's allocation, from 862 sites**, against **0.36% for every previous phase-3 item combined**. Short of the 86.6% ceiling for a reason the per-suite column gives rather than hides: NavierStokes loses 10.1% of its generic invocations and 1.8% of its boxes, EarleyBoyer 99.7% and none, so **most of those two suites' boxes are minted somewhere that is not a binary arithmetic operator** — the next count. Wall clock then measured: **driver 0.981× on six of six ABBA pairs, Crypto 0.912× on six of six**, against controls at 1.005× and 1.006× — so **12.2% of the allocation buys 1.9% of the time**, and no suite is slower. **3-1's own re-measurement then made that stronger.** The element chain decomposes exactly — 0.00 for a raw double, 31.98 for `s = s + a[0]`, 95.99 with a multiply, 159.67 for a read-modify-write — and the element STORE is in none of it: the boxes are minted by the operators, and the read is free today only because what it hands back is already a box. Two things fell out. A numeric literal is **re-boxed on every evaluation** (`a[0] * 1.5` costs two boxes where `a[0] * 2` costs one), measured at **1.2% of requests** and recorded rather than built. And the **bitwise and shift operators had no native form** although the analysis has always typed them — `s = i + 1023` costs 0.00 B/iter and `s = i & 1023` cost 31.84. That half **is built** (`JSNumericOperators`, all six through `ToUint32`, 15 tests on both arms) and takes its shape to **0.00** — **and removes no boxes at all on the corpus**: six suites identical to the digit, and Crypto (42.4 M boxes) differing by less than its own run-to-run variation, measured by running one arm twice. The native form needs both operands native and Crypto's digits live in `this.array[i]`. *Six items have now built machinery array-resident data cannot reach; every one is correct, every one is invisible, and every one is waiting on 3-1*. **3-2 was then measured too, and its one-sentence premise is wrong**: `o.x = 2` allocates **nothing** — a slot store is a reference copy — so `vector.x = 1.5` pays for the **literal**, not the slot, and the slot's own cost shows up only in `o.x = v * 1.5` where the value is a raw double (32 B, the same 32 B for the eleventh time). The field rows match the element rows **to the hundredth** — 31.98 and 96.00 both — so 3-1 and 3-2 are one mechanism with two backends. And 4-1's uncollected "numeric-vs-generic" signal, built at last, splits them exactly: **50.1% of all cache-answered reads hand back a number**, but **98% of those are Box2D's**, while **NavierStokes performs 388 property reads, zero numeric, and mints 29 977 471 boxes**. So **3-1 carries 85% of the corpus's boxes and 3-2 carries Box2D's**, and no work on shape slots reaches the other two suites. **The next count then named every box the corpus mints, and moved the item again.** The compiler's boxing conversion — the only thing a typed store could remove without further operator work — is **5.0% of NavierStokes' requests against 31.0% of Crypto's**, i.e. the two suites are the opposite way round from this item's premise. Chasing the **40.5%** that first pass left unattributed down to **1.0%** found the answer in the operators no census had counted: **`++` and `--` are 30.9% of the corpus's boxing, 51.6% of NavierStokes' and 80.4% of EarleyBoyer's**, and **half of that is `ToNumeric` re-boxing a value that is already a Number** — 17 281 232 requests, 15.4% of all boxing, removable by a guard. **Built, in nine lines**: 17 285 913 requests removed against that prediction (0.03%), **7 050 834 real allocations, 9.4%**, NavierStokes **23.0% of its boxes and 0.906× of its time on six of six pairs**, and the corpus **0.795×** with `0084`. **What did not move is the finding**: EarleyBoyer halved its boxes for 1.002×, because 82 000 a second is not 4 240 000 a second — *a share of a suite's own allocation forecasts nothing, the absolute rate forecasts everything*. **Then the refusal waterfall, which is the count `0084` never took and the largest result the phase has had.** Of 5 396 candidate arithmetic nodes only **862 specialize**; `OrderUnsafe` refuses 1 762 and `NoSavingToMake` 2 718, and those are **one** finding — a left-leaning `a[0]+a[1]+a[2]+a[3]` refuses at the root for order, again at each left child, and its bottom node is then a lone operator with nothing to save. The sub-census names the blocking leaf: **1 028 property reads against 34 element reads**, so the rule this phase assumed was an array problem is an **object-field** one, 984 of them Box2D's. **The fix is that nothing required the leaves to move**: emit each at its own postorder position and put the test where the coercion would have run, and the purity rule has nothing left to protect. **53 353 957 → 6 626 052 generic invocations and 67 795 858 → 31 162 330 boxes — 36 633 528 removed, 54.0% of everything the corpus allocates**, `OrderUnsafe` 1 762 → 0 and `NoSavingToMake` 2 718 → 1 181 untouched. From the pre-`0084` baseline the corpus is **0.366×**. **Driver 0.969× on six of six ABBA pairs, NavierStokes 0.834× and Crypto 0.893× both six of six**, two zero-box controls at 1.002× and 0.999×; Box2D cuts 51% of its own boxes for 1.003× because 861 000/s is not 6 500 000/s, which is `0086`'s lesson holding a second time. *54.0% of the allocation buys 3.1% of the time* — with `0084`'s 12.2% → 1.9%, the third reading of the constant that should size the rest of the phase  **And then the denominator the phase never had**: collection is **1.8–2.0% of the driver**, and of the 768 ms the order-preserving emission removed only **54 ms was collection** — the other 714 ms is the mutator's own allocation work. *A box costs ~14× more to create than to collect*, which makes §Non-goals' "the collector is not the problem" a measurement. At **711 ms per GB** the **0.70 GB of number boxes left is worth ~2.6% of the driver**, so everything remaining here is an XL bidding for under 2% — count the `++`/`--` step's operands before building the typed store, and bid with a rate rather than a share. A sampling profiler was tried and does not decompose this engine: it inflates the driver ~29%, its biggest frame is its own rendezvous point, and compiled JavaScript does not symbolicate  **And the `++`/`--` count is taken**: of 17 282 144 steps, **Element 0, Property 0.3%, LocalSlot 98.1%, Other 0** — the step shares no mechanism with a typed store and belongs to the numeric local. ≈**7.05 M real boxes, 22.6% of what the corpus still allocates**, 6.76 M of it NavierStokes', where one untypable closure variable (`rowSize`) cascades into every `++currentRow`. **Re-opens 3-8**, which priced the guard at the local and measured the tier's *yield* (0.36%); this measures what it *lets through*  **Then scoped**: eight shapes, one per conjunct, rule three suspects out — a nested function declaration is innocent, 3-7's hoisting rule produces a `LocalCell` (NavierStokes: 9 461 760 slots against six cells), and passing the value in only trades `OtherName` for `Parameter`. **One conjunct is left — the analysis will not type a name from outside the function, even one already proven numeric.** Splits into **3-9** (static, import the enclosing scope's conclusion; does *not* reach NavierStokes, whose root is held by 3-7's correctness rule; count its population first) and **3-8a** (run-time, one `IsNumber` test where the value enters) — scoped at **≈115 ms, 0.6% of the driver, an M rather than an XL**. **3-8a was then built complete and closed as a measured regression.** Its population is 26 names, 15 in NavierStokes; the dual representation and all three consumers that can take a raw double are built (the guarded tree's leaf, the element read, the element write), and each moved the number without moving it enough — 1.021×, 1.017×, 1.012×. A counter added **at the read** then settled it: **NavierStokes mints 393 705 boxes reading a speculative local against ≈5 300 removed**, because the 835 584 steps it takes off `Increment` are mostly `x[++i]`, whose result is boxed to be an index either way. *Every premise the item was scoped on survived and the item still lost* — what makes it lose is the read/write ratio of the code it targets, a property of the workload rather than of how many consumers the compiler grows. **Off by default and staying off; §3.5 gains the rule that a representation change is priced by that ratio, counted before the representation is built.** **3-9, the static half of the same split, is closed at a population of ZERO** — 0 names and 0 outer-numeric offers on all seven suites, against 3-8a's 26 from the same call site in the same run — because 3-9 can only import from a name that is both proven numeric and still a raw double despite being captured, which is item 3-7's eight, and none of the eight is read from an assignment inside the function that captures it. *Counted with an instrument proven to discriminate on nine constructed shapes first, and closed for one instrument and no mechanism* | `test262-arrays`, `test262-binary-data`, and — added by 3-3's `let`/`const` half — `test262-lexical-declarations`; allocation reported per item alongside time |
| **4** | 4-3 design ✅ → **4-1 ✅** (shapes and callees; numeric-vs-generic still open per site — item 3-2 collected the aggregate read share, 50.1%, for a phase 3 ranking) → **4-3a ✅** → **4-3b ✅** → **4-2a ✅** → **4-2b ✅** (arithmetic half left open) → 4-4 | XL | The remaining order of magnitude. **4-1 measured the premise: 93.5% of reads and 96.7% of calls are monomorphic by execution weight, so 4-2 and 4-4 are well-founded.** 4-3a stated and enforced the restart contract — and found its no-suspendable-bodies condition was held only by two unrelated accidents, two ordinary refactors away from an async function returning a number instead of a Promise. **4-2 then split the same way**: measuring the branch it was told to replace found it produced *wrong answers* — DeltaBlue died on the shipping tier-2 hook — which 4-2a fixes, and 4-2b's specialization takes **44.7% of the corpus's executed reads off the cache path at 0.818× each**, which is **0.83% of suite time**. That number is the phase's own warning: the whole read path is ≤ ~9% of Octane's execution time here and the whole call path ≤ ~5.5%, so **4-4's ceiling is smaller than the phase assumed** | Deopt correctness proven **before** any speculation ships; full test262 matrix |
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
  **This is now measured rather than asserted (item 3-1).** `GC.GetTotalPauseDuration()` puts
  collection at **1.8–2.0% of the driver**, and of the 768 ms an allocation change removed, **54 ms
  was collection and 714 ms was the mutator** — the pointer bump, the zeroing, the write barriers
  and the cache traffic of touching a gigabyte of fresh memory. *A box costs about fourteen times
  more to create than to collect on this corpus.* Aiming at the collector would have been aiming at
  a fourteenth of the problem, which is what this bullet always claimed and could not previously
  show.
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

# The same three-way split as --compile-scaling, but on the REAL corpora and against the
# body-free control, plus the closure rewrite as its own column (sizes 1-1's remaining half).
# One corpus per process, for --compile-profile's reason. Third argument is repetitions.
dotnet $DLL --compile-phases /path/to/octane 5 codeload-jquery

# How many of a script's functions are ever invoked once it has been evaluated — the
# population 1-1's remaining half is worth. Evaluating and stopping is CodeLoad's own shape.
dotnet $DLL --defer-population /path/to/octane codeload-jquery
```

`--compile-profile` builds its control by replacing every outermost function body with `{}`
and **re-parses it before timing anything** — a control the parser rejects would measure
failing early rather than compiling less. Set `BROILER_COMPILE_PROFILE_DUMP=<dir>` to write
each control out; that is how Mandreel's residue was read.

**`--compile-profile`'s control is not a ceiling for every corpus, and jQuery is the one it is
wrong about.** It stubs every *outermost* function body, and jQuery has exactly one — the IIFE
the whole library is written inside, which `bodyByteShare` reports as **99.91% of the source**.
So its control is an empty file, `full − stub` is the whole compile, and the 96.5% "ceiling" in
1-1's table is *everything except the parse* rather than anything a deferral can take: CodeLoad
evaluates jQuery, which runs that IIFE. `--defer-population` is the instrument that answers the
question the ceiling was being asked, because it counts what is never invoked instead of what is
inside a body.

**`--compile-phases` takes its end-to-end check first, and that ordering is the measurement.**
Every compile in the probe registers a deferred site per relayed lambda, each rooted by a
`GCHandle` that is never freed and each holding its subtree, so a phase timed late in the
sequence pays collection time the phases before it caused. Taken last, the end-to-end column read
**3.4× the sum of the phases on Box2D and 1.0× on jQuery** — and the difference between those two
corpora is exactly how many sites a *deferred* compile registers: **982 against 1**, because
jQuery's top level relays one IIFE and Box2D's relays every one of its top-level functions. That
is item 1-1's own retained-tree artifact one level down from where this document records it.

Both phase-1 changes carry a switch so they can be A/B'd on a single build, which is the only
way to compare two compilers without also comparing two builds:

```bash
# Item 1-4: scope size above which the closure rewrite indexes instead of scanning
# (default 32). Any value larger than a real scope restores the pre-1-4 linear scan.
BROILER_JS_REWRITER_INDEX_THRESHOLD=1000000000 dotnet $DLL --compile-profile /path/to/octane 1

# Item 1-1: 0 restores eager IL generation. Default is on.
BROILER_JS_DEFER_IL=0 dotnet $DLL --compile-profile /path/to/octane 1

# Item 1-1's remaining half: 0 restores the relay-time closure rewrite of a subtree an
# enclosing walk has already rewritten. Default is on. --defer-population reports the three
# counters behind it: relaysRewritten (0 on every corpus), relaysSkipped, and — only on the
# arm below, which is the one that still runs the repeat — capturesInRepeat, the captures the
# repeat creates that the first walk had not. Also 0 on every corpus, and it is the counter
# that separates "the walk repeats" from "the walk is inert".
BROILER_JS_RELAY_REWRITE_ONCE=0 dotnet $DLL --defer-population /path/to/octane codeload-jquery
BROILER_JS_RELAY_REWRITE_ONCE=0 dotnet $DLL --compile-profile /path/to/octane 1 codeload-jquery

# Item 3-7: 0 restores the JSVariable cell for a numeric local a closure names.
# Default is on. The soundness conjunct it does NOT gate — a name a hoisted function
# declaration mentions — holds on both settings, so the two arms differ only in policy.
BROILER_JS_CAPTURED_NUMERIC_LOCALS=0 dotnet $DLL --local-alloc
BROILER_JS_CAPTURED_NUMERIC_LOCALS=0 dotnet $DLL --specializing-tier /path/to/octane baseline counters

# Item 3-8: 0 removes the numeric-local tier ENTIRELY — every raw double local, not one
# item's increment to it. This is the control four phase-3 items were each measured
# without, and the arm that says the whole mechanism is worth 0.36% of the engine's
# number boxing. Default is on.
BROILER_JS_NUMERIC_LOCALS=0 dotnet $DLL --specializing-tier /path/to/octane baseline counters

# Item 3-1: 0 restores the generic JSValue operators for `&`, `|`, `^`, `<<`, `>>`, `>>>`
# on two proven-numeric operands. Default is on. Worth a full box on its shape and
# exactly nothing on the corpus, because the operands there are array elements.
BROILER_JS_NATIVE_BITWISE=0 dotnet $DLL --local-alloc

# Item 3-1's shared half: 0 restores the unguarded emission for an arithmetic tree over
# operands the compiler cannot prove numeric. Default is on.
BROILER_JS_NUMERIC_SPECULATION=0 dotnet $DLL --specializing-tier /path/to/octane baseline counters

# Item 3-1's order-preserving half: 0 restores the HOISTING form of the guarded tree —
# every leaf evaluated into a temporary ahead of one combined test, and the purity rule
# that needs. Default is on. This is the arm to compare against, not the one above:
# BROILER_JS_NUMERIC_SPECULATION=0 turns the whole guarded tree off and would charge this
# change for what 0084 already did.
BROILER_JS_NUMERIC_TREE_ORDER=0 dotnet $DLL --specializing-tier /path/to/octane baseline counters

# Item 3-8a: 1 turns ON the dual-representation speculative numeric local — a raw double, a
# flag, and the JSValue slot, with the ++/-- step, the guarded tree's leaf, the element read
# and the element write all able to take the raw half. Default is OFF, and it stays off: the
# arm is a measured 1.2% regression on the corpus's boxing, and boxingSpeculativeReadRequests
# says why in one number. Kept switchable because the mechanism is correct and tested on both
# settings, so a future workload with a different read/write ratio can be measured on it
# without rebuilding it.
BROILER_JS_SPECULATIVE_NUMERIC_LOCALS=1 dotnet $DLL --specializing-tier /path/to/octane baseline counters
```

**`--specializing-tier … counters` also reports item 3-9's population, and the counter that makes
its zero readable.** `importedOuterNumericCandidates` is how many locals would be numeric if an
identifier resolving to a numeric local of an ENCLOSING function were typed rather than classified
`OtherName` — computed by difference against the real fixed point, like 3-8a's, so it cannot drift
from what the analysis does. It reads **0 on all seven suites**, and a single zero cannot separate
"nested functions never read an enclosing numeric local" from "they read them constantly and never
anywhere typable", so `importedOuterNumericOffers` counts **how often the enclosing scope chain
answers that a name is already a raw `double`** while the pass runs. That is **0 too**: the reads do
not exist. Both are COMPILE-time counters on the same terms as
`speculativeNumericCandidates` — the switch (`BROILER_JS_OUTER_NUMERIC_COUNT=1`) has to be on before
the corpus is compiled — and **`importedOuterNumericCandidates` is bounded above by
`speculativeNumericCandidates` by construction**, since everything an enclosing scope has *proved*
numeric is also something 3-8a's pass would have *assumed* numeric. A reading above 26 on this
corpus is a defect in the counter rather than a discovery, and every fixture asserts the bound.

**`--specializing-tier` reports GC pause per suite on *both* modes, `counters` and `timing`**
(item 3-1): `gcPauseMs` is `GC.GetTotalPauseDuration()` across the driver run — the runtime's own
accounting of time with execution suspended, exact rather than sampled — with `gen0Collections`,
`gen1Collections` and `gen2Collections` beside it, because pause time alone cannot separate "many
cheap gen0s" from "a few expensive gen2s" and those want opposite follow-ups. **Read it against
`elapsedMs` before pricing any allocation item**: it comes out at **1.8–2.0%**, so the collector is
not what an allocation change buys back, and read the *difference* between two arms against the
difference in `elapsedMs` — 54 ms of 768 ms — to see that the rest is the mutator. It costs four
`GC` reads per suite and is on unconditionally, since it is not on any hot path.

**A sampling profiler is not a substitute for it and was checked.** `dotnet-trace collect --format
speedscope --providers Microsoft-DotNETCore-SampleProfiler -- dotnet $DLL --specializing-tier …`
runs and converts cleanly, and then says almost nothing: the driver inflates from ~19.5 s to
**25.4 s**, **28.0%** of self time lands in `Thread.PollGCWorker` (the rendezvous its own stack
walks force, *not* collection — the counter above says collection is 1.8%), and compiled JavaScript
lives in `DynamicMethod`s the stack walker cannot name, so **47.8%** of the run is
`JSFunction.InvokeFunction` with an anonymous JavaScript frame beneath it against **2.4%** on a
named body. Item 4-5's "blocked on a profiler" needs a tool that can symbolicate a `DynamicMethod`.

**`--specializing-tier … counters` also reports item 3-8a's population**: `speculativeNumericCandidates`
is how many locals the analysis would prove numeric if a name the function neither declares nor
takes as a parameter were known to hold a number — computed by difference against the real fixed
point rather than by a new rule, so it cannot drift from what the analysis does. **It is a
COMPILE-time counter and its switch (`SpeculativeNumericLocals.Counting`,
`BROILER_JS_SPECULATIVE_NUMERIC_COUNT=1`) has to be on before the code being measured is compiled**
— set beside the run-time censuses it reports zero, which is how the first version of it was nearly
published. Read it against `numericLocals`: the corpus total is **232 → 258, 1.11×**, and the row that
matters is NavierStokes at **24 → 39, 1.62×**. Off by default because it costs a second analysis
pass per compiled function.

**`--specializing-tier … counters` also reports what item 3-8a's dual representation COSTS**:
`boxingSpeculativeReadRequests` counts boxes minted reading a speculative local, attributed at the
read by a fourth `JSNumber` factory entry (`CreateSpeculativeRead`) beside `CreateLiteral` and
`CreateConversion`. **This is the counter that decides the item, and it is the one that was built
last** — three consumers were converted first, each a guess at where the remaining boxes were,
checked only by whether the total moved. Read it against the fall in `arithmeticUpdateTargets`'
`LocalSlot` row, which is what the representation buys: the item pays exactly while the second
exceeds the first, and on NavierStokes it is **393 705 against ≈5 300**. A run-time counter, so
unlike `speculativeNumericCandidates` it needs no switch of its own beyond
`NumberBoxingDiagnostics.Enabled`.

**`--specializing-tier … counters` also reports where each `++`/`--` step's operand lives**
(item 3-1): `arithmeticUpdateTargets` splits `arithmeticUnaryUpdate` into `Element` (a computed
member), `Property` (a named one), `LocalCell` (a `JSVariable` cell — which is what a *top-level*
`var` is), `LocalSlot` (a statically-resolved local or parameter the numeric analysis did not prove
numeric), `GlobalOrWith` and `Other`. The kind is a compile-time constant carried into the step, so
the run-time cost is the `Enabled` test the step already paid. **The rows sum to
`arithmeticUnaryUpdate` by construction** — the total is recorded by `Increment` itself and the
rows by the overload the compiler calls — so an emission site the census forgot appears as a
shortfall rather than vanishing, and `Other` at a non-zero value is a signal to go back. **Read
them as requests and multiply by the suite's own request-to-allocation ratio before calling them
memory**: Crypto's 7.2 M steps are 0.1% real (the small-integer cache answers its counters) and
NavierStokes' 9.46 M are 71.4%. **A numeric local appears in no row at all**, which is the point
rather than a gap — `i++` on a raw double is a native add that never reaches `Increment` — and it
is what makes 98.1% in `LocalSlot` a statement about the tier's *coverage* rather than about the
operator.

**`--specializing-tier … counters` also reports the numeric-tree refusal waterfall** (item 3-1):
`numericTreeRefusals` attributes every candidate arithmetic node to the **first** eligibility
condition it fails, on the same terms as `numericRejections` — so the counts add up and each row
reads as "widen this and that many sites move". Only a binary node whose operator has a native form
is a candidate; counting anything else would put every `===` and `&&` in the denominator. **Read it
knowing that a refused root re-offers its children**, so a refused chain contributes several rows
and the totals are of candidate *nodes*, not of source expressions — which is the right denominator
here, since the question is how much arithmetic reaches the guarded form. `numericTreeOrderBlockers`
reads against the `OrderUnsafe` row alone, which is its total, and names the kind of leaf that
blocked it: **1 028 property reads against 34 element reads** is what said the order rule is not an
array problem. Both are compile-time counters touched once per site, so they are unconditional and
have no `Enabled` flag.

**`--specializing-tier … counters` reports `cacheHitsNumeric`** (item 3-2): of the property reads
the inline cache answers, how many hand back a number. This is item 4-1's third signal —
"numeric-vs-generic per site" — which 4-1 left uncollected and 3-8 named as the missing instrument.
It costs one `IsNumber` test on the two hit returns and only while
`PropertyOptimizationDiagnostics.Enabled`. **Read it per suite:** the corpus total is 50.1%, and
that single figure conceals Box2D at 54.0% of 18.2 M reads against NavierStokes at 0% of 388.

**`--specializing-tier … counters` also reports the arithmetic-operand census** (item 3-1):
`arithmeticGeneric` is every invocation of a generic two-`JSValue` arithmetic or bitwise operator,
`arithmeticBothNumbers` the subset whose operands were already Numbers before any coercion — i.e.
what a native form guarded on that test could answer — and `arithmeticRawDouble` the shape item 3-5
specialized for `<` and `>`, one side an unboxed double and the other a `JSValue`. Read the second
against `boxesAllocated`, not against the first: 100.00% of the invocations is what says the guard
predicts, and **86.6% of the boxes** is what says the guard is worth building. `arithmeticRawDouble`
counts `+` alone, because it is the only operator with a `JSValue × double` overload — the other
four re-box a raw double to call the generic form. Counters are off by default
(`ArithmeticOperandDiagnostics.Enabled`); the emitter turns them on around the driver run only.

**The census covers the unary operators too, and `arithmeticGeneric` alone will under-report.**
`arithmeticUnaryNegate` is `-x` and `~x`, `arithmeticUnaryUpdate` the `++`/`--` step, and
`arithmeticUnaryToNumeric` the coercion of a `++`/`--` operand — which mints unconditionally, so
the last two are equal on any run whose updates are all on Numbers and **`++` is two boxes, not
one**. They are 30.9% of the corpus's boxing against the binary operators' 47.6%, so a reading that
takes `arithmeticGeneric` for "the operators" is short by two fifths. **The attribution only closes
when every source is subtracted**: `boxingRequests` minus `boxingConversionRequests`,
`boxingLiteralRequests`, `arithmeticGeneric` and the three unary columns leaves 1.0%, which is
builtins reaching the factory directly. Anything larger than that means a hook is missing, which is
how `BitwiseXor` — unhooked, and silent about it — was found.

**`--specializing-tier … counters` also reports the boxing census** (item 3-8):
`boxingRequests` is every call to `JSNumber.Create`, `boxesCached` the share the small-integer
table answers without allocating, and `boxesAllocated` the rest — the last times 24 B is the
ceiling on every raw-double item in phase 3 at once. `boxingLiteralRequests` and
`boxingConversionRequests` split off two named callers through separate factory entries
(`CreateLiteral`, `CreateConversion`) rather than a stack walk: the first is a numeric literal
re-boxed to meet an operator, the second is the compiler carrying a raw double across into a
`JSValue` — **the ceiling on what a typed backing store can remove**, and 5.0% of NavierStokes'
requests against 31.0% of Crypto's. `NumberBoxingDiagnostics.Enabled` is off by
default and the emitter turns it on around the driver run only. **Read it per suite, never only as
a total**: the share runs from 0.31% on DeltaBlue to 66.96% on NavierStokes, and the average of the
seven hides both ends.

**Item 3-7's timing arms need one shape per process.** The winning arm removes two boxes per
iteration, so over 3 M iterations the *off* arm allocates ~192 MB more; run in one process its
collections are charged to whichever function runs next, and the control — identical code on both
arms — reads 1.2857× instead of ~1.000. Generate one file per shape, rotate
`off/on/on/off`, and read the control first: a control outside the noise band invalidates the run.

**Give `--compile-profile` a corpus name as its fourth argument and run one per process.**
The corpora share a heap and item 1-1 keeps an un-generated lambda's tree alive, so a corpus
measured after Mandreel's 5 MB pays collection time that has nothing to do with its own
compile. Measured together, 1-1 read **1.6× and 2.6× slower** on the last two corpora and
0.56–0.65× faster on the first three, with bimodal ratios; measured one per process it is
0.64–0.83× on five of six. *That artifact cost a full A/B run to find, and the tell was the
bimodality, not the sign.*

```bash
BROILER_JS_DEFER_IL=1 dotnet $DLL --compile-profile /path/to/octane 1 codeload-jquery
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
| 1-1 | *excluded by engine §9* | 1-1 | **Emission half landed; capture half open.** Deferring IL generation to first invocation makes all four of the item's named risks vacuous — they are front-end properties and the front end still runs eagerly. jQuery **0.661×**, Box2D 0.636×, PdfJS 0.689×, allocation ~0.52× throughout, steady state **1.0009×**, and **Octane CodeLoad 94.6 → 104.0 (1.099×, 24 samples an arm, 93% pairwise dominance)** — the benchmark the item names, run and passed, though 1.099× is far short of the "large multiple" the item predicts because compilation is only ~27% of what CodeLoad measures. Typescript 1.034× and unexplained. Shipped as `patches/0066` while its push was blocked by a 403; **since applied and pushed — it is commit `9bf9639b`, an ancestor of the pin**. What remains is deferring the parse and tree construction, which needs the capture mechanism |
| 1-3 | *excluded by engine §9* | 1-3 | Open, and **re-aimed**: the synthetic split (parse 0.5% / tree 11% / emission 89%) does not hold on real source, where deferring *all* nested-body emission removes only 17–36%. 1-3 is a front-end item, and its first task is that split on the corpora |
| 1-4 | — (found measuring 1-1's premise) | — | **Landed** — the closure rewrite's per-lambda scope was a `List` asked `Contains` per parameter reference, so IL emission was **quadratic in a scope's binding count**. A reference-keyed multiset, list-backed below 32 bindings: **28.5×** on 2 000 top-level declarations, **3.04× on Mandreel** end-to-end (ABBA, six pairs), inside noise on the narrow-scope corpora. Shipped as `patches/0065` while its push was blocked by a 403; **since applied and pushed — it is commit `1070525a`, an ancestor of the pin** |
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
| 3-1 | — | 3-1 | **Open, re-specified three times, now FIRST and no longer contained.** Its own measurement made it a live-memory item; 3-8's census overturned that ranking (**42.01% of the corpus's allocation is number boxes, 66.96% of NavierStokes**); and its own re-measurement showed the element chain decomposes entirely into **operator** boxes, so a typed store *alone* stays the wash it always was and the item that pays is storage **plus an unboxed element read the numeric operators can consume** — a joint Storage + Compiler **XL**. It is the precondition for every item phase 3 has landed. Its premise measurement also **built the bitwise half** (`JSNumericOperators`) and found a literal is re-boxed per evaluation (1.2% of requests, not built) |
| 3-2 | — | 3-2 | **Open, measured, and re-specified: it is a Box2D item and it goes AFTER 3-1.** Its premise sentence is wrong — `o.x = 2` allocates nothing, so `vector.x = 1.5` pays for the literal and not the slot; the slot's own 32 B appears only when the stored value is already a raw double. Field rows equal element rows to the hundredth, so 3-1 and 3-2 share one compiler half. Sized with 4-1's uncollected numeric-vs-generic signal, built here: **50.1% of cache-answered reads are numeric, 98% of them Box2D's**, against NavierStokes' **388 reads / 0 numeric / 30.0 M boxes**. **4-2b's specialized read already resolves a monomorphic read to a literal slot index**, which is most of the machinery a raw slot needs |
| 3-3 | P2-2 item 3 remainder | 3-3 | **Parameters landed; `let`/`const` and block `var` open and re-ranked ahead of them.** Measured before starting, and the item was right about the target and wrong about the tier: a parameter was excluded from the *scalar* gate, not the numeric one, so it allocated a `JSVariable` cell on every call — **56 B per parameter, a three-parameter call 230.2 → 62.2 B**. The numeric tier cannot be widened to parameters at all, because the caller picks the type; that is phase 4. All four ineligible categories cost the same per site, so the item's ordering was never a cost claim |
| 3-6 | — (found measuring 3-5) | — | **Counted and closed** — the conjunction 3-5 blamed costs 0.1% of the coverage. Splits into 3-7 and 3-8; nothing built, deliberately |
| 3-7 | — (3-6's static half) | — | **Landed** — a captured numeric local lives in the `Box<double>` the expression compiler already makes for any captured CLR local, so the "cell" the item asked for needed no code. Worth **8 names, 224 → 232**, not 3-6's predicted 290/2.4×: **247 of the 478 captured names are named by a hoisted function declaration** and are closed permanently, and 3-6's population was inferred from a subtraction with a missing term (`offered = rejected + dropped + surviving`, and `rejected` had no counter). Lifting the conjunct exposed **two wrong answers and one compile failure** that it had been masking. **63.97 → 0.01 B/iter and shape ÷ control 7.19× → 1.0000× on its shape; +32 B and 1.111× on the losing one; 1.0001× on the corpus.** Switch `BROILER_JS_CAPTURED_NUMERIC_LOCALS` |
| 3-8 | — (3-6's runtime half) | — | **Counted, and closed as written** — the mechanism is right and the target is not. Number boxing is **41.89% of the corpus's allocation** and the **whole** raw-double local tier removes **0.36% of those boxes**, because a box is minted by the operator and a local is one link in the chain. Of 1 916 drops, **76.4% take their value from a property read or a call** and only **2.5% from a parameter** — the category 3-3 deferred as the one that mattered. **Do not start; 3-1 and 3-2 move ahead of it.** Adds `NumberBoxingDiagnostics`, the `BROILER_JS_NUMERIC_LOCALS` whole-tier control, a drop-cause classifier and `NumericDropCauseTests`; also fixes a bookkeeping defect that inflated 3-7's `offered`/`rejected` |
| 3-4 | — (`tagged-js-value` in ownership.json) | 3-4 | Cost, do not start — **but its case is now the strongest in the phase, and 3-8 is why**. A tagged value removes the box at the *operator*, which is where 41.89% of the corpus's allocation is minted, rather than at one end of it; 3-7 had already given it the 247 names a hoisted declaration holds, which need a representation that can carry *uninitialized*. Still a cost rather than a task, and still behind 3-1 and 3-2, which reach the same boxes without an engine-wide redesign |
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

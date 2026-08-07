# Phase 1 — the front end

Compile time and the two worst scores in the suite. Item 1-1 is large enough to have its own part; 1-2, 1-3 and 1-4 are here.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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

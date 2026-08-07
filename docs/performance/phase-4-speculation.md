# Phase 4 — speculation

Type feedback, deoptimization, the specializing tier-2 compile, inlining, and the fixed cost of a call — which is where the phase's budget should go.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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
| **4-2** | **A specializing tier-2 compile** — **split by measurement; 4-2a and 4-2b both landed, arithmetic half outstanding** | `BuiltIns/Function/JSFunction.cs` — replace the `numericPlan == null` branch, plus `Runtime/TypeFeedback.cs` and `.LinqExpressions` for 4-2b | Consume 4-1's feedback: monomorphic property access → shape check plus direct slot read; arithmetic → raw `double`/`int` where feedback says so. **Measuring the branch first found it unsound** — it does not recompile the same code the same way, and DeltaBlue died on it — so the item splits into **4-2a** (S, the recompile contract) and **4-2b** (L, the specializing emission). 4-2b specializes **44.7% of the corpus's executed reads at 0.818× each**, which is **0.83% of suite time**: real, and below the noise floor. **The arithmetic half is now measured and refused** (4-2c): its population is 3-1's `NoSavingToMake` refusal, its census was another seven-suite 100.00% that reads **92.10%** over twelve with a **0.46%–100%** spread, and specializing all of it is **0.119% of the corpus** — net negative at the `+` rate, whose failing guard is **18.567×**. The relational lead is closed with it at **0.022%**, so the **whole generic binary-operator surface is 0.475%** | ~~XL~~ **S + L + refuted** |
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

- **The item's arithmetic half — measured, and refused by its own arithmetic** (`0107`). See
  §4-2c below; it is 0.119% of the corpus, and at the `+` rate it is net negative.
- **A poisoned site still pays its guard**, which 4-3b predicted and recorded as owed to this item.
  The right answer is to re-emit the method without the guard once a site poisons; 30 sites of 1 130
  is small enough that it was not worth building before this item had a throughput number, and now
  that it has one, 0.83% is not the place to spend it.
- **Prototype-resolved reads are not specialized.** A method read — which is most of what Richards
  and DeltaBlue do — needs the receiver shape, the receiver's prototype identity, the global
  prototype version and the holder's shape and slot, all four of which the cache already guards.
  That is a strictly larger guard and it is the same set 4-4's inlining needs, so it belongs with
  4-4 rather than here.

#### 4-2c · The arithmetic half — **priced, and refuted along with the lead it points at** — `0107`

The item's third clause is *"arithmetic → raw `double`/`int` where feedback says so"*, and it stayed
open on one stated blocker: **the numeric-vs-generic signal was left uncollected because the
compiler already proves numeric-ness statically, so a runtime counter has to be defined against
*that* to say anything new.** Defined that way, the population is not "arithmetic" at all.

**Item 3-1's speculation is on by default and already takes 71.76% of candidate nodes.** It
speculates on `+ - * / % **` and the bitwise operators over operands the compiler cannot prove
numeric — statically, with a guard, needing no feedback. So what is left for a feedback-driven tier
is 3-1's **refusals**, and over the widened corpus they are one thing:

| 3-1's decision | nodes | share |
|---|--:|--:|
| Specialized | 57 996 | **71.76%** |
| **NoSavingToMake** | **21 188** | **26.22%** |
| StringLeaf | 1 111 | 1.37% |
| AlreadyNative | 474 | 0.59% |
| TooManyLeaves | 39 | 0.05% |
| WithOrEvalShadow | 8 | 0.01% |
| OrderUnsafe | 0 | 0.00% |

**`NoSavingToMake` is the whole of the residue, and the condition that produces it reasons about
allocation.** A single-node tree over two unprovable leaves — `a.x * b.y` — removes no intermediate
and no already-native leaf, so the guarded form mints the same box the generic operator does. That
is correct about boxes and *says nothing about time*, and time is what a tier-2 specialization sells.

#### The census the item would rest on was a seven-suite figure, and it does not survive widening

§4.1 quotes *"100.00% of the invocations is what says the guard predicts"*. Re-taken over the twelve
suites that run, `arithmeticBothNumbers` is **92.10% of 26 198 356** — and the total is the least
interesting part of it:

| Suite | generic arithmetic | both Numbers |
|---|--:|--:|
| Box2D | 4 152 413 | **100.00%** |
| NavierStokes | 1 738 413 | **100.00%** |
| Gameboy | 13 240 220 | 99.50% |
| Typescript | 2 850 444 | 98.71% |
| **PdfJS** | **3 121 352** | **46.56%** |
| **Splay** | **264 890** | **0.46%** |
| **all twelve** | **26 198 356** | **92.10%** |

**Third instrument to fall to what §4.2a found, and the first where the *spread* is the finding
rather than the total.** A signal that reads 100% everywhere says a static widening would do; a
signal that reads 0.46% on one suite and 100% on another says the opposite — which is the argument
*for* per-site feedback, and it is the first evidence this item has ever had for its own thesis.

#### What the operation costs, three arms, and the instrument had to be fixed first

**The first harness ran each arm's samples consecutively and its generic arms came back with
spreads of 161%, 76% and 470% against effects near 3×** — §3.5's rule about a control varying by
more than the effect, produced by the instrument rather than found by it. Consecutive samples give
an arm a private slice of the process's history: its own gen-0 debt, its own place in the tiered-JIT
ramp, whatever the previous arm left on the heap. It reported `multiply-generic` at **39.00 ns** and
`less-generic` at **20.67 ns**; round-robin with a blocking collection between samples reports
**15.42** and **3.93**. *A 2.5× and a 5.4× error, in the direction that would have founded the item.*

Fixed — every arm once per round, reversed on alternate rounds, ratioed **within** each round so the
round's noise divides out:

| | median pair ratio | rounds favouring | ns saved |
|---|--:|--:|--:|
| multiply | **0.704×** | 11/12 | **6.39** |
| multiply, guard wrong | 1.760× | 0/12 | −14.53 |
| add | 0.906× | 9/12 | 1.78 |
| **add, guard wrong** | **18.567×** | 0/12 | **−281.21** |
| relational | **0.753×** | **12/12** | 0.97 |
| relational, guard wrong | 0.917× | 10/12 | +0.33 |

**`+`'s miss is 18.6× because its failure is a real answer** — a string concatenation, 352 B against
32 — rather than a coercion. A `+` site that is one part in a hundred strings loses overall. And
**relational is the one guard here with no losing side**: even when it fails it is cheaper than the
generic path, because the generic `Less` re-dispatches where the guard's type test does not.

#### Multiplied out, the item is refused

Best case over the corpus — every hit specialized at the multiply rate, every miss paying for it —
is **124 ms of a 104 620 ms driver: 0.119%**. At the `add` rate it is **net negative**. For scale,
**4-2b landed at 0.83%** and this document already called that *"real, and below the noise floor"*.
This is 7× smaller than the thing that was already too small to see.

*(The driver is the **twelve suites that run**, per §4.2a's convention. A first reading of this
divided by all fifteen and got 0.038% — Mandreel spends 286 728 ms hitting the stack guard while
making 1 488 calls, so it is 72% of a fifteen-suite wall clock and near zero of everything being
counted. The direction is unchanged and the magnitude is 3× larger; item 4-4 below records the same
mistake, where it changed a conclusion rather than only a figure.)*

#### The lead it points at is closed the same way rather than left as a guess

Relational is 0.753× with no losing side, and **no fast path in this engine reaches it**: 3-1's
speculation is gated on `IsNativeNumericOperator`, which excludes the relational operators, and 3-5
helps only when one side is *already* an unboxed double — its own counter records how many sites it
could not reach and stops there. That looked like the better item. It is not, and saying so needed
the counter nobody had built: **23 986 595 comparisons, 99.85% both-Numbers** — far more uniform
than arithmetic, PdfJS 98.58% and Box2D 99.64% being the only suites off 100% — and worth
**23 ms, 0.022%**.

**So the whole generic binary-operator surface is bounded, which is worth more than either item
was.** 26.1 M arithmetic invocations at 15.42 ns and 24.0 M comparisons at 3.93 ns is **497 ms of
104 620 ms — 0.475% of the corpus's execution time if it were removed *entirely***. That sits beside
4-2b's own closing bound (the read path ≤ ~9%, the call path ≤ ~5.5%) and is the third side of the
same box: **the operators are not where this engine's time goes.**

**The counter is counted once per source-level comparison by a re-entrancy guard rather than by a
case analysis over which paths delegate** — `JSObject` coerces and calls the primitive's, `JSNumber`
unwraps and calls the base's, the base hands a BigInt comparison to the other operand's mirror — and
that analysis is exactly what left `BitwiseXor` unhooked and silent about it. Four fixtures assert
**exact** counts: N comparisons in the source are N in the counter, through the re-dispatching
object path where a naive hook double counts, with a string comparison separating from a numeric
one, and across all four operators so a missing hook shows as three rather than as a smaller number
nobody questions.

**Status: 4-2's arithmetic half is closed as refuted by measurement, and no relational item is
opened.** The hooks are behind the existing off-by-default flag on the same methods whose arithmetic
siblings already carry one, and nothing that ships changes.

### 4-4 · Inlining of small JS callees — **premise measured, ceiling re-taken over the corpus at 2.43%; still do not start it, because 4-5 is 8.06%**

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

#### The ceiling was a seven-suite number too, and widened it is LARGER

**Everything above is computed over the same seven suites §4.2a found the other censuses were stuck
on**, and the table that produced it says so on its own face — *"All seven"*. `SpecializingTierMetrics`
reaches all fifteen since `0103`, so the same instrument was simply re-read, and **the seven
reproduce**: 6 194 744 invocations against the recorded 6 194 758 and 2 496 730 inlinable against
2 496 760 — **14 and 30 apart in millions, 0.0002%**, which is the engine's own start-up calls
varying between runs — a 19 650 ms driver against 19 694 ms, and the same **1.89%** and **4.48%**.
So what follows is more suites, not a different measurement.

**The denominator has to be the twelve suites that RUN, and getting that wrong the first time is
worth recording.** Three suites report a failure — zlib (`read` is a shell builtin), RegExp (a
pre-existing checksum) and **Mandreel, which spends 286 728 ms hitting the stack guard** — and
§4.2a's own convention already says the widened headlines are over twelve *"and the JSON says so"*.
Summed over all fifteen, Mandreel alone is **72% of the corpus's wall clock** while contributing
**1 488 of 59.7 M calls**, so a per-call ratio computed against it reads 0.65% and means nothing.
*The document's own rule, ignored by the person who wrote the paragraph that states it.*

| | the seven | **the twelve that run** |
|---|--:|--:|
| All invocations | 6 194 744 | **59 372 476** |
| Native callee | 37.0% | **32.1%** |
| Calls with a JavaScript callee | 3 902 604 | **40 523 273** |
| …from a promoted caller | 2 496 730 — **64.0%** | 17 074 137 — **42.1%** |
| Driver | 19 650 ms | 104 620 ms |
| **4-4's ceiling** | **1.89%** | **2.43%** |
| Inlining every JS-callee call | 2.95% | **5.77%** |
| **The fixed call prologue (4-5's surface)** | **4.48%** | **8.06%** |

**The two halves move in opposite directions, and that is the finding.** The *population* share
falls — 64.0% of JavaScript calls come from a promoted caller on the seven, 42.1% on the twelve —
while the *time* share rises, because the suites nobody had counted make far more calls per
millisecond of run time than the seven do. **4-4's ceiling goes up to 2.43% and 4-5's surface to
8.06%**, and the seven suites turn out to be **10.4% of the corpus's calls against 18.8% of its
time**: call-poor, not call-rich, which is the opposite of how they were chosen.

**Cross-checked against a counters-off driver**, because a ratio whose denominator carries the
instrument's own cost is not a measurement: 110 620 ms against 104 620 ms over the same twelve, a
**0.946×** ratio with per-suite ratios spanning 0.75×–1.11× — so the counters are inside run-to-run
variance and the figures read **2.30%** and **7.62%** there. Both are quoted counters-on, as the
1.89% was.

**And the drop is not about inlining — it is about the promotion gate's reach.** The native share
barely moves (37.0% → 32.1%); what collapses is *"from a promoted caller"*, 64.0% → 42.1%, because
the suites nobody had counted hardly promote at all:

| Suite | JS-callee calls | from a promoted caller |
|---|--:|--:|
| EarleyBoyer | 1 537 068 | 93.9% |
| Crypto | 240 363 | 90.3% |
| DeltaBlue | 335 626 | 86.4% |
| **Typescript** | **31 170 780** | **38.8%** |
| Box2D | 1 222 502 | 19.6% |
| Splay | 616 811 | 15.5% |
| **PdfJS** | **949 790** | **1.1%** |
| RegExp, NavierStokes, Mandreel, CodeLoad, zlib | 19 190 | **0.0%** |

Typescript alone is **77% of the corpus's JavaScript calls** and promotes 38.8% of them; PdfJS makes
nearly a million and promotes one in ninety. 4-3b established that a guard needs an observation and
a tier-1 method has none, so **inlining only has meaning inside a tier-2 recompile** — which makes
4-2a's promotion gate the ceiling on 4-4's ceiling. *That gate reaches 42% of the real corpus's
JavaScript calls, not 64%, and widening it is a different item from the one 4-4 describes.*

**So 4-4's re-specification survives, but for a different reason than it was written for.** The
section below recommends 4-5 over 4-4 on the strength of 4.47% against 1.89% — a 2.4× argument.
Widened it is **8.06% against 2.43%**, which is **3.3×**: the ranking is unchanged and firmer, but
**4-4's ceiling did not shrink into irrelevance — it grew to about three times 4-2b's landed
0.83%**. That is a real number for an XL, and the honest statement is narrower than the one this
section previously carried: *4-4 is not too small to matter, it is too small to beat 4-5*, and
4-5 needs no speculation, no guard, no tier and no fallback path, and cannot change a stack trace.
**The recommendation is the same; the argument for it is now about the alternative rather than
about 4-4 being negligible.**

**And the same probe says where the time actually is.** Reads are 37 871 908 × 47.6 ns = **1 804 ms
(9.16%)**. The call *prologue* is paid by all 6 194 758 invocations — a native callee takes the same
entry as a JavaScript one — so at 142 ns fixed that is **880 ms (4.47%)**. The two paths phases 2
and 4 are built around are together **under 14% of Octane's execution time in this engine**,
measured directly rather than as the pair of upper bounds 4-2b could give. §4's header says phase 4
is "the difference between ~100× and ~10×"; **that is not what these numbers say**, and the sentence
should not survive them unqualified.

**Widened, the pair is 22%, and the way it is computed needs saying.** The call half comes from
this host and is re-taken cleanly above: **8.06%**. The read half does *not* — 37 871 908 is 4-1's
count of executed property reads from `TypeFeedbackMetrics`, divided by a driver from
`SpecializingTierMetrics`, so **the 9.16% was already a figure mixed across two hosts**. That is
inherited rather than introduced here, and it is named rather than quietly repeated. Computed the
same way over the same twelve suites — §4.2a's **307.9 M** reads against this host's 104 620 ms —
the read half is **14.01%**, and the pair is **22.07%, not under 14%**. ***Both halves rose, and the
sentence they were drawn for is wrong in the direction that matters***: the two paths phases 2 and 4
are built around are about a fifth of this engine's execution time rather than a seventh — still a
minority, still not *"the difference between ~100× and ~10×"*, but a materially larger minority than
the seven suites showed. (This host's own read columns count the inline cache's hits and misses,
which is a different population from 4-1's, and is why the mixed figure is kept rather than silently
replaced with a single-host one that would not be comparable to the 9.16% it corrects.)

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
  risk. **Over the twelve suites that run the same comparison is 59 372 476 against 17 074 137 — 3.5× the
  calls — and 8.06% against 2.43%**, so widening moved both items *up* and moved 4-5 further ahead
  of 4-4. **Halving the fixed cost is ~4.0% there**, the largest single measured target anywhere in
  phase 4, and still more than 4-4's perfect execution.
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
measurement nobody can re-run is not evidence — **and that is exactly what let the ceiling be
re-taken over the whole corpus without writing a line of engine code**: `0103` widened the host, and
the widened reading was one command away for a patch before anybody ran it.

### 4-5 · The fixed cost of a call — **attributed: 92% of the bookkeeping is Annex B `caller`/`arguments`**

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

#### So where is a call's 142 ns? Not anywhere the REPLICAS can see

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

#### The 85% was not unattributable — the replicas were the wrong instrument — `0108`

**4-4 named the measurement that answers this and it had never been taken:** *"Two call paths exist
in this engine and one is much shorter; **pricing the difference between them is the first thing 4-5
should do**."* The table above prices each mechanism by **replicating it locally**, which is right
for the claim it was testing — *is a `using` scope expensive?* — and is silent on what the engine's
own scopes do inside themselves. `JSFunction.InvokeCallback` is the engine's own short path: the
same `EnterRealm`, the same `SelectInvocationDelegate`, the same `this` coercion, **one `using`
scope instead of five, and none of the executing-function or legacy-caller bookkeeping**. It is a
natural ablation that has been shipping the whole time.

| Entry | ns per call | spread |
|---|--:|--:|
| **`InvokeFunction`** — every emitted JavaScript call site | **114.60** | 12.5% |
| **`InvokeCallback`** — every native builtin's JavaScript callback | **64.43** | 5.4% |
| **difference** | **50.18 — 0.562×** | |

> ***44% of a call entry is bookkeeping, not 10 ns of it.*** **The item is not blocked on a tool.**

Both arms run the same callee with the same prebuilt `Arguments`, and **both are asserted to return
the same answer before anything is timed** — a short path that quietly returned early would look
exactly like the finding this exists to produce. Neither allocates, so the 32 B per argument an
emitted call site pays is outside this measurement by construction, and the callee's own body is in
both arms and cancels.

**The gap is a lower bound in both directions that matter.** The short arm is reached by a delegate
bound once through reflection, so it pays a delegate dispatch the long arm does not (0.68 ns,
already priced above), and it resolves a tail-call sentinel the long path handles in its own loop.
Both make the measured difference *smaller* than the bookkeeping actually is.

**What it is worth.** 50.18 ns on **59 372 476** invocations is **2 979 ms of a 104 620 ms driver —
2.85%**, against 4-4's entire ceiling of 2.43% and 4-2b's landed 0.83%. **It needs no speculation,
no guard, no tier and no fallback path, and it cannot change a stack trace.**

**What it is not.** *The 50 ns is not 50 ns of waste.* The bookkeeping serves `f.caller`, strict
mode across a call boundary, realms and `with` scopes; some of it is required and some of it is
required only for functions that can observe it — `HasLegacyCallerArguments` is already a
per-function test. So the finding is a **budget**, not a saving: it localises the item's missing
85% to eight named operations between the two entries — the executing-function save/set/restore,
the legacy-caller check and frame, `EnterStrictMode`, the `JSEngine.Current` cast and its
`Options.ScriptHostMode` read, the two `with`-scope pushes (both of which return `null` early here,
so the cost is the calls and the property reads rather than the scopes), the second `try`/`catch`,
and the tail-call test. **`EnterRealm` is in both arms and is therefore excluded**, which the
replica pass could not have told anyone.

#### The ablation, and the sum closes — `0109`

**The isolating control needs no engine change either.** `AddLegacyCallerAndArguments` is emitted
for ordinary non-strict function *declarations and expressions*, so an **arrow** and a **shorthand
method** are sloppy callees with no legacy frame. Through the *same long entry*:

| Arm | ns per call | bytes |
|---|--:|--:|
| `InvokeFunction` — ordinary sloppy function | 116.19 | 0 |
| **`InvokeFunction` — arrow** | **71.79** | 0 |
| `InvokeFunction` — object method | 76.53 | 0 |
| `InvokeCallback` — the short entry | 67.98 | 0 |

***An arrow through the long entry costs 3.81 ns more than the short entry that skips all the
bookkeeping.*** So the bookkeeping is almost entirely one thing, and the components — measured
against **the engine's own accessors** this time, not replicas — say which:

| Piece | ns |
|---|--:|
| **the legacy caller/arguments frame** (long − arrow) | **44.40** |
| the executing-function save/set/restore | 2.14 |
| the `JSEngine.Current` cast and its `Options.ScriptHostMode` read | 2.14 |
| the two `with`-scope pushes | 0.01 |
| **sum** | **48.69** |
| *target (long − short)* | *48.21* |

**The sum closes to within 0.5 ns, and 92% of it is a web-compatibility feature.** `PushLegacyFrame`
copies the `Arguments` struct into a `LegacyFrame` and again into the function, then pops it back —
on **every call to every ordinary non-strict function**. As an upper bound that is **2.52% of the
corpus**. Item 2-9 already recorded that these cells cost something at function *creation*; **they
also cost 44 ns per call**, which nothing had measured. The `with`-scope pushes, which read as the
most suspicious line in the method, are **free**.

#### And the control that was supposed to isolate it found something larger

The measurement started from the strict callee, on item 2-9's reasoning that a strict function has
no legacy cells. It does not — and it costs **more**, not less:

| Arm | ns per call | bytes |
|---|--:|--:|
| `InvokeFunction` — sloppy callee | 116.19 | 0 |
| **`InvokeFunction` — strict callee, entered from sloppy code** | **219.06** | **224** |
| `InvokeCallback` — strict callee | 64.12 | 0 |

**102.87 ns and 224 bytes more, per call**, and the short entry shows it is not the callee: it is
`StrictModeScope`. That scope writes the strict-mode `AsyncLocal` when the callee's strictness
differs from the currently executing code's — **on entry and again on exit**. Its own comment says
the write happens *"only on a transition, so the common case is now a ThreadStatic read and a
compare, with no AsyncLocal touched at all"*, which is **true of a uniformly strict or uniformly
sloppy call graph and false at every boundary between them**. *4-5 fixed the read side and left the
write side resting on an argument about frequency that nothing in the engine could check.*

**So it is counted** (`CallPathDiagnostics.RecordStrictTransition`, inside the `changed` branch
where the claim lives, behind the same off-by-default flag the call counting already uses). Whether
102.87 ns matters is a question about the corpus rather than about the mechanism, and until this
counter there was no way to ask it.

**Counted, the comment is right about the corpus and wrong about one suite in it.** Over the twelve
suites that run, **2 813 191 of 59 372 513 calls cross a strictness boundary — 4.74%** — and the
distribution is the finding rather than the total:

| Suite | calls | strict transitions | share |
|---|--:|--:|--:|
| **PdfJS** | 3 995 534 | **2 103 558** | **52.65%** |
| Gameboy | 7 216 202 | 709 632 | 9.83% |
| Typescript | 41 321 153 | 1 | 0.00% |
| *the other nine* | 7 million-odd | **0** | **0.00%** |

**Nine of twelve suites never cross at all**, so *"the common case is a ThreadStatic read and a
compare"* is a fair description of this corpus — and **PdfJS crosses on more than half its calls**,
where the claim is simply false. **2 813 191 × 102.87 ns is 289 ms of a 108 767 ms driver: 0.266%.**
Real, concentrated, and small; the write side does not need fixing for Octane, and the sentence
asserting it should say *"on a uniformly-strict or uniformly-sloppy call graph"* rather than
*"the common case"*.

#### What 4-5 is now, ranked

| | of the corpus |
|---|--:|
| the whole `InvokeFunction` entry | **6.50%** |
| **the legacy caller/arguments frame** — counted, 60.16% of calls | **1.46%** |
| the strict-mode `AsyncLocal` write | 0.266% |
| the executing-function save/set/restore | ~0.08% |
| the `Current` cast and `Options.ScriptHostMode` read | ~0.08% |
| the `with`-scope pushes | **0.00%** |

#### The split, counted, and the ceiling was tight — `0110`

`0109` bounded the legacy frame at **≤1.65%** by charging it to every call with a JavaScript callee,
because nothing said how many of those callees actually carry the pair. **Counted at the entry, it
is 35 715 923 of 59 372 494 calls — 60.16% of all calls and 88.14% of the ones with a JavaScript
callee — worth 1 586 ms of a 108 879 ms driver: 1.46%.** The ceiling was tight, and the item is
real.

| Suite | JS-callee calls | pushes a legacy frame |
|---|--:|--:|
| Richards | 121 402 | **100.00%** |
| NavierStokes | 622 | 98.73% |
| DeltaBlue | 335 626 | 96.23% |
| Splay | 616 815 | 96.12% |
| Crypto | 240 388 | 94.09% |
| Typescript | 31 170 780 | 75.44% |
| Box2D | 1 222 502 | 69.87% |
| RayTrace | 445 021 | 65.76% |
| EarleyBoyer | 1 537 068 | 50.53% |
| **PdfJS** | 949 784 | **0.59%** |
| **Gameboy** | 3 882 298 | **0.02%** |

**The two suites that escape it are the two that were strict**, which is the same split the
strict-transition census found from the other side — PdfJS crosses a strictness boundary on 52.65%
of its calls *because* most of its code is strict, and strict functions carry no legacy pair. *A
program written in strict mode does not pay this at all.*

**And `this`-coercion turns out to be the same population, exactly.** `thisCoercions` equals
`legacyFrames` on every suite, to the call: on this corpus the callees that carry Annex B cells are
precisely the ones that coerce a sloppy `this`. So the two conditions the entry tests separately
are, in practice, one condition — which matters for a fix, because it means a single "ordinary
sloppy function" predicate gates both.

**What a fix looks like, since the item can now be pointed at one.** The cells are already deferred
at creation (2-9); what is not deferred is the per-invocation frame, and `PushLegacyFrame` copies
the `Arguments` struct into a `LegacyFrame` and again into the function, then copies it back on the
way out. Nothing reads either cell in any benchmark, and nothing can read them *except* during the
call — so the state exists to answer a question that is almost never asked. The engine already
maintains `JSEngine.ExecutingFunction`; a `caller`/`arguments` cell that walked a thread-local
invocation stack on demand would need no per-call state at all. **That is an M–L with real
correctness surface** — recursion, re-entrancy, generators suspending mid-call.

#### And the fix was priced before it was built, and refused — `0111`

**0.730×.** Over a control that does the guard and nothing else:

| Arm | ns | over control |
|---|--:|--:|
| control | 0.36 | — |
| **save/restore on the function object** — what the engine does today | **23.32** | **22.96** |
| **push/pop a thread-local stack** — the proposed fix | **17.13** | **16.77** |
| one 56-byte `Arguments` copy, alone | 8.19 | 7.83 |

**The saving is 6.19 ns**, which over 35.7 M calls is **221 ms of a 108 879 ms driver: 0.20% of the
corpus, for an M–L with a generator-suspension hazard in it.** *And the third arm says why:* one
`Arguments` copy alone is **8.19 ns**, so ***the cost is the copying, and relocating where the
copying lands does not remove it.***

**So the lever is eliminating copies, not moving them** — removing the frame outright is **1.46%,
seven times the relocation**, and that is where any effort belongs. It is also the hard one: a
static gate on "does this program ever touch `caller`/`arguments`" is unsound, because both are
reachable through a computed member access no analysis of the source can exclude, and a gate flipped
at *first* materialisation gets the very read that flips it wrong.

**The replica is legitimate here and it is worth saying why, given `0108`.** That patch's finding
was that replicating a *mechanism* — a `using` scope, an EH region — misprices it, because the
engine's own scopes have insides. A **struct copy has no inside**: the JIT emits the same moves for
the same layout wherever it appears. The *absolute* is still an under-estimate — these arms write
static fields where the engine writes instance fields of a heap object, so the engine pays write
barriers these do not, which is most likely the gap between 22.96 ns here and ~44 ns in situ. **The
ratio is what the arm is for**, and the ratio is the answer.

**Status: 4-5's largest cost is measured at 1.46%, its named fix is refused at 0.20%, and no
successor is specified.** That is a better place than the item has been in — *it is no longer
blocked on a tool, or on a design nobody had priced; it is blocked on the fact that the only fix
worth building is the one whose gate cannot be made sound.*

*The shape of the answer is worth stating plainly: **the largest single attributable cost in this
engine's call path is Annex B `caller`/`arguments`**, a feature no benchmark uses and the
specification marks as legacy web reality. Whether it can be made lazy — the cells are already
deferred at creation (2-9); it is the per-call frame that is not — is a design question this item
can now be pointed at instead of a profiler.*

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

#### That item landed, and this section's own control is now four times faster — `0108`

**Item 3-5 shipped on the strength of the paragraph above, and re-running the probe shows it
worked.** The same two shapes, same host, same run set:

| | when this section was written | now |
|---|--:|--:|
| bound is a **parameter** (`i < n`) | 33.77 ns, **32 B** | **7.67 ns, 0 B** |
| bound is a **literal** | 8.36 ns, 0 B | 4.45 ns, 0 B |

**The box per iteration is gone — 32 B → 0 B, which is the exact half of the claim** — and the
parameter shape is now within 1.7× of the literal rather than 4.0×. The timing rows carry ~38%
spread on this machine and the allocation rows carry none, so *the boxing is what is established
and the 4.4× is the noisy corollary*, exactly as the original reading was careful to say in the
other direction.

**Two consequences, and the second is the one that matters here.** First, **every probe in this
document that used the control as a floor was using a floor that has since dropped**, and figures
quoted against it should say which side of 3-5 they were taken on. Second, this section closes by
inferring that *"the arithmetic-only control loop is 16.98 ns an iteration, which points at 3-4,
not at phase 4"* — **that inference no longer holds**. A control-loop iteration is now **7.67 ns**
and a call is still **~147 ns fixed**, so a call is about **19× a loop iteration** where it was
about 4×. *The thing the control was hiding has been fixed, and what it was hiding it from is the
call path.*

#### Copies removed rather than relocated, and the replica had overstated them — `0104`

`0111` refused the item's named fix and, in the same table, said what to try instead: one 56-byte
`Arguments` copy alone is **8.19 ns**, so ***the cost is the copying, and relocating where the
copying lands does not remove it.*** The lever is eliminating copies.

**There was one to eliminate that needed no design at all.** `PushLegacyFrame` returned a 72-byte
`LegacyFrame` **by value** and the caller assigned it into a local. An `out` parameter writes the
displaced frame straight into that local: the same frame, two fewer copies of it, and no semantic
change of any kind.

**Measured in situ, which is the instrument this item has trusted since `0108`** — `--call-entry-cost`,
against a baseline binary built from the unmodified tree on the same machine, twelve interleaved
ABBA pairs:

| | baseline | `out` | |
|---|--:|--:|--:|
| `InvokeFunction` | 117.32 ns | **115.50 ns** | 0.984× |
| the legacy frame alone (long − arrow) | 44.05 ns | **42.02 ns** | 0.954× |
| pairs won by `out` | — | **9 of 12** | |

**1.83 ns on 59 372 476 invocations is 109 ms of a 108 879 ms driver — 0.100%.** *And 9 of 12 is
not the separation §3.5 requires:* "the sample count has to grow until the arms separate by rank,
not by median." **The change is kept because it is strictly less work for identical semantics, not
because 0.100% is established** — which is the opposite of the usual bargain in this document and
worth stating plainly rather than rounding up.

**The gap between the prediction and the result is the finding.** `0111` priced a struct copy at
8.19 ns; removing two of them bought 1.83 ns, not 16. So **most of the return-by-value traffic was
already elided before it reached the machine** — the JIT constructs in place when it can see the
destination. `0111` argued that a replica is legitimate for a struct copy *because a struct copy has
no inside*, and that argument is still right about the **ratio** it was making. What it does not
license is reading the replica's absolute as a count of copies the engine actually performs:
**a struct copy in the source is not a struct copy in the code.**

**What this does not change.** The frame is still **1.46% of the corpus** and still the largest
single attributable cost in the call path; 0.100% of it has been taken by making the bookkeeping
cheaper to carry, and the other 1.36% is still the copying itself, still gated on a soundness
question nobody has answered. *The item's status is unchanged — only its floor moved.*

#### Re-specification

- ~~**The prologue work 4-5 was created to do is mostly not there.**~~ **Superseded by `0108`.**
  The replicated mechanisms could not see it; the engine's own short path can. **44% of a call
  entry — 50.18 ns of 114.60 — is bookkeeping `InvokeCallback` already skips**, which is
  **2.85% of the corpus**, and the next move is an ablation of the eight named operations between
  the two entries rather than a profiler. *The item is unblocked and it is now the largest measured
  target in phase 4.*
- ~~**New 3-5 (M): give a parameter a numeric local.**~~ **Landed, and re-measured here**: the box
  per iteration is gone (32 B → 0 B) and the control loop is **7.67 ns** against 33.77. This
  section's closing inference — that the control points at 3-4 rather than at phase 4 — **falls with
  it**: a call is now ~19× a loop iteration where it was ~4×.
- **What is left of the item, ranked and now attributed** (`0109`). The whole `InvokeFunction`
  entry is **6.50%** of the corpus and the bookkeeping half **2.85%**; 4-4's ceiling is **2.43%**;
  4-2's arithmetic half was refused at **0.119%**. **4-5 is where the phase-4 budget should go**,
  and within it **92% of the bookkeeping is the Annex B `caller`/`arguments` frame at ≤1.65%** —
  everything else named in `0108`'s list is together under 0.5%, and the `with`-scope pushes are
  free. **The item is no longer "make the prologue cheaper"; it is "make the legacy frame lazy."**

---

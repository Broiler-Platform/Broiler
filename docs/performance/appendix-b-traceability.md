# Appendix B — traceability

Where each item in this plan came from in the two source roadmaps, so nothing was dropped in the merge.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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
[`Broiler.JS/docs/performance-roadmap.md`](../../Broiler.JS/docs/performance-roadmap.md) and
[`tests/octane/roadmap.md`](../../tests/octane/roadmap.md) are **archives** — superseded plans
kept for what they contributed, carrying diagnoses this document has since corrected, and
**not back-ported**. `tests/octane/roadmap.md` now says so at the top; the engine one is
labelled only here, because it is inside the submodule and this repository cannot annotate
it without a pointer bump. `tests/octane/benchmarks.md` is different: it is a *reference*,
not a plan, and stays live as the per-benchmark description.

**Dropped in the merge, deliberately:** the engine roadmap's detailed defect
narratives (the `SAUint32Map<T>` sentinel, the Debug-build stack-trace-on-throw, the
six pre-existing test failures, the three frame-recycling defects) are history, not
plan. They stay in
[`Broiler.JS/docs/performance-roadmap.md`](../../Broiler.JS/docs/performance-roadmap.md),
which remains the archive of record; only their transferable lessons were lifted into
§3.5. Likewise `tests/octane/benchmarks.md` remains the per-benchmark reference —
§4.3 carries only the ranked blockers.

---

_Merged 2026-08-01 from `tests/octane/roadmap.md`, `tests/octane/benchmarks.md` and
`Broiler.JS/docs/performance-roadmap.md`. Engine facts verified against `Broiler.JS` at
`cdb2fd41`; Octane code sites at `45f4f679`. Phase 2 worked and measured 2026-08-01/02 at
pointer `685026c0` plus the then-pending `0050`–`0058`, since applied and pinned as
`a6f101cc`; status summary in §0._

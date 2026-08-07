# Item 3-8 · Guard a local's numeric-ness at run time

The census that produced phase 3's denominator — how much of a real run is number boxing at all — and the dual-representation tier built, measured and closed as a regression on it.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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
is not kind: `0090` puts collection at 1.8% of the driver and the measured cost of allocation at
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
matter. At `0090`'s **711 ms per GB** that is **≈0.16 GB, ≈115 ms, 0.6% of the driver**, and the
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
prize is still bounded by `0090`'s exchange rate** — 6.76 M boxes is ≈0.16 GB, ≈115 ms, **0.6% of
the driver** — so what has changed is confidence, not size: the item now has a counted population
in the right place instead of an estimate.

**And the count does not license the build.** The mechanism is still the XL described above: every
fast path keys off `NumericStorage`, and a speculative local is a double only while a flag holds.
What the count settles is that if that work is ever done, there is something for it to reach.

#### Built, complete, measured — and it does not pay: **`0096`**

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
`0095` (**31 162 965**), which is 635 from `0085`'s corpus baseline: since the four unaffected
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
idea. It is bounded by the exchange rate `0090` measured, and this is what that bound looks like
when an item is followed all the way to a number instead of stopped at a plausible one.*

#### 3-9 · Counted, and closed by its own precondition — **`0097`**

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
the discipline `0096` added to §3.5 one item ago, applied to the instrument that decides this one.

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


#### The async resumption race — found by the gates, and it was two threads running JavaScript at once: **`0098`**

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

#### The embedding contract — the residual, measured at 86% and closed: **`0099`**

`0098` fixed where a job is *dispatched* and named what that could not reach: **a job posted while
nothing is executing** takes a host context or the thread pool, because the queue is deliberately
refused at depth zero — that refusal is what makes stranding a job impossible. Such a job could then
run JavaScript while a later `Eval` was running JavaScript.

**"In principle" was doing a lot of work in that sentence.** Reaching the case needs a JavaScript
entry point that is not `Eval`, and a host invoking a `JSValue` directly is exactly one — arm a
promise, settle it from a host thread with nothing running, and evaluate meanwhile. Measured:

| | peak threads in one context | overlaps / 200 rounds |
|---|--:|--:|
| `0098` — dispatch fixed, no lock | **2** | **172 (86%)** |
| `0099` — with the execution lock | **1** | **0** |

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


#### The blocking host wait — one deadlock from each of the last two changes: **`0100`**

`0099` recorded that the lock cost "one pattern it cannot support". Measured, **the attribution was
wrong and there are two patterns, one contributed by each change**:

| Host function called from a script, waiting on a `Task` completed by… | `0098` queue | `0099` + lock |
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

# Phase 3 — value representation

Number boxing, which is 41.89% of everything the corpus allocates. Items 3-1 and 3-8 are large enough to have their own parts; the phase intro and 3-0, 3-2…3-7 are here.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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

### 3-2 · Unboxed doubles in shape slots — **measured; its premise sentence is wrong, and the "one suite" was an artifact of the seven-suite corpus**

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

#### The table above is the seven suites, and widened it overturns the re-specification below

**The signal 3-2 was missing was collected on exactly the corpus §4.2a found the censuses were stuck
on**, and the total gives it away: 20 065 493 cache-answered reads, against **186 831 813** over the
twelve suites that run. *The seven are 10.7% of the corpus's cache-answered reads and 9.7% of its
numeric ones.* `SpecializingTierMetrics` has reached all fifteen since `0103`, so this is a re-read
rather than a new instrument — the fourth figure in this document to need one.

| Suite | cache-answered reads | of them numeric | | boxes allocated | in the seven |
|---|--:|--:|--:|--:|---|
| **Typescript** | **115 082 436** | **64 199 239** | **55.8%** | 8 797 514 | — |
| **Gameboy** | **47 152 809** | **27 437 672** | **58.2%** | **29 322 416** | — |
| Box2D | 18 242 021 | 9 853 002 | 54.0% | 5 225 033 | yes |
| PdfJS | 3 190 918 | 1 054 355 | 33.0% | 6 394 984 | — |
| Splay | 1 338 329 | 415 070 | 31.0% | 29 337 | — |
| Crypto | 651 171 | 74 382 | 11.4% | 13 409 653 | yes |
| NavierStokes | 388 | **0** | 0.0% | 11 747 635 | yes |
| **all twelve** | **186 831 813** | **103 158 443** | **55.2%** | **75 704 490** | |

**The premise strengthens and the plan inverts.** *"Half of every property read the cache answers
hands back a number"* goes from 50.1% to **55.2%**, so the item's founding observation is if
anything better than recorded. But **"3-2 is a Box2D item" is wrong**: Box2D is **9.6%** of the
corpus's numeric reads, not 98%. ***3-2 is a Typescript-and-Gameboy item*** — those two are
**64.2 M and 27.4 M numeric reads, 89% of the corpus's between them**, and neither had ever been
counted.

**The box split inverts with it.** *"3-1 carries 85% of the corpus's boxes (NavierStokes' 30.0 M plus
Crypto's 42.4 M)"* — over twelve suites those two are **25.2 M of 75.7 M, 33.2%**. **Gameboy alone is
29.3 M, 38.7%**, the largest single source in the corpus, and it is *not* one of 3-1's suites.

***And `0113` says which item Gameboy belongs to.*** Its dense element read/write ratio is **1.03**,
so a typed backing store there is an allocation wash — while **58.2% of its cache-answered property
reads hand back a number**. **For the corpus's biggest box source, 3-2 is the item and 3-1 is not.**
That is the opposite of the ordering below, and the two measurements were taken independently.

**What survives unchanged.** *"3-2 cannot touch 3-1's suites"* holds exactly as written and is
sharper for the widening: NavierStokes performs **388** property reads, **zero** numeric, against
11.7 M boxes; Crypto reads 651 171 against 13.4 M. Their numbers still live in `new Array` read by
index, and no work on shape slots reaches them. The two items are still one mechanism with two
backends — the identical per-iteration figures above are untouched by any of this.

#### Re-specification

> **Superseded in its ordering by the widened table above**, which was taken after it. The
> *mechanism* argument — one compiler half, two backends — stands; the *ranking* does not.

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

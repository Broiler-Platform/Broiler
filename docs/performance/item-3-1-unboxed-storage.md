# Item 3-1 · Unboxed backing stores for dense arrays

The longest item in the document: four attempts at a dual-representation numeric local, four refutations, one shared failure mode — and the method for refusing the fifth cheaply.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

---

### 3-1 · Unboxed backing stores for dense arrays — **re-measured; the storage half is re-opened as unmeasured, not refuted (§4.2a)**

> **The evidence that moved this item off storage came from a 7-of-15-suite corpus.** "Only 5.0% of
> NavierStokes' requests are a raw double crossing into a `JSValue`" is what retired the typed
> backing store, and it was never tested against **Gameboy** — a `Uint8Array` memory image with
> register arrays, never in any census, where the conversion is **51.0% of a 52.8 M-request
> workload** and which alone mints **more conversions (26.9 M) than the entire old corpus
> (24.6 M)**. The operator work this item did instead is real and measured and stands; what is
> withdrawn is the *refutation* of the storage half. See §4.2a. **This is not a claim the store is
> worth building** — that needs a wall-clock A/B nobody has run, and by `0086`'s rate lesson
> Gameboy's 1.96 GB over 23.8 s is a lower rate than NavierStokes' 0.49 GB over 2.0 s.
>
> **Since resolved on the axis that decides it** (`0113`): the read/write ratio the verdict rests on
> is now counted, and **Gameboy's is 1.03** — an allocation wash. *The suite that re-opened the item
> does not carry it.* The corpus is **3.34 reads per write**, NavierStokes 5.26 and Crypto 4.80, so
> a typed store is a net allocation loss of ~2.3 boxes per write. **The live-memory case stands and
> is now the whole of the item.**

#### The ratio the whole verdict rests on, counted — and it settles what §4.2a re-opened — `0113`

The item trades a write allocation for a read allocation, so its verdict is a ratio: *"a wash at a
1:1 read/write ratio, a win only when writes dominate, and a loss on read-heavy code"*. It then
asserts that its named targets *"read each element many times per write, which is the unfavourable
direction"*. **That is a claim about the corpus, and nothing counted it.**

Counted on the **dense path** — the population a typed store would serve, since a dictionary-kind
array is outside the item entirely — and split by whether the value is a **number**, because an
array of strings would make a corpus ratio describe arrays the item cannot help:

| Suite | numeric dense writes | numeric dense reads | **reads per write** |
|---|--:|--:|--:|
| NavierStokes — *the item's grid target* | 10 370 089 | 54 560 144 | **5.26** |
| Typescript | 338 606 | 1 723 870 | 5.09 |
| Crypto — *the item's digit-array target* | 8 670 639 | 41 589 626 | **4.80** |
| PdfJS | 16 442 985 | 39 097 665 | 2.38 |
| Box2D | 414 130 | 987 088 | 2.38 |
| **Gameboy** | 1 726 800 | 1 777 310 | **1.03** |
| **Splay** | 2 824 002 | 15 998 | **0.01** |
| **corpus** | **42 351 440** | **141 424 351** | **3.34** |

**The assertion was right and now has a number.** At 32 B boxed per read against 32 B saved per
write, a typed store is a **net allocation loss of about 2.3 boxes per write** over the corpus — and
worse on both suites the item names.

**And it settles what §4.2a re-opened.** That section withdrew the item's refutation because the
evidence retiring the typed store came from a corpus that never contained **Gameboy**, where the
raw-double-to-`JSValue` conversion is 51.0% of a 52.8 M-request workload. *Counted, Gameboy's dense
read/write ratio is 1.03* — **an allocation wash, not a win**. The suite that re-opened the item
does not carry it, and what survives is the **live-memory** argument the item already made and never
needed this measurement for.

**Splay is named rather than averaged away.** It is the one suite running the item's favourable
direction, and it runs it by two orders of magnitude: **2 824 002 writes against 15 998 reads**. A
corpus ratio hides that, which is why the table is per suite.

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

#### Every conversion attributed to the site that mints it, over all fifteen suites — `0103`

**§4.2a re-opened this item on a count that could not name its own producer.** It found conversions
going **24.6 M → 69.3 M** once the census stopped silently running 7 of 15 suites, with Gameboy
alone at 26.9 M on a `Uint8Array` memory image — the shape 3-1 was written for — and withdrew the
item's refutation on that basis. `0113` then settled the *storage* question from the other side, by
counting the dense read/write ratio at 1.03 on Gameboy and 3.34 on the corpus. **Neither asked
where the conversions come from**, and nothing could: the counter sits in `JSNumber.CreateConversion`,
so it sees that a raw `double` crossed into a `JSValue` and not which of the compiler's emission
sites sent it. *A category is not a producer, and this document has now spent three sections
ranking a population by a counter that cannot distinguish its members.*

So each of the compiler's **21** `JSNumberBuilder.New` sites names itself, and the census reports
the split. The site is an ordinary constant argument on the one code path the engine ships — not a
factory entry per site, which would multiply into nine near-identical methods, and not an argument
gated behind the counter flag, which would leave the arm that is measured and the arm that ships
running different code.

**Counted with the counters on, over all fifteen suites** — the first census in this campaign to
have a row for every one of them:

| Site | conversions | of all conversions |
|---|--:|--:|
| **the guarded tree's ROOT box** | **42 847 270** | **61.79%** |
| a native binary operator's result | 9 415 048 | 13.58% |
| the `++`/`--` step | 8 288 977 | 11.95% |
| reading a scalar-replaced numeric local | 4 742 175 | 6.84% |
| a native unary `+`/`-` result | 2 980 358 | 4.30% |
| a numeric constant in an argument or element list | 950 895 | 1.37% |
| an assignment's value in expression position | 114 025 | 0.16% |
| **an operand falling back to the tree's generic arm** | **226** | **0.0003%** |
| *unclassified* | **0** | **0.00%** |

**Two readings, and the second is the one that re-specifies the item.**

**The guarded tree is not leaking.** The generic arm — the fallback an interior node takes when its
speculation fails — is **226 requests of 69.3 M**, zero on eleven of the fifteen suites. `0087`'s
order-preserving emission was argued to be correct and was never counted at run time; counted, its
guards essentially always hold. *There is no recoverable loss inside the mechanism.*

**And what is left is the box the design keeps on purpose.** 61.79% of the corpus's conversions are
the tree's root — one box per evaluation, minted because the root's *consumer* is a `JSValue` local,
slot or element. It is **92.5% of NavierStokes' conversions, 91.5% of Box2D's, 98.5% of Splay's and
84.4% of Crypto's**. That is not a storage problem and not a leak; it is the boundary the tree was
always going to stop at. **So the remaining question for phase 3 is not what the operators mint, and
not what the store holds — it is what the root box's CONSUMER is**, and whether a consumer could
take the raw double the tree already has in hand. That is the measurement this item hands forward,
and it is a compile-time attribution rather than another run-time counter.

**Gameboy, the suite §4.2a re-opened the item on, splits differently from every other suite and
against the item.** Its 26.9 M conversions are **47.3% root, 28.7% the `++`/`--` step (7 723 245) and
16.7% a binary operator** — the update step alone is larger than any other suite's entire conversion
count. §4.2a asked whether Gameboy's conversions are the typed store's population; they are not.
They are item 3-8's, which had priced the `++`/`--` step over a corpus that did not contain the one
suite where it dominates. *The suite that re-opened the storage half turns out to belong to the
locals half.*

> **Reproduction and honesty about the denominator.** 164 626 610 boxing requests and 69 338 974
> conversions over fifteen suites, against §4.2a's 164 127 581 and 69.3 M over twelve — the same
> instrument, 0.3% apart, with Mandreel, zlib and CodeLoad contributing 2, 1 and 600 conversions
> between them. Gameboy reproduced to the digit across two independent runs (26 938 581, matching
> §4.2a exactly); Crypto varied by 0.015% between runs, so *"deterministic"* is very nearly true of
> these counters rather than exactly true, and shares should not be quoted past three figures.

#### And the root's consumer, counted — the answer is the numeric local, not the store — `0105`

`0103` closed by naming the one measurement left: the root is boxed **because its consumer takes a
`JSValue`**, so the only thing that removes it is a consumer able to take the raw `double` the tree
already holds. That is knowable only to the compiler — the box is minted at the tree, and what
receives it is not visible from there — so the consumer travels **with the node being visited**.

**The attribution is restricted so that it cannot leak, which is the whole reason to trust it.** The
consumer is set only for a node that *is* an `AstBinaryExpression`, the one shape that reaches the
tree builder directly, so the field can never survive into a nested visit; the tree builder clears
it for its own construction, because a leaf may contain a tree; and a **compound** assignment does
not claim its right-hand side, because there the tree is an operand of the compound operator rather
than the value stored. *An attribution that leaks reads as a finding about the corpus when it is a
finding about the instrument*, and `a[0] = b * c + sink(d * 2 + i)` — an element store with a second
tree inside a call argument in its own right-hand side — is a test rather than a remark.

**Of 42 849 742 root boxes over all fifteen suites:**

| The root box is consumed by | boxes | of roots | of all conversions |
|---|--:|--:|--:|
| **a LOCAL or a declared binding** | **19 006 647** | **44.36%** | **27.41%** |
| an ELEMENT — `a[i] = …` | 7 673 079 | 17.91% | 11.07% |
| a named PROPERTY — `o.x = …` | 5 631 192 | 13.14% | 8.12% |
| a call ARGUMENT | 1 897 892 | 4.43% | 2.74% |
| a RETURN value | 169 872 | 0.40% | 0.24% |
| *unattributed* | *8 471 060* | *19.77%* | *12.22%* |

***The dominant consumer is a local, and that retires both of the storage items as the answer.*** A
proven-numeric local **already has a raw `double` home** — item 3-3 built it — so a root landing in
a local is not a root waiting for a new representation: it is one **the existing numeric tier failed
to type**. 44.36% of the tree's remaining boxes are being minted to cross into a destination that
did not need to be a `JSValue` at all.

**And it puts a ceiling on the typed backing store that is lower than anything §4.2a suggested.** The
element row — the entire population a typed store reaches — is **17.91% of roots, 11.07% of the
corpus's conversions, 7.67 M boxes**. `0113` already measured that store as an *allocation wash* at
the corpus's 3.34 read/write ratio, so this is a ceiling on something that is not free to begin with.
Item 3-2's shape slots are the property row, **13.14%**. *Neither storage item is where phase 3's
remaining boxes go.*

**The per-suite split is not uniform, and that is the useful half:**

| Suite | roots | dominant consumer | |
|---|--:|---|--:|
| Crypto | 14 396 573 | **local** | **81.8%** |
| Gameboy | 12 741 786 | local / element, near-even | 21.9% / 21.6% |
| Typescript | 4 825 206 | **property** | **74.0%** |
| NavierStokes | 3 734 334 | **element** | **59.2%** |
| PdfJS | 3 436 908 | local | 43.2% |
| Box2D | 2 934 766 | local | 45.1% |
| Splay | 518 880 | **argument** | **98.5%** |

Crypto's digit arrays send **81.8% of their roots to a local**, which is the suite this phase has
called an array workload throughout. NavierStokes is the one suite the typed store genuinely
addresses (59.2% element) and it is also the suite `0113` measured at a **5.26** read/write ratio —
the *worst* case for a typed store. *The suite that wants the mechanism is the suite that pays most
for it.*

> **The 19.77% residual is reported rather than folded away.** It is the assignment forms this pass
> does not wire — destructuring targets, shadowed bindings, and expression positions with no store
> at all. **Gameboy carries 43% of its roots there**, so its row is the least trustworthy in the
> table and its near-even local/element split should not be read as a finding. A default that
> silently absorbed these would have made every other row look more decisive than it is.

**What phase 3 has left, after this.** The element and property rows together are **31.05%** of the
roots and are the two items already measured as a wash and as Box2D-only. The local row is
**44.36%**, it belongs to the numeric-local tier, and that tier's own gap has been measured twice
from other directions — 3-8's `++`/`--` step at 98.1% `LocalSlot`, and 3-8a's dual representation
closed as a regression on the read/write ratio of the code it targeted. *Three independent counts
now point at the same mechanism, which is the strongest signal this phase has produced about its
own remainder.*

#### Which refusal costs the boxes — the tier is counted in the wrong currency — `0106`

`0105` put **44.36% of the corpus's root boxes into a local** and left two candidate explanations.
The cheap one is a **seam**: the destination is a local the tier had already *accepted*, and the box
is minted by the tree and unboxed by the very next instruction, because the assignment path asks for
the right-hand side as a `JSValue` whenever the **static** prover (`ToNativeExpression`) cannot type
it — even though the **whole-function** prover already did, which is the only reason the destination
is a numeric local at all. `AssignToVariable` then stores through `ToDoubleExpression`. Box, unbox,
two instructions apart.

***Measured, the seam is 36 boxes of 18.6 M.*** It is not the explanation — and finding that out
cost one counter and is what makes the rest worth instrumenting rather than guessing at.

**So every one of those locals is one the tier REFUSED, and item 3-6 already counted the refusals —
in the wrong currency.** It counted causes **per name**, which is the right shape for *"how many
bindings would a stronger analysis admit"* and the wrong one for *"what do the refusals cost"*: a
name refused in initialization code and a name refused inside a ten-million-iteration loop weigh the
same in it. The analysis now retains its per-name refusal and the boxing site names it, so the same
vocabulary is ranked by **execution** instead of by **declaration**.

**Of 19 005 731 root boxes consumed by a refused local, over all fifteen suites:**

| Why the tier refused the destination | boxes | share |
|---|--:|--:|
| **`DroppedCandidate`** — a cascade: another refused name reaches it | **7 300 519** | **38.41%** |
| **`ElementRead`** — `a[i]` reaches it | **6 908 814** | **36.35%** |
| *`Unknown`* — *a gap in the instrument, not a cause* | *2 429 752* | *12.78%* |
| `PropertyRead` — `o.x` reaches it | 925 292 | 4.87% |
| `Parameter` — the caller picks the type | 775 865 | 4.08% |
| `CallResult` | 274 411 | 1.44% |
| everything else | 395 078 | 2.07% |

**The largest row has no independent cause to fix.** `DroppedCandidate` is a *cascade* — a name
refused only because another refused name appears in its assigned value — and the analysis's own
documentation already says such a name *"wants nothing at all — fixing its root fixes it for free"*.
So 38.41% of these boxes are downstream of the other rows rather than beside them. **NavierStokes is
96.8% cascade**, which is this document's own already-recorded finding arriving from a second
direction: *"one untypable closure variable (`rowSize`) cascades into every `++currentRow`"*. Gameboy
is 94.6% and Typescript 83.8%. *Three suites are one refusal each, wearing a large number.*

**And the largest independent cause is `ElementRead` at 36.35%** — 58.1% of Crypto's, the suite this
phase has called an array workload throughout. ***That is the same conclusion item 3-1's guarded tree
already reaches, at run time, and discards.*** The tree computes `a[i] * b` on raw doubles behind a
type test; the local's analysis refuses the destination because a *static* prover will not type
`a[i]`; so the tree boxes its root to store into a `JSValue` local. **The two mechanisms disagree
about the same expression, and the boxes are the cost of them not sharing a conclusion.**

> **`Unknown` at 12.78% is a gap in the instrument and is reported as one.** It is a destination
> whose name the analysis map does not carry — a store outside a body the analysis ran on, or a name
> resolved by a path that does not go through the function's own binding set. It is not a thirteenth
> cause, and it bounds how sharp the rest of the table can be.

**Where this leaves the tier, and why the next step is a count rather than a build.** The obvious
move is to let a local take the raw `double` the tree already has, guarded — which is **exactly item
3-8a**, built complete, measured, and closed as a regression at 1.012–1.021×. §3.5 records why:
*"a representation change is priced by the read/write ratio of the code it targets, counted before
the representation is built."* 3-8a's population was 26 names refused for a *different* conjunct;
this population is refused for `ElementRead` and is far larger. **So the measurement this hands
forward is that ratio, for these names** — and this phase has now twice been right to take it before
building, and once paid for not having.

#### The read side is not obtainable by wrapping the read — attempted, refused, reverted

`0106` closed by naming the measurement §3.5 requires before a representation change is built: the
**read/write ratio** for the locals refused for `ElementRead`. The write side is already counted —
one box per guarded-tree root stored into a refused local, **6 908 814** of them for that cause. The
read side is what a raw-`double` representation would begin paying, and it splits in two: a read
compiled as a guarded tree's **leaf** costs nothing (the tree already tests the operand and calls
`DoubleValue` on it), and **every other read** would mint a box the engine does not mint today.

**It was built, and it does not work.** The obvious hook is the local's read expression in
`VisitIdentifier`, wrapped in a counting call, gated at *compile* time so the shipping engine is
untouched — the pattern `SpeculativeNumericLocals.Counting` already uses. With it on, the population
it is supposed to measure fell **18 657 518 → 3 147 314 roots, 0.169×, with Gameboy's at exactly
zero**.

***The first reading of that was that the instrument perturbs the tree, and it is worse than
that.*** Exempting the tree's leaves and counting them from inside the tree — after every refusal is
decided, so eligibility cannot move — reproduced the same **0.169×** to three figures. The counters
were not biasing anything; the suites were **failing**:

```text
Crypto/Decrypt: Error: System.NotImplementedException:
  Assignment target Call (BCallExpression) is not supported
  at ILCodeGenerator.VisitAssign ... at montReduce-crypto.js:583
```

**A local's read expression and its assignment target are the same node.** `variable.Expression`
serves both, so wrapping it turns `x++` and `x op= v` into an assignment whose target is a method
call, which the IL backend rejects outright. The collapsed counts were aborted suites, and the
`0.169×` was the share of the corpus that happened to compile.

**So the ratio is not measured, and this is what stands instead.** The write side holds
(`0106`, unaffected — it is counted at the boxing factory, not in the emitted read). The read side
needs a hook that is *not* the expression the assignment path writes through: the tree's own leaf
save is one such position and yields the free half only; the boxing half has no equivalent, because
an ordinary local read is a bare CLR load with nothing to hang a counter on. **Item 3-1's remaining
question is therefore still open, and now has a named obstacle rather than a plan.** The work is
reverted; nothing of it is in the pin or in `patches/`.

> ***The reason to record a reverted instrument at all is that its first reading was wrong in the
> flattering direction.*** A 0.169× population looks like a subtle measurement bias one could argue
> about, correct for, or quote with a caveat; it was a crash. Two runs and a log line separate those,
> and only the second is a reason to stop. *An instrument that changes its own population by 83%
> should be assumed broken before it is assumed biased.*

#### The free half, counted from the one safe position — `0107`

The whole read side is unobtainable for the reason above. **The guarded tree's leaf save is the one
read position with neither problem**: the value is the right-hand side of an assignment into a fresh
temporary, so it is never an assignment target, and `BuildOrderedTree` runs only *after* every
refusal has been decided on the syntax, so a counter there can change neither what compiles nor
which trees specialize. A guarded leaf is also exactly the read a raw `double` would serve for
free — the tree already tests the operand for `IsNumber` and calls `DoubleValue` on it.

**Both safety claims are checked rather than argued, which is the lesson of the reverted attempt.**
Re-running the census with the counter on reproduces the roots-consumed-by-a-refused-local count —
**18 657 518 against 18 657 815, and 1.000 on every suite individually** — and the three suites that
fail under it (RegExp, Mandreel, zlib) fail identically with it off. *Last time the population moved
83% and the reason was a crash; this time it does not move at all.*

| Refusal | boxed writes | free leaf reads | free reads per write |
|---|--:|--:|--:|
| `CallResult` | 274 411 | 3 632 630 | **13.24** |
| `Parameter` | 775 877 | 10 002 325 | **12.89** |
| **`ElementRead`** | **6 908 985** | **14 799 912** | **2.14** |
| `PropertyRead` | 925 292 | 836 856 | 0.90 |
| `DroppedCandidate` | 7 300 576 | 5 249 912 | 0.72 |
| `NeverOffered` | 241 567 | 123 850 | 0.51 |
| **total** | **16 426 708** | **34 645 485** | **2.11** |

**The shape is a property of the workload, not of the cause.** Within `ElementRead` alone, Crypto
reads 1.98 times per write, **Gameboy 31.06** and **Box2D 0.05** — three orders of magnitude apart
under one heading, which is the same warning §4.2a gave about quoting a corpus share.

***This does not decide the item, and the most useful thing about it is why not.*** The break-even
condition for a raw-`double` representation is the **boxing** reads — the ones a `JSValue` consumer
forces — and those are precisely what has no safe hook. Free reads are neutral: they neither cost
nor save. **Item 3-8a is the standing warning against reading 2.11 as encouraging**: it lost at
1.012–1.021× with **393 705 boxes minted at the read against ≈5 300 removed**, and no count of the
reads it served for free would have predicted that. *A rich free-read population is what a favourable
workload looks like and also what 3-8a's looked like.*

**So what `0107` adds is a bound and a ranking, both weaker than a decision.** Total reads of these
locals are **at least 34.6 M**; the representation breaks even only if fewer than 16.4 M of the
remainder need a box. And the causes rank by how tree-resident their locals are — `Parameter` and
`CallResult` most favourable and both small, `ElementRead` middling at 2.14 and carrying the
population. *The item stays open, one measurement short, and the missing measurement is still the
one the compiler cannot be asked for safely.*

#### The cost side, counted at the CONSUMERS — and the ratio comes back in favour — `0108`

The read cannot carry a counter because it is also the assignment target. **A consumer's operand
can**, because it is a value and nothing else — an argument, a stored value, a returned value. And
`VisitConsumedBy` is already the single choke point for the five consumer categories `0105` plumbed,
so one hook covers an assignment's right-hand side into an element, a property or a local, a call
argument, and a return.

**Non-perturbation checked before anything was read from it**, which is now the standing order in
this item: the roots-consumed-by-a-refused-local count reproduces at **18 657 518 against
18 657 828, 1.000 on every suite**, and the three failing suites fail identically with it off.

***It is a LOWER BOUND on the cost and is quoted as one.*** Every `JSValue` consumer outside those
five — a generic operand, a member base, a condition, a comparison, a literal element — is missing.
**That direction is the useful one**: a bound *above* the saving refutes the representation outright,
while a bound below it does not confirm it.

| Refusal | saving (boxed writes) | cost ≥ (consumer reads) | cost / saving | |
|---|--:|--:|--:|---|
| `CallResult` | 274 412 | 2 581 505 | **9.41** | **refuted** |
| `NeverOffered` | 241 567 | 899 721 | **3.72** | **refuted** |
| `PropertyRead` | 925 292 | 1 689 299 | **1.83** | **refuted** |
| `Parameter` | 775 889 | 96 056 | 0.12 | open |
| **`ElementRead`** | **6 908 985** | **293 259** | **0.04** | **open** |
| **`DroppedCandidate`** | **7 300 576** | **208 905** | **0.03** | **open** |
| **total** | **16 426 721** | **5 768 745** | **0.35** | |

**Three causes are refused at the bound, and they are the small ones** — 1.44 M writes between them.
**The two carrying 14.2 M of the 16.4 M come in at 0.04 and 0.03**, which means the un-instrumented
consumers would have to supply **twenty-five times every read counted anywhere** to reach break-even.

**Per suite it is the same shape, and it lines up with where the boxes are:**

| Suite | writes | consumer reads | ratio |
|---|--:|--:|--:|
| Crypto | 9 547 789 | 194 988 | **0.02** |
| Gameboy | 2 778 674 | 98 880 | **0.04** |
| PdfJS | 1 420 349 | 4 416 | **0.00** |
| **NavierStokes** | 1 181 190 | **0** | **0.00** |
| Box2D | 1 100 346 | 254 317 | 0.23 |
| RayTrace | 35 028 | 146 623 | 4.19 |
| Typescript | 342 102 | 3 826 237 | 11.18 |
| EarleyBoyer | 21 243 | 751 019 | 35.35 |

***NavierStokes' refused locals are read only inside trees — zero instrumented boxing reads at
all***, which is the strongest case in the corpus and the suite whose refusals are 96.8% cascade,
i.e. the ones the analysis's own note says fixing a root fixes for free.

**This is the first affirmative evidence phase 3 has produced for widening the numeric tier**, and
every previous item in the phase that felt this good was wrong, so the qualifications are the
important part:

- **The cost is a lower bound, not the cost.** 0.04 becomes 1.00 if the un-instrumented consumers
  are 25× the instrumented ones. That is a lot, and it is not impossible.
- **Item 3-8a is the standing counter-example and it is not answered by this.** It lost with
  393 705 boxes minted at the read against ≈5 300 removed — a ratio of ~74:1 *against* — which no
  count taken before it was built had produced. What is different here is that a ratio *has* been
  taken and it is 0.03–0.04 on the population that matters; what is the same is that it is being
  read off an instrument rather than off a shipped change.
- **The three refuted causes should be excluded by construction if anything is built**, rather than
  discovered later: a widening that admits `CallResult` locals is buying 274 412 boxes for at least
  2 581 505.

**So the item is, for the first time, pointed at something specific and bounded**: widen the numeric
tier for names refused by `ElementRead` and by cascade from one, exclude `CallResult`,
`NeverOffered` and `PropertyRead`, and expect the saving to be bounded above by 14.2 M boxes —
**8.4% of the corpus's 164.6 M boxing requests**, and *worth building only if a wall-clock A/B says
so*, which §3.5 and `0086`'s rate lesson both insist on.

#### The widening built, and measured as a regression — the saving is not where the mechanism can reach it — `0109`

`0108` selected a population by measurement rather than by argument: `ElementRead` at a cost/saving
of **0.04** over 6 908 985 boxed writes, the cascade at **0.03** over 7 300 576, and `CallResult`,
`NeverOffered` and `PropertyRead` refuted at the bound and therefore excluded by construction. **It
is built.** An element read is not provably numeric, so the widened names go to item 3-8a's dual
representation and never to the sound tier; the cascade needs no rule of its own, because the pass
is a fixed point. One assume flag, one `IsNumeric` arm, one extra pass.

**Measured against the same build with the switch off, counters on, all fourteen suites:**

| | off | on | |
|---|--:|--:|--:|
| boxing requests | 124 693 165 | 132 273 724 | **1.061** |
| boxes allocated | 66 982 650 | 69 582 935 | **1.039** |
| *Gameboy's requests* | *52 835 472* | *59 321 464* | ***1.123*** |

***A regression, and not a small one.*** But the two counters that decide it say the cause is **not**
the read/write ratio `0108` measured:

| | off | on |
|---|--:|--:|
| roots consumed by a refused local — *the saving* | 18 657 804 | **18 656 936** |
| speculative-read boxes — *the cost* | 0 | **7 692 133** |

***868 of the 18.7 M writes the population was selected for are actually removed.*** The saving was
never collected; only the cost arrived.

**The mechanism cannot collect it, and this is the finding.** The assignment path tests
`NumericStorage` — which a *speculative* local does not have — so it still asks for the right-hand
side as a `JSValue`, the tree still boxes its root, and `AssignToSpeculativeVariable` unboxes it
again. **3-8a built raw arms for the tree's LEAF, the element read and the element write, and none
for the tree's ROOT** — which is the one site the entire saving lives at. Item 3-8a's own
re-specification lists its three consumers and that absence is not remarked on anywhere, because
until `0105` nothing had counted what the root's consumer was.

> ***`0108`'s 0.04 was a true measurement of the opportunity and a measurement of nothing about
> whether any available mechanism could take it.*** The ratio said *"if these locals held raw
> doubles, the reads would be nearly free"* — which is still true — and the tier's representation
> has no way to put a raw double into one at the site that mints the box. **A cost/benefit ratio
> prices an outcome; it does not establish that a mechanism reaches the outcome, and this phase has
> now spent two items discovering that separately.**

**Status.** The widening is **off by default and stays off**, kept as the arm a store-path change
would be tested against — the same disposition item 3-8a has, and for a sharper reason: 3-8a was
refuted by its population's read/write ratio, and this is refused by a missing consumer that is
nameable in one sentence. **The next step, if anyone takes it, is a raw arm for the tree's root
into a speculative local**: emit `raw = <native>, flag = true` on the guarded arm and
`slot = <generic>, flag = false` on the other, and box nothing. Whether *that* wins is then
`0108`'s ratio question again, and this time with the saving actually reachable.

#### The raw arm built — the saving is collectable, and the item is refuted anyway — `0110`

`0109` left one sentence of work: *"a raw arm for the tree's root into a speculative local — emit
`raw = <native>, flag = true` on the guarded arm and `slot = <generic>, flag = false` on the other,
and box nothing."* **Built**, at the assignment and at the declarator, in statement position only —
the line item 3-3's `NumericStoreResult` already draws, and for the same reason.

**It works.** Roots consumed by a refused local go **18 657 804 → 16 225 570, 0.870× — 2 431 366
boxes removed**, against `0109`'s 868. The missing consumer was the whole of that defect.

**And the item is refuted anyway.**

| against the widening-off arm | `0109` | `0110` |
|---|--:|--:|
| boxing requests | 1.061× | **1.041×** |
| boxes allocated | 1.039× | **1.039×** |
| roots into a refused local | 1.000× | **0.870×** |
| speculative-read boxes | 7 692 133 | **7 692 133** |

***The cost did not move at all.*** The saving is **2.4 M** and the cost is **7.7 M**, so completing
the mechanism converted a regression caused by collecting nothing into a regression caused by
collecting a third of what it pays for. **Off by default and staying off.**

**Two things this settles that no earlier count in this item could.**

**`0108`'s consumer-side bound was 25× too low, and structurally rather than by bad luck.** It counted
reads at five consumer positions and called the result a lower bound on the cost. The cost is a box
at **every** read of a speculative local that is not one of the three raw-capable consumers, minted
at the local's **own read expression** — precisely the site `0107` established has no safe hook. *A
lower bound taken at the wrong sites is not a loose bound on the right quantity; it is a bound on a
different quantity*, and nothing about its being a bound protects it from that.

**And the saving was never 14.2 M.** A refusal census attributes a name to its **first** cause, so
removing that cause admits the name only if it was the **only** blocker. `var t = a[0] * b + i` with
a parameter `b` is charged to `ElementRead` and is *still* refused once element reads are assumed
numeric, because `b` blocks it independently. That is why 6.9 M `ElementRead` writes yield 2.4 M
removable boxes. **`0106`'s table ranks refusals correctly and does not — and never claimed to —
measure what removing one would admit.**

> **What the three attempts share.** 3-8a priced a representation at the local and lost on reads;
> `0109` priced it on a measured ratio and collected nothing; `0110` completed the mechanism and
> still lost on the same reads. *Every time, the cost has been the boxes minted reading a
> dual-representation local, and every time it has been measured last.* If a fourth attempt is made,
> the read cost is the first thing to count and the only safe way found to count it is to build the
> representation and read `boxingSpeculativeReadRequests` — which is what this patch now makes cheap
> to do for any candidate population.

#### The read cost counted FIRST, for a fourth population — and it is the third one again — `0111`

`0110` closed with a method rather than a plan: *"the read cost is the first thing to count and the
only safe way found to count it is to build the representation and read
`boxingSpeculativeReadRequests`."* **This is that method run once**, on the parameter population,
with the counter read before anything else.

**Why parameters.** Item 3-3 records `parameter` as its one category that *"cannot reach the numeric
tier at all"*; item 3-8a deliberately excluded them (*"they want a guard at entry rather than at an
initializer"*); `0106` weighted the refusal at **775 877** boxed writes; and `0107` found their
locals the **most tree-resident of any cause, 12.89 free leaf reads per write**. *That last number is
exactly the kind that has flattered all three previous attempts*, which is the reason to count the
cost before believing it.

**Counted first:**

| | |
|---|--:|
| **speculative-read boxes — the cost** | **417 582** |
| roots consumed by a refused local | 18 657 804 → **18 962 176** |
| **the saving** | **−304 372** |
| corpus boxing requests | **1.003×** |
| corpus allocations | **1.006×** |

***The saving is negative.*** Admitting more speculative names makes more trees eligible —
`CountSpeculativeLeaves` is a term in the eligibility sum — and each new tree mints a root. So the
population pays 417 582 boxes to mint 304 372 more. **Refuted on one measurement, with no
build-then-diagnose cycle**, which is the entire point of the ordering.

***And the per-suite column carries the real finding.*** NavierStokes mints **exactly 393 705**
speculative-read boxes — *the number this document already records for item 3-8a's failure, to the
digit*. On the suite that decides these items, **the fourth population is the third one wearing a
different refusal**, and the counter says so before any wall clock was taken. Gameboy supplies the
negative saving almost entirely (−313 623 roots) and Crypto contributes nothing at all.

> **Three of `0108`'s conclusions are void rather than confirmed.** That patch refused
> `PropertyRead`, `CallResult` and `NeverOffered` on the consumer-side bound, and `0110` established
> that the bound was on a different quantity. **Those three are un-measured again, not eliminated** —
> and each is now one flag and one run away from an answer, which is what the method buys.

**What phase 3 has after four attempts at this mechanism.** 3-8a priced it at the local and lost on
reads; `0109` priced it on a ratio and collected nothing; `0110` completed the mechanism and lost on
the same reads; `0111` counted the reads first and refused before building anything. *The cost has
been the same quantity every time — boxes minted reading a dual-representation local — and the only
thing that has changed is how early it was known.* **The remaining populations are cheap to test and
none is promising**; the dual representation should be considered refuted as a general mechanism on
this corpus rather than as four unlucky populations.

#### The remaining populations, tested — and `0108`'s ranking was not merely low but inverted — `0112`

`0110` voided `0108`'s consumer-side refusals of `PropertyRead`, `CallResult` and `NeverOffered`.
Two of the three are expressible as assumptions and are measured here by `0110`'s method — build the
representation behind a flag, read `boxingSpeculativeReadRequests` **before anything else**.

| population | cost (spec. reads) | saving (roots) | requests | allocations |
|---|--:|--:|--:|--:|
| `Parameter` (`0111`) | 417 582 | **−304 372** | 1.003× | 1.006× |
| **`PropertyRead`** | **3 828 813** | 72 980 | 1.030× | **1.045×** |
| **`CallResult`** | **913 011** | 431 131 | 1.004× | 1.006× |

***All three refuted. Every population tried costs more than it saves — four of four.***

**And `0108`'s bound was not merely low; it was inverted.** It predicted `CallResult` **9.41** and
`PropertyRead` **1.83**, ranking `CallResult` as the worse of the two by five times. Measured,
`PropertyRead` is **52.5** and `CallResult` **2.12** — *the reverse, by twenty-five times*. `0110`
could say the bound landed on a different quantity; this says the quantity it landed on **does not
even preserve the ordering** of the one it stood in for. *A bound taken at the wrong sites is not a
conservative version of the right answer, and cannot be used to rank.*

**`NavierStokes` mints exactly 393 705 speculative-read boxes under `CallResult`** — the same figure
it mints under `Parameter`, and the same one this document records for item 3-8a. **Three
populations, one number.** On the suite that decides these items they are the same handful of names
reached by three different assumptions, which is why the mechanism keeps failing the same way.

> **`NeverOffered` is not testable by this method, and the reason is structural rather than effort.**
> The cause means the *declaration* is non-numeric — `var a = []`, `var s = ''` — so there is no
> assumption about a value source that admits it; the fixed point drops it on its own initializer.
> Holding it speculatively is not a widening of the analysis but a decision to represent *every*
> non-numeric local speculatively, a different proposition. Its ceiling is **241 567 boxed writes,
> 1.5%** of the 16.4 M, against costs of 0.4–3.8 M on every population measured. **Argued, not
> measured, and labelled as such.**

**Item 3-1's dual-representation line is closed.** Four populations, four refutations, one shared
failure mode, and the last three measured for the price of one run each. *What the item produced is
not a speed-up; it is a method for refusing one cheaply, and the sequence `0106`→`0107`→`0108`→`0110`
is worth reading backwards by anyone who proposes the next representation change in this engine.*

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

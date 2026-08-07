# Phase 2 — the call and property paths

Every item landed or closed. The Richards / DeltaBlue / Box2D cluster, the inline caches, the property map, and the two items that were closed on measurements rather than built.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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
> and pushed, and it is now **`e6222df3`**, an ancestor of `61c8cc65` (the pin at the time). Measurement
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
> and pushed, and it is now **`0812d80d`**, an ancestor of `61c8cc65` (the pin at the time).

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
> and pushed, and it is now **`4d1c4796`**, an ancestor of `61c8cc65` (the pin at the time).

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
> and pushed, and it is now **`fb1e2f4c`**, an ancestor of `61c8cc65` (the pin at the time).

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

#### 2-13 · Where the rest of DeltaBlue's gap is — **measured against the third engine, and it is mostly not Broiler's**

2-12 left the item in a stated but unexplored state: *"whatever is left is not property-cache-shaped,
and the remaining gap now has to be looked for somewhere other than phase 2's subject."* Four
explanations had been eliminated and the suite still failed the gate at 447×, now **399.5×** on the
committed run. This is the look, and it needed **no new instrument** — the answer was in the column
every committed run has carried since Jint was added and nobody had divided.

**The question was asked the wrong way round.** *"Why is Broiler 400× slower than Chromium on
DeltaBlue when it is 141× on Richards?"* presumes the 2.83× between them is Broiler's. Jint is a
managed interpreter with no JIT at all, on the same runtime, in the same run — so asking it the same
question separates *"DeltaBlue is hard for this engine"* from *"DeltaBlue is a suite V8 does
unusually well on"*:

| | Chromium ÷ Broiler | Chromium ÷ Jint |
|---|--:|--:|
| Richards | 141.3× | 203.8× |
| DeltaBlue | **399.5×** | **521.6×** |
| **DeltaBlue ÷ Richards** | **2.83×** | **2.56×** |

**Only 1.10× of the 2.83× is Broiler's own** — and it reproduces on the previous committed run,
independently, at **1.118×** (Broiler 3.04×, Jint 2.72×). *Both managed engines fall behind V8 on
DeltaBlue by nearly the same multiple that they fall behind it on Richards, plus a tenth.*

**The consequence is a bound on the item rather than a lead inside it.** Closing the whole
Broiler-specific residue takes DeltaBlue from **399.5× to 362×** (395× on the older run's numbers).
The gate is **200×**. So **phase 2's exit criterion on DeltaBlue is not reachable by removing a
Broiler-specific deficiency at all** — meeting it requires beating Jint on that suite by a further
~1.8×, which is not a claim any phase 2 item was written to make, and not one the phase's machinery
is shaped to deliver. *An exit criterion expressed as a ratio to another engine inherits that
engine's behaviour, and this one has been read for three sessions as though it were a statement
about ours.*

**The method is checked before it is trusted**, because a ratio of ratios will produce a number for
any pair of suites whether or not it means anything. Across the committed run it separates by two
orders of magnitude and reads in both directions:

| Suite | Chromium ÷ Broiler | Chromium ÷ Jint | Jint ÷ Broiler | Reading |
|---|--:|--:|--:|---|
| MandreelLatency | 5 331.8× | 98.1× | **54.3** | Broiler-specific, and the largest in the suite |
| CodeLoad | 211.4× | 5.6× | **37.8** | Broiler-specific — the front end |
| zlib | 182.8× | 15.2× | **12.0** | Broiler-specific |
| PdfJS | 111.4× | 69.5× | 1.60 | mildly Broiler-specific |
| RegExp | 78.2× | 49.0× | 1.60 | mildly Broiler-specific |
| **DeltaBlue** | **399.5×** | **521.6×** | **0.77** | **Broiler ahead of Jint** |
| Richards | 141.3× | 203.8× | 0.69 | Broiler ahead of Jint |
| Mandreel | 271.2× | 504.3× | 0.54 | Broiler well ahead |
| Crypto | 116.3× | 253.4× | 0.46 | Broiler well ahead |

**Broiler is *ahead* of Jint on DeltaBlue** (0.77), by almost the same margin it is ahead on
Richards (0.69). The three suites where it is genuinely, differentially behind are the front end and
latency — exactly where §1.1 says the structural gap is, and none of them is DeltaBlue.

**And the obvious remaining explanation is falsified by a second control in the same table.** Item
4-1 records DeltaBlue as the worst read case at 77.10% monomorphic with 43 polymorphic read sites
against Richards's 1, which reads like the answer. It is not: **Crypto is 73.82% monomorphic — worse
than DeltaBlue — with 25 polymorphic sites carrying 26.2% of its reads, and Crypto is Broiler's
*best* suite against Jint at 0.46×.** Read polymorphism does not predict the gap in either
direction.

| Suite | read observations | monomorphic | polymorphic sites | polymorphic share |
|---|--:|--:|--:|--:|
| EarleyBoyer | 5 490 829 | 100.0% | 0 | 0.0% |
| Richards | 605 672 | 96.74% | 1 | 3.3% |
| Box2D | 25 963 010 | 94.12% | 247 | 5.9% |
| RayTrace | 2 919 249 | 94.06% | 37 | 5.9% |
| **DeltaBlue** | 1 001 675 | **77.10%** | **43** | **22.9%** |
| **Crypto** | 1 891 092 | **73.82%** | 25 | **26.2%** |

**What is left that phase 2 or phase 4 could still reach, priced.** 4-2b's tier-2 specialization
emits a *monomorphic* read — a shape guard plus a direct slot load — so by construction it reaches
at most **77.1%** of DeltaBlue's reads, and a polymorphic form would add the remaining **22.9%**.
Against 4-5's corpus-wide finding that reads are **9.16%** of execution time, that is **≈2.1% of
DeltaBlue's time**. Worth knowing before anyone builds a polymorphic tier-2 read on DeltaBlue's
account; not worth building on it. (The 9.16% is a corpus figure, not DeltaBlue's own, and is used
here only as an order of magnitude.)

**So 2-10 closes as measured.** The suite kept its own item through four eliminations and two real
defects — `push` costing every array its shape, the redundant prototype write, the un-replaceable
cache entry — every one of which was worth fixing on its own terms, and none of which was the 400×.
The 400× is largely V8's win rather than Broiler's loss, the residue is a tenth, and **what the item
should hand forward is a question about the gate rather than a lead inside the suite**: whether
*"inside 200× of Chromium"* is the right acceptance test for a benchmark on which Chromium is
2.56× further ahead of a plain interpreter than it is on the suite beside it. Recorded as a reading
for the plan to decide, not changed here.

---

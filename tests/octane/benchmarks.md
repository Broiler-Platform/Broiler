# What each Octane benchmark measures — and what blocks Broiler on it

Companion to [`README.md`](README.md), which covers *how* the harness runs. This
document covers *what is being run*: what each of Octane 2.0's benchmarks
actually does, which engine subsystem it puts under load, and — using the
committed results in [`results/`](results/) — where Broiler's time goes.

Numbers quoted for Broiler are from
[`results/comparison.md`](results/comparison.md) (generated 2026-07-31,
Chromium 149.0.7827.55 vs the `BroilerJS --script-host` shell), except where a
score is marked *(0046)*.

> **The committed results are stale.** They were generated 2026-07-31 20:28,
> against a `Broiler.JS` pointer that predated the engine fixes for the five
> failing suites. Those fixes landed at `7ef80c03` and the pointer was bumped to
> `cdb2fd41` in `2d9f39ca` on 2026-08-01 11:45 — about 15 hours later. Nobody has
> re-run the workflow since, so `results/` still shows Crypto, PdfJS, CodeLoad,
> zlib and Typescript failing on an engine that no longer has those defects.
>
> A score marked *(0046)* is the value measured on the fixed engine at the time
> the fix was written, recorded in the patch index that has since been cleared.
> Treat those five as the expectation for the next run, not as a published
> result.

---

## 1. How Octane is structured and scored

Octane 2.0 is 15 benchmark files that produce **17 scores** — `Splay` and
`Mandreel` each report a throughput score *and* a separate latency score.

Each benchmark is normalized against a **reference time** baked into its source
(roughly what V8 took on a reference machine in 2012) and reported as
`100 × reference / measured`, so **higher is better** and a score is a *relative
throughput number*, not milliseconds. The suite total is the **geometric mean**
of the individual scores, which is why one catastrophic score drags the total
far more than one great score lifts it.

Two consequences worth keeping in mind when reading the committed results:

- **The headline totals are not like-for-like.** Chromium's 57245 is a geomean
  over all 17 scores; Broiler's 245 is a geomean over the 12 it completed.
  Restricted to the same 12, Chromium scores 58419 — so the honest headline is
  **≈238× slower on the suites Broiler finishes**, and the ratio would be worse,
  not better, with the missing five included at their measured values.
- **Octane was retired by its authors in 2017**, on the grounds that engines had
  started optimizing for its specific shapes rather than for the web. That makes
  it a poor target to *tune against* and an excellent one to *shake out* a young
  engine with: it is 15 large, real, self-contained programs that exercise almost
  every path an engine has, and it either runs them or it does not.

---

## 2. The benchmarks

### Richards — OS kernel scheduler simulation

**Origin.** Martin Richards' classic BCPL benchmark, ported to JavaScript by
Google. ~500 lines.

**What it does.** Simulates an operating-system task scheduler. Four kinds of
task (an idler, a device, a worker, and two handlers) are held in a
priority-ordered linked list of `TaskControlBlock` objects. The scheduler walks
the list, and each `TaskControlBlock.run()` dispatches on the task's state to the
right task object, which consumes and produces `Packet` objects that are
themselves threaded onto linked queues. The whole thing is one tight loop of
pointer chasing, small-integer arithmetic, and virtual dispatch.

**What it measures.** Property access and **call overhead**, in about the purest
form the suite offers. There is almost no arithmetic and no allocation of note —
just millions of `this.x` reads and method calls through a handful of distinct
object shapes. Engines win here through monomorphic inline caches plus
aggressive inlining of tiny methods.

**Broiler: 108 vs 46754 — 433× slower.** The second-worst ratio of any
throughput benchmark, and it should be read as a direct measurement of per-call
and per-property-access cost. Both are known open items: shape tracking is
gated on `GetType() == typeof(JSObject)` so nothing exotic participates, there is
no shape-transition cache (so the constructor that builds each
`TaskControlBlock` misses on *every* field it creates), and each call still goes
through `JSFunction.InvokeFunction` with a real CLR call and argument marshalling
where V8 would have inlined the callee entirely.

### DeltaBlue — incremental constraint solver

**Origin.** Written in Smalltalk by John Maloney and Mario Wolczko; the standard
JS port.

**What it does.** Builds a chain of variables linked by one-way constraints
(`EqualityConstraint`, `ScaleConstraint`, `EditConstraint`, `StayConstraint`) and
then repeatedly perturbs one end, forcing the planner to re-satisfy the whole
chain. Satisfying a constraint means a topological walk that decides which
variable each constraint should compute, so the hot loop is graph traversal over
many small, short-lived objects.

**What it measures.** **Polymorphic dispatch through a deep prototype
hierarchy.** Nearly every call site sees several receiver shapes
(`UnaryConstraint`/`BinaryConstraint` subclasses), and the code is written in the
prototype-chain style, so method lookups walk prototypes rather than hitting own
properties. It is the suite's stress test for whether an engine's inline caches
survive polymorphism and whether prototype lookup is cached at all.

**Broiler: 171 vs 102708 — 601× slower, the worst throughput ratio in the
suite.** This is the clearest single signal in the whole run. It says the
prototype-method-call path is where Broiler is furthest from V8 — consistent with
the engine's own measurements, where inherited-method call sites had a **0%**
inline-cache hit rate before P1-2 and dictionary-mode fallbacks were being taken
200 006 times per 200 000 iterations. P1-2 fixed the hit rate; the ratio here
says what remains is the cost of the call itself, not of finding the method.

### Crypto — RSA encryption/decryption

**Origin.** Tom Wu's `jsbn` bignum library.

**What it does.** Generates an RSA key and runs encrypt/decrypt rounds. All the
work is in `BigInteger`, which represents numbers as arrays of 28-bit digits and
implements multiplication, division and modular reduction (`am3`, `divRemTo`,
`montgomery reduction`) as tight loops over those arrays doing shifts, masks and
integer multiplies.

**What it measures.** **Integer arithmetic and dense array indexing.** The inner
loops are the closest JavaScript gets to C: `a[i] * b[j] + c` with `&`, `|`,
`>>`, `<<` and `Math.floor` everywhere. Engines win by keeping values as untagged
int32, keeping the digit arrays in unboxed integer element storage, and eliding
bounds checks.

**Broiler: 127 *(0046)* vs 38183 — 301× slower.** Two separate stories here.
First, without patch 0046 this suite does not score at all: `if (r == null)` on a
`BigInteger` ran `ToPrimitive` on the object (spec says an Object compared with
null is `false` with no coercion), which reached `toString → toRadix → divRemTo →`
the same test, recursing until the stack was exhausted and killing the process.
Second, once it runs, the 301× is the boxing tax in its purest form: every digit
that leaves a local or enters an array becomes a heap-allocated `JSValue`.

### RayTrace — ray tracer

**Origin.** Adam Burmister's JavaScript ray tracer.

**What it does.** Renders a small scene (spheres and a checkerboard plane, with
shading, shadows and reflections) at low resolution. Classes are built with a
`Class.create()` helper in the Prototype.js style. The inner loop is vector
math — `Vector.prototype.add/subtract/multiplyScalar/normalize/dot/cross` — and
crucially **each of those returns a new `Vector`**.

**What it measures.** Double-precision FP arithmetic *plus* a very high rate of
short-lived small-object allocation. It is the suite's escape-analysis benchmark:
an engine that can prove those intermediate `Vector`s never escape stack-allocates
them and the allocation disappears; an engine that cannot pays for every one.

**Broiler: 403 vs 117436 — 291× slower.** No escape analysis, and doubles are
boxed once they land in an object field, so each vector operation costs a
`JSObject` plus three `JSValue`s. The unboxed-`double`-locals work from P2-2 does
not reach here: its eligibility gate is a function-top-level `var` not named by any
nested closure, which excludes object fields entirely.

### EarleyBoyer — Scheme benchmarks, machine-translated

**Origin.** Two classic Scheme programs — an Earley chart parser and the Boyer
rewrite-rule theorem prover — compiled to JavaScript by Florian Loitsch's
Scheme2Js.

**What it does.** Parses grammars and proves theorems, but the shape of the code
is what matters: it is *generated*, so it is built out of cons-cell linked lists
(`sc_Pair`), interned symbols, deeply nested closures, and `try`/`catch` used for
control flow. It allocates enormously and almost all of it dies young.

**What it measures.** **Allocation throughput and young-generation GC.** It is
the benchmark that most directly rewards a fast bump-allocator with a cheap
scavenger, and it punishes any per-allocation bookkeeping.

**Broiler: 339 vs 91547 — 270× slower**, and at 33.5 s wall clock it is the
slowest suite Broiler actually completes apart from Mandreel. The relevant history
is P0-1: every `JSValue` construction used to call
`JSObject.NotifyPrototypeChainMutation()` unconditionally — so *allocating a
number invalidated every inline cache in the realm*. That is fixed; what remains
is that a cons cell is a `JSObject` with dictionary-or-shape property storage
rather than a two-word cell, and every element in it is a separately allocated
`JSValue`.

### RegExp — real-world regular expressions

**Origin.** Generated by extracting regular-expression operations from 50 of the
most popular web pages of the era.

**What it does.** Runs a large corpus of real regexes — URL parsers, user-agent
sniffers, whitespace trimmers, HTML tag matchers — against representative input,
using `test`, `exec`, `replace` (including function callbacks), `split` and
`match`, with and without the global flag.

**What it measures.** The **regular-expression engine**, end to end: pattern
compilation, the matcher itself, capture-group handling, and the string
allocation around `replace`. V8 compiles regexes to native code with Irregexp.

**Broiler: 89.9 vs 9890 — 110× slower.** Note that Chromium's *absolute* score
here is the lowest of any benchmark (9890), because the reference time was set
against an already-fast Irregexp — so 110× is measured against a strong baseline.
`Broiler.Regex` is a from-scratch ECMAScript engine whose matcher is a
**backtracking interpreter** over a parsed program, with no compilation to native
code and no equivalent of Irregexp's one-pass or bytecode-specialized paths. That
gap is structural, not a tuning issue. The same engine is on the critical path
for PdfJS and Typescript, which lean on regexes heavily.

### Splay — splay-tree manipulation

**Origin.** Written for the V8 benchmark suite specifically as a GC test.

**What it does.** Builds a splay tree of ~8000 nodes keyed by random numbers,
each carrying a payload of nested objects and strings, then runs a long loop that
inserts a new node and removes an old one on every iteration. A splay tree
*rebalances on every access*, so the pointer structure of a large, long-lived
object graph is being continuously rewritten.

**What it measures.** **The old generation.** Unlike EarleyBoyer, the data here
survives, so it gets promoted, and then the mutation rate forces the GC to keep
tracing and rewriting a big live set. This is the benchmark that motivated
incremental marking in V8.

**Broiler: 283 vs 43027 — 152× slower.** Better than the suite median, which is
the interesting part: the .NET GC handles this workload comparatively well. The
loss is the per-node cost of building the payload objects, not the collector.

### SplayLatency — worst-case pause during Splay

**What it does.** Same workload; different statistic. Instead of total
throughput, Octane records the time of each individual iteration and scores the
**distribution's tail** — how bad the worst pauses were.

**What it measures.** Whether the GC's work is *incremental*. An engine that
stops the world for one long mark-and-sweep scores badly here even if its total
throughput is fine.

**Broiler: 1539 vs 69725 — 45×, by a wide margin Broiler's best result in the
suite.** Worth stating plainly because it is the one axis where the .NET runtime
is doing Broiler a favour: the background/concurrent GC's pause distribution is
genuinely competitive, and the 45× is mostly the throughput deficit showing
through rather than a pause problem. **The GC is not currently a primary
blocker.**

### NavierStokes — 2D fluid dynamics

**Origin.** Oliver Hunt's JavaScript port of Jos Stam's stable-fluids solver.

**What it does.** Simulates fluid on a 2D grid stored in flat arrays of doubles:
a diffusion step, a projection step (a Gauss–Seidel linear solver run to a fixed
iteration count), and an advection step with bilinear interpolation. Nested
counted loops over `field[i + j * width]`, all in double precision.

**What it measures.** **Unboxed double arrays and loop quality** — bounds-check
elimination, strength reduction on the index arithmetic, keeping doubles in
registers across the loop body. It is the suite's numeric-kernel test and the one
closest to what a JIT's loop optimizer is for.

**Broiler: 341 vs 35432 — 104× slower**, one of the better ratios. The
`NumericLoopPlanner` and the unboxed-`double`-locals work are visible here: the
loop *counters* and scalar accumulators can stay raw `double`s. What cannot is the
grid itself — every `field[i]` read materializes a boxed `JSValue`, and dense
element storage holds `JSValue` references rather than raw doubles.

### PdfJS — PDF parsing and rendering

**Origin.** Mozilla's PDF.js, with the canvas back-end stubbed.

**What it does.** Parses a real PDF document embedded in the benchmark, walks its
object graph (streams, dictionaries, cross-reference tables), decodes the content
streams, and interprets the drawing operators.

**What it measures.** A **large, real, mixed-workload application**: byte-level
parsing over `Uint8Array`s, string building, dictionary-shaped object lookups
with dynamic keys, regexes, and a big class hierarchy — plus the sheer volume of
code, which tests parse and compile time as well.

**Broiler: 321 *(0046)* vs 58725 — 183× slower.** Also a suite that scored
nothing before patch 0046, for an instructive reason: `undefined + x`
string-concatenated instead of adding numerically, so
`this.end = (start + length) || bytes.length` with both arguments omitted stored
the truthy string `"undefinedundefined"`; every stream then reported a NaN length
and the parser rejected the document as malformed. One arithmetic-coercion bug,
one whole benchmark.

### Mandreel — Bullet physics, compiled from C++

**Origin.** The Bullet physics engine, compiled from C++ to JavaScript by the
Mandreel compiler (a contemporary of Emscripten).

**What it does.** Runs a rigid-body physics simulation. The *source* is what
matters: one gigantic machine-generated file with a simulated heap in typed
arrays, `|0` and `+x` coercions on every operation, indirect calls through
function tables, and individual functions of preposterous size.

**What it measures.** **Typed-array heap access, indirect calls, and the ability
to compile enormous functions at all.** V8 recognizes the asm.js-shaped
type coercions and compiles to unboxed integer/double code.

**Broiler: 160 vs 47996 — 300× slower, and 313 seconds of wall clock** — it alone
is longer than every other suite combined and needs the workflow's 1800 s
per-suite timeout. Also the suite that most tests the *compiler*: `global_init`
is a single generated function spanning **152,948 lines**, and building it has
been observed to overflow the CLR stack outright (with a JavaScript stack only
eight frames deep — that is the compiler recursing over the AST, not the program
recursing). Compiling via LINQ expression trees means an AST-shaped recursive
walk and a heavyweight IL-emission path, both of which scale badly in exactly
this direction.

### MandreelLatency — pause caused by compiling Mandreel

**What it does.** Same file; measures how long the engine stalls while getting
that code ready to run, rather than how fast it runs afterwards.

**What it measures.** **Compilation latency and its granularity.** Engines score
well by compiling lazily (never compiling function bodies until first call) and
by keeping any single compilation unit small enough not to be perceptible.

**Broiler: 14.5 vs 67368 — 4646× slower, by an order of magnitude the worst
score in the suite.** It is the single most diagnostic number in the whole run:
Broiler compiles this eagerly, in units as large as the source makes them, through
an expression-tree pipeline that is far more expensive per byte of source than a
bytecode emitter. Nothing about steady-state execution speed is being measured
here — this is purely front-end cost.

### Gameboy — GameBoy Color emulator

**Origin.** Grant Galitz's GameBoy Online, running a real ROM for a fixed number
of frames.

**What it does.** Emulates the Z80-like CPU, the LCD controller and the sound
hardware. Opcode dispatch goes through big function-pointer arrays; memory is
typed arrays; the hot code is bit manipulation (`& 0xFF`, `<< 8`, `>>> 1`) and
branchy state-machine logic.

**What it measures.** **Typed arrays, integer/bit operations, and indirect calls
through large dispatch tables** — plus property access on a few very large
objects with hundreds of fields.

**Broiler: 1041 vs 90650 — 87× slower, Broiler's second-best throughput ratio.**
The reason is worth noting: the work per dispatch is large enough that the
constant overhead per call and per property access is amortized. Benchmarks where
Broiler does *relatively* well are the ones doing real work between engine
operations; benchmarks where it does badly (DeltaBlue, Richards) are the ones
where engine operations *are* the work.

### CodeLoad — parse and compile throughput

**Origin.** The sources of jQuery and the Closure Library, embedded as strings.

**What it does.** Repeatedly `eval`s that source against a mocked `window`,
measuring how quickly the engine can get from source text to executable code.
Nothing meaningful is executed afterwards — the load *is* the benchmark.

**What it measures.** **Parser throughput and lazy compilation.** The dominant
strategy is pre-parsing: scan function bodies just enough to find their extents,
and compile nothing until called. jQuery defines thousands of functions; almost
none run.

**Broiler: 83.4 *(0046)* vs 30916 — 371× slower.** The lowest absolute score of
any completing suite. Like MandreelLatency, this is entirely a front-end result
and points at the same place: eager compilation through expression trees. The
prior failure was also instructive — non-strict `eval` routed a `var` initializer
inside a function the eval'd code declared to the *eval* var-environment binding
instead of the function's own hoisted local, so the store never reached the
binding the reads resolved to and leaked out as a global. jQuery's `windowmock`
never initialized, and the benchmark died on `undefined.userAgent`.

### Box2D — 2D rigid-body physics

**Origin.** Box2DWeb, the JavaScript port of Erin Catto's Box2D (by way of
Box2DFlash).

**What it does.** Steps a fixed physics scene — bodies, fixtures, joints and
contacts — through many simulation ticks. Each tick runs broad-phase collision
detection over an AABB tree, narrow-phase contact generation per shape pair, and
then an iterative constraint solver that resolves velocities and positions.

**What it measures.** Arguably the most representative benchmark in the suite,
because it loads three things at once:

- **FP math with heavy short-lived allocation** — `b2Vec2` and `b2Mat22`
  temporaries throughout the solver, the same escape-analysis pressure as
  RayTrace but inside a much larger program;
- **Polymorphic property access over a deep hierarchy** — shapes, joints and
  contacts each have several subclasses, so solver call sites see many receiver
  shapes;
- **Megamorphic call sites** in the contact dispatch, where the callee genuinely
  varies.

**Broiler: 584 vs 99321 — 170× slower.** Note that Box2D also produced a
*correctness* fix on the way: the parser crashed on a bare `return` immediately
before a closing brace (commit `7786cdd5`).

### zlib — compression, compiled with Emscripten

**Origin.** The zlib library compiled to JavaScript by Emscripten (asm.js style).

**What it does.** Compresses and decompresses an embedded data payload
(`zlib-data.js`), doing all of it in a simulated heap of typed arrays with
Emscripten's integer-coercion idioms.

**What it measures.** **Typed-array heap access and int32 arithmetic** in
asm.js-shaped code — the same axis as Mandreel but in a much smaller, more
regular program.

**Broiler: 237 *(0046)* vs 80514 — 340× slower.** Its prior failure was the
cheapest fix in the whole set and the most complete blocker: Emscripten's shell
preamble does `Module.read = read` unconditionally, the way `d8` and SpiderMonkey
provide it, and the Broiler shell had `print` but not `read`. A `ReferenceError`
before the benchmark ran a line. It also takes 647 s once it works, so it needs
the long per-suite timeout too.

### Typescript — the TypeScript compiler compiling itself

**Origin.** TypeScript (an early version), compiling its own source, which is
embedded as `typescript-input.js`.

**What it does.** A full compiler pipeline over ~100k lines: lex, parse, bind,
type-check, emit. The largest single workload in the suite.

**What it measures.** Everything a large real application does at once — string
manipulation, huge object graphs, polymorphic property access, closures, `Map`-ish
dictionary objects with dynamic keys, and sustained GC pressure — plus parse and
compile time for a very large input.

**Broiler: 1009 *(0046)* vs 86327 — 86× slower, the best throughput ratio in the
suite** (and its highest absolute score bar the latency ones). The same pattern as
Gameboy, more so: in a workload this large and this varied, no single engine
overhead dominates, and Broiler's relative standing is at its best. Its prior
failure was a parser bug — the last expression of a C-style `for` head's comma
list was parsed and then discarded, so `for (i = 0, len = a.length; …)` never
assigned `len`, and `Binder.resolveBases` walked `type.implementsTypeLinks` past
its end.

---

## 3. The primary blockers, ranked

Ordered by how much of the gap each accounts for, using the engine's own
measurements in
[`Broiler.JS/docs/performance-roadmap.md`](../../Broiler.JS/docs/performance-roadmap.md)
as evidence.

### B1 · Every JavaScript value is a heap-allocated object

`JSValue` is `public abstract partial class JSValue` — a CLR reference type.
There is no Smi tagging, no NaN-boxing, no pointer-compressed value
representation. V8 represents a small integer as a tagged word and an unboxed
double in a typed field or a `Float64Array` element as raw bits; Broiler
allocates.

The engine's own baseline: **integer arithmetic allocated 128 bytes per
iteration** and an *empty* `for` loop 96. Two mitigations have landed — a
per-thread small-integer cache, and unboxed `double` locals — but the second has
a deliberately narrow eligibility gate (a function-top-level `var`, not a
parameter, not `let`/`const`, and not named by any nested closure). **Object
fields, array elements, parameters, return values, and anything crossing a call
boundary are still boxed.**

This is the single largest multiplier and it applies to all 17 scores. It is most
visible in Crypto (28-bit digit arrays), NavierStokes (double grids), RayTrace
and Box2D (vector temporaries), and Mandreel/zlib (int32 heap traffic).

### B2 · One non-speculative compile tier — no type feedback, no inlining, no deopt

The pipeline is source → `FastParser` → `FastCompiler` → LINQ expression trees →
IL via the custom `Broiler.JavaScript.ExpressionCompiler` → RyuJIT. Real machine
code comes out of the end, so this is not "an interpreter" — but it is compiled
**once, generically, with no knowledge of the types that will flow through it**,
and `docs/performance.md` records that function tiering is off unless a host
opts in.

That means every `+` is a call to a runtime helper implementing the full §13.15
algorithm; every property access goes through the property machinery; every call
goes through `JSFunction.InvokeFunction`. V8's 100× comes from the opposite:
Ignition collects type feedback, Maglev/TurboFan speculate that a site stays
monomorphic and Smi-typed, inline the callee, unbox the representation, and keep
deoptimization as the safety net.

**No JS-into-JS inlining is the sharpest sub-case.** Richards and DeltaBlue are
built out of one-line methods; V8 makes them disappear, Broiler makes a CLR call
for each. That is why those two have the worst ratios in the suite.

### B3 · Shapes and inline caches cover only a slice of the object model

The structures exist and, since P1, work well on the sites they cover — a
monomorphic read went from **0 hits / 200 000 misses to 199 999 / 1**. What they
do not cover is listed as open in the roadmap's §8.1, and each one maps onto a
benchmark above:

| Gap | Effect | Hits |
|---|---|---|
| Shape eligibility is `GetType() == typeof(JSObject)` | `JSArray`, `JSFunction` and every built-in exotic are excluded from shape tracking entirely | Crypto, NavierStokes, Gameboy, zlib — all array-dominated |
| No shape-transition cache | *Creating* a property misses every time, so a constructor that builds an object field-by-field misses on every field | Richards, DeltaBlue, RayTrace, Box2D |
| `o.x++`, `o.x += 1`, computed keys, `super`, optional chains, private names keep the old uncached lowering | `o.x++` measured the most expensive of them | Richards, Gameboy, Box2D |
| Double storage in `TrackShapeDataProperty` | Every tracked object stores each value twice (`shapeSlots` *and* the `PropertySequence`) and must keep them in sync | everything |

### B4 · Compile time and compile latency on large machine-generated code

MandreelLatency (**4646×**) and CodeLoad (**371×**) measure nothing but the front
end, and they are the two worst results in the suite. Three causes compound:

- **Compilation is eager.** CodeLoad exists specifically to reward engines that
  pre-parse and defer compiling function bodies until first call. jQuery defines
  thousands of functions and calls almost none of them.
- **Expression trees are an expensive intermediate.** Building a LINQ expression
  tree and running it through IL emission costs far more per byte of source than
  a bytecode emitter, and it is not incremental.
- **It recurses over the AST.** Mandreel's `global_init` — one generated function
  of 152,948 lines — has been observed to overflow the CLR stack during
  compilation, with a JavaScript stack only eight frames deep.

This is also the blocker with the clearest browser relevance beyond Octane: it is
page load time.

### B5 · The regex engine is a from-scratch backtracking interpreter

`Broiler.Regex` is a new ECMAScript-semantics engine whose `Matching/Matcher.cs`
is a backtracking interpreter with no compilation to native code. V8's Irregexp
JIT-compiles each pattern. RegExp is 110× off *against Octane's lowest reference
baseline*, and the same engine is on PdfJS's and Typescript's critical path.

### B6 · Ambient state on hot paths

`JSEngine` holds both the current context and the current strict-mode flag in
`AsyncLocal<T>` (`Core/JSEngine.cs:39` and `:223`). P0-2 removed the redundant
*writes*, but the roadmap records that `JSValue`'s set accessors still **resolve**
strictness through the `AsyncLocal<bool>` on every property write, and the
preferred fix — threading the compiler's static knowledge into the emitted
property-set helpers so the hot path reads nothing — is not started.

### B7 · GC — *not* currently a primary blocker

Worth stating explicitly to keep it off the priority list. SplayLatency is
Broiler's best result at 45×, and Splay's throughput at 152× is better than the
suite median. The .NET GC is handling a workload it was never tuned for
respectably. The allocation *rate* is a severe problem (B1) — but that is a
problem with what the engine asks the collector to do, not with the collector.

### B8 · Correctness gates that cost whole suites

Before any of the above matters, the benchmark has to run. Five of 15 suites
scored nothing in the committed results, and each traced to a small, general
engine defect rather than anything benchmark-specific: `eval` var-scoping
(CodeLoad), `obj == null` running `ToPrimitive` (Crypto), `undefined + x`
string-concatenating (PdfJS), a dropped `for`-head comma expression (Typescript),
and a missing `read` shell builtin (zlib). All five are fixed, at `7ef80c03`,
and the pinned pointer carries them — the committed results simply predate the
bump.

Worth noting what that says about the class of defect: not one of the five was
exotic. Four were core operator or scoping semantics (`==`, `+`, `for`-head comma
lists, `eval` var routing) that any large real program will hit, and the fifth
was a missing shell builtin. Octane found them because it is 15 large real
programs, which is the argument for keeping it in the loop even though it is a
retired benchmark.

A related structural point, from the change that became `cdb2fd41`: the
engine had no stack limit of its own, only the CLR's stack probe, which fires when
the stack is all but gone. .NET runs a catch handler as a funclet *on top of* the
frames it is handling, so the handler started with no stack and its first call
threw again — escaping the very `try` meant to catch it. Octane's harness is
literally `catch (e) { suite.NotifyError(e) }`, which is why one overflowing
benchmark (Crypto) took its entire suite down instead of being reported and
skipped.

---

## 4. Reading the ratios

Sorted by how far Broiler is off Chromium, using the committed scores plus the
five *(0046)* measurements:

| Benchmark | Chromium | Broiler | × slower | Dominant blocker |
|---|--:|--:|--:|---|
| SplayLatency | 69 725 | 1 539 | 45 | — (best axis; GC pauses are fine) |
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
| MandreelLatency | 67 368 | 14.5 | 4 646 | **B4 compile latency** |

The shape of that list is the finding:

- **The extremes are both front-end and call-path, not arithmetic.** The four
  worst are MandreelLatency, DeltaBlue, Richards and CodeLoad. Two are compilation
  cost; two are the cost of making a call and reading a property. Boxing (B1) is
  the biggest *uniform* multiplier, but it is not what produces the outliers.
- **Broiler does relatively best where the benchmark does the most work per
  engine operation** (Typescript, Gameboy) and relatively worst where the engine
  operation *is* the work (DeltaBlue, Richards). That is the signature of a fixed
  per-operation overhead rather than of a bad optimizer.
- **GC is the one subsystem that is already competitive.**

On that evidence the ordering with the best return is: **B4** (lazy compilation —
it owns the two worst scores outright and is the one that matters most to page
load), then **B2/B3** on the call and property paths (the Richards/DeltaBlue
cluster), then **B1** representation work, which is the largest total win and by
far the largest change.

---

_Sources: [`results/comparison.md`](results/comparison.md),
[`results/diagnostics.md`](results/diagnostics.md),
[`Broiler.JS/docs/performance-roadmap.md`](../../Broiler.JS/docs/performance-roadmap.md),
[`Broiler.JS/docs/performance.md`](../../Broiler.JS/docs/performance.md),
[`patches/README.md`](../../patches/README.md). Benchmark provenance from
[chromium/octane](https://github.com/chromium/octane)._

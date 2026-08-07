# Appendix A — reproducing the measurements

Every probe, its command line, the switches that build the control arm, and the traps each one has already cost somebody.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

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

# Phase 5 item 2: 1 turns ON the per-pattern RegexOptions.Compiled decision — after a pattern's
# thousandth match the engine builds the compiled form, times both arms on the subject in hand,
# and keeps the winner. Default is OFF. The switch is the arm to measure against; `--regex-tiering`
# flips it internally and reports both, so it needs no environment at all.
BROILER_JS_REGEX_TIERING=1 dotnet $DLL --specializing-tier /path/to/octane baseline counters
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

**`--specializing-tier` takes an optional fifth argument: a comma-separated list of suites.**

```bash
# The whole corpus, all fifteen suites, counters on.
dotnet $DLL --specializing-tier /path/to/octane Specializing counters

# Only these, when an earlier suite aborts the process and costs every suite after it.
dotnet $DLL --specializing-tier /path/to/octane Specializing counters "Gameboy,Box2D,zlib,CodeLoad,Typescript"
```

It exists because **checkpointing after every suite retains the rows before an abort and still
loses every row after one** — the suites run in one process in a fixed order, so Mandreel taking
the process down had cost Gameboy, Typescript, Box2D, zlib and CodeLoad in §4.2a's widened run as
well, which is why that section reports twelve suites rather than fifteen. A filtered run writes
its checkpoint to a **different path** (`broiler-specializing-tier-partial-<suites>.json`), so a
partial corpus can never overwrite the full one's and be read later as though it were complete, and
a suite named but not recognised is an error rather than an empty selection. Item `0103`'s
fifteen-suite table is two runs combined this way.

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
**And that 50.1% was another seven-suite figure**: over the twelve that run it is **55.2% of
186 831 813** — the seven are **10.7%** of the corpus's cache-answered reads — which **inverts item
3-2's plan**. Box2D is **9.6%** of the corpus's numeric reads rather than 98%; **Typescript
(64.2 M) and Gameboy (27.4 M) are 89% of them** and neither had been counted.

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

**Phase 5's three regex emitters, and which question each answers.** `--regex-profile` measures
the matcher — nine JS-level shapes per subject character, plus the eleven Octane patterns through
`System.Text.RegularExpressions` with and without `RegexOptions.Compiled`. `--regex-tiering` runs
the same eleven **through the engine** on both settings of `BROILER_JS_REGEX_TIERING` and reports
which way each race went; it flips the switch itself, so it needs no environment.
`--regex-call-envelope` is the one that re-ordered the phase: the identical work at the identical
iteration count, once through `re.test` / `re.exec` / `String.prototype.search` and once through
`Regex.IsMatch` and `Regex.Match` directly, so the difference is everything the engine does around
a match. **Read the envelope first.** Its `-long` row is the discriminator that stops the 2 431 B
per call being mistaken for a subject copy — the same anchored pattern on a subject 18.8× longer
allocates the same bytes to the digit.

```bash
dotnet $DLL --regex-profile
dotnet $DLL --regex-tiering
dotnet $DLL --regex-call-envelope
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
[`tests/octane/results/linux-x64/diagnostics.md`](../../tests/octane/results/linux-x64/diagnostics.md)**, not
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

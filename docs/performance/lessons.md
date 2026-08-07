# Standing measurement lessons

§3.5 — what this campaign has learned about measuring, each lesson attached to the measurement that taught it. The most reusable part of the document and the one to read before designing a probe.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

---

### 3.5 Standing measurement lessons

These were paid for once each. They apply to every phase below.

- **A bound taken at the wrong sites cannot be used to rank, not just to decide.** Item 3-1's
  consumer-side bound predicted `CallResult` at 9.41 and `PropertyRead` at 1.83 — five times apart,
  in that order. Measured on the mechanism itself they are **52.5 and 2.12**, the reverse and
  twenty-five times apart. `0110` established the bound was on a different quantity; `0112` shows
  that quantity does not preserve the ordering either. *A conservative-sounding bound is only
  conservative about the sites it covers; treat its ranking as unfounded until something measures
  the mechanism.*
- **Count the cost the mechanism actually pays, before building the mechanism.** Four attempts at a
  dual-representation local — 3-8a, `0109`, `0110`, `0111` — lost to the same quantity: the boxes
  minted reading the local. The first three measured it after building; the fourth measured it first
  and refused in one run. *The counter that decides a representation is the one on the representation
  itself, so the cheapest honest test is to build it for a candidate population behind a flag and
  read that counter — not to bound the cost from outside, which `0108` showed lands on a different
  quantity.*
- **A lower bound taken at the wrong sites is a bound on a different quantity.** Item 3-1's cost
  side was counted at five consumer positions and called a lower bound; it came out 25× under,
  because the cost is a box at the local's **own read expression** and those five are not where that
  happens. Being a bound protects against under-counting the sites you chose, and against nothing
  else. *Before quoting a bound, say which sites it covers and what share of the mechanism they are.*
- **A refusal census attributes a name to its first cause; removing that cause admits it only if it
  was the only blocker.** `0106` ranked `ElementRead` at 6.9 M boxed writes and the widening built
  for it collected 2.4 M, because `var t = a[0] * b + i` is charged to the element read and stays
  refused for the parameter. The census was right about what it measured and says nothing about what
  fixing it would admit — those are different questions and only one of them was asked.
- **A cost/benefit ratio prices an outcome; it does not establish that a mechanism reaches it.**
  Item 3-1's widening was selected on a measured cost/saving of 0.04 — the best number the phase had
  produced — and built, and it regressed at 1.061× because **868 of the 18.7 M boxes it was selected
  to remove were actually removed**. The saving lived at the tree's ROOT store, and the
  representation being widened into had raw arms for the leaf, the element read and the element
  write and none for the root. *Both the ratio and the build were correct about what they measured;
  nothing had checked that the two met.* Before building to a measured opportunity, name the emission
  site the saving lives at and confirm the mechanism has an arm there.
- **An instrument that changes its own population should be assumed broken before it is assumed
  biased.** Item 3-1's read-side counter wrapped a local's read expression in a counting call and
  the population it measured fell to **0.169×**, Gameboy's to zero. That reads exactly like a
  perturbing instrument — the kind this campaign has corrected for before — and the first fix
  assumed it was one. It was not: `variable.Expression` is *also* the assignment target, so `x++`
  compiled to an assignment whose target was a method call and the IL backend refused it. **The
  suites were crashing, and 0.169× was the share that still compiled.** A bias can be argued about,
  bounded, or quoted with a caveat; a crash cannot, and the log line said so on the first run.
  *Check that the arm ran before deciding what its numbers mean.*
- **A counter that names a category cannot rank its members, and three sections ranked them anyway.**
  The boxing census split its requests into *literal*, *conversion* and *what the operators mint*,
  and "conversion" was then used for three phases as though it named a producer. It does not: it is
  one factory entry that **21** compiler emission sites call. Attributing them (`0103`) found
  **61.8% of the corpus's conversions are a single one of the 21** — the guarded tree's root — and
  that the fallback arm the mechanism was suspected of leaking to is **226 of 69.3 M**. Both facts
  were unavailable from the total, and one of them retires a suspicion the phase had carried for
  three sections. *Before ranking a population, check that the counter can tell its members apart —
  a share computed over a category is a share of an unknown mixture.*
- **A struct copy in the source is not a struct copy in the code.** `0111` priced one 56-byte
  `Arguments` copy at 8.19 ns in a replica and argued — correctly — that a replica is legitimate
  here *because a struct copy has no inside*. Removing two such copies from the engine then bought
  **1.83 ns, not 16** (`0104`), because the JIT constructs in place when it can see the destination
  and had already elided most of the return-by-value traffic. The replica's **ratio** was sound and
  its **absolute** was not a count of anything the machine does. *A replica prices a mechanism's
  shape; only the engine can say how many times that shape survives compilation.*
- **A corpus that cannot be resumed past its worst member is a corpus that is never completed.**
  §4.2a fixed the census losing eight suites when the ninth aborted, by checkpointing after every
  suite — and that fix retains the rows *before* the abort while still losing every row *after* it.
  The suites run in one process in a fixed order, so Mandreel taking the process down had silently
  cost Gameboy, Typescript, Box2D, zlib and CodeLoad in the widened run too, which is why §4.2a
  reports twelve suites and not fifteen. Making the suite list selectable (`0103`) got the last
  three, and Gameboy — the suite §4.2a's own headline rests on — was among the ones a second abort
  had been quietly dropping. *Checkpointing answers "what did we keep"; it does not answer "what did
  we never reach", and only the second question finds a missing suite.*
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
- **A bound can be right about the number and silent about the thing that decides it.**
  `--compile-phases` charged item 1-1's free-name walk at **5.4–9.9%** of tree construction and
  called it a lower bound because it counted identifiers and resolved nothing. Built for real, the
  walk lands at **6.6–12.2%** — so the bound was a good estimate — *of a walk written as one
  bottom-up pass*. Written the obvious way, one scan per function, it costs **47.7%** on the most
  deeply nested corpus, because scanning a function re-walks every function inside it and each
  enclosing level walks it again. *A precondition's price is a property of its implementation, and a
  bound that does not say which implementation it bounds can be off five-fold without being wrong.*
- **A cost you write down as the price of a change should be measured before it is written down,
  because it may not be that change's price at all.** `0099` recorded one deadlock as what the
  execution lock cost. Measured, there were **two**, and the first belonged to `0098`'s job queue —
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
- **"In principle" in a written-up residual is a measurement not taken.** `0098` recorded that a job
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
- **A read-only question asked through a mutating API is not a read-only question.** Item 1-1's
  population probe needed one thing from the compiler: where does this name resolve? The API for
  that is `FastFunctionScope.GetVariable`, and it **sets `RootScope.HasOuterFunctionCaptures` as a
  side effect of answering** — which is a conjunct of item 4-2a's tiering gate. A probe built the
  obvious way would have turned tiering *off* for every function it merely asked about, silently,
  and only on the arm where the counter was enabled: the measured arm would have differed from the
  shipping one in a way no assertion in the instrument could catch, because the instrument was the
  cause. It cost one grep for what reads the flag. *Before reading engine state through an existing
  accessor, read the accessor — an instrument that mutates is measuring a build nobody ships, and
  the arm it corrupts is the one you are reporting.*
- **A declared noise band is a configured number until someone measures it — and once it was
  measured twice, the second measurement corrected the first.** `phase0.json` has carried 7.5%
  since 0-4 built `--repetitions`, every acceptance rule in this campaign is written against it,
  and for a long time nothing had ever run the flag that would check it. Run in a container:
  **5 of 13 scores exceed it**, spread 0.4%–15.9%, with **Richards and DeltaBlue among the
  failures** — the pair phase 2's exit criterion rests on. Run in CI, on the machine the gate
  actually closes on: **1 of 17 exceeds it**, median 3.0%, and all five of the container's
  offenders are inside — Richards at **1.9%**, a 5.6× difference on one suite between two
  honest three-repetition runs of the same engine. **Both readings are right about their own
  machine and neither is right about the other's.** *A tolerance nobody has measured is a
  preference; a tolerance measured somewhere else is still a preference. Measure the band where
  the arms are going to run, expect it to be per-suite, and never carry a spread across a
  machine boundary — including from a development container into an acceptance rule.*
- **A measurement that decides a *design* has to be re-taken before the design ships, because a
  premise can expire.** Phase 5's item 2 declined to ship a `Compiled` policy on the strength of
  one pattern out of eleven measuring **4.3× slower compiled** — stable across three repetitions,
  decomposed with four extra probes, and written up as the reason a use-count rule is unsafe.
  Re-running the *same probe on the same patterns* months later, with nothing in Broiler changed:
  **all three losing rows changed sign**, and the shape the whole decision named now promotes at
  5.27× on Octane's own subject. The original reading had already said the loss *"is .NET's
  codegen, not this engine's"* — which is precisely why it was not a fact about this repository
  and could stop being true without anything here moving. *A number taken from a dependency is a
  reading of that dependency's current version. Encode it in a comparison the engine re-runs, not
  in a branch the engine carries.*
- **"The corpus" is a denominator, and an instrument that does not emit it will be quoted without
  one.** Every phase-3 and phase-4 headline in this document says *"the corpus"* — 41.89% of its
  allocation, 54.0% of it removed, 93.54% of its reads monomorphic — and the censuses producing
  those numbers ran **7 of Octane's 15 suites**. The totals were added up outside the instrument,
  which is the step where the suite list stopped travelling with the number. Widened, the
  monomorphic read share is **80.11%, not 93.54%**, and **87.7% of the corpus's reads were outside
  the seven**. *Emit the aggregate from the instrument, with the population size beside it, so a
  partial corpus is forced to say so at the point of use.*
- **A missing suite is a defect report nobody filed.** The seven were not chosen — Mandreel
  **aborted the census host** with an uncatchable .NET stack overflow, because item 0-2's 16 MiB
  thread and stack reserve are a property of the *shell*, and no benchmark host had them. The
  census then serialized its output only at the end, so that abort discarded the eight suites that
  had already run. Between them those two make a suite permanently unmeasurable and make finding
  out expensive. *When a corpus has a hole in it, the hole is the first thing to measure — and
  make every instrument checkpoint per item, because the run you cannot finish is the one whose
  partial results you most need.*
- **A ratio to another engine is a statement about both engines, and the third column tells you
  which one moved.** Phase 2's exit criterion is *"DeltaBlue and Richards inside 200× of
  Chromium"*, and for three sessions the 400×/141× split was read as a fact about this engine —
  four explanations eliminated, two real defects fixed, no dent in the ratio. Asking the *same*
  question of Jint, a managed interpreter with no JIT that has been in every committed run since
  the harness gained it, splits it in one division: DeltaBlue is **2.83× harder than Richards for
  Broiler and 2.56× harder for Jint**, so only **1.10×** of the gap is ours, and closing all of it
  reaches 362× against a 200× gate. *The criterion was unreachable by construction and nothing in
  the item said so.* The cost of finding out was a division on data already committed. **Whenever
  an acceptance test is expressed as a ratio to a system you do not control, compute it for a third
  system before spending a session inside the numerator** — and prefer a reference that fails the
  way you do (a managed interpreter) over one that does not (a production JIT), because only the
  first can tell a shared difficulty from a private defect.
- **A shared control moving with the subject is the cheapest check a benchmark delta can get, and
  this harness has always emitted one.** Between the two committed Octane runs Broiler's geomean
  reads **351 → 498**, which is a 1.42× improvement if any of it belongs to the engine. Chromium's
  own geomean moved **57 080 → 74 297** on the same runner over the same two days and Jint's
  **616 → 820** — three engines moving 1.30–1.42× together, which is a statement about the runner
  rather than about any of them. Both runs are single-repetition and both say so on their own face,
  so the disclaimer was already there and the tempting number was still sitting next to it. *When a
  result ships beside an unchanged reference, difference the reference first; a delta that the
  control also shows is the machine, and the ratio column exists precisely to divide it out.*
- **A number computed over a subset stays wrong in the same direction every time you re-use it, and
  the subset does not announce itself.** §4.2a found three censuses stuck on 7 of Octane's 15 suites
  and fixed the hosts; what it could not fix is every *figure already derived* from them, because a
  derived figure carries no record of its denominator. Two have since been re-taken and **both
  moved by more than the effects they were used to justify**: item 4-2's `arithmeticBothNumbers`
  from 100.00% to 92.10% with a 0.46%–100% per-suite spread, and item 4-4's inlining ceiling from
  **1.89% to 2.43%** — the latter *upward*, because although *"from a promoted caller"* falls from
  64.0% to 42.1%, the suites nobody had counted make far more calls per millisecond than the seven
  do. Neither re-take needed new code; the widened hosts had been shipping for one patch, and
  the numbers were simply never read again. **The seven suites are 10.4% of the corpus's calls
  against 18.8% of its time** — call-poor, the opposite of how they were chosen. *When an
  instrument's reach changes, re-derive everything that was ever computed from it, and re-derive it
  by reproducing the old reading first* — both re-takes matched the old figure over the old subset,
  4-4's to within 0.0002% on a count of millions, which is what makes the new reading the same
  measurement rather than a different one.
- **A widened denominator has to exclude what does not run, and the suite that breaks it is the one
  that dominates it.** Both re-takes above were first computed against all fifteen Octane suites,
  which reported 4-2's arithmetic half at 0.038% and **4-4's ceiling at 0.65% — a third of the
  seven-suite figure it was correcting, and the wrong direction entirely.** Three suites fail
  (zlib's `read` is a shell builtin, RegExp has a pre-existing checksum, **Mandreel hits the stack
  guard**) and §4.2a had already written the rule: the widened headlines are over the twelve *"and
  the JSON says so"*. **Mandreel spends 286 728 ms failing** — 72% of a fifteen-suite wall clock —
  while making **1 488 of 59.7 M calls**, so it is almost the entire denominator and none of the
  numerator. Over the twelve the same data reads **2.43% and 8.06%**, both *larger* than the
  seven-suite figures, and 4-4's conclusion changes from *"too small to matter"* to *"too small to
  beat 4-5"*. **A fourth has since followed**: item 3-2's numeric-read table, whose 50.1% becomes
  **55.2% of 186 831 813** and whose *"3-2 is a Box2D item, 98% of the corpus's numeric reads are
  Box2D's"* becomes **9.6%** — the item was re-specified around a suite that turns out to be a
  fifteenth of its own population, while Typescript and Gameboy, 89% of it, had never been counted. **The catch came from a cross-check run for an unrelated reason** — a counters-off
  driver, to price the instrument's own overhead, which turned out to be nil (0.946×) and instead
  put the per-suite times side by side, where one row was 72% of the column. *A widening that fixes
  the numerator's coverage silently changes what belongs in the denominator; print the per-suite
  denominator before quoting any total built from it, and re-read the convention you already
  wrote down.*
- **A validated claim is validated of the property it tested, and the sentence that records it will
  drift to the property the item cares about.** `0104` predicted which bindings a deferred body
  captures and checked **membership**: zero missed on 5 157 sites, an honest and load-bearing
  result. Item 1-1's obstacle, in the item's own words, is *"a captured name's **index**"*. Between
  the check and the write-up the sentence became *"the capture layout `0104` settled"*, and four
  later patches — and several paragraphs written by the same person who ran the check — repeated it.
  **The prediction was a `HashSet` derived from a `HashSet`: it had no order, so it could not have
  answered the index question even in principle.** Nobody had to be careless for this; the drift is
  from a true sentence to a shorter one, and the shorter one is the one that gets quoted.
  *Restate the item's obstacle in the item's own words next to the result, and check that the result
  is about the same noun.* Asked properly, the answer was reassuring — 0 mismatches on 4 461
  comparable sites — **and it changed a design constraint**: over-approximation, recorded as a cost,
  shifts every later slot, so the prediction has to **drive** the layout rather than match it.
- **Price a fix before you build it, on the same terms you priced the problem.** Item 4-5's cost
  was measured (~44 ns on 60.16% of calls, 1.46% of the corpus) and its fix was then *named* — move
  the per-invocation frame off the function object onto a thread-local stack — with a size attached
  by inference rather than by measurement, which is the step that usually goes unexamined because
  the problem's number feels like it transfers to the solution. It does not. Priced, the relocation
  is **0.730×: 6.19 ns of the 22.96 the current shape costs, 0.20% of the corpus, for an M–L with a
  generator-suspension hazard in it.** *A third arm said why in one line* — a single 56-byte
  `Arguments` copy is 8.19 ns, so the cost is the **copying**, and the fix moved where the copying
  lands without removing any of it. **The arm that decides a fix is not the arm that measured the
  problem**, and the cheapest version of it is usually one line of the proposed design run in
  isolation. Doing it cost one probe and saved an M–L that would have bought 0.2%.
- **When a component pass cannot account for the whole, suspect the components before the tool.**
  Item 4-5 priced every mechanism in a call's prologue by *replicating* it — five nested `using`s at
  0.011 ns, EH at 0.73, dispatch at 0.68 — got about **10 ns of a ~147 ns call**, and concluded that
  **~85% was "unattributable from outside the engine"**, blocking itself on a sampling profiler the
  container does not have. The replicas were right about what they measured and wrong about what
  was asked: *a replica prices the mechanism, and says nothing about what the engine's own scopes do
  inside themselves.* The engine already shipped the control — `InvokeCallback`, the same call with
  one scope instead of five — and 4-4 had even written down that pricing the two against each other
  was **"the first thing 4-5 should do"**. Taken, it says **50.18 ns of 114.60, 44%**, and the item
  was never blocked. **Two habits, and the second is the cheaper one**: when the parts do not sum to
  the whole, the missing mass is more often in *how* the parts were priced than in a part nobody
  named; and **before declaring an item blocked on a tool, re-read the item that produced it for the
  measurement it already specified.**
- **A residue you can only describe is a residue you have not measured — classify it, and be ready
  for the classification to indict the instrument.** `0105` reported 84.1% of re-entered function
  bodies reproducing the eager tree and characterised the other 15.9% as *"ordinal divergence on
  every instance examined"* — an honest sentence, and an anecdote: it names what the failures looked
  like to somebody reading them, over a sample nobody counted. Classifying them cost one enum and
  two counters, and **three of the four causes turned out not to be about the mechanism at all**.
  The largest was the comparison's own ordinal table, shared across gensym families and keyed on the
  bare number, so `Context3` and `#TempJSValue3` collided and desynchronised every ordinal after
  them. The next two were the check's *second compilation*: it exhausts item 4-2b's process-wide
  site table (24 759 → exactly its 65 536 cap on one corpus), and it races the tier-2 rule that
  re-uses a tier-1 site. **Nothing was left over.** *The value of a classification is not the
  categories you expect to fill — it is the empty "other" bucket at the end, which is the only
  thing that turns "every instance I looked at" into "every instance".* And a checker's residue is
  the first place to look for the checker's own defects, because that is where they are indistin­
  guishable from the subject's.
- **A two-arm microbenchmark run in blocks is measuring the process's history, not the arms.** The
  probe that priced item 4-2's arithmetic half ran each arm's six samples consecutively and came
  back with generic-arm spreads of **161%, 76% and 470%** against effects near 3× — the exact
  condition §3.5 already forbids reading, produced by the instrument rather than found by it. It
  reported `multiply-generic` at **39.00 ns** and `less-generic` at **20.67 ns**; the same code,
  run round-robin with the arms reversed on alternate rounds and a blocking collection between
  samples, reports **15.42** and **3.93**. *A 2.5× and a 5.4× error, both in the direction that
  would have founded the item.* Consecutive samples hand each arm a private slice of the process —
  its own gen-0 debt, its own place in the tiered-JIT ramp, whatever the previous arm left on the
  heap — and in a fixed order the same arm pays the same debt every round, so the error is
  systematic rather than noisy and averaging more samples does not remove it. **Interleave the
  arms, reverse on alternate rounds, and ratio *within* a round**: a ratio of medians inherits
  whatever differed between the blocks, while a median of within-round ratios divides it out. On
  these arms the per-arm spreads stayed above 60% and the pair ratios were still clean at 11/12 and
  12/12 — which is the whole argument for the pairing in one line.

---

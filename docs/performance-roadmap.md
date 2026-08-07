# Broiler performance and benchmark roadmap

The single plan for JavaScript execution speed and the benchmark evidence that
governs it. It merges two documents that were each correct about half the
picture:

| Merged from | What it contributed |
|---|---|
| [`Broiler.JS/docs/performance-roadmap.md`](../Broiler.JS/docs/performance-roadmap.md) | The engine-internal campaign — P0–P3, phases A–F, their measurements, and the open items each deliberately left behind |
| [`tests/octane/roadmap.md`](../tests/octane/roadmap.md) | The forward plan driven by Octane 2.0 — phases 0–5, ordered by where the suite says the losses are |
| [`tests/octane/benchmarks.md`](../tests/octane/benchmarks.md) | What each benchmark exercises, and the ranked blockers B1–B8 that connect the two |

**It lives in the main repository, not in `Broiler.JS`, for a structural reason:**
the plan spans both. The harness, the suite manifest and the workflow are main-repo
(`tests/octane/`, `scripts/`, `.github/workflows/octane-benchmarks.yml`); the engine
is a submodule. A document inside `Broiler.JS` cannot link outward to the harness,
so only the parent can hold the combined view.

- **Owner assemblies:** `Broiler.JavaScript.Runtime`, `.Engine`, `.BuiltIns`,
  `.Storage`, `.Compiler`, `.Parser`, plus `Broiler.Regex` for phase 5.
- **Acceptance protocol:** unchanged and unchallenged —
  [`Broiler.JS/docs/performance.md`](../Broiler.JS/docs/performance.md) governs what
  may be *claimed*. **Nothing in this document closes on the numbers it quotes.**
- **Provenance:** the pinned `Broiler.JS` pointer is **`e5dc2610`**, read 2026-08-07 with
  `git submodule status` rather than from prose. **Read the pointer with the command; never from
  this line.** **Eight** consecutive readings have now found this sentence stale — `07adeb44`,
  `2ebc0c3c`, `71dda1b7`, `9bf9639b`, `61c8cc65`, `cca39b4d`, `14fa4f10` and `8308df51` before it —
  which is a rate rather than an anecdote: **a pointer written into prose is wrong by default**, so
  the sentence to write next to any pointer is the command that reads it. The sibling submodules
  are `Broiler.HTML` **`b829d1ff`**, `Broiler.CSS` **`f960f943`**, `Broiler.Graphics`
  **`e1ac7289`** and `Broiler.DOM` **`358cf058`** — three of the four had also moved since
  anything here described them. It is why §4.1's and §3.4's figures carry the commit they
  were taken at rather than "the pin".
- **`patches/` holds one open `Broiler.JS` patch — `0115`, phase 5's item 2 — and everything
  else this document measures is in the pin.** It was committed against the pin, verified to
  apply to it with `git am --keep-cr`, and **could not be pushed**:
  `Broiler-Platform/Broiler.JS` is outside this session's authorized repository set and the git
  proxy returns 403, which is the signal to fall back to a patch rather than to retry. **The
  pointer is therefore deliberately NOT bumped** — CI clones the submodule by pointer, and
  bumping it to a commit that was never pushed would break the build. It is not listed in
  `scripts/apply-pending-wpt-patches.sh`: it is a performance switch that is off by default and
  changes nothing WPT renders. Read the pointer with `git submodule status`, never from this line.
- **The ten that were open at the last reading have landed, and checking that is what retired
  them.** `0103`–`0112` were recorded here as pushed-and-blocked against pin `8308df51`; the pin
  has since moved to `e5dc2610`, and **all ten subjects are present in the submodule log**, so the
  patch files and their `patches/README.md` rows are deleted per that file's own rule. *The check
  is the one the paragraph below already prescribes — match against the submodule log, not against
  `patches/` — and it matters that it is not the obvious one:* the added files of a
  sequentially-applied stack exist in the pinned tree whether the whole stack landed or only its
  first patch, so file existence cannot retire anything.
- **Before those two, everything this document measured was in the pin.**
  The twelve open at the last reading — `0102`–`0113`: item 1-1's remaining half in five of them,
  plus the widened census corpus, item 4-2's arithmetic half, item 4-5's four, and item 3-1's
  read/write ratio — have been applied, pushed and the pointer bumped. In patch order they are
  **`861daccc`, `18524c34`, `db81b5b2`, `d2711e1b`, `a49d8ba5`, `5ea934fb`, `a06ef9eb`, `046a55fc`,
  `2f8ed84f`, `19b7ac5b`, `ddb20e7d`, `8308df51`** — twelve subjects matched against the twelve
  commits `14fa4f10..HEAD` contains, in one unbroken run with nothing else in it. **That is a
  weaker check than the patch-by-patch `format-patch` diff earlier rounds recorded, and the reason
  is worth keeping:** a patch file is deleted once it lands, so the diff is available only while the
  handoff is still open. Verify a landed claim against the submodule log, not against `patches/`.
  **So every figure in this document describes the pinned pointer directly**, rather than a local
  build plus a patch series applied in order, which is what a succession of sections used to have
  to say. Every commit cited for a measurement anywhere below — `a6f101cc`, `685026c0`, `cdb2fd41`,
  `9bf9639b`, `61c8cc65`, `07adeb44`, `cca39b4d`, `14fa4f10`, `2ebc0c3c`, `71dda1b7`, `7ef80c03`,
  `8228b0da`, `45f4f679` — is an **ancestor** of the pin (`merge-base --is-ancestor`), so nothing
  recorded against any of them is invalidated.
- **A patch number is a citation of this document's history, not a stable file name.** `patches/`
  is one flat namespace across every submodule, so two branches numbering from the same high-water
  mark collide whenever both are open — the ordinary case rather than an unlucky one. It has just
  happened again: **`0102` is now a `Broiler.CSS` patch**, reusing the number item 1-1's
  capture-free population census held one reading ago. Sections below cite `0102`–`0113` as the
  units of work they were; the durable reference for each is the commit above.
- **Measurement dates.** §4.1's figures and §3.4's test262 run were taken at `cdb2fd41` and have
  not been repeated — `685026c0` also carries a string-allocation fix (#936) and item 0-9's probe
  corpus (`aa2b1562`, #938). Octane code sites verified at `45f4f679`. **Phase 2's own
  measurements — §0 and each 2-x section — were taken at exactly the tree `a6f101cc` now is.**
  Item rows are checked against the tree rather than inherited from the prose above them; doing
  that is what caught that **item 1-2's acceptance criterion already passed before any work**
  (phase 1).

> **Path convention.** Because this document moved up a level, every path is written
> **relative to the repository root**. Paths carrying a `Broiler.JS/` prefix are inside
> the submodule — the source documents wrote those without it. Source *files* named in
> the item tables (`Runtime/ObjectShape.cs`, `BuiltIns/Function/JSFunction.cs`, …) are
> relative to `Broiler.JS/Broiler.JavaScript.*`, as they were in the original.

---

---

## 0. Status — the short form

**Last updated 2026-08-07.** One line per phase. **Every one of these is a digest of a
paragraph** — the long form, with the measurements each verdict rests on, is
[`performance/status.md`](performance/status.md), and the item's own section is where
anything may actually be checked. *Nothing here is closed*; §3 governs what may be claimed.

| Phase | Verdict | Detail |
|---|---|---|
| **0** — evidence | **0-1…0-6 ✅, 0-9…0-11 ✅.** 0-6's noise band is satisfied: the committed run carries `--repetitions 3` and **16 of 17 scores are inside the declared 7.5%**. 0-7's BenchmarkDotNet and 0-8's RID matrix remain owed and a container cannot produce either | [phase 0](performance/phase-0-evidence.md) |
| **1** — compile-time | **1-2 ✅, 1-4 ✅ (3.04× on Mandreel), 1-1's emission half ✅ (CodeLoad 1.099×).** 1-1's deferral mechanism is still open and is the phase's remaining L; it is no longer blocked on an unpriced precondition, and 0 missed sites on 5 157 checked says the layout is sound | [phase 1](performance/phase-1-front-end.md), [item 1-1](performance/item-1-1-lazy-compilation.md) |
| **2** — property access | **Every item landed or closed.** The exit criterion splits and stays split on four measurements: **Richards passes at 145×, DeltaBlue fails at 512×** | [phase 2](performance/phase-2-property-access.md) |
| **3** — arithmetic | **3-0, 3-3, 3-5, 3-6, 3-7 ✅; 3-8 counted and refused as written.** The dual-representation numeric local is **refuted on four populations running**, each measured before it was built. What is left is an XL bidding against 2.6%, and nothing here should be started on a box count again | [phase 3](performance/phase-3-value-representation.md), [item 3-1](performance/item-3-1-unboxed-storage.md), [item 3-8](performance/item-3-8-runtime-numeric-guard.md) |
| **4** — tiering | **4-1, 4-2a, 4-2b, 4-3a, 4-3b ✅; 4-2c refuted at 0.119%.** The phase's largest measured target is **4-5 at 6.50% of the corpus**, of which 92% of the bookkeeping is Annex B `caller`/`arguments`; its named fix was priced at 0.20% and refused, and the 1.46% that is left is gated on a soundness question nobody has answered | [phase 4](performance/phase-4-speculation.md) |
| **5** — regex | **Every item this phase named is closed, item 2 included.** The gate overturned the phase once (`Matcher.cs` is not on the Octane path) and item 2 overturned it again: **the matcher is 4.6–6.5% of what `re.test` costs**, so nothing aimed at matching can move this suite. The remaining target is the **fixed ~2.4 µs and 2 431 B every regex call pays** | [phase 5](performance/phase-5-regexp.md) |

---

## How this document is organised

The plan outgrew one file. **This page is the entry point and the only path anything
links to** — it keeps the status above, the sequencing and the non-goals below, and
the per-phase detail lives in [`docs/performance/`](performance/). Nothing was
rewritten in the split: every part below is the same prose, moved.

| Part | What is in it |
|---|---|
| [Status — where the campaign stands](performance/status.md) | §0 in full: the per-phase state, what phase 2 changed, the committed Octane run and what may be read out of it, the noise band, the conformance gate and the patch handoff. The roadmap's summary table is a digest of this. |
| [Phase 0 — establish the baseline](performance/phase-0-evidence.md) | The phase that gates every other one, and the only one containing no engineering: harness readiness, the evidence still owed, and what the probe corpus caught on its first run. |
| [Scope and metrics](performance/scope-and-metrics.md) | §1 — what merging the two source roadmaps produced that neither had. §2 — the five numbers this campaign tracks, in two families, and which question each answers. |
| [Measurement and acceptance protocol](performance/protocol.md) | §3.1–§3.4 — what may be *claimed*, how to run the Octane harness and the engine probes, and the conformance gates every item passes through. |
| [Standing measurement lessons](performance/lessons.md) | §3.5 — what this campaign has learned about measuring, each lesson attached to the measurement that taught it. The most reusable part of the document and the one to read before designing a probe. |
| [Where the engine stands](performance/engine-state.md) | §4 — what phases A–F and 2 completed, the current Octane profile, the seven-suite corpus every phase-3 and phase-4 headline was computed over, and the blockers ranked. |
| [Phase 1 — the front end](performance/phase-1-front-end.md) | Compile time and the two worst scores in the suite. Item 1-1 is large enough to have its own part; 1-2, 1-3 and 1-4 are here. |
| [Item 1-1 · Lazy function compilation](performance/item-1-1-lazy-compilation.md) | Phase 1's largest item, re-specified three times by its own measurements: the emission half landed, the deferral mechanism is still open, and the population it would serve is now counted. |
| [Phase 2 — the call and property paths](performance/phase-2-property-access.md) | Every item landed or closed. The Richards / DeltaBlue / Box2D cluster, the inline caches, the property map, and the two items that were closed on measurements rather than built. |
| [Phase 3 — value representation](performance/phase-3-value-representation.md) | Number boxing, which is 41.89% of everything the corpus allocates. Items 3-1 and 3-8 are large enough to have their own parts; the phase intro and 3-0, 3-2…3-7 are here. |
| [Item 3-1 · Unboxed backing stores for dense arrays](performance/item-3-1-unboxed-storage.md) | The longest item in the document: four attempts at a dual-representation numeric local, four refutations, one shared failure mode — and the method for refusing the fifth cheaply. |
| [Item 3-8 · Guard a local's numeric-ness at run time](performance/item-3-8-runtime-numeric-guard.md) | The census that produced phase 3's denominator — how much of a real run is number boxing at all — and the dual-representation tier built, measured and closed as a regression on it. |
| [Phase 4 — speculation](performance/phase-4-speculation.md) | Type feedback, deoptimization, the specializing tier-2 compile, inlining, and the fixed cost of a call — which is where the phase's budget should go. |
| [Phase 5 — RegExp](performance/phase-5-regexp.md) | The gate that overturned the phase, the three allocation fixes, the per-pattern `Compiled` race, and the per-call envelope the race found. |
| [Appendix A — reproducing the measurements](performance/appendix-a-reproducing.md) | Every probe, its command line, the switches that build the control arm, and the traps each one has already cost somebody. |
| [Appendix B — traceability](performance/appendix-b-traceability.md) | Where each item in this plan came from in the two source roadmaps, so nothing was dropped in the merge. |

**Read them in this order if you are new to the campaign:** scope and metrics, then
the protocol, then the standing lessons — the last of those is what stops a new probe
repeating an old mistake — then the phase you are working on. **If you are about to
quote a number from any of them**, §3 governs what may be claimed and the answer is
usually *not yet*.


## Sequencing

| Phase | Order within it | Size | Unblocks / expected effect | Exit gate |
|---|---|---|---|---|
| **0** | 0-1…0-5 ✅, 0-9…0-11 ✅ → 0-6 workflow run ✅ (17/17 committed, refreshed 2026-08-07) → **0-6's noise band ✅ — the committed CI result now carries `--repetitions 3` and a per-suite spread** → 0-7, 0-8 | — | Everything. 12 → **17 scores** ✅, **known noise band** ✅, and the first evidence any phase A–F can close on | 17/17 ✅, no timeout at the 180 s floor ✅, `comparison.md` reporting the triad ✅, **band on record from the gate machine** ✅ — 16 of 17 scores inside the declared 7.5%, median 3.0%, EarleyBoyer the lone 7.9%. **The declared figure was an assumption `phase0.json` has carried since 0-4 and it is now measured twice — 5 of 13 outside it in a container, 1 of 17 outside it on CI — so what the two runs establish together is that a band does not transfer between machines.** **0-7's BenchmarkDotNet and 0-8's RID matrix are what remain**, and a container cannot produce either |
| **1** | 1-2 mitigation ✅ → 1-2 real fix ✅ (all three passes) → **1-4 ✅** → **1-1 emission half ✅** → **1-1's remaining half measured, and the repeated closure rewrite it found is fixed ✅**; the capture mechanism itself is still open → 1-3 measure | 1-4 S, 1-1 remainder L | The two worst scores in the suite; page-load time generally. **1-4 took the Mandreel half (3.04×); 1-1's deferred emission takes 0.64–0.69× off jQuery, PdfJS and Box2D at 1.0009× steady state, and CodeLoad 94.6 → 104.0 (1.099×)**. **The remaining half is now sized rather than inferred**: parse 9.4–13.5% / tree construction 33.6–63.9% / emission 25–57% on the real corpora, over a population that is **84–99.7% never invoked**. What blocks it is not a pre-parser and not `EmitConstant` — the `Box[]` a creation site passes *is* the capture mechanism — but that its indices are decided by `LambdaRewriter` from a tree the deferred body does not have. **That obstacle is now built and priced rather than bounded** (`0101`): the free-name map that makes the layout addressable costs **6.6–12.2%** of body-tree construction as one bottom-up pass, and **up to 47.7%** written per-function, where the walk is superlinear in nesting depth — so the previously recorded 5.4–9.9% *lower bound* was a fair estimate of the right implementation and five-fold low for the obvious one. Mandreel, wide and not deep, is the control that goes the other way (7.8% → 8.8%). The mechanism itself is still unbuilt and still **L**. **And the population that could skip it entirely is now counted, which closes off the cheap way in** (`0102`): a site whose free names resolve to no enclosing binding needs no `Box[]` and could be deferred today, and that is **728 of 5 762 sites, 12.6%** — 39.7% on the flattest corpus and **7.4% on Mandreel**, i.e. worst exactly where the prize is largest. `Dynamic`, the direct-`eval` risk the item leads with, refuses **7 sites of 5 762**. The reading that looked like an opening — Mandreel's 7 605 bound free names being only **165 function-owned**, because a top-level `var` is a global-object property per spec — is refused by the counter built to test it: **`cellBacked` equals `bound` exactly on all six corpora, 15 118 of 15 118**, since this engine gives a program-level binding a CLR local like any other. *A spec-level fact about where a binding lives is not a fact about where the compiler puts it.* **The repeated closure rewrite the measurement found is fixed and is worth 0.782× on jQuery's whole compile and 0.867× on Typescript's, six of six pairs each** | test262 over the four pinned manifests, no new failure **and no new timeout**; MandreelLatency and CodeLoad out of the tail |
| **2** | 2-0 ✅ → 2-1 ✅ → 2-2 ✅ → 2-4 ✅ → 2-7 ✅ → 2-8 ✅ → **2-9 ✅** (2-3's successor, L); 2-5 and **2-3 closed on measurements**, 2-6 folded into 4-1. **Every item is landed or closed** | M each, 2-9 L | The Richards/DeltaBlue/Box2D cluster | An ownership entry and owned tests **per item**; test262 properties/strict-mode **satisfied** — unchanged at `a6f101cc` plus 2-9; **DeltaBlue and Richards inside 200×** — **measured twice, agreeing: Richards PASSES (183× → 150× after 2-11/2-12 locally; 144.9× in CI), DeltaBlue FAILS (576× → 447× locally; 460× in CI)**, five repetitions per engine on one machine and the committed CI run on another. **2-13 then decomposed the failing half against the third engine and bounded it**: DeltaBlue is 2.83× harder than Richards for Broiler and **2.56× for Jint**, so **1.10× of the gap is Broiler's** (1.118× on the previous run, independently) and closing all of it reaches **362×** against a 200× gate. The criterion is **not reachable by removing a Broiler-specific deficiency**; Broiler is ahead of Jint on DeltaBlue (0.77×) as it is on Richards (0.69×), and the genuinely Broiler-specific suites are MandreelLatency (54.3×), CodeLoad (37.8×) and zlib (12.0×). Read polymorphism is falsified as the cause by Crypto, 73.82% monomorphic and Broiler's best suite against Jint. **2-10 closes as measured**, handing forward a question about the gate |
| **3** | 3-0 ✅ → **3-3 ✅** → 3-5 ✅ → 3-6 ✅ → 3-7 ✅ → 3-8 ✅ (counted, do not start as written) → **3-1 (85% of the corpus's boxes) → 3-2 (Box2D's 11.6 M), sharing one compiler half, and nothing else until they land** → then *cost* 3-4 | L–XL, 3-8 XL | Uniform lift across arithmetic and allocation-heavy suites. **3-7 closes the static half of the coverage question and 3-8 is what is left**: the widening reached 8 names of 2 920 (224 → 232), because 247 of 3-6's 478 captured names are held by a *hoisting* rule that is correctness rather than policy, and 2 439 are not proven numeric. **3-8 then measured the two numbers this phase never had, and they re-order it.** Number boxing is **41.89% of the corpus's allocation** (2.05 GB of 4.88, and 66.96% of NavierStokes) — so the prize was always large — while the **whole** raw-double local tier, every item from P2-2 onward, removes **0.36% of those boxes**. A box is minted by the operator, not by the local, and 76.4% of the names 3-8 would guard take their value from a property read or a call. **3-1 and 3-2 move to the front**: they unbox the sites that mint the boxes, and they have been ranked behind the locals work since the phase opened on no measurement at all. **Started, and the first count moves the item off storage.** What the generic arithmetic operators are handed at run time had never been measured: **73 817 515 of 73 818 646 invocations arrive with both operands already Numbers — every one but 1 131 — and that population is 86.6% of every box the corpus allocates**, while the compiler's own `both are native` proof reaches **0.75%** of the same invocations. *Compile-time provability reaches 0.75% of the arithmetic; run-time truth reaches 100.00%.* So the shared half is a **run-time-guarded specialization of an arithmetic expression tree** — box only the root — and not the typed backing store the item is written around; a typed store returns to being the live-memory item it always measured as. It also partly reverses 3-8's "do not start as written" without contradicting it: 3-8 priced the guard at the **local** (0.36%), this counts it at the **operator** (86.6%). **The shared half is then built** — evaluate each leaf once, test for Number, compute on raw doubles, box only the root — and removes **10 401 782 boxes of 85 249 783, 12.2% of the corpus's allocation, from 862 sites**, against **0.36% for every previous phase-3 item combined**. Short of the 86.6% ceiling for a reason the per-suite column gives rather than hides: NavierStokes loses 10.1% of its generic invocations and 1.8% of its boxes, EarleyBoyer 99.7% and none, so **most of those two suites' boxes are minted somewhere that is not a binary arithmetic operator** — the next count. Wall clock then measured: **driver 0.981× on six of six ABBA pairs, Crypto 0.912× on six of six**, against controls at 1.005× and 1.006× — so **12.2% of the allocation buys 1.9% of the time**, and no suite is slower. **3-1's own re-measurement then made that stronger.** The element chain decomposes exactly — 0.00 for a raw double, 31.98 for `s = s + a[0]`, 95.99 with a multiply, 159.67 for a read-modify-write — and the element STORE is in none of it: the boxes are minted by the operators, and the read is free today only because what it hands back is already a box. Two things fell out. A numeric literal is **re-boxed on every evaluation** (`a[0] * 1.5` costs two boxes where `a[0] * 2` costs one), measured at **1.2% of requests** and recorded rather than built. And the **bitwise and shift operators had no native form** although the analysis has always typed them — `s = i + 1023` costs 0.00 B/iter and `s = i & 1023` cost 31.84. That half **is built** (`JSNumericOperators`, all six through `ToUint32`, 15 tests on both arms) and takes its shape to **0.00** — **and removes no boxes at all on the corpus**: six suites identical to the digit, and Crypto (42.4 M boxes) differing by less than its own run-to-run variation, measured by running one arm twice. The native form needs both operands native and Crypto's digits live in `this.array[i]`. *Six items have now built machinery array-resident data cannot reach; every one is correct, every one is invisible, and every one is waiting on 3-1*. **3-2 was then measured too, and its one-sentence premise is wrong**: `o.x = 2` allocates **nothing** — a slot store is a reference copy — so `vector.x = 1.5` pays for the **literal**, not the slot, and the slot's own cost shows up only in `o.x = v * 1.5` where the value is a raw double (32 B, the same 32 B for the eleventh time). The field rows match the element rows **to the hundredth** — 31.98 and 96.00 both — so 3-1 and 3-2 are one mechanism with two backends. And 4-1's uncollected "numeric-vs-generic" signal, built at last, splits them exactly: **50.1% of all cache-answered reads hand back a number**, but **98% of those are Box2D's**, while **NavierStokes performs 388 property reads, zero numeric, and mints 29 977 471 boxes**. So **3-1 carries 85% of the corpus's boxes and 3-2 carries Box2D's**, and no work on shape slots reaches the other two suites. **The next count then named every box the corpus mints, and moved the item again.** The compiler's boxing conversion — the only thing a typed store could remove without further operator work — is **5.0% of NavierStokes' requests against 31.0% of Crypto's**, i.e. the two suites are the opposite way round from this item's premise. Chasing the **40.5%** that first pass left unattributed down to **1.0%** found the answer in the operators no census had counted: **`++` and `--` are 30.9% of the corpus's boxing, 51.6% of NavierStokes' and 80.4% of EarleyBoyer's**, and **half of that is `ToNumeric` re-boxing a value that is already a Number** — 17 281 232 requests, 15.4% of all boxing, removable by a guard. **Built, in nine lines**: 17 285 913 requests removed against that prediction (0.03%), **7 050 834 real allocations, 9.4%**, NavierStokes **23.0% of its boxes and 0.906× of its time on six of six pairs**, and the corpus **0.795×** with `0084`. **What did not move is the finding**: EarleyBoyer halved its boxes for 1.002×, because 82 000 a second is not 4 240 000 a second — *a share of a suite's own allocation forecasts nothing, the absolute rate forecasts everything*. **Then the refusal waterfall, which is the count `0084` never took and the largest result the phase has had.** Of 5 396 candidate arithmetic nodes only **862 specialize**; `OrderUnsafe` refuses 1 762 and `NoSavingToMake` 2 718, and those are **one** finding — a left-leaning `a[0]+a[1]+a[2]+a[3]` refuses at the root for order, again at each left child, and its bottom node is then a lone operator with nothing to save. The sub-census names the blocking leaf: **1 028 property reads against 34 element reads**, so the rule this phase assumed was an array problem is an **object-field** one, 984 of them Box2D's. **The fix is that nothing required the leaves to move**: emit each at its own postorder position and put the test where the coercion would have run, and the purity rule has nothing left to protect. **53 353 957 → 6 626 052 generic invocations and 67 795 858 → 31 162 330 boxes — 36 633 528 removed, 54.0% of everything the corpus allocates**, `OrderUnsafe` 1 762 → 0 and `NoSavingToMake` 2 718 → 1 181 untouched. From the pre-`0084` baseline the corpus is **0.366×**. **Driver 0.969× on six of six ABBA pairs, NavierStokes 0.834× and Crypto 0.893× both six of six**, two zero-box controls at 1.002× and 0.999×; Box2D cuts 51% of its own boxes for 1.003× because 861 000/s is not 6 500 000/s, which is `0086`'s lesson holding a second time. *54.0% of the allocation buys 3.1% of the time* — with `0084`'s 12.2% → 1.9%, the third reading of the constant that should size the rest of the phase  **And then the denominator the phase never had**: collection is **1.8–2.0% of the driver**, and of the 768 ms the order-preserving emission removed only **54 ms was collection** — the other 714 ms is the mutator's own allocation work. *A box costs ~14× more to create than to collect*, which makes §Non-goals' "the collector is not the problem" a measurement. At **711 ms per GB** the **0.70 GB of number boxes left is worth ~2.6% of the driver**, so everything remaining here is an XL bidding for under 2% — count the `++`/`--` step's operands before building the typed store, and bid with a rate rather than a share. A sampling profiler was tried and does not decompose this engine: it inflates the driver ~29%, its biggest frame is its own rendezvous point, and compiled JavaScript does not symbolicate  **And the `++`/`--` count is taken**: of 17 282 144 steps, **Element 0, Property 0.3%, LocalSlot 98.1%, Other 0** — the step shares no mechanism with a typed store and belongs to the numeric local. ≈**7.05 M real boxes, 22.6% of what the corpus still allocates**, 6.76 M of it NavierStokes', where one untypable closure variable (`rowSize`) cascades into every `++currentRow`. **Re-opens 3-8**, which priced the guard at the local and measured the tier's *yield* (0.36%); this measures what it *lets through*  **Then scoped**: eight shapes, one per conjunct, rule three suspects out — a nested function declaration is innocent, 3-7's hoisting rule produces a `LocalCell` (NavierStokes: 9 461 760 slots against six cells), and passing the value in only trades `OtherName` for `Parameter`. **One conjunct is left — the analysis will not type a name from outside the function, even one already proven numeric.** Splits into **3-9** (static, import the enclosing scope's conclusion; does *not* reach NavierStokes, whose root is held by 3-7's correctness rule; count its population first) and **3-8a** (run-time, one `IsNumber` test where the value enters) — scoped at **≈115 ms, 0.6% of the driver, an M rather than an XL**. **3-8a was then built complete and closed as a measured regression.** Its population is 26 names, 15 in NavierStokes; the dual representation and all three consumers that can take a raw double are built (the guarded tree's leaf, the element read, the element write), and each moved the number without moving it enough — 1.021×, 1.017×, 1.012×. A counter added **at the read** then settled it: **NavierStokes mints 393 705 boxes reading a speculative local against ≈5 300 removed**, because the 835 584 steps it takes off `Increment` are mostly `x[++i]`, whose result is boxed to be an index either way. *Every premise the item was scoped on survived and the item still lost* — what makes it lose is the read/write ratio of the code it targets, a property of the workload rather than of how many consumers the compiler grows. **Off by default and staying off; §3.5 gains the rule that a representation change is priced by that ratio, counted before the representation is built.** **3-9, the static half of the same split, is closed at a population of ZERO** — 0 names and 0 outer-numeric offers on all seven suites, against 3-8a's 26 from the same call site in the same run — because 3-9 can only import from a name that is both proven numeric and still a raw double despite being captured, which is item 3-7's eight, and none of the eight is read from an assignment inside the function that captures it. *Counted with an instrument proven to discriminate on nine constructed shapes first, and closed for one instrument and no mechanism*. **Then the denominator itself was checked** (§4.2a): the census producing every figure in this row ran **7 of 15 suites**, and widened it reads **90.6 M boxes and 12.93 GB against 31.4 M and 3.13 GB — 65.4% of the boxes outside the seven**, with **Gameboy alone at 41.3 M, 1.32× the whole measured corpus**. `0090`'s GC denominator survives (1.80% against 2.29%); the phase's ranking of its own remainder does not. **Attributing the widened corpus then partly reverses 3-1's move off storage**: conversions go **24.6 M → 69.3 M** with **64.4% of them outside the seven**, and **Gameboy alone mints 26.9 M at 51.0% of its own requests** — more than all seven together — on a `Uint8Array` memory image, which is the shape a typed backing store was written for. **3-1's storage half re-opens as unmeasured rather than refuted** | `test262-arrays`, `test262-binary-data`, and — added by 3-3's `let`/`const` half — `test262-lexical-declarations`; allocation reported per item alongside time **Then the conversion counter was split by emission site over all fifteen suites** (`0103`), and it both retires a suspicion and re-points what is left: **61.79% of 69.3 M conversions are the guarded tree's ROOT box**, the generic fallback arm is **226 of 69.3 M**, and Gameboy — the suite §4.2a re-opened the storage half on — is **28.7% `++`/`--` step**, i.e. item 3-8's population and not the store's. **The next measurement is the root box's CONSUMER**, which is a compile-time attribution rather than another run-time counter. **Then the root's consumer was counted** (`0105`) and it answers the question: **44.36% of the 42.8 M root boxes are consumed by a LOCAL**, 17.91% by an element and 13.14% by a property, so neither storage item is where the remaining boxes go — a proven-numeric local already has a raw `double` home, and a root landing there is one the numeric tier failed to type. **Phase 3's remainder is the numeric-local tier**, which is now the third independent count to say so. **Then the refusals were weighted by execution** (`0106`): the seam hypothesis is refuted at 36 boxes of 18.6 M, and of the 19.0 M boxes consumed by a refused local, **38.41% are cascades with no independent cause** and **36.35% are `ElementRead`** — the conjunct item 3-1's guarded tree already settles at run time. Next measurement: the read/write ratio for that population, before any representation is built (§3.5, and item 3-8a's regression). |
| **4** | 4-3 design ✅ → **4-1 ✅** (shapes and callees; numeric-vs-generic still open per site — item 3-2 collected the aggregate read share, 50.1%, for a phase 3 ranking) → **4-3a ✅** → **4-3b ✅** → **4-2a ✅** → **4-2b ✅** → **4-2c ✅ refuted** (the arithmetic half priced at 0.119% and closed, the relational lead closed with it at 0.022%, and the whole generic binary-operator surface bounded at 0.475% of the corpus) → **4-5 ✅ unblocked** (44% of a call entry is bookkeeping the engine's own short path skips — **2.85% of the corpus**, the largest measured target left in the phase, and an ablation of eight named operations rather than a profiler) → **4-4 ✅ measured, not started** (its ceiling re-taken over the twelve suites that run is **2.43%**, *larger* than the seven-suite 1.89% — the promotion gate reaches 42.1% of the corpus's JavaScript calls rather than 64.0%, but the never-counted suites are far call-denser per millisecond — while 4-5's surface is **8.06%**, so the ranking holds by 3.3×) | XL | The remaining order of magnitude. **4-1 measured the premise: 93.5% of reads and 96.7% of calls are monomorphic by execution weight, so 4-2 and 4-4 are well-founded** — over **seven** suites. **§4.2a re-took it over twelve and it is 80.11% and 86.35%**, because the census corpus every phase-3 and phase-4 headline is computed over was 7 of 15 and never said so; Mandreel had been aborting the census host with an uncatchable stack overflow, since item 0-2's stack reserve is a property of the *shell* and no benchmark host had it. Fixed, and the number is still high enough to found the phase. 4-3a stated and enforced the restart contract — and found its no-suspendable-bodies condition was held only by two unrelated accidents, two ordinary refactors away from an async function returning a number instead of a Promise. **4-2 then split the same way**: measuring the branch it was told to replace found it produced *wrong answers* — DeltaBlue died on the shipping tier-2 hook — which 4-2a fixes, and 4-2b's specialization takes **44.7% of the corpus's executed reads off the cache path at 0.818× each**, which is **0.83% of suite time**. That number is the phase's own warning: the whole read path is ≤ ~9% of Octane's execution time here and the whole call path ≤ ~5.5%, so **4-4's ceiling is smaller than the phase assumed** | Deopt correctness proven **before** any speculation ships; full test262 matrix **4-5's floor moved 0.100% on the lever `0111` named** (`0104`, `out` parameter, 9 of 12 ABBA pairs) and its 1.46% frame is untouched; the useful residue is that removing two struct copies bought 1.83 ns against a replica's 8.19 ns each, so *a struct copy in the source is not a struct copy in the code*. |
| **5** | profile ✅ → per-match subject copy on `replace`/`exec` ✅ → single-match `replace` without a builder ✅ (both builtins) → the global case's retained result list ✅ → **`Compiled` per pattern ✅ — built as a race, measured, and shipped switchable with the default off** → ~~then consider compiling `Broiler.Regex`~~ **→ the per-call envelope, which is where the phase's remaining time actually is** | L | RegExp, plus PdfJS and Typescript | Octane regex corpus profiled **before** any rewrite — **satisfied**, and it re-ordered the phase twice. The second re-ordering is item 2's: the matcher is **4.6–6.5%** of a `re.test`, so nothing aimed at matching — the .NET compiler, and by the same argument a compiled `Broiler.Regex` — can move this suite. **The ~2.4 µs and 2 431 B a regex call pays before any matching happens is the item**, and it is unstarted |

**Dependencies.**

- Phase 0 gates every claim in phases 1–5 *and* retroactively gates closing A–F.
- Phases 1 and 2 are independent of each other and of phase 5, and can run in parallel.
- 3-2 is cheaper after 2-1.
- Phase 4 depends on 4-3 (for everything in the phase) and on 4-1 for 4-4's callee feedback — what was 2-6 is now inside 4-1 — and
  benefits from 3-1/3-2 having established unboxed representations to speculate into.

**The bolded item in each phase is the one to start with**, and in three of the five it
is not the one that sounds most important: 1-1 over 1-3, 2-1 over 2-2, 4-3 over 4-2.
Each ordering is argued where the item is described.

**Every phase closes under
[`Broiler.JS/docs/performance.md`](../Broiler.JS/docs/performance.md)**, unchanged: two
runs inside the configured band, on the release RID matrix, reporting time, allocation
and working set together, with an `ownership.json` entry naming each item's benchmark
and semantic owner. **Phases A–F are all *implemented* and none is *closed* for exactly
this reason.** This plan should not add to that debt.

---

## Non-goals

Stated explicitly so effort does not drift into them.

- **GC work.** SplayLatency at 45× is the *best* result in the suite (B7). The
  allocation **rate** is a severe problem — that is phase 3, and it is a problem with
  what the engine asks the collector to do, not with the collector.
  **This is now measured rather than asserted (item 3-1).** `GC.GetTotalPauseDuration()` puts
  collection at **1.8–2.0% of the driver**, and of the 768 ms an allocation change removed, **54 ms
  was collection and 714 ms was the mutator** — the pointer bump, the zeroing, the write barriers
  and the cache traffic of touching a gigabyte of fresh memory. *A box costs about fourteen times
  more to create than to collect on this corpus.* Aiming at the collector would have been aiming at
  a fourteenth of the problem, which is what this bullet always claimed and could not previously
  show. **Qualified since, and the qualification is that "the corpus" was seven suites** (§4.2a):
  measured over every suite that runs, collection is **1.07%** of elapsed — but the spread is
  **0.7% to 10.3%**, and the top of it is **Splay**, the suite Octane includes to stress the
  collector and the one no census had ever run. The conclusion holds everywhere measured
  (allocation dominates collection on every suite); what should stop being quoted is a single
  exchange rate, since on Splay it is nearer 9:1 than 14:1.
- **asm.js or WebAssembly special-casing** for Mandreel and zlib. Recognizing asm.js
  type annotations would move two scores and is exactly the optimize-for-the-benchmark
  behaviour that got Octane retired in 2017. Phases 3 and 4 reach the same code through
  general mechanisms.
- **Chasing the geomean directly.** If a change raises the total without raising the
  worst scores, it has not smoothed anything (§2.1).
- **Anything that trades conformance for speed.** Every item is a
  same-observable-behaviour change. Where the spec-visible surface is genuinely at risk
  (1-1's early errors, 2-1…2-4's `OrdinarySetWithOwnDescriptor`, all of phase 4) the
  risk is called out and the gating manifest named.
- **Security.** Broiler.JS is not a sandbox, and none of this changes that. Compliance
  and performance completion must never be presented as isolation of untrusted scripts.

**Scope discipline.** Octane was retired by its authors precisely because engines began
optimizing for its shapes. Every item above is justified by a *mechanism* that matters
to real JavaScript, with the benchmark used as **evidence that the mechanism is
missing** — never as the target.

**No longer non-goals:** parsing/compilation and a speculating tier, both of which the
engine roadmap excluded. See §1.1.

---

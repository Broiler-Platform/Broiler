# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Index

| Patch | Submodule | Note |
| --- | --- | --- |
| `0104-js-capture-layout-checker` | `Broiler.JS` | **Item 1-1's remaining half attempted, and the checker built to validate it found two obstacles the item's statement does not name.** The item is blocked on one stated thing — a captured name's `Box[]` index is decided by `LambdaRewriter` *from the tree*, and a deferred body has none — so the layout must come from source alone, and `0101` built the walk that would derive it. **The property that matters is asymmetric**: over-approximating costs a box per creation site and that name's numeric tier, while under-approximating means a deferred body resolves a name to a box that is not there — *a miscompile*. One missed capture disqualifies the approach, so the checker exists to make a zero mean something: **nine fixtures, one of which deliberately records an empty prediction against a non-empty truth and asserts the miss is reported**, without which the rest are vacuous (`0096`'s rule, applied to a comparison rather than an emitter). **Obstacle 1, found on the simplest fixture there is.** `ClosureRepository` holds **two** populations and the item's phrase *"which variables this lambda captures"* means one of them: bindings **handed in** from an enclosing scope (`Setup`, `index >= 0`), and the lambda's **own locals that something nested captures** (`Convert`, `index == -1`). A free-name walk answers the first and correctly does not name the second — so compared against the whole repository, `function outer(){ var q = 1; var inner = function(){ return q; }; }` reports ***`outer` missing `q`***, a function missing its own local. *The prediction was right and the comparison was wrong.* **The deferral therefore needs two derivations, not one**: the enclosing function learns which of its own locals to box from **its children's** free-name sets, and the deferred body learns its own free names' indices — `FreeNameScan.ForProgram` already composes bottom-up and can serve both. **Obstacle 2: every function is handed a `ScriptInfo_*` binding no source identifier names** (script metadata the compiler threads in), beside `this`, `arguments` and `new.target` — so the layout needs a reserved region for compiler-introduced captures alongside the source-derived one. **What is deliberately NOT claimed**: with the comparison corrected the corpus reports **170 missed sites of 5 762 and 6 138 missed names**, and the same run reports Mandreel at **2 622 exact against 1 476 predicted sites** — more checks than predictions — so the per-site figures double-count somewhere. *A miss rate from an instrument that disagrees with itself is not a finding*, so **no go/no-go is claimed and the item is not declared blocked on it**; the next steps are the double-count, then the three unexplained sample shapes (a top-level `var` read from a top-level function, a named function expression's own name, jQuery's minified nesting), in that order. **The deferral mechanism itself remains unbuilt and remains L.** Off by default (`BROILER_JS_CAPTURE_LAYOUT_CHECK=1`), changes no emission on any setting. Compiler suite green: **1 250 tests**, +9 and every one of them this patch's. **Apply after `0103`.** |
| `0103-js-widen-census-corpus` | `Broiler.JS` | **The corpus every phase-3 and phase-4 headline is computed over was 7 of Octane's 15 suites, and the reason had never been written down.** Item 2-13 established that this engine's largest *private* deficiency is **zlib — 12.0× behind Jint relative to Chromium** — so the next step was to ask what the censuses say about it. They say nothing: zlib is in none of them, and neither are Mandreel, Gameboy, PdfJS, Typescript, CodeLoad, Splay or RegExp. **`TypeFeedbackMetrics` and `SpecializingTierMetrics` both ran the same seven**, whose output the roadmap quotes as ***"the corpus"*** over a denominator it never states — while the third, `PropertyMapDistributionMetrics`, **lists all fifteen and reaches nine**, aborting on the same Mandreel overflow and producing *nothing at all* when it does, so items 2-7 and 2-9's map figures were **not reproducible from a clean tree**. *Three instruments, three different partial answers, one phrase used for all of them.* **Why seven is not a choice about cost**: widening the list aborts at the ninth suite with an **uncatchable .NET stack overflow** in Mandreel's `global_init`. Item 0-2's 16 MiB thread and 4 MiB reserve are a property of **the shell** (`Program.cs`), and the benchmark hosts built plain contexts on whatever stack the runtime handed `Main` — *every benchmark host in this campaign has been running without the reserve the shell has had since phase 0*, so the one suite that needs it was permanently unmeasurable. **And all three made finding out as expensive as possible**: each serialized once, at the end, so the abort discarded every suite before it. **All fixed** — all three run on the shell's thread with the shell's `MaxStackUsageBytes` and checkpoint after every suite, Mandreel now fails catchably instead of taking the process down, and `TypeFeedbackMetrics` **emits its corpus aggregate with the suite count beside it** rather than leaving it to be totalled by whoever quotes it — which is the step where the suite list stopped travelling with the number. **Both widened counts move a headline, and both reproduce their old value over the old seven**, so each is the same instrument over more suites rather than a different measurement: monomorphic reads **93.54% of 37.9 M → 80.11% of 307.9 M**, monomorphic calls **96.70% of 4.24 M → 86.35% of 52.9 M**, boxes allocated **31 401 346 → 90 641 738**, allocated bytes **3.13 GB → 12.93 GB**. **87.7% of the corpus's property reads and 65.4% of its boxes were outside the seven.** The suites that move them had never been counted: **Gameboy** is 34.60% monomorphic with **1 282 polymorphic sites** and allocates **41.3 M boxes on its own — 1.32× the entire previously measured corpus**; **Splay** is **10.15%** monomorphic; **Typescript** is 67% of the corpus's reads and allocates 4.96 GB. **What survives the widening is worth as much as what does not**: `0090`'s GC denominator holds at **1.80% against 2.29%**, so *"a box costs ~14× more to create than to collect"* was not an artifact of the corpus and §Non-goals' GC bullet stands — while phase 3's ranking of its own remainder, *"an XL bidding for under 2%"*, was sized against a driver a fifth the size of the real one. **Honest limits, stated in the output**: zlib (`read` builtin lives in the shell), Mandreel (now hits the guard) and RegExp (pre-existing checksum) report setup-only counts, so both headlines are over **12** suites and the JSON says so. **It also re-takes two landed phase-2 items their own instrument could not previously complete**: **2-9 is corroborated** at **2 202 782** maps on the full corpus, the right side of its recorded 16.2 M → 2.5 M; **2-7 splits**, its live-memory result still favouring the shipped policy but by less than recorded (**0.644×** against **0.56×**) while its **allocated-bytes win changes sign** — geometric growth pays **33×** the node copying (13 858 188 against 424 472) on suites 2-7 never saw, turning **0.82× into 1.044×**. The decision stands, taken on live memory; the allocated column should stop being quoted. **Two instrument defects surfaced rather than silently fixed**: the policy table still labels `round-up-16` *"current"* though 2-7 replaced it, and one histogram bucket reads **−15**, so `negativeBucketCounts` now ships in the output. **Purely additive to behaviour**: four benchmark-host files, no engine source touched, nothing that ships changes. **Applies to `14fa4f10`; independent of `0102`, though both were verified applying in sequence with `git am --keep-cr`.** |
| `0102-js-deferral-population` | `Broiler.JS` | **Item 1-1's remaining half: the population counted, and the shortcut that looked available refused by the counter built to test it.** The item defers a function body's expression-tree construction to first invocation — 33.6–63.9% of compile over a population **84–99.7% never invoked** — and names one obstacle: a captured name's index in the enclosing lambda's `Box[]` is decided by `LambdaRewriter` **from the tree**, and a deferred body has none. `0101` built and priced the walk that makes the layout addressable. **What nobody had counted is how many sites need the layout at all**: a function whose free names resolve to nothing an enclosing scope holds captures nothing, is handed no boxes, and is deferrable with the mechanism that already exists. **Counted, over 5 762 function sites: 728 are capture-free, 12.6%** — 7.4% on Mandreel, 39.7% on the flattest corpus. So there is no cheap subset to take first: **87.3% of sites need the capture mechanism**, which is the item. `Dynamic` — the direct-`eval` risk the item's text spends most of its words on — refuses **7 sites of 5 762, 0.1%**, the second time this item's stated risks have come in an order the measurement reverses. **And the reading that looked like an opening is refused.** Mandreel is 1 364 *top-level* declarations, and its 7 605 bound free names are only **165 function-owned**; a script's top-level `var` is a property of the global object per spec, so those looked deferrable for nothing. They are not — **`cellBacked` equals `bound` exactly on all six corpora, 15 118 of 15 118** — because this engine gives a program-level binding a CLR local in the program lambda like any other. ***A spec-level fact about where a binding lives is not a fact about where the compiler puts it.*** **The instrument was made to discriminate before it was pointed anywhere**, which is what makes that equality a finding rather than a claim about the counter: 18 fixtures whose load-bearing half is the negatives — the **same body text in two enclosing scopes gives two different verdicts** (no identifier scan can do that), a parameter or an inner `var` sharing the outer spelling captures nothing (the distinction `NestedFunctionScanner` cannot make), and `cellBacked` is shown to separate from `bound` *before* the equality is reported, via a named function expression's own name, which binds with no CLR local and reads **1 bound / 0 cellBacked** against an ordinary local's 1 / 1. Deliberately breaking the resolver fails **exactly the four capture-detecting fixtures** and leaves the other eleven green. **The denominator is cross-checked rather than asserted**: classified sites match `--compile-profile`'s own function count **exactly on four corpora**, and by **+2 and +1** on the two that evaluate a CodeLoad epilogue — which contains exactly 2 and 1 inline functions. One hazard avoided and pinned: the probe reads the enclosing scope through a new side-effect-free `TryResolveBinding` rather than `GetVariable`, because `GetVariable` sets `RootScope.HasOuterFunctionCaptures` and **that flag is a conjunct of the tiering gate** — a probe built on it would turn tiering *off* for functions it merely asked about; a fixture runs the same program on both settings of the switch. Off by default (`BROILER_JS_DEFER_TREE_COUNT=1`), a compile-time counter on the same terms as `0094`'s and `0097`'s, and it changes no emission on any setting. **Purely additive** — one new file, one new test file, plus counters and 30 lines of benchmark instrumentation, with no existing emission source touched. Whole compiler suite green: **1 241 tests**, +18 and every one of them this patch's. **Applies to `14fa4f10`, and depends on nothing else.** |

**`0102`, `0103` and `0104` are pending against the `Broiler.JS` pinned pointer `14fa4f10`**, on the usual terms: the
push to `Broiler-Platform/Broiler.JS` returned **403** from the session's git proxy, so the pointer
is deliberately *not* bumped and every figure in its section was measured on a local build of the
pin plus those patches. `0102` and `0103` are **independent of each other** — the compiler and its diagnostics against
four benchmark-host files — but **`0104` builds on `0102`**, whose side-effect-free
`TryResolveBinding` its prediction uses, so **`0102` → `0103` → `0104` is the order**, verified by
applying all three in sequence to a clean checkout of the pin with `git am --keep-cr`. None needs a
**main-repo fallback**: `0102`'s and `0104`'s counters are off by default
(`BROILER_JS_DEFER_TREE_COUNT=1`, `BROILER_JS_CAPTURE_LAYOUT_CHECK=1`) and change no emission on any
setting, and `0103` touches only the benchmark host, so nothing CI runs behaves differently with or
without any of them.

## What has cleared

**The handoff has now completed a fourth time, and this time it took all three stacks at
once.** The fifteen files pending at the last reading — `0087` (`Broiler.HTML`), `0088`
(`Broiler.CSS`) and `0089`–`0101` (`Broiler.JS`) — have all been applied, pushed and their
pointers bumped.

They were checked **patch by patch against each submodule's log rather than inferred from this
prose**, which is the only reading of a pointer this campaign now trusts: each patch's `Subject`
resolved to a commit, that commit's own `format-patch` output was diffed against the patch file
(identical once the `From <sha>` line, the blob `index` lines and the trailing git version are set
aside — the whole of the difference on all fifteen), and the pointer each was pending against was
confirmed an ancestor of the new pin.

| Patch | Submodule | Landed as | Pending against |
| --- | --- | --- | --- |
| `0087-html-backdrop-painting-props` | `Broiler.HTML` | `2f94c0d5` | `29bf9c33` |
| `0088-css-nth-child-of-selector` | `Broiler.CSS` | `076ed5d5` | `dba36efb` |
| `0089-js-numeric-tree-order` | `Broiler.JS` | `12760bb9` | `cca39b4d` |
| `0090-js-gc-pause-accounting` | `Broiler.JS` | `48ad65e7` | `cca39b4d` |
| `0091-js-update-target-census` | `Broiler.JS` | `01c79c46` | `cca39b4d` |
| `0092-js-numeric-local-defeat-tests` | `Broiler.JS` | `2bab9775` | `cca39b4d` |
| `0093-js-3-8a-defeat-ab` | `Broiler.JS` | `16389682` | `cca39b4d` |
| `0094-js-speculative-numeric-population` | `Broiler.JS` | `e0bb9b40` | `cca39b4d` |
| `0095-js-speculative-numeric-storage` | `Broiler.JS` | `cfed00ef` | `cca39b4d` |
| `0096-js-speculative-numeric-read-paths` | `Broiler.JS` | `c2667c29` | `cca39b4d` |
| `0097-js-imported-outer-numeric-population` | `Broiler.JS` | `6ff52f3b` | `cca39b4d` |
| `0098-js-async-job-scheduling` | `Broiler.JS` | `ba31a4a9` | `cca39b4d` |
| `0099-js-execution-exclusion` | `Broiler.JS` | `b80327ac` | `cca39b4d` |
| `0100-js-blocking-host-wait` | `Broiler.JS` | `3fa35e14` | `cca39b4d` |
| `0101-js-free-name-scan` | `Broiler.JS` | `14fa4f10` | `cca39b4d` |

**So every figure recorded for those fifteen now describes the pinned pointers directly**, rather
than a local build plus a patch series applied in order, which is what their roadmap sections had
to say while they were pending. The `Broiler.JS` pin is now `14fa4f10`, `Broiler.HTML` `2f94c0d5`
and `Broiler.CSS` `076ed5d5`.

The fifty-two patches this file carried before them (`0001`–`0086`, in seven earlier bumps) had
already cleared the same way.

**A 403 has now meant *deferred* rather than *stranded* four times running**, and what makes that
safe is unchanged: the pointer is never bumped locally, so nothing written here can name a commit
CI cannot clone. The renumbering note the last series carried — `0087`–`0099` written, `+2` shifted
once `main` landed the HTML and CSS pair on the same two numbers — is retired with the series, but
the reason it happened is not, and it is the one thing worth keeping from it: **`patches/` is one
flat namespace across every submodule**, so two branches numbering from the same high-water mark
collide whenever both are open, which is the ordinary case rather than an unlucky one. Number from
`git ls-files patches/` at the moment of writing, and re-verify the chain after any rename.

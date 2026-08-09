# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

The directory is a backlog, not an archive: it holds only what is *currently*
pending. A patch is deleted from here, along with its row below, once its fix is
upstream and the submodule pointer is bumped.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Exercised on CI before they land

`scripts/apply-pending-wpt-patches.sh` applies the patches listed in its
`PENDING_PATCHES` array to the checked-out submodule trees before the WPT run, so
a pending fix is reflected in CI's numbers rather than waiting on the pointer. It
is idempotent — a patch already contained in the pinned pointer reverse-applies
and is skipped — so an entry stops applying by itself once a maintainer lands it.

## Index

| Patch | Submodule | What it is |
|---|---|---|
| `0128-css-style-engine-cache-sharding` | `Broiler.CSS` | **The style engine's three memo caches came off the single `_sync` lock, and a fourth was added so a warm pass has somewhere to publish.** Multithreading roadmap item #12, step 2. `_cache`, `_sparseCache` and `_declaredCascadeCache` were plain dictionaries behind one `Monitor`, which made every cache probe on the cascade's hot path a lock acquire — the shortest possible critical section and therefore the one with the worst work-to-synchronisation ratio once more than one thread cascades. They are `ConcurrentDictionary` now. **What deliberately did not change is the generation-guard protocol**: a cascade still captures `_cacheGeneration` up front and still publishes under `_sync`, so "is this result still current" and "publish it" cannot be split by a concurrent `InvalidateAll`, and `InvalidateAll` still clears inside the lock even though the caches no longer need it for their own sake. That guard is what makes the lock-free compute window correct, so it survives by intent rather than by omission. **The fourth cache is the point of the patch.** `GetCascadedStyle` memoized nothing at its own level — only its inner declared cascade did — so custom-property and `var()` resolution, CSS-wide keywords, shorthand expansion, `attr()` substitution and relative font weights were recomputed on every call. Within one render that saves nothing, because a render asks for each element once; it exists as the store `0129`'s warm pass writes into. **Exit gate:** `CssStyleEngineConcurrencyTests` grows a second case, and it tests a different thing from the first — the existing one says the caches survive concurrent access and never compares a value, so it cannot say the *answers* do. The new one takes a sequential cascade over one document and an eight-thread cascade over a second, structurally identical one, and compares property by property. `Broiler.CSS.Dom.Tests` 384 passed with the two `CssDomArchitectureTests` failures that are pre-existing (verified on a clean tree), `Broiler.CSS.Tests` 341/341. **Must be applied with `0129`** — that patch's warm pass is what makes this one do anything. |
| `0129-html-cascade-substage-trace-and-warm-pass` | `Broiler.HTML` | **`parse+cascade` is timed sub-stage by sub-stage, and every element's cascade is resolved on a thread budget before the box walk consumes it.** Multithreading roadmap items P0-a (the split Phase 2 §9 said the document owed) and #12. Small — one file, two call sites — because both types it calls live in the main repository (`Broiler.Layout`), the way `TileParallelReplay` does for item #5, so the main repo builds and its tests compile whether or not this patch is applied. **The measurement half:** the four sub-stages are wrapped in `RenderStageTrace` scopes, which are off by default and cost a static `bool` read and a null-check `Dispose` when they are. It had to be instrumentation rather than P0-a's usual out-of-band subtraction, because none of the four is a pure function of the source — each consumes the tree and style set the previous one produced. The answer: **the cascade is 81.3–98.2% of the stage on every corpus page and both parse halves together are 0.5–4.8%**, so the stage's name overstates the parse on every page including the one with 211 592 characters of source. **The threading half:** `CssStyleRecalc.Warm` runs before `CascadeApplyStyles`. The obvious reading of item #12 is to thread that walk over sibling subtrees, and the walk cannot be split without changing what it produces — it rewrites `display` when `float` is set so children observe the corrected value, pushes `text-decoration` down onto children, hides a closed `<details>`'s subtree *after* cascading it, and inserts generated `::before`/`::after` boxes into child lists on the way back up, the last two writing to nodes the walk has already left. The expensive part is not in the walk anyway: per box it is one `GetCascadedStyle` call, which reads the canonical DOM and the registered stylesheets and mutates neither, so it is a pure function of state that is already final. The warm pass resolves all of it on `BROILER_STYLE_THREADS` and the walk below is byte-for-byte the walk that was there, reading cache hits. **Measured: cascade stage 1.16–2.16× at four threads, 1.06–2.10× end to end**, with the harness now publishing the serial residue per page (16–55%) so what is left is stated rather than guessed. **Exit gate:** pixel-identical at 1/2/4 across the corpus (`--style-scaling`, which fails the run on a single differing byte) and at 1/2/3/4/8 over 22 `ParallelStyleRecalcTests` cases across five documents chosen for what makes an element's cascade depend on something other than itself, with a guard test asserting each document clears the warm pass's element threshold so no equality assertion can pass vacuously. **Must be applied with `0128`**, which supplies the memo the warm pass writes into; without it the pass computes correct results and discards them, which is correct and pure overhead. |
| `0130-js-tests-reenable-parallelization` | `Broiler.JS` | **`Broiler.JavaScript.BuiltIns.Tests` runs its cases in parallel again.** Multithreading roadmap item #21. The assembly set `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, and the roadmap's guess about why was right: the engine used to dispatch promise continuations, async-function resumptions and generator steps onto `ThreadPool` threads whenever no synchronization context was installed — which is exactly the situation in a unit test — so two tests at once could have their continuations interleaved onto each other's ambient context. Item #15 removed that dispatch. What makes it safe rather than merely no-longer-obviously-unsafe is that a context is reachable from exactly one thread: `JSEngine.Current` is `[ThreadStatic]` and the async-local mirror that restores it across await points is `AsyncLocal`, so two xUnit threads each holding their own `JSContext` cannot see each other's at all. What they do share is process-wide and already concurrent — interned key strings, the built-in registry's static constructors (serialised by the CLR), and `DictionaryCodeCache.Current`, whose per-key compilation is serialised by `Lazy<T>`. **Measured: 2 118 tests, 0 failures, 57–59 s serial against 31–37 s parallel on four cores, three runs each (~1.75×).** The file keeps a note on what to look for if it ever has to go back: a new piece of process-wide mutable state, not a new test. **Deliberately NOT listed in `scripts/apply-pending-wpt-patches.sh`** — the WPT run never builds this assembly, so applying it there would exercise nothing. |

**All three are pending because the push was denied.** `Broiler-Platform/Broiler.HTML`,
`Broiler-Platform/Broiler.CSS` and `Broiler-Platform/Broiler.JS` are outside this
session's GitHub scope, so the git proxy declined to inject a credential and every
push returned 403. The submodule pointers are therefore **not** bumped — CI clones
submodules by pointer and would break on a commit it cannot fetch.

**One ordering constraint, and it is the only one.** `0128` and `0129` are a
single change (multithreading item #12) split across two submodules because the
memo lives in `Broiler.CSS` and the call site in `Broiler.HTML`. Apply them
together. Either alone is *correct* — `0129` without `0128` resolves every
element's cascade and then discards the results, and `0128` without `0129` adds a
memo nothing reads twice — but neither alone is the feature. Everything else here
is independent and can be applied in any order.

All three were verified against their pinned pointer the way this file's
instructions apply them: `git am --keep-cr` onto a clean checkout of the pin,
then the resulting tree diffed against the branch the patch was generated from
(identical in every case).

`0124`/`0125` (band-parallel scanline fills and the glyph outline cache) and now
`0126` (tile-parallel replay, `Broiler.HTML`) and `0127` (raster band parallelism,
`Broiler.Graphics`) were the previous entries. All four are upstream and all the
pointers are bumped, so they reach CI through the pointer; their files and rows
are deleted because this directory is a backlog, not an archive. `0126`'s entry in
`scripts/apply-pending-wpt-patches.sh` is removed with it — the idempotence guard
would skip it from here on, and an entry that can only ever skip is noise.

**How to tell, rather than assume.** A patch is upstream when it *reverse-applies*
to the pinned pointer's tree — `cd <Submodule> && git apply --reverse --check
../patches/NNNN-<slug>.patch` succeeding means the fix is already there. That is
the same test `apply-pending-wpt-patches.sh` uses for its idempotence guard, and
it is worth running over the whole directory at the start of a task: a patch that
has landed since the file was last edited is otherwise indistinguishable from one
that has not, and re-applying it fails confusingly rather than harmlessly.

<!-- Retired index note, kept for the record:

**Empty — nothing was pending.** `0123-css-cascade-rule-index` was the last entry;
it is upstream as `Broiler.CSS` `377c6dd` on `claude/css-cascade-rule-index` and
the submodule pointer is bumped, so the fix now reaches CI through the pointer
rather than through `apply-pending-wpt-patches.sh`. Its `PENDING_PATCHES` entry is
removed for the same reason: the script's idempotence guard would have skipped it
from here on, and an entry that can only ever skip is noise.

-->

<!-- Retired index row, kept for the record of what 0123 claimed and measured:

| `0123-css-cascade-rule-index` | `Broiler.CSS` | **The cascade tested every rule of every sheet against every element; it now tests only the rules an element's own id, classes and tag can reach.** Multithreading roadmap item #11 — the item that document's Phase 0 measurements identify as its highest-value one, ahead of every parallel item including the rasterizer, and single-threaded work. The evidence it was written against: on the roadmap corpus's rule-heavy page (700 `<div>`s, two classes each, against a 900-rule sheet) `parse+cascade` was **96.5% of a 5.2 s render**, and `RuleScalingBenchmarks` — which holds the *matched* rule count fixed at four while growing the sheet — measured cost and allocation both linear in **total** rules: 32× the rules gave 30.8× the time and 32.0× the bytes. `CssCascadeRuleIndex` flattens the sheet set into document order once and files each rule under the simple selector its subject must carry (id / class / type), with everything else in a universal bucket; an element merges the buckets its own keys reach and tests those. **The correctness argument is one-sided by construction and that is the whole point**: a key is a *necessary* condition for a match, never a sufficient one, candidates still go through the unchanged selector matcher, so the index can only be wrong by *omitting* a rule — and every case that is not certainly narrower resolves to the universal bucket (a bare `:is()`/`:not()`, an attribute-only compound, a namespaced type, an escaped identifier, anything the key scanner does not model). Candidates come back in **document order**, via a k-way merge of the buckets rather than a sort, so the cascade's source-order tie-break is untouched — a rule that never matched never advanced the tie-break counter, so visiting only candidates yields the same winners. `@media`/`@supports` are resolved once at index-build time (they depend on the environment and the feature oracle, both fixed for an index's lifetime) so a non-applying group costs nothing per element; `@container` depends on the element, so those conditions travel with the entry and are evaluated per candidate. The index is memoized against a **sheet** generation bumped only by sheet/environment changes — deliberately not by DOM mutation, which bumps the computed-style caches constantly and would otherwise rebuild the index on every tree edit. **The linear scan is kept and kept reachable** behind `CssStyleEngine.UseRuleIndex`, as the index's oracle: `CssCascadeRuleIndexTests` (22 cases) asserts the two cascades agree property by property — id/class/type keying, source-order tie-breaks, a rule reachable through two keys, combinators, pseudo-elements, the six unkeyable selector shapes, matching and non-matching `@media`, `@supports`, nested groups, late-added sheets, `ClearStyleSheets`, a viewport change — and over a generated 1200-rule sheet against a 120-element document. **Measured, and the exit gate is the scaling shape rather than a single number**: with `RuleScalingBenchmarks` holding the matched set at four and growing only the rules that cannot match, 32× the rules used to cost 30.8× the time and 32.0× the bytes and now costs **1.64× the time and 1.13× the bytes** — 114.9 ms → 15.74 ms at 100 rules, 3 543.6 ms → **25.89 ms** at 3 200 (136.9×), with allocation 5 817.9 MB → **9.69 MB** (600×). On a whole render the corpus `rules` page goes **5 218.96 ms → 1 841.71 ms** end to end and its `parse+cascade` stage **5 035.35 ms → 1 698.11 ms** (2.8× and 3.0×) — the smaller figure is Amdahl, since that stage also parses 110 296 characters and then cascades the rules that do match. **No rendering change**: `css/css-backgrounds` + `css/CSS2/linebox` (62 tests) is identical before and after — same pass/fail/skip on every test and the same 98.62245927777205% average pixel match to fourteen digits. Component suites: `Broiler.CSS.Tests` 341/341, `Broiler.CSS.Dom.Tests` 383 passed with the two `CssDomArchitectureTests` failures that are **pre-existing** (verified on a clean tree: 361 passed, the same 2 failed). Applies cleanly to the pinned `Broiler.CSS` pointer (`edc9fa9`) and survives `git am --keep-cr` — verified by applying it to a fresh clone of the pin and diffing the result against the branch it was generated from (identical tree). **Listed in `scripts/apply-pending-wpt-patches.sh`**: unlike a thread-safety or scheduling patch, this one *could* move rendering if the key extraction were ever wrong, so having the WPT run exercise it against the pinned pointer is the check worth having — and its claim is precisely that the numbers do not move. |

-->

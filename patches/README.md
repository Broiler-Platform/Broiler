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
git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

## Index

| Patch | Submodule | Note |
| --- | --- | --- |
| `0070-js-restart-contract` | `Broiler.JS` | Item 4-3a. States the contract the tiering pilot's bailout runs under — it is *restart*, not resume, so it is sound only if every guard fires before any observable effect, the bailout leaves no `CallFrameStack` slot behind, and the body is not suspendable. The first two hold for good reasons and are now written down on `NumericLoopPlan.Compile`. **The third held only by accident, twice over**: `EnableTiering` sits inside `CreateFunction`'s ordinary-function `else` branch, and the tiering gate borrows `CanScalarReplaceLocals`, which refuses generators for its own unrelated reasons. Defeating both — each an ordinary refactor away — a legal `async function` whose body matches the planner's shape returns **`number` instead of a Promise** from its second call on. Fixed with one condition at the decision point (`NumericLoopPlanner.TryCreate` refuses a suspendable function), which restores correct answers with both accidental exclusions still defeated. Adds `RestartContractTests` (16 cases). **Independent of `0067`–`0069`.** |
| `0069-js-type-feedback-collection` | `Broiler.JS` | Item 4-1. The inline caches observe shapes to answer the *current* read — entries are replaced when stale and dropped once a site passes four shapes — so they cannot say what a site saw over a whole run, which is what a specializing tier speculates on. `TypeFeedback` retains, per site, the distinct receiver shapes at a read and the distinct callee identities at a call. **Two gates:** property feedback is a runtime flag inside the site helper (which already pays a branch per read for the cache-hit counter); call feedback is gated at *compile* time, so with the flag clear the emitted call is unchanged — a call costs ~255 ns and instrumenting it unconditionally to measure it would be self-defeating. The item buys no throughput, so the deliverable is what it reports: over seven Octane suites, **93.54% of 37.9 M property reads and 96.70% of 4.24 M calls happen at a site that only ever saw one shape or one callee**, weighted by executed operations. That is the premise 4-2 and 4-4 rest on, measured for the first time. Megamorphism is essentially absent (18 sites in total). Adds `--type-feedback` and `TypeFeedbackTests` (16 cases). **Independent of `0067`/`0068`** — different assemblies, applies in any order. |
| `0068-js-block-scoped-numeric-var` | `Broiler.JS` | Item 3-3's third half, which completes the item. A `var` declared inside a block is hoisted to the function, so between entry and the block it is observably `undefined` — a raw double hoisted to 0 answers 0. The rule is the function body's own dominance argument one level down, with two admissions: an unlabelled `{ … }` that is a direct statement of the body (or of another such block) is **transparent** — entered whenever reached, exits only via `return`/`throw` — so its `var`s need no extra condition; any other block **confines** its declaration, which then needs every reference inside it, and that is the loop-body temporary the item is about. Only a *direct* statement of the block qualifies, which is what excludes `if (c) var t = 1;`; a labelled block and a `catch` are excluded too. **`block-var` 31.98 → 0.00 B/iteration and 1 → 3 numeric locals**, all twelve other `--local-alloc` rows byte-identical. Two defects were caught by testing and neither shipped — a non-dominating declaration marking a name readable, and the over-correction that fix caused. Adds `BlockScopedVarNumericLocalTests` (43 cases). **Apply after `0067`** — same file, and it builds on the gate `0067` restructures. |
| `0067-js-numeric-lexical-bindings` | `Broiler.JS` | Item 3-3's second half. A function-body-top-level `let` or `const` the compiler proves only ever holds a number now lives in a raw CLR `double`, as an eligible `var` already did: **`let-binding` and `const-binding` 31.98 → 0.00 B/iteration and 1 → 3 numeric locals**, identical to the eligible floor, with all twelve other `--local-alloc` rows byte-identical (both arms from one tree). Only the **numeric** tier admits lexical names — the JSValue tier stays closed, because a `let`'s TDZ and a `const`'s read-only-ness live in the cell either tier removes and only the numeric gate discharges them (the dominance argument makes the TDZ throw unreachable; a written `const` is rejected outright). Also moves the `NumericStorage` test *before* the lexical branch in `VisitVariableDeclaration`, since a numeric local's `Expression` is a boxing read. Adds `LexicalNumericLocalTests` (58 cases, including the withdrawn first attempt's reproduction as a pinned test) and `scripts/compliance/test262-lexical-declarations.txt`, a manifest for the `let`/`const`/`block-scope` paths **no pinned manifest covered**. **Independent of everything cleared below** — it applies to the pin directly. |

**`0067`–`0070` are pending against the pinned pointer `9bf9639b`.** All four were applied in
sequence from a clean checkout of that commit with **`git am`**, not merely `git apply` — the two
differ, and this README's own instructions use `am`, so `apply` is not the check. **`0068` must
follow `0067`**: it edits the same file and builds on the gate `0067` restructures. `0069` and
`0070` touch different assemblies again and apply in any order relative to the others.

Each push to `Broiler-Platform/Broiler.JS` returned **403** from the session's git proxy, so the
pointer is deliberately *not* bumped. **No main-repo fallback is needed for any of them:**
`0067` and `0068` are optimizations with no behaviour difference, so CI without them is correct
and only allocates more; `0069` is collection that is off unless `TypeFeedback.Enabled` is set,
so without it CI simply cannot report the distribution; and `0070` closes a hazard that is not
reachable in the tree as it stands — two other conditions currently exclude the same case — so
CI is correct without it and one refactor less safe.

The eighteen patches this file carried before it (`0049`–`0066`) have all been applied, pushed
and their pointers bumped; they are listed under *Recently cleared* below with the commit each
landed as. Every one was verified against the submodule log rather than inferred from this
prose, and each landed commit was checked to be an ancestor of the pin with
`git merge-base --is-ancestor`.

## Recently cleared

Kept only long enough to be useful; delete a row once nobody is reading the
results it explains.

| Patch | Submodule | Landed as | Note |
| --- | --- | --- | --- |
| `0046-js-octane-suite-engine-fixes` | `Broiler.JS` | `7ef80c03` | Five engine defects behind the five failing Octane 2.0 suites: non-strict `eval` dropping `var` initializers in nested functions, `obj == null` running `ToPrimitive`, `undefined + x` string-concatenating, the last expression of a C-style `for` head's comma list being discarded, and a missing `read` shell builtin. |
| `0047-js-numeric-local-assignment-value-position` | `Broiler.JS` | `8228b0da` | A scalar-replaced numeric local's assignment produced a raw `double` in value position, so the CLR rejected the compiled method (`InvalidProgramException`). Blocked no Octane suite. |
| `0048-js-stack-limit-reserve` | `Broiler.JS` | `cdb2fd41` | `JSContextOptions.MaxStackUsageBytes` — a stack reserve so a `catch` can still run after an overflow. Defaults to 0 (disabled); the `--script-host` shell opts in at 16 MiB with a 12 MiB budget. |
| `0049-js-compilation-stack` | `Broiler.JS` | `43bc4230` | Item 1-2's mitigation. Compilation runs on a thread the engine sizes (`CompilationStack`, 64 MiB, `BROILER_JS_COMPILE_STACK_BYTES`, `0` to opt out), so deeply nested source no longer aborts the process. Three passes overflowed, in three assemblies. Sources under 512 characters compile in place, which keeps the cost inside the noise band. |
| `0050-js-construct-prototype-invalidation` | `Broiler.JS` | `2df877a0` | Item 2-0. Every `new` published a global prototype-mutation notice and retired every prototype-keyed inline-cache entry in the process — 200 001 invalidations per 200 000 allocations, against 0 for the same objects as literals. Installs the instance prototype by construction instead. |
| `0051-js-store-cache-property-creation` | `Broiler.JS` | `5d31617a` | Item 2-1. A store that *creates* its property could never hit the store cache: 0 hits against 600 000 misses → 599 997 / 3, ~20% on a constructor loop. Adds a transition entry form; its prototype guards are only affordable because of `0050`. |
| `0052-js-object-allocation-metrics` | `Broiler.JS` | `32701894` | Benchmarks only. The `--object-alloc` emitter, which re-specified 2-3 and found 2-7. |
| `0053-js-array-shape-eligibility` | `Broiler.JS` | `641241af` | Item 2-2. Shape eligibility was an exact `GetType() == typeof(JSObject)` test, so every named property on a `JSArray` was a 100% miss — 0 → 199 999. `a.length` is unchanged and cannot change. |
| `0054-js-update-expression-cache` | `Broiler.JS` | `f9c2193f` | Item 2-4's update half. `obj.name++` reached neither cache — 0 hits *and* 0 misses — and now takes both, 199 999 each side. |
| `0055-js-function-shape-eligibility` | `Broiler.JS` | `850121a0` | Item 2-8, with the fix for the DeltaBlue regression an earlier draft shipped folded in. Statics on a constructor function were a 100% miss, which is DeltaBlue's hot path at 601× off: 0 → 199 999. Two prerequisites (bare `ownProperties.Put` installs, Annex B deferred cells) had to be fixed first. |
| `0056-js-compound-assignment-cache` | `Broiler.JS` | `c5842c9d` | Item 2-4's compound half, and it corrects the reason `0054` gave for skipping it. Twelve operators; `&&=`/`||=`/`??=` stay out because their write is conditional. Median paired ratio 0.903 against a control at 1.002. |
| `0057-js-property-map-distribution-metrics` | `Broiler.JS` | `55c6b1fb` | Storage instrumentation plus an emitter, no behaviour change. `--property-map-distribution` records the final node-group count of every map and simulates each candidate growth policy against it. A node is 56 B, so the old floor was 920 B per object with any named property. |
| `0058-js-property-map-growth-policy` | `Broiler.JS` | `a6f101cc` | Item 2-7, decided by `0057`'s run of record: 43.9% of 47 M maps never outgrow one four-node group. Geometric growth from one group — live map bytes 0.56×, allocated 0.82×, `ctor-1` 1 256 → 584 B. Real losing side: an 8-field object pays ~27% more bytes. |
| `0059-js-single-match-replace-one-allocation` | `Broiler.JS` | `962ca06a` | Phase 5's first follow-up to item 1. A single-match `replace` assembled its answer through a `StringBuilder`, costing two full UTF-16 copies of the subject. `string.Concat` over three spans writes it in one allocation: **4.020 → 2.020 bytes per subject character**, in `RegExp.prototype[@@replace]` *and* in `String.prototype.replace`'s string-`searchValue` builtin. |
| `0060-js-stream-global-replace` | `Broiler.JS` | `6f56d24f` | Phase 5's second follow-up. §22.2.6.11 collects every match before reading any of their properties, so a global replace held one result array per match live — **2 032.8 B per match, dead linear**. Streams when nothing can observe the results: **478.3 B/match, 0.235×.** |
| `0061-js-measure-2-9-materialization-cause` | `Broiler.JS` | `e6222df3` | Measurement and instrumentation only, no behaviour change. Refuted item 2-9's losing-side hypothesis against the control it never had (a *strict* function): every function materializes its trie exactly once either way, so the planned follow-up was withdrawn before being built. |
| `0062-js-array-length-keeps-shape` | `Broiler.JS` | `0812d80d` | Item 2-10. `JSArray.SetLengthWritable` recorded a `length` descriptor through `GetOwnProperties()`, abandoning the object's shape permanently — on the grow path, so `push`/`pop`/`concat` each cost an array its shape on first use. **DeltaBlue's dictionary fallbacks 2 503 → 0**, though its hit rate and score do not move. |
| `0063-js-prototype-rewrite-no-invalidate` | `Broiler.JS` | `4d1c4796` | Item 2-11. A write re-applying the prototype an object already had retired **every** prototype-keyed cache entry in the process — once per `new` for a class. **Richards's read hit rate 86.61% → 99.97%**, DeltaBlue 65.96% → 69.45%, Box2D 96.39% → 97.72%. |
| `0064-js-refresh-stale-cache-entry` | `Broiler.JS` | `fb1e2f4c` | Item 2-12. The cache's add path deduplicated on two keys while a hit checked six, so a stale entry was declined rather than refreshed and its site missed for the rest of the process — 77.7% of DeltaBlue's misses. Refreshing in place: **DeltaBlue 69.45% → 93.16%**, Box2D → 98.83%. |
| `0065-js-linear-closure-rewrite-scope` | `Broiler.JS` | `1070525a` | Item 1-4. `LambdaRewriter.Scope` held a lambda's in-scope bindings in a `List` and asked it `Contains` once per parameter reference, so IL emission was **quadratic in a scope's binding count**. A reference-keyed multiset makes it linear: **28.5× on that shape**, Mandreel's front end 21 307 → 7 015 ms. |
| `0066-js-defer-nested-lambda-il` | `Broiler.JS` | `9bf9639b` | Item 1-1's emission half. A nested function's `DynamicMethod` is now generated on first invocation, memoized per syntactic site. **jQuery 0.661×, Box2D 0.636×, PdfJS 0.689×** on compile, allocation ~0.52×, steady state 1.0009×; **Octane CodeLoad 94.6 → 104.0**. `BROILER_JS_DEFER_IL=0` restores eager generation. |

All twenty-one are ancestors of the pinned `Broiler.JS` pointer **`9bf9639b`**. `0046`–`0048`
were bumped in `2d9f39ca` on 2026-08-01; `0049`–`0058` — the whole of item 1-2's mitigation
and phase 2 — on 2026-08-02; `0059`–`0066` — phase 5's follow-ups, items 2-10…2-12, and
phase 1's 1-4 and 1-1 — on 2026-08-03. The patch handoff
[`docs/performance-roadmap.md`](../docs/performance-roadmap.md) §0 listed as owed is retired,
and has stayed retired across two further bumps.

`0046`–`0048` changed core JavaScript semantics (`+`, `==`, the `for` head, `eval`
scoping, and assignment codegen), which is why they were deliberately kept out of
`scripts/apply-pending-wpt-patches.sh`'s `PENDING_PATCHES` while pending: a large
share of WPT exercises those indirectly, so carrying them there would have moved
the WPT job's numbers without the re-baseline such a move needs. That re-baseline
has since happened on its own — `ff819e06` refreshed
`tests/wpt-baseline/failed-tests.json` for a **net 36 fewer failures** (50
removed, 14 added).

**The committed Octane results in `tests/octane/results/` are no longer stale.** They were
regenerated by the workflow on 2026-08-03 at the pointer above and now report **17 of 17
scores with all 15 suites `ok`**, against the same-machine Chromium and Jint columns — which
closes the headline half of item 0-6 in
[`docs/performance-roadmap.md`](../docs/performance-roadmap.md), the plan of record now that
`tests/octane/roadmap.md` has been merged into it.

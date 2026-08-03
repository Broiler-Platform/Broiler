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
| `0059-js-single-match-replace-one-allocation` | `Broiler.JS` | Phase 5's first follow-up to item 1. A single-match `replace` assembled its answer through a `StringBuilder`, costing two full UTF-16 copies of the subject — into the chunk list, then back out through `ToString()`. `string.Concat` over three spans writes it in one allocation: **4.020 → 2.020 bytes per subject character**, exactly the predicted halving, in `RegExp.prototype[@@replace]` *and* in `String.prototype.replace`'s string-`searchValue` builtin, which had the same three appends into a builder that was already sized exactly right. Adds `SingleMatchReplaceAllocationTests` (41 cases) and a `replace-one-string` profile row. |
| `0060-js-stream-global-replace` | `Broiler.JS` | Phase 5's second follow-up. §22.2.6.11 collects every match before reading any of their properties, so a global replace held one result array per match live — **2 032.8 bytes per match, dead linear**, 10.3 MB for one 5 000-match call. Streams instead when nothing can observe the results: the receiver's `exec` is the pristine `%RegExp.prototype.exec%` captured at realm init, the replacement is a string, and it contains no `$`. **478.3 B/match, 0.235x.** Splits `Exec` into `ExecMatch` + `BuildExecResult` so both paths share the `lastIndex`/sticky/statics code, and adds `JSContext.IntrinsicRegExpExec` and `GlobalReplaceStreamingTests` (23 cases). **Apply after `0059` — it builds on the same function.** |

| `0061-js-measure-2-9-materialization-cause` | `Broiler.JS` | Measurement and instrumentation only, **no behaviour change**. Item 2-9's losing-side hypothesis said the Annex B deferred cells force the trie rebuild; a *strict* function is the control it never had, and it rebuilds just as much — **1.00 per function on all four rows**. What materializes is the `prototype` install, withheld from shape-only storage by 2-8's DeltaBlue fix, so the planned "stop materializing for a deferred cell" follow-up is withdrawn before being built. Adds `--deferred-cell-cost` and a `RecordNamedPropertiesMaterialized` counter. **Independent of `0059`/`0060`** — different files, applies in any order. |

The ten `Broiler.JS` patches this file previously carried (`0049`–`0058`) have all been
applied and their pointer bumped; they are listed under *Recently cleared* below with the
commit each landed as.

**`0059`, `0060` and `0061` are pending against the pinned pointer `2ebc0c3c`.** `0059` and
`0060` must go in that order — `0060` touches the function `0059` restructures, so applying
them out of order will conflict. `0061` touches a disjoint set of files and applies in any
order relative to the other two. Each push to `Broiler-Platform/Broiler.JS` returned 403 from
the session's git proxy, so the pointer is deliberately *not* bumped. None of the three needs
a main-repo fallback: `0059` and `0060` are allocation reductions with no behaviour
difference, so CI is correct without them and only more allocating, and `0061` adds a
benchmark emitter and an opt-in counter that is off unless
`PropertyOptimizationDiagnostics.Enabled` is set.

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

All thirteen are ancestors of the pinned `Broiler.JS` pointer **`a6f101cc`**. `0046`–`0048`
were bumped in `2d9f39ca` on 2026-08-01; `0049`–`0058` — the whole of item 1-2's mitigation
and phase 2 — are in the pointer as of 2026-08-02, which retires the patch handoff
[`docs/performance-roadmap.md`](../docs/performance-roadmap.md) §0 listed as owed.

`0046`–`0048` changed core JavaScript semantics (`+`, `==`, the `for` head, `eval`
scoping, and assignment codegen), which is why they were deliberately kept out of
`scripts/apply-pending-wpt-patches.sh`'s `PENDING_PATCHES` while pending: a large
share of WPT exercises those indirectly, so carrying them there would have moved
the WPT job's numbers without the re-baseline such a move needs. That re-baseline
has since happened on its own — `ff819e06` refreshed
`tests/wpt-baseline/failed-tests.json` for a **net 36 fewer failures** (50
removed, 14 added).

**The committed Octane results in `tests/octane/results/` predate every pointer
bump above** (generated 2026-07-31 20:28) and still show the five suites failing —
on an engine that is now ten commits further on. They are stale, not wrong about the engine as it was; the
Octane workflow needs a re-run — item 0-6 in
[`docs/performance-roadmap.md`](../docs/performance-roadmap.md), which is the plan of
record now that `tests/octane/roadmap.md` has been merged into it.

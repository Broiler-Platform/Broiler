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

| Patch | Submodule | Targets | Note |
| --- | --- | --- | --- |
| `0053-js-array-shape-eligibility` | `Broiler.JS` | [`docs/performance-roadmap.md`](../docs/performance-roadmap.md) items 2-2 and 2-8 | **Apply after `0051`.** Shape eligibility was an exact `GetType() == typeof(JSObject)` test in six places, so every named property on a `JSArray`, a `JSFunction` or any exotic was a 100% cache miss — measured 0 hits against 200 000. Makes the gate a virtual `SupportsShapeTracking` (following the existing `SupportsOrdinaryIndexedWrite` pattern) and opts `JSArray` in: named reads and writes on arrays go 0 → 199 999 hits. `a.length` is unchanged and cannot change — it is computed from the element store, not held as a data property. Functions are deliberately NOT opted in: they install `length`, `name` and `prototype` with bare `ownProperties.Put`, so their shape would claim a key set missing three keys every function has, breaking the invariant `GetPrototypeLookupShapeId` and the transition entry both rely on — recorded as item 2-8 with its prerequisites, and it is where DeltaBlue's 601× score is. 12 tests in `PropertyShapeCacheTests`, including the bound: growing an array through a built-in reaches `GetOwnProperties(create: true)` and abandons the shape by design. Suite 7 319 tests, 0 failures. |
| `0052-js-object-allocation-metrics` | `Broiler.JS` | [`docs/performance-roadmap.md`](../docs/performance-roadmap.md) items 2-3 and 2-7 | Independent of the other patches (benchmarks only, no engine change). Adds an `--object-alloc` emitter reporting bytes per object by construction form and field count, because item 2-3's only surviving justification was memory and there was no way to measure memory per object from a clean checkout. Its first run re-specified 2-3 (the slot array is 4.5% of an object's bytes) and found 2-7: the property map's trie allocates from a 16-node floor, so **one field costs the same as three and ~1 040 B more than none**. |
| `0051-js-store-cache-property-creation` | `Broiler.JS` | [`docs/performance-roadmap.md`](../docs/performance-roadmap.md) item 2-1 | **Apply after `0050`, which it depends on.** A store that *creates* its property could never hit the store inline cache — the cache only described overwriting an existing slot, so the shape it recorded after the write never matched the shape the next object presented before it. Measured 0 hits against 600 000 misses for 200 000 three-field constructions; now 599 997 / 3, and ~20% faster on a 2 M-iteration constructor loop (median paired ratio 0.797 over four interleaved pairs). Adds a transition entry form guarded by shape identity, receiver-prototype identity, the global prototype version and extensibility — the prototype guards only hold up because `0050` stopped `new` from advancing that version per allocation. 17 tests in `PropertyStoreCacheTests`; removing the two hit-time prototype guards fails four of them. Suite 7 307 tests, 0 failures. **test262 `properties-proxy` and `strict-mode` are owed** — this touches the last step of `OrdinarySetWithOwnDescriptor` and the phase-2 exit gate requires them. |
| `0050-js-construct-prototype-invalidation` | `Broiler.JS` | [`docs/performance-roadmap.md`](../docs/performance-roadmap.md) item 2-0 | `OrdinaryCreateFromConstructor` installed an instance's prototype by overwriting the one the `JSObject` constructor had just set, and that second write reads as a `[[SetPrototypeOf]]` on a live object — so every `new` published a global prototype-mutation notice and retired every prototype-keyed inline-cache entry in the process. Measured 200 001 invalidations for 200 000 allocations against 0 for the same objects built as literals; an inherited-method site inside an allocating loop ran at a 50% hit rate (199 999 / 200 002) and now matches its hoisted control (399 998 / 3), ~11% faster on wall clock. Installs the prototype by construction at the two allocation sites instead, routing them through P1-2's existing guard rather than adding one. `PropertyShapeCacheTests` gains six tests, including every staleness path re-checked *with* an allocation in the loop — the removed invalidation was conservative, so it had been making those tests pass for the wrong reason. Suite 7 290 tests, 0 failures. |
| `0049-js-compilation-stack` | `Broiler.JS` | [`docs/performance-roadmap.md`](../docs/performance-roadmap.md) item 1-2, mitigation step | Compilation runs on a thread the engine sizes (`CompilationStack`, 64 MiB, `BROILER_JS_COMPILE_STACK_BYTES` to change, `0` to opt out), so deeply nested source no longer aborts the process. **Three** passes overflowed, in three assemblies — `FastParser`'s recursive descent, `SyntaxValidation`/`AstReduce`, and `ILCodeGenerator` — at ~19 400 nested operators on the shell's 16 MiB thread. Workers are parked and reused, and a source under 512 characters compiles in place (nesting depth cannot exceed source length), which is what keeps the cost inside the noise band. Adds `DeeplyNestedSourceTests`; repository suite 7 288 tests, 0 failures. |

**No main-repo fallback exists for 0049**, unlike #1119's: every pass that overflows
is inside `Broiler.JS`, so there is no outer layer that can stand in. Until the patch
is applied, deeply nested source still aborts the process on any host whose JavaScript
thread is smaller than the source's nesting depth needs — which on the evidence to hand
is win-x64 rather than the Linux CI, so the workflow will not show it.

## Recently cleared

Kept only long enough to be useful; delete a row once nobody is reading the
results it explains.

| Patch | Submodule | Landed as | Note |
| --- | --- | --- | --- |
| `0046-js-octane-suite-engine-fixes` | `Broiler.JS` | `7ef80c03` | Five engine defects behind the five failing Octane 2.0 suites: non-strict `eval` dropping `var` initializers in nested functions, `obj == null` running `ToPrimitive`, `undefined + x` string-concatenating, the last expression of a C-style `for` head's comma list being discarded, and a missing `read` shell builtin. |
| `0047-js-numeric-local-assignment-value-position` | `Broiler.JS` | `8228b0da` | A scalar-replaced numeric local's assignment produced a raw `double` in value position, so the CLR rejected the compiled method (`InvalidProgramException`). Blocked no Octane suite. |
| `0048-js-stack-limit-reserve` | `Broiler.JS` | `cdb2fd41` | `JSContextOptions.MaxStackUsageBytes` — a stack reserve so a `catch` can still run after an overflow. Defaults to 0 (disabled); the `--script-host` shell opts in at 16 MiB with a 12 MiB budget. |

All three are ancestors of the pinned `Broiler.JS` pointer `cdb2fd41`, bumped in
`2d9f39ca` on 2026-08-01.

They changed core JavaScript semantics (`+`, `==`, the `for` head, `eval`
scoping, and assignment codegen), which is why they were deliberately kept out of
`scripts/apply-pending-wpt-patches.sh`'s `PENDING_PATCHES` while pending: a large
share of WPT exercises those indirectly, so carrying them there would have moved
the WPT job's numbers without the re-baseline such a move needs. That re-baseline
has since happened on its own — `ff819e06` refreshed
`tests/wpt-baseline/failed-tests.json` for a **net 36 fewer failures** (50
removed, 14 added).

**The committed Octane results in `tests/octane/results/` predate the pointer
bump** (generated 2026-07-31 20:28, bumped 2026-08-01 11:45) and still show the
five suites failing. They are stale, not wrong about the engine as it was; the
Octane workflow needs a re-run — item 0-6 in
[`docs/performance-roadmap.md`](../docs/performance-roadmap.md), which is the plan of
record now that `tests/octane/roadmap.md` has been merged into it.

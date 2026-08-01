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

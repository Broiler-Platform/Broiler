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

**None pending.** Every patch this directory carried has been applied and its
submodule pointer bumped.

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
Octane workflow needs a re-run. See
[`tests/octane/roadmap.md`](../tests/octane/roadmap.md) §2.

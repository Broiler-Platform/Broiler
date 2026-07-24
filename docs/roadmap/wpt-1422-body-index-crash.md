# WPT #1422 — `body-:0,0 — Index was outside the bounds of the array.` (top crash, 59 377 tests)

Triage note for [issue #1422](https://github.com/Broiler-Platform/Broiler/issues/1422)'s
dominant problem. Companion to `wpt-triage-and-diagnostics.md`; kept separate because
the finding is that the crash is a **direct consequence of `patches/0013`**, which
this repo's WPT CI applies to the `Broiler.JS` working tree via
`scripts/apply-pending-wpt-patches.sh`.

## Symptom

The run's #1 problem is a single crash bucket gating **59 377** of 60 396 failures:

```
body-:0,0 — Index was outside the bounds of the array.
```

Decoding the signature (`Broiler.Wpt/ExceptionSignature.cs` builds `{frame} — {message}`):

- **`body-:0,0`** is a stack frame, not part of the message. Every top-level
  script/eval/program is compiled into a dynamic method named `body`
  (`FastCompiler.cs:268` — `BExpression.Lambda<JSFunctionDelegate>("body", …)`; the
  runtime method name comes from `FunctionName.FullName` = `"{Name}-{Location}:{Line},{Column}"`
  ⇒ `body-:0,0` for `Name="body"`, null location, line/col 0). So the crash is in
  **top-level program execution**, which is why its blast radius is essentially every
  testharness-driven test (the ~1 000 non-crashing failures are reftests/manual/parse
  failures that never reach script execution).
- **`Index was outside the bounds of the array.`** is a runtime
  `IndexOutOfRangeException` thrown *inside* that compiled `body` method.

## Root cause — `patches/0013` converts a compile crash into a runtime crash

The manifest history is conclusive (`tests/wpt-baseline/failed-tests.json`,
`exceptionSignatures`):

| Run (commit) | Top signature |
|---|---|
| `044a704` — before 0013 reached CI | `ILCodeGenerator.VisitParameter — The given key '#TempJSValue100032' was not present…` (issue #1419's **compile-time** `KeyNotFoundException`, fragmented per temp id) |
| `65da1b3` — with 0013 applied | `body-:0,0 — Index was outside the bounds of the array.` × 59 377 (a **runtime** `IndexOutOfRangeException`) |

The #1419 KeyNotFound buckets vanish and one unified runtime crash appears. Same
tests, different failure mode.

### Mechanism

1. A pooled compiler scratch temp `#Temp<Type><id>` (minted by
   `FastFunctionScope.GetTempVariable`, kept in the **function-level**
   `TopScope.variableScopeList`) can reach the IL generator **undeclared** when it was
   minted after its enclosing block's `VariableParameters` snapshot — the temp lives in
   the function scope but appears in no block's variable list, so
   `LambdaRewriter`'s `CollectBlockVariables` never registers it and no local is
   declared for it. (This is the "out-of-order VariableParameters snapshot" that
   `patches/0013` describes.)
2. **Before 0013:** the undeclared reference hit the throwing `variables[exp]` indexer
   in `ILCodeGenerator.VisitParameter` → `KeyNotFoundException` → the whole script's
   compilation aborted (`ScriptError`, whole-page render failure). This was #1419.
3. **After 0013:** `VisitParameter` no longer throws for `#Temp`-prefixed params — it
   declares a local on demand (`variables.Create`). Compilation now **succeeds**, and
   the `body` method runs far enough to execute the array access that the dropped temp
   participates in. Top-level identifier/slot access resolves through
   `ScriptInfo.Indices[constantSlot]` (`ScriptInfoBuilder.Index` →
   `Expression.ArrayIndex(…Indices, Constant(index))`), where `Indices` is a
   `KeyString[]` sized at compile time to `keyStrings.List.Count`
   (`ScriptInfoBuilder.Build`). A dropped top-level temp desynchronizes slot
   assignment from that finalized array length, so the emitted constant slot is
   **out of bounds at runtime** → `IndexOutOfRangeException` in `body`.
   (`JSValue` is a reference type, so a merely-uninitialized temp local would raise
   `NullReferenceException`, not this — consistent with the fault being an array index,
   not a null value.)

### Secondary gap in 0013

`patches/0013` patches only the **read** path (`ILCodeGenerator.VisitParameter`). The
**store** path (`ILCodeGenerator.AssignParameter`, ~line 26) still falls through to the
throwing `variables[exp]` indexer, so a dropped temp whose *first* emitted reference is
a write is unhandled. (Empirically the observed temps are read-first, so this is latent
rather than active, but it makes 0013 asymmetric.)

## Net effect

`patches/0013` does **not** reduce the WPT failure count for this cluster — it relabels
#1419's compile-time crash as a runtime crash on the same ~59 k tests (its own
validation was the 250 `Broiler.JavaScript.Compiler.Tests` unit tests, which do not
exercise the drop; the crash needs the full sequential shard run, where the global
`FastFunctionScope.id` reaches ~100 020–100 238 — cumulative session state that does
not arise on an isolated render).

## Hypotheses tested in-sandbox (narrows the search)

The exact runtime array-index site could not be reproduced on an isolated render
(the fault is cumulative-state dependent — the global `FastFunctionScope.id` reaches
~100 020 only after a long sequential run). But three tractable candidates were
checked directly and **refuted**, so a maintainer with the full corpus can skip them:

- **The dropped temp's own value is not the array fault.** `JSValue` is an
  `abstract class` (reference type), so an undeclared/never-written `#TempJSValue`
  local reads as `null`; using it would raise `NullReferenceException`, not
  `IndexOutOfRangeException`. The array fault is therefore an **indirect** corruption
  downstream of the dropped temp, not the temp load itself.
- **`ScriptInfo.Indices[]` slot overflow — refuted.** Top-level identifier access
  emits an unbounded `ScriptInfo.Indices[constSlot]` (`ScriptInfoBuilder.Index`),
  and the array is frozen to `_keyStrings.List.Count` at `ScriptInfoBuilder.Build`
  (`FastCompiler.cs`). Instrumenting the compiler to compare the key count at `Build`
  vs at the end of `BuildProgram` across a battery of scripts (top-level function
  declarations, destructuring catch, classes, getters/setters) showed **no growth
  after `Build`** — so no slot index exceeds the sized array from this path.
- **`KeyStrings.entries` capacity overflow — refuted.** The global metadata array is
  grown under `PublicationLock` (`Math.Max(len*2, id+1)`) and every read
  (`GetMetadata`/`GetNameString`) is bounds-checked (`id < snapshot.Length ? … : default`),
  so it never throws `IndexOutOfRange` regardless of how high the id climbs.

Remaining candidates for the indirect array fault (unverified): the closure-capture
array / `Closures` layout, the arguments array, or a generator/spill slot — reachable
only once compilation proceeds past the dropped temp (which `0013`/`0014` now allow).

## The real fix (needs the full WPT run to validate)

The correct fix is at the **temp-registration layer in `Broiler.JS`**, not the IL
generator's fallback: ensure a top-level `#Temp` is registered (and its
`ScriptInfo.Indices` slot accounted for) in the block/scope that references it, so it is
never dropped — i.e. take the `VariableParameters`/`CollectBlockVariables` snapshot
after all temps for that scope exist, or register a late-minted temp into the enclosing
`body` block. Candidate sites: `FastCompiler.cs` (body block assembly, lines ~222/249),
`LambdaRewriter.VisitBlock`/`CollectBlockVariables`, and the destructuring-catch
snapshot in `FastCompiler.VisitTryStatement.cs:88`. Any change must be validated against
a full sharded WPT run (not just the compiler unit tests) because the failure is
cumulative-state-dependent and did not reproduce on isolated renders in-sandbox.

`patches/0014` completes `0013`'s IL-generator fallback by mirroring the on-demand
`variables.Create` on the **store** path (`AssignParameter`), so a dropped `#Temp` whose
first emitted reference is a write no longer aborts compilation with
`KeyNotFoundException` either. This is a symmetry/robustness completion of 0013's
approach — it removes the remaining compile-abort surface but does not by itself resolve
the indirect runtime array fault above (that needs the root-cause drop prevention).

Until that lands, `patches/0013` + `0014` should be understood as a partial mitigation
(they clear the #1419/#1422 compile-abort surface so unrelated work on the same pages can
proceed) rather than a fix for the crash cluster, which persists at runtime as this issue.

## 2026-07-24 — recurs as [issue #1425](https://github.com/Broiler-Platform/Broiler/issues/1425)'s #1 problem

The 2026-07-24 WPT top-30 report ([#1425](https://github.com/Broiler-Platform/Broiler/issues/1425))
leads with the identical signature — `body-:0,0 — Index was outside the bounds of the array.`
— now gating **59 311** tests (was 59 377 in #1422). Same cluster, same runtime
`IndexOutOfRangeException` in the top-level `body` program lambda; the small drift in count
is unrelated churn elsewhere in the corpus, not a change in this fault. No new root cause:
this is #1422 persisting, exactly as the "Net effect" section predicts — `patches/0013`+`0014`
are applied to the `Broiler.JS` working tree on the WPT CI run (via
`scripts/apply-pending-wpt-patches.sh`), so the cluster is in its **runtime** form rather than
the pre-0013 **compile-abort** form.

### New in-sandbox result — the drop does not reproduce on isolated temp-heavy constructs

To narrow the "remaining candidates" search above, the two on-demand fallback branches
(`ILCodeGenerator.VisitParameter` / `AssignParameter`, the *only* code paths that execute
when a `#Temp` was dropped) were instrumented with a counter and driven through a 36-case
battery of the highest-temp-density constructs compiled in isolation, one fresh `JSContext`
each:

- array/object/nested **destructuring `catch`** bindings (incl. holes, rest, defaults, and
  nested-in-function forms) — the `FastCompiler.VisitTryStatement.cs:88` snapshot candidate;
- `try/finally` (the `VisitFinallyBlock` `#Temp`), `switch`, `with`, labelled `continue`;
- `for-of` / `for-of` with array & object patterns, spread in array/object/call, object &
  array rest;
- classes with instance/static **private fields**, **private methods**, and accessors (the
  member-init temps that motivated the `CreateFunction.cs:311-316` after-`InitMembers`
  snapshot);
- template / tagged-template literals, optional chaining, generators, `async`.

**Result: zero on-demand temp declarations across all 36** (the only non-clean entry was the
expected top-level-`using` syntax error, unrelated). This positively **corroborates** the
"cumulative-state-dependent" conclusion: none of these constructs drops a temp on a single
isolated compile, so the drop is not a property of the construct alone — it emerges from
pooled-temp reuse interacting with block-variable snapshots only over a long sequential run
(where the global `FastFunctionScope.id` reaches ~100 020). It also means the destructuring-
`catch` snapshot at `VisitTryStatement.cs:88`, though a genuine order-of-snapshot asymmetry of
the same class as the already-fixed `CreateFunction` site, is **not** by itself the reproducer
— so a speculative reorder there is not landed here (it would be unvalidated and could perturb
delicate catch/completion semantics without demonstrably moving the cluster). The all-250
`Broiler.JavaScript.Compiler.Tests` remain green with `0013`+`0014` applied.

**Status for #1425:** the root-cause drop-prevention described under "The real fix" is now
implemented as **`patches/0015`** (see below).

## 2026-07-24 — root-cause fix landed as `patches/0015`

`patches/0013`+`0014` are a **reactive** fallback: they let compilation proceed past a dropped
`#Temp` (declaring it on demand) but do not stop the temp from being dropped, and #1425 shows
the runtime `body-:0,0 — Index was outside the bounds of the array.` crash persisting on the
CI run that has them applied. `patches/0015` removes the two mechanisms that produce the
dropped/aliased temp in the first place.

### The two mechanisms (confirmed by source, narrowed by in-sandbox instrumentation)

1. **Late-minted top-level temp → in no block (`FastCompiler.BuildProgram`).** The body block's
   `vList.AddRange(fx.VariableParameters)` ran *right after* `Visit(jScript)`. But the trailing
   statements built afterwards — `BExpression.Return(l, script.ToJSValue())` and the per-global
   `JSContext.Register` calls — can still mint pooled `#Temp` locals via `GetTempVariable`. Any
   temp minted in that window lived in the function scope but in **no** block's variable list, so
   the IL generator declared no local for it up front (→ #1419 `KeyNotFound`; → #1422/#1425 once
   0013/0014 declare it mid-method instead). **Fix:** take the snapshot *after* those statements
   are built. It is a superset of the old snapshot, so a succeeding compile is unchanged.

2. **Temp declared in a nested block → slot freed and reused (`ILCodeGenerator.VisitBlock`).**
   `BBlockExpression.FlattenVariables` hoists variables only out of *directly-nested child*
   blocks, **not** out of a block buried inside a non-block expression (`try/finally`, loop,
   conditional). A `#Temp` that lands in such a block is declared under that block's transient
   `tvs` scope; on block exit its IL local is freed and — via `ILWriter.TempVariable`'s
   `IsBusy`-flag pool (`New`/`Dispose`) — **reused** by a later sibling's `NewTemp` of the same
   type. A later reference to the temp then reads/writes another variable's slot: an **indirect**
   corruption (not the temp's own null value), exactly the array-index-shaped runtime fault the
   §0014 note could not localise. **Fix:** declare compiler temps as stable **function-lifetime**
   locals (`variables.Create(p)` rather than `variables.Create(p, tvs)`), so their slot is never
   reused. Value semantics are unchanged — a temp is always assigned before each use.

### Why it is validated for non-regression but not reproduced here

The crash is specific to the WPT runner's **in-process, sequential** execution (thousands of
compilations sharing static `FastFunctionScope.id`, which climbs to ~100 020). The test262 runner
spawns **one process per test** (`run_test262.py`: `subprocess.Popen([... "--script-host" ...])`),
so its `id` resets each test — which is why it "works like a charm" and never shows this cluster.
In-sandbox the fault did not reproduce under **any** available lever: a 36-construct temp-density
battery (0 on-demand fallbacks), compiling `testharness.js` / `testharnessreport.js` /
`testdriver.js` (all clean), a 3000-iteration in-process stress that pushed `id` well past 100 k
(0 drops), and per-block instrumentation (0 temps ever declared below the root block for available
scripts). So `0015` is validated **for non-regression** — all 250 compiler tests pass and the
harness files still compile — and its flip of the cluster must be confirmed on the sharded
WPT/test262 CI, where the triggering corpus script and the accumulated in-process state exist.
Both changes are strict supersets/hardenings that cannot change a compilation that currently
succeeds, so landing them is safe even ahead of that confirmation.

# Submodule patches

These capture submodule changes that could not be pushed from the session (the
`Broiler-Platform/Broiler.*` submodule remotes are outside the session's GitHub push
scope, so `git push` returns **403**). Each is captured here as a `git format-patch`
file for a maintainer to apply to the target submodule, push, and bump the submodule
pointer in the parent repo.

**Status.** Patches **0001–0004 have been applied upstream and the parent submodule
pointers pin the commits that contain them** (retained only for provenance). Patch
**0004** in particular is now applied — the pinned `Broiler.HTML` `52f65d9` *"DomParser:
treat `<iframe>` as a replaced element; hide inline fallback content"* contains its
`CorrectIframeBoxes` pass (verified 2026-07-20). Patches **0005, 0006 and 0007 are now
applied upstream** — the submodule commits were pushed to `Broiler.HTML` `main`
(`52f65d9..5c16c12`) and the parent pointer is bumped to `5c16c12`, which contains
`CorrectVideoBoxes` (0005), `CorrectProgressBoxes` (0006) and `CorrectSelectMultipleBoxes`
(0007). Their main-repo `ReplaceVideoWithPlaceholder` / `ReplaceProgressLike` /
`ReplaceSelectMultiple` fallbacks (and their exclusive helpers/patterns) **have been dropped
from `HtmlPostProcessor`** now that the renderer handles these elements natively. **0007
stacks on 0006** (they share `SetUniformBorder`/`ReadNumericAttribute`).

Patches **0008 and 0009 are now also applied upstream** (verified 2026-07-21) — the
`Broiler.JS` pointer was bumped from `3a8f302` to **`3f0c7054`**, whose history contains
`ffe8956e` *"JSModuleContext: host-overridable module resolution and source read"* (0008)
and `3f0c7054` *"Integration tests: lock in simultaneous JSContext session isolation"*
(0009).

**Patches `0010` and `0011` are now also applied upstream (verified 2026-07-22)** — the
`Broiler.JS` pointer has since been bumped again from `3f0c7054` to **`98b07636`** (parent
commit `71f3ce9` *"Update submodules"*), whose history contains `64fda04f`
*"GeneratorRewriter: only seed the ScriptInfo box on first entry (fix TLA resume)"* (0010)
and `98b07636` *"Modules: run module init on a pumped loop and bind imports without Clr"*
(0011). **So all eleven patches `0001`–`0011` are applied upstream and pinned by the parent
pointers**; the files here are retained for provenance. The direct consequence:
`EngineModuleSupport.Available` — the bridge's timeout-guarded probe of whether the engine
binds a static import — now returns **`true`** on the pinned engine (verified this session by
building `Broiler.HtmlBridge.Scripting` against `98b07636` and running the probe), so the
engine-driven ES-module path is the **active** path (the `EsModuleLinker` is now the dormant
fallback used only if an un-patched engine is ever pinned). The 288 `~Module` `Broiler.Cli.Tests`
pass on this active engine path with zero failures.

**Pending patches are applied on the WPT CI run.** The WPT runner
(`.github/workflows/wpt-tests.yml`) checks out submodules at their pinned pointers and does not,
by itself, apply anything under `patches/`. To exercise a fix that is captured here but **not yet
in the pinned pointer** (currently **0012**, **0013**, **0014**, **0015** and **0016**), the `run` job invokes
`scripts/apply-pending-wpt-patches.sh` after the recursive checkout and before the build. The
script is **idempotent and scoped to the pending list only**: a patch already present in the
checked-out tree (reverse-apply succeeds) is skipped, so it stops being applied automatically the
moment a maintainer lands the fix upstream and the pointer is bumped. The build compiles submodule
source in place, so patching the working tree is sufficient — no commit, no pointer bump; only
Broiler's render is affected, not Chromium reference generation.

**Submodule pointers (2026-07-24).** The `Broiler.JS` pointer is advanced to its `origin/main`
head **`1aa46f21`** (the `ci: update test262 failed testcase list [skip ci]` commit — a CI-metadata
update on top of `98b07636`, **no engine-source change**). The four pending patches above were
re-verified this session to apply cleanly on top of the current submodule trees (`Broiler.JS`
`1aa46f21`, `Broiler.HTML` `5c16c121`); the parent build compiles them in place with **0 errors**
and all **250** `Broiler.JavaScript.Compiler.Tests` pass. The `Broiler.JS` push remains **403**
(remote outside session scope), so `0013`/`0014`/`0015` stay pending patches applied on the WPT CI
run, not pointer commits.

**CI evidence that `0013`/`0014`/`0015` do not fix the `body-:0,0` crash ([#1428](https://github.com/Broiler-Platform/Broiler/issues/1428)).**
WPT run [`30072567162`](https://github.com/Broiler-Platform/Broiler/actions/runs/30072567162)
(`head_sha f88905d`) applied the **entire** pending set — every sharded `run (N)` job records step
"Apply pending submodule patches → success" — yet its top problem is still
`body-:0,0 — Index was outside the bounds of the array.` gating **59 282** tests. So the
dropped/aliased-`#Temp` model that `0013`/`0014`/`0015` address is **not** the operative cause of
the residual runtime fault; test262 stays green only because it runs one process per test (resetting
the process-global state), so it cannot manifest this cumulative-state crash and does not validate a
WPT-layer fix.

**Root cause found and fixed — `0016` (the real `body-:0,0` fix).** The crash was reproduced
in-process against a testharness-heavy corpus and localised to
`Broiler.JavaScript.Storage/StringMap<T>`: its "not found" sentinel was a shared mutable
`static Node Empty`. On the create path `GetNode` can hand that sentinel to `Put`/`Save`/the indexer,
which writes a (large, cumulative) value into it; afterwards **every fresh map** (`storage==null`)
returns the sentinel and `TryGetValue` reports a false hit with the stale value for **any** key,
without matching it. In the engine a fresh script's `_keyStrings.GetOrAdd` then returns the leaked
cumulative index while its key `List` stays empty, so `ScriptInfo.Indices` (sized to `List.Count`) is
emitted with an out-of-bounds constant index → `IndexOutOfRangeException` at runtime in the top-level
`body` lambda (the `DomBridge.RegisterDocument` bootstrap, whose `ScriptInfo.FileName` defaults to
`vm.js`). It only surfaces in the long in-process WPT run (the pollution needs a big compilation to
overflow, then persists in the static) and never in the per-process test262 runner — exactly the
observed signature. `0016` makes the sentinel `[ThreadStatic]` and resets it to a pristine node at
each `GetNode` entry, so a stray write can never be observed by a later read (no struct-layout change,
no cross-thread race). Verified: a new `StorageTests` regression (a fresh `StringArray` after a heavy
one) returns `86704` on the old code and `0` with the fix; all 250 `Compiler.Tests` + 12
`Storage.Tests` pass; and a 1000+ test in-process WPT corpus that produced 7312 poisoned lookups
pre-fix produces **zero** with `0016` applied. The patch table's `0013`–`0016`
entries retain the diagnosis and validation evidence.

Patch **`0010` — APPLIED** (pinned `98b07636`; push 403 at authoring, later applied upstream
by a maintainer) — fixes the top-level-await **codegen** bug that
§0008 root-caused. The generator box-load prologue re-seeded the persisted `ScriptInfo`
box with a null body-local on **every** resume, so after a top-level-await resume any
identifier/member access resolved through `scriptInfo.Value.Indices[…]` dereferenced null
(constant receivers / bare globals resolve via constant `KeyStrings` and survived, which
made the fault look receiver-shaped). Guarding the seed on `nextJump == 0` (first entry
only) fixes it. **Validated against the full `Broiler.JS` suite — zero regressions** (only
the pre-existing Intl/ICU-locale and doc-file env failures remain), and a new
`TopLevelAwaitResumeTests` pins the corrected behaviour. `0010` fixes
the *codegen* blocker; the module-orchestration completion piece is patch **`0011`** below.

Patch **`0011` — APPLIED** (pinned `98b07636`, commit on top of `0010`; push 403 at
authoring, later applied upstream) — the module-orchestration completion fix that lets the
engine's own ES-module machinery **bind a static import's value** (with `0010` alone it no
longer crashed but resolved to `undefined`/`0`). Three coupled changes in
`Broiler.JavaScript.Modules`: (a) `RunScriptAsync`/`RunAsync` run the whole init under one
pumped `AsyncPump.Run` loop (like `JSContext.ExecuteAsync`) so a top-level-await body's
continuation actually drains instead of stalling on the default un-pumped context; (b) a
direct `JSModule.CompileDirect` hook that `InitAsync` awaits, removing the
`Task → IJSPromise → Task` compile double-marshal that re-posted the body off the loop; and
(c) `import()`/`require()` convert their module `Task<JSValue>` to a promise via the
engine-native `JSValue.CreatePromiseFromTask` instead of `JSEngine.ClrInterop.Marshal` —
the interop is only the full `DefaultClrInterop` when the optional `Broiler.JavaScript.Clr`
assembly is loaded, and its fallback returns `undefined` for a `Task`, which is why an
imported binding silently came back `undefined`. **Validated against the full `Broiler.JS`
suite — zero regressions**, plus a new `EngineModuleImportBindingTests` (named/namespace/
default imports, transitive chain, diamond shared-dep-evaluated-once, top-level-await
dependency). **Now applied upstream** (pinned `98b07636`, stacked on `0010`). The bridge is
wired to the engine path (P7.27, `BridgeModuleContext` + `EngineModuleSupport`); with `0011`
pinned the probe is `true`, so the engine path is active and the `EsModuleLinker` is the
dormant fallback (its removal, plus the sub-document/CLI paths and genuine event-loop
ordering, is the remaining separately-scoped bridge task).

| Patch | Target submodule | Pinned commit / status | Main-repo follow-up |
|---|---|---|---|
| 0001 — plumb `viewportZoom` through the static render entry | `Broiler.HTML` | applied — `9977672` *HtmlRender: plumb viewportZoom through the static render entry* | **Unblocked, not yet wired** — the visual-viewport render cutover (see below). The serialization bake remains the active fallback until it lands. |
| 0002 — make `DomNodeCollectionExtensions` public | `Broiler.DOM` | applied — `5c71ac9` *Make DomNodeCollectionExtensions public for host reuse* (ancestor of the pinned `8e8325f`) | **Done** — `DomBridge.ChildIndexOf` delegates to `element.ChildNodes.IndexOfReference(child)`. |
| 0003 — `Normalize()` fires one `characterData` record per text run | `Broiler.DOM` | applied — `8e8325f` *DomNode.Normalize(): one characterData record per contiguous text run* (the pinned pointer) | **Done / works either way** — `DomBridge.NormalizeNode` already delegates to `node.Normalize()`; canonical now matches the bridge's former one-record-per-run behaviour exactly. |
| 0004 — treat `<iframe>` as a replaced element (hide inline fallback) | `Broiler.HTML` | **applied** — pinned `52f65d9` *DomParser: treat `<iframe>` as a replaced element; hide inline fallback content* contains `CorrectIframeBoxes` | **Unblocked, not yet wired** — the pinned renderer now hides iframe fallback natively, so `HtmlPostProcessor.StripIframeContent` can be dropped (it is now a redundant belt-and-suspenders strip). Not done here to keep this change focused. |
| 0005 — render `<video>` as a black inline-block replaced box (hide fallback) | `Broiler.HTML` | **applied** — pushed as `5561eb0`; contained in the pinned `5c16c12` | **Done** — the pinned renderer paints `<video>` natively, so `HtmlPostProcessor.ReplaceVideoWithPlaceholder` (Phase 6 concern 2) was dropped. |
| 0006 — render `<progress>`/`<meter>` as a native track with proportional fill | `Broiler.HTML` | **applied** — pushed as `444cace`; contained in the pinned `5c16c12` | **Done** — the pinned renderer paints `<progress>`/`<meter>` natively, so `HtmlPostProcessor.ReplaceProgressLikeWithPlaceholder` was dropped (native coverage: `FormControlRenderTests.Meter_NativeRender_Follows_WritingMode_And_Direction`). |
| 0007 — render `<select multiple>` as a native list box (both appearance modes) | `Broiler.HTML` | **applied** — pushed as `5c16c12` (the pinned pointer); **stacks on 0006** | **Done** — the pinned renderer paints `<select multiple>` natively (both `appearance:auto`/`none`), so `HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder` was dropped; the string-rewrite unit test was retired in favour of the WPT/Acid reftest gate. Reads the box's `Appearance` (the `appearance` box property landed in the main repo's `Broiler.Layout`). |
| 0008 — `JSModuleContext`: host-overridable module resolution/source read | `Broiler.JS` | **applied** — `ffe8956e`; contained in the pinned `98b07636` | **Applied and wired** — Phase 7 item 6. The seam lives in the pinned engine; with the codegen (`0010`) and orchestration (`0011`) fixes also pinned, the bridge's `BridgeModuleContext` drives the engine module path (P7.27) and `EngineModuleSupport.Available` is `true`. |
| 0009 — session-isolation regression tests | `Broiler.JS` | **applied** — `3f0c7054` (the pinned pointer) | **Test-only guard** — Phase 2. Locks in that two live `JSContext` instances are isolated (the property the bridge's "two simultaneous sessions" exit criterion relies on). No production change; the bridge-side criterion is also proven CI-green by `DomBridgeSessionLifetimeTests.Two_Simultaneous_Sessions_Do_Not_See_Each_Others_State`. |
| 0010 — `GeneratorRewriter`: seed the `ScriptInfo` box on first entry only (fix TLA resume) | `Broiler.JS` | **APPLIED** — `64fda04f`; contained in the pinned `98b07636` | **Fixes the §0008 codegen blocker** — Phase 7 item 6. Top-level await now runs post-resume statements correctly (full-suite validated, zero regressions). Module *value* binding additionally needs `0011` (also pinned). |
| 0011 — `Modules`: pumped module init + Clr-independent import binding | `Broiler.JS` | **APPLIED** — `98b07636` (the pinned pointer); **stacks on `0010`** | **Completes the engine-driven module path** — Phase 7 item 6. A static import now binds its value (was `undefined`/`0`): pumped `AsyncPump.Run` init, `CompileDirect` (no compile double-marshal), and `import()` via `JSValue.CreatePromiseFromTask` instead of the Clr-only interop. Full-suite validated, zero regressions. With `0010`+`0011` pinned, `EngineModuleSupport.Available` is `true` and the bridge runs modules through the engine (P7.27); retiring `EsModuleLinker` + wiring sub-document/CLI + event-loop ordering is the remaining bridge task. |
| 0012 — `PaintWalker`: propagate `background-clip:text` color into table cell text | `Broiler.HTML` | **PENDING upstream** (push 403; pinned `5c16c12` does not contain it) — **applied on the WPT CI run** by `scripts/apply-pending-wpt-patches.sh` (idempotent; reverts to skip once a maintainer bumps the pointer) | **WPT cluster 50** — `PaintChildren` routed `table`/`inline-table` boxes to `PaintTableChildren` without the `bgClipTextColor`, so a `<table>` with `background-clip:text` left its cell text uncomposited (background not clipped to glyphs). Threads it through so table cell text is clipped like block/inline-block/float descendants (verified on `css-backgrounds/background-clip/clip-text-descendants`: the `<table>`/`<table transformed>` rows now render purple). **No main-repo fallback** — the fix is entirely in the `Broiler.HTML` paint layer. The reftest stays pixel-gated on orthogonal table-layout metrics + glyph AA, so it does not flip until the patch lands and those are addressed, but the specific clip bug is fixed. |
| 0013 — `ILCodeGenerator.VisitParameter`: declare pooled compiler temps on demand | `Broiler.JS` | **PENDING upstream** (push 403; pinned `98b07636` does not contain it) — **applied on the WPT CI run** by `scripts/apply-pending-wpt-patches.sh` (idempotent; reverts to skip once a maintainer bumps the pointer) | **WPT issue #1419 crash cluster** (top-30 by blast radius; ~29/30) — a pooled scratch temp `#Temp<Type><id>` (from `FastFunctionScope.GetTempVariable`) could reach `VisitParameter` before the block that lists it in its `Variables` was visited by the IL generator, or after an out-of-order `VariableParameters` snapshot dropped it, hitting the `variables[exp]` indexer and throwing `KeyNotFoundException` — surfacing as `ILCodeGenerator.VisitParameter — The given key '#TempJSValue… was not present in the dictionary'` and aborting the **entire** script compile (whole-page render → `ScriptError`). The patch declares the local on demand for `#Temp`-prefixed parameters: temps are keyed by reference, so the on-demand local is the same one every later read/write of that temp resolves to (`VisitBlock` skips re-creating an already-present key), preserving value semantics regardless of visit order. **Safety:** the branch runs only on the path that already threw, so it cannot change a compilation that currently succeeds; genuine *user*-variable resolution failures still surface via the original throwing indexer (guarded to the `#Temp` prefix, unreachable for a source identifier or a `#private` name). **No main-repo fallback** — the fix is entirely in the `Broiler.JS` IL generator. All 250 `Broiler.JavaScript.Compiler.Tests` pass with the patch applied. The crash reproduces only in the full sharded WPT run (deterministic temp ids ~`100020`–`100238` in `tests/wpt-baseline/failed-tests.json`), not on an isolated render, so it is not locally re-triggered here; the fix targets the exact throwing site. **Follow-up:** after 0013 reached CI, issue **#1422**'s top crash became `body-:0,0 — Index was outside the bounds of the array.` (a *runtime* `IndexOutOfRangeException` in the top-level `body` program lambda, gating 59 377 tests) — the same dropped-temp cluster's failure mode shifting from compile-time to run-time once 0013 stops the compile-abort. The `0014` entry below completes 0013's read/write symmetry, but the runtime fault needs the full sharded run to reproduce/validate. |
| 0014 — `ILCodeGenerator.AssignParameter`: declare pooled compiler temps on demand (store-path counterpart to 0013) | `Broiler.JS` | **PENDING upstream** (push 403; pinned `98b07636` does not contain it) — **applied on the WPT CI run** by `scripts/apply-pending-wpt-patches.sh` after `0013` (idempotent; reverts to skip once a maintainer bumps the pointer). **Depends on 0013** (uses its `IsCompilerTemp` helper). | **WPT issue #1422 follow-up.** `0013` patched only the *read* path (`VisitParameter`); the *store* path (`AssignParameter`) still fell through to the throwing `variables[exp]` indexer, so a dropped `#Temp` whose **first** emitted reference is a write still threw `KeyNotFoundException` and aborted the whole script compile — the asymmetry the read-only fix left open. `0014` mirrors 0013's on-demand `variables.Create` for `#Temp`-prefixed parameters on the store path (same guard, same keyed-by-reference reasoning: the on-demand local is the identical one every later read/write resolves to). **Safety:** runs only on the path that already threw, so it cannot change a compilation that currently succeeds. All 250 `Broiler.JavaScript.Compiler.Tests` pass. **Scope note:** this hardens 0013's fallback to be read/write symmetric; the observed #1422 temps are read-first (already handled by 0013), so 0014 closes a latent write-first compile-crash rather than being confirmed to flip #1422's *runtime* `IndexOutOfRange` — that fault is an indirect corruption (the dropped temp is a `JSValue`, a reference type, so its own use would raise `NullReferenceException`, not an array-index fault) that only reproduces in the full sequential shard run. The two locally-tractable runtime hypotheses were tested and **refuted** in-sandbox: `ScriptInfo.Indices` does not grow after `ScriptInfoBuilder.Build` (so no slot-count overflow), and `KeyStrings.entries` is grown-under-lock and bounds-checked on every read. See the `0015` and `0016` entries below. |
| 0015 — `ILCodeGenerator.VisitBlock` + `FastCompiler.BuildProgram`: stable function-lifetime temp locals & late body-temp snapshot (root-cause fix for 0013/0014's runtime successor) | `Broiler.JS` | **PENDING upstream** (push 403; pinned `98b07636` does not contain it) — **applied on the WPT CI run** by `scripts/apply-pending-wpt-patches.sh` after `0013`/`0014` (idempotent; reverts to skip once a maintainer bumps the pointer). **Depends on 0013** (uses its `IsCompilerTemp` helper). | **WPT issue #1422/#1425 root-cause fix.** Where `0013`/`0014` are a *reactive* fallback (declare a dropped `#Temp` on demand so the compile no longer aborts), `0015` removes the two mechanisms by which a pooled temp becomes dropped or aliased in the first place. **(1) `FastCompiler`**: the body block's `VariableParameters` snapshot moves to *after* the trailing register/return statements are built — `script.ToJSValue()` and `JSContext.Register` can still mint pooled `#Temp` locals via `GetTempVariable`, and the former snapshot (right after `Visit(jScript)`) left such a late-minted temp in the function scope but in **no** block's variable list. The new snapshot is a **superset** of the old one, so a currently-succeeding compile is unchanged. **(2) `ILCodeGenerator.VisitBlock`**: compiler temps (`#Temp<Type><id>`) are declared as stable **function-lifetime** locals rather than block-scoped pooled slots. `BBlockExpression.FlattenVariables` does **not** hoist a temp out of a block nested inside a non-block expression (`try/finally`, loop, conditional), so such a temp is declared under a transient `tvs` scope whose IL local is freed on block exit and **reused** by a later sibling's `NewTemp` of the same type (`ILWriter.TempVariable` pooling) — aliasing another variable's slot when the temp is referenced across the boundary, the indirect corruption consistent with the `body-:0,0 — Index was outside the bounds of the array.` runtime fault `0014`'s note could not localise. A stable local is never reused; value semantics are unchanged (a temp is assigned before each use). **Safety/validation:** both changes are strict supersets/hardenings that cannot alter a compilation that currently succeeds; all **250** `Broiler.JavaScript.Compiler.Tests` pass, and `testharness.js` + `testdriver.js` compile clean. **Repro caveat:** as with 0013/0014, the crash reproduces only in the full sequential **in-process** WPT shard run — not in the per-process test262 runner, nor on any isolated construct, large harness compile, or 3000-iteration high-`id` in-process stress exercised in-sandbox (all clean) — so 0015's flip of the cluster is validated for **non-regression** here and must be confirmed on the sharded WPT/test262 CI. See the `0016` entry below for the independently reproduced final root cause. |

| 0016 — `StringMap`: thread-local, per-lookup-reset not-found sentinel | `Broiler.JS` | **PENDING upstream** (push 403; pinned `1aa46f21` does not contain it) — **applied on the WPT CI run** by `scripts/apply-pending-wpt-patches.sh` after `0013`–`0015` (idempotent; different files, no conflict; reverts to skip once a maintainer bumps the pointer) | **WPT issue #1428 root-cause fix — the real `body-:0,0` crash.** `StringMap<T>`'s "not found" sentinel was a shared mutable `static Node Empty`; a create-path overflow return let `Put`/`Save`/the indexer write a cumulative index into it, after which every fresh map returned that stale value for any key (false hit, `List` never grows). A fresh script then resolved a key to the leaked index while `ScriptInfo.Indices` was sized to `List.Count` → runtime `IndexOutOfRange` in the `body` lambda, gating ~59k tests; only in the long in-process run, never in per-process test262. Fix: `[ThreadStatic]` sentinel reset to pristine at each `GetNode` entry. **No main-repo fallback** — entirely in the `Broiler.JS` storage layer; and no `Broiler.Layout`/bridge API is involved, so nothing in the parent depends on it. Deterministic regression test added (`StorageTests`), + verified against a 1000+ test in-process WPT corpus (7312 poisoned lookups → 0). |

| 0017 — `CssStyleEngine`: reject a unitless `<number>` in a length-context `min()`/`max()`/`clamp()` | `Broiler.CSS` | **PENDING upstream** (push 403; pinned `2aad88b` does not contain it). CI runs strictly against the pinned pointer — `scripts/apply-pending-wpt-patches.sh`'s `PENDING_PATCHES` is currently empty (see commit *ci(wpt): stop applying submodule patches, keep the mechanism*), so this lands on CI only when a maintainer applies the patch and bumps the pointer (to re-exercise it on CI before then, add `Broiler.CSS\|patches/0017-css-values-unitless-zero-math.patch` to that array). | **WPT issue #1431 — `css/css-values/max-unitless-zero-invalid` (0.0% match, red exposed).** Per CSS Values 4 calc-type-checking, a bare `<number>` (including a unitless `0` — *not* the dimensionless `0` length permitted outside a math function) is a type mismatch as a top-level `min()`/`max()`/`clamp()` argument in a `<length-percentage>` context, so `height: min(0, 100%)` is invalid and must be dropped, letting the earlier `height: min(100%)` win (abspos `#outer` then fills the viewport → all-green). `IsAcceptableDeclarationValue` now rejects such a value for a conservative set of `<length-percentage>` properties (sizing/inset/margin/padding/gap/text-indent/flex-basis); `calc()` is left alone (a `<number>` is a valid operand there) and `<number>`-typed properties (`opacity`/`line-height`) are not policed. **No main-repo fallback** — the cascade validator is entirely engine-side (`Broiler.CSS.Dom`, as with clusters 38/39/43). Guard: `CssDeclarationValidatorTests` (12 new cases); full `Broiler.CSS.Dom.Tests` green apart from the two pre-existing `CssDomArchitectureTests` environment failures. |

To apply a *future* patch (kept for reference):

```sh
cd <Submodule>
git am < ../patches/<NNNN-name>.patch      # or: git apply
git push origin HEAD                        # maintainer has push access
cd ..
git add <Submodule>                         # bump the pointer
```

Then do the "Follow-up (main-repo)" wiring named for that patch — it is deferred until
the pointer is bumped because it references the patched API, which does not exist at the
previously-pinned submodule SHA (so it would not compile against the pinned clone on CI).

---

## 0007 — `Broiler.HTML`: render `<select multiple>` as a native list box (appearance:auto)

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: APPLIED** — pushed as `5c16c12` (the pinned pointer); the note below is the original authoring record.
**Depends on:** **0006** (shares the `SetUniformBorder`/`ReadNumericAttribute` helpers — apply 0006 first)
and the main-repo `Broiler.Layout` `appearance` box property (already landed in the parent — see below).

**What it does.** HTML §4.10.7: `<select multiple>` is a replaced list-box control. Broiler has no native
control chrome, so a post-cascade `CorrectSelectMultipleBoxes` pass renders it as an `inline-block` list box:
one 16px row track per visible option (`size`, clamped 2..8, default 4), the first row painted as the
selection highlight `#3875d7` and the rest alternating `#ffffff`/`#f7f7f7`, honouring `writing-mode` for
vertical/`-rl`-reversed boxes, and hides the real `<option>` children. Track/chrome boxes are absolutely
positioned within the relative host (laid out because the engine discovers absolutes by walking the tree at
layout time). Both appearance modes are covered, branched on the box's cascaded `Appearance`:
`appearance:auto` (grey `#f0f0f0` field, `#767676` border, an edge `#dcdcdc` scrollbar-chrome strip) and
`appearance:none` (white field, lighter `#9a9a9a` border, no chrome). Matches
`HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder`.

**The `appearance` box property is main-repo, not a patch.** `Broiler.Layout` is a directory in this repo
(not a submodule), so `CssBox.Appearance` + its `CssUtils` get/set dispatch landed directly on the parent
branch. This patch reads `box.Appearance`; because the `Broiler.HTML` submodule is built in the parent
checkout, that property is present at build time.

**Verified locally** (patch applied on top of 0006 in the submodule working tree, parent build compiling it in
place) via render-probes on raw `<select multiple>`: the `appearance:auto` box paints border `#767676`, first
row `(56,117,215)`, alternating rows `(247,247,247)`, chrome `(220,220,220)`+`(184,184,184)` border, row
borders `(208,208,208)`; the `appearance:none` box drops the chrome entirely (0 chrome pixels) and uses a
white field. Full pixel validation needs the Acid/WPT reftest gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403**, so per `CLAUDE.md` it ships as
`patches/0007-html-select-multiple-native-listbox.patch` with the pointer left **unbumped** and the working
tree reverted.

**Main-repo follow-up (once applied + pointer bumped).** Drop
`HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder` (the `appearance` property it depends on is already
in the parent). **Current fallback (unchanged until then):** the string rewrite runs before the renderer, so a
real `<select multiple>` never reaches `CorrectSelectMultipleBoxes` yet and nothing on CI depends on this patch.

---

## 0006 — `Broiler.HTML`: render `<progress>`/`<meter>` as a native track with proportional fill

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: APPLIED** — pushed as `444cace`; contained in the pinned `5c16c12`. The note below is the original authoring record.
**Depends on:** nothing (adds a call adjacent to `0005`'s — see the table note).

**What it does.** HTML §4.10.13/4.10.14: `<progress>`/`<meter>` are replaced form controls. Broiler has no
native control chrome, so a post-cascade `CorrectProgressBoxes` pass renders each as a bordered
`inline-block` track (1px `#767676`, background `#f0f0f0` progress / `#e6e6e6` meter, `120×16` — `16×120`
vertical) with an **absolutely-positioned fill bar** proportional to `value` (`#0a84ff` progress / `#4caf50`
meter), honouring `writing-mode`/`direction` for vertical and RTL-reversed bars, and hides the element's
fallback text. The value ratio mirrors the fallback (`meter` reads `min`/`max`/`value`; `progress` reads
`value`/`max`). Runs post-cascade so the injected fill box and forced track geometry are not re-cascaded (the
absolute fill is still laid out — the engine discovers absolutely-positioned boxes by walking the box tree at
layout time).

**Verified locally** (patch applied in the submodule working tree, parent build compiling it in place) via a
render-probe using `HtmlRender.RenderToImageWithStyleSet` on **raw** `<progress value="0.5">` / `<meter
value="0.5">` (bypassing the string fallback): the track paints border `#767676` + track grey, the left ~60px
(ratio 0.5 × 120) paints the fill colour — `(10,132,255)` blue for progress, `(76,175,80)` green for meter —
and the region past the fill is track grey `(240,240,240)`. Full pixel validation needs the Acid/WPT reftest
gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403** (submodule remote outside the session's GitHub
scope), so per `CLAUDE.md` it ships as `patches/0006-html-progress-meter-native-track-fill.patch` with the
pointer left **unbumped** and the working tree reverted.

**Main-repo follow-up (once applied + pointer bumped).** Drop
`HtmlPostProcessor.ReplaceProgressLikeWithPlaceholder` (the regex that rewrites `<progress>`/`<meter>` into a
styled `<div>` track) — the renderer then boxes them natively. **Current fallback (unchanged until applied):**
that rewrite runs in the string pipeline before the renderer, so a real `<progress>`/`<meter>` never reaches
`CorrectProgressBoxes` yet and nothing on CI depends on this patch.

---

## 0005 — `Broiler.HTML`: render `<video>` as a black inline-block replaced box (hide fallback)

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: APPLIED** — pushed as `5561eb0`; contained in the pinned `5c16c12`. The note below is the original authoring record.
**Depends on:** nothing.

**What it does.** HTML §4.8.9: a `<video>` is a replaced element; a UA that cannot present the media shows
the poster/first frame and never renders the inline fallback content between the tags. Broiler cannot decode
video, so a post-cascade `CorrectVideoBoxes` pass (mirroring `CorrectIframeBoxes`/`CorrectFramesetBoxes`)
sets the box to `inline-block`, sizes it (author CSS size wins; else the `width`/`height` presentation
attributes; else the CSS-default intrinsic **300×150**), paints it **black**, and hides the fallback children
(`display:none`). Runs post-cascade so a cascade-time hide of a block fallback child cannot be re-shown.

**Verified locally** (patch applied in the submodule working tree, parent build compiling it in place) via a
render-probe using `HtmlRender.RenderToImageWithStyleSet` on **raw** `<video>` (bypassing the string
fallback): a bare `<video></video>` paints black at a pixel well inside the 300×150 box (0,0,0), and a
`<video><div style="background-color:green;width:120px;height:120px"></div></video>` paints **black** — not
green — over the fallback area (the inline fallback is hidden). Full pixel validation needs the Acid/WPT
reftest gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403** (submodule remote outside the session's GitHub
scope), so per `CLAUDE.md` it ships as `patches/0005-html-video-replaced-element-black-box.patch` with the
pointer left **unbumped** and the working tree reverted.

**Main-repo follow-up (once applied + pointer bumped).** Drop `HtmlPostProcessor.ReplaceVideoWithPlaceholder`
(the regex that rewrites `<video>…</video>` into a black `<div>`) from `Process`/`ProcessForBrowsing` — the
renderer then boxes `<video>` natively. **Current fallback (unchanged until applied):** that rewrite runs in
the string pipeline *before* the renderer, so a real `<video>` never reaches `CorrectVideoBoxes` yet and
nothing on CI depends on this patch.

---

## 0004 — `Broiler.HTML`: treat `<iframe>` as a replaced element (hide inline fallback)

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Orchestration/Parse/DomParser.cs`).
**Status: APPLIED** — pinned `52f65d9` contains its `CorrectIframeBoxes` pass. The note below is the original authoring record.
**Depends on:** nothing.

**What it does.** HTML §4.8.5: an `<iframe>` hosts a nested browsing context; UAs that support
iframes never render the inline fallback content between the tags (the loaded sub-document replaces
it). This static renderer laid out and painted the fallback children as a visible block (probe: an
`<iframe><div style="background:green;width:80px;height:80px"></div></iframe>` painted 6400 green px).
The patch adds a post-cascade `CorrectIframeBoxes` pass that sets `display:none` on each iframe box's
direct children (hiding the whole fallback subtree), mirroring the frameset `<noframes>` handling. It
runs **post-cascade** because a cascade-time hide is re-shown by the per-box cascade for a block child
(e.g. a `<div>`). Sub-documents compose separately (no in-tree `#subdoc-root` child), so a *loaded*
iframe sub-document is unaffected.

**Verified locally** (patch applied in the submodule working tree, parent build compiling it in place):
the same probe now paints **0** green px (fallback hidden) while a real out-of-iframe green div still
paints 6400; the standing iframe sub-resource tests fail identically to baseline (pre-existing network
env, not this change). Full pixel validation needs the Acid/WPT reftest gate.

**Why it's a patch.** The `Broiler.HTML` push returned **403** (submodule remote outside the session's
GitHub scope), so per `CLAUDE.md` it ships as
`patches/0004-html-iframe-replaced-element-hide-fallback.patch` with the pointer left **unbumped**.

**Main-repo follow-up (once applied + pointer bumped).** Drop `HtmlPostProcessor.StripIframeContent`
(the regex that empties `<iframe>…</iframe>` fallback) from `ProcessForBrowsing` and `Process` — the
renderer then suppresses the fallback natively. **Current fallback (unchanged until applied):**
`StripIframeContent` stays in both pipelines, so iframe fallback is still emptied at the string level
and nothing on CI depends on this patch.

## 0003 — `Broiler.DOM`: `DomNode.Normalize()` fires one `characterData` record per text run

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`). **Status: APPLIED — pinned at `Broiler.DOM` `8e8325f`.**

**What it does.** DOM Standard §4.4 `normalize` replaces a Text node's data with the
concatenation of its contiguous exclusive Text siblings' data in a single "replace data" step, so
a `characterData` MutationObserver observes **one** record per contiguous text run. Canonical
`DomNode.Normalize()` did `text.Data += next.Data` per merged sibling, publishing one CharacterData
record per merge step. The patch concatenates into a `StringBuilder` and sets `Data` once. Final
tree state is unchanged (the canonical `Normalize_Merges_Adjacent_Text_And_Removes_Empty_Text`
test still passes); only the mutation-record granularity is corrected.

**Applied.** The pinned `Broiler.DOM` `8e8325f` is exactly this change (its `Normalize()` is
byte-identical to the patch output). The main-repo `DomBridge.NormalizeNode`
(`HtmlFragmentMutation.cs`) delegates to `node.Normalize()` (Phase 4 item 5) and behaves correctly
against it — now including matching the one-record-per-run granularity.

## 0002 — `Broiler.DOM`: make `DomNodeCollectionExtensions` public

**Target:** `Broiler.DOM` (`Broiler.Dom/DomNode.cs`). **Status: APPLIED — pinned at `Broiler.DOM`
`5c71ac9` (ancestor of the pinned `8e8325f`).**

**What it does.** `DomNodeCollectionExtensions.IndexOfReference(this IReadOnlyList<DomNode>,
DomNode)` — the reference-equality child-index scan `DomRange` already uses internally — was on an
`internal` class, so bridge/host consumers couldn't reuse it. The patch makes the class `public`
(the method is already `public`) so the canonical scan can be shared. Behaviour-neutral (visibility
only).

**Applied.** The pinned `Broiler.DOM` makes `DomNodeCollectionExtensions` public. The main-repo
follow-up is done: `DomBridge.ChildIndexOf` (`DomBridge.cs`) delegates to
`element.ChildNodes.IndexOfReference(child)`, the manual loop deleted (byte-identical reuse).

## 0001 — `Broiler.HTML`: plumb `viewportZoom` through the static render entry

**Target:** `Broiler.HTML` (`Source/Broiler.HTML.Image/HtmlRender.cs`). **Status: APPLIED — pinned
at `Broiler.HTML` `9977672`.**

**What it does.** An earlier patch added `HtmlContainer.ViewportZoom` (a document-root paint
magnification applied in `PerformPaint`), but the static `RenderToImageWithStyleSet` /
`RenderToImageCore` entry points never exposed it, so a caller using those helpers (the WPT runner,
the product capture path) could not request a viewport zoom. The patch threads an optional
`viewportZoom` (default `1f`, byte-identical) through `RenderToImageWithStyleSet` →
`RenderToImageCore`, setting `container.ViewportZoom` before layout/paint.

**Applied.** The pinned `Broiler.HTML` `9977672` exposes `viewportZoom` on
`RenderToImageWithStyleSet` / `RenderToImageCore` and sets `container.ViewportZoom = viewportZoom`.

**Main-repo follow-up (now UNBLOCKED — the visual-viewport render cutover — not yet wired).** Thread
the bridge's pinch scale into the render:
- In the WPT render path (`WptTestRunner.RenderHtmlFileBitmap` / `RenderToImageWithStyleSet` call),
  pass `viewportZoom:` the active `visualViewport.scale` (exposed from the bridge, e.g. via
  `GetVisualViewportScale()`), gated on the pinch being active.
- Then stop `DomBridge.AnchorResolver.ApplyVisualViewportSerializationState` from writing the pinch
  factor into the document-root `zoom` (keep the root-scroll seed) — the render now magnifies
  natively via `ViewportZoom` instead of the serialization bake. Validate against a pinch-zoom
  render (no committed reftest corpus for pinch; use a render probe).

**Current fallback (unchanged until the cutover lands):** `ApplyVisualViewportSerializationState`
still bakes `zoom = usedZoom × scale` on the root, so pinch-zoom rendering works via the existing
serialization bake. Nothing on CI depends on the cutover.

---

## 0008 — `Broiler.JS`: host-overridable module resolution and source read

**Target:** `Broiler.JS` (`Broiler.JavaScript.Modules/JSModuleContext.cs`, `+` a `Modules.Tests` case).
**Status: APPLIED** (2026-07-21) — pushed as `ffe8956e`; the parent pointer is bumped and the pinned
`Broiler.JS` `3f0c7054` contains it. (Original authoring note preserved below.)
**Depends on:** nothing (behaviour-preserving for the default filesystem context).

**What it does.** Phase 7 item 6 tail. `Broiler.JavaScript.Modules` already implements a capable module
system — `CompileModuleAsync` compiles each module with the ES-module arg list under
`CoreScript.AllowTopLevelAwaitScope()` (so top-level `await` and dynamic `import()` are supported) — but
its resolution is hard-wired to the **filesystem** (`Resolve` uses `File.Exists`/`node_modules`/
`package.json`, `LoadModuleAsync` computes a filesystem directory, `CompileModuleAsync` reads via
`StreamReader`). A browser host resolves **URLs** against a base and fetches them under a
content-security policy, so it cannot use any of that. The patch opens three overridable seams without
changing default behaviour:

- `Resolve(dirPath, relativePath)`: `internal` → `protected virtual`.
- `GetModuleDirectory(fullPath)` (new `protected virtual`, default `Path.GetDirectoryName`): the base a
  loaded module's own relative imports resolve against — a URL host returns the module URL so nested
  imports resolve as URLs, not mangled filesystem paths. Used by `LoadModuleAsync`.
- `ReadModuleSourceAsync(module)` (new `protected virtual`, default reads the file): a URL host fetches
  the source over its transport instead.

**Validated** by a new `Modules.Tests` case (`HostUrlSeams_Resolve_Fetch_And_Execute_A_Url_Dependency`):
a `UrlModuleContext` subclass overriding the three seams drives a URL entry importing a URL dependency
from an in-memory store (no filesystem) — the entry and its dependency are resolved, fetched, and
executed through the seams. Push **403** → ships here, pointer **unbumped**, working tree reverted.

**Main-repo follow-up (NOT yet wired — and blocked below this patch).** The intent was for the bridge to
subclass `JSModuleContext`, override these seams with its `UrlResolver` + CSP-gated `ResourceLoader`, and
reuse the engine's real module compilation to gain the Phase 7 item-6 tail (live cyclic bindings, genuine
top-level-await ordering, dynamic `import()`) instead of the bridge's own `EsModuleLinker` string
transform. Validating this patch, and a subsequent deeper investigation, established that the engine's
module machinery is **itself incomplete beyond resolution — the follow-up is blocked on a core async
engine bug, not on this seam.**

**Root cause (diagnosed, not yet fixed).** A module body with a **top-level `await`** does not run to
completion — the code after the first `await` is dropped. Every static `import` desugars to
`tempRequire = yield import(spec)` (see `FastCompiler.VisitImportStatement`), so a static import *is* a
top-level await; that is why `import { x } from …` leaves `x` `undefined`. Reproduced minimally on the
**stock** engine (no seams, filesystem `.js`): a module body `globalThis.a=1; await Promise.resolve();
globalThis.a2=2;` sets `a` but never `a2`. The existing `Modules.Tests` TLA case only asserts the module's
return value is non-null, so it never caught this. Mechanism: the working `JSContext.Execute` path runs the
body under a **pumped** `AsyncPump` loop and drains the job loop (`WaitTask`) before taking the result,
keeping the async continuation on the engine's loop; the module path
(`RunAsync`/`RunScriptAsync` → `LoadModuleAsync` → `JSModule.InitAsync` → `CompileModuleAsync`) instead
awaits an un-pumped `SynchronizationContext`, and drives the body through a **double-marshaled** bridge
(`newModule.Compile` is a `JSFunction` returning `ClrInterop.Marshal(CompileModuleAsync())`, re-awaited via
`IJSPromise.Task`). The body promise settles at the first suspension and the real continuation resumes
later, off the loop.

**Module-orchestration layer (necessary, not sufficient).** The module path
(`RunAsync`/`RunScriptAsync` → `LoadModuleAsync` → `JSModule.InitAsync` → `CompileModuleAsync`) has two real
defects versus the working `JSContext.ExecuteScriptAsync`: it awaits an un-pumped `SynchronizationContext`,
and it drives the body through a **double-marshaled** bridge (`newModule.Compile` is a `JSFunction`
returning `ClrInterop.Marshal(CompileModuleAsync())`, re-awaited via `IJSPromise.Task`, whose continuation
hops to the thread pool). The correct module-side shape is to mirror `ExecuteScriptAsync`: run the whole
init under one `AsyncPump.Run` worker loop, add a non-marshalled `CompileDirect` hook so `InitAsync` awaits
`CompileModuleAsync` directly (no `JSFunction`/`IJSPromise` re-wrap), and in `CompileModuleAsync` drain
`WaitTask` then `await` the body promise on that same loop. This change is regression-safe (all
`Modules.Tests` stay green) — **but it does not make TLA work**, because the real blocker is one layer
deeper, in the engine's codegen.

**Deeper root cause — a stock-engine generator-rewriter codegen bug (proven, independent of the module
layer).** With the double-marshal removed, the continuation no longer escapes the loop (the earlier
`InvalidOperationException: The collection has been marked as complete` disappears) — it now resumes on the
pumped loop and **throws a `NullReferenceException` from inside the compiled module body**. This same NRE
reproduces on the **pristine** engine (pointer `3a8f302`, no seams, no module code) through the engine's own
`JSContext.EvalWithTopLevelAwaitAsync`, and it is fully deterministic and thread-independent (fails
identically under `AsyncPump` on a worker thread, under `AsyncPump` on the caller thread, and under a plain
`await` with no pump at all). So it is a **compile-time bug in the top-level-await async generator**, not an
async-runtime/pump/marshal problem.

**Re-confirmed on the now-pinned engine (2026-07-21).** With patch `0008` applied upstream and the parent
pointer bumped from `3a8f302` to **`3f0c7054`**, the codegen bug was re-verified to still be present on
that pinned engine via `JSContext.EvalWithTopLevelAwaitAsync` with **zero module code**: the boundary below
reproduces unchanged (e.g. `await Promise.resolve(); Math.max(1,2)` → NRE; `await Promise.resolve();
'hi'.length` → OK). A companion probe also shows the failure extends to a **local declared *after* the
await** and then read (`await Promise.resolve(); var x=5; x` → NRE), i.e. any read that dereferences a
box-lifted local on the resume path — consistent with, and slightly broader than, the receiver-spill
description below. Bumping the pointer therefore did **not** unblock the engine-driven module path; the
bridge `EsModuleLinker` remains in use.

Precise trigger boundary (each line is a whole module body; `await Promise.resolve()` is the suspension):

| Body after the first `await` resumes | Result |
| --- | --- |
| `… ; 1+1` / `'hi'.length` (constant receiver) | **OK** |
| `… ; globalThis` / `Math` / `typeof Math` (bare identifier read) | **OK** |
| `var g=Math; await …; g` (local read) | **OK** |
| `… ; globalThis.z` (member get, **variable** receiver) | **NRE** |
| `var g=Math; await …; g.max(1,2)` (member call, **variable** receiver) | **NRE** |
| `… ; Math.max(1,2)` / `(Math).max(1,2)` | **NRE** |

The discriminator is the **receiver**: a member access / call whose receiver is an identifier read
(local *or* global) — which the compiler spills into a temporary — dereferences null after the resume,
while a member access on a *constant* receiver (no spilled temp) survives. The failing frame is the
generated `vm-` delegate itself (`ClrGeneratorV2` → `GetNext` → the compiled body), not the async driver.
The fault is in the interaction between member-access **receiver spilling** and
`GeneratorRewriter.VisitBlock`'s **box-lifting** of the await-containing block's locals
(`Broiler.JavaScript.LinqExpressions/.../GeneratorsV2/GeneratorRewriter.cs`): the spilled receiver temp,
lifted into a generator `Box`, reads null on the resume path. Because static `import` desugars to
`tempRequire = yield import(spec)` (a top-level await), every static import that is followed by *any*
member access / call on an imported or global value trips this — which is why `import { x } from …; x.y()`
fails while the whole-module-snapshot linker does not.

This gating codegen defect is now **fixed by patch `0010`** (see §0010 below): the fault is not
receiver-specific but a `ScriptInfo`-box re-seed on every resume, and the corrected boundary is "any read of
a box-lifted local / an `Indices`-resolved name after the resume." The fix was validated against the **full**
`Broiler.JS` test suite with zero regressions. The corrected takeaway supersedes the earlier "the fix is a
completion-mechanism/pump change" note: the module-side completion path above is still correct and needed,
but the *gating* defect was the generator-rewriter re-seed bug — and that is what `0010` fixes.

Patch `0008` is the *resolution* seam, `0010` is the *codegen* fix, and **`0011` is the
module-orchestration completion fix** (§0011 below) — together they let the engine's own module machinery
bind a static import's value. The bridge still keeps its own working `EsModuleLinker` for now (which links
static import/export, `import.meta` — roadmap P7.18 — and dynamic `import()` — P7.20 — via a synchronous
IIFE + registry, with snapshot bindings and no TLA); driving the engine path instead and retiring the linker
is a remaining, separately-scoped bridge task, not a `Broiler.JS` gap.

---

## 0009 — `Broiler.JS`: session-isolation regression tests

**Target:** `Broiler.JS` (`Broiler.JavaScript.Integration.Tests/SessionIsolationTests.cs`) — test-only.
**Status: APPLIED** (2026-07-21) — pushed as `3f0c7054` (the pinned pointer). (Original authoring note
preserved below.)
**Depends on:** nothing (no production change).

**What it does.** Phase 2 investigation established that the engine already isolates two live
`JSContext` instances — the roadmap's earlier "two simultaneous sessions share globals, last-created
wins" note was outdated. This patch adds the regression guard that pins the guarantee: interleaved evals
on two contexts keep separate globals; a stored callback resolves *its own* context after another context
is created (so the constructor's last-wins `CurrentContext = this` does not leak); and 2000× concurrent
two-thread evals never clobber each other. Isolation comes from the `[ThreadStatic]` + `AsyncLocal`
current-context flow (`JSEngine.Current`/`_current`) plus the realm scope entered by `Eval` /
`InvokeFunction` — no production change is needed, so this is a guard only.

**Why it's a patch.** The `Broiler.JS` push returned **403**, so per `CLAUDE.md` it ships as
`patches/0009-js-session-isolation-tests.patch` with the pointer left **unbumped** and the working tree
reverted. There is no main-repo follow-up: the bridge-level exit criterion it underpins is already proven
CI-green in the parent by `DomBridgeSessionLifetimeTests.Two_Simultaneous_Sessions_Do_Not_See_Each_Others_State`.

---

## 0010 — `Broiler.JS`: seed the `ScriptInfo` box on first entry only (fix top-level-await resume)

**Target:** `Broiler.JS`
(`Broiler.JavaScript.LinqExpressions/LinqExpressions/GeneratorsV2/GeneratorRewriter.cs`, `+` a new
`Broiler.JavaScript.Core.Tests/TopLevelAwaitResumeTests.cs`).
**Status: APPLIED** (2026-07-22) — pushed as `64fda04f`; contained in the pinned `98b07636`. (Push 403 at
authoring; a maintainer later applied it upstream and the parent pointer was bumped. Original authoring note
preserved below.)
**Depends on:** nothing (it fixes the codegen blocker §0008 root-caused; independent of `0008`'s seam).

**What it does.** Fixes the top-level-await codegen bug from §0008. Every async function / generator body
is lowered by `GeneratorRewriter` into a state machine whose *box-load prologue* runs on **every** (re)entry
— first call and each `await`/`yield` resume — before the jump switch dispatches to the resume label. That
prologue reloads each lifted local's persistent `Box<T>` (`box = clrGenerator.GetVariable(i)`), and for the
`scriptInfo` local it additionally re-seeded the box's value:

```csharp
if (original == _replaceScriptInfo)
    boxes.Add(Assign(Field(_scriptInfoBox, "Value"), _replaceScriptInfo));
```

`_replaceScriptInfo` is the body-local `scriptInfo` parameter, whose only writes (`scriptInfo = new
ScriptInfo(){ … Indices … }`) are themselves redirected into `_scriptInfoBox.Value` — so as a *bare* local
it is always its default (`null`) at prologue time. On the first entry the re-seed is harmless (the body
sets the real value immediately after). On a **resume**, the `goto` jumps past that body assignment, so the
prologue's re-seed clobbers the `ScriptInfo` the box had persisted from the first run — including its
`Indices` key table — back to `null`.

Consequently, any statement executed after a top-level-await resume that resolves an identifier or member
through `scriptInfo.Value.Indices[…]` dereferenced null and threw `NullReferenceException`:

| Body after the first `await` resumes | Before `0010` | After `0010` |
| --- | --- | --- |
| `'hi'.length` (constant receiver), `globalThis` (bare global) | OK | OK |
| `var x = 5; x` (local read), `Math.PI` (member get), `Math.max(1,2)` (member call), `globalThis.a = 2` (member set), `o.m` on an awaited value | **NRE** | **OK** |

Constant receivers and bare globals resolve via constant `KeyStrings` (not the `Indices` table), which is
why the fault looked receiver-shaped in the original §0008 diagnosis; the true trigger is any
`Indices`-resolved access on the resume path. Because a static `import` desugars to `tempRequire = yield
import(spec)` (a top-level await), this gated every static import followed by use of the imported binding.

**The fix.** Guard the re-seed on the first entry only — `nextJump == 0` is the suspended-start state
(`yield`/`await` jump ids start at 1), so on every resume the persisted box value is preserved:

```csharp
if (original == _replaceScriptInfo)
    boxes.Add(IfThen(
        Equal(nextJump, Constant(0)),
        Block(Assign(Field(_scriptInfoBox, "Value"), _replaceScriptInfo), Empty)));
```

The first-entry seed that nested async/generator functions rely on is kept; the resume path no longer
clobbers the ScriptInfo. (The `true` branch is voided via `Block(…, Empty)` so the prologue block's stack
stays balanced.)

**Validated.** New `TopLevelAwaitResumeTests` (9) pin local read, member get/call/assign,
member-on-awaited-value, sequential awaits, constant-receiver survival, plus async-function and generator
regression guards. The **full `Broiler.JS` suite** was run: Core (23), Compiler (250), Runtime (11), Modules
(7), Ast (5), Parser (71), Clr (3), Debugger (3), Portable (5), Storage (11) all green; BuiltIns (1908/1909)
and Integration (4481/4486) and ModuleExtensions (2/3) carry only pre-existing Intl/ICU-locale and
doc-file-existence env failures — **all confirmed identical on the pristine engine (baselined by stashing
the fix), i.e. zero regressions from this change.**

**Why it's a patch.** The `Broiler.JS` push returned **403**, so per `CLAUDE.md` it ships as
`patches/0010-js-generator-scriptinfo-reseed-first-entry-only.patch` with the pointer left **unbumped**
(pinned `3f0c7054`) and the submodule working tree reverted. **No main-repo follow-up / fallback is needed**:
the parent bridge does not exercise the engine-driven top-level-await path (it runs modules through its own
`EsModuleLinker`), so nothing in the parent repo depends on or regresses without this patch.

**Scope.** `0010` fixes the *codegen* blocker only. Fully driving the engine's own ES-module machinery
(so a static import binds its value) additionally requires the module-orchestration completion fix — now
shipped as patch **`0011`** (§0011 below: `CompileDirect` + one pumped `AsyncPump.Run` loop + Clr-independent
import promise). With `0010` alone a static import no longer crashes but its value still resolves to
`undefined`/`0`; with `0010`+`0011` it binds. The bridge keeps its `EsModuleLinker` until it is wired to the
engine path.

---

## 0011 — `Broiler.JS`: pumped module init + Clr-independent import binding (module-orchestration completion)

**Target:** `Broiler.JS` (`Broiler.JavaScript.Modules/JSModule.cs`, `Broiler.JavaScript.Modules/JSModuleContext.cs`,
`+` a new `Broiler.JavaScript.Modules.Tests/EngineModuleImportBindingTests.cs`).
**Status: APPLIED** (2026-07-22) — pushed as `98b07636` (the pinned pointer); stacked on `0010`
(`64fda04f`). (Push 403 at authoring; later applied upstream and the parent pointer bumped. Original
authoring note preserved below.)
**Depends on / stacks on:** **`0010`** (the codegen fix — without it a top-level-await body still NREs).

**What it does.** With `0010` applied, an engine-driven static import no longer crashes but its value
resolved to `undefined`/`0` — the dependency *ran* (side effects fired) yet the imported binding was empty.
Three coupled defects, each fixed here:

1. **Un-pumped init loop.** `RunScriptAsync`/`RunAsync` set a plain (default) `SynchronizationContext` and
   `await`-ed the module init on the caller's thread. A module body compiled with `AllowTopLevelAwaitScope`
   is an async state machine; its continuation after the first `await` (every static import desugars to
   `tempRequire = yield import(spec)`, a top-level await) is *posted* to the ambient context. The default
   context never pumps on the current thread, so the body stalled at its first suspension. Now both entry
   points run their core under one `AsyncPump.Run` loop on a worker thread — exactly like
   `JSContext.ExecuteAsync` — so continuations drain and the body runs to completion. (The async-local
   current context flows across the `Task.Run` boundary, so engine state resolves on the worker thread.)

2. **Compile double-marshal.** `JSModule.InitAsync` invoked the `Compile` **JS function**, which wrapped the
   `CompileModuleAsync` .NET task in a JS promise (`ClrInterop.Marshal(task)`) that `InitAsync` then
   re-awaited (`promise.Task`) — a `Task → IJSPromise → Task` round-trip whose continuation hops off the
   running loop. Added a direct `JSModule.CompileDirect` (`Func<Task>`) hook that `InitAsync` awaits when set,
   so compilation stays on the one pumped loop with no re-marshal. The `Compile` JS function is retained for
   the JS-facing `module.compile()` API and as the fallback when `CompileDirect` is null.

3. **Clr-dependent import promise (the actual value loss).** `import()`/`require()` converted their
   `LoadModuleAsync` `Task<JSValue>` to a JS promise with `JSEngine.ClrInterop.Marshal(task)`. `ClrInterop`
   is only the full `DefaultClrInterop` (which maps `Task<JSValue> → CreatePromiseFromTask`) when the
   **optional** `Broiler.JavaScript.Clr` assembly is loaded; otherwise it is `FallbackClrInterop`, whose
   `Marshal` returns `JSUndefined` for any `Task` (`_ => UndefinedValue`). `Broiler.JavaScript.Modules` does
   **not** reference Clr, so `import(...)` resolved to `undefined` and the imported binding was lost — the
   dependency still ran because `LoadModuleAsync` was invoked for its side effects before the (undefined)
   marshal. Routed these through the engine-native `JSValue.CreatePromiseFromTask` (the same factory
   `DefaultClrInterop` uses for a `Task<JSValue>`, populated by the always-referenced BuiltIns assembly), via
   a small `TaskToPromise` helper, so import value binding works with or without Clr.

**Validated.** New `EngineModuleImportBindingTests` (6) drive the engine's own module machinery over an
in-memory URL store (no filesystem, no Clr): named import (`add(d,5)==12`), namespace import (`ns.d==7`),
default import (`v+1==42`), a transitive chain (`b==11`), a diamond whose shared dependency is evaluated
exactly once (`x+y==13`, `evalCount==1`), and a top-level-await dependency (`await Promise.resolve(42)` →
`v+1==43`). The pre-existing `Modules.Tests` (7, incl. `HostUrlSeams…`) stay green. The **full `Broiler.JS`
suite** was re-run: Core/Compiler/Runtime/Modules/Ast/Parser/Clr/Debugger/Portable/Storage green; BuiltIns
(1908/1909), Integration (4481/4486), ModuleExtensions (2/3) carry only the same **pre-existing** Intl/ICU
and doc-file env failures as at `0010` — **zero regressions**.

**Why it's a patch.** The `Broiler.JS` push returned **403**, so per `CLAUDE.md` it ships as
`patches/0011-js-module-orchestration-completion.patch` with the pointer left **unbumped** (pinned
`3f0c7054`) and the submodule working tree reverted. It **stacks on `0010`** (apply `0010` first, then
`0011`). **No main-repo follow-up / fallback is needed**: the parent bridge runs modules through its own
`EsModuleLinker`, so nothing in the parent repo depends on or regresses without this patch. Driving the
now-working engine module path from the bridge (and retiring the `EsModuleLinker`) is the remaining Phase-7
bridge task — application wiring, not a `Broiler.JS` gap.

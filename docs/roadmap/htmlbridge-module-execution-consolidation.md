# HtmlBridge module-execution consolidation (Phase 7 item-6 tail)

Status: **in delivery** — 2026-07-22. This record tracks closing the Phase 7 item-6 tail: migrate the two
remaining script-execution surfaces that still bypass the engine ES-module path onto it, then delete the
string-rewriting `EsModuleLinker` fallback. It also scopes out (as a separate, larger effort) the "genuine
event-loop ordering" rework.

## Premise verified

The whole migration presupposes the engine can actually drive ES modules end-to-end. **Confirmed empirically
on 2026-07-22**: with the `Broiler.JS` submodule pinned at `98b07636` (carrying module patches 0010+0011),
`EngineModuleSupport.Available` returns **`true`** in this environment (verified by running the probe through
a throwaway test), and the 303 module/snapshot/boundary tests pass on the engine-driven path. So the
engine-driven path is the live one and the linker is a dormant fallback.

## The three script-execution surfaces (measured)

There are three independent script-execution engines; they diverge on modules:

| Surface | Entry point | Module handling today |
| --- | --- | --- |
| **A. ScriptEngine** (App render/interactive) | `ScriptEngine.ExecuteCore` / `ExecuteInteractive` → `RunPageScripts` | Engine path (`ModuleRoots` + `BridgeModuleContext`) when `EngineModuleSupport.Available`, else linker fallback (`ModuleScripts` pre-appended to deferred by the host) |
| **B. Sub-documents / iframes** | `DomBridge.ExecuteSubDocumentScripts(container, html)` (`Broiler.HtmlBridge.Dom`) | **Linker only** — evals `extraction.ModuleScripts` directly on the shared `_jsContext`; never consults the engine probe |
| **C. CLI capture** | `CaptureService.ExecuteScriptsWithDom` (`Broiler.Cli`) | **None** — modules are neither detected nor linked; a `type="module"` script is eval'd as a classic script and throws/drops |

Only **A** is engine-gated. **B** is the one production `ModuleScripts` consumer that always uses the linker;
**C** has no module support at all.

## The blocker that made this a refactor, not a quick migration

The engine-module seam — `BridgeModuleContext` (a `JSModuleContext` wired to the browser host: URL/`data:`
resolution + CSP-gated fetch) and the `EngineModuleSupport` probe — lived in **`Broiler.HtmlBridge.Scripting`**.
But the two surfaces that must adopt it sit **below or outside** Scripting in the assembly graph:

- `Broiler.HtmlBridge.Scripting` **references** `Broiler.HtmlBridge.Dom`, so **Dom cannot reference Scripting** —
  the iframe path (in Dom) could not reach `BridgeModuleContext`.
- `Broiler.Cli` does **not** reference `Broiler.HtmlBridge.Scripting` at all — `CaptureService` could not reach
  it either.

So the seam had to move **down** the stack before either path could use it. `BridgeModuleContext` depends only
on `Broiler.JavaScript.Modules` (engine) + `ContentSecurityPolicy`/`ScriptExtractionService` (Core) — all
reachable from Dom — so **Dom is the correct new home** (Dom is referenced by Scripting, by Cli, and by App).

## Increment 1 — relocate the engine-module seam into Dom ✅ (2026-07-22)

Moved `BridgeModuleContext.cs` and `EngineModuleSupport.cs` from `Broiler.HtmlBridge.Scripting` into
`Broiler.HtmlBridge.Dom` (namespace kept as `Broiler.HtmlBridge.Scripting` to avoid consumer `using` churn).
Added the `Broiler.JavaScript.Modules` project reference to Dom, and granted Dom `InternalsVisibleTo`
`Broiler.HtmlBridge.Scripting` so `ScriptEngine` keeps accessing the still-`internal` `BridgeModuleContext`.
Behavior-neutral — no logic changed, the engine path runs identically from its new home.

Public-API effect (baselines regenerated, diff verified to be exactly this): the `public`
`EngineModuleSupport` type moves from the **Scripting** assembly baseline to the **Dom** baseline; its
fully-qualified name (`Broiler.HtmlBridge.Scripting.EngineModuleSupport`) and surface (`Available`) are
unchanged, so source consumers are unaffected. `BridgeModuleContext` is `internal`, so it is in no baseline.

Verified: all eleven affected projects build clean; **303/303** module + public-API-snapshot + boundary-guard
tests pass. This unblocks surfaces **B** (Dom can now reach the seam) and **C** (Cli → Dom).

## Remaining increments (sequenced)

2. **Migrate the iframe path (B) onto the engine path.** In `ExecuteSubDocumentScripts`
   (`Broiler.HtmlBridge.Dom/DomBridge/SubDocuments.cs`), run `extraction.ModuleRoots` through a
   `BridgeModuleContext` on the sub-window realm when `EngineModuleSupport.Available`, keeping the
   `ModuleScripts`/linker branch as the fallback (mirroring surface A). Update the emptiness guard to consider
   `ModuleRoots`. Verify against the sub-document module suite.
3. **Add module support to CaptureService (C).** Route module discovery/execution through the engine path
   (now reachable via Dom). Additive — modules are dropped today — so it cannot regress existing capture; it
   gives captured pages working modules.
4. **Retire the linker.** Once A/B always take the engine path and C is engine-driven, drop the `ModuleScripts`
   production (`ScriptExtractionService.ExtractAll` → `ModuleGraphLoader.Load` → `EsModuleLinker.Render`), the
   `ScriptExtractionResult.ModuleScripts` property (public-API removal, baseline regen), and delete
   `EsModuleLinker`, `EsModuleScanner`, `EsModuleLiveRefs`, `ModuleGraphLoader`, `ModuleScriptWrapper`
   (all in `Broiler.HtmlBridge.Internal.Scripting`). The `EngineModuleSupport` probe/gate can then be
   simplified since the fallback is gone — a deliberate commit to the engine path, gated on the pinned engine.

## Out of scope here — genuine event-loop ordering

The roadmap also lists "genuine event-loop ordering (vs the eager deferred-bucket run)". Today `RunPageScripts`
(and the iframe/capture equivalents) run scripts → deferred → modules → load as **fixed phase buckets**,
draining async work to a fixed point between phases via `DrainAsyncWork`; `BrowserEventLoop` fires timers as an
unordered per-step snapshot with no deadline ordering. A spec-faithful loop (a single ordered task queue with
per-task microtask checkpoints, deadline-ordered timers, module evaluation enqueued after graph resolution)
is a **separate, larger `BrowserEventLoop` rework** — also a Phase-2 residual — and is tracked apart from the
linker-retirement work above.

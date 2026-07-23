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

## Increment 2 — migrate the iframe path onto the engine path ✅ (2026-07-22)

In `ExecuteSubDocumentScripts` (`Broiler.HtmlBridge.Dom/DomBridge/SubDocuments.cs`) the iframe module step now
prefers the engine path: when the page is engine-driven — the shared `_jsContext` is a `JSModuleContext`
(a `BridgeModuleContext`) **and** `EngineModuleSupport.Available` **and** `ModuleRoots` are present — it runs
each authorised root through `RunScriptAsync` on that context (inside the existing
`RunWithWindowContext(subWindow, …)` scope, so `document`/`window` resolve to the sub-window), exactly like
`ScriptEngine.RunPageScripts`. Otherwise it falls back to eval'ing the linked `ModuleScripts` strings — so a
non-module-context parent (the CLI-capture path, or a plain-`JSContext` test) is unchanged. The emptiness guard
now also considers `ModuleRoots` (robust for when `ModuleScripts` is later deleted). This closes the one
production `ModuleScripts` consumer that was not engine-gated.

New `SubDocumentEngineModuleTests` (2) — the iframe module path had **no** execution coverage before: an
iframe `srcdoc` module runs and mutates the sub-document DOM through the engine path when the parent is a
`BridgeModuleContext`, and through the linker fallback on a plain `JSContext`. Verified regression-free: the
new tests pass; the module/sub-document/iframe suites are otherwise green (the only failures — 3
`HttpSubResourceTests` network cases + 1 `DomBridge_SerializeToHtml_*` — reproduce identically on a stashed
clean baseline, i.e. pre-existing bare-container env failures).

## Increment 3 — add module support to CaptureService ✅ (2026-07-22)

`CaptureService.ExecuteScriptsWithDom` (`Broiler.Cli`) had no module support: its regex extractor bucketed
every `<script>` into `scripts`/`deferredScripts`, so a `type="module"` script was eval'd as a classic script,
threw, and was dropped. Reachable now that the seam lives in Dom (Cli → Dom). Added a `TypeModuleAttrPattern`,
routed `type="module"` scripts to a `moduleRoots` bucket (excluded from the classic buckets), and — when
`moduleRoots` is non-empty **and** `EngineModuleSupport.Available` — built the capture context as a
`BridgeModuleContext(csp, url)` instead of a plain `JSContext`, running the roots through `RunScriptAsync`
after the deferred scripts (page URL as module base; per-root unique key). When the engine can't bind imports
the context stays a plain `JSContext` and the roots are left unrun — same net effect as before (modules did
not execute), minus the spurious syntax-error log.

Additive and behavior-preserving for existing captures: for a page with no modules the `moduleRoots` bucket is
empty, so the "nothing to run" guard, the context type, and the execution path are byte-identical to before; a
corpus sweep confirmed **no** existing capture/Acid/WPT test uses `type="module"`. New `CaptureServiceModuleTests`
(2): an inline module imports a value and mutates the captured DOM; a no-script page passes through untouched.
Verified: `Broiler.Cli`/`Cli.Tests` build clean; a 729-test sweep over the capture consumers (Acid2/Acid3,
capture, module, sub-document, iframe) shows only the 11 pre-existing bare-container env failures (6 Acid3
score/CSS, 1 Acid2 image, 3 `HttpSubResource` network, 1 `SerializeToHtml`) — all module-unrelated and all
previously confirmed on baseline.

## Increment 4 — retire the linker ✅ (2026-07-22)

With all three surfaces on the engine path, the string-rewriting `EsModuleLinker` fallback is deleted. Changes:

- **`ScriptExtractionService.ExtractAll`** no longer builds the linked graph: the authorised top-level modules
  are collected directly as `ModuleRoot`s (the intermediate `ModuleGraphLoader.GraphModule` is gone), and the
  `ModuleGraphLoader.Load` → `EsModuleLinker.Render` call and the `ResolveDependencyModule` dependency-fetch
  helper are removed. `ModuleRoots` is the sole module-execution output.
- **`ScriptExtractionResult.ModuleScripts`** (the linked-strings property) is removed — the one public-API
  change; baseline regenerated, diff verified to be exactly the property + its constructor parameter and
  nothing else.
- **`RenderingPipeline`** drops the `DeferredScripts.Concat(ModuleScripts)` fallback and always passes
  `ModuleRoots` (ScriptEngine gates on `EngineModuleSupport.Available`). **`SubDocuments`** (iframe) drops the
  `ModuleScripts` else-branch.
- **Deleted** (all in `Broiler.HtmlBridge.Internal.Scripting`): `EsModuleLinker`, `EsModuleScanner`,
  `EsModuleLiveRefs`, `ModuleGraphLoader`, `ModuleScriptWrapper`, `EsModuleSyntax` — ~1,900 lines of
  string-rewriting module-linking machinery. Doc comments/log messages that named the fallback were updated.

**Documented behavior change.** With no fallback, a sub-document (iframe) module runs only under an
engine-driven parent (a `JSModuleContext`); a module-bearing iframe on a *module-less* host page (whose
context is a plain `JSContext`) is left unrun rather than linker-run. This is the trade-off of the engine-only
path; on the pinned engine the main production paths (App render, CLI capture) give module-bearing pages an
engine context, so the affected case is narrow.

**Test changes.** `EsModuleGraphTests` (34 tests of the linker/graph machinery) deleted. `ModuleScriptSliceTests`
rewritten to assert against `ModuleRoots`/`ModuleMap` instead of the removed `ModuleScripts`/`ModuleScriptWrapper`
(the module-extraction contract it pins is unchanged). `EngineModuleWiringTests` simplified to the engine-only
path. The increment-2 `Iframe_Module_Linker_Fallback_On_Plain_Context` became
`Iframe_Module_Not_Run_Under_Plain_Context_Parent`, pinning the documented limitation.

Verified: all projects build clean; the public-API snapshot (one removal), boundary-guard, and the module /
sub-document / iframe / capture suites are green — 372/377 in the sweep, the 5 failures being pre-existing
bare-container env failures (1 Acid3 image-capture, 3 `HttpSubResource` network, 1 `SerializeToHtml`), all
module-unrelated.

## Outcome

All four increments delivered. Every script-execution surface (App render/interactive, iframes/sub-documents,
CLI capture) now runs ES modules through the engine's own module machinery via `BridgeModuleContext`, gated by
the `EngineModuleSupport` probe; the string-rewriting `EsModuleLinker` and its scanner/linker/loader/wrapper
are gone. The Phase 7 item-6 tail's *"sub-document/CLI paths still use the linker"* item is closed.

## Out of scope here — genuine event-loop ordering

The roadmap also lists "genuine event-loop ordering (vs the eager deferred-bucket run)". Today `RunPageScripts`
(and the iframe/capture equivalents) run scripts → deferred → modules → load as **fixed phase buckets**,
draining async work to a fixed point between phases via `DrainAsyncWork`; `BrowserEventLoop` fires timers as an
unordered per-step snapshot with no deadline ordering. A spec-faithful loop (a single ordered task queue with
per-task microtask checkpoints, deadline-ordered timers, module evaluation enqueued after graph resolution)
is a **separate, larger `BrowserEventLoop` rework** — also a Phase-2 residual — and is tracked apart from the
linker-retirement work above.

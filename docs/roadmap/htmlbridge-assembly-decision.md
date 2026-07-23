# HtmlBridge assembly decision (Phase 8 item 6)

Status: **decided** — 2026-07-22. This is the decision record for Phase 8 item 6 of the
[complexity-reduction roadmap](htmlbridge-complexity-reduction-roadmap.md): *"Decide final assemblies from
dependency and deployment needs: likely Core, WebApi bindings and Scripting/Host. Avoid assembly-per-feature."*
It settles the target assembly shape and, for every disputed type, its home — and sequences the physical
moves that the two structural exit criteria still require:

- *"Core contains contracts/value objects, not regex parsers, networking and mutable global logging together."*
- *"A public v3 is proposed only for changes which cannot be adapted behind v2."*

The one-execution-pipeline exit criterion is already met (P8.6).

## Current state (measured 2026-07-22)

Four `Broiler.HtmlBridge.*` code assemblies exist. The build graph is `ProjectReference`-based (there is no
top-level `.sln`; only `Broiler.Graphics` carries one), so an assembly is folded by editing the consuming
`.csproj` references and the `InternalsVisibleTo` set, not a solution file.

| Assembly | Files | Role today | Referenced by |
| --- | --- | --- | --- |
| `Broiler.HtmlBridge.Core` | 19 | Contracts + value objects **+** scripting mechanism (parsers, loaders, CSP, logging) | `Dom`, `Scripting`, `DevConsole`, `Wpt` |
| `Broiler.HtmlBridge.Dom` | 239 | The Web API bindings (`DomBridge` + `Features/*` + `Runtime/*`) | `Cli`, `Engines.Baseline`, `Scripting`, `Wpt`, `Wpt.Tests` |
| `Broiler.HtmlBridge.Rendering` | **1** | One `internal` regex HTML post-processor | `Browser.Core`, `Cli`, `Wpt` |
| `Broiler.HtmlBridge.Scripting` | 8 | `ScriptEngine` host + interactive session | `Browser.Core`, `Cli.Tests` |

Dependency spine: `Scripting → Dom → Core` and `Scripting → Core`; `Rendering` depends on **nothing** (BCL
regex only). Both `Dom` **and** `Scripting` pull scripting *mechanism* out of `Core` (`Dom` uses `Origin`×59,
`RenderLogger`×34, `ContentSecurityPolicy`×10, `UrlResolver`, `ScriptExtractionService`; `Scripting` uses
`EsModuleLinker`×6, `ScriptExtractionService`×3, `UrlResolver`) — so that mechanism cannot move *up* into
`Scripting`; its only shared home below both is `Core`.

### What `Core` actually holds (the purity gap)

`Core`'s 19 files mix all four categories the exit criterion says to keep apart:

- **Contracts / value objects (belong in Core):** `IDomBridgeRuntime`, `Origin` (43 lines), `PageContent`
  (29), `ScriptExecutionResult` (39), `ScriptExtractionResult` (142), `ContentSecurityPolicy` (443),
  `MicroTaskQueue` (70), `ScriptProfilingHook` (74).
- **Regex parsers / scanners (do not belong):** `EsModuleScanner` (620), `ScriptExtractionService` (403),
  `EsModuleSyntax` (123), `CspMetaDiscovery` (71).
- **Networking / loaders (do not belong):** `ModuleGraphLoader` (118), `UrlResolver` (30),
  `EsModuleLinker` (263), `EsModuleLiveRefs` (262), `CspSourceMatching` (59), `ModuleScriptWrapper` (23).
- **Mutable global logging (do not belong):** `RenderLogger` — a `static class` with mutable static
  `_entries`, `_minimumLevel` and a static `EntryLogged` event.

## Decision

### 1. Final shape: three code assemblies (fold `Rendering` away)

The target is exactly the roadmap's *"Core, WebApi bindings, Scripting/Host"* — realised as the three
**existing** code assemblies once `Rendering` is eliminated:

- **`Broiler.HtmlBridge.Core`** — the shared kernel: contracts + value objects, **plus** the scripting
  mechanism that both `Dom` and `Scripting` share (see decision 3). It stays one assembly; the contracts/
  mechanism boundary is enforced by **namespace**, not by splitting off a fourth assembly (a
  `Core.Mechanism` split would be assembly-per-feature — the anti-goal — and would gain nothing at deploy
  time, since every consumer of the mechanism already consumes `Core`).
- **`Broiler.HtmlBridge.Dom`** — the Web API bindings. Already a clean deployment unit (239 files, the whole
  `DomBridge`/`Features`/`Runtime` surface). No change.
- **`Broiler.HtmlBridge.Scripting`** — the Scripting/Host layer (`ScriptEngine`, `InteractiveSession`, the v2
  capability interfaces). Already a clean unit (8 files). No change.

Rationale: these three are the real dependency/deployment units. An embedder that only renders static HTML
takes `Core`+`Dom`; one that runs script takes `+Scripting`. There is no deployment scenario that wants a
finer split, so finer assemblies would only add build/versioning surface — the "avoid assembly-per-feature"
caution.

### 2. `Broiler.HtmlBridge.Rendering` → delete; relocate `HtmlPostProcessor` into `Dom`

`Rendering` is the one unambiguous assembly-per-feature artifact: a whole assembly for a single `internal
static HtmlPostProcessor`. Its Phase-6 mandate was already "delete this project"; item 6 confirms it.

- **Not into `Core`.** `HtmlPostProcessor` is a regex HTML rewriter — precisely what the Core exit criterion
  excludes. Folding it into `Core` would deepen the purity gap this decision is closing.
- **Into `Broiler.HtmlBridge.Dom`.** All three consumers reach `Dom` transitively today
  (`Browser.Core → Scripting → Dom`; `Cli → Dom`; `Wpt → Dom`), so the `Rendering` project reference is
  removable from each without adding a new edge. `Dom` already owns HTML parsing/serialization
  (`DomBridge.HtmlParsing`, `DomBridge.Serialization`), so a host-facing HTML string post-processor is
  co-located with its kin, and it stays `internal` (the `InternalsVisibleTo` list moves onto `Dom`, which
  already grants IVT to the same consumers). One caveat to honour on execution: `HtmlPostProcessor`'s
  **test-harness** half (`Process`/`Strip*`/`RewriteRootSelector`, split out in P6.2) is transitional
  scaffolding slated for removal once the corresponding submodule patch lands — relocating it must not
  resurrect it into the production `ProcessForBrowsing` path.

### 3. `Core` purity: carve mechanism and logging into named internal namespaces, keep one assembly

The exit criterion's operative word is *"together"* — `Core` is a grab-bag. The fix is **separation, not a
new assembly** (both `Dom` and `Scripting` depend on the mechanism, so it cannot leave `Core` without a
fourth shared assembly, which is the anti-goal):

- Move the parsers/scanners (`EsModuleScanner`, `EsModuleSyntax`, `ScriptExtractionService`,
  `CspMetaDiscovery`), loaders (`ModuleGraphLoader`, `UrlResolver`, `ModuleScriptWrapper`) and linker
  (`EsModuleLinker`, `EsModuleLiveRefs`, `CspSourceMatching`) under an explicit
  `Broiler.HtmlBridge.Internal.Scripting` (or `…Core.Mechanism`) **namespace**, leaving the top-level
  `Broiler.HtmlBridge` namespace for contracts/value objects only. This makes the boundary visible and
  greppable without a build-graph change, and lets a later `internal`-ization pass (many of these are public
  only to be reachable from `Dom`/`Scripting`, which share IVT) shrink the public surface behind the v2
  adapter.
- **`RenderLogger` stays**, as a deliberate, documented exception. It is a cross-cutting diagnostics sink
  used by every layer (34 call sites in `Dom` alone); routing it through an injected `IRenderLog` would touch
  hundreds of sites for no deployment benefit. The decision is to **keep the static sink** but treat it as an
  explicitly-sanctioned diagnostics primitive in its own `Broiler.HtmlBridge.Logging` namespace (already
  there), not as "contracts drift." If a future host needs per-instance log isolation, that is the trigger to
  revisit — not before.

### 4. Profiling (`IScriptProfiling` / `ScriptProfilingHook`) — **keep** (resolves the P8.7-deferred call)

P8.7 made profiling consistent across every script kind but recorded that **no production code or test sets
`Profiler`, instantiates the hook, or reads `.Entries`**. Item 6 owns the keep-vs-trim decision:

- **Keep it.** Removal is a public-surface deletion (`IScriptProfiling` off the `IScriptEngine` aggregate, the
  `Profiler` property, `ScriptProfilingHook`) that **cannot be adapted behind the v2 interface** — it would
  force a v3 *solely* to drop one now-consistent, low-cost, opt-in hook. That fails the *"v3 only for changes
  which cannot be adapted behind v2"* bar on a cost/benefit basis: the capability is null by default (zero
  runtime cost when unset), it is now correct (P8.7), and it has a real test consumer
  (`ScriptProfilingConsistencyTests`). The "no consumers" observation is not, by itself, worth a breaking
  surface revision.
- The relocation-to-host-diagnostics option is therefore **declined**, not deferred: keeping the consistent
  in-engine hook is the lower-risk, v2-preserving outcome.

## Execution status and sequencing

Item 6 is a **decision** item; this record satisfies it. The physical moves it authorises are **structural
exit-criterion follow-ups**, each touching the `ProjectReference`/namespace graph and so best delivered as
separately-verified increments (full build + WPT/Acid baseline) rather than one sweep:

| Follow-up | Scope | Risk | Verifies |
| --- | --- | --- | --- |
| **F1 — delete `Rendering`, relocate `HtmlPostProcessor` → `Dom`** ✅ **done** | `git mv` 1 file; drop 3 `ProjectReference`s; add `Browser.Core` to `Dom` IVT; drop the empty `Rendering` public-API baseline + snapshot param | Low–med (build-graph of 3 apps + tests) | Full build + snapshot/boundary/HtmlPostProcessor green |
| **F2 — namespace-carve `Core` mechanism** ✅ **done** | Move the 7 already-internal mechanism types to `Broiler.HtmlBridge.Internal.Scripting`; add cross-`using`s | Low (namespace-only, no logic change) | Full build + snapshot/boundary/mechanism suites green |
| **F3 — `internal`-ize now-namespaced mechanism** ✅ **done** | Flip `CspMetaDiscovery` + `ModuleScriptWrapper` (public but consumed only by `Core` + tests) `public`→`internal` and move them into the internal namespace; `ScriptExtractionService` stays public (host API) | Med (public-API baseline shifts; adaptable behind v2) | API snapshot regenerated + full build |

Decisions **1** (three-assembly target) and **4** (keep profiling) are settled with no code change required.
F1–F3 are **all delivered**, closing the `Core`-purity exit criterion: `Core` is now the shared kernel with
a clean, greppable boundary — contracts/value objects in `Broiler.HtmlBridge.Scripting`, the internal
mechanism in `Broiler.HtmlBridge.Internal.Scripting`, and the one host-facing public service
(`ScriptExtractionService`) kept public.

### F1 delivered (2026-07-22)

`Broiler.HtmlBridge.Rendering` is deleted and its sole `internal static HtmlPostProcessor` moved into
`Broiler.HtmlBridge.Dom` (co-located with the existing HTML parse/serialize helpers). The three consumers
(`Browser.Core`, `Cli`, `Wpt`) drop their `Rendering` `ProjectReference` — each already reaches `Dom`
transitively — and `Dom`'s `InternalsVisibleTo` gains `Broiler.Browser.Core` (the one grant `Rendering` had
that `Dom` lacked). One relocation wrinkle: in the old BCL-only `Rendering` assembly the unqualified `Regex`
bound to the BCL type, but `Dom` transitively references the `Broiler.Regex` namespace, whose `Regex` member
is reachable through the enclosing `Broiler` namespace and shadows the simple name; an **in-namespace**
`using Regex = System.Text.RegularExpressions.Regex;` alias (resolved before the enclosing namespace's
members) restores the binding. The `Rendering` public-API baseline was empty (`HtmlPostProcessor` is
internal), so it is removed along with the assembly's snapshot-test parameter — **no public surface is lost**,
confirming decision 2. Verified: `Dom`/`Browser.Core`/`Wpt`/`Cli`/`Cli.Tests`/`Wpt.Tests` all build clean;
the public-API snapshot (3 assemblies), boundary-guard and `HtmlPostProcessor` native-support suites are green
against a clean baseline; `HtmlPostProcessor` behaviour is byte-identical (its 2 `<video>`-stripping test
failures pre-exist on baseline — the assertion is stale now that video renders natively). The full 2297-test
`Cli.Tests` run is not a usable gate in a bare container (its network/graphics-parity/PDF classes crash or
fail for environmental reasons), so F1 was validated by targeted baseline-vs-change comparison on the
assembly- and consumer-relevant suites.

### F2 delivered (2026-07-22)

The scripting *mechanism* is carved out of the top-level `Broiler.HtmlBridge.Scripting` namespace into a new
`Broiler.HtmlBridge.Internal.Scripting` namespace, making the contracts-vs-mechanism boundary greppable
inside `Core` without splitting off a fourth assembly. Consumer analysis during implementation refined which
types move:

- **Moved (7, all already `internal` → zero public-API impact):** `EsModuleScanner`, `EsModuleSyntax`,
  `EsModuleLinker`, `EsModuleLiveRefs`, `ModuleGraphLoader`, `UrlResolver`, `CspSourceMatching` — the module
  scanners/parsers/linker/loader plus URL/CSP matching.
- **Kept in `Broiler.HtmlBridge.Scripting`:** the value objects/contracts (`ContentSecurityPolicy`,
  `MicroTaskQueue`, `PageContent`, `ScriptExecutionResult`, `ScriptExtractionResult`, `ScriptProfilingHook`)
  and `Origin` — an `internal static` origin-serialization primitive the inventory loosely called a value
  object; it is a common identifier (moving it risks false-positive `using` churn) and pairs conceptually
  with the value side, so it stays.
- **Deferred to F3:** the three `public` mechanism-ish types. Two (`CspMetaDiscovery`, `ModuleScriptWrapper`)
  are public but consumed only by `Core` + tests, so F3 will `internal`-ize them and then move them. The
  third, **`ScriptExtractionService`, is genuinely host-facing public API** — `Broiler.App` and `Broiler.Cli`
  call it to extract scripts from HTML — so it is **not** internal mechanism and will **stay public** in a
  public namespace, refining the original decision-3 enumeration which had listed it to move.

Cross-namespace `using`s were added where the split created new references (stayed `Core` types referencing
moved ones get `using …Internal.Scripting`; moved types referencing stayed value objects get
`using …Scripting`), plus the two `Dom` files and four `Cli.Tests` files that consume the moved types via IVT.
Pure compile-time reorganization — no logic changed. Verified: all eleven affected projects build clean
(`Core`, `Dom`, `Scripting`, `Browser.Core`, `Cli`, `Cli.Tests`, `Wpt`, `Wpt.Tests`, `DevConsole`,
`DevConsole.Tests`, `Engines.Baseline`); the public-API snapshot is **unchanged** (the move touched only
`internal` types), and the boundary-guard plus the moved types' own suites (CSP-source-matching, URL-resolver,
ES-module-graph, engine-module-wiring, module-script-slice) are green — 69/69 on the targeted filter. No
stale fully-qualified `Broiler.HtmlBridge.Scripting.<moved-type>` references remain.

### F3 delivered (2026-07-22) — `Core`-purity exit criterion closed

The two remaining `public` mechanism helpers are `internal`-ized and moved into
`Broiler.HtmlBridge.Internal.Scripting`, completing the mechanism carve started in F2:

- **`CspMetaDiscovery`** (`FindPolicyContent`) and **`ModuleScriptWrapper`** (`WrapInlineModule`) — both were
  `public` but a tree sweep confirmed they are consumed only by `Core` and `Cli.Tests` (which reaches them via
  IVT), so `internal` costs no real consumer. Flipped `public`→`internal` and moved to the internal namespace.
- **`ScriptExtractionService` stays `public`** in the top-level `Broiler.HtmlBridge` namespace — it is
  host-facing API (`Broiler.App`/`Broiler.Cli` call it), so it is the one extraction service that belongs on
  the public surface, not in the internal mechanism.

This removes exactly two entries from the `Core` public-API baseline (`CspMetaDiscovery.FindPolicyContent`,
`ModuleScriptWrapper.WrapInlineModule`) and nothing else — the baseline was regenerated with
`UPDATE_API_BASELINES=1` and the diff verified to be those two removals only. The two `Cli.Tests` consumers
(`CspMetaDiscoveryTests`, `ModuleScriptSliceTests`) gained the internal-namespace `using`. Verified: `Core`
and `Cli.Tests` build clean; the public-API snapshot (now reflecting the two removals), boundary-guard and the
mechanism suites are green — 80/80 on the targeted filter.

**End state.** `Core` now presents a clean contracts-vs-mechanism split: the top-level
`Broiler.HtmlBridge.Scripting` namespace holds the value objects/contracts (`ContentSecurityPolicy`,
`MicroTaskQueue`, `Origin`, `PageContent`, `ScriptExecutionResult`, `ScriptExtractionResult`,
`ScriptProfilingHook`), the `Broiler.HtmlBridge.Internal.Scripting` namespace holds the nine internal
mechanism types (module scanners/parsers/linker/loader, URL resolver, CSP discovery/matching, module wrapper),
`ScriptExtractionService` remains the public host-facing extraction service, and `RenderLogger` remains the
sanctioned static diagnostics primitive in `Broiler.HtmlBridge.Logging` (decision 3). The `Core`-purity exit
criterion — *"Core contains contracts/value objects, not regex parsers, networking and mutable global logging
together"* — is met: they are no longer grab-bagged together, but separated by namespace within the one
shared kernel assembly (a fourth assembly being the anti-goal, since `Dom` and `Scripting` share the
mechanism). With F1–F3 delivered and all decisions settled, Phase 8 item 6 is fully realized.
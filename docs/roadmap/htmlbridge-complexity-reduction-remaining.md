# HtmlBridge complexity-reduction — remaining phases

Status: Phase 6 in progress (concerns 1 & 3 delivered); Phases 7–8 proposed

This document tracks the **not-yet-delivered** phases of the HtmlBridge
complexity-reduction program: removing `Broiler.HtmlBridge.Rendering` (Phase 6),
isolating loading / security / browsing-context policy (Phase 7), and
simplifying Core and Scripting before reconsidering assemblies (Phase 8).

For where these sit in the critical path, see
[Priority and sequencing](htmlbridge-complexity-reduction-roadmap.md#priority-and-sequencing)
in the overview.

Companion documents:

- [Overview & governance](htmlbridge-complexity-reduction-roadmap.md).
- [Implemented delivery log (Phases 0–5)](htmlbridge-complexity-reduction-implemented.md).
- [Working notes: native dialog / backdrop feature track](htmlbridge-complexity-reduction-notes.md).

---

## Detailed delivery roadmap (remaining)

### Phase 6 - remove Broiler.HtmlBridge.Rendering

Goal: dissolve a project which currently groups three unrelated concepts.

Disposition:

| Current type | Interim action | End state |
|---|---|---|
| SharedLayoutGeometryProvider | Put behind ILayoutView and make lifetime/cache correct | implementation in HTML.Orchestration/HTML.Headless |
| HtmlPostProcessor | Convert to ordered, non-destructive render-preparation passes | native HTML/Layout behavior; remaining Acid/WPT shims in test support |
| CanvasRenderingContext2D / CanvasDrawCommand | Internalize in Canvas binding and cap/remove unused command storage | real immutable Broiler.Graphics display list if a renderer consumes it |

HtmlPostProcessor must not be moved wholesale into Broiler.Dom.Html: it strips or
replaces valid content and contains renderer/test policy. The migration is to
replace each workaround with native HTML/Layout behavior, not to rename it.

Exit criteria:

- Rendering project has no consumers and is deleted.
- Render preparation never mutates the live script-visible DOM.
- Production browsing does not apply Acid/WPT-specific transforms.
- Canvas commands are either rendered and bounded or are not recorded.

#### Delivered increments

- **Concern 1 — `SharedLayoutGeometryProvider` (already dissolved).** The end state is in place: the
  geometry provider moved behind `ILayoutView`, implemented by `HeadlessLayoutView` in
  `Broiler.HTML.Headless`, with the bridge's `DomBridge/SharedLayoutGeometry.cs` seam. No
  `SharedLayoutGeometryProvider` type remains in production (only the historical test name persists), and
  the Rendering project no longer contains it.

- **P6.1 (2026-07-20) — Concern 3, Canvas: internalized + unused command storage removed.** The
  `canvas.getContext("2d")` binding's `CanvasRenderingContext2D` recorded every drawing call into a
  growing `List<CanvasDrawCommand>` that **no renderer ever read** — the context lived only inside the
  `BuildCanvas2DContext` JS closures, and no code path retrieved a canvas element's commands to paint
  them (unbounded dead storage). Per the exit criterion "Canvas commands are either rendered and bounded
  or are not recorded", the recorder and its `CanvasDrawCommand` / `CanvasDrawCommandType` DTOs were
  deleted and the type internalized into the Canvas binding (`Broiler.HtmlBridge.Dom`,
  `DomBridge/CanvasRenderingContext2D.cs`, now `internal`). It keeps only the script-observable drawing
  state (styles + the `save`/`restore` state stack); the pure drawing methods stay callable no-ops so the
  JS API is unchanged. `CanvasCommandRecorder.cs` is removed from the Rendering project and the
  `Broiler.HtmlBridge.Dom → Broiler.HtmlBridge.Rendering` project reference is dropped (Dom used Rendering
  only for Canvas); `Broiler.Browser.Core` — which consumed `HtmlPostProcessor` transitively through that
  edge — now references Rendering directly. New `CanvasContextBindingTests` (5) pin the observable
  behaviour (context object, style round-trip, save/restore stack, no-op draws don't throw); guard suites
  green. Since a headless canvas is never painted, the disposition's conditional end state (a real
  Broiler.Graphics display list "if a renderer consumes it") does not apply.

- **P6.2 (2026-07-20) — Concern 2, `HtmlPostProcessor`: production / test-harness profile split.** The
  `Process()` pipeline (a ~660-line regex HTML-string rewriter) was applied verbatim in **production
  browsing** (`BrowserApp`) and CLI capture (`CaptureService`) as well as the WPT/Acid harness, so
  production applied the Acid/WPT-specific `StripHiddenTestArtifacts` cleanup — which strips test
  scaffolding (`linktest`/`FailDiv`) **and, incidentally, valid content such as `<map>`** that real pages
  must keep. Investigation confirmed the split is safe: `StripHiddenTestArtifacts` is entirely
  Acid2/Acid3-specific; the production capture entry (`CaptureImageAsync`) has no test callers; and no
  test asserts `Process()` *applies* the artifact cleanup (the Acid image tests render via
  `HtmlRender.RenderToImageWithStyleSet` directly, `Acid3RegressionTests` call the individual `Strip*`
  delegators, `Acid3CssComplianceTests` use `ExecuteScriptsWithDom`; the only `Process()` assertion is a
  *negative* guard that it does not strip tables). Delivered:
  - `HtmlPostProcessor.Process(html)` is kept **byte-identical** (shared replaced-element passes →
    `StripHiddenTestArtifacts` → `RewriteRootSelector`) as the **test-harness** profile; the WPT runner and
    the render-helper tests keep using it, so no test render input changes.
  - New `HtmlPostProcessor.ProcessForBrowsing(html)` is the **production** profile: the shared
    replaced-element passes + `:root`→`html`, but **not** `StripHiddenTestArtifacts`. `BrowserApp` (×3) and
    `CaptureService` capture now call it — closing the exit criterion *"production browsing does not apply
    Acid/WPT-specific transforms."* (This also stops production from mangling real `<map>` elements.)
  - New `HtmlPostProcessorProfileTests` (6) pin the split deterministically: production preserves
    `<map>`/`linktest`/`FAIL`; the harness still strips them; both apply the shared replaced-element
    preparation (script/video) and neither strips `<table>`. Full Cli.Tests run: only the 3 pre-existing
    environmental pixel/writing-mode failures (`CssPseudoElement_ContentUrl_Renders_Image_Content`,
    `Border_Shorthand_Expands_Color_To_Individual_Sides`, `SelectListBox_SizingAndScrolling_Follow_WritingMode`),
    confirmed identical on the clean baseline → zero regressions.

- **P6.3 (2026-07-20) — Concern 2, native-behaviour migration: retire `RewriteRootSelector` from
  production.** A/B render-probe investigation (font-independent background-colour / text-presence checks
  via `HtmlRender.RenderToImageWithStyleSet`) classified each production transform as dead vs still-needed:
  - `RewriteRootSelector` (`:root`→`html`) — **DEAD.** The renderer paints a `:root{background}` rule
    natively without the rewrite, and the rewrite was actively *buggy* (it lowered `:root`'s specificity
    from a pseudo-class (0,1,0) to the `html` type selector (0,0,1), which can flip the cascade on real
    pages). **Removed from `ProcessForBrowsing`** — production now relies on native `:root`.
  - `StripScriptTags` — dead for its stated *content-bleed* purpose (the renderer does not render
    `<script>` bodies, incl. HTML-like markup inside them), but **kept**: it runs first as a protective
    normaliser so the later regex passes cannot match `<video>`/`<iframe>` literals inside a script string
    and corrupt real content across the boundary.
  - `StripIframeContent` — **still needed** (the renderer *does* render `<iframe>` fallback children as a
    visible block; probe: 6400 green px). Native fix (treat `<iframe>` as a replaced element, suppress
    fallback) is `Broiler.HTML` → submodule-push-gated.
  - `ReplaceVideo`/`ReplaceProgressLike`/`ReplaceSelectMultiple` — legitimate replaced-element fallbacks
    (Broiler cannot decode video / lacks native progress/meter/listbox chrome); kept pending native support.

  New `HtmlPostProcessorNativeSupportTests` (4) pin the evidence (native `:root` paints; `<script>` content
  does not bleed) and the string-level split (production keeps `:root`; the harness still rewrites it).
  Only the same 3 environmental failures; zero regressions. `RewriteRootSelector` stays in the test-harness
  `Process()` pending the WPT/Acid reftest gate to retire it there too.

- **Remaining for Phase 6 — Concern 2 native migration + project deletion.** The still-needed workarounds —
  `StripIframeContent` (native iframe replaced-element handling) and, in the test-harness profile, the
  Acid/WPT strips (`StripHiddenTestArtifacts`, `StripCssDataUriBackgrounds`, `StripObjectContent`,
  `StripTables`, `StripForms`) plus the now-redundant `StripScriptTags`/`RewriteRootSelector` — are largely
  `Broiler.HTML` / `Broiler.CSS` **submodule-push-gated → patch workflow** and need the Acid/WPT pixel
  reftest gate (environmental in a bare container) to validate. Per the disposition,
  `HtmlPostProcessor` must **not** be moved wholesale to rename it; the migration is behavioural. Once the
  test-harness profile's remaining transforms are relocated to test support, the Rendering project (then
  empty) is deleted.

### Phase 7 - isolate loading, security and browsing-context policy

Goal: separate deterministic document algorithms from host I/O and policy.

Work:

1. Split ContentSecurityPolicy into immutable directive parsing/evaluation,
   document meta discovery, and URL/origin context.
2. Replace regex HTML discovery in CSP and ScriptExtractionService with
   Broiler.Dom.Html parser output.
3. Make script extraction return metadata-rich descriptors: source kind, URL,
   nonce, async/defer/module flags and document order.
4. Route scripts, stylesheets, fetch, XHR and frames through one injected
   ResourceLoader with explicit file/data/http policy and cancellation.
5. Keep CSP checks in the host/browser layer; DOM and CSS receive already
   authorized content.
6. Move module-script support and script ordering into the browser event loop;
   do not silently skip a recognized module.

Exit criteria:

- No direct HttpClient/file/data-URI switch remains in feature callbacks.
- Unit tests use a deterministic in-memory loader.
- One URL resolution/origin implementation is shared by script, CSS, fetch, XHR
  and frames.
- CSP tests distinguish parse, discovery, policy and load/execution decisions.

### Phase 8 - simplify Core and Scripting, then reconsider assemblies

Goal: leave small contracts whose names match their responsibility.

Work:

1. Split IScriptEngine execution, interactive-session, profiling and
   microtask/event-loop capabilities. Preserve the old interface as an adapter
   until a deliberate public-surface v3.
2. Give every InteractiveSession a private event-loop/context lifetime. Ensure
   failed construction disposes it.
3. Make async-drain-limit exhaustion an explicit diagnostic/result, not a silent
   stop.
4. Apply profiling consistently or move it to host diagnostics if there are no
   real consumers.
5. Rename IScriptExtractor.cs to match ScriptExtractionResult, or restore a
   meaningful interface.
6. Decide final assemblies from dependency and deployment needs:
   likely Core, WebApi bindings and Scripting/Host. Avoid assembly-per-feature.

Exit criteria:

- Core contains contracts/value objects, not regex parsers, networking and
  mutable global logging together.
- ScriptEngine has one execution pipeline shared by normal, detailed, typed and
  interactive entry points.
- A public v3 is proposed only for changes which cannot be adapted behind v2.


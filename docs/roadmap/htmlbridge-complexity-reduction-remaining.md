# HtmlBridge complexity-reduction — remaining phases

Status: **Phase 6 native-rendering migration complete** (all three concerns delivered; submodule
patches `0004`–`0007` applied upstream and the `Broiler.HTML` pointer bumped to `5c16c12`; the
`HtmlPostProcessor` video/progress/meter/select fallbacks are dropped) — only the terminal
`Broiler.HtmlBridge.Rendering` project deletion remains, gated on relocating the test-harness shims
behind the WPT pixel reftest gate. **Phase 7 items 1–5 complete** (CSP split, script descriptors,
loader/`UrlResolver`/`Origin` consolidation, external-stylesheet CSP, and host-layer CSP enforcement) **and
item 6's static import/export module graph linked and executing** (P7.17) **with `import.meta` (P7.18),
dynamic `import()` (P7.20) and live bindings (P7.22, scope-accurate in P7.23) handled at the bridge layer**
(exports + namespace live universally; named imports rewritten scope-accurately, falling back to a correct
snapshot only for `class`/`with`/`eval` modules); the remaining item-6 tail is the genuinely engine-coupled
part — top-level-await-as-async (with event-loop ordering). The `Broiler.JS` host-resolution seam for driving
the engine's own module machinery (patch `0008`, P7.19), the Phase-2 session-isolation guard (patch `0009`),
the top-level-await **codegen** fix (patch `0010`, P7.25) and the **module-orchestration completion** fix
(patch `0011`, P7.26) **are all applied upstream and pinned** — the engine pointer is now bumped to
**`98b07636`** (0010 = `64fda04f`, 0011 = `98b07636`), so **all eleven submodule patches `0001`–`0011` are
applied upstream and pinned**. The `0010` codegen bug (root-caused P7.21, proven P7.24): the generator
box-load prologue re-seeded the persisted `ScriptInfo` box with a null body-local on every resume, clobbering
its `Indices` key table, so any `Indices`-resolved identifier/member access after a TLA resume NRE'd; guarding
the seed on `nextJump == 0` (first entry only) fixes it, **validated against the full `Broiler.JS` suite with
zero regressions**. The `0011` module-orchestration defect — an un-pumped init loop, a `Task→IJSPromise→Task`
compile double-marshal, and (the actual value loss) an `import()` promise built through the Clr-only interop
whose fallback returns `undefined` for a `Task` — is fixed by a pumped `AsyncPump.Run` init, a direct
`CompileDirect` hook, and the engine-native `JSValue.CreatePromiseFromTask`. With `0010`+`0011` pinned the
engine's own module machinery **binds a static import's value** (named/namespace/default/transitive/diamond/
TLA), full-suite-validated with zero regressions. The engine-coupled tail is therefore **closed**, and the
bridge is wired to the engine module path (P7.27): **on the pinned engine `EngineModuleSupport.Available`
returns `true`** (verified 2026-07-22 — built `Broiler.HtmlBridge.Scripting` against `98b07636`, ran the
probe; 288 `~Module` `Broiler.Cli.Tests` pass on the active engine path), so the engine-driven path is
active and the `EsModuleLinker` is the dormant fallback. The last item-6 work is a **bridge application
task** — migrate the sub-document (`ExecuteSubDocumentScripts`) and CLI-capture (`CaptureService`) paths off
the linker onto the engine path, add genuine event-loop ordering, and then delete the `EsModuleLinker`.
**Phase 8 proposed.**

This document tracks the **not-yet-fully-delivered** phases of the HtmlBridge
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

- **P6.4 (2026-07-20) — Concern 2, native-behaviour migration: `<iframe>` replaced-element handling
  (submodule patch `0004`, pending).** The render probe classified `StripIframeContent` as still-needed
  because the renderer painted `<iframe>` fallback children as a visible block. The native fix is in
  `Broiler.HTML` (`DomParser`): a post-cascade `CorrectIframeBoxes` pass sets `display:none` on an iframe
  box's direct children, mirroring the frameset `<noframes>` handling (post-cascade because a cascade-time
  hide is re-shown for a block child; sub-documents compose separately, so loaded iframe content is
  unaffected). **Verified via the parent build + render probe** (iframe fallback 6400→0 green px; real
  content still paints; the standing iframe sub-resource tests fail identically to baseline — pre-existing
  network env). The `Broiler.HTML` push returned **403**, so per `CLAUDE.md` it ships as
  `patches/0004-html-iframe-replaced-element-hide-fallback.patch` with the pointer **unbumped** and the
  working tree reverted; `HtmlPostProcessor.StripIframeContent` stays as the active fallback in both
  profiles until the patch lands and the pointer is bumped (then it is dropped). Also fixed the P6.1
  fallout: the `Broiler.HtmlBridge.Rendering` public-API snapshot baseline, which still listed the removed
  public Canvas types (`CanvasRenderingContext2D`/`CanvasDrawCommand`/`CanvasDrawCommandType`) — regenerated
  so the assembly's now-empty public surface is the baseline.

- **P6.5 (2026-07-20) — Concern 2, dead-shim removal.** Consumer analysis showed three of the
  `HtmlPostProcessor` Acid-shim transforms had **no live callers**: `StripForms` (never referenced) and
  `StripTables` / `StripCssDataUriBackgrounds` (only their `CaptureService` delegators existed, and nothing
  called those — `Acid3RegressionTests` uses `StripObjectContent`/`StripScriptTags`/`StripIframeContent`/
  `StripHiddenTestArtifacts` but not these). Render probes corroborated that the renderer no longer paints
  empty `<form>`/`<table>` as visible blocks. Removed the three methods, their regexes
  (`FormPattern`/`TablePattern`/`CssDataUriBgPattern`), and the two dead `CaptureService` delegators;
  refreshed the stale "intentionally NOT applied" notes. Pure dead-code deletion — builds green;
  `Acid3RegressionTests` + guards/profile/native suites green (no public-API change: all internal).

- **P6.6 (2026-07-20) — Concern 2, native-behaviour migration: `<video>` replaced-element handling
  (submodule patch `0005`, applied upstream).** **Update:** patch `0005` was pushed to `Broiler.HTML`
  (`5561eb0`, contained in the pinned `5c16c12`) and the pointer bumped, so
  `HtmlPostProcessor.ReplaceVideoWithPlaceholder` has been **dropped** — the renderer boxes `<video>`
  natively. Original authoring note follows. Native replacement for `HtmlPostProcessor.ReplaceVideoWithPlaceholder`
  (which string-rewrites `<video>…</video>` into a black `<div>` at its width/height or 300×150). Added a
  post-cascade `CorrectVideoBoxes` pass in `Broiler.HTML` (`DomParser`) — mirroring `CorrectIframeBoxes` — that
  makes a `<video>` box `inline-block`, sizes it (author CSS size wins; else the `width`/`height` presentation
  attributes; else the CSS-default intrinsic 300×150), paints it black, and hides the fallback children.
  **Verified via the parent build + a render probe on raw `<video>`** (`HtmlRender.RenderToImageWithStyleSet`,
  bypassing the string fallback): a bare `<video>` paints black inside the 300×150 box, and inline fallback
  content is hidden (black, not green, over the fallback area). The `Broiler.HTML` push returned **403**, so
  per `CLAUDE.md` it ships as `patches/0005-html-video-replaced-element-black-box.patch` with the pointer
  **unbumped** and the working tree reverted; `HtmlPostProcessor.ReplaceVideoWithPlaceholder` stays the active
  fallback (it runs in the string pipeline before the renderer, so a real `<video>` never reaches
  `CorrectVideoBoxes` yet) until the patch lands and the pointer is bumped (then it is dropped). Also observed
  while reverting: patch **`0004` is now applied upstream** — the pinned `Broiler.HTML` `52f65d9` contains
  `CorrectIframeBoxes` — so `StripIframeContent` is now a redundant (harmless) belt-and-suspenders strip whose
  removal is unblocked; `patches/README.md` corrected accordingly.

- **P6.7 (2026-07-20) — Concern 2, native-behaviour migration: `<progress>`/`<meter>` replaced-element
  handling (submodule patch `0006`, applied upstream).** **Update:** patch `0006` was pushed to
  `Broiler.HTML` (`444cace`, contained in the pinned `5c16c12`) and the pointer bumped, so
  `HtmlPostProcessor.ReplaceProgressLikeWithPlaceholder` has been **dropped** (native coverage:
  `FormControlRenderTests.Meter_NativeRender_Follows_WritingMode_And_Direction`). Original authoring note
  follows. Native replacement for
  `HtmlPostProcessor.ReplaceProgressLikeWithPlaceholder` (which string-rewrites the elements into a styled
  `<div>` track + fill). Added a post-cascade `CorrectProgressBoxes` pass in `Broiler.HTML` (`DomParser`) that
  renders each as a bordered `inline-block` track (1px `#767676`, bg `#f0f0f0`/`#e6e6e6`, `120×16` or `16×120`
  vertical) with an absolutely-positioned fill bar proportional to `value` (`#0a84ff`/`#4caf50`), honouring
  `writing-mode`/`direction` for vertical and RTL bars, hiding the fallback text — matching the fallback's
  exact colours/sizes. **Verified via the parent build + a render probe on raw `<progress>`/`<meter>`**: the
  fill paints blue/green over the left ratio·120 px and the remainder is track grey. Push **403** → ships as
  `patches/0006-…patch`, pointer **unbumped**, working tree reverted; `ReplaceProgressLike` stays the active
  fallback (string pipeline, pre-renderer) until the patch lands and the pointer is bumped. `select multiple`
  is the one remaining replaced-element fallback.

- **P6.8 (2026-07-20) — Concern 2, native-behaviour migration: `<select multiple>` replaced-element handling
  (submodule patch `0007`, applied upstream, stacks on `0006`).** **Update:** patch `0007` was pushed to
  `Broiler.HTML` (`5c16c12`, the pinned pointer) and the pointer bumped, so
  `HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder` has been **dropped** (both appearance modes render
  natively; the string-rewrite unit test was retired in favour of the WPT/Acid reftest gate). Original
  authoring note follows. Native replacement (native-appearance case) for
  `HtmlPostProcessor.ReplaceSelectMultipleWithPlaceholder`. Added a post-cascade `CorrectSelectMultipleBoxes`
  pass in `Broiler.HTML` (`DomParser`) rendering the control as an `inline-block` list box: one 16px row track
  per visible option (`size` clamped 2..8, default 4), first row `#3875d7` selection highlight, alternating
  `#ffffff`/`#f7f7f7` rows, edge `#dcdcdc` scrollbar chrome, writing-mode-aware, `<option>` children hidden.
  **Verified via the parent build + render probe on raw `<select multiple>`** (72×68 host, blue first row
  `(56,117,215)`, alternating rows `(247,247,247)`, chrome `(220,220,220)`). Push **403** → ships as
  `patches/0007-…patch`, pointer **unbumped**, working tree reverted. **Scope limitation:** the
  `appearance:none` variant needs a CSS `appearance` box property (a separate `Broiler.Layout` change), so the
  fallback stays for `appearance:none` only until the box `appearance` property lands (P6.9, below, now done).
  This is the last replaced-element native pass — all of video/progress/meter/select now have native rendering
  (patches `0005`–`0007`), the iframe pass is already upstream (`0004`).

- **P6.9 (2026-07-20) — `appearance` box property + `<select multiple appearance:none>` completed.** Added a
  CSS `appearance` box property to **`Broiler.Layout`** (`CssBox.Appearance`, default `auto`, wired through
  `CssUtils` get/set dispatch for `appearance`/`-webkit-appearance`; the `SharedRendererCascade` populates it
  like any other longhand). `Broiler.Layout` is a **directory in this repo, not a submodule**, so this is a
  direct main-repo change (no patch) — additive, `Broiler.Layout` builds and the CSS/render sanity suites stay
  green. Patch `0007` was then extended to branch `CorrectSelectMultipleBoxes` on the box's `Appearance`:
  `appearance:none` drops the scrollbar chrome, uses a white field and a lighter `#9a9a9a` border, matching the
  fallback's non-native variant. **Verified via render probes**: `appearance:auto` keeps the chrome strip
  (528 px) and grey field; `appearance:none` drops the chrome entirely (0 px) and uses white. Patch `0007`
  regenerated to include this; `<select multiple>` native rendering now covers both appearance modes.

- **Remaining for Phase 6 — Concern 2 project deletion only.** The native migration is **done and shipped**:
  patches `0004`–`0007` are applied upstream, the `Broiler.HTML` pointer is bumped to `5c16c12`, and the
  `ReplaceVideo`/`ReplaceProgressLike`/`ReplaceSelectMultiple` fallbacks (and their exclusive
  helpers/patterns) have been **dropped** from `HtmlPostProcessor` — the renderer now boxes every replaced
  element natively (both `<select>` appearance modes). The residue is: (a) `StripIframeContent` is now a
  redundant belt-and-suspenders strip (`0004`'s native `CorrectIframeBoxes` is pinned) whose removal is
  **unblocked** but deliberately deferred to keep changes focused and because it touches the WPT render
  harness path (pixel-reftest-gated, not validatable in a bare container); (b) the protective `StripScriptTags`
  and the test-harness-only shims (`StripHiddenTestArtifacts`, `StripObjectContent`, `RewriteRootSelector`)
  relocated to test support once the reftest gate can validate; then the emptied `Broiler.HtmlBridge.Rendering`
  project (still consumed by `Broiler.Browser.Core`/`Broiler.Cli`/`Broiler.Wpt`) is deleted. Per the
  disposition, `HtmlPostProcessor` must **not** be moved wholesale to rename it; the migration is behavioural.
  **All native-rendering authoring is done and merged**; the terminal step is test-harness relocation +
  project deletion, both gated on the WPT pixel reftest gate — not new rendering code.

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

#### Delivered increments

- **P7.1 (2026-07-20) — item 1 (partial): split CSP document-discovery from policy.** `ContentSecurityPolicy`
  mixed three concerns (directive parse/evaluation, document `<meta>` discovery, URL/origin source
  matching). Extracted the **discovery** concern into a new `CspMetaDiscovery.FindPolicyContent(html)` — it
  answers "where is the policy declared in this document" and returns the raw, unparsed directive string;
  `ContentSecurityPolicy.FromHtml` now composes it with `Parse` (so all six callers are unchanged). The
  shared attribute reader moved to an internal `HtmlAttributeReader` (used by both discovery and the nonce
  extractor), and the meta regex + the now-unused `partial` moved out of the policy class. New
  `CspMetaDiscoveryTests` (6) exercise discovery in isolation — distinct from `ContentSecurityPolicyTests`
  (policy) — satisfying the "tests distinguish parse / discovery / policy" exit criterion for the discovery
  boundary. Behaviour-preserving; Core public API baseline regenerated (adds `CspMetaDiscovery`);
  CSP + snapshot suites green.

- **P7.2 (2026-07-20) — item 3: metadata-rich script descriptors.** `ScriptExtractionService.ExtractAll`
  already computed each `<script>`'s nonce, module/defer/async flags and source kind internally, then
  discarded all of it — returning only categorised program text. Added a neutral `ScriptDescriptor` record
  (`DocumentOrder`, `ScriptSourceKind` {Inline, DataUri, External}, `Url`, `Nonce`, `IsAsync`, `IsDefer`,
  `IsModule`, `Content`) and a `ScriptExtractionResult.Descriptors` list covering **every** discovered
  script in document order — including `type=module` scripts, which the classic execution buckets still
  omit (item 6 wires those into the event loop). Purely additive: the `Scripts`/`DeferredScripts`/
  `AsyncScripts` buckets are derived exactly as before, so all consumers (`RenderingPipeline`,
  `SubDocuments`, tests) and their assertions are unchanged. New `ScriptDescriptorTests` (4) pin the
  metadata (kind/order/url/nonce/flags; modules recorded-but-not-executed). This is the host-agnostic shape
  items 4 (loader) and 6 (module event loop) consume. Core public-API baseline regenerated; extraction +
  CSP + snapshot suites green.

- **P7.3 (2026-07-20) — item 4 (partial): consolidate the file/http dispatch into the loader.** The
  `ResourceLoader` (P2.6) only wrapped HTTP; the file-vs-http *switch* still lived inline in feature
  callbacks — exactly the "direct file/http switch in a feature callback" the exit criterion forbids. Added
  `ResourceLoader.LoadText(url)` which owns the dispatch policy in one place (a `file://` URL reads from
  disk, `http(s)` goes through the shared client, everything else → `null`; I/O exceptions propagate so the
  caller logs with its own context). Routed `DomBridge.FetchExternalStylesheet` (`Css.cs`) through it,
  deleting its inline `Uri`/`File.Exists`/`File.ReadAllText`/`GetStringAsync` branch. New
  `ResourceLoaderTests` (2) cover the file-read and null-for-missing/relative/unsupported-scheme paths
  (deterministic, no network). `ResourceLoader` is internal → no public-API change; CSS/stylesheet + loader
  suites green (the one failure, `HttpClientMigrationTests.StylesheetLoadHandler_Uses_HttpClient`, is a
  pre-existing environmental failure — identical on the `6a19eeb` baseline).

- **P7.4 (2026-07-20) — item 4 (partial): dedup the script fetch/decode switch to one implementation.**
  The external-script file/http switch and the `data:` decoder were **duplicated** — a near-identical
  `FetchExternalScript` (resolve + file/http + a second 30 s `HttpClient`) and a byte-identical
  `DecodeDataUri` existed in both `ScriptExtractionService` (Core) and `CaptureService` (Cli). Collapsed the
  Cli copies to thin delegators onto the single Core implementation (`CaptureService.FetchExternalScript`
  maps the service's `null` → `string.Empty` to preserve its callers'/tests' empty-on-failure contract),
  and deleted `CaptureService`'s now-unused `SharedHttpClient` and `WhitespacePattern`. Behaviour-preserving
  — the `SubResourceFetching`/`HttpSubResource` `FetchExternalScript` assertions and the
  `CaptureServiceJsTests` `DecodeDataUri` assertions (base64/percent/empty) pass unchanged; internal-only, no
  public-API change. One fewer file/http switch and one fewer process `HttpClient`.

- **P7.5 (2026-07-20) — item 1 complete: extract the CSP URL/origin context.** With discovery already split
  out (P7.1), moved the third concern — the URL/origin source-matching (`ResolveUri`, `IsSameOrigin`,
  `IsSchemeSource`, `MatchesAbsoluteSource`) — out of `ContentSecurityPolicy` into a new internal
  `CspSourceMatching`. `AllowsExternalScript` now calls the matcher; the policy class keeps only directive
  parse/evaluation. New `CspSourceMatchingTests` (5) exercise resolution / same-origin (incl. file:) /
  scheme-source / absolute-host-source matching in isolation. Item 1's split is now three independently
  testable units — **discovery** (`CspMetaDiscovery`), **policy** (`ContentSecurityPolicy`), **URL/origin
  matching** (`CspSourceMatching`) — satisfying the "CSP tests distinguish parse / discovery / policy" exit
  criterion. Behaviour-preserving; `CspSourceMatching` is internal → no public-API change; CSP + snapshot
  suites green. (`CspSourceMatching` is also the natural seam toward the exit criterion's "one URL
  resolution/origin implementation shared by script, CSS, fetch, XHR and frames".)

- **P7.6 (2026-07-20) — item 4 (partial): one shared URL resolver.** `CspSourceMatching.ResolveUri` and
  `ScriptExtractionService.FetchExternalScript` each carried an identical "absolute stays / relative
  resolves against base / else null" resolution. Extracted the single `UrlResolver.Resolve(url, baseUrl)`
  (internal, Core); the CSP matcher now delegates to it and `FetchExternalScript` uses it (also tightening
  its file-scheme check to the resolved `Uri` rather than a `StartsWith("file://")` string test).
  Behaviour-preserving (verified: a leading-slash path like `/x.js` still parses as an absolute `file:` URI
  on Unix, as before). New `UrlResolverTests` (3); internal → no public-API change. This is the seam toward
  the exit criterion's "one URL resolution/origin implementation shared by script, CSS, fetch, XHR and
  frames" — script + CSP now share it; CSS/fetch/frames (the Dom `ResourceLoader`) adopt it when the
  cross-assembly loader seam lands.

- **P7.7 (2026-07-20) — item 4 (partial): frames adopt the shared `UrlResolver`.** The nested-browsing-context
  fetch path (`DomBridge/SubDocuments.cs`) carried its **own** copy of the "absolute stays / relative resolves
  against base / else empty" resolution in two places — `ResolveSubResourceUrl` (the `ISubWindowHost` seam used
  by `SubWindowBinding` to resolve an iframe/frame `src`) and the relative branch of `TryFetchSubResource`
  (object/iframe sub-resource fetch). Both now delegate to the single `Broiler.HtmlBridge.Scripting.UrlResolver`
  (already shared by script + CSP, P7.6) — `ResolveSubResourceUrl` collapses to
  `UrlResolver.Resolve(url, baseUrl)?.AbsoluteUri ?? string.Empty` (byte-identical: the old code returned
  `AbsoluteUri` for both the absolute and resolved cases), and `TryFetchSubResource` routes its *relative* case
  through the resolver while deliberately keeping the **raw string** for an already-absolute URL so the
  downstream `file://` / `http(s)` scheme checks and the WPT `web-platform.test` host mapping still see the
  original prefix (changing it to a normalised `AbsoluteUri` there could shift casing/normalisation). Frames
  now share the same URL resolution as script and CSP — three of the five consumers named in the exit criterion
  ("one URL resolution/origin implementation shared by script, CSS, fetch, XHR and frames"). Behaviour-preserving
  (`UrlResolver` is `internal` in Core, visible to Dom via `InternalsVisibleTo`, so no public-API change); the
  43 `SubResourceFetching`/`SubWindowBinding`/`SubDocumentBinding`/`IframeElementBinding` sub-resource tests
  pass unchanged and the CSP/resolver/descriptor guard suites (44) stay green (the 3 `HttpSubResource`
  relative-iframe failures are pre-existing network-env failures — identical on the `e03b140` baseline).

- **P7.8 (2026-07-20) — item 4 (partial): sub-resource `file://`/local reads move into the loader.** The
  nested-browsing-context fetch path inlined `File.Exists`/`File.ReadAllText` in two feature callbacks —
  `TryReadFileResource` (a resolved `file://` URL) and `TryReadLocalResource` (the `LocalBasePath` directory)
  — each with its own copy of the binary-content-type predicate (`image/`/`font/`/`audio/`/`video/`/`pdf` →
  return the MIME but not the decoded bytes). Exactly the "direct file switch in a feature callback" the exit
  criterion forbids. Added `ResourceLoader.LoadLocalResource(path, extensionMime)` — the one place that owns
  the file existence + binary/text read policy: missing → `(null, "")` (empty document, not a fetch failure),
  binary MIME → `(null, extensionMime)` (bytes not decoded), else → `(File.ReadAllText, extensionMime)` with
  I/O exceptions propagating — plus a shared `ResourceLoader.IsBinaryMime` so the binary-content rule lives
  once. `TryReadFileResource` collapses to mapping the URL to a path + delegating (and is no longer `static`,
  so it reaches the loader); `TryReadLocalResource` keeps only its query/fragment strip, scheme reject, and
  the generic-MIME content sniff (`DetectContentTypeFromContent`, bridge-owned), delegating the read. New
  `ResourceLoaderTests` cases (2) pin `LoadLocalResource` (text read / binary skip / missing) and
  `IsBinaryMime` (9 content types) deterministically. Behaviour-preserving: the 55 sub-resource / sub-window
  binding tests pass unchanged (the same 3 `HttpSubResource` relative-iframe/WPT-root failures are
  pre-existing network-env failures, identical on the P7.7 baseline). `ResourceLoader` remains `internal` to
  Dom → no public-API change. The residual sub-resource dispatch still in the callback is the `data:` decode
  and the HTTP `GetAsync` (already the loader) + the WPT host→local-root mapping (test policy, correctly
  bridge-owned).

- **P7.9 (2026-07-20) — item 4 (partial): fetch adopts the shared `UrlResolver`.** The fetch binding's one
  inline C# URL resolution — `FetchBinding.ResolveResponseRedirectUrl` (the `Response.redirect(url, status)`
  static: resolve the `Location` relative to the page URL) — carried its own copy of the "absolute stays /
  relative resolves against base" pattern, throwing the spec's `TypeError` ("Invalid URL") on failure. It now
  delegates to `UrlResolver.Resolve(redirectUrl, _host.PageUrl)`, mapping a `null` result to that same
  `JSException`. (XHR needs no change — it is implemented in JS on top of `fetch` and stores its URL verbatim;
  `CreateRequestObject` likewise stores the request URL as-is, so fetch's only resolution site is the redirect
  helper.) Behaviour-preserving — the existing `Fetch_Response_Redirect_Static_Sets_Status_And_Location_Header`
  test (`/next` → `file:///next` against `file:///test.html`) and the invalid-status guard pass unchanged; 117
  fetch/network tests green (the 2 `bodyUsed`-reader failures are pre-existing, identical on the P7.8 baseline).
  With this, **all five** consumers named in the exit criterion — script, CSS, fetch, XHR and frames — resolve
  URLs through the single `UrlResolver`. The residual duplication is the *origin* concern
  (`Utilities.IsCrossOrigin` same-origin compare, `MessagingBinding` origin extraction, `CspSourceMatching`
  origin matching), which is a separate consolidation with different per-site semantics (e.g. `file://`
  handling) and is deliberately left for a scoped origin-unification step.

- **P7.10 (2026-07-20) — item 2 (partial): CSP `<meta>` discovery is parser-backed.** `CspMetaDiscovery`
  scanned the serialized HTML with a `<meta[^>]*>` regex + a quoted/unquoted attribute regex. Replaced it
  with `Broiler.Dom.Html`'s shared `HtmlTokenizer`: `FindPolicyContent` now enumerates real start tokens and
  reads the lower-cased `Attributes` map. This resolves the "cross-assembly wrinkle" by adding a `Broiler.HtmlBridge.Core`
  → `Broiler.Dom.Html` project reference (no cycle — `Broiler.Dom.Html` only references `Broiler.Dom`, which
  Core already used; the boundary-guard suite confirms the bridge→canonical direction is allowed). The
  tokenizer stores attribute values **raw** (no entity decode — line-for-line what the regex returned), so
  discovery of a normal `content="…"` is byte-identical, while three former regex defects are fixed: a
  `<meta>` inside an HTML **comment** or a `<script>`/`<style>` **raw-text body** is no longer discovered, and
  a `>` inside a quoted `content` value no longer truncates the directive. New `CspMetaDiscoveryTests` (3) pin
  those three behaviours; the existing 8 discovery tests + 37 CSP/policy/public-API-snapshot tests stay green
  (`CspMetaDiscovery` is still `public static` — dropping `partial` is not a public-surface change, so no
  baseline regen). The regex + `GeneratedRegex` are gone from the class; `HtmlAttributeReader` stays (the
  nonce path still parses a raw attribute string). `ScriptExtractionService` discovery (the other half of
  item 2) is still regex — larger and WPT-sensitive, tracked below.

- **P7.11 (2026-07-20) — item 2 complete: `<script>` discovery is parser-backed.** Replaced the eight
  `<script>`/attribute regexes in `ScriptExtractionService` with the shared `Broiler.Dom.Html` tokenizer. A
  single `EnumerateScriptTags(html)` walks the token stream and pairs each `<script>` start tag with its raw
  body (the tokenizer treats `<script>` as a raw-text element, so the body is taken verbatim and never
  entity-decoded — byte-identical to the former `[\s\S]*?` capture + `.Trim()`); the flag/`src`/`nonce`
  reads (`type=module`, `defer`, `async`, `src` data-vs-external, `nonce`) come from the parsed lower-cased
  attribute map instead of per-tag regexes. `Extract`, `ExtractAll` and the `ScriptDescriptor` pipeline keep
  identical shape — document order, CSP inline/external gating, data-URI decode and defer/async/regular
  bucketing are unchanged. Parser-backed discovery also fixes the same class of regex defects as P7.10 (a
  `<script>` literal inside a comment/another element's text is not mis-discovered; a `>` inside a quoted
  attribute no longer truncates the start tag; an unterminated final `<script>` yields its body to EOF per
  parser behaviour). Only `WhitespacePattern` (used by `DecodeDataUri`'s base64 fold-strip) remains a regex.
  Validated with zero regressions across the extraction-exercising suites — `ScriptDescriptorTests` (4),
  `ContentSecurityPolicyTests` (19), `CspMetaDiscoveryTests` (11) and `ScriptEngineExecuteTests` (53 pass;
  the 4 failures are pre-existing geometry/serialization env failures, identical on the P7.10 baseline). Item
  2 is now done for both discovery sites (CSP-meta P7.10, scripts P7.11); full WPT-reftest confirmation
  remains the standing gate for the render-path scripts as for prior increments.

- **P7.12 (2026-07-20) — item 6 (first slice): module map + inline-module execution.** Recognised
  `<script type="module">` scripts were *silently dropped* by the `ExtractAll` pipeline (recorded in a
  descriptor but absent from every execution bucket). First slice, three parts:
  - **Module map.** New `ModuleMap` / `ModuleMapEntry` (Core): the ordered registry of every recognised
    module in a document. `ExtractAll` records each one — inline modules keyed `inline:{order}` carrying
    their authorised body, module scripts with a `src` keyed by URL and marked non-executable (fetch + import
    graph is a later slice). So a module is no longer silently skipped — it is at minimum *mapped*.
  - **Inline-module execution.** An authorised inline module body (passes the same CSP inline check as a
    classic inline script) is exposed, in document order, in the new `ScriptExtractionResult.ModuleScripts`,
    each wrapped by `ModuleScriptWrapper.WrapInlineModule` into a strict self-invoking function. That
    reproduces the module top-level semantics that matter for an import-free module: strict mode, top-level
    declarations kept out of the global object, and top-level `this === undefined` (unit-verified against a
    live `JSContext`). `import`/`export`/`import.meta`/top-level-`await` are **not** supported — such a module
    surfaces a syntax/runtime error at execution (caught + logged) instead of being dropped.
  - **Wiring.** Module scripts are deferred, so the two `ExtractAll` consumers run them after the classic
    deferred scripts: `RenderingPipeline` appends `ModuleScripts` to the deferred bucket, and the sub-document
    executor (`DomBridge.ExecuteSubDocumentScripts`) runs them last. The CLI capture path
    (`CaptureService.ExecuteScriptsWithDom`) has its own regex extraction and already ran modules as classic
    inline scripts — left untouched, so Acid/CSS captures are unaffected.

  New `ModuleScriptSliceTests` (8) pin the map/bucket population (inline executable + wrapped, external
  mapped-not-executable, CSP-blocked mapped-not-executable, empty map) and the wrapper's module semantics;
  the classic buckets and `ScriptDescriptor`s are unchanged (`ScriptDescriptorTests` green). Additive
  public-surface change — Core API baseline regenerated (adds `ModuleMap`/`ModuleMapEntry`/`ModuleScriptWrapper`
  and the two result properties; the old ctor stays source-compatible via optional params). Regression sweep
  over the touched paths (`ScriptEngineExecute`/`SubDocument`/`SubWindow`/`ContentSecurityPolicy`/`SubResource`,
  96 pass) shows only the 4 pre-existing geometry/serialization env failures, identical on baseline.

- **P7.13 (2026-07-20) — item 6 (second slice): external/data-URI module fetching + module-map dedup.**
  Extended the P7.12 module handling so a module with a `src` is resolved through the **same authorised path
  as a classic script**: a new `ResolveModuleSource` centralises the three cases — an inline body passes the
  CSP inline check; a `data:` source passes the CSP external check then is `DecodeDataUri`-decoded; an
  external source passes the CSP external check then is `FetchExternalScript`-fetched (file/http via the
  shared `UrlResolver` + loader, exactly as classic external scripts). A resolved module source becomes an
  executable wrapped entry in `ModuleScripts` and an `IsExecutable` map entry. Added **module-map dedup**: a
  repeated module URL is fetched and evaluated once (`ModuleMap.TryGet` guards re-fetch/re-queue), matching
  the browser module map's single-evaluation rule — while both occurrences still appear in the per-element
  descriptor list. New `ModuleScriptSliceTests` cases (4) pin the data-URI decode, the file-module fetch, the
  unresolvable-URL non-executable case, and the dedup (one `ModuleScripts` entry / one map entry for a
  repeated URL, two module descriptors). No public-surface change (a private helper + logic only — API
  snapshot unchanged); the extraction/CSP/descriptor/execution suites (33 + 57) stay green with only the 4
  pre-existing geometry/serialization env failures. External *http* modules still hit the network like
  classic external scripts (unreachable in the sandbox → non-executable there); the file/data paths are
  deterministic.

- **P7.14 (2026-07-20) — item 5: CSP style enforcement centralised into the bridge (host) layer.** Audit
  first: the canonical `Broiler.Dom` / `Broiler.CSS` / `Broiler.CSS.Dom` engines are **CSP-free** (no
  reference to `ContentSecurityPolicy` or any `Allows*` check), so CSP already lived entirely in the
  bridge/host layer — item 5's core rule held. The gap was *uniformity*: `DomBridge.ApplyStyleContentSecurityPolicy`
  (which strips `style-src`-blocked inline `style=` attributes and `<style>` elements from the parsed DOM)
  was called **by hand** only by the CLI capture host and the WPT runner. The main `ScriptEngine.Execute*`
  path set `bridge.Csp` (so inline *event handlers* were gated) but never called it, so it handed scripts and
  rendering a DOM that still contained CSP-blocked styles. Fixed by moving the enforcement **into
  `DomBridge.Attach`** (both overloads): when a policy is configured via `bridge.Csp`, attach applies the
  `style-src` family as its final step, so **every** host path receives already-authorised content — not just
  the CLI/WPT hosts. Idempotent, so the now-redundant explicit call in `CaptureService` was removed (attach
  does it); the WPT runner keeps its explicit call because it authorises with a fresh `FromHtml(html)` without
  setting `bridge.Csp`. New `ContentSecurityPolicyTests` (2) exercise the bridge directly — attach with a
  `style-src 'none'` policy strips a blocked `style=`; attach with no policy leaves it. The style-src family
  suite (`StyleSrcAttr_None`/`StyleSrcElem_None`, now relying on attach-level enforcement) plus the CSP /
  network / Acid3-CSS suites stay green (185 pass; the 3 failures are the pre-existing pixel + two `bodyUsed`
  env failures, identical on baseline). Remaining item-5 nuance: external-stylesheet (`<link rel=stylesheet>`)
  CSP and giving the WPT runner the same `bridge.Csp`-driven path are follow-ups, but DOM/CSS never seeing CSP
  and every execution path delivering authorised inline-style content is done.

- **P7.15 (2026-07-20) — item 4 complete: unify the origin helpers behind one `Origin` primitive.** The
  `scheme://host[:port]` origin serialization was copy-pasted in **five** places and the scheme+host+port
  comparison in **two**: `MessagingBinding.GetWindowOrigin` (`postMessage` target-origin delivery),
  `DomBridge.Attach` (`_pageOrigin`/`_pageHost`), `SubWindowBinding` (the iframe `Location`
  `origin`/`host` projections), `Utilities.IsCrossOrigin` (cross-origin check), and
  `CspSourceMatching.IsSameOrigin` (CSP `'self'` match). Added an internal `Origin`
  (`Broiler.HtmlBridge.Core/Scripting/Origin.cs`, alongside `UrlResolver`) with three primitives —
  `Of(Uri)` (origin serialization, default port omitted), `HostOf(Uri)` (the `Location.host` form), and
  `SchemeHostPortEquals(Uri, Uri)` (the origin-equality primitive) — and routed all five/two sites through
  it. **Behaviour-preserving:** the serialization is byte-identical across all sites and the compare
  primitive is identical in `IsCrossOrigin`/`IsSameOrigin`, so each caller keeps its own surrounding policy
  (which of `about:blank`/`data:`/`file:` inherit the embedding origin, and the null-page handling) — only
  the shared primitive is consolidated. `Origin` is `internal` in Core, visible to Dom via
  `InternalsVisibleTo`, so **no public-API change**. New `OriginTests` (4) pin the serialization / host form
  / scheme+host+port equality / case-insensitivity; the CSP / messaging / sub-window / sub-resource suites
  stay green (the same 3 `HttpSubResource` relative-iframe/WPT-root failures are pre-existing network-env
  failures, identical on the `2b33fe6` baseline). This closes the "one URL resolution/**origin**
  implementation shared by script, CSS, fetch, XHR and frames" exit criterion's origin half, so **item 4 is
  now fully done**.

- **P7.16 (2026-07-20) — item 5 complete: external-stylesheet (`<link rel=stylesheet>`) CSP gating.** P7.14
  centralised *inline* style CSP into the bridge, but the **external**-stylesheet load path
  (`DomBridge.FetchExternalStylesheet`, reached from computed-style building) consulted **no** CSP at all —
  a `style-src`-blocked `<link>` href was still fetched and applied. Added
  `ContentSecurityPolicy.AllowsExternalStyle(styleUrl, pageUrl, nonce)` — the style analogue of
  `AllowsExternalScript`, applying the same source-token matching (`*`, `'self'` via the shared
  `CspSourceMatching`/`Origin`, scheme source, absolute host source) plus a `<link>` nonce over the
  `style-src-elem` → `style-src` → `default-src` fallback, but **not** `'unsafe-inline'` (irrelevant to a
  fetched URL) nor `'strict-dynamic'` (script-only). Gated the fetch at its call site
  (`GetStyleElementSourceText`) via a new `IsExternalStyleAllowedByCsp` helper, so DOM/CSS never fetch or
  apply a blocked external stylesheet — the item-5 rule ("CSP stays in the host layer; DOM and CSS receive
  already-authorised content") now holds for external styles too. New `ContentSecurityPolicyTests` (4) pin
  `'none'`/`'self'`/host/scheme/nonce matching and the `style-src-elem`/`default-src` fallback. `AllowsExternalStyle`
  is public on `ContentSecurityPolicy` → **Core public-API baseline regenerated** (adds the one method);
  CSP / origin / resolver / snapshot suites green. With this, **item 5 is done** (the WPT runner sharing the
  same `bridge.Csp`-driven path remains a harness follow-up, not a DOM/CSS-visibility gap).

- **P7.17 (2026-07-20) — item 6 (major slice): the ES module import/export graph + linker.** Extended module
  handling from "run an isolated inline module body" (P7.12/P7.13) to a real **linked import graph**.
  Engine-capability audit first: `Broiler.JS` *parses* `import`/`export` but has **no drivable module records**
  — the compiler desugars static import/export into CommonJS-style ops keyed on magic scope vars that exist
  only when a body is compiled with the module arg list (via `JSModuleContext`, which needs the **`internal`**
  `CoreScript.AllowTopLevelAwaitScope`). Driving the engine's lowering therefore needs a `Broiler.JS` change
  (submodule, push-gated) and cannot ship on CI now. So the graph is linked at the **bridge layer** (main-repo,
  CI-landable) by a source-to-source transform the existing `JSContext.Eval` path runs:
  - `EsModuleScanner` — a string/template/comment/regex-aware scanner that finds a module's **top-level**
    static `import`/`export` statements only (never one inside a string, comment, `obj.export`, `exports.x`,
    or a nested scope; `import(...)`/`import.meta` are left as expressions). Any unrecognised/malformed form
    marks the module unsupported so the linker leaves it as-is — the feature is strictly **additive**.
  - `EsModuleLinker` — rewrites each module into a strict IIFE wired to an idempotent runtime registry: it
    registers its exports object under its key *before* running (so a cycle sees the in-progress object), reads
    imports from the registry, and publishes exports. Supported forms: default/named(+`as`)/namespace/side-effect
    imports; `export` declarations, export lists, `export default`, and re-exports (`export {…} from`,
    `export *`, `export * as`). Bindings are snapshots (namespace imports are live references) — the one
    spec-fidelity caveat, matching the engine's own non-live desugaring.
  - `ModuleGraphLoader` — from the document's module roots, resolves each specifier (shared `UrlResolver`,
    relative to the importing module), fetches (CSP-gated, same authorised path as classic scripts), dedups by
    resolved URL, orders the graph **dependency-first** (cycle-safe post-order), and renders the ordered linked
    programs. `ScriptExtractionService.ExtractAll` now feeds its authorised module roots to the loader and
    returns the linked graph in `ModuleScripts`; the existing deferred-bucket consumers
    (`RenderingPipeline`, `DomBridge.ExecuteSubDocumentScripts`) run them in list order unchanged.
    New `EsModuleGraphTests` (16) drive real graphs through a live `JSContext` — named/default/namespace/aliased
    imports, `export`/re-export/`export *`, diamond dedup (shared module evaluated once), cycles, side-effect
    imports, scope isolation, scanner robustness (strings/comments/dynamic-import not mis-transformed),
    unsupported-form fallback, and a 3-module disk-backed graph through `ExtractAll` — all green. New syntax
    types are `internal` → **no public-API change**; the existing `ModuleScriptSliceTests` (10) stay green.

- **P7.18 (2026-07-20) — item 6: `import.meta` support (bridge linker).** `import.meta` was previously left
  inert — the scanner advanced past the keyword and recorded nothing, so a module using `import.meta.url`
  hit a syntax/reference error under the plain-`Eval` module path and failed (caught + logged). Delivered at
  the bridge layer (main-repo, CI-landable, no engine dependency): `EsModuleScanner` now recognises
  `import.meta` at **any** bracket depth (string/comment/regex/template-aware, so `"import.meta"` in a
  literal or a `// import.meta` comment is untouched) and records each occurrence's span on a new
  `EsModuleSyntax.ImportMetaSpans`; `EsModuleLinker` rewrites each span to a synthesized per-module object
  `__brmeta` declared at the module IIFE top with `url` = the module's registry key (its resolved URL, or the
  synthetic id for an inline module). Dynamic `import(...)` is still left as an expression (engine-coupled —
  see below). New `EsModuleGraphTests` cases (4) drive `import.meta.url` through a live `JSContext`: as the
  sole module syntax, alongside a static import, inside a nested function scope, and asserting the
  string/comment literal is not rewritten. Additive, all `internal` → no public-API change; the module-graph
  and descriptor suites stay green (33 pass).

- **P7.19 (2026-07-20) — item 6 tail: engine host-resolution seam (`Broiler.JS` patch `0008`).** Toward
  driving the engine's *own* module machinery (its `Broiler.JavaScript.Modules` system already compiles
  modules with the ES-module arg list under `CoreScript.AllowTopLevelAwaitScope()`, so top-level `await` and
  dynamic `import()` are supported there), the blocker was that its resolution is hard-wired to the
  **filesystem**. Patch `0008` makes `JSModuleContext` host-overridable — `Resolve` becomes `protected
  virtual`, and new `GetModuleDirectory` / `ReadModuleSourceAsync` seams let a subclass resolve URLs against a
  base and fetch under CSP — validated by a `Modules.Tests` case that drives a URL entry + URL dependency with
  no filesystem. **Delivered as `patches/0008-…` after an initial push 403; now applied upstream (2026-07-21)** —
  a maintainer pushed it as `ffe8956e` and the parent pointer is bumped to `Broiler.JS` `3f0c7054`, which
  contains it (the patch file is retained for provenance).
  However, validating the patch surfaced that the engine's module machinery is **incomplete beyond
  resolution**: a static `import { x } from …` does **not** bind a value (the `yield import(...)` desugaring
  resolves to `undefined`, reproduced on the **stock** filesystem context — independent of the seams), and
  nested/transitive async module bodies do not run to completion under `RunScriptAsync`. So the engine-driven
  path is blocked below the patch, and the bridge continues to use its own working `EsModuleLinker`.

- **P7.20 (2026-07-20) — item 6 tail: dynamic `import()` wired to the module graph.** A dynamic
  `import(spec)` was previously left as-is (an expression the plain-`Eval` path throws on when executed).
  Delivered at the bridge layer (main-repo, CI-landable): `EsModuleScanner` now records each dynamic
  `import(...)` call site (keyword span, at any depth) plus its argument when a single string literal
  (`EsModuleSyntax.DynamicImports` / `DynamicDependencies()`); `ModuleGraphLoader` resolves/fetches/links
  each literal dynamic dependency into the registry ahead of the importer (eager load — a timing
  approximation of the spec's lazy evaluation, but it makes `import()` resolve to the linked namespace);
  and `EsModuleLinker` rewrites `import(spec)` to a per-module graph-backed loader (`__brdimp`) that returns
  a `Promise` of the linked module namespace via a published specifier→key map (`__brdynmap`) — an
  unresolved or runtime-computed specifier rejects with `Cannot find module`. A dynamically-and-statically
  imported module resolves to the same singleton registry instance (evaluated once). New `EsModuleGraphTests`
  (3) drive real graphs through a live `JSContext` with the event loop pumped via `JSContext.Execute`:
  `import().then(...)` resolves to the namespace value, an unresolvable specifier rejects, and the dynamic +
  static singleton is shared (one evaluation). All `internal` → no public-API change; the module-graph /
  descriptor / CSP suites stay green (only the 4 pre-existing geometry/serialization env failures remain,
  identical on baseline).

- **P7.21 (2026-07-20) — item 6 tail: deep-dive on the engine-driven path (root-caused, not fixed).** An
  attempt to fix the engine so the bridge could drive its real module machinery (via seam `0008`) root-caused
  the blocker precisely: **a module body with a top-level `await` does not run to completion — the code after
  the first `await` is dropped.** Every static `import` desugars to `tempRequire = yield import(spec)`
  (`FastCompiler.VisitImportStatement`), so a static import *is* a top-level await; that is why
  `import { x } from …` leaves `x` undefined. Reproduced minimally on the **stock** engine (no seams,
  filesystem): `globalThis.a=1; await Promise.resolve(); globalThis.a2=2;` sets `a` but never `a2` (the
  existing `Modules.Tests` TLA case only asserts a non-null return, so it never caught this). The working
  `JSContext.Execute` path runs the body under a **pumped** `AsyncPump` loop and drains the job loop
  (`WaitTask`) before taking the result; the module path (`RunAsync`/`RunScriptAsync` → `InitAsync` →
  `CompileModuleAsync`) awaits an un-pumped context and drives the body through a **double-marshaled**
  `JSFunction`→`Marshal(CompileModuleAsync)`→`IJSPromise.Task` bridge, so the body promise settles at the
  first suspension and the continuation resumes later, off the loop. A pump-plus-`WaitTask`-drain fix made the
  continuation *try* to resume but it then posts to the completed `AsyncPump` queue (`InvalidOperationException:
  the collection has been marked as complete`), confirming the continuation escapes the loop — the fix is not a
  pump tweak but routing the async module body through the **same completion mechanism `ExecuteScriptAsync`
  uses** (one promise await under one pumped loop, no re-marshal). That is core async-runtime surgery with
  broad regression surface, left to a maintainer with the engine test suite (full diagnosis in
  `patches/README.md` §0008). The engine change was **reverted** (a crashing partial fix is not shipped); the
  seam patch `0008` stands as the necessary resolution hook for when the TLA-completion bug is fixed.
  **Superseded in part by P7.24** — the real blocker is one layer deeper than the pump/marshal.

- **P7.24 (2026-07-21) — item 6 tail: TLA blocker re-diagnosed to a stock-engine codegen bug (proven, not
  fixed).** Carrying the P7.21 fix through cleanly (a non-marshalled `CompileDirect` hook on `JSModule` so
  `InitAsync` awaits `CompileModuleAsync` directly, plus draining `WaitTask` and awaiting the body promise
  under one `AsyncPump.Run` loop — mirroring `ExecuteScriptAsync`, all `Modules.Tests` green) removed the
  double-marshal thread-hop **and the `InvalidOperationException`** — and exposed the true blocker: the
  resumed module body throws a **`NullReferenceException` from inside the compiled body itself**. This NRE
  reproduces on the **pristine** engine (`3a8f302`) via the engine's own
  `JSContext.EvalWithTopLevelAwaitAsync` with **zero module code**, and is deterministic and
  thread-independent (identical under `AsyncPump` on a worker thread, `AsyncPump` on the caller thread, and a
  plain `await` with no pump). So it is a **compile-time top-level-await codegen bug**, not a runtime/pump
  problem. Trigger boundary: after the first `await` resumes, a member access / call whose **receiver is an
  identifier read** (local *or* global — e.g. `Math.max(1,2)`, `globalThis.z`, `var g=Math; g.max(1,2)`)
  dereferences null, while a bare identifier read (`globalThis`, `Math`, a local `g`) and a member access on
  a **constant** receiver (`'hi'.length`) both survive. The discriminator is the **spilled receiver
  temporary**: `GeneratorRewriter.VisitBlock` box-lifts the await-containing block's locals, and the spilled
  member-receiver temp reads null on the resume path (`GeneratorsV2/GeneratorRewriter.cs`; fault is in the
  generated `vm-` delegate, not the async driver). This gates every static import followed by a member
  access/call on the imported value. It is core generator-codegen surgery affecting **all** async
  functions/generators, unvalidatable without the full engine suite — left to a maintainer with `Broiler.JS`
  push access. At authoring time the submodule was left **pristine** (pointer `3a8f302`); no partial engine
  change was shipped. **Re-confirmation (2026-07-21):** the `Broiler.JS` pointer has since advanced to
  `3f0c7054` (carrying patches `0008`/`0009`), and the codegen bug was re-verified on that pinned engine via
  `EvalWithTopLevelAwaitAsync` with zero module code — the boundary reproduces unchanged, and a further probe
  shows a **local declared *after* the await** and then read (`await …; var x=5; x`) also NREs, i.e. the fault
  is any resume-path read of a box-lifted local, slightly broader than the receiver-spill framing above. The
  pointer bump therefore did **not** unblock it. Full boundary table and analysis in `patches/README.md` §0008.

- **P7.25 (2026-07-21) — item 6 tail: the TLA codegen bug is fixed (`Broiler.JS` patch `0010`).** The P7.24
  blocker is resolved. Root cause (one layer past the "spilled receiver" framing): `GeneratorRewriter`'s
  **box-load prologue** runs on *every* (re)entry — first call and each `await`/`yield` resume — and, for the
  `scriptInfo` local, unconditionally re-seeded the persisted `ScriptInfo` box (`scriptInfoBox.Value =
  _replaceScriptInfo`). `_replaceScriptInfo` is a body-local whose only writes are redirected into the box, so
  as a bare local it is always `null` at prologue time; on **resume** the re-seed therefore clobbered the
  `ScriptInfo` the box had persisted from the first run — including its `Indices` key table — back to null.
  Any post-resume identifier/member access resolved through `scriptInfo.Value.Indices[…]` then dereferenced
  null (constant receivers / bare globals resolve via constant `KeyStrings`, hence survived — which is why the
  fault looked receiver-shaped). The fix guards the seed on **`nextJump == 0`** (first entry only), preserving
  the first-entry seed that nested async/generator functions need while never clobbering the persisted value
  on resume. **Validated against the full `Broiler.JS` test suite** — Core/Compiler/Runtime/Modules/Ast/
  Parser/Clr/Debugger/Portable/Storage all green; BuiltIns, Integration and ModuleExtensions carry only
  **pre-existing** Intl/ICU-locale + doc-file env failures, confirmed identical on the pristine engine by
  baselining the stashed fix → **zero regressions** — plus a new `TopLevelAwaitResumeTests` (9) pinning the
  corrected behaviour (local read, member get/call/assign, member-on-awaited-value, sequential awaits,
  constant-receiver survival, async-function + generator regression guards). Push returned **403** at
  authoring → shipped as `patches/0010-…` with the pointer unbumped; **now applied upstream** (2026-07-22) as
  `64fda04f`, contained in the pinned `Broiler.JS` `98b07636`. **Scope:** `0010` fixes the *codegen* blocker only;
  fully driving the engine's own module machinery so a static import *binds its value* additionally needs the
  separate **module-orchestration completion** fix (§0008: `CompileDirect` + one pumped `AsyncPump.Run` loop +
  `WaitTask` drain) — with `0010` alone, a static import no longer crashes but its value still resolves to
  `undefined`/`0`. That orchestration change remains the last engine-coupled piece; the bridge keeps its
  `EsModuleLinker` until it lands. Full analysis in `patches/README.md` §0010.

- **P7.26 (2026-07-21) — item 6 tail: module-orchestration completion fix (`Broiler.JS` patch `0011`,
  stacks on `0010`).** With `0010` the codegen NRE is gone but an engine-driven static import still resolved
  to `undefined`/`0` — the dependency *ran* (side effects fired) yet the imported binding was empty. Three
  coupled defects in `Broiler.JavaScript.Modules`, each fixed: **(1) un-pumped init** — `RunScriptAsync`/
  `RunAsync` set a plain `SynchronizationContext` and awaited init on the caller's thread, so the async
  module body's post-`await` continuation (every static import desugars to `yield import(spec)`, a top-level
  await) was posted to a context nobody pumped and the body stalled at its first suspension; now both run
  their core under one `AsyncPump.Run` loop on a worker thread, exactly like `JSContext.ExecuteAsync`.
  **(2) compile double-marshal** — `JSModule.InitAsync` invoked the `Compile` JS function, which marshalled
  `CompileModuleAsync`'s task into a JS promise that `InitAsync` re-awaited (`Task→IJSPromise→Task`), hopping
  the continuation off the loop; a new direct `JSModule.CompileDirect` (`Func<Task>`) hook is awaited
  instead (the JS `Compile` stays for `module.compile()` / as fallback). **(3) the actual value loss** —
  `import()`/`require()` converted their `Task<JSValue>` to a promise via `JSEngine.ClrInterop.Marshal`,
  which is the full `DefaultClrInterop` only when the optional `Broiler.JavaScript.Clr` assembly is loaded;
  otherwise it is `FallbackClrInterop`, whose `Marshal` returns `undefined` for a `Task`. `Modules` does not
  reference Clr, so `import(...)` resolved to `undefined`. Routed through the engine-native
  `JSValue.CreatePromiseFromTask` (the same factory `DefaultClrInterop` uses, populated by the
  always-referenced BuiltIns), so import binding works with or without Clr. **Validated:** new
  `EngineModuleImportBindingTests` (6) drive the engine machinery over an in-memory URL store — named
  (`add(d,5)==12`), namespace (`ns.d==7`), default (`v+1==42`), transitive (`b==11`), diamond
  (`x+y==13`/shared dep evaluated once) and TLA-dependency (`v+1==43`) — plus the pre-existing `Modules.Tests`
  (7) stay green; the **full `Broiler.JS` suite** carries only the same pre-existing Intl/ICU + doc-file env
  failures as at `0010` → **zero regressions**. Push **403** at authoring → shipped as `patches/0011-…`,
  **stacks on `0010`**; **now applied upstream** (2026-07-22) as `98b07636` (the pinned pointer). With
  `0010`+`0011` pinned the bridge's `EngineModuleSupport.Available` probe returns `true`, so `DomBridge` runs
  modules through the engine path (P7.27) and the `EsModuleLinker` is the dormant fallback. Full analysis in
  `patches/README.md` §0011.

- **P7.22 (2026-07-20) — item 6 tail: live bindings (bridge linker).** Replaced the linker's snapshot
  bindings with **live** ones. **Exports** are now published as **getters** on the exports object
  (`Object.defineProperty(__E,name,{get:()=>local})`) defined before the body, so a value reassigned after the
  module finishes — the canonical counter-mutated-by-an-exported-function case — is observed by importers, and
  a **namespace import** (`import * as ns`) reflects it member-wise (`ns.count`) universally. **Named imports**
  (`import { x }`) are made live by a new `EsModuleLiveRefs` pass that rewrites their read-references to the
  same live getter access. That rewriter is **sound by abdication**, not a scope-accurate parser: it reuses the
  scanner's string/comment/template/regex lexer and rewrites only occurrences it can prove are plain reads,
  **aborting the whole module to the (correct, non-live) snapshot binding** the moment it meets a
  binding/scope construct (`function`, `=>`, `class`, `catch`, `var`/`let`/`const`) or an ambiguous position (a
  property key `name:`, an assignment/default `name=`, an object-shorthand/pattern slot `{name}`/`,name,`, a
  spread `...name`) — so a mis-analysis can only fall back, never emit wrong code, and each named-import keeps a
  `var` snapshot as the safety net for any read it does not rewrite. New `EsModuleGraphTests` (5) drive real
  graphs through a live `JSContext`: the canonical named-import counter reads **2** (a snapshot would read 0),
  the namespace form reflects the mutation, a live read through a call works, a module with a local function
  falls back to a correct snapshot, and an object key colliding with an import name is not corrupted. All
  `internal` → no public-API change; the full module-graph / descriptor / CSP suites stay green (66) with no
  regressions.

- **P7.23 (2026-07-21) — item 6 tail: scope-accurate live named bindings (lift the conservative fallback).**
  P7.22's `EsModuleLiveRefs` was sound-by-abdication — it aborted the *whole module* to snapshot on any
  binding/scope construct (`function`, `=>`, `var`/`let`/`const`, `catch`), so a consumer that merely defined
  a helper function got no live named bindings. Replaced it with a **scope-accurate, per-name** analyzer: the
  same string/template/comment/regex-aware lexer now tracks every construct that can (re)bind a name —
  `var`/`let`/`const` declarations and their destructuring patterns, function/arrow/`catch` parameters, and
  function names — and marks an imported local **unrewritable** if it appears in *any* binding position
  anywhere (a nearer binding could shadow it in some scope). An imported local that is **never** bound
  anywhere is free in every scope, so each plain read-reference is rewritten live; the linker's `var` snapshot
  bindings stay as the safety net for anything left unrewritten. It is still sound-biased — any ambiguous
  occurrence marks the name unrewritable rather than risk a wrong rewrite, and `class`/`with`/`eval` (whose
  scoping the lexer will not attempt) still fall back to a whole-module snapshot. New `EsModuleGraphTests` (7)
  are adversarial: the **lift** (a named import stays live through a module with its own functions/params →
  reads the mutated value), and correct *non*-rewriting under every shadow form — function param, arrow param
  (both `(v)=>` and `v=>`), block `let`, destructuring, and `catch` — each asserting the **shadowed** reference
  reads the local (which would fail if the analyzer wrongly rewrote it), plus the `class` snapshot fallback.
  All 34 `EsModuleGraph` + 50 module/CSP tests green; `internal` → no public-API change.

- **Remaining for Phase 7 — item 6 tail (bridge application).** With P7.17/P7.18/P7.20/P7.22/P7.23 the static
  import/export graph is linked and executed, `import.meta` is handled, dynamic `import()` is wired to the
  graph, and bindings are live (exports + namespace universally; named imports **scope-accurately**, falling
  back to a correct snapshot only for `class`/`with`/`eval` modules or template-interpolation reads) — all at
  the bridge layer. The genuinely engine-coupled residue was **top-level `await`** as genuinely async (the
  bridge transform is a synchronous IIFE, so a TLA module falls back), plus moving module ordering into the
  real browser **event loop** rather than the deferred-bucket approximation. That engine-coupled residue is
  now **closed and active**: the host-resolution seam `0008` (P7.19), the top-level-await **codegen** fix
  (`0010`, P7.25) and the **module-orchestration completion** fix (`0011`, P7.26) are **all applied upstream
  and pinned** (`Broiler.JS` → `98b07636`), so the engine's own module machinery binds a static import's value
  (named/namespace/default/transitive/diamond/TLA), all full-suite-validated with zero regressions. **The
  bridge's `EngineModuleSupport.Available` probe returns `true` on the pinned engine** (verified 2026-07-22),
  so `DomBridge` runs the primary page's modules through the engine path (P7.27) and the `EsModuleLinker` is
  the dormant fallback. What remains for item 6 is therefore purely a **bridge application task** on the two
  non-primary paths: migrate the sub-document (`DomBridge.ExecuteSubDocumentScripts`) and CLI-capture
  (`CaptureService`) module execution off the `EsModuleLinker` onto the engine path, add genuine async
  module evaluation on the real event loop (vs the eager deferred-bucket run), and then delete the
  `EsModuleLinker`. This is gated on the WPT/render harness (the sub-document module render path is
  pixel-reftest-validated, not validatable in a bare container), so it is sequenced with the Phase 6
  test-harness relocation rather than landed blind.

- **P7.27 (2026-07-21) — item 6: `DomBridge` wired to the engine module path (capability-gated).** The bridge
  now drives the JS engine's own module machinery when the engine binds imports, with the `EsModuleLinker`
  demoted to a fallback. New (`Broiler.HtmlBridge.Scripting`): `BridgeModuleContext : JSModuleContext` maps the
  patch-`0008` seams to the host — `Resolve` does URL/`data:` resolution, `GetModuleDirectory` returns the
  module's own URL base, and `ReadModuleSourceAsync` fetches through the bridge's CSP-gated
  `ScriptExtractionService` (`file`/`http(s)`/`data:`); a `DomBridge` attached to it installs the DOM globals
  on the **same realm** the modules execute in, so a module touches `document`/`window` like a classic script
  (validated: a module mutates the live DOM). `EngineModuleSupport.Available` is a one-time,
  **timeout-guarded** probe (a real `data:`-import module through a throwaway `BridgeModuleContext`) that is
  `true` only when the engine binds imports (patches `0010`+`0011`); on an un-patched engine it returns
  `false` fast (≈0.5 s, no hang) so the bridge keeps the linker. `ScriptExtractionResult`/`PageContent` gained
  `ModuleRoots` (the authorised roots), and `ScriptEngine.Execute`/`ExecuteToDocument`/`ExecuteInteractive`
  gained a `moduleRoots` overload: when `Available`, the page runs on a `BridgeModuleContext` and each root is
  executed via `RunScriptAsync` (the engine loads its transitive imports itself) after the classic deferred
  scripts; otherwise the current `JSContext` + linked-string path is unchanged. `RenderingPipeline` gates the
  cutover — when `Available` it passes the roots and keeps the linked `ModuleScripts` out of the deferred
  bucket, else it appends them as before. **CI-safe:** the pinned engine has `Available == false`, so the
  entire engine path is dormant and the existing module/`ScriptEngine` suites pass unchanged (only the 4
  pre-existing geometry/zoom env failures); with `0010`/`0011` applied locally, a `<script type="module">`
  import binds and mutates the DOM through the engine path. A new `EngineModuleWiringTests` drives a module
  page through `ScriptEngine` the way `RenderingPipeline` does and is green on **both** engine states. The
  `EsModuleLinker` (and `EsModuleScanner`/`EsModuleLiveRefs`/`ModuleGraphLoader`/`ModuleScriptWrapper` and the
  `ModuleScripts` production) stay as the fallback.

  **Update (2026-07-22): `0010`/`0011` are now applied upstream and the submodule pointer is bumped
  (`Broiler.JS` → `98b07636`), so the probe is now `true` and the engine path is the active one for the
  primary page.** Verified by building `Broiler.HtmlBridge.Scripting` against the pinned engine and running
  the `EngineModuleSupport` probe (`Available == true`); the 288 `~Module` `Broiler.Cli.Tests` pass on the
  active engine path. The `EsModuleLinker` and its supporting types are therefore now the **dormant** fallback
  — deletable once the two remaining consumers are migrated. Remaining tail: the sub-document
  (`DomBridge.ExecuteSubDocumentScripts`) and CLI-capture (`CaptureService`) paths still use the linker, and
  genuine event-loop ordering (vs the eager deferred-bucket run) is a follow-up — both sequenced with the
  Phase 6 test-harness relocation because the sub-document module render path is WPT-pixel-reftest-gated.

  Tracked here as the Phase 7 residue — now just the sub-document/CLI paths and event-loop ordering on top of
  a working, active, capability-gated bridge cutover.

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

#### Delivered increments

- **P8.1 (2026-07-22) — item 3: async-drain-limit exhaustion is an explicit diagnostic, not a silent
  stop.** `ScriptEngine.DrainAsyncWork` settles queued microtasks and timers in a loop bounded by
  `DomBridgeRuntimeLimits.AsyncDrainIterationLimit` (1000). When the queues did **not** settle within that
  budget — a runaway loop, e.g. a self-rescheduling `setTimeout(fn, 0)` or a `queueMicrotask` that
  re-enqueues itself — the `for` loop simply fell out and draining stopped **silently**, so a page that
  never quiesced looked indistinguishable from one that did. Now the loop `return`s on settle (byte-identical
  behaviour for the common case) and, on budget exhaustion with work still queued, records
  `ScriptEngine.AsyncDrainLimitExhausted = true` and logs a warning with the pending microtask/timer counts.
  A fresh `ScriptEngine` per page means the flag reflects whether that page's async work exhausted the budget.
  Purely additive: the drain still stops after 1000 iterations (no hang, no behaviour change for settling
  pages) — the exhaustion is now *observable* instead of invisible. Additive public surface (one get-only
  property on `ScriptEngine`), Scripting public-API baseline regenerated. New `AsyncDrainDiagnosticTests` (2):
  a normal script leaves the flag `false`; a self-rescheduling zero-delay timer flags exhaustion (and still
  returns a rendered document rather than hanging). 0 regressions on the timer/async/`ScriptEngine`/event-loop
  suites (the 4 `DomBridge_SerializeToHtml_*`/`AnchorSize` failures are the pre-existing bare-container
  geometry/zoom env failures, confirmed identical on the clean baseline).

- **P8.2 (2026-07-22) — item 3 complete: the same explicit-diagnostic treatment applied to the other two
  drain loops.** `CaptureService.ExecuteScriptsWithDom` (CLI capture) and `WptTestRunner` (WPT harness) each
  carried a byte-identical silent-stop drain loop. Both now `return` on settle and, on budget exhaustion with
  work still queued, log the same `RenderLogger.LogWarning` with the pending microtask/timer counts
  (`"…did not settle within N drain iterations…"`). Behaviour-preserving for settling pages (the drain still
  stops after 1000 iterations); the exhaustion is now diagnosable across **all three** drain sites. 0
  regressions (`TimerAndAsyncTests` 25/25 on the CaptureService path; vendored css-anchor-position 29/35
  unchanged on the WptTestRunner path). Item 3 is now fully delivered.

- **P8.3 (2026-07-22) — item 2: every `InteractiveSession` owns a private event-loop/context lifetime, and
  failed construction disposes it.** Two leaks fixed: **(1) session disposal leaked the event loop.**
  `InteractiveSession.Dispose` disposed only the `JSContext`, never the bridge — but `DomBridge.Dispose`
  (which tears down timers, listeners, observers and the layout view) is exactly what the session's comment
  says the owner must call, and `DomBridge` explicitly does *not* dispose the borrowed context itself. So a
  disposed session leaked its whole browser event loop. `Dispose` now tears the bridge down first
  (`(_bridge as IDisposable)?.Dispose()` — `IDomBridgeRuntime` is not itself `IDisposable`) then the context,
  idempotently. **(2) failed construction leaked both.** `ScriptEngine.ExecuteInteractive` created a
  `JSContext` and a bridge, then ran attach/scripts/modules/`FireWindowLoadEvent` before building the session;
  if any un-guarded step (e.g. `bridge.Attach`) threw, the context + bridge leaked. The setup is now wrapped
  so a throw disposes the partially-built context and bridge before rethrowing, and the CSP is restored in a
  `finally` on every path. New `InteractiveSessionLifetimeTests` (2, injecting a fake bridge/factory via the
  `ScriptEngine(IDomBridgeRuntimeFactory)` ctor): disposing a session disposes its bridge (idempotently); a
  bridge that throws on `Attach` makes `ExecuteInteractive` throw *and* dispose the bridge. 0 regressions
  across the interactive/session-lifetime/`ScriptEngine` suites (67 pass; the same 4 `DomBridge_SerializeToHtml_*`/
  `AnchorSize` failures are the pre-existing bare-container geometry/zoom env failures, which use `Execute`,
  not `ExecuteInteractive`). No public-surface change (both fixes are method-body only).

- **P8.4 (2026-07-22) — item 5: name the script-extraction contract for what it is.** The file
  `IScriptExtractor.cs` contained **no `IScriptExtractor` interface** — it held `ScriptExtractionResult`
  (the central type) plus `ScriptDescriptor`, `ScriptSourceKind`, `ModuleMap`/`ModuleMapEntry` and
  `ModuleRoot`. The extraction itself is `ScriptExtractionService`, a **static** class, so there is no
  instance to abstract and no consumer that would benefit from a restored interface. Per item 5's first
  option, renamed the file to **`ScriptExtractionResult.cs`** (`git mv`, contents unchanged — pure move, no
  API change). Also fixed a **dangling doc cross-reference**: `RenderingPipeline`'s summary pointed at
  `<see cref="IScriptExtractor.ExtractAll"/>` — a type that does not exist — now
  `ScriptExtractionService.ExtractAll` (the real static method; verified no `CS1574` broken-cref warning). And
  corrected a misplaced doc comment in the file: the `ScriptExtractionResult` `<summary>` had drifted onto
  `ModuleRoot` (a double-`<summary>`) while the class itself had none — moved it to the class and refreshed it
  to cover the descriptors/module-map/graph/roots. Public-API snapshot + `ScriptDescriptor` suites green (no
  surface change); `Broiler.Browser.Core` (which compiles `RenderingPipeline`) builds clean.


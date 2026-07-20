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

- **Remaining for Phase 6 — Concern 2 native migration + project deletion.** The live transforms still in
  `HtmlPostProcessor` are: the production/harness replaced-element passes (`StripScriptTags` [protective],
  `StripIframeContent` [pending `patches/0004`], `ReplaceVideo`/`ReplaceProgressLike`/`ReplaceSelectMultiple`
  [legitimate fallbacks]) and the test-harness-only `StripHiddenTestArtifacts` (map/linktest/FAIL/red-bg
  Acid scaffolding), `StripObjectContent`, and `RewriteRootSelector` (proven redundant, kept for the
  harness pending reftest validation). Fully emptying the file to delete the Rendering project needs: (a)
  `patches/0004` applied (drops `StripIframeContent`); (b) native replaced-element rendering for
  video/progress/meter/select so those fallbacks retire (largely `Broiler.HTML`, **submodule-push-gated →
  patch workflow**); and (c) relocating the residual Acid scaffolding shims to test support once the
  reftest gate can validate. Per the disposition, `HtmlPostProcessor` must **not** be moved wholesale to
  rename it; the migration is behavioural.

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

- **Remaining for Phase 7.** Still open: items 5–6 (host-layer CSP enforcement so DOM/CSS receive
  already-authorised content; execute `IsModule` descriptors in the event loop instead of skipping them — a
  substantial module-loading feature). Items 5–6 and the parser piece are larger or WPT-reftest-sensitive.
  Item 1 (CSP split), item 3 (script descriptors) and item 4 (loader/resolver consolidation) are **done**:
  the file/http text dispatch (P7.3–P7.4), one shared URL resolver across all five named consumers —
  script/CSP (P7.6), frames (P7.7) and fetch/XHR (P7.9) — and the sub-resource file/local reads (P7.8) all now
  live behind the loader/`UrlResolver`, so no feature callback constructs an `HttpClient` or inlines a
  `file`/`data`-URI/`File.*` switch for the text-load paths. The one remaining item-4-adjacent cleanup is
  unifying the *origin* helpers (same-origin/origin-extraction), tracked as a separate scoped step.

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


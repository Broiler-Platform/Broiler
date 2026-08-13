# Changelog

All notable changes to the Broiler component packages are documented here. The
format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
packages use [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Packages
are versioned in lockstep during the preview.

## [Unreleased]

### Added

- `Broiler.Layout` — CSS Images 3 §5.5 `object-fit` and §5.6 `object-position`,
  which nothing read: every replaced element was stretched to its content box, so
  `object-fit-contain-svg-001i`, `-fill-svg-001i` and `-none-svg-001i` rendered to
  byte-identical PNGs. `IR.ObjectFitPlacement` resolves the concrete object size and
  where it sits, clipping to the content box when it overflows, and
  `IR.CssPositionValue` reads the whole `<position>` grammar — including the three-
  and four-component edge-offset forms (`top 25% left 25%`), which paint dropped
  silently and which `background-position` and gradient layers now share. `css/css-images`
  goes 234 → 262 of 460 reftests with no losses; the 42 `object-fit-*i` tests go 21 → 42.
  `ImageIntrinsics` reports the intrinsic aspect ratio and whether its size is real
  alongside them, since an SVG with only a `viewBox` has a ratio and no size.
- `Broiler.Wpt` — a WPT `-print` reftest now renders the paint its own `@page`
  rule puts on the sheet (CSS Paged Media 3 §7): the page background over the
  whole page box, margins included, and the page's border and padding on the box
  the margins leave, with the flow inset by them. Independent of the
  `BROILER_WPT_PAGED_PRINT` lever, because a page paints whether or not the flow
  is paginated — `css-page/page-background-image-print` states outright that its
  background should print and not show on screen. A page whose root element
  generates no box paints nothing at all, and `visibility` applies in the page
  context. `css/css-page` goes 133 → 138 of 224 reftests, nothing lost; documents
  that declare no page paint render byte-identically to before. (137 of those came
  without the one-line `Broiler.HTML` call site below; that is upstream and pinned
  now, so `page-box-002-print` passes and the 138th is in.)
- `Broiler.Layout` — CSS 2.1 §11.1.2 `clip`, the legacy rectangular clip on an
  absolutely positioned element. Resolved into the `clip-path: inset()` that names
  the same operation (`IR.ClipRect`, in `ComputedStyleBuilder`, where the used
  border box is known), so nothing downstream needs a second clip; a real
  `clip-path` supersedes it, per CSS Masking 1 §7. Together with the `Broiler.HTML`
  change that stops an empty `inset()` being dropped as if it were no clip
  (upstream and pinned), `css/CSS2/visufx` goes 6 → 50 of 51 reftests and
  `css-masking/clip` gains two, none lost.
- `Broiler.Wpt` — inline scripts in an XHTML test are unwrapped from their XML
  CDATA section before execution. `<![CDATA[` is a syntax error, so every script
  in such a document was lost — including the functions an `onload` attribute goes
  on to call, which left the test rendering its pre-script state.
  `css/CSS2` goes 4863 → 4908 of 6216 reftests, nothing lost.
- `Broiler.Layout` — `Engine.CanvasBackdrop`, the colour a translucent canvas
  background (CSS 2.1 §14.2) is composited against when the surface underneath it
  already carries paint. Thread-static and null by default, so a render that does
  not set it is unchanged. Read by a one-line `Broiler.HTML` change, upstream and
  pinned (`1bf117a`).

### Changed

- `Broiler.HtmlBridge` — a script names itself in the stack traces of the errors it
  raises. `JSContext.Eval` takes the location it reports in stack frames and every
  host passed none, so every frame of every script read `vm.js`: an exception at
  `vm.js:3,159` could be any of a dozen scripts on a real page, one of which is a
  megabyte of minified vendor code, and nothing in the trace narrowed it down. The
  labels the hosts already used for their profiling entries and error messages —
  `inline-{n}`, `deferred-{n}`, `module-{key}`, per bucket in document order — are now
  the evaluation's location too, so the trace, the profiling timeline and the logged
  message all name the same thing. `CaptureService` reports which script failed at all
  now, where it used to log a bare "Script execution error". Because the location is
  part of the code-cache key, `ScriptCompileAhead` gained a per-source overload
  (`ScriptCompileUnit`): compiling every source under one location while evaluating
  under per-script ones would have filed each entry where the evaluation does not look
  and silently lost the compile/evaluate overlap.

- `Broiler.Layout` / `Broiler.HTML.Image` — a page's display list is now replayed
  into disjoint horizontal strips of the target surface at once, and the managed
  rasterizer no longer walks pixels its clip is certain to reject. The raster
  stage is 1.76-3.49x faster per page of the roadmap corpus at four cores
  (`paint` 1323.7 -> 461.4 ms), and about half of that is the clip narrowing
  rather than the threads: a document taller than its viewport was rasterising
  text and boxes that nothing could see. Output is byte-identical to the
  single-tile render at any tile count, and the full WPT run reports the same
  verdict and the same pixel-match percentage on every test. Set
  `BROILER_RASTER_TILES=1` to replay on the calling thread only, which a host
  that already runs several renders at once should do.
- `Broiler.Media.Image.Managed` — PNG expand-to-RGBA and JPEG dequantize/IDCT and
  colour conversion now run across threads (2.08–2.61x on a 1024x1024 JPEG,
  1.22–1.29x on the PNG equivalent, at four cores). Output is byte-identical to
  the single-threaded decoder at any thread count — the sequential path is the
  same code with one band, not a separate implementation. Inflate, unfilter and
  Huffman decode are unchanged: they carry real sequential dependencies. Set
  `BROILER_IMAGE_DECODE_THREADS=1` to decode on the calling thread only, which a
  host that already runs several decodes at once should do.

### Fixed

- `Broiler.HtmlBridge` — `window` is the global object, as it is in a browser, so a
  page's `window.x = …` and its unqualified `x` name one property. They were separate
  objects, and identifier resolution consults only the global, so `window.google = _g`
  left the bare `google` a `ReferenceError` — which aborts the whole `<script>`, not
  just the statement. google.com bootstraps exactly that way *within one script*
  (`window.google=_g` in one IIFE, `google.sn='webhp'` in the next), so its namespace
  was never finished and every later script that named it died the same way: five
  `google is not defined` errors from one broken script, and a homepage with none of
  its script-driven content. The window→global mirror covered the between-scripts
  half by copying members across afterwards; nothing could cover the within-one-script
  half, because no host runs between the write and the read. The mirror and its public
  re-run `SyncWindowMembersOntoGlobal` are kept and return immediately when the two
  objects are one. Three things the split had hidden came with it: `frames` was
  registered twice with different shapes (a live getter on the window, a static
  snapshot on the global) and is now the accessor alone; `GetWindowOrigin` walked
  `parent` to inherit an `about:blank` document's origin and only terminated because
  the top window's `parent` used to be the bare global, so a top-level `about:blank`
  page now recursed 33 707 frames into a stack overflow; and the top window's origin
  can no longer be read back out of its `location`, because `RunWithWindowContext`
  swaps that to a frame's while the frame's scripts run — which is when a frame calls
  `parent.postMessage` — so it comes from the document through a new
  `IMessagingHost.PageOrigin` seam.
- `Broiler.HtmlBridge` — `fetch` resolves its input against the document's base URL
  (Fetch §5.4) instead of handing the string to `HttpClient` untouched. A relative
  request URI with no `BaseAddress` is not a request at all: `PrepareRequestMessage`
  throws `InvalidOperationException("An invalid request URI was provided…")` before
  anything reaches the wire. google.com hits it on every page view, beaconing timing to
  `/gen_204?atyp=i&…` through `navigator.sendBeacon`, which delegates to this `fetch`;
  so does any `XMLHttpRequest` opened on a path, because the XHR polyfill calls
  `fetch(this._url)`. It adopts the same `UrlResolver` that `Response.redirect`
  already used, which also makes `response.url` the resolved URL as the spec requires.
  A target that resolves against nothing is reported as an error `Response`, like any
  other failed fetch, rather than thrown — throwing would abort the calling script over
  one beacon.
- `Broiler.Layout` — `position: relative` now offsets an inline-level box. CSS 2.1
  §9.4.3's offset is visual, so it has to reach the box's words; `PerformLayout`
  applied it for every box it lays out, but an inline-level box is laid out by
  `CreateLineBoxes` and never goes through `PerformLayout` — so neither an inline
  `<span>` nor an inline `<img>` moved at all. **+73 reftests, none lost**, over a
  16 059-test sweep: `css/css-writing-modes` +68 (419 → 487 of 1139),
  `CSS2/box-display` +3, `CSS2/positioning` +1, `CSS2/visuren` +1. The family that
  gains is
  `abs-pos-non-replaced-v{lr,rl}-*`, whose *references* place their swatch with
  `position: relative`. Vertical containers are excluded — their words sit in the
  engine's rotated space, so a physical `left`/`top` arrives turned a quarter turn
  and needs a per-writing-mode mapping first.
- `Broiler.Layout` — a replaced element whose width is a percentage no longer
  ignores its stated height. CSS 2.1 §10.4 uses the intrinsic ratio to fill in a
  dimension left `auto`, not to overrule one the author stated, but the
  percentage-width branch of `MeasureImageSize` set the derive-the-height flag
  unconditionally — so `<img width="100%" height="50">` came out as tall as it was
  wide. **+89 reftests, none lost**, over a 16 059-test sweep of every directory
  that sizes a replaced element: `css/CSS2/backgrounds` +43 (204 → 247 of 339),
  `normal-flow` +22, `borders` +13, `positioning` +10. The tests were right all
  along — the bug was in the reference documents they are compared against, which
  draw their coloured band exactly that way.
- `Broiler.Documents` — the DOCX reader walked only the direct `w:p` children of
  `w:body`, so a document whose content lived inside a layout table (the shape CV
  and letterhead templates use) opened completely empty in Broiler.Writer. Block
  content is now walked recursively: tables, structured document tags, accepted
  revisions, and `mc:AlternateContent`.
- `Broiler.Documents` — the DOCX reader ignored `word/styles.xml`, so template
  documents (whose paragraphs carry no direct formatting at all) read as
  undifferentiated body text. Named paragraph and character styles now resolve
  through `w:docDefaults` and the `w:basedOn` chain, and `w:rFonts` theme
  references resolve against `word/theme/theme1.xml`.

### Added

- `Broiler.Documents.Model` — `InlineStyle.Capitalization` (`none`/`all caps`/
  `small caps`), extending the ADR 0014 inline style set. Capitalization is a
  display property: the text keeps the casing the author typed, so an
  open-and-save no longer rewrites it. Round-trips as DOCX `w:caps`/
  `w:smallCaps`, RTF `\caps`/`\scaps`, and CSS `text-transform`/`font-variant`.
- `Broiler.UI.RichEdit` — draws capitalization, synthesizing small caps by
  drawing letters typed in lower case as capitals at a reduced size, plus
  `RichEditCommand.AllCaps`/`SmallCaps` and formatting-code tokens
  `[All Caps ON]`, `[Small Caps ON]`, and `[Caps OFF]`.
- `Broiler.Documents` — DOCX read diagnostics: `docx.read.summary`,
  `docx.document.empty`, `docx.table.flattened`, `docx.block.unsupported`,
  `docx.limit.depth`, `docx.part.headerfooter`, `docx.styles.missing`,
  `docx.styles.unknown`, `docx.styles.cycle`, and `docx.styles.depth`.
- `Broiler.Cli` — `--convert-doc` prints every read diagnostic and the character
  count, not just a diagnostic count.
- `Broiler.Writer` — the status bar calls out a document that read as no content,
  and `BROILER_WRITER_DOCUMENT_LOG=1` writes the read diagnostics to stderr.

## [0.1.0-preview.1] — first preview

First packaged preview of the Broiler component libraries. **APIs are unstable**
and this preview is for evaluation, testing, and contribution — not production.

### Added

- NuGet packaging for the reusable component libraries (the `Broiler.Writer` and
  browser applications are not packaged):
  - `Broiler.DOM` — `Broiler.Dom`, `Broiler.Dom.Html`
  - `Broiler.CSS` — `Broiler.CSS`, `Broiler.CSS.Dom`
  - `Broiler.Layout`
  - `Broiler.Graphics` — core plus Direct2D / Linux / OpenGL / Vulkan backends
  - `Broiler.HTML` — renderer libraries
  - `Broiler.JS` — engine plus `Broiler.DateTime`, `Broiler.Regex`, Unicode data
  - `Broiler.Media` — core, audio, image, video (+ managed implementations)
  - `Broiler.Input` — device contracts and platform backends
  - `Broiler.UI` — retained-mode toolkit (control contracts + Standard implementations)
  - `Broiler.Documents` — model, RTF, DOCX, HTML, Markdown codecs
- Convenience meta-packages: `Broiler.Media.All`, `Broiler.Input.All`, `Broiler.UI.All`.
- Each package ships an icon, README, Apache-2.0 license expression, XML
  documentation, a symbol package (`.snupkg`), and SourceLink metadata.

### Notes

- Packages are licensed under Apache-2.0. `Broiler.HTML` and `Broiler.JS` include
  `THIRD_PARTY_NOTICES.md` (HTML Renderer BSD-3-Clause; Yantra JS Apache-2.0).
- Public release to NuGet.org is gated on the per-component `HUMAN_REVIEW.md`
  records; `Broiler.JS` review is still pending.

[Unreleased]: https://github.com/Broiler-Platform/Broiler/compare/nuget-v0.1.0-preview.1...HEAD
[0.1.0-preview.1]: https://github.com/Broiler-Platform/Broiler/releases/tag/nuget-v0.1.0-preview.1

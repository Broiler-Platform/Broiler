# Changelog

All notable changes to the Broiler component packages are documented here. The
format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
packages use [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Packages
are versioned in lockstep during the preview.

## [Unreleased]

### Added

- `Broiler.Wpt` — a WPT `-print` reftest now renders the paint its own `@page`
  rule puts on the sheet (CSS Paged Media 3 §7): the page background over the
  whole page box, margins included, and the page's border and padding on the box
  the margins leave, with the flow inset by them. Independent of the
  `BROILER_WPT_PAGED_PRINT` lever, because a page paints whether or not the flow
  is paginated — `css-page/page-background-image-print` states outright that its
  background should print and not show on screen. A page whose root element
  generates no box paints nothing at all, and `visibility` applies in the page
  context. `css/css-page` goes 133 → 137 of 224 reftests (138 once
  `patches/0001-html-canvas-backdrop-lever.patch` is applied), nothing lost;
  documents that declare no page paint render byte-identically to before.
- `Broiler.Layout` — CSS 2.1 §11.1.2 `clip`, the legacy rectangular clip on an
  absolutely positioned element. Resolved into the `clip-path: inset()` that names
  the same operation (`IR.ClipRect`, in `ComputedStyleBuilder`, where the used
  border box is known), so nothing downstream needs a second clip; a real
  `clip-path` supersedes it, per CSS Masking 1 §7. With
  `patches/0002-html-empty-inset-clip.patch`, which stops an empty `inset()` being
  dropped as if it were no clip, `css/CSS2/visufx` goes 6 → 50 of 51 reftests and
  `css-masking/clip` gains two, none lost.
- `Broiler.Wpt` — inline scripts in an XHTML test are unwrapped from their XML
  CDATA section before execution. `<![CDATA[` is a syntax error, so every script
  in such a document was lost — including the functions an `onload` attribute goes
  on to call, which left the test rendering its pre-script state.
  `css/CSS2` goes 4863 → 4908 of 6216 reftests, nothing lost.
- `Broiler.Layout` — `Engine.CanvasBackdrop`, the colour a translucent canvas
  background (CSS 2.1 §14.2) is composited against when the surface underneath it
  already carries paint. Thread-static and null by default, so a render that does
  not set it is unchanged. Read by a one-line `Broiler.HTML` change carried as
  `patches/0001-html-canvas-backdrop-lever.patch`.

### Changed

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

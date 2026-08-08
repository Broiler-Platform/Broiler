# Changelog

All notable changes to the Broiler component packages are documented here. The
format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
packages use [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Packages
are versioned in lockstep during the preview.

## [Unreleased]

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

# Broiler.Layout Component Plan

**Status:** Decided — to be implemented after the Phase 5 renderer cutover lands.
**Date:** 2026-06-26
**Scope:** Extract the renderer's CSS box-model and layout engine out of
`Broiler.HTML.Dom` into a standalone `Broiler.Layout` component that consumes a
computed style (from `Broiler.CSS.Dom`) over the canonical `Broiler.Dom` tree and
produces box geometry, without depending on a graphics backend, the JavaScript
bridge, networking, or the HTML facade.

## 1. Decision

**Yes — create `Broiler.Layout`.** This is the natural next extraction after
`Broiler.CSS` / `Broiler.CSS.Dom` (see [`broiler-css-component.md`](broiler-css-component.md)):
CSS owns syntax/cascade/computed style; `Broiler.Layout` owns formatting and box
geometry; the HTML renderer keeps painting, resource loading, and platform glue.

Per project direction at this stage, the **cons** in the usual pro/con framing for
this extraction — "the component could absorb too much and become another monolith"
and "extraction may be premature" — are **explicitly out of scope**. They are
recorded here only so the decision is traceable; they do not gate the work. The
mitigations are the same ones the CSS extraction already proved: an ownership table,
forbidden-dependency architecture tests, and a dual-run migration.

The top-level component name is `Broiler.Layout` (alongside `Broiler.CSS` and
`Broiler.DOM`), matching the requested casing.

## 2. Scope

### 2.1 Moves into `Broiler.Layout`

The ~9,700 lines of box/layout code currently in
`Broiler.HTML/Source/Broiler.HTML.Dom/`:

| File | Lines | Role |
|---|---:|---|
| `CssBox.cs` | 4,222 | Box tree + block/inline layout, positioning, containing blocks |
| `CssBoxProperties.cs` | 1,781 | ~80 CSS box properties (string storage) + `Actual*` used-value getters |
| `CssLayoutEngine.cs` | 1,624 | Block/inline flow, line breaking, image sizing |
| `CssLayoutEngineTable.cs` | 997 | Table layout |
| `CssBoxHelper.cs` | 586 | Box-tree construction |
| `CssLineBox.cs` | 170 | Inline line rectangles |
| `CssRect.cs` / `CssRectWord.cs` / `CssRectImage.cs` | ~150 | Inline word/image rectangles |
| `CssBoxImage.cs` / `CssBoxHr.cs` | ~150 | Replaced-element / `<hr>` boxes |
| `Utils/CssUtils.cs` | 797 | Whitespace/length/property helpers |

Entry point: `CssBox.PerformLayout(RGraphics g)` (`CssBox.cs:519`).
`CssBox` is `internal`; reached by 8 assemblies via `InternalsVisibleTo`.

### 2.2 Stays with consumers

| Responsibility | Owner after extraction |
|---|---|
| Cascade / computed style | `Broiler.CSS.Dom` (already extracted) |
| Painting, borders, backgrounds, images, SVG | `Broiler.HTML.Rendering` + backends |
| Hit-testing, selection, JS box bindings | `Broiler.HtmlBridge.*` |
| Resource loading (fonts, images, stylesheets) | HTML orchestration / host |
| HTML parsing, `<style>`/`<link>` discovery | `Broiler.HTML.Orchestration` |

## 3. Dependency target

```
Broiler.Layout
 ├─ Broiler.CSS.Dom   (CssComputedStyle — the layout input)
 ├─ Broiler.CSS       (CssLength/units/constants used during used-value resolution)
 ├─ Broiler.Dom       (canonical element link, SourceElement)
 └─ ILayoutEnvironment (host-injected: text metrics, image intrinsics, color parse)
```

**Forbidden:** `Broiler.HTML.*` facade, `Broiler.HtmlBridge.*`, `Broiler.JavaScript.*`,
concrete `Broiler.Graphics` backends, WPF/Image/network/filesystem. Enforced by
architecture tests, mirroring `CssDomArchitectureTests`.

## 4. The coupling to break (the design problem)

Layout currently reaches platform/renderer services directly. These become an
injected abstraction at layout time — the same move the CSS extraction made when it
replaced `IColorResolver` + font-availability with explicit environment inputs.

| Today | Used for | Becomes |
|---|---|---|
| `RGraphics.MeasureString(text, RFont)` | inline width/height, line breaking | `ILayoutEnvironment.MeasureText(font, text)` |
| `RGraphics.GetWhitespaceWidth` / font metrics | word spacing, baselines | `ILayoutEnvironment` font metrics |
| `RImage.Width/Height/HasIntrinsicRatio` | replaced-element sizing | `ILayoutEnvironment.GetImageIntrinsics(handle)` |
| `IHtmlContainerInt.GetFont` / `ParseColor` / `RequestRefresh` | font resolution, color parse, invalidation | `ILayoutEnvironment` (font + color), host callback |
| `System.Drawing` `Color`/`RectangleF`/`PointF`/`SizeF` | box geometry, colors | kept initially (intrinsic to the box model); a `Broiler.Layout` geometry primitive set is a later option |

`RGraphics` itself is already backend-agnostic (abstract base in
`Broiler.HTML.Adapters`); the goal is to depend on a *narrow layout-metrics
interface* rather than the full adapter/container surface.

## 5. Input seam — depends on Phase 5

Phase 5 makes the renderer project an immutable `CssComputedStyle` onto
`CssBoxProperties` (the `GetCascadedStyle` → `CssUtils.SetPropertyValue` path).
That projection is exactly the boundary `Broiler.Layout` consumes: layout receives
already-cascaded/inherited style and resolves only *used* values (containing-block
%, font metrics, intrinsic sizes). **This extraction should start only after the
Phase 5 flag is flipped**, so layout has a single, principled style input and the
legacy `CssData`/`CssBlock` cascade no longer writes into `CssBoxProperties`.

## 6. Phased migration (mirrors the CSS extraction)

0. **Guard.** Architecture tests freezing `Broiler.HTML.Dom`'s current references and
   the `InternalsVisibleTo` surface; characterization of layout outputs (box rects)
   for representative pages, to distinguish movement from behavior change.
1. **Create `Broiler.Layout` + tests.** Introduce the project; define
   `ILayoutEnvironment`; no code moved yet (arch tests forbid Broiler.HTML refs).
2. **Abstract the environment.** Replace direct `RGraphics`/`RImage`/`IHtmlContainerInt`
   use in the layout files with `ILayoutEnvironment`, adapted by a thin renderer-side
   implementation. Dual-run: renderer still owns the files.
3. **Adopt the computed-style input.** Layout takes `CssComputedStyle` (post Phase 5)
   instead of reading `CssData`-assigned fields; keep `Actual*` used-value resolution.
4. **Move the files.** Relocate `CssBox*`/`CssLayout*`/`CssRect*`/`CssLineBox`/`CssUtils`
   into `Broiler.Layout`; repoint the 8 consumers; keep the public/`internal` surface.
5. **Cleanup.** Trim now-unneeded `InternalsVisibleTo`; update architecture/API docs;
   remove dead renderer glue.

## 7. Verification

- Architecture tests: `Broiler.Layout` references only `Broiler.CSS`, `Broiler.CSS.Dom`,
  `Broiler.Dom`, and BCL; no graphics/JS/HTML-facade leak.
- Layout characterization (box-rect) parity across Acid2/Acid3 and the WPT pixel
  subset (`tests/wpt`), dual-run before each consumer cutover.
- Full `Broiler.slnx` build green; renderer pixel + WPT gates within the documented
  baseline.

## 8. Non-goals (initial)

- No painting, compositing, image decode, SVG raster, or font shaping moves.
- No new public NuGet package or Git submodule before the in-repo migration proves
  the API.
- No layout-algorithm rewrite — this is an extraction, not a re-implementation.

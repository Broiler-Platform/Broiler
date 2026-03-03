# ADR-009: Acid1 Differential Testing with Headless Chromium

## Status

Accepted

## Context

The [Acid1 test](https://www.w3.org/Style/CSS/Test/CSS1/current/test5526c.htm)
is the W3C CSS1 conformance test. A CSS1-conformant renderer must produce output
pixel-identical to the
[reference rendering](https://www.w3.org/Style/CSS/Test/CSS1/current/sec5526c.gif)
(except for font rasterisation and form widgets).

Existing Acid1 tests in `Broiler.Cli.Tests` validate the HTML-Renderer engine
in isolation using pixel counting and similarity scoring. However, they do not
compare against a live reference renderer. This ADR establishes a differential
testing approach that renders `acid1.html` (and its 10 split sections) in both
the HTML-Renderer engine and headless Chromium (via Playwright), compares the
pixel output, and documents the discrepancies.

## Decision

Add `Acid1DifferentialTests` to `HtmlRenderer.Image.Tests` that:

1. Render the full `acid1.html` and each split section in both engines.
2. Produce side-by-side pixel-diff reports (Broiler PNG, Chromium PNG, diff
   PNG, fragment JSON, display-list JSON).
3. Use a generous 95 % pixel-diff threshold so the tests serve as
   **documentation baselines**, not enforcement gates.
4. Run as part of the nightly differential CI workflow
   (`nightly-differential.yml`), excluded from per-commit builds via
   `Category=Differential`.

## Observed Errors

Testing was performed on 2026-02-26 using HTML-Renderer (Broiler) and
Playwright Chromium 145.0.7632.6. All pixel-diff ratios are measured at
800×600 viewport with 30 per-channel colour tolerance.

| Section | CSS1 Feature | Pixel Diff | Severity | Category |
|---------|-------------|-----------|----------|----------|
| Full page | All CSS1 features combined | < 50 % | High | Layout |
| 1 – Body border | `html` bg blue, `body` bg white + border | 89.97 % | High | Layout |
| 2 – `dt` float:left | `float:left`, percentage width (10.638 %) | 86.30 % | High | Layout / Float |
| 3 – `dd` float:right | `float:right`, border, width, side-by-side with `dt` | 84.16 % | High | Layout / Float |
| 4 – `li` float:left | Multiple `float:left` stacking, gold bg | 82.05 % | High | Layout / Float |
| 5 – `blockquote` | `float:left`, asymmetric borders | 92.00 % | Critical | Layout / Float / Border |
| 6 – `h1` float | `float:left`, black bg, normal font-weight | 91.23 % | Critical | Layout / Float |
| 7 – `form` line-height | `line-height: 1.9` on form paragraphs | 85.22 % | High | Layout / Typography |
| 8 – `clear:both` | `clear:both` paragraph after floats | 72.17 % | Medium | Layout / Clear |
| 9 – Percentage width | `10.638 %` and `41.17 %` widths | 84.64 % | High | Layout / Box Model |
| 10 – `dd` height/clearance | Content-box height, float clearance | < 50 % | Medium | Layout |

### Error Categories

1. **Float layout (Sections 2–6, 8):** The HTML-Renderer float algorithm does
   not correctly implement CSS1 §5.5.25/5.5.26. Floated elements are
   positioned incorrectly relative to adjacent content and do not properly
   reduce available line width. This is the dominant source of rendering
   differences.

2. **Border rendering (Sections 1, 5):** Asymmetric border widths
   (`border-width: 1em 1.5em 2em .5em`) and `em`-unit borders are not
   rendered to the same dimensions as Chromium. The body's `.5em solid black`
   border also shows positioning differences.

3. **Percentage width resolution (Sections 2, 9):** Percentage widths
   (`10.638 %`, `41.17 %`, `50 %`) are resolved but the containing-block
   width calculation differs from the CSS1 specification, causing cascading
   layout shifts.

4. **Typography / line-height (Section 7):** `line-height: 1.9` is applied
   but the resulting line-box height differs from Chromium, affecting vertical
   spacing within form elements.

5. **Clear property (Section 8):** `clear: both` moves below floats but the
   vertical clearance distance is not computed identically to Chromium.

6. **Background propagation (Section 1):** The CSS1 rule that propagates the
   `html` element's background to the canvas is not correctly applied; the
   blue background does not cover the full viewport as it does in Chromium.

## Fix Roadmap

### Priority 1 – Float Layout Engine (Sections 2–6) ✅ Fixed

- ✅ **Float positioning algorithm:** Implemented CSS1 §5.5.25/5.5.26 for float
  placement. Left floats use iterative collision detection against both left and
  right floats. Right floats now use the same iterative collision detection
  approach (previously had no collision detection at all).
- ✅ **Available width reduction:** Left floats now check against right floats
  to ensure they do not extend past them. Right floats check against left floats
  to avoid overlap.
- ✅ **Float stacking:** Multiple `float:left` elements stack horizontally
  (existing). Multiple `float:right` elements now stack from right-to-left
  until the available width is exhausted, then wrap to a new line.
- ✅ **Float clearing:** `clear:left`, `clear:right`, and `clear:both`
  correctly compute the clearance distance below active floats (existing).

#### Changes Made

- `CssBox.PerformLayoutImp()`: Added iterative collision detection for
  `float:right` elements, matching the existing algorithm for `float:left`.
  Right floats now avoid overlapping with preceding right floats (stacking
  from right-to-left) and preceding left floats. When there is not enough
  room, the right float drops below the lowest overlapping float.
- `CssBox.PerformLayoutImp()`: Updated left float collision detection to
  also check against preceding right floats, preventing left floats from
  extending into the space occupied by right floats.
- Added 5 new programmatic tests in `Acid1ProgrammaticTests.cs`:
  - `Float_TwoRightFloats_StackRightToLeft`
  - `Float_RightFloats_WrapWhenContainerFull`
  - `Float_RightDoesNotOverlapLeft`
  - `Float_DtLeftDdRight_SideBySide`
  - `Float_LeftDoesNotOverlapRight`
- Added 6 repeated render validation tests in `Acid1RepeatedRenderTests`:
  - Full acid1.html and Sections 2–6 rendered multiple times to verify
    determinism after float layout changes.

### Priority 2 – Box Model / Containing Block (Sections 1, 9) ✅ Fixed

- ✅ **Containing block width:** Verified that percentage widths correctly
  resolve against the content width of the containing block (not the
  padding-box or border-box). The existing `CssBox.PerformLayoutImp()`
  already subtracts the containing block's padding and border before
  resolving percentage values.
- ✅ **Body/html background propagation:** Implemented CSS2.1 §14.2 canvas
  background propagation. The root element's (`html`) background now fills
  the entire viewport/canvas, not just the element's bounding box. If the
  root element has a transparent background, the body element's background
  is used for the canvas.
- ✅ **Border-box vs content-box:** Verified that `width` sets the content-box
  width and borders/padding are added outside it (CSS1 content-box model).
  The existing code correctly adds `ActualPaddingLeft + ActualPaddingRight +
  ActualBorderLeftWidth + ActualBorderRightWidth` after resolving the
  explicit width value.

#### Changes Made

- `PaintWalker.Paint()`: Added `viewport` parameter and
  `EmitCanvasBackground()` / `FindCanvasBackground()` methods that implement
  CSS2.1 §14.2 canvas background propagation. The root element's background
  is emitted as a `FillRectItem` covering the full viewport before painting
  the fragment tree.
- `HtmlContainerInt.PerformPaint()`: Computes the viewport rectangle from
  `MaxSize`/`PageSize` and passes it to `PaintWalker.Paint()`.
- Added 4 new programmatic tests in `Acid1ProgrammaticTests.cs`:
  - `CanvasBackground_HtmlBgColor_CoversEntireViewport`
  - `CanvasBackground_HtmlBgColor_CoversBelowContent`
  - `CanvasBackground_Acid1Section1_HtmlBlueCoversViewportEdges`
  - `CanvasBackground_Section9_PercentageWidthWithBlueBg`
- Added 1 new split test in `Acid1SplitTests.cs`:
  - `Section1_BodyBorder_HtmlBgCoversEntireViewport`
- Added 2 new repeated render tests in `Acid1RepeatedRenderTests`:
  - `Section1_BodyBorder_RepeatedRender_IsDeterministic`
  - `Section9_PercentageWidth_RepeatedRender_IsDeterministic`

### Priority 3 – Border Rendering (Section 5) ✅ Fixed

- ✅ **Asymmetric borders:** Solid borders with different widths per side
  (`border-width: 1em 1.5em 2em .5em`) are now rendered as CSS-compliant
  trapezoids instead of thick lines. Each border side is drawn as a filled
  polygon with proper diagonal corner joins, matching how Chromium renders
  asymmetric solid borders.
- ✅ **Em-unit borders:** Verified that `em`-based border widths are correctly
  resolved against the element's computed `font-size` via
  `CssValueParser.GetActualBorderWidth()` → `ParseLength()`.

#### Changes Made

- `RGraphicsRasterBackend.RenderDrawBorder()`: Changed solid border rendering
  from thick lines (`DrawLine` with pen width) to filled polygons
  (`DrawPolygon` with 4-point trapezoid), matching the approach used by the
  old `BordersDrawHandler` for inset/outset borders. Dotted and dashed borders
  continue to use the line-based approach for correct dash pattern rendering.
- Added 1 new IR test in `IRTypesTests.cs`:
  - `PaintWalker_AsymmetricBorder_EmitsDrawBorderItemWithCorrectWidths`
- Added 4 new programmatic tests in `Acid1ProgrammaticTests.cs`:
  - `AsymmetricBorder_EmUnit_BottomWiderThanTop`
  - `AsymmetricBorder_EmUnit_RightWiderThanLeft`
  - `AsymmetricBorder_Solid_NoCornersGap`
  - `AsymmetricBorder_Acid1Section5_CorrectBorderBoxSize`
- Added 1 new split test in `Acid1SplitTests.cs`:
  - `Section5_BlockquoteFloat_BottomBorderThickerThanTop`

### Priority 4 – Typography (Section 7) ✅ Fixed

- ✅ **Line-height computation:** Verified that `line-height: 1.9` produces a
  line-box height of `1.9 × font-size` as defined in CSS1 §5.4.8. The existing
  `CssValueParser.ParseLength()` correctly treats unitless values as `em`
  multipliers via the `defaultUnit` parameter.
- ✅ **Block-in-inline line breaks:** Block-level `<p>` elements inside an
  inline `<form>` now correctly force line breaks before and after themselves
  (CSS2.1 §9.2.1.1 anonymous block box behaviour). Previously, block elements
  inside inline parents were flowed inline, causing all paragraph content to
  appear on a single line.
- ✅ **Form element line-height:** Verified that `line-height` applies correctly
  to paragraphs containing inline form controls (radio buttons).

#### Changes Made

- `CssLayoutEngine.FlowBox()`: When encountering a block-level child (`b.IsBlock`)
  during inline flow, the method now forces a line break before and after the block.
  Before the block: if there is existing content on the current line (`curx > startx`
  or `maxbottom > cury`), advance `cury` to `maxbottom`, reset `curx`, and create a
  new `CssLineBox`. After the block: always advance `cury` to `maxbottom`, reset
  `curx`, and create a new `CssLineBox`. This ensures block elements like `<p>`
  inside inline containers like `<form display:inline>` start on their own line.
- Added 4 new programmatic tests in `Acid1ProgrammaticTests.cs`:
  - `LineHeight_Unitless_ProducesCorrectLineBoxHeight`
  - `LineHeight_TwoParagraphs_VerticalSeparation`
  - `LineHeight_BlockInsideInlineForm_SeparateLines`
  - `LineHeight_FormWithRadioButtons_CorrectSpacing`
- Added 1 new split test in `Acid1SplitTests.cs`:
  - `Section7_FormLineHeight_TwoLinesWithSpacing`
- Added 1 new repeated render test in `Acid1RepeatedRenderTests`:
  - `Section7_FormLineHeight_RepeatedRender_IsDeterministic`

## Consequences

- The Acid1 differential tests serve as a living dashboard for CSS1
  compliance progress. As rendering improvements land, the diff ratios will
  decrease and the threshold can be tightened.
- Nightly CI generates side-by-side visual reports that developers can
  inspect to understand exactly where the rendering diverges.
- Sub-issues should be created for each priority block above and linked to
  the tracking issue.
- The 95 % threshold is intentionally permissive and should be lowered to
  50 %, then 25 %, then 5 % as the roadmap items are completed.

## Related Tests

| Test File | Project | Purpose |
|-----------|---------|---------|
| `Acid1DifferentialTests.cs` | `HtmlRenderer.Image.Tests` | Per-section pixel comparison against Chromium |
| `Acid1CaptureTests.cs` | `Broiler.Cli.Tests` | Visual regression and structural validation |
| `Acid1ProgrammaticTests.cs` | `Broiler.Cli.Tests` | Layout and float positioning assertions |
| `Acid1SplitTests.cs` | `Broiler.Cli.Tests` | Per-section CSS1 feature isolation |

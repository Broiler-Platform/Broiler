# Roadmap: Making Broiler ACID3 Compliant

> **Status**: Active — last updated based on real pixel-comparison results  
> **Tracking issue**: Create Roadmap for Making Broiler ACID3 Compliant

---

## Table of Contents

1. [Current State](#1-current-state)
2. [Test Automation Setup](#2-test-automation-setup)
3. [Image Comparison Methodology](#3-image-comparison-methodology)
4. [Identified Rendering Differences](#4-identified-rendering-differences)
5. [Prioritised TODO List](#5-prioritised-todo-list)
6. [Suggested Test Coverage Improvements](#6-suggested-test-coverage-improvements)
7. [Fidelity Targets and Milestones](#7-fidelity-targets-and-milestones)

---

## 1. Current State

### Acid3 Score (JavaScript)

Broiler's JavaScript engine currently achieves a score of **98/100** when
executing the Acid3 test harness via `CaptureService.ExecuteScriptsWithDom`.
This is tracked by existing regression tests in
`src/Broiler.Cli.Tests/Acid3RegressionTests.cs` (102 individual tests).

### Pixel Fidelity (Visual Rendering)

A pixel-by-pixel comparison of the Broiler CLI render (`acid/acid3/acid3.png`)
against a Chromium reference (`acid/acid3/acid3-reference.png`) shows:

| Metric                   | Value        |
|--------------------------|--------------|
| Full-image pixel match   | **13.66 %**  |
| Content-area match       | **0.43 %**   |
| Score-area match         | 0.81 %       |
| Bucket-area match        | 0.90 %       |
| Bottom-text-area match   | 0.14 %       |
| Broiler image size       | 1024 × 891   |
| Reference image size     | 1024 × 768   |

The gap is large: the rendering engine produces structurally different output
from a compliant browser across nearly every area of the page.

---

## 2. Test Automation Setup

### 2.1 Rendering with Broiler CLI

Broiler CLI supports image capture via the `--capture-image` flag. The
pipeline is:

```
HTML → ExecuteScriptsWithDom (JS execution) → HtmlPostProcessor → HtmlRender (SkiaSharp)
```

**Invocation:**

```bash
dotnet run --project src/Broiler.Cli -- \
    --capture-image "file://$(pwd)/acid/acid3/acid3.html" \
    --output acid/acid3/acid3.png \
    --width 1024 --height 768 --full-page
```

The `--full-page` flag uses `HtmlRender.RenderToImageAutoSized` which expands
the canvas to fit all content, explaining the 891px-tall output. For viewport-
constrained capture, omit `--full-page` to use `HtmlRender.RenderToFile` at
the exact 1024 × 768 viewport.

### 2.2 Rendering with Chromium (Playwright)

A reference image is generated using headless Chromium via Playwright:

```bash
# One-time setup
npm install playwright
npx playwright install chromium

# Capture
node -e "
const { chromium } = require('playwright');
const path = require('path');
(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });
    await page.goto('file://' + path.resolve('acid/acid3/acid3.html'), { waitUntil: 'load' });
    await page.waitForTimeout(15000);   // let test harness + animations finish
    await page.screenshot({ path: 'acid/acid3/acid3-reference.png', fullPage: false });
    await browser.close();
})();
"
```

**Key details:**

- `waitUntil: 'load'` plus a 15-second timeout ensures all Acid3 animations
  and the score computation finish before capture.
- `fullPage: false` constrains the screenshot to the 1024 × 768 viewport,
  matching the reference rendering specification.

### 2.3 Running the Full Pipeline

The repository includes `scripts/acid3-pixel-test.sh` which orchestrates all
three steps (Broiler render → Chromium render → pixel comparison):

```bash
bash scripts/acid3-pixel-test.sh                 # full pipeline
bash scripts/acid3-pixel-test.sh --skip-reference # reuse existing reference
```

Output artefacts are written to `acid/acid3/`:

| File                  | Description                       |
|-----------------------|-----------------------------------|
| `acid3.png`           | Broiler CLI render                |
| `acid3-reference.png` | Chromium reference render         |
| `acid3-diff.png`      | Colour-coded diff image           |
| `acid3-report.txt`    | Structured comparison report      |

---

## 3. Image Comparison Methodology

### 3.1 Comparison Script

`scripts/acid3-compare.py` performs the comparison using Pillow and NumPy:

1. **Load and normalise** — both images are converted to RGB and resized to
   the reference dimensions (1024 × 768).
2. **Per-pixel match** — each pixel is compared per-channel with a tolerance
   of **5** (matching `DeterministicRenderConfig.ColorTolerance`).
3. **Content vs background mask** — a pixel is classified as *background* only
   if all three RGB channels exceed 240 in *both* images. Everything else is
   *content*. This ensures that foreground rendering fidelity is the primary
   metric, explicitly ignoring background-only mismatches.
4. **Region breakdown** — three named regions are analysed independently:
   - `score_area` (350–700 x, 0–80 y) — the "100/100" score display
   - `bucket_area` (0–1024 x, 80–400 y) — the six coloured test buckets
   - `bottom_area` (0–1024 x, 400–768 y) — the instruction paragraph
5. **Diff image** — colour-coded: green = match, red = content mismatch,
   yellow = background mismatch.

### 3.2 Running the Comparison Standalone

```bash
pip install Pillow numpy
python3 scripts/acid3-compare.py \
    acid/acid3/acid3.png \
    acid/acid3/acid3-reference.png \
    --output-dir acid/acid3/
```

---

## 4. Identified Rendering Differences

Based on the pixel comparison and visual inspection of the Broiler render, the
following categories of differences have been identified. Each is assigned an
ID for tracking in the TODO list below.

### D1 — Root Background Colour (`:root { background: silver }`)

**Severity: High**

The Acid3 CSS sets `:root { background: silver; }` (RGB 192,192,192). The
Chromium reference correctly renders a silver background across the entire
viewport. Broiler renders a **white** (255,255,255) background instead.

**Evidence:** All four corners of the Broiler image read (255,255,255) while
the reference reads (192,192,192).

**Root cause:** The `HtmlRender.RenderToImage` method hard-codes
`SKColors.White` as the fallback background. The CSS `:root` background
declaration is either not parsed or not applied to the root-level canvas
clear colour.

### D2 — Gray Border Layout (`border: 2cm solid gray`)

**Severity: High**

The `<html>` element has `border: 2cm solid gray` with `:root` overriding to
`border-width: 0 0.2em 0.2em 0`. The reference shows a correctly positioned
gray border frame around the content. Broiler's gray border extends
differently (rows 19–727 vs reference 20–452) and starts at different column
offsets (col 9–810 vs reference 20–663).

**Sub-issues:**
- `2cm` unit conversion may be incorrect (should be ~75.6px at 96 DPI).
- The `border-width` shorthand (`0 0.2em 0.2em 0`) overriding the longhand
  `2cm` value may not cascade properly through CSS specificity resolution.
- The asymmetric border (0 top, 0.2em right, 0.2em bottom, 0 left) changes
  the geometry of the entire page frame.

### D3 — Content Viewport Overflow

**Severity: High**

Broiler renders the page at 1024 × 891 pixels (with `--full-page`), while the
reference fits within the 1024 × 768 viewport. Even the viewport-constrained
render (`acid3-viewport.png`) produces only a 9.0% overall match.

**Root cause:** Layout computation overflows the viewport, likely due to:
- Incorrect box-model computation with `border: 2cm` + negative margins.
- Missing `overflow: hidden` on the root element.
- Incorrect total width calculation (`width: 32em` = 640px with 20px font).

### D4 — Score Display ("98/100" → "98100")

**Severity: High**

The Broiler render shows the score text as "98100" rather than "98/100". The
slash character either does not render, renders invisibly, or is dropped
during post-processing.

**Root cause candidates:**
- The `#slash` element uses `color: hsla(0, 0%, 0%, 1.0)` (after a fallback
  `color: red`). HSLA colour parsing may fail, causing the element to inherit
  transparent or white colour on a white background.
- The slash is inserted via JavaScript (`document.createTextNode('/')`);
  the DOM bridge may fail to serialize it.
- `font-weight: bolder` or `font-size: 5em` interaction with the slash
  element might produce zero-width rendering.

### D5 — Blue Border Artefacts in Test Buckets

**Severity: Medium**

The Acid3 CSS applies `border: 1px blue` to all elements via the `*`
selector, then overrides borders on specific elements. Broiler renders
**4,706 blue pixels** in the bucket area (rows 202–398), while the reference
has only **431** blue pixels (rows 391–405).

**Root cause:** The CSS specificity override chain is not working correctly:
- `* { border: 1px blue; }` sets a global blue 1px border.
- `* + * > * > p { margin: 0; border: 1px solid !important; }` should override.
- `.z { visibility: hidden; }` should hide unfilled buckets.
- Broiler may fail to resolve `!important` overrides or visibility correctly.

### D6 — Text Layout and Word Spacing

**Severity: Medium**

The bottom instruction text in the Broiler render runs together without proper
word spacing ("Topassthetest,abrowsermuseitsdefaultsettings..."). The
reference shows properly spaced text.

**Evidence:** The bottom-text-area match is only 0.14%. Broiler has 7,082
dark pixels in the text area vs 698 in the reference (text is in a different
position and runs much longer).

**Root cause candidates:**
- `white-space` handling may collapse spaces incorrectly.
- The `font: 0.8em` instruction text may compute incorrect glyph metrics.
- `margin-right: -20px; padding-right: 20px` negative margin handling.
- Word-break or line-break algorithm differs from the CSS specification.

### D7 — Font Rendering Fidelity (Title, Score, Instruction Text)

**Severity: Medium**

All text areas show significant pixel-level differences. The title "Acid3"
region achieves only 28.0% match. Contributing factors:
- `text-shadow: rgba(192,192,192,1.0) 3px 3px` on `h1` may not render.
- `font-weight: bolder` may resolve to a different weight than Chromium.
- SkiaSharp font metrics (glyph width, ascent, descent) may differ from
  Chromium's Skia-based text shaper.
- Arial font may not be available; fallback font selection differs.

### D8 — CSS `@font-face` and Custom Font Loading

**Severity: Medium**

The Acid3 test declares `@font-face { font-family: "AcidAhemTest"; src: url(font.ttf); }` and uses it for the `map::after` pseudo-element. Broiler
may not load or apply the custom font from the local file.

### D9 — Pseudo-Element Rendering (`::after`, `::before`)

**Severity: Medium**

The `map::after` pseudo-element should render an "X" character at a fixed
position. If `@font-face` or `::after` content generation is not supported,
this element will be absent or incorrectly styled.

### D10 — CSS `hsla()` and Advanced Colour Functions

**Severity: Medium**

The Acid3 test uses `color: hsla(0, 0%, 0%, 1.0)` for the `#slash` element.
If HSLA parsing fails, the slash would inherit a wrong colour or be invisible.
The CSS also uses `rgba()` colour values (`text-shadow: rgba(192,192,192,1.0) 3px 3px`) which require proper alpha channel support.

### D11 — CSS `display: inline-block` and `vertical-align` on Bucket Elements

**Severity: Medium**

Bucket elements use `display: inline-block; vertical-align: 2em; border: 2em dotted red; padding: 1.0em 0 1.0em 2em`. The complex interaction of
inline-block layout, vertical alignment with em units, and dotted borders
may not be fully implemented.

### D12 — CSS Box-Model Edge Cases

**Severity: Low**

Multiple CSS rules use negative margins (`margin: -0.2em 0 0 -0.2em`,
`margin: -2.19em 0 0`) and the body uses `border: solid 1px black`.
Negative margin collapsing and interaction with borders requires precise
box-model computation.

### D13 — SVG and `<object>` Element Rendering

**Severity: Low**

Acid3 includes SVG content via `svg.xml` and `<object>` elements with fixed
positioning. The reference may render these elements; Broiler may skip or
incorrectly position them.

### D14 — `<iframe>` Zero-Size Layout

**Severity: Low**

The CSS sets `iframe { float: left; height: 0; width: 0; }`. Broiler must
correctly lay out zero-sized floated iframes without affecting surrounding
content flow.

### D15 — Data-URI Background Images

**Severity: Low**

The body and instruction text use `data:image/gif;base64,...` background
images. The `HtmlPostProcessor` intentionally preserves these (per the Acid2
lesson), but the rendering engine must correctly decode and position them.

### D16 — Missing Magenta Pixel Artefact

**Severity: Low**

Broiler renders **91 magenta pixels** (visible as a small pink square in the
lower-left of the content area) that are not present in the reference. This
likely comes from the `map::after { background: fuchsia; }` pseudo-element
rendering at an incorrect position or size.

---

## 5. Prioritised TODO List

### P0 — Show-Stoppers (must fix for meaningful pixel comparison)

- [ ] **TODO-1 (D3): Fix viewport overflow / page height calculation**
  - Investigate `HtmlRender.RenderToImageAutoSized` vs fixed-viewport render.
  - Ensure `width: 32em` (640px) + `border: 2cm` + margins fit within 1024px.
  - Verify `overflow` handling on root and body elements.
  - Sub-steps:
    1. Add unit test for box-model computation with `border: 2cm` + `width: 32em`.
    2. Fix total width = content (640px) + border + margin calculation.
    3. Ensure viewport-constrained render clips at 768px height.

- [ ] **TODO-2 (D1): Apply `:root` background colour to canvas**
  - The `:root { background: silver }` declaration must propagate to the
    canvas clear colour in `HtmlRender.RenderToImage`.
  - Sub-steps:
    1. Parse and resolve the computed `background-color` on the `<html>` element.
    2. Pass resolved background to `canvas.Clear()` instead of hard-coded white.
    3. Add regression test: `:root { background: silver }` → silver canvas.

- [ ] **TODO-3 (D2): Fix CSS border shorthand cascade and unit conversion**
  - `border: 2cm solid gray` must convert `2cm` to ~75.6px at 96 DPI.
  - `border-width: 0 0.2em 0.2em 0` must override the longhand values from
    the `border` shorthand per CSS cascade rules.
  - Sub-steps:
    1. Verify `cm` unit support in `ParseCssValue`.
    2. Verify 4-value `border-width` shorthand expansion.
    3. Test cascade: more-specific `:root` rule overrides less-specific `html` rule.

- [ ] **TODO-4 (D4): Fix slash rendering in score display**
  - Ensure `document.createTextNode('/')` content is serialised by DomBridge.
  - Ensure `color: hsla(0, 0%, 0%, 1.0)` is parsed and applied.
  - Sub-steps:
    1. Add unit test for HSLA colour parsing with integer-percent values.
    2. Verify DomBridge text-node serialisation for dynamically created nodes.
    3. Verify `#slash` element receives correct computed colour.

### P1 — High Priority (significant visual impact)

- [ ] **TODO-5 (D5): Fix CSS specificity for `!important` border overrides**
  - `* + * > * > p { border: 1px solid !important }` must win over
    `* { border: 1px blue }`.
  - Sub-steps:
    1. Verify `!important` flag handling in CSS cascade resolution.
    2. Test: `!important` on more-specific selector overrides less-specific rule.
    3. Reduce blue pixel count in bucket area to match reference (~431 pixels).

- [ ] **TODO-6 (D6): Fix word spacing and text layout in instruction paragraph**
  - Investigate `white-space` collapsing and word-break algorithm.
  - Verify `font: 0.8em` computed size inheritance.
  - Sub-steps:
    1. Add test for text word-spacing with inherited font sizes.
    2. Fix whitespace collapsing between inline elements.
    3. Verify `margin-right: -20px; padding-right: 20px` does not collapse text.

- [ ] **TODO-7 (D7): Improve font rendering fidelity**
  - Investigate SkiaSharp font metrics vs browser expectations.
  - Verify `text-shadow` rendering support.
  - Verify `font-weight: bolder` resolution.
  - Sub-steps:
    1. Add test for `text-shadow` with RGBA colours.
    2. Verify `bolder` resolves to correct numerical weight (700 or 900).
    3. Compare glyph metrics for Arial at 20px between SkiaSharp and Chromium.

### P2 — Medium Priority (feature completeness)

- [ ] **TODO-8 (D8): Implement `@font-face` with local file loading**
  - Load `font.ttf` from the Acid3 directory when rendering.
  - Register custom font family with SkiaSharp's font manager.
  - Sub-steps:
    1. Parse `@font-face` declarations from CSS.
    2. Resolve `url(font.ttf)` relative to the HTML file's base path.
    3. Register font with `SKFontManager` before rendering.

- [ ] **TODO-9 (D9): Implement `::after` / `::before` pseudo-element rendering**
  - Generate pseudo-element content from CSS `content` property.
  - Apply positioning and styling to generated content.
  - Sub-steps:
    1. Parse `content: "X"` and generate inline box.
    2. Apply `position: absolute` and coordinate properties.
    3. Apply `background`, `color`, and `font` properties to pseudo-element.

- [ ] **TODO-10 (D10): Implement HSLA colour function parsing**
  - Parse `hsla(h, s%, l%, a)` and convert to RGBA.
  - Support both comma-separated and space-separated syntax.
  - Sub-steps:
    1. Add HSLA-to-RGBA conversion utility.
    2. Integrate into CSS colour parser.
    3. Add regression tests for edge cases (0%, 100%, alpha 0/1).

- [ ] **TODO-11 (D11): Fix `display: inline-block` with `vertical-align` in em units**
  - Verify inline-block baseline alignment with `vertical-align: 2em`.
  - Verify dotted border rendering at 2em width.
  - Sub-steps:
    1. Add unit test for `vertical-align` with em values.
    2. Verify inline-block elements participate in inline formatting context.
    3. Test `border-style: dotted` rendering.

### P3 — Low Priority (minor fidelity differences)

- [ ] **TODO-12 (D12): Fix negative margin collapsing**
  - Verify `margin: -0.2em 0 0 -0.2em` correctly offsets elements.
  - Verify `margin: -2.19em 0 0` pulls score display into position.

- [ ] **TODO-13 (D13): Implement SVG rendering within `<object>` elements**
  - Parse and render inline SVG content.
  - Support `position: fixed` on `<object>` elements.

- [ ] **TODO-14 (D14): Fix zero-sized floated iframe layout**
  - Ensure `float: left; height: 0; width: 0` does not consume space.

- [ ] **TODO-15 (D15): Verify data-URI background image rendering**
  - Decode `data:image/gif;base64,...` and render as background.
  - Support `background: url(...) no-repeat <position>` syntax.

- [ ] **TODO-16 (D16): Fix stray magenta pseudo-element positioning**
  - The `map::after { position: absolute; top: 18px; left: 638px; }`
    pseudo-element renders at an incorrect position and should be invisible
    in the final state.

---

## 6. Suggested Test Coverage Improvements

### 6.1 Pixel Regression Tests

Currently, the 102 Acid3 regression tests in `Acid3RegressionTests.cs` focus
on **JavaScript execution correctness** (DOM manipulation, CSS computed style
queries, event handling). There are no automated tests for **visual rendering
fidelity**.

**Recommendation:** Add a pixel-comparison integration test that:

1. Renders `acid/acid3/acid3.html` via `HtmlRender.RenderToImage`.
2. Compares the output against `acid/acid3/acid3-reference.png`.
3. Asserts a minimum content-area match percentage (start at 5%, increase as
   fixes land).

### 6.2 CSS Unit Tests

Add targeted CSS unit tests for properties used by Acid3:

| Property / Feature          | Current Coverage | Needed |
|-----------------------------|-----------------|--------|
| `border-width` shorthand    | Partial         | Full 4-value expansion + cascade |
| `cm` unit conversion        | Unknown         | 96 DPI conversion test |
| `hsla()` colour parsing     | None            | Full parsing test |
| `text-shadow`               | None            | Render test with offset + colour |
| `@font-face` loading        | None            | Local file loading test |
| `::after` / `::before`      | None            | Content generation + positioning |
| `display: inline-block`     | Partial         | With `vertical-align` in em units |
| Negative margins            | Partial         | Collapsing with borders |

### 6.3 End-to-End Score Test

Add a test that runs the full Acid3 harness and asserts:

- JavaScript score ≥ 98 (current baseline).
- No fatal script execution errors.
- Score display contains "/" separator.

This already partially exists in `PhaseE_Acid3_Score_At_Least_100` but should
be kept up to date as fixes land.

### 6.4 CI Pipeline

Consider adding the pixel-comparison pipeline to CI:

```yaml
# .github/workflows/acid3-pixel-test.yml
name: Acid3 Pixel Test
on: [push, pull_request]
jobs:
  acid3:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - uses: actions/setup-python@v5
        with: { python-version: '3.12' }
      - run: pip install Pillow numpy
      - run: bash scripts/acid3-pixel-test.sh --skip-reference
      - uses: actions/upload-artifact@v4
        with:
          name: acid3-results
          path: |
            acid/acid3/acid3.png
            acid/acid3/acid3-diff.png
            acid/acid3/acid3-report.txt
```

---

## 7. Fidelity Targets and Milestones

| Milestone | Content-Area Match | Key Fixes                              |
|-----------|-------------------|----------------------------------------|
| M0        | 0.43% (current)   | Baseline — no rendering fixes          |
| M1        | ≥ 15%             | D1 (root background), D2 (border layout), D3 (viewport) |
| M2        | ≥ 40%             | + D4 (slash), D5 (!important), D6 (text spacing) |
| M3        | ≥ 70%             | + D7 (fonts), D10 (HSLA), D11 (inline-block) |
| M4        | ≥ 90%             | + D8 (@font-face), D9 (pseudo-elements), D12–D16 |
| M5        | ≥ 99%             | Full ACID3 pixel-perfect compliance     |

Each milestone should be validated by re-running the pixel comparison pipeline
and updating this document with the new match percentage.

---

*This roadmap should be reviewed and expanded as new rendering differences are
discovered. Update the TODO status and match percentages after each round of
fixes.*

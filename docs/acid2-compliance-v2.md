# Acid2 Compliance Report — Version 2

> **Version:** 2.2
> **Date:** 2026-03-07
> **Supersedes:** All previous Acid2 compliance documentation (including `acid/acid2/acid2-compliance-roadmap.md`)

---

## Summary

| Metric | Value |
|---|---|
| **Content-area pixel match** | **8.97%** (3,861 / 43,065 content pixels) |
| Content bounding-box pixel match | 42.24% (15,965 / 37,800 pixels in face region) |
| Full-image pixel match (incl. background) | 97.70% — **misleading**: 94.5% of the image is white background that matches trivially |
| Red-pixel leak (CSS failure indicator) | **0** in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |
| Content bounding box (Chromium) | x: [72, 240], y: [51, 276] — 168 × 225 px |
| Render target | `acid2.html#top` (face test area) |
| Automated test status | **All 5 differential tests passing** |
| Chromium version | 145.0.7632.6 (Playwright v1.58.2) |
| Last verified | 2026-03-07 |

### Current State: Far From Compliant

Broiler's html-renderer produces a **severely broken** Acid2 face.  While
the full-image pixel match is 97.70%, this is entirely misleading — 94.5% of
both images is plain white background.  When comparing only the content
pixels (any pixel that is non-white in either render), **only 8.97% match**.

Visual inspection confirms: the nose is malformed, the smile is broken, and
the overall face structure is wrong.  The renderer is **not close** to Acid2
compliance.

As of Phase 5.4, all red pixel leaks have been eliminated (0 remaining).
The eyes region renders with correct layout (Phase 5.3) and absolutely
positioned elements now use CSS `top`/`left` properties correctly (Phase 5.4).

---

## 1  Methodology

### 1.1  Broiler CLI Render

The Acid2 test page is rendered at the `#top` anchor using the Broiler CLI:

```bash
dotnet run --project src/Broiler.Cli -- \
  --capture-image "file://$(pwd)/acid/acid2/acid2.html#top" \
  --output acid/acid2/acid2.png \
  --width 1024 --height 768
```

The CLI supports URL fragments: when a `#fragment` is present, the renderer
lays out the full page with a tall viewport (99,999 px), locates the anchor
element via `HtmlContainer.GetElementRectangle`, and renders the 1024×768
viewport starting at the anchor's Y position.

Output: `acid/acid2/acid2.png`

### 1.2  Chromium / Playwright Reference Render

The same page is rendered in Chromium (standards-compliant browser) using
Playwright:

```bash
node -e "
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch({ headless: true });
  const p = await b.newPage();
  await p.setViewportSize({ width: 1024, height: 768 });
  const acid2 = 'file://' + path.resolve('acid/acid2/acid2.html') + '#top';
  await p.goto(acid2, { waitUntil: 'networkidle' });
  await p.waitForTimeout(500);
  await p.screenshot({ path: 'acid/acid2/acid2-reference.png' });
  await b.close();
})();
"
```

Output: `acid/acid2/acid2-reference.png`

### 1.3  Comparison

Both images are compared using two complementary approaches:

- **Full-image pixel diff:** Compares every pixel at position (x,y) in the
  Broiler render against (x,y) in the Chromium reference.  This produces the
  "95% match" number, but **this metric is misleading** because the vast
  majority of both images is white background.  The automated test suite
  (`PixelDiffRunner`) uses this method as a regression guard.
- **Content-area pixel diff:** Isolates pixels where at least one RGB channel
  is below 250 (i.e., not near-white) in *either* image, then compares only
  those.  Near-white is defined as all channels > 250, a threshold chosen to
  exclude the plain white background while tolerating minor anti-aliasing.
  This produces the **8.97% content match** — the honest measure of how well
  the Acid2 face is rendered.
- **Content bounding-box diff:** Crops both images to the Chromium
  reference's content bounding box (x: 72–240, y: 51–276, 168 × 225 px) and
  compares pixel-by-pixel.  This gives **42.24% match** within the face
  region (including internal white areas like the face background).
- **Visual diff heatmap:** `acid/acid2/acid2-diff.png` shows red = different
  pixels, green = matching pixels, blended 50/50 with the Broiler render.

### 1.4  Automated Tests

Five differential tests in `Acid2DifferentialTests.cs` guard against
regressions:

```bash
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal
```

| Test | What It Checks |
|---|---|
| `Acid2Top_PixelMatch_MeetsMinimumThreshold` | Full-image pixel match ≥ 95% (inflated by background) |
| `Acid2Top_RedPixelLeak_BelowMaximum` | Red pixels ≤ 2,000 |
| `Acid2Top_RenderDimensions_MatchViewport` | Output is 1024 × 768 |
| `Acid2Top_Render_IsDeterministic` | Two renders produce identical output |
| `Acid2Top_AnchorElement_IsFoundDuringLayout` | `#top` anchor found with Y > 100 |

**Note:** These tests use full-image pixel comparison, which is dominated by
white background.  They serve as regression guards but do not measure true
content-area compliance.

---

## 2  Image Comparison

| Image | Description |
|---|---|
| `acid2.png` | Broiler CLI render at `acid2.html#top` |
| `acid2-reference.png` | Chromium (Playwright) reference screenshot at `acid2.html#top` |
| `acid2-diff.png` | Pixel-diff heatmap (red = different, green = matching) |

All images are located in `acid/acid2/`.

### 2.1  Overall Comparison

| Metric | Value |
|---|---|
| Full-image pixel match | 95.01% (747,161 / 786,432) — **inflated by 94.5% white background** |
| Content-pixel match | **8.97%** (3,861 / 43,065 non-white pixels) |
| Content bounding-box match | **42.24%** (15,965 / 37,800 pixels in face region) |
| Different content pixels | 39,204 |
| Red-pixel leak | 1,680 (Broiler), 0 (Chromium) |

**Visual assessment:** The Broiler render is severely broken.  The overall
face outline is vaguely recognisable but nearly every facial feature differs
from the reference: eyes are missing, the nose is malformed with red
bleed-through, the forehead shape is wrong, the smile is broken, and there
are large areas of incorrect color.

### 2.3  Region-Level Diff Summary

Region-level mismatch percentages below are computed over the full image
width (1024 px) at each Y range, so they are diluted by white background
on the right side of each row.  The actual content-area mismatch within each
region is much higher.

| Region | Y Range | Row Mismatch | Red Leak | CSS Features Tested |
|---|---|---|---|---|
| Hello World! text | 0–50 | 0.0% | 0 px | font, margin, color |
| Scalp | 50–120 | 2.3% | 0 px | `position:fixed`, `min-height`/`max-height` |
| 2nd line / ears | 120–160 | 7.0% | 96 px | attribute selectors, `float`, shrink-wrap |
| Forehead | 160–195 | 15.9% | 0 px | `width`, `overflow`, `background-image` data-URI |
| Eyes | 195–235 | TBD | ~~1,254~~ 0 px | paint order (Appendix E), `background:fixed`, `<object>` fallback — **Phase 5.3 complete** |
| Nose | 200–310 | 21.3% | 1,584 px | `float`, auto margins, `::before`/`::after` |
| Smile | 310–360 | 4.2% | 0 px | margin collapsing, `clear`, negative clearance, `position:relative` |
| Chin | 360–395 | 5.1% | 0 px | `line-height`, `display:inline`, data-URI background |
| Parser area | 395–430 | 0.8% | 0 px | CSS comment parsing, error recovery, cascade |
| Table bottom | 430–470 | 0.0% | 0 px | `display:table`, anonymous table cells |
| Background (right) | 470+ | 0.0% | 0 px | overflow clipping |

### 2.4  Diff Pixel Color Distribution (Broiler-side)

| Color | Pixel Count | Likely Cause |
|---|---|---|
| Black | 5,546 | Border/outline/text misposition — layout offsets |
| White | 14,744 | Missing content or over-clipping |
| Red | 1,680 | CSS failure indicator — stacking or pseudo-element gaps |
| Yellow | 16,392 | Background fill misposition or sizing |
| Other | 909 | Anti-aliasing or blended colours |
| **Total** | **39,271** | |

### 2.5  Improvement Since v1

These numbers use the full-image metric for comparability with v1, but
remember: both are inflated by white-background matching.

| Metric | v1 (2026-03-05) | v2 (2026-03-06) | Change |
|---|---|---|---|
| Full-image pixel match | 90.91% | 95.01% | +4.10 pp |
| Different pixels | 71,456 | 39,271 | −32,185 |
| Red-pixel leak | 3,744 | 1,680 | −2,064 |
| Content-area match | not measured | **8.97%** | — |
| Content bbox match | not measured | **42.24%** | — |

---

## 3  Root-Cause Analysis

### 3.1  Previously Fixed Issues (Phases 0–3, Phase 5.1)

All items below have been resolved and are retained for historical context.

#### 3.1.1  External Stylesheet Not Loaded (Phase 0) ✅

The `<link rel="appendix stylesheet" href="data:text/css,...>` tag was not
loaded, leaving the `.picture { background: red }` rule unoverridden.
**Fix:** Implemented `<link>` element parsing, `data:text/css` URI support,
and cascade application.

#### 3.1.2  Red-Pixel Leak — Sibling Combinator (Phase 1) ✅

The `+` (adjacent sibling) combinator did not account for implicit `<p>`
closure caused by `<table>`.
**Fix:** Implemented `_pClosingTags` in HTML parser and fixed
`GetPreviousElementSibling()`.

#### 3.1.3  CSS Parser Error Recovery (Phase 1) ✅

Malformed declarations were not skipped correctly.
**Fix:** Proper error recovery per §4.1.7: escaped braces, malformed
`!important`, bare semicolons, `* html` filtering.

#### 3.1.4  min-height > max-height Override (Phase 2) ✅

CSS 2.1 §10.7 precedence rule was not implemented.
**Fix:** `min-height` wins when it exceeds `max-height`.

#### 3.1.5  Shrink-to-Fit Width for Abs-Pos Blocks (Phase 2) ✅

`GetMinMaxWidth()` per §10.3.7 was incomplete for abs-pos containing floats.
**Fix:** Correct shrink-to-fit calculation.

#### 3.1.6  Negative Clearance (Phase 2) ✅

`clear:both` produced zero clearance instead of negative.
**Fix:** Implemented per §8.3.1, §9.5.1.

#### 3.1.7  position:relative with Negative Bottom (Phase 2) ✅

**Fix:** Implemented per §9.4.3.

#### 3.1.8  Anonymous Table-Cell Box Generation (Phase 2) ✅

**Fix:** Completed per §17.2.1, §17.2.

#### 3.1.9  background-attachment:fixed Offset (Phase 3) ✅

**Fix:** Fixed offset for tiled images per §14.2.1.

#### 3.1.10  Paint Order (Phase 3) ✅

**Fix:** Blocks → floats → inlines per Appendix E.

#### 3.1.11  Overflow Clipping (Phase 3) ✅

**Fix:** Per §11.1.1 — clips to padding edge.

#### 3.1.12  line-height at Sub-Pixel Sizes (Phase 3) ✅

**Fix:** Per §10.8.

#### 3.1.13  `<object>` Fallback Chain (Phase 3) ✅

**Fix:** Per HTML 4.01 §13.3.

#### 3.1.14  height:0 and ActualBottom Consistency (Phase 5.1) ✅

`IsValidLength("0")` was rejected; `ActualBottom` double-counted
`border-bottom` across sibling positioning, float collision, clearance, and
`MarginBottomCollapse`.
**Fix:** Accept `"0"` as valid CSS length (§4.3.2) and eliminate border-bottom
double-counting.

### 3.2  Remaining Issues

The following issues produce the remaining 39,204 mismatched content pixels
(91% of all content) and 1,680 red pixels.  The renderer is far from
compliant — nearly every facial feature is broken.

#### 3.2.1  Eyes Region — Background Image & Stacking (~~1,584~~ 0 red px) ✅

**Location:** Eyes region (y 216–239 in viewport).

**Root cause (updated 2026-03-06):** Investigation reveals the 1,584 red
pixels previously attributed to the nose pseudo-elements actually originate
from the eyes stacking context:

- **#eyes-c** (`display:block; background:red`): 1,296 red px.  Painted in
  CSS2.1 Appendix E Step 3 (block-level).  Should be covered by #eyes-a and
  #eyes-b (Step 4, floats) but their `background: fixed url(data:...)` images
  do not render opaquely in the full Acid2 context, so the red leaks through.
- **#eyes-b** (`float:left; border-right: solid 1em red`): 288 red px.
  The red right border is intended to be hidden by #eyes-a painting above it,
  but both are floats and paint in DOM order (a before b); nothing covers b.

The `url()` wrapper stripping (Phase 5.2) is confirmed working — background
images from `data:` URIs load correctly in isolation (including with
`background-attachment: fixed`).  The issue is specific to the full Acid2
layout where the eyes `<span>` elements with `float:left` and `background:
fixed url(...)` fail to fully cover the block-level red background.

The original Phase 5.2 diagnosis (url() wrapper stripping) is confirmed
**complete** — it was fixed in a prior commit and no red pixels remain from
that specific cause.

**Phase 5.3 investigation (2026-03-07):**

1. **Three-phase painting implemented:** `PaintWalker.PaintChildren` now
   correctly splits block painting into background phase (Appendix E Step 3),
   float phase (Step 4), and foreground phase (Step 5) via
   `PaintFragmentBackgroundPhase` / `PaintFragmentForegroundPhase`.  This
   ensures inline content from blocks paints above sibling floats.

2. **Layout blocker resolved:** The `.eyes` div had `position:absolute` with
   no explicit width.  Two root causes were identified and fixed:

   **Root cause A — NaN `ActualWordSpacing`:**
   `CssBoxProperties.ActualWordSpacing` defaults to `double.NaN` and is only
   computed when `MeasureWordSpacing` runs (called from `MeasureWordsSize`).
   `GetMinMaxWidth` was called during shrink-to-fit *before* children's
   `MeasureWordsSize` had run, so `word.FullWidth` (which includes
   `ActualWordSpacing`) was `NaN`.  This propagated through
   `maxSum → preferred → stfWidth`, making the entire shrink-to-fit result
   `NaN`.  **Fix:** Added `EnsureDescendantWordsMeasured(g)` which
   recursively calls `MeasureWordsSize` on all descendant boxes before
   computing intrinsic min/max widths.

   **Root cause B — Additive block-float width accumulation:**
   `CssBoxHelper.GetMinMaxSumWords` accumulated float explicit widths
   additively with preceding block content widths.  For the `.eyes` div,
   `#eyes-a`'s inline content width (96px) was summed with `#eyes-b`'s
   explicit width (120px), producing a preferred width of 299px instead of
   the correct 144px.  **Fix:** Added `ComputeShrinkToFitWidth()` method
   which independently measures each direct child's total width (explicit
   width + borders + padding, or intrinsic width for auto-width children)
   and returns the maximum.  This correctly treats each block/float child
   as its own "line."

3. **Explicit height override fixed (CSS2.1 §10.6.3):**
   `CssBox.PerformLayoutImp` used `Math.Max(ActualBottom, Location.Y +
   borderBoxHeight)` for explicit heights, meaning `height:0` could never
   override the height computed by `CreateLineBoxes` from `line-height`.
   This caused `#eyes-b` and `#eyes-c` to be positioned 24px below `#eyes-a`
   instead of overlapping as intended.  **Fix:** Changed to direct assignment
   `ActualBottom = Location.Y + borderBoxHeight` so explicit height always
   takes precedence.  Content overflow remains visible per default
   `overflow:visible`.

**Results (Phase 5.3):**
- `.eyes` width: `NaN` → 144px ✓
- `#eyes-a` width: `NaN` → 144px ✓
- `#eyes-a` height: 24px → 0px (correct per CSS `height:0`) ✓
- `#eyes-b`, `#eyes-c` Y position: overlapping with `#eyes-a` ✓
- Red pixel leak: 1,680 → 96 (94% reduction) ✓
- Full-image pixel match: 95.01% → 96.31% ✓
- Test thresholds tightened: `MinMatchRatio` 0.95→0.96, `MaxRedPixelLeak` 2000→200

**Impact:** ~1,584 red pixels eliminated.  Remaining 96 red pixels were
resolved by Phase 5.4 (absolute positioning fix).

**CSS 2.1 reference:** Appendix E (paint order), §10.3.7 (shrink-to-fit
width), §10.6.3 (explicit height), §14.2.1 (background images).

#### 3.2.2  Nose Region — Pseudo-Elements (0 red px) ✅

**Location:** Nose region.

**Status (updated 2026-03-06):** Investigation confirms that the nose
pseudo-elements (`::before`/`::after` with border-based CSS triangles) render
**correctly** in isolation.  The 1,584 red pixels previously attributed to the
nose are actually from the eyes stacking context (see §3.2.1).  The nose
pseudo-element selectors (`.nose div div:before`, `.nose div :after`) match
correctly, border-style/color/width shorthand parsing is correct, and the
border trapezoid rendering covers the inner div's red background as intended.

**Impact:** 0 red pixels from this source.

**CSS 2.1 reference:** §12.1 (generated content), §9.5 (floats).

#### 3.2.3  Forehead — Overflow / Background Extent (0 red px)

**Location:** Forehead region (y 160–195).

**Root cause:** Data-URI `background-image` extent or `overflow:hidden` clip
rect is slightly off, producing 15.9% mismatch despite no red leak.

**Impact:** ~5,700 diff pixels.

**CSS 2.1 reference:** §11.1.1 (overflow), §14.2 (background).

#### 3.2.4  2nd-Line Ears — position:fixed Stacking (96 red px)

**Location:** 2nd line (y 156–157 in viewport).

**Root cause (resolved in Phase 5.4):** The `.eyes` div (`position:absolute;
top:5em; left:3em`) was placed at its static position (viewport y ≈ 192)
instead of the CSS-specified offset from the containing block (viewport y =
144).  The layout code only applied CSS `top`/`left` properties for
`position:fixed` elements, not for `position:absolute`.  Additionally, the
`blockquote.first.one` (`position:absolute`) had auto-height 0 instead of
12px because the BFC check in `MarginBottomCollapse` did not include
`position:absolute`, causing float children to be excluded from the height
calculation.

**Fix:** (1) After computing the static position for non-fixed elements,
override with CSS `top`/`left` values when `position:absolute` and the
values are not `auto`.  Added `FindPositionedContainingBlock()` to locate
the nearest positioned ancestor per CSS 2.1 §10.1.  (2) Added
`Position == Absolute` and `Position == Fixed` to the `isBfc` check in
`MarginBottomCollapse` so absolutely positioned elements correctly include
float children in their auto-height (CSS 2.1 §10.6.7).

**Results (Phase 5.4):**
- `.eyes` position: viewport (48, 192) → (84, 144) ✓
- `blockquote.first.one` height: 0 → 12px ✓
- Red pixel leak: 96 → **0** (100% reduction) ✓
- Full-image pixel match: 96.31% → 97.70% ✓
- Test thresholds tightened: `MinMatchRatio` 0.96→0.97, `MaxRedPixelLeak` 200→0

**Impact:** 96 red pixels eliminated.  **All red pixels now eliminated.**

**CSS 2.1 reference:** §9.9 (stacking), §9.6.1 (fixed positioning), §10.1
(containing block for absolute), §10.3.7 (absolute positioning), §10.6.7
(auto-height with floats).

#### 3.2.5  Chin — Inline Line-Height (0 red px)

**Location:** Chin region (y 360–395).

**Root cause:** `display:inline` with `font:2px/4px serif` produces a
different line-height calculation at tiny font sizes.

**Impact:** ~1,800 diff pixels.

**CSS 2.1 reference:** §10.8 (line-height).

#### 3.2.6  Smile — Margin Collapsing Precision (0 red px)

**Location:** Smile region (y 310–360).

**Root cause:** `clear:both` with negative clearance interaction produces
slightly incorrect vertical offset.  4.2% mismatch, no red.

**Impact:** ~2,200 diff pixels.

**CSS 2.1 reference:** §8.3.1 (margin collapsing), §9.5.1 (clearance).

#### 3.2.7  Scalp — position:fixed Viewport Anchor (0 red px)

**Location:** Scalp region (y 50–120).

**Root cause:** Fixed-position element not anchoring to viewport top correctly
when scrolled to `#top` — ambiguity in how "viewport" maps when rendering a
scrolled region.

**Impact:** ~1,600 diff pixels.

**CSS 2.1 reference:** §9.6.1 (fixed positioning).

---

## 4  Roadmap to Full Acid2 Compliance

### Current Compliance Level

- **Content-area pixel match: 8.97%** — the renderer fails 91% of content pixels.
- **Red-pixel leak: 0** — all red pixels eliminated by Phase 5.4.
- **Full-image pixel match: 97.70%** — up from 96.31%.
- **Visual assessment: far from compliant** — nose wrong, smile broken, but eyes now render and no red leak.

### Phase 5 — Eliminate Red Pixels (Target: 0 red pixels) ✅ Complete

Red pixels are the canonical Acid2 failure signal.  **0 remain after Phase 5.4.**

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 5.2 | **Fix background-image `url()` wrapper stripping** — Strip `url()` prefix before passing to `ImageLoadHandler.LoadImage` so `data:image/...` URIs are detected.  Add null guard in `RenderDrawImage` for `SKBitmap.Decode` returning null. | ~~1,254~~ 0 red px | §14.2.1 | S | ✅ Done |
| 5.3 | **Fix eyes stacking / background rendering** — Resolved NaN width for `.eyes` div by: (1) recursively measuring descendant word sizes before shrink-to-fit computation, (2) computing per-child max width instead of additive accumulation, (3) fixing explicit `height:0` override via direct `ActualBottom` assignment.  Red pixels from eyes region eliminated.  **Remaining red:** 96px from `p.bad` fixed stacking (Phase 5.4). | ~~1,584~~ 0 red px | §9.7, §10.3.7, §10.6.3, App. E | L | ✅ Done |
| 5.4 | **Fix absolute positioning and BFC auto-height** — (1) Applied CSS `top`/`left` for `position:absolute` elements by overriding static position with the CSS-specified offset from the positioned containing block (`FindPositionedContainingBlock`).  (2) Added `position:absolute`/`fixed` to the BFC check in `MarginBottomCollapse` so float children contribute to auto-height per §10.6.7.  Eyes position corrected from viewport (48,192) to (84,144); blockquote height from 0→12px.  All 96 remaining red pixels eliminated. | ~~96~~ 0 red px | §9.9, §10.1, §10.3.7, §10.6.7 | M | ✅ Done |

**Measurable outcome:** `Acid2Top_RedPixelLeak_BelowMaximum` passes with
`MaxRedPixelLeak = 0`.  ✅ Achieved.

### Phase 6 — Layout Precision (Target: ≥ 70% content-area match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 6.1 | **Fix forehead overflow clip rect** — Ensure `overflow:hidden` clip matches the padding-edge extent exactly when combined with data-URI `background-image`. | ~5,700 px | §11.1.1 | M | P1 |
| 6.2 | **Fix smile margin-collapsing precision** — Correct the clearance value for `clear:both` after floats with negative margins. | ~2,200 px | §8.3.1, §9.5.1 | L | P1 |
| 6.3 | **Fix ears/2nd-line layout** — Correct float shrink-wrap and attribute-selector matching for compound selectors in the 2nd-line ear region. | ~2,900 px | §10.3.7, §5.8 | M | P2 |
| 6.4 | **Fix chin inline line-height** — Correct `display:inline` line-height calculation at tiny font sizes (`font:2px/4px serif`). | ~1,800 px | §10.8 | S | P2 |
| 6.5 | **Fix scalp position:fixed viewport anchor** — Fixed-position elements should anchor to viewport top regardless of scroll position. | ~1,600 px | §9.6.1 | M | P2 |

**Measurable outcome:** Content-area pixel match ≥ 70%.  Content bounding-box
match ≥ 85%.

### Phase 7 — Visual Perfection (Target: ≥ 95% content-area match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort |
|---|---|---|---|---|
| 7.1 | **Sub-pixel anti-aliasing** — Match Chromium's sub-pixel text rendering for border edges and font glyphs. | ~1,000 px | — | L |
| 7.2 | **Remaining background-image tiling** — Verify all 2×2 fixed-position background tiles match exactly. | ~500 px | §14.2.1 | S |
| 7.3 | **Final pixel-perfect audit** — Manual pixel-by-pixel comparison of any remaining differences. | remaining | — | M |

**Measurable outcome:** Content-area pixel match ≥ 95%.  Content bounding-box
match ≥ 99%.  `MaxRedPixelLeak = 0`.

### Effort Key

- **S** = Small (< 1 day)
- **M** = Medium (1–3 days)
- **L** = Large (3–5 days)

---

## 5  How to Reproduce

### Quick Verification (Automated Tests)

```bash
# Run all 5 Acid2 differential tests (from repo root)
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal
```

### Manual Verification

```bash
# 1. Render with Broiler CLI at #top
dotnet run --project src/Broiler.Cli -- \
  --capture-image "file://$(pwd)/acid/acid2/acid2.html#top" \
  --output /tmp/acid2-broiler.png \
  --width 1024 --height 768

# 2. Render with Chromium at #top (requires: npm install playwright && npx playwright install chromium)
node -e "
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch({ headless: true });
  const p = await b.newPage();
  await p.setViewportSize({ width: 1024, height: 768 });
  const acid2 = 'file://' + path.resolve('acid/acid2/acid2.html') + '#top';
  await p.goto(acid2, { waitUntil: 'networkidle' });
  await p.waitForTimeout(500);
  await p.screenshot({ path: '/tmp/acid2-chromium.png' });
  await b.close();
})();
"

# 3. Compare (Python + Pillow + NumPy)
python3 -c "
from PIL import Image; import numpy as np
b = np.array(Image.open('/tmp/acid2-broiler.png').convert('RGBA'))[:,:,:3]
c = np.array(Image.open('/tmp/acid2-chromium.png').convert('RGBA'))[:,:,:3]
diff = np.abs(b.astype(int) - c.astype(int))
total = b.shape[0] * b.shape[1]
full_match = np.sum(np.all(diff == 0, axis=2))
# Content-area: pixels where at least one channel <= 250 in either image (not near-white)
content_mask = ~(np.all(b > 250, axis=2) & np.all(c > 250, axis=2))
content_total = np.sum(content_mask)
content_match = np.sum(content_mask & np.all(diff == 0, axis=2))
red = np.sum((b[:,:,0]>200)&(b[:,:,1]<50)&(b[:,:,2]<50))
print(f'Full-image match: {full_match/total*100:.2f}% (inflated by background)')
print(f'Content-area match: {content_match/content_total*100:.2f}% ({content_match}/{content_total})')
print(f'Red leak: {red} px')
"
```

### Test Thresholds

The automated tests use full-image pixel comparison, which is **inflated by
white background matching**.  These thresholds are regression guards only —
they do not measure actual compliance.

| Threshold | Value | Purpose |
|---|---|---|
| `MinMatchRatio` | 0.96 (96%) | Full-image regression floor (inflated by background) |
| `MaxRedPixelLeak` | 200 | Maximum allowed red pixels |
| Viewport | 1024 × 768 | Standard Acid2 test dimensions |
| `ColorTolerance` | 5 | Per-channel tolerance for pixel comparison |

**Important:** A 96% full-image match does not mean the renderer is 96%
compliant.  Only 8.97% of content pixels actually match.  Future test
improvements should add content-area-specific assertions.

---

## 6  Compliance Checklist

### Identification & Analysis

- [x] Render Acid2 test page at `#top` with Broiler CLI as full-page image (`acid2.png`)
- [x] Render Acid2 test page at `#top` with Chromium/Playwright for reference (`acid2-reference.png`)
- [x] Compare both images programmatically (pixel-diff with `PixelDiffRunner`)
- [x] Compare both images visually (diff heatmap in `acid2-diff.png`)
- [x] Document all rendering differences by region (§2 Image Comparison)
- [x] Categorize discrepancies by CSS/HTML feature (§3 Root-Cause Analysis)
- [x] Analyze root causes for each mismatch category
- [x] Verify Chromium reference matches fresh Playwright render (2026-03-06: identical)

### Completed Fixes (Phases 0–3, 5.1, 5.2, 5.3)

- [x] **Phase 0** — Load external `<link>` stylesheets (`data:text/css` URI, cascade)
- [x] **Phase 1** — Eliminate bulk red pixels (HTML parser, sibling combinator, CSS error recovery)
- [x] **Phase 2** — Layout correctness (min/max height, shrink-to-fit, negative clearance, tables)
- [x] **Phase 3** — Visual polish (fixed backgrounds, paint order, overflow clipping, line-height)
- [x] **Phase 5.1** — `height:0` / `ActualBottom` consistency fix
- [x] **Phase 5.2** — Fix `background-image` `url()` wrapper stripping
- [x] **Phase 5.3** — Fix eyes stacking / background rendering (1,584 red px → 0)
  - [x] Implement CSS2.1 Appendix E three-phase block painting in `PaintWalker`
  - [x] Fix NaN `ActualWordSpacing` in shrink-to-fit: `EnsureDescendantWordsMeasured`
  - [x] Fix additive block-float width accumulation: `ComputeShrinkToFitWidth`
  - [x] Fix CSS2.1 §10.6.3 explicit height override: direct `ActualBottom` assignment

### In Progress

- [ ] **Phase 5.4** — Fix `position:fixed` stacking for `p.bad` (96 red px)

### Remaining Work

- [ ] **Phase 6.1** — Fix forehead overflow clip rect (~5,700 px)
- [ ] **Phase 6.2** — Fix smile margin-collapsing precision (~2,200 px)
- [ ] **Phase 6.3** — Fix ears/2nd-line layout (~2,900 px)
- [ ] **Phase 6.4** — Fix chin inline line-height (~1,800 px)
- [ ] **Phase 6.5** — Fix scalp `position:fixed` viewport anchor (~1,600 px)
- [ ] **Phase 7** — Sub-pixel perfection and final audit
- [ ] Achieve 0 red-pixel leak
- [ ] Achieve ≥ 70% content-area pixel match (Phase 6 target)
- [ ] Achieve ≥ 95% content-area pixel match (Phase 7 target)
- [ ] Add content-area-specific assertions to automated tests

---

## 7  Architecture Notes

### Render Pipeline

1. **HTML Parsing** → `HtmlContainer.SetHtml(html)` builds the DOM/box tree
2. **CSS Application** → External `<link>` stylesheets, inline styles, cascade
3. **Layout** → `PerformLayout(canvas, rect)` with tall viewport for full-page measurement
4. **Anchor Scroll** → `GetElementRectangle("top")` returns anchor position
5. **Paint** → `PerformPaint(canvas, scrolledRect)` with canvas translation

### Key Files

| File | Role |
|---|---|
| `src/Broiler.Cli/CaptureService.cs` | CLI image capture, anchor/fragment support |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image/PixelDiffRunner.cs` | Deterministic pixel comparison |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/Acid2DifferentialTests.cs` | Automated regression tests |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.CSS/Core/Parse/CssParser.cs` | CSS parsing and error recovery |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.CSS/Core/Parse/CssValueParser.cs` | CSS value parsing (colors, lengths) |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Dom/Core/Dom/CssBox.cs` | Box model, layout, margin collapsing |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Dom/Core/Dom/CssLayoutEngine.cs` | Layout engine, line boxes |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Orchestration/Core/IR/PaintWalker.cs` | CSS2.1 Appendix E paint walker, three-phase block painting |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Orchestration/Core/IR/FragmentTreeBuilder.cs` | Fragment tree builder from CssBox layout tree |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Dom/Core/Utils/RenderUtils.cs` | Overflow clipping |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Rendering/Core/Handlers/ImageLoadHandler.cs` | Image loading (data-URI) |

### Known Architectural Constraints

- **`url()` wrapper in `BackgroundImage`:** `CssBox.BackgroundImage` stores
  the CSS `url(...)` wrapper.  `ImageLoadHandler.LoadImage` expects a raw URI.
  Stripping must happen before the load call.
- **`ActualBottom` inconsistency:** The property inconsistently includes
  `border-bottom` depending on the code path (inline vs block containers).
  Coordinated changes needed across all `ActualBottom`-setting paths.
- **`position:fixed` in scrolled renders:** CSS 2.1 §9.6.1 defines fixed
  positioning relative to the viewport, but when rendering a scrolled region,
  the viewport reference frame is ambiguous.
- **`NaN` width for auto-width absolutely positioned elements:** The layout
  engine (`CssBox.PerformLayoutImp`) does not compute shrink-to-fit width for
  absolutely positioned elements with `width:auto`.  `CssLayoutEngine` has a
  partial shrink-to-fit path (`ActualRight >= 90999` check) but it does not
  cover this case.  This causes `Size.Width = NaN` which cascades to child
  `Rectangles`, preventing background image and border rendering.  Affected
  in Acid2: `.eyes` div → `#eyes-a` → inline `<object>` children.  Fix
  requires implementing §10.3.7 shrink-to-fit in `PerformLayoutImp` or
  sanitising `NaN` in `FragmentTreeBuilder`.

---

## References

- [Acid2 Test](https://webstandards.org/acid2/test/)
- [Acid2 Guide — What It Tests](https://webstandards.org/acid2/guide/)
- [Wikipedia: Acid2](https://en.wikipedia.org/wiki/Acid2)
- [CSS 2.1 Specification](https://www.w3.org/TR/CSS21/)
- [Playwright Screenshot API](https://playwright.dev/docs/api/class-page#page-screenshot)
- [Acid2 Test (Wayback Machine)](https://web.archive.org/web/20201112082604/http://www.webstandards.org/action/acid2/)

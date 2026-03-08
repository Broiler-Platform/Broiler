# Acid2 Compliance Report — Version 2

> **⚠️ SUPERSEDED:** This document (v2) has been superseded by
> [Acid2 Compliance Report — Version 3](acid2-compliance-v3.md).
> Refer to the v3 document for current metrics, analysis, and roadmap.

> **Version:** 2.6
> **Date:** 2026-03-08
> **Supersedes:** All previous Acid2 compliance documentation (including `acid/acid2/acid2-compliance-roadmap.md`)

---

## Summary

| Metric | Value |
|---|---|
| **Content-area pixel match** | **62.95%** (15,349 / 24,381 content pixels) |
| Content bounding-box pixel match | TBD — re-measure after Phase 7+ fixes |
| **Full-image pixel match (incl. background)** | **98.85%** — misleading: 94.5% of the image is white background that matches trivially |
| Red-pixel leak (CSS failure indicator) | **0** in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |
| Content bounding box (Chromium) | x: [72, 240], y: [51, 276] — 168 × 225 px |
| Render target | `acid2.html#top` (face test area) |
| Automated test status | **All 15 tests passing** (6 differential + 4 margin/relpos + 5 fixed-position) |
| Chromium version | 145.0.7632.6 (Playwright v1.58.2) |
| Last verified | 2026-03-08 |

### Current State: Progressing — Phase 6.5 Complete

Broiler's html-renderer produces a **recognisable but imperfect** Acid2 face.
The content-area pixel match has improved from 8.97% to **62.95%** after
Phase 6.2–6.4 fixes.  The face outline, forehead, eyes, nose, smile bar,
and chin borders are all visible in approximately correct positions.  The
face height is 242px (reference: 225px), a 17px difference primarily from
parser/list-item rendering in the bottom face area.

Phase 6.5 investigated and resolved the `position:fixed` viewport anchor
ambiguity (CSS 2.1 §9.6.1):
1. **OffsetTop/OffsetLeft fix**: `position:fixed` children are no longer
   shifted by ancestor `position:relative` offsets.  The recursive
   `OffsetTop`/`OffsetLeft` methods now skip `Position == Fixed` children.
2. **PaintWalker validation**: confirmed that `OffsetDisplayItems` correctly
   repositions fixed elements to viewport-relative coordinates during paint.
3. **Containing block audit**: fixed elements use `GetActualLocation()` with
   viewport dimensions, not `FindPositionedContainingBlock()`.
4. **Regression tests**: 5 new tests validate viewport anchoring, ancestor
   independence, scroll stability, and BFC auto-height for fixed elements.

Phase 6.4 fixed three CSS 2.1 compliance issues:
1. **MarginTopCollapse parent-child border check** (§8.3.1): parent-child
   margins no longer collapse when the parent has non-zero border-top or
   border-bottom, matching the `.picture` transparent border in Acid2.
2. **MarginBottomCollapse relative positioning** (§9.4.3): `position:relative`
   offsets no longer leak into parent auto-height calculation.  The
   `.smile div` (bottom:-1em) was adding 12px to the smile's height.
3. **Sibling positioning relative offset** (§9.4.3): siblings of relatively
   positioned elements now use the flow-position bottom (before visual offset).

Phase 6.2 fixed the float height override for percentage heights resolving
to auto (CSS 2.1 §10.5), correcting the `.nose` float's border-box height
from 12px to 48px and improving the `.smile` clearance computation.

Phase 6.3 fixed the shrink-to-fit width for absolutely positioned
elements to include the element's own borders and padding (CSS 2.1 §10.3.7),
correcting the `.first.one` blockquote width from 48px to 96px.  Phase 6.3
completion validated attribute-selector matching for the ears/2nd-line region,
confirmed float stacking and auto-height calculations meet Acid2 requirements,
and added NaN guards in `ComputeShrinkToFitWidth()` and
`FragmentTreeBuilder.BuildFragment()` for robustness.

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
  This produces the **55.20% content match** (up from 8.97% before Phase 6)
  — the honest measure of how well the Acid2 face is rendered.
- **Content bounding-box diff:** Crops both images to the Chromium
  reference's content bounding box (x: 72–240, y: 51–276, 168 × 225 px) and
  compares pixel-by-pixel.  This gives **42.24% match** within the face
  region (including internal white areas like the face background).
- **Visual diff heatmap:** `acid/acid2/acid2-diff.png` shows red = different
  pixels, green = matching pixels, blended 50/50 with the Broiler render.

### 1.4  Automated Tests

Six differential tests in `Acid2DifferentialTests.cs` guard against
regressions:

```bash
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal
```

| Test | What It Checks |
|---|---|
| `Acid2Top_PixelMatch_MeetsMinimumThreshold` | Full-image pixel match ≥ 98% (inflated by background) |
| `Acid2Top_RedPixelLeak_BelowMaximum` | Red pixels ≤ 0 |
| `Acid2Top_ContentAreaMatch_MeetsMinimumThreshold` | Content-area pixel match ≥ 55% |
| `Acid2Top_RenderDimensions_MatchViewport` | Output is 1024 × 768 |
| `Acid2Top_Render_IsDeterministic` | Two renders produce identical output |
| `Acid2Top_AnchorElement_IsFoundDuringLayout` | `#top` anchor found with Y > 100 |

**Note:** The full-image pixel match is dominated by white background.
The content-area test (added in Phase 6.3) isolates non-white pixels to
provide a more honest compliance metric.

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
| Full-image pixel match | 98.56% (775,084 / 786,432) — **inflated by 94.5% white background** |
| Content-pixel match | **55.20%** (13,969 / 25,317 non-white pixels) |
| Content bounding-box match | TBD — re-measure after Phase 6.4+ fixes |
| Different pixels | 11,348 |
| Red-pixel leak | **0** (Broiler), 0 (Chromium) |

**Visual assessment:** The Broiler render produces a **recognisable** Acid2
face.  The scalp, forehead, eyes region, nose, smile bar, and chin borders
are all visible in approximately correct positions.  Remaining differences
are primarily vertical offset issues, font metric differences, and some
elements appearing below the face that should be hidden.

### 2.3  Region-Level Diff Summary

Region-level mismatch percentages below are computed over **content pixels
only** (non-white pixels within each Y range) for honest measurement.
Percentages represent the fraction of content pixels that differ between
Broiler and Chromium renders.

| Region | Y Range | Content Mismatch | Red Leak | CSS Features Tested |
|---|---|---|---|---|
| Hello World! text | 0–50 | 0.0% | 0 px | font, margin, color |
| Scalp | 50–120 | 74.5% | 0 px | `position:fixed`, `min-height`/`max-height` |
| 2nd line / ears | 120–160 | 14.5% | 0 px | attribute selectors, `float`, shrink-wrap |
| Forehead | 160–195 | 14.2% | 0 px | `width`, `overflow`, `background-image` data-URI |
| Eyes | 195–240 | 46.4% | 0 px | paint order (Appendix E), `background:fixed`, `<object>` fallback |
| Nose | 240–310 | 89.2% | 0 px | `float`, auto margins, `::before`/`::after` |
| Smile | 310–360 | 0.0% | 0 px | margin collapsing, `clear`, negative clearance, `position:relative` |
| Chin | 360–400 | 0.0% | 0 px | `line-height`, `display:inline`, data-URI background |
| Parser area | 400–440 | 0.0% | 0 px | CSS comment parsing, error recovery, cascade |
| Table bottom | 440–470 | 0.0% | 0 px | `display:table`, anonymous table cells |

### 2.4  Diff Pixel Color Distribution

| Metric | Count |
|---|---|
| Total diff pixels | 11,348 |
| Content diff pixels | 11,348 |
| Red-pixel leak | **0** |

The remaining 11,348 diff pixels are primarily from:
- Scalp region: `position:fixed` viewport anchor offset (1,612 px)
- Eyes region: fixed background tiling precision (3,240 px)
- Nose region: `::before`/`::after` pseudo-elements and auto margins (4,980 px)
- Forehead/ears: minor offset differences (1,516 px)

### 2.5  Improvement History

These numbers use the full-image metric for comparability with v1, but
remember: both are inflated by white-background matching.

| Metric | v1 (2026-03-05) | v2 (2026-03-06) | v2.3 (2026-03-07) | v2.4 (2026-03-07) |
|---|---|---|---|---|
| Full-image pixel match | 90.91% | 95.01% | **98.56%** | **98.56%** |
| Different pixels | 71,456 | 39,271 | **11,348** | **11,348** |
| Red-pixel leak | 3,744 | 1,680 | **0** | **0** |
| Content-area match | not measured | **8.97%** | **55.20%** | **55.20%** |
| Content bbox match | not measured | **42.24%** | TBD | TBD |
| Automated tests | — | 5 passing | 5 passing | **6 passing** |

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

The following issues produced the majority of mismatched content pixels.
After Phase 6.2/6.3, diff pixels reduced from 39,204 to 11,348 and
content-area match improved from 8.97% to 55.20%.  Red-pixel leak is 0.

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

#### 3.2.6  Smile — Margin Collapsing Precision (0 red px) ✅

**Location:** Smile region (y 310–360).

**Root cause (resolved in Phase 6.2):** The float height override in
`CssBox.PerformLayoutImp` did not check whether a percentage `height` value
resolved to `auto` (CSS 2.1 §10.5).  For the `.nose` float with
`height:60%`, the containing block (`.picture`) has auto height, so
`height:60%` resolves to auto.  However, the override treated it as an
explicit height, computing `ActualHeight = 60% × 0 = 0` and setting
`ActualBottom = Location.Y + 0 + border = Location.Y + 12`.  This
collapsed the nose's border-box from 48px (correct: 36px content from
`max-height:3em` + 12px border-bottom) to just 12px.

The incorrect float height cascaded to `CollectMaxFloatBottom` in
`CssBoxHelper.cs`, which also used the same flawed `ActualHeight` for
floats with non-auto `Height`.  The smile's `clear:both` clearance was
computed against a float bottom that was 36px too high, misplacing the
smile and all subsequent elements.

**Fix:** Added `resolveToAuto` check (matching the explicit height
constraint at §10.6.3) to both:
1. The float height override in `PerformLayoutImp` — skip override when
   percentage height resolves to auto
2. `CollectMaxFloatBottom` in `CssBoxHelper.cs` — use `ActualBottom`
   (layout-computed) instead of `ActualHeight` (CSS-specified) for
   percentage-auto heights

**Results (Phase 6.2):**
- `.nose` border-box height: 12px → 48px ✓
- `.smile` vertical position: corrected by 36px ✓
- Content-area pixel match: 8.97% → 50.52% (Phase 6.2 alone)
- Full-image pixel match: 98.39% → 98.39% (shifted content)

**Impact:** ~2,200 diff pixels corrected.

**CSS 2.1 reference:** §8.3.1 (margin collapsing), §9.5.1 (clearance),
§10.5 (percentage heights), §10.6.1 (float height).

#### 3.2.7  Scalp — position:fixed Viewport Anchor (0 red px)

**Location:** Scalp region (y 50–120).

**Root cause:** Two issues identified:
1. **OffsetTop/OffsetLeft bug (fixed):** The recursive `OffsetTop()` and
   `OffsetLeft()` methods in `CssBox.cs` shifted ALL child elements —
   including `position:fixed` children — when a parent applied a
   `position:relative` visual offset.  Per CSS 2.1 §9.6.1, fixed-position
   elements are positioned relative to the viewport and must not be affected
   by ancestor positioning.  **Fix:** Skip children with `Position == Fixed`
   in the recursive offset methods.
2. **Remaining Acid2 difference (unresolved):** The ~1,600 diff pixels in the
   scalp region are primarily from font metrics and the 17px overall face
   height difference (Broiler 242px vs reference 225px), not from
   `position:fixed` placement.  The Acid2 `p` and `p.bad` fixed elements are
   direct children of the root, so the OffsetTop fix does not change
   their positions.

**Impact:** General CSS compliance fix.  ~1,600 Acid2 diff pixels remain
(attributable to font metrics / vertical offset, tracked in Phase 7).

**CSS 2.1 reference:** §9.6.1 (fixed positioning), §9.4.3 (relative
positioning visual-only offset).

---

## 4  Roadmap to Full Acid2 Compliance

### Current Compliance Level

- **Content-area pixel match: 62.95%** — up from 8.97% after Phase 6.2/6.3/6.4 fixes.
- **Red-pixel leak: 0** — all red pixels eliminated by Phase 5.4.
- **Full-image pixel match: 98.85%** — up from 98.39%.
- **Automated tests: 15 passing** — 6 differential + 4 margin/relpos + 5 fixed-position.
- **Visual assessment: recognisable face** — scalp, forehead, eyes, nose, smile, chin borders visible. Remaining issues are vertical offsets and font metrics.

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
| 6.1 | **Fix forehead overflow clip rect** — Fixed negative-margin collapsing in `MarginTopCollapse` (CSS 2.1 §8.3.1): replaced `Math.Max(prev, cur)` with the general formula `max(positives,0) + min(negatives,0)`.  This correctly handles the `.nose` float's `margin-top: -2em` against the `.forehead`'s `margin-bottom: 4em`, moving the nose up 24 px and filling the gap in the face.  Pixel match improved from 97.70% → 98.39%.  Diff pixels reduced from ~18,056 → 12,675 (~5,381 px). | ~~5,700~~ 5,381 px | §8.3.1 | M | ✅ Done |
| 6.2 | **Fix smile margin-collapsing precision** — Float height override for percentage heights resolving to auto incorrectly collapsed the `.nose` border-box from 48px to 12px.  Fixed by adding `resolveToAuto` check to both the float height override in `PerformLayoutImp` and `CollectMaxFloatBottom` in `CssBoxHelper.cs`.  Smile vertical position corrected by 36px.  Content-area match improved from 8.97% → 50.52%. | ~~2,200~~ fixed | §8.3.1, §9.5.1, §10.5 | L | ✅ Done |
| 6.3 | **Fix ears/2nd-line layout** — Shrink-to-fit width for abs-pos elements now adds the element's own borders/padding (§10.3.7).  Blockquote width corrected from 48px to 96px.  Content-area match improved from 50.52% → 55.20%.  Phase 6.3 completion: validated attribute-selector matching (`[class~=one].first.one`, `[class=second\ two]`) and float interactions for the ears region; confirmed shrink-to-fit border-box width is correct; added NaN guards in `ComputeShrinkToFitWidth` and `FragmentTreeBuilder`; added content-area match test assertion. | ~~2,900~~ partially fixed | §10.3.7, §5.8 | M | ✅ Done |
| 6.4 | **Fix margin collapsing and relative positioning** — Three CSS 2.1 compliance fixes: (1) Parent-child margins no longer collapse when parent has non-zero border (§8.3.1); (2) `position:relative` offset excluded from parent auto-height in `MarginBottomCollapse` (§9.4.3); (3) siblings use flow-position bottom for layout.  Content-area match improved from 55.20% → 62.95%.  Face height reduced from 254px → 242px (reference: 225px).  Added 4 regression tests. | ~~1,800~~ fixed | §8.3.1, §9.4.3 | M | ✅ Done |
| 6.5 | **Fix scalp position:fixed viewport anchor** — (1) Fixed `OffsetTop`/`OffsetLeft` in `CssBox.cs` to skip `position:fixed` children, preventing ancestor `position:relative` offsets from incorrectly shifting fixed elements (CSS 2.1 §9.6.1).  (2) Validated PaintWalker correctly offsets fixed elements by viewport coordinates during paint.  (3) Audited containing block determination: fixed elements use `GetActualLocation()` with viewport dimensions, not `FindPositionedContainingBlock()`.  (4) Reviewed prior phases for missed dependencies — none found.  Remaining ~1,600 px Acid2 difference is from font metrics and overall vertical offset of the face structure, not from position:fixed placement.  Added 5 regression tests. | ~1,600 px (general fix, Acid2 pixels unchanged) | §9.6.1 | M | ✅ Done |

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
| `MinMatchRatio` | 0.988 (98.8%) | Full-image regression floor (inflated by background) |
| `MaxRedPixelLeak` | 0 | Maximum allowed red pixels |
| `MinContentMatchRatio` | 0.62 (62%) | Content-area pixel match floor |
| Viewport | 1024 × 768 | Standard Acid2 test dimensions |
| `ColorTolerance` | 5 | Per-channel tolerance for pixel comparison |

**Important:** A 98.8% full-image match does not mean the renderer is 98.8%
compliant.  62.95% of content pixels actually match (up from 8.97%).
The content-area assertion (`MinContentMatchRatio`) was added in Phase 6.3 to
provide a more honest compliance metric.

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

### Completed Fixes (Phases 0–3, 5.1–5.4, 6.1–6.5)

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
- [x] **Phase 5.4** — Fix `position:absolute` top/left and BFC auto-height (96 red px → 0)
- [x] **Phase 6.1** — Fix forehead negative-margin collapsing (98.39% match)
- [x] **Phase 6.2** — Fix smile margin-collapsing precision (content match 8.97% → 50.52%)
  - [x] Add `resolveToAuto` check to float height override in `PerformLayoutImp`
  - [x] Add `resolveToAuto` check to `CollectMaxFloatBottom` in `CssBoxHelper.cs`
  - [x] `.nose` border-box height corrected: 12px → 48px
- [x] **Phase 6.3** — Fix ears shrink-to-fit and validate attribute-selector matching (content match → 55.20%)
  - [x] Add borders/padding to shrink-to-fit width for abs-pos elements
  - [x] Validate attribute-selector matching: `[class~=one].first.one`, `[class=second\ two]`
  - [x] Validate float interactions within shrink-to-fit ears region
  - [x] Add NaN guards in `ComputeShrinkToFitWidth` and `FragmentTreeBuilder`
  - [x] Add content-area match assertion to automated test suite
- [x] **Phase 6.4** — Fix margin collapsing and relative positioning (content match 55.20% → 62.95%)
  - [x] Parent-child margin collapsing blocked by non-zero parent border (§8.3.1)
  - [x] `position:relative` offset excluded from parent auto-height (§9.4.3)
  - [x] Siblings use flow-position bottom for layout (§9.4.3)
  - [x] Added 4 margin-collapsing / relative-positioning regression tests
- [x] **Phase 6.5** — Fix scalp `position:fixed` viewport anchor (§9.6.1)
  - [x] Investigate ambiguity in fixed positioning mapped in scrolled renders (`#top` anchor)
  - [x] Validate PaintWalker places `position:fixed` elements at correct viewport-relative Y
  - [x] Fix `OffsetTop`/`OffsetLeft` to skip `position:fixed` children
  - [x] Audit containing block determination: fixed uses viewport, not positioned ancestor
  - [x] Review prior phases (6.1–6.4) for missed dependencies — none found
  - [x] Added 5 fixed-position regression tests

### Remaining Work

- [ ] **Phase 7** — Sub-pixel perfection and final audit
- [x] Achieve 0 red-pixel leak ✓
- [ ] Achieve ≥ 70% content-area pixel match (Phase 6 target — currently 62.95%)
- [ ] Achieve ≥ 95% content-area pixel match (Phase 7 target)
- [x] Add content-area-specific assertions to automated tests ✓

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
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/FixedPositionTests.cs` | Position:fixed viewport anchor regression tests |
| `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/MarginCollapsingTests.cs` | Margin collapsing / relative positioning regression tests |
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
  positioning relative to the viewport.  ~~When rendering a scrolled region,
  the viewport reference frame is ambiguous.~~  **Resolved in Phase 6.5.**
  Fixed elements use `GetActualLocation()` with viewport dimensions for
  layout positioning, and PaintWalker's `OffsetDisplayItems()` repositions
  them to viewport-relative coordinates during paint.  The
  `OffsetTop`/`OffsetLeft` methods skip `position:fixed` children to prevent
  ancestor `position:relative` offsets from incorrectly shifting fixed
  elements.
- **`NaN` width for auto-width absolutely positioned elements:** ~~The layout
  engine (`CssBox.PerformLayoutImp`) does not compute shrink-to-fit width for
  absolutely positioned elements with `width:auto`.~~  **Resolved in Phase 5.3
  and 6.3.**  Shrink-to-fit width is now computed via `ComputeShrinkToFitWidth()`
  in `PerformLayoutImp`.  NaN guards added in Phase 6.3 protect against
  edge cases where `GetMinMaxWidth` returns NaN from deeply nested inline
  elements.  `FragmentTreeBuilder` sanitises any remaining NaN widths by
  falling back to `ActualRight - Location.X`.

---

## References

- [Acid2 Test](https://webstandards.org/acid2/test/)
- [Acid2 Guide — What It Tests](https://webstandards.org/acid2/guide/)
- [Wikipedia: Acid2](https://en.wikipedia.org/wiki/Acid2)
- [CSS 2.1 Specification](https://www.w3.org/TR/CSS21/)
- [Playwright Screenshot API](https://playwright.dev/docs/api/class-page#page-screenshot)
- [Acid2 Test (Wayback Machine)](https://web.archive.org/web/20201112082604/http://www.webstandards.org/action/acid2/)

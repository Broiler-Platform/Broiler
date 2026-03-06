# Acid2 Compliance Report — Version 2

> **Version:** 2.0
> **Date:** 2026-03-06
> **Supersedes:** All previous Acid2 compliance documentation (including `acid/acid2/acid2-compliance-roadmap.md`)

---

## Summary

| Metric | Value |
|---|---|
| Overall pixel match (at `#top`) | **95.01%** |
| Different pixels | 39,271 / 786,432 |
| Red-pixel leak (CSS failure indicator) | 1,680 in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |
| Render target | `acid2.html#top` (face test area) |
| Automated test status | **All 5 differential tests passing** |
| Test thresholds | MinMatchRatio = 0.95, MaxRedPixelLeak = 2,000 |
| Chromium version | 145.0.7632.6 (Playwright v1.58.2) |
| Last verified | 2026-03-06 (Chromium reference pixel-identical to fresh Playwright render) |

Broiler's html-renderer produces a recognisable Acid2 face when rendered at
the `#top` anchor, matching the Chromium reference at **95.01%**.  Prior fix
phases (P0–P3) and Phase 5 item 5.1 addressed external stylesheet loading,
red-pixel elimination, layout correctness, visual polish, and CSS `height:0`
/ `ActualBottom` consistency.

The remaining **4.99%** pixel difference (39,271 pixels) and **1,680 red
pixels** come from layout positioning errors, missing CSS features, and
rendering precision gaps described in the root-cause analysis and roadmap
below.

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

Both images are compared:

- **Programmatically** via the `PixelDiffRunner` (SkiaSharp-based) in the
  automated test suite, and independently with NumPy/Pillow for validation.
- **Visually** via a diff heatmap (`acid/acid2/acid2-diff.png`) where
  red = different pixels, green = matching pixels, blended 50/50 with the
  Broiler render for spatial context.

### 1.4  Automated Tests

Five differential tests in `Acid2DifferentialTests.cs` guard against
regressions:

```bash
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal
```

| Test | What It Checks |
|---|---|
| `Acid2Top_PixelMatch_MeetsMinimumThreshold` | Pixel match ≥ 95% |
| `Acid2Top_RedPixelLeak_BelowMaximum` | Red pixels ≤ 2,000 |
| `Acid2Top_RenderDimensions_MatchViewport` | Output is 1024 × 768 |
| `Acid2Top_Render_IsDeterministic` | Two renders produce identical output |
| `Acid2Top_AnchorElement_IsFoundDuringLayout` | `#top` anchor found with Y > 100 |

---

## 2  Image Comparison

| Image | Description |
|---|---|
| `acid2.png` | Broiler CLI render at `acid2.html#top` |
| `acid2-reference.png` | Chromium (Playwright) reference screenshot at `acid2.html#top` |
| `acid2-diff.png` | Pixel-diff heatmap (red = different, green = matching) |

All images are located in `acid/acid2/`.

### 2.1  Region-Level Diff Summary

| Region | Y Range | Mismatch | Red Leak | CSS Features Tested |
|---|---|---|---|---|
| Hello World! text | 0–50 | **0.0%** | 0 px | font, margin, color |
| Scalp | 50–120 | 2.3% | 0 px | `position:fixed`, `min-height`/`max-height` |
| 2nd line / ears | 120–160 | 7.0% | 96 px | attribute selectors, `float`, shrink-wrap |
| Forehead | 160–195 | 15.9% | 0 px | `width`, `overflow`, `background-image` data-URI |
| Eyes | 195–235 | 22.4% | 1,254 px | paint order (Appendix E), `background:fixed`, `<object>` fallback |
| Nose | 200–310 | 21.3% | 1,584 px | `float`, auto margins, `::before`/`::after` |
| Smile | 310–360 | 4.2% | 0 px | margin collapsing, `clear`, negative clearance, `position:relative` |
| Chin | 360–395 | 5.1% | 0 px | `line-height`, `display:inline`, data-URI background |
| Parser area | 395–430 | 0.8% | 0 px | CSS comment parsing, error recovery, cascade |
| Table bottom | 430–470 | 0.0% | 0 px | `display:table`, anonymous table cells |
| Background (right) | 470+ | 0.0% | 0 px | overflow clipping |

### 2.2  Diff Pixel Color Distribution (Broiler-side)

| Color | Pixel Count | Likely Cause |
|---|---|---|
| Black | 5,546 | Border/outline/text misposition — layout offsets |
| White | 14,744 | Missing content or over-clipping |
| Red | 1,680 | CSS failure indicator — stacking or pseudo-element gaps |
| Yellow | 16,392 | Background fill misposition or sizing |
| Other | 909 | Anti-aliasing or blended colours |
| **Total** | **39,271** | |

### 2.3  Improvement Since v1

| Metric | v1 (2026-03-05) | v2 (2026-03-06) | Change |
|---|---|---|---|
| Pixel match | 90.91% | 95.01% | **+4.10 pp** |
| Different pixels | 71,456 | 39,271 | **−32,185** |
| Red-pixel leak | 3,744 | 1,680 | **−2,064** |
| Black diff pixels | 38,367 | 5,546 | **−32,821** |
| White diff pixels | 17,809 | 14,744 | **−3,065** |
| Yellow diff pixels | 13,845 | 16,392 | +2,547 |

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

The following issues produce the remaining 39,271 diff pixels and 1,680 red
pixels.

#### 3.2.1  Eyes Region — Background Image Loading (1,254 red px)

**Location:** Eyes region (y 195–235).

**Root cause:** The `background` shorthand stores `url(data:image/...)` with
the `url()` wrapper.  `ImageLoadHandler.LoadImage` checks
`src.StartsWith("data:image")` which fails because of the `url()` prefix.
Additionally, `RenderDrawImage` lacks a null guard when `SKBitmap.Decode`
returns null.

**Impact:** ~8,000 diff pixels, 1,254 red pixels.

**CSS 2.1 reference:** Appendix E (paint order), §14.2.1 (background images).

#### 3.2.2  Nose Region — Pseudo-Element Positioning (1,584 red px)

**Location:** Nose region (y 200–310).

**Root cause:** `::before`/`::after` pseudo-elements with border-based CSS
triangles are not fully positioned.  Remaining layout offsets cause red
background to bleed through.

**Impact:** ~15,000 diff pixels, 1,584 red pixels.

**CSS 2.1 reference:** §12.1 (generated content), §9.5 (floats).

#### 3.2.3  Forehead — Overflow / Background Extent (0 red px)

**Location:** Forehead region (y 160–195).

**Root cause:** Data-URI `background-image` extent or `overflow:hidden` clip
rect is slightly off, producing 15.9% mismatch despite no red leak.

**Impact:** ~5,700 diff pixels.

**CSS 2.1 reference:** §11.1.1 (overflow), §14.2 (background).

#### 3.2.4  2nd-Line Ears — position:fixed Stacking (96 red px)

**Location:** 2nd line (y 120–160).

**Root cause:** The `p.bad` element with `border-bottom:red` is
`position:fixed` and should be hidden behind the face content in stacking
order.  96 red pixels leak through.

**Impact:** ~2,900 diff pixels, 96 red pixels.

**CSS 2.1 reference:** §9.9 (stacking), §9.6.1 (fixed positioning).

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

### Phase 5 — Eliminate Red Pixels (Target: 0 red pixels)

Red pixels are the canonical Acid2 failure signal.  Eliminating all 1,680
is the primary gate to passing.

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 5.2 | **Fix background-image `url()` wrapper stripping** — Strip `url()` prefix before passing to `ImageLoadHandler.LoadImage` so `data:image/...` URIs are detected.  Add null guard in `RenderDrawImage` for `SKBitmap.Decode` returning null. | ~1,254 red px | §14.2.1 | S | 🔴 P0 |
| 5.3 | **Fix nose pseudo-element positioning** — Correct `::before`/`::after` border-triangle layout for floated elements with `height:0` borders. | ~1,584 red px | §12.1, §9.5 | L | 🔴 P0 |
| 5.4 | **Fix position:fixed stacking for p.bad** — Ensure `position:fixed` elements with `border-bottom:red` are covered by subsequently-positioned content per Appendix E stacking order. | ~96 red px | §9.9, App. E | M | 🔴 P0 |

**Measurable outcome:** `Acid2Top_RedPixelLeak_BelowMaximum` passes with
`MaxRedPixelLeak = 0`.

### Phase 6 — Layout Precision (Target: ≥ 98% match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 6.1 | **Fix forehead overflow clip rect** — Ensure `overflow:hidden` clip matches the padding-edge extent exactly when combined with data-URI `background-image`. | ~5,700 px | §11.1.1 | M | P1 |
| 6.2 | **Fix smile margin-collapsing precision** — Correct the clearance value for `clear:both` after floats with negative margins. | ~2,200 px | §8.3.1, §9.5.1 | L | P1 |
| 6.3 | **Fix ears/2nd-line layout** — Correct float shrink-wrap and attribute-selector matching for compound selectors in the 2nd-line ear region. | ~2,900 px | §10.3.7, §5.8 | M | P2 |
| 6.4 | **Fix chin inline line-height** — Correct `display:inline` line-height calculation at tiny font sizes (`font:2px/4px serif`). | ~1,800 px | §10.8 | S | P2 |
| 6.5 | **Fix scalp position:fixed viewport anchor** — Fixed-position elements should anchor to viewport top regardless of scroll position. | ~1,600 px | §9.6.1 | M | P2 |

**Measurable outcome:** `Acid2Top_PixelMatch_MeetsMinimumThreshold` passes
with `MinMatchRatio = 0.98`.

### Phase 7 — Visual Perfection (Target: ≥ 99.5% match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort |
|---|---|---|---|---|
| 7.1 | **Sub-pixel anti-aliasing** — Match Chromium's sub-pixel text rendering for border edges and font glyphs. | ~1,000 px | — | L |
| 7.2 | **Remaining background-image tiling** — Verify all 2×2 fixed-position background tiles match exactly. | ~500 px | §14.2.1 | S |
| 7.3 | **Final pixel-perfect audit** — Manual pixel-by-pixel comparison of any remaining differences. | remaining | — | M |

**Measurable outcome:** `MinMatchRatio = 0.995` and `MaxRedPixelLeak = 0`.

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
d = np.sqrt(np.sum((b.astype(float) - c.astype(float))**2, axis=2))
total = d.size
match = np.sum(d == 0)
red = np.sum((b[:,:,0]>200)&(b[:,:,1]<50)&(b[:,:,2]<50))
print(f'Match: {match/total*100:.2f}% ({match}/{total})')
print(f'Diff: {total-match} pixels')
print(f'Red leak: {red} px')
"
```

### Test Thresholds

| Threshold | Value | Purpose |
|---|---|---|
| `MinMatchRatio` | 0.95 (95%) | Minimum pixel match floor |
| `MaxRedPixelLeak` | 2,000 | Maximum allowed red pixels |
| Viewport | 1024 × 768 | Standard Acid2 test dimensions |
| `ColorTolerance` | 5 | Per-channel tolerance for pixel comparison |

These thresholds are regression guards.  As rendering improves, raise
`MinMatchRatio` and lower `MaxRedPixelLeak` accordingly.

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

### Completed Fixes (Phases 0–3, 5.1)

- [x] **Phase 0** — Load external `<link>` stylesheets (`data:text/css` URI, cascade)
- [x] **Phase 1** — Eliminate bulk red pixels (HTML parser, sibling combinator, CSS error recovery)
- [x] **Phase 2** — Layout correctness (min/max height, shrink-to-fit, negative clearance, tables)
- [x] **Phase 3** — Visual polish (fixed backgrounds, paint order, overflow clipping, line-height)
- [x] **Phase 5.1** — `height:0` / `ActualBottom` consistency fix

### Remaining Work

- [ ] **Phase 5.2** — Fix `background-image` `url()` wrapper stripping (1,254 red px)
- [ ] **Phase 5.3** — Fix nose pseudo-element positioning (1,584 red px)
- [ ] **Phase 5.4** — Fix `position:fixed` stacking for `p.bad` (96 red px)
- [ ] **Phase 6.1** — Fix forehead overflow clip rect (~5,700 px)
- [ ] **Phase 6.2** — Fix smile margin-collapsing precision (~2,200 px)
- [ ] **Phase 6.3** — Fix ears/2nd-line layout (~2,900 px)
- [ ] **Phase 6.4** — Fix chin inline line-height (~1,800 px)
- [ ] **Phase 6.5** — Fix scalp `position:fixed` viewport anchor (~1,600 px)
- [ ] **Phase 7** — Sub-pixel perfection and final audit
- [ ] Achieve 0 red-pixel leak
- [ ] Achieve ≥ 98% pixel match
- [ ] Achieve ≥ 99.5% pixel match (stretch goal)

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

---

## References

- [Acid2 Test](https://webstandards.org/acid2/test/)
- [Acid2 Guide — What It Tests](https://webstandards.org/acid2/guide/)
- [Wikipedia: Acid2](https://en.wikipedia.org/wiki/Acid2)
- [CSS 2.1 Specification](https://www.w3.org/TR/CSS21/)
- [Playwright Screenshot API](https://playwright.dev/docs/api/class-page#page-screenshot)
- [Acid2 Test (Wayback Machine)](https://web.archive.org/web/20201112082604/http://www.webstandards.org/action/acid2/)

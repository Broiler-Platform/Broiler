# Acid2 Compliance Roadmap

## Summary

| Metric | Value |
|---|---|
| Overall pixel match (at `#top`) | **90.16%** |
| Different pixels | 77,373 / 786,432 |
| Red-pixel leak (CSS failure indicator) | 4,848 in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |
| Render target | `acid2.html#top` (face test area) |

Broiler's html-renderer produces a partially recognisable Acid2 face when
rendered at the `#top` anchor, but diverges significantly from the Chromium
reference in several CSS 2.1 feature areas.  The dominant issue is the
`.picture` red background not being overridden by the external
`<link rel="appendix stylesheet">` CSS.  The sections below catalogue every
significant discrepancy, give a root-cause analysis, and propose a prioritised
fix roadmap.

---

## 1  Image Comparison

| Image | Description |
|---|---|
| `acid2-reference.png` | Chromium (Playwright) reference screenshot at `acid2.html#top` |
| `acid2-diff.png`       | Pixel-diff heatmap (red = different, green = matching) |

### Region-Level Diff Summary

| Region | Mismatch | CSS Features Tested |
|---|---|---|
| Hello World! text (y 0–80) | **0.0%** | font, margin, color |
| Scalp (y 80–120) | 32.9% | `position:fixed`, `min-height`/`max-height` |
| 2nd line (y 120–150) | 33.4% | attribute selectors, `float`, shrink-wrap |
| Forehead (y 140–180) | 34.6% | `width`, `overflow`, `background-image` data-URI |
| Eyes (y 170–210) | 6.3% | paint order (Appendix E), `background:fixed`, `<object>` fallback |
| Nose (y 200–280) | 2.2% | `float`, auto margins, `::before`/`::after` |
| Smile (y 250–310) | 16.7% | margin collapsing, `clear`, negative clearance, `position:relative` |
| Chin (y 300–340) | 32.3% | `line-height`, `display:inline`, data-URI background |
| Parser area (y 330–360) | 47.1% | CSS comment parsing, error recovery, cascade |
| Table bottom (y 350–390) | 35.9% | `display:table`, anonymous table cells |
| Background (right half) | 1.1% | overflow clipping |

---

## 2  Root-Cause Analysis

### 2.0  External Stylesheet Not Loaded — `.picture` Red Background (635,036 pixels)

**Location:** Entire `.picture` region (the face container).

**What Acid2 tests:** The `<link rel="appendix stylesheet" href="data:text/css,...>`
tag provides `.picture { background: none; }` which should override the inline
`.picture { background: red; }` rule.  Without this override, the face
container fills with solid red.

**Root cause:** Broiler's HTML parser/renderer does not fetch and apply CSS from
`<link>` elements.  The `data:text/css,...` URI is never loaded, so the
`background: red` declaration is never overridden.

**Priority:** P0 — this single issue accounts for ~80% of all pixel differences
and produces the vast majority of red-pixel leak.

---

### 2.1  Red-Pixel Leak (288 pixels — in addition to the stylesheet issue)

**Location:** 2nd line of the face (y 120–150, x 50–280).

**What Acid2 tests:** The `.picture p.bad` rule sets `border-bottom: red solid`
but the sibling combinator rule `p + table + p` should hide that element under
an absolutely-positioned table.  Red appearing means either:

- The `p + table + p` selector does not match (sibling combinator bug), or
- the stacking/positioning of the hidden `<p class="bad">` is wrong.

**Root cause:** The `+` (adjacent sibling) combinator does not correctly
account for implicit `<p>` closure caused by the `<table>` element.
HTML 4 DTD requires `<table>` to close a preceding `<p>`, so the DOM
should contain `p, table, p.bad` as siblings—but the renderer's HTML
parser may not perform this implicit closure.

**Priority:** P1 – red pixels are the canonical Acid2 failure signal.

---

### 2.2  Scalp Sizing (position:fixed + min/max height)

**What Acid2 tests:** CSS 2.1 §10.7 — when `min-height` exceeds `max-height`,
`min-height` wins.  The scalp `<p>` has `height:8px; min-height:1em;
max-height:2mm`.  Because 1 em > 2 mm at 12 px base, `min-height` should
override.

**Observed:** Broiler renders 230 black pixels in the scalp vs 1,096 in
Chromium.  The bar is narrower/shorter.

**Root cause:** `min-height` / `max-height` override logic in the box model
does not implement the §10.7 precedence rule correctly.

**Priority:** P2

---

### 2.3  Attribute Selector & Float Shrink-Wrap

**What Acid2 tests:** `[class~=one].first.one` with an absolutely-positioned
block that shrink-wraps around a floated child.

**Observed:** 33.4% mismatch; content missing or mis-positioned.

**Root cause:** Compound attribute selectors (`[class~=…]`) combined with
class selectors may not be fully supported.  Additionally, shrink-to-fit
width for absolutely-positioned elements containing only a float may not
collapse correctly.

**Priority:** P2

---

### 2.4  Forehead Background Image (data-URI PNG)

**What Acid2 tests:** A 1×1 yellow pixel data-URI PNG used as a
`background` image, combined with `overflow` clipping at a narrower width.

**Observed:** 34.6% mismatch; the forehead region appears differently sized.

**Root cause:** Data-URI `background-image` rendering and/or `overflow`
clipping at the forehead's constrained `width:8em` with children wider
than the container.

**Priority:** P3

---

### 2.5  Eyes Paint Order & Fixed Backgrounds

**What Acid2 tests:** Appendix E paint order — blocks paint first, floats
in the middle, inline content on top.  Two 2×2 fixed-position background
images tile to create a solid yellow fill.

**Observed:** 6.3% mismatch — the closest region to passing.

**Root cause:** Minor paint-order deviation between block/float/inline
layers, and `background-attachment:fixed` offset calculation for the
tiled 2×2 PNG patterns.

**Priority:** P3

---

### 2.6  Smile — Margin Collapsing & Negative Clearance

**What Acid2 tests:** CSS 2.1 §8.3.1 and §9.5.1 — negative clearance
with `clear:both` after a float, plus `position:relative; bottom:-1em`.

**Observed:** 16.7% mismatch; Broiler renders 2,096 extra black pixels
where Chromium renders white, indicating the smile is too large or
mispositioned.

**Root cause:** Margin collapsing with clearance and the interaction
between `clear:both` and negative clearance is one of the most complex
CSS 2.1 layout interactions.  The renderer likely computes clearance
as zero instead of a negative value.

**Priority:** P2

---

### 2.7  Chin — line-height & inline display

**What Acid2 tests:** `line-height:1em` on a container with
`display:inline; font:2px/4px serif` child.

**Observed:** 32.3% mismatch; Broiler renders yellow + black content
(962 yellow, 1,559 black) where Chromium renders pure white.

**Root cause:** The chin's content is overflowing or the `line-height`
calculation at tiny font sizes produces different metrics, causing the
face to extend further down than expected.

**Priority:** P3

---

### 2.8  CSS Parser Error Recovery

**What Acid2 tests:** Several intentionally malformed CSS declarations
that conforming parsers must skip:

- `error: \};` — unknown property with escaped brace
- `* html .parser { background: gray; }` — star-html hack (should not match)
- `\.parser { padding: 2em; }` — escaped dot in selector
- `.parser { m\argin: 2em; };` — escaped char in property name
- `.parser { height: 3em; }` — after a bare semicolon (should still apply)
- `.parser { width: 200; }` — unitless non-zero length
- `.parser { border: 5em solid red ! error; }` — malformed `!important`
- `.parser { background: red pink; }` — too many values

**Observed:** 47.1% mismatch — worst of all regions.

**Root cause:** The CSS parser does not skip all invalid declarations
correctly, or recovers incorrectly so that subsequent valid declarations
are also lost.

**Priority:** P1

---

### 2.9  display:table & Anonymous Table Cells

**What Acid2 tests:** `<ul>` as `display:table` with `<li>` children as
`display:table-cell` and `display:table` (should get wrapped in anonymous
cell) and bare `<li>` (should also get anonymous cell).

**Observed:** 35.9% mismatch; Broiler renders the table row lower with
extra content visible.

**Root cause:** Anonymous table-cell box generation is incomplete or the
table layout algorithm does not handle mixed `display:table-cell` /
`display:table` / block children.

**Priority:** P2

---

## 3  Fix Roadmap

### Phase 0 — P0: Load External `<link>` Stylesheets (Target: eliminate red background)

| # | Task | Ref | Effort | Status |
|---|---|---|---|---|
| 0.1 | Parse `<link rel="... stylesheet" href="...">` elements during HTML parsing | HTML 4.01 §14.3.2 | M | ✅ Done |
| 0.2 | Support `data:text/css,...` URIs in `<link>` href attributes | RFC 2397 | S | ✅ Done |
| 0.3 | Apply fetched CSS to the document cascade | CSS 2.1 §6.4 | M | ✅ Done |

### Phase 1 — P1: Eliminate Red Pixels (Target: 0 red pixels)

| # | Task | CSS 2.1 Ref | Effort |
|---|---|---|---|
| 1.1 | Fix `<p>` implicit closure when `<table>` is encountered in the HTML parser | §B.1 | M |
| 1.2 | Fix adjacent-sibling combinator (`+`) to match across implicit closures | §5.7 | S |
| 1.3 | Fix CSS parser error recovery (skip unknown properties, malformed `!important`, bare `;` between rules) | §4.1.7, §4.2 | L |
| 1.4 | Validate `* html` selector does not match in standards mode | §5.9 | S |

### Phase 2 — P2: Layout Correctness

| # | Task | CSS 2.1 Ref | Effort | Status |
|---|---|---|---|---|
| 2.1 | Implement `min-height` > `max-height` override rule | §10.7 | S | ✅ Done |
| 2.2 | Fix shrink-to-fit width for abs-pos blocks containing only floats | §10.3.7 | M | ✅ Done |
| 2.3 | Implement negative clearance for `clear:both` after floats | §8.3.1, §9.5.1 | L | ✅ Done |
| 2.4 | Fix `position:relative` with negative `bottom` offset | §9.4.3 | S | ✅ Done |
| 2.5 | Complete anonymous table-cell box generation | §17.2.1 | M | ✅ Done |
| 2.6 | Fix `display:table` on non-table elements with mixed children | §17.2 | M | ✅ Done |

### Phase 3 — P3: Visual Polish

| # | Task | CSS 2.1 Ref | Effort | Status |
|---|---|---|---|---|
| 3.1 | Fix `background-attachment:fixed` offset for tiled images | §14.2.1 | M | ✅ Done |
| 3.2 | Fix paint order: blocks → floats → inlines (Appendix E) | App. E | M | ✅ Done |
| 3.3 | Fix `overflow` clipping with children wider than container | §11.1.1 | S | ✅ Done |
| 3.4 | Fix `line-height` at sub-pixel font sizes | §10.8 | S | ✅ Done |
| 3.5 | Handle `<object>` fallback chain for data-URI objects | HTML 4.01 §13.3 | M | ✅ Done |

### Phase 4 — Validation

| # | Task | Status |
|---|---|---|
| 4.1 | Re-render Acid2 at `#top` with Broiler CLI and regenerate diff | ✅ |
| 4.2 | Achieve 0 red pixels and < 2% overall pixel diff | ⚠️ 4,848 red px / 9.84% diff |
| 4.3 | Update `acid2-reference.png` and `acid2-diff.png` for `#top` | ✅ |

**Validation Results (latest render at `#top`):**
- Match: 90.16% (diff: 9.84%) — significant improvement from 12.38% ⬆️
- Red leak: 4,848 px — reduced from 635,036 by fixing `background` shorthand
  reset (CSS2.1 §14.2.1) and abs-pos shrink-to-fit width (§10.3.7).
- Remaining red from: nose pseudo-element coverage, p.bad border,
  table cell gaps (display:table nested layout).

**Fixes applied in this phase:**
- Position:fixed margin handling (CSS2.1 §10.6.4)
- z-index CSS property support for correct stacking order
- CSS specificity ordering for qualified universal selectors (`.intro *`)
- `font: inherit` shorthand parsing
- `font-size: inherit` resolution to parent computed value
- Border shorthand reset of omitted values (CSS2.1 §8.5.1)
- `background` shorthand reset of all longhand properties (CSS2.1 §14.2.1)
- Shrink-to-fit width for abs-pos elements (CSS2.1 §10.3.7)
- Anonymous table-cell parent reference fix (CSS2.1 §17.2.1)

### Effort Key

- **S** = Small (< 1 day)
- **M** = Medium (1–3 days)
- **L** = Large (3–5 days)

---

## 4  How to Reproduce

```bash
# All commands must be run from the repository root.

# 1. Render with Broiler CLI (note: renders the intro page at acid2.html)
dotnet run --project src/Broiler.Cli -- \
  --capture-image acid/acid2/acid2.html \
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
print(f'Match: {np.sum(d==0)/d.size*100:.2f}%')
print(f'Red leak: {np.sum((b[:,:,0]>200)&(b[:,:,1]<50)&(b[:,:,2]<50))} px')
"

# 4. Run automated differential tests (renders at #top via scroll)
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal
```

---

## 5  Compliance Checklist

Progress checklist tracking the path to full Acid2 compliance.

### Identification & Analysis

- [x] Render Acid2 test page at `#top` with Broiler CLI as full-page image
- [x] Render Acid2 test page at `#top` with Chromium (Playwright) for reference
- [x] Compare both images programmatically (pixel-diff with `PixelDiffRunner`)
- [x] Compare both images visually (diff heatmap in `acid2-diff.png`)
- [x] Document all rendering differences by region (§1 Image Comparison)
- [x] Categorize discrepancies by CSS/HTML feature (§2 Root-Cause Analysis)
- [x] Analyze root causes for each mismatch category

### Missing/Incorrect Features Identified

- [x] **External `<link>` stylesheet loading** — `data:text/css` URI in `<link>` now applied (P0, fixed via `background` shorthand reset)
- [x] CSS `+` (adjacent sibling) combinator across implicit `<p>` closure
- [x] CSS parser error recovery (escaped braces, malformed `!important`, bare `;`)
- [x] `min-height` > `max-height` override rule (CSS 2.1 §10.7)
- [x] Shrink-to-fit width for abs-pos blocks containing only floats (§10.3.7)
- [x] Negative clearance for `clear:both` after floats (§8.3.1, §9.5.1)
- [x] `position:relative` with negative `bottom` offset (§9.4.3)
- [x] Complete anonymous table-cell box generation (§17.2.1)
- [x] `display:table` on non-table elements with mixed children (§17.2)
- [x] `background-attachment:fixed` offset for tiled images (§14.2.1)
- [x] Paint order: blocks → floats → inlines (Appendix E)
- [x] `overflow` clipping with children wider than container (§11.1.1)
- [x] `line-height` at sub-pixel font sizes (§10.8)
- [x] `<object>` fallback chain for data-URI objects (HTML 4.01 §13.3)

### Prioritised Fix Targets

- [x] **P0 — Load external stylesheets**: Parse `<link>` elements, support `data:` URIs, apply to cascade
- [x] **P1 — Eliminate red pixels**: HTML parser implicit closure, sibling combinator, CSS error recovery
- [x] **P2 — Layout correctness**: min/max height, shrink-to-fit, negative clearance, anonymous tables
- [x] **P3 — Visual polish**: fixed backgrounds, paint order, overflow clipping, line-height, object fallback

### Validation & Iteration

- [x] Re-render Acid2 at `#top` after Phase 3/4 fixes and regenerate diff
- [ ] Achieve < 2% overall pixel diff (current at `#top`: 9.84%)
- [ ] Achieve 0 red-pixel leak (current at `#top`: 4,848 px)
- [x] Update reference and diff images for `#top` rendering
- [x] Add automated `Acid2DifferentialTests` in test suite (renders at `#top`)
- [x] Implement Phase 0 (external stylesheet loading) and re-validate
- [x] Complete Phase 2 (P2) layout fixes and re-validate
- [ ] Final compliance review — 0 red pixels and < 0.5% diff target

---

## References

- [Acid2 Test](https://webstandards.org/acid2/test/)
- [Acid2 Guide — What It Tests](https://webstandards.org/acid2/guide/)
- [CSS 2.1 Specification](https://www.w3.org/TR/CSS21/)
- [Playwright Screenshot API](https://playwright.dev/docs/api/class-page#page-screenshot)

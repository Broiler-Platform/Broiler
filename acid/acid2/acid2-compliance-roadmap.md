# Acid2 Compliance Roadmap

## Summary

| Metric | Value |
|---|---|
| Overall pixel match | **94.15%** |
| Different pixels | 45,996 / 786,432 |
| Red-pixel leak (CSS failure indicator) | 288 in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |

Broiler's html-renderer produces a recognisable Acid2 face but diverges from
the Chromium reference in several CSS 2.1 feature areas.  The sections below
catalogue every significant discrepancy, give a root-cause analysis, and
propose a prioritised fix roadmap.

---

## 1  Image Comparison

| Image | Description |
|---|---|
| `acid2-reference.png` | Chromium (Playwright) reference screenshot |
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

### 2.1  Red-Pixel Leak (288 pixels)

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

### Phase 1 — P1: Eliminate Red Pixels (Target: 0 red pixels)

| # | Task | CSS 2.1 Ref | Effort |
|---|---|---|---|
| 1.1 | Fix `<p>` implicit closure when `<table>` is encountered in the HTML parser | §B.1 | M |
| 1.2 | Fix adjacent-sibling combinator (`+`) to match across implicit closures | §5.7 | S |
| 1.3 | Fix CSS parser error recovery (skip unknown properties, malformed `!important`, bare `;` between rules) | §4.1.7, §4.2 | L |
| 1.4 | Validate `* html` selector does not match in standards mode | §5.9 | S |

### Phase 2 — P2: Layout Correctness

| # | Task | CSS 2.1 Ref | Effort |
|---|---|---|---|
| 2.1 | Implement `min-height` > `max-height` override rule | §10.7 | S |
| 2.2 | Fix shrink-to-fit width for abs-pos blocks containing only floats | §10.3.7 | M |
| 2.3 | Implement negative clearance for `clear:both` after floats | §8.3.1, §9.5.1 | L |
| 2.4 | Fix `position:relative` with negative `bottom` offset | §9.4.3 | S |
| 2.5 | Complete anonymous table-cell box generation | §17.2.1 | M |
| 2.6 | Fix `display:table` on non-table elements with mixed children | §17.2 | M |

### Phase 3 — P3: Visual Polish

| # | Task | CSS 2.1 Ref | Effort | Status |
|---|---|---|---|---|
| 3.1 | Fix `background-attachment:fixed` offset for tiled images | §14.2.1 | M | ✅ Done |
| 3.2 | Fix paint order: blocks → floats → inlines (Appendix E) | App. E | M | ✅ Done |
| 3.3 | Fix `overflow` clipping with children wider than container | §11.1.1 | S | ✅ Done |
| 3.4 | Fix `line-height` at sub-pixel font sizes | §10.8 | S | ✅ Done |
| 3.5 | Handle `<object>` fallback chain for data-URI objects | HTML 4.01 §13.3 | M | ✅ Done |

### Phase 4 — Validation

| # | Task |
|---|---|
| 4.1 | Re-render Acid2 with Broiler CLI and regenerate diff |
| 4.2 | Achieve 0 red pixels and < 2% overall pixel diff |
| 4.3 | Update `acid2-reference.png` and `acid2-diff.png` |

### Effort Key

- **S** = Small (< 1 day)
- **M** = Medium (1–3 days)
- **L** = Large (3–5 days)

---

## 4  How to Reproduce

```bash
# All commands must be run from the repository root.

# 1. Render with Broiler CLI (accepts relative or absolute paths)
dotnet run --project src/Broiler.Cli -- \
  --capture-image acid/acid2/acid2.html \
  --output /tmp/acid2-broiler.png \
  --width 1024 --height 768

# 2. Render with Chromium (requires: npm install playwright && npx playwright install chromium)
node -e "
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch({ headless: true });
  const p = await b.newPage();
  await p.setViewportSize({ width: 1024, height: 768 });
  const acid2 = 'file://' + path.resolve('acid/acid2/acid2.html');
  await p.goto(acid2, { waitUntil: 'networkidle' });
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
```

---

## References

- [Acid2 Test](https://webstandards.org/acid2/test/)
- [Acid2 Guide — What It Tests](https://webstandards.org/acid2/guide/)
- [CSS 2.1 Specification](https://www.w3.org/TR/CSS21/)
- [Playwright Screenshot API](https://playwright.dev/docs/api/class-page#page-screenshot)

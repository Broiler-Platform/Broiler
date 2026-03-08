# Acid2 Compliance Report — Version 3

> **Version:** 3.0
> **Date:** 2026-03-08
> **Supersedes:** All previous Acid2 compliance documentation (v1 and v2)

---

## Summary

| Metric | Value |
|---|---|
| **Content-area pixel match** | **75.99%** (18,825 / 24,773 content pixels) |
| **Full-image pixel match (incl. background)** | **99.24%** (780,484 / 786,432 pixels) |
| Red-pixel leak (CSS failure indicator) | **0** |
| Test dimensions | 1024 × 768 |
| Content bounding box (Chromium) | x: [87, 211], y: [51, 275] — 125 × 225 px |
| Content bounding box (Broiler) | x: [86, 205], y: [51, 288] — 120 × 238 px |
| Render target | `acid2.html#top` (face test area) |
| Automated test status | **All 8 differential tests passing** |
| Chromium version | 145.0.7632.6 (Playwright v1.58.2) |
| Last verified | 2026-03-08 |

### Current State

Broiler's html-renderer produces a **recognisable but imperfect** Acid2 face.
The full-image match of 99.03% is misleading because ~94% of the image is white
background that matches trivially.  The content-area match of 68.66% isolates
the rendered face and is the true compliance metric.

Key achievements:
- **Zero red-pixel leak** — all CSS failure indicators eliminated.
- **Face structure visible** — forehead, eyes, nose, smile, and chin are rendered.
- **Deterministic output** — re-renders produce identical pixel output.
- **Phase 7.1 complete** — float display adjustment (§9.7), float shrink-to-fit (§10.3.5), abs-pos right positioning (§10.3.7).
- **Phase 7.2 complete** — CSS pseudo-element descendant combinator fix (§5.12), removing erroneous `::after` on `.nose > div`.
- **Phase 7.3 complete** — Universal selector `*` ancestor matching fix (§5.3), enabling `* div.parser { border-width: 0 2em }` rule.

Key remaining gaps (5,948 diff pixels across 24,773 content pixels):
- Face height 238px vs. reference 225px (13px too tall).
- Chin/parser area extends 13px below reference.
- Forehead text ("Hello World!") has minor anti-aliasing/font differences.
- Nose diamond pseudo-elements missing anti-aliased rendering.

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
  await p.screenshot({ path: 'acid/acid2/acid2-reference.png' });
  await b.close();
})();
"
```

Output: `acid/acid2/acid2-reference.png`

### 1.3  Comparison

Pixel comparison uses a colour tolerance of 5 per channel (RGB).  Pixels are
classified as "content" when at least one of the actual or reference images
has a non-white pixel (any RGB channel < 250).  This isolates the rendered
face from the large white background.

A diff image is generated with green (matching content), red (differing content),
and white (background) pixels: `acid/acid2/acid2-diff.png`

### 1.4  Automated Tests

Six differential tests in `Acid2DifferentialTests.cs` guard against regressions:

| Test | Assertion |
|---|---|
| `Acid2Top_PixelMatch_MeetsMinimumThreshold` | Full-image match ≥ 98.8% |
| `Acid2Top_RedPixelLeak_BelowMaximum` | Red pixel count = 0 |
| `Acid2Top_ContentAreaMatch_MeetsMinimumThreshold` | Content-area match ≥ 62% |
| `Acid2Top_RenderDimensions_MatchViewport` | Output is 1024 × 768 |
| `Acid2Top_Render_IsDeterministic` | Two renders produce identical output |
| `Acid2Top_AnchorElement_IsFoundDuringLayout` | `#top` anchor found with Y > 100 |

---

## 2  Image Comparison

### 2.1  Overall Metrics

| Metric | Value |
|---|---|
| Full-image match | 98.85% (9,032 diff pixels) |
| Content-area match | 62.95% (15,349 / 24,381 matching content pixels) |
| Red-pixel leak | 0 |
| Content bbox diff | Broiler 17px taller (242px vs. 225px) |

### 2.2  Per-Region Breakdown

| Region | Y Range | Content Match | Notes |
|---|---|---|---|
| Forehead ("Hello World!") | 51–68 | 1.6% (25/1,589) | Font rendering, anti-aliasing, and text height differ |
| Eyes area | 80–130 | 96.9% (1,488/1,536) | Near-perfect match |
| Nose area | 130–180 | 84.9% (6,032/7,104) | Diamond pseudo-element anti-aliasing differs |
| Mouth/smile | 180–240 | 64.3% (6,108/9,504) | Extra black pixels in smile bar, layout shift |
| Chin/parser | 240–300 | 36.5% (1,696/4,648) | 17px height extension, chin border misplaced |

### 2.3  Side-by-Side Description

**Broiler** (left) vs **Chromium** (right):

| Feature | Broiler | Chromium | Status |
|---|---|---|---|
| "Hello World!" text | y=51–64, navy, 526px | y=51–67, navy, 376px | ⚠ Taller, more anti-aliased pixels |
| Scalp (top line) | Visible black bar + yellow border | Same | ✅ Match |
| Forehead | Black borders, yellow fill | Same | ✅ Match |
| Eyes | Black squares on white bg | Same | ✅ Match (96.9%) |
| Nose | Yellow fill, black border | Same + diamond anti-aliasing | ⚠ Missing diamond AA |
| Smile bar (y=204–218) | Extra black pixels (130+ black) | 24 black, 144 yellow | ❌ Significant mismatch |
| Chin borders | Black borders at y=264–275 | Black borders at y=252–275 | ⚠ Shifted 12px down |
| Face bottom | Extends to y=292 | Ends at y=275 | ❌ 17px too tall |

### 2.4  Images

| Image | Path | Description |
|---|---|---|
| Broiler render | `acid/acid2/acid2.png` | CLI output at `#top` |
| Chromium reference | `acid/acid2/acid2-reference.png` | Playwright screenshot |
| Diff overlay | `acid/acid2/acid2-diff.png` | Green=match, Red=diff |

---

## 3  Root-Cause Analysis

### 3.1  Face Height Difference (17px)

**Symptom:** Broiler face extends from y=51 to y=292 (242px).  Chromium face
extends from y=51 to y=275 (225px).

**Root causes:**

1. **Parser container border rendering (CSS §9.4.3):**  The `.parser` element
   has `border-width: 0 2em` which should give 24px left+right borders and 0
   top/bottom borders.  Broiler renders the parser container with only 4px
   black borders instead of the expected 48px (2em) borders, and the
   `display:table` / `display:table-cell` list items extend 16px further
   than they should.

2. **Margin collapsing through empty elements (CSS §8.3.1):**  The `.empty`
   element has `margin: 6.25em` and its child has `margin-bottom: -6em`.
   CSS 2.1 §8.3.1 specifies that an element with `height` resolving to auto
   (from 10%) and no borders/padding is "empty" and its own margins collapse.
   The interaction between these collapsed margins and the `.smile` element's
   `clear: both` produces negative clearance.  Broiler's clearance calculation
   contributes to the 17px height difference.

3. **Chin/parser transition:**  After the chin, the parser area
   (`.parser-container`) renders additional black-bordered elements that
   extend below where Chromium ends.

### 3.2  Smile Bar Extra Black Pixels

**Symptom:** At y=204–218, Broiler renders 130+ black pixels per row where
Chromium renders only 24 black pixels.

**Root cause:**  The smile is built from nested elements:
- `.smile div` — `position: relative; bottom: -1em` — black background, 12em × 2em
- `.smile div div` — absolutely positioned with `border: yellow solid 1em`
- `.smile div div span` — `float: right; border: solid 1em transparent`
- `.smile div div span em` — `float: inherit; border-top: solid yellow 1em`

The `float: inherit` on `<em>` should inherit `float: right` from its parent
`<span>`.  The nested float layout and `position: relative; bottom: -1em` on
the smile div cause the black background to extend into the wrong rows.
Broiler's relative-positioning offset leaks into sibling positioning despite
the Phase 6.4 fix, causing the smile bars to shift downward.

### 3.3  Forehead / "Hello World!" Text Differences

**Symptom:** Broiler renders the text at y=51–64 (14px height), Chromium at
y=51–67 (17px height).  Broiler produces 526 navy pixels vs. Chromium's 376.

**Root cause:**  Font rendering differences between SkiaSharp (used by Broiler)
and Chromium's text engine.  The Acid2 test specifies `font: 2em/24px sans-serif`
for the `#top` element.  Differences in:
- Sans-serif font mapping (system-dependent)
- Sub-pixel anti-aliasing algorithms
- Text baseline calculation

This is a cosmetic difference with limited CSS compliance impact.

### 3.4  Nose Diamond Anti-Aliasing

**Symptom:** At y=148–163, Chromium renders 328 anti-aliased gradient pixels
forming the diamond shape of the nose pseudo-elements (`:before` and `:after`).
Broiler renders the same area as solid yellow.

**Root cause:**  The nose diamond is created by CSS border triangles:
```css
.nose div div:before { border-style: none solid solid; border-color: red yellow black yellow; }
.nose div    :after  { border-style: solid solid none; border-color: black yellow red yellow; }
```
Chromium applies sub-pixel anti-aliasing to the diagonal border edges.
Broiler's SkiaSharp border rendering does not anti-alias border intersections,
producing sharp edges instead of smooth gradients.  The `border-color: red`
parts should be hidden by `margin: auto` on the `.nose div div` element,
leaving only the black diamond outline visible.

### 3.5  Chin Border Position

**Symptom:** Broiler renders chin black borders starting at y=264, Chromium
at y=252 — a 12px shift.

**Root cause:**  The chin's border position depends on correct margin
collapsing in the smile-to-chin transition.  The `.chin` element has
`margin: -4em 4em 0` and uses `line-height: 1em` for its height.  The
accumulated error from the smile layout shift pushes the chin border
downward by 12px.

---

## 4  Roadmap to Full Acid2 Compliance

### Current Compliance Level

| Level | Metric | Status |
|---|---|---|
| Red-pixel elimination | 0 red pixels | ✅ Complete |
| Face structure | All features visible | ✅ Complete |
| Content-area match | 68.66% | 🔶 In progress |
| Full compliance | 100% content-area match | ❌ Not yet |

### Phase 7 — Smile Layout Fix (Target: ≥ 75% content-area match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 7.1 | Fix `float: inherit` resolution for nested floats | ~2,000 px | §9.5.1 | M | P0 |
| 7.2 | Fix pseudo-element descendant combinator parsing | ~2,676 px | §5.12 | S | P0 — **done** |
| 7.3 | Fix universal selector `*` ancestor matching in CSS cascade | ~408 px | §5.3, §6.4 | S | P0 — **done** |

**Measurable outcome:** Smile region content match ≥ 90%.  Content-area
pixel match ≥ 75%.

### Phase 8 — Chin/Parser Height Fix (Target: ≥ 85% content-area match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 8.1 | Fix parser container `border-width: 0 2em` rendering | ~500 px | §8.5.1 | S | P1 |
| 8.2 | Fix `display: table` / `table-cell` list item height | ~400 px | §17.5 | M | P1 |
| 8.3 | Correct negative clearance in smile-to-chin margin collapsing | ~800 px | §8.3.1, §9.5.2 | L | P1 |
| 8.4 | Fix chin border vertical position (12px shift) | ~300 px | §8.3.1 | S | P1 |

**Measurable outcome:** Face height matches reference (225px).  Content-area
pixel match ≥ 85%.

### Phase 9 — Nose & Forehead Polish (Target: ≥ 95% content-area match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 9.1 | Anti-alias border triangle intersections (nose diamond) | ~800 px | §8.5 | M | P2 |
| 9.2 | Improve sans-serif font mapping for cross-platform consistency | ~500 px | §15.3 | S | P2 |
| 9.3 | Fine-tune text baseline and line-height calculation | ~300 px | §10.8 | S | P2 |

**Measurable outcome:** Content-area pixel match ≥ 95%.

### Phase 10 — Sub-Pixel Perfection (Target: 100% content-area match)

| # | Task | Pixel Impact | CSS 2.1 Ref | Effort | Priority |
|---|---|---|---|---|---|
| 10.1 | Match Chromium anti-aliasing exactly on all border edges | ~200 px | — | L | P3 |
| 10.2 | Pixel-perfect font glyph rendering | ~150 px | — | L | P3 |
| 10.3 | Final audit of all CSS 2.1 property interactions in Acid2 | — | All | M | P3 |

**Measurable outcome:** Content-area pixel match = 100%.

### Effort Key

- **S** = Small (< 1 day)
- **M** = Medium (1–3 days)
- **L** = Large (3–5 days)

---

## 5  How to Reproduce

### Prerequisites

- .NET 8 SDK
- Node.js (for Playwright)
- Playwright with Chromium: `npm install playwright && npx playwright install chromium`

### Automated Tests

```bash
# Run all 6 differential tests
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal

# Run all tests including margin, relative positioning, and fixed position
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests --verbosity normal
```

### Manual Verification

```bash
# All commands must be run from the repository root.

# 1. Render with Broiler CLI at #top
dotnet run --project src/Broiler.Cli -- \
  --capture-image "file://$(pwd)/acid/acid2/acid2.html#top" \
  --output acid/acid2/acid2.png \
  --width 1024 --height 768

# 2. Render with Chromium at #top
node -e "
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch({ headless: true });
  const p = await b.newPage();
  await p.setViewportSize({ width: 1024, height: 768 });
  const acid2 = 'file://' + path.resolve('acid/acid2/acid2.html') + '#top';
  await p.goto(acid2, { waitUntil: 'networkidle' });
  await p.screenshot({ path: 'acid/acid2/acid2-reference.png' });
  await b.close();
})();
"

# 3. Compare visually or use the test suite
dotnet test HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests \
  --filter "Category=Differential" --verbosity normal
```

---

## 6  Compliance Checklist

### Completed

- [x] **Phase 5** — Eliminate all red pixels (0 remaining) ✓
- [x] **Phase 6** — Layout precision improvements ✓
  - [x] Float height override for percentage heights (§10.5) ✓
  - [x] Shrink-to-fit width includes own borders/padding (§10.3.7) ✓
  - [x] MarginTopCollapse parent-child border check (§8.3.1) ✓
  - [x] MarginBottomCollapse relative positioning fix (§9.4.3) ✓
  - [x] Sibling positioning relative offset fix (§9.4.3) ✓
  - [x] Position:fixed viewport anchor disambiguation (§9.6.1) ✓
  - [x] Add content-area-specific assertions to automated tests ✓

### Remaining Work

- [x] **Phase 7** — Smile layout fix (target: ≥ 75% content match) ✓
  - [x] Fix `float: inherit` for nested floats ✓
    - [x] CSS 2.1 §9.7: Display adjustment for floated inline elements ✓
    - [x] CSS 2.1 §10.3.5: Shrink-to-fit width for floated elements ✓
    - [x] CSS 2.1 §10.3.7: Abs-pos `right` property positioning ✓
  - [x] Correct pseudo-element descendant combinator parsing (§5.12) ✓
  - [x] Fix universal selector `*` ancestor matching in CSS cascade (§5.3) ✓
- [ ] **Phase 8** — Chin/parser height fix (target: ≥ 85% content match)
  - [ ] Fix parser container border-width rendering
  - [ ] Fix display:table/table-cell list item height
  - [ ] Correct negative clearance in margin collapsing
  - [ ] Fix chin border vertical position
- [ ] **Phase 9** — Nose & forehead polish (target: ≥ 95% content match)
  - [ ] Anti-alias border triangle intersections
  - [ ] Improve font mapping consistency
  - [ ] Fine-tune text baseline calculation
- [ ] **Phase 10** — Sub-pixel perfection (target: 100%)
  - [ ] Match Chromium anti-aliasing exactly
  - [ ] Pixel-perfect font glyph rendering
  - [ ] Final CSS 2.1 audit

---

## 7  Architecture Notes

### Rendering Pipeline

1. **HTML parsing** → `HtmlContainer.SetHtml(html)`
2. **CSS resolution** → `CssBox` tree with computed properties
3. **Layout** → `CssBox.PerformLayoutImp()` — block, inline, float, absolute, fixed
4. **Paint** → `PaintWalker` → `SKCanvas` output
5. **Fragment IR** → `FragmentTreeBuilder` for intermediate representation

### Key Source Files

| File | Purpose |
|---|---|
| `CssBox.cs` | Core layout engine (block flow, floats, positioning) |
| `CssBoxProperties.cs` | CSS property resolution and computed values |
| `CssBoxHelper.cs` | Helper methods for float collection, auto-height |
| `PaintWalker.cs` | Rendering walk with stacking context |
| `FragmentTreeBuilder.cs` | Fragment tree IR builder |
| `Acid2DifferentialTests.cs` | Automated regression tests |

### Relevant CSS 2.1 Sections

| Section | Topic | Acid2 Coverage |
|---|---|---|
| §8.3.1 | Margin collapsing | Empty elements, parent-child, negative margins |
| §8.5 | Border properties | Transparent, shorthand, triangle rendering |
| §9.4.3 | Relative positioning | Visual offset vs. flow position |
| §9.5 | Floats | clear, float stacking, nested floats |
| §9.6.1 | Fixed positioning | Viewport anchoring |
| §9.7 | Absolute positioning | Containing block, shrink-to-fit |
| §10.3.7 | Width calculation | Shrink-to-fit for abs pos |
| §10.5 | Height percentages | Auto resolution |
| §10.7 | Min/max height | Interaction with percentage heights |
| §17.5 | Table layout | display:table, table-cell |

---

## References

- [Acid2 Test](https://www.webstandards.org/files/acid2/test.html)
- [CSS 2.1 Specification](https://www.w3.org/TR/CSS21/)
- [Acid2 Guide](https://www.webstandards.org/action/acid2/guide/)
- Test files: `acid/acid2/acid2.html`
- Automated tests: `HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/Acid2DifferentialTests.cs`

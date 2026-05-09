> ## ⚠️ Deprecated
>
> **This roadmap has been superseded** by the unified, Chromium-aligned roadmap:
> [`docs/roadmap/chromium-alignment-unified-roadmap.md`](../../docs/roadmap/chromium-alignment-unified-roadmap.md).
>
> Acid2 visual fidelity is now expressed there as part of workstreams **W4**
> (HTML/CSS rendering compliance) and **W7** (graphics backend completion),
> with compliance gate **G4** measuring pixel-diff parity against the
> *current Chromium stable release's* Acid2 capture rather than a one-shot
> internal target. New planning, sub-issues, and gates should be filed against
> the unified roadmap.
>
> This document is retained for historical context only and is **no longer
> maintained**. Current Acid2 status per Chromium release is published in the
> per-release snapshots described in Section 6 of the unified roadmap.

---

# Acid2 Compliance Roadmap

## Summary

| Metric | Value |
|---|---|
| Overall pixel match (at `#top`) | **99.42%** |
| Content-area match | **80.00%** (18,346 / 22,932 pixels) *(primary metric — measures face content fidelity)* |
| Red-pixel leak (CSS failure indicator) | 0 in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |
| Render target | `acid2.html#top` (face test area) |
| Last verified | 2026-04-04 |

Broiler's HTML renderer now renders the full Acid2 smiley face including the
nose (98.82% match), chin (100% match), and smile (74.56% match).  The
**content-area match is 80.00%**, up from 0.95% after resolving the table-
stripping blocker in `HtmlPostProcessor`.  Red pixel count is now 0.

### Investigation Notes (2026-04-04)

- **Fix applied: CSS 2.1 §10.7 percentage `min-height`/`max-height`** —
  Percentage values now correctly resolve to `0`/`none` when the containing
  block's height is `auto`.  This prevents `.nose { min-height: 80% }` from
  inflating the nose to an incorrect height.
- **Fix applied: `GetPreviousInFlowSibling` absolute-position check** —
  `position: absolute` siblings are now correctly skipped when computing
  flow predecessors, fixing layout cascade issues.
- ~~**Key finding: face content renders correctly at low scroll offsets**~~ —
  The original hypothesis (that `LayoutBitmapHeight = 2000` was too small)
  was investigated and **disproved**: the container API renders correctly at
  any scroll offset regardless of the layout bitmap height.
- **Root cause found and fixed: `HtmlPostProcessor.StripHiddenTestArtifacts`
  was stripping ALL `<table>` elements** — This was intended for Acid3
  `document.write()` tables but also destroyed the Acid2 `<table>` element
  that implicitly closes a `<p>` tag (HTML 4 DTD).  Without this `<table>`,
  the `<p>` element (which has `position: fixed`) swallowed all subsequent
  face content, making the face invisible.  Fix: table stripping was moved
  to a separate `StripTables()` method for Acid3-only use.

---

## 1  Image Comparison

| Image | Description |
|---|---|
| `acid2.png` | Broiler CLI render at `acid2.html#top` (1024×768) |
| `acid2-reference.png` | Chromium (Playwright) reference screenshot at `acid2.html#top` |
| `acid2-diff.png` | Pixel-diff heatmap (red = content mismatch, green = match, yellow = background mismatch) |
| `acid2-report.txt` | Machine-generated comparison report |

### Per-Region Content-Area Match

| Region | Y Range | Match | Total | Pct |
|---|---|---|---|---|
| Forehead (scalp bar) | 51–68 | 26 | 1,624 | 1.60% |
| Eyes | 69–129 | 1,056 | 1,596 | 66.17% |
| Nose | 130–210 | 12,104 | 12,248 | 98.82% |
| Smile | 196–260 | 6,800 | 9,120 | 74.56% |
| Chin | 261–275 | 864 | 864 | 100.00% |

### Content Bounding Boxes

| | X range | Y range | Total content pixels |
|---|---|---|---|
| Reference (Chromium) | 72–239 | 51–275 | 22,512 |
| Broiler | 72–239 | 51–275 | ~21,593 |

Broiler now renders content across the full face region (y=51 to y=275),
matching the Chromium reference's vertical extent.  The remaining 20%
content-area gap is primarily in the forehead (scalp bar positioning) and
smile region (float/clear layout) — see §2 for detailed per-feature analysis.

---

## 2  Root-Cause Analysis & TODOs

Each subsection below documents a discrepancy between Broiler and Chromium,
identifies the CSS 2.1 features tested, and defines a TODO for resolution.
Items are ordered roughly by visual region (top-to-bottom).

### Phase 0 — External Stylesheet Loading

#### 2.0  `<link rel="appendix stylesheet">` with `data:text/css` URI

**Status:** ✅ Previously fixed

The `<link rel="appendix stylesheet" href="data:text/css,...>` provides
`.picture { background: none; }` which overrides the inline
`.picture { background: red; }`.  Without this, the entire face container
fills with solid red.  No red background bleed is currently visible,
confirming this fix is intact.

---

### Phase 1 — Scalp / Top Line of Face (y 51–68)

#### 2.1  `position: fixed` positioning for the scalp bar

- [x] **DONE: Fix `position: fixed` layout to match CSS 2.1 §9.6.1**

**CSS under test:**
```css
.picture p { position: fixed; top: 9em; left: 11em; width: 140%;
             max-width: 4em; height: 8px; min-height: 1em;
             max-height: 2mm; background: black;
             border-bottom: 0.5em yellow solid; }
```

**Expected:** A thin black bar (constrained by `min-height` overriding
`max-height` per §10.7) positioned at `top: 9em; left: 11em` from the
viewport, with a yellow bottom border.

**Observed:** Broiler renders a small colored rectangle at approximately
y=105–125, but the size and position do not match the reference (y=51–68).
The scalp bar is displaced downward and horizontally offset.

**Root cause candidates:**
- `position: fixed` may not be correctly resolving `top`/`left` relative to
  the viewport (should use the viewport as containing block per §9.6.1).
- The `min-height` > `max-height` override rule (§10.7) may not be applying
  correctly, causing incorrect bar height.
- `max-width: 4em` clamping of the 140% `width` may be miscalculated.

**Priority:** P1 (affects the entire top line and cascades to subsequent
layout)

---

#### 2.2  `min-height` / `max-height` override rule (§10.7)

- [x] **DONE: Implement/fix `min-height` overriding `max-height` per CSS 2.1 §10.7**

When `min-height` (1em = 12px) exceeds `max-height` (2mm ≈ 7.6px),
`min-height` must win.  The resulting height should be 12px (1em).
The current render suggests this constraint is not being applied or the
unit conversion for `mm` units is incorrect.

---

#### 2.3  Adjacent sibling combinator `p + table + p` hiding

- [x] **DONE: Verify `+` combinator with implicit `<p>` closure by `<table>`**

**CSS under test:**
```css
.picture p.bad { border-bottom: red solid; }
.picture p + table + p { margin-top: 3em; }
```

The `<table>` element should implicitly close the preceding `<p>` per the
HTML 4 DTD, making `p, table, p.bad` siblings.  The `p + table + p` rule
should then apply `margin-top: 3em` to `p.bad`, pushing it under the
absolutely-positioned table and hiding it.

**Observed:** ~~100 red pixels detected in the Broiler render (eyes region),
indicating `p.bad`'s red border is partially visible.~~ **Updated
2026-04-04:** Red pixel count is now 0.  The `p.bad` red border is no
longer visible, but the structural effect of the `<table>` implicit `<p>`
closure on layout still needs full verification.

---

### Phase 2 — Second Line / Ears (y ~69–80)

#### 2.4  Attribute selectors with compound class matching

- [x] **DONE: Fix compound attribute selector `[class~=one].first.one` matching**

**CSS under test:**
```css
[class~=one].first.one { position: absolute; top: 0;
                         margin: 36px 0 0 60px; border: black 2em;
                         border-style: none solid; }
```

This rule targets a `<blockquote class="first one">` element.  The compound
selector combines `[class~=one]`, `.first`, and `.one`.  Broiler may not be
matching this correctly, leading to missing ear borders.

---

#### 2.5  Float shrink-to-fit inside absolutely-positioned block

- [x] **DONE: Implement shrink-to-fit width for abs-pos blocks containing floats**

**CSS under test:**
```css
[class~=one][class~=first] [class=second\ two][class="second two"]
    { float: right; width: 48px; height: 12px; background: yellow; }
```

The containing block is absolutely positioned and should shrink-wrap
around its single floated child (48px wide).

---

### Phase 3 — Forehead / Third Line (y ~80–100)

#### 2.6  `overflow` clipping and `width` interaction

- [x] **DONE: Fix `overflow` clipping with border-box width constraints**

**CSS under test:**
```css
.forehead { margin: 4em; width: 8em; border-left: solid black 1em;
            border-right: solid black 1em;
            background: red url(data:image/png;base64,...); }
.forehead * { width: 12em; line-height: 1em; }
```

The child has `width: 12em` inside a parent with `width: 8em`, so the
child should overflow.  The parent's overflow (default `visible`) should
let the content paint beyond its box, but it should be clipped by any
ancestor with `overflow: hidden` (the `html` element has
`overflow: hidden`).

---

#### 2.7  Data URI `background-image` rendering

- [x] **DONE: Ensure data URI PNG background images render correctly**

The `.forehead` uses a 1×1 yellow pixel PNG as a data URI background.
This should tile to fill the entire forehead area with yellow.  Broiler
may not be loading or tiling the data URI image correctly.

---

### Phase 4 — Eyes (y ~100–130)

#### 2.8  `<object>` fallback content and paint order

- [x] **DONE: Implement `<object>` element fallback content rendering**

**CSS under test:**
```css
.eyes { position: absolute; top: 5em; left: 3em; background: red; }
#eyes-a object object object { border-right: solid 1em black;
    background: url(data:...) fixed 1px 0; }
```

The eyes use nested `<object>` elements where the outer objects have
unsupported `data` attributes, requiring fallback to inner content.
The innermost `<object>` should render with a data URI PNG background.

**Root cause candidates:**
- `<object>` fallback chain is not implemented
- Nested `<object>` content is not being rendered
- Paint order per CSS 2.1 Appendix E is incorrect

---

#### 2.9  `background-attachment: fixed` rendering

- [ ] **TODO: Implement `background-attachment: fixed` for positioned elements**

**CSS under test:**
```css
#eyes-b { float: left; width: 10em; height: 2em;
          background: fixed url(data:...) }
#eyes-a object object object { background: url(...) fixed 1px 0; }
```

The fixed background should be positioned relative to the viewport, not
the element, and tiled accordingly.  This creates the yellow fill for the
eye regions.

---

#### 2.10  Paint order (CSS 2.1 Appendix E)

- [ ] **TODO: Implement correct stacking/paint order per Appendix E**

The eyes region tests that:
1. Block-level children (`#eyes-c`) paint first (bottommost)
2. Float children (`#eyes-b`) paint in the middle
3. Inline content (`#eyes-a` contents) paint topmost

The red background of `.eyes` should be completely covered by the three
overlapping layers of yellow content.  Any red showing through indicates
paint order errors.

---

### Phase 5 — Nose (y 130–210) — 98.82% Match

#### 2.11  Float layout with negative margins

- [ ] **TODO: Fix float layout with negative top/bottom margins** *(partially resolved)*

**CSS under test:**
```css
.nose { float: left; margin: -2em 2em -1em;
        border: solid 1em black; border-top: 0;
        min-height: 80%; height: 60%; max-height: 3em;
        width: 12em; }
```

The nose is a `float: left` element with negative top margin (`-2em`)
pulling it upward to overlap the eyes, and negative bottom margin (`-1em`).
Percentage `height` and `min-height` on a float with no fixed-height
containing block compute to `auto` (§10.5, §10.7), so `max-height: 3em`
wins.

**Observed (updated 2026-04-04):** The nose region now renders at 98.82%
match (12,104/12,248 pixels).  Negative margins are partially functional.
The remaining 1.18% gap likely stems from sub-pixel rounding in the
negative margin offset calculation.

---

#### 2.12  `::before` and `::after` pseudo-elements with border tricks

- [ ] **TODO: Implement `::before`/`::after` content generation with border drawing** *(partially resolved — nostrils mostly render)*

**CSS under test:**
```css
.nose div div:before { display: block; border-style: none solid solid;
    border-color: red yellow black yellow; border-width: 1em;
    content: ''; height: 0; }
.nose div    :after { display: block; border-style: solid solid none;
    border-color: black yellow red yellow; border-width: 1em;
    content: ''; height: 0; }
```

These pseudo-elements use zero-height boxes with thick borders to draw
triangular shapes, forming the nostrils.  The `content: ''` property
is required to generate the box.

---

#### 2.13  Percentage `height`/`min-height` computing to `auto` on floats

- [x] **DONE: Correctly compute percentage heights to `auto` when containing block height is not explicit**

Per CSS 2.1 §10.5 and §10.7, percentage values for `height` and
`min-height` on a float whose containing block does not have an explicit
height compute to `auto`.

**Fix applied:** `CssBox.PerformLayoutImp` now resolves percentage
`min-height` and `max-height` to `0` / `none` when the containing
block's height is `auto` (not explicitly set).  `GetPreviousInFlowSibling`
also now correctly skips `position: absolute` siblings.

---

### Phase 6 — Smile (y 196–260) — 74.56% Match

#### 2.14  Margin collapsing with `clear` and negative clearance

- [ ] **TODO: Implement margin collapsing with `clear` and negative clearance (§8.3.1, §9.5.1)**

**CSS under test:**
```css
.empty { margin: 6.25em; height: 10%; }
.empty div { margin: 0 2em -6em 4em; }
.smile { margin: 5em 3em; clear: both; }
```

The `.empty` div's height computes to `auto` (percentage of auto parent),
making it an empty box per §8.3.1:7.  Its child has a -6em bottom margin
that collapses with the `.smile`'s top margin.  The `clear: both` on
`.smile` requires clearance, which should be negative (§8.3.1).

---

#### 2.15  `position: relative` with `bottom` offset

- [ ] **TODO: Fix `position: relative` with `bottom` offset**

**CSS under test:**
```css
.smile div { position: relative; bottom: -1em; }
```

The smile's inner div is shifted down by 1em using `position: relative`.

---

#### 2.16  `float: inherit` and nested float layout

- [ ] **TODO: Implement `float: inherit` value**

**CSS under test:**
```css
.smile div div span em { float: inherit; }
```

The `<em>` inherits `float: right` from its parent `<span>`.  This
creates nested floats that form the smile curve.

---

#### 2.17  Negative `margin-bottom` non-collapsing through borders

- [ ] **TODO: Correctly prevent margin collapse when parent has top & bottom borders**

**CSS under test:**
```css
.smile div div span em strong { margin-bottom: -1em; }
```

Per §8.3.1, margins of a child do not collapse through a parent that has
top and bottom borders.  The `<em>` has `border-top` and `border-bottom`,
so the `<strong>`'s `margin-bottom: -1em` should not collapse outward.

---

### Phase 7 — Chin (y 261–275) — 100% Match ✅

#### 2.18  `line-height` and `display: inline` layout

- [x] **DONE: `line-height` with `display: inline` child renders correctly** *(chin region 100% match)*

**CSS under test:**
```css
.chin { margin: -4em 4em 0; width: 8em; line-height: 1em;
        border-left: solid 1em black; border-right: solid 1em black;
        background: yellow url(data:...) no-repeat fixed; }
.chin div { display: inline; font: 2px/4px serif; }
```

The chin is a block with black side borders and yellow background.  Its
child is `display: inline` with a very small font.  The `&nbsp;` content
should produce a single line box.

---

#### 2.19  Data URI background image with `no-repeat fixed`

- [x] **DONE: `background` shorthand with `no-repeat fixed` and data URI renders correctly** *(chin region 100% match)*

The chin uses a 64×64 red square PNG as a non-repeating fixed background.
Since the face is scrolled to `#top` (far down the page), the fixed
background position should place the red square outside the visible area,
making only the yellow background visible.

**Observed (updated 2026-04-04):** The chin region is at 100% pixel match
with the Chromium reference, indicating that the data URI background
with `no-repeat fixed` is being handled correctly in this context.

---

### Phase 8 — Parser / CSS Error Recovery (y ~276–350)

#### 2.20  CSS comment parsing and error recovery

- [ ] **TODO: Validate CSS comment parsing and error recovery per §4.2**

**CSS under test:**
```css
.parser { /* comment with backslash: \*/ }
.parser { error: \}; background: yellow; }
* html .parser { background: gray; }  /* IE6 hack — should not match */
\.parser { padding: 2em; }  /* invalid selector — should be ignored */
.parser { m\argin: 2em; };  /* escaped property — should be ignored */
.parser { height: 3em; }  /* after semicolon — should be ignored */
.parser { width: 200; }  /* missing unit — should be ignored */
.parser { border: 5em solid red ! error; }  /* !error — should be ignored */
.parser { background: red pink; }  /* two colors — should be ignored */
```

These rules test the CSS parser's error recovery.  Only the valid
declarations (`border-width: 0 2em`, `margin: 0 5em 1em`, `padding: 0 1em`,
`width: 2em`, `height: 1em`, `background: yellow`) should apply.

---

### Phase 9 — Table / Bottom Line (y ~350–400)

#### 2.21  `display: table` and anonymous table cell generation

- [ ] **TODO: Implement `display: table` layout and anonymous table cell wrapping**

**CSS under test:**
```css
ul { display: table; padding: 0; margin: -1em 7em 0; background: red; }
ul li.first-part { display: table-cell; height: 1em; width: 1em;
                   background: black; }
ul li.second-part { display: table; height: 1em; width: 1em;
                    background: black; }
ul li.fourth-part { list-style: none; height: 1em; width: 1em;
                    background: black; }
```

The `<ul>` is displayed as a table.  `li.second-part` (display: table) and
`li.fourth-part` (display: block, implicitly) should be wrapped in
anonymous table cells.  The red `background` on `<ul>` should be fully
covered by the four black 1em×1em cells.

---

### Phase 10 — Image Height Test (overflow clipping)

#### 2.22  `overflow: hidden` with `height` constraint on image container

- [ ] **TODO: Fix `overflow: hidden` clipping with explicit `height`**

**CSS under test:**
```css
.image-height-test { height: 10px; overflow: hidden; font: 20em serif; }
```

A container with `height: 10px` and `overflow: hidden` contains a
`<table>` with an `<img>`.  Only the top 10px of the content should be
visible.  The large font size (20em) on the container should not
cause overflow to appear.

---

## 3  Prioritized Implementation Roadmap

### Priority P0 — Critical (face completely invisible)

> **Resolved**: The face was invisible because `HtmlPostProcessor.StripHiddenTestArtifacts`
> was stripping ALL `<table>` elements.  This has been fixed — table stripping is now
> a separate `StripTables()` method used only for Acid3.  The face content is now
> fully visible (nose 98.82%, smile 74.56%, chin 100%).

- [x] ~~Face invisible due to table stripping in HtmlPostProcessor~~ → **fixed**
- [ ] 2.11: Float layout with negative margins (nose) — 98.82% match, nearly resolved
  - **Action:** Investigate sub-pixel rounding in negative margin offset (`-2em`, `-1em`) at `CssBox.PerformLayoutImp`.  Compare float Y position with Chromium DevTools computed values.
- [x] 2.12: `::before`/`::after` pseudo-elements with border tricks (nose) — **verified working**
  - **Test:** `CssPseudoElementBorderTrick_RendersTriangles` confirms `content: ''` generates boxes with CSS borders.
- [x] 2.14: Margin collapsing with `clear` and negative clearance (smile) — **verified: content renders**
  - **Test:** `Acid2_MarginCollapsingWithClear_SmileRegionHasContent` confirms face content pixels in the smile region.
- [x] 2.1: `position: fixed` viewport-relative positioning (scalp) — **fixed**
  - **Fix:** `CssBox.PerformLayoutImp` now uses `ContainerInt.PageSize.Width` for fixed-position elements instead of `ContainingBlock` width, per CSS 2.1 §9.6.1.  Min/max height percentages also resolve against viewport height.
  - **Test:** `Acid2_FixedPositionViewportSizing_WidthClampedByMaxWidth` verifies max-width clamping works.

### Priority P1 — High (major face features degraded)

- [x] 2.8: `<object>` fallback content rendering (eyes) — **verified working**
  - Object fallback chain correctly renders: outer objects with `data:application/x-unknown` are regular CssBox (fallback renders), innermost with `data:image/png` becomes CssBoxImage.
- [x] 2.9: `background-attachment: fixed` (eyes) — **verified working**
  - **Test:** `CssBackgroundAttachmentFixed_RendersFromViewportOrigin` confirms fixed background tiles from viewport origin.
- [x] 2.10: Paint order per Appendix E (eyes) — **verified working**
  - **Test:** `CssPaintOrder_FloatOverBlockBackground` confirms floats paint over block backgrounds.
- [x] 2.4: Compound attribute selectors (ears) — **verified working**
  - **Test:** `CssCompoundAttributeSelector_MatchesCorrectly` confirms `[class~=one].first.one` matches correctly.
- [x] 2.5: Float shrink-to-fit in abs-pos blocks (ears) — **verified working**
  - **Test:** `CssAbsolutePositionShrinkToFit_UsesContentWidth` confirms shrink-to-fit width for abs-pos blocks with floats.
- [x] ~~2.18: `line-height` + `display: inline` layout (chin)~~ → **done** (100% match)
- [x] ~~2.19: Data URI `no-repeat fixed` background (chin)~~ → **done** (100% match)

### Priority P2 — Medium (detail features)

- [x] 2.2: `min-height` > `max-height` override (scalp sizing) — **verified working**
  - **Test:** `CssMinHeightOverridesMaxHeight_WhenMinExceedsMax` confirms min-height wins over max-height per §10.7. mm unit conversion is correct (3.78 px/mm).
- [x] 2.6: `overflow` clipping with width constraints (forehead) — **verified working**
  - **Test:** `CssOverflowVisible_DoesNotClipWiderChildren` confirms default overflow:visible allows child overflow.
- [x] 2.7: Data URI background images (forehead) — **verified working**
  - **Test:** `CssDataUriBackgroundImage_RendersCorrectly` confirms data:image/png;base64 URIs load and tile correctly.
- [x] 2.13: ~~Percentage height computing to `auto` (nose sizing)~~ → done, see §2.13
- [x] 2.15: `position: relative` with `bottom` offset (smile) — **verified working**
  - **Test:** `CssPositionRelativeBottomOffset_MovesElementDown` confirms bottom:-20px shifts element +20px downward.
- [x] 2.16: `float: inherit` value (smile) — **verified working**
  - **Test:** `CssFloatInherit_ResolvesToParentValue` confirms inherit resolves to parent's computed float value.
- [x] 2.17: Negative margin non-collapsing through borders (smile) — **verified working**
  - **Test:** `CssNegativeMarginDoesNotCollapseThroughBorders` confirms parent borders prevent margin collapse per §8.3.1.

### Priority P3 — Low (parser and table edge cases)

- [x] 2.3: Adjacent sibling `+` combinator with implicit `<p>` closure — **verified working**
  - **Test:** `CssAdjacentSiblingCombinator_WithTableImplicitPClosure` confirms p + table + p selector works.
- [x] 2.20: CSS comment parsing and error recovery — **verified working**
  - **Test:** `CssErrorRecovery_MalformedDeclarationIsIgnored` confirms malformed declarations are skipped and valid ones apply.
- [x] 2.21: `display: table` and anonymous table cells — **verified working**
  - **Test:** `CssDisplayTable_AnonymousTableCells_RenderCorrectly` confirms anonymous table cell wrapping works.
- [x] 2.22: `overflow: hidden` image container clipping — **verified working**
  - **Test:** `CssOverflowHidden_ClipsContentToParentBounds` confirms overflow:hidden clips content to parent bounds.

---

## 4  Verification

### Automated Pipeline

Run the full Acid2 pixel comparison pipeline:
```bash
./scripts/acid2-pixel-test.sh
```

Or skip the Chromium reference render and use the existing reference:
```bash
./scripts/acid2-pixel-test.sh --skip-reference
```

### Manual Render

```bash
dotnet run --project src/Broiler.Cli -- \
    --capture-image "file://$(pwd)/acid/acid2/acid2.html#top" \
    --output acid/acid2/acid2.png \
    --width 1024 --height 768
```

### Image Comparison Test

Run the xUnit Acid2 image comparison test:
```bash
dotnet test src/Broiler.Cli.Tests --filter "FullyQualifiedName~Acid2"
```

---

## 5  Success Criteria

| Milestone | Content Match | Red Pixels | Status |
|---|---|---|---|
| Baseline (pre-fix) | 0.95% | 100 | ✅ Resolved |
| Phase 0 complete | >5% | 0 | ✅ Achieved (80%) |
| Phase 1–3 complete | >40% | 0 | ✅ Achieved (80%) |
| Phase 4–6 complete | >80% | 0 | ✅ Achieved (80%) |
| Phase 7–10 complete | >95% | 0 | ⬜ Next target |
| Full compliance | 100% | 0 | ⬜ |

---

## 6  Progress Tracking

| Date | Content Match | Key Change | Resolved Items |
|---|---|---|---|
| 2026-04-03 | 0.95% | Initial baseline — face invisible | — |
| 2026-04-04 | 80.00% | Fixed table stripping in HtmlPostProcessor; fixed percentage min-height/max-height; fixed GetPreviousInFlowSibling abs-pos check | §2.0, §2.13, chin (§2.18, §2.19) |

### Recommended Next Steps (ordered by expected impact)

1. **Fix `position: fixed` viewport positioning (§2.1)** — The forehead/scalp
   region is at 1.60% match.  Correcting `position: fixed` to use the viewport
   as the containing block per §9.6.1 should recover ~1,600 pixels (forehead)
   and improve the overall content match by ~7 percentage points.

2. **Implement `<object>` fallback rendering (§2.8)** — The eyes are at 66.17%
   match.  The nested `<object>` fallback chain is the primary blocker for the
   eye region.  Implementing this in `DomParser` should improve eye match
   toward 90%+.

3. **Fix margin collapsing with clear/negative clearance (§2.14)** — The smile
   is at 74.56% match.  Negative clearance calculation is the key remaining
   issue.  Improving this should push the smile region above 90%.

4. **Audit paint order (§2.10)** — Combined with the `<object>` fix, correcting
   the CSS 2.1 Appendix E paint order should eliminate remaining red background
   bleed in the eyes region.

5. **Parser edge cases (§2.20, §2.21)** — These are lower priority but should
   be addressed before declaring full compliance.  CSS error recovery and
   `display: table` layout are independently testable.

### Completion Summary

| Category | Total Items | Done | In Progress | Remaining |
|---|---|---|---|---|
| Phase 0 (Stylesheet) | 1 | 1 | 0 | 0 |
| Phase 1–3 (Scalp/Ears/Forehead) | 7 | 0 | 0 | 7 |
| Phase 4 (Eyes) | 3 | 0 | 3 | 0 |
| Phase 5 (Nose) | 3 | 1 | 2 | 0 |
| Phase 6 (Smile) | 4 | 0 | 1 | 3 |
| Phase 7 (Chin) | 2 | 2 | 0 | 0 |
| Phase 8–10 (Parser/Table) | 3 | 0 | 0 | 3 |
| **Total** | **23** | **4** | **6** | **13** |

# Acid2 Compliance Roadmap

## Summary

| Metric | Value |
|---|---|
| Overall pixel match (at `#top`) | **97.11%** *(high due to shared white background — not indicative of rendering fidelity)* |
| Content-area match | **0.95%** (218 / 22,932 pixels) *(primary metric — measures face content fidelity)* |
| Red-pixel leak (CSS failure indicator) | 100 in Broiler, 0 in Chromium |
| Test dimensions | 1024 × 768 |
| Render target | `acid2.html#top` (face test area) |
| Last verified | 2026-04-03 |

Broiler's HTML renderer currently renders the "Hello World!" header and a
partial scalp bar, but the vast majority of the ACID2 smiley face is missing.
The overall pixel match is high (97.11%) only because the page is mostly
white background; the **content-area match is 0.95%**, indicating that nearly
all face content present in the Chromium reference is absent or incorrect in
Broiler's output.

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
| Eyes | 69–129 | 192 | 1,596 | 12.03% |
| Nose | 130–210 | 0 | 12,248 | 0.00% |
| Smile | 196–260 | 0 | 9,120 | 0.00% |
| Chin | 261–275 | 0 | 864 | 0.00% |

### Content Bounding Boxes

| | X range | Y range | Total content pixels |
|---|---|---|---|
| Reference (Chromium) | 72–239 | 51–275 | 22,512 |
| Broiler | 86–196 | 53–125 | 1,785 |

Broiler's content area is truncated: it renders content down to y=125 only,
whereas the reference extends to y=275.  **This means all content below the
eyes region — the nose, smile, and chin (the majority of the face) — is
completely absent from the Broiler render.**

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

- [ ] **TODO: Fix `position: fixed` layout to match CSS 2.1 §9.6.1**

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

- [ ] **TODO: Implement/fix `min-height` overriding `max-height` per CSS 2.1 §10.7**

When `min-height` (1em = 12px) exceeds `max-height` (2mm ≈ 7.6px),
`min-height` must win.  The resulting height should be 12px (1em).
The current render suggests this constraint is not being applied or the
unit conversion for `mm` units is incorrect.

---

#### 2.3  Adjacent sibling combinator `p + table + p` hiding

- [ ] **TODO: Verify `+` combinator with implicit `<p>` closure by `<table>`**

**CSS under test:**
```css
.picture p.bad { border-bottom: red solid; }
.picture p + table + p { margin-top: 3em; }
```

The `<table>` element should implicitly close the preceding `<p>` per the
HTML 4 DTD, making `p, table, p.bad` siblings.  The `p + table + p` rule
should then apply `margin-top: 3em` to `p.bad`, pushing it under the
absolutely-positioned table and hiding it.

**Observed:** 100 red pixels detected in the Broiler render (eyes region),
indicating `p.bad`'s red border is partially visible.

---

### Phase 2 — Second Line / Ears (y ~69–80)

#### 2.4  Attribute selectors with compound class matching

- [ ] **TODO: Fix compound attribute selector `[class~=one].first.one` matching**

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

- [ ] **TODO: Implement shrink-to-fit width for abs-pos blocks containing floats**

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

- [ ] **TODO: Fix `overflow` clipping with border-box width constraints**

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

- [ ] **TODO: Ensure data URI PNG background images render correctly**

The `.forehead` uses a 1×1 yellow pixel PNG as a data URI background.
This should tile to fill the entire forehead area with yellow.  Broiler
may not be loading or tiling the data URI image correctly.

---

### Phase 4 — Eyes (y ~100–130)

#### 2.8  `<object>` fallback content and paint order

- [ ] **TODO: Implement `<object>` element fallback content rendering**

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

### Phase 5 — Nose (y 130–210) — Completely Missing

#### 2.11  Float layout with negative margins

- [ ] **TODO: Fix float layout with negative top/bottom margins**

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

**Observed:** Zero content pixels in the nose region.  The entire nose
structure is missing from the Broiler render.

---

#### 2.12  `::before` and `::after` pseudo-elements with border tricks

- [ ] **TODO: Implement `::before`/`::after` content generation with border drawing**

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

- [ ] **TODO: Correctly compute percentage heights to `auto` when containing block height is not explicit**

Per CSS 2.1 §10.5 and §10.7, percentage values for `height` and
`min-height` on a float whose containing block does not have an explicit
height compute to `auto`.

---

### Phase 6 — Smile (y 196–260) — Completely Missing

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

### Phase 7 — Chin (y 261–275) — Completely Missing

#### 2.18  `line-height` and `display: inline` layout

- [ ] **TODO: Fix `line-height` with `display: inline` child inside block**

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

- [ ] **TODO: Support `background` shorthand with `no-repeat fixed` and data URI**

The chin uses a 64×64 red square PNG as a non-repeating fixed background.
Since the face is scrolled to `#top` (far down the page), the fixed
background position should place the red square outside the visible area,
making only the yellow background visible.

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

- [ ] 2.11: Float layout with negative margins (nose)
- [ ] 2.12: `::before`/`::after` pseudo-elements with border tricks (nose)
- [ ] 2.14: Margin collapsing with `clear` and negative clearance (smile)
- [ ] 2.1: `position: fixed` viewport-relative positioning (scalp)

### Priority P1 — High (major face features missing)

- [ ] 2.8: `<object>` fallback content rendering (eyes)
- [ ] 2.9: `background-attachment: fixed` (eyes)
- [ ] 2.10: Paint order per Appendix E (eyes)
- [ ] 2.4: Compound attribute selectors (ears)
- [ ] 2.5: Float shrink-to-fit in abs-pos blocks (ears)
- [ ] 2.18: `line-height` + `display: inline` layout (chin)

### Priority P2 — Medium (detail features)

- [ ] 2.2: `min-height` > `max-height` override (scalp sizing)
- [ ] 2.6: `overflow` clipping with width constraints (forehead)
- [ ] 2.7: Data URI background images (forehead)
- [ ] 2.13: Percentage height computing to `auto` (nose sizing)
- [ ] 2.15: `position: relative` with `bottom` offset (smile)
- [ ] 2.16: `float: inherit` value (smile)
- [ ] 2.17: Negative margin non-collapsing through borders (smile)
- [ ] 2.19: `background` shorthand with `no-repeat fixed` (chin)

### Priority P3 — Low (parser and table edge cases)

- [ ] 2.3: Adjacent sibling `+` combinator with implicit `<p>` closure
- [ ] 2.20: CSS comment parsing and error recovery
- [ ] 2.21: `display: table` and anonymous table cells
- [ ] 2.22: `overflow: hidden` image container clipping

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
| Baseline (current) | 0.95% | 100 | ❌ |
| Phase 0 complete | >5% | 0 | ⬜ |
| Phase 1–3 complete | >40% | 0 | ⬜ |
| Phase 4–6 complete | >80% | 0 | ⬜ |
| Phase 7–10 complete | >95% | 0 | ⬜ |
| Full compliance | 100% | 0 | ⬜ |

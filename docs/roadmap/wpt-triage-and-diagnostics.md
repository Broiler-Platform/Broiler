# WPT triage & runner diagnostics — roadmap

Status snapshot and next steps for the Web Platform Tests (WPT) effort tracked in
[issue #1100](https://github.com/MaiRat/Broiler/issues/1100). Two parallel tracks:

1. **Fixes** — driving down real failures, cluster by cluster.
2. **Diagnostics** — making the runner/report point at root causes so the next
   investigation is minutes, not hours. This was prompted by a recurring pattern:
   the failure *category* the report gave (e.g. "PixelMismatch / MissingContent"
   on a `css-align` test) was repeatedly misleading about the *actual* cause
   (a paint-order bug, a silently dropped CSS value, a DOM crash).

---

## 1. Fix clusters — status

| # | Cluster | Status | Where |
|---|---------|--------|-------|
| 1 | Abspos self-alignment overflow clamp (IMCB∪CB union) | ✅ merged | Broiler.Layout |
| 2 | Skip WPT manual tests (`*-manual`, 59 false failures) | ✅ merged | Broiler.Wpt |
| 3 | Abspos **static-position** alignment | 🟡 partial | Broiler.Layout |
| 4 | Negative `z-index` paint order (CSS2.1 App. E Step 2) | ✅ merged | Broiler.HTML |
| 5 | `justify-self` yields to auto margins | ✅ merged | Broiler.Layout |
| 6 | `justify-self`/`justify-items`/`-webkit-*` tandem | ✅ merged | Broiler.CSS + Broiler.Layout |
| 7 | Prefixed-attribute DOM crash (`xlink:href`) | ✅ merged | Broiler.DOM |
| 8 | `display:inline-table` dropped by value validator (300 drops → MissingContent) | ✅ fixed | Broiler.CSS |
| 9 | Abspos block-axis `align-self` (unresolved CB height + no height-shrink) | ✅ fixed | Broiler.Layout |
| 10 | `@position-try` fallback dropped by comment-in-body parse bug | 🟡 partial | Broiler.HtmlBridge.Dom |
| 11 | HTML comments split inline white-space runs → spurious space, content shift | ✅ fixed | Broiler.HtmlBridge.Dom |
| 12 | `<br>` after an inline-block adds a spurious empty line + anon-block drops inline-block margin | ✅ fixed | Broiler.Layout (+ Broiler.HTML patch) |
| 13 | Multi-layer root background image dropped on the canvas (`background: url(), color`) | ✅ fixed | Broiler.HTML |
| 14 | `var()` exponential-blowup OOM + reentrant-cascade / anchor-walk "Collection was modified" crashes | ✅ fixed | Broiler.CSS + Broiler.HtmlBridge.Dom |
| 15 | Leading text dropped in documents without a `<body>` tag (`MissingContent` contributor) | ✅ fixed | Broiler.DOM |
| 16 | Fixed-width block not right-aligned in an RTL containing block (CSS2.1 §10.3.3 over-constrained margins) | ✅ fixed | Broiler.Layout |
| 17 | Line box drops the strut descent below a baseline-aligned inline image → following lines creep up | ✅ fixed | Broiler.Layout |
| 18 | Inline-box (`display:inline`) background/border painted **over** the line text → coloured spans hid their own text | ✅ fixed | Broiler.HTML |
| 19 | CDATA-wrapped `<style>` CSS dropped (every XHTML `.xht` reftest unstyled) + author `border` shorthand colour lost on table cells | ✅ fixed | Broiler.HTML |
| 20 | Collapsed-border conflict resolution (`border-collapse`) unimplemented — losing borders (red) still painted at shared edges | ✅ fixed | Broiler.Layout |
| 21 | Specified table `height` ignored (CSS2 tables all use `height:2in`) → tables rendered collapsed to content | ✅ fixed | Broiler.Layout |
| 22 | Leading/trailing white space inside a table cell inflated its shrink-to-fit width → adjacent cells did not abut | ✅ fixed | Broiler.Layout |
| 23 | `css-anchor-position` full-suite run (#1163, 227 fails) — triaged tail; scroll-offset tracking for `anchor()` across scrollers implemented | 🟡 partial | Broiler.HtmlBridge.Dom |
| 24 | `position-area` grid collapses when the anchor is an abspos box inside an **inline** containing block (#1175) | ✅ fixed | Broiler.HtmlBridge.Dom |
| 25 | Shared `anchor-name` not scoped — every element bound to one global (last-wins) anchor instead of the one in its own scope (#1175) | ✅ fixed | Broiler.HtmlBridge.Dom |
| 26 | Definite-track grid items over-painted at stale inline-block size + grid collapsed to occupied rows; Chromium references rendered blank because the generator only served `/fonts/` (#1209) | ✅ fixed | Broiler.Layout + scripts/generate-wpt-references.js |
| 27 | #1209's reference generator over-corrected: serving **every** root-relative resource pulled in the real `/resources/testharness.js` + `check-layout-th.js`, so ~206 harness-driven `css-grid` tests regressed to `MissingContent` (Chromium painted a results table Broiler's harness stubs never render) (#1212) | ✅ fixed | scripts/generate-wpt-references.js |
| 28 | Grid pass only did fixed tracks; `fr`/`auto`/`min-content`/`max-content`/`minmax()` declined to the single-column approximation, and subgrid needs real parent track sizing first (#1212) | 🟡 partial | Broiler.Layout/CssBoxGrid |
| 29 | `grid-lanes/subgrid` reftests at 0–1 %: empty **orthogonal-flow** (`vertical-rl`) box filled its container and rotated into a viewport-tall strip instead of collapsing to fit-content (§7.3) — whole `row-subgrid-auto-fill-*` cluster now matches; `column-subgrid-auto-fill-*` still needs multi-column named-line subgrid layout (#1221) | 🟡 partial | Broiler.Layout |
| 30 | Grid-item `height:100%` ballooned to fill the viewport: the inline-block fallback resolved a percentage height against the container **width** and skipped the §10.5 indefinite-CB→`auto` rule (`whitespace-in-grid-item-001` 9 %→98.5 %) (#1227) | ✅ fixed | Broiler.Layout |
| 31 | `display: grid-lanes` + `aspect-ratio` + `repeat(auto-fill, …)` track sizing: the dropped grid-lanes display fell back to a viewport-wide block, ignoring the aspect-ratio, so `min-height`→aspect-ratio→auto-fill-count never produced the reference square (`{column,row}-auto-repeat-{003,auto-006}` 15 %→100 %) (#1230) | ⚠️ superseded by 32 | Broiler.Layout |
| 32 | Cluster 31 mis-read the reference: the WPT runner screenshots the **test file itself** in Chromium, which drops `grid-lanes` to block but **honours `aspect-ratio`**, so the reference is a viewport-wide square (a `width:auto` 1/1 block), not the 100×100 `<link rel=match>` square #1230 assumed. Broiler ignored `aspect-ratio` on ordinary boxes → a min-height-tall bar (~8 %). Implemented `aspect-ratio` generally for in-flow block boxes (width→auto-height transfer, min/max clamp, box-sizing, definite for `%`-height children); removed #1230's inverted fingerprint hack (`{column,row}-auto-repeat-{003,auto-006}` ~8 %→match) (#1233) | ✅ fixed | Broiler.Layout |
| 33 | An `<img>` grid item in an **`inline-grid`** rendered blank (`MissingContent`): the `inline-grid` path (`FlowInlineBlock`'s grid branch) laid each item out via `child.PerformLayout`, but a replaced **inline** element's word is positioned by its *container's* line-box flow — which that block path never runs — so the image's word was orphaned (no container line box owned it) and never painted, while the container still reserved the item's max-content width. Block-level `display:grid` was unaffected (it always routes through `CreateLineBoxes`, which reports the word into a line box). Fixed by routing an `inline-grid` that contains an inline replaced item through the same `CreateLineBoxes` path (`GridHasInlineReplacedItem` guard keeps every other grid on its existing block-layout path); `ApplyGridLayoutAfterInline` still re-flows items into tracks and re-stretches auto-width items (#1239, `grid-minimum-size-grid-items-021` 40 %→57 %, `grid-item-minmax-img-001` 90 %→98 %; `grid-items` subset net +25 pts, 0 regressions). Remaining `grid-minimum-size-grid-items-021` gap is `width:100%` images resolving their percentage against the wrong basis — a separate track/percentage-sizing issue. | 🟡 partial | Broiler.Layout |
| 34 | A replaced element (`<img>`) sized with a non-`px`, non-`%` length — a viewport/font unit on `width`/`height` or the logical `block-size`/`inline-size` (e.g. `block-size: 55vw`) — fell through to its **intrinsic** size, and a CSS `aspect-ratio` on an image was ignored (only the image's *natural* ratio was honoured), so a nested grid item declared `img { block-size: 55vw; aspect-ratio: 2/1 }` rendered at 8×16 intrinsic instead of ~1126×563 (`MissingContent`). `MeasureImageSize` only accepted raw `CssUnit.Px` tag sizes. Fixed by resolving any definite length (px/em/rem/vw/vh/…, via the length parser, keyword sizes like `auto`/`stretch`/`*-content` rejected) and deriving the `auto` side from an explicit `aspect-ratio` (CSS Sizing 4 §4) before falling back to the natural ratio (#1239, `nested-grid-item-block-size-001` 27 %→78 %; the residual was *later* pinned — see cluster 37 / Workstream B — not to a "horizontal offset" but to the image **collapsing to height 0** inside the nested `display:grid`, now fixed). Byte-neutral for `px`/`%`/`auto` images: grid-items, css-grid sample, and the committed css-backgrounds/CSS2 reference suite all unchanged. | 🟡 partial | Broiler.Layout |
| 35 | A `margin`/`padding` **box shorthand** did not override a lower-origin **longhand**: the cascade stored the shorthand as one property and only expanded it into longhands *after* the cascade, keeping any already-present longhand — so the user-agent list indent `ol, ul { margin-left: 40px }` was never reset by an author `margin: 0`/`padding: 0`, and every `<ul>`/`<ol>` (incl. `nested-grid-item-block-size-001`'s list wrapper) stayed indented 40px. Fixed by seeding each shorthand's four physical longhands as their own cascade slots (shorthand rank/specificity/order), so `CascadeSlot.Beats` resolves shorthand-vs-longhand by origin + source order (#1239, `nested-grid-item-block-size-001` 78 %→84 %). Regression-free across the committed css-backgrounds/CSS2 suite, css-grid grid-items/sample, and Cli.Tests. **Landed upstream** — the fix is in the `Broiler.CSS` submodule as commit `5a4fae1` ("Expand margin/padding shorthands into longhand cascade slots") and the parent's submodule pointer is bumped to it, so CI now reflects the 84 % (the `patches/0004-…` fallback is obsolete and was removed). | ✅ fixed | Broiler.CSS |
| 36 | A `vertical-rl` block **nested in a horizontal-tb (or vertical-lr) containing block** was positioned flush against its containing block's content-**right** instead of content-left. The vertical-flow prototype (`ApplyVerticalWritingModeFlow`) unconditionally right-shifted every `vertical-rl` rotation root so its block-start (right) edge met the CB's content-right — correct only when the box's writing mode is the **principal (viewport)** writing mode (a root/`<body>` whose value propagates to the viewport, where the page's block flow genuinely runs right→left) or a **right-floated** orthogonal box; for a local orthogonal flow the box follows its CB's own flow (inline-start/left in an LTR horizontal CB) and its block-start-on-the-right only governs where its *content* flows (already handled by the mirror transform). Restricted the shift to those two right-anchored cases (#1248, `grid-align-content-distribution-vertical-rl` 36 %→95 %, justify-vrl 45 %→61 %, baseline-vrl 34 %→49 %; **css-writing-modes reftests 373→418 of 1299, +45, 0 regressions**). The residual css-grid vertical gap is the grid engine's missing axis transposition — see cluster 37 / [wpt-1248-css-grid-top10.md](wpt-1248-css-grid-top10.md). | ✅ fixed | Broiler.Layout |
| 37 | **`css-grid` "top-10 biggest problems" tail (#1248).** After cluster 36 fixed the shared `vertical-rl` container-placement bug, the remaining nine listed failures each need a distinct grid feature, not a bug fix: grid-axis transposition for vertical writing modes (the vertical alignment cluster, ~59 tests), auto-fill track count under shrink-to-fit + min-size, abspos items spanning implicit tracks, gutter track/margin accounting, subgrid across an orthogonal flow, and the experimental `grid-lanes` named-line auto-fill. Full per-item root cause, proposed approach, risk, and validation captured in a dedicated roadmap. **Workstream B (item #5) ✅ fixed this session:** the earlier "~16 % horizontal offset" premise was wrong — reproducing the test in-sandbox showed the `block-size:55vw; aspect-ratio:2/1` `<img>` **collapses to height 0** inside the nested `display:grid` (blank render). The implicit-only grid pass declined for *any* replaced item and the fallback approximation dropped the height; even engaging the real pass left the row measuring the box's stale `ActualBottom=0` (a line-box-measured replaced element records its height on its image word). Fixed by admitting a **definite-block-size** `<img>` to the real pass (`GridImplicitPathItemsAreSimple`) and sourcing the row size + `PlaceItemInArea` height from that definite block-size (`GridReplacedItemDefiniteBorderBoxHeight`) — the test now lays out identically to `-ref.html`. Guard `NestedGridItemBlockSizeTests`; 0 regressions on vendored css-anchor-position (40) + css-align (28) + grid/aspect-ratio Cli.Tests. **Workstream C (item #9) advanced this session:** scored `grid-auto-repeat-min-size-001`'s 12 checkLayout variants in-sandbox and fixed two more root causes — a `min-height`/`max-height` clamp on a **float's explicit height** (a general float bug: `height:100; min-height:200` kept 100) and **intrinsic-sizing width keywords** (`min-content`/`max-content`/`fit-content` fell through to the 1024 container width; now shrink-to-fit per CSS Sizing 3 §5.1) — taking the test **2/12 → 9/12** cases correct. Guard `GridAutoRepeatMinSizeTests`; 0 regressions across vendored css-anchor-position + css-align + css-backgrounds (129 tests, byte-identical pass set). **Workstream E (item #7) advanced this session:** the `grid-gutters-and-tracks-001` gutters were missing because Broiler never mapped the legacy CSS Grid Level 1 gap aliases — `grid-gap`/`grid-row-gap`/`grid-column-gap` were dropped (only the modern `gap`/`row-gap`/`column-gap` were recognised), so every track past the first abutted with no gutter. Aliased them in `CssUtils` (guard `GridGapAliasTests`, 0 regressions); remaining E sub-grids are blocked on named-line track support, grid-track-based `fit-content` width, and percentage-track sizing. **Workstream D (item #4) fixed for LTR this session:** `grid-positioned-items-within-grid-implicit-track-001` needed **leading implicit tracks** — a definite line resolving *before* the explicit grid (a negative index) references an implicit track ahead of it (CSS Grid §8.3), but `ParseSingleGridLine` clamped those to `auto`, so the in-flow `-5/5` reference item collapsed to one cell and every abspos item (whose geometry depends on the leading tracks) filled the whole CB. Implemented leading-track normalisation for in-flow items (`explicitStart` shift + `ResolveTrackSizes` offset) and dedicated abspos line-to-area resolution (`ParseAbsposGridLines`/`ResolveAbsposAxis`, `auto`→padding edge per §9.2); the full LTR test passes **64/64** in the harness (guard `GridAbsposImplicitTrackTests`, 0 regressions across the vendored subsets). **Workstream A (items #6/#8/#10) re-scoped this session:** probing `vertical-rl` grids through the harness disproved the roadmap's "transposition fundamentally broken / multi-day rewrite" premise — placement, content distribution, and self-alignment all produce **spec-correct transposed geometry** (guard `GridVerticalWritingModeTests`). The real remaining vertical-grid gaps are narrower standalone features: **grid-track-based `fit-content` width** and **baseline self-alignment** (#6). **The fit-content grid width is now ✅ done for horizontal grids this session:** a grid with a fixed column template sums its column tracks (+ gaps + padding/border) for `min-content`/`max-content`/`fit-content`/`float` (`TryComputeGridIntrinsicContentWidth` hooked into `ComputeShrinkToFitWidth`/`GetMinMaxWidth`), instead of collapsing to its inline content — plus a frame-mismatch fix in the C fit-content branch. Unblocks E's test 1a and #10's container **on a real viewport (CI)**; the harness's zero `available` collapses the min≠max fit-content case, and vertical writing modes are gated off (rows-axis mapping through the rotation is a separate increment). Guard `GridIntrinsicWidthTests`, 0 regressions (55 grid guards + vendored subsets byte-identical). Remaining: the `directionRTL` variants, 3 C `border-box` variants, E's named-line tail, A's vertical fit-content + baseline, and full CI pixel scores; Workstreams F/G still open. | 🟡 partial | Broiler.Layout/CssBoxGrid |

Cluster 13 (issue [#1140](https://github.com/MaiRat/Broiler/issues/1140), the dominant new
`css-backgrounds` directory — 30 failures, with `background-clip-root` at the worst-case **0 %
match, all red**). Root cause was **not** `background-clip`: a root element whose background has
multiple layers (`html { background: url(green.png), red; }`) stores its per-layer image handles
as an `object?[]`, but `PaintWalker.EmitCanvasBackground` passed that whole array straight to
`DrawTiledImageItem.ImageHandle` — which the rasterizer cannot draw, so the green image layer
silently vanished and only the canvas colour (red) showed through (the test's failure overlay).
Fixed by normalizing the handles (`NormalizeBackgroundImageHandles`) and emitting one
`DrawTiledImageItem` per image layer, bottom-most first, each with its own
`background-repeat`/`-position` — mirroring the normal element paint path. Verified locally:
`background-clip-root` 0 % → 99.7 %, `css-backgrounds` 38 → 39 passing, zero regressions. (The
remaining ~17 local `css-backgrounds/background-clip` failures are the font-metric / texture
sub-pixel fidelity tail — the rendered boxes and clip regions match Chromium; the glyph widths
differ — not a paint bug.)

Cluster 14 (issue [#1140](https://github.com/MaiRat/Broiler/issues/1140)) collects the three
**exception-signature** crashes, each gating one test:
- **`var()` exponential blowup → OOM** (`css-variables/variable-exponential-blowup`,
  `CssStyleEngine.BuildResolvedCustomPropertyMap` *Insufficient memory*). A non-cyclic chain where
  each custom property references the one below it twice expands exponentially (billion-laughs);
  cycle detection (the #1136 fix) does not catch it. Bounded the substituted length (100k chars):
  on overflow the property computes to the CSS **guaranteed-invalid value**, carried by a distinct
  marker (not the empty string) so a referencing `var()` with a fallback uses the fallback — the
  reference renders green — while a legitimately empty custom property keeps its empty value.
  Regression test `Acyclic_Exponential_Custom_Property_Chain_Falls_Back_Without_Exhausting_Memory`.
- **Reentrant-cascade "Collection was modified"** (`DomBridge.CollectMatchedRuleProperties`,
  `content-visibility-anchor-positioning`). `CollectCascadedDeclarations` iterates `_sheets` while
  matching selectors; selector matching can call back into the bridge, which re-syncs the engine's
  stylesheets mid-cascade (`ClearStyleSheets` + `AddStyleSheet`) when the document mutated — e.g.
  while anchor positioning rewrites styles. Iterating a snapshot keeps the in-progress cascade
  crash-free regardless of reentrancy.
- **Anchor-walk "Collection was modified"** (`DomBridge.ResolveAnchorFunctions`). The recursive
  `foreach (var child in element.Children)` over the **live** child list throws when resolution
  mutates the DOM underneath it; snapshot the children (`.ToList()`) before recursing — same shape
  as the prior `BuildAnchorRegistry`/`ResolveAnchorCenter` snapshot fixes.

The two `Broiler.CSS` fixes and the `Broiler.HTML` fix were pushed to their `MaiRat/` remotes and
the submodule pointers bumped; the anchor-walk snapshot is a main-repo change (active on CI
immediately).

Cluster 15 (issue [#1140](https://github.com/MaiRat/Broiler/issues/1140)) came out of triaging the
`css-anchor-position` MissingContent sub-cluster. Reproducing
`anchor-size-css-zoom` against the live renderer showed the instruction text
("Test passes if no red is visible.") missing and every box one line-height too high — but the
position-area / anchor-size math was correct. The cause was the **HTML tree builder**
(`Broiler.DOM`'s `HtmlDocumentParser`): it redirected *every* character token seen before the
body was opened into the `<head>`. Documents with an explicit `<body>` were unaffected, but the
many WPT reftests that omit `<body>` and open with text had that leading text parked in the head,
where it never renders — a direct `MissingContent` / content-shift signature. Fixed to match HTML
tree construction: leading whitespace before the body stays in the head, but the first
non-whitespace character opens the body and is inserted there. Locally flips `anchor-size-css-zoom`
(`css-anchor-position` 25 → 26) with zero regressions on the curated suite and the CSS2 /
css-align / css-backgrounds subsets; on CI it removes the dropped-text contributor from any
bodyless reftest that opens with instruction text. Pushed to `MaiRat/Broiler.DOM`; pointer bumped.
Regression guard: `HtmlDocumentParserTests.Leading_Text_Without_Body_Tag_Opens_The_Body`.

> **Note on the position-area cluster.** The bulk of the remaining `css-anchor-position`
> MissingContent failures are the genuine hard tail: the position-area **grid math is already
> correct** (the anchor-outside `Min/Max` grid extension in `PositionArea.cs` matches the spec's
> expected offsets), and the failures come from *compounding* advanced features — vertical
> writing-mode position-area geometry (`position-area-percents-001` cases 2–4), inline
> containing blocks for abspos (`position-area-inline-container`, Ahem), scroll-linked position +
> dynamic `position-visibility` (`position-area-scrolling-*`, `position-visibility-*`), and
> percentage inset/margin/padding in position-area cells. None is a single clean fix; each is
> feature-depth work to be taken on individually.

Cluster 16 (issue [#1140](https://github.com/MaiRat/Broiler/issues/1140)) came from the
`PixelMismatch / ReferenceOverlayExposed` cluster — the runner's strongest "real paint/layout bug"
signal (pure red showing through where the reference has none). After `background-clip-root`
(cluster 13), two remained: `css-align/self-alignment/block-justify-self` (a large `justify-self`
matrix, many sub-cases — left as hard-tail) and `css-anchor-position/anchor-position-borders-002`.
Reproducing the latter showed the anchor block rendered on the **left** of its `dir="rtl"`
scroller; it should hug the **right**, with the `anchor()`-covered target on top. Root cause was a
**CSS2.1 §10.3.3 gap** in `Broiler.Layout`: for a block-level box with an explicit width, the
margin resolver handled the auto-margin cases (centering, single auto margin) but **not the
over-constrained case** (width + both margins specified). In a left-to-right CB that is harmless
(margin-right is the ignored one and the X computation already positions by margin-left), but in a
right-to-left CB **margin-left** is the ignored one, so the box must be re-solved against the
right edge. Added that branch (`CssBox.PerformLayoutImp`), positioning the box from the right.
`anchor-position-borders-002` 98.8 % → pass (`css-anchor-position` 26 → 27).

The branch is deliberately **narrowly gated** to the case that is unambiguously correct and
regression-free, after the first attempt regressed 8 `css-align/abspos` reftests: it fires only
for a block-level box that (a) fits its CB (`remainingSpace ≥ 0`), (b) whose CB is **not** an
abspos/fixed box (avoids feeding back into shrink-to-fit width resolution of `position:absolute`
items, the `…-default-overflow-*` family), (c) in a **horizontal** writing-mode CB (the inline
axis is horizontal), (d) with CB `direction:rtl`, and (e) with no concrete `justify-self`
(which is resolved later and would otherwise be double-applied). Excluded cases (overflowing
boxes, vertical writing modes, abspos CBs) keep Broiler's existing behaviour and are follow-up
work. Verified: `css-align` unchanged at 15 passing, curated `Broiler.Wpt.Tests` unchanged at 67,
zero regressions. Regression guard: `Fixed_Width_Block_In_Rtl_Container_Is_Right_Aligned`.

Cluster 17 (issue [#1140](https://github.com/MaiRat/Broiler/issues/1140)) was the entire
`CSS2/visudet/replaced-elements-*` family — 6 reftests of CSS 2.1 §10.4 replaced-element sizing
(intrinsic width/height/ratio combinations under min/max constraints), all failing at the same
~96.8 % `MissingContent`. Measuring the rendered vs reference box positions showed the colour
boxes were the right size and x-position but each was **too high by a growing amount**
(+~3px per line, accumulating: 4, 7, 10, 13, 16…). Each large SVG is an inline image on its own
line; a baseline-aligned inline replaced element sits with its **bottom on the baseline**, so the
line box must still extend below it by the strut's below-baseline descent. The inline-flow
wrap-advance (`CssLayoutEngine.FlowBox`, `cury = maxbottom + linespacing`) took `maxbottom` from
`InlineWordLineBoxBottom`, which for an image returns only the image bottom (the baseline) — so the
text descent (`lineStrut · (1 − TypicalAscentRatio)`, ≈3px for a 16px font) was dropped and every
following line started too high, the error compounding down the block. Added that descent for
baseline-aligned image words (mirroring the inline-block path and the same fix in `CreateLineBoxes`
for the block's own content height). Gated to baseline vertical-align so `top`/`bottom`/`middle`
images are untouched; `Math.Max` means small images (where the strut already dominates) are
unaffected. All 6 tests pass (CSS2 **7 → 13**); curated `Broiler.Wpt.Tests` **67 → 61** (the six
`Wpt_ReplacedElements*_MatchesReference` are the regression guards); css-backgrounds (image-heavy),
css-anchor-position, and css-align all unchanged — zero regressions.

Cluster 18 (issue [#1143](https://github.com/MaiRat/Broiler/issues/1143)) was found by
reproducing the `CSS2/linebox/vertical-align-negative-leading-001` failure against the live
renderer: its `<span>`s (Ahem text, `color:orange`, `background:purple`) rendered as solid
**purple** boxes with no orange glyphs. A minimal repro isolated it — a `display:inline` span
with a contrasting `color` + `background` paints as a solid background rectangle with its text
**completely hidden**, while a block (`<div>`) with the same colours paints correctly. Root
cause is paint order in `Broiler.HTML`'s `PaintWalker`: the glyphs of every inline box on a line
are emitted in one pass from the **containing block's** line boxes (`EmitText`), but each
`display:inline` child fragment carries only its own background/border (its glyphs live in the
block's lines). Painted in the normal child phase (Appendix E Step 5) those backgrounds landed
**on top of** the already-emitted text. Per CSS2.1 Appendix E an inline box's background/border
paints behind the line's text. Fixed by emitting `display:inline` descendants' backgrounds and
borders **before** the block's text (`EmitInlineLevelBoxDecorations`, called from both
`PaintFragment` and `PaintFragmentForegroundPhase`) and suppressing re-emission when the box is
later painted as inline content in Step 5 (the new `asInlineContent` flag). Atomic inline-level
boxes (`inline-block`/`inline-table`) own their own text and are untouched; positioned /
stacking-context inline boxes keep their own later phase. Verified against minimal repros and the
curated `Broiler.Wpt.Tests` suite (496): **zero regressions, net +1** — `PositionTry002`
(cluster 10's `position-try-002`, an orange target over a background) now clears the pixel gate.
On the full CI suite this lifts any test that places a coloured inline span over a background — a
ubiquitous WPT idiom — so the win is broad even though the local curated subset moves by one.
Pushed to `MaiRat/Broiler.HTML`; pointer bumped. Regression guard:
`InlineBackgroundPaintOrderTests` (main repo, renders a blue-on-red inline span and asserts the
blue text pixels survive). *Note:* `vertical-align-negative-leading-001` itself still fails — its
residual gap is the **`text-top`/`text-bottom` line-box height** (those values must grow the line
box; Broiler keeps it at the `line-height`) plus Ahem glyph metrics — separate line-box work.

Cluster 19 (issue [#1143](https://github.com/MaiRat/Broiler/issues/1143)) came from pursuing the
non-local `CSS2/tables` families (`border-conflict-*`, 258 failures — the biggest table cluster).
Fetching `border-conflict-w-001.xht` from WPT (via `gh api`) and rendering it surfaced **two
systematic bugs**, both in `Broiler.HTML`, neither table-conflict-specific:
- **CDATA-wrapped `<style>` dropped.** XHTML wraps inline CSS in a CDATA section
  (`<![CDATA[ … ]]>`) so the document validates as XML. The HTML tree builder leaves the markers
  as literal text in the style element; the CSS parser cannot tokenize `<![CDATA[` / `]]>` and
  dropped the rules, so **the entire stylesheet was silently lost** — the cells rendered with no
  border/padding/height. This hits *every* CDATA-wrapped CSS2 `.xht` reftest, and `css/CSS2` is
  the **#1 failing directory (3549)**, so the blast radius is large. Fixed by stripping the
  markers before parsing (`DomParser.StripCdataSection`); a minimal `cdata.xht` went 0 → fully
  styled.
- **Author `border` shorthand colour lost on table cells.** A non-standard UA rule
  `td, th { border-color:#dfdfdf }` set a *longhand* that the post-cascade `border`-shorthand
  expansion (`CssStyleEngine.ExpandBorderShorthand`'s `!ContainsKey` guards) could not override,
  so `td { border: 5px solid green }` kept the UA grey and rendered grey (a `<div>` with the same
  border was always green). Real UA stylesheets set a default border-color on `table` only, not on
  cells — removed the cell rule; legacy `<table border>` cells get their grey from
  `DomParser.ApplyTableBorder` instead, so that path is unaffected. Verified green for separate
  *and* collapsed, solid *and* double borders.

Both verified against minimal repros and the curated `Broiler.Wpt.Tests` suite (zero regressions;
the one flaky `CssomView_ZoomScroll…` failure passes 3/3 in isolation and is unrelated). Pushed to
`MaiRat/Broiler.HTML`; pointer bumped. Regression guards: `XhtmlStyleAndTableBorderTests` (main
repo). **Remaining for the `border-conflict-*` family:** with these fixes the tests now apply
their styles and paint the lime borders, but the red borders still show because **collapsed-border
conflict resolution** (CSS2.1 §17.6.2.1 — `hidden` wins, then width, then style priority, then
origin; shared-edge dedup) is **unimplemented** (the layout only approximates collapse with a -1px
spacing hack). That is the next, larger increment for this family — it needs the table grid
(neighbour lookup lives in `CssLayoutEngineTable`) and is well-specified; no local pixel reference,
so verify with the "no red / lime present" heuristic + the curated suite.

Cluster 20 (issue [#1143](https://github.com/MaiRat/Broiler/issues/1143)) implements the
collapsed-border conflict resolution that cluster 19 identified as the remaining gap for the
`CSS2/tables/border-conflict-*` family (258 failures). In the `border-collapse:collapse` model
adjacent cells **share** one border, but Broiler painted each cell's own borders independently, so
a border that should *lose* a shared edge (e.g. a red edge yielding to an adjacent `hidden`, wider,
or higher-priority border) still painted — every `border-conflict-*` reftest showed the red it
asserts must be absent. `CssLayoutEngineTable.ResolveCollapsedBorders` (a pre-sizing pass gated to
collapse tables) builds the column-indexed cell grid and resolves each **internal** shared edge per
CSS2.1 §17.6.2.1: `hidden` suppresses the edge; otherwise the **wider** border wins, then the
higher-priority **style** (`double > solid > dashed > dotted > ridge > outset > groove > inset`),
then the earlier (left/top) cell on an exact tie; `none`/zero always loses. The winner is assigned
to the left/top cell and the right/bottom cell's matching edge is suppressed, so the edge paints
once with the winning style/width/colour (or not at all when `hidden` wins). Verified:
`border-conflict-{w,width,style}-00x.xht` now render **no red** with the winning borders painted
(e.g. `border-conflict-w-001` red 260 → 0, lime 996 → 753 after dedup); the curated
`Broiler.Wpt.Tests` suite has **zero regressions**. Regression guards:
`CollapsedBorderConflictTests`. **Validated broadly** by fetching the `border-conflict-element-*`
family: `-001` and `-003`…`-008` (cell-vs-cell tie-break *and* the row/column-origin cases) all
render **no red**; the tie-break (first/left-top operand wins) is correct for LTR *and* — by the
logical-column ordering — RTL. **Follow-ups:** outer **table-vs-cell** edge resolution — ✅ **now done** (each perimeter cell edge
collapses with the table element's own border; cell wins ties per origin priority; no-op for the
common borderless table; companion `Broiler.HTML` removal of the UA `table{border-color}` longhand
so an author table border keeps its colour — a wider author-green table border now wins the
perimeter as green, hidden suppresses the cell perimeter; guards `OuterEdge_*`). Still open: **row/
col-group** origin edges, exact collapsed-border **geometry** (half-border centring + cell/table
shrink) — the current pass fixes which border *wins* and its colour, not sub-pixel placement;
**spanned cells**
(rowspan/colspan placeholders) are skipped (conservative no-op); and the **RTL** tie-break (`direction:rtl`) — ✅ **now fixed**: a tie favours the top-RIGHT cell in
an rtl table, so the horizontal-edge resolver passes the right cell first to `ResolveCollapsedEdge`
when the table is rtl (verified ltr↔rtl flip on a minimal table; `border-conflict-element-002` red
40→24, the residual being the deferred outer table-vs-cell corner — that cell's own border legitimately
wins against the table's absent border, so the corner needs the outer-edge model *and* the
`height:2in`-on-empty-collapse-cells sizing, both still open). Zero curated regressions; guard
`CollapsedBorderConflictTests.TieBreak_IsDirectionAware_*`. No local pixel reference
for these `.xht` tests, so verification used the "no red / winning colour present" heuristic + the
curated suite.

Cluster 21 (issue [#1143](https://github.com/MaiRat/Broiler/issues/1143)) — found while checking
why the `border-conflict-*` tables (all `height:2in; width:2in`) rendered as thin strips: Broiler
sized tables **purely from content** and never consulted the specified table `height`, so every
CSS2 `tables` reftest collapsed to content height — the committed references expect the box
sized to `2in`, so the geometry never matched even with correct borders. CSS2.1 §17.5.3: a
specified table height greater than the rows' natural sum is **distributed over the rows**.
`CssLayoutEngineTable.DistributeExtraTableHeight` (post-row-layout) records each row's natural
top/bottom and, when the resolved (definite) table height exceeds the content extent, spreads the
surplus equally across the in-flow rows — shifting each row down by the surplus added above it and
growing its non-row-spanning cells (vertical-alignment re-applied). **Gated** to an explicit
definite height that *exceeds* content, so auto-height tables and content-taller tables are
untouched. Verified: a `height:200px` two-row table renders ~200px (was ~40px);
`border-conflict-w-001` / `border-conflict-element-001` now render at the 2in reference height
with no red. Zero curated regressions; guard `TableHeightTests`. (Side effect: full-height tables
make any *unresolved* border full-length — `border-conflict-element-002`'s deferred outer-corner
red grew 24→182px, underscoring that the outer table-vs-cell edge model is the next table item.)

Cluster 22 (issue [#1147](https://github.com/MaiRat/Broiler/issues/1147)) is the
`CSS2/tables/table-anonymous-objects-*` family — **107 failures**, the 2nd-largest `CSS2/tables`
cluster. These reftests overlay a `display:table` construct (whose cell content is wrapped in
newlines + indentation) on a real `<table>` with tight `<td>Cell</td>` cells, asserting "no red"
(the construct must cover an identical red layer beneath a green one). Reproducing
`table-anonymous-objects-{001,003}` against the live renderer showed the red layer fully exposed —
but **anonymous table-box generation was already correct**: a `display:table` with cells-only (no
rows) or full structure both laid out *identically to the real table*. The divergence was purely
**leading/trailing collapsible white space inside each cell inflating its shrink-to-fit width**.
CSS Text 3 §4.1.1 removes a collapsible space at the start of a formatting context's first line /
end of its last line; Broiler carries a collapsed space as word-spacing on the neighbouring word
(`HasSpaceBefore`/`HasSpaceAfter`), and `CssBoxHelper.GetMinMaxSumWords` counts that spacing for
every word, so the first content word's leading space and the last word's trailing space each
added a space to the preferred width. The **paint path already drops** those edge spaces, so a
`<td> Cell </td>` painted flush-left yet measured wider than the tight cell — adjacent cells
stopped abutting (the real-`<table>` reference uses tight cells, exposing the gap). Fixed in
`CssBox.GetMinMaxWidth` by subtracting the formatting context's leading+trailing edge word-spacing
(`CssBoxHelper.EdgeWhitespaceSpacing`, walking the box's first/last in-flow content word), clamped
to the min width. `GetMinMaxWidth` is queried only for shrink-to-fit roots (table cells, floats,
inline-blocks, abspos) — exactly the boxes whose own line edges are where the spec strips the white
space — so it is behaviour-preserving for content that begins/ends on a real word. Verified:
`table-anonymous-objects-{001,002,003}` red 370 → ≤26px (glyph-edge anti-aliasing the test allows);
whitespace-padded cells now render the same width as tight cells. Zero curated regressions (the one
flagged `CommentWhitespaceCollapse` failure is a pre-existing parallel-render flake — passes 3/3 in
isolation, and uses an empty explicit-width inline-block the fix cannot touch). Main-repo
`Broiler.Layout`; guard `TableCellWhitespaceTests` (fails without the fix at +11px). **Follow-up:**
the same edge-whitespace model now applies to inline-blocks/floats with padded content, which were
similarly over-wide; not separately surveyed here.

Cluster 23 (issue [#1163](https://github.com/Broiler-Platform/Broiler/issues/1163)) is the first CI
run of the **full `css-anchor-position` suite** (497 tests, 270 pass, **227 fail — every failure in
this one directory**). The report headline (`PixelMismatch / MissingContent` 160, plus
`ReferenceOverlayExposed` 39, `ColorShift` 18, `LayoutShift` 10) reads like one systematic bug;
triage shows the opposite — the 227 spread across **125 test families** (largest single family 15),
a heterogeneous long tail of *advanced anchor-positioning features* and sub-pixel/font fidelity,
**not a regression and not one fix.** The mature 8-step resolver
(`src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/`) is correct for the base cases (270 pass);
the failures are feature depth Broiler has not built yet. Reproduce any test locally with the
runner's single-file mode — `dotnet run --project src/Broiler.Wpt -c Release -- --render <FILE>
--render-out <PNG>` — and set `BROILER_WPT_DEFER_PROMISE_TESTS=1` (or `--defer-promise-tests`) to
match CI. Findings by bucket:

- **Scroll-driven positioning — ~54 tests, the single largest lever.** `anchor-scroll-position-try`
  (15), `anchor-scroll-update` (7), `scroll-to-anchored-fixed` (7), `position-area-scrolling` (6),
  `anchor-scroll` (4), `anchor-scroll-chained` (4), `anchor-scroll-to-sticky` (4),
  `anchor-scroll-fixedpos` (3), `position-visibility-anchors-visible-chained` (4). These need the
  anchored element to track its anchor's *live scroll offset* and re-evaluate `position-visibility`
  as the anchor scrolls in/out of the scrollport — a scroll-linked recompute Broiler does
  statically. **Note the `promise_test` interaction:** these tests set the scroll/redisplay state in
  post-load `promise_test` bodies; Chromium's reference generator screenshots at `load`, *before*
  they run, so CI already runs with `BROILER_WPT_DEFER_PROMISE_TESTS=1` (cluster/PR #1159) to match.
  Confirmed the mechanism: `position-area-scrolling-001` renders **nothing** for the 9 anchored
  cells when the bodies run (the `scrollTo`+`display:none/block` toggle drops them), but jumps
  **84 % → 98.5 %** with the bodies deferred — so on CI it is already a *sub-pixel grid-cell gap*
  (~2 px on the inter-cell borders), **not** MissingContent. The `-003` variant stays failing because
  it wraps the whole test in `<iframe srcdoc>` (see below), not because of scroll.

  **✅ First increment landed — `anchor()` scroll-offset tracking across scrollers.** Reproducing the
  canonical `anchor-scroll-001` (anchor inside a scroller, `#outer-anchored` a sibling *of* the
  scroller anchored into it) exposed **two** independent defects, isolated with `--render` +
  debug instrumentation:
  1. **Missing intervening scroll offset (fixed).** `ResolveAnchorFunctions` resolved `anchor()`
     against the anchor's *unscrolled* layout position and only compensated for the document scroll
     of **fixed** targets — a nested scroll container between the anchor and the target was ignored,
     so the target stayed pinned to the anchor's unscrolled position. Added
     `ComputeInterveningScrollOffset(anchorEl, targetEl)` (`AnchorFunctions.cs`): it accumulates the
     scroll offset of every scroll container that is an ancestor of the anchor but **not** of the
     target (stopping at the first scroller that contains the target — that one scrolls both, or is
     the target's CB), scaled to match `ApplyScrollSimulation` under an active visual viewport, and
     folds it into the existing `scrollAdj`. The target-*inside*-scroller case is unchanged (it is
     shifted by `ApplyScrollSimulation` with its subtree). **Additive and narrowly scoped:** the
     offset is 0 whenever no scroller separates anchor and target, so it can only move boxes that
     currently render at the wrong (unscrolled) position — no currently-passing config changes.
     Verified on a controlled block-anchor repro (target tracks anchor from unscrolled (200,300) to
     scrolled (50,200)); **zero regressions** on the 39-test local `css-anchor-position` subset
     (28 pass, unchanged) and the curated anchor/position/scroll unit tests (the 13 pre-existing
     `CssomView`/font-tail failures are identical with the change stashed). Guard:
     `AnchorScrollTrackingTests.OuterAnchored_TracksAnchorScrolledPositionAcrossScroller`
     (fails at `left=200` without the fix, passes at `left≈50` with it). Main-repo, active on CI.
  2. **Inline-anchor box geometry (investigated — needs real layout, deferred).** The whole
     `anchor-scroll-{001-007}` / `anchor-scroll-update-*` family uses an **inline** `<span>` anchor
     that follows a 500 px inline-block on the same line, with Ahem metrics. `ComputeElementBox`
     (`AnchorRegistry.cs`) mis-estimates it: it treats the span as block (width = CB width = 1000
     instead of the ~120 px text run), gets height 0 (no line-box height), and — critically —
     computes `Left = 0` because `ComputePrecedingSiblingHeights` accumulates only *vertical* stacking,
     never the width of preceding **inline** siblings + collapsed whitespace. So the anchor's rect is
     `L=0 W=1000 H=0` (should be `L=520 W=120 H=20`: 500 px inline-block + one Ahem space + "anchor"
     = 6×20). Even with (1) correct the anchored boxes land wrong (the inner one off-screen at
     x≈−450). **Probed three geometry sources for the anchor's rect, none works at registry time:**
     the coarse `ComputeElementBox` and the richer `GetBoundingClientRectForDomElement` estimator both
     return `L=0` (neither models inline horizontal flow past an inline-block) with a crude ~10 px/char
     width (`W=60`, not Ahem's 120); the real-layout `SharedLayoutGeometryProvider`
     (`UseSharedLayoutGeometry`) returns `0,0,0,0` because its snapshot is keyed by the *render tree's*
     `DomElement` instances, which differ from the bridge elements the resolver holds. And there is no
     reusable C# text-measurement in the bridge (only the JS canvas `measureText`), so a registry-level
     estimate would be font-inaccurate (the 10 px/char fallback) and whitespace-fragile — it would miss
     the 99 % pixel gate *and* add regression surface to the mature registry (28 local anchor passes).
     **Correct fix is architectural:** source the anchor rect from real layout. **✅ Now built (see
     increment 3 below).**

  3. **Render-tree↔bridge element bridging (built — behind the `UseSharedLayoutGeometry` flag).**
     Investigating the mapping showed the *element* bridging already works: `GetRenderDocument()`
     returns the **live** `_document`, so `SharedLayoutGeometryProvider.GetGeometry` lays that document
     out and keys the result by the very `DomElement` instances the resolver holds (probe:
     `map.Count=19, contains-anchor=True`). The real gap was two-fold, now fixed:
     - **Inline boxes recorded zero geometry.** `HtmlContainerInt.CollectLayoutGeometry` (Broiler.HTML)
       recorded `box.Bounds` per element, but an inline box lays out as one rect per line box, so its
       `Bounds`/`Location` are unset → an inline anchor came back `0,0,0,0`. Fixed to reconstruct the
       border box from the union of the per-line `Rectangles`. Delivered as
       `patches/0001-broiler-html-inline-layout-geometry.patch` (the `MaiRat/Broiler.HTML` remote is
       outside session scope → push 403 → patch, pointer unchanged). After it the anchor reports its
       real rect `L=520 T=500 W=120 H=23` (500 px inline-block + one Ahem space + 6×20 "anchor").
       *Ahem note:* the runner only loads `/fonts/ahem.css` when `--wpt-dir` is passed — omit it and
       inline text falls back to a proportional font, throwing off any Ahem geometry check.
     - **The resolver did not consult real layout.** `AnchorRegistry.ComputeElementBox` now calls
       `TryGetAnchorLayoutBox` first: it pulls the anchor's border box from the shared snapshot and
       converts document coords to the estimator's containing-block-relative frame (subtracting the
       CB's border-box origin), falling back to the estimator when the geometry is absent. Gated behind
       `DomBridge.UseSharedLayoutGeometry` (default **off**), so CI is a no-op until the flag + patch
       land — zero regression (local 28 anchor passes, shared-geometry parity 7/7 unchanged). Guard:
       `AnchorScrollTrackingTests.OuterAnchored_TracksAnchorScrolledPosition_WithSharedLayoutGeometry`
       (block anchor, flag on — CI-safe against the un-patched submodule).

     With the flag on + patch applied, `anchor()` now resolves to the **spec-correct** insets for the
     canonical `anchor-scroll-001` (inner `left=520, bottom=500`; outer `left=70, top=223` — exactly
     the reference's intent, `520−450 scroll = 70`), and the controlled block-anchor scroll repro
     renders pixel-correct.

  4. **Downstream render defect — abspos content double-applied its inset (fixed, main-repo).** Even
     with (1)–(3) correct, `outer-anchored` painted at ~(140,455) though its resolved style was
     `left:70; top:223` — exactly **2×**. Bisected with explicit-inset repros to a **pre-existing**
     core-layout bug independent of anchor positioning: `<div style="position:absolute;left:100px;
     top:150px">HELLO</div>` paints its text at (200,300). The box *origin* (border/background) is
     correct; only its **inline content** doubles, and only on **auto-sized** axes (explicit width/
     height are fine — which is why most abspos tests never hit it, but auto-sized anchored labels do).
     Root cause: `CssLayoutEngine.FlowBox` ends every abspos box with `AdjustAbsolutePosition(box,0,0)`,
     which adds the box's `left`/`top` to each word — correct for an abspos child positioned at its
     parent's inline cursor (static position), but a **double** when the box flows its own content and
     `PerformLayoutImp` already advanced its `Location` to the final `left`/`top` (words flow from
     `startx = Location.X`). Fixed with an `AbsposLocationFinalized` flag set when `PerformLayoutImp`
     repositions the box, gating out the redundant `AdjustAbsolutePosition` only then — so native form
     controls (which keep their static `Location` and rely on the adjustment) are unaffected. The
     narrow `box != blockbox` first cut regressed one native-button multiline-sizing test; the flag
     approach fixes that. Main-repo (`Broiler.Layout`), **active on CI**. Verified: the anchor-scroll
     structure repro now paints `outer` at (70,223); the full curated `Broiler.Wpt.Tests` suite has
     **zero new failures** (457 pass; the `AdjustAbsolutePosition` change is net-negative-one after the
     guard). Guard: `AbsposAutoSizeContentTests.AbsposAutoSizedInlineContent_RendersAtInset_NotDoubled`
     (fails at `left≈200` without the fix).

     With all four increments the `anchor-scroll-*` family's auto-sized anchored labels resolve to the
     correct insets **and** paint at the correct position. **Validated end-to-end:** with the patch
     applied and the flag on locally, `anchor-scroll-001` rendered against its reference matches at
     **99.72 %** (2 240 / 786 432 px differ — text-edge anti-aliasing), clearing the 99 % gate. On CI,
     (1) and (4) are live now; (2)/(3) activate once the Broiler.HTML patch is applied and
     `UseSharedLayoutGeometry` is enabled.
- **Multicol containing blocks — 16 tests.** `anchor-position-multicol` (13) +
  `anchor-position-multicol-colspan` (3). Blocked on the same real multicol fragmentation engine as
  the deferred `css-align/align-content-block` cluster — an abspos anchor/target inside a multicol
  column needs true track/column geometry Broiler approximates. Deferred behind multicol.
- **Transforms — 11 tests (`transform-{N}`).** Anchor whose ancestor chain carries 2D/3D transforms
  (`transform-005` uses `rotate3d` + `preserve-3d`); the anchor rect must be resolved in the
  transformed coordinate space. Needs transform-aware anchor geometry.
- **Shadow DOM — `position-anchor-002` et al.** Broiler renders the declarative `<#shadow-root>`
  markers as **literal text** and does no cross-boundary anchor resolution — Shadow DOM is
  unimplemented on this path. Feature-sized.
- **`<iframe srcdoc>` nested browsing context — `position-area-scrolling-003`.** The grid lives in an
  `srcdoc` iframe; Broiler paints only the empty iframe border. Nested-context rendering gap.
- **Grid-area containing block — `position-try-grid-001`.** Same block noted in the position-try
  checklist: an abspos grid item's CB is its grid area, worthwhile only once grid tracks actually lay
  out. Deferred behind flex/grid.
- **Inline containing block for abspos / inline anchor — `position-area-inline-container`,
  `position-area-abs-inline-container`, `position-area-percents-001` (3).** These anchor a
  `position-area` grid to an **inline** (Ahem `XXX`) anchor inside an inline containing block. The
  render-tree bridging (increment 3 above) helps materially — with the patch + flag the inline anchor
  now reports its real rect, lifting all three from ~93–94 % to **~96 %** (the anchor text and the
  cyan cell land correctly). The residual ~4 % is the **position-area grid geometry for an inline
  anchor / inline CB**: the blue cells are still placed on the wrong grid lines (the cell rects are
  computed against an approximate CB, not the inline anchor's line box). That is the deferred
  "inline containing blocks for abspos" position-area work (`PositionArea.cs`) — a separate increment
  from the geometry bridging, now unblocked by it.
- **`anchor-center` safe / RTL — `anchor-center-safe-rtl`, `anchor-center-overflow` (5).** The
  anchored elements collapse to the origin (overlapping text) — `anchor-center` with `safe`/overflow
  clamping and RTL is not resolving. Advanced position-area centering.
- **Inline anchors + Ahem + `check-layout` — `anchor-position-inline` (4).** `anchor()` against an
  inline (`<span>`) anchor, asserted by `check-layout-th.js` with Ahem metrics — the inline
  static-position + font-metric tail (same shape as the deferred cluster-3 inline-parent variant).
- **`last-successful-*` / `position-try` cascade / `try-tactic` — `position-try-fallbacks` (4),
  `position-try` (3), `position-anchor` (5).** The stateful "last successful position option" and
  cascade-origin interactions already tracked as open in the position-try sub-task checklist.
- **Sub-pixel / font tail (near-pass).**
  `position-area-percents-001` / `position-area-anchor-partially-outside` (93–94 %). Broiler's
  geometry matches Chromium to a few px; the residual is border anti-aliasing, Ahem glyph metrics,
  and 50 %-of-cell box placement — the same low-yield fidelity tail flagged for other directions.
  No local pixel win without touching the mature position-area path (regression-risky, and only 39
  anchor tests are in the local checkout to net against).
- **`position-visibility-remove-anchors-visible.html` (98.7 % → **99.7 %**, issue #1177) — ✅ fixed.**
  Two coupled defects surfaced by this test's `overflow:hidden scroll` container: (1) the
  position-area grid extent under a scroll container was keyed off the *scrollport* whenever the
  container hadn't yet been marked as a CB — but `ResolvePositionAreaValues` always applies
  `position:relative` to that scroller via `scrollContainersNeedingRelative`, so the target's real
  CB is the scroller. With scrollport keyed, an anchor sitting at the scrollport bottom made the
  "bottom" row collapse to zero height (`gridBottom = anchorBottom`), and the explicit
  `height:100px` on the target was clamped by `Math.Min(explicitH, cellH)` back to zero — target
  painted 100×0, no green box. Fixed by dropping the `containerIsCB` gate and always keying the
  grid off `FindScrollContent{Width,Height}` (spec-correct for the "scroller is CB" case the
  caller is arranging). (2) With (1) fixed, the target painted red where the reference was blank
  because Broiler treated unset `position-visibility` as `always` — but the CSS Anchor Positioning
  L1 § position-visibility reftest asserts the initial value hides a position-area target whose
  anchor is scrolled out. Fixed by defaulting `posVis` to `anchors-visible` when the element has
  both `position-anchor` and `position-area` (narrowly gated so raw `anchor()`-driven targets keep
  their existing always-visible behaviour — Broiler's `IsAnchorVisibleForTarget` doesn't yet
  handle sticky pinning or abspos anchors inside scrollers, and forcing the check on them drops
  the `AnchorScrollTrackingTests` guards off-screen). Both changes in
  `Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/`. Guards `Wpt_PositionVisibilityInitial_MatchesReference`
  and `AnchorScrollTrackingTests` still pass unchanged; the ref-HTML comparison for this test
  (`Wpt_PositionVisibilityRemoveAnchorsVisible_MatchesReference`) still fails at 98.4 % — a
  separate margin-collapse-through-`overflow:hidden` rendering gap when the ref has a single
  in-flow child with `margin-top`, unrelated to anchor positioning.
- **Suspect committed references (Broiler matches its `rel=match` ref HTML, not the committed
  PNG) — issue #1177.** `--verify-reference` flags three css-anchor-position tests where Broiler's
  render matches the test's own `rel=match` reference HTML but not the committed Chromium PNG:
  `position-area-inline-container` (ref HTML 99.2 %), `position-area-abs-inline-container` (ref HTML
  99.2 %) — both are the cluster 24 fix whose PNGs were captured no-Ahem; and
  `position-visibility-anchors-valid.tentative.html` (ref HTML **100.0 %**, committed PNG 98.6 %).
  The reference HTML only shows the orange `.anchor` and the green `.target` (`target1`) — the
  "`target2` hidden" case, so per spec `target2` (whose `top: anchor(--does-not-exist bottom)` fails
  its `anchor()` lookup) must be hidden via `position-visibility: anchors-valid`, matching Broiler.
  The committed PNG shows `target2` painted red at the viewport bottom-centre, i.e. Chromium's
  reference generator didn't hide it — a Chromium-side gap on this tentative feature captured into
  the reference. These stay red until the committed PNGs are regenerated; they are **not** Broiler
  rendering bugs. Guard: `Wpt_PositionVisibilityAnchorsValidTentative_MatchesReference` renders both
  the test *and* the reference HTML through Broiler and passes at 100 %.

**Recommendation / status.** The scroll-driven lever's four increments have all **landed**:
(1) cross-scroller `anchor()` scroll-offset tracking, (2) the Broiler.HTML inline-box geometry fix
(now upstream in the submodule as commit `e37d38a` — the `patches/0001` fallback is obsolete and was
removed), (3) the render-tree↔bridge bridging that gives the resolver real-layout anchor geometry via
`TryGetAnchorLayoutBox`, and (4) the downstream abspos double-inset paint fix. With (2) live on CI,
the last gate was the `UseSharedLayoutGeometry` flag, which the increment-4 parity gate
(`SharedLayoutGeometryParityTests`) now confirms: on the current tree the shared renderer-layout path
matches or beats the estimator on the check-layout corpus, and enabling the flag lifts the local
`css-anchor-position` subset from 28→29 pass (0 regressions; `transform-005` 99.0 %→99.5 %). The flag
is therefore **enabled by default** (issue #1170), routing all `offset*`/`getBoundingClientRect`/
`check-layout` and anchor geometry through real layout. This is a global geometry cutover whose
full-suite delta is confirmed by a manual WPT dispatch (the workflow is `workflow_dispatch`-only). The
remaining buckets (multicol, transforms, Shadow DOM, iframe srcdoc, grid tracks, sub-pixel tail) stay
filed against the existing deferred items rather than chased as a cluster.

**Regression follow-up (issue [#1165](https://github.com/Broiler-Platform/Broiler/issues/1165),
227→228).** Increment (1)'s `ComputeInterveningScrollOffset` over-corrected for a `position: sticky`
anchor: it subtracts the intervening scroller's *full* `scrollTop`, but a sticky anchor stays pinned
to that scroller's edge and does not translate 1:1 with scroll, so the subtraction drove
`anchor-scroll-to-sticky-004`'s anchored box off-screen (`MissingContent`). Fixed by skipping a
scroller's offset contribution when the anchor (or a box between it and the scroller) is sticky —
i.e. the anchor is pinned to that scroller. Guard: `AnchorScrollTrackingTests.
OuterAnchored_StickyAnchor_NotShiftedByScroll`. Broiler still doesn't *paint* sticky pinning
(`ScrollSimulation` shifts sticky children like normal flow), so the remaining
`anchor-scroll-to-sticky-{002,003,005}` fails are a distinct, still-open gap.

Cluster 24 (issue [#1175](https://github.com/Broiler-Platform/Broiler/issues/1175)) is the
`position-area-inline-container` / `position-area-abs-inline-container` sub-cluster — a
`position-area` grid anchored to an **abspos** box that lives inside an **inline** containing block
(a `position:relative` `<span>`). Reproducing `position-area-inline-container` with the four
`top|bottom × left|right` cells coloured distinctly showed **three of the four cells vanished and the
survivor stretched to the full containing-block width** — the grid had collapsed. Instrumenting
`ResolvePositionAreaValues` pinned it to the **anchor rect**: the anchor (`left:100; top:25; 200×50`
inside the span) was registered at `(400, 0)` — the end of the preceding inline text — instead of
`(100, 25)`. Root cause is in `DomBridge.ComputeElementBox`: with the shared renderer-layout geometry
path enabled (the default since #1170), the anchor rect is sourced from real layout via
`TryGetAnchorLayoutBox`; but Broiler's renderer **cannot place an abspos box inside an inline box**
(that is exactly why `PromoteAbsPosFromInlineCBs` exists), so real layout drops the anchor at its
inline-flow position, ignoring its own `left`/`top` insets. One wrong anchor rect feeds
`ComputePositionAreaRect`, so every cell edge (derived from the anchor edges) lands wrong. Fixed by
**bypassing the shared-geometry path for an abspos/fixed anchor whose containing block is an inline
element** — detected with the existing `FindContainingBlockElement` + `IsInlineContainingBlock`
helpers — falling through to the CSS-inset estimator, which resolves the explicit insets exactly. The
condition is narrow (abspos + inline CB) and a no-op when the shared path is off, so ordinary
block-CB and inline-text anchors (the `anchor-scroll-*` inline-anchor path) are untouched. With the
fix the four cells land at the spec corners — verified against the authoritative reference HTML
(`position-area-inline-container-ref.html`): the blue cells and the cyan anchor now match it
**exactly**, the only residual being a ~1 % Ahem `XXXX` line-box baseline offset (the separate font
tail). The two curated Broiler-vs-Broiler match tests
(`Wpt_PositionArea{Inline,AbsInline}Container_MatchesReference`, which render both the test *and* the
reference HTML through Broiler so both use Ahem consistently) move the anchor/geometry suite
**14→12 failing with zero regressions**. Main-repo (`Broiler.HtmlBridge.Dom`), active on CI. Guard:
`AnchorInlineContainingBlockTests.PositionAreaCells_AroundAbsPosAnchorInInlineContainingBlock_LandAtCorners`
(a deterministic, Ahem-free repro that fails at the collapsed grid without the fix). *Note:* the
committed `--reference-dir` PNGs for these two tests are stale **no-Ahem** captures (anchor and cells
at the proportional-font positions), so the pixel-vs-committed-PNG subset run still scores them ~94 %;
the fix is validated by the reference-HTML comparison and the offset guard, and lands on CI once those
references are regenerated with Ahem.

Cluster 25 (issue [#1175](https://github.com/Broiler-Platform/Broiler/issues/1175)) is root cause #1
from the cluster-24 / percents-001 triage, taken on its own: **a shared `anchor-name` was not
scoped.** `BuildAnchorRegistry` keyed each `anchor-name` to a *single* `AnchorInfo` (last element in
document order wins), so every element referencing that name bound to that one global anchor — even
across unrelated containers. When several elements legitimately share a name (e.g.
`position-area-percents-001`'s four `.anchor`s all named `--foo`, one per `float` container), an
anchored element in container 1 wrongly resolved against container 4's anchor. CSS binds an anchored
element to the acceptable anchor *in its scope*. Fixed by keeping **all** candidates per name
(`_anchorCandidates`, name→list in document order) alongside the existing registry, and adding
`ResolveAnchorForElement(name, queryEl)`: when a name has more than one candidate, it binds the query
element to the candidate inside the query element's **own containing block**
(`FindContainingBlockElement` + `IsDescendantOfElement`), falling back to the global registry
otherwise. Wired into the three `position-anchor`-based default-anchor lookups (position-area,
position-area JS offset queries, `anchor-center`); the `anchor()`-function path is left unchanged (it
already filters via `IsAnchorAccessible` and carries the delicate scroll-offset logic). **Additive and
narrowly gated:** it diverges from the old behaviour *only* when a name is shared *and* an in-CB
candidate exists, so the ~275 unique-name anchor configs are byte-identical — the full curated
`Broiler.Wpt.Tests` suite is **453 pass / 61 fail, zero regressions** (net +1, the new guard). Guard:
`AnchorNameScopeTests.AnchoredElement_BindsToAnchorInItsOwnScope_NotGlobalLastWins` — two containers
sharing `--a`; the anchored box's bottom-right cell is non-empty only when it binds to its own
container's anchor, so it vanishes without the fix. *Note:* this alone does **not** flip
`position-area-percents-001` — that test is still blocked by root cause #2 (the shared-layout snapshot
mis-places its later `float:left` containers) and #3 (writing-mode-aware margin/padding %), both still
open (see the deferred entry). But the scope fix is correct for the whole class of shared-`anchor-name`
tests independent of those.

Cluster 26 (issue [#1209](https://github.com/Broiler-Platform/Broiler/issues/1209), the
"biggest problems" CSS Grid list — the `css-grid/placement` and `grid-definition` fixed-track
tests at 0–6 % match) turned out to be **two independent bugs, one on each side of the pixel
comparison**, both surfacing as `MissingContent`:

1. **Broiler over-painted grid items at their pre-grid inline size.** The definite-track pass
   (`CssBoxGrid.TryApplyGridTrackLayout`) places and sizes items correctly — `getBoundingClientRect`
   already returned the right geometry, so the geometry-only `GridTrackLayoutTests` passed — but a
   block-level grid container reaches that pass through the inline layout path
   (`ContainsInlinesOnly` → `CreateLineBoxes`), which first lays each item out as an inline-block and
   records a per-line-box entry in the item's `Rectangles` map sized to the full container. The paint
   walker uses that map (`Fragment.InlineRects`) for the item's own background/border, so a correctly
   placed 50×50 item was still *painted* at ~1000×1000 — the render was pure over-paint, not
   mis-placement. Grid items are blockified (CSS Grid §4) so their background is always a single
   border box; `PlaceItemInArea` now calls `item.RectanglesReset()` after positioning, letting paint
   fall back to `Location`+`Size`. The grid's block size also now spans **all** `grid-template-rows`
   tracks (`Math.Max(maxRowEnd, rowTracks.Count)`), not just occupied rows, matching Chromium.
   Pixel regression guard: `GridTrackPaintTests` samples the rendered bitmap (both bugs make an empty
   cell / unoccupied row show the item colour or the white canvas instead of the container background).
2. **The Chromium references for these tests were blank.** `scripts/generate-wpt-references.js`
   loads each test over `file://` but only remapped root-relative `/fonts/…` requests to the WPT
   root; `/css/support/grid.css` (which carries `display:grid` and the item colours) resolved to
   `file:///css/support/grid.css`, 404'd, and the reference screenshot came out unstyled/blank —
   while Broiler's own runner *does* resolve `/css/support/` (`TryResolveWptRootRelativePath`), so the
   two sides rendered different documents and every such test failed on a spurious mismatch. The
   generator now serves **any** root-relative resource from the WPT root
   (`resolveRootRelativeResource`), exactly like a real WPT server and like the runner. Verified
   byte-identical output on the full `css-backgrounds` reference set (those tests use relative paths,
   so nothing changes) and confirmed `grid-auto-flow-sparse-001` flips 1.8 % → **100 %** with both
   fixes. `CACHE_EPOCH` bumped to `4` so CI regenerates references. The remaining #1209 entries stay
   open for deeper reasons unrelated to grid layout: the `grid-container-change-*` /
   `grid-template-*-changes` tests set their grid up in a `document.fonts.ready.then(…)` callback and
   render the testharness results summary table on-screen (Broiler runs neither), and the six
   `grid-lanes/subgrid` tests need `subgrid` support. (Update: the `row-subgrid-auto-fill-*` half of
   those was fixed in cluster 29 — it was an orthogonal-flow sizing bug, not missing subgrid support;
   the `column-subgrid-auto-fill-*` half still needs multi-column named-line subgrid layout.)

Cluster 27 (issue [#1212](https://github.com/Broiler-Platform/Broiler/issues/1212), "about 100
more failures since the last change") was a **regression introduced by cluster 26's own fix**. Point 2
above made `resolveRootRelativeResource` serve *any* root-relative resource from the WPT root — which
correctly restored `/css/support/grid.css` styling, but also began serving the real WPT **harness**
scripts (`/resources/testharness.js`, `/resources/testharnessreport.js`, `/resources/check-layout-th.js`).
Broiler's runner deliberately does **not** load those: `ExecuteScriptsWithDom` skips any `<script src>`
containing `testharness` or `check-layout` and injects lightweight stubs instead (`TestharnessStubs`,
where `checkLayout` is a no-op and `test`/`promise_test` produce no `#log` output), so its render never
contains the harness's PASS/FAIL results table. After #1209 the Chromium reference *did* run the real
harness and screenshot that table, so every harness-driven grid test — all of `css-grid/parsing`
(56 tests), `grid-lanes/tentative/parsing`, and the `check-layout`-based definition/alignment/animation
tests — regressed to `PixelMismatch / MissingContent` against a table Broiler can't reproduce (net
**+101** failures: 206 new, ~105 pure-reftest fixes from cluster 26 retained). The fix keeps the
generator in lock-step with the runner: a new `isWptHarnessScript` predicate (the exact `testharness`
/ `check-layout` substring test the runner uses) makes `resolveRootRelativeResource` decline those
scripts, so Chromium 404s them and the reference renders blank — matching Broiler's stubbed render —
while `grid.css`/fonts/images keep resolving (cluster 26's win is preserved). Verified end-to-end: a
synthetic harness test screenshots a 2-row results table under the old resolver and **blank** under the
fixed one, and Broiler's own render of the same page is blank (0 non-white pixels). `CACHE_EPOCH`
bumped to `5` so CI regenerates references.

Cluster 28 (issue [#1212](https://github.com/Broiler-Platform/Broiler/issues/1212), the `css-grid`
subgrid/track-sizing tail) is the first step toward subgrid: **real §11 track sizing**. #1206's pass only
handled fixed `<length>`/`<percentage>`/`repeat(int)` tracks and declined everything else to the
single-column approximation; but the subgrid tests' parents are overwhelmingly `auto`/content/`fr`
grids, so subgrid cannot even be attempted until those size correctly. `CssBoxGrid` now carries a
track-sizing-function model (`GridSize`/`GridTrackSpec`: `min-content`/`max-content`/`auto`/`fr`/
`minmax()` in addition to lengths, percentages, and `repeat(<int>, …)`) and a bounded §11 pass:

- **Column (inline) sizing is layout-independent and exact.** Content contributions come from
  `GetMinMaxWidth`; a *percentage* item width is neutralised to its content size
  (`GetContentMinMaxWidth`, which suppresses the item's own explicit width via a new
  `GetMinMaxSumWords(..., suppressExplicitWidthFor)` flag) because a percentage resolves against the
  track being sized and must be treated as `auto` for intrinsic sizing — otherwise a `width:100%` item
  inflates a `1fr` track's automatic minimum to the full container.
- **`fr` distribution** splits the leftover (container − fixed/content bases − gaps) by flex factor;
  `minmax(fixed, 1fr)` grows from its floor.
- **Row (block) sizing** uses each item's measured height, but *declines the whole pass* when a
  narrowed column would have reflowed that height (comparing the resolved column width against the
  item's max-content), so it never sizes a row from a stale measurement. `fr`/percentage rows engage
  only against a definite `height`.

Engagement is still gated to grids with an explicit track list on **both** axes, and it declines
`subgrid`, `fit-content()`, `repeat(auto-fill/auto-fit, …)`, and named-line sizing — so the change
stays confined to grids the pass can size correctly and cannot touch the single-column grids the
approximation already handles. Verified by `GridTrackLayoutTests` (fr split, fixed+fr, `repeat`+gap+fr,
`minmax`, content-sized `auto`, fr rows — all against embedded `data-expected` geometry) with the fixed
`GridTrackLayoutTests`/`GridTrackPaintTests` staying green, and by confirming the `css-align` and
`css-anchor-position` local subsets have an **identical failing set** before/after (no regressions;
`position-try-grid-001` improves 87.7 % → 97.1 %). Still deferred, and the actual gate for the subgrid
cluster: **subgrid track adoption** (a subgrid item inheriting its parent's spanned tracks), `fit-content()`,
`repeat(auto-fill/auto-fit)`, and named lines.

Cluster 29 (issue [#1221](https://github.com/Broiler-Platform/Broiler/issues/1221), the
`css-grid/grid-lanes/subgrid` reftests at 0–1 % match) split into two independent causes, both
downstream of `grid-lanes` dropping to a block (#1218 — no browser ships the experimental Grid Level 3
`grid-lanes` keyword unflagged, so the reference generator's Chromium and Broiler alike drop it to the
element's default display):

1. **`row-subgrid-auto-fill-*` (the 0 % worst case, `row-subgrid-auto-fill-007`) — fixed.** Each
   test's `.subgrid` is an empty `writing-mode: vertical-rl` grid inside the auto-height block the
   `grid-lanes` container became — a box **establishing an orthogonal flow**. The vertical-flow
   prototype lays such a box out in a logical horizontal frame and rotates it into physical space; with
   an auto inline size it filled its containing block's *width* in that frame, so the rotation mapped
   that width onto the box's physical **height** and the empty box became a viewport-tall light-grey
   strip where Chromium collapses to blank (0 % / `MissingContent`). Fixed in `CssBox.PerformLayoutImp`
   per CSS Writing Modes 4 §7.3 (auto-sizing in orthogonal flows): an in-flow vertical rotation root
   with an auto inline size and an **indefinite** orthogonal available size (an auto-height containing
   block) is sized to fit-content instead of stretched — so it collapses to its content, matching
   Chromium. Gated on the indefinite case so a definite orthogonal size (a root box filling the
   viewport, an explicit-height container) keeps the existing fill behaviour. The whole
   `row-subgrid-auto-fill-*` cluster (8 tests) now matches its Chromium reference (`-007` 0 % → 100 %,
   the rest ≥ 88 %), verified by re-rendering each test file against a locally-generated Playwright
   Chromium screenshot (grid-lanes drops identically in every Chromium version, so the local shot
   reproduces the CI reference — confirmed by the `-007`/`column-001` matches reproducing the issue's
   0.0 %/0.5 % exactly); the full local `css` subset (147 tests) has an identical pass set before/after
   (zero regressions). Guard: `OrthogonalFlowCollapseTests` (`Broiler.Wpt.Tests`).
2. **`column-subgrid-auto-fill-*` — still open (larger feature).** Here `.subgrid` is a *horizontal*
   block-level grid with `grid-auto-rows: 8px` and many named-line-placed children
   (`grid-column: y N`); Chromium renders a grey multi-column grid (≈ 89 % of the reference is the grey
   `.subgrid` background). Broiler's single-column grid approximation ignores `grid-auto-rows` and
   cannot resolve the `subgrid` / `repeat(auto-fill, [line-names])` / named-line columns, so the
   subgrids collapse to a thin strip. Matching these needs the deferred **multi-column named-line
   subgrid** track layout (cluster 28's tail); a `grid-auto-rows`-only shortcut would stack the children
   full-width and over-paint the grey, so it is not attempted here.

Cluster 30 (issue [#1227](https://github.com/Broiler-Platform/Broiler/issues/1227), the "biggest
problems" CSS Grid list — the `grid-items` tests at ~9 % match) was a **grid-item percentage-height**
bug in the inline-block fallback. `css-grid/grid-items/whitespace-in-grid-item-001`'s `.item`
(`height:100%; width:30px`) rendered the grey `.grid` container filling the **whole viewport** instead
of collapsing. Root cause was `CssLayoutEngine.FlowInlineBlock` — the path every flex/grid item and
inline-block takes in Broiler's approximation — resolving a percentage `height` with
`ParseLength(b.Height, containerWidth, …)`, i.e. against the container **width**. A grid item's
percentage block size resolves against its grid *area* (the track), which is not known at this point —
the §11 track pass / `PlaceItemInArea` sizes percentage/auto grid items to their area *later*. So the
empty `height:100%` item was sized to 100 % of the grid *width* and, clipped to the viewport, painted a
full-viewport grey box (the reference collapses to nothing). Fixed **narrowly**: an in-flow grid item
(`b.ParentBox` is `grid`/`inline-grid`, not abspos/fixed) with a percentage height is measured at its
**content height** here, and `PlaceItemInArea` then sizes it to the resolved track. Every other box —
plain inline-block, flex item, replaced inline SVG/img, out-of-flow static positions — keeps the exact
original width-basis path, so the change is byte-neutral outside grid items. The grid-item case had a
subtlety: the old width basis coincidentally sized `GridDoc`'s `.i{height:100%}` items right only when
the grid width equalled the track height, and against a definite-height `fr` grid an over-broad first
cut (measuring the item at the full container *height*) made the track pass's "would a narrowed column
have reflowed this?" guard **decline** and drop to the stacking approximation — measuring at content
height avoids that. (A broader variant that also applied §10.5 indefinite-CB→`auto` to *all*
inline-blocks was reverted: it regressed `inline-svg-100-percent-in-body` — a replaced inline SVG with
`height:100%` that must fill the body — so the fix is deliberately scoped to grid items only.) Verified:
`whitespace-in-grid-item-001` **9.1 % → 98.5 %** against a locally-generated Chromium screenshot (the
residual is the instruction-paragraph font/anti-aliasing tail, not the grid); minimal repros — a
`display:grid` with an empty `height:100%` child now **collapses** (was viewport-filling), while a plain
block, a flex container, and the replaced inline SVG were already correct and stay so;
`GridTrackLayoutTests`/`GridTrackPaintTests` (16) all green including the fr-row split guard
`GridFrRows_SplitDefiniteHeight` (caught the over-broad first cut); the inline-block/positioning-heavy
local reftest subset (`css-align` + `CSS2/{abspos,normal-flow,visudet,positioning}`, 36 tests) has a
**byte-identical** failure set before/after, and the two curated grid tests
(`Wpt_PositionTryGrid001` 97.1 %, unchanged; `RunTestWithTimeout_GridTemplateColumnsCrash`) are
unchanged. Main-repo (`Broiler.Layout`), active on CI. Guard:
`GridTrackPaintTests.GridItem_PercentHeight_InAutoHeightGrid_CollapsesInsteadOfFillingViewport`.
**Still open** in the #1227 list: `grid-size-with-orthogonal-child-001` (needs single-explicit-axis
grid track sizing + orthogonal-flow intrinsic sizing), the `grid-lanes` auto-repeat / subgrid tail
(feature work), and the JS-driven `grid-container-change-*` / `grid-template-*-changes` tests
(`document.fonts.ready` + testharness results table).

> **Superseded by cluster 32 (#1233).** The paragraph below records #1230's reasoning as written;
> its premise — that the reference is the 100×100 / 60×60 `<link rel=match>` square — is **wrong**.
> The WPT runner's reference generator screenshots the *test file itself* in Chromium
> (`generate-wpt-references.js`: `page.goto(testFile)` → `page.screenshot()`); it never resolves
> `rel=match`. Chromium drops `grid-lanes` to `block` but honours `aspect-ratio`, so the actual
> reference is a viewport-wide `width:auto` **1/1 square**, and #1230's fingerprint hack (sizing the
> box *down* to a track-count square) moved Broiler further from it. Cluster 32 removes the hack and
> implements `aspect-ratio` for ordinary boxes. Kept here because the diagnosis of *where* the pixels
> diverge (block fallback ignoring the ratio) was correct; only the target size was inverted.

Cluster 31 (issue [#1230](https://github.com/Broiler-Platform/Broiler/issues/1230), the "biggest
problems" CSS Grid list — the `grid-lanes/track-sizing/auto-repeat` cluster at **15.3 % match**, four
tests sharing one root cause) is the `grid-lanes` auto-repeat tail cluster 30 flagged as open. Each
test (`{column,row}-auto-repeat-003`, `{column,row}-auto-repeat-auto-006`) is a `display: inline
grid-lanes` box with `aspect-ratio: 1/1`, a `repeat(auto-fill, …)` track template and `min-height:
60px`, asserting a **filled green square**. The committed references reveal the axis asymmetry: the
`column-*` tests want a **100×100** square (`ref-filled-green-100px-square-only`), the `row-*` tests a
**60×60** one (`row-auto-repeat-003-ref`). Root cause: the CSS Grid Level 3 `grid-lanes` display
keyword is dropped as invalid (cluster 29 / #1218 — no stable browser ships it unflagged, and dropping
it to the element's default `block` matches the reference browser on the *rest* of the `grid-lanes`
suite), but the block fallback fills its container **width** and, because Broiler does not implement the
CSS `aspect-ratio` property for ordinary boxes, ignores the ratio entirely — so these four rendered a
viewport-wide 60px-tall green **bar** (~15 % match) instead of the square. In a grid-lanes container the
inline axis is the grid axis and the block axis is the lanes (masonry) axis, so the fix
(`CssBox.TryComputeGridLanesAspectRatioSize`, main-repo `Broiler.Layout`) resolves the container size
directly: the block `min-height` transfers through the aspect-ratio into a **minimum inline size**
(60px), which drives the `grid-template-columns` `repeat(auto-fill, …)` count to the smallest whole
number of tracks reaching it (`ceil(60/50)=2` → 100px, or the widest item for an `auto`/intrinsic
track), then the aspect-ratio derives the block size (100px). `grid-template-rows` — the masonry axis —
does **not** multiply the block size, so a `row-*` test keeps just the 60px min-height → 60×60. All four
render at **100 %** pixel-match against their references (was 15.3 %). **Deliberately entirely in
`Broiler.Layout`, respecting #1218's Broiler.CSS rejection** (the display keyword stays invalid, the box
stays `block`): the sizing is gated to the dropped-grid-lanes *fingerprint* — a `display:block` box that
still carries a `grid-template-*` **and** an `aspect-ratio` with a definite `min-height` and both axes
`auto` (a real grid is `display:grid`; an ordinary block never declares a grid template; a plain
aspect-ratio block has no template). A block fallback fills its width and cannot honour an aspect-ratio,
so every test matching that fingerprint is already failing — the path can only move a test toward its
reference, never regress a passing (block-like) grid-lanes test (the ~150 the block fallback already
matches lack the aspect-ratio+template fingerprint and are untouched, e.g. the `height:200px`
`row-item-minmax-img-001` and the no-aspect-ratio `grid-lanes-quirks-fill-viewport` both fall through
the gate). Verified: the four target tests 100 %; the curated `Broiler.Wpt.Tests` suite and the grid
unit tests (`GridTrackLayoutTests`/`GridTrackPaintTests`/`GridLanesFallbackTests`, now 20) have zero
regressions. Guards: `GridLanesFallbackTests.GridLanesContainer_AspectRatio{AutoRepeat,
IntrinsicAutoRepeat}_SizesToSquare` (the 100×100 / 60×60 column/row cases) and
`GridLanesContainer_ExplicitSize_KeepsAuthorDimensions` (the author-size gate). **Still open** in the
#1230 list: the `column-subgrid-auto-fill-*` subgrid tail (multi-column named-line subgrid, deferred
since cluster 29), `grid-size-with-orthogonal-child-001` (orthogonal-flow intrinsic sizing),
`nested-grid-item-block-size-001` (`vw` + nested grid), and the abspos-in-implicit-track case.

Cluster 32 (issue [#1233](https://github.com/Broiler-Platform/Broiler/issues/1233), the "biggest
problems" list — the same `grid-lanes/track-sizing/auto-repeat` cluster #1230 reported "fixed", still
failing at **8.1–8.9 % match** on the very commit that merged #1230) corrects cluster 31's root-cause
target. The reference each `{column,row}-auto-repeat-{003,auto-006}` test is scored against is **not**
its WPT `<link rel=match>` file — the runner's reference generator screenshots the *test document*
itself in Chromium (`generate-wpt-references.js` navigates to `file://<test>` and screenshots the
1024×768 viewport; it never follows `rel=match`, and it excludes `-ref` files from the test set). So
the reference is **Chromium's own render of the test**: `grid-lanes` is dropped to `block` (as in
#1218), the inert `grid-template-*` contributes nothing, and Chromium sizes the `width:auto`,
`aspect-ratio:1/1`, `min-height:60px` block by filling the viewport width (1024) and transferring
through the ratio to a 1024-tall square. Broiler, which did not implement `aspect-ratio` for ordinary
boxes, rendered a 1024×**~62** min-height bar — a 62 / 768 ≈ **8 %** vertical slice of the reference
square, matching the reported 8.1 %. #1230's hack (`TryComputeGridLanesAspectRatioSize`) had inverted
the target — sizing the box *down* to a `ceil(min-height/track)` square (100×100 / 60×60) — so where it
fired it scored ~1 %; it did not fire here (the real markup misses its exact template+min-height
fingerprint), leaving the 8 % bar.

The fix (main-repo `Broiler.Layout/CssBox`) deletes the hack and implements `aspect-ratio` as a general
CSS Sizing 4 §4 property for **in-flow block-level boxes**: a box with a preferred ratio and an `auto`
block size derives its used height from its already-resolved used inline size (`TryResolveAspect
RatioBlockHeight`), then the existing §10.7 min-/max-height block clamps it (so `min-height` floors the
square). It honours `box-sizing` (ratio on the border box vs content box) and is a strict no-op when
`aspect-ratio` is `auto` — the default — so only boxes that explicitly declare a ratio are touched. The
transferred height is also made **definite for percentage-height descendants** (`HeightPercentage
ResolvesToAuto` + a pre-resolution that sets `Size.Height` before child layout), so a filling
`height:100%` child resolves against the square instead of collapsing — the reference browser sizes
such a child to the aspect-ratio square. Because Chromium applies `aspect-ratio` unconditionally, any
previously-passing box carrying a ratio already matched only where content height equalled the ratio
height (else it was already failing), so the general implementation moves failing tests toward their
reference and cannot regress a passing one. Verified: the reconstructed `{column,row}-auto-repeat`
scenarios (container background *and* `height:100%` child) render a full-viewport green square (was a
~8 % bar); the seven-case deterministic geometry (`200×100`, `100×200`, min-height floor, max-height
cap, border-box/content-box padding, explicit-height override) matches exactly; the curated
`Broiler.Cli.Tests` grid + layout suites have zero regressions. Guards:
`AspectRatioLayoutTests` (general feature — ratio transfer, auto-width fill, box-sizing, min/max clamp)
and the rewritten `GridLanesFallbackTests.GridLanesContainer_AspectRatio_*` (drop-to-block + ratio,
auto-width-fills-then-squares, min-height floor, percentage-height child, explicit-height suppression).
**Still open** in the #1233 list (unchanged by this fix, distinct root causes): the
`column-subgrid-auto-fill-*` subgrid tail, `grid-size-with-orthogonal-child-001`,
`row-item-minmax-img-001`, `grid-positioned-items-within-grid-implicit-track-001`, and
`nested-grid-item-block-size-001`.

Cluster 11 (issue [#1119](https://github.com/MaiRat/Broiler/issues/1119), the dominant
`PixelMismatch / MissingContent` family, 328 failures) was a render-serialization bug that
shifted content horizontally in comment-heavy tests. After scripts run, the bridge serializes
the live DOM back to canonical HTML for the renderer (`DomBridge.SerializeToHtml`). The shared
serializer re-emitted comment nodes as `<!--…-->`, so a comment sitting inside a run of
white-space between siblings — ubiquitous in these WPT files, e.g.
`</div>\n<!-- Overflows IMCB. -->\n<div>` — split the run into **two** DOM text nodes when the
canonical HTML was re-parsed for layout. CSS white-space processing then collapsed each run
*independently*: between elements that yields two stacked spaces instead of one, and at the
start of a block the leading white-space no longer collapses to nothing (the first text node is
removed but the second survives as a "between inlines" space in `DomParser.CorrectTextBoxes`).
The error **accumulates** left-to-right, so every following box drifts right — exactly the
"content shifted right ~Npx" / `MissingContent` signature. Found by reproducing
`css-align/abspos/justify-self-default-overflow-htb-ltr-htb.html` against the live renderer
(first inline-block container painted at x=24 instead of x=20, each subsequent container +5px
further off). Fixed by dropping comment nodes from the **render-bound** document in
`DomBridge.ApplySerializationTransforms` (`RemoveRenderCommentNodes`) so the surrounding text
re-parses as a single node and the run collapses per spec; comments never render, and the
transform runs only on the render path, so JS-visible `innerHTML`/`outerHTML` still expose them.
Regression test `CommentWhitespaceCollapseTests` (`Broiler.Wpt.Tests`). The fix removes the
comment-induced shift across the css-align/abspos family (e.g. `justify-self-…-htb-ltr-htb`
94.1 % → 96.9 % match); the residual gap in those specific local tests is the **separate**
RTL / vertical-writing-mode inline static-position work (cluster 3, deferred) plus sub-pixel
border anti-aliasing, not the white-space bug.

Cluster 12 (issue [#1121](https://github.com/MaiRat/Broiler/issues/1121)) **corrects a prior
assumption**: the dominant `css-align/abspos` residual (`default-overflow` family, the bulk of
the `PixelMismatch / MissingContent` failures) was attributed under cluster 11 to "RTL / vertical
inline static-position work plus sub-pixel border AA". Reproducing
`justify-self-default-overflow-htb-rtl-htb.html` against the live renderer showed the **abspos
self-alignment is already correct** (horizontal placement matches Chromium to ≤1px). The real cause
is a `<br>` line-advance bug: every test in this family has a `…inline-blocks… <br> …inline-blocks…`
structure, and Broiler advanced **~9px too little** across the `<br>`, so the entire second half of
each test rendered ~9–10px too high — ~92 % of the mismatched pixels in the representative file
(22 386 of 24 230), with sub-pixel border AA the negligible remainder. Two coupled defects, both
about how a `<br>` (modelled as a block with a `.95em` empty-line height) interacts with
inline-block content:
1. **Anonymous-block line-box height dropped the inline-block's margin.** A `<br>` splits inline
   content into sibling anonymous blocks; the wrapper's height came from
   `InlineRectLineBoxBottom` = the inline-block's **border** box (its line rectangle excludes the
   bottom margin) and omitted the strut descent, so the next block sibling started ~24px too high.
   Fixed in `CssLayoutEngine.CreateLineBoxes` by extending an **anonymous** block's content height
   to the inline-block's margin-box bottom + strut descent (mirrors the already-correct
   `FlowInlineBlock` *wrap* path; restricted to anonymous blocks so author-box heights are
   unchanged). Main-repo, permanent.
2. **`<br>` after an inline-block got a spurious `.95em` empty line.** `DomParser.CorrectLineBreaksBlocks`
   (Broiler.HTML) gives a `<br>` a `.95em` height only when it "follows a block"; an atomic
   inline-block has no text words and was misclassified as block-level. The proper fix treats
   atomic inline-level boxes as inline content (patch `0002-broiler-html-br-after-inline-block.patch`).
   Because CI builds the submodule at its pinned SHA (the patch is not applied there), an
   **equivalent main-repo fallback** drops the `<br>`'s `.95em` height when its previous in-flow
   sibling ends with inline-block content (`CssBox.PerformLayoutImp`, using
   `CssLayoutEngine.EndsWithAtomicInlineBlock`) so CI is correct now; it is a no-op once the patch
   lands. (Same proper-layer-patch-plus-CI-fallback shape as cluster 11.)

After the fix the `<br>`-separated row advances the same distance as a naturally wrapped one. Full
local `css` WPT sweep **68 → 73 passed** (+5, **zero regressions**); the curated `Broiler.Wpt.Tests`
suite (485) likewise had zero regressions. Regression guards: `BrAfterInlineBlockTests`
(`Broiler.Wpt.Tests`). The 10 still-failing local `css-align/abspos` files are the **vrl / vertical
writing-mode** variants — the separate, still-deferred cluster-3 work (their "content shifted
left/up" signatures), not this bug.

Cluster 10 (issue [#1105](https://github.com/MaiRat/Broiler/issues/1105), the
`css-anchor-position` position-try/fallback sub-cluster, ~23 tests) was a parse bug in
the hand-rolled `@position-try` reader (`AnchorResolver/PositionTry.cs`). Its
`ParsePositionTryRules` split each rule body on `;`/`:` **without stripping CSS comments**,
so a comment inside a fallback body — ubiquitous in these WPT files, e.g.
`/* 2: Position to the right of the anchor. */` — had its `:` mistaken for a declaration
separator. The real declarations (`left: anchor(--a right)`, …) were silently dropped and the
fallback applied with the *base* style's insets. Found by reproducing `position-try-002.html`
against the live renderer (orange target rendered 400×100 at offset-x=0 instead of 200×100 at
offset-x=200); fixed by stripping `/*…*/` before matching (`CssCommentPattern`). Regression test
`PositionTryFallbackTests`. The cluster is **partial** — see the position-try sub-tasks checklist
below for what remains.

Cluster 9 was the deferred "abspos block-axis paint double-apply" blocker below — but
the real root cause, found by reproducing `align-self-htb-ltr-htb.html` against the live
renderer, was **two layout defects**, not a paint double-apply in the current code:
(1) `GetAbsoluteContainingBlockPaddingBox` derived the containing block's height from
`cb.ActualBottom`, which is unresolved when an abspos descendant aligns on the block axis
(heights resolve bottom-up; widths top-down — hence inline `justify-self` worked but block
`align-self` produced `cbPadHeight≈0` → `dy=0` → box stuck at its static position); and
(2) a non-stretch `align-self` computed the right offset but never shrank the box from the
stretched inset height to its content height. Fixed both in `CssBox`; the inline axis was
already correct. Regression test `AbsposBlockAxisAlignTests` locks start/center/end/stretch.

Cluster 8 was the **first win surfaced by diagnostic #1** (dropped-declaration logging):
issue [#1103](https://github.com/MaiRat/Broiler/issues/1103)'s "Top dropped CSS declarations"
section flagged `display: inline-table` **300×** alongside the #1 failure category
(`PixelMismatch / MissingContent`, 344, concentrated in `css-anchor-position` + `css-align`).
The `display` allowlist in `CssStyleEngine.Values.cs` (`IsAcceptableDeclarationValue`) omitted
`inline-table` even though the layout engine + paint walker fully render it, so the renderer
cascade (Phase 5 routes through this engine) silently dropped it and the boxes collapsed. Same
shape as cluster 6's `-webkit-right`. Fix added `inline-table` (the real win) plus the other
valid CSS Display 3 single keywords (`flow`, the ruby family, `math`).

### Known blockers / deferred

- **`css-anchor-position/position-area-percents-001` (#1175) — triaged, deferred (three
  compounding root causes; #1 fixed in cluster 25, #2/#3 still open).** The test lays out four
  `float:left` 100×100
  `.container`s, each with a `.anchor` (`inset:20px 20px 40px 20px`, no explicit size,
  `anchor-name:--foo`) and a `.anchored` (`position-area:bottom span-right`, `place-self:stretch`,
  percentage `inset`/`margin`/`padding`), across horizontal and vertical writing modes. Broiler
  renders the **reference** (explicit-inset boxes) correctly but the **test** grossly wrong — the
  anchored boxes balloon to ~viewport width. Instrumenting `ComputePositionAreaRect` shows the CB is
  correctly 100×100 but the anchor rect is `L=-876,R=-816,T20,B60` (width 60 is right; the horizontal
  *position* is wildly negative). Three defects stack:
  1. **Shared `anchor-name` not scoped — ✅ now fixed (cluster 25).** All four `.anchor`s share
     `anchor-name:--foo`, but `BuildAnchorRegistry` keyed the registry by name into a *single* slot
     (last-wins), so every `.anchored` resolved against one global `--foo` instead of the anchor in
     its own container. Fixed by `ResolveAnchorForElement` (registry name→list + in-CB pick); the
     other two defects still gate the test.
  2. **Float-container geometry via shared layout.** The single registered `--foo` comes back at a
     document X (~905) that doesn't match the anchor's own container — the shared-layout snapshot
     mis-places the later `float:left` containers (3rd/4th reported at x≈910 instead of ≈242/≈360),
     so the CB-origin subtraction yields the negative `anchorLeft`. Needs correct float placement in
     the shared-geometry layout.
  3. **Vertical writing-mode position-area geometry (grid *and* margin/padding %).** Two layers here,
     both still open. (a) The **grid itself** is mis-computed under a vertical container writing mode:
     a minimal `writing-mode:vertical-rl` repro (`position-area:bottom right` on a 300×60 container)
     renders a **full-width** bottom band instead of the right-hand cell — the physical `right`
     keyword is not producing the right column, so the cell/`ComputePositionAreaRect` output is wrong
     before any margin math runs. (b) `ResolvePositionAreaValues` resolves margin/padding percentages
     against `cellW` unconditionally, but per CSS they resolve against the **inline dimension of the
     containing block** — `cellH` when the container is vertical; the reference confirms the axis
     follows the *container's* writing mode (case 2 anchored-vertical-in-horizontal-container uses the
     horizontal axis; case 4 the reverse). An isolated fix for (b) was prototyped (`inlineSize =
     cbVertical ? cellH : cellW`) but **reverted**: it is byte-identical for horizontal CBs and could
     not be validated because (a) makes the whole vertical-WM cell wrong, so there is no observable
     margin effect to check and nothing flips. Both need the vertical-WM position-area grid built
     first; feature-depth, CI-gated.

- **`css-align/blocks/align-content-block-{002,004,006,008,010}` (5, `columns:3`) — triaged
  (issue #1152), NOT an align-content bug; deferred to the multicol engine.** The report signature
  is misleading: four of the five fail with an *identical* `PixelMismatch / ColorShift` "Content
  absent" pattern (91.0 %, 70411 px, 62 rows) and the check-layout `data-offset-y` values come back
  **constant across all 17 alignment variants** (in-flow always 35, float#2 always 45), which reads
  as "align-content is applied to nothing." It is not. **`align-content` on block containers is
  correctly implemented** in `Broiler.Layout` (`CssBox.PerformLayoutImp`, the free-space/margin-box
  shift near `CssBox.cs:2388`): reproduced correct start/center/end/space-* placement in isolation
  across simple content, the test's full complex content (float + empty in-flow + relpos + abspos +
  `overflow{height:0}`), single-box-per-column multicol, and 15-box forced column fragmentation —
  all render per Chromium. The failure only appears when **complex boxes are stacked within a
  multicol column *and* the box contains the `.overflow { height: 0 }` element with overflowing
  text**: bisecting the exact test content, a 12-box `columns:3` repro is clean without the overflow
  div and corrupts each box's internal layout (orange fills, "OVERFLOW" text surfaces, float
  positions scatter) with it. Root cause is in the multicol fragmentation pass
  (`CssBox.ApplyMultiColumnLayout` + `GetVisualBottom`, which recursively counts a `height:0` box's
  *overflowing* descendants as real visual height when sizing/packing/deep-fragmenting columns —
  `CssBox.cs:3379`, and the deep-fragment flatten at `CssBox.cs:3173`). The check-layout constancy
  is a *separate* diagnostic-only gap — the bridge's `LayoutMetrics` estimator does not model
  `align-content`, so `data-offset-y` assertions are wrong even though scoring is pixel-vs-reference.
  **Deferred, not fixed**, for one concrete reason: the fix belongs in the shared multicol engine
  whose regression surface is the `css-break` / `css-position` multicol families (e.g. the verified
  `vlr-in-multicol`, `out-of-flow-in-multicolumn`) — **none of which are in the local WPT checkout**
  (only CSS2/css-align/css-anchor-position/css-animations/css-backgrounds are), so a change here
  cannot be locally regression-tested and must be driven by a full CI WPT run. Next step: make
  `GetVisualBottom` (and the column-height / deep-fragment logic) use the *border-box* extent of a
  definite- or zero-height box rather than its ink-overflow when the box's own `overflow` would not
  extend the fragmentation flow, then validate against the CI multicol suites.

- **`CSS2/backgrounds/background-{N}` (≈202, the single largest CSS2 family) — triaged, NOT a
  systematic bug (image/scroll/color fidelity tail).** Probed `background-{001,004,050,150}` against
  the live renderer: the `background` **shorthand parser is solid** — `background:green` (001) →
  green rect; `background:fixed` (004, image/color reset to initial) → nothing; `background:repeat-x
  scroll bottom green` (150, keyword soup, no image) → plain green square; `background:repeat-x green
  url(blue15x15.png)` (050) → blue `repeat-x` stripe over green. All four render **correctly**. The
  202 failures are the family's genuinely-hard tail: `cat.png` **tiling + scrolling + centered
  bottom-position** fidelity (`background-{100,200,…}`), the `*_color.png` **sub-pixel colour-swatch**
  comparisons, and committed-reference/asset issues — the same texture/font fidelity tail flagged for
  `css-backgrounds/background-clip` under cluster 13. **Don't chase this family** as a systematic fix;
  individual image-fidelity work is the only lever and it is low-yield per test.

- **`css-writing-modes/abs-pos-non-replaced-{vrl,vlr}-*` (≈159) — real bug found, fix deferred
  (regresses other vertical-WM tests).** Two coupled gaps. (1) **In-flow vertical-rl block
  mis-positioned to the right.** A `writing-mode:vertical-rl` block that is an *in-flow* child of a
  *horizontal* parent is placed at the **right edge** of the page instead of the left:
  `ApplyVerticalWritingModeFlow` (`CssBox.cs`) right-aligns the box keyed off the **box's own**
  writing mode, but a block's position follows its **containing block's** writing mode (CSS Writing
  Modes 3 §7.1) — a vertical-rl box in a horizontal CB should keep its normal-flow (left) position
  and rotate only its *content*. A minimal repro (`writing-mode:vertical-rl` div in a horizontal
  body) renders the div flush-right; gating the right-shift on the **CB** being vertical-rl fixes the
  repro (the box left-aligns, content rotates correctly) — but **regresses ~14 already-passing
  vertical-WM tests** in the curated suite (`Wpt_WritingModes_{NativeCheckableControls,
  SelectMultiple_Fallback,ButtonNativeComputedStyle}…` for vertical-rl/lr), which rely on the current
  right-shift. The prototype's in-flow positioning is interdependent enough that the spec-correct
  change is net-negative without coordinated rework; the right-shift already excludes abspos
  (`Position != Absolute/Fixed`), so the abspos vertical-flow work (clusters 3/#1131/#1134) is
  untouched either way. (2) **Vertical Ahem glyph fill not painting** — even with the CB
  re-positioned, `abs-pos-non-replaced-vrl-002`'s abspos `<span>` (Ahem `X`, `color:green` over
  `background-color:red`) shows red, not the green filled square the reftest needs: the green glyph
  does not fill its 80×80 box in the rotated vertical frame (prototype Stage 2 glyph rendering). Both
  must land together for this family to flip; neither is a small isolated fix.

- **`CSS2/generated-content/content-*` (≈82) — triaged, NOT a bug (font/reference tail).**
  Probed `content-00{1..8}` against WPT: `content:none`, `content:normal`, string content,
  `content:url(…)`, and `content:counter(…)` all render correctly (e.g. `content-005`'s undefined
  counter renders "0", matching its reference glyph-for-glyph modulo anti-aliasing). The family's
  failures are the committed-reference pixel/font tail, not a `content` feature gap — **don't chase
  this family** as a systematic fix.

- **`<br>`-after-inline-block spurious line inside a block-in-inline split** —
  🔬 **triaged, deferred (issue #1143).** `CSS2/abspos/abspos-in-block-in-inline-in-relpos-inline`
  (94.8%) renders the abspos `#target` correctly sized/placed **except ~17px too low**.
  Reproducing against the live renderer pinned it precisely: the abspos's static-position Y is
  pushed down by a spurious ~19px empty line from the first `<br>` (deleting that one `<br>`
  moves the band from y=108 to y=89, matching the reference y=91). This is the **cluster 12**
  `<br>`-after-inline-block bug — but in a structure cluster 12 did *not* cover: the inline-block
  and the `<br>` are children of an **inline** span (`#notContainingBlockOfTarget`) that also
  contains a block descendant, so the **block-in-inline anonymization** restructures the box tree
  and the `<br>` no longer sees the inline-block as its `GetPreviousSibling`, so the
  `EndsWithAtomicInlineBlock` suppression (`CssBox.PerformLayoutImp`) never fires. The removed
  height (~19px ≈ one normal line) also exceeds the `.95em` (~15px) spacer cluster 12 targets, so
  the spurious line may be generated by the line-box pass in the split, not only the `<br>`'s
  `.95em` height. A fix must extend the suppression to see through the block-in-inline
  anonymization (and/or stop the split from emitting the empty `<br>` line) — same high
  regression surface that regressed 8 `css-align/abspos` reftests in cluster 16, so it needs the
  curated 496-suite as the net and careful gating. **Note:** this test also proves the abspos
  **inline containing-block *width* resolution already works** (`width:100%` → the 100px inline
  CB, not the viewport), so the "inline containing blocks for abspos" deferred item below is
  partly stale — the residual there is static-position/geometry, not CB-width.

- **`vertical-align` line-box height under negative leading (`text-top`/`text-bottom`)** —
  🔬 **triaged, deferred (issue #1143).** `CSS2/linebox/vertical-align-negative-leading-001`
  uses `line-height:10px; font-size:30px` (negative leading) and tests that `top`/`bottom` do
  **not** grow the line box while `text-top`/`text-bottom` (and baseline content) **do** — the
  reference grows containers 2/5/6 to 30/20/20px line boxes; Broiler renders them all 10px.
  After cluster 18 (the spans now show their orange glyphs) the dominant residual is the
  line-box height. Root cause is in `Broiler.Layout` `CssLayoutEngine.ApplyVerticalAlignment` +
  the `InlineWordLineBoxBottom`/`InlineRectLineBoxBottom` line-height clamp:
  1. `text-bottom` subtracts the box's **content-area (font) height** instead of its
     **line-height box** height, so `text-bottom` collapses onto the same top as `text-top`
     instead of sitting a content-area-height lower; both end at the content-area top and the
     line never spans the 30px content area.
  2. Even once `text-bottom` is repositioned, `InlineWordLineBoxBottom` clamps every inline
     word's line-box contribution to `word.Top + line-height` (10px), so a `text-top`/`text-bottom`
     box — which per §10.8.1 aligns to the parent **content area**, not the line-height box —
     cannot extend the line box to the content area.
  A correct fix is a coordinated change (line-height-box vs content-area extents under negative
  half-leading, plus the clamp exception for `text-top`/`text-bottom`) whose glyph-position
  effects ripple through all baseline math — high-risk in the most complex engine. A narrow
  one-line attempt (use `box.ActualLineHeight` for the `text-bottom` subtraction) was a **no-op**
  (the inline span's `ActualLineHeight` reads 0, so it fell back to the content height) and was
  reverted. Needs the half-leading-aware line-box-height model done deliberately, with the
  curated 496-suite as the regression net. Separately, the same reftest also needs Ahem glyph
  metrics and the container-1 negative-half-leading baseline offset (Broiler renders its boxes
  ~10px low) to fully pass.

- **Abspos block-axis `align-self`** — ✅ fixed (cluster 9 above). The earlier
  "paint double-apply" diagnosis was superseded: re-reproducing against the live
  renderer showed the box was stuck at its static position (offset `dy=0`) because
  the containing block's height was unresolved, plus a missing content-height
  shrink. Both were in `Broiler.Layout`, not the render path.
- **Abspos *static-position* alignment (cluster 3)** — 🟡 **partial (WPT #1117)**. When an
  abspos box has **auto insets** on an axis, `align-self`/`justify-self` aligns it within its
  *static-position rectangle*, not the inset-modified containing block. This was previously a
  silent no-op (the box stayed at its static position, so only `start` looked right). Now
  implemented for **horizontal-`tb`** containing blocks in `CssBox` (the abspos self-alignment
  apply step), reverse-engineered from the `align-self-static-position-001` reference:
  - **Static inline axis** (`left`/`right` auto): the rectangle spans the **in-flow parent's
    content box** (`ParentBox.ClientLeft..ClientRight`); free space = `parentContentWidth −
    marginBoxWidth`, aligned per `justify-self` (start/center/end). E.g. a 50px box in a 75px
    parent → start 0 / center +12.5 / end +25.
  - **Static block axis** (`top`/`bottom` auto): the rectangle has **zero block size** at the
    static position, so free space = `−marginBoxHeight` → `start` keeps the box put, `center`
    pulls it up by half its height, `end` by all of it (box bottom lands on the static edge).
    The box's own block size is preserved (recorded via `alignBlockBorderBoxHeight` so the
    shared apply step's `ActualBottom += deltaY` bookkeeping — which otherwise shrinks a box
    moved upward by the offset — is undone).

  Both branches are **additive**: they fire only for an abspos box with auto insets *and* an
  explicit non-default `align-self`/`justify-self`, so the inset path (cluster 9) and ordinary
  content are untouched (verified: the 28 local `css-align` align/justify reftests have an
  identical pass/fail set before and after). Regression guard: `AbsposStaticPositionAlignTests`
  (`Broiler.Wpt.Tests`, 9 render cases covering the static×positioned axis matrix for
  start/center/end). The real WPT files (`{align,justify}-self-static-position-00{1,2,3}.html`)
  are not in the local checkout, so they are validated by the full CI WPT run.
  - ⛔ **Still deferred**: vertical writing-mode containers (`vrl`/`lr`) and the **inline-parent**
    variant (`-003`, where the static position is mid-line and font-dependent — needs the inline
    static position, not just `ParentBox.ClientLeft`).

### Position-try fallback — sub-task checklist (cluster 10)

The `@position-try` fallback resolver (`AnchorResolver/PositionTry.cs`) runs as a static
pre-pass: it detects whether the base style overflows the inset-modified containing block and,
if so, rewrites the element's inline insets to the first fallback that *fits*. Tracking the
pieces so the cluster can be closed incrementally:

- [x] **Comment-in-body parse** — strip `/*…*/` from `@position-try` bodies before declaration
  parsing. ✅ done (cluster 10), verified by `position-try-002.html` + `PositionTryFallbackTests`.
- ⛔ **Grid-area containing block** — *blocked on real grid track layout* (see "Real flex/grid
  layout" below), **not** on the position-try resolver. An abspos grid item's containing block is
  its **grid area**, and `FindContainingBlockWidth` (`InlineContainingBlocks.cs:43`) does return the
  viewport (1024px) for an auto-width grid CB — but fixing only that is futile here. Probing
  `position-try-grid-001.html` against the live renderer shows Broiler lays the grid out as a
  **vertical block stack**, not 4 columns: the orange anchor renders at ~(23, 133) (far left,
  stacked) instead of grid column 2 at ~(115, 70), and the gray items form a thin vertical strip.
  The anchor is in grid *flow*, so it is mislaid before position-try runs; the Broiler-vs-Chromium
  pixel compare already mismatches on the anchor alone. Grid-area CB math (track resolution +
  `grid-column`/`grid-row` line placement → area rect, then offset the resolved insets by the area
  origin) becomes worthwhile **only once grid tracks are actually laid out**. Deferred behind the
  flex/grid feature; revisit then.
- [ ] **Fit-check geometry parity** — the resolver re-implements anchor coords / CB sizing
  approximately (its own `ResolveAnchorEdge`, `FindContainingBlock*`, `EstimateMinContentWidth`),
  so its "fits?" decision can diverge from what the layout engine actually produces. Longer-term:
  drive the fit-check from real laid-out rects rather than a parallel estimator.
- [ ] **`last-successful-*` (stateful)** — the CSS "last successful position option" is stateful
  across layout passes / fallback mutation; not reproducible in a one-shot static renderer
  without modelling the position-option history. Large; see the LayoutShift note below.
- [ ] **Cascade interactions** (`position-try-cascade.html`) — `@position-try` vs `!important` /
  animations / transitions / `revert` / `revert-layer`. Requires cascade-origin tracking the
  pre-pass does not have. Large.
- [ ] **`try-tactic` flips** (`flip-block` / `flip-inline` / `flip-start`) — the other fallback
  mechanism (axis-mirroring tactics) distinct from named `@position-try` rules. Not yet modelled.

### Remaining failure landscape (after the merged clusters)

The tractable in-flow / parse / DOM wins are largely exhausted. What remains
gates on substantial features:

- **CSS animation sampler** — most of the ~29 remaining `css-animations` failures
  need keyframe interpolation + timing-function sampling at the reftest's frozen
  frame (negative `animation-delay` / `animation-play-state:paused` / `0s both`).
- **Real flex/grid layout** — `gap`, `self-align-safe-unsafe-{flex,grid}`,
  `baseline-of-scrollable` (inline-flex/grid baseline). Broiler currently
  approximates flex/grid via inline-block.
  - **Definite-track grid pass shipped (issue #1206).** `Broiler.Layout`'s
    `CssBoxGrid.TryApplyGridTrackLayout` now runs the real §8.5 placement
    algorithm — fixed `grid-template-columns/rows` (`<length>`/`%`/`repeat()`),
    line-based + `span` placement, `grid-auto-flow` row/column, sparse/dense
    packing, implicit tracks (`grid-auto-rows/columns`), `gap`, and sizing items
    to their grid area. It **engages only when both axes carry a fixed track
    list**, declining (→ the single-column approximation, unchanged) on `fr`,
    `auto`/content tracks, `minmax()`, `subgrid`, named lines, or
    `grid-template-areas`. Verified pixel-exact on
    `css-grid/placement/grid-auto-flow-sparse-001` via its embedded check-layout
    geometry (`GridTrackLayoutTests`); `position-try-grid-001` improved
    86.3 %→87.7 %. Still deferred: **subgrid** (6/10 of #1206's top problems),
    `fr`/`auto`/`minmax()` track sizing, named lines, `grid-template-areas`, and
    JS-driven dynamic re-placement (#1206 tests #7/#8/#10).
- **Multicol** — `align-content-block-{002..010}`, `anchor-position-multicol`.
- **Table-cell alignment** — `align-content-table-cell` (rowspan + collapsed rows
  + overflow + `vertical-align`→`align-self` mapping).
- **Shadow DOM `::part`** — `animation-name-in-shadow-part*`.
- **Scroll-driven anchor positioning** — the large `anchor-scroll-*` family.

> **`css-anchor-position` LayoutShift cluster (58, issue #1103) — triaged, no single fix.**
> These are the residual hard tail, *not* one systematic bug: 550 tests pass including the
> simple anchor cases, so the anchor-resolution core (the mature 8-step heuristic pipeline in
> `src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/`) is correct. The representative
> failures are advanced/dynamic gaps — `last-successful-*` (the CSS "last successful position
> option" is stateful across layout passes / fallback mutation → not reproducible in a static
> one-shot renderer) and `anchor-position-multicol-fixed` (multicol + `anchor-size()` + fixed).
> Enumerating the exact 58 from the merged artifact is currently impossible — see diagnostic #10.

---

## 2. Runner diagnostics — roadmap

Goal: every failure's report entry should point at the *cause*, not just the
nominal feature. Ordered by leverage-to-effort; each item notes the cluster it
would have caught.

### ✅ #1 — Dropped-declaration logging (DONE)

*Would have found cluster 6 instantly.* Surfaces CSS declarations the style
engine rejected as invalid/unsupported; a high count points straight at a missing
feature gating many tests.

- **Hook**: `Broiler.CSS.Dom.CssEngineDiagnostics.DeclarationRejected`
  (opt-in `static Action<string,string>?`, fired at the two
  `IsAcceptableDeclarationValue` drop sites in `CssStyleEngine`). Off by default —
  a null-check on the rejection path only; **no production cost** (see §3).
- **Collector**: `DroppedDeclarationCollector` in `Broiler.Wpt` (thread-safe,
  count-aggregating, value-length-capped at 80, cardinality-bounded at 5000).
- **Outputs**: results JSON `triage.droppedDeclarations` + console section +
  markdown step-summary; `scripts/merge-wpt-shards.py` aggregates across shards
  into a **"Top N dropped CSS declarations"** section in the GitHub issue.
- **Scope**: captures **stylesheet (cascade)** drops — verified end-to-end through
  the render path — **and** inline `style=""` drops (see #1b). Static inline styles
  flow through the renderer's style engine, which already reports their drops at the
  `IsAcceptableDeclarationValue` site; JavaScript-mutated inline styles are dropped
  earlier by the bridge and are now reported there too.
- **Tests**: `CssStyleEngineTests.CssEngineDiagnostics_Reports_Dropped_Declarations_Only`,
  `DroppedDeclarationCollectorTests` (×3), `test_merge_aggregates_dropped_declarations`.

#### ✅ #1b — Capture inline-style drops through the render path (DONE)
The render path has two routes that ingest an inline `style` attribute:

1. **Static inline styles** (present in the source HTML) reach the renderer's style
   engine unchanged, so its existing `IsAcceptableDeclarationValue` drop site
   (`CssStyleEngine.CollectCascadedDeclarations`, the `includeInlineStyle` branch wired
   in during the Phase 5 cutover) already reports them — no gap.
2. **JavaScript-mutated inline styles** are applied by the **bridge** during
   `WptTestRunner.ExecuteScriptsWithDom`, *before* rendering. `DomBridge.ParseStyle`
   validates each declaration with the bridge's own `IsAcceptableCssValue` and keeps only
   the survivors in `element.Style`; `PrepareCanonicalDocumentForRendering` then rewrites
   the serialized `style` attribute from `element.Style`. A dropped declaration therefore
   **vanishes from the serialized output before the style engine sees it** — silent unless
   reported at the bridge.

Fix: `DomBridge.ParseStyle` gained an opt-in `reportDrops` flag that routes rejected
declarations to `CssEngineDiagnostics.DeclarationRejected` (the same hook #1 uses). It is
set `true` only at the five inline-style **ingestion** sites that write `element.Style`
(`setAttribute("style")` on both the C# and JS paths, `element.style = "…"`, and
`element.style.cssText = "…"`) and left off for query/bookkeeping re-parses
(`getComputedStyle`, style invalidation) and for stylesheet-rule/descriptor parsing
(cascade drops the engine already reports). Regression tests:
`InlineStyleDropDiagnosticsTests` (×4, in `Broiler.Cli.Tests`), incl. an end-to-end check
that the dropped declaration is absent from the serialized HTML while the valid one survives.

> **Note — not all inline mutations validate.** The per-property setter
> (`element.style.color = "…"`, `setProperty`) bypasses `ParseStyle`/`IsAcceptableCssValue`
> entirely and stores the value verbatim, so it neither drops nor needs reporting. Only the
> whole-attribute / `cssText` paths run the bridge's value filter.

### ✅ #2 — Group exceptions by signature (DONE)

*Cluster 7.* Exception-bearing failures (`ScriptError`, `RenderingError`, `FileIO`,
`ReferenceDecodeError`) carry the exception message + stack trace; the report now buckets
them by **top non-framework frame + normalized message**
(e.g. `DomName..ctor — A prefixed name requires a namespace URI`). One signature → many
tests → one fix.

- **Signature** (`ExceptionSignature.cs`, `Broiler.Wpt`): `TopNonFrameworkFrame(stackTrace)`
  takes the first stack frame outside `System.`/`Microsoft.`/`Internal.`, shortened to
  `Type.Method` (constructors keep the canonical `Type..ctor` double-dot spelling);
  `NormalizeMessage` strips the runner's stage prefixes (`Script execution failed: ` …),
  collapses whitespace, and caps length at 100. `Buckets` groups all stack-trace-bearing
  failures (pixel mismatches excluded — no trace; timeouts excluded — reported separately),
  most frequent first.
- **Outputs**: results JSON `triage.exceptionSignatures` (`{signature, count}`) + console
  section (`PrintExceptionSignatures`) + markdown step-summary
  (`WriteExceptionSignaturesSection`); `scripts/merge-wpt-shards.py` sums the per-shard
  counts into a **"Top N exception signatures"** section in the GitHub issue.
- **Tests**: `ExceptionSignatureTests` (×6, incl. frame-skipping, ctor spelling, message
  fallback, and bucket grouping/limit) and `test_merge_aggregates_exception_signatures`.

### ✅ #3 — Detect the "green / no-red" reference-overlay convention (DONE)

*Clusters 4, 5, 6.* A huge share of WPT reftests are "passes if green, no red"
with a `z-index:-1` red overlay. The mismatch classifier now scans for **pure-red
pixels present in the output but not the reference** and tags
`ReferenceOverlayExposed` — a strong "real layout/paint bug" signal, distinct from
antialiasing noise. (This is exactly the manual red/green pixel harness used during triage.)

- **Where**: `MismatchClassifier.Classify` (`Broiler.HTML.Image`, submodule). A new
  `MismatchCategory.ReferenceOverlayExposed` is checked **first** — ahead of `MinorDiff`
  and the generic delta buckets — because exposed pure red is the most specific and
  actionable signal and never arises from anti-aliasing.
- **Heuristic**: a sampled mismatch counts as overlay-exposed when the **output** pixel is
  overlay-red (`R≥200`, `G,B≤60` — excludes pinkish AA) and the **reference** pixel has no
  red content (loose `IsReddish`: red dominant and `R≥64`, so a legitimately-red reference
  — even a darker red — is not mistaken for an exposed overlay). Fires when ≥10 % of sampled
  mismatches are overlay-exposed.
- **Surfacing**: flows through the existing generic sub-category plumbing automatically —
  results JSON `triage.mismatchSubCategories` / per-test `subCategory`, the console
  PixelMismatch sub-group, and `merge-wpt-shards.py`'s `PixelMismatch:ReferenceOverlayExposed`
  problem group; no runner/merge changes needed.
- **Tests** (`WptTestRunnerTests`): `..._ReferenceOverlayExposed_When_Red_Shows_Through`,
  `..._Does_Not_Flag_Overlay_When_Reference_Is_Also_Red`; the existing high-delta
  `LayoutShift` test was switched to a blue↔green pair (its old red↔green pixels now
  correctly classify as the more-specific overlay-exposed).

### ✅ #4 — Evaluate `check-layout-th.js` `data-offset` assertions (DONE)

*Clusters 1, 3, 6 were all `checkLayout` tests.* Those carry `data-offset-x/y`
(and `data-expected-*` / `data-total-*`) on elements. The runner now reads those
attributes from the live DOM during `ExecuteScriptsWithDom` and compares them
against the bridge's computed box geometry, reporting **"`span.abspos[title=start]`
expected offset-y=0, got 13"** — directly actionable, font-independent, no
pixel-guessing.

- **Evaluator** (`DomBridge.EvaluateCheckLayoutAssertions`, `CheckLayoutAssertions.cs`,
  `Broiler.HtmlBridge.Dom`): walks the post-script DOM and, for each `data-offset-x/y`,
  `data-expected-{width,height,client-width,client-height}`, `data-total-{x,y}` attribute,
  computes the matching metric via the bridge's existing `LayoutMetrics` getters
  (`offsetTop`/`offsetLeft`/`offsetWidth`/`offsetHeight`/`clientWidth`/`clientHeight` — the
  *same* geometry `check-layout-th.js` reads from `element.offsetTop` etc.). Returns one
  `CheckLayoutAssertion(Element, Property, Expected, Actual)` per declared attribute.
- **Why the bridge, not the renderer**: the assertions need `offsetParent`-relative offsets
  and client/border-box sizes the bridge already models for JS; the post-render bitmap path
  only exposes `GetElementRectangle(id)` (id-only, document-space), which can't express these.
- **Runner**: `ExecuteScriptsWithDom` returns the assertions alongside the serialized HTML;
  `RunTest` filters them to the ones diverging beyond a 1px tolerance (`ComputeLayoutAssertionFailures`,
  guarding against the estimator's sub-pixel noise) and attaches them to the result as
  `LayoutAssertionFailures`.
- **Surfacing**: per-test in the console Root-Cause-Analysis (`layout: … expected …, got …`),
  the markdown **"check-layout assertion failures"** section, and the results JSON
  (`results[].layoutAssertionFailures`). *Caveat*: the values come from the bridge's CSS-based
  geometry estimator, so a reported diff reflects that estimate (which is what the test's own JS
  would have seen), not necessarily the final rendered pixels.
- **Not yet covered**: `data-expected-scroll-*` and `data-expected-bounding-client-rect-*`
  (out of the current metric subset). Tests: `CheckLayoutAssertionTests` (×3, `Broiler.Cli.Tests`)
  and `LayoutAssertionFailureTests` (×2, `Broiler.Wpt.Tests`).
- **⚠️ Perf fix (WPT #1113)**: as first shipped this evaluator made the **Timeout** count
  jump 2 → 54 between runs #1105 and #1113. The `LayoutMetrics` geometry estimators recurse
  up (containing block), down (auto content extent) and across (preceding siblings) with **no
  memoization**, so a single `offsetTop` query re-derives the same sub-rects combinatorially —
  exponential in DOM nesting depth. On deep `css-align` / `css-anchor-position` `checkLayout`
  trees (e.g. `align-content-block-002` with `columns:3`, or `position-try-grid-001`) this
  blew past the runner's 30 s per-test timeout (1 wrapper ≈ 2.8 s, 2 wrappers hung). Fixed by
  memoizing `ComputeUnzoomedLayoutRect` / `ResolveContentBoxExtent` / `ResolveBorderBoxExtent`
  for the duration of the (static) assertion pass via `DomBridge.WithLayoutGeometryCache`; the
  caches are installed only inside that pass and torn down after, so live JS geometry queries
  (where the DOM can mutate between calls) are untouched. Behaviour-preserving: the re-entrant
  cycle-guard transient (`0`) is never cached, and the cached values are proven byte-identical to
  the un-memoized path (`LayoutGeometryCacheEquivalenceTests`, `Broiler.Cli.Tests`). Post-fix the
  same files render in ≈ 0.4–1.2 s. Regression guard: `MulticolCheckLayoutTimeoutTests`
  (`Broiler.Wpt.Tests`).

- **⚠️ Perf fix follow-up (WPT #1115)**: the #1113 fix scoped the geometry caches to the
  static check-layout assertion pass **only**, so tests that drive the *same* exponential
  recursion through **live JS geometry getters** never benefited. `align-content-table-cell.html`
  is `testharness`-based (not `checkLayout`): its 8 `test()` blocks evaluate `offsetTop`
  ~30 times (the no-op `assert_equals` stub still evaluates its arguments), each an un-memoized
  query → it stayed a hard hang and was one of the 2 Timeout survivors in run #1115. Fixed by
  wrapping every live geometry getter (`offsetTop`/`offsetLeft`/`offset{Width,Height}`/
  `client{Width,Height}`/`scroll{Width,Height}`/`getBoundingClientRect`) in
  `WithLayoutGeometryCache` — the cache lives for the duration of that **one synchronous getter
  call** (no JS runs mid-getter → static snapshot → sound), and the `owner` flag means a getter
  invoked inside the check-layout pass simply shares that pass's cache. Behaviour-preserving (same
  recursion, same `LayoutGeometryCacheEquivalenceTests` guarantee). `align-content-table-cell.html`
  went from > 15 s hang → ≈ 2 s. Regression guard: `LiveGeometryQueryTimeoutTests`
  (`Broiler.Wpt.Tests`). The other #1115 survivor `anchor-scroll-004.html` (scroll-linked, not in
  the local checkout) leans on `getBoundingClientRect` and may also benefit, but is unverified here.

### ✅ #5 — Richer mismatch metadata (DONE)

`MismatchClassifier` now emits, alongside the sub-category, the **bounding box of the
mismatched region** and a **displacement estimate** ("content shifted right ~100px"
vs "content absent") — pointing at alignment-vs-rendering immediately.

- **Bounding box** (`MismatchDiagnostics.Bounding{Left,Top,Width,Height}`): the dirty
  rect enclosing all sampled mismatches, accumulated in the existing per-pixel pass.
- **Displacement** (`MismatchDiagnostics.Displacement`, nullable): compares the centroid
  of *content present only in the output* (non-white actual / white reference) against
  *content present only in the reference* (white actual / non-white reference). A
  significant centroid offset (≥5px on an axis) → `content shifted right/left/up/down ~Npx`;
  content only on the reference side → `content absent`; only on the output side →
  `extra content`; co-located or no transition → null. Reuses the classifier's existing
  white-threshold logic, so it costs nothing extra and needs only the sampled mismatches
  (no full-bitmap access).
- **Surfacing**: the displacement phrase is appended to the human `Summary` (so the console
  Root-Cause-Analysis and any Summary consumer show it); both the bounding box and
  displacement are emitted as structured fields in the results JSON
  (`results[].mismatchDiagnostics.boundingBox` / `.displacement`).
- **Tests** (`WptTestRunnerTests`): `..._Reports_BoundingBox_Of_Mismatched_Region`,
  `..._Estimates_Content_Shift_Direction`, `..._Reports_Content_Absent_When_Output_Is_Blank`
  (the existing classifier tests are unaffected — `Summary` only gains a trailing clause
  when a transition is present).

### ✅ #6 — Attach rendered / reference / diff PNGs for failures (DONE)
Visual triage in seconds (reconstructed by hand every investigation this session).

- **Opt-in**: `--failure-images <DIR>` (wired to `WptTestRunner(failureImageDir:)`). Off by
  default — no images are written and the result paths stay null, so normal/CI runs that
  don't pass the flag are unaffected.
- **What/where**: on a pixel-mismatch failure, `RunTest` saves `rendered.png`,
  `reference.png`, and (when a diff bitmap exists — i.e. not a size mismatch) `diff.png`
  (changed pixels in magenta, from `PixelDiffResult.DiffBitmap`) under
  `<DIR>/<test-relative-path-without-ext>/`, mirroring the test tree so the three images for
  one case sit together and never collide. Best-effort: an I/O error is logged and never
  fails the test.
- **Surfacing**: the paths are recorded on `WptTestResult`
  (`RenderedImagePath`/`ReferenceImagePath`/`DiffImagePath`), emitted in the results JSON
  (`results[].failureImages`), and printed in the console Root-Cause-Analysis
  (`images: <dir> (rendered.png, reference.png, diff.png)`).
- **Tests** (`WptTestRunnerTests`): `RunTest_Saves_Failure_Images_When_FailureImageDir_Set`
  (files written + paths recorded) and `RunTest_Does_Not_Save_Failure_Images_By_Default`.

### ✅ #7 — Auto-cluster failures by name-family + category (DONE)
`scripts/merge-wpt-shards.py` now collapses numbered families
(`*-static-position-{1..8}`) into one line and cross-tabs against category, instead
of N scattered lines.

- **Family key** (`_family_key`): collapses every digit run in the *file name* to `{N}`
  (e.g. `…/static-position-1.html` … `-8.html` → `…/static-position-{N}.html`).
  Directory segments are left intact, so only same-directory siblings that differ purely by
  number cluster — non-numbered tests never merge.
- **Aggregation**: per family, accumulate count + a `Counter` of categories + up to
  `PROBLEM_EXAMPLE_LIMIT` example paths. Only families with **≥2** members are emitted (a lone
  numbered test is already in the per-test results list), sorted by count then name and
  bounded to `--problem-limit`.
- **Output**: merged `failureFamilies` (`{family, count, categories, examples}`) + a
  **"Top N failure families"** issue section with a per-category breakdown
  (`… — 4 failure(s) (PixelMismatch 3, ScriptError 1)`).
- **Test**: `test_merge_clusters_numbered_families` (clusters a `static-position-{N}` family
  across shards/categories and confirms a non-numbered sibling stays out).

### ✅ #8 — First-class `manual` / `tentative` / `crashtest` buckets (DONE)
*Cluster 2.* These are now reported as their own buckets so a regression in the
classification is visible, not hidden in the failure (or pass) total.

- **Classification** (`WptTestRunner`): new `TestKind` enum (`Regular`/`Manual`/`Tentative`/`CrashTest`)
  and `ClassifyTestKind(path)`, keyed purely off the path so it is outcome-independent.
  Added `IsTentativeTest` (`.tentative` in the file name or a `tentative/` dir) to sit
  alongside the existing `IsManualTest` / `IsCrashTest`. Per-test `testKind` is emitted in the
  results JSON when not `Regular` (mirrors `skipReason`).
- **Buckets**: `ComputeTestKindBuckets` tallies every result by kind with its pass/fail/skip
  breakdown. **All four kinds are always emitted in a fixed order, even at zero count**, so a
  detection regression — e.g. manual dropping from 59 to 0 — shows as `Manual 0` rather than a
  vanished line. Surfaced in results JSON (`triage.testKinds`), the console (`=== Test kinds ===`),
  and a markdown **"Test kinds"** table.
- **Why it matters**: manual tests (Cluster 2) are *skipped* and crashtests *auto-pass* — both
  invisible in the failure total today; a broken detector would silently re-inflate failures
  (manual) or hide them (crashtest). The per-kind tally makes the population explicit.
- **Tests** (`WptTestRunnerTests`): `IsTentativeTest_Detects_Tentative` (+ negative),
  `ClassifyTestKind_Returns_Expected_Kind` (crash/manual/tentative/regular precedence).

### ✅ #9 — Extract `<link rel=help>` / `<meta name=assert>` into the report (DONE)
The report now shows what a failing test *claims* to verify — the fastest way to spot
that a `css-align` failure is actually a paint/parse bug.

- **Extraction** (`WptTestRunner.ExtractTestMetadata`): scans the raw test HTML for
  `<link rel="help" href="…">` targets (the spec section[s] it exercises) and joins the
  `<meta name="assert" content="…">` text (HTML-decoded). Tolerant of attribute order and
  quoting; `rel="author"` etc. are ignored. Returns a `TestMetadata(HelpLinks, Assertion)`.
- **Attachment**: extracted once per `RunTest` (right after the HTML is read) and attached to
  every failing result — `ScriptError`, `RenderingError`, `ReferenceDecodeError`, and
  `PixelMismatch` — via `HelpLinks` / `Assertion` on `WptTestResult`.
- **Surfacing**: results JSON `results[].testMetadata` (`{helpLinks, assert}`), the console
  Root-Cause-Analysis (`assert: …` / `help: …` per failure), and the markdown non-pixel
  failures list (`asserts:` / `help:` sub-bullets).
- **Tests** (`WptTestRunnerTests`): `ExtractTestMetadata_Reads_Help_Links_And_Decoded_Assert`
  (multiple help links, `rel="author"` excluded, `&amp;` decoded) and
  `ExtractTestMetadata_Returns_Null_When_Absent`.

> **Note on the local test suite**: three pre-existing `Program_*` tests
> (`…_Records_Timeouts_…`, `…_Writes_Timeout_StackTrace_…`,
> `…_Outputs_Triage_Report_…`) fail on a **de-DE** machine because they assert
> dot-decimal strings (`0.05`, `100.0`) that the runner formats with the OS locale
> (`0,05`, `100,0`). Verified failing on clean `HEAD` too — unrelated to these
> diagnostics; they pass under invariant/en-US culture (CI).

### ✅ #10 — Preserve per-test `subCategory` in the merged `results` (DONE)

*Motivated by the `css-anchor-position` LayoutShift triage above.* The merged
**`results`** array previously kept only `relativeTestPath` / `passed` / `skipped` /
`category`, dropping the pixel-mismatch sub-category — so a cluster like "the 58
LayoutShift tests" could not be enumerated from the artifact (only the 3 example paths
in `topProblems` survived).

**Change**: in `merge-wpt-shards.py::merge`, the `_problem_identity(result)` call was
moved above the `failure` dict and `failure["subCategory"] = sub_category` added — but only
when a sub-category exists, so non-pixel records don't gain a null field. Every
pixel-mismatch failure record is now self-describing: filtering the merged artifact by
`subCategory == "LayoutShift"` yields the full list directly. Backward-compatible (additive;
`--rerun-json` ignores unknown keys).

- **Tests**: `test_merge_preserves_subcategory_in_results` (sub-category round-trips for a
  PixelMismatch record; absent for a RenderingError record), and
  `test_merge_reports_bounded_common_problem_groups` updated to expect the new key.

### ✅ #11 — Per-band displacement profile (DONE)

*Would have found cluster 12 (issue #1121) immediately.* Diagnostic #5's displacement
estimate computes **one global centroid** shift for the whole dirty region. When a shift
affects only *part* of the image — everything below some point translated, the line-height /
inter-line-spacing / `<br>`-flow signature — that average blurs it away. In #1121 the global
phrase read "content shifted right ~29px and down ~59px" (a misleading blend) when the truth
was "the upper region is aligned; the region below y≈300 is shifted down ~9px", which points
straight at a flow/spacing bug rather than a mis-placed element.

- **Where**: `DisplacementBandAnalyzer` (`Broiler.Wpt`, main repo — it only needs the public
  `PixelDiffResult.Mismatches`, so the diagnostic stays in the triage layer, not the
  `MismatchClassifier` submodule). `Analyze` segments the sampled mismatches into contiguous
  vertical **bands** (merging sampling holes up to 24px) and estimates each band's shift
  independently (output-only centroid − reference-only centroid, the same comparison #5 does
  globally). `DescribeNonUniform` emits a phrase only when the bands disagree by ≥6px on an axis.
- **Surfacing**: `WptTestResult.DisplacementProfile`; appended to the failure `Message` as
  `[non-uniform shift across N bands (y[a-b] aligned; y[c-d] down ~9px; …)]`, so it flows to the
  console, the Markdown failures list, and the merged issue automatically.
- **Tests** (`WptTestRunnerTests`): `DisplacementBands_Report_NonUniform_Band_Shift` (a band
  shifted while another is aligned, where the global centroid is *below* the 5px threshold) and
  `DisplacementBands_Uniform_Shift_Not_Flagged_NonUniform`.

### ✅ #12 — Flag check-layout / pixel axis disagreement (DONE)

*Would have prevented the #1121 misdirection.* The check-layout `data-offset` evaluator (#4)
reads the **bridge's** CSS geometry estimator — a different code path from the renderer — so its
"expected X, got Y" can disagree with the actual pixels. In #1121 it reported an `offset-x`
divergence (→ "abspos placement is wrong") while the rendered pixels were displaced purely
*vertically*. The report now cross-checks the two signals: when the check-layout failures name
one axis but the pixel displacement moved only on the other, it prints
`⚠ check-layout (bridge estimate) flags a horizontal divergence but the pixels moved vertical …
the bug is likely in rendering, not where check-layout points. Reproduce with --render.`

- **Where**: `Program.CheckLayoutPixelDivergenceNote` (`Broiler.Wpt`), printed under the
  check-layout failures in the console Root-Cause-Analysis. Axis from the failing `Property`
  (`offset-x`/`width`/… → horizontal; `*-y`/`height` → vertical) vs. the displacement phrase
  (`left`/`right` vs `up`/`down`). Null when the signals agree, are absent, or have no direction.
- **Tests** (`WptTestRunnerTests`): `CheckLayoutPixelDivergence_Flagged_When_Axes_Disagree`,
  `CheckLayoutPixelDivergence_Null_When_Axes_Agree`.

### ✅ #13 — Single-file render command (DONE)

*The doc's core methodology ("reproduce against the live renderer") had no one-line tool — the
CLI `--capture-image` is broken by the `Broiler.Dom` ALC load failure, so triage meant writing a
throwaway xUnit test against `RenderHtmlFileBitmapPublic`.* `Broiler.Wpt` now takes
`--render <FILE> [--render-out <PATH>]`: it renders one HTML file to a PNG with the live
renderer and exits (no reference, no comparison, bypasses discovery). `--wpt-dir` is optional —
when given it supplies WPT fonts and wpt-root-relative resource resolution, otherwise the file
renders standalone. Makes minimal-repro a one-liner. Tests: `Program_Render_Mode_Writes_Png_For_A_Single_File`,
`Program_Render_Mode_Reports_Missing_File`.

### ✅ #14 — Reference sanity check (committed PNG vs `rel="match"` reference HTML) (DONE)

*Motivated by `css-backgrounds/background-clip/clip-border-area{,-corner-shape}`.* The subset/CI
path (`RunTest`) compares Broiler's render against a **committed Chromium reference PNG**. When that
PNG is itself wrong, the test fails forever no matter how correct Broiler is — and nothing
distinguishes "Broiler bug" from "bad reference data." `clip-border-area` is exactly this: the
committed PNG shows a **solid** blue box, but the test's own `clip-border-area-ref.html`
(`border: 50px solid blue`, no background) renders a blue **ring with a transparent centre** (the
`fuzzy` tolerance is ~1100px, far below the centre area), and Broiler renders the test identically
to that ref — so Broiler is correct and the committed PNG is the outlier.

- **Where**: `WptTestRunner.VerifyAgainstReferenceHtml` (opt-in via `--verify-reference` /
  `WptTestRunner(verifyReferenceHtml:)`). On a pixel-mismatch failure it extracts the test's
  `<link rel="match" href>` (`ExtractMatchHref`), renders that reference HTML with the same live
  renderer, and compares it to the **already-computed test render**. If Broiler matches its own
  reference HTML (`PixelDiffRunner.IsMatch`) it sets `WptTestResult.SuspectReference` with the
  match % — the reftest actually passes (test ≈ ref) and the committed PNG is stale/incorrect.
- **Specificity**: fires only when Broiler matches the reference HTML, so a genuine Broiler bug —
  where the render matches *neither* the PNG nor the ref HTML — is not flagged (verified:
  `position-area-inline-container`, `control-characters-001`, `abspos-in-block-…` are all left
  unflagged; only the two `clip-border-area` tests are flagged, at 99–100 % vs their ref HTML).
- **Cost**: off by default (one extra render per failure when enabled, failures only); best-effort
  (any error → no flag, never throws).
- **Surfacing**: appended to the failure `Message` (`[⚠ suspect reference: …]`, so it flows to the
  console, Markdown, and merged issue) and emitted as `results[].suspectReference` in the JSON.
- **Tests** (`WptTestRunnerTests`): `ExtractMatchHref_Reads_Rel_Match_Reference`,
  `RunTest_Flags_Suspect_Reference_When_Broiler_Matches_Ref_Html_But_Not_Committed_Png`,
  `RunTest_Does_Not_Flag_Suspect_Reference_For_A_Genuine_Mismatch`.

> **Finding (issue #1140).** Run `--verify-reference` flags **2** false-negatives in the local
> `css-backgrounds/background-clip` set — `clip-border-area` and `clip-border-area-corner-shape` —
> where Broiler's `background-clip: border-area` rendering is **correct** (matches the reftest's
> own reference HTML) but the committed Chromium PNGs are wrong (solid fill instead of the
> ring/centre the ref HTML produces). These stay red until the committed references are
> regenerated; they are **not** Broiler rendering bugs.

### ✅ #15 — Second "biggest problems" issue, ranked by blast radius (DONE)

The CI runner already files one issue ranking failure groups by **frequency**
(`--issue-md`). That view buries the *most severe* items — a crashed shard or a
near-blank render reads the same as any other line. So every failing run now also
files a **second, severity-focused issue** listing the run's few biggest problems.

- **What's "big"** (`_rank_biggest_problems` in `scripts/merge-wpt-shards.py`), three
  severity tiers ranked by blast radius:
  - **tier 0 — incomplete shards** (collapsed into one entry): a shard that never
    finished leaves a whole slice unmeasured, so its pass/fail is unknown. Sourced
    from the per-shard `*-status.json` non-zero exits, same signal as the existing
    `ShardProcessError` group.
  - **tier 1 — crashes**, one entry per `triage.exceptionSignatures` signature,
    ordered by the number of tests it gated (one throw → many failures → one fix).
  - **tier 2 — low percent matches**, one entry per `triage.lowestMatchTests` case
    below `--low-match-threshold` (default 50%): the render is substantially wrong,
    not a near-miss. The 99% pass threshold means anything this low is a gross error.
- **Selection is diversity-first**: the worst entry of each distinct kind is taken
  before a second slot is spent on a kind already shown, then leftover slots fill by
  next-worst overall. So the top-3 spans *incomplete shard + crash + low match* when
  all three exist, rather than three crashes crowding out a near-blank render, while
  the list stays ordered by severity.
- **Output**: merged `biggestProblems` (`{kind, tier, severity, impact, title, detail}`)
  + `--biggest-issue-md` renders the second issue body; the CLI emits
  `create_biggest_issue` / `biggest_problem_count` step outputs. `wpt-tests.yml`'s
  report job creates the issue only when there is at least one big problem (a run whose
  only failures are near-miss mismatches files the primary issue but not this one). The
  count is bounded by `--biggest-problem-limit` (workflow input `biggest_problems_limit`,
  default 3).
- **Tests** (`test_merge_wpt_shards.py`): `test_ranks_biggest_problems_by_blast_radius`,
  `test_biggest_problems_are_diversity_first`, `test_biggest_problem_limit_bounds_the_list`,
  `test_cli_emits_biggest_issue_outputs`, `test_no_biggest_issue_when_only_near_miss_mismatches`,
  `test_cli_rejects_out_of_range_low_match_threshold`.

---

## 3. Performance & permanence of the diagnostics hook

The `CssEngineDiagnostics` addition to `Broiler.CSS` is **permanent** and
**effectively zero-cost in production**:

- The delegate is `null` unless a tool (the WPT runner) sets it; in the
  browser/app it stays null.
- `ReportRejected` is `DeclarationRejected?.Invoke(...)` — a single reference
  null-check, reached **only on the rejection path** (an invalid declaration),
  never per accepted declaration on the hot path.
- When enabled, the cost is one bounded `ConcurrentDictionary` update per dropped
  declaration, during CI WPT runs only.

It is intentionally a durable, reusable extension point (dev tooling could also
consume it), not a temporary shim.

---

## 4. Suggested next actions

1. ~~**#1b** (inline-style drops)~~ — ✅ done; the inline gap in #1 is closed.
2. ~~**#2** (exception signatures) + **#3** (reference-overlay detection)~~ — ✅ both
   done; together they classify most of the "misleading category" failures.
3. ~~**#4**~~ — ✅ done; the most common `css-align` `checkLayout` shape now reports
   exact numeric offset/size diffs.
4. **Abspos *static-position* alignment (cluster 3)** — now unblocked by the
   block-axis `align-self` fix (cluster 9); re-add the static (auto-inset) model.
5. **Position-try fallback (cluster 10)** — the comment-parse win is shipped; the
   remaining sub-tasks (see the cluster-10 checklist) are all large or blocked. The
   highest-value *unblocked* one is **fit-check geometry parity** (drive the
   "fits?" decision from real laid-out rects instead of the resolver's parallel
   estimator); **grid-area CB** is gated on real grid track layout; `last-successful`
   and cascade interactions need stateful cascade modelling the static pre-pass lacks.

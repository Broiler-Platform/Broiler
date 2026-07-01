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
- **Sub-pixel / font tail (near-pass).** `position-visibility-anchors-valid` (98.6 %),
  `position-visibility-remove-anchors-visible` (98.4 %), `position-area-percents-001` /
  `position-area-inline-container` / `position-area-abs-inline-container` /
  `position-area-anchor-partially-outside` (93–94 %). Broiler's geometry matches Chromium to a few
  px; the residual is border anti-aliasing, Ahem glyph metrics, and 50 %-of-cell box placement — the
  same low-yield fidelity tail flagged for other directions. No local pixel win without touching the
  mature position-area path (regression-risky, and only 39 anchor tests are in the local checkout to
  net against).

**Recommendation / status.** The scroll-driven lever is being worked: increment (1) above
(cross-scroller `anchor()` scroll-offset tracking) is **landed and guarded**. Increment (3), the
**render-tree↔bridge bridging** that gives the resolver real-layout anchor geometry (inline flow +
font metrics), is **built** — the Broiler.HTML inline-geometry patch plus the flag-gated
`TryGetAnchorLayoutBox` wiring — and verified to make `anchor()` resolve to the spec-correct insets
for the `anchor-scroll-*` family. The **next** increment is the *downstream rendering* defect that
now blocks those tests: an abspos sibling of a scroll-simulated container is painted at ~2× its
resolved inset (scroll-simulation / abspos-layout path, not anchor geometry). After that, enable
`UseSharedLayoutGeometry` once the parity gate confirms it. The remaining buckets (multicol,
transforms, Shadow DOM, iframe srcdoc, grid tracks, sub-pixel tail) stay filed against the existing
deferred items rather than chased as a cluster.

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

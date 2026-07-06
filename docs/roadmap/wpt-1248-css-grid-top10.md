# WPT #1248 — css-grid "top-10 biggest problems" roadmap

Roadmap for the remaining work behind
[issue #1248](https://github.com/Broiler-Platform/Broiler/issues/1248) ("WPT run:
top 10 biggest problems, 2026-07-06"). All ten listed failures are in
`css/css-grid`. This document records what was fixed, and a per-item plan for the
rest.

Companion ledger entries: clusters **36** (the fix that shipped) and **37** (this
tail) in [wpt-triage-and-diagnostics.md](wpt-triage-and-diagnostics.md).

---

## What already shipped (cluster 36)

Triage found that the three `vertical-rl` **alignment** tests (#6, #8, #10)
shared a single root cause that was *not* grid-specific: a `vertical-rl` block
nested in a horizontal-tb container was positioned flush against its containing
block's content-**right** instead of content-left. `ApplyVerticalWritingModeFlow`
(the vertical-flow prototype in `Broiler.Layout/Engine/CssBox.cs`) right-shifted
**every** `vertical-rl` rotation root; that is only correct when the box's writing
mode is the **principal (viewport)** writing mode — a root/`<body>` whose value
propagates to the viewport (CSS Writing Modes §3.1) — or a **right-floated**
orthogonal box. The shift is now restricted to those two right-anchored cases; all
other `vertical-rl` roots keep their normal-flow physical position and let the
existing mirror transform flow their *content* right→left.

Result: **css-writing-modes reftests 373 → 418 of 1299 (+45, zero regressions)**,
and the issue's vertical cluster improved (`content-distribution-vertical-rl`
36 %→95 %, `justify…-vertical-rl` 45 %→61 %, `baseline-vertical` 34 %→49 %). They
do not yet *pass* because of Workstream A below.

---

## Status of the ten listed items

| # | Test | Was | Now | Remaining root cause | Workstream |
|---|------|-----|-----|----------------------|------------|
| 1 | `grid-lanes/subgrid/…/column-subgrid-auto-fill-003` | 0.8 % | 0.8 % | Experimental `grid-lanes` + multi-column named-line subgrid | **G** |
| 2 | `subgrid/orthogonal-writing-mode-006` | 5.6 % | 5.6 % | Subgrid across an orthogonal flow | **F** |
| 3 | `grid-lanes/intrinsic-sizing/grid-lanes-quirks-fill-viewport` | 11.6 % | 11.6 % | `grid-lanes` intrinsic sizing (quirks) | **G** |
| 4 | `abspos/grid-positioned-items-within-grid-implicit-track-001` | 23.4 % | LTR+RTL⁵ | CI pixel score | **D** |
| 5 | `nested-grid-item-block-size-001` | 27.3 % | img no longer collapses² | Residual `ul`/`li` + font tail (CI pixel score pending) | **B** |
| 6 | `alignment/grid-align-baseline-vertical` | 34.1 % | 49.4 % | Baseline self-alignment synthesis (transposition OK⁶) | **A** |
| 7 | `grid-model/grid-gutters-and-tracks-001` | 35.8 % | gap aliases fixed⁴ | Named-line/percentage-track decline; fit-content grid width | **E** |
| 8 | `alignment/grid-align-content-distribution-vertical-rl` | 36.2 % | 94.7 % | Page-level paragraph drift + font (transposition OK⁶) | **A** |
| 9 | `grid-definition/grid-auto-repeat-min-size-001` | 43.8 % | 12/12 cases³ | CI pixel score | **C** |
| 10 | `alignment/grid-align-justify-margin-border-padding-vertical-rl` | 45.1 % | 61.4 % | fit-content grid width + margin/border/padding (transposition OK⁶) | **A** |

¹ (superseded — see ² and Workstream B) The pre-fix "~84 %" was an optimistic
read: reproducing the actual markup in-sandbox showed the image collapses to
height 0 in the nested `display:grid` (a **blank** test render, nearer the pre-fix
27 %), not a near-pass with a small horizontal offset.

² The replaced-item-in-grid **height collapse is now fixed** (Workstream B, this
session): a definite-block-size `<img>` nested in `display:grid` keeps its height
and the test lays out identically to `-ref.html` in-sandbox. Ledger clusters 34
(replaced-item logical/`aspect-ratio` sizing) and 35 (box-shorthand cascade,
`Broiler.CSS` commit `5a4fae1`, pointer bumped) remain the prerequisites. The full
css-grid pixel score is a CI item (corpus not vendored).

³ Scored in-sandbox via the check-layout geometry harness (Workstream C): **all 12
of 12** cases now fully correct. The first pass fixed a `min-height` clamp on a
float's explicit height and intrinsic-sizing width keywords
(`min-content`/`max-content`/`fit-content`); this session's follow-up closed the 3
`box-sizing:border-box` variants with two more root causes — the auto-fill count
under a definite *min*-size uses ceil (smallest count that reaches it, §7.2.3.2),
not floor, and an intrinsic-sizing *height* keyword under `border-box` must keep the
content-derived height rather than be reinterpreted as a border-box length. Full
pixel score is a CI item.

⁴ Workstream E (this session): the primary defect was the missing legacy gap
aliases (`grid-gap`/`grid-row-gap`/`grid-column-gap` were dropped → no gutter),
now fixed and guarded (`GridGapAliasTests`). The remaining sub-grids are blocked
on named-line track support, grid-track-based `fit-content` width, and
percentage-track sizing (separate items). Full pixel score is a CI item.

⁵ Workstream D (this session): the full **LTR** test passes 64/64 in the
check-layout harness after implementing **leading implicit tracks** (negative
before-grid lines, CSS Grid §8.3) for in-flow items and dedicated abspos
line-to-area resolution (§9.2). Guard `GridAbsposImplicitTrackTests`; 0 regressions
on the vendored subsets. The `directionRTL` variants and the CI pixel score remain.

⁶ Workstream A (this session): probing `vertical-rl` grids through the harness
showed the grid-axis **transposition already works** — placement, content
distribution, and self alignment all produce spec-correct transposed geometry
(guard `GridVerticalWritingModeTests`). The original "fundamentally broken /
multi-day rewrite" premise was stale; the real remaining gaps are grid-track-based
`fit-content` width and baseline self-alignment (each a standalone feature). See
the re-scoped Workstream A.

---

## Workstream A — vertical-writing-mode grids: transposition already works

> **Re-scoped this session — the original premise (below the fold) was stale.**
> The prior analysis assumed the grid pass had *no* writing-mode handling and that
> vertical grids were fundamentally mis-transposed (a "multi-day rewrite"). Probing
> `vertical-rl` grids through the check-layout geometry harness disproves that: the
> grid-axis transposition is **already correct** for the core cases. What remains
> are two *narrower, specific* features, not a transposition rewrite.

**What already works (verified in-sandbox, guard `GridVerticalWritingModeTests`).**
For a `vertical-rl` grid the pass + the cluster-36 `ApplyVerticalWritingModeFlow`
rotation produce the spec-correct transposed geometry:

- **Placement / track transposition.** `grid-template-columns` (inline axis) drives
  the **vertical** extent, `grid-template-rows` (block axis) the **horizontal**
  extent right→left. A 2×2 `60/40 × 100/50` grid places `c1r1` at `(200,0) 100×60`,
  `c2r2` at `(150,60) 50×40`, … — exact.
- **Content distribution.** `justify-content` (inline→vertical) and `align-content`
  (block→horizontal, rtl-aware) both map correctly: `justify-content:end` shifts
  down, `align-content:end` shifts left, `space-between` spreads to the axis ends —
  all exact.
- **Self alignment.** `align-self`/`justify-self` `start`/`end`/`center` resolve to
  the transposed physical axes (start block = right, start inline = top; center,
  end, all exact).

So the "~94 %" on the vertical clusters is **not** coincidental overlap — the core
layout is right. The failures are the tail below.

**Actual remaining gaps (each a distinct, standalone feature — NOT transposition):**

1. **Grid-track-based `fit-content` intrinsic width — ✅ done for horizontal grids
   (this session).** A grid with a **fixed** column template now sizes its
   `min-content`/`max-content`/`fit-content`/`float`/`inline-grid` width to the sum
   of its column tracks (+ gaps + own padding/border) instead of the max-content of
   its (often empty) inline content, which collapsed the container. Implemented as
   `TryComputeGridIntrinsicContentWidth` (fixed lengths + `minmax(fixed,fixed)`;
   declines `fr`/`auto`/content/`auto-fill`/percentage — those need the real track
   pass) hooked into `ComputeShrinkToFitWidth` (max-content) and `GetMinMaxWidth`
   (both sides). Also fixed a **frame-mismatch bug** in the Workstream C fit-content
   keyword branch (min-content was border-box, max-content content-box → padding
   double-counted). Guard `GridIntrinsicWidthTests`; 0 regressions (55 grid guards,
   vendored subsets byte-identical). **Verified in-harness:** `max-content` →
   column sum (`minmax(10,50)`→50), `min-content` → min sides (→10), fixed
   `fit-content`/`float` → track sum (100/100→200, +gap, +border). *Remaining for
   #10 / E test 1a:* (a) **`fit-content` with min≠max** (a `minmax()` track) needs a
   real viewport — `fit-content = min(max(min,available),max)` and the harness's
   containing-block `available` is ~0, so it collapses to min-content here but
   resolves on CI; (b) **vertical writing modes** are gated off — the physical-width
   axis (rows) must be applied through the `ApplyVerticalWritingModeFlow` rotation
   (#10 is `vertical-rl`), a separate increment.
2. **Baseline self-alignment.** #6 (`grid-align-baseline-vertical`) needs
   `align-self:baseline` / `justify-items:*baseline` to synthesise a shared baseline
   across a row/column (CSS Align §9) and shift each item to it. The pass currently
   falls to `start` for baseline. A cross-item feature, independent of writing mode
   (horizontal baseline grids are affected too).
3. **Margin/border/padding detail + font/paragraph tail.** #10 also exercises
   physical vs logical margin mapping under alignment; #8's residual is the
   deferred UA paragraph-rhythm (see the note below), plus Ahem sub-pixel.

**Key files.** `CssBox.cs` (`ComputeShrinkToFitWidth`/`GetMinMaxWidth` for the
fit-content grid width — writing-mode aware); `CssBoxGrid.cs` (baseline synthesis in
`ResolveTrackSizes`/`PlaceItemInArea`). **Risk: medium** per feature (the fit-content
grid width touches all shrink-to-fit grids — gate to fixed/known tracks and diff the
vendored subsets). **Effort:** medium per feature — *not* the multi-day rewrite the
original framing implied.

<details><summary>Original (pre-reproduction) analysis — superseded: assumed a full
transposition rewrite was needed</summary>

**Blast radius: largest.** Covers issue items #6, #8, #10 and ~58 more
`css-grid/alignment` tests (61 vertical/orthogonal alignment tests, currently
0 passing). `CssBoxGrid.cs` assumes `horizontal-tb` throughout … *(the premise that
vertical grids come out as "a plain physical 2×2 instead of the transposed
arrangement" is contradicted by `GridVerticalWritingModeTests`; the two
implementation options for a logical-axis rewrite are moot for the core cases,
which already transpose correctly)*.

</details>

**Note on the #8 residual — investigated in-sandbox, deferred to CI.** After
cluster 36 `content-distribution-vertical-rl` is at 94.7 % with items visually
correct; the residual is a per-section ~10 px **vertical** drift affecting the
intro `<p>` too — a text-rhythm / paragraph-margin issue, *not* grid. Fetching the
verbatim test confirmed it is a **check-layout** test whose intro is a plain
`<p>` with **default UA margins** (its `vertical-rl` applies only inside the grid,
via the support-file `.verticalRL` class — the paragraph itself is horizontal-tb).
So the drift is a **UA default paragraph/line-box rhythm** difference, whose fix
surface (default `<p>` margin, `<body>` margin, or `normal` line-height) is the
**highest blast radius** in the engine. Two blockers make it unsafe to pursue
in-sandbox:
- **Not measurable here.** The drift is scored against a Chromium screenshot of
  the test; the css-grid corpus clone 403s in-session, so there is no reference to
  size the deviation against. Probing the intro `<p>` in isolation via the
  check-layout geometry harness gives a self-consistent 35 px consumed height
  (offsetTop-relative), but nothing to call it right *or* wrong against.
- **Not regression-testable here.** A change to a UA default would ripple through
  every rendered test, and the css-grid / css-writing-modes suites that would
  catch a regression are **not vendored**. The two vendored subsets that *are*
  present (CSS2, css-align, css-anchor-position, css-animations, css-backgrounds)
  cannot net a paragraph-rhythm change safely.

**Next step (CI-gated):** on a host with the css-grid corpus + Chromium
references, diff the intro `<p>`'s rendered top/height against Chromium to fix the
exact default (margin vs line-height), then re-net the full css/CSS2 pixel suite —
it is *not* a bounded, sandbox-verifiable change like Workstream B was.

---

## Workstream B — replaced-item grid intrinsic sizing (#5)

**Status: root cause corrected and the collapse ✅ fixed (this session).** Clusters
34 & 35 (aspect-ratio replaced sizing + box-shorthand cascade) landed; cluster 35
(`patches/0004-…`) is applied upstream (`Broiler.CSS` commit `5a4fae1`, pointer
bumped). But reproducing the actual test in-sandbox (`--render` + the in-process
check-layout geometry harness, using the verbatim WPT markup fetched from the
`master` raw path) showed the earlier "~16 % **horizontal** offset" premise is
**wrong**: the image does not shift sideways — it **collapses to height 0** inside
the nested `display:grid`, so the test renders **blank** (every box at `x=0`; the
`<img>` is `w≈1130 h=0` where the reference is `w≈1130 h≈567`).

**Root cause.** The test nests the image `li (grid item) > inline-block >
display:grid > img{block-size:55vw; aspect-ratio:2/1}`. The inner `display:grid`
has no explicit template, so it takes the implicit-only pass — which
`GridImplicitPathItemsAreSimple` **declined for any replaced item**, falling back
to the single-column approximation that dropped the image's height. Even forcing
the real pass to engage was not enough: a replaced element laid out through its
container's line box records its used height on its **image word**, leaving the
box's `ActualBottom` at 0, so the auto row measured `ActualBottom − Location.Y = 0`
and `PlaceItemInArea` then clobbered the image to height 0 (and `RectanglesReset()`
wiped the correct line-rect). Isolation (`img` in inline-block vs block>grid vs
inline-block>grid vs `block-size:200px`) confirmed the `display:grid` wrapper is
the sole trigger and it is **not** `vw`-specific.

**Fix (landed, main-repo `Broiler.Layout/Engine/CssBoxGrid.cs`), narrowly gated to
a replaced item with a *definite* block-size** (its height has no reflow dependence
on the resolved column width, so a single measurement is safe — exactly the "make
the real pass trust a replaced item's definite block size" follow-up flagged
below):

1. `GridImplicitPathItemsAreSimple` now admits an `<img>` whose block-size (the
   `Height` getter, which maps `block-size`/writing-mode → physical height) resolves
   to a definite length via `CssLayoutEngine.TryResolveDefiniteImageLength`
   (viewport/font units included). A ratio-only or percentage-height image still
   declines — its height *does* follow the column.
2. `GridReplacedItemDefiniteBorderBoxHeight` supplies that definite border-box
   height to both the **row measurement** (in place of the stale
   `ActualBottom − Location.Y`) and **`PlaceItemInArea`** (in place of the stale
   `Size.Height`), so the row sizes correctly and the image is placed at full size.

With the fix the test lays out **identically to `-ref.html`** — `<img>` at
`x=0 y=0 w≈1130 h≈567`, exact match — and the `--render` output is the correct
top-left red block (was blank). **Verification (in-sandbox, no css-grid corpus):**
guard `NestedGridItemBlockSizeTests` (fails at `height=0` without the fix), the 42
`GridTrackLayout`/`GridTrackPaint`/`GridLanesFallback`/`AspectRatio`/`PercentChild`
Cli.Tests, `Table` (27) all green; the runner over the vendored **css-anchor-position
(40)** and **css-align (28)** subsets shows a **byte-identical pass set** before/after
(0 status diffs). The remaining ~16 % font/`ul`-`li` tail and the full css-grid pixel
score still need a CI run (corpus not vendored — clone 403s in-sandbox).

**Key files.** `Broiler.Layout/Engine/CssBoxGrid.cs`
(`GridImplicitPathItemsAreSimple`, `GridReplacedItemDefiniteBorderBoxHeight`, the
row-measurement + `PlaceItemInArea` sites), `CssLayoutEngine.cs`
(`TryResolveDefiniteImageLength`, made `internal`). **Risk: low** (gated to
definite-block-size replaced items; 0 regressions on the vendored subsets).

---

## Workstream C — auto-fill track count under shrink-to-fit + min-size (#9)

`grid-auto-repeat-min-size-001` is a 12-case `checkLayout` test:
`grid: repeat(auto-fill, 50px) / repeat(auto-fill, 100px); min-width: 300px;
min-height: 200px; float: left` and variants with explicit `width`/`height`,
`min-content`/`max-content`, `border`, and `box-sizing: border-box`. Expected
outer size 300×200 (3×4 tracks); the item pinned to the last cell (`grid-column:
-2; grid-row: -2`) lands at (200, 150).

**Root cause — corrected by reproduction (this session).** The original premise
(below) was that the column-axis auto-fill count lacked a `min-width` raise
mirroring the row axis's `ComputeAutoRepeatBlockSize`. Reproducing the scenario
against the live layout engine via the in-process check-layout geometry harness
(`Broiler.Cli.Tests/GridTrackLayoutTests` style — `DomBridge.EvaluateCheckLayoutAssertions`,
no pixel reference needed) shows that premise is **wrong**. With
`grid-template-columns: repeat(auto-fill, 100px); min-width: 300px` and an item at
`grid-column: -2` (negative line, so its column depends on the resolved track
count), the four width-source variants behave as:

| Width source | container width | item `grid-column:-2` x | verdict |
|---|---|---|---|
| `width: 300px` (definite) | 300 | 200 | ✅ correct |
| `width: fit-content` | **300** | **200** | ✅ correct |
| `float: left` | **1024** | **900** | ❌ fills viewport |
| `display: inline-grid` | **1024** | **900** | ❌ fills viewport |

The row-axis control (`min-height: 200px`, item `grid-row: -2`) is correct at
y=150, confirming the block axis already works. Crucially **`width: fit-content`
already resolves to 300 with 3 columns and the item at x=200** — i.e. the auto-fill
count *already* honours `min-width` once the used inline size is a real
shrink-to-fit value. There is **no missing `ComputeAutoRepeatInlineSize`**.

The actual defect is one layer up, in **grid intrinsic-width measurement**:
`float: left` and `display: inline-grid` grids do **not** shrink-to-fit — they lay
out at the full available width (1024). The float shrink-to-fit path exists
(`CssBox.cs` ~L1294, `Width:auto && Float != none`: `preferred =
ComputeShrinkToFitWidth()`, then `Math.Min(Math.Max(prefMin, available),
preferred)`, then a `min-width` clamp that only *raises*), but for an auto-fill
grid `ComputeShrinkToFitWidth()`/`GetMinMaxWidth` returns the full `available`
(1024) as the preferred/max-content, so `min-width: 300` (300 < 1024) never
reduces it. The `width: fit-content` path measures the same grid's preferred width
correctly (small → clamped up to `min-width` 300), which is why only it and the
definite case pass. So the two width-resolution paths disagree on a grid's
intrinsic width.

**Root cause narrowed further — ✅ fixed (this session).** Drilling in with the
geometry harness pinned it precisely: a floated / `inline-grid` grid **does**
shrink-to-fit correctly when its items have no explicit width (the `float`/
`inline-grid` cases resolve to 300 with the item at x=200, same as `fit-content`).
The `1024` only appears when a grid **item** carries a *percentage* width
(`width:100%`, as WPT `sizedToGridArea` items do). Root cause: intrinsic-width
measurement (`CssBoxHelper.GetMinMaxSumWords` and `CssBox.ComputeShrinkToFitWidth`)
resolved a child's `width:100%` against the container's *tentative/available*
width (1024) and used that as the child's max-content contribution — so the grid's
shrink-to-fit width ballooned to 1024 and `min-width:300` (which only raises) never
reduced it. Per **CSS Sizing 3 §5.1** a percentage width resolves against the size
being computed, so it must be treated as `auto` for intrinsic sizing (the code
already did this for grid items via `suppressExplicitWidthFor`; the fix generalizes
it to any percentage width). The `fit-content` path escaped the bug only because it
runs before `Size.Width` is set (so `100%` resolved against 0). Not grid-specific —
a plain `float` wrapping a `width:100%` block had the same 1024 balloon.

**Fix (landed, main-repo `Broiler.Layout`).** `GetMinMaxSumWords` treats a box
whose own width is a percentage as auto (falls through to content measurement);
`ComputeShrinkToFitWidth` measures a percentage-width child via `GetMinMaxWidth`
instead of resolving the percentage. Now the `float`/`inline-grid` auto-fill grid
resolves to `min-width` 300 with three 100px columns and the `grid-column:-2` item
lands at x=200. Guard: `PercentChildShrinkToFitTests` (float / inline-block /
abspos + the auto-fill grid case — in-process check-layout geometry, no pixel
reference). **Validation:** a targeted layout-suite diff (Flex / Grid / AspectRatio
/ Table check-layout classes) shows **zero regressions** vs baseline (47/49 pass on
both; the 2 flex failures are pre-existing and font-rasterization dependent). The
full **`css-grid` / CSS2 pixel diff still needs a CI run** — the WPT corpus is not
vendored and cannot be fetched in the sandbox (WPT clone → 403; empty
`node_modules`). This closes the shrink-to-fit balloon; item #9's remaining 12
`checkLayout` variants (border / `box-sizing` / `min-content` / `max-content`)
still need a full-suite score on CI.

**Update (this session) — the 12 variants scored in-sandbox; 2 more root causes
fixed (9/12 now pass).** The percentage-child balloon fix above did **not** close
this test — its items have *no* width, so that path never engaged. Fetching the
verbatim test (it is fully self-contained; the external `grid.css` is overridden)
and running all 12 `checkLayout` cases through the geometry harness showed **10/12
failing** on two *distinct* defects the earlier note did not cover:

1. **`min-height`/`max-height` was not re-applied to a float's explicit height.**
   The `float` + explicit-`height` override (`CssBox.PerformLayoutImp`, the
   `Float != none && Height != auto` branch) runs *after* the §10.7 min/max clamp,
   so a `float:left` grid with `height:100; min-height:200` kept **100** even
   though its auto-fill row count had already grown to `min-height` (item correctly
   at y=150, container wrongly 100). Not grid-specific — any float ignored
   `min-height` on this path. Fixed with `ClampSpecifiedHeightToMinMax` (clamps in
   the shared box-sizing frame before `ResolveSpecifiedHeightToBorderBox`
   normalizes), flipping g2/g6/g10 heights → 200/220/200.
2. **Intrinsic-sizing width keywords fell through to the stretched width.**
   `width: min-content`/`max-content`/`fit-content` matched none of the auto/float
   shrink-to-fit branches, so they stayed at the container width (**1024**). Added
   an `IsIntrinsicSizingWidthKeyword` branch (CSS Sizing 3 §5.1): `min-content` →
   min-content, `max-content` → max-content, `fit-content` → `min(max(min, avail),
   max)`, then the `min-width`/`max-width` clamp and own border/padding — mirroring
   the proven float shrink-to-fit path. Flips the six `min/max-content` cases
   (g3/g4/g7/g8/g11/g12) width → 300/320.

Together these take **grid-auto-repeat-min-size-001 from 2/12 → 9/12** cases fully
correct in the geometry harness. Guard: `GridAutoRepeatMinSizeTests` (the 8
non-`border-box` cases; fails without either fix). **Zero regressions** — 74 grid/
aspect/table/percent Cli.Tests green, and the runner over vendored
**css-anchor-position (40) + css-align (28) + css-backgrounds (61)** shows a
**byte-identical pass set** before/after (0 status diffs across 129 tests).

**✅ The 3 `box-sizing:border-box` variants g9/g11/g12 now fixed (this session) —
grid-auto-repeat-min-size-001 is 12/12.** They resolved to a 200 border-box height
where WPT expects 220; two distinct root causes, reproduced in the geometry harness
(g9=320×220 item (200,150), g10 correctly 300×200 (100,100) — a definite height so it
keeps floor, g11/g12=320×220 (200,150)):

1. **Auto-fill repetition count under a definite *min*-size floored instead of
   ceiling.** `ExpandAutoRepeatTrackList` computed the count as the largest that does
   not overflow the available size (floor) — correct when the available size is a
   definite *size* or *max-size*, but a definite *min*-size (indefinite used size) is
   filled by the *smallest* count that reaches it (ceil, §7.2.3.2). The g1–g8 min-
   sizes are clean track multiples (200/50=4, 300/100=3) so floor=ceil hid the bug;
   with `box-sizing:border-box` the content min-height is 200−20=180, so the row
   count is ⌈180/50⌉=4 (floor gave 3 → a 200-tall box with the item one row too high).
   Fixed by threading a `fillMinimum` flag from `ComputeAutoRepeatBlockSize` (set only
   when the returned block size came from `min-height` with **no** definite height, so
   g10's definite explicit height still floors) into the count computation, which
   ceils in that case. Only the row axis is threaded — the column path is unchanged
   (byte-identical widths). Also corrected `GridTrackLayoutTests.GridAutoFillRows_
   ResolveCountFromMinHeight`, which had encoded the old floored count.
2. **Intrinsic-sizing *height* keyword under `border-box` dropped the border.**
   `height:min-content`/`max-content` on the float was treated as a specified length:
   the explicit-height branches fed the already content-derived `ActualHeight` (200,
   the grid's 4-row content) into `ResolveSpecifiedHeightToBorderBox`, which under
   `box-sizing:border-box` returns it unchanged as the *border-box* height → 200
   instead of 220. Fixed with `IsIntrinsicSizingHeightKeyword`, which excludes such a
   keyword from the two "explicit length height" branches (`CssBox`) so the content-
   computed height stands and only the §10.7 min/max clamp applies.

Both fixes are gated to the intrinsic/min case, so the vendored **css-align (19/28) +
css-anchor-position (30/40) + css-backgrounds (39/61)** subsets are byte-identical
before/after (0 regressions), and 93 grid/aspect/table/percent Cli.Tests stay green.
Guard `GridAutoRepeatMinSizeTests` now asserts all 12 variants. The full pixel score
of all 12 still lands on CI (corpus not vendored).

<details><summary>Original (pre-reproduction) analysis — superseded</summary>

`repeat(auto-fill, …)` **column** count is resolved against
`contentWidth = Size.Width − padding − border` (`CssBoxGrid.cs` L94). For a
shrink-to-fit float the width is indefinite, so per CSS Grid §7.2.3.1 the count
must be computed from the **definite `min-width`** — exactly what
`ComputeAutoRepeatBlockSize` already does for the **row** axis via `min-height`
(L1162). The column axis has no equivalent min-width raise, so the auto-fill count
collapses to one repetition. → *Disproved:* `width: fit-content` already yields 3
columns and x=200, so the count already honours `min-width`; the real gap is
float/inline-grid shrink-to-fit sizing above.

</details>

**Key files.** `Broiler.Layout/Engine/CssBox.cs` (float/inline shrink-to-fit width
at ~L1294; `ComputeShrinkToFitWidth` L3980; `GetShrinkToFitWidth` L4672),
`CssBoxGrid.cs` (`GetMinMaxWidth` intrinsic grid-track measurement). **Effort:**
medium.

---

## Workstream D — abspos grid items in implicit tracks (#4)

**✅ Fixed for LTR (this session); the root cause was bigger than assumed.**
`grid-positioned-items-within-grid-implicit-track-001` is a **check-layout** test:
a `.grid` (explicit `200px 300px / 150px 250px`, `grid-auto-*` 100/50, 800×600,
border 5, padding 15) holds an in-flow magenta `.sixRowsAndSixColumns`
(`grid-column:-5/5; grid-row:-5/5`) plus a cyan abspos item per case. Reproducing
all 16 assertions through the geometry harness showed the suspected diagnosis
("abspos line resolution to the padding edge") was only *half* of it — the
**in-flow magenta itself** collapsed to a single 100×50 cell.

**Root cause — leading implicit tracks were unsupported.** A definite line that
resolves *before* the explicit grid (a negative index — `-5` with 2 explicit
tracks → boundary `-2`) references a **leading implicit track** (CSS Grid §8.3).
`ParseSingleGridLine` **clamped every such boundary to `auto`**, so `-5/5`
degenerated to a 1-track span and no leading tracks were created — and every abspos
item's expected geometry *depends* on those leading tracks shifting the explicit
grid. Two coupled fixes (main-repo `CssBoxGrid.cs`), both gated so a grid with no
before-grid line is byte-identical:

1. **In-flow leading tracks.** `ParseSingleGridLine` now returns the true (possibly
   negative) boundary; the pass computes `explicitColStart`/`explicitRowStart` =
   `−min(0, placed lines)`, shifts every placement right/down so index 0 is the
   leftmost/topmost referenced line, and widens the track count. `ResolveTrackSizes`
   gained an `explicitStart` offset so the explicit specs map to `[explicitStart,
   explicitStart+count)` and leading/trailing tracks use `grid-auto-*`.
   Auto-placement into leading tracks is out of scope, so the pass declines if an
   auto-placed item coexists with a before-grid line. (Fixes the magenta:
   `-5/5` → 6 tracks, 900×600 at the padding origin.)
2. **Abspos placement into the extended grid.** A dedicated `ParseAbsposGridLines`
   resolves `grid-column`/`grid-row` to two *boundary* lines in the shifted
   coordinate — an `auto` line staying auto (→ padding edge, §9.2) rather than
   collapsing into a span as the in-flow `ParseGridLine` does — and the rewritten
   `ResolveAbsposAxis` builds the area from those two lines (a `null` line → the
   container's padding edge). The area becomes the item's containing block,
   overriding its `top/left/width/height:100%` fallback.

**Result: the full LTR test passes — 64/64 assertions** (8 abspos cases × 4 props +
8 magenta references) exact in the harness. Guard: `GridAbsposImplicitTrackTests`
(fails at 57/64 without the fix). **Zero regressions** — 48 grid/aspect Cli.Tests
green, byte-identical pass set on vendored css-anchor-position (40) + css-align (28)
+ css-backgrounds (61) (0 status diffs across 129 tests).

**RTL now also fixed (this session).** The 8 **`directionRTL`** variants exercised
the grid engine's RTL column-axis mirroring, which was absent (RTL laid out
identically to LTR). Added it for both in-flow items (mirror the resolved column
edges within the content box — which also flips `justify-content` start↔end) and
abspos items (mirror the resolved area around the padding box, CSS Grid §9.2).
Verified against the test's RTL values (magenta `offset-x -85`, all 4 sampled
abspos cases exact — 32/32 assertions), guard `GridRtlTests`, 0 regressions. Only
the full `css-grid/abspos` **pixel** score on CI remains. The leading-implicit-track
foundation also generally unblocks in-flow
negative-line placement, not just this test.

**Key files.** `Broiler.Layout/Engine/CssBoxGrid.cs` (`ParseSingleGridLine`, the
leading-shift normalisation, `ResolveTrackSizes` `explicitStart`,
`ParseAbsposGridLines`, `ResolveAbsposAxis`). **Risk: medium** (gated to grids with
a before-grid line; 0 regressions on the vendored subsets). **Effort:** done for
LTR; RTL is a follow-up.

---

## Workstream E — gutter contribution to tracks/spanning/margins (#7)

`grid-gutters-and-tracks-001` is a large `checkLayout` test asserting that
`grid-gap`/`row-gap`/`column-gap` (a) add to the grid container size, (b) add to
spanning items, (c) do **not** alter item positioning, margin computation, or
track sizing, across `fit-content`, percentage tracks, `minmax`, named lines, and
a `verticalRL directionRTL` case. Broiler currently mismatches at 35.8 %
(`MissingContent`), i.e. several sub-checks lay out wrong.

**Root cause found — the primary defect is ✅ fixed (this session).** Fetching the
verbatim test (16 sub-grids; classes from `support/grid.css` — the base `.grid` is
`display:grid; position:relative`, `.fit-content` is `width:fit-content`) and
running the fixed/percentage-track sub-grids through the check-layout geometry
harness pinned the dominant cause: **Broiler never mapped the legacy CSS Grid
Level 1 gap aliases.** The test writes `grid-gap:16px` (test 15),
`grid-row-gap:12px; grid-column-gap:23px` (tests 3–8, 12), but `CssUtils`
recognised only the modern `gap`/`row-gap`/`column-gap`, so every `grid-*-gap`
declaration was dropped and the tracks abutted with **no gutter** — every item
past the first landed exactly one gap short (test 15's column 2 at 100 instead of
116, row 2 at 100 instead of 116). Fixed by aliasing `grid-gap`/`grid-row-gap`/
`grid-column-gap` onto `gap`/`row-gap`/`column-gap` in `CssUtils.SetPropertyValue`
(and the getter). With it the gutters land exactly (test 15 → 116/116). Guard:
`GridGapAliasTests` (borderless grids so the offsets are exact; fails without the
alias). **Zero regressions** — 23 grid guards green, byte-identical pass set on
vendored css-align (28) + css-anchor-position (40).

**Still open (each blocks a distinct group of the 16 sub-grids, separate work):**
- **Named-line templates (tests 5/6/12, `gridMultipleFixed = [first]37px[foo]…`) —
  corrected + partly fixed (this session).** The premise ("named lines decline the
  pass") was stale: `ParseTrackTokens` already **skips** `[name]` brackets, so the
  named-line *track sizes* parse and the pass engages. The real blockers were
  **placement**, in two parts:
  - **`grid-area` shorthand + `grid-*-start`/`grid-*-end` longhands were dropped** —
    Broiler parsed only `grid-row`/`grid-column`, so `grid-area:3/3` (test 5's
    `thirdRowThirdColumn`) and `grid-column-end:foo` (test 6) auto-placed into the
    wrong cell. ✅ **Fixed** (`ApplyGridAreaShorthand` + `SetGridLineSide` in
    `CssUtils`; guard `GridPlacementShorthandTests`, 0 regressions). Test 5 now
    places correctly (numeric + `grid-area`).
  - **Named-line *placement resolution*** (`grid-column: bar`, `grid-row: 1 / bar`,
    `grid-area: bar / bar`, `grid-column-start: foo`) — ✅ **fixed** (this session).
    `ParseLineNames` builds a per-axis line-name → index map from the template
    (expanding `repeat(<int>,…)`), threaded into `ParseSingleGridLine` and the
    abspos `ParseAbsposGridLines`; a `<name>` resolves to its first labelled line
    (or `<int> <name>` to the Nth), an unknown name still falls back to `auto`.
    Guard `GridNamedLineTests`. With this + `grid-area`, tests 5/6/12's placement is
    correct; their gutter assertions resolve on CI (the harness's −1 offsetParent
    border still offsets the check-layout offsets uniformly). 0 regressions on the
    vendored subsets.
- **`width:fit-content` grid intrinsic width — ✅ implemented (this session, see
  Workstream A).** A grid with a fixed column template now sums its column tracks
  (+ gaps + padding/border) for `min-content`/`max-content`/`fit-content`/`float`,
  so test 1a's container resolves correctly on a real viewport (CI). Two residuals
  keep it from being exact *in the harness*: `fit-content` with a `minmax()` track
  (min≠max) needs a non-zero `available` (CI), and the `verticalRL` sub-grid needs
  the vertical-axis mapping (gated off). Guard `GridIntrinsicWidthTests`.
- **Percentage-track grids collapse** (tests 9/10/13/14): rows/heights come back
  short — the §11 percentage-track path declines or mis-sizes with gaps.
- **check-layout estimator vs offsetParent border (uniform −1).** Broiler's
  `offsetTop`/`offsetLeft` (`LayoutMetrics`, deliberately relative to the offset
  parent's *padding* edge) is 1 px short of Chromium's values, which include the
  parent's border, so every bordered sub-grid's item offsets are uniformly −1
  in the harness. This is a check-layout **estimator** nuance (the #4 diagnostic),
  not necessarily the pixel render; scoping a change to `offsetTop` border
  handling is a separate, broader-impact item.

**Key files.** `Broiler.Layout/Engine/CssUtils.cs` (gap aliases — done),
`CssBoxGrid.cs` (`ResolveGridGap`, named-line + percentage track sizing — open).
**Risk: medium.** **Effort:** medium (per remaining group).

---

## Workstream F — subgrid across an orthogonal flow (#2)

`subgrid/orthogonal-writing-mode-006`: a `subgrid` whose writing mode is
orthogonal to its parent grid. Requires the parent to hand down track sizes
(already modelled — `SubgridColumnSizes`/`SubgridRowSizes`) **and** the subgrid to
consume them through a transposed logical axis. **Depends on Workstream A**
(logical-axis abstraction) plus the existing subgrid plumbing. **Risk: high.**
**Effort:** large. Sequence after A.

---

## Workstream G — experimental `grid-lanes` (#1, #3)

`grid-lanes/…/column-subgrid-auto-fill-003` (0.8 %) and
`grid-lanes/…/grid-lanes-quirks-fill-viewport` (11.6 %) exercise the experimental
`display: grid-lanes` value with `repeat(auto-fill, [named-lines])` inside an
orthogonal-parent subgrid. `grid-lanes` is unsupported and deliberately dropped to
`block` (ledger clusters 29/31/32); unflagged browsers do the same, so the
Chromium reference is the *dropped* rendering. #1 additionally needs multi-column
named-line subgrid layout. **Lowest priority — extremely niche, spec still
in flux.** Only worth it once A/F land, as it composes subgrid + orthogonal flow +
named-line auto-fill. **Risk: high, value: low.**

---

## Suggested sequencing

1. **B** — ✅ replaced-item-in-grid **height collapse fixed** (item #5 no longer
   renders blank; lays out identically to `-ref.html` in-sandbox). Remaining: the
   ~16 % `ul`/`li` + font tail and the full css-grid pixel score, both CI items.
2. **C** — item #9: ✅ **12/12 cases now correct** — after the earlier
   percentage-width balloon fix and the `min-height`-float-clamp / intrinsic-width-
   keyword fixes, this session closed the last 3 `box-sizing:border-box` variants:
   the auto-fill count under a definite *min*-size ceils (not floors, §7.2.3.2), and
   an intrinsic-sizing *height* keyword under `border-box` keeps its content-derived
   height. See Workstream C. Remaining: the full pixel score on CI (corpus not
   vendored).
3. **The #8 ~5 % vertical text-drift** — investigated (this session): it is a UA
   default paragraph/line-box rhythm difference on a plain intro `<p>`, **not** a
   bounded sandbox win — its fix surface is a high-blast-radius UA default that is
   neither measurable nor regression-testable without the css-grid corpus +
   Chromium references. CI-gated (see the "Note on the #8 residual" above).
4. **E** — item #7: ✅ **gap aliases fixed** (`grid-gap`/`grid-row-gap`/
   `grid-column-gap` were dropped → no gutter). Remaining sub-grids blocked on
   named-line track support, grid-track-based `fit-content` width, and
   percentage-track sizing — each a distinct follow-up.
5. **D** — item #4: ✅ **LTR + RTL** — leading implicit tracks (negative before-grid
   lines) + abspos line-to-area resolution (LTR 64/64), then RTL column-axis
   mirroring for in-flow and abspos items (RTL values exact, `GridRtlTests`).
   Remaining: the CI pixel score.
6. **A** — vertical-writing-mode grids: ✅ **transposition already works** (this
   session — placement, content distribution, self alignment all verified;
   guard `GridVerticalWritingModeTests`). Re-scoped from "multi-day rewrite" to two
   standalone features: **grid-track-based `fit-content` width** — ✅ **done for
   horizontal grids this session** (`GridIntrinsicWidthTests`; unblocks E test 1a
   and #10's container on CI; vertical-axis mapping + min≠max fit-content remain) —
   and **baseline self-alignment** (#6, still open).
7. **F**, **G** — subgrid-orthogonal and `grid-lanes`. F's dependency on A is now
   just the fit-content/transposed sizing, not a logical-axis rewrite.

---

## Reproduction harness (for whoever picks this up)

The `css-grid` and `css-writing-modes` tests are not vendored; fetch and score
them locally. **Sandbox caveat:** inside a Claude-Code-on-the-web session the WPT
clone (step 1) returns **403** — `web-platform-tests/wpt` is outside the session's
GitHub egress scope — and `tests/wpt/node_modules` is empty, so the Chromium
reference generation (step 2) cannot run either. Run these steps on a host with
open network (or a CI job) to produce the references. For layout-geometry work the
in-process `data-offset-*`/`data-expected-*` harness
(`Broiler.Cli.Tests/GridTrackLayoutTests`, `DomBridge.EvaluateCheckLayoutAssertions`)
reproduces track geometry **without** a pixel reference and runs in the sandbox —
use it to reproduce/bisect, then confirm the full pixel diff on CI.

```sh
# 1. Sparse-checkout the needed WPT subtrees.
git clone --no-checkout --depth 1 --filter=blob:none \
    https://github.com/web-platform-tests/wpt.git wpt-grid
cd wpt-grid && git sparse-checkout init --cone
git sparse-checkout set css/css-grid css/reference css/support fonts resources \
    css/css-writing-modes
git checkout

# 2. Generate Chromium references with the pre-installed browser.
#    (generate-wpt-references.js now honours BROILER_CHROMIUM_PATH so it uses the
#    container's /opt/pw-browsers chromium instead of downloading a pinned build.)
export BROILER_CHROMIUM_PATH=/opt/pw-browsers/chromium \
       NODE_PATH=/path/to/tests/wpt/node_modules
node scripts/generate-wpt-references.js \
    wpt-grid/css/css-grid REFS/css/css-grid --base-dir wpt-grid
#    NB: pass the output dir as the REFS *root* (not REFS/css/css-grid) — the
#    generator mirrors the base-dir-relative path, so a nested subpath double-nests.

# 3. Score Broiler against them, saving failure triptychs.
dotnet run --project src/Broiler.Wpt --no-build -c Release -- \
    --wpt-dir wpt-grid --reference-dir REFS \
    --subset "css/css-grid/alignment/*" \
    --json-output out.json --failure-images fail-img/
```

Regression protocol used by cluster 36 (reuse it): snapshot the passing-test set
before the change, re-run after, and diff — a net gain is not enough, the lost set
must be empty (or every loss individually justified).

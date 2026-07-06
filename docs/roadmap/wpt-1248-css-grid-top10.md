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
| 4 | `abspos/grid-positioned-items-within-grid-implicit-track-001` | 23.4 % | 23.4 % | Abspos grid item resolving to an implicit line | **D** |
| 5 | `nested-grid-item-block-size-001` | 27.3 % | ~84 %¹ | Residual `ul`/`li` + nested-inline-block offset | **B** |
| 6 | `alignment/grid-align-baseline-vertical` | 34.1 % | 49.4 % | Grid-axis transposition + vertical baselines | **A** |
| 7 | `grid-model/grid-gutters-and-tracks-001` | 35.8 % | 35.8 % | Gutter contribution to track/spanning/margin sizing | **E** |
| 8 | `alignment/grid-align-content-distribution-vertical-rl` | 36.2 % | 94.7 % | Grid-axis transposition (residual page-level drift) | **A** |
| 9 | `grid-definition/grid-auto-repeat-min-size-001` | 43.8 % | 43.8 % | Auto-fill track count under shrink-to-fit + min-size | **C** |
| 10 | `alignment/grid-align-justify-margin-border-padding-vertical-rl` | 45.1 % | 61.4 % | Grid-axis transposition + margin/border/padding in vertical | **A** |

¹ Item #5 is driven to ~84 % by ledger clusters 34 (replaced-item
logical/`aspect-ratio` sizing) and 35 (box-shorthand-vs-longhand cascade). Cluster
35 has now **landed upstream** — `Broiler.CSS` commit `5a4fae1`, the parent's
submodule pointer bumped — so CI reflects the ~84 % (the earlier `patches/0004-…`
fallback is obsolete and removed). Its true remaining work is Workstream B only.

---

## Workstream A — grid-axis transposition for vertical writing modes

**Blast radius: largest.** Covers issue items #6, #8, #10 and ~58 more
`css-grid/alignment` tests (61 vertical/orthogonal alignment tests, currently
0 passing). This is the single highest-value grid workstream.

**Root cause.** `Broiler.Layout/Engine/CssBoxGrid.cs` (~1,800 lines) assumes
`horizontal-tb` throughout: `grid-template-columns` is sized against the physical
width, `grid-template-rows` against the physical height, item placement and track
positions are all physical. It has **no** writing-mode / logical-axis handling
(one incidental match on a grep). For a `vertical-rl`/`vertical-lr` grid the
inline axis is vertical and the block axis is horizontal, so:

- `grid-template-columns` (inline-axis track sizes) must drive the **vertical**
  extent, `grid-template-rows` the **horizontal** extent;
- auto-placement and `grid-auto-flow` advance along the transposed axes;
- self/content alignment (`align-*` = block axis, `justify-*` = inline axis) must
  map to the physical axes through the grid's own writing mode.

Today the grid lays out physically and the whole subtree is then rotated by
`ApplyVerticalWritingModeFlow`. That rotation gets the *container* and simple
content right (why #8 reached 94.7 %), but a genuine 2×2 track grid comes out as a
plain physical 2×2 instead of the transposed arrangement — the ~94 % on the
`grid-self-alignment-stretch-vertical-*` cluster is **coincidental block overlap**,
not near-correct layout (verified by eye against the Chromium reference).

**Proposed approach.** Introduce a logical-axis abstraction inside the grid pass:
resolve `writing-mode`/`direction` once, then read/write track sizes and item
rectangles through inline/block accessors that map to physical width/height/x/y
per the grid's own writing mode — mirroring how the block engine already
distinguishes logical vs physical. Two implementation options:

1. **Lay out in a logical frame, keep the post-layout rotation.** Have the grid
   pass emit a logical (as-if-`horizontal-tb`) layout with columns→inline and
   rows→block, and let the existing `ApplyVerticalWritingModeFlow` rotation map it
   to physical. Smaller change, reuses the rotation, but must guarantee the grid's
   logical dimensions (from `width`/`height` transposed via
   `WillBeVerticalTransposed`) feed track sizing correctly and that alignment is
   computed in logical space *before* rotation.
2. **Full physical transposition in the grid pass.** Make the pass writing-mode
   aware end-to-end and *not* rely on the rotation for grids. Cleaner long-term,
   larger diff, higher regression surface.

Start with option 1 — it composes with cluster 36 and the existing rotation, and
the ~94 % on `content-distribution` shows the rotation already handles the easy
cases.

**Key files.** `Broiler.Layout/Engine/CssBoxGrid.cs` (track sizing, placement,
alignment); `CssBox.cs` (`ApplyVerticalWritingModeFlow`, `WillBeVerticalTransposed`,
`GetShrinkToFitHeight`); `CssBoxProperties.cs` (`IsVerticalWritingMode`).

**Risk: high.** ~91 horizontal `css-grid/alignment` tests pass today and must stay
green. Gate every new logical-axis branch behind
`IsVerticalWritingMode(WritingMode)` so horizontal grids are byte-identical, and
diff the full passing set (baseline vs after) exactly as cluster 36 did.

**Effort:** large (multi-day). **Validation:** `css-grid/alignment` full suite
diff + `css-grid/{grid-model,grid-definition,subgrid}` as regression guards.

**Note on the #8 residual.** After cluster 36 `content-distribution-vertical-rl`
is at 94.7 % with items visually correct; the residual is a per-section ~10 px
**vertical** drift affecting the intro `<p>` too — a text-rhythm / paragraph-margin
issue, *not* grid. Worth confirming separately; it may block several ~95 % tests
from crossing the 99 % pixel threshold and is likely cheaper than Workstream A.

---

## Workstream B — replaced-item grid intrinsic sizing (#5)

**Status: nearly done — the submodule patch has landed.** Clusters 34 & 35 take
`nested-grid-item-block-size-001` to ~84 %; cluster 35 (`patches/0004-…`) is now
**applied upstream** (`Broiler.CSS` commit `5a4fae1`, pointer bumped), so CI
reflects the 84 %. Remaining action:

1. ~~Land `patches/0004`~~ — ✅ done (pointer bumped to `5a4fae1`).
2. Close the residual **~16 %**: a `ul`/`li` + nested-inline-block horizontal
   offset exposed once the image sizes to full `55vw`. Compare Broiler vs the
   `-ref.html` render (both available via `--render`); the item's grid/inline-block
   chain offset is the suspect.

Separately, the grid pass **declines for replaced items** by design (documented at
`CssBoxGrid.cs` ~L197-209: "a replaced or form-control item … where sizing an auto
row from the measured height collapses"). When a grid row must be sized from a
replaced item's block size, the fallback approximation can collapse the row (seen
in synthetic `img`-in-`display:grid` repros). Making the real pass trust a
replaced item's definite block size (it has no reflow dependence on column width)
would let more replaced-in-grid cases keep the real track pass — a contained
follow-up.

**Key files.** `Broiler.Layout/Engine/CssBoxGrid.cs`, `CssBoxImage.cs`,
`CssBoxHelper.cs`; `Broiler.CSS` cascade (`patches/0004`). **Risk: low–medium.**
**Effort:** small once the patch lands.

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

`grid-positioned-items-within-grid-implicit-track-001`: an absolutely-positioned
grid item whose line references land **outside** the explicit grid must resolve
against the grid's *padding edge* extended by implicit lines, then be sized/placed
into that area (the reference shows a magenta abspos box overflowing the grid; in
Broiler it is dropped and the in-flow cyan item fills the grid instead).

**Root cause (suspected).** Abspos grid-item placement (`CssBoxGrid.cs`
placement + `CssBox.cs` abspos self-alignment, cf. cluster from #1215
"Fix absolutely-positioned grid items") does not resolve implicit-line references
for out-of-flow items to the padding edge, so the item collapses / is not painted.

**Proposed approach.** For an abspos grid item, resolve unknown/implicit grid
lines to the grid container's padding edges (CSS Grid §9.1), build the
containing-block rectangle from the resolved area, then run abspos sizing +
self-alignment against it. Confirm the in-flow item's placement is unaffected.

**Key files.** `Broiler.Layout/Engine/CssBoxGrid.cs`, `CssBox.cs` (abspos
resolution). **Risk: medium.** **Effort:** medium. **Validation:**
`css-grid/abspos` suite (212 tests with references already generated in the repro
harness below).

---

## Workstream E — gutter contribution to tracks/spanning/margins (#7)

`grid-gutters-and-tracks-001` is a large `checkLayout` test asserting that
`grid-gap`/`row-gap`/`column-gap` (a) add to the grid container size, (b) add to
spanning items, (c) do **not** alter item positioning, margin computation, or
track sizing, across `fit-content`, percentage tracks, `minmax`, named lines, and
a `verticalRL directionRTL` case. Broiler currently mismatches at 35.8 %
(`MissingContent`), i.e. several sub-checks lay out wrong.

**Approach.** Instrument which of the ~30 sub-grids fail (each `<div>` carries
`data-expected-*`), then fix the specific gutter accounting — most likely the
container's used size not including trailing/leading gaps, or spanning items not
adding the spanned gaps. The final block also exercises `vertical-rl` + RTL, so it
partly depends on **Workstream A**.

**Key files.** `Broiler.Layout/Engine/CssBoxGrid.cs` (`ResolveGridGap`, track
positioning, container size). **Risk: medium.** **Effort:** medium.

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

1. **B** — `patches/0004` has landed (pointer bumped); close the ~16 % `ul`/`li`
   offset (cheap, item #5).
2. **C** — item #9: ✅ **shrink-to-fit balloon fixed** — the real cause was a
   percentage-width grid item inflating intrinsic width (CSS Sizing 3 §5.1), not an
   auto-fill-count gap. See Workstream C. Remaining: score the 12 `checkLayout`
   variants on CI (needs the css-grid WPT corpus, unavailable in the sandbox).
3. **The #8 ~5 % vertical text-drift** — confirm/​fix the page-level paragraph
   rhythm; may cheaply flip several ~95 % vertical tests.
4. **A** — grid-axis transposition (unlocks #6/#8/#10 + ~58 alignment tests; the
   big one).
5. **D**, **E** — abspos implicit tracks, gutter accounting.
6. **F**, **G** — subgrid-orthogonal and `grid-lanes` (depend on A).

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

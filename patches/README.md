# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

The directory is a backlog, not an archive: it holds only what is *currently*
pending. A patch is deleted from here, along with its row below, once its fix is
upstream and the submodule pointer is bumped.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Exercised on CI before they land

`scripts/apply-pending-wpt-patches.sh` applies the patches listed in its
`PENDING_PATCHES` array to the checked-out submodule trees before the WPT run, so
a pending fix is reflected in CI's numbers rather than waiting on the pointer. It
is idempotent — a patch already contained in the pinned pointer reverse-applies
and is skipped — so an entry stops applying by itself once a maintainer lands it.

## Index

| Patch | Submodule | What it is |
|---|---|---|
| `0127-graphics-raster-band-parallelism` | `Broiler.Graphics` | **The second copy of the scanline rasterizer now narrows every fill to its clip and splits large ones into scanline bands.** Multithreading roadmap item #3 — the port of item #4's partitioner and item #5's clip narrowing into `Broiler.Graphics/Rendering/BCanvas.cs`, which backs `BImageRenderer` and through it Broiler.UI and the Writer. Independent of `0126`: different submodule, different rasterizer, no shared file. **The port's own measurement disagrees with the roadmap about which half was worth porting.** §8 of the roadmap says the clip narrowing is the part to port first; on this copy it is worth 1.04×/1.14×/1.02× on three of four scenes, and the sequential win came instead from something the banding forced by accident — `CurrentTarget`, a property that walks the layer stack, was being re-evaluated *per pixel*, and hoisting it out of the loop is worth 1.79×/1.39×/2.25×. The narrowing does pay where it can: 2.21× on the one scene whose content is inside the surface and outside the clip, which is what a clipped pane is and what the roadmap's own corpus does not contain. Sequential totals, medians of 13 at 1 280×1 024: `chrome` 184.2 → 99.1 ms, `list` 205.4 → 129.7, `pane` 149.3 → 50.5, `canvas` 825.5 → 360.4. **Threads add little on top, as §4 predicted for a different reason.** At four threads against one: `canvas` 1.39×, `pane` 1.26×, `chrome` 1.08×, `list` 1.00× — and the split shares say why it is not for want of splittable fills (85–100% of area split on three of four scenes), which is that most of a UI scene's cost is per-primitive rather than per-pixel. **It carries a floor the sibling copy does not, and that floor is a bug fix.** A fill that can only be cut two ways runs inline: a two-way split measured **437.7 ms against 362.9 ms sequential**, reproducing to a tenth of a millisecond across separate processes, while three bands measured 297.9 and four 270.4. Without it a two-core host runs *slower* with the parallelism on than off. The same inversion is visible in the sibling (`paint` page, 660.9 ms at one thread against 735.7 at two) and fixing it there needs a full WPT run, so it is left as a follow-up. **Exit gate:** 34 `RasterBandParallelismTests` cases covering every primitive that reaches the partitioner, each rendered at budgets of 1, 2, 3, 4 and 8 and compared byte for byte with its own single-threaded render; every clipped case additionally compared against an unclipped render masked by the clip, which is what catches a narrowing that drops a pixel the clip admits. `Broiler.Graphics.Tests` 99 tests, 0 failures; over-narrowing by one pixel fails five of them, two of which predate this change. **Deliberately NOT listed in `scripts/apply-pending-wpt-patches.sh`** — WPT renders through `Broiler.HTML.Image`'s rasterizer, not this one, so applying it there would exercise nothing. The main repo's `--graphics-raster-scaling` benchmark mode needs it and compiles itself out without it (see that project's `.csproj`), so a clean checkout still builds. |
| `0126-raster-tile-parallel-replay` | `Broiler.HTML` | **One display list is now replayed into disjoint horizontal strips of the surface at once, and the rasterizer stops walking pixels its clip is certain to reject.** Multithreading roadmap item #5, which item #4's measurement re-aimed as *the only shape of raster parallelism a page can use*: band-splitting one primitive moved one corpus page and nothing on three, because a page's raster is thousands of small fills rather than a few large ones. A tile's unit of work is "every fill that touches these rows", which exists on every page. **Four changes, and the first is not about threads at all.** (1) `BCanvas` keeps a running intersection of its *including* clips and narrows every primitive's pixel loop to it — exact, since a pixel the box excludes is one `IsVisible` already rejected, and it is what lets a tile draw its own rows instead of iterating the whole surface and discarding three quarters of it. (2) `BCanvas.CreateTileView` returns a canvas over the same surface carrying the current transform and clip stack plus the tile as one further clip; the transform goes across **unchanged**, so device coordinates are computed by the same arithmetic on the same inputs and the tiled image is identical rather than equivalent — translating geometry into tile-local space would have re-rounded every coordinate. (3) `RGraphicsRasterBackend` offers the replay to `TileParallelReplay` (in `Broiler.Layout`, this repository) and culls an item whose drawn rectangle the backend itself derives — without which every tile rebuilt the gradient tile bitmap of all 1 400 gradients on the `paint` page. (4) The text backend rejects a run whose row band the clip cannot admit, computed from the font's own ascent and descent before a glyph is looked at; a document is usually much taller than its viewport, so most runs draw nothing, and under tiling each of them was walked once per tile. **The bug worth recording is the clip stack.** A tile view's `GetClip()` returns its parent's clip **unnarrowed**, because a caller may derive geometry from the clip rather than only obey it: `DrawClippedImage` recomputes a scaled image's *source* rectangle from the intersection of its destination with `GetClip()`, so a tile-narrowed clip resampled the image. Algebraically the two mappings agree; a float rounding apart, they do not, and it surfaced as one row of different pixels on two `background-size` WPT tests and on no synthetic document tried. **Measured on the stage corpus, medians of `PerformPaint` at 1 → 4 tiles:** `paint` 1 323.7 → 461.4 ms (**2.87×**), `rules` 51.0 → 14.6 (3.49×), `text` 129.0 → 53.3 (2.42×), `mixed` 143.4 → 58.9 (2.44×), `boxes` 14.0 → 8.0 (1.76×) — and tiles beat band parallelism on all five, including the three bands could not touch. The culling alone, before any thread, takes the sequential raster stage from 210.9 → 148.4 ms on `text` and 41.7 → 14.0 on `boxes`. **Exit gate:** pixels identical at 1, 2 and 4 tiles crossed with 1 and 4 bands on all five corpus pages via the new `--tile-scaling` mode, which exits non-zero on a single differing byte; 69 `RasterTileParallelismTests` cases over eight documents chosen for what makes a tile more than a clip (compositing layers, nested clips, rounded clips, scaled background images, text past the viewport), with a guard test asserting each document actually reaches the tiled path and a direct assertion that a tile view reports its parent's clip; and a full WPT run at 1 and at 4 tiles whose **entire output diffs to zero lines** — every verdict, every pixel-match percentage, every bucket. Tiling is gated on the surface tolerating concurrent pixel writes *and* on every item in the list being one the raster canvas draws alone, so no tile reaches the compat backend the tiles share; `TileParallelReplay.CompatFallbacks` counts the gap between that per-item test and the surface's few per-call fallbacks, and it is asserted zero rather than assumed. **Listed in `scripts/apply-pending-wpt-patches.sh`** — it rewrites the loop that draws every WPT pixel, so having the run exercise it against the pinned pointer is exactly the check worth having, and its claim is that nothing moves. |

**Both are pending because the push was denied.** `Broiler-Platform/Broiler.HTML`
and `Broiler-Platform/Broiler.Graphics` are outside this session's GitHub scope,
so the git proxy declined to inject a credential and each push returned 403. The
submodule pointers are therefore **not** bumped — CI clones submodules by pointer
and would break on a commit it cannot fetch.

The two are independent and can be applied in either order, to different
submodules. Both were verified against their pinned pointer the way this file's
instructions apply them: `git am --keep-cr` onto a clean checkout of the pin,
then the resulting tree diffed against the branch the patch was generated from.

`0124` (band-parallel scanline fills, `Broiler.HTML`) and `0125` (glyph outline
cache, `Broiler.Graphics`) were the previous entries. Both are upstream and both
pointers are bumped, so they reach CI through the pointer; their files and rows
are deleted because this directory is a backlog, not an archive. `0126` applies
on top of `0124` — it is a further change to the same rasterizer, not a
replacement for it. `0127` is the same work carried across to the *other*
rasterizer, in the other submodule; it depends on neither.

<!-- Retired index note, kept for the record:

**Empty — nothing was pending.** `0123-css-cascade-rule-index` was the last entry;
it is upstream as `Broiler.CSS` `377c6dd` on `claude/css-cascade-rule-index` and
the submodule pointer is bumped, so the fix now reaches CI through the pointer
rather than through `apply-pending-wpt-patches.sh`. Its `PENDING_PATCHES` entry is
removed for the same reason: the script's idempotence guard would have skipped it
from here on, and an entry that can only ever skip is noise.

-->

<!-- Retired index row, kept for the record of what 0123 claimed and measured:

| `0123-css-cascade-rule-index` | `Broiler.CSS` | **The cascade tested every rule of every sheet against every element; it now tests only the rules an element's own id, classes and tag can reach.** Multithreading roadmap item #11 — the item that document's Phase 0 measurements identify as its highest-value one, ahead of every parallel item including the rasterizer, and single-threaded work. The evidence it was written against: on the roadmap corpus's rule-heavy page (700 `<div>`s, two classes each, against a 900-rule sheet) `parse+cascade` was **96.5% of a 5.2 s render**, and `RuleScalingBenchmarks` — which holds the *matched* rule count fixed at four while growing the sheet — measured cost and allocation both linear in **total** rules: 32× the rules gave 30.8× the time and 32.0× the bytes. `CssCascadeRuleIndex` flattens the sheet set into document order once and files each rule under the simple selector its subject must carry (id / class / type), with everything else in a universal bucket; an element merges the buckets its own keys reach and tests those. **The correctness argument is one-sided by construction and that is the whole point**: a key is a *necessary* condition for a match, never a sufficient one, candidates still go through the unchanged selector matcher, so the index can only be wrong by *omitting* a rule — and every case that is not certainly narrower resolves to the universal bucket (a bare `:is()`/`:not()`, an attribute-only compound, a namespaced type, an escaped identifier, anything the key scanner does not model). Candidates come back in **document order**, via a k-way merge of the buckets rather than a sort, so the cascade's source-order tie-break is untouched — a rule that never matched never advanced the tie-break counter, so visiting only candidates yields the same winners. `@media`/`@supports` are resolved once at index-build time (they depend on the environment and the feature oracle, both fixed for an index's lifetime) so a non-applying group costs nothing per element; `@container` depends on the element, so those conditions travel with the entry and are evaluated per candidate. The index is memoized against a **sheet** generation bumped only by sheet/environment changes — deliberately not by DOM mutation, which bumps the computed-style caches constantly and would otherwise rebuild the index on every tree edit. **The linear scan is kept and kept reachable** behind `CssStyleEngine.UseRuleIndex`, as the index's oracle: `CssCascadeRuleIndexTests` (22 cases) asserts the two cascades agree property by property — id/class/type keying, source-order tie-breaks, a rule reachable through two keys, combinators, pseudo-elements, the six unkeyable selector shapes, matching and non-matching `@media`, `@supports`, nested groups, late-added sheets, `ClearStyleSheets`, a viewport change — and over a generated 1200-rule sheet against a 120-element document. **Measured, and the exit gate is the scaling shape rather than a single number**: with `RuleScalingBenchmarks` holding the matched set at four and growing only the rules that cannot match, 32× the rules used to cost 30.8× the time and 32.0× the bytes and now costs **1.64× the time and 1.13× the bytes** — 114.9 ms → 15.74 ms at 100 rules, 3 543.6 ms → **25.89 ms** at 3 200 (136.9×), with allocation 5 817.9 MB → **9.69 MB** (600×). On a whole render the corpus `rules` page goes **5 218.96 ms → 1 841.71 ms** end to end and its `parse+cascade` stage **5 035.35 ms → 1 698.11 ms** (2.8× and 3.0×) — the smaller figure is Amdahl, since that stage also parses 110 296 characters and then cascades the rules that do match. **No rendering change**: `css/css-backgrounds` + `css/CSS2/linebox` (62 tests) is identical before and after — same pass/fail/skip on every test and the same 98.62245927777205% average pixel match to fourteen digits. Component suites: `Broiler.CSS.Tests` 341/341, `Broiler.CSS.Dom.Tests` 383 passed with the two `CssDomArchitectureTests` failures that are **pre-existing** (verified on a clean tree: 361 passed, the same 2 failed). Applies cleanly to the pinned `Broiler.CSS` pointer (`edc9fa9`) and survives `git am --keep-cr` — verified by applying it to a fresh clone of the pin and diffing the result against the branch it was generated from (identical tree). **Listed in `scripts/apply-pending-wpt-patches.sh`**: unlike a thread-safety or scheduling patch, this one *could* move rendering if the key extraction were ever wrong, so having the WPT run exercise it against the pinned pointer is the check worth having — and its claim is precisely that the numbers do not move. |

-->

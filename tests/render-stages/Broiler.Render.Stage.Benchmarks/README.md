# Broiler.Render.Stage.Benchmarks

The stage-level measurement harness for the
[multithreading roadmap](../../../docs/architecture/multithreading.md) — item **P0-a**
(a profile that attributes headless-render wall time to named stages) and item
**#19** (what the GC configuration is worth).

Everything in this project exists because the roadmap's own honesty caveat says every
gain figure in it is *structural, not measured*. Nothing past Phase 0 should be started
against an estimate.

## The profile — P0-a's exit gate, one command

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- \
    --profile --iterations 15 --warmup 5 --json results/stage-profile.json
```

Renders the whole corpus at 1280×1024 and reports, per page, the share of end-to-end wall
time taken by `parse+cascade`, `layout`, `paint (display list)`, `raster`, and an explicit
`(unattributed)` remainder. **Exits 1** when any page attributes less than 90% to named
stages, so the gate is checked by the command that produces the evidence rather than by a
human reading a table.

The published run is [`../results/stage-profile.md`](../results/stage-profile.md) with the
machine-readable copy beside it.

Two properties of the method are load-bearing and documented at length in `StageProfile.cs`:

- **The stage boundaries are the public pipeline**, not instrumentation added to the engine.
  `HtmlRender.RenderToImageCore` is four public calls in a row and the profiler walks the
  same four, so nothing here can drift from the real path by being edited separately.
- **The residual is reported, not absorbed.** A profile whose total was defined as the sum
  of its parts would pass its own exit gate by construction.

`raster` is derived (`PerformPaint − CreateDisplayList`) because those two share no public
seam; rows derived by subtraction are marked `*`.

## The benchmarks

BenchmarkDotNet, for the question the profile does not answer — how long one stage takes,
precisely, with error bars.

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- \
    --filter '*CascadeBenchmarks*' --job short
```

| Filter | P0-a harness | What it is for |
|---|---|---|
| `*RenderStageBenchmarks*` | (2), (3), (5) | `PerformLayout`, `PerformPaint`/`CreateDisplayList`, and the end-to-end render, per page |
| `*CascadeBenchmarks*` | (1) | The cascade alone, driven through `CssStyleEngine`: cold pass over every element, and the memoized pass |
| `*RuleScaling*` | (1) | The rule-count axis: element set and *matched* rules held fixed, sheet size varied. This is the case item #11 has to be argued from |
| `*DecodeBenchmarks*` | (4) | PNG and JPEG decode, two sizes × two content shapes |

`--job short` is enough to separate anything that differs by more than a few percent and
finishes in minutes; drop it for publication numbers.

## Thread scaling — items #3, #4, #5, #6, #7, #8, #12

Six modes that answer "how does this stage scale with threads, and does it change a
pixel". All report the second question as part of the first and **exit non-zero if any
setting produced different bytes**, because a speedup measured without that check is not
evidence of anything.

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --raster-scaling
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --graphics-raster-scaling
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --tile-scaling
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --decode-scaling
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --image-prefetch-scaling
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --style-scaling
```

`--raster-scaling` renders the whole corpus at 1, 2, 4 and *cores* raster threads, and
follows the timings with the partitioner's own count of how many fills were large enough to
split. That second table is the point: it is what distinguishes "the threads did not help"
from "no fill ever reached the threshold", which call for opposite next steps. It is how the
threshold in `BRasterParallelism` was chosen, and re-running it is how to re-choose it when
the rasterizer's per-pixel cost changes.

`--graphics-raster-scaling` is the same question asked of the **other** rasterizer —
`Broiler.Graphics.BCanvas`, which backs `BImageRenderer` and through it Broiler.UI and the
Writer (item #3). It does not render HTML, because that path does not reach this rasterizer;
it replays four `BRenderList` scenes built from the draw-call mix `Broiler.UI/src` actually
issues, and reports the same split-share table for the same reason `--raster-scaling` does.

Two things about it are worth knowing before quoting a number from it. `--only-threads N`
times a single setting so that one build can be compared against another — which is how the
port was separated from the parallelism, and is the only way to compare against a build that
lacks the feature. And the scenes always run in the order the corpus lists them, because a
process that replays only the largest scene measures it at 823 ms where this harness measures
it at 362: that scene is thirteen enormous fills and enters the fill path too few times to
leave OSR-compiled code, and the small fills ahead of it are what promote it. **A figure from
this command is comparable to another figure from this command and to nothing else.**

It needs `patches/0127-graphics-raster-band-parallelism.patch` applied to the
`Broiler.Graphics` submodule; without it the mode compiles itself out and says so, so the
project still builds on a clean checkout.

`--tile-scaling` renders the corpus at 1, 2, 4 and *cores* tiles (item #5) and reports the
`PerformPaint` stage as well as the whole render — tiling changes one stage, and on a page
where that stage is a tenth of the render an end-to-end table divides the effect by ten before
printing it. It follows the timings with the driver's own count of how many replays were tiled
and, when one was not, whether the **surface** refused it or its **display list** did. Those
call for opposite next steps, which is why they are counted apart. It also prints how many tile
views reached the compat backend, which is the one assumption the design rests on and is
expected to be zero.

`--decode-scaling` decodes 1 024² PNG and JPEG fixtures in two content shapes (a
photographic ramp and flat tiles, which load the entropy stages very differently) at the same
settings.

`--image-prefetch-scaling` renders an image-heavy document at 1, 2, 4 and *cores* concurrent
image loads (item #8) and reports the `PerformLayout` stage — where an image load runs — beside
the whole render. **It owns its fixture rather than using the corpus, and that is not a
shortcut:** the five corpus pages contain no images by construction, since each is built to
load one stage, so adding one would both fold decode into another page's row and invalidate
every number the published profile quotes. The fixture is a directory of real PNG, JPEG files
plus the document that references them, because `SetImageFromFile` is the path that is inline
and serial; a `data:` URI takes a different one. It follows the timings with the walk's own
count of how many loads it issued and, when it declined a document, whether that was for naming
too few images or for a host that loads them asynchronously — as with the tile driver's two
refusal counts, those call for opposite next steps.

`--style-scaling` renders the corpus at 1, 2, 4 and *cores* style threads (item #12) and
reports the two cascade sub-stages together, because the item has two halves that do not
scale together: `cascade (resolve)` is the threaded warm pass and `cascade (project)` is the
ordered box walk that consumes it and cannot be threaded. Its `project residue` column is
therefore Amdahl's serial fraction **measured rather than assumed** — it says how much of a
further speedup is even available, which is what decides whether another thread is worth
adding to a page. A budget of 1 is not one thread through the warm pass but the warm pass
switched off, which is the code that shipped before the item. Published run:
[`../results/style-scaling.md`](../results/style-scaling.md).

Reading it needs the sub-stage rows the profile also prints, and those come from
`RenderStageTrace` — the one place in this project that times *inside* the engine rather than
around it. Why that is not the drift risk P0-a rejects, and what a reader has to check in its
place, is written up on the type itself.

Its last column is the full concurrent-load budget with the *within*-image decode budget divided
across the loads in flight — the correction that `N` loads × `N` bands on `N` cores obviously
calls for. Over four runs it has no consistent sign (three favour it by 1–8%, one penalises it by
9%), and it is kept as a column so that null result stays re-runnable rather than remembered.

**All of them interleave their settings within each iteration rather than measuring one setting
at a time.** This container's throughput drifts by tens of percent over tens of seconds — enough
that the same untouched decode measured in two consecutive processes differs by more than the
effect being measured. Blocked measurement cannot tell a speedup from the box getting faster.
The divided column above is the cautionary example: measured in its own block it read 1.10×
faster than the undivided one on one run and 1.17× *slower* on the next, and only interleaving
it showed that the true answer is neither.

The budgets are also settable directly, which is what a caller running several renders at once
should do (see the multithreading roadmap, "two kinds of parallelism now multiply"):

| Variable | Controls |
|---|---|
| `BROILER_RASTER_THREADS` | Threads one scanline fill may use (item #4) |
| `BROILER_RASTER_MIN_AREA` | Pixels a fill must cover before it is split at all |
| `BROILER_RASTER_MIN_BAND` | Pixels one band must be worth |
| `BROILER_RASTER_TILES` | Tiles one display-list replay may be split into (item #5) |
| `BROILER_RASTER_MIN_TILE_ROWS` | Rows a tile must be worth before the surface is split further |
| `BROILER_IMAGE_DECODE_THREADS` | Threads one decode pass may use (items #6, #7) |
| `BROILER_IMAGE_PREFETCH_THREADS` | Image loads a document may have in flight at once (item #8) |

The two raster budgets do not compound: a tile view runs its scanline bands inline, so a render
spends whichever of the two it is using and never both. A host dividing cores between processes
should give each the same figure rather than a share of it.

The two *decode* budgets do compound — a concurrent image load decodes through the band
partitioner — but dividing them measured as nothing (see above), so they are also given the same
figure. `BROILER_IMAGE_PREFETCH_THREADS=1` turns the walk off entirely rather than running it one
wide, which is what makes it the sequential path the exit gate compares against.

## Relayout — item #14

Two modes about the *second* layout, the one nothing else here performs. Everything above
renders a page once from a clean container, which is the case a rebuild-avoidance item cannot
be measured against at all.

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --relayout-profile
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --relayout-parity
```

`--relayout-profile` mutates each corpus page the way a script would — nine mutations, from a
class toggle to a twenty-write burst — and reports what the following layout cost, split into
the box-tree rebuild and the layout pass beneath it. Its `rebuilt?` column reports the
decision `RenderTreeInvalidation` recorded rather than leaving it to be inferred from a time,
and it has three states: a submodule tree that consults the ledger nowhere prints `n/a`, which
is not the same claim as `ELIDED`. Published run:
[`../results/relayout-profile.md`](../results/relayout-profile.md).

`--relayout-parity` is the exit gate, and it is the one that can fail. It renders every page
after every mutation twice — elision on, then off (`RenderTreeInvalidation.Elision`, which is
the version compare that shipped before item #14) — and compares the two images byte for byte.
A wrong classification is a *stale page*, so this is the check that a skipped rebuild produced
the page a rebuild would have. It also fails a run in which nothing was elided: a green run
that compared no elisions is how this would quietly stop being a gate.

The same switch is available as `BROILER_RENDER_TREE_ELISION=0`, which is what to reach for
first when a rendering bug is suspected to be a stale relayout.

## Layout composition and independence — item #13

Two modes about what a *single* layout pass contains, built before any of item #13 so the
item's two proposed shapes could be checked against a measurement rather than a reading.

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --layout-composition
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --layout-independence --wpt-dir tests/wpt
```

`--layout-composition` splits a pass into disjoint self times — intrinsic sizing, text
measurement, line breaking, table and flex — and reports the remainder as a residual rather
than absorbing it. Two controls sit in the output because the shares are otherwise
unreadable: every page is *also* rendered with the trace off, interleaved iteration by
iteration, so a reader can see how much of the traced pass is the trace; and the disabled
cost is bounded by measuring one `Count` over 20 M calls and multiplying by each page's
exact call count, because this host's layout stage drifts by more than the quantity a
whole-render A/B would be trying to resolve. Published run:
[`../results/layout-composition.md`](../results/layout-composition.md).

`--layout-independence` runs the same census over real WPT documents instead of the corpus.
It exists because the corpus's own answer is a property of its generator — `paint` *is* 1 400
`position:absolute` divs — so the corpus can report an independence ceiling of 99.6% and mean
nothing by it. Counts only, no clock. Published run:
[`../results/layout-independence.md`](../results/layout-independence.md).

## GC configuration — item #19

The mode is fixed when the runtime starts, so one process cannot compare the two. Run it
twice:

```sh
DOTNET_gcServer=0 dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --gc-config --rounds 5
DOTNET_gcServer=1 dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --gc-config --rounds 5
```

Reports throughput plus allocation and per-generation collection counts for whichever mode
the process is in. The project deliberately does **not** set `ServerGarbageCollection`;
pinning a mode in the props file would make the project unable to answer the question it
exists for.

## The corpus

Five pages, generated deterministically in `Corpus.cs` — no `Random`, no checked-in
fixtures, no dependency on a WPT checkout, so two runs on two machines render byte-identical
documents.

| Page | Built to load |
|---|---|
| `text` | line breaking, text measurement, glyph raster |
| `rules` | the cascade's per-element rule scan (item #11) |
| `boxes` | layout: a nested block/flex/grid tree |
| `paint` | raster: overlapping gradients, borders, alpha (items #4, #5) |
| `mixed` | blended control — a bordered table with text, no stage emphasised |

`GraphicsSceneCorpus.cs` holds a second, smaller corpus for `--graphics-raster-scaling`: four
`BRenderList` scenes rather than HTML pages, because item #3's rasterizer is reached by
drawing a render list and not by rendering a document. Same rules — deterministic, generated
in code, no fixtures.

| Scene | Built to load |
|---|---|
| `chrome` | a whole window: title bar, toolbar, sidebar tree, content rows, status bar |
| `list` | a scrolled list whose overflow falls **off the surface** — the case the loop clamp already handles |
| `pane` | a grid clipped to a pane, overflow **on the surface** — the case only a clip bound can reject |
| `canvas` | the control: surface-sized fills and nothing small, so a flat row means the threads, not the content |

Pages that load one stage each are what makes the stages separable. A single
"representative page" produces one blended number and answers none of the roadmap's open
questions; `mixed` is there to say what a page with no particular emphasis costs.

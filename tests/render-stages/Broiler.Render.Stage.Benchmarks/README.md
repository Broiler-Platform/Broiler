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
| `paint` | raster: overlapping gradients, borders, alpha (items #3, #5) |
| `mixed` | blended control — a bordered table with text, no stage emphasised |

Pages that load one stage each are what makes the stages separable. A single
"representative page" produces one blended number and answers none of the roadmap's open
questions; `mixed` is there to say what a page with no particular emphasis costs.

# Headless render stage profile

> **Two notes before reading the `parse+cascade` breakdown.** It is produced by
> `RenderStageTrace`, whose scopes are opened in `Broiler.HTML`'s `DomParser`.
> Those scopes arrived as `patches/0129-html-cascade-substage-trace-and-warm-pass`
> and are **upstream now**, so the pinned pointer carries them and a bare checkout
> reproduces these rows; against a `Broiler.HTML` older than the pin the sub-stage
> rows are all zero and the whole stage lands in `(untimed)`.
>
> And this run **predates multithreading item #12**, so its single `cascade` row
> is the stage as the roadmap had been carrying it. Today's profiler splits that
> row in two — `cascade (resolve)`, the threaded warm pass, and
> `cascade (project)`, the ordered box walk — which is what
> [`style-scaling.md`](style-scaling.md) reads. The composition this run reports
> is what Phase 3 §1 of the multithreading roadmap quotes, and it is kept as
> written for the reason the roadmap keeps every superseded measurement: it is
> what the decision was made on.

- Viewport: 1280x1024
- Iterations: 15 measured, 5 warm-up; figures are medians
- Runtime: 10.0.10, 4 logical cores
- Raster threads: 4 (item #4; `BROILER_RASTER_THREADS`, default one per core)
- Raster tiles: 4 (item #5; `BROILER_RASTER_TILES`, default one per core)
- GC: Workstation, Interactive latency mode

## text — line breaking, text measurement, glyph raster

77,629 chars of source; 266.61 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 90.60 | 34.0% |
| layout | 51.56 | 19.3% |
| paint (display list) | 6.66 | 2.5% |
| raster * | 114.29 | 42.9% |
| (unattributed) * | 3.50 | 1.3% |

Attributed to named stages: **98.7%**

Inside `parse+cascade`:

| Sub-stage | ms | of stage | of render |
|---|---:|---:|---:|
| html parse | 2.19 | 2.4% | 0.8% |
| css parse | 0.38 | 0.4% | 0.1% |
| cascade | 73.67 | 81.3% | 27.6% |
| box fixups | 7.31 | 8.1% | 2.7% |
| (untimed) | 7.04 | 7.8% | 2.6% |

## rules — cascade: per-element rule scan (item #11)

110,296 chars of source; 2898.69 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 2813.13 | 97.0% |
| layout | 43.42 | 1.5% |
| paint (display list) | 5.48 | 0.2% |
| raster * | 24.15 | 0.8% |
| (unattributed) * | 12.50 | 0.4% |

Attributed to named stages: **99.6%**

Inside `parse+cascade`:

| Sub-stage | ms | of stage | of render |
|---|---:|---:|---:|
| html parse | 28.84 | 1.0% | 1.0% |
| css parse | 3.42 | 0.1% | 0.1% |
| cascade | 2769.65 | 98.5% | 95.5% |
| box fixups | 4.14 | 0.1% | 0.1% |
| (untimed) | 7.09 | 0.3% | 0.2% |

## boxes — layout: nested block/flex/grid tree

47,210 chars of source; 425.28 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 349.28 | 82.1% |
| layout | 50.60 | 11.9% |
| paint (display list) | 4.22 | 1.0% |
| raster * | 12.74 | 3.0% |
| (unattributed) * | 8.42 | 2.0% |

Attributed to named stages: **98.0%**

Inside `parse+cascade`:

| Sub-stage | ms | of stage | of render |
|---|---:|---:|---:|
| html parse | 6.46 | 1.9% | 1.5% |
| css parse | 1.06 | 0.3% | 0.2% |
| cascade | 339.89 | 97.3% | 79.9% |
| box fixups | 2.37 | 0.7% | 0.6% |
| (untimed) | 0.00 | 0.0% | 0.0% |

## paint — raster: overlapping gradients, borders, alpha

211,592 chars of source; 857.91 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 248.22 | 28.9% |
| layout | 31.40 | 3.7% |
| paint (display list) | 10.88 | 1.3% |
| raster * | 555.54 | 64.8% |
| (unattributed) * | 11.87 | 1.4% |

Attributed to named stages: **98.6%**

Inside `parse+cascade`:

| Sub-stage | ms | of stage | of render |
|---|---:|---:|---:|
| html parse | 10.81 | 4.4% | 1.3% |
| css parse | 0.63 | 0.3% | 0.1% |
| cascade | 238.05 | 95.9% | 27.7% |
| box fixups | 0.64 | 0.3% | 0.1% |
| (untimed) | 0.00 | 0.0% | 0.0% |

## mixed — blended control: table, borders, text

20,102 chars of source; 223.38 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 113.02 | 50.6% |
| layout | 31.83 | 14.2% |
| paint (display list) | 2.91 | 1.3% |
| raster * | 72.93 | 32.6% |
| (unattributed) * | 2.70 | 1.2% |

Attributed to named stages: **98.8%**

Inside `parse+cascade`:

| Sub-stage | ms | of stage | of render |
|---|---:|---:|---:|
| html parse | 2.37 | 2.1% | 1.1% |
| css parse | 0.33 | 0.3% | 0.1% |
| cascade | 104.65 | 92.6% | 46.8% |
| box fixups | 1.27 | 1.1% | 0.6% |
| (untimed) | 4.39 | 3.9% | 2.0% |

`*` derived by subtraction rather than timed directly; see `StageProfile`.

Wrote results/stage-profile.json

P0-a exit gate MET: every page attributes >=90% of wall time to named stages (worst: boxes at 98.0%).

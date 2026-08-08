# Headless render stage profile

- Viewport: 1280x1024
- Iterations: 15 measured, 5 warm-up; figures are medians
- Runtime: 10.0.10, 4 logical cores
- Raster threads: 4 (item #4; `BROILER_RASTER_THREADS`, default one per core)
- GC: Workstation, Interactive latency mode

## text — line breaking, text measurement, glyph raster

77,629 chars of source; 292.03 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 48.83 | 16.7% |
| layout | 30.82 | 10.6% |
| paint (display list) | 3.49 | 1.2% |
| raster * | 199.75 | 68.4% |
| (unattributed) * | 9.14 | 3.1% |

Attributed to named stages: **96.9%**

## rules — cascade: per-element rule scan (item #11)

110,296 chars of source; 2059.84 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 1925.79 | 93.5% |
| layout | 32.39 | 1.6% |
| paint (display list) | 5.12 | 0.2% |
| raster * | 87.62 | 4.3% |
| (unattributed) * | 8.91 | 0.4% |

Attributed to named stages: **99.6%**

## boxes — layout: nested block/flex/grid tree

47,210 chars of source; 269.18 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 199.33 | 74.1% |
| layout | 29.06 | 10.8% |
| paint (display list) | 2.21 | 0.8% |
| raster * | 38.65 | 14.4% |
| (unattributed) * | 0.00 | 0.0% |

Attributed to named stages: **100.0%**

## paint — raster: overlapping gradients, borders, alpha

211,592 chars of source; 975.72 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 164.82 | 16.9% |
| layout | 21.60 | 2.2% |
| paint (display list) | 7.49 | 0.8% |
| raster * | 774.11 | 79.3% |
| (unattributed) * | 7.69 | 0.8% |

Attributed to named stages: **99.2%**

## mixed — blended control: table, borders, text

20,102 chars of source; 220.69 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 72.04 | 32.6% |
| layout | 21.93 | 9.9% |
| paint (display list) | 1.73 | 0.8% |
| raster * | 125.14 | 56.7% |
| (unattributed) * | 0.00 | 0.0% |

Attributed to named stages: **100.1%**

`*` derived by subtraction rather than timed directly; see `StageProfile`.

Wrote tests/render-stages/results/stage-profile.json

P0-a exit gate MET: every page attributes >=90% of wall time to named stages (worst: text at 96.9%).

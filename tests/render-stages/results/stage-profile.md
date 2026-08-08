# Headless render stage profile

- Viewport: 1280x1024
- Iterations: 15 measured, 5 warm-up; figures are medians
- Runtime: 10.0.10, 4 logical cores
- Raster threads: 4 (item #4; `BROILER_RASTER_THREADS`, default one per core)
- Raster tiles: 4 (item #5; `BROILER_RASTER_TILES`, default one per core)
- GC: Workstation, Interactive latency mode

## text — line breaking, text measurement, glyph raster

77,629 chars of source; 164.44 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 60.59 | 36.8% |
| layout | 32.26 | 19.6% |
| paint (display list) | 7.62 | 4.6% |
| raster * | 64.96 | 39.5% |
| (unattributed) * | 0.00 | 0.0% |

Attributed to named stages: **100.6%**

## rules — cascade: per-element rule scan (item #11)

110,296 chars of source; 2047.69 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 1989.68 | 97.2% |
| layout | 30.80 | 1.5% |
| paint (display list) | 2.77 | 0.1% |
| raster * | 20.65 | 1.0% |
| (unattributed) * | 3.80 | 0.2% |

Attributed to named stages: **99.8%**

## boxes — layout: nested block/flex/grid tree

47,210 chars of source; 263.93 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 213.38 | 80.8% |
| layout | 21.87 | 8.3% |
| paint (display list) | 4.16 | 1.6% |
| raster * | 18.96 | 7.2% |
| (unattributed) * | 5.56 | 2.1% |

Attributed to named stages: **97.9%**

## paint — raster: overlapping gradients, borders, alpha

211,592 chars of source; 667.62 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 176.46 | 26.4% |
| layout | 25.84 | 3.9% |
| paint (display list) | 7.78 | 1.2% |
| raster * | 454.02 | 68.0% |
| (unattributed) * | 3.52 | 0.5% |

Attributed to named stages: **99.5%**

## mixed — blended control: table, borders, text

20,102 chars of source; 162.36 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 86.24 | 53.1% |
| layout | 18.90 | 11.6% |
| paint (display list) | 1.96 | 1.2% |
| raster * | 48.35 | 29.8% |
| (unattributed) * | 6.90 | 4.3% |

Attributed to named stages: **95.7%**

`*` derived by subtraction rather than timed directly; see `StageProfile`.

Wrote results/stage-profile.json

P0-a exit gate MET: every page attributes >=90% of wall time to named stages (worst: mixed at 95.7%).

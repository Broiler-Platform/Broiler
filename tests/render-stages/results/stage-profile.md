# Headless render stage profile

- Viewport: 1280x1024
- Iterations: 15 measured, 5 warm-up; figures are medians
- Runtime: 10.0.10, 4 logical cores
- GC: Workstation, Interactive latency mode

## text — line breaking, text measurement, glyph raster

77,629 chars of source; 494.10 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 125.71 | 25.4% |
| layout | 32.02 | 6.5% |
| paint (display list) | 3.34 | 0.7% |
| raster * | 314.75 | 63.7% |
| (unattributed) * | 18.27 | 3.7% |

Attributed to named stages: **96.3%**

## rules — cascade: per-element rule scan (item #11)

110,296 chars of source; 5218.96 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 5035.35 | 96.5% |
| layout | 33.71 | 0.6% |
| paint (display list) | 3.68 | 0.1% |
| raster * | 135.26 | 2.6% |
| (unattributed) * | 10.96 | 0.2% |

Attributed to named stages: **99.8%**

## boxes — layout: nested block/flex/grid tree

47,210 chars of source; 528.60 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 423.28 | 80.1% |
| layout | 26.08 | 4.9% |
| paint (display list) | 2.93 | 0.6% |
| raster * | 69.21 | 13.1% |
| (unattributed) * | 7.11 | 1.3% |

Attributed to named stages: **98.7%**

## paint — raster: overlapping gradients, borders, alpha

211,592 chars of source; 1932.97 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 403.69 | 20.9% |
| layout | 19.16 | 1.0% |
| paint (display list) | 8.06 | 0.4% |
| raster * | 1503.70 | 77.8% |
| (unattributed) * | 0.00 | 0.0% |

Attributed to named stages: **100.1%**

## mixed — blended control: table, borders, text

20,102 chars of source; 497.90 ms end to end.

| Stage | ms | share |
|---|---:|---:|
| parse+cascade | 221.20 | 44.4% |
| layout | 19.40 | 3.9% |
| paint (display list) | 2.24 | 0.4% |
| raster * | 247.33 | 49.7% |
| (unattributed) * | 7.74 | 1.6% |

Attributed to named stages: **98.4%**

`*` derived by subtraction rather than timed directly; see `StageProfile`.

Wrote tests/render-stages/results/stage-profile.json

P0-a exit gate MET: every page attributes >=90% of wall time to named stages (worst: text at 96.3%).

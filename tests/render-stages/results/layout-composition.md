# What a layout pass is made of

- Viewport: 1280x1024, fixed width — the shape every path in this repository uses
- Iterations: 11 measured, 3 warm-up; times are medians, counts are structural (asserted invariant across iterations)
- Runtime: 10.0.10, 4 logical cores

## Self time per operation

Disjoint: a scope opened inside another is charged to the inner one and subtracted
from the outer, so `residual` is the block-flow walk itself rather than an artefact.

| page | traced ms | untraced ms | trace cost | intrinsic sizing | text measure | line breaking | table layout | flex layout | residual |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| text | 58.44 | 66.85 | 0.87x | 1.25 (2.1%) | 2.49 (4.3%) | 18.27 (31.3%) | 0.00 (0.0%) | 0.00 (0.0%) | 36.43 (62.3%) |
| rules | 51.17 | 47.38 | 1.08x | 1.63 (3.2%) | 2.28 (4.5%) | 11.60 (22.7%) | 0.00 (0.0%) | 0.00 (0.0%) | 35.66 (69.7%) |
| boxes | 42.72 | 37.70 | 1.13x | 6.40 (15.0%) | 4.52 (10.6%) | 4.42 (10.3%) | 0.00 (0.0%) | 3.93 (9.2%) | 23.44 (54.9%) |
| paint | 33.83 | 28.97 | 1.17x | 0.45 (1.3%) | 2.95 (8.7%) | 2.01 (5.9%) | 0.00 (0.0%) | 0.00 (0.0%) | 28.42 (84.0%) |
| mixed | 28.12 | 37.41 | 0.75x | 2.52 (9.0%) | 3.09 (11.0%) | 2.71 (9.6%) | 4.76 (16.9%) | 0.00 (0.0%) | 15.03 (53.5%) |

`untraced ms` is the identical render with the trace off, measured interleaved with the
traced one. **Trace cost spans 0.75x–1.17x** — and a ratio below 1.00 is a
tracer that made the pass faster, which is impossible, so the spread is this host's drift
rather than the trace. The shares are shares of the traced pass, and the control is a
column rather than a footnote because without it they would describe an instrumented
engine with no way to tell.

### What it costs when off

A disabled `Count` measured over 20,000,000 calls: **0.34 ns**.
Multiplied by the exact call count each page makes — structural, so no drift enters —
that bounds what an engine carrying this instrumentation pays with it switched off:

| page | disabled calls | bound (ms) | of the pass |
|---|---:|---:|---:|
| text | 5497 | 0.0018 | 0.0% |
| rules | 12617 | 0.0042 | 0.0% |
| boxes | 31607 | 0.0106 | 0.0% |
| paint | 8417 | 0.0028 | 0.0% |
| mixed | 13101 | 0.0044 | 0.0% |

The scope-based `Measure` sites are not in that count and are fewer than the `Count`
sites by more than an order of magnitude; disabled, `Measure` returns a `default` struct
whose `Dispose` is a null check, so it is bounded by the same constant.

## Intrinsic sizing, counted

`visits` is how many boxes the recursive min/max-content helpers touch; `visits/box`
divides it by the boxes the pass laid out. A pass that measured each box's content
once would read 1.

| page | boxes laid out | intrinsic calls | intrinsic visits | visits/box | calls/box |
|---|---:|---:|---:|---:|---:|
| text | 264 | 264 | 4969 | 18.82 | 1.00 |
| rules | 704 | 704 | 11209 | 15.92 | 1.00 |
| boxes | 724 | 2434 | 28449 | 39.29 | 3.36 |
| paint | 1404 | 1404 | 5609 | 4.00 | 1.00 |
| mixed | 716 | 2136 | 10249 | 14.31 | 2.98 |

## Independent subtrees, counted

`independent` is the union — a box under two independent roots is counted once — and it
is the ceiling a subtree split is bounded by. Multi-column and subdocuments are on item
#13's list and are **not** counted: a column is not a box in this engine and a
subdocument is laid out by its own container, so neither has a root this walk can name.

| page | tree boxes | depth | abspos roots / boxes | table cells / boxes | flex+grid items / boxes | independent | share |
|---|---:|---:|---:|---:|---:|---:|---:|
| text | 1246 | 6 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0.0% |
| rules | 2806 | 7 | 0 / 0 | 0 / 0 | 0 / 0 | 0 | 0.0% |
| boxes | 2166 | 13 | 0 / 0 | 0 / 0 | 810 / 3510 | 2070 | 95.6% |
| paint | 1406 | 5 | 1400 / 1400 | 0 / 0 | 0 / 0 | 1400 | 99.6% |
| mixed | 1501 | 8 | 0 / 0 | 710 / 1420 | 0 / 0 | 1420 | 94.6% |


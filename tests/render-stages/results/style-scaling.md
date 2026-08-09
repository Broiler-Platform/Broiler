# Parallel style recalc scaling (multithreading item #12)

- Runtime: 10.0.10, 4 logical cores
- Viewport: 1280x1024
- 9 measured iterations, 3 warm-up; figures are medians
- Style threads: 1, 2, 4 (1 = warm pass off)
- Minimum elements to run the warm pass: 64
- GC: Workstation

Cascade sub-stages (`cascade (resolve)` + `cascade (project)`):

| Page | 1T ms | 2T ms | 4T ms | speedup | project residue |
|---|---:|---:|---:|---:|---:|
| text | 77.2 | 63.4 | 66.6 | 1.16x | 28% |
| rules | 2838.9 | 1950.8 | 1413.3 | 2.01x | 16% |
| boxes | 339.8 | 269.7 | 233.3 | 1.46x | 55% |
| paint | 262.7 | 189.7 | 166.4 | 1.58x | 48% |
| mixed | 120.9 | 100.1 | 96.2 | 1.26x | 41% |

End to end, the same renders:

| Page | 1T ms | 2T ms | 4T ms | speedup | pixels |
|---|---:|---:|---:|---:|---|
| text | 270.7 | 237.2 | 250.1 | 1.08x | identical |
| rules | 2974.3 | 2119.6 | 1521.1 | 1.96x | identical |
| boxes | 419.7 | 354.8 | 307.9 | 1.36x | identical |
| paint | 892.2 | 812.2 | 797.1 | 1.12x | identical |
| mixed | 248.7 | 231.2 | 229.6 | 1.08x | identical |

Every thread setting rendered pixel-identically to the sequential cascade.

## Reading this

Reproduce with:

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- \
    --style-scaling --iterations 9 --warmup 3
```

**`project residue` is the number to read second, and it is the one that says what
is left.** The warm pass threads the per-element cascade; the box walk that consumes
it stays ordered and single-threaded, for the reasons written up in
`CssStyleRecalc`. So the residue is Amdahl's serial fraction measured rather than
assumed, and it is the ceiling on adding *cores*: at 16% on `rules` a bigger machine
still has up to 6x left to give before the walk becomes the floor; at 55% on `boxes`
there is at most 1.8x however many cores are added, and the 1.46x already measured is
most of it. Threading the walk itself is a different and much harder lever, and its
payoff is the mirror image — near-nothing on `rules`, most of what remains on `boxes`.

**`text` is slower at four threads than at two in this run**, which is what a 77 ms
cascade on a four-core box looks like when the fourth worker returns less than it
costs. A second run at seven iterations does not reproduce the ordering, so the claim
worth making is the weaker one: on that page the fourth thread buys nothing
measurable, in either direction.

**The table reproduces.** A second run reads 1.17x / 2.16x / 1.35x / 1.43x / 1.27x on
the cascade sub-stages against the 1.16x / 2.01x / 1.46x / 1.58x / 1.26x above, with
the residue column within two points on every page.

**The end-to-end column divides the stage win by whatever else the page does.**
`paint` gains 1.58x on the cascade and 1.12x on the render, because two thirds of
that render is raster. That is not a disappointment — it is the same arithmetic that
made item #4 worth 1.42x end to end on the page it was aimed at — but a reader
comparing items has to compare the same column.

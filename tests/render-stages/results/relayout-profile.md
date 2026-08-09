# Relayout profile — the precondition for multithreading item #14

What a *second* layout costs after the document is mutated the way script mutates
it. Roadmap [§7](../../docs/architecture/multithreading.md#7-item-14-has-no-measurement-it-can-be-started-against-and-building-it-first-would-be-building-it-blind)
named this harness as the first thing Phase 3's remainder should build, ahead of
any dirty bit, because item #14 bounds a stage nothing in P0-a performs.

Reproduce with:

```sh
dotnet run --project tests/render-stages/Broiler.Render.Stage.Benchmarks -c Release \
  -- --relayout-profile [--iterations N] [--warmup N]
```

Host: 4 cores, .NET 10, Release, viewport 1280×1024, medians of 5 iterations
after 1 warm-up. Requires `patches/0129` for the sub-stage split; without it the
run reports totals and says so rather than printing zeros.

## What "relayout" is in this engine today

`HtmlContainerInt` holds a bound `DomDocument` and a copy of its `Version`.
`EnsureBoundDocumentCurrent` compares the two at the top of every `PerformLayout`
and, when they differ, calls `BuildBoundDocument` — which **disposes the render
tree and regenerates it from scratch**. So a relayout is not a layout pass: it is
a full box-tree rebuild and a full cascade, followed by a full-tree layout.

Because `SetDocument` builds the tree, the **1st layout** column below is the
layout pass alone; **relayout** is rebuild + layout.

## The result

| Page | Mutation | 1st layout | Relayout | Rebuild | Layout | Rebuild share |
|---|---|---|---|---|---|---|
| text | class toggle | 93.29 | 287.66 | 191.59 | 96.06 | 66.6% |
| text | inline style write | 76.33 | 179.04 | 111.53 | 67.52 | 62.3% |
| text | text write | 62.06 | 161.00 | 103.96 | 57.04 | 64.6% |
| text | inserted subtree | 56.80 | 185.43 | 111.70 | 73.73 | 60.2% |
| rules | class toggle | 53.04 | **1 446.35** | 1 404.17 | 42.18 | **97.1%** |
| rules | inline style write | 51.09 | **1 598.97** | 1 534.86 | 64.12 | 96.0% |
| rules | text write | 51.73 | 1 462.52 | 1 405.11 | 57.41 | 96.1% |
| rules | inserted subtree | 54.79 | 1 668.82 | 1 611.66 | 57.16 | 96.6% |
| boxes | class toggle | 57.34 | 332.32 | 291.49 | 40.83 | 87.7% |
| boxes | inline style write | 52.89 | 324.08 | 273.76 | 50.33 | 84.5% |
| boxes | text write | 47.86 | 373.21 | 314.72 | 58.49 | 84.3% |
| boxes | inserted subtree | 61.03 | 344.26 | 293.99 | 50.28 | 85.4% |
| paint | class toggle | 37.66 | 254.90 | 207.41 | 47.50 | 81.4% |
| paint | inline style write | 38.75 | 245.42 | 197.57 | 47.85 | 80.5% |
| paint | text write | 30.36 | 180.03 | 163.09 | 16.93 | 90.6% |
| paint | inserted subtree | 32.67 | 243.71 | 211.69 | 32.03 | 86.9% |
| mixed | class toggle | 38.83 | 170.22 | 124.96 | 45.26 | 73.4% |
| mixed | inline style write | 43.78 | 149.30 | 114.64 | 34.66 | 76.8% |
| mixed | text write | 33.95 | 149.82 | 119.92 | 29.90 | 80.0% |
| mixed | inserted subtree | 28.15 | 148.92 | 104.84 | 44.08 | 70.4% |

Rebuild sub-stages are dominated by the cascade on every page — `cascade
(resolve)` plus `cascade (project)` are 1 108.6 + 218.7 ms of the `rules` page's
1 404 ms rebuild, against 4.8 ms of HTML parse and 3.7 ms of CSS parse.

## Three findings, and the first one re-aims item #14

**1. A relayout is 60–97% rebuild. The layout pass is 17–96 ms and barely moves.**
Item #14 as written — "dirty bits + relayout roots" on `CssBox.PerformLayout` —
bounds the *layout* column, which is between 3% and 39% of what a relayout costs.
On the rule-heavy page it is 2.9%. **The item is aimed at the smaller half**, and
on the page where relayout hurts most it is aimed at almost none of it. The
invalidation that would pay is on the box tree and the cascade — the work
`BuildBoundDocument` throws away and redoes — not on the layout pass beneath it.

**2. A one-attribute change costs a whole-document re-cascade, and the engine
cannot tell the four mutations apart.** A class toggle, an inline-style write, a
text write and an inserted subtree all bump `DomDocument.Version` by one and all
produce the same total work, to within the noise, on every page. There is no
granularity to exploit yet: the version counter is the only signal, and it says
"something changed" and nothing else. Any dirty-bit scheme has to start by giving
the DOM a way to say *what* changed, which is a `Broiler.DOM` change, before
anything downstream can act on it.

**3. The relayout is 3–31× the first layout, so the interactive case is worse
than the first-render case rather than a cheaper version of it.** The `rules`
page lays out in 53 ms and relays out in 1 446 ms. Item #14's estimate column
("5–50× on interactive relayout") now has a measurement behind it, and the
measurement says the ceiling is real but sits somewhere other than the item
claims: eliminating the rebuild entirely would take that page's relayout from
1 446 ms to about 42 ms — **34×** — while a perfect layout dirty bit alone would
take it to 1 404 ms, or **1.03×**.

## What this does not measure

The mutation is applied and then one layout is requested, which is the shape of a
script that changes one thing and reads geometry back. It does not cover a burst
of mutations coalesced into one layout (where the rebuild is amortised across
them and the layout share rises), nor a mutation that changes nothing observable
(where a correct dirty-bit scheme should cost zero and today costs a full
rebuild). Both are worth adding when item #14 is picked up; the second is the
cheapest possible demonstration of the item's value and is one document away.

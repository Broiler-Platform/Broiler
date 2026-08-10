# Layout passes per render

Produced by `--layout-passes` in `tests/render-stages/Broiler.Render.Stage.Benchmarks`.
The precondition for the `Broiler.Layout` roadmap's **step 1** — "stop laying out
the whole tree twice" — measured before the step is written.

> The `passes` column depends on `LayoutPassCounter.Record` being called from
> `HtmlContainerInt.PerformLayout`, which is `patches/0133` against `Broiler.HTML`.
> Against a submodule tree without it the column reads zero and the run says so
> rather than reporting zeros as measurements.

- Viewport: 1280x1024
- Iterations: 11 measured, 4 warm-up; times are medians, counts are structural
- Runtime: 10.0.10, 4 logical cores

`calls` counts `PerformLayout` invocations; `passes` counts full-tree
`Root.PerformLayout` walks. They differ when the engine's shrink-to-fit branch fires
(`MaxSize.Width <= 0.1`).

| page | shape | calls | passes | layout ms | render ms |
|---|---|---:|---:|---:|---:|
| text | viewport | 1 | 1 | 42.41 | 143.46 |
| text | autosize | 1 | 2 | 53.36 | 154.48 |
| text | measure | 2 | 3 | 139.84 | 226.19 |
| rules | viewport | 1 | 1 | 49.47 | 1641.91 |
| rules | autosize | 1 | 2 | 60.44 | 1524.07 |
| rules | measure | 1 | 2 | 54.38 | 1595.02 |
| boxes | viewport | 1 | 1 | 41.13 | 304.59 |
| boxes | autosize | 1 | 2 | 52.81 | 308.31 |
| boxes | measure | 1 | 2 | 53.04 | 322.86 |
| paint | viewport | 1 | 1 | 38.09 | 248.67 |
| paint | autosize | 1 | 2 | 51.51 | 261.37 |
| paint | measure | 1 | 2 | 51.15 | 255.93 |
| mixed | viewport | 1 | 1 | 41.23 | 173.53 |
| mixed | autosize | 1 | 2 | 33.06 | 146.63 |
| mixed | measure | 2 | 3 | 61.02 | 173.79 |

## The three shapes, and why the profile needs three

The branch is on `MaxSize.Width`, which is a property of the **caller**, not of the
page. A profile that renders every page the one way the stage profile does could
not see the branch at all, in either direction. The three shapes are the three that
exist in this repository:

- **viewport** — `MaxSize = (1280, 1024)`. What `HtmlRender.RenderToImage` does, and
  through it the WPT runner, the CLI capture, `Broiler.Browser.Core`, and every
  benchmark in this project.
- **autosize** — `MaxSize = (0, 0)`. What `HtmlRendererUtils.Layout` sets when a host
  asks the document to size itself: the embedding-control path.
- **measure** — `HtmlRendererUtils.MeasureHtmlByRestrictions`: lay out unrestricted to
  learn the width, then lay out again against it. Up to three `PerformLayout` calls,
  and the first is itself on the shrink-to-fit path, so passes multiply rather than add.

## What it says

**1. The branch is unreachable from every path this repository measures.** Every
`viewport` row is one call and one pass. Roadmap step 1 is stated as the highest-value
sequential item in Phase 4 — "worth more than steps 3 and 4 combined" — and on the WPT
runner, the CLI capture, the browser and this benchmark project it is worth exactly
nothing, because none of them ever sets `MaxSize.Width` to zero. It fires for embedding
hosts that ask a document to size itself, which is a real case and not one any number
in this document has ever been measured on.

**2. Where it does fire, the second pass is not a doubling.** `autosize` performs twice
the passes of `viewport` and costs **0.80–1.35×** the layout time (`text` 42.41 → 53.36,
`boxes` 41.13 → 52.81, `paint` 38.09 → 51.51, `rules` 49.47 → 60.44, `mixed` 41.23 → 33.06
— the last below 1.0, i.e. inside the spread). The reason is structural rather than
noise: the first of the two passes runs at width 99999, where almost nothing wraps and
the line breaker — the dominant cost in a layout pass on a text-heavy page — barely runs.
The pass step 1 proposes to cache away is **the cheaper of the two**.

**3. `measure` is the shape that actually multiplies**, and only on the two pages whose
unrestricted width exceeds the viewport: `text` and `mixed` reach 3 passes across 2 calls
(`text` 139.84 ms against 42.41 for one pass — 3.3×). The other three pages fit in 1280px
unrestricted, so the helper's second call never happens. If step 1 is ever built, this is
the shape to aim it at, not the single-call `autosize` one the roadmap step names.

## Method note: this harness nearly produced a false positive

Measured shape by shape — all of `viewport`'s iterations, then all of `autosize`'s —
`text | viewport` read **136.80 ms** in one run and **83.83 ms** in the next with no code
change between them, and the `viewport → autosize` ratio inverted between the two. Both
readings also disagreed with [`stage-profile.md`](stage-profile.md), which measures the
same configuration at ~51 ms.

The cause is the one Phase 2 §7 of the multithreading roadmap already documents for this
host: throughput drifts by tens of percent over tens of seconds, so a configuration
measured in its own contiguous block sits in its own slice of the drift, and its median
is not comparable with another block's. The shapes are **interleaved** here — iteration
by iteration, not shape by shape — which is what `DecodeScaling` does and for the same
reason. Interleaved, two consecutive runs agree, and `text | viewport` (40.21 / 50.93 /
42.41 ms over three runs) agrees with the stage profile.

That is the second time in this document that a measurement's first form was the
measurement's own bug, and both times the tell was the same: a number that moved
when nothing did.

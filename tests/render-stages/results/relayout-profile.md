# Relayout profile — the precondition for multithreading item #14, and its first slice

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
after 1 warm-up. The `rebuilt?` column needs `patches/0131` applied to the
`Broiler.HTML` tree; without it every row reads `n/a` and the run says so.

## What "relayout" is in this engine

`HtmlContainerInt` holds a bound `DomDocument` and a copy of its `Version`.
`EnsureBoundDocumentCurrent` compares them at the top of every `PerformLayout`
and, when they differ, rebuilds: `BuildBoundDocument` **disposes the render tree
and regenerates it** — box tree and full cascade — before the layout pass runs.
So a relayout is not a layout pass: it is a rebuild followed by a full-tree
layout.

Because `SetDocument` builds the tree, the **1st layout** column below is the
layout pass alone; **relayout** is rebuild + layout.

## The result

Two runs on one host, back to back: the pinned `Broiler.HTML` (**baseline**) and
the same tree with `patches/0131` applied (**ledger**). Seven mutations per page;
the last three were added when item #14 was picked up.

| Page | Mutation | Baseline relayout | Ledger relayout | Ratio | Rebuilt? |
|---|---|---:|---:|---:|---|
| text | class toggle | 241.94 | 256.53 | 0.94 | yes |
| text | inline style write | 104.33 | 126.86 | 0.82 | yes |
| text | text write | 89.74 | 88.71 | 1.01 | yes |
| text | inserted subtree | 81.97 | 82.09 | 1.00 | yes |
| text | **detached build** | 117.18 | **11.72** | **10.0×** | **ELIDED** |
| text | burst (20 writes) | 95.29 | 109.61 | 0.87 | yes |
| text | unstyled attribute | 86.01 | 101.60 | 0.85 | yes |
| rules | class toggle | 1 042.69 | 1 071.77 | 0.97 | yes |
| rules | inline style write | 1 088.94 | 1 024.44 | 1.06 | yes |
| rules | text write | 1 014.65 | 1 120.15 | 0.91 | yes |
| rules | inserted subtree | 1 017.58 | 1 034.53 | 0.98 | yes |
| rules | **detached build** | 1 032.72 | **11.50** | **89.8×** | **ELIDED** |
| rules | burst (20 writes) | 1 074.82 | 1 037.45 | 1.04 | yes |
| rules | unstyled attribute | 1 072.06 | 997.76 | 1.07 | yes |
| boxes | class toggle | 261.66 | 296.13 | 0.88 | yes |
| boxes | inline style write | 257.86 | 241.80 | 1.07 | yes |
| boxes | text write | 250.80 | 261.61 | 0.96 | yes |
| boxes | inserted subtree | 278.92 | 257.06 | 1.09 | yes |
| boxes | **detached build** | 264.18 | **10.25** | **25.8×** | **ELIDED** |
| boxes | burst (20 writes) | 250.63 | 255.97 | 0.98 | yes |
| boxes | unstyled attribute | 269.50 | 249.14 | 1.08 | yes |
| paint | class toggle | 223.48 | 194.63 | 1.15 | yes |
| paint | inline style write | 219.67 | 199.75 | 1.10 | yes |
| paint | text write | 203.83 | 170.44 | 1.20 | yes |
| paint | inserted subtree | 206.66 | 195.51 | 1.06 | yes |
| paint | **detached build** | 224.76 | **9.94** | **22.6×** | **ELIDED** |
| paint | burst (20 writes) | 216.10 | 185.90 | 1.16 | yes |
| paint | unstyled attribute | 199.24 | 197.35 | 1.01 | yes |
| mixed | class toggle | 111.40 | 107.33 | 1.04 | yes |
| mixed | inline style write | 101.37 | 96.51 | 1.05 | yes |
| mixed | text write | 110.21 | 98.20 | 1.12 | yes |
| mixed | inserted subtree | 124.29 | 98.02 | 1.27 | yes |
| mixed | **detached build** | 116.87 | **7.16** | **16.3×** | **ELIDED** |
| mixed | burst (20 writes) | 98.13 | 98.14 | 1.00 | yes |
| mixed | unstyled attribute | 97.67 | 91.03 | 1.07 | yes |

**The thirty non-elided rows span 0.82–1.27, with no page and no mutation
systematically on one side.** That is this host's run-to-run spread, which is the
claim those rows are here to support: the ledger changes what is skipped, not what
is done. The five elided rows report a rebuild of exactly 0.00 ms in the sub-stage
table, so the saving is the absence of the stage and not a faster one.

**Nothing else moved.** `Broiler.Cli.Tests` was run in full both ways on the same
host — 2 931 tests, 82 failures each way, and the two failure sets are **identical
name for name** (77 unique names; none added, none fixed). Those 82 are
pre-existing on the pinned pointer and environmental in character: the
PDF-converter tests `CLAUDE.md` warns about, the Skia and `WebClient` architecture
guards, the Acid image comparisons. `Broiler.Layout.Tests` is 308/308.

Rebuild sub-stages remain dominated by the cascade on every page that rebuilds —
`cascade (resolve)` plus `cascade (project)` are the overwhelming majority of the
`rules` page's ~1 000 ms rebuild, against a few ms of HTML parse and CSS parse.

## Findings

**1. A relayout is 60–97% rebuild. The layout pass barely moves.** Item #14 as
written — "dirty bits + relayout roots" on `CssBox.PerformLayout` — bounds the
*layout* column, which is between 3% and 39% of what a relayout costs and 2.9% on
the rule-heavy page. **The item is aimed at the smaller half.** The invalidation
that pays is on the box tree and the cascade — the work `BuildBoundDocument`
throws away and redoes — not on the layout pass beneath it. *(Unchanged from this
file's first publication; it is what re-aimed the item.)*

**2. The engine could not tell the four original mutations apart — but the DOM
could, and always could.** All four bump `Version` by one and cost the same. The
conclusion drawn from that here was that item #14 "has to start by giving the DOM
a way to say *what* changed, which is a `Broiler.DOM` change". **That was wrong,
and cheaply checkable.** `DomDocument.Mutated` publishes a typed
`DomMutationRecord` — type, target, added and removed nodes, attribute name, old
and new value — and has since before the item was written; `DomRange` and
`DomNodeIterator` subscribe to it. What was missing was a *consumer*. The whole of
`EnsureBoundDocumentCurrent` was a version compare, standing next to a feed of
exactly the records it needed.

**3. The first thing a consumer can prove is connectivity, and it is worth 10–90×
on the page it applies to.** `RenderTreeInvalidation` classifies each record by
whether its target hangs off the bound document. Nothing else can contribute a
box, so nothing else can change what the tree shows. `ChildList` records name the
*parent*, which is what makes the test sound in both directions: nodes added to a
detached parent are themselves detached, and a node moved *out* of the page is
reported against the still-connected parent it left. The `detached build` row —
twenty-four nodes assembled off-document and never inserted, the shape of every
`DocumentFragment` population and every build-then-insert — goes from a full
rebuild to none.

**4. The burst does not amortise, and that is a correction to what this file
predicted.** The uncovered-cases note below used to say a coalesced burst would be
a case "where the rebuild is amortised across them and the layout share rises".
The layout share is **2.4% for one class toggle and 2.5% for twenty writes** on
`rules`, and flat or slightly *lower* on `boxes` (11.3% → 10.4%), `paint` (10.9% →
9.6%) and `mixed` (19.3% → 18.1%). Only `text` moves, from 55% to 70%, and that is
the one page whose figures carry the first-measured-page overhead visible in its
1st-layout column. There is nothing to amortise: the rebuild is a *whole-document* re-cascade for a single attribute
write, so twenty of them cost exactly what one does. The burst case is worth
keeping — it is what makes that statement a measurement — but it is a null result,
and the prediction attached to it came from assuming a per-mutation cost the engine
does not have.

**5. What is left is bigger than what was taken, and the `unstyled attribute` row
sizes it.** One `data-*` write that no corpus selector can reach still costs
997.76 ms on `rules`. Eliding *that* is worth roughly what the connectivity rule
was worth, and it needs something the connectivity rule does not: an answer to
whether any rule's subject could match differently, which is invalidation sets over
the cascade's rule index. That is the rest of item #14, and this row is the number
it should be measured against.

## What this measures, and what it does not

The mutation is applied and then one layout is requested, which is the shape of a
script that changes one thing and reads geometry back. The two cases this file
originally listed as uncovered — the coalesced burst and the mutation that changes
nothing observable — are covered now, with one correction worth recording: at the
*value* level there is nothing to elide, because `Broiler.DOM` returns before
publishing when an attribute or text write does not change the value, so the
version never moves. The honest form of "changes nothing observable" is the
detached case, which is why that is the one measured.

Still not covered: a mutation to a connected element that produces no box (a
`<meta>`, a `<title>`), and a relayout at a changed viewport rather than a changed
document.

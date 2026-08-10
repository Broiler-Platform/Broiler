# Relayout profile — the precondition for multithreading item #14, and both of its slices

What a *second* layout costs after the document is mutated the way script mutates
it. Roadmap [§7](../../docs/architecture/multithreading.md#7-item-14-has-no-measurement-it-can-be-started-against-and-building-it-first-would-be-building-it-blind)
named this harness as the first thing Phase 3's remainder should build, ahead of
any dirty bit, because item #14 bounds a stage nothing in P0-a performs.

Reproduce with:

```sh
dotnet run --project tests/render-stages/Broiler.Render.Stage.Benchmarks -c Release \
  -- --relayout-profile [--iterations N] [--warmup N]

# the exit gate: same pages, same mutations, rendered with the elision on and off
dotnet run --project tests/render-stages/Broiler.Render.Stage.Benchmarks -c Release \
  -- --relayout-parity
```

Host: 4 cores, .NET 10, Release, viewport 1280×1024, medians of 5 iterations
after 1 warm-up. The `rebuilt?` column needs `patches/0132` applied to the
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

Two runs on one host, back to back: the pinned `Broiler.HTML` with neither half of
item #14 applied (**baseline**) and the same tree with both (**invalidation set**).
Nine mutations per page — the last two are the controls that arrived with the
second half.

| Page | Mutation | Baseline relayout | Invalidation set | Ratio | Rebuilt? |
|---|---|---:|---:|---:|---|
| text | **class toggle** | 287.47 | **118.08** | **2.4×** | **ELIDED** |
| text | inline style write | 140.31 | 191.57 | 0.73 | yes |
| text | text write | 155.60 | 178.52 | 0.87 | yes |
| text | inserted subtree | 171.05 | 169.26 | 1.01 | yes |
| text | detached build | 84.04 | 85.97 | 0.98 | **ELIDED** |
| text | **burst (20 writes)** | 157.05 | **109.12** | **1.44** | **ELIDED** |
| text | **unstyled attribute** | 179.57 | **85.91** | **2.1×** | **ELIDED** |
| text | **styled attribute** | 175.89 | **93.48** | **1.9×** | **ELIDED** |
| text | **styled class** | 173.66 | **98.88** | **1.8×** | **ELIDED** |
| rules | **class toggle** | 1541.53 | **42.79** | **36.0×** | **ELIDED** |
| rules | inline style write | 1526.76 | 1597.90 | 0.96 | yes |
| rules | text write | 1565.66 | 1574.84 | 0.99 | yes |
| rules | inserted subtree | 1360.72 | 1433.89 | 0.95 | yes |
| rules | detached build | 34.97 | 48.78 | 0.72 | **ELIDED** |
| rules | burst (20 writes) | 1552.48 | 1445.05 | 1.07 | yes |
| rules | **unstyled attribute** | 1476.73 | **42.95** | **34.4×** | **ELIDED** |
| rules | styled attribute | 1501.45 | 1455.79 | 1.03 | yes |
| rules | styled class | 1350.27 | 1544.20 | 0.87 | yes |
| boxes | class toggle | 282.68 | 312.01 | 0.91 | yes |
| boxes | inline style write | 302.92 | 312.98 | 0.97 | yes |
| boxes | text write | 269.77 | 328.19 | 0.82 | yes |
| boxes | inserted subtree | 268.34 | 330.06 | 0.81 | yes |
| boxes | detached build | 35.00 | 39.84 | 0.88 | **ELIDED** |
| boxes | burst (20 writes) | 262.00 | 331.61 | 0.79 | yes |
| boxes | **unstyled attribute** | 275.91 | **29.10** | **9.5×** | **ELIDED** |
| boxes | **styled attribute** | 270.12 | **39.53** | **6.8×** | **ELIDED** |
| boxes | styled class | 330.50 | 329.58 | 1.00 | yes |
| paint | **class toggle** | 247.57 | **20.21** | **12.2×** | **ELIDED** |
| paint | inline style write | 256.15 | 238.42 | 1.07 | yes |
| paint | text write | 190.18 | 202.71 | 0.94 | yes |
| paint | inserted subtree | 259.91 | 230.27 | 1.13 | yes |
| paint | detached build | 17.26 | 16.79 | 1.03 | **ELIDED** |
| paint | burst (20 writes) | 238.97 | 235.96 | 1.01 | yes |
| paint | **unstyled attribute** | 245.88 | **17.32** | **14.2×** | **ELIDED** |
| paint | **styled attribute** | 245.44 | **16.88** | **14.5×** | **ELIDED** |
| paint | styled class | 276.98 | 229.38 | 1.21 | yes |
| mixed | **class toggle** | 180.10 | **19.16** | **9.4×** | **ELIDED** |
| mixed | inline style write | 165.86 | 155.88 | 1.06 | yes |
| mixed | text write | 161.77 | 142.11 | 1.14 | yes |
| mixed | inserted subtree | 159.24 | 150.71 | 1.06 | yes |
| mixed | detached build | 11.92 | 29.13 | 0.41 | **ELIDED** |
| mixed | **burst (20 writes)** | 162.82 | **11.47** | **14.2×** | **ELIDED** |
| mixed | **unstyled attribute** | 179.49 | **12.98** | **13.8×** | **ELIDED** |
| mixed | **styled attribute** | 162.96 | **30.56** | **5.3×** | **ELIDED** |
| mixed | **styled class** | 159.45 | **28.56** | **5.6×** | **ELIDED** |

**Reading the table.** A **bold** row is one the ledger answered without rebuilding.
`detached build` was already elided by the first slice, so its ratio here is
baseline noise on a row that costs 12–86 ms either way, not a change. The rows that
still rebuild span **0.79–1.21**, which is this host's run-to-run spread with no page
and no mutation systematically on one side.

**The two control rows are the point of the table, not its footnotes.** `rules` is
the only corpus page whose sheet mentions `data-k`, and it is the only page where
`styled attribute` still rebuilds (1.03 — unchanged). `boxes` is the page whose
deepest element carries a class its sheet styles, and it is the only page where
`class toggle` still rebuilds (0.91 — unchanged); `styled class` rebuilds there and
on `rules` too. Same mutation, opposite decision, decided by the document: that is
the set being a function of the stylesheets rather than of the attribute's name.

## The exit gate

```
45 pairs, 22 of them with the rebuild skipped.
PASS: every elided relayout renders the page a rebuild would have.
```

`--relayout-parity` renders every page after every mutation twice — elision on, then
off — and compares the images byte for byte. It is the only check that can catch the
failure this item risks, which is not a slow page but a **stale** one: a unit test
over the classifier agrees with the implementation's own reasoning and cannot say
whether that reasoning is right about the engine. It also fails a run in which
nothing was elided, because a green run that compared no elisions is how this would
quietly stop being a gate.

**It failed the first time it was run, and the bug was not in the classification.**
Three `boxes` rows differed — including `detached build`, which the *first* slice
already elided and had shipped. The no-mutation control explains it: on that page,
laying the same box tree out a second time produced a different image from laying it
out once, with or without any mutation at all. Reduced, the whole of it is
`<div style="margin-top:2px">` directly inside `<body>`: `CollapsedMarginTop` records
what the previous margin collapse decided, a first in-flow child's top margin
propagates by shifting its parent down only when the child's margin exceeds what the
parent has already absorbed, and read from the *previous pass* that comparison fails.
The document lays out two pixels shorter the second time.

Nothing in this repository had ever laid the same box tree out twice — every relayout
disposed the tree and rebuilt it — so a pass-dependent result could sit in the margin
code indefinitely. The moment a rebuild can be skipped, "lay out the same tree again"
has to mean what "build it and lay it out" meant. The fix is a reset at the top of
each box's own layout (`CssBox.ResetCollapsedMarginState`), kept by
`LayoutIdempotenceTests` — including a case asserting the margin is still *applied*,
because the tempting fix is to make both passes agree on the short answer.

`Broiler.Layout.Tests` is 353/353, and the render-bearing half of `Broiler.Cli.Tests`
— Acid, WPT, `GoogleSearchPolyfill`, form-control and CSSOM, 928 tests — was run in
full both ways on one host with **failure sets identical name for name** (41 failures
each way, 40 unique names, none added and none fixed; all pre-existing and
environmental in character). The full assembly is not the comparison, because it
aborts partway on this host for memory reasons in both directions.

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
a case "where the rebuild is amortised across them and the layout share rises". The
layout share was **2.4% for one class toggle and 2.5% for twenty writes** on `rules`,
and flat or slightly *lower* on `boxes`, `paint` and `mixed`. There is nothing to
amortise: the rebuild is a *whole-document* re-cascade for a single attribute write,
so twenty of them cost exactly what one does. The burst case is worth keeping — it is
what makes that statement a measurement — but it is a null result, and the prediction
attached to it came from assuming a per-mutation cost the engine does not have.
*(The burst now elides on the pages whose sheets do not name its `burst-N` classes,
which is the second half at work rather than any amortisation.)*

**5. The second half is worth what this file said it would be, and on more rows
than the one that sized it.** `CascadeInvalidationSet` asks the sheets the tree was
cascaded from whether any rule could match differently — and, separately, whether box
construction reads the attribute for reasons of its own. The `unstyled attribute` row
that finding 5 previously sized at ~1 000 ms is **1 476.7 → 43.0 ms (34.4×)** on
`rules`, 14.2× on `paint`, 13.8× on `mixed`, 9.5× on `boxes` and 2.1× on `text` —
whose remainder is the layout pass, which is most of what a relayout costs on a page
of prose. The class toggle joins it wherever the sheet does not name the token:
**36.0× on `rules`**, 12.2× on `paint`, 9.4× on `mixed`, and unchanged on `boxes`,
where the token it replaces *is* styled.

**6. What is left is a scoped rebuild, not a skipped one.** Everything still
rebuilding is a connected mutation the engine genuinely has to react to: an inline
style write, a text change, an inserted subtree, an attribute a rule really does
match on. Nothing above narrows *what* is rebuilt — the box tree is regenerated whole
and the document is re-cascaded whole for a one-element change. That is the next
increment, and it is a different kind of work from either half of this one: it needs
the rebuild to have a unit smaller than the document, which neither the ledger nor
the set provides.

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
document. Both still rebuild.

One more thing the table cannot show: an elided rebuild does not re-run the host
callbacks a rebuild runs — the image-load event, in particular, which is handed every
attribute of the element. A host that repaints on `data-*` writes it sets itself is
relying on the rebuild rather than on a callback, and `BROILER_RENDER_TREE_ELISION=0`
is the switch that tells it apart from a bug here.

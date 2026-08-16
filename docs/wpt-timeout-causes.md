# WPT timeouts: what causes them

The WPT runner abandons a test at a per-test budget (30 s by default; see
`--timeout` / `BROILER_WPT_TIMEOUT_SECONDS`) and reports it as a `Timeout`. A
timeout is worth more attention than its count suggests: it is the one failure a
pixel comparison cannot see, because the run is aborted before anything renders,
and it costs the shard its full budget in wall clock as well.

This page records what the timeouts in the 2026-08-16 run (65 of them) actually
turned out to be, because almost none of them was what its directory name
suggested.

## The pattern: a per-iteration full-document pass

Six of the seven causes below are the same shape. Something walks the document —
elements, insertions, geometry reads, animation frames — and each step
redoes work whose cost is the *whole document*: a stylesheet discovery walk, a
style invalidation, a layout, a clone-and-cascade. Nothing is wrong with any
single step. The product is what misses the budget.

So when a timeout has no obvious infinite loop, the question to ask is not "what
is stuck" but **"what is being redone once per element?"** Measure the exponent
before theorising: render synthetic documents of N, 2N and 4N elements and
difference the times — that cancels the fixed startup cost and gives a clean
estimate. An exponent near 2 says the loop is the bug, not its contents.

Three of these were already fixed once elsewhere under the same reasoning —
WPT #1113 (the check-layout evaluator) and #1115 (the live geometry getters)
both wrapped a tree walk in a single `DomBridge.WithLayoutGeometryCache` pass.
Their regression guards are `MulticolCheckLayoutTimeoutTests` and
`LiveGeometryQueryTimeoutTests`.

## Fixed

| tests | cluster | what it actually was |
| --- | --- | --- |
| 5 | `html/webappapis/scripting/processing-model-2` | `for(;)` parsed as `for(;;)` — a SyntaxError became an infinite loop. `Broiler.JS`, shipped as a patch. |
| 28 | `encoding/legacy-mb-*` | Not encoding. Stylesheet discovery re-walked the whole tree once per element resolved. |
| 3 | `conformance-checkers/…/table/integrity` | The tail of the same discovery quadratic. |
| 1 | `conformance-checkers/…/img/src-isvalid` | Synchronous image fetch on the layout thread with no `HttpClient` timeout — .NET's 100 s default, per unroutable host, per layout pass. |
| 1 | `css/css-break/…/special-elements-crash` | Not fragmentation. `elementFromPoint` built and tore down a full geometry snapshot per element visited. |
| 1 | `css/css-fonts/variations/font-opentype-collections` | Not the TTC parser. The runner's synchronous `requestAnimationFrame` stub had no re-entrancy cap, so a non-converging rAF loop recursed to CLR stack exhaustion. |

Guards: `StyleSheetDiscoveryCacheTests`, `HitTestAndRafBudgetTimeoutTests`, and
the `for`-head theories inside the `Broiler.JS` patch.

## Not a defect

`css/filter-effects/crashtests/filter-primitive-crash.html` renders in ~7 s on an
idle box and never timed out under measurement. `feMorphology` is not implemented
at all, so the 10 000 primitives it builds are a no-op; the cost is linear in
element count. Its CI timeout is contention on a shard, not an engine bug —
worth remembering before chasing it again.

**Contention distorts these numbers badly.** On a saturated 4-CPU box two
`shift_jis` tests measured 60 s that measure 8 s idle. Always re-measure a
borderline result on a quiet machine before concluding anything.

## Open, with the cause established

### `css/css-overflow` — 20 timeouts

A genuine hang, and the largest remaining cluster. Each
`overflow-alignment-*.html` is one ~1400-div table whose load handler writes
`scrollTop`/`scrollLeft` on 84 elements — 168 scroll-offset writes. Every write
goes `ElementGeometryBinding.SetScrollTop` →
`DomBridge.SetElementScrollOffsetsWithBehavior` and costs **four** full document
clones plus layouts, with an unbounded per-iteration retention on top that makes
the whole thing superlinear.

Hoisting the redundant passes is necessary but measured as **not sufficient**:
the fixed overhead is ~6.4 s and each snapshot ~0.67 s cold, so even a 2× cut
leaves ~63 s against a 30 s budget. This one needs the snapshot to survive
*across* queries — invalidated on actual DOM/style mutation rather than torn down
in every `WithLayoutGeometryCache` `finally` — which is a larger change than the
per-call-site wraps above and wants its own issue.

### `css/css-variables/url-syntax-crash.html` — 1 timeout

Renders in ~33 s against the 30 s budget. Custom properties and `@property` are
not the cost: a bare 10 000-span document with no CSS at all is just as slow, and
every sibling `css-variables` crash test finishes in 2–3 s. The cost is the
10 000 `appendChild` calls — each scripted insertion funnels through
`DomBridge.InsertNodeAt` and triggers a whole-tree style invalidation, so
building a tree from script is quadratic in its size. The `-crash` tests take a
batched path that softens it; a non-crash test of the same shape would not.

### `css/WOFF2` — 1 timeout

Not reproduced. WOFF2 is not implemented (`WoffDecoder` handles WOFF 1.0 only)
and no file in that directory is accepted by the sfnt sniff, so no font parsing
happens. All 599 files are `.xht` with no script and render in 2–3 s, except
`testcaseindex.xht` (269 KB, ~4770 elements) at ~9 s — the only plausible
candidate under a parallel shard, but unconfirmed.

## Reproducing

```sh
dotnet run --project src/Broiler.Wpt -- --wpt-dir tests/wpt/checkout \
  --render <FILE>
```

Raise `--timeout` to separate a hang from a document that merely wants more than
the budget: a hang still does not finish with the limit lifted. `dotnet-stack
report -p <pid>` against the live process is what identified most of the causes
above — a repeated stack over several samples names the loop directly, which is
faster and far more reliable than reasoning from the source.

# Multithreading analysis and roadmap

Where concurrency can make Broiler faster, where it cannot, and the order the
work has to happen in. Scope is every component in the aggregate repository plus
the tooling.

## Status

**Phase 4 is open, and it opened by re-measuring the rationale for deprioritising
it.** That rationale had gone stale: this document deprioritised parallel layout on
the grounds that layout is 0.6–6.5% of a render, and on today's code it is
**3.3–20.1%** — not because layout got slower (it costs the same 28–51 ms it always
did) but because Phases 2 and 3 removed cost from everything around it
([§1](#1-layouts-share-tripled-without-layout-changing-and-the-number-this-row-quoted-was-measured-before-two-phases-of-work)).
**Every share in this document decays as the items above it land**, and a phase gated
on one has to re-read it rather than cite it. It does not change the ranking — 1.5–2.5×
of a 20% stage is ~10% end to end for 20–30 days at High risk, against a
`parse+cascade` still at 23–81% — but the phase is now last for a reason that is
currently true. The phase's other result is a retirement: the `Broiler.Layout`
roadmap's **step 1**, "stop laying out the whole tree twice", ranked above both
parallel steps, is **unreachable from every path this repository measures**, and where
it does fire two passes cost 0.80–1.35× one, because the unrestricted first pass barely
runs the line breaker — the traversal it proposed to remove is the cheaper of the two
([§2](#2-the-double-layout-is-unreachable-from-every-path-this-repository-measures-and-where-it-fires-it-is-not-a-doubling)).
That makes **six items running whose row named the wrong thing** — five blockers, now
one beneficiary. `patches/0133`. Published:
[`tests/render-stages/results/layout-passes.md`](../../tests/render-stages/results/layout-passes.md)

| Item | State | Evidence |
|---|---|---|
| #13 — parallel intrinsic sizing and independent subtrees | **Not started; re-argued** | Its stage is 3.3–20.1% of a render, up from the 0.6–6.5% the phase row quoted, entirely by Amdahl ([§1](#1-layouts-share-tripled-without-layout-changing-and-the-number-this-row-quoted-was-measured-before-two-phases-of-work)) |
| Layout roadmap step 1 — stop laying out the tree twice | **Retired as written** | `--layout-passes` (`patches/0133`): every fixed-viewport row is 1 call / 1 pass; auto-size is 2 passes at **0.80–1.35×** one pass, not 2× ([§2](#2-the-double-layout-is-unreachable-from-every-path-this-repository-measures-and-where-it-fires-it-is-not-a-doubling)) |
| #18 — Web Workers | **Built (first slice); gate passed** | Contexts isolated and genuinely parallel — **2.66×/3.22× at 4 threads** ([§10](#10-item-18s-gate-passes-on-both-halves--and-one-of-them-is-the-half-nobody-was-asking-about)). `MessageChannel` and structured clone were **already built**, with cross-realm cloning verified ([§11](#11-messagechannel-was-already-built-the-unverified-word-was-cross-context)). **`Worker` now exists**: own thread, own `JSContext`, messages cloned twice — sender-side into an unreachable graph, receiver-side into its realm — and replies queued onto the page's loop, so item #15 holds. 12 tests; WPT classification identical name for name ([§12](#12-worker-is-built-and-the-design-content-was-where-the-clone-happens)). `importScripts`, module/shared/nested workers, worker `requestAnimationFrame` and transferables remain |
| Phases 2–3 measured on WPT | **Null result** | `ca53d44` vs HEAD, both sequential per render, 2 213 reftests over two suites: **1.017× and 1.013×**, inside the host's 5–7% drift, classification identical. WPT pages are 1 018 bytes at the median against a 20–212 KB corpus, so the surviving sequential wins have nothing to act on ([§4](#4-phases-2-and-3-are-worth-nothing-on-a-wpt-run-and-the-pages-are-why)) |
| Per-render fixed cost | **Measured; §4's conclusion refuted** | Engine: **3.48 ms** empty, 15–19 ms at WPT median, 77% of the empty render being bitmap alloc + erase. Runner: the render is **1.6–2.0%** of a test — **76–79% is `ExecuteScriptsWithDom`** and **16–21% `PixelDiffRunner.Compare`**. Neither is a rendering problem, and neither is in this document ([§5](#5-the-engines-per-render-fixed-cost-is-35-ms-and-4s-closing-sentence-was-wrong)) |
| Pixel comparison | **Fixed** | **284.61 → 4.50 ms (62×)** on the match path; the suite 368.5 → 297.3 s (1.24×). 92% of it was a PNG round trip measured to be an identity — not the per-pixel loop §5 pointed at, which was ~22 ms of 284. Classification identical name for name. `patches/0134` ([§6](#6-the-pixel-comparison-is-fixed-and-the-part-that-read-worst-was-not-the-part-that-cost)) |
| `ExecuteScriptsWithDom` | **Profiled** | **`DomBridge.RegisterDocument` was 50.6–53.6% of a whole WPT run** at ~440 ms per document, twice per reftest — and *fixed* cost (436 vs 446 ms/call across two subsets). The DOM build next to it is **0.61 ms**. Script eval is 20–25%; the render 1.1–1.7% ([§7](#7-half-a-wpt-run-is-publishing-the-dom-api-and-the-dom-build-is-06-ms)) |
| `RegisterDocument` | **Fixed** | **422.10 → 13.74 ms/call (30.7×)**; the suite 368.5 → 195.7 s (**1.88×**). The bridge was recompiling its own constant JavaScript for every document; the process-shared code cache is installed for that call only, so no page-controlled source can enter it. Failing set identical to pristine name for name ([§8](#8-registerdocument-was-recompiling-the-bridges-own-javascript-per-document--422-ms--137-ms)) |
| Script eval | **Fixed, in part** | Injected stubs **64.49 → 1.24 ms (52×)** and the window→global sync **11.98 → 0.12 ms (100×)** — the same constant-recompiled-per-document fault in two more places. **Page scripts deliberately untouched** (41.99 → 43.60): sharing page-derived compiled code across documents is the one thing a conformance runner must not do. Suite **368.5 → 108.6 s cumulative, 3.39×** ([§9](#9-the-same-fault-in-two-more-places-and-the-boundary-that-stopped-it-spreading)) |

**Phase 3 is complete: #12, #21, #16 and #14.** The relayout harness
[§7](#7-item-14-has-no-measurement-it-can-be-started-against-and-building-it-first-would-be-building-it-blind)
said had to exist before any dirty bit was built first, and it changed the item:
**a relayout is 60–97% box-tree rebuild and re-cascade**, so dirty bits on the
layout pass bound the smaller half
([§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half)).
Aimed at the rebuild instead, the item's first slice **stops rebuilding for
mutations that cannot reach the render tree** — `rules` 1 032.7 → 11.5 ms on the
offscreen-build case — and it needed no `Broiler.DOM` change at all, because the
typed mutation feed the item was recorded as blocked on has been there all along
([§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise)).
That makes **five items running whose stated blocker was not the operative one**.
Its second half asks the *stylesheets* instead of the tree — an attribute write no
selector filters on, no `attr()` reads and no box-construction path consults is
answered without a rebuild — which takes the row §11 left open from **1 476.7 to
43.0 ms (34.4×)** on the rule-heavy page. **Its exit gate failed on first run, and
what it found was older than the item**: laying the same box tree out twice was not
idempotent, so the two-pixel drift it produced had been live in the first slice all
along ([§12](#12-item-14s-second-half-the-sheets-already-knew-and-the-gate-caught-a-bug-nothing-else-could)).
The phase opened by answering the question Phase 2
§9 said the document owed before #12 could begin, and the answer reframed the
item: **`parse+cascade` is 81.3–98.2% cascade on every corpus page**
([§1](#1-parsecascade-is-a-cascade-stage-the-name-overstates-the-parse)). Building
#12 then contradicted both blockers its own row named — the lock was not the
bottleneck
([§3](#3-the-_sync-lock-was-not-the-bottleneck-the-item-names-and-the-cascades-own-cost-is))
and the box walk is not the parallel unit
([§4](#4-the-parallel-unit-is-not-the-box-walk--this-is-a-prefetchconsume-split-the-fourth-in-this-document)).
**Then #16 contradicted the blocker this document had written for it two sections
later**, which is the same failure mode three items running: the store §6 said had
to be built already existed
([§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache)). Its
measurement also went one step past the number — the obvious cause of a 1.62×
ceiling on four cores was tested and **is not the cause**
([§9](#9-the-compile-stages-ceiling-is-not-the-thing-that-looks-like-it)).

| Item | State | Evidence |
|---|---|---|
| #12 — cache sharding + parallel style recalc | **Done** | `CssStyleRecalc` (budget `BROILER_STYLE_THREADS`) resolves every element's cascade ahead of the unchanged box walk; the engine's memo caches are `ConcurrentDictionary` with the generation guard intact, plus a fourth cache that gives the warm pass a store. **Cascade stage 1.16–2.01× at 4 threads, 1.08–1.96× end to end**, pixel-identical at 1/2/4 across the corpus (`--style-scaling`) and at 1/2/3/4/8 over 22 `ParallelStyleRecalcTests` cases; `CssStyleEngineConcurrencyTests` extended from "the caches survive" to "the answers match". Published: [`tests/render-stages/results/style-scaling.md`](../../tests/render-stages/results/style-scaling.md) |
| #21 — re-enable JS test parallelization | **Done** | `Broiler.JavaScript.BuiltIns.Tests` no longer sets `DisableTestParallelization`; isolation is structural (`[ThreadStatic]` current context + `AsyncLocal` mirror), and the only shared state is process-wide and already concurrent. **2 118 tests, 0 failures, 57-59 s → 31-37 s (~1.75×)** on 4 cores |
| #16 — parallel script compile | **Done** | `ScriptCompileAhead` (budget `BROILER_SCRIPT_COMPILE_THREADS`) compiles a document's classic script sources into the context's own code cache while the host parses and while the ordered eval loop runs the scripts ahead of them; the loop is untouched and finds each compile done. **Compile stage 1.41×/1.62×/1.52× at 2/4/8 threads, whole capture 1.44× on a compile-heavy document and 1.22× on a modestly scripted one**; 45 tests, 8 documents × budgets 1/2/3/4/8 compared byte for byte, plus an assertion on the cache's own counters that the worker's key is the key `Eval` asks for. **No submodule change and no patch** — see [§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache). Published: [`tests/render-stages/results/script-compile-ahead.md`](../../tests/render-stages/results/script-compile-ahead.md) |
| #14 — incremental invalidation (was "layout dirty bits") | **Done** | The relayout harness §7 asked for exists (`--relayout-profile`) and re-aimed the item: **a relayout is 60–97% box-tree rebuild and re-cascade, not layout**, so the item as written bounds 3–39% of the cost and 2.9% on the rule-heavy page ([§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half)). `RenderTreeInvalidation` consumes `DomDocument.Mutated` — which needed no `Broiler.DOM` change, the typed record having been published all along — and skips the rebuild for mutations that cannot reach the render tree. **Offscreen build: `rules` 1 032.7 → 11.5 ms (89.8×)**, `boxes` 25.8×, `paint` 22.6×, `mixed` 16.3×, `text` 10.0×; the other thirty harness rows move 0.82–1.27, i.e. run-to-run spread. 16 `RenderTreeInvalidationTests` cases, and `Broiler.Cli.Tests` run in full both ways with **identical failure sets** (2 931 tests, the same 82 pre-existing failures each way). **The second half then took what was left**: `CascadeInvalidationSet` scans the sheets the tree was cascaded from for every class, id and attribute name any selector mentions and any `attr()` reads, and elides a connected attribute write that neither the cascade nor box construction can see — **`rules` 1 476.7 → 43.0 ms (34.4×)** on the row §11 sized, 14.2× `paint`, 13.8× `mixed`, 9.5× `boxes`, 2.1× `text`, plus 36.0× on `rules`' class toggle; rows that still rebuild span 0.79–1.21. Its gate is `--relayout-parity` (45 pairs, 22 elided, images identical), **which failed on first run and found a defect older than the item** — laying the same box tree out twice was not idempotent ([§12](#12-item-14s-second-half-the-sheets-already-knew-and-the-gate-caught-a-bug-nothing-else-could)). What is left is a *scoped* rebuild rather than a skipped one. `patches/0132`. Published: [`tests/render-stages/results/relayout-profile.md`](../../tests/render-stages/results/relayout-profile.md) |

**Phase 2 is complete.** All nine items have landed (#9, #4, #5, #6, #7, #8, #10,
#17, #3). What building them
changed is in [What building Phase 2 changed](#what-building-phase-2-changed) —
including the findings that matter most for what is left: **band parallelism
inside a primitive is the wrong unit for a page** (§4), **the largest single win
the phase has produced was a cache, not a thread** (§5), and — from item #5 —
**most of what looked like raster parallelism was the rasterizer drawing pixels
nothing could see** (§8). The raster stage is no longer the largest share of a
render on any corpus page ([§9](#9-raster-is-no-longer-the-stage-to-aim-at-and-the-published-profile-says-so)).
Item #17 added a tenth: **the split item #2 built was never reached by the host
that does its own script extraction**, so a whole family of round trips was still
serial in the path this repository measures
([§10](#10-item-17s-win-was-not-the-scan-it-was-a-host-that-never-reached-item-2s-split)).
Item #8 added an eleventh, and it is the sharpest one in the phase: **only half of
a load was safe to move earlier.** Moving a load's *completion* off the layout
thread as well as its decode changed the rendered page — on the failure path only,
which is the path whose callback does something
([§12](#12-only-half-of-an-image-load-was-safe-to-move-and-the-other-half-changed-the-page)).
Item #3 closed the phase with a twelfth, and it is a correction to this document's
own instructions: **the part §8 said to port first was not the part that paid.**
The sequential win in item #3's rasterizer came from a target lookup that banding
forced out of the per-pixel loop, and the clip narrowing paid only on content that
is inside the surface and outside the clip — which the corpus scene written to
exercise it did not contain
([§13](#13-item-3s-sequential-win-was-not-the-clip-narrowing-this-document-told-it-to-port)).

| Item | State | Evidence |
|---|---|---|
| #9 — shared render-path caches | **Done** | `FontsHandler`, `BImageRenderer`, `FallbackSystemFont` contour caches and `TrueTypeFont`'s five lazy tables; `RenderPathConcurrencyTests` (6 cases, 5 of which fail against the code before the change). Both P0-c residuals closed with it |
| #4 — band-parallel raster (`Broiler.HTML.Image`) | **Done** | `BRasterParallelism`, upstream in the pinned `Broiler.HTML` pointer (its patch file is retired); corpus `paint` page **1 594.7 ms → 1 096.5 ms (1.42×)** at 4 threads, 92.7% of its fill area split; pixels identical at 1/2/4 on all five pages, `RasterBandParallelismTests` 42 cases. Flat on the three pages whose raster is glyphs — see [§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page) |
| #3 — band-parallel raster (`Broiler.Graphics`) | **Done** | `patches/0127-…`, and `--graphics-raster-scaling` — the benchmark [§2](#2-the-rasterizer-the-profile-measures-is-item-4s-copy-not-item-3s) said did not exist, over four scenes built from the draw-call mix Broiler.UI actually issues. **The port is worth 1.58–2.96× sequentially and 1.00–1.39× on top of that at four threads**, and the split is not the one this document predicted: most of the sequential win is a per-pixel `CurrentTarget` lookup that banding forced out of the loop, and the clip narrowing pays 2.21× on the one scene whose overflow lands inside the surface and 1.02–1.14× on the three where it does not ([§13](#13-item-3s-sequential-win-was-not-the-clip-narrowing-this-document-told-it-to-port)). A two-band split measured *slower than sequential*, reproducibly, so the ported partitioner refuses one — the sibling copy has the same inversion and does not. Pixels identical at budgets of 1/2/3/4/8 across 34 `RasterBandParallelismTests` cases and every scene of the benchmark; `Broiler.Graphics.Tests` 99 tests, 0 failures |
| #5 — tile-parallel replay | **Done** | `TileParallelReplay`, `patches/0126-…`; `PerformPaint` at 1 → 4 tiles: `paint` **1 323.7 → 461.4 ms (2.87×)**, `rules` 3.49×, `text` 2.42×, `mixed` 2.44×, `boxes` 1.76× — faster than band parallelism on all five pages, including the three bands could not touch. Pixels identical at 1/2/4 tiles crossed with 1/4 bands on every page (`--tile-scaling`), 69 `RasterTileParallelismTests` cases, and a full WPT run at 1 and 4 tiles whose entire output diffs to **zero lines**. Most of the win was single-threaded — see [§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see) |
| #6/#7 — image decode | **Done** | `ImageDecodeParallelism`; JPEG **2.08–2.61×**, PNG **1.22–1.29×** at 4 threads, byte-identical at every setting (`--decode-scaling`, plus two cases in `Broiler.Media.Image.Managed.Tests`) |
| #8 — concurrent decode across images | **Done** | `ImagePrefetch` / `CssBox.PrefetchDocumentImages` / `DeferredImageLoad`; a document's image loads are issued from a worker pool before the layout pass and joined before it starts, so the pass finds every image already loaded. On a 12-image fixture at 4 concurrent loads: `PerformLayout` **183.7 → 97.1 ms (1.89×)**, whole render 204.3 → 118.4 (1.73×), over four runs 1.80–1.96× and 1.70–1.81× (`--image-prefetch-scaling`). Pixels identical at 1/2/3/4/8 loads over eleven documents (51 `ImagePrefetchTests` cases) and `css/css-backgrounds` identical at the budget on and off (40/16/5 both ways). **Only the decode moved; the completion callback did not** — moving it changed the page, see [§12](#12-only-half-of-an-image-load-was-safe-to-move-and-the-other-half-changed-the-page). It also discharges P0-c's outstanding debt: this is the first worker in the repository to establish the ambient render state and arm its assertion (6 `ImagePrefetchWorkerContractTests` cases, 3 of which fail against the code before the change) |
| #10 — glyph outline cache | **Done** | `TrueTypeFont` caches outlines by glyph index; raster stage **1.34×** on `text`, **1.54×** on `boxes`, **1.00×** on the text-free `paint` control. The shaped-run cache the item names is *not* built — see [§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names) |
| #17 — preload scan | **Done** | `PreloadScanner` / `SpeculativePreloadScan`; a document's sub-resources are found by one tokenizer pass on a worker started before the parse. Stylesheet requests are **in flight while the document is still parsing** and external scripts in the capture host now overlap instead of going one at a time — **755.1 → 521.4 ms** (median paired ratio **0.655** over 5 interleaved pairs) on 8 scripts at 40 ms each, peak concurrency 6 against 1. Exit gate: `css/css-backgrounds` identical at `BROILER_PRELOAD_SCAN` on and off (40/16/5 both ways, every verdict and pixel-match percentage the same). 40 test cases, of which the two load-order ones are assertions on *when* a request reaches the origin rather than on a stopwatch. It also found a URL-resolution defect the CSP matcher was reading through — see [§10](#10-item-17s-win-was-not-the-scan-it-was-a-host-that-never-reached-item-2s-split) and [§11](#11-a-root-relative-url-resolved-to-the-filesystem-root-and-csp-was-matching-against-it) |

**Phase 1 is complete.** All four items landed; what each one turned out to be
worth is in [What building Phase 1 changed](#what-building-phase-1-changed).

| Item | State | Evidence |
|---|---|---|
| #1 — WPT worker pool | **Done** | `--workers <N>\|auto` in `src/Broiler.Wpt/Program.cs`; **full corpus 90 min → 45 min (2.0×)** at 4 cores, 45.2 s → 23.4 s on the 61-test subset it was developed against, identical classification at 1, 4 and auto |
| #20 — CLI batch parallelism | **Done** | `src/Broiler.Cli/BatchRunner.cs`; batch capture byte-identical at 1 and 4 threads, fuzz 14.8 s → 8.9 s with the same 224 failure files |
| #2 — concurrent sub-resource fetch | **Done, partly** | `SubResourcePrefetcher`; wired to external scripts and `<link>` stylesheets. Iframes/`fetch()`/XHR are **not** wired — see the item below |
| #11 — CSS rule indexing | **Done** | `patches/0123-css-cascade-rule-index.patch`; exit gate met — cascade cost is flat in total rules (32× rules: 30.8× → **1.64×** time, 32.0× → **1.13×** bytes); corpus `rules` page 5 219 ms → 1 842 ms; WPT unchanged |

**Phase 0 is complete.** The estimates below are no longer the only evidence:
there is a stage profile, a cascade benchmark, and a GC measurement, and three of
the four Phase 0 items closed against their exit gates. What the numbers changed
is recorded in [What measuring Phase 0 changed](#what-measuring-phase-0-changed);
the master table's "Est. gain" column is still an estimate wherever a row has no
measurement, and rows that now have one say so.

| Item | State | Evidence |
|---|---|---|
| P0-a — stage benchmarks | **Done** | [`tests/render-stages/results/stage-profile.md`](../../tests/render-stages/results/stage-profile.md); ≥96.3% of wall time attributed on every corpus page |
| P0-b — single-threaded determinism (#15) | **Done** | Two of the three named sites had already landed (`JSMicrotaskQueue`, `JSContext.PostJob`); the third is `patches/0122-js-async-generator-await-dispatch.patch` |
| P0-c — static-state audit | **Done** | [Shared mutable state on the render path](multithreading-static-state.md); assertion in `Broiler.Layout/AmbientRenderState.cs` |
| #19 — GC configuration | **Done — negative result** | Server GC measured **1.62× slower** on a 4-core headless render; see the item below |

Two residuals are named rather than hidden, and both are one patch each: the
`Viewport` ambient slot has no read-side assertion (it lives in `Broiler.CSS`),
and `DocumentModeContext` is never published on the HTML-string render path.
Both are in the P0-c document.

## Method and honesty caveat

This analysis began as **structural, not measured**. It came from reading the hot
paths and classifying their data dependencies — which loops write disjoint
memory, which caches are shared, which stages are genuinely sequential — because
the repository's BenchmarkDotNet projects (`Broiler.JavaScript.Engine.Benchmarks`,
two Unicode ones, two formatting-code phases) **covered none of cascade, layout,
raster, or image decode**.

`Broiler.Render.Stage.Benchmarks` now does, so a stage-level figure below is an
observation and is marked as one. Every unmarked "estimated gain" is still a bound
derived from the shape of the code (loop independence, Amdahl ceiling given the
sequential remainder), not an observation, and **no item should be started before
its stage has a number**. Per the
[documentation rules](../README.md#documentation-rules), an estimate is not
evidence.

All figures below were taken on a 4-logical-core Linux container, .NET 10.0.10,
Workstation GC, at 1280×1024. Core count matters to some of them and where it
does, it is said.

## Summary of findings

1. **The largest safe win is the software rasterizer.** Both rasterizers
   (`Broiler.Graphics` and `Broiler.HTML.Image`) are scanline loops of the form
   `for y { for x { BlendPixel } }` over a display list that is already a flat,
   immutable, ordered command list. Tile-parallel replay needs no locks and no
   algorithmic change. It speeds up WPT, the CLI, PDF/image export, and the
   WebAssembly host — not the Windows/Linux browsers, which raster on the GPU.
2. **The largest user-visible win is not CPU parallelism at all — it is
   concurrent sub-resource fetching.** External scripts, stylesheets, iframes,
   and `fetch()` all block on `GetAwaiter().GetResult()`, **one resource at a
   time**. A page with 20 sub-resources pays 20 serial round trips.
3. **Two prerequisites are correctness work, not performance work.** JS
   continuations dispatch to `ThreadPool` when no `SynchronizationContext` is
   installed, so script callbacks already race the main-thread layout pass —
   this is the documented cause of WPT #1445 and #1143, and the mitigations are
   locks bolted onto the CSS engine and the bridge memo maps. Adding threads
   before this is fixed multiplies an existing bug.
4. **Two of the biggest speedups in the render path are single-threaded fixes,
   and they must come first.** The cascade does a linear scan of every rule of
   every stylesheet for every element (no rule bucketing, no ancestor filter),
   and layout re-lays the entire box tree from the root on every pass (no dirty
   bits — twice, in the auto-width case). Parallelizing either one before
   fixing it just spends cores hiding an algorithmic defect.
5. **Parallel layout is last, not first.** `CssBox` is a mutable tree mutated in
   place, with thread-static ambient state (`CssLengthParser` viewport,
   `DocumentModeContext`). It is the highest-effort, highest-risk item with the
   lowest ceiling, because block flow is sequential in the block direction by
   construction.

The cheapest high-value item in the whole document is the **WPT runner**: it
runs 188 tests through a *single* worker process in a `foreach`.

**Finding 1 has since been half-refuted.** The rasterizer is the largest stage, as
it says. But "both rasterizers are scanline loops, so parallelize the loops" turned
out to be the wrong reading of it: building that against the copy the profile
measures moved one corpus page and left three untouched, because a page's raster is
thousands of *small* fills rather than a few large ones. Only the sentence about
tile-parallel replay survives intact, and it is now the whole of the finding —
[What building Phase 2 changed §4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page).

**And the surviving half has now been built, which retires the finding.** Tile
parallelism is 1.76–3.49× per corpus page and beats band parallelism on all five,
so the shape was right. But it also took raster from 46.9–80.8% of a render down to
1.0–68.0%, which means **the finding's premise — that the rasterizer is the largest
safe win — no longer holds on any page in the corpus**
([§9](#9-raster-is-no-longer-the-stage-to-aim-at-and-the-published-profile-says-so)).
Roughly half of the win was not parallelism at all
([§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see)).

**The other rasterizer has now been measured too, and it refutes the same half of
the finding independently.** The finding's reasoning was that a scanline loop is
trivially splittable, so both copies should be split. `Broiler.Graphics`'s copy was,
and the threads bought 1.00–1.39× where the *single-threaded* changes made in the
same port bought 1.58–2.96× — with 85–100% of the pixel area splittable, so this
time the ceiling is not fill size but the per-primitive work a band split cannot
touch ([§13](#13-item-3s-sequential-win-was-not-the-clip-narrowing-this-document-told-it-to-port)).
Two rasterizers, two different reasons, one conclusion: **"it is a loop, so split
it" is not a reason to expect a speedup.**

**Finding 4 needs a third example.** It names the cascade's rule scan and layout's
full-tree relayout as single-threaded fixes that must precede any threading. The
glyph outline path was a third, hidden in a phase about threads and larger than the
threading item that shared its stage
([§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names)).
The generalisation the finding is reaching for: **before parallelizing a stage,
check what the stage is recomputing.** Item #5 supplied a fourth example and
widened it once more — the rasterizer was not only recomputing, it was *computing
pixels nothing could see*, and deleting that work was worth about as much as the
threads were ([§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see)).

**Finding 4's second half was aimed at the wrong pass, and is now half-built.** It
names "layout re-lays the entire box tree from the root on every pass (no dirty
bits — twice, in the auto-width case)". Measured, the second traversal is guarded
by `MaxSize.Width <= 0.1` and never happens on a headless path; and the pass that
does happen is not the expensive one — a relayout is 60–97% *box-tree rebuild and
re-cascade*, so the finding names the smaller half of the smaller half. Aimed at
the rebuild, its first slice is built and is worth 10–90× on the case it covers
([§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise)).
So the finding's instinct — an algorithmic fix before threads — was right for the
third time running, and its *identification* of the algorithm was wrong for the
second.

**Findings 1, 4, and 5 have since been measured, and one of them was wrong.** The
next section has the numbers; in short, finding 1 holds, finding 4 understates the
cascade by a wide margin, and finding 5's conclusion is right for a reason it does
not give.

## What measuring Phase 0 changed

Four findings, in descending order of how much they should change what happens next.

### 1. The cascade is not "a big win"; on a rule-heavy page it is the entire render

Finding 4 above called the cascade's linear rule scan one of two single-threaded
fixes that must come first. It understates it. On the corpus's `rules` page — 700
`<div>`s carrying two classes each, against a 900-rule sheet — `parse+cascade` is
**96.3% of a 5.0-second render**, and a `CssStyleEngine` benchmark over the same
element set puts **3.68 s** of that in the cascade itself. The memoized pass over
the same elements is **0.35 ms**, a ratio of about 10 600×.

`RuleScalingBenchmarks` isolates the mechanism by holding the element set *and the
matched-rule count* fixed at four while growing the sheet:

| Rules in sheet | Cold cascade, 400 elements | Allocated |
|---:|---:|---:|
| 100 | 114.9 ms | 181.7 MB |
| 400 | 466.5 ms | 722.0 MB |
| 1 600 | 2 532.2 ms | 2 897.5 MB |
| 3 200 | 3 543.6 ms | 5 817.9 MB |

32× the rules, 30.8× the time, **32.0× the allocation** — for the same four matches.
That is item #11's claim measured exactly, and it makes the roadmap's "10–100× on
rule-heavy pages" a floor rather than a hope. The allocation figure is a second,
independent defect the roadmap did not name: the cascade allocates in proportion to
*total* rules, so a rule index would cut garbage as much as it cuts time.

**Consequence:** item #11 is the highest-value item in the document, ahead of every
parallel item including the rasterizer, and it is single-threaded work.

### 2. Layout is not the bottleneck, on any page in the corpus

| Page | layout share |
|---|---:|
| `text` | 6.5% |
| `rules` | 0.6% |
| `boxes` | 4.9% |
| `paint` | 0.9% |
| `mixed` | 4.6% |

`boxes` is a nested block/flex/grid tree built specifically to load layout, and
layout is 4.9% of it — against 80.1% for `parse+cascade`. The roadmap's ordering
(layout last) is right; its stated *reason* — a low ceiling because block flow is
sequential in the block direction — is not the operative one. **The operative reason
is that layout is 0.6–6.5% of a render, so even a perfect parallel layout is worth
at most a few percent.** Items #13 (20–30 d) and #14 (15–20 d) are 35–50 days
against a stage that does not currently dominate anything measured here.

The double-layout pass finding 4 names is also not reachable on this path: it is
guarded by `MaxSize.Width <= 0.1`, and a headless render at a fixed viewport sets
`MaxSize`. It costs a second traversal only in the auto-size shrink-to-fit case.

### 3. The rasterizer estimate holds

| Page | raster share |
|---|---:|
| `paint` | 80.8% |
| `text` | 63.7% |
| `mixed` | 46.9% |

Items #3 and #5 are aimed at the right thing, and the corpus's `paint` page says a
tile-parallel replay has 80% of a render to work with. The paint *walk* — fragment
tree to display list — is 0.3–0.7% everywhere, so the display-list construction is
not worth parallelizing; only its replay is.

### 4. Server GC is a pessimization here, not a 1.1–1.4× win

See item #19 below.

## What building Phase 1 changed

Four items, and the two things worth carrying forward are a shortfall and a
constraint neither of which the plan anticipated.

### 1. Rule indexing met its exit gate; the render it sits in did not move 10–100×

Both statements are measurements of the same change and neither is the headline
alone.

**The exit gate — "cost scales with matched rules, not total rules" — is met.**
`RuleScalingBenchmarks` holds the element set and the matched-rule count fixed at
four and grows only the rules that cannot match:

| Rules in sheet | Cold cascade, before | after | | Allocated, before | after |
|---:|---:|---:|---:|---:|---:|
| 100 | 114.9 ms | **15.74 ms** | 7.3× | 181.7 MB | **8.57 MB** |
| 400 | 466.5 ms | **15.65 ms** | 29.8× | 722.0 MB | **8.70 MB** |
| 1 600 | 2 532.2 ms | **18.80 ms** | 134.7× | 2 897.5 MB | **9.10 MB** |
| 3 200 | 3 543.6 ms | **25.89 ms** | 136.9× | 5 817.9 MB | **9.69 MB** |

32× the rules used to cost 30.8× the time and 32.0× the bytes; it now costs
**1.64× the time and 1.13× the bytes**. That is the linear-in-total-rules defect
gone, on both axes — and the allocation collapse (600× at the top of the range) is
the second defect Phase 0 named, which no GC setting could have fixed. The
projected 10–100× is real on this axis and exceeded at the top of it.

*(Run with BenchmarkDotNet's short job, so the confidence intervals are wide — but
the claim being made is flat-versus-linear across two orders of magnitude, which no
plausible interval touches.)*

**On a whole render it is 2.8×.** The corpus's `rules` page goes **5 218.96 ms →
1 841.71 ms** end to end, its `parse+cascade` stage **5 035.35 ms → 1 698.11 ms**.
That is not a contradiction: the benchmark isolates exactly the cost the index
removes, while the stage also parses 110 296 characters of source and 900 rules and
then cascades against the rules that *do* match. Amdahl, not a shortfall.

**Consequence:** `parse+cascade` is still 92.2% of that page, and it is now unknown
which half. The next cascade item is splitting that stage — item #12 (parallel style
recalc) must not be started against a stage whose composition is unmeasured, and it
would be aimed at the cascade half of a row that may now be mostly parse.

### 2. The render path cannot be parallelized in-process yet, and that decides item #20's shape

Item #20 reads "per-file / per-page parallelism", blocked by "Nothing". That is
true of document conversion — `Broiler.Documents` has no assignable statics at all,
only lookup tables — and false of everything that renders. The P0-c audit's two
genuine unsynchronised caches (`FontsHandler`'s four dictionaries and
`BImageRenderer._images`) sit on process-wide singletons on the render path, so two
captures, or two layout-fuzz cases, in one process is a data race today.

So the CLI parallelizes document conversion on threads and capture and layout fuzz
across **child processes** — the same answer the WPT runner's worker pool already
rested on, for the same reason. It is not a workaround: process isolation is what
makes "identical output at any worker count" checkable by comparing bytes, which is
what the global exit gate asks for.

**Consequence:** item #9 (font-cache thread safety, Phase 2) gates more than the
three items it is credited with. Until it lands, *every* CPU-parallel item on the
render path is a process-parallel item.

### 3. Item #2 is wired to two of its four call-site families, and the other two need item #17

The prefetch/consume split is built and is live for external scripts and `<link>`
stylesheets — the two places where the whole set of URLs is already known at a
single point (the script scan; the collected sheet list). Iframes/sub-documents
(`SubDocuments.cs`) and `fetch()`/XHR have no such point: a sub-document URL becomes
known when the element is reached, which is the moment it is consumed, so there is
nothing to overlap it with.

That is what item #17, the speculative preload scan, is for, and the roadmap already
says #17 "feeds step 1". The dependency is stronger than "compounds": for those two
families the preload scan is not an amplifier, it is the only source of a prefetch
trigger.

**This section is right about the families and wrong about the unit.** Building #17
showed that "wired to two of its four call-site families" is a statement about *one
host*: `CaptureService` extracts scripts itself and so reached none of the wiring
described here, and its script fetches were still strictly serial. A split is a
property of a call site, not of a codebase —
[Phase 2 §10](#10-item-17s-win-was-not-the-scan-it-was-a-host-that-never-reached-item-2s-split).

### 4. The WPT pool is bounded by memory, and the default has to know it

Sized `min(cores, availableRam / 1.5 GiB)`, which on the 4-core/13 GiB container is
4 workers and on a 16-core box with 4 GiB free is 2. Measured on
`css/css-backgrounds` (61 tests): 45.2 s at one worker, 23.4 s at four — 1.93×, not
4×, because worker startup and the runner's own serial reporting are a fixed share
of a 61-test run. On a full shard the constant amortizes.

**It does.** A full WPT run on the same 4-core shape goes **90 minutes → 45**, so
the whole-corpus figure is **2.0×** against the subset's 1.93× — the fixed cost
this section predicted would wash out at scale does wash out, and the remaining
gap to 4× is the per-test GiB budget capping the pool at four workers, not the
runner's serial remainder. That is the first measurement of item #1 at corpus
scale; every earlier figure here is the 61-test subset.

> **Which tree that was measured on matters, and it is not the one a reader would
> assume.** `ca53d44` was written while Phase 2 was one commit old: it contains item
> #9 — a thread-safety prerequisite the master table records as "not a speedup
> itself" — and **none** of #4, #5, #6, #7, #8, #10, #17, #3, or any of Phase 3. So
> both endpoints of the 2.0× are an essentially **end-of-Phase-1** engine, and the
> figure is a pool ratio measured on a tree that predates every sequential win since.
> Phase 4 §4 re-measured the endpoints against today's engine and found them
> **unmoved**, which is a finding about WPT rather than about the wins
> ([§4](#4-phases-2-and-3-are-worth-nothing-on-a-wpt-run-and-the-pages-are-why)).

Where the memory figure cannot be read at all, the pool stays at **one** worker.
Guessing high on an unreadable budget is how a runner OOM-kills a CI box, and the
per-test allowance is a full GiB.

## What building Phase 2 changed

Eleven findings. The first three came out of item #9, the gate; §4–§9 came out of
the raster, text and decode work that followed it, and three of those change what
the rest of the phase — and the phase after it — should be. The last two came out
of item #17, and neither is about a thread: one is about which *host* a
prefetch/consume split reaches, the other about a URL-resolution defect the whole
bridge was reading through.

### 1. Item #9 was four sites, not one, and the audit's method is what hid two of them

The item is scoped as "make the font caches thread-safe", and P0-c had already
corrected the roadmap once on this point: the hazard is **instance** state on a
process-wide singleton, not `static` fields, which is why a scan for mutable
statics reported the render path as almost clean. Building it corrected the
method a second time. Two of the four sites are not caches at all:

* **`FallbackSystemFont`'s two contour caches** were left as plain dictionaries
  when the four advance/glyph-index caches beside them were converted. Same
  shape, same read-path population, same singleton — and they are the ones a
  *painting* thread reaches, which is the thread this phase adds.
* **`TrueTypeFont`'s five lazily-parsed OpenType tables**, which is the one worth
  generalising from. Each was `if (_parsed) return _t; _parsed = true; _t =
  Parse();` — the latch published **before** the value. A second thread inside
  that window reads a null table, and every accessor reads null as *this font
  does not have this table*. Nothing throws. The render is simply wrong: no
  ligatures, no mark positioning, or — for a CFF-outline font — `HasOutlines`
  false and the text drawn with the built-in 5×7 block glyphs.

**A lazy-init latch is as much shared mutable state as a cache is**, and neither
the roadmap's framing nor P0-c's enumeration looks for one. That is worth carrying
into #12 and #13, where the same shape is likely to exist in the style and layout
caches.

It also cost a test. The first draft of the lazy-table test probed
`IsMarkGlyph('A')` and a `liga` substitution on `'A'` — both of which answer the
same with and without the table, so the test passed against the bug. It now picks
a glyph the *warm* font calls a mark and asks that first, from every thread, on a
font instance nobody has touched. Five of the six new tests fail against the code
before the change and pass after it; that is the claim the item rests on, not the
64/64 after.

### 2. The rasterizer the profile measures is item #4's copy, not item #3's

Item #3 is credited in the master table with "**4–6× on 8 cores** of a stage
measured at 46.9–80.8% of a render — the largest measured share in the document".
The share is real. The attribution is not: `Broiler.Render.Stage.Benchmarks`
resolves `BBitmap` to `Broiler.HTML.Image.BBitmap` and times
`HtmlRender.RenderToImageCore`, so the pixels in that 46.9–80.8% are drawn by
`Broiler.HTML/Source/Broiler.HTML.Image/BCanvas.cs` — **item #4**. Item #3's
`Broiler.Graphics/Rendering/BCanvas.cs` backs `BImageRenderer`, and through it
Broiler.UI and the Writer, none of which the corpus renders.

Two consequences for whoever starts the raster work:

* **The order in the Broiler.Graphics roadmap below is right for a reason it does
  not give.** It says unify the two rasterizers first so the parallelism is not
  built twice. The stronger reason is that building it against item #3's copy
  first would parallelize the rasterizer the corpus does not exercise, and the
  measurement proving it worked would have to come from a benchmark that does not
  exist yet.
* **The two files are not the same length** (949 vs 1 180 lines), so "the same
  scanline rasterizer twice" is an approximation and unification is a real piece
  of work, not a merge.

### 3. Item #9 removes the constraint Phase 1 §2 imposed on everything below it

Phase 1's finding was that the render path's unsynchronised caches made two
in-process renders a data race, so *every* CPU-parallel render-path item was
really a process-parallel item — which is why the CLI parallelizes capture and
layout fuzz across child processes rather than threads. Both caches it named are
now synchronised, and `BImageRenderer` additionally moved its replay transform
state per-call, so two threads can render through one renderer into two surfaces.

That does not retroactively make the CLI's process isolation a workaround: it is
still what makes "identical output at any worker count" checkable by comparing
bytes. It does mean items #8, #10, #12 and #13 no longer have to route around
this, and that a future in-process worker pool is now a design choice rather than
a blocked one.

**What was still owed before that pool exists — and item #8 has now paid it.** The
ambient-state contract was instrumented on all three slots but off by default,
because nothing called `AmbientRenderState.Establish` on a worker thread: there
were no worker threads. The debt was recorded here as *the first item that creates
one has to arm `EnforceOnThisThread` in the same place it calls `Establish`, or the
instrumentation bought here goes unused.* Item #8's image-prefetch worker is that
thread, and it does both — `ImagePrefetch.RunAll` establishes every slot per load
and arms the assertion for the duration, then clears the record and restores the
switch so a pooled thread does not vouch for the next document's state or leave a
trap for whatever runs on it next. `ImagePrefetchWorkerContractTests` asserts all
of that on the real worker, and 3 of its 6 cases fail against the code before it.

**What arming it bought, given that nothing failed:** an audit rather than a fix.
An image load can reach SVG rasterization, which opens a canvas and draws — text
included — so a prefetch worker is a render thread by any reasonable definition.
Following the enforcement through said `BSvgRasterizer` reads none of the three
slots: it parses its own attributes and builds its own coordinate context, and the
font cache underneath it was made thread-safe by item #9. That is now a checked
claim instead of an assumption, and the check is what will fire the day an image
decode grows a dependency on the viewport.

Band-parallel raster (item #4) does **not** discharge that debt, and it is worth
saying why it did not need to: a band never leaves the primitive it was created
in, so it inherits nothing and establishes nothing. The ambient slots are read
during *layout* and during display-list *construction*, both of which have
finished before a fill starts.

### 4. Band parallelism inside a primitive is the wrong unit for a page

Item #3 is credited in the master table with **4–6× on 8 cores**, and §2 above
established that the stage the profile measures is item **#4's** copy of the
rasterizer. Building #4 produced a number, and it is not 4–6×. It is **1.42× on
one of the five corpus pages and nothing at all on three of them** — and the
reason is worth more than the number.

The partitioner counts what it decided. On the corpus, at 1 280×1 024:

| Page | fills taken inline | fills split | fill area inline | fill area split | share of area split |
|---|---:|---:|---:|---:|---:|
| text | 3 858 | 0 | 365 760 | 0 | 0.0% |
| rules | 1 659 | 0 | 123 929 | 0 | 0.0% |
| boxes | 717 | 0 | 108 349 | 0 | 0.0% |
| paint | 5 600 | 2 800 | 1 353 600 | 17 248 400 | 92.7% |
| mixed | 4 390 | 11 | 372 865 | 1 345 172 | 78.3% |

**A page's raster is not a few big fills; it is thousands of small ones.** The
`paint` page — built specifically to load the rasterizer — issues 8 400 fills
averaging about 2 200 pixels each. The `text` page issues 3 858 fills averaging
95 pixels: they are glyphs, and a glyph is one or two dozen scanlines. There is no
threshold at which splitting a 95-pixel fill pays, so on three of the five pages
band parallelism is structurally unable to do anything, at any core count.

**The threshold is where a plausible number makes a feature inert.** The first
value tried was 24 K pixels — a defensible "don't split anything small" figure. At
that setting *not one fill on four of the five pages reached it*, `paint` split
19% of its area, and the whole feature measured 1.03×. Lowering it to 2 048
pixels — still 20× a glyph — moved `paint` to 92.7% of area split and 1.42×
end-to-end (raster stage 1.61×) with pixels unchanged. A tuning constant nobody
measured would have shipped a no-op and called it item #4.

**What this says about item #5.** Tile-parallel replay partitions the *surface*
and replays the whole display list per tile, so its unit of work is a tile's worth
of the page — every fill that touches the tile — rather than one primitive. That
is precisely the unit the table above says exists on all five pages and the
per-primitive unit does not. Item #5 was scheduled as "supersedes #3 for whole-page
renders"; the measurement upgrades it to **the only shape of raster parallelism
the corpus can use**, and the 4–6× estimate on rows #3/#4 should be read as
belonging to #5.

### 5. The phase's largest win so far is a cache, and not the one item #10 names

Item #10 says to add a shaped-run cache and, in its own words, to *measure the
cache alone before adding threads; it may be the whole win*. Following that
instruction found a different cache, and the item's advice was right about the
wrong object.

`ComplexTextShaper.Shape` — what item #10 proposes caching — runs only when
`RequiresShaping` is true: complex scripts, RTL, or explicit
`font-feature-settings`. The corpus is Latin by design (so that a page measuring
layout does not also measure shaping), so a shaped-run cache would have measured
**zero** on every page available, and it remains unbuilt for that reason rather
than being claimed as done.

What was actually being repeated per glyph was one layer down.
`TrueTypeFont.GetGlyphContours` re-walked `glyf` — or re-ran the CFF charstring
interpreter — re-flattened the quadratic segments and allocated fresh arrays **on
every draw of every glyph**, for the same glyph index, thousands of times per page.
`FallbackSystemFont` has always cached exactly this; `TrueTypeFont` did not, and
nothing in the roadmap or in P0-c pointed at it because it is not shared mutable
state and not a thread. Caching it, on the same corpus, two runs each side:

| Page | raster before | raster after | ratio |
|---|---:|---:|---:|
| text | 292.7 ms | 218.4 ms | **1.34×** |
| boxes | 71.0 ms | 46.2 ms | **1.54×** |
| mixed | 154.8 ms | 134.6 ms | 1.15× |
| rules | 126.2 ms | 91.4 ms | 1.38× |
| paint | 829.1 ms | 827.9 ms | 1.00× |

**`paint` is the control, and it is the reason the rest is believable.** That page
draws no text; if it had moved, the table would be measuring the host's mood
rather than the cache. It did not.

This is a **single-threaded fix inside a phase about threads**, and it beat the
threading item that shares its stage. Finding 4 of the summary already says two of
the biggest render-path speedups are single-threaded fixes that must come first;
this is a third, and the general lesson is narrower and more useful than "cache
things": *before parallelizing a stage, check what the stage is recomputing.* A
thread makes redundant work finish sooner; deleting it makes the work not happen.

### 6. Item #8 is a prefetch/consume split, not a `Parallel.For`

The master table describes item #8 as decoding *N* page images concurrently and
lists its blocker as `BImageRenderer._images`, which item #9 cleared. Reading the
path shows a second blocker the row does not name, and it is the one that decides
the item's shape.

`ImageLoadHandler.SetImageFromFile` already queues decode to the thread pool —
*unless* `AvoidAsyncImagesLoading` is set, and every headless entry point sets it
(`HtmlRender.RenderToImageCore` and its two siblings), because the render is
synchronous and must have every image before layout and paint. So on exactly the
paths this document measures, images are decoded **one at a time, inline**, and
the concurrency the row credits the code with is switched off.

That makes item #8 the same shape as item #2, not the shape its row implies: the
URL set has to be discovered when the document is parsed, all decodes issued at
once, and the existing synchronous call site left where it is, consuming from a
cache. The `SubResourcePrefetcher` written for #2 is the precedent, and item #17
(the preload scan) is what supplies the URL set — so **#17 should come before
#8**, which is the reverse of the order the component roadmap lists them in.

**Half of that prediction was right, and the half that was wrong saved the work.**
The shape is a prefetch/consume split, as this section says. But the URL set turned
out not to need item #17 at all: the box tree already holds it, and it holds it
better. A preload scan finds what a document's *source* names, which is a
superset — an `<img>` inside a `display:none` subtree, or one a script removed
before layout, is a URL in the source and not a load the document makes. The box
tree, walked at the document root after construction and before the layout pass,
names what layout is about to ask for; and because a box the walk skips simply
loads inline as it always did, an incomplete answer costs speed rather than
correctness. So `PreloadScanResult.ResolvedUrls(PreloadKind.Image)` is still
unconsumed, and item #16 remains its only prospective customer. The general point
is worth keeping: **discovering a resource set from the structure the consumer will
actually walk beats discovering it from the source, whenever that structure exists
in time.** For images it does; for stylesheets, which item #17 does feed, it does
not — those are wanted before there is a tree at all.

**And the "consuming from a cache" half was unnecessary too.** Layout already asks
the host for a loader per image and calls `LoadImage` on it; the prefetch is those
same two calls, moved. No cache keyed by URL, no ownership transfer, no second way
to load an image — and no submodule change, because the whole of it sits in
`Broiler.Layout`. A cache would additionally have had to decide what to do about a
document referencing one file twice, which today is two loaders and two decodes;
collapsing them would have changed how many times the file is read, which is not a
change a latency item gets to make on the way past.

**What the exit gate actually ran on.** The corpus scaling harness compares pixels
per page, which is five documents. The gate the roadmap sets is the WPT corpus, and
it was run twice at four workers — once with the raster and decode budgets forced
to **4** and once to **1** — over all 147 discovered tests. Both runs report
**104 passed, 36 failed, 7 skipped**, and diffing their full output leaves only
reordered `[RUN ]` progress lines: every per-test verdict, every pixel-match
percentage, every failure bucket identical. The 61-test `css/css-backgrounds`
subset was run the same way at 1 and 4 workers with the same result, and there the
pool's own 2× is visible in the wall clock (46 s → 23 s) while the classification
does not move.

### 7. Two kinds of parallelism now multiply, and the runner has to divide

Phase 1 gave the WPT runner a pool of *N* worker processes. Phase 2 gives every
process band-parallel raster, tile-parallel replay and band-parallel decode, each
defaulting to one thread per core. Nobody sees both numbers except the runner: the default is
per-process and correct in isolation, and the pool size is per-run and correct in
isolation, but together they are *N × cores* runnable threads on *cores* cores.
That is slower than either alone, and it makes the per-test timeout a lottery
rather than a limit.

`Program.ApplyRenderThreadBudget` divides — `max(1, cores / workers)` — and
publishes the result into the environment the workers inherit, leaving any
variable the caller set explicitly untouched. **The tile budget gets the same
figure as the band budget rather than a share of it**: the two do not compound,
because a tile view runs its bands inline, so a render spends whichever of them
it is using and never both. They are printed in the run header
next to the pool size, because a run whose parallelism settings are invisible is a
run whose timings cannot be compared with another's.

This generalises past the WPT runner: **every host that runs more than one render
at once now owns a division it did not have to make before** — the CLI's batch
processes are the other one in this repository.

> **Phase 3 added a fifth budget to this division**, `BROILER_STYLE_THREADS`
> (item #12's warm pass). It passes the test below without argument — a thread
> resolving an element's cascade holds a core the way a raster band does — and it
> is the one that would have been worst to miss, because it runs *earlier* than
> any of the other four: a pool of *N* workers would each have opened its render
> with *N* threads on the cascade, before layout, let alone paint.

**Item #17's worker is deliberately not in that division, and the reason is the
test for whether something belongs there.** `ApplyRenderThreadBudget` divides
budgets that put *cores* under contention: a raster band, a tile, a decode band
each occupy a core for the length of a render. The preload scan occupies one pool
thread for one tokenizer pass and then blocks on I/O, so *N* workers do not produce
*N × cores* runnable threads — they produce *N* threads that are almost always
waiting on a socket. It has an on/off switch (`BROILER_PRELOAD_SCAN`) because the
exit gate requires a sequential equivalent, not because a host has to size it.

**Item #8's budget passes that test and is in the division**
(`BROILER_IMAGE_PREFETCH_THREADS`): a concurrent image load holds a core for the
length of a decode, which is exactly the shape the division is for. One consequence
is worth stating rather than discovering: at the pool's default of one worker per
core the per-worker figure is 1, and 1 turns the walk off. That is the right answer
— *N* workers each rendering a document already saturate the cores, and there is
nothing left for a second axis to win — but it does mean **a default WPT run does
not exercise item #8**, which is why its gate is run at `--workers 1`.

**Its two budgets are the first pair in this document that genuinely compound, and
dividing them is still wrong.** A concurrent image load decodes through items
#6/#7's band partitioner, so *N* loads at *N* bands is *N²* runnable threads on
*N* cores — unlike bands and tiles, which a tile view keeps from ever running at
once. The correction was built and measured at nothing: over four interleaved runs
the layout stage reads 94.7 / 91.3 / 101.8 / 85.6 ms undivided against
92.1 / 90.6 / 94.0 / 93.6 divided — three runs favour the division by 1–8% and the
fourth penalises it by 9%, so the effect does not have a sign, let alone a size.
The .NET pool already serialises the excess, and a document's images do not divide
evenly into waves — the last wave runs with fewer loads than the budget and wants
every core it can get. So the
prefetch budget gets the same figure as the decode budget, for a different reason
than the tile budget does, and `--image-prefetch-scaling` keeps the divided
configuration as a column so the null result stays re-runnable.

**How that null result was nearly a false positive is the more useful half.**
Measured in its own block rather than inside the interleave, dividing read **1.10×
faster** on the first run and **1.17× slower** on the second — the sign of the
effect changed between two runs of the same code. `DecodeScaling` already documents
why for this host (throughput drifts by tens of percent over tens of seconds) and
its own settings are interleaved for that reason; the mistake was assuming a
configuration measured *outside* the interleave could be compared with the ones
inside it, however carefully its own medians were taken. **A median is only
comparable with another median taken under the same drift.**

### 8. Most of item #5 was not parallelism; it was the rasterizer drawing pixels nothing could see

Tile-parallel replay was built as a threading item and it delivered threading
numbers — 2.87× on the `paint` page, 2.42× on `text`, and faster than band
parallelism on all five pages including the three bands could not touch. But the
first thing building it required was not a thread, and that part is worth more
than the item's own framing suggests.

**A tile that inherits only a clip does the whole page's work.** Each tile replays
the whole display list, so every cost that is paid per primitive rather than per
drawn pixel is paid once per tile. Three rejections had to be added before the
tiles were worth anything, and each of them is a sequential fix:

1. **`BCanvas` narrows the loop, not just the pixel.** It keeps a running
   intersection of its including clips and clamps every primitive's pixel bounds
   to it. Exact rather than approximate — a pixel the bounding box excludes is one
   the per-pixel test already rejected — and it turns "reject three quarters of the
   surface" from a per-pixel cost into a per-primitive one.
2. **The backend skips an item whose drawn rectangle it derives itself.** A tiled
   gradient allocates and renders a gradient tile bitmap *before* it draws with it,
   so without this every tile rebuilt all 1 400 of the `paint` page's gradients.
3. **The text backend rejects a run by row band**, computed from the font's own
   ascent and descent before a glyph is looked at.

**The order they were built in is the evidence.** With only (1), the `paint` page
reached 1.59× at four tiles — still behind band parallelism — and `boxes`
*regressed* to **0.81×**: it was paying four list walks for a page whose raster is
34 ms. Adding (2) took `paint` to 2.84× and left `boxes` at 0.82×, because that
page's per-tile cost is text. Adding (3) moved every page, `boxes` included. Two of
the three are not about threads at all, and without them the item would have
shipped a regression on one of the five pages it was measured against.

**Here is what the three are worth with no threads at all.** Raster stage, one
tile, one band:

| Page | before | after culling | ratio |
|---|---:|---:|---:|
| text | 210.9 ms | 148.4 ms | 1.42× |
| boxes | 41.7 ms | 14.0 ms | **2.98×** |
| rules | 97.4 ms | 51.0 ms | 1.91× |
| mixed | 153.8 ms | 143.4 ms | 1.07× |
| paint | 1 334.7 ms | 1 323.7 ms | 1.01× |

The two pages that barely move are the two whose content is mostly *on screen* —
`paint` is 1 400 boxes swept across the viewport by construction. The pages that
move are the ones taller than their viewport, which is to say: normal documents.

This is the third time in this document that a stage's biggest win turned out to
be work the stage should not have been doing — after the cascade's rule scan
(#11) and the glyph outline cache ([§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names)).
Finding 4 of the summary generalises to: **before parallelizing a stage, check
what the stage is recomputing — and what it is computing that nobody will see.**

**The bug worth carrying forward is about clips.** A tile view's `GetClip()`
returns its parent's clip *unnarrowed*; the tile is added to the rasterizer's
per-pixel test and to nothing above it. That is not tidiness. `DrawClippedImage`
recomputes a scaled image's **source** rectangle from the intersection of its
destination with `GetClip()`, so a tile-narrowed clip re-derives a different
source rectangle and resamples the image. Algebraically the two mappings agree; a
floating-point rounding apart, they do not — it surfaced as *one row of different
pixels on two WPT tests out of 147*, and on none of the synthetic documents tried
against it. **A caller may read a clip to derive geometry, not merely to obey
it**, so any future work that hands a component a narrowed view of a surface owes
the same audit. It is checked directly now (a tile view reports its parent's
clip) rather than left to a pixel comparison that happened to catch it.

### 9. Raster is no longer the stage to aim at, and the published profile says so

Phase 0's measurement put raster at 46.9–80.8% of a render and called items #3
and #5 "aimed at the right thing". After #4, #10 and #5 the same corpus at the
same viewport on the same box reads:

| Page | raster share, Phase 0 | now |
|---|---:|---:|
| paint | 80.8% | 68.0% |
| text | 63.7% | 39.5% |
| mixed | 46.9% | 29.8% |
| boxes | 11.9% | 7.2% |
| rules | 4.4% | 1.0% |

`parse+cascade` is now the largest stage on four of the five pages, and on `rules`
it is 96%+ of a render that item #11 already made three times faster. **The next
measurement this document owes is the split of `parse+cascade` into parse and
cascade** — Phase 1 §1 already flagged that it is unknown which half dominates,
and item #12 (parallel style recalc) must not be started against a stage whose
composition is unmeasured. That is now the largest unattributed question in the
document, and it is larger than anything left in Phase 2.

> **Answered in Phase 3**, and not close on any page: the cascade is 81.3–98.2% of
> the stage and both parse halves together are 0.5–4.8%. See
> [Phase 3 §1](#1-parsecascade-is-a-cascade-stage-the-name-overstates-the-parse).

### 10. Item #17's win was not the scan; it was a host that never reached item #2's split

The item is scoped as "a worker scans raw bytes for `src`/`href` while the main
parse runs, feeding #2", and that is what was built: one `HtmlTokenizer` pass on a
pool thread, started as the first statement of `DomBridge.ParseHtml`, whose sink
hands the stylesheet set to the loader before the parse has produced a node. The
part of it that produced a number was somewhere else.

**The stylesheet half is worth exactly the parse, and no more.** Item #2 already
issues a document's sheets concurrently *with each other*; all this moves is when
they start, from after the parse to before it. The gain is therefore bounded by the
parse time and is invisible on a small document. It is asserted rather than timed —
an origin that holds every request open shows three sheet requests arrived while
`Attach` was still running, and shows **zero** requests exist at the same point with
the scan off. That is the claim; a stopwatch on it would mostly measure the host.

**The script half was a serial fetch nobody had noticed, in the path this
repository measures.** `CaptureService.ExecuteScriptsWithDom` — the capture and WPT
entry point — walks the script tags with its own regex and called
`FetchExternalScript` inline as it reached each one. Item #2's prefetch/consume
split does exist for scripts, in `ScriptExtractionService.ExtractAll`, and this host
does not call it: it extracts scripts itself. So the split was built, tested,
measured, and **not reached by the host that renders the corpus**. Fed by the scan,
that loop's round trips overlap:

| 8 external scripts, 40 ms each | serial (scan off) | overlapped (scan on) | ratio | saved |
|---|---:|---:|---:|---:|
| idle machine | 755.1 ms | 521.4 ms | **0.655×** | 233.7 ms |
| under the rest of the suite | 941.2 ms | 678.6 ms | 0.702× | 262.6 ms |

Medians of five interleaved pairs each. The arithmetic predicts a saving of 240 ms
— eight requests at six-per-host is two waves, so 320 ms of latency becomes 80 ms —
and both runs bracket it, which is the point of quoting two: the *ratio* moves with
what else the box is doing, the *saving* does not, because it is a count of round
trips rather than a speed. Peak concurrency at the origin is **6** with the scan and
exactly **1** without, which is the assertion the table rests on — `1` is the
definition of serial, and it is checked rather than inferred.

**The exit gate.** `BROILER_PRELOAD_SCAN=0` is the sequential path rather than an
approximation of it: with no scan there is no prefetcher entry for any speculated
URL, so every consume site takes the branch it took before the scan existed.
`css/css-backgrounds` (61 tests) was run at both settings — **40 passed, 16 failed,
5 skipped** each time, and diffing the full output leaves the header's
available-memory reading and two elapsed-time lines. Every per-test verdict, every
pixel-match percentage and every failure bucket is identical, which is what the
change claims: it moves when a request starts and nothing else.

**The generalisation is about where a split lives.** A prefetch/consume split is a
property of a *call site*, not of a codebase: a second host that re-implements the
extraction re-implements the serial fetch along with it, and nothing in the first
host's tests can see that. Phase 1 §3 recorded item #2 as "wired to two of its four
call-site families"; the truer statement is that it was wired to two families **in
one of the hosts that has them**. Before declaring a latency item done, ask which
entry points reach the code it was built in — and note that this is the same lesson
as §2's, one layer up: there, the profile was measuring a different copy of the
rasterizer than the item was aimed at.

### 11. A root-relative URL resolved to the filesystem root, and CSP was matching against it

`UrlResolver.Resolve` is the bridge's one URL-resolution implementation — script
fetching, `@import`, sub-documents, `fetch()` redirects and the **CSP source
matcher** all go through it. Its first line asked `Uri.TryCreate(url,
UriKind.Absolute, …)` and returned the result if it succeeded.

On Unix that succeeds for `/app.js`, because a leading slash is a valid absolute
*file path* there, and yields `file:///app.js`. So every path-absolute reference in
a document was taken off the filesystem root instead of the page's own origin:
`<script src="/app.js">` on an `https:` page resolved to `file:///app.js`, and
`CspSourceMatching.ResolveUri` handed that to the policy comparison — a
`script-src 'self'` check on a URL whose origin had been replaced. Scheme-relative
`//cdn.example/x` had the same fault for the same reason.

It is one line to fix (neither form is an absolute URI under RFC 3986 — both
require the base), and the fix is invisible on `file:` pages, which is the whole of
the WPT corpus: resolving `/x` against `file:///a/b.html` gives `file:///x` either
way. That is why nothing caught it.

**One copy of the same mistake is left, and is deliberately left.**
`SubDocuments.LoadSubResource` does not call the shared resolver first — it asks
`Uri.TryCreate(resourceUrl, UriKind.Absolute, …)` itself and keeps the raw string
when it succeeds, which is the identical fault at an identical line. Fixing it
changes which document an `<iframe src="/x">` loads, so it belongs in a change that
can show a WPT run either side of it, not folded into a latency item. It is
recorded here rather than in a comment because the shared resolver being right is
now what makes that site's inline copy visibly wrong.

**What is worth carrying forward is how it surfaced.** No behaviour changed to
expose it. It appeared because item #17's tests asserted a *resolved* URL by
value — `https://example.test/assets/a.css` — where the existing tests on the same
resolver asserted that fetching worked. This is the fourth time in this document
that a stage's own instrumentation found a defect the feature was not looking for,
and it suggests the narrower rule behind §5 and §8: **an item that asserts its
inputs by value audits every layer it reads through**, and the layers under a
latency item are usually older than it is.

### 12. Only half of an image load was safe to move, and the other half changed the page

Item #8 is a latency-and-CPU item with an easy-looking shape: a document's images
are read and decoded one at a time, inline, so issue them all at once from a pool
before the layout pass and join before it starts. That is what shipped, and it is
worth **1.73–1.89×** on a 12-image document. The first version of it rendered a
different page, and what it got wrong is the part worth recording.

**A load has two halves and only one of them is arithmetic.** The expensive half
resolves the source, reads the bytes and decodes them. The other half is the
completion callback: it stores the image and its rectangle on the box, sets a 2px
error border when the image is null, and may ask the host to refresh. Moving the
whole load to a worker moves both — and the second half is *observable*, because a
box that acquires an error border before its width is resolved lays out differently
from one that acquires it after. Three broken `<img>` elements rendered **6–10
pixels wider** with the loads on workers than with them inline.

**Every document with working images was byte-identical, which is how this class of
defect survives a test suite.** The divergence lived entirely on the failure path,
because that is the path whose callback *does* something: on success the callback
stores two values the layout pass was going to read anyway, and the order it stores
them in relative to the pass is invisible. Ten of the eleven documents in the item's
test suite passed against the broken version. So the fix is not "handle failures
too" — it is to stop relying on which documents were tried:
`DeferredImageLoad` wraps the callback the host's loader is given, captures the
completion on the worker, and applies it from the inline call site the serial path
would have completed at. The real callback then runs on the layout thread, at the
same call site, in the same order, with the arguments the loader produced. There is
no document for which that can differ.

**The generalisation.** Item #5's lesson was *check what a stage is recomputing, and
what it is computing that nobody will see*. This one is the other half of moving
work earlier: **ask what the work notifies, not only what it computes.** A pure
function can be hoisted; a function that writes to the object its caller is about to
measure cannot, and the difference is invisible on every input whose write happens
to be idempotent with respect to the measurement.

**A second thing this item did not need, and the reason is reusable.** §6 predicted
#8 would consume item #17's image URL set. It does not, and should not: a preload
scan finds what a document's *source* names, which is a superset of what layout will
ask for — an `<img>` in a `display:none` subtree is in the source and is not a load
the document makes. The box tree, walked at the root after construction, names what
the consumer is about to walk. **Discover a resource set from the structure the
consumer will actually walk, whenever that structure exists in time.** For images it
does. For stylesheets, which #17 does feed, it does not: those are wanted before
there is a tree at all. That is the whole of the difference between the two items,
and it is why one scan does not serve both.

**Recorded in passing, and deliberately not fixed here:** a missing image file
completes with a null image and reports **nothing** to the host —
`ImageLoadHandler.SetImageFromFile` calls `ImageLoadComplete()` on the
`!source.Exists` branch without a `ReportError`, where every other failure path
reports. It also passes `async: true` there on a host that has asked for
synchronous loading, which is what makes a broken image request a refresh it should
not. Both are pre-existing, both live in `Broiler.HTML`, and neither is something a
concurrency item gets to change on the way past — a load that starts reporting
would change what a host is told about pages that render identically today. They
surfaced because this item's tests counted error reports, which is §11's lesson
again: an item that asserts a value audits the layer under it.

### 13. Item #3's sequential win was not the clip narrowing this document told it to port

[§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see)
ends by telling whoever ports item #5's work to item #3's rasterizer that the clip
narrowing is the part worth porting first. It was ported first, and it was not the
part that mattered.

**What the port is worth, decomposed.** Three builds of the same rasterizer,
measured by the same command in the same order at one thread, medians of 13
replays at 1 280×1 024:

| Scene | pinned | + target hoist | + clip narrowing | total |
|---|---:|---:|---:|---:|
| chrome | 184.2 ms | 103.0 | 99.1 | **1.86×** |
| list | 205.4 | 147.3 | 129.7 | **1.58×** |
| pane | 149.3 | 111.7 | 50.5 | **2.96×** |
| canvas | 825.5 | 366.7 | 360.4 | **2.29×** |

The middle column is not a feature anybody set out to build. `BCanvas.CurrentTarget`
is a property that reads the layer stack — `_layerStack.Count > 0 ? Peek().Bitmap :
_rootBitmap` — and every pixel loop in the file called it **per pixel**, as the
argument to `BlendPixel`. Banding forced it out: a band closure has to capture the
target once, because the layer a fill draws into must not be re-resolved while the
fill is in flight. So the largest sequential win in the port is a side effect of
making the loops parallelizable, collected without a thread ever running.

**The narrowing pays exactly where the surface clamp cannot.** On three of the four
scenes it is worth 1.02–1.14×, and the first reading of that was "the narrowing is
worth nothing here". It is worth **2.21×** on `pane`. The difference is not the
amount of clipped-away content, it is *where* that content is: every fill already
clamps its loop to the target's height and width, so a primitive below the bottom of
the surface costs nothing with or without a clip bound. What only a clip bound can
reject is content **on the surface and outside the clip** — a list beside a sidebar,
a table in a pane, any clip that is not the whole screen. The corpus scene that was
written to exercise the narrowing (`list`, a scrolled list overflowing far past the
viewport) turned out to be the case the clamp already handled, and it took a second
scene to see the effect at all. **A benchmark can fail to measure the thing it was
written for, and report a number rather than nothing when it does.**

**Threads add 1.00–1.39× on top, and not for want of splittable fills.** At four
threads against one: `canvas` 1.39×, `pane` 1.26×, `chrome` 1.08×, `list` 1.00×.
§4's explanation for the same shape on the HTML corpus was that nothing was large
enough to split — three of five pages split zero fills. That is *not* what is
happening here: these scenes split **85–100% of their pixel area**, because a UI
scene has surface-sized backgrounds where a text page has glyphs. The ceiling is
Amdahl instead. On `list`, 2.07 M pixels of fill sit inside a 130 ms replay; at the
rasterizer's ~21 ns per pixel that is a third of the time, and the other two thirds
are 2 400 primitives being transformed and rejected — per-primitive work no band
split touches. **Both copies now say band parallelism is not the shape that pays,
and they say it for opposite reasons**, which is worth more than either alone: it is
not a property of glyph-heavy content, it is a property of the unit.

**And the two-band split is a regression, reproducibly.** A budget of two threads
measured **437.7 ms against 362.9 ms sequential** on `canvas`, repeating to a tenth
of a millisecond across separate processes, while three bands measured 297.9 and four
270.4. A fill pays a join at the end of every band and a two-way cut buys one band of
overlap to pay for it, which does not cover the bill. So the ported partitioner
carries a floor the original does not: a fill that can only be cut two ways runs
inline. Without it every two-core host would run slower with the feature on than off
— and **the sibling in `Broiler.HTML.Image` has no such floor and shows the same
inversion** (corpus `paint`, 660.9 ms at one thread against 735.7 at two). Fixing it
there is a change to the rasterizer whose exit gate is a full WPT run, so it is left
named rather than folded in. It is the one open thread this item leaves behind.

**A methodological note that cost an hour and will cost the next person one.** A
process that replays only `canvas` measures it at 823 ms where the same build in the
same harness measures 362 ms, because that scene is 13 enormous fills and enters the
fill path few enough times to stay on OSR-compiled code; the 1 500 small fills of
`chrome` ahead of it are what promote it. Two figures from this rasterizer are
comparable only if the process did the same work before taking them. The
interleaving that `--raster-scaling` and `--decode-scaling` rely on was checked
against one-setting-per-process runs and does agree, so that convention is sound —
but it is sound by measurement now, not by assumption.

## What building Phase 3 changed

Twelve findings. The first two come out of the measurement Phase 2 §9 said the
document owed before item #12 could be started, and they change what item #12
*is*. §3–§5 come out of building it. §6 and §7 were about the two items this phase
had not yet built, and both were reports of a missing precondition rather than of a
difficulty — which is the useful thing to know about them. §8–§11 are what building
those two turned up: both preconditions turned out to be misdescribed, in the same
way, one section after the other. §12 finishes the item §11 left half-built, and it
is the first section in this document whose most useful result is a *defect* the
work found rather than a speedup it delivered — the exit gate for a skipped rebuild
turns out to be the first thing in this repository that ever laid the same box tree
out twice.

### 1. `parse+cascade` is a cascade stage; the name overstates the parse

Phase 2 §9 made this the largest unattributed question in the document: the stage
is the largest on four of five corpus pages, and item #12 must not be started
against a stage whose composition is unmeasured. Measured, at four raster threads
and four tiles, **before item #12 existed** — so this is the composition of the
stage as the roadmap had been carrying it, not of the stage as it is now (today's
profile splits the cascade row in two, `cascade (resolve)` and
`cascade (project)`, which is what §5 reads):

| Page | stage ms | html parse | css parse | cascade | box fixups | (untimed) |
|---|---:|---:|---:|---:|---:|---:|
| text | 90.6 | 2.4% | 0.4% | **81.3%** | 8.1% | 7.8% |
| rules | 2 813.1 | 0.5% | 0.1% | **98.2%** | 0.1% | 1.1% |
| boxes | 316.9 | 1.9% | 0.3% | **96.8%** | 0.7% | 0.4% |
| paint | 240.8 | 4.4% | 0.2% | **96.5%** | 0.3% | 0.0% |
| mixed | 114.9 | 2.2% | 0.3% | **96.2%** | 1.1% | 0.6% |

**The cascade is 81.3–98.2% of it and both parse halves together are 0.5–4.8%.**
The question the roadmap has been carrying — which half dominates — has an
answer, and it is not close on any page. Two consequences. Item #12 is aimed at
the whole stage rather than at a fraction of it, so the estimate in its row
("2–4× on styling") understates its reach: on `rules` the stage is 97% of the
render, so styling *is* the render. And the phrase `parse+cascade` should be read
as a legacy of when nobody had measured it; the parse it names is a rounding
error on every page in the corpus, including the one with 211 592 characters of
source.

The `text` row is the one that is not 96%+, and its 8.1% of box fixups is
`CorrectTextBoxes` calling `ParseToWords` on 240 paragraphs — real work, correctly
attributed, and not the cascade.

### 2. Measuring it needed instrumentation, and P0-a's method note is why that is worth saying

P0-a is explicit that its stage boundaries are public pipeline calls *and not*
instrumentation added to the engine, so that the profiler cannot drift from the
real path; and where a split had no public seam (raster vs. the paint walk) it was
derived by subtracting an out-of-band re-measurement of a pure function.

Neither technique reaches inside `SetHtmlWithStyleSet`. The four sub-stages are
private calls in a row, and **none of them is a pure function of the source** —
each consumes the box tree and the style set the previous one produced, so
re-running them out of band would measure four operations that never see each
other's state. So this is the first instrumentation in the profile:
`RenderStageTrace`, off by default, one static `bool` read and a null-check
`Dispose` when disabled, four `Stopwatch.GetTimestamp` pairs per render when
enabled.

The drift risk P0-a names is answered differently here rather than ignored: the
timers are *on* the real path, so there is no second implementation to drift.
What a reader has to check instead is that the scopes tile the stage without
overlapping — which is why the sub-rows are published against the measured stage
figure with their own `(untimed)` residual, on the same rule as the top-level
table. The 7.8% residual on `text` is a real gap — `InitialiseRoot` also loads
`@font-face` fonts and resolves font-feature values over every box after the
parser returns, and neither is inside a scope — and it is reported rather than
divided among its neighbours.

### 3. The `_sync` lock was not the bottleneck the item names, and the cascade's own cost is

Item #12's "What blocks it today" column says the global lock over the three memo
caches *becomes the bottleneck* and needs sharding plus a per-thread L1. The
sharding was worth doing and is done — the caches are `ConcurrentDictionary` now,
and the generation-guard protocol that makes the lock-free compute window correct
survives unchanged, which was the actual constraint. But the lock was never what
made the stage expensive. On `boxes` — a five-rule sheet over roughly 1 400
elements — the cascade cost **210 µs per element** before this phase. An
uncontended `Monitor` acquire is tens of nanoseconds; there is no number of cache
probes per element that turns that into 210 µs.

What costs is everything between the declared cascade and the projected value:
custom-property and `var()` resolution, CSS-wide keywords, shorthand expansion,
`attr()` substitution, relative font weights, and the ancestor recursion that
feeds them. That is per-element work, it was already memo-guarded at the wrong
granularity (`GetCascadedStyle` had no memo of its own; only its inner declared
cascade did), and it is what item #12 had to move.

**Consequence for the estimate column:** "2–4× on styling *after #11*" was written
as if #11 and #12 attack the same cost. They do not. #11 removed the rules that
cannot match; what is left is proportional to elements, not rules, and no rule
index makes it smaller.

### 4. The parallel unit is not the box walk — this is a prefetch/consume split, the fourth in this document

The roadmap's step 3 says "parallel style recalc over sibling subtrees, with a
per-thread cache that publishes to the shared cache at subtree completion", which
reads as threading `CascadeApplyStyles`. That walk cannot be split without
changing what it produces, and the reasons are not subtle: it inherits from the
parent box before touching a child, rewrites `display` when `float` is set so that
children observe the corrected value, pushes `text-decoration` down onto children,
hides a closed `<details>`'s subtree *after* that subtree has been cascaded, and
inserts generated `::before`/`::after` boxes into child lists on the way back up.
The last two write to nodes the walk has already left, which is not expressible as
an independent-subtree claim at all.

The expensive part is not in the walk. Per box the walk does a handful of field
assignments and one call to `CssStyleEngine.GetCascadedStyle`, and that call reads
the canonical DOM tree and the registered stylesheets — neither of which the box
walk mutates. It is a pure function of state that is already final. So item #12 is
built as **`CssStyleRecalc.Warm`**: resolve every element's cascade on *N* threads
first, leave the results in the engine's memo, and run the box walk afterwards
byte-for-byte as it was, reading cache hits instead of computing.

That is the same shape as item #2 (sub-resource fetch), item #8 (image decode) and
item #17 (preload scan), and it is the **fourth** item whose win came from
splitting a call site into prefetch and consume rather than from the parallel loop
its own row described. The raster and decode items (#3–#7) really are loops over
disjoint memory and really did get a `Parallel.For`; every item that had to move
work off an *ordered* path arrived at this shape instead, and none of them was
written expecting to. That is a pattern rather than a coincidence, and it is worth
stating before Phase 4 writes "parallel independent subtrees" and means it
literally.

Building it needed one thing the roadmap's step 3 did not: a store. `GetCascadedStyle`
memoized nothing at its own level, so a warm pass would have computed every
element's cascade and thrown it away. The fourth cache (`_cascadedStyleCache`)
exists for that and saves nothing on its own within a single render, because a
render asks for each element exactly once.

### 5. The serial residue is measured now, and it differs three-fold across pages

At four threads, pixel-identical to the sequential cascade on all five pages:

| Page | cascade 1T | cascade 4T | speedup | end to end | serial residue |
|---|---:|---:|---:|---:|---:|
| text | 77.2 | 66.6 | 1.16× | 1.08× | 28% |
| rules | 2 838.9 | 1 413.3 | **2.01×** | **1.96×** | 16% |
| boxes | 339.8 | 233.3 | 1.46× | 1.36× | 55% |
| paint | 262.7 | 166.4 | 1.58× | 1.12× | 48% |
| mixed | 120.9 | 96.2 | 1.26× | 1.08× | 41% |

The last column is what the new `--style-scaling` mode exists to report: the box
walk's share of the pair once the warm pass has done its work, which is Amdahl's
serial fraction **measured rather than assumed**. Read it as the ceiling on adding
*cores*: at 16% on `rules`, a machine with more of them still has up to 6× left to
give before the walk becomes the floor; at 55% on `boxes` there is at most 1.8×
however many cores are added, and the 1.46× already measured is most of it.
Threading the walk is a different and much harder lever — §4 is about why — and
its payoff is the mirror image: near-nothing on `rules`, most of what remains on
`boxes`. A scaling table without this column would have reported "1.46× at four
threads" on `boxes` and "2.01× on `rules`" and left a reader unable to tell which
page is worth returning to, or with which change.

The whole table reproduces. A second run at seven iterations reads 1.17× / 2.16× /
1.35× / 1.43× / 1.27× against the 1.16× / 2.01× / 1.46× / 1.58× / 1.26× above, with
the residue column within two points on every page.

Two honest notes. `text` is *slower* at four threads than at two (66.6 against
63.4) — inside the run-to-run spread, and the second run does not reproduce the
ordering, so the claim worth making is the weaker one: on a page whose whole
cascade is 77 ms, the fourth worker on a four-core box returns nothing measurable. And the end-to-end column divides the
stage win by whatever else the page does: `paint` gains 1.58× on the cascade and
1.12× on the render because two thirds of that render is raster. That is the same
arithmetic that made item #4 worth 1.42× end to end, and items have to be compared
in the same column.

### 6. Item #16 is blocked on a store, not on concurrency — the same shape as §4, found earlier

> **Superseded by [§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache).**
> The reasoning below is kept because it is the reasoning the item was scheduled
> on, and because the half of it that is right — a compile needs no live context —
> is what made the item cheap once the other half was checked. The blocker it
> names does not exist.

The item's "What blocks it today" says the code cache *is already concurrent,
which is the hard part*, and it is: `DictionaryCodeCache` is a
`ConcurrentDictionary` whose per-key compilation is serialised by `Lazy<T>`, and
`CoreScript.Compile` already takes an explicit `codeCache` and explicit
`compilationOptions`, so a compile does not need a live context.

What is missing is the store. `JSContext` builds `Options.UseProcessSharedCodeCache
? DictionaryCodeCache.Current : new DictionaryCodeCache(Options.CodeCache)`, and
nothing on the render path sets that flag — the one place in the repository that
does is a performance test. So every document's scripts compile into a cache
created inside the context's own constructor, which does not exist yet when the
script sources become known, and `JSContextOptions` can pass cache *options* but
not a cache *instance*. A compile-ahead worker today has nowhere to put its
results.

That makes item #16 a two-part change whose first part is not about threads at
all: give `JSContextOptions` a cache instance (or make the bridge take the
process-shared cache, which is a cross-document isolation decision and needs a
full WPT run of its own), and only then queue the compiles. **The estimate and the
risk in its row should be read as covering only the second part.**

### 7. Item #14 has no measurement it can be started against, and building it first would be building it blind

Item #14's own estimate column already says the quiet part: **"5–50× on
interactive relayout — unmeasured, and the *first-render* layout it bounds is
0.6–6.5% of wall time; the interactive case this claims is not covered by the P0-a
corpus."** Phase 3 did not change that, and nothing built in Phases 1 or 2 changed
it either. The corpus renders each page once, from a clean container, at a fixed
viewport; dirty bits bound the *second* layout, and there is no second layout in
anything this repository measures.

The sequential prerequisite the Broiler.Layout roadmap lists as step 1 — "stop
laying out the whole tree twice" — is in the same position and for a reason
already recorded in Phase 1 §2: the second `Root.PerformLayout` is guarded by
`MaxSize.Width <= 0.1`, and every headless entry point sets `MaxSize`. It costs a
second traversal only in the auto-size shrink-to-fit case, which is not on the
WPT, CLI or WebAssembly paths.

So the precondition for item #14 is a **relayout benchmark** — a harness that
renders a document, mutates it the way script does (a class toggle, an inline
style write, an inserted subtree), and lays out again — and P0-a does not contain
one. That harness is small next to the item (15–20 days) and it is what turns
"5–50×" from a hope into a number. **Recorded as the first thing Phase 3's
remainder should build, ahead of any dirty bit.**

> **Built — see [§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half).**
> `--relayout-profile`. It turned "5–50×" into a number and moved the item: the
> magnitude is there (34× on the rule-heavy page) but it is in the box-tree
> rebuild and the cascade, not the layout pass this section assumed. **The item's
> first slice was then built against it**
> ([§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise)),
> which is what this section was asking for: the harness came first and the number
> chose the target.


### 8. Item #16's blocker did not exist: the store is the context's own cache

[§6](#6-item-16-is-blocked-on-a-store-not-on-concurrency--the-same-shape-as-4-found-earlier)
recorded item #16 as blocked, and the master table copied the finding into its
"What blocks it today" cell as *"a store, before a queue"*. The premise was
checked and is correct: `JSContext` builds its cache in its own constructor, and
`JSContextOptions` carries cache *options* rather than a cache *instance*. The
conclusion drawn from it — *"a compile-ahead worker today has nowhere to put its
results"* — does not follow, for a reason visible on the next screen of the same
file: **`JSContext.CodeCache` is a public settable property.** The store exists
the moment the context does.

**And the context is not late.** The shape §6 was reaching for is a worker
started *before* the context, at preload-scan time, which is why it wanted the
cache to precede it. But a document's script sources are not all known until the
external fetches have returned, and those are consumed by the host's own
extraction loop, which finishes after the context is built. So there is nothing
for a pre-context worker to compile: the earliest honest start is exactly where
the context already is, and from there the compiles overlap the parse, the DOM
build and the execution of every script ahead of them.

Three consequences, and the third is the one worth carrying forward:

1. **No engine change, no submodule patch.** The whole item is one main-repo
   type plus a two-line call site — the reverse of the situation the "keep the
   patch small" note in `CLAUDE.md` exists to engineer, arrived at for free.
2. **The cross-document isolation question §6 raised does not arise.** It was a
   consequence of the *other* option §6 floated (making the bridge take the
   process-shared cache); a per-context cache handed to a per-document worker has
   exactly the lifetime it had before, so no WPT run of its own is owed.
3. **This is the fourth item in a row whose stated blocker was not the operative
   one** — #8 (§6 of Phase 2), #12 (§3 and §4), and now #16. The pattern is not
   that the blockers were wrong when written; it is that they were written from
   the item's *description* rather than from the code, and none of them cost more
   than an hour to check. The master table's "What blocks it today" column should
   be read as a hypothesis, and checking it should be the first hour of any item
   it gates — not the last.

**What the item does still owe its restrictions to is real, and it is not the
cache.** The compiler reads direct-eval state from the ambient context
(`FastCompiler`'s constructor, via `JSEngine.Current`), and the cache key does
*not* carry it. For a top-level script that state is "none" on both threads — a
worker has no ambient context at all — so the two agree by construction. An
`eval()` body's would not, which is why `ScriptCompileAhead` is offered document
script sources and nothing else, and why that restriction is stated in the type
rather than left to a caller to infer.

### 9. The compile stage's ceiling is not the thing that looks like it

Item #16's compile stage scales **1.41× / 1.62× / 1.52× at 2 / 4 / 8 threads** on
a four-core host. Saturating at four is right; stopping at 1.62× rather than
approaching 4× is not obviously so, and this document's habit would have been to
publish the number with a plausible cause attached.

There was an unusually good candidate. `CompilationStack.Run` moves every
compilation onto a second, engine-sized pooled thread and **blocks the caller**,
so each concurrent compile occupies two threads rather than one — on four cores
at budget 4 that is eight threads for four cores of work. It also has an
environment opt-out, so the hypothesis was testable in one run rather than
arguable. With `BROILER_JS_COMPILE_STACK_BYTES=0` the ceiling is **1.69× against
1.62×** — inside the run-to-run spread.

**So the handoff is not what bounds it, and the cause is recorded as
unattributed.** The remaining candidate this document can name is the GC:
compilation is allocation-heavy, and item #19 measured Workstation as the faster
mode *for a whole render* without asking what a compile-bound parallel section
prefers. `--gc-config` is the harness that would settle it, and that is the next
measurement rather than a guess to publish now.

**The experiment did turn up a number, and it is a sequential one.** Turning the
handoff off takes the serial stage from 4 491.7 → 3 742.9 ms and the four-thread
stage from 2 779.5 → 2 364.7 ms — **15–17% either way**, far more than the
~180 µs per compile `CompilationStack` documents for itself. That is not a
recommendation to change the default: the engine sizes that stack because a
front-end stack overflow is not a catchable exception on .NET, so the tax buys a
host that does not abort on a deeply nested script. It belongs to whoever repairs
`StackGuard` — the real fix `CompilationStack` says it is only buying time for —
and it is recorded here because it is the largest single figure this item's
measurement produced, and it would have gone unmeasured had the ceiling been
explained instead of tested.

### 10. Item #14's harness exists now, and it says the item is aimed at the smaller half

[§7](#7-item-14-has-no-measurement-it-can-be-started-against-and-building-it-first-would-be-building-it-blind)
recorded that item #14 had no measurement to be started against, and named the
harness: render a document, mutate it the way script does, lay out again.
`--relayout-profile` is that harness, over the P0-a corpus, with four mutations
per page — a class toggle, an inline-style write, a text write and an inserted
subtree. Published:
[`tests/render-stages/results/relayout-profile.md`](../../tests/render-stages/results/relayout-profile.md).

**What a relayout is in this engine is not what the item's wording assumes.**
`HtmlContainerInt` keeps a bound `DomDocument` and a copy of its `Version`;
`EnsureBoundDocumentCurrent` compares them at the top of every `PerformLayout`
and, when they differ, calls `BuildBoundDocument`, which **disposes the render
tree and regenerates it** — box tree and full cascade — before the layout pass
runs at all. So the stage item #14 names is the last and smallest part of what a
relayout costs:

| | layout pass | rebuild | rebuild share |
|---|---|---|---|
| `rules` | 42.2 ms | 1 404.2 ms | **97.1%** |
| `paint` | 47.5 ms | 207.4 ms | 81.4% |
| `boxes` | 40.8 ms | 291.5 ms | 87.7% |
| `mixed` | 45.3 ms | 125.0 ms | 73.4% |
| `text` | 96.1 ms | 191.6 ms | 66.6% |

**Three things follow, and the first is a correction to the item.**

1. **Dirty bits on `CssBox.PerformLayout` bound 3–39% of a relayout, and 2.9% of
   the worst one.** Eliminating the rebuild takes the `rules` page's relayout from
   1 446 ms to about 42 ms (**34×**); a perfect layout dirty bit alone takes it to
   1 404 ms (**1.03×**). The item's "5–50×" estimate turns out to be roughly right
   about the *magnitude available* and wrong about *where it lives*. Its row and
   its Broiler.Layout roadmap entry should be read as naming the box tree and the
   cascade, not the layout pass.
2. **The engine cannot tell the four mutations apart.** All four bump the version
   counter by one and all cost the same to within noise, on every page. There is
   no granularity to exploit yet: the only signal is "something changed". Item #14
   therefore begins in `Broiler.DOM`, giving mutations a shape a consumer can act
   on, and not in `Broiler.Layout` — which makes it a submodule change first, with
   everything the patch workflow implies.
3. **The interactive case is worse than the first-render case, not a cheaper
   version of it.** `rules` lays out in 53 ms and relays out in 1 446 ms — 27×. The
   document has said since Phase 1 §2 that layout is 0.6–6.5% of a *first* render
   and used that to defer Phase 4; this is the first evidence that the second
   render has a completely different profile, and it is a cascade profile, which is
   the same place items #11 and #12 already found the first render's cost.

**The harness deliberately does not cover two cases**, and they are the two that
would make item #14 look best: a burst of mutations coalesced into one layout,
where the rebuild amortises and the layout share rises; and a mutation that
changes nothing observable, where a correct scheme costs zero and today costs a
full rebuild. Both belong to whoever picks the item up — adding them now would be
choosing the fixture that flatters the conclusion before the conclusion is being
tested.

> **Both are covered now, and one of the two predictions in that paragraph is
> wrong** — see [§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise).
> The burst does not amortise (the rebuild is whole-document for one attribute
> write, so twenty cost what one does), and "changes nothing observable" turns out
> to mean the *detached* case, because a same-value write never reaches the version
> counter at all. Point 2 above is also wrong about where the item starts.

### 11. Item #14's blocker did not exist either, and the burst does not amortise

[§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half)
re-aimed item #14 correctly and then misidentified its first step. Its point 2
reads: "The engine cannot tell the four mutations apart… Item #14 therefore begins
in `Broiler.DOM`, giving mutations a shape a consumer can act on, and not in
`Broiler.Layout` — which makes it a submodule change first, with everything the
patch workflow implies."

The observation is right and the conclusion does not follow. The DOM has published
a typed record for every edit since before the item was written:

```csharp
public event Action<DomMutationRecord>? Mutated;   // DomDocument.cs
```

with `DomMutationType` (`ChildList` / `Attributes` / `CharacterData` / `Adoption`),
the target node, added and removed nodes, previous and next sibling, attribute name
and namespace, and old and new value. `MutationObserver` is built on it, and
`DomRange` and `DomNodeIterator` subscribe to it. What was missing was a
**consumer**. This was the whole of the container's invalidation:

```csharp
private void EnsureBoundDocumentCurrent()
{
    if (_boundDocument != null && _boundDocumentVersion != _boundDocument.Version)
        BuildBoundDocument();
}
```

A version compare, standing next to a feed of exactly the records it needed, calling
the most expensive function in a relayout. **This is the fifth item whose stated
blocker was not the operative one** — #8, #12 twice, #16, and now #14 — and
[§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache) drew the
lesson from the fourth: *"the 'What blocks it today' column is a hypothesis, and
checking it should be the first hour of any item it gates — not the last."* §10 was
written in the same phase as §8 and did not take it. The hour it would have cost
there is the hour it cost here.

#### What a consumer can prove today, and what it is worth

`Broiler.Layout.Engine.RenderTreeInvalidation` subscribes to the feed and
classifies each record by one rule: **does its target hang off the bound
document?** The box tree is generated by walking that document and nothing else, so
a node rooted anywhere else — a `DocumentFragment`, a `<template>`'s inert
contents, an orphaned subtree — contributes no box, and neither do its descendants.

`ChildList` records name the *parent*, which is what makes the test sound in both
directions and is the trap it could have fallen into: nodes added to a detached
parent are themselves detached, and a node moved *out* of the page is reported
against the still-connected parent it left, not against the node that has gone. An
implementation that looked at insertions would have elided a mutation that empties
part of the page.

**The backstop is what makes the elision safe against the case the rule cannot
see.** The ledger records the document version each record arrived at, and answers
"rebuild" unconditionally when the counter has moved further than the records
account for — an unobserved publish path, an earlier subscriber that threw before
this one ran, a document mutated before it was bound. An elision is only possible
when every bump since the last build is accounted for; every classification failure
falls towards the behaviour that was there before.

**Measured, paired, on one host** ([published in
full](../../tests/render-stages/results/relayout-profile.md)): the offscreen-build
row goes from a full rebuild to none on every corpus page — `rules`
**1 032.7 → 11.5 ms (89.8×)**, `boxes` 25.8×, `paint` 22.6×, `mixed` 16.3×, `text`
10.0×. The other thirty rows span **0.82–1.27**, which is this host's run-to-run
spread with no page and no mutation systematically on one side: the ledger changes
what is skipped, not what is done. `Broiler.Cli.Tests` was run in full both ways on
the same host and the two failure sets are **identical name for name** — 2 931
tests, 82 failures each way, none added and none fixed, all 82 pre-existing on the
pinned pointer.

#### Two things the measurement said that the item did not

**The burst does not amortise.** §10 offered the coalesced burst as a case "where
the rebuild is amortised across them and the layout share rises". Twenty connected
writes against one: the layout share is **2.4% and 2.5%** on `rules`, and flat or
slightly lower on `boxes`, `paint` and `mixed`. There is nothing to amortise,
because the rebuild is a *whole-document* re-cascade for a single attribute write —
twenty of them cost exactly what one does. The prediction assumed a per-mutation
cost the engine does not have, and the case is worth keeping precisely because it
turns that into a null result rather than an expectation.

**"Changes nothing observable" is already free at the value level, so it is not the
case §10 meant.** `Broiler.DOM` returns before publishing when an attribute or text
write does not change the value, so the version never moves and no rebuild was ever
going to happen. The reachable form of that case is the *detached* one, which is
why that is the one the ledger elides.

#### What is left, and it is larger than what was taken

A `data-*` write that no corpus selector can reach still costs **997.8 ms** on the
`rules` page, because the classification cannot yet ask whether any rule's subject
could match differently. That question is invalidation sets over the rule index item
#11 built, and answering it is the rest of item #14 — worth roughly what the
connectivity rule was worth, on the mutations scripts actually perform. The item's
remaining estimate should be read against that row.

**One process note, because it nearly published a backwards table.** The harness's
new `rebuilt?` column was two-valued at first — rebuilt or elided — and the baseline
run is against a `Broiler.HTML` that consults the ledger nowhere, so both counters
stay at zero and every row of the run where *everything* rebuilt printed as
`ELIDED`. A diagnostic that cannot say "no decision was recorded" will say the
opposite of the truth on exactly the run it exists to be compared against.

### 12. Item #14's second half: the sheets already knew, and the gate caught a bug nothing else could

[§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise) left
the item with a named remainder and a number for it: a `data-*` write that no corpus
selector can reach still cost **997.8 ms** on the `rules` page, because the ledger
could ask whether a mutation was *connected* and nothing else. Answering the other
question — could any rule match differently — is
`Broiler.Layout.Engine.CascadeInvalidationSet`, built from the sheets the render tree
was last cascaded from and installed by the same call that marks the tree current.

**Three sources, and the second is the one an implementation would miss.** Selectors
are obvious: every class, id and attribute name mentioned anywhere in a selector's
text. `attr()` is not — `content: attr(data-label)` makes a `data-*` write change the
rendering with no selector mentioning it at all, so declaration values are scanned
too. The third is the escape hatch: an escaped identifier, a namespaced attribute
(`[svg|href]`), anything the scanner does not model makes the whole set conservative
and every attribute affecting, which is the behaviour that was there before it.

**The scan is flat, and that is what makes it sound under combinators.** An attribute
write changes what a selector matches if the selector mentions that attribute
*anywhere*, not only in its subject compound: `.a .b` is affected by a class write on
an ancestor, `[hidden] + p` by one on a sibling, `:has([open])` by one on a
descendant. So the set files every name it sees, including inside `:not()` and
`:is()`, and the test is pure membership — a name that appears nowhere cannot change
any match, whatever the combinators between it and the subject. No combinator is
modelled, and none needs to be.

**The cascade's answer is not sufficient on its own, and this is the half the item's
wording does not contain.** Box construction reads attributes for reasons that never
go through a selector: `src`, `href`, `colspan`, and the presentational attributes
`DomParser.TranslateAttributes` maps onto box properties. A set built from stylesheets
knows nothing about any of that, so it would happily elide a `<td colspan>` write on a
page whose sheet never mentions `colspan`. The elision therefore needs *both* halves
to say no, and the box half is an **allow-list** — `class`, `data-*`, `aria-*`,
`role` — because a deny-list puts every attribute nobody thought of on the elidable
side, and the cost of missing one is a stale page.

Two carve-outs inside that allow-list are worth stating because both were found by
reading the engine rather than by reasoning about the web platform:

- **`data-broiler-*` is not inert.** The custom-data namespace is inert by
  definition, and then the engine helped itself to part of it: `data-broiler-scroll-top`,
  `-top-layer`, `-backdrop`, `-frame-document`, `-anchor-cb`, `-part` are how the script
  bridge tells the layout engine things it has no other channel for. Eliding those would be
  eliding the engine's own messages to itself.
- **Nothing inside an `<svg>` subtree is inert.** `FragmentTreeBuilder` serializes an
  inline `<svg>` back to markup *attribute by attribute* and hands the string to a
  renderer with its own idea of what a `class` or a `data-*` means. The allow-list is a
  statement about this engine's HTML box construction and does not transfer, so foreign
  content is excluded wholesale.

**`id` is deliberately not on the list**, and it is the case that looks most elidable
and is not: with no `#id` selector there is no cascade dependency at all, but
`HtmlContainerInt` reads `id` off boxes to build the link map a PDF export writes and
`DomUtils` resolves a fragment anchor by scanning boxes for it. An elided `id` write
leaves those answering from the old tree — a wrong answer rather than a slow one.

#### What it is worth, and the two rows that say it is the sheets doing the work

Paired on one host, the pinned `Broiler.HTML` against the same tree with both halves
applied ([published in full](../../tests/render-stages/results/relayout-profile.md)).
The row §11 sized at 997.8 ms — one `data-*` write no selector reaches — is elided on
every page: `rules` **1 476.7 → 43.0 ms (34.4×)**, `paint` 14.2×, `mixed` 13.8×,
`boxes` 9.5×, `text` 2.1× (whose remainder is the layout pass, which is most of what a
relayout costs on a page of prose). A class toggle joins it wherever the sheet does not
name the token: **36.0× on `rules`**, 12.2× on `paint`, 9.4× on `mixed`. Rows that
still rebuild span 0.79–1.21, this host's spread.

**The harness grew two control rows with the feature, and they are what make the rest
of the table readable.** "The unstyled write went to zero" is equally consistent with
an implementation that elides *every* `data-*` write, which would be a stale page
rather than a fast one. So `styled attribute` writes `data-k` — the attribute `rules`
builds 180 of its 900 selectors around — and `styled class` writes a class the page's
own sheet names. `rules` is the only page whose sheet mentions `data-k`, and the only
page where `styled attribute` still rebuilds; `boxes` is the page whose deepest element
carries a styled class, and the only page where `class toggle` still rebuilds. Same
mutation, opposite decision, decided by the document.

#### The gate failed the first time it ran, and the bug was older than the item

The exit gate is `--relayout-parity`: render every corpus page after every mutation
twice, once with the elision on and once with it off, and compare the images byte for
byte. A unit test over the classifier agrees with the implementation's own reasoning
and therefore cannot say whether that reasoning is right *about the engine*; only a
rendered page can. It also fails a run in which nothing was elided, because a green run
that compared no elisions is how a gate stops being one.

It failed three `boxes` rows — and one of them, `detached build`, is elided by the
**first** slice, which shipped in `patches/0131`. So the failure could not be the new
classification. The no-mutation control found it: on that page, laying the same box
tree out a second time produced a different image from laying it out once, with no
mutation at all. Reduced, the whole of it is `<div style="margin-top:2px">` directly
inside `<body>`. `CollapsedMarginTop` records what the previous margin collapse
decided; a first in-flow child's top margin propagates by shifting its parent down
only when the child's margin exceeds what the parent has already absorbed; read from
the *previous pass*, that comparison fails and the shift does not happen. **The
document lays out two pixels shorter the second time.**

**Nothing in this repository had ever laid the same box tree out twice.** Every
relayout disposed the render tree and rebuilt it, so every layout got a fresh box and
a pass-dependent result could sit in the margin code indefinitely — correct on the
only path that existed. Item #14 is what creates the second path, and the two-pixel
drift was live in the first slice for as long as that slice has been upstream. The fix
is a reset at the top of each box's own layout (`CssBox.ResetCollapsedMarginState`),
kept by `LayoutIdempotenceTests`, including a case asserting the margin is still
*applied* — the tempting fix makes both passes agree on the short answer. The
render-bearing half of `Broiler.Cli.Tests` (Acid, WPT, `GoogleSearchPolyfill`,
form-control and CSSOM — 928 tests) was run in full both ways on one host and the two
failure sets are **identical name for name**: 41 failures each way, 40 unique names,
none added and none fixed.

**The lesson generalises past this bug**, and it is the one this section is worth
keeping for: an optimisation that *skips* work is a new execution path, and its gate
has to exercise that path end to end. Every other item in this document was gated on
"the parallel answer equals the sequential answer" over the same code; this one is the
first whose gate compares two *different* paths through the engine, and the first
run of it found a defect the whole test suite had never had a reason to look for.
`BROILER_RENDER_TREE_ELISION=0` is the switch that separates the two paths outside the
harness, which is where a suspected stale render should be bisected first.

#### What is left of item #14

A **scoped** rebuild rather than a skipped one. Everything still rebuilding is a
mutation the engine genuinely has to react to — an inline-style write, a text change,
an inserted subtree, an attribute a rule really does match on — and it reacts by
regenerating the whole box tree and re-cascading the whole document for a one-element
change. Narrowing that needs the rebuild to have a unit smaller than the document,
which neither the ledger nor the set provides, and it is a different kind of work from
either half of this one.

## What building Phase 4 changed

Nothing parallel yet, deliberately. The phase's own row said to start it only once the
measurements say layout is still worth aiming at, so the first thing built was the
measurement — and it changed the phase twice before a line of parallel layout was
written.

### 1. Layout's share tripled without layout changing, and the number this row quoted was measured before two phases of work

The sequencing table justified deprioritising this phase with "layout is 0.6–6.5% of a
first render", citing Phase 1 §2. That figure was taken before Phase 2 and Phase 3
landed. Re-running the stage profile on today's code:

| page | render ms | layout ms | share | share as published (pre-#12) |
|---|---:|---:|---:|---:|
| text | 252.07 | 50.59 | **20.1%** | 19.3% |
| boxes | 325.32 | 45.16 | **13.9%** | 11.9% |
| mixed | 224.51 | 28.64 | **12.8%** | 14.2% |
| paint | 822.82 | 33.00 | **4.0%** | 3.7% |
| rules | 1 368.87 | 44.58 | **3.3%** | 1.5% |

**Layout is 3.3–20.1%, not 0.6–6.5%.** The important half is *why*, and it is visible in
the absolute column rather than the share: layout costs 28–51 ms, which is what it cost
before — 51.56, 43.42, 50.60, 31.40, 31.83 in the published profile against 50.59, 44.58,
45.16, 33.00, 28.64 now. **Layout did not get slower; everything else got faster.** The
`rules` page is the clearest case: item #12 took it from 2 898.69 ms to 1 368.87, and
layout's share of it went 1.5% → 3.3% without the layout pass moving at all.

This is Amdahl arriving on schedule, and it is a standing hazard for a document
organised as a ranked backlog: **every share in this document decays as the items above
it land, and a phase gated on a share has to re-read it rather than cite it.** Phase 1 §2
was right when it was written and is wrong now, and nothing in the process would have
caught that, because a superseded measurement here is kept deliberately (it is what a
decision was made on) and nothing distinguishes "kept for the record" from "still true".

What it does **not** do is rescue item #13. A 20% stage at the roadmap's own estimate of
1.5–2.5× is ~8–12% end to end on the one page where the stage is largest, for 20–30 days
at High risk — against a `parse+cascade` that is still 23–81% of every render on the same
five pages. The finding changes the *argument*, not the ranking: the phase is still last,
but it is last for a reason that is currently true.

### 2. The double layout is unreachable from every path this repository measures, and where it fires it is not a doubling

The `Broiler.Layout` roadmap orders its own steps "do the sequential work first" and
states that **steps 1 and 2 are worth more than steps 3 and 4 combined**. Step 2 is
item #14 and is done. Step 1 — "stop laying out the whole tree twice" — was never
measured, only read out of the source, and the source reading is correct:
`HtmlContainerInt.PerformLayout` runs `Root.PerformLayout` a second time whenever
`MaxSize.Width <= 0.1`, so the first pass can find the shrink-to-fit width the second
lays out against.

The question that decides the step's size is not in the source: `MaxSize` is set by the
**caller**, so which hosts reach the branch is a property of the hosts. `--layout-passes`
counts the passes at the site (`patches/0133`, four lines) across the three caller shapes
that exist here. Published:
[`layout-passes.md`](../../tests/render-stages/results/layout-passes.md).

**Every fixed-viewport row is one call and one pass.** The WPT runner, the CLI capture,
`Broiler.Browser.Core` and every benchmark in `tests/render-stages` set a viewport width,
so none of them has ever executed the branch. The step the roadmap ranks above both
parallel steps is worth **exactly nothing** on every path this document has ever
measured. It fires on the auto-size embedding path — `HtmlRendererUtils.Layout(autoSize:
true)` — which is a real case that no number in this document covers.

**And where it fires it is not the doubling the step assumes.** Two passes cost
**0.80–1.35×** one pass, not 2× (`text` 42.41 → 53.36 ms, `boxes` 41.13 → 52.81,
`paint` 38.09 → 51.51, `rules` 49.47 → 60.44, `mixed` 41.23 → 33.06 — the last below 1.0,
inside the spread). The reason is structural: the first of the two passes runs at width
99999, where almost nothing wraps and the line breaker — the dominant cost of a layout
pass on a text-heavy page — barely runs. **The pass step 1 proposes to cache away is the
cheaper of the two.** The shape that actually multiplies is
`MeasureHtmlByRestrictions`, at three passes across two calls on the two pages whose
unrestricted width exceeds the viewport (`text` 139.84 ms against 42.41 — 3.3×); if step 1
is ever built, that is the shape to aim it at, and not the one the step names.

So step 1 is retired as written rather than built. That makes **six** items in this
document whose row named the wrong thing — five blockers, and now one beneficiary.

### 3. The harness nearly published a false positive, the same way Phase 2 §7's did

Measured shape by shape — all of `viewport`'s iterations, then all of `autosize`'s —
`text | viewport` read **136.80 ms** in one run and **83.83 ms** in the next with no code
change between them, and the `viewport → autosize` ratio inverted between the two. Both
also disagreed with the stage profile, which measures that configuration at ~51 ms.

The cause is already written down in this document. Phase 2 §7 records a null result that
was nearly a false positive because a configuration measured *outside* an interleave was
compared with ones measured inside it, and concludes: **a median is only comparable with
another median taken under the same drift.** This host drifts by tens of percent over tens
of seconds. Interleaved iteration by iteration — which is what `DecodeScaling` does, for
this reason — two consecutive runs agree and `text | viewport` (40.21 / 50.93 / 42.41 ms
over three runs) agrees with the stage profile.

Worth stating because the failure recurred in a harness written by someone who had read
the section warning about it. The tell was cheap and general: **a number moved when
nothing did.** The counts, being structural rather than timed, were correct in every run —
which is the argument for measuring a structural question with a counter rather than
inferring it from a time.

### 4. Phases 2 and 3 are worth nothing on a WPT run, and the pages are why

Phase 1 §4's "90 minutes → 45" is a `--workers 1` vs `--workers 4` ratio, and its note
above now records the thing that makes the ratio's *endpoints* interesting: `ca53d44`
contains Phase 2 item #9 and nothing else, so both endpoints are an end-of-Phase-1
engine. Every sequential win since — #5's off-screen elision, #10's glyph cache, #3's
per-pixel target lookup, #12's fourth memo, #14's invalidation — postdates the number,
and none of them was ever measured on WPT. The expectation was that they had moved the
absolute endpoints down and the ratio had simply divided them out of both sides.

**They have not moved them.** Baseline `ca53d44` against today's HEAD, both at
`--workers 4` on 4 cores — where HEAD's own header reports `Render threads: 1 per
worker`, so both trees are sequential per render — run A,B,B,A so drift cancels:

| suite | reftests | baseline (s) | head (s) | head/baseline |
|---|---:|---|---|---:|
| `css/css-backgrounds` | 713 | 368.5, 367.1 (367.8) | 377.3, 371.1 (374.2) | 1.017 |
| `css/css-fonts` + `css/css-writing-modes` | 1 500 | 781.3, 788.0 (784.7) | 792.1, 797.3 (794.7) | 1.013 |

Classification is identical on every run of both suites (443/267/1 and 685/815/0).
**The 1.3–1.7% is not a regression**: the same HEAD, same command, ninety minutes later
read 393.7 / 399.4 s on the first suite — 5–7% off itself, so the deltas are inside the
host's drift. What is *outside* the drift is the size of the thing being looked for.
The two suites were picked to disagree if page character mattered —
`css-backgrounds` is the best case for the raster items, `css-fonts`/`css-writing-modes`
for the glyph cache — and they agree.

**The mechanism is the pages, and it is not overhead.** The run is CPU-bound at 359% of
4 cores (90% utilization), and per-process fixed cost is 1.69 CPU-s — 1.8% of the run it
was subtracted from. What is left is ≈2.2–2.5 CPU-s per reftest, ≈1.1 CPU-s per render,
roughly flat across two unrelated test sets. And the documents are **1 018 bytes at the
median** (p90 1 895, max 8 518, n = 1 168) against a corpus of 20 102–211 592 — *every*
corpus page is larger than *every* WPT page in that directory, and a 77 KB corpus page
renders in ~250 ms where a 1 KB WPT page costs ~1.1 CPU-s.

So the per-render cost here is dominated by something that does not scale with the
document, and every surviving sequential win does: #5 needs a page taller than the
viewport (a reftest is written to fit one), #10 needs repeated glyphs, #12 needs many
elements, #14 needs a *second* layout, and #3's win is in `Broiler.Graphics`' rasterizer,
which [§2](#2-the-rasterizer-the-profile-measures-is-item-4s-copy-not-item-3s) already
records is not the copy this path uses. The wins are real on the corpus, which was built
to load each stage, and have nothing to act on here.

**One hypothesis was tested rather than published.** The obvious candidate for the small
negative sign was item #14 charging bookkeeping a suite that never relayouts can only
pay for. With its own switch: `BROILER_RENDER_TREE_ELISION=0` **399.4 s**, `=1` **393.7 s**
— turning it off is not faster, so that is not the cause.

**Scope, stated rather than implied:** this is the reftest suite, not the golden-image
suite 90/45 comes from, and 2 213 of the corpus's 19 398 reftests were measured, not all
of them — a full A/B is ~6 h on this host and two independent suites already agree
inside the drift. The negative conclusion is what transfers, because its mechanism is
page size, a property of WPT documents generally rather than of the directories sampled.

**What it makes actionable is not in this document.** The run is CPU-bound at ~1.1 CPU-s
per render of a 1 KB page, flat in document size — per-render *fixed* cost inside the
engine, which no item here has measured or aimed at. That is the target for WPT wall
clock, and it is a different investigation from anything Phases 0–4 contain. Published:
[`wpt-sequential-wins.md`](../../tests/render-stages/results/wpt-sequential-wins.md).

### 5. The engine's per-render fixed cost is 3.5 ms, and §4's closing sentence was wrong

[§4](#4-phases-2-and-3-are-worth-nothing-on-a-wpt-run-and-the-pages-are-why) ended by naming
per-render fixed cost "inside the engine" as the target for WPT wall clock. That was an
inference from a null result, and measuring it took one harness and refuted it.

**The engine side.** `--render-fixed-cost` sweeps document size through
`HtmlRender.RenderToImageWithStyleSet` at 1024x768 — the call `WptTestRunner` makes — and fits
`ms = 6.06 + 0.19 * boxes`. An **empty** document renders in **3.48 ms**, a WPT-median one
(~1 KB) in **15–19 ms**. Three quarters of the empty render is allocating and clearing the
3 MB bitmap (2.32 of 3.02 ms); `SetHtmlWithStyleSet` is 0.58, layout 0.09, paint 0.02, the
container constructor and disposal both 0.00. Nothing there is within two orders of magnitude
of the ~1.1 CPU-s per render §4 inferred.

**The runner side.** `--phase-trace` (in `Broiler.Wpt`, single-process so wall ≈ CPU) says where
a test's time really goes, on two subsets picked to differ:

| phase | `css-backgrounds/animations` (41) | `css-fonts` (373) | ms/call |
|---|---:|---:|---|
| **scripts + DOM bridge** | **76.0%** | **79.3%** | 928 / 704 |
| **pixel compare** | **20.5%** | **15.9%** | 501 / 327 |
| **render** | **1.6%** | **2.0%** | 19.5 / 17.7 |
| file read, fonts, post-process, diagnostics | <0.2% each | <0.1% each | |
| attributed | 98.3% | 97.3% | |

**The render is 1.6–2.0% of a WPT test**, and its 19.5 / 17.7 ms per call independently
reproduces the first harness's 15–19 ms by a completely different method — which is the reason
to believe either. What costs the run is `ExecuteScriptsWithDom` (build a DOM, run the classic
scripts, re-serialize — twice per reftest) and `PixelDiffRunner.Compare`, which walks 786 432
pixels through a per-pixel `GetPixel` on both bitmaps and a `SetPixel` on a diff bitmap for
*every* pixel, and allocates that diff bitmap unconditionally before discarding it on the ~62%
of tests that pass. That accessor pattern is the same shape item #3 already found and fixed in
the rasterizer, where hoisting the per-pixel lookup was worth 1.58–2.96× by itself.

**So §4's mechanism was right and its conclusion was not.** Phases 2 and 3 measured zero on WPT
because they were aimed at 2% of the run — and only at the page-proportional part of that 2%.
No engine work, parallel or sequential, could have shown up.

**The reasoning error is the part worth keeping.** §4 ruled out I/O and process startup, found
the remaining cost was CPU and flat in document size, and concluded it was fixed cost *in the
engine*. "Flat in document size" was equally consistent with fixed cost in the **harness**,
which is where it was. Eliminating two candidates does not confirm a third — and this document
has now made the same class of mistake twice in one phase, after
[§3](#3-the-harness-nearly-published-a-false-positive-the-same-way-phase-2-7s-did) caught the
first. Published:
[`render-fixed-cost.md`](../../tests/render-stages/results/render-fixed-cost.md).

### 6. The pixel comparison is fixed, and the part that read worst was not the part that cost

[§5](#5-the-engines-per-render-fixed-cost-is-35-ms-and-4s-closing-sentence-was-wrong) named two
targets outside this document. The smaller one is done: **`PixelDiffRunner.Compare` goes
284.61 → 4.50 ms on the match path (62×)** and 285.23 → 7.92 on the mismatch path (36×).

**The fix was not where the code looked worst.** The per-pixel `GetPixel`/`SetPixel` loop over
786 432 pixels — the thing §5 singled out, and the same shape item #3 had already fixed in the
rasterizer — is real, and it was **~22 ms of 284**. The other **92% was `NormalizeForComparison`**:
`BBitmap.Decode(source.Encode(Png, 100))` on *both* inputs before a pixel was read, a full PNG
compress and decompress of 3 MB each, per comparison. So §5's own diagnosis was half right in
the same way §4's was — the mechanism it named existed, and was not the cost.

**The round trip was an identity, and this time it was measured before anything was deleted.**
A synthetic opaque image, one with graded alpha, one fully transparent with non-zero RGB — the
two cases where a PNG codec may premultiply or collapse the colour type — and 25 real WPT
reference PNGs off disk all round-tripped byte-identical. It could not have been otherwise:
`Encode` serialises the same `_pixels` array `GetPixel` reads, so a lossless round trip has
nothing to normalise between two `BBitmap`s.

Three changes, in descending order of what they were worth: the inputs are compared directly;
the loop reads backing spans instead of a call and a `BColor` 1.57 M times; and the diff bitmap
is built **only when the comparison failed**, instead of being allocated and written for every
pixel and discarded on the match path — 3 MB and 786 432 `SetPixel` calls nothing looked at, on
the ~62% of tests that pass.

On the suite: `css/css-backgrounds` reftests at four workers, **368.5 → 297.3 s (1.24×)** — less
than the 186 CPU-s removed, because four workers divide it. Classification identical (444/266/1,
failing sets identical name for name) and `Broiler.Wpt.Tests` 748/57 both ways.

**The verification produced one false alarm worth keeping.** The first post-fix run differed by
three `background-size` tests. Restoring normalization while keeping the loop rewrite reproduced
*the same three*, which cleared both changes; running the **pristine** tree under the same
invocation then produced exactly the fix's 266-failure set. They are a pre-existing
nondeterministic fringe in the suite, and the runs they had been compared against were taken
back-to-back inside one script invocation. **A before/after taken under different invocation
conditions is not a before/after** — the same lesson as
[§3](#3-the-harness-nearly-published-a-false-positive-the-same-way-phase-2-7s-did), arriving
through a different door.

`patches/0134`. Published:
[`pixel-compare.md`](../../tests/render-stages/results/pixel-compare.md). **What is left of §5's
two targets is the larger one**: `ExecuteScriptsWithDom` at 76–79% of a WPT run.

### 7. Half a WPT run is publishing the DOM API, and the DOM build is 0.6 ms

[§5](#5-the-engines-per-render-fixed-cost-is-35-ms-and-4s-closing-sentence-was-wrong)'s larger
remaining target was `ExecuteScriptsWithDom` at 76–79% of a WPT run. Profiled sub-phase by
sub-phase, on two unrelated subsets:

| phase | `css-backgrounds/animations` (41) | `css-fonts` (373) | ms/call |
|---|---:|---:|---|
| **scripts + DOM bridge** | **79.3%** | **78.2%** | 683 / 651 |
| ├ script scan + sheet inlining | 0.0% | 0.0% | 0.30 / 0.12 |
| ├ JSContext construction | 0.8% | 0.6% | 7.14 / 4.95 |
| ├ **DomBridge.Attach** | **52.0%** | **54.9%** | 448 / 457 |
| · ParseHtml (DOM build) | **0.1%** | **0.0%** | **0.61 / 0.40** |
| · **RegisterDocument (DOM API surface)** | **50.6%** | **53.6%** | **436 / 446** |
| ├ script eval + drains | 25.0% | 20.5% | 216 / 171 |
| ├ load event + snapshots + anchors | 1.1% | 1.9% | 9.9 / 16.1 |
| └ SerializeToHtml | 0.1% | 0.2% | 0.57 / 1.81 |
| render | 1.1% | 1.7% | 9.5 / 14.4 |
| attributed | 98.8% | 97.2% | |

**`DomBridge.RegisterDocument` is 50.6–53.6% of a whole WPT run** — publishing the document, the
window and the DOM API surface onto a fresh `JSContext`, ~440 ms per document and twice per
reftest. It is the largest single item measured anywhere in this investigation, larger than the
render, the pixel comparison and script execution combined.

**The phase above it is misnamed for what it does, which is why this needed measuring rather than
reading.** "`DomBridge.Attach`" sounds like a DOM build, and [§5](#5-the-engines-per-render-fixed-cost-is-35-ms-and-4s-closing-sentence-was-wrong)
described it as one. The DOM build is **0.61 / 0.40 ms — 0.1% and 0.0%**. Attach is 99.9% API
registration. That is the fourth time in this phase that the operation an item is named after was
not the operation that costs.

**And it is fixed cost, measured rather than inferred**: 435.86 ms/call on one subset against
445.80 on the other — 2% apart across unrelated directories and 9× the test count — while the
parse that *does* scale with the document stays under a millisecond. A WPT reftest document is
1 018 bytes at the median, so nearly all of what a test costs is the engine rebuilding the same
API surface from nothing, twice, for a page with almost nothing in it.

Two incidental findings from reading the path: the
`scripts.Count == 0 && deferredScripts.Count == 0` early-out in
`WptTestRunner.ExecuteScriptsWithDom` is **unreachable** (two `scripts.Insert(0, …)` calls run
before it, so the list is never empty), and `SerializeToHtml` — the DOM-back-to-markup step this
architecture is built on — is **0.57 ms**, so the round trip through markup is not what costs.

**Not attempted, deliberately.** Making `RegisterDocument` cheaper is a design change to the
bridge — lazy or cached host-object registration, or reusing a context across documents where
isolation permits — not a profiling result, and per-document isolation is exactly the property a
WPT runner must not lose. Published:
[`script-dom-phase.md`](../../tests/render-stages/results/script-dom-phase.md).

### 8. `RegisterDocument` was recompiling the bridge's own JavaScript per document — 422 ms → 13.7 ms

[§7](#7-half-a-wpt-run-is-publishing-the-dom-api-and-the-dom-build-is-06-ms) named
`DomBridge.RegisterDocument` as half a WPT run and stopped at profiling. Profiled one level
further, the phase splits into seven steps and **all seven fall by the same factor**, which is the
shape of one shared cause rather than a hot spot:

| step | before (ms/call) | after (ms/call) |
|---|---:|---:|
| document object | 106.66 | 6.67 |
| window basics + fetch | 61.25 | 1.66 |
| content-rendering polyfills | 61.73 | 1.65 |
| security/constructor polyfills | 148.49 | 2.12 |
| window→global mirror | 40.54 | 1.36 |
| window globals, performance/navigator/viewport | 3.41 | 0.09 |
| **RegisterDocument** | **422.10** | **13.74** |

**The cause is compilation, not execution.** Registration evaluates a fixed set of *bridge-owned*
JavaScript sources — the content-rendering polyfill asset, the `DOMException`/`Node`/`SVGLength`
constructors, `XMLHttpRequest`, the mutation-observer and event shims, the window→global mirror.
Every document gets a fresh `JSContext`, a fresh context builds its own `DictionaryCodeCache`, and
so every one of those sources was parsed and compiled again from nothing, per document, twice per
reftest. Installing the process-shared cache for the duration of the call — and restoring the
context's own afterwards — removes 97% of the phase while changing nothing about what the sources
do when they run. **That the swap alone accounts for it is the evidence that the cost was
compiling.**

**The scope is the call, deliberately, and this is the interesting design point.** The engine
already offers `JSContextOptions.UseProcessSharedCodeCache`, which applies the shared cache to
*everything* a context evaluates including page script — a far larger claim, and one that would
put one document's compiled code where the next document's evaluation can find it. A WPT runner is
exactly where that must not happen. Nothing here needs it: inside `RegisterDocument` every
evaluated source is a compile-time constant owned by the bridge assembly — **verified rather than
assumed**, no reachable `Eval` takes an interpolated or page-derived string — and page script does
not run until the host's loop, after `Attach` returns. Inline event handlers, which *are*
page-controlled, compile at dispatch time and still use the context's own cache. So the shared
cache holds a bounded set of strings that ship in the assembly and cannot grow with documents
rendered.

**On the suite: `css/css-backgrounds` reftests 368.5 → 195.7 s, 1.88×** — 173 s off a six-minute
subset, with `patches/0134` unapplied, so the two fixes are independent and compose.

**Correctness:** failing-test set **identical to the pristine tree, name for name** (266) and
stable across two runs, classification 444/266/1 unchanged, and `css-fonts` + `css-writing-modes`
unchanged at 685/815. `Broiler.Wpt.Tests` goes 748/57 → **750/55 with nothing newly failing**; the
two that flip are both `RunTestWithTimeout_*_Completes_Without_Timing_Out`, which is the fix doing
its job rather than noise.

Published: [`register-document.md`](../../tests/render-stages/results/register-document.md).
**Where that leaves a WPT run:** the render was 1.1–1.7% before any of this and the two largest
terms have now been cut; what remains largest is script eval.

### 9. The same fault in two more places, and the boundary that stopped it spreading

[§8](#8-registerdocument-was-recompiling-the-bridges-own-javascript-per-document--422-ms--137-ms)
left script eval as the largest remaining phase. Split into its parts, most of it was not the
document's scripts:

| step | calls | before (ms/call) | after (ms/call) |
|---|---:|---:|---:|
| **injected stubs** | 164 | **64.49** | **1.24** |
| page scripts | 92 | 41.99 | 43.60 |
| **window→global sync** | 253 | **11.98** | **0.12** |
| drains | 335 | 1.30 | 1.81 |
| **script eval** | 82 | **240.15** | **82.48** |

**The stubs are the runner's own constants** — `BrowserApiStubs` (~10 KB), `TestharnessStubs`
(~4.8 KB) and a one-line flag, all `private const string` — recompiled for every document: **28.5%
of a whole WPT run, 52×**. **The window→global sync is a constant mirror source the host
re-evaluates after every script** — 253 calls across 41 reftests, recompiled each time, **100×**.
Same fault as §8 in two more places, fixed the same way.

**The interesting part is where the fix stops.** Page scripts are 43.60 ms/call and are now the
largest item in the phase, and they are deliberately left alone: they are page content, and
sharing their compiled form across documents is precisely the cross-document path a conformance
runner must not create. The measurement confirms the boundary held — 41.99 before, 43.60 after.
Every swap in §8 and here wraps a single evaluation of a *named constant*, so what the shared
cache can hold is a fixed, bounded set of strings shipped in the assemblies, and no page-derived
source can reach it.

**Cumulative on `css/css-backgrounds` reftests: 368.5 → 195.7 → 108.6 s, 3.39×**, with
`patches/0134` still unapplied. Failing set identical to pristine name for name (266) and stable,
`css-fonts`/`css-writing-modes` unchanged at 685/815, `Broiler.Wpt.Tests` 748/57 → 750/55 with
nothing newly failing (the two that flip are the timeout tests, and the suite runs 10 m → 3.5 m).

**Where a WPT run now stands, against the item this document is about**: the render — everything
Phases 0–3 optimised — was 1.1–1.7% of a run before any of this began, and the four things that
actually cost were a PNG round trip, and three copies of "recompile a constant for every
document". None of them is a threading problem, and none was in this document. Published:
[`script-eval.md`](../../tests/render-stages/results/script-eval.md).

### 10. Item #18's gate passes, on both halves — and one of them is the half nobody was asking about

The roadmap does not schedule Web Workers until the P0-c static-state audit clears them, and #18's
row asserts the other side ("per-context state exists … so isolation is feasible"). Both are claims
about code, so the gate is built here as something that runs.

**Correctness.** `JSContextIsolationTests` — five cases, four real threads each owning a
`JSContext` for its lifetime (item #18's proposed shape), released together on a barrier, each case
aimed at a named piece of process-wide state: globals not leaking, interned key strings and shape
transitions under identical property names, identical sources compiled at maximum contention, the
process-shared code cache, and the built-in registry's static initialization. **All five pass**, and
each run asserts the threads *actually overlapped*, because a concurrency test whose threads run one
after another passes for the wrong reason.

**Throughput, which is the half a correctness test cannot reach.** An engine holding one global lock
would pass every assertion above and still make a worker useless — the point of a worker is to run
*while* the main context runs, so **correct-but-serialized is the outcome that would sink the item**.
`--js-context-scaling`, one context per thread, same CPU-bound workload each, compiled before the
clock:

| code cache | 1 thread | 2 threads | 4 threads |
|---|---:|---:|---:|
| per-context | 1.00× | 1.82× | **2.66×** |
| process-shared | 1.00× | 1.88× | **3.22×** |

**The premise holds.** Contexts are isolated under real concurrency and run genuinely in parallel.
Item #18 is not gated on an engine that would serialize it.

**The second finding is about this phase's own work.** §8/§9 routed the bridge's and the runner's
constant sources through the process-shared `DictionaryCodeCache.Current`, which is safe today only
because those hosts render on one thread per process — under #18, several threads reach it at once.
It is not just safe under contention, it **scales better** than the per-context cache (3.22× against
2.66×), which is what work that stops being repeated per context should do. A blocker introduced by
this phase's own optimisation would have been an unpleasant thing to find after building the
feature on top of it.

**What this is not**: the feature. `Worker`, `MessageChannel` and structured-clone message passing
are unwritten and the 30–40 day, High-risk estimate stands; the workload exercises the shared state
the roadmap names, not the bridge, timers, promises or modules. Published:
[`js-context-concurrency.md`](../../tests/render-stages/results/js-context-concurrency.md).

### 11. `MessageChannel` was already built; the unverified word was "cross-context"

Item #18's row scopes the work as "New: `Worker` / `MessageChannel`", state "Not implemented", with
structured-clone message passing as the shape to build. Checking before building: `MessageChannel`,
`MessagePort`, entanglement, transfer lists, the pending queue, `MessageEvent` and
`window.postMessage` are all in `MessagingBinding` with three test files over them — and **both**
`window.postMessage` and `MessagePort.postMessage` already run their payload through
`CloneForMessaging`, which calls the engine's own `structuredClone` (itself complete, down to
Date/RegExp/Map/Set/ArrayBuffer and cycles). **Seventh row in this phase whose stated state was not
the operative fact.**

**What was genuinely unverified is whether a clone crosses a realm.** Everything above clones and
delivers *within one context*, which is all a same-document channel needs. A worker is the other
case, and a clone that quietly produced a sender-owned graph would pass every existing test while
being exactly the cross-realm leak a worker must not have. That is testable without threads, so it
was: `CrossContextStructuredCloneTests`, six cases, all passing — values copied with no identity
shared, sender mutation not reaching the receiver (and the reverse), cloned objects answering to the
**receiving** realm's `Array`/`Date`/`Map`/`Object.prototype`, functions refused, cycles preserved.

**The sixth case is what makes the third mean anything**: it asserts the two contexts have *distinct*
intrinsics, and that an array handed directly from A into B answers **false** to B's
`instanceof Array`. Without it, a shared `Array` would have made every realm assertion true
regardless of which realm built the clone. So the structured-clone half of item #18 is **done**, and
now has a standing gate.

**What is left is small and named.** The clone is made on the *sender's* side — `CloneForMessaging`
runs before `QueueFrameAction`, with the sender's context current — which is correct only because
both ends of a port share a realm today. A worker changes exactly that: the clone must be produced
with the *receiving* context current, and delivery must hop to the receiving thread's event loop
rather than the sender's frame-action queue. The rest is the `Worker` object itself — a thread
owning its context and loop, a port pair straddling them, and termination rules. Published:
[`message-channel-slice.md`](../../tests/render-stages/results/message-channel-slice.md).

### 12. `Worker` is built, and the design content was where the clone happens

Item #18's remaining piece. `new Worker(url)` starts a thread that owns a `JSContext`, runs the
worker script in it and pumps messages; the page gets `postMessage`/`onmessage`/`onerror`/
`addEventListener`/`terminate`, the worker gets `self`/`postMessage`/`onmessage`/
`addEventListener`/`close`/`console`.

**The design content is not the thread — it is where the clone happens.** Two obvious designs are
both wrong. Cloning once on the sender and handing the result over puts one realm's graph in another
thread's hands. Cloning once on the receiver from the sender's *live* value is worse: the sending
script keeps running and can mutate that graph while the receiver walks it, which is a data race on
engine internals. So a message is cloned on the **sending** thread into a graph no script can reach,
and cloned again on the **receiving** thread with the receiving context current. The intermediate is
unreachable from either side, so nothing can mutate it while it is read — and the first clone is
also what makes post-send mutation invisible and raises `DataCloneError` on the sender, where it
belongs. Both clones are the engine's own `structuredClone`; a second implementation of the walk
would have been a second definition of which types survive.

**Item #15 is kept rather than bent.** Each context is still driven by one thread and one loop, and
nothing dispatches JavaScript from a foreign thread: a worker's reply is *queued* onto the page's
`BrowserEventLoop` (a `ConcurrentDictionary`, so enqueuing across threads is safe) and the page's own
drain runs it. Pending frame actions count as pending work, so a reply in flight keeps the host's
drain alive instead of racing the end of the document.

**Verified** by twelve `WorkerBindingTests` cases driving the real bridge — round trip; cloning proved
in *both* directions by mutating each side after sending; `Date`/`RegExp`/arrays/nesting/cycles
surviving; realm separation plus asynchronous delivery; a missing script firing `error` without
throwing; and `terminate()` plus disposal joining threads promptly.

**The regression risk was feature detection, and it did not fire.** Adding `Worker` to the global can
silently change which path a WPT test takes. `css/css-backgrounds` is **identical name for name**
(266 failures, 444/266/1), `css-fonts`/`css-writing-modes` unchanged at 685/815, and
`Broiler.Wpt.Tests` unchanged at 750/55.

**Timers are in, and they are deliberately not the page's loop.** `setTimeout`/`setInterval` (shared
id space, interchangeable clears, `(deadline, seq)` ordering) run on **real** deadlines, because the
page's `BrowserEventLoop` is explicitly virtual — "not wall-clock… only the relative ordering of
deadlines" — which suits a loop drained in bounded bursts and breaks one that is pumped continuously:
a worker's `setInterval(fn, 1000)` under a virtual clock has its deadline reached the moment the loop
looks at it, so it would spin hot forever. The pump therefore waits for whichever comes first, an
inbound message or the next deadline, and an idle worker with no timers blocks. The determinism this
costs is confined to the worker — a page's timers still fire virtually, and a page only ever observes
a worker through messages it must wait for. Two of the six timer cases are the ones that catch a
wrong pump: a live interval must not starve the inbox, and `terminate()` must win over a repeating
timer rather than wait for it to go quiet.

**Out of this slice, refused rather than half-built**: `importScripts`, module workers,
`SharedWorker`, nested workers, `requestAnimationFrame` in a worker, transferables (an `ArrayBuffer`
in a transfer list is cloned, not transferred), and network-fetched worker scripts. Published:
[`worker-object.md`](../../tests/render-stages/results/worker-object.md).

## Master table

Gain is per-stage unless stated. Effort is engineering days for one person
familiar with the component. Risk is the chance of introducing a
non-deterministic correctness defect.

| # | Component | Site | Currently | Parallel shape | What blocks it today | Est. gain (unmeasured) | Risk | Effort | Phase |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Tooling | [`Broiler.Wpt/Program.cs`](../../src/Broiler.Wpt/Program.cs) `RunDiscoveredTests` — **DONE** | Pool of *N* worker processes draining a shared queue (`--workers <N>\|auto`) | Work queue over *N* worker processes; results already buffered into `allResults` and sorted after the run, so order is not load-bearing | Nothing. Worker protocol is already one-command-per-line JSON over stdio | **Measured: 1.93× at 4 workers** on a 61-test subset (45.2 s → 23.4 s); identical classification at 1, 4 and auto. Bounded by RAM, not cores | Low | Done | 1 |
| 2 | HtmlBridge | [`ScriptExtractionService.cs:303`](../../src/Broiler.HtmlBridge.Core/Scripting/ScriptExtractionService.cs), [`ResourceLoader.cs:56`](../../src/Broiler.HtmlBridge.Dom/Runtime/ResourceLoader.cs), [`SubDocuments.cs:581`](../../src/Broiler.HtmlBridge.Dom/DomBridge/SubDocuments.cs) | Serial `GetStringAsync(..).GetAwaiter().GetResult()`, one resource at a time | Bounded-concurrency prefetch (6/host, browser convention) into a content-addressed cache; call sites then hit the cache | Call sites are synchronous and ordering-sensitive for `document.write`; needs a prefetch/consume split, not an `async` conversion | **Done for scripts and `<link>` stylesheets** (`SubResourcePrefetcher`, bounded 6/host), and since #17 **in the capture host too** — it extracts scripts itself and so reached none of this wiring, leaving its round trips serial ([§10](#10-item-17s-win-was-not-the-scan-it-was-a-host-that-never-reached-item-2s-split)). Iframes and `fetch()`/XHR are still not wired: #17 now supplies their URL set, but the sub-document consume site yields `(content, contentType)` through a policy chain the text prefetcher cannot serve | Medium | Done (scripts, sheets) | 1 |
| 3 | Graphics | [`Rendering/BCanvas.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/BCanvas.cs) — **DONE** | Was single-threaded scanline loops that re-resolved the layer target per pixel and walked pixels the clip could never admit; now clip-narrowed loops split into row bands via `BRasterParallelism` | `Parallel.For` over scanline bands; per-band coverage buffer, and a running intersection of the including clips to bound the loop | Nothing | **DONE, and the sequential half was worth more than the threads — but not the sequential half this document named.** Per-scene, one thread: `chrome` 184.2 → 99.1 ms, `list` 205.4 → 129.7, `pane` 149.3 → 50.5, `canvas` 825.5 → 360.4 (**1.58–2.96×**). Four threads add 1.00–1.39× on top, with 85–100% of pixel area split — so unlike item #4 the ceiling here is Amdahl (per-primitive work), not fill size. A two-band split is a measured regression and is refused ([§13](#13-item-3s-sequential-win-was-not-the-clip-narrowing-this-document-told-it-to-port)) | Low | Done | 2 |
| 4 | HTML | [`Broiler.HTML.Image/BCanvas.cs`](../../Broiler.HTML/Source/Broiler.HTML.Image/BCanvas.cs) — **DONE** | Was the same scanline shape; now `Parallel.For` over row bands via `BRasterParallelism`, with a measured area threshold and a per-band coverage buffer in `FillGlyphContours` | Nothing | **DONE, and smaller than the estimate.** Corpus `paint` page 1 594.7 ms → 1 096.5 ms (**1.42×** end to end, 1.61× on the stage) at 4 threads; **flat on `text`, `rules` and `boxes`, which split 0 fills between them** — their raster is glyphs, and a 95-pixel fill is not splittable at any core count. Pixels identical at 1/2/4 on all five pages. See [§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page) | Low | Done | 2 |
| 5 | Graphics / Layout | [`DisplayList.cs`](../../Broiler.Layout/Broiler.Layout/IR/DisplayList.cs) replayed by [`RGraphicsRasterBackend`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/RGraphicsRasterBackend.cs) — **DONE** | Was one pass over the whole surface; now `Parallel.For` over horizontal strips via [`TileParallelReplay`](../../Broiler.Layout/Broiler.Layout/IR/TileParallelReplay.cs), each replaying the whole list into its own strip | Nothing | **DONE, and the estimate was right about the ceiling for the wrong reason.** `PerformPaint` at 1 → 4 tiles: `paint` **1 323.7 → 461.4 ms (2.87×)**, `rules` 3.49×, `text` 2.42×, `mixed` 2.44×, `boxes` 1.76× on a 4-core box — and it beats band parallelism on all five pages. But on the pages taller than their viewport — which is most documents — a large share of it came from *not drawing invisible pixels* rather than from threads ([§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see)). Pixels identical at 1/2/4 tiles × 1/4 bands; WPT output identical to the line | Low–Medium | Done | 2 |
| 6 | Media | [`JpegDecoder.cs:385`](../../Broiler.Media/Broiler.Media.Image.Managed/Jpeg/JpegDecoder.cs) dequantize+IDCT per block, `:424` upsample + YCbCr→RGB per row | Sequential | `Parallel.For` over blocks / rows. Entropy decode stays sequential (optionally per-RST-interval) | Nothing structural; blocks and output rows are disjoint | **DONE. Measured 2.08–2.61× at 4 threads** (gradient / flat-block fixtures, 1 024²), which lands inside the estimate. Byte-identical at 1, 2 and 4 threads. `JpegDct.Inverse` also took a caller-owned scratch buffer, removing a 512-byte allocation per block | Low | Done | 2 |
| 7 | Media | [`PngDecoder.cs:293`](../../Broiler.Media/Broiler.Media.Image.Managed/Png/PngDecoder.cs) expand-to-RGBA | Sequential | `Parallel.For` over rows | Inflate (`:62`) and unfilter (`:63`) are genuinely sequential — Up/Paeth read the previous row | **DONE. Measured 1.22–1.29× at 4 threads**, just under the estimate: inflate and unfilter are a larger share of a PNG decode than the estimate assumed, and both are on the do-not-parallelize list | Low | Done | 2 |
| 8 | Media / HTML — **DONE** | The document-wide walk in [`CssBox.ImagePrefetch.cs`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.ImagePrefetch.cs), consumed at `MeasureWordsSize`; loads still run through [`ImageLoadHandler`](../../Broiler.HTML/Source/Broiler.HTML.Core/Handlers/ImageLoadHandler.cs) | One worker per image, up to `BROILER_IMAGE_PREFETCH_THREADS`, joined before the layout pass | Nothing. **Neither blocker this cell named was the one that mattered.** `BImageRenderer._images` was cleared by #9; the shape was decided by every headless entry point setting `AvoidAsyncImagesLoading`, which makes decode synchronous and inline ([§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor)). It needed neither #17's URL set nor a cache, and it needed one thing the row could not have predicted: the completion callback has to stay on the layout thread ([§12](#12-only-half-of-an-image-load-was-safe-to-move-and-the-other-half-changed-the-page)) | **Measured: 1.89× of `PerformLayout`, 1.73× end to end** at 4 concurrent loads on a 12-image fixture (1.80–1.96× / 1.70–1.81× over four runs). Sub-linear in image count, not near-linear: the decodes already split into bands, so the cores were not idle | Low | Done | 2 |
| 9 | Graphics | [`FontsHandler.cs`](../../Broiler.Graphics/Broiler.Graphics/Text/FontsHandler.cs), [`TrueTypeFont.cs`](../../Broiler.Graphics/Broiler.Graphics/Text/TrueTypeFont.cs), [`FallbackSystemFont.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/FallbackSystemFont.cs), [`BImageRenderer.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/BImageRenderer.cs) — **DONE** | Plain `Dictionary`, no synchronization; and two lazy-init latches published before their values | Not a speedup itself — a **hard prerequisite** for #10, #12, #13, and per Phase 1 §2 for *every* CPU-parallel render-path item | Nothing now | **DONE.** Nested map flattened to one concurrent dictionary; image table concurrent and handle allocation interlocked; replay transform state moved per-call; `TrueTypeFont`'s five lazy tables re-published through `Lazy` in `ExecutionAndPublication`. Both P0-c residuals closed with it. Enables ~4 items | Low | Done | 2 |
| 10 | Graphics | [`TrueTypeFont.GetGlyphContours`](../../Broiler.Graphics/Broiler.Graphics/Text/TrueTypeFont.cs) — **DONE**; [`ComplexTextShaper.Shape:72`](../../Broiler.Graphics/Broiler.Graphics/Text/ComplexTextShaper.cs) — not built | Outlines were re-extracted per glyph *occurrence*; shaping is called per run during layout | Concurrent cache by glyph index, published through `GetOrAdd` | Nothing | **The item was right that the cache is the whole win, and wrong about which cache.** Glyph outlines: raster stage **1.34×** (`text`), **1.54×** (`boxes`), 1.00× on the text-free `paint` control. The shaped-run cache is deliberately unbuilt — `RequiresShaping` is false for the whole Latin corpus, so it would measure nothing ([§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names)) | Low | Done (outlines) | 2 |
| 11 | CSS | [`CssStyleEngine.CollectFromRules:623`](../../Broiler.CSS/Broiler.CSS.Dom/CssStyleEngine.cs) — linear scan of every rule of every sheet, per element | O(elements × rules) | **Not multithreading.** Rule index (bucket by id/class/tag) + ancestor bloom filter | Nothing — this is the standard engine design and it is simply absent | **DONE.** Exit gate met: with matches fixed at four, 32× the rules now costs 1.64× the time and 1.13× the bytes (was 30.8× / 32.0×) — up to **136.9× faster** and **600× less garbage** at 3 200 rules. On a whole render the corpus `rules` page is 5 218.96 ms → 1 841.71 ms (2.8×); see [What building Phase 1 changed](#what-building-phase-1-changed) §1. `patches/0123-css-cascade-rule-index.patch` | Low | Done | 1 |
| 12 | CSS | [`CssStyleEngine.GetCascadedStyle`](../../Broiler.CSS/Broiler.CSS.Dom/CssStyleEngine.cs) resolved ahead of the box walk by [`CssStyleRecalc`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/Parse/CssStyleRecalc.cs) — **DONE** | Was sequential per element, with the three memo caches under one global `_sync` | Warm pass over every element on `BROILER_STYLE_THREADS`, then the unchanged ordered box walk reads the memo; caches sharded to `ConcurrentDictionary`, generation guard kept | Nothing now. **Neither blocker this cell named was the operative one.** The lock was not the bottleneck — the per-element cascade was 210 µs on a five-rule page ([§3](#3-the-_sync-lock-was-not-the-bottleneck-the-item-names-and-the-cascades-own-cost-is)) — and the parallel unit is not the box walk, which cannot be split at all ([§4](#4-the-parallel-unit-is-not-the-box-walk--this-is-a-prefetchconsume-split-the-fourth-in-this-document)) | **Measured: 1.16–2.01× on the cascade stage at 4 threads, 1.08–1.96× end to end**, pixel-identical at 1/2/4. The serial residue is now measured per page (16–55%), so what is left is stated rather than guessed ([§5](#5-the-serial-residue-is-measured-now-and-it-differs-three-fold-across-pages)) | Medium | Done | 3 |
| 13 | Layout | [`CssBox.PerformLayout:347`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.cs), [`PerformLayoutImp:37`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.Layout.cs) | Full-tree, in-place mutation, from the root every pass — **twice** when width is unrestricted ([`HtmlContainerInt.cs:929,936`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/HtmlContainerInt.cs)) | Parallel intrinsic sizing; parallel independent subtrees (abspos/fixed, flex+grid items, table cells, multicol, subdocuments) | Mutable shared tree; ambient thread-static state (`CssLengthParser` viewport, [`DocumentModeContext.cs:22`](../../Broiler.Layout/Broiler.Layout/DocumentModeContext.cs)); no dirty-bit invalidation to bound the work | **1.5–2.5×** of a stage **measured at 3.3–20.1% of a render** — re-measured on today's code, where the share is up from the 0.6–6.5% this cell used to quote **not because layout got slower but because Phases 2–3 made everything else faster** ([§1](#1-layouts-share-tripled-without-layout-changing-and-the-number-this-row-quoted-was-measured-before-two-phases-of-work)). ~8–12% end to end on the one page where the stage is largest, against `parse+cascade` still at 23–81% of the same renders | **High** | 20–30 d | 4 |
| 14 | DOM / Layout | [`RenderTreeInvalidation`](../../Broiler.Layout/Broiler.Layout/Engine/RenderTreeInvalidation.cs) and [`CascadeInvalidationSet`](../../Broiler.Layout/Broiler.Layout/Engine/CascadeInvalidationSet.cs) consulted by `HtmlContainerInt.EnsureBoundDocumentCurrent` — **done**; a *scoped* rebuild is the remainder | Was: **any** DOM version bump disposes the render tree and re-cascades the whole document, then lays out the whole tree | **Not multithreading.** A consumer for `DomDocument.Mutated`, then invalidation sets over the rule index | **Neither blocker this cell named was the operative one, and that is now five items running.** The DOM's signal was never a bare counter — `DomDocument.Mutated` has published a typed `DomMutationRecord` since before the item was written; what was missing was a consumer, and it is in the main repo, not `Broiler.DOM` ([§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise)). What blocks the **remainder** is real and different: eliding a *connected* mutation needs the cascade to answer whether any rule's subject could match differently | **Measured, both halves: the offscreen-build case goes from a full rebuild to none — `rules` 1 032.7 → 11.5 ms (89.8×)**, `boxes` 25.8×, `paint` 22.6×, `mixed` 16.3×, `text` 10.0×; and the connected `data-*` write the first half could not touch goes **1 476.7 → 43.0 ms (34.4×)** on `rules`, 14.2× `paint`, 13.8× `mixed`, 9.5× `boxes`, 2.1× `text`, with a class toggle 36.0× on `rules`. Rows that still rebuild span 0.79–1.21, i.e. run-to-run spread. A perfect layout dirty bit alone remains worth 1.03× | Medium–High | Done; `patches/0132` | 3 |
| 15 | JS | [`JSPromise.Post:376`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Promise/JSPromise.cs), [`JSAsyncFunction.cs:152`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Function/JSAsyncFunction.cs), [`JSGenerator.cs:435`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Generator/JSGenerator.cs) — `ThreadPool.QueueUserWorkItem` when `sc == null` | JS continuations run on pool threads, racing main-thread layout | **Remove the parallelism.** Always pump a single-threaded event loop | This is the root cause behind WPT #1445 / #1143; the CSS `_sync` lock and the concurrent bridge memo maps are mitigations for it | Negative CPU gain, **large correctness gain**; removes lock overhead on hot cascade paths | Low | 5–8 d | 0 |
| 16 | JS | [`ScriptCompileAhead`](../../src/Broiler.HtmlBridge.Core/Scripting/ScriptCompileAhead.cs), consumed by the eval loop in [`CaptureService`](../../src/Broiler.Cli/CaptureService.cs) — **DONE** | Was compiled on demand, serially, by the ordered eval loop | Every classic script source compiled on `BROILER_SCRIPT_COMPILE_THREADS` into the context's own cache; the loop is unchanged and reads hits | Nothing, and **the blocker this cell used to name did not exist**: `JSContext.CodeCache` is public, so the store is the context's own and no engine change is needed. The context is not late either — a document's sources are not all known until its fetches return ([§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache)) | **Measured: compile stage 1.41×/1.62×/1.52× at 2/4/8 threads; whole capture 1.44× on a compile-heavy document, 1.22× on a modestly scripted one.** The estimate's 1.5–3× lands only on the stage, and only where a page's scripts are large. The sub-linear ceiling is **not** the compile-thread handoff — tested, not assumed ([§9](#9-the-compile-stages-ceiling-is-not-the-thing-that-looks-like-it)) | Low | Done | 3 |
| 17 | DOM / HTML | [`PreloadScanner.cs`](../../src/Broiler.HtmlBridge.Core/Scripting/PreloadScanner.cs), [`SpeculativePreloadScan.cs`](../../src/Broiler.HtmlBridge.Core/Scripting/SpeculativePreloadScan.cs) — **DONE** | The parse stays sequential (correctly — the HTML tokenizer is spec-sequential); the sub-resource *discovery* was sequential with it | A worker tokenizes the same immutable source and hands the URL sets to the prefetcher, started as the first statement of `ParseHtml` | Nothing; it is a read-only pass over an immutable string | **DONE, and it found a second thing.** Sheet requests are in flight while the document is still parsing (asserted at the origin, not timed). The number came from the script side: the capture host does its own extraction and so never reached item #2's split — 8 scripts at 40 ms go **755.1 → 521.4 ms**, median paired ratio **0.655** over 5 pairs, peak concurrency 6 against 1. It tokenizes rather than pattern-matching bytes, and `srcset`, `<template>` and `<noscript>` are documented exclusions. See [§10](#10-item-17s-win-was-not-the-scan-it-was-a-host-that-never-reached-item-2s-split) | Low | Done | 2 |
| 18 | JS | New: `Worker` / `MessageChannel` | Not implemented | One `JSContext` per worker thread, structured-clone message passing | Per-context state exists and `JSEngine.CurrentContext` is `AsyncLocal`, so isolation is feasible; needs the static-mutable audit from P0-c across Broiler.JS | Capability, not a speedup of existing work | High | 30–40 d | 4 |
| 19 | Runtime | `Directory.Build.props` sets neither; `Broiler.JavaScript.Engine.Benchmarks.csproj` **does** set `ServerGarbageCollection` | Workstation GC defaults everywhere except that one benchmark project | Config knob; evaluate Server GC for headless/batch hosts | Nothing | **Measured: Server GC is 1.62× SLOWER** on a 4-core headless render (see below). Keep Workstation | Low | Done | 0 |
| 20 | Tooling | [`Broiler.Cli`](../../src/Broiler.Cli) — capture, document convert, layout fuzz — **DONE** | Repeatable inputs + `--output-dir`, `--threads` | Threads for document convert; **child processes** for capture and fuzz | **Not "nothing":** the render path's unsynchronised caches (item #9) make two in-process renders a race, so those two go through processes | Fuzz 14.8 s → 8.9 s at 4; capture byte-identical at 1 and 4. Per-page PDF is out of scope here — the CLI shells out to the standalone Broiler.Pdf app | Low | Done | 1 |
| 21 | Tests | [`Broiler.JavaScript.BuiltIns.Tests/AssemblyInfo.cs`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns.Tests/AssemblyInfo.cs) — **DONE** | Was `DisableTestParallelization = true`, serializing the whole assembly | Removed; xUnit's default per-collection parallelism | Nothing. The roadmap's guess was right — it was disabled because of #15's `ThreadPool` dispatch — and isolation is structural: `JSEngine.Current` is `[ThreadStatic]`, its await-point mirror is `AsyncLocal`, and the only shared state is process-wide and already concurrent | **Measured: 2 118 tests, 0 failures, 57-59 s → 31-37 s (~1.75×)** on 4 cores over three runs each | Low | Done | 3 |
| 22 | Input | [`Broiler.Input.*`](../../Broiler.Input) — Linux keyboard `Task.Run`, Windows camera `new Thread`, `lock (_gate)` throughout | **Already correctly threaded** | — | — | None available | — | — | — |
| 23 | UI | [`Broiler.UI`](../../Broiler.UI/src) measure/arrange over the widget tree | Sequential | Parallel measure is possible but widget trees are small (~hundreds of nodes) | — | Negligible; the real win is keeping layout/paint off the input thread (responsiveness, not throughput) | — | — | Not recommended |
| 24 | Documents | [`DocxReader`](../../Broiler.Documents/Broiler.Documents.Docx/DocxReader.cs), `RtfReader`, `HtmlReader`, `MarkdownReader` | Sequential single-document parsers | Batch conversion only — covered by #20 | — | None intra-document | — | — | Not recommended |

## Cross-cutting prerequisites

These gate everything below them and are not optional.

### P0-a — A stage-level benchmark suite — **DONE**

**Current evidence:**
[`tests/render-stages/Broiler.Render.Stage.Benchmarks`](../../tests/render-stages/Broiler.Render.Stage.Benchmarks/),
in `Broiler.Benchmarks.slnx`. All five harnesses the item asked for: cascade +
computed style (`CascadeBenchmarks`, plus `RuleScalingBenchmarks` for the
rule-count axis), `PerformLayout`, display-list raster at 1280×1024, PNG/JPEG
decode, and end-to-end headless render — over a five-page corpus generated
deterministically in code, so the profile reproduces on a bare checkout with no
WPT checkout and no fixture files.

**Republished after Phase 1 and Phase 2.** The profile checked in when P0-a closed
predates item #11 and every item below, so the numbers in
[What measuring Phase 0 changed](#what-measuring-phase-0-changed) are what the
figures *were* — they are kept as written because that is what the decisions of the
time were made on. The current publication is the file itself; the corpus, the
method and the exit gate are unchanged, and the header now also records the raster
thread budget, because the raster share is a function of a setting and two
publications taken at different settings are not comparable without it.

**Exit gate — met.** One command:

```sh
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- \
    --profile --iterations 15 --warmup 5 --json results/stage-profile.json
```

It attributes **96.3–100.0%** of wall time to named stages, worst page `text` at
96.3%, and **exits 1 if any page falls below 90%** — so the gate is checked by the
command that produces the evidence. Published output:
[`tests/render-stages/results/stage-profile.md`](../../tests/render-stages/results/stage-profile.md).

Two method points, both documented at length in `StageProfile.cs`. The stage
boundaries are the public pipeline (`HtmlRender.RenderToImageCore` is four public
calls in a row) rather than instrumentation added to the engine, so the profiler
cannot drift from the real path. And the residual is a reported row rather than
absorbed: a profile whose total was the sum of its parts would pass its own gate by
construction.

**Not covered, and named as such:** the profile measures a *headless* render, which
is the WPT/CLI/WebAssembly path. It says nothing about the Windows/Linux browsers,
which raster on the GPU — for those the raster rows below do not apply at all.

### P0-b — Single-threaded determinism first (item #15) — **DONE**

**The item was largely already built when this document was written**, which the
document did not know. `Broiler.JS` grew `JSMicrotaskQueue` — a per-context
execution lock plus job queue — and `JSContext.PostJob`, a five-case dispatch rule
that replaced the two wrong answers this section names. `JSPromise.Post` and
`JSAsyncFunction.ToPromise` were both moved onto it. `JSMicrotaskQueue`'s remarks
carry the measurement that motivated it: an async function called synchronously
answered `"2,12"` instead of `"2,2"` in **0.60% of 3 000 runs**, and a detector
before the execution lock existed saw **peak 2 concurrent executions and 172
overlaps in 200 rounds**.

**What was left, and is now closed.** `JSGenerator.AwaitThenable` — the third site
this document names — was the one still deciding for itself: prefer whatever
`SynchronizationContext` happened to be current, else `ThreadPool.QueueUserWorkItem`.
Resuming an async generator runs the rest of its body, so that let a second thread
into the context. Routed through `JSContext.PostJob`:
`patches/0122-js-async-generator-await-dispatch.patch` (the `Broiler.JS` remote is
outside this session's GitHub scope, so it is a patch rather than a pointer bump).

**Exit gate — met, with one qualification recorded rather than glossed.** No engine
callback dispatches outside `PostJob`, and every path it takes either runs on the
context's own queue or takes the execution lock. The qualification is about how that
was *tested*: the obvious regression test — drive the racy dispatch and watch
`JSContext.ExecutionConcurrency` — **passes against the unfixed engine**, because
the counter is incremented by `EnterExecution` and the whole defect is that the
resumption never entered the context. A counter of entries cannot observe the
absence of one. The discriminating assertion is ordering (the resumption must run
before the host's next statement, where the specification puts a microtask): 5 of 5
failures pre-fix, 5 of 5 passes with it. `AsyncGeneratorExclusionTests` records both
cases and says which is which.

**Still open, and it is the second half of this item:** whether `CssStyleEngine`
still needs `_sync`. Not re-measured. The lock is cheap relative to what the cascade
costs (finding 1 above), so removing it is not the win it was expected to be — but
the *sharding* the Broiler.CSS roadmap step 2 proposes should now be judged against
the measured cascade cost, not against lock contention.

### P0-c — Shared-cache thread-safety audit — **DONE**

**Current evidence:**
[Shared mutable state on the render path](multithreading-static-state.md), with the
enumeration re-derivable from `scripts/audit-mutable-statics.py`.

**Exit gate — met.** The documented list is that page; the assertion is
[`AmbientRenderState`](../../Broiler.Layout/Broiler.Layout/AmbientRenderState.cs),
covered by `AmbientRenderStateTests` on a real second thread.

**This item's own framing turned out to be wrong, and its headline example is the
proof.** Counting files that declare a mutable private static does not size the job.
Enumerated over the nine render-path projects there are 27 non-thread-static static
fields, and 21 are lookup tables filled by a static initialiser and never written
again — safe under concurrent reads. The remaining six are set-once initialisation
latches or, in one case (`ImageAnimationClock`), deliberately process-wide.

The state that actually corrupts is **instance** state on a process-wide singleton,
which a scan for `static` fields does not see. `FontsHandler`'s four caches — this
section's own example — are `private readonly Dictionary` *instance* fields on the
`RAdapter` behind `CompatProvider.ImageAdapter`; `BImageRenderer._images` is the
same shape. The audit therefore classifies **shared instance roots** separately, and
that list is short and specific: eight roots, of which two are genuine
unsynchronised caches on the render path.

**Two residuals, both one patch each, both named in the audit document:**

1. The `Viewport` ambient slot has no read-side assertion. `CssLengthParser` is in
   `Broiler.CSS`, so instrumenting its eight thread-statics needs a patch there.
2. **`DocumentModeContext` has a latent per-thread defect today.**
   `CurrentQuirksMode` is written *only* by the HtmlBridge DOM path; the HTML-string
   render path never publishes it. Single-threaded that is harmless — the default is
   `false` and standards mode is what that path wants — but the class's claim that
   "each parse overwrites it, so a stale `true` never leaks" holds only because one
   thread renders one document at a time. It becomes reachable with the first pooled
   render thread and should be fixed before item #13, not after.

This is also why `AmbientRenderState.EnforceOnThisThread` is **off by default**: arming the
assertion unconditionally would fail every render in the repository for residual 2,
which single-threaded execution makes unreachable.

### #19 — GC configuration — **DONE, negative result**

25 headless renders of the corpus at 1280×1024, same binary, same allocation, GC
mode set through `DOTNET_gcServer`:

| | Workstation | Server | Server ÷ Workstation |
|---|---:|---:|---:|
| per render | **1 741.9 ms** | 2 829.7 ms | **1.62× slower** |
| allocated per render | 1 970.33 MiB | 1 970.29 MiB | 1.00× |
| gen 0 collections | 3 036 | 4 531 | 1.49× |
| gen 1 / gen 2 | 245 / 50 | 282 / 54 | 1.15× / 1.08× |

**Do not enable Server GC for headless rendering.** The roadmap's "historically
1.1–1.4× on allocation-heavy batch work" does not hold here, and the direction is
wrong, not just the magnitude — identical allocation with *half again* as many
gen-0 collections.

**Read with its condition:** 4 logical cores, and a render that is single-threaded
end to end. Server GC sizes its heap per core and is designed for a process with
several allocating threads; on few cores with one allocating thread it is a known
pessimization, so this measures "not on a small container", not "never". The
question becomes live again if items #3/#5/#12 land and the render allocates from
several threads — **re-measure then**, with the same command:

```sh
DOTNET_gcServer=0|1 dotnet run -c Release \
  --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --gc-config --rounds 5
```

**The finding under the finding is the allocation itself:** ~1.97 GiB per render,
and `RuleScalingBenchmarks` says it scales with *total* stylesheet rules rather than
matched ones. No GC setting fixes a 2 GiB working set per page; item #11 does.
`Directory.Build.props` is deliberately left unchanged.

## Roadmaps per component

### Broiler.Graphics

The best return in the repository, and the least risky.

1. **Unify the two rasterizers.** `Broiler.Graphics/Rendering/BCanvas.cs` and
   `Broiler.HTML/Source/Broiler.HTML.Image/BCanvas.cs` are the same scanline
   rasterizer twice. Parallelizing both separately doubles the work and the risk.
   *Exit gate:* one rasterizer, both consumers on it, pixel output unchanged
   against the WPT reference set.
   **Both have now been parallelized separately, so the doubling is spent** — and
   what it bought is the reason to still do this. The two copies were ported to
   the same design deliberately (same partitioner shape, same environment
   variable, same exit-gate structure), and they have already diverged on one
   measured point: only `Broiler.Graphics` refuses a two-band split. That
   divergence is a defect in the copy that lacks it, and it is the kind that
   unification makes impossible rather than merely unlikely. There is also a
   concrete cost being paid now — both assemblies export a `BRasterParallelism`,
   so anything referencing both (the benchmark project does) must alias one.
2. **Make the font caches thread-safe** (item #9) — **DONE**. It was four sites,
   not one, and two of them were not caches: see
   [What building Phase 2 changed](#what-building-phase-2-changed) §1.
   *Exit gate — met.* `RenderPathConcurrencyTests`, 6 cases; 5 fail against the
   code before the change and pass after it, `Broiler.Graphics.Tests` 64/64 over
   five consecutive runs. Blocked items 10, 12, 13 — and, per Phase 1 §2, every
   CPU-parallel render-path item.
3. **Scanline-band parallelism inside the primitives** — **DONE for both copies**
   (item #4, then item #3's port). `BRasterParallelism` splits the `y` range,
   `FillGlyphContours`' coverage accumulator and crossing list moved inside the
   band, and the budget is `BROILER_RASTER_THREADS` (default one thread per core,
   read by both copies so a host dials the rasterizer down once).
   *Exit gate — met on both.* Pixels identical at 1, 2 and 4 threads on all five
   corpus pages (`--raster-scaling`) and across 42 `RasterBandParallelismTests`
   cases in `Broiler.HTML.Image`; identical at 1, 2, 3, 4 and 8 across 34 cases and
   four scenes in `Broiler.Graphics` (`--graphics-raster-scaling`). A budget of 1
   splits nothing at all.
   **What the port found, and it is not what §4 or §8 predicted.** The threading
   is worth 1.00–1.39× at four threads — the same small number item #4 got, but
   for the opposite reason: these scenes split 85–100% of their pixel area, so the
   ceiling is per-primitive work rather than fill size. The sequential win is the
   large one (1.58–2.96×) and most of it is a `CurrentTarget` lookup that was
   running per pixel until banding forced it out of the loop; the clip narrowing
   §8 named pays 2.21× on a clipped pane and ~1.0× everywhere else. Details and
   the decomposition:
   [§13](#13-item-3s-sequential-win-was-not-the-clip-narrowing-this-document-told-it-to-port).
   **One thing is left open by it:** the `Broiler.Graphics` partitioner refuses a
   two-band split because two bands measured slower than none, and the
   `Broiler.HTML.Image` copy shows the same inversion without the guard. Giving it
   the same floor is a rasterizer change whose exit gate is a full WPT run, so it
   is a follow-up rather than part of item #3.
4. **Tile-parallel display-list replay** (item #5) — **DONE**. The surface is
   split into horizontal strips and each replays the whole list into its own
   strip, so a tile's unit of work is every fill that touches it — the only unit
   all five pages have. Layers work per tile without a scratch buffer of their
   own: each tile pushes its own layer bitmap and composites it back over the
   box its clip could have written, which is the tile.
   *Exit gate — met.* Pixels identical at 1, 2 and 4 tiles crossed with 1 and 4
   bands on all five corpus pages (`--tile-scaling`, which fails the run if any
   setting differs), 69 `RasterTileParallelismTests` cases over eight documents,
   and a full WPT run at 1 and at 4 tiles whose entire output diffs to zero
   lines. Scaling curve published above and in
   [§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see),
   which is also where to read **before** starting item #3: half the win was a
   sequential fix that item #3's copy of the rasterizer has not had.
5. **Glyph outline cache — DONE**; shaped-run cache and parallel shaping (item
   #10) — not built. The item's own advice, measure the cache alone first, is
   what found the outline cache one layer below the shaper, and it is worth more
   than the threading it was a prerequisite for:
   [§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names).
   The shaped-run cache needs a complex-script page in the corpus before it can
   be measured at all, which is the work to do before starting it.

**Not recommended:** parallelizing the Direct2D / OpenGL / Vulkan backends. They
already hand work to the GPU; adding CPU threads there adds synchronization for
no gain.

### Broiler.CSS

1. **Rule indexing first** (item #11) — **DONE**, as
   `patches/0123-css-cascade-rule-index.patch`. Rules are bucketed by their
   rightmost simple selector (id / class / type / universal) in
   `CssCascadeRuleIndex`; an element merges the buckets its own keys reach and
   tests those.
   *Exit gate — met.* `RuleScalingBenchmarks` with matches held at four: 32× the
   rules costs 1.64× the time and 1.13× the allocation, against 30.8× and 32.0×
   before — the per-element cost now tracks matched rules, not total rules.
   Computed-style output unchanged: `Broiler.CSS.Tests` 341/341,
   `Broiler.CSS.Dom.Tests` 383 passed (the 2 failures are pre-existing
   architecture tests, verified on a clean tree), and WPT
   `css/css-backgrounds` + `css/CSS2/linebox` is identical over 62 tests —
   same pass/fail/skip and the same average pixel match to fourteen digits.
   *Not built:* the ancestor bloom filter. The key buckets alone flattened the
   scaling curve, so the filter has nothing left to remove on this benchmark;
   it belongs with a measurement that shows descendant-combinator rejection
   costing something.
   Note for step 2 and item #12: the cascade now also carries a *third*
   generation counter. The rule index is memoized against sheet/environment
   changes only, deliberately not against the DOM-mutation invalidation that
   clears the caches below — a sharding design must not collapse the two.

2. **Shard the caches** — **DONE**. `_cache`, `_sparseCache` and
   `_declaredCascadeCache` are `ConcurrentDictionary` rather than plain
   dictionaries behind one `_sync`, and a fourth (`_cascadedStyleCache`) memoizes
   `GetCascadedStyle`'s whole result, which is the store step 3 needed. The
   generation-guard protocol is intact and its stores still happen under `_sync`
   — it is what makes the lock-free compute window correct, so the compare and
   the publish must not be separable by a concurrent `InvalidateAll`.
   *Not built:* the per-thread first-level cache. It was there to relieve lock
   contention, and the lock turned out not to be where the time went
   ([§3](#3-the-_sync-lock-was-not-the-bottleneck-the-item-names-and-the-cascades-own-cost-is));
   it belongs with a measurement that shows cache probes costing something.
3. **Parallel style recalc** (item #12) — **DONE**, but *not* over sibling
   subtrees: the box walk cannot be split without changing what it produces, so
   the threaded unit is the per-element cascade the walk consumes, resolved ahead
   of it by `CssStyleRecalc.Warm`
   ([§4](#4-the-parallel-unit-is-not-the-box-walk--this-is-a-prefetchconsume-split-the-fourth-in-this-document)).
   Budget: `BROILER_STYLE_THREADS`, where 1 is the pre-change path rather than an
   approximation of it.
   *Exit gate — met.* Pixel-identical renders at 1, 2, 3, 4 and 8 threads over
   five documents chosen for what makes an element's cascade depend on something
   other than itself (deep inheritance chains, custom properties resolved against
   an ancestor, relative font weights, many rules, combinators) — 22
   `ParallelStyleRecalcTests` cases with a guard test asserting each document
   clears the warm pass's element threshold, so none of the equality assertions
   can pass vacuously; `CssStyleEngineConcurrencyTests` extended from "the caches
   survive" to "the *answers* match", comparing a sequential cascade against an
   eight-thread one property by property on a second, structurally identical
   document; and the whole corpus pixel-identical at every setting of the
   `--style-scaling` mode, which fails the run on a single differing byte.
   Published scaling: [`tests/render-stages/results/style-scaling.md`](../../tests/render-stages/results/style-scaling.md).

### Broiler.Layout

The most expensive and the least certain. Do it last, and do the sequential work
first.

1. **Stop laying out the whole tree twice** — **measured, and retired as written.**
   `HtmlContainerInt.PerformLayout` does run `Root.PerformLayout` a second time
   whenever `MaxSize.Width <= 0.1` (shrink-to-fit), and this step claimed that
   caching intrinsic widths from the first pass "removes a whole tree traversal".
   Both halves of that are wrong in practice
   ([§2](#2-the-double-layout-is-unreachable-from-every-path-this-repository-measures-and-where-it-fires-it-is-not-a-doubling)).
   **Nothing this repository measures reaches the branch** — the WPT runner, the CLI
   capture, the browser and every benchmark set a viewport width, so all of them lay
   the tree out exactly once, which `--layout-passes` counts at the site rather than
   inferring. It fires on the auto-size embedding path, and there the two passes cost
   **0.80–1.35× one pass, not 2×**: the first runs at width 99999, where the line
   breaker barely runs, so the traversal being removed is the cheaper of the two.
   The shape worth aiming at, if this is ever built, is
   `HtmlRendererUtils.MeasureHtmlByRestrictions` — three passes across two calls,
   3.3× a single pass on the text-heavy page — and not the single-call shrink-to-fit
   one this step names.
2. **Dirty-bit invalidation** (item #14). Today every relayout starts at the
   root. Relayout roots bound the work *and* give parallel layout its unit of
   independence — without them, "parallel subtree layout" has no subtrees to
   claim. **Measured now, and the order is the other way round from how this step
   is written**: a relayout is 60–97% box-tree rebuild and re-cascade, so the
   invalidation that pays is on the tree and the cascade, and a layout dirty bit
   on its own is worth 1.03× on the page that hurts most
   ([§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half)).
   It does **not** start a layer down, which this step used to claim on the
   grounds that `DomDocument.Version` is the only mutation signal the DOM offers:
   `DomDocument.Mutated` has published a typed record all along, and the first
   slice is a consumer for it in `Broiler.Layout`
   ([§11](#11-item-14s-blocker-did-not-exist-either-and-the-burst-does-not-amortise)).
   That slice is built — mutations that cannot reach the render tree no longer
   rebuild it, worth 10–90× on the offscreen-build case. **The connected half is
   built too**: `CascadeInvalidationSet` scans the sheets the tree was cascaded
   from for every class, id and attribute name any selector mentions and any
   `attr()` reads, and a connected attribute write that neither the cascade nor
   box construction can see is answered without a rebuild
   ([§12](#12-item-14s-second-half-the-sheets-already-knew-and-the-gate-caught-a-bug-nothing-else-could)).
   What is left of the item is the *scoped* rebuild — narrowing a rebuild rather
   than skipping one, for the connected mutations that do reach the tree.
3. **Parallel intrinsic sizing.** Min/max-content measurement of independent
   subtrees is a pure function of the subtree given resolved style, so it
   parallelizes before the mutating flow pass does.
4. **Parallel independent subtrees** (item #13), in ascending order of risk:
   subdocuments/iframes → `position:absolute/fixed` subtrees (they do not
   contribute to their parent's flow) → table cells → flex/grid items. Each
   worker must establish the thread-static ambient state from P0-c.
   *Exit gate per class:* fragment trees identical to the sequential run across
   the WPT corpus, verified with `FragmentJsonDumper`, at 1 and 8 threads.

**Constraint to keep in view:** normal block flow is sequential in the block
direction — a block's position depends on the height of its predecessors — so
the ceiling here is low regardless of core count. Steps 1 and 2 were said to be
worth more than steps 3 and 4 combined; **step 2 was, and step 1 is worth nothing
on any path measured here**, so what remains of that claim rests entirely on the
item that has already landed.

### Broiler.JS

1. **Item #15, the single-threaded event loop.** Correctness, and a
   prerequisite for the whole document.
2. **Re-enable test parallelization** (item #21) — **DONE**. Contexts *are*
   isolated per test, and by construction rather than by convention:
   `JSEngine.Current` is `[ThreadStatic]` and the mirror that restores it across
   await points is `AsyncLocal`, so two xUnit threads each holding their own
   `JSContext` cannot observe each other's. What they share is process-wide and
   already concurrent (interned key strings, the built-in registry's static
   constructors, `DictionaryCodeCache.Current`). The attribute is gone and the
   file now records what to look for if it ever has to come back: a new piece of
   process-wide mutable state, not a new test.
3. **Parallel/background script compile** (item #16) — **DONE**. The
   `DictionaryCodeCache` is concurrent, which is the hard part, and the store the
   compile-ahead writes into turned out to be the context's own: `CodeCache` is a
   public property, so nothing had to be added to `JSContextOptions`
   ([§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache)).
   It is **not** fed by the preload scanner, and that was a deliberate change of
   plan rather than an omission: the scanner supplies URLs, whereas a compile
   needs *source*, and a document's sources are not all in hand until its fetches
   have returned — which is after the context exists. The earliest honest start is
   therefore where the context already is, and from there the compiles overlap the
   parse, the DOM build, and the execution of every script ahead of them.
4. **Workers** (item #18) — a feature, scoped and scheduled as one. Gate it on
   the P0-c static-state audit; anything genuinely global (interned
   strings, shapes/hidden classes, the code cache) needs an explicit
   thread-safety contract before a second context runs concurrently.

**Not recommended:** parallelizing parse or codegen of a *single* script. The
dependency chain is real and the payoff is small next to compiling independent
scripts concurrently.

### Broiler.HTML / HtmlBridge / DOM

1. **Concurrent sub-resource fetch** (item #2). The highest user-visible value
   in this document. Split every synchronous call site into
   *prefetch* (issued concurrently, bounded per host) and *consume* (reads the
   cache, stays synchronous), so no call site has to become `async`.
   *Exit gate:* a page with *N* sub-resources issues them concurrently, and
   `document.write` ordering plus script execution order are unchanged across
   the WPT corpus.
2. **Speculative preload scan** (item #17) — **DONE**. A worker tokenizes the raw
   source while the main parse runs and hands the URL sets to step 1's
   prefetcher. Read-only over an immutable string, so it is safe by construction,
   and `BROILER_PRELOAD_SCAN=0` is the sequential path rather than an
   approximation of it. It found that the capture host never reached step 1's
   split at all: [§10](#10-item-17s-win-was-not-the-scan-it-was-a-host-that-never-reached-item-2s-split).
   **It gates step 3**, which is why it moved above it, and it feeds item #16.
   Frames are the one family it discovers but does not wire — their consume site
   yields `(content, contentType)` through a policy chain the text prefetcher
   cannot serve, and on the paths measured here a frame is a disk read rather
   than a round trip.
3. **Concurrent image decode** (item #8). The decoder statics are cleared — they
   have a re-entrancy test — and `BImageRenderer._images` is fixed, but the
   headless render path loads images synchronously and inline, so this is the
   same prefetch/consume split as step 1 over the image URLs step 2 supplies —
   `PreloadScanResult.ResolvedUrls(PreloadKind.Image)`, which now exists:
   [§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor).

**Not recommended:** parallel HTML tokenization. The tokenizer is a
specification-mandated state machine whose transitions depend on the tree
builder's insertion mode. Speculation is the only correct form of parallelism
here, and step 2 is that.

### Broiler.Media

1. **Concurrent decode across images** (item #8) — **DONE**, and it is the better
   win when a page has several images. A prefetch/consume split rather than the
   coarse `Parallel.For` this step used to describe
   ([§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor)), but **not** fed by
   item #17 as that section predicted: the box tree names what layout will actually
   ask for, where a source scan names a superset
   ([§12](#12-only-half-of-an-image-load-was-safe-to-move-and-the-other-half-changed-the-page)).
   The walk lives in `Broiler.Layout` — `ImagePrefetch` holds the budget
   (`BROILER_IMAGE_PREFETCH_THREADS`, where 1 is the pre-change path) and
   `DeferredImageLoad` is what keeps each completion on the layout thread.
   *Exit gate — met.* Pixels identical at 1, 2, 3, 4 and 8 concurrent loads over
   eleven documents chosen for what decides something about the walk (background
   layers, a box that is both, duplicate sources, both kinds of failure, SVG, inline
   data, hidden subtrees, table cells) — 51 `ImagePrefetchTests` cases; the walk's
   claim count asserted directly where pixels cannot see it, which is what pins the
   `display:none` exclusion; and WPT `css/CSS2` — whose `visudet` replaced-element
   tests carry seven images each, the shape a late decode would corrupt — identical
   at the budget on and off (13 passed, 4 failed, 0 skipped both ways, 97.16%
   average match, whole output differing by one elapsed-time line).
   `css/css-backgrounds` was run the same way and is also identical (40/16/5), but
   **that run is a no-regression check rather than a gate**: no file in that subset
   references two images, so the walk never ran in it. Worth saying, because a
   subset that cannot reach the code under test proves nothing about it.
2. **JPEG intra-image** (item #6) — **DONE**. Bands of block rows for
   dequantize + IDCT, bands of output rows for upsample + YCbCr→RGB; the
   dequantized block and the IDCT intermediate moved inside the band, which is
   both what makes it thread-safe and less garbage than the sequential version
   produced. Entropy decode is untouched. **2.08–2.61× at 4 threads.**
3. **PNG intra-image** (item #7) — **DONE**, expand-to-RGBA only, interlaced
   passes included (a pass's rows land on distinct `y`, so bands of one pass stay
   disjoint; the seven passes stay sequential because they overwrite each other by
   design). Inflate and unfilter untouched. **1.22–1.29× at 4 threads**, just under
   the 1.3–1.6× estimate.
   *Exit gate for both — met.* Decoded output byte-identical to the sequential
   decoder at 1, 2, 4 and 8 threads over four fixtures including an interlaced PNG
   and a progressive JPEG (`Broiler.Media.Image.Managed.Tests`), and at every
   setting of the `--decode-scaling` harness, which fails the run on a single
   differing byte.

Both go through `ImageDecodeParallelism`, whose budget is
`BROILER_IMAGE_DECODE_THREADS` and whose default any host running several renders
at once must divide — see
[§7](#7-two-kinds-of-parallelism-now-multiply-and-the-runner-has-to-divide). Item
#8's budget is in the same division, and the two of them are the first pair in this
document that genuinely compound: a concurrent load decodes through the band
partitioner, so *N* loads at *N* bands is *N²* threads. Dividing the inner one
anyway measured as nothing, and §7 has the figures.

### Tooling — Broiler.Wpt and Broiler.Cli

Do this first. It costs days, carries almost no risk, and it accelerates every
other item in this document.

1. **Parallel WPT workers** (item #1). Replace the single `WptWorkerClient` with
   a pool of *N* worker processes draining a shared queue. The runner already
   buffers results and sorts them afterwards, so output determinism is
   preserved.

   **Size the pool by memory, not by cores.** `WptMemoryGuard` allows each test
   1024 MiB of resident-set *growth* (`--memory-limit-mb`), so *N* workers can
   legitimately need *N* GiB plus baseline. Default to
   `min(cores, availableRam / 1.5 GiB)` and make it overridable.

   *Exit gate:* pass/fail/skip classification identical to a sequential run
   across the full corpus, at 1 and *N* workers; per-test timeout and memory
   attribution still name the right test.
2. **Preserve the sequential path.** Keep `--no-worker-isolation` and add
   `--workers 1` so a failure can always be reproduced deterministically.
3. **Parallel CLI batch operations** (item #20) — per-file for document convert
   and capture, per-page for PDF.

### Broiler.UI, Broiler.Input, Broiler.Documents

No parallelism work recommended.

- **Broiler.Input** is already correctly threaded: dedicated reader threads per
  device with `lock (_gate)` state handoff. Leave it alone.
- **Broiler.UI** widget trees are too small for measure/arrange parallelism to
  pay for its risk. The responsiveness problem — layout and paint running on the
  input thread — is a *scheduling* problem, not a throughput one, and it is
  solved by moving the pipeline off the input thread, not by adding workers.
- **Broiler.Documents** readers are sequential per document; the only useful
  parallelism is per-file batch conversion, which item #20 covers in the CLI.

## What not to parallelize

Recording these so they are not revisited each time the topic comes up:

| Candidate | Why not |
|---|---|
| HTML tokenizer / tree builder | Spec-sequential state machine; insertion mode depends on prior tokens. Speculative preload scanning (#17) is the only correct form. |
| PNG inflate and unfilter | DEFLATE is a sequential back-reference stream; Up/Paeth filters read the reconstructed previous row. |
| JPEG entropy decode | Huffman bitstream is sequential except at restart-interval boundaries. |
| Normal block flow | Block position depends on the accumulated height of predecessors. Only *independent* subtrees are claimable. |
| Single-script JS parse/codegen | Real dependency chain, small payoff; compile independent scripts concurrently instead (#16). |
| GPU backends (D2D / OpenGL / Vulkan) | Work is already off-CPU; CPU threads add synchronization for no gain. |
| Broiler.UI measure/arrange | Trees are too small; the real problem is scheduling, not throughput. |
| More `ThreadPool` dispatch in the JS engine | Item #15 exists to *remove* the dispatch that is already there. |

## Suggested sequencing

| Phase | Contents | Rationale |
|---|---|---|
| **0 — Make it measurable and deterministic** | P0-a benchmarks, P0-b single-threaded event loop (#15), P0-c static-state audit, GC config evaluation (#19) | Nothing after this is trustworthy without it. #15 is a correctness fix that also removes lock overhead from the cascade. |
| **1 — Free wins and the sequential fixes** — **DONE** | WPT worker pool (#1), CLI batch (#20), concurrent sub-resource fetch (#2), CSS rule indexing (#11) | Cheap, low-risk, and #1 shortens the feedback loop for everything else. #11 is single-threaded but must precede #12. **What it changed:** #11 met its exit gate (cascade cost is now flat in total rules) but is 2.8× on a whole render, and `parse+cascade` still dominates the rule-heavy page — so #12 needs that stage split before it is started; #20 had to use processes, which makes item #9 a gate on every render-path item, not three. |
| **2 — Raster, decode, text** — **DONE** | Rasterizer unification + band/tile parallelism (#3, #4, #5), font-cache safety (#9), text caches (#10), image decode (#6, #7, #8), preload scan (#17) | Largest CPU wins, disjoint memory, verifiable by exact pixel comparison. **#9 first, and it is done** — Phase 1 §2 made it the gate on every other item here, not just on #10/#12/#13. **What it changed, in the order it matters:** band parallelism inside a primitive turned out to be the wrong unit for a page — three of five corpus pages split zero fills, because their raster is glyphs — which promotes **#5 from "supersedes #3" to the only raster parallelism the corpus can use (§4)**; the phase's largest single win was a *cache*, and not the one #10 names (§5); the pool and the in-process threads multiply, so the runner now divides them (§7); the item-#9 findings (§1–§3) stand. **#5 landed and about half of its win was single-threaded** — the rasterizer was walking pixels its clip could never admit (§8) — and between them #4, #10 and #5 have taken raster from the largest stage on three pages to the largest on one (§9). **#17 landed and its number came from somewhere the item did not name:** the capture host does its own script extraction and so never reached the split item #2 built, leaving that family serial in the path this repository measures (§10). **#8 landed, and it needed neither of the two things §6 predicted** — not #17's URL set (the box tree names what layout will actually ask for, where a source scan names a superset) and not a cache (layout's existing loader seam is the split) — but it did need one thing nothing predicted: **only the decode was safe to move off the layout thread, not the completion callback**, which changed the rendered page on the failure path and nowhere else (§12). It also discharges P0-c's last debt, being the first worker to establish the ambient state and arm its assertion. **#3's port closed the phase, and it corrected this document rather than confirming it** — §8 said to port the clip narrowing first, and the sequential win turned out to be a per-pixel target lookup that banding forced out of the loop; the narrowing itself pays only on content inside the surface and outside the clip, which the corpus scene written for it did not contain (§13). Its threading is 1.00–1.39x at four threads with 85–100% of area split, so **both copies now say band parallelism is the wrong unit, for opposite reasons.** It leaves one named follow-up: a two-band split measured slower than none, and only the ported copy refuses one. The largest open question is still the parse/cascade split Phase 1 §1 named, which gates #12 — and §9 makes it the largest unattributed question in the document, now that nothing in Phase 2 is open. See [What building Phase 2 changed](#what-building-phase-2-changed). |
| **3 — Style and incremental layout** — **DONE** | Cache sharding + parallel style recalc (#12), layout dirty bits (#14), parallel script compile (#16), re-enable test parallelization (#21) | Depends on Phase 1's algorithmic fixes and Phase 0's determinism. **What it changed, in the order it matters:** the phase opened by answering the question Phase 2 §9 said it owed, and the answer reframes the item it gates — **`parse+cascade` is 81.3–98.2% cascade on every page**, so item #12 aims at the whole stage rather than a fraction of it, and the stage's name is a legacy of nobody having measured it ([§1](#1-parsecascade-is-a-cascade-stage-the-name-overstates-the-parse)); getting that number needed the profile's first instrumentation *inside* the engine, because none of the four sub-stages is a pure function of the source and P0-a's out-of-band trick therefore does not reach them ([§2](#2-measuring-it-needed-instrumentation-and-p0-as-method-note-is-why-that-is-worth-saying)). **#12 landed and both of the blockers its row named were the wrong ones.** The `_sync` lock was not the bottleneck — the per-element cascade was 210 µs on a *five-rule* page, which no number of lock acquires explains ([§3](#3-the-_sync-lock-was-not-the-bottleneck-the-item-names-and-the-cascades-own-cost-is)) — and the parallel unit is not the box walk, which cannot be split at all: it rewrites `display` before children read it, hides a closed `<details>`'s subtree after cascading it, and inserts generated boxes on the way back up. So #12 is a **prefetch/consume split**, the fourth in this document to arrive at that shape after the item named a different one ([§4](#4-the-parallel-unit-is-not-the-box-walk--this-is-a-prefetchconsume-split-the-fourth-in-this-document)). **1.16–2.01× on the cascade stage at four threads, 1.08–1.96× end to end, pixel-identical at 1/2/4** — and the harness now publishes the serial residue per page (16–55%), so what is left is measured rather than guessed ([§5](#5-the-serial-residue-is-measured-now-and-it-differs-three-fold-across-pages)). **#21 landed** and cost nothing but the reading that says why it is safe — 2 118 tests, 0 failures, **57–59 s → 31–37 s (~1.75×)** on four cores. **#16 landed, and it needed none of what this document said it needed.** The store §6 said had to be built was already there — `JSContext.CodeCache` is public — so the item is one main-repo type and a two-line call site, with no engine change, no submodule patch and none of the cross-document isolation §6 worried about ([§8](#8-item-16s-blocker-did-not-exist-the-store-is-the-contexts-own-cache)). That makes **four items running whose stated blocker was not the operative one** (#8, #12 twice, #16), and the lesson is now explicit: the "What blocks it today" column is a hypothesis, and checking it belongs in an item's first hour rather than its last. **Compile stage 1.41×/1.62×/1.52× at 2/4/8 threads, whole capture 1.44× on a compile-heavy document and 1.22× on a modestly scripted one** — and its measurement tested the obvious explanation for the ceiling instead of publishing it, which is how the explanation turned out to be wrong and a 15–17% *sequential* tax turned up instead ([§9](#9-the-compile-stages-ceiling-is-not-the-thing-that-looks-like-it)). **#14 is the phase's remainder, and its precondition is now built.** The relayout harness §7 asked for exists (`--relayout-profile`), and its first result re-aims the item: a relayout is **60–97% box-tree rebuild and re-cascade**, because any DOM version bump disposes the render tree and re-cascades the whole document before the layout pass runs — so dirty bits on `CssBox.PerformLayout` bound 3–39% of the cost, and 2.9% on the rule-heavy page. The available ceiling is real (34× on that page if the rebuild goes, against 1.03× for a perfect layout dirty bit alone) but it sits in the box tree and the cascade, and the work starts a layer down in `Broiler.DOM`, where every script-shaped mutation is today indistinguishable from every other ([§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half)). **#14's first slice then landed, and its blocker did not exist either — the fifth in a row.** The DOM was never limited to a version counter: `DomDocument.Mutated` has published a typed `DomMutationRecord` since before the item was written, so the item does not start in `Broiler.DOM` at all; what was missing was a consumer, and `RenderTreeInvalidation` is it. Mutations that cannot reach the render tree no longer rebuild it — **`rules` 1 032.7 → 11.5 ms (89.8×)** on the offscreen-build case, 10.0–25.8× on the other four pages, with the remaining thirty rows inside the run-to-run spread. The measurement also refuted one of §10's own two predictions (the burst does not amortise — a rebuild is whole-document for a single attribute write) and reinterpreted the other ("changes nothing observable" is already free at the value level). **The remainder was larger than the slice** — an unstyled `data-*` write still cost 997.8 ms on `rules` — and **#14's second half then took it**: `CascadeInvalidationSet` asks the sheets the tree was cascaded from whether any rule could match differently, and asks box construction separately whether it reads the attribute for reasons of its own, because a set built from stylesheets would happily elide a `<td colspan>` write on a page whose sheet never mentions `colspan`. **`rules` 1 476.7 → 43.0 ms (34.4×)** on that row, 9.5–14.2× on three other pages, 36.0× on `rules`' class toggle, with two control rows (`styled attribute`, `styled class`) that still rebuild on exactly the pages whose sheets name what they write. **The phase's last finding is not a speedup but a defect, and the gate is what makes that worth stating**: `--relayout-parity` renders every page with the elision on and off and compares images, it failed on first run, and the cause was that laying the same box tree out twice was never idempotent — a two-pixel margin drift that had been live in the *first* slice since it shipped, because nothing in this repository had ever taken that path twice ([§12](#12-item-14s-second-half-the-sheets-already-knew-and-the-gate-caught-a-bug-nothing-else-could)). An optimisation that skips work is a new execution path, and its gate has to run that path end to end. What is left of #14 is a *scoped* rebuild rather than a skipped one. See [What building Phase 3 changed](#what-building-phase-3-changed). |
| **4 — Parallel layout and workers** — **open** | Parallel intrinsic sizing and independent subtrees (#13), Web Workers (#18) | Highest cost, highest risk, lowest ceiling. **The phase opened by re-measuring its own rationale, and the rationale had gone stale in the direction that matters.** This row used to justify deprioritising the phase with "layout is 0.6–6.5% of a first render (Phase 1 §2)". Re-run on today's code, layout is **3.3–20.1%** ([§1](#1-layouts-share-tripled-without-layout-changing-and-the-number-this-row-quoted-was-measured-before-two-phases-of-work)) — its absolute cost is unchanged at 28–51 ms, and every phase that removed cost from raster and the cascade raised its share by Amdahl. That does *not* make #13 worth 20–30 days at High risk: 1.5–2.5× of a 20% stage is ~10% end to end on one page. It does mean the phase has to be argued from a current number. The relayout side still says what it said — the layout pass is **3–39% of a relayout** ([§10](#10-item-14s-harness-exists-now-and-it-says-the-item-is-aimed-at-the-smaller-half)), the rest being box-tree rebuild and cascade — so the interactive case does not rescue the phase either. **What the phase has built is the measurement its sequential step asked for, and it retired that step**: the `Broiler.Layout` roadmap's step 1 ("stop laying out the whole tree twice", stated as worth more than the two parallel steps combined) is **unreachable from every path this repository measures**, and where it does fire it is not a doubling ([§2](#2-the-double-layout-is-unreachable-from-every-path-this-repository-measures-and-where-it-fires-it-is-not-a-doubling)). Phase 3 §4, §8 and §11 remain a warning about #13's wording, and this phase adds a sixth: **six** items running have found that the structure an item names is not the structure that can be split, or that the blocker — or, here, the *beneficiary* — it names is not the operative one. See [What building Phase 4 changed](#what-building-phase-4-changed). |

**Global exit gate:** every parallel path has a `--threads 1` equivalent that
reproduces the sequential output exactly, and the WPT corpus produces identical
pass/fail classification at 1 and *N* threads across three consecutive runs.
Non-reproducible output is a regression regardless of how much faster it is.

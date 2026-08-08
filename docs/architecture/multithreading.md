# Multithreading analysis and roadmap

Where concurrency can make Broiler faster, where it cannot, and the order the
work has to happen in. Scope is every component in the aggregate repository plus
the tooling.

## Status

**Phase 2 is under way.** Six of its nine items have landed (#9, #4, #5, #6, #7,
#10); three have not (#3, #8, #17). What building them
changed is in [What building Phase 2 changed](#what-building-phase-2-changed) —
including the three findings that matter most for what is left: **band parallelism
inside a primitive is the wrong unit for a page** (§4), **the largest single win
the phase has produced was a cache, not a thread** (§5), and — from item #5 —
**most of what looked like raster parallelism was the rasterizer drawing pixels
nothing could see** (§8). The raster stage is no longer the largest share of a
render on any corpus page ([§9](#9-raster-is-no-longer-the-stage-to-aim-at-and-the-published-profile-says-so)).

| Item | State | Evidence |
|---|---|---|
| #9 — shared render-path caches | **Done** | `FontsHandler`, `BImageRenderer`, `FallbackSystemFont` contour caches and `TrueTypeFont`'s five lazy tables; `RenderPathConcurrencyTests` (6 cases, 5 of which fail against the code before the change). Both P0-c residuals closed with it |
| #4 — band-parallel raster (`Broiler.HTML.Image`) | **Done** | `BRasterParallelism`, upstream in the pinned `Broiler.HTML` pointer (its patch file is retired); corpus `paint` page **1 594.7 ms → 1 096.5 ms (1.42×)** at 4 threads, 92.7% of its fill area split; pixels identical at 1/2/4 on all five pages, `RasterBandParallelismTests` 42 cases. Flat on the three pages whose raster is glyphs — see [§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page) |
| #3 — band-parallel raster (`Broiler.Graphics`) | Not started | The partitioner and the exit-gate harness now exist; this is porting them to the second copy, and [§2](#2-the-rasterizer-the-profile-measures-is-item-4s-copy-not-item-3s) still says unify first. [§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page) and [§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see) both say the *clip narrowing* is the part worth porting first |
| #5 — tile-parallel replay | **Done** | `TileParallelReplay`, `patches/0126-…`; `PerformPaint` at 1 → 4 tiles: `paint` **1 323.7 → 461.4 ms (2.87×)**, `rules` 3.49×, `text` 2.42×, `mixed` 2.44×, `boxes` 1.76× — faster than band parallelism on all five pages, including the three bands could not touch. Pixels identical at 1/2/4 tiles crossed with 1/4 bands on every page (`--tile-scaling`), 69 `RasterTileParallelismTests` cases, and a full WPT run at 1 and 4 tiles whose entire output diffs to **zero lines**. Most of the win was single-threaded — see [§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see) |
| #6/#7 — image decode | **Done** | `ImageDecodeParallelism`; JPEG **2.08–2.61×**, PNG **1.22–1.29×** at 4 threads, byte-identical at every setting (`--decode-scaling`, plus two cases in `Broiler.Media.Image.Managed.Tests`) |
| #8 — concurrent decode across images | Not started | Unblocked by #9, but the headless render path loads images **synchronously and inline** (`AvoidAsyncImagesLoading`), so this needs #2's prefetch/consume split rather than a `Parallel.For` — see [§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor) |
| #10 — glyph outline cache | **Done** | `TrueTypeFont` caches outlines by glyph index; raster stage **1.34×** on `text`, **1.54×** on `boxes`, **1.00×** on the text-free `paint` control. The shaped-run cache the item names is *not* built — see [§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names) |
| #17 — preload scan | Not started | — |

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

Where the memory figure cannot be read at all, the pool stays at **one** worker.
Guessing high on an unreadable budget is how a runner OOM-kills a CI box, and the
per-test allowance is a full GiB.

## What building Phase 2 changed

Nine findings. The first three came out of item #9, the gate; the rest came out of
the raster, text and decode work that followed it, and three of them change what
the rest of the phase — and the phase after it — should be.

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

**What is still owed before that pool exists:** the ambient-state contract is
instrumented on all three slots but still off by default, and nothing yet calls
`AmbientRenderState.Establish` on a worker thread because there are no worker
threads. The first item that creates one has to arm `EnforceOnThisThread` in the
same place it calls `Establish`, or the instrumentation bought here goes unused.

Band-parallel raster (item #4) does **not** discharge that debt, and it is worth
saying why it does not need to: a band never leaves the primitive it was created
in, so it inherits nothing and establishes nothing. The ambient slots are read
during *layout* and during display-list *construction*, both of which have
finished before a fill starts. The first item that has to arm the enforcement is
still the first one that renders a whole document off the calling thread.

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

## Master table

Gain is per-stage unless stated. Effort is engineering days for one person
familiar with the component. Risk is the chance of introducing a
non-deterministic correctness defect.

| # | Component | Site | Currently | Parallel shape | What blocks it today | Est. gain (unmeasured) | Risk | Effort | Phase |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Tooling | [`Broiler.Wpt/Program.cs`](../../src/Broiler.Wpt/Program.cs) `RunDiscoveredTests` — **DONE** | Pool of *N* worker processes draining a shared queue (`--workers <N>\|auto`) | Work queue over *N* worker processes; results already buffered into `allResults` and sorted after the run, so order is not load-bearing | Nothing. Worker protocol is already one-command-per-line JSON over stdio | **Measured: 1.93× at 4 workers** on a 61-test subset (45.2 s → 23.4 s); identical classification at 1, 4 and auto. Bounded by RAM, not cores | Low | Done | 1 |
| 2 | HtmlBridge | [`ScriptExtractionService.cs:303`](../../src/Broiler.HtmlBridge.Core/Scripting/ScriptExtractionService.cs), [`ResourceLoader.cs:56`](../../src/Broiler.HtmlBridge.Dom/Runtime/ResourceLoader.cs), [`SubDocuments.cs:581`](../../src/Broiler.HtmlBridge.Dom/DomBridge/SubDocuments.cs) | Serial `GetStringAsync(..).GetAwaiter().GetResult()`, one resource at a time | Bounded-concurrency prefetch (6/host, browser convention) into a content-addressed cache; call sites then hit the cache | Call sites are synchronous and ordering-sensitive for `document.write`; needs a prefetch/consume split, not an `async` conversion | **Done for scripts and `<link>` stylesheets** (`SubResourcePrefetcher`, bounded 6/host). Iframes and `fetch()`/XHR are not wired: they have no point where the URL set is known before it is consumed, which is what item #17 supplies | Medium | Done (scripts, sheets) | 1 |
| 3 | Graphics | [`Rendering/BCanvas.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/BCanvas.cs) `FillRect:106`, `FillGlyphContours:241`, `DrawBitmap:345`, `FillRectTiled:389`, `FillLinear/Radial/ConicGradientRect:425/468/508` | Single-threaded scanline loops | `Parallel.For` over scanline bands; per-band coverage buffer and clip stack | Per-row `coverage[]` and the `_clipOperations` list are instance state shared across rows — must become per-worker | **The 4–6× estimate belongs to item #5, not here.** Item #4 built this shape against the copy the profile measures and got 1.42× on one corpus page and nothing on three; the ceiling is set by fill size, not by cores ([§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page)). `BRasterParallelism` and `RasterBandParallelismTests` are the port-ready partitioner and exit gate | Low | 2–3 d (port) | 2 |
| 4 | HTML | [`Broiler.HTML.Image/BCanvas.cs`](../../Broiler.HTML/Source/Broiler.HTML.Image/BCanvas.cs) — **DONE** | Was the same scanline shape; now `Parallel.For` over row bands via `BRasterParallelism`, with a measured area threshold and a per-band coverage buffer in `FillGlyphContours` | Nothing | **DONE, and smaller than the estimate.** Corpus `paint` page 1 594.7 ms → 1 096.5 ms (**1.42×** end to end, 1.61× on the stage) at 4 threads; **flat on `text`, `rules` and `boxes`, which split 0 fills between them** — their raster is glyphs, and a 95-pixel fill is not splittable at any core count. Pixels identical at 1/2/4 on all five pages. See [§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page) | Low | Done | 2 |
| 5 | Graphics / Layout | [`DisplayList.cs`](../../Broiler.Layout/Broiler.Layout/IR/DisplayList.cs) replayed by [`RGraphicsRasterBackend`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/RGraphicsRasterBackend.cs) — **DONE** | Was one pass over the whole surface; now `Parallel.For` over horizontal strips via [`TileParallelReplay`](../../Broiler.Layout/Broiler.Layout/IR/TileParallelReplay.cs), each replaying the whole list into its own strip | Nothing | **DONE, and the estimate was right about the ceiling for the wrong reason.** `PerformPaint` at 1 → 4 tiles: `paint` **1 323.7 → 461.4 ms (2.87×)**, `rules` 3.49×, `text` 2.42×, `mixed` 2.44×, `boxes` 1.76× on a 4-core box — and it beats band parallelism on all five pages. But on the pages taller than their viewport — which is most documents — a large share of it came from *not drawing invisible pixels* rather than from threads ([§8](#8-most-of-item-5-was-not-parallelism-it-was-the-rasterizer-drawing-pixels-nothing-could-see)). Pixels identical at 1/2/4 tiles × 1/4 bands; WPT output identical to the line | Low–Medium | Done | 2 |
| 6 | Media | [`JpegDecoder.cs:385`](../../Broiler.Media/Broiler.Media.Image.Managed/Jpeg/JpegDecoder.cs) dequantize+IDCT per block, `:424` upsample + YCbCr→RGB per row | Sequential | `Parallel.For` over blocks / rows. Entropy decode stays sequential (optionally per-RST-interval) | Nothing structural; blocks and output rows are disjoint | **DONE. Measured 2.08–2.61× at 4 threads** (gradient / flat-block fixtures, 1 024²), which lands inside the estimate. Byte-identical at 1, 2 and 4 threads. `JpegDct.Inverse` also took a caller-owned scratch buffer, removing a 512-byte allocation per block | Low | Done | 2 |
| 7 | Media | [`PngDecoder.cs:293`](../../Broiler.Media/Broiler.Media.Image.Managed/Png/PngDecoder.cs) expand-to-RGBA | Sequential | `Parallel.For` over rows | Inflate (`:62`) and unfilter (`:63`) are genuinely sequential — Up/Paeth read the previous row | **DONE. Measured 1.22–1.29× at 4 threads**, just under the estimate: inflate and unfilter are a larger share of a PNG decode than the estimate assumed, and both are on the do-not-parallelize list | Low | Done | 2 |
| 8 | Media / HTML | Per-image decode, driven from [`ImageLoadHandler.cs:177`](../../Broiler.HTML/Source/Broiler.HTML.Core/Handlers/ImageLoadHandler.cs) | Already `ThreadPool.QueueUserWorkItem` for file reads; decode is on the completion path | Decode *N* page images concurrently — coarser and better than #6/#7 | **Not what this cell said.** `BImageRenderer._images` is fixed and the decoders are covered by a re-entrancy test, but every headless entry point sets `AvoidAsyncImagesLoading`, so decode is synchronous and inline. Needs #2's prefetch/consume split fed by #17 ([§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor)) | **Near-linear in image count** up to core count | Low | 3–4 d, after #17 | 2 |
| 9 | Graphics | [`FontsHandler.cs`](../../Broiler.Graphics/Broiler.Graphics/Text/FontsHandler.cs), [`TrueTypeFont.cs`](../../Broiler.Graphics/Broiler.Graphics/Text/TrueTypeFont.cs), [`FallbackSystemFont.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/FallbackSystemFont.cs), [`BImageRenderer.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/BImageRenderer.cs) — **DONE** | Plain `Dictionary`, no synchronization; and two lazy-init latches published before their values | Not a speedup itself — a **hard prerequisite** for #10, #12, #13, and per Phase 1 §2 for *every* CPU-parallel render-path item | Nothing now | **DONE.** Nested map flattened to one concurrent dictionary; image table concurrent and handle allocation interlocked; replay transform state moved per-call; `TrueTypeFont`'s five lazy tables re-published through `Lazy` in `ExecutionAndPublication`. Both P0-c residuals closed with it. Enables ~4 items | Low | Done | 2 |
| 10 | Graphics | [`TrueTypeFont.GetGlyphContours`](../../Broiler.Graphics/Broiler.Graphics/Text/TrueTypeFont.cs) — **DONE**; [`ComplexTextShaper.Shape:72`](../../Broiler.Graphics/Broiler.Graphics/Text/ComplexTextShaper.cs) — not built | Outlines were re-extracted per glyph *occurrence*; shaping is called per run during layout | Concurrent cache by glyph index, published through `GetOrAdd` | Nothing | **The item was right that the cache is the whole win, and wrong about which cache.** Glyph outlines: raster stage **1.34×** (`text`), **1.54×** (`boxes`), 1.00× on the text-free `paint` control. The shaped-run cache is deliberately unbuilt — `RequiresShaping` is false for the whole Latin corpus, so it would measure nothing ([§5](#5-the-phases-largest-win-so-far-is-a-cache-and-not-the-one-item-10-names)) | Low | Done (outlines) | 2 |
| 11 | CSS | [`CssStyleEngine.CollectFromRules:623`](../../Broiler.CSS/Broiler.CSS.Dom/CssStyleEngine.cs) — linear scan of every rule of every sheet, per element | O(elements × rules) | **Not multithreading.** Rule index (bucket by id/class/tag) + ancestor bloom filter | Nothing — this is the standard engine design and it is simply absent | **DONE.** Exit gate met: with matches fixed at four, 32× the rules now costs 1.64× the time and 1.13× the bytes (was 30.8× / 32.0×) — up to **136.9× faster** and **600× less garbage** at 3 200 rules. On a whole render the corpus `rules` page is 5 218.96 ms → 1 841.71 ms (2.8×); see [What building Phase 1 changed](#what-building-phase-1-changed) §1. `patches/0123-css-cascade-rule-index.patch` | Low | Done | 1 |
| 12 | CSS | `GetComputedStyle:151` / `GetCascadedDeclarationMap:555` over the element set | Sequential per element; caches under one global `_sync` lock | Parallel style recalc over DOM subtrees, Stylo-style | Global lock over `_cache`/`_sparseCache`/`_declaredCascadeCache` becomes the bottleneck; needs sharding + per-thread L1. Do #11 first or you parallelize the wrong thing | **2–4× on styling** after #11 | Medium | 10–15 d | 3 |
| 13 | Layout | [`CssBox.PerformLayout:347`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.cs), [`PerformLayoutImp:37`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.Layout.cs) | Full-tree, in-place mutation, from the root every pass — **twice** when width is unrestricted ([`HtmlContainerInt.cs:929,936`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/HtmlContainerInt.cs)) | Parallel intrinsic sizing; parallel independent subtrees (abspos/fixed, flex+grid items, table cells, multicol, subdocuments) | Mutable shared tree; ambient thread-static state (`CssLengthParser` viewport, [`DocumentModeContext.cs:22`](../../Broiler.Layout/Broiler.Layout/DocumentModeContext.cs)); no dirty-bit invalidation to bound the work | **1.5–2.5×** of a stage **measured at 0.6–6.5% of a render** on every corpus page, including one built to load layout — so a few percent overall at best | **High** | 20–30 d | 4 |
| 14 | Layout | Same as #13 | No incremental invalidation | **Not multithreading.** Dirty bits + relayout roots | — | **5–50× on interactive relayout** — unmeasured, and the *first-render* layout it bounds is 0.6–6.5% of wall time; the interactive case this claims is not covered by the P0-a corpus. Also the precondition that makes #13 safe | Medium | 15–20 d | 3 |
| 15 | JS | [`JSPromise.Post:376`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Promise/JSPromise.cs), [`JSAsyncFunction.cs:152`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Function/JSAsyncFunction.cs), [`JSGenerator.cs:435`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Generator/JSGenerator.cs) — `ThreadPool.QueueUserWorkItem` when `sc == null` | JS continuations run on pool threads, racing main-thread layout | **Remove the parallelism.** Always pump a single-threaded event loop | This is the root cause behind WPT #1445 / #1143; the CSS `_sync` lock and the concurrent bridge memo maps are mitigations for it | Negative CPU gain, **large correctness gain**; removes lock overhead on hot cascade paths | Low | 5–8 d | 0 |
| 16 | JS | [`JSContext.cs:1562`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.Engine/JSContext.cs) `DictionaryCodeCache` (process-shared, already concurrent) | Scripts compiled on demand, serially | Background/parallel compile of independent `<script>` sources; overlap with parse and network | Cache is already concurrent; needs a compile-ahead queue fed by the preload scanner (#17) | **Removes compile from the critical path**; 1.5–3× on script-heavy first paint | Medium | 8–12 d | 3 |
| 17 | DOM / HTML | [`HtmlTokenizer.cs`](../../Broiler.DOM/Broiler.Dom.Html/HtmlTokenizer.cs), [`DomParser.cs`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/Parse/DomParser.cs) | Sequential (correctly — the HTML tokenizer is spec-sequential and cannot be parallelized) | **Speculative preload scan**: a worker scans raw bytes for `src`/`href` while the main parse runs, feeding #2 | Nothing; it is a read-only scan of an immutable byte buffer | Overlaps network with parse — compounds #2 rather than adding CPU | Low | 4–6 d | 2 |
| 18 | JS | New: `Worker` / `MessageChannel` | Not implemented | One `JSContext` per worker thread, structured-clone message passing | Per-context state exists and `JSEngine.CurrentContext` is `AsyncLocal`, so isolation is feasible; needs the static-mutable audit from P0-c across Broiler.JS | Capability, not a speedup of existing work | High | 30–40 d | 4 |
| 19 | Runtime | `Directory.Build.props` sets neither; `Broiler.JavaScript.Engine.Benchmarks.csproj` **does** set `ServerGarbageCollection` | Workstation GC defaults everywhere except that one benchmark project | Config knob; evaluate Server GC for headless/batch hosts | Nothing | **Measured: Server GC is 1.62× SLOWER** on a 4-core headless render (see below). Keep Workstation | Low | Done | 0 |
| 20 | Tooling | [`Broiler.Cli`](../../src/Broiler.Cli) — capture, document convert, layout fuzz — **DONE** | Repeatable inputs + `--output-dir`, `--threads` | Threads for document convert; **child processes** for capture and fuzz | **Not "nothing":** the render path's unsynchronised caches (item #9) make two in-process renders a race, so those two go through processes | Fuzz 14.8 s → 8.9 s at 4; capture byte-identical at 1 and 4. Per-page PDF is out of scope here — the CLI shells out to the standalone Broiler.Pdf app | Low | Done | 1 |
| 21 | Tests | [`Broiler.JavaScript.BuiltIns.Tests/AssemblyInfo.cs:3`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns.Tests/AssemblyInfo.cs) — `DisableTestParallelization = true` | Whole assembly serialized | Re-enable once #15 lands and per-test context isolation is confirmed | Almost certainly disabled *because* of #15 | Test-suite wall time | Low | 1–2 d | 3 |
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
2. **Make the font caches thread-safe** (item #9) — **DONE**. It was four sites,
   not one, and two of them were not caches: see
   [What building Phase 2 changed](#what-building-phase-2-changed) §1.
   *Exit gate — met.* `RenderPathConcurrencyTests`, 6 cases; 5 fail against the
   code before the change and pass after it, `Broiler.Graphics.Tests` 64/64 over
   five consecutive runs. Blocked items 10, 12, 13 — and, per Phase 1 §2, every
   CPU-parallel render-path item.
3. **Scanline-band parallelism inside the primitives** — **DONE for item #4**,
   the copy the profile measures; item #3's copy is an unstarted port.
   `BRasterParallelism` splits the `y` range, `FillGlyphContours`' coverage
   accumulator and crossing list moved inside the band, and the budget is
   `BROILER_RASTER_THREADS` (default one thread per core).
   *Exit gate — met.* Pixels identical at 1, 2 and 4 threads on all five corpus
   pages (`--raster-scaling`, which fails the run if any setting differs) and
   across 42 `RasterBandParallelismTests` cases covering every split primitive,
   including one under a clip; a budget of 1 splits nothing at all.
   **Read [§4](#4-band-parallelism-inside-a-primitive-is-the-wrong-unit-for-a-page)
   before porting it to item #3's copy.** It bought 1.42× on one corpus page and
   nothing on three, because those pages' raster is glyph fills of ~95 pixels and
   no threshold makes those splittable. The port is cheap now that the partitioner
   and the gate exist, but it should be scheduled for what it is — the Writer and
   Broiler.UI paths, which the corpus does not render — and not for a repeat of
   the 4–6× estimate.
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

2. **Shard the caches.** `_cache`, `_sparseCache`, and `_declaredCascadeCache`
   sit behind one `_sync`. Move to per-shard locks or `ConcurrentDictionary`
   with a per-thread first-level cache, keeping the existing generation-guard
   protocol (`CssStyleEngine.cs:611–618`) intact — it is what makes the
   lock-free compute window correct and must survive the change.
3. **Parallel style recalc** (item #12) over sibling subtrees, with a
   per-thread cache that publishes to the shared cache at subtree completion.
   `CssStyleEngineConcurrencyTests` already exercises concurrent access with
   `Parallel.For` — extend it into the acceptance harness for this item.
   *Exit gate:* identical computed style for the full WPT corpus at 1 and 8
   threads; measured scaling published; no regression in the single-thread path.

### Broiler.Layout

The most expensive and the least certain. Do it last, and do the sequential work
first.

1. **Stop laying out the whole tree twice.** `HtmlContainerInt.PerformLayout`
   runs `Root.PerformLayout` a second time whenever `MaxSize.Width <= 0.1`
   (shrink-to-fit). Caching intrinsic widths from the first pass removes a
   whole tree traversal before any threading is considered.
2. **Dirty-bit invalidation** (item #14). Today every relayout starts at the
   root. Relayout roots bound the work *and* give parallel layout its unit of
   independence — without them, "parallel subtree layout" has no subtrees to
   claim.
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
the ceiling here is low regardless of core count. Steps 1 and 2 are worth more
than steps 3 and 4 combined.

### Broiler.JS

1. **Item #15, the single-threaded event loop.** Correctness, and a
   prerequisite for the whole document.
2. **Re-enable test parallelization** (item #21) once contexts are isolated per
   test.
3. **Parallel/background script compile** (item #16), fed by the preload scanner
   so compilation overlaps download and parse. The `DictionaryCodeCache` is
   already process-shared and concurrent, which is the hard part.
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
2. **Speculative preload scan** (item #17) on a worker over the raw byte buffer,
   feeding step 1 and item #16. Read-only over an immutable buffer, so it is
   safe by construction. **It now also gates step 3**, which is why it moved
   above it.
3. **Concurrent image decode** (item #8). The decoder statics are cleared — they
   have a re-entrancy test — and `BImageRenderer._images` is fixed, but the
   headless render path loads images synchronously and inline, so this is the
   same prefetch/consume split as step 1 over the image URLs step 2 supplies:
   [§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor).

**Not recommended:** parallel HTML tokenization. The tokenizer is a
specification-mandated state machine whose transitions depend on the tree
builder's insertion mode. Speculation is the only correct form of parallelism
here, and step 2 is that.

### Broiler.Media

1. **Concurrent decode across images** (item #8) — still the better win when a
   page has several images, but it is a prefetch/consume split rather than the
   coarse `Parallel.For` this step used to describe, and it wants item #17 first:
   [§6](#6-item-8-is-a-prefetchconsume-split-not-a-parallelfor).
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
[§7](#7-two-kinds-of-parallelism-now-multiply-and-the-runner-has-to-divide).

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
| **2 — Raster, decode, text** — *in progress; 6 of 9 items done* | Rasterizer unification + band/tile parallelism (#3, #4, #5), font-cache safety (#9), text caches (#10), image decode (#6, #7, #8), preload scan (#17) | Largest CPU wins, disjoint memory, verifiable by exact pixel comparison. **#9 first, and it is done** — Phase 1 §2 made it the gate on every other item here, not just on #10/#12/#13. **What it changed, in the order it matters:** band parallelism inside a primitive turned out to be the wrong unit for a page — three of five corpus pages split zero fills, because their raster is glyphs — which promotes **#5 from "supersedes #3" to the only raster parallelism the corpus can use (§4)**; the phase's largest single win was a *cache*, and not the one #10 names (§5); #8 is a prefetch/consume split needing #17 first, not a `Parallel.For` (§6); the pool and the in-process threads multiply, so the runner now divides them (§7); the item-#9 findings (§1–§3) stand. **#5 landed and about half of its win was single-threaded** — the rasterizer was walking pixels its clip could never admit (§8) — and between them #4, #10 and #5 have taken raster from the largest stage on three pages to the largest on one (§9). **What is left:** #3's port, #17 then #8 — none of them the next thing to do. The largest open question is now the parse/cascade split Phase 1 §1 named, which gates #12. See [What building Phase 2 changed](#what-building-phase-2-changed). |
| **3 — Style and incremental layout** | Cache sharding + parallel style recalc (#12), layout dirty bits (#14), parallel script compile (#16), re-enable test parallelization (#21) | Depends on Phase 1's algorithmic fixes and Phase 0's determinism. |
| **4 — Parallel layout and workers** | Parallel intrinsic sizing and independent subtrees (#13), Web Workers (#18) | Highest cost, highest risk, lowest ceiling. Only worth starting once Phase 3's measurements say layout is still the bottleneck. |

**Global exit gate:** every parallel path has a `--threads 1` equivalent that
reproduces the sequential output exactly, and the WPT corpus produces identical
pass/fail classification at 1 and *N* threads across three consecutive runs.
Non-reproducible output is a regression regardless of how much faster it is.

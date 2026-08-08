# Multithreading analysis and roadmap

Where concurrency can make Broiler faster, where it cannot, and the order the
work has to happen in. Scope is every component in the aggregate repository plus
the tooling.

## Status

**Phase 1 is complete.** All four items landed; what each one turned out to be
worth is in [What building Phase 1 changed](#what-building-phase-1-changed).

| Item | State | Evidence |
|---|---|---|
| #1 — WPT worker pool | **Done** | `--workers <N>\|auto` in `src/Broiler.Wpt/Program.cs`; 45.2 s → 23.4 s on a 61-test subset at 4 cores, identical classification at 1, 4 and auto |
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

Where the memory figure cannot be read at all, the pool stays at **one** worker.
Guessing high on an unreadable budget is how a runner OOM-kills a CI box, and the
per-test allowance is a full GiB.

## Master table

Gain is per-stage unless stated. Effort is engineering days for one person
familiar with the component. Risk is the chance of introducing a
non-deterministic correctness defect.

| # | Component | Site | Currently | Parallel shape | What blocks it today | Est. gain (unmeasured) | Risk | Effort | Phase |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Tooling | [`Broiler.Wpt/Program.cs`](../../src/Broiler.Wpt/Program.cs) `RunDiscoveredTests` — **DONE** | Pool of *N* worker processes draining a shared queue (`--workers <N>\|auto`) | Work queue over *N* worker processes; results already buffered into `allResults` and sorted after the run, so order is not load-bearing | Nothing. Worker protocol is already one-command-per-line JSON over stdio | **Measured: 1.93× at 4 workers** on a 61-test subset (45.2 s → 23.4 s); identical classification at 1, 4 and auto. Bounded by RAM, not cores | Low | Done | 1 |
| 2 | HtmlBridge | [`ScriptExtractionService.cs:303`](../../src/Broiler.HtmlBridge.Core/Scripting/ScriptExtractionService.cs), [`ResourceLoader.cs:56`](../../src/Broiler.HtmlBridge.Dom/Runtime/ResourceLoader.cs), [`SubDocuments.cs:581`](../../src/Broiler.HtmlBridge.Dom/DomBridge/SubDocuments.cs) | Serial `GetStringAsync(..).GetAwaiter().GetResult()`, one resource at a time | Bounded-concurrency prefetch (6/host, browser convention) into a content-addressed cache; call sites then hit the cache | Call sites are synchronous and ordering-sensitive for `document.write`; needs a prefetch/consume split, not an `async` conversion | **Done for scripts and `<link>` stylesheets** (`SubResourcePrefetcher`, bounded 6/host). Iframes and `fetch()`/XHR are not wired: they have no point where the URL set is known before it is consumed, which is what item #17 supplies | Medium | Done (scripts, sheets) | 1 |
| 3 | Graphics | [`Rendering/BCanvas.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/BCanvas.cs) `FillRect:106`, `FillGlyphContours:241`, `DrawBitmap:345`, `FillRectTiled:389`, `FillLinear/Radial/ConicGradientRect:425/468/508` | Single-threaded scanline loops | `Parallel.For` over scanline bands; per-band coverage buffer and clip stack | Per-row `coverage[]` and the `_clipOperations` list are instance state shared across rows — must become per-worker | **4–6× on 8 cores** (estimate) of a stage **measured at 46.9–80.8% of a render** — the largest measured share in the document | Low | 4–6 d | 2 |
| 4 | HTML | [`Broiler.HTML.Image/BCanvas.cs`](../../Broiler.HTML/Source/Broiler.HTML.Image/BCanvas.cs) `:128/262/364/406/456/508/554/655` | Same scanline shape, second copy of the rasterizer | Same as #3 | Same as #3, plus the duplication itself | Same as #3 | Low | 3–4 d (or 0, if unified with #3 first) | 2 |
| 5 | Graphics / Layout | [`DisplayList.cs`](../../Broiler.Layout/Broiler.Layout/IR/DisplayList.cs) + [`BRenderList.cs`](../../Broiler.Graphics/Broiler.Graphics/RenderList/BRenderList.cs) replayed by [`RGraphicsRasterBackend`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/RGraphicsRasterBackend.cs) | One pass, whole surface | **Tile-parallel replay**: partition the target into tiles, each worker replays the whole immutable list clipped to its tile | Already immutable and flat with explicit `ClipItem`/`RestoreItem`/`OpacityItem` stack items — this is the right structure. Needs per-tile state and layer (`OpacityItem`) handling | **5–7× on 8 cores** (estimate) for full-page renders; better locality than #3. **Measured:** the replay is 46.9–80.8% of a render while the display-list *build* is 0.3–0.7%, so only the replay is worth parallelizing | Low–Medium | 6–10 d | 2 |
| 6 | Media | [`JpegDecoder.cs:385`](../../Broiler.Media/Broiler.Media.Image.Managed/Jpeg/JpegDecoder.cs) dequantize+IDCT per block, `:424` upsample + YCbCr→RGB per row | Sequential | `Parallel.For` over blocks / rows. Entropy decode stays sequential (optionally per-RST-interval) | Nothing structural; blocks and output rows are disjoint | **2–2.5× on decode** (Amdahl: Huffman ≈35% stays serial) | Low | 2–3 d | 2 |
| 7 | Media | [`PngDecoder.cs:293`](../../Broiler.Media/Broiler.Media.Image.Managed/Png/PngDecoder.cs) expand-to-RGBA | Sequential | `Parallel.For` over rows | Inflate (`:62`) and unfilter (`:63`) are genuinely sequential — Up/Paeth read the previous row | **1.3–1.6× on decode** only | Low | 1–2 d | 2 |
| 8 | Media / HTML | Per-image decode, driven from [`ImageLoadHandler.cs:177`](../../Broiler.HTML/Source/Broiler.HTML.Core/Handlers/ImageLoadHandler.cs) | Already `ThreadPool.QueueUserWorkItem` for file reads; decode is on the completion path | Decode *N* page images concurrently — coarser and better than #6/#7 | Decoder statics must be verified re-entrant; `BImageRenderer._images` is a plain `Dictionary` | **Near-linear in image count** up to core count | Low | 2–3 d | 2 |
| 9 | Graphics | [`FontsHandler.cs:13`](../../Broiler.Graphics/Broiler.Graphics/Text/FontsHandler.cs) `_fontsCache`, `_featuredFontsCache`; [`TrueTypeFont.cs`](../../Broiler.Graphics/Broiler.Graphics/Text/TrueTypeFont.cs) | Plain `Dictionary`, no synchronization | Not a speedup itself — a **hard prerequisite** for #10, #12, #13 | Nested `Dictionary<string, Dictionary<double, Dictionary<FontStyle, RFont>>>` corrupts under concurrent read/write | Enables ~3 items | Low | 2 d | 2 |
| 10 | Graphics | [`ComplexTextShaper.Shape:72`](../../Broiler.Graphics/Broiler.Graphics/Text/ComplexTextShaper.cs) | Called per run during layout | Already a static pure function → parallel-safe. Add a shaped-run cache keyed by (font, text, features) | Needs #9 | **Cache alone is the bigger win**; parallel shaping 3–5× on the shaping stage | Low | 3–4 d | 2 |
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
2. **Make the font caches thread-safe** (item #9). Blocks items 10, 12, 13.
3. **Scanline-band parallelism inside the primitives** (item #3): hoist
   `coverage[]` and the clip stack to per-band state, `Parallel.For` over bands
   with a minimum band height so small fills stay on one thread.
   *Exit gate:* WPT pixel results byte-identical to the sequential path; a
   `--raster-threads 1` switch reproduces the sequential output exactly.
4. **Tile-parallel display-list replay** (item #5). This supersedes step 3 for
   whole-page renders — step 3 remains the right answer for a single large
   primitive. Handle `OpacityItem`/`BlendModeItem` layers by rendering the layer
   into a tile-local scratch buffer.
   *Exit gate:* identical output at 1, 2, 4, and 8 tiles; measured scaling curve
   published.
5. **Shaped-run cache + parallel shaping** (item #10). Measure the cache alone
   before adding threads; it may be the whole win.

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
   safe by construction.
3. **Concurrent image decode** (item #8) once the decoder statics are cleared by
   P0-c.

**Not recommended:** parallel HTML tokenization. The tokenizer is a
specification-mandated state machine whose transitions depend on the tree
builder's insertion mode. Speculation is the only correct form of parallelism
here, and step 2 is that.

### Broiler.Media

1. **Concurrent decode across images** (item #8) — coarse, simple, and the
   better win when a page has several images.
2. **JPEG intra-image** (item #6): `Parallel.For` over blocks for
   dequantize + IDCT, and over rows for upsample + YCbCr→RGB. Optionally split
   entropy decode at restart-interval markers when the stream has them.
3. **PNG intra-image** (item #7): only the expand-to-RGBA pass. Inflate and
   unfilter carry real sequential dependencies (Up/Paeth read the previous row)
   — do not attempt them.
   *Exit gate for both:* decoded output byte-identical to the sequential
   decoder across the existing `Broiler.Media.Image.Managed.Tests` corpus.

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
| **2 — Raster, decode, text** | Rasterizer unification + band/tile parallelism (#3, #4, #5), font-cache safety (#9), shaped-run cache (#10), image decode (#6, #7, #8), preload scan (#17) | Largest CPU wins, disjoint memory, verifiable by exact pixel comparison. |
| **3 — Style and incremental layout** | Cache sharding + parallel style recalc (#12), layout dirty bits (#14), parallel script compile (#16), re-enable test parallelization (#21) | Depends on Phase 1's algorithmic fixes and Phase 0's determinism. |
| **4 — Parallel layout and workers** | Parallel intrinsic sizing and independent subtrees (#13), Web Workers (#18) | Highest cost, highest risk, lowest ceiling. Only worth starting once Phase 3's measurements say layout is still the bottleneck. |

**Global exit gate:** every parallel path has a `--threads 1` equivalent that
reproduces the sequential output exactly, and the WPT corpus produces identical
pass/fail classification at 1 and *N* threads across three consecutive runs.
Non-reproducible output is a regression regardless of how much faster it is.

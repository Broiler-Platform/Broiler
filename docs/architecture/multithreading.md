# Multithreading analysis and roadmap

Where concurrency can make Broiler faster, where it cannot, and the order the
work has to happen in. Scope is every component in the aggregate repository plus
the tooling.

## Method and honesty caveat

This analysis is **structural, not measured**. It comes from reading the hot
paths and classifying their data dependencies — which loops write disjoint
memory, which caches are shared, which stages are genuinely sequential. The
repository has five BenchmarkDotNet projects
(`Broiler.JavaScript.Engine.Benchmarks`, two Unicode ones, two formatting-code
phases) and **none of them cover cascade, layout, raster, or image decode**, so
no stage-level profile exists to attribute wall time to.

Every "estimated gain" below is therefore a bound derived from the shape of the
code (loop independence, Amdahl ceiling given the sequential remainder), not an
observation. **Phase 0 exists to replace those estimates with numbers**, and no
item past Phase 0 should be started before its stage has a benchmark. Per the
[documentation rules](../README.md#documentation-rules), an estimate is not
evidence.

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

## Master table

Gain is per-stage unless stated. Effort is engineering days for one person
familiar with the component. Risk is the chance of introducing a
non-deterministic correctness defect.

| # | Component | Site | Currently | Parallel shape | What blocks it today | Est. gain (unmeasured) | Risk | Effort | Phase |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Tooling | [`Broiler.Wpt/Program.cs:359`](../../src/Broiler.Wpt/Program.cs) — `foreach (var testPath in discoveredTests)` with one `WptWorkerClient` | Sequential, 1 worker process, ≤30 s/test | Work queue over *N* worker processes; results already buffered into `allResults` and sorted after the run, so order is not load-bearing | Nothing. Worker protocol is already one-command-per-line JSON over stdio | **Near-linear in worker count**; bounded by RAM, not cores (§Phase 1) | Low | 2–3 d | 1 |
| 2 | HtmlBridge | [`ScriptExtractionService.cs:303`](../../src/Broiler.HtmlBridge.Core/Scripting/ScriptExtractionService.cs), [`ResourceLoader.cs:56`](../../src/Broiler.HtmlBridge.Dom/Runtime/ResourceLoader.cs), [`SubDocuments.cs:581`](../../src/Broiler.HtmlBridge.Dom/DomBridge/SubDocuments.cs) | Serial `GetStringAsync(..).GetAwaiter().GetResult()`, one resource at a time | Bounded-concurrency prefetch (6/host, browser convention) into a content-addressed cache; call sites then hit the cache | Call sites are synchronous and ordering-sensitive for `document.write`; needs a prefetch/consume split, not an `async` conversion | **Page-load latency 3–6×** on multi-resource pages (latency, not CPU) | Medium | 5–8 d | 1 |
| 3 | Graphics | [`Rendering/BCanvas.cs`](../../Broiler.Graphics/Broiler.Graphics/Rendering/BCanvas.cs) `FillRect:106`, `FillGlyphContours:241`, `DrawBitmap:345`, `FillRectTiled:389`, `FillLinear/Radial/ConicGradientRect:425/468/508` | Single-threaded scanline loops | `Parallel.For` over scanline bands; per-band coverage buffer and clip stack | Per-row `coverage[]` and the `_clipOperations` list are instance state shared across rows — must become per-worker | **4–6× on 8 cores** (memory-bandwidth bound for large fills, core-bound for glyphs/gradients) | Low | 4–6 d | 2 |
| 4 | HTML | [`Broiler.HTML.Image/BCanvas.cs`](../../Broiler.HTML/Source/Broiler.HTML.Image/BCanvas.cs) `:128/262/364/406/456/508/554/655` | Same scanline shape, second copy of the rasterizer | Same as #3 | Same as #3, plus the duplication itself | Same as #3 | Low | 3–4 d (or 0, if unified with #3 first) | 2 |
| 5 | Graphics / Layout | [`DisplayList.cs`](../../Broiler.Layout/Broiler.Layout/IR/DisplayList.cs) + [`BRenderList.cs`](../../Broiler.Graphics/Broiler.Graphics/RenderList/BRenderList.cs) replayed by [`RGraphicsRasterBackend`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/IR/RGraphicsRasterBackend.cs) | One pass, whole surface | **Tile-parallel replay**: partition the target into tiles, each worker replays the whole immutable list clipped to its tile | Already immutable and flat with explicit `ClipItem`/`RestoreItem`/`OpacityItem` stack items — this is the right structure. Needs per-tile state and layer (`OpacityItem`) handling | **5–7× on 8 cores** for full-page renders; better locality than #3 | Low–Medium | 6–10 d | 2 |
| 6 | Media | [`JpegDecoder.cs:385`](../../Broiler.Media/Broiler.Media.Image.Managed/Jpeg/JpegDecoder.cs) dequantize+IDCT per block, `:424` upsample + YCbCr→RGB per row | Sequential | `Parallel.For` over blocks / rows. Entropy decode stays sequential (optionally per-RST-interval) | Nothing structural; blocks and output rows are disjoint | **2–2.5× on decode** (Amdahl: Huffman ≈35% stays serial) | Low | 2–3 d | 2 |
| 7 | Media | [`PngDecoder.cs:293`](../../Broiler.Media/Broiler.Media.Image.Managed/Png/PngDecoder.cs) expand-to-RGBA | Sequential | `Parallel.For` over rows | Inflate (`:62`) and unfilter (`:63`) are genuinely sequential — Up/Paeth read the previous row | **1.3–1.6× on decode** only | Low | 1–2 d | 2 |
| 8 | Media / HTML | Per-image decode, driven from [`ImageLoadHandler.cs:177`](../../Broiler.HTML/Source/Broiler.HTML.Core/Handlers/ImageLoadHandler.cs) | Already `ThreadPool.QueueUserWorkItem` for file reads; decode is on the completion path | Decode *N* page images concurrently — coarser and better than #6/#7 | Decoder statics must be verified re-entrant; `BImageRenderer._images` is a plain `Dictionary` | **Near-linear in image count** up to core count | Low | 2–3 d | 2 |
| 9 | Graphics | [`FontsHandler.cs:13`](../../Broiler.Graphics/Broiler.Graphics/Text/FontsHandler.cs) `_fontsCache`, `_featuredFontsCache`; [`TrueTypeFont.cs`](../../Broiler.Graphics/Broiler.Graphics/Text/TrueTypeFont.cs) | Plain `Dictionary`, no synchronization | Not a speedup itself — a **hard prerequisite** for #10, #12, #13 | Nested `Dictionary<string, Dictionary<double, Dictionary<FontStyle, RFont>>>` corrupts under concurrent read/write | Enables ~3 items | Low | 2 d | 2 |
| 10 | Graphics | [`ComplexTextShaper.Shape:72`](../../Broiler.Graphics/Broiler.Graphics/Text/ComplexTextShaper.cs) | Called per run during layout | Already a static pure function → parallel-safe. Add a shaped-run cache keyed by (font, text, features) | Needs #9 | **Cache alone is the bigger win**; parallel shaping 3–5× on the shaping stage | Low | 3–4 d | 2 |
| 11 | CSS | [`CssStyleEngine.CollectFromRules:623`](../../Broiler.CSS/Broiler.CSS.Dom/CssStyleEngine.cs) — linear scan of every rule of every sheet, per element | O(elements × rules) | **Not multithreading.** Rule index (bucket by id/class/tag) + ancestor bloom filter | Nothing — this is the standard engine design and it is simply absent | **10–100× on rule-heavy pages**, single-threaded. Dwarfs any parallel styling | Low | 8–12 d | 1 |
| 12 | CSS | `GetComputedStyle:151` / `GetCascadedDeclarationMap:555` over the element set | Sequential per element; caches under one global `_sync` lock | Parallel style recalc over DOM subtrees, Stylo-style | Global lock over `_cache`/`_sparseCache`/`_declaredCascadeCache` becomes the bottleneck; needs sharding + per-thread L1. Do #11 first or you parallelize the wrong thing | **2–4× on styling** after #11 | Medium | 10–15 d | 3 |
| 13 | Layout | [`CssBox.PerformLayout:347`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.cs), [`PerformLayoutImp:37`](../../Broiler.Layout/Broiler.Layout/Engine/CssBox.Layout.cs) | Full-tree, in-place mutation, from the root every pass — **twice** when width is unrestricted ([`HtmlContainerInt.cs:929,936`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/HtmlContainerInt.cs)) | Parallel intrinsic sizing; parallel independent subtrees (abspos/fixed, flex+grid items, table cells, multicol, subdocuments) | Mutable shared tree; ambient thread-static state (`CssLengthParser` viewport, [`DocumentModeContext.cs:22`](../../Broiler.Layout/Broiler.Layout/DocumentModeContext.cs)); no dirty-bit invalidation to bound the work | **1.5–2.5×** — block flow is sequential in the block direction, so the ceiling is low | **High** | 20–30 d | 4 |
| 14 | Layout | Same as #13 | No incremental invalidation | **Not multithreading.** Dirty bits + relayout roots | — | **5–50× on interactive relayout**; also the precondition that makes #13 safe | Medium | 15–20 d | 3 |
| 15 | JS | [`JSPromise.Post:376`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Promise/JSPromise.cs), [`JSAsyncFunction.cs:152`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Function/JSAsyncFunction.cs), [`JSGenerator.cs:435`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns/Generator/JSGenerator.cs) — `ThreadPool.QueueUserWorkItem` when `sc == null` | JS continuations run on pool threads, racing main-thread layout | **Remove the parallelism.** Always pump a single-threaded event loop | This is the root cause behind WPT #1445 / #1143; the CSS `_sync` lock and the concurrent bridge memo maps are mitigations for it | Negative CPU gain, **large correctness gain**; removes lock overhead on hot cascade paths | Low | 5–8 d | 0 |
| 16 | JS | [`JSContext.cs:1562`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.Engine/JSContext.cs) `DictionaryCodeCache` (process-shared, already concurrent) | Scripts compiled on demand, serially | Background/parallel compile of independent `<script>` sources; overlap with parse and network | Cache is already concurrent; needs a compile-ahead queue fed by the preload scanner (#17) | **Removes compile from the critical path**; 1.5–3× on script-heavy first paint | Medium | 8–12 d | 3 |
| 17 | DOM / HTML | [`HtmlTokenizer.cs`](../../Broiler.DOM/Broiler.Dom.Html/HtmlTokenizer.cs), [`DomParser.cs`](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/Parse/DomParser.cs) | Sequential (correctly — the HTML tokenizer is spec-sequential and cannot be parallelized) | **Speculative preload scan**: a worker scans raw bytes for `src`/`href` while the main parse runs, feeding #2 | Nothing; it is a read-only scan of an immutable byte buffer | Overlaps network with parse — compounds #2 rather than adding CPU | Low | 4–6 d | 2 |
| 18 | JS | New: `Worker` / `MessageChannel` | Not implemented | One `JSContext` per worker thread, structured-clone message passing | Per-context state exists and `JSEngine.CurrentContext` is `AsyncLocal`, so isolation is feasible; needs the static-mutable audit from P0-c across Broiler.JS | Capability, not a speedup of existing work | High | 30–40 d | 4 |
| 19 | Runtime | `Directory.Build.props` — no `ServerGarbageCollection` / `ConcurrentGarbageCollection` setting anywhere | Workstation GC defaults | Config knob; evaluate Server GC for headless/batch hosts and keep Workstation for interactive | Nothing | Unknown until measured; historically 1.1–1.4× on allocation-heavy batch work | Low | 0.5 d + measurement | 0 |
| 20 | Tooling | [`Broiler.Cli`](../../src/Broiler.Cli) — capture, document convert, layout fuzz | Sequential per input | Per-file / per-page parallelism | Nothing | Near-linear in file count | Low | 2–3 d | 1 |
| 21 | Tests | [`Broiler.JavaScript.BuiltIns.Tests/AssemblyInfo.cs:3`](../../Broiler.JS/Broiler.JS/Broiler.JavaScript.BuiltIns.Tests/AssemblyInfo.cs) — `DisableTestParallelization = true` | Whole assembly serialized | Re-enable once #15 lands and per-test context isolation is confirmed | Almost certainly disabled *because* of #15 | Test-suite wall time | Low | 1–2 d | 3 |
| 22 | Input | [`Broiler.Input.*`](../../Broiler.Input) — Linux keyboard `Task.Run`, Windows camera `new Thread`, `lock (_gate)` throughout | **Already correctly threaded** | — | — | None available | — | — | — |
| 23 | UI | [`Broiler.UI`](../../Broiler.UI/src) measure/arrange over the widget tree | Sequential | Parallel measure is possible but widget trees are small (~hundreds of nodes) | — | Negligible; the real win is keeping layout/paint off the input thread (responsiveness, not throughput) | — | — | Not recommended |
| 24 | Documents | [`DocxReader`](../../Broiler.Documents/Broiler.Documents.Docx/DocxReader.cs), `RtfReader`, `HtmlReader`, `MarkdownReader` | Sequential single-document parsers | Batch conversion only — covered by #20 | — | None intra-document | — | — | Not recommended |

## Cross-cutting prerequisites

These gate everything below them and are not optional.

### P0-a — A stage-level benchmark suite

**Current evidence:** no benchmark covers cascade, layout, raster, or decode.
Every number in the table above is an estimate.

**Next action:** add BenchmarkDotNet harnesses under `Broiler.Benchmarks.slnx`
for (1) cascade + computed style over a representative DOM, (2) `PerformLayout`
over the same, (3) display-list raster at 1280×1024, (4) PNG/JPEG decode, (5)
end-to-end headless render of a fixed page set. Report per-stage share of wall
time.

**Exit gate:** a published profile that attributes ≥90% of headless-render wall
time to named stages, reproducible from one command.

### P0-b — Single-threaded determinism first (item #15)

**Current evidence:** `JSPromise.Post` falls back to
`ThreadPool.QueueUserWorkItem` when no `SynchronizationContext` is installed.
The comment block at
[`CssStyleEngine.cs:26–37`](../../Broiler.CSS/Broiler.CSS.Dom/CssStyleEngine.cs)
records the consequence: "JS continuations dispatched on ThreadPool threads run
that computed-style/geometry work concurrently with the main-thread layout pass
— a plain Dictionary/List corrupts under that race and aborts the process".

**Next action:** install a single-threaded pump for every embedding (the
`AsyncPump` machinery already exists in
`Broiler.JavaScript.Runtime/AsyncPump.cs`) and make the `sc == null` fallback an
error rather than a thread-pool dispatch. Then re-measure whether the CSS engine
still needs `_sync` at all.

**Exit gate:** no engine callback runs on a thread other than the one that owns
the document; the WPT suite is stable across 3 consecutive runs; the mitigation
locks are either removed or justified by a *deliberate* threading design.

### P0-c — Shared-cache thread-safety audit

**Current evidence:** unsynchronized mutable caches on paths that parallel work
would reach — `FontsHandler._fontsCache` (nested `Dictionary`),
`BImageRenderer._images`, the `RFont`/glyph caches. Files declaring at least one
mutable private static, by component (a scope estimate, not a site count):
Broiler.JS 306, `src/` 118, Broiler.HTML 67, Broiler.UI 56, Broiler.Documents
46, Broiler.CSS 38, Broiler.Graphics 37, Broiler.Layout 34.

Some ambient state is *already* thread-affine and must stay that way —
[`SvgFilterTable`](../../Broiler.Layout/Broiler.Layout/IR/SvgFilterTable.cs) and
`DocumentModeContext` are `[ThreadStatic]`, and `CssLengthParser` keeps a
thread-static viewport. Any worker thread that runs layout or paint must
initialize all three or it will silently read another document's state.

**Next action:** enumerate the mutable statics on the render path, classify each
as immutable / thread-static / needs-synchronization, and record the ambient
state a worker thread must establish before it may run layout or paint.

**Exit gate:** a documented list, and a debug-mode assertion that fires when a
thread-static ambient value is read before being set on that thread.

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

1. **Rule indexing first** (item #11) — bucket rules by rightmost simple
   selector (id / class / tag / universal) and add an ancestor bloom filter, so
   an element tests a handful of candidate rules instead of all of them. This is
   single-threaded and is expected to dominate every parallel option.
   *Exit gate:* cascade benchmark from P0-a shows the per-element rule tests
   scaling with matched rules, not total rules; computed-style output unchanged
   across the CSS + WPT suites.
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
| **1 — Free wins and the sequential fixes** | WPT worker pool (#1), CLI batch (#20), concurrent sub-resource fetch (#2), CSS rule indexing (#11) | Cheap, low-risk, and #1 shortens the feedback loop for everything else. #11 is single-threaded but must precede #12. |
| **2 — Raster, decode, text** | Rasterizer unification + band/tile parallelism (#3, #4, #5), font-cache safety (#9), shaped-run cache (#10), image decode (#6, #7, #8), preload scan (#17) | Largest CPU wins, disjoint memory, verifiable by exact pixel comparison. |
| **3 — Style and incremental layout** | Cache sharding + parallel style recalc (#12), layout dirty bits (#14), parallel script compile (#16), re-enable test parallelization (#21) | Depends on Phase 1's algorithmic fixes and Phase 0's determinism. |
| **4 — Parallel layout and workers** | Parallel intrinsic sizing and independent subtrees (#13), Web Workers (#18) | Highest cost, highest risk, lowest ceiling. Only worth starting once Phase 3's measurements say layout is still the bottleneck. |

**Global exit gate:** every parallel path has a `--threads 1` equivalent that
reproduces the sequential output exactly, and the WPT corpus produces identical
pass/fail classification at 1 and *N* threads across three consecutive runs.
Non-reproducible output is a regression regardless of how much faster it is.

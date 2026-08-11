# Per-render fixed cost, and where a WPT test's time actually goes

Two harnesses, one question: `wpt-sequential-wins.md` measured a reftest at ~2.2–2.5 CPU-s,
ruled out I/O and process startup, and attributed the remainder to per-render fixed cost
**"inside the engine"**. That was an inference. It is wrong, and both halves of this file
exist because measuring it was cheap and believing it was not.

- `--render-fixed-cost` in `tests/render-stages` measures the engine side.
- `--phase-trace` in `src/Broiler.Wpt` measures what the runner spends around it.

## Part 1 — the engine: an empty render costs 3.5 ms

Through `HtmlRender.RenderToImageWithStyleSet` at 1024x768 — the call `WptTestRunner` makes —
sweeping document size, sizes interleaved, 21 iterations, medians:

| boxes | bytes | one-shot ms | ctor | set html | layout | paint | bitmap | dispose | staged total |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 145 | **3.48** | 0.00 | 0.58 | 0.09 | 0.02 | 2.32 | 0.00 | 3.02 |
| 1 | 171 | **6.94** | 0.01 | 0.72 | 0.11 | 0.47 | 4.76 | 0.00 | 6.06 |
| 2 | 197 | **4.37** | 0.00 | 0.85 | 0.12 | 0.74 | 2.56 | 0.00 | 4.28 |
| 5 | 275 | **6.52** | 0.00 | 1.08 | 0.17 | 1.74 | 3.26 | 0.00 | 6.27 |
| 10 | 405 | **7.70** | 0.00 | 1.72 | 0.25 | 2.78 | 2.10 | 0.00 | 6.86 |
| 25 | 810 | **14.65** | 0.00 | 3.58 | 0.52 | 3.87 | 4.21 | 0.00 | 12.19 |
| 50 | 1485 | **19.19** | 0.00 | 8.35 | 1.01 | 4.63 | 2.47 | 0.01 | 16.48 |
| 100 | 2835 | **25.33** | 0.00 | 9.62 | 1.88 | 4.59 | 3.67 | 0.02 | 19.78 |
| 250 | 7035 | **45.83** | 0.00 | 26.54 | 4.62 | 5.16 | 2.24 | 0.05 | 38.61 |
| 500 | 14035 | **101.12** | 0.00 | 66.27 | 11.66 | 6.23 | 3.28 | 0.11 | 87.55 |

`one-shot ms = 6.06 + 0.19 * boxes`

- **Per-render fixed cost (intercept): 6.06 ms**; measured directly at zero boxes, **3.48 ms**
- Per-box cost (slope): 0.19 ms
- A WPT-median document (1 018 bytes ≈ 25–50 boxes here) renders in **15–19 ms**

Where the empty render's 3 ms goes — and it is not the parts this roadmap has been optimising:

| step | ms | share |
|---|---:|---:|
| bitmap alloc + erase | 2.32 | **77.0%** |
| SetHtmlWithStyleSet | 0.58 | 19.2% |
| PerformLayout | 0.09 | 3.1% |
| PerformPaint | 0.02 | 0.6% |
| container ctor | 0.00 | 0.1% |
| dispose | 0.00 | 0.0% |
| **staged total** | **3.02** | |
| one-shot, same document | 3.48 | (0.46 unattributed — the staged pass replicates `RenderToImageCore` rather than calling it) |

Three quarters of an empty render is allocating and clearing a 3 MB bitmap. **Nothing here is
close to a second.** The inference was off by two orders of magnitude.

## Part 2 — the runner: the render is 1.6–2.0% of a WPT test

`--phase-trace --no-worker-isolation --workers 1`, so one process and one thread and wall ≈ CPU:

| phase | `css-backgrounds/animations` (41 tests) | | `css-fonts` (373 tests) | |
|---|---:|---:|---:|---:|
| | **share** | ms/call | **share** | ms/call |
| file read | 0.0% | 0.21 | 0.0% | 0.12 |
| font registration | 0.0% | 1.10 | 0.0% | 0.33 |
| **scripts + DOM bridge** | **76.0%** | **927.93** | **79.3%** | **703.79** |
| post-process | 0.0% | 0.35 | 0.0% | 0.13 |
| **render** | **1.6%** | **19.46** | **2.0%** | **17.71** |
| **pixel compare** | **20.5%** | **501.26** | **15.9%** | **326.74** |
| failure diagnostics | 0.1% | 3.50 | 0.0% | 4.33 |
| (unattributed) | 1.7% | | 2.7% | |
| attributed | 98.3% | | 97.3% | |

The two subsets were chosen to differ and they agree. And the `render` column —
19.46 and 17.71 ms/call — **independently reproduces Part 1's 15–19 ms** for a document of
that size, by a completely different method. That agreement is the reason to trust both.

### What is actually expensive

**`ExecuteScriptsWithDom` — 76–79%, ~0.7–0.9 s per render.** Every render builds a DOM, runs
the document's classic scripts through the JS engine, and serializes the mutated tree back to
markup — twice per reftest, since the reference goes through the same helper. This is the WPT
run, in wall-clock terms. Nothing in the multithreading roadmap touches it.

**`PixelDiffRunner.Compare` — 16–21%, ~0.3–0.5 s per comparison.** For 1024x768 that is 786 432
pixels through a per-pixel `GetPixel` on *both* bitmaps and a `SetPixel` on a diff bitmap — for
every pixel, matching or not. Two details make it worse than it needs to be:

- the diff bitmap is allocated and written **unconditionally**, then disposed on the `isMatch`
  path — so the ~62% of tests that pass pay for a 3 MB image nobody looks at;
- the per-pixel accessor pattern is the same shape item #3 already found and fixed in the
  rasterizer, where hoisting a per-pixel lookup out of the loop was worth 1.58–2.96x on its own.

## What this changes

`wpt-sequential-wins.md` closed by saying the target for WPT wall clock was "per-render fixed
cost inside the engine". **It is not.** The engine's per-render fixed cost is 3.5 ms and its
whole render is 1.6–2.0% of a test. The targets are the script/DOM pass and the pixel
comparison, which are 92–97% of a run between them, and neither is a rendering problem.

It also closes out the question this phase opened with. Phases 2 and 3 measured zero on WPT not
because their wins were small but because **they were aimed at 2% of the run**, and only at the
page-proportional part of that 2%. No amount of engine parallelism or engine sequential work
could have shown up.

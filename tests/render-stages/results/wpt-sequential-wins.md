# What Phases 2 and 3 are worth on a WPT run

Phase 1 §4 published **90 min → 45** for the WPT worker pool (item #1) and this
document then read that figure as a *ratio* — `--workers 1` against `--workers 4`.
It is, but the tree it was measured on was not the one assumed: `ca53d44` contains
**Phase 2 item #9 and nothing else** from Phases 2–3, and item #9 is a thread-safety
prerequisite that the master table records as "not a speedup itself". So both
endpoints of that ratio are an essentially **end-of-Phase-1** engine, and the
sequential wins Phases 2 and 3 landed afterwards are absent from both.

That left an open question this file answers: **the pool's 2.0× is measured, but the
absolute endpoints were never re-measured, and every sequential win since should have
moved them down.**

They did not move them at all.

## Setup

| | |
|---|---|
| Baseline | `ca53d44` — the tree the 90/45 figure was measured on, with its own submodule pointers (`Broiler.CSS` at the rule index, `Broiler.Graphics` at item #9, `Broiler.HTML` pre-#4/#5) |
| Head | `efdcb35` — after Phases 2 and 3 |
| Suite | WPT **reftests** (`--reftests-only`), which render both sides with Broiler and need no Chromium references |
| Config | `--workers 4` on 4 cores — the default full-run shape. HEAD's header confirms `Render threads: 1 per worker`, so every in-process budget is 1 and both trees are sequential per render |
| Order | A,B,B,A within each suite, so monotone host drift cancels between trees rather than landing on whichever ran second |

## Result: no win, on either kind of page

| suite | reftests | baseline (s) | head (s) | head/baseline | classification |
|---|---:|---|---|---:|---|
| `css/css-backgrounds` | 713 | 368.5, 367.1 (**367.8**) | 377.3, 371.1 (**374.2**) | **1.017** | identical — 443 passed / 267 failed / 1 skipped, all four runs |
| `css/css-fonts` + `css/css-writing-modes` | 1 500 | 781.3, 788.0 (**784.7**) | 792.1, 797.3 (**794.7**) | **1.013** | identical — 685 passed / 815 failed, all four runs |

**The 1.3–1.7% is not a regression, and the file will not claim one.** A later HEAD
pair on the same `css-backgrounds` subset read **393.7 / 399.4 s** — 5–7% slower than
the same tree, same command, ninety minutes earlier. The deltas above sit inside that
band. What is outside the band is the size of the effect that was being looked for:
nothing resembling a Phase-2-and-3-sized win is there on either suite.

The two suites were chosen to disagree if the result depended on page character.
`css-backgrounds` is the best case for the raster wins (#4, #5) and `css-fonts` +
`css-writing-modes` for the glyph-outline cache (#10). They agree.

## Why: the run is CPU-bound, and the CPU is not doing what the wins made faster

**It is not I/O or process overhead.** At 4 workers the run takes 359% of 4 cores
(33.3 s wall against 119.4 CPU-s) — 90% utilization. And the per-process fixed cost
is negligible: a run that discovers **zero** tests costs **1.69 CPU-s**, which is 1.8%
of the 41-test run it was subtracted from.

So the time is per-test work:

| tests | CPU-s | per test |
|---:|---:|---:|
| 0 | 1.69 | — (fixed) |
| 20 | 50.96 | **2.46 CPU-s** |
| 41 | 91.84 | **2.20 CPU-s** |

≈2.2–2.5 CPU-s per reftest, i.e. **about 1.1 CPU-s per render** — and roughly flat
across two unrelated test sets.

**The pages are the reason that is not page work.** The WPT documents in
`css/css-backgrounds` (n = 1 168 `.html`):

| | bytes |
|---|---:|
| median | **1 018** |
| p90 | 1 895 |
| max | 8 518 |

against the stage-profile corpus at 20 102 – 211 592 bytes. **Every corpus page is
larger than every WPT page in that directory**, and the median differs by ~76×. A
77 KB corpus page renders end to end in ~250 ms; a 1 KB WPT page costs ~1.1 CPU-s.
Per-render cost here is dominated by something that does not scale with the document.

That is the whole mechanism. The Phase 2–3 wins that survive at one thread all scale
with page content, and a 1 KB single-render viewport-sized document supplies almost
none of it:

- **#5's off-screen elision** needs a page taller than the viewport. A reftest is
  written to fit one.
- **#10's glyph-outline cache** pays back on repeated glyphs. There are few.
- **#12's fourth memo cache** pays back across many elements. There are few.
- **#14's invalidation** needs a *second* layout. A WPT test renders once.
- **#3's per-pixel target lookup** is in `Broiler.Graphics`' rasterizer, and Phase 2 §2
  already records that the path this repository measures uses **item #4's copy** — so it
  is not on this path at all.

### One hypothesis tested and refuted

The obvious candidate for the small negative sign was item #14 charging its
bookkeeping — `CascadeInvalidationSet` scans a document's sheets, and a suite that
never relayouts can only pay for it. Measured with the switch the item ships
(`BROILER_RENDER_TREE_ELISION`): **off 399.4 s, on 393.7 s**. Turning it off is not
faster, so that is not where the sign comes from. Recorded because the plausible
explanation for a small number is exactly the kind that gets published untested.

## Scope, stated rather than implied

- This is the **reftest** suite, not the golden-image suite the 90/45 figure comes
  from. A golden-image run compares one Broiler render against a Chromium PNG; a
  reftest renders both sides with Broiler. The per-test shape differs, so the wall
  clocks are not comparable with 90/45 — the *engine* work per render is the same,
  which is what this file is about.
- **2 213 of the corpus's 19 398 reftests (11.4%)** were measured, not all of them.
  A full-corpus A/B is ~6 h of wall clock on this host, and two independent suites of
  713 and 1 500 tests already agree to within the host's own drift. Generating
  Chromium references for a full *golden-image* A/B is the step CI caches per shard
  and was not attempted.
- The conclusion that transfers is the negative one: **there is no sequential win here
  to find at any scale**, because the mechanism — page size — is a property of WPT
  documents generally, not of the two directories sampled.

## What this says about making WPT faster

Not one item in the multithreading roadmap. The run is CPU-bound at ~1.1 CPU-s per
render of a 1 KB document, and that cost is flat in document size.

> **Correction — the next sentence used to say that cost was "per-render fixed cost
> inside the engine", and that was an inference, not a measurement. It is wrong.**
> [`render-fixed-cost.md`](render-fixed-cost.md) measured the engine directly through
> the same entry the runner calls: an empty document renders in **3.5 ms** and a
> WPT-median one in **15–19 ms**, and a phase trace of the runner puts the render at
> **1.6–2.0% of a test**. The ~1.1 CPU-s per "render" counted here is the runner's
> per-render *path*, not the engine's render: **76–79% of it is
> `ExecuteScriptsWithDom`** (DOM build, JS execution, re-serialization, run twice per
> reftest) and **16–21% is `PixelDiffRunner.Compare`**. Those two are the target for
> WPT wall clock, and neither is a rendering problem.
>
> The reasoning that produced the wrong answer is worth keeping visible: this file
> ruled out I/O and process startup, found the remaining time was CPU and did not scale
> with the document, and concluded it was fixed cost *in the engine* — when "does not
> scale with the document" was equally consistent with fixed cost in the **harness**,
> which is where it turned out to be. Eliminating two candidates does not confirm a
> third.

Everything above the correction stands: the sequential wins are worth nothing here, and
the mechanism is that they are aimed at work a WPT document barely has. The measurement
that followed only sharpens it — they were aimed at the page-proportional part of 2% of
the run.

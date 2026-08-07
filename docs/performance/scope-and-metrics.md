# Scope and metrics

§1 — what merging the two source roadmaps produced that neither had. §2 — the five numbers this campaign tracks, in two families, and which question each answers.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

---

## 1. What the merge produces that neither document had

Two findings come out of putting these side by side. Both change the plan, so they
lead.

### 1.1 The engine roadmap's scope statement is wrong, and Octane is why

[`performance-roadmap.md` §9](../../Broiler.JS/docs/performance-roadmap.md) declares two
areas out of scope:

> - **Parsing and compilation.** Fresh-context startup is 1.20 ms and
>   `script:evaluation` runs in 37 ms; neither showed up as a bottleneck.
> - **A real JIT / tiered compilation.** […] Everything above is achievable in the
>   current architecture.

Both exclusions are **superseded here**, and the reason is not a change of opinion —
it is that the probe corpus could not see the effect:

- **Front end.** The probes are one-liners in a fresh `JSContext`. Octane runs 15
  large real programs, and when this was written the two worst scores in the entire suite
  were **MandreelLatency at 4646×** and **CodeLoad at 371×**. `script:evaluation` at 37 ms was
  a true measurement of a corpus small enough that eager compilation is free. It is not
  free on jQuery, on the TypeScript compiler, or on a 152,948-line generated function.
  **The front end is phase 1.**
  > **Correction, from running both suites (phase 1).** This bullet used to call those two
  > scores "the two that measure nothing but the front end", and used them as the phase's
  > success metric. Measured, **CodeLoad is ~27% compilation and MandreelLatency is 0%** —
  > Octane compiles `mandreel.js` at script load and starts its timer afterwards. The
  > argument above is unaffected, because it rests on the *probe corpus being too small to
  > see eager compilation*, which is still true and is why phase 1 exists. What it cost was
  > the phase's target list: see Phase 1's header.
  >
  > **The two scores it named are no longer the two worst** (§4.2, 2026-08-03): CodeLoad is
  > 228× and three suites are now behind it — DeltaBlue 460×, Mandreel 290×, RayTrace 256×.
  > MandreelLatency at 4 584× is still the tail by an order of magnitude. Neither correction
  > touches the argument, which was never about *which* scores were worst.
- **Speculation.** Engine §9 scoped itself to "achievable in the current
  architecture", which was an honest boundary for a bookkeeping-removal campaign. The
  remaining ~100× is not achievable inside it. **Speculation is phase 4**, and the
  scaffolding for it is already built and tested.

This is the general hazard the merge exists to close: **an in-process probe answers
"what does this operation cost", and a benchmark suite answers "what does this
program spend its time on".** Neither substitutes for the other, and the excluding
section was written with only the first.

*(Section numbers prefixed **engine** or **Octane** refer to a source document; bare
`§n.n` refers to this one. Phases are always named, never numbered as sections.)*

### 1.2 Both roadmaps are blocked on the same missing thing

They describe it differently and it is one gate:

- The Octane roadmap's phase 0 owes **a workflow run** — the committed results
  predate the engine fixes by ~15 hours, so the geomean, the coverage count and the
  spread all quote a superseded engine.
- Engine §8.1 owes **acceptance evidence** — no BenchmarkDotNet comparison, no
  RID-matrix run, and the probes that produced every number in it have no permanent
  home.

Neither list can be *judged* until both are satisfied, and they satisfy the same
requirement from two directions. They are merged into **Phase 0**, which is
consequently the only phase with no engineering in it and the only one that blocks
everything else.

---

## 2. Metrics

Track five numbers, in two families. Reporting one without the other is how the two
source documents ended up disagreeing.

### 2.1 The suite view — three numbers per Octane run

| Metric | Superseded run (2026-07-31) | **Committed run at the pin (2026-08-07)** | Target |
|---|---|---|---|
| **Scores reported** out of 17 | 12 / 17 | **17 / 17** — 15 of 15 suites `ok` | **17 / 17** |
| **Geomean** over all 17 scores | 245 over the 12 that completed | **372** over all 17 | — |
| **Spread** = worst ÷ best, as ×-slower-than-Chromium | 4 646 / 45 ≈ **103×** | 4 375 / 31.7 ≈ **138×** | **< 5×** |
| **Against Jint**, geomean of per-benchmark ratios | not measured | **0.644×** | > 1 |
| **Noise band**, scores outside the declared 7.5% | not measured | **1 / 17** — median spread 3.0% | — |

The right column is the workflow's own run, so its Chromium and Jint columns were measured on
the same machine at the same time and the ratios are directly comparable. It is **three
repetitions per suite**, so unlike every earlier committed run it reports the fourth number as
well as the first three — which is 0-6's band, and it is the row that decides whether a future
change to any suite is claimable at all. **It still cannot be differenced against the runs
above it**, which were single-repetition; the first differenceable pair is this run and the
next banded one.

**The spread went up, and that is not a regression.** It is 138× against the superseded run's
103× because the superseded run had *five suites scoring nothing at all*: a suite that fails
contributes no ratio, and the four of those five that now score (Crypto, PdfJS, zlib,
Typescript) landed across the middle of the range while the best axis improved from 45× to
31.7×. Spread is a ratio of two suites, so widening the denominator widens it. Compare the
column honestly or not at all — which is the reason this table names both dates rather than
saying "before" and "after".

**Spread is the organizing metric.** Because the suite total is a geometric mean,
flattening the curve and raising the total are the same work: moving MandreelLatency
from 14.5 to 1000 is worth more than tripling every score already above 300. A run
where every suite is uniformly 150× off is a far healthier engine than today's at a
similar geomean, because no single subsystem is pathological.

All three are emitted by `run-octane.mjs` into `results/<platform>/comparison.md` and
`comparison.json`, so the trend comes out of the run rather than being reconstructed
by hand.

### 2.2 The engine view — time *and* allocation per hot path

Wall clock alone hid the largest result of the completed campaign. The shadow-stack
change (§4.1) took an argument-less call from 80 B to **0 B** with throughput
*unchanged*; the pooled predecessor was banked as an allocation win at no speedup.
Conversely P2-2's item 3 looked like an allocation change and turned out to be worth
9–99% of wall clock once its eligibility gate was widened.

**Report time, allocation, and working set together.** This is already the rule in
`performance.md`; it is restated because the campaign twice found the interesting
half in the column it was not looking at.

### 2.3 Which number answers which question

| Question | Instrument |
|---|---|
| Did this operation get cheaper? | In-process probes, Appendix A |
| What does an object or an element cost in bytes? | `--object-alloc`, `--element-alloc` |
| What does a local, a binding or a parameter cost? | `--local-alloc`, which reports the compiler's own eligibility counts beside the bytes |
| What does a regex cost, and which engine ran it? | `--regex-profile` |
| How much of a compile is function bodies — i.e. what can 1-1 win? | `--compile-profile <octane-dir>` |
| Which of parse / tree construction / IL emission is the cost, and is it linear? | `--compile-scaling` |
| Did the cache actually start hitting? | `PropertyOptimizationDiagnostics.Snapshot()` |
| Did real programs get faster? | Octane, ≥3 repetitions, median + spread |
| Is the engine still correct? | test262 over the pinned manifests |
| May we publish the number? | `performance.md` gates only — none of the above |

---

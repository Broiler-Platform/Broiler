# Item #18's gate: contexts are isolated *and* they scale

The multithreading roadmap does not schedule Web Workers (item #18) until one thing is settled:
*"Gate it on the P0-c static-state audit; anything genuinely global (interned strings,
shapes/hidden classes, the code cache) needs an explicit thread-safety contract before a second
context runs concurrently."* The item's master-table row asserts the other half — *"per-context
state exists … so isolation is feasible"*.

Both are claims about code. This phase has repeatedly found that the claim in a row is not the
operative fact, so the gate is built here as something that runs rather than something read.

## Two questions, because passing the first alone would be misleading

**1. Correctness — do concurrent contexts stay isolated?** `JSContextIsolationTests`, five cases,
four real threads each owning a `JSContext` for its lifetime (the shape item #18 proposes), started
together on a barrier:

| case | aimed at |
|---|---|
| globals do not leak between contexts | the property the whole feature rests on |
| shared property names and shapes stay correct | interned key strings, shape/hidden-class transitions |
| identical sources compile and run correctly | the compiler and per-context code cache, at maximum contention |
| process-shared code cache is safe | `DictionaryCodeCache.Current` — the newest shared-state user, added by Phase 4 §8/§9 |
| built-ins behave identically on every thread | the registry's static initialization, hit by several threads at once |

**All five pass.** Each run also asserts that the threads *actually overlapped* — peak concurrency
above one — because a concurrency test whose threads happen to run one after another passes for the
wrong reason and proves nothing.

**2. Throughput — do they run at the same time, or merely correctly?** This is the half a
correctness test cannot answer: an engine holding one global lock would pass every assertion above
and still make a worker useless, since the point of a worker is to run *while* the main context
runs. **Correct-but-serialized is the outcome that would sink item #18.**

`--js-context-scaling`, one context per thread, same CPU-bound allocation-light workload on each,
compiled before the clock starts, configurations interleaved:

| code cache | threads | wall ms | speedup vs 1 thread |
|---|---:|---:|---:|
| per-context | 1 | 210.46 | 1.00× |
| per-context | 2 | 231.42 | **1.82×** |
| per-context | 4 | 316.31 | **2.66×** |
| process-shared | 1 | 182.27 | 1.00× |
| process-shared | 2 | 194.05 | **1.88×** |
| process-shared | 4 | 226.29 | **3.22×** |

Each thread does the *same* work, so perfect scaling would hold wall time flat as threads rise.

## What it says

**The gate passes on both halves.** Contexts are isolated under real concurrency, and they run
genuinely in parallel — **2.66× per-context and 3.22× shared, on four cores**. Item #18's premise
holds: this is not an engine that would serialize workers.

**And the change this phase made is not a worker blocker.** Phase 4 §8/§9 routed the bridge's and
the runner's constant sources through the process-shared `DictionaryCodeCache.Current`, which is
safe today only because those hosts render on one thread per process — under item #18 several
threads would reach it at once. It is not merely safe under contention: it **scales better** than
the per-context cache (3.22× against 2.66×), which is what you would expect from work that stops
being repeated per context. That was worth measuring rather than assuming, since a blocker
introduced by this phase's own optimisation would have been an unpleasant thing to discover later.

## What this does *not* establish

- **It is a gate, not the feature.** `Worker`, `MessageChannel` and structured-clone message
  passing are unwritten; the item's 30–40 day, High-risk estimate stands.
- The workload is deliberately narrow — property access, arithmetic, calls. It exercises the
  shared state the roadmap names; it does not exercise the DOM bridge, timers, promises or the
  module machinery, none of which a worker gets in the spec anyway but all of which a real
  implementation would have to bound.
- 2.66–3.22× on four cores is not linear, and this measurement does not attribute the shortfall.
  It is enough to answer "do they run concurrently"; it is not a scaling study.

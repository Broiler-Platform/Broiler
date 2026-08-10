# Inside `ExecuteScriptsWithDom`: half a WPT run is publishing the DOM API

`render-fixed-cost.md` left one target: **`ExecuteScriptsWithDom` at 76–79% of a WPT run**,
~0.7–0.9 s per render, run twice per reftest. This profiles it.

Produced by `--phase-trace --no-worker-isolation --workers 1` (one process, one thread, so
wall ≈ CPU). Sub-phases nest under their parent and are excluded from the attributed total.

> Taken with `patches/0134` **unapplied** — the submodule sits at its pinned pointer — so the
> `pixel compare` row still shows its pre-fix ~313 ms. Everything else is unaffected by it.

## `css/css-backgrounds/animations` — 41 tests, 70.7 s

| phase | total s | share of wall | calls | ms/call |
|---|---:|---:|---:|---:|
| file read | 0.01 | 0.0% | 82 | 0.07 |
| font registration | 0.04 | 0.1% | 41 | 0.93 |
| **scripts + DOM bridge** | **56.05** | **79.3%** | 82 | 683.48 |
| ├ script scan + sheet inlining | 0.02 | 0.0% | 82 | 0.30 |
| ├ JSContext construction | 0.59 | 0.8% | 82 | 7.14 |
| ├ **DomBridge.Attach** | **36.77** | **52.0%** | 82 | 448.40 |
| · ParseHtml (DOM build) | 0.05 | **0.1%** | 82 | **0.61** |
| · **RegisterDocument (DOM API surface)** | **35.74** | **50.6%** | 82 | **435.86** |
| ├ script eval + drains | 17.70 | 25.0% | 82 | 215.80 |
| ├ load event + snapshots + anchors | 0.81 | 1.1% | 82 | 9.88 |
| └ SerializeToHtml | 0.05 | 0.1% | 82 | 0.57 |
| post-process | 0.03 | 0.0% | 82 | 0.32 |
| render | 0.78 | 1.1% | 82 | 9.53 |
| pixel compare | 12.85 | 18.2% | 41 | 313.36 |
| failure diagnostics | 0.08 | 0.1% | 24 | 3.54 |
| (unattributed) | 0.85 | 1.2% | | |
| **attributed** | | **98.8%** | | |

## `css/css-fonts` — 373 tests, 620.9 s

A different directory, nine times the tests, to test whether the split is a property of the run
or of that one subset.

| phase | share of wall | ms/call |
|---|---:|---:|
| **scripts + DOM bridge** | **78.2%** | 650.80 |
| ├ script scan + sheet inlining | 0.0% | 0.12 |
| ├ JSContext construction | 0.6% | 4.95 |
| ├ **DomBridge.Attach** | **54.9%** | 457.10 |
| · ParseHtml (DOM build) | **0.0%** | **0.40** |
| · **RegisterDocument (DOM API surface)** | **53.6%** | **445.80** |
| ├ script eval + drains | 20.5% | 170.53 |
| ├ load event + snapshots + anchors | 1.9% | 16.11 |
| └ SerializeToHtml | 0.2% | 1.81 |
| render | 1.7% | 14.41 |
| pixel compare | 17.2% | 330.64 |
| attributed | **97.2%** | |

**`RegisterDocument` costs 435.86 ms/call on one subset and 445.80 on the other** — 2% apart
across two unrelated directories and a 9× difference in test count. That is the evidence that it
is fixed cost rather than an average over documents.

## What it says

**Half of a WPT run — 50.6% — is `DomBridge.RegisterDocument`**: publishing the document, the
window and the DOM API surface onto a fresh `JSContext`, 436 ms per document and twice per
reftest. It is the largest single item measured anywhere in this investigation, larger than the
render, the pixel comparison and the script execution combined.

**The name of the phase above it is misleading, which is why this had to be measured.**
"`DomBridge.Attach`" sounds like a DOM build, and `render-fixed-cost.md` described it that way.
The DOM build — `ParseHtml`, tokenize plus tree construction plus the bridge's node tables — is
**0.61 ms, 0.1%**. Attach is 99.9% API registration and 0.1% DOM.

**It is fixed cost, not document cost** — measured, not inferred: 435.86 ms/call on one subset
and 445.80 on another, 2% apart across unrelated directories and 9× the test count, while the
parse that *does* scale with the document stays under a millisecond. A WPT reftest document is
1 018 bytes at the median, so essentially all of what a WPT test costs is the engine building
the same API surface again from nothing, twice, for a page with almost nothing in it.

**Script execution is the honest second at 25.0%** (215.80 ms/call) — and note that every WPT
render evaluates the injected `BrowserApiStubs` and, where the test references them, the
testharness stubs, so that figure is not only the page's own scripts.

## Two incidental observations from reading the path

- The `scripts.Count == 0 && deferredScripts.Count == 0` early-out in
  `WptTestRunner.ExecuteScriptsWithDom` is **unreachable**: two `scripts.Insert(0, …)` calls
  (the promise-test flag and `BrowserApiStubs`) run before it, so the list is never empty. Every
  test takes the full path. That is probably intended — the bridge pass also resolves anchor
  positions, animation snapshots and check-layout assertions — but the branch below it cannot
  run and reads as if it can.
- `SerializeToHtml`, the DOM-back-to-markup step, is **0.57 ms**. The round trip through markup
  that this architecture is built on is not what costs.

## Where this leaves the WPT wall clock

| | share of a run |
|---|---:|
| `RegisterDocument` | **50.6%** |
| script eval + drains | 25.0% |
| pixel compare (fixed by `patches/0134`: 313 → ~5 ms) | 18.2% |
| everything else, **including the render** | ~5% |

The render is 1.1%. Nothing in the multithreading roadmap addresses any of the top three, which
is the same conclusion `wpt-sequential-wins.md` reached from the other end — now with the
largest term named.

**Not attempted here, and deliberately:** making `RegisterDocument` cheaper is a design change
to the bridge (lazy or cached registration of host objects, or reusing a context across
documents where isolation permits), not a profiling result. The measurement says where to aim;
it does not say the aim is easy, and per-document isolation is exactly the property a WPT runner
must not lose.

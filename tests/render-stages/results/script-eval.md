# Script eval: the stubs were the cost, not the page

`register-document.md` left script eval as the largest remaining phase of a WPT run
(~215 ms/call, 20–25%). Profiled into its parts, most of it turns out not to be the document's
scripts at all.

## The split, and the fix

`css/css-backgrounds/animations`, 41 tests, single-process:

| step | calls | before (ms/call) | after (ms/call) |
|---|---:|---:|---:|
| **injected stubs** | 164 | **64.49** | **1.24** |
| page scripts | 92 | 41.99 | 43.60 |
| **window→global sync** | 253 | **11.98** | **0.12** |
| drains | 335 | 1.30 | 1.81 |
| **script eval total** | 82 | **240.15** | **82.48** |
| run wall | | **37.1 s** | **24.2 s** |

**Injected stubs, 52×.** The leading entries of the script list are the *runner's* own constant
sources, not the page's: `BrowserApiStubs` (~10 KB), `TestharnessStubs` (~4.8 KB) when the test
pulls in testharness.js, and a one-line flag. They are `private const string` fields, and every
document recompiled all of them — **28.5% of a whole WPT run**.

**The window→global sync, 100×.** `SyncWindowMembersOntoGlobal` evaluates a constant mirror
source, and a host calls it after *every* script — 253 calls across 41 reftests. Registration's
own use of it was already cached; this path was not, so the same source was recompiled once per
script per document.

Both are the same fault `register-document.md` fixed one layer up, in two more places: a
compile-time constant owned by the host, recompiled for every document because every document
gets a fresh `JSContext` with a fresh code cache. Both are fixed the same way — the process-shared
`DictionaryCodeCache.Current` installed for the duration of those evaluations only.

## What is deliberately *not* fixed

**Page scripts — 43.60 ms/call, now the largest item in the phase — are untouched, and should
be.** They are page content. Sharing their compiled form across documents is exactly the
cross-document path a conformance runner must not create, and it would buy little anyway: WPT
documents rarely repeat a script verbatim, so the cache would mostly miss while holding
page-derived source. The measurement confirms the boundary held: page scripts read 41.99 before
and 43.60 after, i.e. unchanged.

The scope of each swap is a single evaluation of a named constant, and the cache it feeds
therefore holds a fixed, bounded set of strings that ship in the assemblies.

## Cumulative, on `css/css-backgrounds` reftests (713 tests, `--workers 4`)

| | median (s) | vs pristine |
|---|---:|---:|
| pristine | **368.5** | 1.00× |
| + `RegisterDocument` shared cache | **195.7** | 1.88× |
| + stubs and window sync | **108.6** | **3.39×** |

Measured with `patches/0134` (the pixel-comparison fix, worth a further ~70 s on this subset at
the pre-fix scale) **unapplied**, since the submodule sits at its pinned pointer.

## Correctness

- **Failing-test set identical to the pristine tree, name for name** (266), stable across two
  runs; classification 444 passed / 266 failed / 1 skipped unchanged.
- `css/css-fonts` + `css/css-writing-modes` unchanged at 685 passed / 815 failed.
- `Broiler.Wpt.Tests` 748/57 → **750/55, nothing newly failing**. The two that flip are both
  `RunTestWithTimeout_*_Completes_Without_Timing_Out` — tests that assert a run completes inside a
  timeout, which is the fix doing its job. The suite itself runs 10 m → 3.5 m.

## What is left

Script eval is still 27.9% of a run, and it is now genuinely the page's own JavaScript (16.6%)
plus drains. That is real work a conformance run has to do. The render, for the record, was
1.1–1.7% before any of this started.

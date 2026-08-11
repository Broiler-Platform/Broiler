# `RegisterDocument`: 422 ms → 13.7 ms, and a WPT run nearly halves

`script-dom-phase.md` found `DomBridge.RegisterDocument` at **50.6–53.6% of a whole WPT run** —
~440 ms per document, twice per reftest, and fixed rather than document-proportional. This is
the fix.

## Where the 422 ms went

Profiled one level further, `css/css-backgrounds/animations`, 41 tests:

| step | before (ms/call) | after (ms/call) |
|---|---:|---:|
| document object | 106.66 | **6.67** |
| window basics + fetch | 61.25 | **1.66** |
| window globals | 3.20 | 0.09 |
| performance/navigator/viewport | 0.21 | — |
| content-rendering polyfills | 61.73 | **1.65** |
| security/constructor polyfills | 148.49 | **2.12** |
| window→global mirror | 40.54 | **1.36** |
| **RegisterDocument total** | **422.10** | **13.74** |

**30.7×.** Every step fell by roughly the same factor, which is the shape of a single shared
cause rather than one hot spot.

## The cause: compiling the bridge's own JavaScript once per document

Registration evaluates a fixed set of **bridge-owned** JavaScript sources — the content-rendering
polyfill asset, the `DOMException` / `Node` / `SVGLength` constructors, `XMLHttpRequest`, the
mutation-observer and event shims, and the window→global mirror. Every document gets a fresh
`JSContext`, and a fresh context builds its own `DictionaryCodeCache`, so all of it was parsed
and compiled again from nothing, for every document, twice per reftest.

The fix installs the engine's process-shared cache (`DictionaryCodeCache.Current`) for the
duration of `RegisterDocument` and restores the context's own cache afterwards.

**That the cost was compilation rather than execution is what the measurement shows**: the swap
changes nothing about what the sources do when they run, only whether they are compiled again,
and it removes 97% of the phase.

## Why the swap is scoped to the call, not set on the context

The engine already offers `JSContextOptions.UseProcessSharedCodeCache`, which applies the shared
cache to **everything** a context evaluates — including page script. That is a much larger claim:
it would put one document's compiled code where the next document's evaluation can find it, and
a WPT runner is precisely where cross-document leakage must not happen.

Nothing here needs it. Within `RegisterDocument` the only sources evaluated are compile-time
constants owned by the bridge assembly — **verified, not assumed**: no `Eval` reachable from that
method takes an interpolated or page-derived string, and page script does not run until the
host's own loop, after `Attach` has returned. Inline event handlers, which *are* page-controlled,
are compiled at dispatch time and still go through the context's own cache.

So the shared cache holds a fixed, bounded set of strings that ship in the assembly. It does not
grow with the number of documents rendered, and no page-controlled source can reach it this way.

Correctness rests on a key the engine already defines: `DictionaryCodeCache` keys on source,
location, argument list and `JSCompilationOptions`, so an entry is only served to a context that
would have compiled exactly the same thing. That is the same property item #16's compile-ahead
relies on to hand a worker's output to the eval loop.

## On the whole suite

`css/css-backgrounds` reftests (713 tests), `--workers 4`, same host:

| | runs (s) | median |
|---|---|---:|
| before | 373.2, 363.8 | **368.5** |
| after | 198.0, 193.3 | **195.7** |

**1.88× — 173 s off a 6-minute subset.** Measured with `patches/0134` (the pixel-comparison fix,
~70 s on this subset) **unapplied**, since the submodule sits at its pinned pointer; the two are
independent and compose.

## Correctness

- **Failing-test set identical to the pristine tree, name for name** (266 failures), and stable
  across both runs — compared against the pristine control established in `pixel-compare.md`
  rather than against a differently-invoked earlier run.
- Classification 444 passed / 266 failed / 1 skipped, unchanged.

# Why a Google search stalls after the consent page

An investigation into the reported symptom: after accepting Google's consent
form, a search never reaches its results. The page sits on *"Please click here
if you are not redirected within a few seconds"*, and the JavaScript log carries

```
TypeError: Cannot get property length of undefined
    at Item   D:\Broiler\Broiler.JS\…\Broiler.JavaScript.Runtime\JSUndefined.cs:41
    at inline  vm97.js:5566          (×12)
    at ia      inline-3:9
    at la      inline-3:13
    at inline  inline-3:13
```

**The throw is not an engine defect.** `vm97.js` is Google's bot-check VM, and
the VM contains a watchdog that times itself. Two slices over **16 384 ms** and
it overwrites its own interpreter-context pointer with the boolean `true`; the
next opcode that dereferences that pointer reads `true.yl`, gets `undefined`,
and asks it for `.length`. `JSUndefined.cs:41` is where a correct engine reports
that, and every browser would report the same thing given the same state.

**What is ours is the speed that gets it there.** On identical bytes Broiler
runs this VM **250× slower than Chromium**, and the slowest slice is **108×
longer** — 584 ms against 5.4 ms, into a 16 384 ms budget. That margin is what a
debug build, a slower machine, or a heavier challenge spends.

| | Chromium 141 | Broiler (Release) | ratio |
| --- | --- | --- | --- |
| VM wall time | 24 ms | 5 989 ms | 250× |
| Slowest watchdog slice | 5.4 ms | 584 ms | 108× |
| Compile of the 62 887-byte VM source | 3.9 ms | 521 ms | 134× |
| Run of that source (definitions only) | 1.6 ms | 700 ms | 437× |

Same page, same bytes, replayed from disk. Method under
[Measuring a build's headroom](#measuring-a-builds-headroom).

## The page

`https://www.google.com/search?q=…` does not always answer with results. When
Google wants to check the client first it answers with an interstitial titled
`Google Search` whose whole visible content is

> Please click here if you are not redirected within a few seconds. If you're
> having trouble accessing Google Search, please click here, or send feedback.

Its scripts solve a challenge, set an `SG_SS` cookie, and `location.replace` to
the results URL. Post-consent is simply when a browser meets it most reliably —
it is not conditional on consent, and a bare `Broiler.Cli` capture of
`search?q=broiler` reproduces it directly:

```sh
dotnet run --project src/Broiler.Cli -- \
  --capture-image "https://www.google.com/search?q=broiler" \
  --output out.png --diagnostic-dir artifacts/google-diag
```

Two markers identify a bundle as this page: `SG_SS` in the document, and an
inline script defining `la`/`ia` around `challenge_version` and `cbs`. Its five
inline scripts land in `resources/` as `inline-0` … `inline-4`; `inline-2` is
the VM loader and `inline-3` the challenge driver named in the trace.

`inline-3` ends with the two frames the report shows:

```js
Z.then(function (a) { a || la(); }).catch(function (a) { X(a); });
```

`la()` calls `ia()`, which hands the challenge program to the VM:

```js
function ia(a, b) {
  var c = C[g];                       // C[g] === window.knitsail
  if (c) { b = c.a; var h = [ja()]; b(p, function (e) { return void e(a, h); }, !1, …, !0); }
  else b(Error("f"));
}
```

## `vm97.js`, and how to get a copy of it

`vm97.js` is not a file Google serves. `inline-2` builds the VM as an array of
strings, joins it, prefixes `Array(Math.random()*7824|0).join("\n")` and passes
the result to an **indirect eval** — which is why the program is one 61 798-char
line preceded by several thousand blank ones, and why its number varies per run.
Broiler names such programs `vm<N>.js`, and
[`AnonymousProgramDump`](../Broiler.JS/Broiler.JS/Broiler.JavaScript.Compiler/AnonymousProgramDump.cs)
writes them out under exactly that name:

```sh
BROILER_JS_DUMP_PROGRAMS=artifacts/programs \
  dotnet run --project src/Broiler.Cli -- --capture-image "$URL" --output out.png
```

The identifiers inside are re-randomised per serve, so names below are from the
reported `vm97.js` and will differ in a fresh capture. The *shapes* do not.

Note that Broiler has no Trusted Types. Google's loader probes for it, and
without it falls back to passing a plain string to `eval` — which is what makes
the instrumentation further down possible at all; under Chromium the argument
arrives as a `TrustedScript` and has to be stringified first.

## Decoding the trace

Resolving each column against the program's AST gives the whole stack,
outermost first:

| Frame | Function | Role |
| --- | --- | --- |
| `inline-3:13,1126` | `function (a) { a \|\| la() }` | the `Z.then` callback |
| `inline-3:13,24` | `la` | start the challenge |
| `inline-3:9,1027` | `ia` | hand it to `window.knitsail.a` |
| `vm97:5566,22340` | `bQ` | `knitsail.a` |
| `vm97:5566,1430` | `S` | |
| `vm97:5566,61690` | `knitsail.crb_` | builds the VM instance |
| `vm97:5566,22179` | `Ei` | the VM constructor |
| `vm97:5566,25484` | `xf` | its body — at the `Bz(11, 0, true, W, S([…]))` call |
| `vm97:5566,4190` | `Bz` | run entry: stamps the clock, then `Fe(…)` |
| `vm97:5566,15923` | `Fe` | |
| `vm97:5566,32539` | `IH` | |
| `vm97:5566,57786` | dispatcher | |
| `vm97:5566,39658` | `pr` | the interpreter loop |
| `vm97:5566,29453` | `function (y, C) { iQ("", 104, …, O.J, 361) }` | opcode 9 — push a frame |
| `vm97:5566,37173` | `iQ` | **throws** |

So it dies inside the constructor, running the VM's own bootstrap program.
`iQ` is one statement long:

```js
iQ = function (V, x, F, D, M, z) {
  M.yl.length > x
    ? Xa([lQ, 36], M, V, F)                                  // "call stack too deep"
    : (M.yl.push(M.g.slice()), (M.g[z] = void 0), v(M, z, D));
};
```

`M` is `O.J`. `O` is `xf`'s own alias for the VM object `W`, and `W.yl = []` and
`W.J = W` both run before `Bz(…)` is reached — the `Bz` frame proves the
constructor got that far. `M.yl` is `undefined`, so **`O.J` is no longer the VM
object**, and `iQ` reads `length` off the `undefined` that `true.yl` yields.

## The watchdog

`.J` is assigned in seven places, and six of them can only ever store the VM
object: the constructor's `W.J = W`, two save/restore pairs
(`z = x.J; x.J = x; try { … } finally { x.J = z }`), and a setter reached
through `W.z6` that the program never calls — `z6` appears exactly once more, in
a comparison (`c == y.z6`). The seventh is in `kr`, on the path `pr` takes for
**every** dispatch — `kr(39, (…, 1), false, D, false, x)`:

```js
D.J =
  ( ((D.B += ((d = (…, (P = (q = D.ii == 4) || W ? D.u() : D.Vl), P - D.Vl)),
              d >> 14 > 0)), D).Z && (D.Z ^= …),
    D.B + x ) >> 2 != 0 || D.J;
```

Read plainly, with `x === 1` and `D.B` starting at `1`:

- `D.u()` is `this.NM + performance.now()` — wall-clock milliseconds.
- `D.ii` counts dispatches; every fourth one takes a fresh reading and resets
  `D.Vl`. So `d` is the elapsed time of a **four-dispatch slice**.
- `d >> 14 > 0` is `d > 16383 ms`. Each such slice increments `D.B`.
- `D.J = ((D.B + 1) >> 2 != 0) || D.J`. That is `false || D.J` — a no-op — while
  `D.B <= 2`, and `true` from `D.B == 3` onward.

`B` starts at 1, so it takes **two** stalled slices to poison `.J`, and from
then on every write re-poisons it. The VM carries on until an opcode
dereferences `.J`; `iQ` (opcode 9, "push a call frame") is the one that did.

Verified against a captured challenge by rewriting the VM's own comparison
through an `eval` hook so that every slice reports a stall: `B` climbed to 8 and
the poisoned `true` was written 6 times. That capture still finished, because
its (small) challenge program never executes opcode 9 — which is also why this
cannot be reproduced on demand from a plain `Broiler.Cli` run. The reported
challenge was larger and did.

### What is not the cause

- **The clock.** `performance.now()` and `performance.timeOrigin` are present,
  positive, non-decreasing and sub-hour, matching Node's on the same probe;
  `u()`'s constant `NM` base cancels in the subtraction, so a wrong origin could
  not produce a wrong delta. One caveat for future readers:
  `WindowDocumentMiscBinding.PerformanceNow` derives from
  `DateTimeOffset.UtcNow`, so it is whole-millisecond and follows the wall clock
  rather than a monotonic source. A clock step (an NTP correction, a VM resume)
  of more than 16.4 s, twice, would trip this same watchdog with the engine
  entirely idle. Nothing suggests that happened here, but it is the one way to
  reach the failure without being slow.
- **`document.hidden`, in this VM.** The full yield predicate is
  `D.rW > 0 && … && document.hidden == 0`, and `D.rW` is `0` in the reported
  program — so the predicate is false whatever `document.hidden` says. It was
  missing anyway, and is [fixed below](#what-changed-here).
- **Language semantics.** A 124-expression differential probe — `ToInt32` and
  the shifts on fractional/negative/out-of-range doubles, the abstract-equality
  corners this obfuscator switches on (`![] == Number()`,
  `[] == (null != ((true == ![]) != ![]))`), left-to-right evaluation of nested
  assignment/comma chains, `try`/`finally`, labelled `break`, throwing arrays,
  accessor properties via `Object.create`/`defineProperty` — matches Node
  exactly, with `document.hidden` the only divergence.

## Measurements

All figures below are one container, one saved copy of the interstitial, replayed
from `http://127.0.0.1` so both engines see identical bytes.

Per-opcode profile of the outer dispatch loop (23 dispatches, 16 distinct
opcodes):

| | Chromium | Broiler |
| --- | --- | --- |
| Total dispatch time | 33 ms | 6 302 ms |
| Slowest single opcode | ~1 ms | 175 ms |

Most of Broiler's time is *not* in opcode bodies: under a second of the 6 302 ms
is, and the rest is compiling the program and running the constructor's own
expression. `new Function(src)` on the 62 887-byte VM measures 521 ms against
Chromium's 3.9 ms, and a second compile of the same text is 2 ms, so the
compiler's cache works and the cost is a genuine first-compile cost.

A live run measured with the script below: 69 slices, slowest 712 ms, **23×
headroom**.

This is the engine-throughput campaign's territory, not a bug with a discrete
fix; it is tracked in
[`Broiler.JS/docs/roadmap/Roadmap.md`](../Broiler.JS/docs/roadmap/Roadmap.md).

## Measuring a build's headroom

```sh
python scripts/measure-google-challenge-headroom.py
python scripts/measure-google-challenge-headroom.py --configuration Debug
python scripts/measure-google-challenge-headroom.py --page saved-challenge.html
```

It fetches the interstitial (or reads one), installs an `eval` hook that rewrites
the VM's timing check so each slice is reported without changing what the VM
computes, replays the page under `Broiler.Cli --capture-image`, and prints the
slowest slice against the 16 384 ms budget:

```
configuration : Release
challenge     : solved
VM wall time  : 10712 ms
slices        : 69 (clock samples: 69)
slowest slice : 712.0 ms
watchdog      : 16384 ms per slice, two stalls poison the VM
headroom      : 23.0x
```

Google does not serve the interstitial to every request; the script says so and
exits 2 rather than reporting a number it did not measure. It also exits
non-zero if the obfuscation has been re-spun far enough that its two rewrite
patterns no longer match — those patterns are the only part of it that Google
can invalidate.

## What changed here

`document.hidden` and `document.visibilityState` are now registered on
`document` (`false` and `"visible"`: a capture renders one document in one
viewport and never backgrounds it).

This does **not** fix the reported failure — `D.rW == 0` short-circuits the
predicate before `document.hidden` is reached in this particular program. It is
worth having regardless. Pages spell the check as a loose comparison, and
`undefined` is not a third state any of them have a branch for: `document.hidden
== 0` is `true` for `false` and `false` for `undefined`, so an absent property
reads as "backgrounded" to the idiom that is actually used. In a serve where
`rW > 0` it would also decide whether the VM samples per dispatch or per four
dispatches — a 4× difference in the very quantity the watchdog compares against.

Covered by `Document_Hidden_Is_False_And_Compares_Loosely_To_Zero` and
`Document_VisibilityState_Is_Visible` in `GoogleSearchPolyfillTests`.

### Other gaps this turned up, not fixed here

Observed missing on `document` while probing, none of them on this failure's
path: `readyState`, `hasFocus`, `referrer`, `domain`, `lastModified`, `charset`,
`activeElement`, `currentScript`. On `window`: `trustedTypes`,
`requestIdleCallback`, `structuredClone`, `OffscreenCanvas`,
`onvisibilitychange`.

## Reading a future report of this shape

`Cannot get property length of undefined` from a `vmNN.js` frame on a Google
property is this page until shown otherwise. Before looking at the engine:

1. Dump the program with `BROILER_JS_DUMP_PROGRAMS` and check whether it defines
   `knitsail` — if it does, it is this VM.
2. Grep it for `>>14>0`. If that expression is present, the 16 384 ms watchdog
   is present, and a slow build is the first hypothesis.
3. Run `scripts/measure-google-challenge-headroom.py` on the same build. A
   headroom under ~10× makes it the likely explanation; a large headroom points
   somewhere else and is worth chasing.

# Script compile-ahead scaling — multithreading item #16

What compiling a document's classic scripts on a thread budget, ahead of the
ordered loop that evaluates them, is worth.

Reproduce with:

```sh
dotnet test src/Broiler.Cli.Tests/Broiler.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~ScriptCompileAheadOverlap" \
  --logger "console;verbosity=detailed"
```

Host: 4 cores, .NET 10, Release, Workstation GC (item #19's measured default).
Every figure is the median of interleaved pairs — one serial run and one
budgeted run back to back, repeated five times — because the two settings are
being compared on a shared machine and measuring all of one and then all of the
other charges any drift in the host entirely to whichever ran second. The full
observed range is given beside each median, because on this host the spread is
part of the answer.

## The compile stage

The same eight sources (250 distinct functions each) compiled into a fresh
per-context cache: once by an ordered inline loop calling `CoreScript.Compile`
per source — which is what `JSContext.Eval` does, minus the evaluation — and
once through `ScriptCompileAhead`.

| Budget | Serial median | Ahead median | Speedup | Serial range | Ahead range |
|---|---|---|---|---|---|
| 2 | 4 542.6 ms | 3 461.1 ms | **1.41×** | 4 085.3–5 615.7 | 3 071.3–3 930.3 |
| 4 | 4 491.7 ms | 2 779.5 ms | **1.62×** | 4 128.4–5 384.6 | 2 660.0–3 233.9 |
| 8 | 4 160.8 ms | 2 774.5 ms | **1.52×** | 4 032.4–4 385.7 | 2 646.7–2 938.5 |

It saturates at four on a four-core box, as it should, and 8 is inside the
noise of 4. What it does **not** do is approach linear: four threads buy 1.62×,
not ~4×.

## Whole capture

`CaptureService.ExecuteScriptsWithDom` end to end — parse, DOM build, script
execution, timer drain and serialization included — on the same compile-heavy
document, and on a modestly scripted one.

| Document | Budget | Serial median | Overlapped median | Speedup |
|---|---|---|---|---|
| 8 scripts × 250 functions | 4 | 6 773.0 ms | 4 789.8 ms | **1.44×** |
| 6 scripts × 30 functions | 4 | 1 448.5 ms | 1 206.2 ms | **1.22×** |

The second row is the one bounded by what a document actually contains, and it
is the honest headline: on a page whose scripts are ordinary, compilation is a
smaller share of the capture and Amdahl bounds this hard.

## Why the stage is measured apart from the capture

An earlier version of this harness derived the stage's scaling from the capture
alone and could not be published: five *identical* serial captures of the heavy
fixture landed between 7.3 s and 9.3 s, a spread wider than the difference being
measured, and the resulting "curve" read 1.35× / 1.54× / 1.07× / 1.46× at
budgets 2/3/4/8 — not a scaling curve, a noise sample. The capture charges this
item for the parse, the DOM build, the style pass, the layout and the
serialization, none of which it touches. Both figures are kept: the stage one
says what the change does, the capture one says what a host feels.

## The sub-linear ceiling is not the compile-thread handoff

`CompilationStack.Run` moves every compilation onto a second, engine-sized
pooled thread and blocks the caller, so each concurrent compile occupies two
threads. That is the obvious suspect for a 1.62× ceiling on four cores, and it
has an opt-out, so it was measured rather than assumed —
`BROILER_JS_COMPILE_STACK_BYTES=0` compiles on the calling thread instead:

| Budget | Serial median | Ahead median | Speedup |
|---|---|---|---|
| 2 | 3 823.5 ms | 2 823.1 ms | 1.35× |
| 4 | 3 742.9 ms | 2 364.7 ms | **1.69×** |
| 8 | 3 794.8 ms | 2 338.9 ms | 1.57× |

**The ceiling does not move** — 1.69× against 1.62× is inside the spread. So the
handoff is not what bounds the parallelism, and the cause of the sub-linear
scaling is **unattributed**; the remaining candidate this document can name is
the GC, since compilation is allocation-heavy and item #19 measured Workstation
as the faster mode for a whole render without asking what a compile-bound
parallel section prefers. `--gc-config` is the harness that would answer it.

**What the handoff *is* worth is a flat tax, in both directions.** Turning it
off takes the serial stage from 4 491.7 → 3 742.9 ms and the four-thread stage
from 2 779.5 → 2 364.7 ms — about 15–17% either way. That is a sequential
finding, not a threading one, and it is not a recommendation to change the
default: the engine sizes that stack because a stack overflow in the front end
is not a catchable exception on .NET, so the tax buys a host that does not abort
on a deeply nested script. It is recorded here because it is the largest single
number this item's measurement turned up, and it belongs to whoever repairs
`StackGuard` — the real fix `CompilationStack` says it is only buying time for.

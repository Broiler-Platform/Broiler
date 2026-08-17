# Why the browser hung on pages the CLI rendered

Opening `https://www.google.com` in the desktop browser never finished: the window
stayed on **"Rendering..."**, stopped responding, and read as a timeout.
`Broiler.Cli` rendered the same URL. Two things differed, and both were in the
browser's half of the load path.

## 1. The browser ran the page's timers on its message-pump thread

`ScriptEngine.ExecuteInteractive` drains **microtasks only** — it passes
`_ => MicroTasks.Drain()` as both its inter-script and final drain, where the
non-interactive `ScriptEngine.Execute` passes `DrainAsyncWork`. So every
`setTimeout`, `setInterval` and animation-frame callback a page schedules while
loading is left queued for the caller to step.

The browser stepped them from the UI thread. `BrowserWindow.StartAnimationTimer(16)`
arms a `WM_TIMER`; `Direct2DWindow`'s window procedure dispatches it to
`OnAnimationTick` → `StepAnimation` → `InteractiveSession.Step`. Every callback
batch therefore ran **inside the WndProc**, with the message pump blocked, and
`StepAnimation` then fed the re-serialised document back through
`SetHtmlWithStyleSet` — a full re-parse and re-layout, per tick. One batch of
google.com measured **13 s**. Windows calls a window that has not pumped for five
seconds "Not Responding".

The CLI never had this: `CaptureService.DrainAsyncWork` drains the same work in
one bounded loop, on its own thread, and serialises once.

## 2. The stop condition could never be true

The engine has two predicates, and `IDomBridgeRuntime` says which is which:

| Predicate | Question | Goes false on google.com? |
| --- | --- | --- |
| `HasPendingTimers` | are *any* callbacks queued? | **never** — an interval always has a next tick |
| `HasPendingTimersDueBy(horizon)` | is anything due *within the load window*? | yes |

Both non-interactive drains ask the bounded one against
`DomBridgeRuntimeLimits.AsyncDrainVirtualTimeBudgetMs` (5 s of virtual page
time), with `AsyncDrainIterationLimit` as the backstop for work that regenerates
at the current instant and never lets the clock advance.

`InteractiveSession.HasPendingWork` asks the unbounded one, and the browser gated
everything on it: the busy state and "Rendering..." status, the 16 ms tick,
`StepAnimation`, and `StopSession`. On a page holding an interval, none of those
could end.

## Measured against the live page

A harness driving `BrowserApp.LoadUrlOnWorkerAsync`'s exact sequence against
`https://www.google.com`:

| Stage | Before | After |
| --- | --- | --- |
| document GET | 0.9 s | 0.9 s |
| `LoadPageAsync` (fetch + script extraction) | 0.2 s | 0.2 s |
| `ExecuteScriptsInteractive` | 5.4 s | 5.4 s |
| load-window timers | **632 steps / 60 s on the UI thread, no closer to finishing** | **26.5 s on the load worker, settled** |
| `SetHtmlWithStyleSet` + `PerformLayout` (UI thread) | 1.8 s, then again per tick | 0.17 s, once |
| **UI-thread work** | **unbounded, in 13 s batches** | **~0.17 s** |

The CLI's `ExecuteScriptsWithDom` finishes the same page in 27.8 s, which is the
whole of why it "worked".

## The fix

- `InteractiveSession.SettleLoadWindow(CancellationToken)` runs the load window to
  a fixed point with the same loop and the same two bounds as the drains, and
  returns the settled document. `BrowserApp.LoadUrlOnWorkerAsync` calls it on the
  load worker, passing the navigation token so Stop still stops.
- `InteractiveSession.HasWorkDueInLoadWindow` asks the bounded question, and the
  viewport's busy state, tick and teardown are driven on it
  (`BrowserViewport.HasPendingWork`, `StepAnimation`, `LoadUrlOnWorkerAsync`).
- `StepAnimation` skips the re-parse when a step did not change the serialised
  document.

A settled page hands the viewport nothing to step, so the session is disposed at
load and the UI thread runs no script at all. `HasPendingWork` keeps its meaning;
it is simply not a load-completion signal. `InteractiveSessionLoadWindowTests`
covers both predicates, the settle, and its cancellation.

## What this does not fix

- **A self-rescheduling `requestAnimationFrame` loop still has no horizon.**
  `HasWorkDueBy` counts any queued frame callback as due at every horizon
  (correctly — a frame callback has no deadline), so an animating page settles
  only against `AsyncDrainIterationLimit`, and then keeps the viewport pumping
  and reporting busy. That work is now bounded and starts off-thread, but such a
  page will animate slowly: `StepAnimation` re-serialises, re-parses and re-lays
  out the document per frame. `StepDocument`, which hands the live `DomDocument`
  to the renderer instead of round-tripping through HTML, is the way out.
- **Sub-resources are re-fetched on every re-parse.** `SetHtmlWithStyleSet`
  builds a fresh `DomParser`/`StylesheetLoadHandler` each time, so every
  `<link rel=stylesheet>` is fetched synchronously again (5 s timeout each), and
  `HtmlContainerInt.TryLoadRemoteFont` re-downloads each `@font-face` on a new
  `HttpClient` (10 s each). Nothing caches between parses. Both live in
  `Broiler.HTML`, so a fix there ships as a patch under `patches/`.
- **`ExecuteScriptsInteractive` still leaves timer work undrained by design.**
  Settling is now the host's call, which is right for a host that also wants to
  animate — but every future host has to know to make it.

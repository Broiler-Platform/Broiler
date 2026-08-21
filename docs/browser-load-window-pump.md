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

## Settling silently cost every intermediate frame

Moving the load window off the UI thread was right. Running it **silently** was
not, and it took the browser's animated rendering with it.

Acid3 advances its score one test per `setTimeout`, and the whole chain — about
150 ms of page time for 100 tests — is inside the load window. The viewport used
to step it one batch per 16 ms tick, so the score counted up on screen; once the
settle ran the batches to a fixed point before the first paint, the page arrived
with its final score and none of the count. Every page that animates while
loading lost the same way, and a heavy page showed nothing at all until it was
finished: mediawiki.org's first paint moved to the end of a 33 s load.

`SettleLoadWindow` now takes an `Action<Func<string>>` and calls it after every
batch. `BrowserApp.LoadProgress` is what the browser passes:

- **The parse stays on the worker.** A frame is a serialise plus
  `BrowserViewport.CreateContentContainer`; the UI thread is posted a finished
  container and only swaps it in and lays it out. It still runs no script.
- **One frame is in flight at a time**, released in `BrowserApp.RenderFrame` once
  it has actually been painted. That self-paces: a document that lays out in
  milliseconds gets a frame per batch and animates; one that costs a second a
  frame simply gets fewer, instead of queueing work the UI thread cannot finish.
- **Frame work is capped at a quarter of the settle's own running time.** The
  document is offered as a thunk precisely so a skipped frame costs nothing to
  decline. This matters because a parse re-fetches the document's stylesheets and
  web fonts every time (see below): unbudgeted, mediawiki.org went from 33 s to
  86 s — it appeared sooner but finished much later.

Measured (Linux, CPU renderer, `BrowserApp`'s own load path):

| Page | Paints during load, before | after | Load time, before | after | First paint, after |
| --- | --- | --- | --- | --- | --- |
| Acid3 (local) | 0 | 57 | 18.9 s | 17.5 s | during the count |
| www.google.com | 0 | 3 | 29.9 s | 29.6 s | 16.9 s |
| www.mediawiki.org | 0 | 2 | 33 s | 41.2 s | 16.6 s |

`BrowserLoadProgressTests` drives the whole loop a windowed host runs against a
local page that counts inside its load window, and asserts the count reached the
screen.

## A `file://` navigation ran the whole load on the UI thread

`LoadUrl` started the load with a bare `_ = LoadUrlInBackgroundAsync(…)`, and an
async method runs on the calling thread until it first suspends. The load only
suspends if the fetch does — so for `file://`, which reads the page without ever
yielding, the fetch, the scripts *and* the entire load-window settle ran inside
the UI thread's call to `NavigateTo`. That is the freeze this document is about,
still present for local pages, and it also swallowed the intermediate frames:
the thread that was supposed to paint them was the one doing the settling. The
load now starts with `Task.Run`, so it leaves the UI thread whatever the fetch
does.

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
  `Broiler.HTML`, so a fix there ships as a patch under `patches/`. This is what
  makes an intermediate frame expensive, and so what `LoadProgress`'s quarter-of-
  the-settle budget is really rationing; a parse cache would buy the frames back.
- **`ExecuteScriptsInteractive` still leaves timer work undrained by design.**
  Settling is now the host's call, which is right for a host that also wants to
  animate — but every future host has to know to make it.

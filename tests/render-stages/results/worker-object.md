# `Worker`: a document script on its own thread, in its own realm

Multithreading item #18's remaining piece. The two facts it rests on were measured before it was
written: contexts stay isolated under real concurrency and **run genuinely in parallel**
(2.66×/3.22× at four threads — [`js-context-concurrency.md`](js-context-concurrency.md)), and a
clone taken with the receiving context current produces receiver-realm objects
([`message-channel-slice.md`](message-channel-slice.md)).

## What it is

`new Worker(url)` starts a thread that owns a `JSContext`, evaluates the worker script in it, and
then pumps messages until closed or terminated.

| side | surface |
|---|---|
| page | `worker.postMessage(v)`, `worker.onmessage`, `worker.onerror`, `worker.addEventListener('message'\|'error')`, `worker.terminate()` |
| worker | `self`, `postMessage(v)`, `onmessage`, `addEventListener('message')`, `close()`, `console` |

## The design decision that matters: messages are cloned **twice**

Two obvious designs are both wrong:

- **Clone once on the sender, hand the result over.** That puts one realm's object graph in another
  thread's hands, and the receiver would then be reading objects whose prototypes belong to the
  sender's realm.
- **Clone once on the receiver, from the sender's live value.** Worse: the sending script keeps
  running and can mutate that graph while the receiver walks it — a data race on engine internals.

So a message is cloned on the **sending** thread into a graph no script can reach, and cloned again
on the **receiving** thread with the receiving context current. The intermediate is unreachable from
either side, so nothing can mutate it while it is read. The first clone is also what makes post-send
mutation invisible (as the messaging model requires) and what raises `DataCloneError` at the right
moment, on the sender.

Both clones are the engine's own `structuredClone`. Reimplementing the walk would have meant a
second definition of which types survive, and it would have drifted from the first.

## Item #15 is kept, not bent

Each context is still driven by exactly one thread and one event loop. Nothing dispatches JavaScript
from a foreign thread: a worker's outbound message is **queued** onto the page's `BrowserEventLoop`
as a frame action — the store is a `ConcurrentDictionary`, so enqueuing from the worker thread is
safe — and the page's own drain runs it. Because pending frame actions count as pending work, a
reply in flight keeps the host's drain alive instead of racing the end of the document.

## Verified

`WorkerBindingTests`, seventeen cases, all driving the real bridge rather than the binding in isolation —
the seam most likely to be wrong is where the worker thread meets the page's loop:

| case | asserts |
|---|---|
| receives a message and replies | the round trip works end to end |
| **cloned in both directions** | worker mutation invisible to the page; page mutation after send invisible to the worker's retained copy |
| clone types survive | `Date`, `RegExp`, arrays, nesting, **and a cycle** |
| own realm, async delivery | page globals invisible to the worker and vice versa; the reply is not present when `postMessage` returns |
| missing script fires `error` | no throw from the constructor, no crash |
| terminate + disposal | a terminated worker delivers nothing; bridge disposal joins threads promptly |

**No regressions.** Adding `Worker` to the global was the real risk here — WPT tests feature-detect,
so a newly-defined global can silently change which path a test takes. It did not:

- `css/css-backgrounds` reftests: **failing set identical name for name** (266), 444/266/1 unchanged.
- `css/css-fonts` + `css/css-writing-modes`: unchanged at 685/815.
- `Broiler.Wpt.Tests`: 750 passed / 55 failed, unchanged.

## Timers, and why they are not the page's loop

`setTimeout`, `setInterval`, `clearTimeout`, `clearInterval` — one shared id space, interchangeable
clears, delays clamped so `NaN`/negative/absent all mean 0, and firing in `(deadline, seq)` order so
a later-registered shorter timeout beats an earlier longer one.

**Reusing the page's `BrowserEventLoop` was tried first and its clock is why it cannot be.** That
loop is explicitly virtual — *"not wall-clock: a synchronous drain has no real time, only the
relative ordering of deadlines"* — because the page's timers are drained in bounded bursts by a host
that wants determinism, never pumped continuously. A worker is the opposite: a long-lived pump. Under
a virtual clock a worker's `setInterval(fn, 1000)` has its deadline reached the instant the loop
looks at it, so it would fire as fast as the CPU allows and the worker would spin hot forever.

So worker timers use real deadlines, and the pump waits for **whichever comes first — an inbound
message or the next deadline**. A plain blocking take would sleep through every timer; a poll would
burn a core. An idle worker with no timers blocks indefinitely.

**What this costs is determinism, and it is confined to the worker.** A page's timers still fire on
the virtual clock and a capture is still reproducible; a worker's fire in real time, so *when* its
messages arrive relative to the page's drain is not. That is inherent to running on another thread
rather than a choice made here, and the containment is that a page only ever observes a worker
through queued messages it must explicitly wait for.

Six more cases cover it, including the two that catch a wrong pump: **a live `setInterval` must not
starve the inbox** (the bug a pump that drains timers to exhaustion before checking messages would
have), and **`terminate()` must win over a repeating timer** rather than wait for it to go quiet.

## `importScripts`

Synchronous, in order, in the worker's own global, and re-entrant — an imported script may import
further scripts.

**Specifiers resolve against the worker's own directory**, which is what the spec means by "relative
to the worker's script URL" — not the document's base path. The resolver seam therefore takes a base
directory: `null` for `new Worker(url)` (resolve against the page), the worker's directory for
`importScripts`.

That distinction is easy to get accidentally right, so the test is built so it cannot be: the worker
lives in a subdirectory and a **different file of the same name** sits next to the page, with the
page's base path pointing at it. Resolving the wrong way finds the decoy and reports the wrong
marker rather than failing to load.

Failure behaviour follows the spec: a specifier that cannot be loaded raises **`NetworkError`** and
**aborts the whole call**, so later specifiers in the same `importScripts` do not run — asserted, not
assumed. A script that *throws* propagates to the importer rather than being swallowed; catching it
would leave the worker running with a half-initialised global and no way to find out.

## Deliberately out of this slice

Refused or absent rather than half-built: module workers, `SharedWorker`, nested workers,
`requestAnimationFrame` in a worker, and transferables (an `ArrayBuffer` in a transfer list is
cloned, not transferred). Worker scripts — including imports — resolve from the filesystem only; one
that would have to be fetched over the network fires `error` (or `NetworkError` for an import)
rather than blocking a render on a request this host has no policy for.

Those are the honest boundary of a first slice. Each is additive to what is here, and none of them
changes the two-clone contract above, which is the part that would have been expensive to get wrong.

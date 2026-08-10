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

`WorkerBindingTests`, six cases, all driving the real bridge rather than the binding in isolation —
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

## Deliberately out of this slice

Refused or absent rather than half-built: timers inside a worker, `importScripts`, module workers,
`SharedWorker`, nested workers, and transferables (an `ArrayBuffer` in a transfer list is cloned,
not transferred). Worker scripts resolve from the filesystem only — a worker whose script would have
to be fetched over the network fires `error` rather than blocking a render on a request this host
has no policy for.

Those are the honest boundary of a first slice. Each is additive to what is here, and none of them
changes the two-clone contract above, which is the part that would have been expensive to get wrong.

# The `MessageChannel` slice: mostly already built, and the missing word is "cross-context"

Item #18's master-table row scopes the work as **"New: `Worker` / `MessageChannel`"**, state
**"Not implemented"**, with *structured-clone message passing* as the shape to build. Checking that
before building it — the habit this phase has been forced into — most of it is there.

## What already exists

| piece | where | state |
|---|---|---|
| `MessageChannel`, `MessagePort` | `MessagingBinding` | built, registered on every document |
| port entanglement, closed/started marks, pending queue | `MessagePortRegistry` | built |
| `postMessage` transfer lists, port transfer | `MessagingBinding` | built |
| `MessageEvent` construction and dispatch | `MessagingBinding` | built |
| `window.postMessage` with origin checks | `MessagingBinding` | built |
| **structured clone of the payload** | `CloneForMessaging` → engine `structuredClone` | **built, on both `window` and port paths** |
| `structuredClone` itself | `JSGlobal` / `BuiltInsAssemblyInitializer` | built, incl. Date/RegExp/Map/Set/ArrayBuffer/TypedArray, cycles |

Three test files already cover it (`WebMessagingTests`, `MessagePortRegistryTests`,
`MessagingBindingModuleTests`). **That is the seventh time in this phase that a row's stated state
was not the operative fact.**

## What was actually unverified: does a clone cross a *realm*?

Everything above clones a value and delivers it **within one context**, which is all a
same-document `MessageChannel` needs. A worker is the other case: the sender's value lives in
context A, and the receiver must end up with a value owned by context B sharing no object identity
with A's. **A clone that quietly produced an A-owned graph would pass every existing test and be
exactly the cross-realm leak a worker must not have.**

That is testable without threads — two contexts on one thread settle ownership completely, and
threading adds scheduling but no semantics. `CrossContextStructuredCloneTests`, 6 cases, all pass:

| case | asserts |
|---|---|
| values copied, no identity shared | the receiver sees the data; the clone is not the sender's object |
| mutating the sender does not reach the receiver | and the reverse direction, for a worker's replies |
| cloned objects belong to the receiving realm | `instanceof Array/Date/Map` and `Object.prototype` resolve in **B** |
| supported types survive, functions refused | Date/RegExp/Map/Set cross; a function throws |
| circular graphs clone | `incoming.self === incoming` |
| **two contexts have distinct intrinsics** | the guard below |

**The last case is the one that makes the third mean anything.** If two contexts shared one `Array`
constructor, every `instanceof` would answer true no matter which realm built the clone, and the
realm test would assert nothing. It asserts instead that `a.Eval("Array") !== b.Eval("Array")`,
that `Object.prototype` differs, and — the consequence that matters — that an array handed
*directly* from A into B answers **false** to B's `instanceof Array`. So the realm check genuinely
discriminates, and cross-context clone genuinely produces receiver-owned objects.

**Conclusion: the structured-clone half of item #18 is done and now has a standing gate.**

## What is left, precisely

The clone is made on the **sender's** side — `CloneForMessaging` runs before `QueueFrameAction`,
with the sender's context current — and the payload is then queued for delivery. That is correct
today because both ends of every port share a realm.

**A worker changes exactly that.** The receiving port lives in another context, so the clone has to
be produced with the *receiving* context current (or re-cloned on delivery), and the delivery itself
has to hop to the receiving thread's event loop instead of the sender's frame-action queue. Those
two are the real content of the remaining `MessageChannel` work, and they are small and named rather
than open-ended.

The rest of item #18 is the `Worker` object itself: a thread owning its own `JSContext` and event
loop, a port pair straddling them, and the lifetime/termination rules. `JSContextIsolationTests` and
`--js-context-scaling` already show that side is feasible and genuinely parallel
(2.66×/3.22× at four threads). None of that is written, and the 30–40 day, High-risk estimate stands.

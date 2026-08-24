# Broiler.JS gaps — open

> Part of the [Broiler.JS gaps](broiler-js-gaps-roadmap.md) set:
> [closed](broiler-js-gaps-closed.md) · **open** · [in progress](broiler-js-gaps-in-progress.md) · [won't fix](broiler-js-gaps-wont-fix.md).
> Statuses were last reconciled on **2026-08-24**. Every **fixed** entry names the pinned
> `Broiler.JS` commit that carries it and the regression that holds it.

Gaps that are real and not started, plus the surfaces that need an explicit product decision
before anything can be built. Work that is part-landed with named remaining steps lives in
[in progress](broiler-js-gaps-in-progress.md) instead.

A confirmed gap closes only under the rules in
[the hub](broiler-js-gaps-roadmap.md#status-and-closure-rules).

## Track 1 — Core language and built-ins

- Remaining Annex B cases must be reduced from the current manifest rather than reconstructed
  from deleted issue snapshots.
- `slice/create-proto-from-ctor-realm-array.js` — the one array case that still fails, a
  cross-realm species case. Reaching it needs the CLI host's two-`JSContext` wiring
  (`$262.createRealm`), so it is not reproducible from the engine suites alone.
- The `$262` remainder from track 0's corpus run, mostly cross-realm identity and missing-throw
  cases.

### Actions

1. Add or retain one minimal observable-value or expected-error regression for every bullet.
2. Fix parser, compiler, environment, property-storage, or built-in ownership separately; avoid
   broad fixes that make the failing suite path impossible to attribute.
3. Run the focused cluster, affected full shard, and cross-feature cases involving Proxy,
   species, accessors, strict mode, and realm boundaries.
4. Remove failure-manifest paths only after the pinned CI run confirms the change.

**Exit gate:** supported parser, eval, scope, control-flow, Array, Reflect, Symbol, property-order,
and Proxy-sensitive Test262 clusters contain no unexpected failures. Deliberate deviations are
documented separately and do not appear as ordinary failures.

## Track 3 — Scripts, tasks, and modules

Module *syntax* is closed — see
[closed](broiler-js-gaps-closed.md#track-3--module-syntax). Module *binding* semantics are
part-landed — scope isolation and import immutability are fixed in patches and live bindings are
characterized-not-fixed, in
[in progress](broiler-js-gaps-in-progress.md#track-3--module-execution-semantics). What remains
below is host semantics and two decisions.

### Confirmed host-semantic gaps

- Parser-blocking scripts execute after the complete document is parsed, so they cannot observe
  or mutate the correct mid-parse DOM. See [WPT reftest status](wpt-reftests.md).
- Deferred and module scripts, timers, rendering tasks, and microtask checkpoints use fixed phase
  buckets instead of one ordered task model. See [HtmlBridge architecture](architecture/htmlbridge.md).
- A subdocument module can start without deadlocking but its continuation and DOM effect are not
  drained in the engine-only module path. See [xUnit status](xunit-suite-status.md).
- `document.currentScript` remains approximate for unresolved or CSP-blocked sources and hosts
  that hoist async scripts. See [the focused investigation](google-about-current-script.md).

### Needs a product decision

- **Still open — a JSON module's default import is `undefined`.** `import d from './data.json'`
  yields `undefined`, so JSON modules are effectively unusable. The module host wraps a `.json`
  file as `module.exports = <json>`, which replaces the exports object with the parsed value, and
  a default import then reads `.default` off that value and finds nothing. Per ES2025 a JSON
  module has exactly one export, `default`, and no named exports — but this engine serves both
  `require` (which wants the object itself) and `import` from the same wrapper, so making the two
  agree is a product decision about the CommonJS/ESM boundary rather than a mechanical fix, and is
  left for one. Characterized, not guessed at.
- **Still open — `import.meta`** reports "import.meta not supported" (deterministic, not a crash),
  and `import defer` (stage 3) is not parsed. Both are capability decisions rather than defects.
- Attribute **enforcement**: import attributes now parse everywhere the grammar allows, but
  nothing acts on them. Rejecting a module whose type does not match its attribute is a separate
  capability.

### Actions

1. Execute parser-blocking scripts at the parser checkpoint while preserving `document.write`,
   current-script identity, and error propagation.
2. Define one ordered host task model for classic, deferred, async, and module scripts, timers,
   rendering callbacks, and microtask checkpoints.
3. Give top-level and subdocument module execution an explicit completion/drain contract.
4. Pin ordering fixtures that combine frames, promises, timers, deferred scripts, module graphs,
   failures, and navigation requests.
5. Remove current-script heuristics that associate a blocked or hoisted script with a neighbour.

**Exit gate:** parser-blocking fixtures observe the correct partial DOM; task-order fixtures match
the published model; iframe modules complete their expected DOM effects; current-script identity
is correct for success, failure, deferred, async, module, and blocked-script paths.

## Track 4 — Workers, concurrent contexts, and shared memory

### Worker first-slice gaps

The current Worker slice excludes module workers, `SharedWorker`, nested workers, worker
`requestAnimationFrame`, `MessagePort` transfer, and network-fetched worker scripts. Its lifecycle,
FIFO, cancellation, error, shutdown, failed-transfer atomicity, and explicit shared-memory policy
remain acceptance work. See [Concurrency status](../Broiler.JS/docs/roadmap/Concurrency.status.md)
and [the Worker result](../tests/render-stages/results/worker-object.md).

### Concurrent-context correctness

General concurrent-context safety is not accepted. Mutable inline-cache/site and type-feedback
ownership, every async and host entry, and disposed-context reclamation have not been fully
enumerated and validated.

### Shared memory and Atomics

Cross-agent `SharedArrayBuffer` and Atomics are not implemented. The existing single-agent or
simulated behavior does not establish shared backing-store lifetime, no-tear access, ECMAScript
ordering, atomic read-modify-write operations, waiter lists, `AgentCanSuspend`, or cleanup during
growth and termination.

### Actions

1. Specify the agent lifecycle and every queue, interleaving, error, close, and termination rule.
2. Make transfer-list validation atomic: a later invalid entry must not expose partial detachment.
3. Implement or explicitly reject each excluded Worker capability until its complete gate passes.
4. Enumerate and isolate mutable engine state before advertising general parallel contexts.
5. Treat cross-agent shared memory as a separate capability; keep it unavailable until its full
   ordering, no-tear, RMW, wait/notify, timeout, growth, and termination tests pass.

**Exit gate:** applicable Worker, structured-clone, and Test262 cases pass under deterministic
multi-agent stress; failed transfers are atomic; unsupported capabilities reject explicitly; no
shared-memory claim is made before the complete memory-model gate passes.

## Track 5 — Essential browser JavaScript APIs

### Fetch, navigation, storage, and networking

- ~~`fetch()` returns a self-returning thenable rather than a conforming chainable Promise.~~
  **Fixed** — `fetch()` and the body methods return real Promises; see
  [closed](broiler-js-gaps-closed.md#track-5--essential-browser-javascript-apis).
- `location.assign`, `replace`, `reload`, and `href=` record requests but do not navigate.
- Some HTTP subresource, iframe, worker, socket, and navigation attempts never complete or call
  back to the probing script.
- IndexedDB, Cache API, service workers, `cookieStore`, `navigator.storage`, WebSocket,
  EventSource, and `SharedWorker` are absent.

See [the privacy-page gap inventory](privacy-test-page-gaps.md) and
[the Location changelog entry](../CHANGELOG.md).

### Window, document, navigator, URL, and timing semantics

- Navigator identity, hardware, connection, permissions, storage, media-device, media-capability,
  and user-agent-data surfaces remain incomplete.
- Window and screen geometry plus `BarProp` objects are absent.
- `window.trustedTypes` is absent — a **capability decision**, not an omission: Trusted Types is an
  enforcement API (policy creation, sink guarding, CSP integration), and a shape-only stub would
  claim a policy mechanism that does not exist. The rest of that audit line —
  `document.hasFocus`, `referrer`, `domain`, `lastModified`, `charset`, `activeElement`, and
  `onvisibilitychange` — was confirmed missing and is now implemented; see
  [closed](broiler-js-gaps-closed.md#track-5--essential-browser-javascript-apis).
- ~~Non-special URLs such as `data:` can report an empty `.protocol`.~~ **Does not reproduce** — see
  [closed](broiler-js-gaps-closed.md#retired--did-not-reproduce).
- Performance Navigation Timing exposes no timing marks. This is an API-semantic gap, not speed
  work. (`performance.now()` no longer reports a whole-millisecond wall clock — it is now monotonic
  and sub-millisecond; see [closed](broiler-js-gaps-closed.md#track-5--essential-browser-javascript-apis).)

See [the privacy inventory](privacy-test-page-gaps.md),
[the Google current-script investigation](google-about-current-script.md), and
[the Google post-consent investigation](google-search-post-consent-challenge.md).

### Actions

1. ~~Replace the fetch thenable with Promise-conforming settlement and chaining while retaining
   correct `await` behavior.~~ **Done.**
2. Define capture-mode navigation semantics and a complete callback/error contract; do not expose
   browser-like methods whose only observable effect is silent non-navigation.
3. Implement storage and networking APIs in independently testable slices with origin, lifetime,
   failure, and frame/worker behavior pinned from the start.
4. Complete foundational window, document, navigator, URL, screen, and timing properties before
   using broad compatibility pages as acceptance evidence.
5. Publish an API support matrix that distinguishes implemented, negative stub, deliberately
   unsupported, and not-yet-implemented surfaces.

**Exit gate:** Promise chaining, navigation/callback, URL, timing, origin, frame, worker, and
storage fixtures pass for every claimed API; every absent API has an explicit product decision
and deterministic detection behavior.

## Track 6 — DOM, CSSOM, SVG, and script-visible document behavior

### DOM interface and collection model

- DOM wrappers do not consistently use genuine interface/prototype chains.
- `Blob`, `FileList`, `NodeList`, `HTMLCollection`, and per-tag `HTML*Element` constructors remain
  undefined; `childNodes` returns a JavaScript array instead of `NodeList`.
- Qualified mixed-case attributes such as `viewBox`, `preserveAspectRatio`, and `xlink:href` can
  be inaccessible through canonical DOM lookup.
- CharacterData failures are not proper `DOMException` objects.
- ~~`compareDocumentPosition` returns `-1`, `0`, or `1` instead of the required position bitmask.~~
  **Does not reproduce** — it returns the correct bitmask; see
  [closed](broiler-js-gaps-closed.md#retired--did-not-reproduce). The companion
  `Node.DOCUMENT_POSITION_*` constants *were* missing, which is what made the correct bitmask
  undecodable; that half is now **fixed** — see
  [closed](broiler-js-gaps-closed.md#track-6--dom-cssom-svg-and-script-visible-document-behavior).

See [HTML5 exceptions](html5test-exceptions.md) and
[the DOM bridge roadmap](../Broiler.DOM/docs/roadmap.md).

### Custom Elements, templates, and Shadow DOM

- WPT currently relies on a `customElements` runner shim; there is no production implementation.
- `template.content` is a snapshot rather than the parser-owned fragment required by HTML.
- Shadow DOM uses synthetic markers, selector rewriting, and light-child hiding rather than a
  canonical shadow and composed tree with slot assignment, fallback, hit-testing, traversal, and
  event retargeting.

See [the WPT shim record](wpt-rendering-gaps-fixed.md) and
[the root roadmap](ROADMAP.md).

### CSSOM, fonts, SVG, and JS-visible layout algorithms

- A linked stylesheet can report zero `cssRules`, while `getComputedStyle` ignores its
  declarations.
- `getComputedStyle().display` can report `inline` for every element.
- Font Loading is a synchronous compatibility facade and accepts malformed non-empty shorthands.
- SVG lacks conforming live DOM integration for features such as `requiredFeatures` and
  `SVGStringList`; serialized rendering prevents some script mutations and cascade changes from
  reaching paint.
- Current tests retain JS-visible failures involving SVG `elementFromPoint`, writing-mode
  `scrollIntoView`, keyframes read from style text, scroll clamping, and mutated iframe state.

See [open WPT gaps](wpt-rendering-gaps-open.md),
[MediaWiki computed-style evidence](mediawiki-vector-rendering.md),
[the Font Loading changelog entry](../CHANGELOG.md), and
[current xUnit status](xunit-suite-status.md).

### Actions

1. Establish real interface prototypes and Web IDL collection behavior before adding more
   compatibility-only constructor globals.
2. Fix attribute, CharacterData, position-bitmask, range, mutation, and exception semantics with
   focused DOM regressions.
3. Implement production Custom Elements and parser-owned template contents.
4. Replace the synthetic Shadow DOM model with canonical shadow/composed-tree ownership.
5. Make CSSOM rules and computed style read from the same declarations used by cascade and
   rendering.
6. Connect live SVG DOM mutations to cascade and paint.
7. Characterize form dirty/default/reset/radio behavior before promoting it from the retest queue.

**Exit gate:** claimed DOM interfaces have correct prototypes, collections, exceptions, and
algorithms; Custom Elements, templates, shadow/composed trees, CSSOM, computed style, and SVG
mutations pass focused WPT paths without runner-only shims.

## Track 7 — Graphics, media, and advanced Web APIs

These surfaces require product scope decisions before implementation because several are
deliberately absent rather than accidentally broken.

### Canvas and graphics

- Canvas `measureText` is approximate and font-insensitive.
- Affine transforms, `drawImage`, gradients, patterns, clipping, ellipses, line dashes, `Path2D`,
  and `toBlob` are absent.
- The backing bitmap is script-readable but is not painted into the page.
- WebGL, `OffscreenCanvas`, and CSS Paint are absent.

### Media, communications, devices, and security

- Web Audio is absent.
- WebRTC, `RTCDataChannel`, media devices, and `getUserMedia` are absent.
- MediaSource and media playback are negative stubs without a playback pipeline.
- `crypto.subtle` is absent.
- Generic sensors, speech synthesis, and Bluetooth are absent.
- Notifications expose denial behavior because there is no presentation surface.

See [HTML5 JavaScript exceptions](html5test-exceptions.md),
[the privacy API inventory](privacy-test-page-gaps.md),
[open WPT API gaps](wpt-rendering-gaps-open.md), and [the changelog](../CHANGELOG.md).

### Actions

1. Record implement, defer, or unsupported decisions for each surface and for each claimed product
   profile.
2. Complete Canvas 2D and page-paint integration as one coherent capability rather than exposing
   unrelated methods over an invisible bitmap.
3. For every approved API, define security, origin, lifecycle, error, and worker/frame behavior
   before exposing its global.
4. Keep unapproved capabilities absent or explicitly negative; do not add shape-only stubs that
   imply usable functionality.

**Exit gate:** every advertised graphics, media, communications, device, and security API passes
its focused behavior suite; every deferred or unsupported capability is explicit in the support
matrix and produces deterministic feature detection.

## Track 8 — Conditional portable and Native-AOT profile

Broiler.JS currently has no general JavaScript path for environments where dynamic code emission
is prohibited. The portable implementation is a numeric precompiled-bytecode seed and lacks
general JavaScript values, objects, calls, closures, exceptions, modules, eval, async/generators,
and host integration.

This is a conditional capability roadmap, not a defect in the supported Full IL profile. Start it
only after the owning product decision chooses an execution-only, narrow-runtime, full-runtime,
or no-go profile.

See [the Phase 6 plan](../Broiler.JS/docs/roadmap/Phase-6.md),
[current status](../Broiler.JS/docs/roadmap/Phase-6.status.md), and
[public profiles](../Broiler.JS/docs/public-api.md).

**Exit gate:** expected results, Full-profile results, and portable results agree for every
approved capability; every claimed runtime identifier publishes and executes that profile; narrow
profiles publish deterministic exclusions; a no-go decision removes the implied general-JavaScript
claim.

## Still in the retest queue

Not yet confirmed as current defects. Do not schedule fixes for these until the
smallest current-pointer reproduction exists.

- an unreproduced module-initializer-ordering failure — **narrowed, still open.** It is a single
  recorded `ModuleExtensions.Tests` failure whose first test is order-dependent by construction
  ("before the BuiltIns `[ModuleInitializer]` that wires it had run"); 12 further runs are clean,
  which with the 9 already on record is 21 without a recurrence — but the owning record's own
  point stands, that a handful of runs cannot separate a 1-in-10 flake from a 1-in-10 regression,
  so it is not retired. One *related* order dependence has been removed since: compilation
  back ends registered from a `[ModuleInitializer]` that only ran if the host happened to load
  the emitter assembly, which is now forced (below); and
- form-control dirty/default/reset/radio semantics, which remain uncharacterized.

**Retest rule:** add the minimal current-pointer reproduction first. If it reproduces, move it to
the owning track and apply the normal closure gate. If it does not, record the exact cases and
revisions tried, then retire or narrow the note rather than carrying it as an asserted gap.

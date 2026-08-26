# Broiler.JS gaps — open

> Part of the [Broiler.JS gaps](broiler-js-gaps-roadmap.md) set:
> [closed](broiler-js-gaps-closed.md) · **open** · [in progress](broiler-js-gaps-in-progress.md) · [won't fix](broiler-js-gaps-wont-fix.md).
> Statuses were last reconciled on **2026-08-26**.

Gaps that are real and not started, plus the surfaces that need an explicit product decision
before anything can be built. Work that is part-landed with named remaining steps lives in
[in progress](broiler-js-gaps-in-progress.md) instead.

**This document holds only what is still open.** A fixed or retired item is removed from here
outright and lives in [closed](broiler-js-gaps-closed.md), which keeps its root cause, what landed
and the evidence — so a gap appears in exactly one place and "what is left?" can be read straight
off this page. Where fixing one thing left a smaller thing behind, only the remainder is stated
here, without its history.

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
part-landed — scope isolation and import immutability are fixed and landed upstream, live bindings
are characterized-not-fixed, in
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

- **`import defer`** (stage 3) is not parsed — a capability decision rather than a defect.
  `import.meta`, listed here with it, is decided and implemented; see
  [closed](broiler-js-gaps-closed.md#track-3--module-binding-semantics). What that decision left
  open is **`import.meta.resolve`**, which is absent for a reason that is itself the next
  decision: `JSModuleContext.Resolve` is existence-based — it probes for the file and answers null
  when nothing is there — while `import.meta.resolve` resolves a specifier to a URL whether or not
  anything is at it. Building it on today's resolver would throw where a browser answers. Making
  the resolver able to answer without loading is the change that would unblock it, and it is
  shared with anything else that needs to resolve without fetching.
- **Requiring an import attribute on a JSON module** — the one part of attribute enforcement left
  open, and deliberately. Enforcement is otherwise done and is in
  [closed](broiler-js-gaps-closed.md#track-3--module-binding-semantics): an unknown key and a
  duplicate key are early SyntaxErrors, an unknown module type and a `type` that does not match the
  resolved module are load-time TypeErrors. What is *not* enforced is the converse — a `.json`
  module imported with no attribute at all loads here, where a browser rejects it.
  <br>The argument for leaving it is written up with the fix: on the web the attribute defends
  against a server returning JSON where script was expected, a mismatch that cannot arise in a host
  whose locally resolved key is itself the type, and this context serves `require` from the same
  place, where no attribute exists at all. The argument against is portability — source that works
  here and not in a browser. It is pinned by a test either way, so changing it is a decision.

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

- `location.assign`, `replace`, `reload`, and `href=` record requests but do not navigate.
- Some HTTP subresource, iframe, worker, socket, and navigation attempts never complete or call
  back to the probing script.
- IndexedDB, Cache API, service workers, `cookieStore`, WebSocket, EventSource, and `SharedWorker`
  are absent. `navigator.storage` exists and truthfully reports an empty quota, because none of the
  backends it counts does — see [closed](broiler-js-gaps-closed.md#track-5--essential-browser-javascript-apis);
  it starts reporting real numbers when they land.

See [the privacy-page gap inventory](privacy-test-page-gaps.md) and
[the Location changelog entry](../CHANGELOG.md).

### Window, document, navigator, URL, and timing semantics

- **`window.trustedTypes` is absent — a capability decision, not an omission.** Trusted Types is an
  enforcement API (policy creation, sink guarding, CSP integration), and a shape-only stub would
  claim a policy mechanism that does not exist.

See [the privacy inventory](privacy-test-page-gaps.md),
[the Google current-script investigation](google-about-current-script.md), and
[the Google post-consent investigation](google-search-post-consent-challenge.md).

### Actions

1. Define capture-mode navigation semantics and a complete callback/error contract; do not expose
   browser-like methods whose only observable effect is silent non-navigation.
2. Implement storage and networking APIs in independently testable slices with origin, lifetime,
   failure, and frame/worker behavior pinned from the start.
3. Complete foundational window, document, navigator, URL, screen, and timing properties before
   using broad compatibility pages as acceptance evidence.
4. Publish an API support matrix that distinguishes implemented, negative stub, deliberately
   unsupported, and not-yet-implemented surfaces.

**Exit gate:** Promise chaining, navigation/callback, URL, timing, origin, frame, worker, and
storage fixtures pass for every claimed API; every absent API has an explicit product decision
and deterministic detection behavior.

## Track 6 — DOM, CSSOM, SVG, and script-visible document behavior

### DOM interface and collection model

- **The engine's own members are still own properties of each wrapper**, so an interface prototype
  carries nothing of its own: `Text.prototype.splitText` is `undefined` and
  `Object.getOwnPropertyNames(node)` lists the whole interface. The prototype *chain* is real, so a
  page can extend `Element.prototype` and be heard; what has not happened is the engine putting its
  members there. Relocating them is the larger object-model change, and the one this item turns on.
  `Range`, `Selection` and `Blob` are the worked examples of the target shape — their members do
  live on their prototypes, with per-instance state in a weak table.
- **An SVG element reports `SVGElement`** where a browser says `SVGRectElement`, `SVGSVGElement`
  and the rest. The per-tag SVG interfaces are not registered at all, and minting globals purely so
  a name can be reported is what this track's action 1 rules out — so it is a capability decision
  (implement the SVG interface set, or keep the base) rather than an oversight. Pinned as the
  current answer.
- **`document.all` is absent, and cannot be added at this layer.** A browser's is an
  `HTMLAllCollection` whose `typeof` is `"undefined"` and which is falsy — the `[[IsHTMLDDA]]`
  internal slot, the one legacy exotic behaviour ECMAScript still specifies. The engine has no way
  to mint an object with that slot, so the collection is implementable and its distinguishing
  behaviour is not; adding it as an ordinary object would make the standard `document.all`
  feature-detect (which reads truthiness, precisely to exclude it) answer the wrong way round. This
  needs a `Broiler.JS` capability before it is a bridge question at all.
- **There is no *user* selection.** `Selection` is implemented for everything a script drives, but
  nothing populates it on its own, no `selectionchange` fires (nothing but script can change it),
  and the selection is not painted — a rendering question rather than a scripting one.
  `Selection.modify()` needs text segmentation this engine does not have, and
  `getComposedRanges()` needs the canonical shadow tree that is still open below; both are pinned so
  implementing either is a decision rather than a drift.

See [HTML5 exceptions](html5test-exceptions.md) and
[the DOM bridge roadmap](../Broiler.DOM/docs/roadmap.md).

### Templates and Shadow DOM

Custom Elements are complete — the core slice, customized built-ins, `adoptedCallback` and form
association are all in [closed](broiler-js-gaps-closed.md#track-6--dom-cssom-svg-and-script-visible-document-behavior).
`formStateRestoreCallback` is the one reaction that never fires, and deliberately: it reports a value
restored by session history or an autofill pass, and this engine performs neither.

- **Shadow DOM uses synthetic markers**, selector rewriting, and light-child hiding rather than a
  canonical shadow and composed tree with slot assignment, fallback, hit-testing, traversal, and
  event retargeting.

See [the WPT shim record](wpt-rendering-gaps-fixed.md) and
[the root roadmap](ROADMAP.md).

### CSSOM, fonts, SVG, and JS-visible layout algorithms

- **Font Loading is a synchronous compatibility facade** — a modelling choice rather than a parsing
  defect. Broiler resolves fonts synchronously against what it already has, so `status` is always
  `"loaded"`, `ready` is already resolved and `check()` of a *parsable* shorthand is always `true`.
  Whether to model a real load — which needs a font pipeline that can report one — is a capability
  decision, not a bug fix.
- **SVG lacks conforming live DOM integration** for features such as `requiredFeatures` and
  `SVGStringList`; serialized rendering prevents some script mutations and cascade changes from
  reaching paint.
- **The JS-visible failures this line was written for are all fixed** — SVG `elementFromPoint`,
  mutated iframe state, writing-mode `scrollIntoView`, and the `foreignObject` layout gap that
  outlived them; see
  [closed](broiler-js-gaps-closed.md#track-6--dom-cssom-svg-and-script-visible-document-behavior).
  Two others once listed here no longer reproduce — a `@keyframes` rule read from style text answers
  the same `type`/`name`/`cssRules.length` triple (`7`/`spin`/`2`), and out-of-range
  `scrollTop`/`scrollLeft` writes clamp identically. That was a spot check of one shape each rather
  than the failing cases the line was written from, so the owning manifests are what should settle
  those two.
- **A `viewBox` leaves `foreignObject` content unplaced.** The remainder of the layout gap above.
  A `<foreignObject>`'s HTML content is laid out against the element's viewport rect, but only where
  one user unit is one CSS pixel. A `viewBox` maps user space by a scale that is a function of the
  viewport's *used* size, and the placement pass runs before layout, so it cannot know it; under one
  the content keeps no box while the element itself still reports the rect its attributes resolve to.
  Chromium on `<svg width="200" height="100" viewBox="0 0 100 100">` with a `foreignObject` at
  `(10,10) 40×40` holding a 20×20 `<div>` answers `60,10,40,40` for the element and `60,10,20,20`
  for the `<div>`; Broiler answers the element's rect and `0,0,0,0` for the `<div>`. Pinned by
  `SvgForeignObjectContentTests.UnderAViewBoxTheContentKeepsNoBox`, so closing it is a deliberate
  change. Resolving it means placing the box after the viewport's used size is known rather than
  during the box fix-ups — the same "resolve against used size" shape as the nested-`<svg>` viewport
  whose own box position is not SVG-accurate either.
- **`getComputedStyle` does not apply the UA stylesheet's `display`**, so every element whose display
  comes from the UA sheet rather than an author rule reports the CSS initial value: a plain `<div>`
  in the body answers `inline`, not `block`, and a `<script>` or `<noscript>` answers `inline` rather
  than `none`. The bridge has the table and the resolution — `CssUserAgentDefaults.DisplayValues` and
  `ApplyUserAgentDisplayDefaults` — but only the anchor resolver consults them; the JS binding's
  computed map does not, so nothing the UA sheet says about `display` reaches script. It does reach
  *rendering*, which is why this is a CSSOM gap and not a layout one: the elements lay out correctly.
  <br>Recorded before now on a single element in `NoscriptRenderingTests` ("it reports `inline` for a
  `<script>` too, and for every other element the UA stylesheet hides … it wants its own change") and
  measured across elements while closing the `foreignObject` gap above, whose own record wrongly
  attributed the `inline` to the missing box. Fixing it is a narrow change at one call site; what it
  needs is a pass over the assertions written against the current answer, since it moves a value many
  tests read.

See [open WPT gaps](wpt-rendering-gaps-open.md),
[MediaWiki computed-style evidence](mediawiki-vector-rendering.md),
[the Font Loading changelog entry](../CHANGELOG.md), and
[current xUnit status](xunit-suite-status.md).

### Actions

Most of the original list is complete and recorded in
[closed](broiler-js-gaps-closed.md#track-6--dom-cssom-svg-and-script-visible-document-behavior),
production Custom Elements among it; what is left of its first action is the element-wrapper half
named in the first bullet above. The rest:

1. Relocate the engine's own interface members onto the interface prototypes, so an instance
   carries no members of its own.
2. Replace the synthetic Shadow DOM model with canonical shadow/composed-tree ownership.
3. Make CSSOM rules and computed style read from the same declarations used by cascade and
   rendering.
4. Connect live SVG DOM mutations to cascade and paint.

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

- **An unreproduced module-initializer-ordering failure — narrowed, still open.** It is a single
  recorded `ModuleExtensions.Tests` failure whose first test is order-dependent by construction
  ("before the BuiltIns `[ModuleInitializer]` that wires it had run"); 12 further runs are clean,
  which with the 9 already on record is 21 without a recurrence — but the owning record's own
  point stands, that a handful of runs cannot separate a 1-in-10 flake from a 1-in-10 regression,
  so it is not retired. One *related* order dependence has been removed since: compilation
  back ends registered from a `[ModuleInitializer]` that only ran if the host happened to load
  the emitter assembly, which is now forced.

**Retest rule:** add the minimal current-pointer reproduction first. If it reproduces, move it to
the owning track and apply the normal closure gate. If it does not, record the exact cases and
revisions tried, then retire or narrow the note rather than carrying it as an asserted gap.

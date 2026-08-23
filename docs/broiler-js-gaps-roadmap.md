# Broiler.JS gaps roadmap

- **Status:** Active
- **Scope:** Missing, incomplete, unsupported, or observably incorrect JavaScript behavior
- **Last reconciled:** 2026-08-23
- **Evidence basis:** Repository-wide Markdown audit plus the current component revisions

This document consolidates JavaScript gaps recorded anywhere in the Broiler repository, not
only under `Broiler.JS`. It therefore includes core ECMAScript behavior and JavaScript-visible
host, DOM, CSSOM, SVG, worker, and browser APIs. The implementation owner may be Broiler.JS,
HtmlBridge, Broiler.DOM, Broiler.CSS, or another component.

Execution speed, allocation, startup, tiering, caching, boxing, benchmark scores, and other
performance-only work are out of scope. Web Performance API defects remain in scope when the
problem is missing or incorrect observable behavior rather than speed.

This is a coordination roadmap, not a replacement for the owning documents or current failure
manifests. Where this document and an older investigation disagree, the current known-gap test,
current failure manifest, current component roadmap, and current source revision take priority.

## Status and closure rules

- **Confirmed gap:** currently reproduced, present in a current failure manifest, or retained by
  an explicit known-gap regression.
- **Coverage gap:** the runner or host cannot yet provide trustworthy conformance evidence.
- **Capability decision:** an absent platform surface that needs an explicit implement, defer, or
  unsupported-product decision.
- **Retest:** suspected, historical, or unreproduced behavior that is not asserted as a current
  defect.
- **Deliberate exclusion:** a documented profile or product boundary; it is not a Full-profile
  engine defect unless Broiler advertises the excluded capability.

A confirmed gap closes only when:

1. a minimal repository regression fails before and passes after the change;
2. the focused pinned Test262 or WPT path and the affected full shard pass;
3. the failure is removed from its manifest only after CI confirmation;
4. unsupported cases continue to fail deterministically rather than partially succeeding; and
5. the owning status document and publishable compliance evidence are reconciled together.

## Sources of truth

- [Broiler.JS known compliance gaps](../Broiler.JS/docs/compliance/known-gaps.md)
- [Broiler.JS component roadmap](../Broiler.JS/docs/roadmap/Component.md)
- [Broiler.JS compliance dashboard](../Broiler.JS/docs/compliance/dashboard.md)
- [Broiler.Regex implementation status](../Broiler.JS/Broiler.Regex/Broiler.Regex/README.md)
- [Broiler.Regex roadmap](../Broiler.JS/Broiler.Regex/docs/roadmap.md)
- [JavaScript concurrency plan](../Broiler.JS/docs/roadmap/Concurrency.md) and
  [status](../Broiler.JS/docs/roadmap/Concurrency.status.md)
- [Privacy-page API inventory](privacy-test-page-gaps.md)
- [HTML5 JavaScript exceptions](html5test-exceptions.md)
- [Open WPT rendering and API gaps](wpt-rendering-gaps-open.md)
- [Current xUnit suite status](xunit-suite-status.md)
- [DOM bridge roadmap](../Broiler.DOM/docs/roadmap.md)

Do not copy changing pass/fail totals into this roadmap. Link the exact result artifact or update
the dashboard instead.

## Roadmap summary

| Order | Track | Current state | Required outcome |
|---:|---|---|---|
| 0 | Conformance evidence | Coverage gaps closed; pinned-corpus CI run outstanding | Test failures and timeouts are trustworthy |
| 1 | Core language and built-ins | Confirmed gaps | Supported Test262 language clusters are clean |
| 2 | RegExp | Partial implementation | ECMAScript syntax and matching semantics use a complete backend |
| 3 | Scripts, tasks, and modules | Partial host semantics | Parsing and task ordering match observable browser behavior |
| 4 | Workers and shared memory | Worker first slice; shared memory not started | Claimed agent capabilities are complete and deterministic |
| 5 | Essential browser JavaScript APIs | Mixed partial, absent, and stubbed surfaces | A tested support matrix replaces accidental omissions |
| 6 | DOM, CSSOM, and SVG from JavaScript | Partial object and tree models | Script-visible objects and algorithms meet their claimed standards |
| 7 | Graphics, media, and advanced APIs | Large capability decisions | Each surface is implemented or explicitly excluded |
| 8 | Portable/Native-AOT profile | Numeric seed only | Optional profile decision and, if approved, a truthful capability set |

Tracks 1 and 2 can proceed in parallel once track 0 makes their results trustworthy. Tracks 3
through 7 share host and DOM dependencies and must use one published support matrix rather than
silently exposing partial globals.

## Track 0 — Restore trustworthy conformance evidence

**Status: the coverage gaps below are closed; the remaining work is a full pinned-corpus
run in CI and the product decisions for three `$262` hooks.**

### What changed

- **Async results follow test262's marker protocol.** `$DONE` is upstream
  `doneprintHandle.js`, injected into every `flags: [async]` test, and it prints
  `Test262:AsyncTestComplete` / `Test262:AsyncTestFailure:`. No marker, two markers, or a
  failure marker are each a failure with the kind recorded (`asyncCompletion`); a test that
  neither settles nor returns is ended by the per-test timeout. Measured correction over a
  seeded 400-file sample of the 5487 script-goal async files: 10.3% of async results were
  passes that are not passes (~560 across the corpus).
- **Fixtures that must fail.** `Broiler.JS/scripts/compliance/fixtures/async-protocol/`
  holds deliberately failing, rejecting, never-settling, double-completing,
  dying-after-completing and never-returning tests with the verdict each must produce;
  `run_test262.py --self-check` enforces them, and every CI shard runs it first.
- **`module` and `raw` are executed, not skipped.** A module test runs in place under
  `--module-host` with its harness preloaded as a script (so `assert` and `$DONE` are
  globals its body and its `_FIXTURE.js` imports can see); a raw test is handed the file's
  own unmodified bytes. 824 module and 30 raw files were previously reported as skipped.
- **`$262` is defined** for `global`, `createRealm`, `detachArrayBuffer`, `evalScript` and
  `gc`, and a test is excluded for the exact hook it needs and lacks rather than for
  mentioning `$262` — 640 more files now run.
- **An uncaught error is reported by its JavaScript name** (`Uncaught SyntaxError: …`), so
  `negative: phase: parse` tests are matched on the type they raise instead of failing on
  the diagnostic while rejecting the program correctly.
- **Per-mode totals** (selected, executed, passed, failed, skipped, timed out) ride on every
  shard report, survive the shard merge, and appear in both CI summaries.

See [the host-coverage inventory](../Broiler.JS/docs/compliance/known-gaps.md#host-coverage-gaps)
and [the component host-mode plan](../Broiler.JS/docs/roadmap/Component.md#2-expand-host-mode-coverage).

### Remaining work

1. Run the pinned supported corpus in CI under the new protocol and modes. A local run of
   the 1494 files the modes unblocked (824 module, 30 raw, 640 `$262`; Debug host, so read
   it as shape rather than as a rate) executed all of them: raw 30/30 pass, `$262` 515 of
   640, module 332 of 824. The failures are engine work, mapped below — none of it is host
   coverage:
   - 141 module early errors that never fire (`dup-bound-names`, `await` as a module
     identifier, JSON module validation) → track 1;
   - 109 files whose specifier the parser rejects (`import defer`, import attributes) →
     track 1;
   - 52 that hang, nearly all dynamic `import()` of a module exporting a class or function
     → track 3;
   - 22 NullReferenceException crashes in module namespace and ambiguous-export paths →
     track 3;
   - 12 "invalid program" IL failures on top-level `for await` → track 1;
   - the `$262` remainder, mostly cross-realm identity and missing-throw cases → track 1.
2. Record product decisions for the three excluded `$262` hooks: `$262.agent` (112 files,
   multi-agent Atomics — owned by track 4), `$262.IsHTMLDDA` (42 files) and
   `$262.AbstractModuleSource` (8 files).
3. Enable `--include-negative` in release runs so negative-metadata totals are published.

**Exit gate:** deliberately failing and never-settling async fixtures fail deterministically;
every Test262 file is executed by an appropriate host mode or has a precise product-scope
exclusion; the dashboard records exact engine and suite revisions.

## Track 1 — Core language and built-in correctness

### Direct eval, parser, scope, and control flow

- A captured read after deleting an eval-introduced `var` retains the torn-down cell instead of
  re-resolving the name outward.
- `new.target` is rejected in a direct eval nested inside an eval-compiled function.
- An empty statement does not correctly terminate a Directive Prologue.
- Comment and regular-expression-literal lexical edge cases remain in the failure manifest.
- Labeled and unlabeled `continue`, block-scoped loop bindings, and other lexical-environment
  cases remain open.
- Remaining Annex B cases must be reduced from the current manifest rather than reconstructed
  from deleted issue snapshots.
- A parameter named `undefined` does not shadow the global binding.
- Async and generator bodies do not enter the runtime strict-mode scope, so a failed strict
  `[[Set]]` may not throw.

Evidence:

- [Active semantic clusters](../Broiler.JS/docs/compliance/known-gaps.md#active-semantic-clusters)
- [Current component failure clusters](../Broiler.JS/docs/roadmap/Component.md#1-close-the-supported-test262-failure-set)
- [Parameter-shadowing record](../Broiler.JS/docs/roadmap/Phase-3.status.md)
- [Strict async/generator record](../Broiler.JS/docs/roadmap/Archive.md), retained by
  [Measurement.md](../Broiler.JS/docs/roadmap/Measurement.md)

### Objects, arrays, symbols, and Proxy-sensitive behavior

- Symbol own keys enumerate by Symbol-creation order rather than property-creation order.
- `slice`, `unshift`, `toReversed`, `reduceRight`, array mutation limits, near-maximum lengths,
  and Proxy-created results retain confirmed failure paths.
- `Reflect.set(base, key, value, receiver)` gives a new receiver property the base property's
  attributes instead of the all-true descriptor required by `CreateDataProperty`.

Evidence:

- [Symbol and array gaps](../Broiler.JS/docs/compliance/known-gaps.md#active-semantic-clusters)
- [Reflect.set known deviation](../Broiler.JS/docs/roadmap/Phase-2.status.md)

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

## Track 2 — Complete ECMAScript RegExp behavior

### Confirmed implementation gaps

- `\p{...}` and `\P{...}` Unicode property escapes parse, but resolution throws
  `NotSupportedException`.
- `v`-mode class-set intersection, subtraction, nesting, and `\q{...}` string alternatives are
  parse-only.
- Ignore-case matching has only ASCII and partial simple folding rather than the complete
  ECMAScript `Canonicalize` tables.
- Annex B leading/trailing escapes, RegExp literal cases, and Unicode ignore-case paths remain
  open in the tracked corpus.
- Native routing remains conservative; `Split`, `Replace`, and `IJSRegExp.Value` still use the
  .NET backend.

See [the current limitations](../Broiler.JS/Broiler.Regex/Broiler.Regex/README.md#known-limitations-stubbed--todo),
[the RegExp roadmap](../Broiler.JS/Broiler.Regex/docs/roadmap.md), and
[the integration gate](../Broiler.JS/docs/roadmap/Component.md#3-finish-regexp-backend-adoption).

### Actions

1. Connect Unicode property escapes to reviewed Broiler.Unicode property data.
2. Implement and test UnicodeSets operands and string alternatives before routing `v` patterns.
3. Implement complete mode-sensitive canonicalization and pin astral and multi-script cases.
4. Move `Exec`, `Split`, and `Replace` to one match-data abstraction.
5. Expand native routing only for syntax and semantics covered by focused and pinned corpus tests.
6. Retire the translator only after captures, named groups, indices, `lastIndex`, species,
   replacement substitutions, and observable property order are clean.

**Exit gate:** the pinned supported RegExp corpus is clean without sending unsupported syntax to
the native backend, and all public RegExp operations consume the same conforming match data.

## Track 3 — Scripts, tasks, and modules

### Confirmed host-semantic gaps

- Parser-blocking scripts execute after the complete document is parsed, so they cannot observe
  or mutate the correct mid-parse DOM. See [WPT reftest status](wpt-reftests.md).
- Deferred and module scripts, timers, rendering tasks, and microtask checkpoints use fixed phase
  buckets instead of one ordered task model. See [HtmlBridge architecture](architecture/htmlbridge.md).
- A subdocument module can start without deadlocking but its continuation and DOM effect are not
  drained in the engine-only module path. See [xUnit status](xunit-suite-status.md).
- `document.currentScript` remains approximate for unresolved or CSP-blocked sources and hosts
  that hoist async scripts. See [the focused investigation](google-about-current-script.md).

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

- `fetch()` returns a self-returning thenable rather than a conforming chainable Promise.
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
- `document.hasFocus`, `referrer`, `domain`, `lastModified`, `charset`, `activeElement`,
  `window.trustedTypes`, and `onvisibilitychange` remain unresolved in the current audit.
- Non-special URLs such as `data:` can report an empty `.protocol`.
- `performance.now()` uses a whole-millisecond wall clock rather than a monotonic source, and
  Performance Navigation Timing exposes no timing marks. These are API-semantic gaps, not speed
  work.

See [the privacy inventory](privacy-test-page-gaps.md),
[the Google current-script investigation](google-about-current-script.md), and
[the Google post-consent investigation](google-search-post-consent-challenge.md).

### Actions

1. Replace the fetch thenable with Promise-conforming settlement and chaining while retaining
   correct `await` behavior.
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
- `compareDocumentPosition` returns `-1`, `0`, or `1` instead of the required position bitmask.

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

## Retest queue — not yet confirmed as current defects

Do not schedule fixes for these until the smallest current-pointer reproduction exists:

- suspected overlap or offset wrong answers in `TypedArray.prototype.set`;
- older `Intl.DateTimeFormat` range/parts, SameValue, and Proxy-ordering reports;
- historical M0 failures where `JSON.stringify` ignored a `toJSON` result and
  `Array.isArray(new Proxy([], {}))` returned false;
- a rejected function-`prototype` write historically changing later `[[Construct]]` behavior;
- the archived observation that async continuations did not run under in-process `Eval` or
  `Execute`;
- an unreproduced module-initializer-ordering failure; and
- form-control dirty/default/reset/radio semantics, which remain uncharacterized.

Sources:

- [TypedArray gate](../Broiler.JS/docs/roadmap/Component.md#immediate-correctness-gate-typedarrayprototypeset)
- [Older compliance triage](../Broiler.JS/docs/compliance/known-gaps.md)
- [M0 Test262 subset](../tests/m0-baseline/conformance/test262-subset/test262-subset-summary.md)
- [Historical status reconciliation](../Broiler.JS/docs/roadmap/Roadmap.status.md)
- [Archived async observation](../Broiler.JS/docs/roadmap/Archive.md)
- [Module initialization record](../Broiler.JS/docs/roadmap/Phase-1.status.md)
- [DOM form roadmap](../Broiler.DOM/docs/roadmap.md)

**Retest rule:** add the minimal current-pointer reproduction first. If it reproduces, move it to
the owning track and apply the normal closure gate. If it does not, record the exact cases and
revisions tried, then retire or narrow the note rather than carrying it as an asserted gap.

## Stale and deliberately excluded records

Do not reopen these solely because older Markdown calls them pending:

- the older direct-eval lexical-closure fixes are landed; the two direct-eval issues in track 1
  are different defects;
- the RegExp embedded-NUL and terminal-backslash fixes are landed;
- the prefix/postfix parser fix for forms such as `!c++ && 1` is landed;
- the Broiler.CSS evaluator used by `CSS.supports()` is landed;
- the main `document.currentScript`, `readyState`, `requestIdleCallback`, `sessionStorage`, and
  `structuredClone` surfaces have later fixed evidence, although narrower edge cases above remain;
- Broiler.HTML static-renderer exclusions do not prove that the aggregate Browser/HtmlBridge stack
  lacks every excluded API; and
- Minimal and deliberately narrow Portable profiles are not gaps in the Full profile.

Removed or proprietary surfaces such as WebSQL and `chrome.loadTimes()` are not roadmap work.
Diagnostics that merely hide first-chance exceptions are useful tooling work but are not language
feature gaps and are not tracked here.

## Completion gate

This roadmap is complete when:

1. every confirmed item is fixed or converted into an explicit, reviewed product exclusion;
2. supported Test262 and applicable WPT modes produce trustworthy, reproducible results;
3. no runner shim or shape-only stub is required to claim a supported JavaScript feature;
4. every unsupported global or method has deterministic detection and failure behavior;
5. the retest queue is empty or contains only dated, explicitly deferred investigations; and
6. the component roadmaps, support matrix, known-gap inventory, and compliance dashboard agree.

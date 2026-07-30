# Broiler root roadmap

- **Status:** Active preview
- **Scope:** Only unfinished work that crosses component or application boundaries
- **Last reconciled:** 2026-07-30

Component-local work is tracked in the roadmaps linked from
[the documentation index](README.md). This file does not repeat completed
extractions, phase logs, or per-test investigation journals.

## Release and distribution

### Publish a reproducible first preview

**Current evidence:** the repository has component package metadata, the
[`nuget-packages`](../.github/workflows/nuget-packages.yml) workflow, a
lockstep preview version, SourceLink/symbol-package configuration, and
commit-scoped human-review records.

**Next actions:**

1. Validate installation from an isolated package feed without relying on the
   aggregate checkout.
2. Ensure every release submodule pointer contains, or is paired with, every
   required pending patch recorded in [`patches/README.md`](../patches/README.md).
3. Complete exact-commit human, dependency, license, static-analysis, and
   vulnerability review for the release graph.
4. Configure the protected release environment and publishing credentials.
5. Run the tag path and install the resulting packages and applications on clean
   supported hosts.

**Exit gate:** the exact reviewed commit produces deterministic packages and
symbols, installs from the advertised feeds on supported platforms, passes the
published smoke suite, and can be reproduced without uncommitted submodule
changes.

### Deliver installation and update paths

**Current evidence:** the repository builds portable Windows and Linux
applications, but the proposed native installation and self-update system is not
a completed product surface.

**Next actions:**

- Freeze product identifiers, release channels, artifact names, signed manifest
  format, update ownership, rollback behavior, and key-rotation policy.
- Ship deterministic signed portable releases before adding an in-app updater.
- Add an atomic per-user portable install/update transaction with recovery and
  uninstall behavior.
- Add Windows and Linux native delivery only after the portable transaction and
  release-signing gates pass.
- Keep macOS delivery gated on native application, graphics, input, signing, and
  notarization support.

**Exit gate:** each claimed platform has one documented update owner, verified
signatures and hashes, rollback after interrupted activation, clean repair and
uninstall paths, and end-to-end release tests.

## Standards and test infrastructure

### Publish a reproducible Chromium baseline

**Current evidence:** Chromium 148 is captured in
[`chromium-148.lock.json`](../tests/m2-conformance/chromium-reference/chromium-148.lock.json),
but its WPT revision is unresolved and there is no published root snapshot or
alignment workflow.

**Next actions:**

1. Resolve and record exact WPT and Test262 revisions.
2. Pin the WPT and Octane workflows to reviewed commit SHAs instead of floating
   `master`.
3. Publish one generated snapshot that links the lock, focused JS/HTML/bridge
   results, visual references, and performance results.
4. Automate the refresh without making unrelated checkouts download the full WPT
   corpus.

**Exit gate:** a clean checkout can reproduce the same corpus and focused
results from immutable revisions, and an upstream refresh is a reviewable diff.

### Re-establish current Acid, Google, and WPT evidence

**Current evidence:** focused regression tests exist and historical campaigns
landed many fixes, but checked implementation tasks and a 100/100 Acid script
score do not establish current pixel fidelity or broad standards conformance.
The current WPT artifacts remain the evidence source:
[`HTML results`](../tests/html/wpt-results/) and
[`CSS results`](../tests/css/wpt-results/).

**Next actions:**

- Capture fresh Acid1/Acid2/Acid3 viewport references and report script score,
  geometry, content, and pixel metrics separately.
- Add a local HTTP fixture for the remaining Acid3 status/content-type cases.
- Re-run the Google comparison against a recorded input and Chromium revision;
  record actual milestone measurements rather than inferring compliance from API
  presence.
- Keep prioritized WPT failures in generated reports and component roadmaps.
  Root tracking should cover only cross-component runner, timeout, reference, and
  ownership problems. The worst-scoring pixel mismatches of the current run, with
  the capability each is missing and the component that owns it, are in
  [WPT rendering gaps](wpt-rendering-gaps.md).
- Prototype per-component stress attribution with a small Broiler.JS slice before
  investing in full coverage-guided selection.

**Exit gate:** every published claim names its corpus revision, environment,
metric, tolerances, skips, and reproducible command; regressions are assigned to
one owning component.

## Browser WebAssembly

The durable ownership and rendering decisions are in
[the browser WebAssembly architecture](architecture/browser-webassembly.md).
Phases 0–5 have local implementation and smoke evidence; they are not yet a
broad browser-support claim.

**Next actions:**

- Restore the phase baseline verifier's composition root by registering the
  managed image-codec catalog before PNG generation, then keep verification of
  the relocated fixture as an executable gate.
- Add committed Chromium and Firefox CI for the phase smoke suites and published
  application.
- Record frame time, input-to-present latency, memory, resize retention, payload,
  and ten-minute soak evidence for interpreted, trimmed, and supported AOT modes.
- Run real IME, trusted clipboard, keyboard-only, RTL, and screen-reader checks;
  publish the exact supported combinations.
- Finish and evidence the Writer WebAssembly workflow, browser resource
  open/save, and failure/permission UX.
- Package the browser application with immutable assets, integrity/cache policy,
  diagnostics, and an explicit support statement.
- Treat a full Broiler browser-engine port as a separate opt-in decision; it is
  not required for the Writer application preview.

**Exit gate:** the published Writer workflow passes the supported browser matrix,
performance and accessibility gates, handles capability denial honestly, and is
reproducible from CI artifacts.

## Android applications

The durable topology and platform decisions are in
[the Android application architecture](architecture/android.md): `net10.0-android`
from the .NET for Android workload, **no .NET MAUI**, one `Activity` hosting one
`SurfaceView`, and reuse of `Broiler.UI`, `Broiler.Input`, `Broiler.Graphics`,
`Broiler.Documents`, `Broiler.Browser.Core`, and `Broiler.Writer.Core` rather
than a parallel stack.

**Current evidence:** the Android input providers (phase A2) are implemented and
unit-tested; no other Android code, project, or workflow exists yet. What the
rest of the port builds on: `IUiHost` is a five-member interface with three
working implementations (Win32, X11, browser Canvas); `Broiler.Graphics` core is
platform-neutral, trimmable, and AOT-annotated, and the GPU backends only upload
and blit a CPU-rasterized frame; `Broiler.Documents` and the media codecs are
managed, with the one native image path (a WIC WebP accelerator) already guarded
to Windows and backed by a managed decoder. No Android code has run on a device
or an emulator. The gaps are equally concrete and are recorded per phase below.

Phases A1–A3 are platform-neutral or backend work that Windows and Linux touch
support would also need; A4 and A5 are the applications. Writer leads because it
needs no JS engine, no network policy, and no engine-scale surface.

### A0 — Freeze the Android platform baseline

**Owner:** root, with `Broiler.Graphics` and `Broiler.Input` consulted.

**Current evidence:** no target framework, ABI, API level, runtime, linker, or
signing decision exists for Android. The desktop applications pin
`net10.0` with `Debug-Linux`/`Release-Linux`-style configurations and explicit
runtime identifiers; the WebAssembly Writer shows the established pattern for a
non-desktop head with its own SDK, backend, and host.

**Next actions:**

1. Freeze minimum and target API levels against the workload's documented floor
   and the Play target-API requirement in force at release, plus the shipped ABI
   set (`android-arm64` required, `android-x64` for emulator CI).
2. Freeze the managed runtime and the linker/AOT mode, and record the
   consequence for JS execution: `Broiler.JavaScript.ExpressionCompiler` uses
   `System.Reflection.Emit` and cannot run under full AOT, while
   `Broiler.JavaScript.Portable` can. Writer may adopt a stricter mode than
   Browser.
3. Define the Android workspace topology for
   [`eng/solutions.json`](../eng/solutions.json) — the intended roots and the
   `forbiddenProjectPatterns` excluding Windows, Linux, and WebAssembly
   projects. The entries themselves land with the first Android project that can
   serve as a root, since a solution cannot reference a project that does not
   exist yet.
4. Record the product identifiers, package names, release channels, and signing
   key policy, and reconcile them with the pending release-and-distribution work
   above rather than inventing a second scheme.

**Exit gate:** one reviewed document states the TFM, API levels, ABIs, runtime,
linker/AOT mode, JS execution mode, workspace topology, and signing policy, and
every later Android phase cites it instead of re-deciding.

### A1 — Android graphics presentation backend

**Owner:** `Broiler.Graphics` (submodule).

**Current evidence:** `LinuxOpenGlRenderer.Render` rasterizes through
`BImageRenderer.RenderToImage` and presents the bitmap; the GL surface uses only
texture-upload and framebuffer-blit entry points, with no shader pipeline. That
makes the Android backend a translation rather than new rendering work. Three
differences are known and must be handled: Linux binds `EGL_OPENGL_API` with
`EGL_OPENGL_BIT` where Android needs `EGL_OPENGL_ES_API` and `EGL_OPENGL_ES3_BIT`;
Linux P/Invokes `libEGL.so.1` where Android's soname is `libEGL.so`; and
`glBlitFramebuffer` requires GL ES 3.0, which sets the feature floor.
Separately, `FallbackSystemFont` knows only Linux, Windows, and macOS font paths,
so an Android build currently has no usable system font.

**Next actions:**

1. Add `Broiler.Graphics.Android`: EGL context and window surface over the
   `ANativeWindow` behind a `SurfaceView`, a surface-sized texture, upload, blit,
   and swap, plus the off-screen path used by tests.
2. Implement surface loss and recreation across `SurfaceDestroyed`/`SurfaceCreated`
   and resize/rotation, reporting device loss through the existing
   `BDeviceLostException` rather than a new channel.
3. Add Android system font roots (`/system/fonts`) and a Noto/Roboto candidate
   set to `FallbackSystemFont`, keeping the existing platform lists intact.
4. Record the presentation contract: surface format, colour handling, and the
   scaling rule when the surface size and the logical viewport disagree.
5. Follow the submodule mechanics in [CLAUDE.md](../CLAUDE.md) — push to the
   component remote and bump the pointer, or, on a denied push, land a patch
   under [`patches/`](../patches/README.md) and leave the pointer untouched.

**Exit gate:** an Android device presents a frame whose pixels match the CPU
reference within the established tolerance; rotation, backgrounding, and
resize survive without leaking or losing GPU resources; text renders with a
system font on a clean device.

### A2 — Real touch, pen, and IME input

**Owner:** `Broiler.Input` for providers, `Broiler.UI` for the neutral event
surface.

**Current evidence:** the Android providers are implemented and unit-tested —
`Broiler.Input.Android` plus the `Touch`, `Pen`, `Keyboard`, and `Text` backends,
and the neutral provider contracts they needed
(`ITouchInputProvider`/`TouchOpenOptions` and the pen and text equivalents,
mirroring the keyboard pattern). These are the **first implementations of
`TouchInputDevice`, `PenInputDevice`, and `TextInputDevice` on any platform**;
Windows touch/pen and Linux touch/text remain unstarted.
`Broiler.Input.Android.Tests` covers pointer-id tracking across a finger lift,
capture-loss cancellation, tool-type routing, the IME composition state machine,
provider lifecycle, and an assembly-boundary check, and runs on any host because
the backends carry no Android SDK reference.

What is delivered is the Input half. Two things still block touch end to end:
the neutral UI event drops what the backends now produce —
`UiInputEvent.FromTouchContact` keeps only the position and discards `ContactId`,
`TouchContactState`, and `Pressure`, and `FromPenContact` does the same — and
`IUiTextInputHost` exposes only `PublishCaret`/`ClearCaret`, which cannot satisfy
`InputConnection`. Both are Broiler.UI changes. No hardware evidence exists for
any of it.

**Next actions:**

1. Extend `UiInputEvent` to carry contact identity, phase, pressure, and — for
   pen — tilt and eraser state, without regressing the mouse and keyboard paths
   that currently construct it. Until this lands, the delivered backends cannot
   express a second finger to any control.
2. Define the editor-side text contract that a real IME needs — text around the
   cursor, current selection, composing-region set/clear, and commit — as a
   `Broiler.UI` concern, since Windows TSF and browser composition need the same
   thing. `IAndroidEditorTextSource` and `AndroidTextEditRequest` are the
   Android-side statement of that gap and should collapse into the neutral
   contract when it exists.
3. Drive the providers from a real Activity and `SurfaceView`, including
   soft-keyboard show/hide, keyboard type, and IME action from editor focus.
4. Populate descriptors from `InputDevice.getDeviceIds()` and
   `InputManager.InputDeviceListener` so capability reporting and hot-plug
   reflect the real device set rather than the `RegisterDefault*` fallbacks.
5. Verify the stylus tilt conversion against a real digitizer; the formula is
   implemented and self-consistent but its sign convention is unconfirmed.

**Exit gate:** a two-finger gesture is distinguishable from two sequential taps
at the `Broiler.UI` boundary; a CJK IME composes, converts, and commits into
RichEdit with correct candidate placement; stylus pressure reaches the pen
contract; no Android type appears in `Broiler.Input` core or `Broiler.UI`.

### A3 — Touch-first interaction in Broiler.UI

**Owner:** `Broiler.UI`.

**Current evidence:** no control consumes `UiInputEventKind.TouchContact`.
`StandardScrollView` scrolls by wheel or by dragging the scrollbar thumb/track,
and its pointer path requires `MouseButton.Left`, which a touch-derived event
does not carry — so the primary scrolling gesture on a touch device does
nothing. There is no gesture recognizer, no kinetic scrolling, and no
touch-target sizing anywhere in the component. The existing pointer-capture and
focus mechanisms (`Session.CaptureInput`, `SetFocus`) are reusable.

**Next actions:**

1. Add a shared gesture recognizer over neutral contact streams — tap,
   double-tap, long-press, drag, fling with momentum, and pinch — resolved once
   and consumed by every control, not reimplemented per backend or per control.
2. Give `StandardScrollView` content-drag scrolling, fling with deceleration,
   overscroll behavior, and scroll-chaining rules; keep wheel and scrollbar
   behavior unchanged for desktop.
3. Add touch-target minimum sizes, touch-appropriate hit slop, and long-press
   context activation to the design-system token work already open in the
   component roadmap.
4. Add selection and caret handles, and a text-selection interaction model that
   works without a hover state, for `Edit` and `RichEdit`.
5. Consume host-published insets so content reflows around the soft keyboard,
   the navigation bar, and display cutouts; keep the focused caret visible when
   the keyboard opens.
6. Apply the system font-scale and reduced-motion settings to the existing
   tokens rather than adding an Android-only path.

**Exit gate:** Writer and Browser are fully operable by touch alone on a
handheld — scroll, select, edit, and invoke every command — with no control
requiring hover or a physical wheel; the same gestures work on a Linux or
Windows touch device once those providers exist.

### A4 — Broiler.Writer.Android

**Owner:** `Broiler.Writer.Android`, reusing `Broiler.Writer.Core`.

**Current evidence:** `WriterApp` is shared and already runs under three hosts.
Its document I/O is desktop-shaped: `StandardFileDialog` builds places from
`Environment.SpecialFolder` and `Directory.GetLogicalDrives`, and open/save use
`File.ReadAllBytes`/`File.WriteAllBytes` against full paths. Under Android
scoped storage those reach only the app-private sandbox. `Broiler.Documents`
(RTF, DOCX, HTML, Markdown) and the managed codecs port unchanged.

**Next actions:**

1. Add the Activity host: `SurfaceView`, `Choreographer`-driven frames that stop
   when not resumed, main-thread marshalling through `IUiHost.Post`, and
   graphics teardown/rebuild across surface loss.
2. Express Writer open/save as a stream pair plus a display name so the system
   picker (`ActionOpenDocument`/`ActionCreateDocument`) can satisfy it; the
   WebAssembly Writer has the same constraint and should share the seam rather
   than growing a second one.
3. Add autosave and crash recovery to app-private storage, and restore document
   and caret state across process death — not merely across rotation.
4. Adapt the Writer chrome for a handheld: collapsible toolbar, reachable
   command surfaces, and a formatting affordance that works without a menu bar.
5. Wire clipboard through the platform clipboard, and share/print through the
   system intents where they are supported.

**Exit gate:** open, edit, format, save, and reopen a DOCX and an RTF through the
system picker on a real device; document and caret state survive rotation,
backgrounding, and process death; the editing surface is usable by touch and
with an attached keyboard.

### A5 — Broiler.Browser.Android

**Owner:** `Broiler.Browser.Android`, reusing `Broiler.Browser.Core`.

**Current evidence:** `BrowserApp` is shared and host-agnostic, and fetches
through `HttpClient`, which needs the `INTERNET` permission and an explicit
cleartext policy. The desktop runner polls a 16 ms `PeriodicTimer`, which is not
an acceptable handheld frame loop. The preview safety notice already states that
JavaScript is not a sandbox; that statement carries more weight on a personal
device.

**Next actions:**

1. Add the Activity host on the same lifecycle, scheduling, and surface-loss
   rules as A4.
2. Adapt browser chrome to a handheld: single-column layout, touch-sized
   controls, an address surface that coexists with the soft keyboard, and system
   back mapped to history navigation.
3. Add pinch-zoom and touch panning of page content, including the viewport
   meta-tag interaction, through the shared gesture layer from A3.
4. Decide and document the JS execution mode against the A0 linker/AOT decision,
   and measure startup, frame time, input latency, and memory on a mid-range
   device rather than an emulator.
5. Restate the security posture honestly for a mobile context — controlled
   content, no sandbox claim, explicit network and permission policy — and do
   not ship a general-purpose browsing claim.

**Exit gate:** the published support statement names the exact devices, API
levels, and content scope tested; navigation, zoom, and back behave correctly on
hardware; performance and memory are recorded from a real device; no capability
is claimed that the security posture does not support.

### A6 — Android build, CI, and delivery

**Owner:** root.

**Current evidence:** the four existing workflows cover preview packaging, NuGet,
Octane, and WPT; none provisions an Android SDK, and no `.slnx` workspace exists
for an Android head. Release, signing, and update policy are already open items
under [Release and distribution](#release-and-distribution) and must not be
forked.

**Next actions:**

1. Add an Android build workflow that provisions the SDK and workload and builds
   the Android solutions on every change to the shared applications, so the port
   does not silently rot.
2. Add an instrumented smoke run — launch, render a frame, dispatch synthetic
   touch and text, rotate, background, resume — on an emulator, with the honest
   note that emulator evidence is not hardware evidence.
3. Produce signed AAB/APK artifacts through the frozen A0 signing policy and
   reconcile channels, versioning, and update ownership with the existing
   release work.
4. Record the Android support statement in [the README](../README.md) and the
   component READMEs only after the A1–A5 gates pass, and keep it scoped to what
   was measured.

**Exit gate:** a clean checkout produces a signed installable artifact through
CI; the smoke suite gates every change to the shared application code; the
published support claim names its devices, API levels, and evidence.

## HtmlBridge runtime

The current assembly and ownership boundaries are in
[the HtmlBridge architecture](architecture/htmlbridge.md).

### Component rehoming roadmap

**Current evidence:** the 2026-07-12 promotion audit was correct for the source
tree it examined, but it is no longer a complete description of HtmlBridge.
Compared with the source tree at the 2026-07-24 documentation consolidation,
55 bridge files have changed by 4,097 insertions and 1,225 deletions. The new
concentrations include the 1,030-line
[`DomBridge.ViewTransition.cs`](../src/Broiler.HtmlBridge.Dom/DomBridge.ViewTransition.cs),
4,620 lines in
[`AnchorResolver`](../src/Broiler.HtmlBridge.Dom/DomBridge/AnchorResolver/),
and new animation, stylesheet-import, shadow-selector, transform, and scroll-snap
implementations.

Several of those paths now duplicate or work around canonical facilities:

- `Broiler.Dom.Html` already owns fragment parsing and HTML-semantic tree queries.
- `Broiler.CSS` exposes parsed `@import` metadata, while `Broiler.CSS.Dom` owns
  stylesheet scope assembly and the canonical style engine.
- `Broiler.Layout` already has native anchor, animation, zoom, and used-geometry
  paths, but bridge fallbacks still calculate or bake parts of the same behavior.
- [`GetRenderDocument`](../src/Broiler.HtmlBridge.Dom/DomBridge.Serialization.cs)
  now imports the canonical tree into an isolated renderer projection before
  applying compatibility transforms and reflecting runtime state. The projection
  is still a bridge-private mechanism rather than a neutral renderer contract.

The second-wave rule is to move reusable models and algorithms, not entire
bridge-shaped classes. JavaScript identity, callbacks, promises, events, browser
policy, resource access, and session lifecycle remain in HtmlBridge.

#### Target ownership

The API names below are working names; the ownership and dependency direction
are the contract.

| Current bridge slice | Better canonical home | What remains in HtmlBridge |
| --- | --- | --- |
| Table, select, form, and fragment tree operations in `Features/*Binding.cs` and `HtmlFragmentMutation.cs` | `Broiler.Dom.Html`: stateless HTML element queries and mutations first, then a document-scoped companion for engine-neutral control dirty/default/selection rules; generic tree primitives remain in `Broiler.Dom` | IDL overloads and exceptions, JS collection identity, callbacks, and conversion to/from the canonical control state |
| `#shadow-root` state, slot projection, and selector stamping | `Broiler.Dom`: a real `DomShadowRoot`, host link, slot assignment, and composed-tree traversal; `Broiler.CSS.Dom`: scoped matching for `:host`, `:host-context`, and `::slotted`; `Broiler.HTML.Dom`/orchestration: composed-tree rendering | `attachShadow()` wrappers, JS identity, mode policy, focus/event behavior, and lifecycle |
| `StyleImports`, `StyleBaseHref`, constructed/adopted-sheet application, and bridge stylesheet assembly | `Broiler.CSS`: token-aware import and URL algorithms; `Broiler.CSS.Dom`: a document style set over parsed, adopted, imported, and scoped sheets; `Broiler.Dom.Html`: effective document-base and link/meta semantics | Fetching, CSP and origin policy, JS `CSSStyleSheet`/rule-list identity, and host-supplied source text |
| Bridge animation rule lookup, selector matching, easing, and value interpolation | `Broiler.CSS`: typed keyframes and value interpolation; `Broiler.CSS.Dom`: cascade/rule selection; `Broiler.Layout`: used-value sampling and application | `Animation` objects, promises, timelines, callbacks, event dispatch, and document scheduling |
| Transform, scroll-snap, anchor, sticky, hit-test, top-layer, and replaced-element fallback calculations | `Broiler.Layout`: an additive layout snapshot with visual geometry, clipping, scroll extents, snap/anchor results, and paint/hit-test order; `Broiler.HTML`: native replaced-element and top-layer painting | CSSOM View argument/result conversion, browser scroll state, events, dialog/popover state, and layout-session coordination |
| View-transition CSS matching, snapshot geometry, synthetic pseudo-tree, and paint properties | `Broiler.CSS.Dom`: transition pseudo-style resolution; `Broiler.HTML`/`Broiler.Layout`: a neutral `ViewTransitionRenderPlan` and snapshot/overlay rendering | `startViewTransition()`, update callback/thenables, promises, transition lifecycle, and capture identity |
| Serialization transforms and `HtmlPostProcessor` | Native `Broiler.HTML` rendering fed by a non-destructive render context; Acid/WPT-only cleanup belongs in `Broiler.Wpt` or test fixtures | Projection of genuinely live bridge state into neutral render inputs; no production regex cleanup |
| Runtime and execution responsibilities in `Broiler.HtmlBridge.Core` | Runtime interfaces to `Broiler.HtmlBridge.Dom`; module execution plus execution DTOs/profiling to `Broiler.HtmlBridge.Scripting`; generic meta discovery to `Broiler.Dom.Html` | Host orchestration and public v2 forwarding facades until a major-version boundary |
| Logging, URL/origin/CSP, and resource responsibilities in `Broiler.HtmlBridge.Core` | `RenderLogger` to `Broiler.Diagnostics`; injected web URL/origin/security/resource primitives to a small host-services component only after its dependency seam is proven | Security decisions, credentials/network policy, and browser API bindings |

Canvas is deliberately not a current extraction candidate: its implementation is
primarily JS-observable state and the draw calls are still no-ops. A future real
display-list or raster contract belongs in `Broiler.Graphics`; moving the stub
would only put bridge behavior in the wrong assembly.

#### DOM component rehoming

**Owner:** `Broiler.DOM` for the canonical APIs, HtmlBridge Dom for the cutover
and deletions, `Broiler.CSS.Dom` as the one other consumer.

The DOM-bound slices of Phase 1 and Phase 3 are specific enough to sequence on
their own. API design, unit tests, and component exit gates are items D1–D8 in
[the Broiler.DOM roadmap](../Broiler.DOM/docs/roadmap.md); this section owns the
order, the bridge-side cutover, the guard changes, and the submodule mechanics.

**Current evidence (2026-07-30 source tree):** `Broiler.HtmlBridge.Dom` is 36,080
lines against 2,473 in `Broiler.Dom` and 1,180 in `Broiler.Dom.Html`. The
DOM-bound share is small and identified: roughly 2,000 lines of neutral algorithm
to promote plus roughly 500 lines of bridge shim to delete. Specifically,
`DomBridge/Attributes.cs` holds a case-insensitive qualified-name attribute
lookup with ~195 bridge call sites that is independently reimplemented in
`HtmlElementQueries.ReadNumericAttribute` and `CssSelectorMatcher.MatchesAttribute`;
`Features/CharacterDataBinding.cs` implements DOM §4.10 against a 54-line
`DomCharacterData`; `DomBridge.GetElementTextContent` duplicates canonical
`DomNode.TextContent`; and the legacy-facade shim block in `DomBridge.cs`
(`ChildElements`, `ChildAt`, `SetParent`, `IsText`, and siblings) accounts for
~490 occurrences that mostly paraphrase canonical members. Everything larger in
the assembly — `AnchorResolver` (4,620), `LayoutMetrics*` (2,718),
`DomBridge.ViewTransition.cs` (1,024), the stylesheet and import code — belongs
to Layout, CSS, or HTML per the target-ownership table above, not to `Broiler.DOM`.

**Waves.** Each wave lands the canonical API first, cuts every consumer over, then
deletes the old path. Waves 1–4 are behavior-preserving apart from two deliberate
spec corrections that need their own tests — the `compareDocumentPosition` bitmask
replacing a tri-state result, and character-data offset failures becoming a named
`IndexSizeError` instead of a raw exception string. Waves 5 and 6 are gated on
evidence that does not exist yet.

| Wave | Canonical items | Bridge cutover and deletion | Other consumers | Gate |
| --- | --- | --- | --- | --- |
| 1 — API gaps | D1 attribute accessors, D2 character data, D3 `textContent` setter / `compareDocumentPosition` / element traversal | Delete the `Attributes.cs` accessor set, `GetElementTextContent`, `SetElementTextContent`, `CompareTreeOrder`, and the arithmetic in `CharacterDataBinding` | `CssSelectorMatcher.MatchesAttribute`, `HtmlElementQueries.ReadNumericAttribute` | Owner suites, bridge guards, pinned WPT/Acid A/B |
| 2 — shim retirement | D4 `ChildNode`/`ParentNode` mixins | Classify and retire the `DomBridge.cs` shim block; inline canonical members at ~490 sites | None | Owner suites, full bridge suites, pinned WPT/Acid A/B |
| 3 — HTML element ops | D5 `HtmlTableOperations`, `HtmlSelectQueries` | Reduce `TableBinding` and `SelectBinding` to wrapper installation and coercion; promote or delete `IsTableCellElement` | None | Owner suites, targeted table/select tests, WPT A/B |
| 4 — fragment and metadata | D6 fragment context, base-href discovery, meta scanners, adjacency resolution | Delete `HtmlTreeBuilding.cs` and the bridge `<base>`/`<meta>` scans; keep CSP policy and the `innerHTML` orchestration | `Broiler.HtmlBridge.Core` CSP discovery | Owner suites, CSP tests, pinned pixel A/B (serialization-visible) |
| 5 — form control state | D7 `HtmlFormState`, `HtmlFormQueries` | Move the neutral reflectors first; move state only after the baseline | None | Recorded Chromium characterization baseline, then owner suites |
| 6 — shadow model | D8 `DomShadowRoot`, slots, composed traversal | Delete selector stamping, marker attributes, light-child hiding, sentinel unwrapping | `Broiler.CSS.Dom` scoped matching, `Broiler.HTML.Dom` and Layout composed painting | Phase 3 exit gate |

**Ordering rationale:** wave 1 removes duplication that every later wave would
otherwise have to preserve in three places. Wave 2 follows it because the
attribute shims cannot be classified until the canonical accessors exist, and
because interleaving the two diffs would make a ~490-site cleanup unreviewable.
Waves 3 and 4 are independent of each other and can run in parallel. Wave 5 is
blocked on characterization, not on the earlier waves. Wave 6 is Phase 3 and is
blocked on a canonical model that does not exist yet; it must not be
started by promoting the current synthetic `#shadow-root`.

**Guard and gate changes:**

1. Extend `HtmlBridgeBoundaryGuardTests` so each completed wave is pinned: no
   case-insensitive attribute scan outside `Broiler.Dom`, no bridge copy of
   `textContent` or document-order comparison, and no bridge table/select tree
   arithmetic. Assert against types and members, not source text, wherever the
   reflection surface allows it.
2. The deleted helpers are `internal static`, so `htmlbridge-public-surface/v2`
   snapshots should be unaffected. Confirm that per wave rather than assuming it;
   a snapshot diff is a v3 question, not a refactor detail.
3. The 750-line file ratchet should move down as waves land. Do not add a new
   exemption to accommodate a promotion.

**Submodule mechanics.** `Broiler.DOM` is a submodule, so every wave is two
changes: the component commit, then the parent pointer bump plus the bridge
adapter. Follow the documented order in [CLAUDE.md](../CLAUDE.md) — push the
component commit first and bump the pointer only if the push succeeded; on a 403,
capture the change under [`patches/`](../patches/README.md) with an index entry
and leave the pointer untouched. Two additional constraints apply here:

- `Broiler.CSS` nests its own `Broiler.DOM` submodule pointer. Wave 1 is consumed
  by `CssSelectorMatcher`, so it requires bumping the nested pointer as well as
  the aggregate one; bumping only the aggregate leaves `Broiler.CSS` compiling
  against a DOM without the new accessor.
- No wave may be declared complete while its canonical commit is unreachable from
  a pushed pointer, because CI clones submodules by pointer.

**Exit gate:** the bridge contains no reimplementation of a DOM or HTML-semantic
algorithm that `Broiler.Dom`/`Broiler.Dom.Html` can express; the attribute
lookup, character-data mutation, tree mixins, table/select operations, and
document-metadata queries each have exactly one owner with owner-local tests;
`Broiler.Dom` is still dependency-free and `Broiler.Dom.Html` still references
only `Broiler.Dom`; and the WPT/Acid failure set is identical or improved at every
wave boundary.

#### Phase 0 — trustworthy gates and a non-destructive seam

**Owner:** HtmlBridge, `Broiler.HTML`, and `Broiler.Layout` integration.

**Current evidence:** the stale runtime-state guard now recognizes the
concern-specific per-session state tables, and the 750-line ratchet records
`DomBridge.ViewTransition.cs` as its only current exemption;
`DomBridge.Serialization.cs` is below the limit. Render preparation imports into
an isolated owner document and
[`RenderProjectionIsolationTests`](../src/Broiler.Cli.Tests/RenderProjectionIsolationTests.cs)
pin live version, mutation-record, attribute, and child-tree invariants.
`DomBridgeSessionOptions` allows simultaneous sessions to use different layout
factories. Legacy hosts still register the process-static fallback, native zoom
and anchor inputs remain thread-static, and the projection inputs are not yet a
public neutral `Broiler.HTML` contract.

**Next actions:**

1. Migrate CLI, WPT, baseline, and test composition roots from the compatibility
   `LayoutViewFactory` fallback to `DomBridgeSessionOptions`, then remove the
   process-static fallback.
2. Add a session-scoped neutral render input in `Broiler.HTML.Orchestration`
   (for example, `RenderDocumentContext`) containing the canonical document,
   base URI, resolved stylesheet sources, form values, nested documents, and
   neutral shadow/top-layer/transition state; make the private projection produce
   that contract.
3. Replace the remaining thread-static native zoom, anchor, and visual-viewport
   channels with immutable layout-request/session options.
4. Record HTML, geometry, and pixel parity baselines for the projection cutover.
   Do not add new one-shot serialization transforms.
- Give the element JS wrapper a shared prototype instead of a per-instance
  surface. `DomBridge.ToJSObject` installs the whole element API — every
  reflector, method, `style`, `classList`, `dataset` — onto each wrapper object,
  so one script-created element costs **~550 KiB** of retained memory. Measured
  on this container (peak RSS of `Broiler.Wpt --render`, 107 MiB baseline):

**Exit gate:** all repaired guards pass; two simultaneous sessions can use
different renderer/layout configuration; a render pass is non-destructive; and
old and new projection paths have recorded HTML, geometry, and pixel baselines.
  | Page | Peak RSS |
  | --- | --- |
  | 10 000 `<span>`s in the markup (no wrapper) | 223 MiB |
  | 4 000 `document.createElement("span")`, never inserted | 2 290 MiB |
  | 4 000 created and appended | 2 294 MiB |

  The cost is linear per created element and attaches to `createElement` itself,
  not to insertion, layout, or style: creating the wrapper is what allocates.
  This is what the WPT runner's per-test memory guard reports as
  `Program.Main — Test aborted after exceeding the … per-test memory limit`
  (issue #1491 problems 2 and 3: `css/css-variables/url-syntax-crash.html` and
  `editing/crashtests/insertparagraph-in-listitem-in-svg-followed-by-collapsible-spaces.html`,
  both of which create ~10 000 elements from script — the custom property in the
  first is incidental, a bare 10 000-span loop blows the same limit). It crosses
  the bridge and the JS engine's object model, so it cannot be fixed inside
  either alone.

**Exit gate:** all execution surfaces use the same ordered scheduling model,
session dependencies are instance-scoped, native zoom/top-layer behavior is
enabled for every supported consumer, a script-created element's wrapper costs a
constant handful of bytes over its canonical node, and focused plus broad
regression gates remain green.

#### Phase 1 — pure promotions and quick deletions

**Owner:** `Broiler.DOM`, `Broiler.CSS`, `Broiler.CSS.Dom`, HtmlBridge adapters,
and the production/test hosts that call `HtmlPostProcessor`. The `Broiler.DOM`
share of actions 1 and 4 is sequenced in
[DOM component rehoming](#dom-component-rehoming) as waves 1–5.

**Current evidence:** `HtmlElementQueries.CollectTableRows` is already canonical
while neighboring caption/section/row/cell operations remain in `TableBinding`;
`HtmlDocumentParser.ParseFragment` is canonical while the bridge still owns the
mutation orchestration; `CssomRuleMetadata.GetImport` is canonical while
`StyleImports` scans CSS text again. Position-try collection and transform
parsing also have multiple consumers or implementations.

**Next actions:**

1. Add tested `Broiler.Dom.Html` table algorithms, stateless select option/value
   queries, fragment mutation operations, and generic document metadata queries.
   Characterize and correct form validity/default-state behavior before
   canonizing it.
2. Add token-aware CSS import/URL traversal and a typed transform representation
   to `Broiler.CSS`; add one DOM-aware position-try rule collector to
   `Broiler.CSS.Dom`.
3. Replace the bridge's limited animation selector matcher with the canonical
   scoped style engine.
4. Remove thin parser wrappers such as `HtmlTreeBuilding` after their callers use
   canonical APIs.
5. Prove native script and iframe-fallback rendering in Browser and Capture,
   remove `ProcessForBrowsing`, and relocate or retire each Acid/WPT-only
   transform instead of moving its regex into `Broiler.HTML`.

**Exit gate:** each promoted API has owner-local unit tests and no JS/bridge/host
dependency; every old caller delegates to it; duplicate parsing/tree logic is
deleted; and production rendering no longer calls `HtmlPostProcessor`.

#### Phase 2 — one document stylesheet authority

**Owner:** `Broiler.CSS`, `Broiler.CSS.Dom`, `Broiler.Dom.Html`, and HtmlBridge
CSSOM/resource adapters.

**Current evidence:** `CssStyleScopeBuilder` already synchronizes ordered,
media-filtered sources into `CssStyleEngine`, but the bridge separately expands
imports, rebases URLs, applies CSSOM and adopted-sheet mutations, and builds
render-time style text. The bridge's computed-style projection already delegates
most cascade work to `CssStyleEngine` and should not grow into another engine.

**Next actions:**

1. Evolve the scope builder into a document style-set service over parsed author,
   imported, constructed/adopted, and shadow-scoped sheets. Accept resolved text
   or a narrow resource callback; never perform network or CSP policy in CSS.
2. Put effective base-URL discovery in the HTML semantic layer and token-aware
   CSS URL resolution in CSS, with the host supplying document/resource URLs.
3. Close canonical computed-style gaps such as explicit `inherit` folding and UA
   display defaults, then route CSSOM reads, layout consumers, and renderer
   consumers to the appropriate canonical result.
4. Keep JS stylesheet/rule wrappers as live bridge objects, but make their
   mutations update the one canonical style set.

**Exit gate:** one document style set supplies CSSOM reads, layout, and rendering;
external I/O remains injected; and serialization no longer needs
`ApplyCssomStyleSheetMutations`, `InlineStyleSheetImports`,
`ApplyAdoptedStyleSheets`, or CSS URL/base rewrites.

#### Phase 3 — canonical shadow and composed trees

**Owner:** `Broiler.Dom`, `Broiler.CSS.Dom`, `Broiler.HTML.Dom`, Layout, and the
HtmlBridge Shadow DOM adapter. The canonical model in action 1 is item D8 of
[the Broiler.DOM roadmap](../Broiler.DOM/docs/roadmap.md), delivered as wave 6 of
[DOM component rehoming](#dom-component-rehoming).

**Current evidence:** shadow ownership is a bridge runtime table plus a synthetic
`#shadow-root`; rendering deletes/hides light children, unwraps the sentinel, and
rewrites selectors onto marker attributes. Promoting those workarounds would make
them permanent.

**Next actions:**

1. Add dependency-free shadow-root/host relationships, slot assignment, and
   composed-tree traversal to `Broiler.Dom`.
2. Teach `Broiler.CSS.Dom` scoped rule provenance and shadow selectors against
   that model.
3. Make `Broiler.HTML.Dom` and Layout consume the composed tree while retaining
   canonical node identity for geometry and events.
4. Cut bridge wrappers over the canonical model, then delete selector stamping,
   marker attributes, light-child hiding, and sentinel unwrapping.

**Exit gate:** DOM, style, layout, rendering, hit testing, and event retargeting
share one shadow/composed-tree model; no render pass structurally rewrites the
document; and focused Shadow DOM plus broad WPT/pixel comparisons show no new
regressions.

#### Phase 4 — native layout and renderer cutover

**Owner:** `Broiler.Layout`, `Broiler.HTML`, every `ILayoutView`/renderer consumer,
and thin HtmlBridge CSSOM View adapters.

**Current evidence:** native anchor and zoom routes exist, but
`AnchorResolver`, `LayoutMetrics.Transform`, `LayoutMetrics.ScrollSnap`, zoom
serialization, progress/meta/top-layer transforms, and compatibility flags still
carry parallel behavior. A geometry-only dictionary is too narrow for consumers
that then reconstruct used layout policy.

**Next actions:**

1. Add an additive `LayoutSnapshot` or sibling query contract with visual rects,
   scroll extents, clipping, snap offsets, anchor/position-try results, used
   values, and paint/hit-test order.
2. Inventory each residual bridge fallback as a native-layout parity case. Extend
   Layout rather than transplanting `AnchorResolver` or bridge CSS-length math.
3. Make `Broiler.HTML` natively own meta color-scheme canvas policy, replaced
   elements, composed-tree painting, top layer/backdrop, and every remaining
   production render behavior.
4. Cut over Browser, Capture, WPT, and baseline-engine consumers before deleting
   bridge anchor, sticky, scroll-snap, transform, zoom, hit-test, and render-bake
   fallbacks.

**Exit gate:** every supported consumer uses the native path; CSSOM View is a thin
projection over a shared layout snapshot; native zoom, anchor, sticky, snap,
top-layer, and replaced-element behavior pass focused and broad gates; and the
fallback switches and corresponding bridge implementations are deleted.

#### Phase 5 — animation and view-transition split

**Owner:** `Broiler.CSS`, `Broiler.CSS.Dom`, `Broiler.Layout`, `Broiler.HTML`, and
HtmlBridge lifecycle adapters.

**Current evidence:** CSS interpolation exists independently in bridge CSS
animation resolution, Web Animations, and Layout. View-transition code combines
JS promises and lifecycle with selector parsing, geometry capture, synthetic
DOM, clipping, and painting in one file.

**Next actions:**

1. Add one canonical CSS keyframe/value interpolator with narrow callbacks for
   percentage or used-length resolution, then reuse it from Layout and Web
   Animations.
2. Let the canonical style engine select animation and view-transition rules;
   let Layout sample used values.
3. Define a neutral renderer-facing view-transition plan containing capture IDs,
   old/new snapshots, geometry, clipping, stacking, and resolved pseudo styles.
4. Keep callbacks, promises, timelines, events, and lifecycle in HtmlBridge;
   delete bridge selector/interpolation and synthetic-paint implementations after
   the native route is universal.

**Exit gate:** CSS parsing/interpolation has one authority; Layout/HTML own
sampling and pixels; bridge animation/view-transition code contains only JS and
document lifecycle; and no canonical assembly references a JavaScript type.

#### Phase 6 — Core consolidation and public cleanup

**Owner:** HtmlBridge Dom/Scripting, host composition, and the public API boundary.

**Current evidence:** the 1,507-line Core assembly mixes runtime contracts,
logging, CSP/origin/URL policy, HTML metadata discovery, script fetching,
microtasks, execution DTOs, and profiling. Its public types are protected by the
`htmlbridge-public-surface/v2` snapshots, so physical relocation is not an
internal refactor.

**Next actions:**

1. Move runtime contracts to Dom and execution/module/profiling DTOs to Scripting
   behind v2 forwarding facades or type forwarders. Inject a module executor into
   Dom before moving the current module context, so subdocument execution does
   not create a Dom-to-Scripting dependency cycle.
2. Fold `MicroTaskQueue` into the document event loop while replacing fixed
   script-phase buckets with one ordered task model for scripts, timers,
   animation frames, and microtask checkpoints.
3. Generalize parser-backed CSP meta discovery in `Broiler.Dom.Html`; keep script
   discovery there, but make `ScriptExtractionService` a Scripting facade over
   injected security and resource services.
4. Consolidate script/style/frame/fetch/XHR resource access behind an injected
   host service, while keeping CSP enforcement, credentials/network policy, and
   browser bindings outside canonical components.
5. Define an injected diagnostics contract for the existing bridge, CLI, WPT,
   and DevConsole consumers, move `RenderLogger` to `Broiler.Diagnostics`, and
   keep the public v2 facade. Create a web-primitives assembly only when its API
   and dependency seam are independently useful; do not use a new assembly as a
   folder.
6. Decide at an approved v3 boundary whether the remaining Core compatibility
   assembly is retained, renamed, or dissolved.

**Exit gate:** Core has one documented cohesive purpose or no implementation;
all execution surfaces share the ordered event loop and injected host services;
the v2 API snapshots remain compatible until an explicit v3; and direct Browser,
CLI, WPT, baseline-engine, and DevConsole consumers have migrated.

#### Delivery and evidence rules

For every slice:

1. Land owner-local neutral APIs and unit tests in the canonical submodule first.
2. Push the submodule commit, then update the parent pointer and add the thin
   bridge adapter; use the documented patch fallback if the commit cannot be
   made reachable.
3. Cut over all production and test consumers before deleting the old path.
4. Keep temporary dual-route switches document/session-scoped. Remove the switch
   immediately after its final parity gate; do not turn it into permanent
   configuration.
5. Keep public-v2 changes additive or behavior-preserving. Reserve type removal
   and assembly reshaping for an approved v3.
6. Run the owner suites, HtmlBridge architecture/boundary/public-API guards,
   targeted bridge tests, and the applicable Release solution builds. Render-visible slices
   additionally require pinned WPT/Acid/pixel A/B evidence with an identical or
   improved failure set.

Success is fewer competing authorities and a thinner adapter, not a target line
count. No canonical component may reference HtmlBridge or JavaScript runtime
types, and no networking, CSP enforcement, or JS object identity may leak into
DOM, CSS, Layout, or HTML merely to reduce the bridge assembly.

## Linux application preview

Graphics, input, layout, media, and UI details belong to their component
roadmaps. Root work is limited to application composition and release evidence.

**Next actions:**

- Complete the supported graphics presentation path, input ownership migration,
  resize/device-loss handling, and deterministic fallback behavior.
- Run Browser and Writer smoke suites on the declared distro/driver matrix,
  including software rendering and permission-denied input cases.
- Record package dependencies, evdev permissions, diagnostics, accessibility
  limitations, and hardware evidence.

**Exit gate:** Browser and Writer install and run on the declared Linux matrix,
produce comparable artifacts, shut down without leaked native resources, and
publish an evidence-based preview support statement.

## PDF conversion decision

`Broiler.Cli --convert-pdf` describes an external `Broiler.Pdf` application, but
no `src/Broiler.Pdf` project exists in the current checkout. Do not continue an
old parser milestone as though that baseline were present.

**Next action:** choose one of the following and record an owner:

- restore/scaffold the standalone application and re-baseline its corpus,
  dependencies, CLI compatibility, security limits, and M1 entry gate; or
- remove the unavailable source-project fallback and narrow the CLI/documentation
  claim to an explicitly external tool.

**Exit gate:** the advertised CLI behavior resolves to a shipped, tested tool, or
fails with documentation that exactly matches the supported configuration.

## Maintenance policy

- Completed implementation records are removed after durable decisions and open
  work have moved to their owners.
- Pending submodule changes are tracked in
  [`patches/README.md`](../patches/README.md), not duplicated in incident
  roadmaps.
- New work enters this file only when at least two components or a root
  application/release workflow must coordinate to close it.

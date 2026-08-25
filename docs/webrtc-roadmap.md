# WebRTC implementation roadmap

- **Status:** Active design; implementation has not started
- **Scope:** Cross-component delivery of WebRTC 1.0, media capture, data channels,
  real-time audio/video, browser policy, conformance, and supported-platform release
- **Last verified:** 2026-08-24
- **Primary owner:** Browser and HtmlBridge integration
- **Contributing owners:** Broiler.WebRtc, Broiler.Input, Broiler.Graphics,
  Broiler.Media/Playback, platform application heads, test infrastructure, release,
  and security
- **Sequencing authority:** This document for cross-component WebRTC work; an owning
  component roadmap and ADR control component-local implementation details

This roadmap turns WebRTC from an explicitly absent feature into a measured,
secure, interoperable browser capability. It is an implementation plan, not a
support claim. Until the applicable release gate passes, the corresponding Web
globals remain absent in production profiles; Broiler must not expose
shape-compatible objects backed by no transport, capture, or media behavior.

## Outcome and decisions at a glance

The target is standards-based `RTCPeerConnection`, `RTCDataChannel`,
`MediaStream`, `MediaStreamTrack`, `navigator.mediaDevices`, and
`HTMLMediaElement.srcObject`, with real ICE/STUN/TURN, DTLS-SRTP, SCTP, RTP/RTCP,
audio, video, permissions, statistics, and teardown behavior.

The shortest credible route is:

1. Make a document's JavaScript realm and event loop survive beyond the current
   bounded page-load window.
2. Select and pin a maintained native WebRTC engine behind a Broiler-owned stable
   C ABI after a time-boxed evidence spike. Do not implement the protocol suite
   from scratch.
3. Land neutral RTC contracts and deterministic fakes, then implement a data-channel
   vertical slice before device access.
4. Reuse the existing `Broiler.Input.Camera` and `Broiler.Input.Microphone`
   contracts for capture, adding privacy mapping, constraints, asynchronous bounded
   adapters, and device watching where needed.
5. Add a live audio/video presentation path; stored-media placeholders and a
   serialize/reparse render loop cannot carry a call.
6. Deliver Windows audio/video first because Windows is the only platform with
   camera and microphone providers today. Advance Linux and Android only when their
   native capture, output, permission, lifecycle, and packaging gates pass.
7. Add a real `wptserve`/`testharness.js` lane and cross-browser interop lab. The
   existing screenshot WPT runner is retained for visual tests but is not WebRTC
   conformance evidence.

WebRTC intentionally does not define application signaling. Browser WebSocket
support is a sibling compatibility dependency, not part of the RTC transport.
No production signaling or TURN service is required for browser conformance, and
Broiler Office Server must not silently become one.

## Definition of success

WebRTC is complete only when all of the following are true for a claimed product
profile:

- Sites can negotiate, connect, exchange ordered/unordered data, send and receive
  audio/video, renegotiate, restart ICE, query statistics, and close cleanly with
  shipping browsers through direct and TURN-relayed routes.
- WebIDL shape, prototypes, task ordering, Promise settlement, events, state
  transitions, exception types, constraints, and garbage-collection/lifetime
  behavior pass every applicable mandatory Web Platform Test in the supported
  manifest; no in-scope test is failed, timed out, crashed, skipped, or hidden as an
  expected failure.
- Camera and microphone access occurs only in a trustworthy secure context after
  an origin-scoped user decision; labels and identifiers follow privacy rules;
  active capture is visible and revocable.
- Navigation, document destruction, permission revocation, device removal,
  connection close, and application shutdown leave no live capture, socket,
  native reference, callback, task, thread, indicator, or unbounded queue.
- Native binaries are reproducibly pinned, licensed, scanned, signed, packaged for
  every claimed RID, represented in notices/SBOMs, and covered by a security-update
  policy.
- The support matrix distinguishes Windows, Linux, Android, CLI/headless, and test
  profiles. A profile that cannot provide the service does not advertise it.

## Current-state evidence

### Browser and JavaScript lifetime

`BrowserApp.LoadUrlOnWorkerAsync` creates an `InteractiveSession`, runs a bounded
virtual load window, and disposes the session when that window settles. That is
appropriate for today's mostly static result, but it destroys the JS realm before
late ICE, data-channel, permission, or media events can arrive. Windows and Android
also stop their pumps when no current work is pending, so a native callback needs a
host wakeup path rather than polling.

`BrowserEventLoop` is the right task seam and the Worker binding demonstrates
background-to-page delivery, but the current loop is a virtual synchronous drain.
The JavaScript engine requires a single owner thread while code executes. WebRTC
therefore depends on a durable document session, ordered external task sources,
microtask checkpoints, and owner-thread dispatch before any browser global is
enabled.

### Capture and devices

The repository already has useful neutral capture components:

- `Broiler.Input.Camera` exposes providers, devices, formats, capabilities,
  timestamped frame leases, bounded delivery, statistics, and a latest-frame
  preview adapter.
- `Broiler.Input.Microphone` exposes the equivalent PCM capture lifecycle and
  timestamped buffer leases.
- Windows Media Foundation camera and WASAPI microphone providers enumerate and
  capture real devices.
- `Broiler.Input.Testing` provides fake camera/microphone providers, fake devices,
  an explicit clock, invalidation, format changes, and bounded-delivery checks.

Those components own discovery and raw capture, not browser permissions, Web
constraints, codecs, playback, or networking. Windows is the only implemented
camera/microphone platform. Provider refresh is not yet a continuous OS watcher;
formats are selected narrowly; frames are copied; and delivery calls consumers on
the capture thread. A small bounded asynchronous RTC ingress adapter is mandatory
so a slow encoder never blocks Media Foundation or WASAPI. Prefer native NV12 when
the chosen backend accepts it, add tested I420 conversion when required, and decode
MJPEG before raw-frame ingestion.

Input's opaque IDs are not Web device IDs. Before the document has successfully
captured, `enumerateDevices()` exposes at most one device of each kind and uses empty
`deviceId`, `label`, and `groupId` values as required by the frozen Media Capture
rules. Once device information may be exposed, the browser generates per-origin
salted `deviceId` values and document-scoped/privacy-correct `groupId` values. Raw
Media Foundation symbolic links and WASAPI endpoint IDs must never cross into
JavaScript, diagnostics, or metrics.

### Media presentation

`Broiler.Media` is deliberately decode-first. It does not implement RTP/RTCP,
Opus, VP8, an encoder pipeline, jitter buffering, congestion control, or A/V sync.
`Broiler.Playback` supplies useful stored-media state-machine patterns, but no
physical audio playout device or live `MediaStream` route exists. Current HTML
audio/video behavior is capability-negative and rendering produces replaced-element
placeholders rather than live frames.

The Win32 UI demo proves camera preview and several pixel conversions, but it
allocates and recreates images per frame. The general renderer only creates and
releases static images. WebRTC needs a mutable/double-buffered live-frame surface,
correct rotation/color-space conversion, device-loss handling, and a repaint signal
that does not serialize and reparse the DOM for every frame. Remote audio needs a
real clocked output with bounded buffering and underrun metrics.

### Transport, signaling, and servers

No ICE, STUN, TURN, DTLS, SRTP, SCTP, RTP/RTCP, SDP/JSEP, peer connection, or data
channel implementation or dependency exists. Browser WebSocket is also absent;
the debugger's internal `ClientWebSocket` is not a browser API and must not be
promoted as one without WebSocket semantics, origin policy, backpressure, and
lifecycle work.

Broiler Office Server currently hosts Writer assets and small health/info routes.
It has no authentication, authorization, WebSocket endpoint, signaling model,
TURN credential issuer, readiness check, or RTC metrics. Packaged defaults include
public HTTP and sample reverse-proxy configurations are not WebSocket-ready. It is
not a prerequisite for WebRTC. Any reference signaling endpoint stays test/demo-only
unless a separate product decision adds TLS, authentication, room authorization,
origin validation, schemas, limits, observability, and operations ownership.

TURN remains a separately operated service. The browser consumes page-supplied,
short-lived credentials in `RTCConfiguration`; it never embeds server secrets.

### Test and release infrastructure

The existing Broiler WPT runner is a pixel/reftest runner. Its minimal
`testharness.js` substitutions do not execute asynchronous WebRTC behavior and its
incidental `webrtc` baselines do not prove API conformance. The repository also has
no normal pull-request job that runs the full xUnit graph, and that graph has known
unrelated failures. WebRTC must start with its own green, required, focused job and
ratchet broader coverage separately.

Deterministic Input fakes are strong foundations for PR CI. Hardware, network-lab,
interop, sanitizer/fuzzer, soak, and package-install evidence require separate
lanes because they have different trust, timing, platform, and flakiness properties.

## Scope

### Required first stable surface

The first stable WebRTC profile includes:

- `navigator.mediaDevices`, `getUserMedia()`, `enumerateDevices()`,
  `getSupportedConstraints()`, and `devicechange`;
- `MediaDeviceInfo`, `InputDeviceInfo`, `MediaStream`, and `MediaStreamTrack`,
  including cloning, enable/mute/end, constraints, capabilities, settings, and
  lifecycle events;
- `RTCPeerConnection` configuration, offer/answer, local/remote descriptions,
  trickle ICE, connection/gathering/signaling states, ICE restart, close, and all
  required events;
- Unified Plan senders, receivers, and transceivers, including `addTrack`,
  `removeTrack`, `addTransceiver`, `replaceTrack`, direction changes, and remote
  `track` delivery;
- `RTCDataChannel` strings and binary data, `binaryType`, ordering, negotiated and
  in-band channels, reliability options, message-size validation, `bufferedAmount`,
  low-threshold events, backpressure, errors, and close;
- RTP audio and video with standards-required codec interoperability, RTCP,
  bandwidth estimation, pacing, congestion response, jitter/loss handling,
  keyframes, and A/V synchronization;
- `HTMLMediaElement.srcObject` for local preview and remote streams, with muted,
  volume, visibility, sizing, object-fit, repaint, and teardown behavior;
- required candidate, certificate, session-description, transport, RTP, error, and
  event interfaces and correct WebIDL prototypes;
- `getStats()` and the mandatory-to-implement statistics selected by the frozen
  conformance baseline.

### Deliberately separate or deferred surfaces

The initial stable gate does not include screen capture/`getDisplayMedia`,
`MediaRecorder`, Web Audio, WebCodecs, MediaSource, encoded transforms/insertable
streams, SVC, broad simulcast policy, SFU/MCU products, an identity provider,
DTMF beyond required compatibility, a production signaling service, or a bundled
TURN service. Each requires its own product decision, threat model, WPT manifest,
and release gate. The implementation may support a dependency's internal feature,
but that does not authorize exposing the corresponding Web API.

Legacy prefixed constructors, callback-only APIs, and Plan B are not product goals.
Compatibility aliases may be considered only after the standards surface is
complete and real site evidence justifies them.

### Initial frame and worker boundary

The RTC-3/RTC-5 local-device preview is limited to top-level secure documents.
`RTCPeerConnection` and data-channel exposure follows the frozen WebIDL and WPT
requirements independently; the implementation must not incorrectly hide an API in
a context where the standard exposes it. M4/stable additionally requires the
normative same-origin and top-level-delegated iframe cases from RTC-8, with real
per-frame origins, realm/session isolation, and Permissions Policy enforcement. If
that general frame dependency is not delivered, the result remains a non-conforming
top-level-only preview and cannot be called complete WebRTC. Dedicated Worker
exposure is limited to interfaces the frozen WebIDL/spec actually exposes; capture
prompting and peer-connection ownership remain document-bound unless a later
standards decision says otherwise.

## Standards baseline

Phase RTC-0 freezes exact document and corpus revisions in an ADR and lock file.
As of this roadmap review, the planning inputs are:

- [WebRTC 1.0](https://www.w3.org/TR/webrtc/), W3C Recommendation,
  13 March 2025;
- [Media Capture and Streams](https://www.w3.org/TR/mediacapture-streams/),
  W3C Candidate Recommendation Draft, 9 October 2025;
- [WebRTC Statistics](https://www.w3.org/TR/webrtc-stats/), W3C Candidate
  Recommendation Draft, 25 September 2025;
- [JSEP, RFC 9429](https://www.rfc-editor.org/rfc/rfc9429.html), which supersedes
  RFC 8829;
- ICE [RFC 8445](https://www.rfc-editor.org/rfc/rfc8445.html), STUN
  [RFC 8489](https://www.rfc-editor.org/rfc/rfc8489.html), TURN
  [RFC 8656](https://www.rfc-editor.org/rfc/rfc8656.html), Trickle ICE
  [RFC 8838](https://www.rfc-editor.org/rfc/rfc8838.html), and consent freshness
  [RFC 7675](https://www.rfc-editor.org/rfc/rfc7675.html);
- WebRTC data channels [RFC 8831](https://www.rfc-editor.org/rfc/rfc8831.html),
  setup [RFC 8832](https://www.rfc-editor.org/rfc/rfc8832.html), and SCTP over
  DTLS [RFC 8261](https://www.rfc-editor.org/rfc/rfc8261.html);
- RTP media requirements [RFC 8834](https://www.rfc-editor.org/rfc/rfc8834.html),
  transports [RFC 8835](https://www.rfc-editor.org/rfc/rfc8835.html),
  DTLS-SRTP [RFC 5764](https://www.rfc-editor.org/rfc/rfc5764.html), BUNDLE
  [RFC 8843](https://www.rfc-editor.org/rfc/rfc8843.html), and multiplexing
  [RFC 7983](https://www.rfc-editor.org/rfc/rfc7983.html);
- audio requirements [RFC 7874](https://www.rfc-editor.org/rfc/rfc7874.html) and
  video requirements [RFC 7742](https://www.rfc-editor.org/rfc/rfc7742.html);
- WebRTC security [RFC 8826](https://www.rfc-editor.org/rfc/rfc8826.html),
  security architecture [RFC 8827](https://www.rfc-editor.org/rfc/rfc8827.html),
  and IP-address handling [RFC 8828](https://www.rfc-editor.org/rfc/rfc8828.html);
- [Secure Contexts](https://www.w3.org/TR/secure-contexts/),
  [Permissions](https://www.w3.org/TR/permissions/), and
  [Permissions Policy](https://www.w3.org/TR/permissions-policy/);
- a pinned [Web Platform Tests](https://web-platform-tests.org/) revision and the
  upstream `webrtc`, `webrtc-stats`, `mediacapture-streams`, and applicable HTML
  media manifests.

The stable gate targets the frozen normative requirements and mandatory WPTs,
not every editor's-draft experiment. Candidate amendments are separately listed
and consciously accepted or deferred when the lock is refreshed.

## Product support policy

| Product profile | First honest claim | Required progression |
| --- | --- | --- |
| Browser Windows | Data-channel developer preview, then full A/V preview | Existing capture providers, new permission UI, live video, real audio playout, native backend, WPT/interop/hardware gates |
| Browser Linux | Absent initially; optionally data-only behind an explicit preview flag | Native backend packaging first; then camera/mic providers, audio output, permissions, distro/driver evidence before A/V is advertised |
| Browser Android | Absent initially; optionally data-only only after lifecycle/network validation | Native backend for supported ABIs, `CAMERA`/`RECORD_AUDIO` only with implemented runtime prompts, capture/output adapters, pause/resume/background policy, package tests |
| CLI, WPT, DevConsole, headless | No physical devices or unrestricted RTC by default | Explicit deterministic fake service for tests; opt-in network/native mode in isolated integration runs |
| Browser WebAssembly | Unsupported by this roadmap | Requires a separate host/browser capability decision and transport/media design |

Feature detection is fail-closed. An unavailable backend or disabled policy means
the related globals are absent. A data-only preview may expose `RTCPeerConnection`
and `RTCDataChannel` while `navigator.mediaDevices` remains absent, but the build's
support statement and tests must describe that profile precisely.

## Target ownership and dependency boundaries

### Proposed component family

Create `Broiler.WebRtc` only after RTC-0 approves the native approach. The initial
component layout is:

| Project or area | Owns | Must not own |
| --- | --- | --- |
| `Broiler.WebRtc` | Neutral engine/factory contracts, peer/data/media state and value types, callbacks, limits, diagnostics contract | JavaScript objects, UI, browser permission decisions, platform device APIs |
| `Broiler.WebRtc.Native.<Backend>` | Broiler-owned C ABI shim, P/Invoke, native handles, worker threads, protocol/media backend, RID assets | DOM/WebIDL, prompts, origin storage, application signaling |
| `Broiler.WebRtc.Input` | Bounded adapters from Broiler camera/microphone leases to RTC track sources, format/time conversion, drop/discontinuity metrics | Device permission UI, native identifiers in Web-facing types |
| `Broiler.WebRtc.Testing` | Fake engine, virtual network/route, fake clock/RNG/certificates, scripted callbacks, fault injection | Production fallback or a fake advertised as support |
| `Broiler.WebRtc.Tests` and native tests | Contract/state/ABI/protocol/loopback tests and architecture guards | Hardware-dependent required PR tests |

If the selected backend makes one of these seams meaningless, RTC-0 may merge it,
but it must preserve the neutral contract, native deployment seam, and testing
seam. Create a reusable `Broiler.Media.Live` project only when a second non-RTC
producer needs the same live source/sink API; otherwise keep RTC media adapters in
the RTC component and add only general live-surface primitives to their canonical
graphics/audio owners.

The new component follows repository conventions: README, component roadmap, ADR
index, `HUMAN_REVIEW.md`, license/provenance records, public API baseline, package
metadata, architecture tests, and deterministic testing assembly. Add project roots
to `eng/solutions.json`, generate solutions with `scripts/update-solutions.ps1`, and
verify with `scripts/verify-solution-projects.ps1`; do not hand-edit generated
solution files.

### Dependency direction

Compile-time dependencies point toward neutral contracts:

```text
Windows / Linux / Android application head
  -> Browser.Core
  -> Broiler.WebRtc.Native.<Backend>
  -> Broiler.WebRtc.Input

Browser.Core -> HtmlBridge.Dom / HtmlBridge.Scripting
HtmlBridge.Dom -> Broiler.WebRtc
Broiler.WebRtc.Native.<Backend> -> Broiler.WebRtc
Broiler.WebRtc.Input -> Broiler.WebRtc
Broiler.WebRtc.Input -> Broiler.Input.Camera / Broiler.Input.Microphone

Browser live-media adapter -> Broiler.WebRtc + Broiler.Graphics/HTML/audio output
```

The separate runtime data flow is capture -> bounded RTC input adapter -> local
native track, and remote native frames/PCM -> browser live-media adapter -> mutable
graphics surface/platform audio output. These data-flow arrows do not reverse the
compile-time dependencies above.

Canonical DOM, HTML, Layout, Graphics, Input, and Media assemblies must not
reference HtmlBridge, the JavaScript runtime, or native backend types. HtmlBridge
references only neutral RTC contracts. Platform application heads select and
construct native services; no feature binding calls `new` on a Windows, Linux,
Android, or vendor-specific implementation.

### Existing components to extend

| Owner | Extension |
| --- | --- |
| HtmlBridge Dom/Scripting | `WebRtcBinding` feature area; reusable non-node `EventTarget`; WebIDL/prototype objects; Promise/event/exception translation; runtime-service injection; session disposal registration |
| Browser Core | `BrowserRuntimeServices`; durable `BrowserDocumentSession`; permission broker; secure-context/origin policy; active-capture chrome; resource budgets; wakeup and shutdown |
| Broiler.Input | Continuous device watching, capability accuracy, efficient/pool-friendly leases where proven, platform providers; retain discovery/capture-only boundary |
| Broiler.Graphics/HTML/Layout | Mutable live-frame surface or render resource; device-loss safe update; `<video>`/`srcObject` replaced-element integration without DOM reparse per frame |
| Broiler.Media/Playback or platform output owner | Real clocked PCM output and controls, bounded buffers, underrun/latency metrics; retain stored-media and RTC boundaries |
| Browser platform heads | Backend/RID wiring, capture/output providers, permission prompts, OS capability declarations, lifecycle behavior |
| Test infrastructure | Real `wptserve`/testharness mode, fake services, network lab, interop drivers, package and soak lanes |

## Runtime, threading, and lifetime model

WebRTC cannot be an RTC-specific background poll. It must use the browser's general
document task model:

1. A `BrowserDocumentSession` owns the JS realm, `DomBridge`, ordered event loop,
   cancellation token, host services, resource registry, and render-facing live
   resources from navigation commit until replacement or window close.
2. Page load completion and document destruction are separate states. A settled
   load stops unnecessary frame work but does not dispose the session.
3. Timers, networking, permission results, media device changes, RTC state/events,
   data messages, and frame invalidations enqueue typed tasks. Enqueueing transitions
   an idle session to runnable and signals the platform host.
4. Exactly one owner thread executes a document's JavaScript. Native callbacks copy
   or retain only neutral immutable/leased data, enqueue work, and return; they never
   call JS, DOM, layout, or renderer APIs directly.
5. Each task runs to completion, applies the spec-defined state snapshot, fires the
   event or settles the Promise in the required order, performs a microtask
   checkpoint, then publishes a render invalidation if needed.
6. Resource/activity leases keep required native work alive but do not keep a
   navigated-away realm alive. Navigation first prevents new callbacks, then cancels
   tasks, closes RTC resources/tracks, stops devices/indicators, drains native
   releases, and finally destroys JS state.
7. Callback envelopes carry document generation and object identity. Stale
   generation, closed object, canceled session, or disposed native handle means
   discard without invoking JS.

The implementation extends the HtmlBridge Phase 6 ordered-event-loop and injected
host-service work; it does not create a second RTC-only scheduler. Tests must prove
late wakeup after a settled page, cross-session isolation, run-to-completion
snapshots, callback ordering, reentrant close, navigation during negotiation, and
zero callbacks after disposal.

## Security and privacy architecture

WebRTC adds untrusted parsers, native code, UDP/TCP sockets, local-network reach,
device capture, persistent identifiers, and user-observable indicators to a browser
that currently warns JavaScript is not sandboxed. Until the security release gate
passes, RTC builds are controlled-content previews. A native RTC broker contains the
new protocol/media risk but does not erase the browser's broader content-security
warning; a general-Web stable claim also depends on an approved whole-browser
untrusted-content isolation/hardening baseline. Without both, the support profile
remains controlled/trusted content.

### Mandatory browser policy

- Build one canonical origin and potentially-trustworthy secure-context service.
  Permit capture only for trustworthy contexts, with a separately tested loopback
  development exception. Do not infer trust merely from a URL string.
- Add an origin-keyed permission broker. The first honest implementation is
  session-only because Broiler has no coherent persistent browser profile. Prompt
  only from an eligible foreground document and coalesce concurrent equivalent
  requests.
- Before successful capture permits device-information exposure, return at most one
  device of each kind and keep `deviceId`, `label`, and `groupId` empty. After
  exposure is allowed, make device IDs origin-scoped and salted and group IDs scoped
  exactly as the frozen Media Capture rules require. Rotate identifiers with the
  defined profile/session/storage boundary and never reveal backend IDs.
- Enforce Permissions Policy for `camera` and `microphone`; block cross-origin frame
  capture until frame-origin and delegation enforcement exist.
- Show an unambiguous camera/microphone-in-use indicator with per-origin stop/revoke
  controls. OS denial, app denial, busy devices, removal, unsupported constraints,
  and capture faults map to the correct DOMException without exposing sensitive
  details.
- Stop capture on track stop, last-source release when required, navigation,
  permission revocation, app shutdown, and applicable mobile lifecycle changes.
- Data-only peer connections do not require or trigger capture permission. Apply
  the frozen standard's exposure rules plus origin, network/IP privacy, and
  per-origin resource policy without inventing a media-device prompt.

### Network and protocol controls

- Require DTLS-SRTP, fingerprint validation, OS CSPRNG, safe certificate generation,
  SRTP replay protection, RTCP mux/BUNDLE rules, consent freshness, and teardown on
  failed consent.
- Validate and cap ICE server count, URL and credential lengths, SDP size/lines,
  candidates, transceivers, encodings, codecs, data channels, message size, queued
  bytes, bitrate, pending operations, timers, sockets, and connections per origin.
- Implement an explicit IP-address policy: host-candidate suppression or mDNS where
  support is correct, relay-only mode, route/interface filtering, bad-port blocking,
  and local-network protections. Do not claim privacy from merely redacting logs.
- Parse every remote SDP/candidate/packet as hostile. Fuzz SDP, STUN, TURN, DTLS,
  SCTP, RTP, and RTCP boundaries and run native sanitizers in isolated CI.
- Never log media, keys, TURN credentials, full SDP, candidate addresses, raw device
  IDs, or user/room/peer identifiers. Diagnostics use bounded state summaries and
  candidate type/protocol only.

### Native dependency and supply chain

RTC-0 must record the exact upstream revision, patch set, build container/toolchain,
enabled codecs/features, supported architectures, license and patent assessment,
binary provenance/checksums, update cadence, CVE response owner/SLA, SBOM contents,
and rollback/disable mechanism. Add native vulnerability and artifact scanning,
signing, notices, source-offer obligations where applicable, and review coverage in
`scripts/check-publish-approval.ps1`.

An out-of-process native broker is a required decision in the threat model. If
initial delivery remains in-process, document why, restrict the preview audience,
and retain an ABI that permits later isolation without changing WebIDL.

## Native backend decision gate

Building ICE, STUN, TURN, DTLS, SRTP, SCTP, RTP/RTCP, codecs, jitter buffers,
bandwidth estimation, congestion control, pacing, AEC, and device adaptation from
scratch is explicitly out of scope. The native dependency is nevertheless too
consequential to select by popularity alone.

### Candidates to spike

| Approach | Strengths | Known costs or gaps | Planning position |
| --- | --- | --- | --- |
| [Google libwebrtc](https://webrtc.googlesource.com/src/) behind a Broiler C ABI | Intended for browser/native WebRTC; mature end-to-end transport, codecs, audio processing, congestion control, platform support, and test hooks | Very large source/build graph; unstable C++ API; complex GN/Ninja packaging; frequent security updates; binary size and codec/patent review | **Preferred hypothesis for full browser A/V**, subject to every RTC-0 gate |
| [libdatachannel](https://libdatachannel.org/) behind its C API or a Broiler shim | Smaller C++17 implementation; clear data-channel/transport focus; Windows/Linux/Android; simpler embedding | Does not provide the complete browser capture, codec, AEC, rendering, and media-processing system; Broiler would own substantially more RTP media behavior and conformance risk | Strong data-only option; full A/V only if spike quantifies and accepts the missing ownership |
| New managed/native protocol stack | Maximum control | Multi-year security and interoperability burden across every hostile parser and real-time algorithm | **No-go** for this roadmap |

Other candidates may enter only with a written license, maintenance, standards,
and support analysis. A library's presence in NuGet or a claim of WebRTC support is
not evidence that it supplies a browser-grade API or safe native update path.

### Decision scorecard

RTC-0 weights and records, rather than informally debating, these criteria:

- required WebRTC 1.0/JSEP behavior and Unified Plan semantics;
- data channels, ICE restart, trickle ICE, BUNDLE, TURN UDP/TCP/TLS, IPv4/IPv6,
  consent freshness, stats, and controllable IP privacy;
- required audio/video codecs and packetization, AEC/NS/AGC, resampling, jitter
  handling, A/V sync, bandwidth estimation, congestion control, and test hooks;
- Windows x64/arm64, Linux x64/arm64, and Android ABI feasibility;
- upstream maintenance, security advisories, release cadence, CVE response, and
  ability to carry/retire a minimal patch set;
- deterministic source build, offline cache, reproducible artifacts, compiler and
  CRT compatibility, debug symbols, sanitizer/fuzzer support, and RID packaging;
- stable C ABI feasibility, callback/thread rules, ownership, cancellation,
  teardown, crash containment, and out-of-process migration;
- license compatibility, notices/source obligations, codec patent review, binary
  distribution rights, SBOM completeness, size, startup, CPU, and memory;
- direct and TURN-relayed interop with current Chromium and Firefox for data,
  audio, video, renegotiation, stats, and close.

The decision ADR contains raw spike commands/results, the selected upstream commit,
rejected alternatives, residual risks, initial support matrix, and a replacement
strategy. Failure to find a backend that meets the security, licensing, packaging,
and interop gates blocks WebRTC; it does not lower those gates.

## Delivery sequence

The phases below are outcome gates. A phase can overlap another only where its
dependency is represented by tested neutral contracts or fakes; no later feature
may be advertised before every earlier gate on its critical path passes.

| Phase | Outcome | Depends on | Parallel work |
| --- | --- | --- | --- |
| RTC-0 | Standards/profile freeze, threat model, backend ADR and estimate | None | WPT harness prototype, live-surface prototype |
| RTC-1 | Persistent document session and external task model | Existing HtmlBridge Phase 6 direction | Permission UI design, fake RTC API |
| RTC-2 | Governed RTC component, C ABI, packaging, deterministic engine tests | RTC-0 | RTC-1, WPT infrastructure |
| RTC-3 | MediaStream/MediaDevices model over fakes and permission policy | RTC-1; RTC-0 API lock | RTC-2 data transport, graphics live surface |
| RTC-4 | Standards-shaped peer/data API, reliable data channels, then the minimum Unified Plan media-negotiation sub-gate | RTC-1, RTC-2 | Windows capture/output adapters |
| RTC-5 | Secure Windows device capture and local `srcObject` preview | RTC-1, RTC-3, live video surface | RTC-4 hardening |
| RTC-6 | Interoperable send/receive audio | RTC-2–RTC-5, RTC-4 media sub-gate, real audio output | Video pipeline |
| RTC-7 | Interoperable send/receive video | RTC-2–RTC-5, RTC-4 media sub-gate, live video surface | Audio hardening |
| RTC-8 | Exhaustive negotiation/transceiver/stats semantics | RTC-4 media sub-gate, RTC-6, RTC-7 | Network/security lab |
| RTC-9 | Network/privacy/security hardening | RTC-0–RTC-8 | Platform ports |
| RTC-10 | Linux and Android parity for claimed profiles | RTC-2, RTC-8, RTC-9 | Release/package automation |
| RTC-11 | Conformance, interop, performance and stable enablement | All claimed-profile phases | Documentation and operations handoff |

The first four user-visible milestones are:

- **M1 — data-channel developer preview:** RTC-0, RTC-1, RTC-2, and the
  data-channel subset of RTC-4 pass behind an explicit preview flag.
- **M2 — Windows local-media preview:** RTC-3 and RTC-5 pass; capture and local
  preview are real, but A/V call support is not yet claimed.
- **M3 — Windows A/V interoperability preview:** RTC-6 through RTC-9 pass for the
  preview manifest and controlled-content security statement.
- **M4 — stable supported profile:** RTC-11 passes, then WebRTC is enabled by
  default only for the exact platform/profile recorded in the release matrix.

Calendar estimates are deliberately deferred until RTC-0 measures the backend build,
C ABI, WPT, interop, binary-size, and live-media prototypes. The ADR must produce
staffed ranges, critical-path dependencies, and confidence bounds; an unmeasured
date is not an exit gate.

## RTC-0 — Freeze the product, standards, backend, and threat model

**Owner:** Browser architecture with WebRtc, security, legal/release, platform,
and test-infrastructure owners.

**Current evidence:** WebRTC is absent; no protocol dependency has been approved;
Broiler's JavaScript runs without a security sandbox; Windows is the only capture
platform; no real asynchronous WPT lane or live media renderer exists.

**Next actions:**

1. Freeze the first data-only, Windows A/V preview, and stable API manifests. For
   every WebIDL member, identify its normative source, exposure context, backend
   capability, owner, test path, and defer/implement decision.
2. Freeze exact W3C, RFC, Chromium, Firefox, WPT, compiler, SDK, and target-platform
   revisions in machine-readable locks. Separate Recommendation requirements from
   accepted candidate amendments.
3. Write the WebRTC threat model: assets, origins, permission actors, device and
   IP disclosures, attacker-controlled parse inputs, native trust boundary, local
   network reach, logging, crash impact, and update/disable response.
4. Build minimal libwebrtc and libdatachannel spikes behind the same tiny C ABI:
   create/destroy factory and peer, callbacks, offer/answer, trickle candidates,
   data message/backpressure, stats snapshot, cancellation, and error reporting.
5. Extend the preferred spike through local and TURN UDP/TCP/TLS routes, IPv4 and
   IPv6 where available, Chromium and Firefox interop, audio/video loopback, ICE
   restart, renegotiation, and repeated teardown. Record every required patch.
6. Prototype deterministic native builds and NuGet/RID assets on Windows and Linux;
   assess Android ABIs. Measure build time, artifact size, cold load, baseline
   memory/threads, symbol size, and update mechanics.
7. Prototype one remote video frame reaching a mutable render surface and one remote
   PCM stream reaching real output without JS-thread blocking. Record format,
   ownership, clock, copy, and device-loss behavior.
8. Prototype real `wptserve`/HTTPS/testharness execution and results capture for a
   small `webrtc`/`mediacapture-streams` subset.
9. Decide in-process versus brokered native execution; record a staged isolation
   path if preview delivery is in-process.
10. Land ADRs for component topology, backend selection, security boundary,
    codec/patent policy, support profile, and upstream update policy. Publish a
    staffed range and stop/go decision.

**Exit gate:** every lock and ADR is reviewed; one backend passes the shared ABI,
data, A/V, TURN, interop, teardown, build, license, and packaging spikes; the threat
model has owners and mitigations; WPT and live-output feasibility are demonstrated;
and a measured plan shows no unresolved blocker to M1 or M3. Otherwise the feature
remains absent and the blocked criterion is recorded.

## RTC-1 — Keep document realms alive and deliver external tasks correctly

**Owner:** HtmlBridge Dom/Scripting and Browser host composition.

**Current evidence:** a settled bounded load window can dispose
`InteractiveSession`; `BrowserEventLoop` has no host wakeup/activity contract; and
the animation pump may stop before a late callback. `DomBridgeDisposalRegistry` and
Worker delivery provide partial seams, while the HtmlBridge root roadmap already
requires one ordered event loop and injected host services.

**Next actions:**

1. Introduce `BrowserDocumentSession` (name subject to owner ADR) as the owner of
   the JS context, DomBridge, document event loop, generation, runtime services,
   cancellation, diagnostics, live render resources, and disposal registry.
2. Separate load settlement from document lifetime. Retain an interactive session
   until navigation replacement or application close without keeping the UI in a
   continuous animation loop.
3. Define document task sources for DOM manipulation, timers, networking, permission,
   media devices, RTC, and rendering. Preserve FIFO/order rules within a source and
   explicitly define cross-source selection and microtask checkpoints.
4. Add thread-safe `Post`, monotonic due-time scheduling, an idle-to-runnable wakeup
   event, activity/resource leases, queue limits, cancellation, and diagnostics.
5. Make each platform head wake and pump the correct document generation on its JS
   owner thread. Do not execute JS on a native/media/network worker or rely on
   frame-rate polling.
6. Extend `DomBridgeSessionOptions`/`ScriptEngine` composition with neutral runtime
   services while preserving the public v2 contract through additive APIs/facades
   and updating public API baselines intentionally.
7. Extract reusable non-node `EventTarget` machinery from Messaging rather than
   coupling RTC types to MessagePort internals.
8. Specify the shutdown transaction: close admission, invalidate generation,
   cancel pending operations, stop sources, close peers/channels, deterministically
   unregister and fence callbacks, release native handles, remove indicators,
   release render resources, and destroy the realm within a bound. Finalizers are a
   leak-safety backstop only; shutdown neither waits for nor depends on them.
9. Add deterministic tests for a task arriving after load settlement, ordering and
   Promise checkpoints, concurrent enqueue, reentrancy, navigation during callback,
   stale generations, two isolated sessions, and close/dispose idempotence.

**Exit gate:** a page remains interactive while idle without busy-polling; a late
fake network/media callback wakes it and runs once on the owner thread in the
specified task/microtask order; navigation and shutdown suppress all stale work and
release every registered resource; existing Browser, CLI, WPT, and DevConsole
profiles retain their intended settlement behavior.

## RTC-2 — Establish the neutral RTC component and native execution seam

**Owner:** Broiler.WebRtc and native backend owners, with build/release and security.

**Current evidence:** no RTC component exists. Input and Media architecture tests
ban unapproved third-party package dependencies, so placing a native engine inside
either would violate their current boundaries. Native assets are not represented in
solution manifests, notices, publish approval, or package smoke tests.

**Next actions:**

1. Scaffold the approved component family, README, roadmap, ADRs, API baseline,
   architecture guards, testing helpers, packaging metadata, notices, licenses,
   human review, and solution-manifest entries.
2. Define neutral asynchronous contracts for factory configuration, peer connection,
   descriptions/candidates, data channels, local sources, remote sinks, stats,
   diagnostics, callbacks, cancellation, limits, and terminal disposal. Keep WebIDL
   names and JS identity out of the engine API.
3. Define a versioned, size-tagged C ABI using opaque handles, explicit ownership,
   status/error codes, buffer length negotiation, callback registration tokens, and
   no C++ exceptions or allocator ownership across the boundary.
4. Pin and build the selected backend from reviewed source. Record flags and disabled
   features, create debug/sanitizer and release artifacts, retain symbols, and verify
   deterministic inputs/checksums.
5. Make native callbacks shallow and non-blocking. Copy or lease bounded payloads,
   attach object/session generation, enqueue through the neutral callback sink, and
   make unregister/destroy wait or fence against callback races.
6. Add injected clock/RNG/certificate/network hooks where the backend permits them;
   otherwise build a deterministic fake engine with the same contract and isolate
   nondeterministic native tests.
7. Add ABI version/load checks, wrong-architecture and missing-asset errors, double
   close, cancellation, callback-after-close, repeated factory creation, and failure
   injection tests.
8. Package every intended RID without leaking Windows/native references into Linux,
   Android, portable, or WebAssembly solution closures. Test framework-dependent and
   self-contained publishing, trimming/ReadyToRun decisions, symbols, signing, SBOM,
   and clean-feed consumption.
9. Add bounded, off-by-default diagnostic events with stable IDs and correlation
   tokens. Never emit packet payloads or sensitive configuration.

**Exit gate:** a clean supported host loads the reviewed native asset through the
versioned ABI, creates two neutral peers, completes direct and relayed loopback,
exchanges data, obtains a redacted stats snapshot, and tears down with zero callbacks
or native handles left. Component/solution architecture guards, package install,
license, SBOM, sanitizer, and security-review gates pass.

## RTC-3 — Implement MediaStream, media devices, constraints, and permission semantics

**Owner:** HtmlBridge Web API binding and Browser permission policy, with
Broiler.Input and Broiler.WebRtc.Testing.

**Current evidence:** `navigator.mediaDevices`, `getUserMedia`, `MediaStream`, and
`MediaStreamTrack` are absent. Input supplies reusable capture contracts/fakes but
not browser permissions, Web constraints, origin-private IDs, continuous device
events, or track semantics. No canonical secure-context/permission broker exists.

**Next actions:**

1. Build the neutral origin/trustworthy-context and session-only permission broker,
   including top-level eligibility, foreground/user-choice policy, prompt coalescing,
   deny/dismiss/revoke, OS denial mapping, application shutdown, and test control.
2. Implement the exact device-information exposure algorithm: before successful
   capture, list at most one device of each kind with empty `deviceId`, `label`, and
   `groupId`; after exposure is permitted, use per-origin/session salted device IDs,
   privacy-correct group IDs, and allowed labels. Map stable private IDs back to
   Input devices only inside the broker; never expose native identifiers. In the
   session-only profile, keep a same-origin device ID stable only for the defined
   browser session and rotate it when that session ends. If persistent permission/
   profile storage is later added, persist only under the frozen rules, never when
   that origin's storage is blocked, rotate when its storage is cleared, and keep
   `groupId` unique to the document as required.
3. Implement a spec-derived constraints solver with mandatory/basic and advanced
   selection, fitness distance, default-device policy, capabilities/settings, and
   deterministic `OverconstrainedError.constraint`. Keep selection separate from
   provider-specific open formats.
4. Extend Input providers with accurate capability enumeration and device watching
   as owner-local changes. Coalesce and privacy-throttle `devicechange`; do not let
   polling keep an otherwise idle application busy.
5. Implement WebIDL/prototypes and object registries for `MediaDevices`, device info,
   `MediaStream`, and `MediaStreamTrack`. Cover `id`, `kind`, `label`, `enabled`,
   `muted`, `readyState`, clone, stop, add/remove tracks, constraints, capabilities,
   settings, and events.
6. Model shared physical sources explicitly: cloned tracks have distinct JS identity
   and enabled state while respecting source mute/end and last-consumer capture
   release. Register every source/track with document disposal.
7. Implement correct Promise/event sequencing and map permission, no-device, busy,
   removed, unsupported, overconstrained, capture, cancellation, and insecure-context
   failures to the required DOMException.
8. Wire only deterministic fake devices initially. Add fake permission grant/deny/
   revoke, device add/remove/default change, format change, paced frames/audio,
   discontinuity/fault, and slow-consumer scenarios.
9. Add WebIDL, WPT, state, privacy, cross-origin/session isolation, constraint,
   clone, teardown, and callback-thread tests. Ensure CLI/headless defaults deny or
   omit physical services.

**Exit gate:** focused media-capture tests pass over fakes; prompts and exception
types are correct; pre-capture enumeration is limited to one device per kind with
empty identifiers/labels/groups and post-capture exposure obeys origin/document
isolation rules; track clone/mute/stop/end/source-sharing state is deterministic;
device changes and late callbacks run through the document task source;
denial/navigation/revoke releases all sources. No production profile advertises
capture yet.

## RTC-4 — Deliver peer connection and data channels end to end

**Owner:** Broiler.WebRtc transport/backend and HtmlBridge WebRTC binding.

**Current evidence:** no peer/data API, JSEP state machine, Web signaling transport,
or protocol stack exists. `fetch` can exchange test/demo descriptions, but WebSocket
is an independent browser gap. Existing shape-only probes intentionally fail rather
than imply support.

**Next actions:**

1. Implement the WebIDL surface for peer configuration, session descriptions,
   candidates, certificates, ICE/DTLS/SCTP transports, errors, peer events, and data
   channels over neutral engine handles.
2. Implement the JSEP signaling-state and operations chain: offer/answer,
   current/pending descriptions, implicit/explicit rollback where required, candidate
   validation, trickle/end-of-candidates, configuration changes, close, and Promise
   ordering. Do not delegate Web-visible state solely to native callback timing.
3. Implement candidate gathering and connection state aggregation, ICE server URL/
   credential parsing, BUNDLE/RTCP-mux policy, ICE restart, and generation filtering.
4. Implement `createDataChannel` validation, in-band/negotiated channels, ID and
   reliability rules, ordering, strings/ArrayBuffer/Blob behavior required by the
   baseline, `binaryType`, maximum message size, buffered amount, low-threshold
   events, backpressure, error, and close.
5. Make send fail or apply backpressure exactly as specified; cap native and managed
   queues. Deliver inbound messages as document tasks and preserve data-channel
   order independently of unrelated task sources.
6. Add an in-process exchange fixture and a same-origin HTTPS fetch-based signaling
   demo for development, so the first slice does not depend on Broiler's unfinished
   cross-origin credential/CORS behavior. Keep signaling messages, rooms, users,
   auth, retries, and presence outside the RTC component.
7. Start the separate browser WebSocket roadmap needed by real sites: standards
   handshake, origin/TLS/mixed-content policy, cookies/auth once canonical,
   extensions, binary data, limits, close, backpressure, and proxy tests. WebRTC M1
   itself must not wait for a production signaling product.
8. Test null/invalid configuration, all state transitions, glare/perfect negotiation,
   rollback, ICE restart, candidate races, close during operations, remote close,
   ordered/unordered and partial reliability, large/zero binary payloads, queue
   saturation, network failure, navigation, and repeated setup/teardown.
9. Interoperate through direct and TURN-relayed paths with pinned Chromium and
   Firefox. Capture redacted state/stat results, not full SDP or addresses.
10. After the M1 data slice and before RTC-6/RTC-7, pass a minimum Unified Plan
    media-negotiation sub-gate: `addTrack`/`addTransceiver`, media offer/answer
    sections, sender/receiver/transceiver identity, MID and direction/current-
    direction, remote track delivery, one-audio/one-video codec parameter route, and
    baseline `replaceTrack`. Test renegotiation and close for this subset; RTC-8
    remains responsible for exhaustive algorithms, races, validation, and stats.

**Exit gate:** two Broiler peers and Broiler-to-Chromium/Firefox can negotiate via
out-of-band signaling, gather valid candidates, connect directly and through TURN,
exchange each supported data-channel mode with correct ordering/backpressure, restart
ICE, and close without leaks. The focused data-channel/JSEP/WPT manifest has zero
failures, timeouts, crashes, or skips among its applicable mandatory tests;
resource-limit negatives pass, and M1 remains explicitly preview-only.

**RTC-6/RTC-7 entry gate:** the RTC-4 minimum Unified Plan media-negotiation subset
passes two-Broiler and reference-browser offer/answer/renegotiation tests for one fake
audio and one fake video source, preserves sender/receiver/transceiver identity and
directions, delivers remote track events on the document task source, and closes
cleanly. Media implementation must not be written against semantics that only arrive
later in RTC-8.

## RTC-5 — Integrate secure Windows capture and live local preview

**Owner:** Browser Windows/Core, Broiler.WebRtc.Input, Broiler.Input, HtmlBridge,
Broiler.Graphics/HTML, and UI.

**Current evidence:** Windows camera and microphone providers exist and the Win32 UI
demo proves basic camera preview, but Browser Windows references neither provider.
Capture callbacks are synchronous on provider threads, native device identifiers are
available in private descriptors, and no Web permission UI, activity indicator,
`srcObject`, live HTML surface, or continuous device watcher exists.

**Next actions:**

1. Compose Windows camera/microphone providers and the RTC backend in the Windows
   application head through `BrowserRuntimeServices`. Keep Browser Core and
   HtmlBridge free of Windows types.
2. Implement a Browser permission prompt using neutral dialog infrastructure with
   requesting origin, requested devices, Allow, Block, dismiss, and application
   shutdown behavior. Prevent background or stale documents from completing a prompt.
3. Add browser-owned capture indicators and a stop/revoke control. Couple indicator
   lifetime to actual source activity, not merely Promise resolution or track object
   reachability. Prompts, indicators, and controls require keyboard/focus behavior,
   screen-reader announcements, high-contrast visibility, spoof-resistant origin
   display, and UI-automation coverage.
4. Build bounded asynchronous camera and microphone adapters. Video may use a
   latest-frame policy with counted drops; audio uses a small loss-sensitive FIFO,
   surfaces discontinuities, and never blocks the WASAPI capture thread. Define
   ownership at every native/managed boundary.
5. Negotiate the least-conversion camera format: prefer compatible NV12, handle
   BGRA/RGBA/RGB24/Gray8/YUY2 with tested color conversion, decode MJPEG before raw
   ingress, and add I420 conversion only where the backend requires it. Honor stride,
   planes, rotation, range, primaries/transfer/matrix metadata, timestamps, and
   format changes.
6. Define microphone conversion from the WASAPI shared-mode mix to the backend's
   clock/rate/channel/sample format. At this phase the track may be captured and
   inspected but is not claimed as interoperable send audio until RTC-6.
7. Add a general mutable/double-buffered live image surface in the renderer with
   reusable buffers or native textures, atomic publish, repaint invalidation,
   device-loss rebuild, explicit release, and bounded memory. Do not allocate a new
   renderer image and reparse the document on every frame.
8. Implement the live-stream subset of `HTMLMediaElement`: `srcObject` assignment and
   replacement, `play()` Promise, pause/autoplay/muted behavior, ready state,
   intrinsic video dimensions, and the metadata/can-play/playing/waiting/resize/ended
   events required by the frozen HTML tests. Keep stored URL media outside this
   slice unless its owning roadmap delivers it independently.
9. Connect `HTMLVideoElement.srcObject` to a `MediaStream`, select its video track,
   publish frames to the live surface, and implement local muted preview. Preserve
   normal layout, clipping, opacity, transforms, object-fit/object-position, page
   visibility, and resize semantics.
10. Implement device refresh/change, default-device changes, unplug, busy, format
   change, capture fault, permission revoke, track stop, navigation, and application
   close. Make source and indicator release observable in tests.
11. Run deterministic fake tests in PR CI and an opt-in Windows hardware matrix for
    built-in/USB camera and microphone, deny/revoke, busy/remove, slow consumer,
    start/stop loops, device loss, and handle/thread/memory leaks.

**Exit gate:** an HTTPS top-level test page can request real Windows camera and
microphone after a correct prompt, display a correctly oriented/color-managed local
video through `srcObject`, observe accurate settings/state/device changes, and stop
or revoke immediately. Prompt, indicator, and revoke UI pass keyboard/focus,
nonvisual announcement, high-contrast, spoof-resistant origin, and UI-automation
checks. Capture threads never block on RTC/render work, queues remain bounded,
labels/IDs remain private, indicators match physical capture, navigation and repeated
cycles release all resources, and M2 is documented as local-media preview rather
than A/V call support.

## RTC-6 — Send, receive, process, and play interoperable audio

**Owner:** Broiler.WebRtc native/media owners, Windows capture/output, HtmlBridge
media binding, and performance/test infrastructure.

**Current evidence:** Input captures PCM on Windows, but only at the current WASAPI
shared-mode mix. There is no Opus or G.711 RTP pipeline, AEC/noise suppression/AGC,
resampling/channel mixing, jitter-buffer playout contract, physical audio output,
or WebRTC A/V clock in the browser. `BufferedAudioOutput` is an in-memory test sink,
not a speaker implementation.

**Next actions:**

1. Enable and verify the audio codecs and RTP parameters required by the frozen
   profile, including Opus and the required G.711 PCMA/PCMU interoperability. Expose
   only codecs actually compiled, licensed, packetized, negotiated, and tested.
2. Define the capture clock conversion and resampling/channel-mix pipeline into the
   backend's expected audio blocks, preserving discontinuity and monotonic timing.
   Bound added latency and reset cleanly on format/device changes.
3. Integrate the selected backend's audio processing module or an explicitly reviewed
   equivalent for echo cancellation, noise suppression, automatic gain control, and
   high-pass/level behavior. Feed the reverse/playout stream required for AEC; do
   not expose constraint settings that are ignored.
4. Add a real clocked Windows audio renderer, either through the approved backend
   audio-device module or a canonical WASAPI output adapter. Specify device choice,
   buffer target, underrun/recovery, volume/mute, default-device change, exclusive
   conflicts, and device removal.
5. Connect remote audio tracks to `HTMLMediaElement.srcObject`, respecting media
   element muted/volume/play state and the browser's autoplay decision. Local preview
   remains muted by default to prevent feedback.
6. Implement inbound jitter buffering, packet-loss concealment, NACK/RTCP feedback
   where required, playout timing, drift correction, source mute/unmute/end, and
   audio-level/stat reporting through the backend contract.
7. Validate sender `replaceTrack`, track `enabled`, source mute/end, transceiver
   directions, renegotiation, output changes, and close without clicks, stale audio,
   or capture/output resources remaining.
8. Add deterministic PCM fixtures and comparison tolerances for resampling, channel
   mapping, timestamp drift, discontinuity, silence, packet loss/reorder/jitter,
   concealment, queue overflow, AEC routing, and A/V clock handoff.
9. Run Broiler-to-Broiler and Broiler-to-Chromium/Firefox interop for every advertised
   codec and direction, direct/relay routes, mute/unmute, device switch, loss, ICE
   restart, renegotiation, and long calls.
10. Measure capture-to-network and network-to-playout latency, underruns, concealment,
    jitter, RTT, loss, bitrate, CPU, allocations, threads, memory, and teardown.
    Record control baselines and approved tolerances rather than inventing budgets.

**Exit gate:** a Windows Broiler endpoint exchanges intelligible, synchronized,
duplex audio with pinned Chromium and Firefox through direct and TURN routes using
every advertised required codec; AEC/processing constraints report honest settings;
mute, device changes, loss recovery, renegotiation, and close behave correctly; the
focused WPT/interop/audio quality and resource gates pass without unbounded buffers,
capture-thread stalls, persistent playback, or leaked handles.

## RTC-7 — Send, receive, and render interoperable video

**Owner:** Broiler.WebRtc native/media owners, Broiler.WebRtc.Input,
Broiler.Graphics/HTML/Layout, Browser platform composition, and performance tests.

**Current evidence:** Windows camera capture and a sample preview converter exist,
but there is no WebRTC video encoder/decoder, RTP feedback/adaptation, remote-frame
sink, reusable live renderer resource, or HTML media integration. Current sample
conversion ignores some rotation/color-space metadata and recreates images per frame.

**Next actions:**

1. Enable and verify the video codecs/profiles/packetization required by the frozen
   RFC 7742/WebRTC profile, including VP8 and required H.264 constrained-baseline
   interoperability. Record software/hardware acceleration, fallback, license/patent,
   and platform differences; do not advertise a codec by name alone.
2. Feed camera frames through the bounded ingress path with correct crop/scale,
   rotation, color range/space, timestamp, stride, pixel format, and ownership.
   Avoid RGB round trips when the backend and renderer can share NV12/I420/native
   textures safely.
3. Implement sender adaptation for negotiated resolution/framerate, bitrate changes,
   keyframe requests, encoder overload, visibility, network congestion, and source
   format changes. Keep constraint settings and `getParameters()` truthful.
4. Implement receive depacketization/decoding, reordering/jitter, NACK/PLI/FIR and
   retransmission behavior required by the selected profile, corruption/drop
   handling, decoder reset, frame timing, and synchronization to remote audio.
5. Publish remote frames through the general live surface with bounded buffering,
   latest-frame selection, render invalidation, correct intrinsic dimensions,
   rotation/color, object-fit/object-position, transforms/clipping/opacity, page
   visibility, resize, device loss, and final-frame/ended behavior.
6. Avoid holding backend decoder buffers across uncontrolled UI delays. Define
   zero-copy/native-texture fast paths and a bounded copy fallback with equivalent
   lifetime semantics.
7. Connect local and remote `MediaStream`/track changes to `srcObject`, including
   add/remove track, enabled/muted/ended, stream replacement, play/pause policy, and
   element/document disposal.
8. Validate replaceTrack across compatible sources, direction changes, renegotiation,
   codec preference/capability APIs included in the baseline, ICE restart, and
   sender/receiver/transceiver identity.
9. Add deterministic synthetic moving/color/rotation/timestamp fixtures and compare
   decoded frame order, dimensions, colors, freezes/drops, A/V skew, queue bounds,
   and render resource lifetime under loss/reorder/jitter and device loss.
10. Run interop at representative resolution/framerate/network profiles, long-call
    soak, repeated attach/detach/navigation, software fallback, and optional hardware
    acceleration. Measure CPU/GPU, allocations/copies, memory, encode/decode time,
    frames encoded/decoded/dropped/frozen, keyframe recovery, bitrate, latency, and
    A/V sync against recorded controls.

**Exit gate:** a Windows Broiler endpoint sends and displays correctly sized,
oriented, color-correct, synchronized video with pinned Chromium and Firefox through
direct and relay routes for every advertised codec/profile. Loss, congestion,
keyframe recovery, source changes, renegotiation, visibility, device loss, and close
remain bounded; rendering never serializes/reparses the DOM per frame; focused WPT,
interop, visual, performance, soak, and leak gates pass.

## RTC-8 — Complete negotiation, RTP, object, and statistics semantics

**Owner:** HtmlBridge WebRTC binding and Broiler.WebRtc, with standards/test owners.

**Current evidence:** RTC-4 through RTC-7 establish vertical slices, but stable
WebRTC requires the complete state machine and object graph, not only a happy-path
offer/answer call. Current HtmlBridge has no generated/general WebIDL system that can
be assumed to supply these semantics automatically.

**Next actions:**

1. Complete the frozen WebIDL inventory and prototype/constructor/exposure matrix
   for peer, ICE, DTLS, SCTP, RTP sender/receiver/transceiver, codecs/parameters,
   certificates, descriptions/candidates, streams/tracks, data channels, stats,
   errors, and events.
2. Implement Unified Plan transceiver creation/reuse/stopping, MID assignment,
   directions/currentDirection, sender/receiver identity, stream association, remote
   track addition/removal, negotiation-needed flag calculation, and rollback.
3. Complete the operations chain and races for simultaneous offer/glare, perfect
   negotiation, repeated negotiation, ICE restart/generation, candidate-before-
   description, close during pending operation, remote rejection, and backend failure.
4. Implement `getCapabilities`, `getParameters`, `setParameters`, codec/header-
   extension/encoding validation, sender `replaceTrack`, DTMF where required by the
   frozen profile, and failure/Promise timing. Defer simulcast/SVC controls unless
   explicitly accepted in RTC-0.
5. Implement certificate generation, expiration/algorithm state, and the
   `RTCCertificate` serializable-object hooks: origin-bound serialization and
   deserialization, structured-clone integration, protected non-exportable key
   handles, and canonical persistent-storage integration where available. If safe
   required serialization/persistence semantics cannot be supplied, the applicable
   certificate surface and stable tests are blocked rather than approximated. Also
   complete candidate parsing/accessors, transport object identity/state,
   garbage-collection roots, and collection snapshot/live behavior.
6. Implement `getStats()` as a snapshot `RTCStatsReport` with correct selector,
   timestamps, IDs/references, types, iteration/map behavior, and every mandatory
   field in the frozen baseline. Extend the native ABI where necessary; inability to
   supply a mandatory value blocks the stable claim. Derive/marshal values only with
   defined, tested units and privacy policy.
7. Build an algorithm-derived error matrix for every operation and state. Distinguish
   ECMAScript `TypeError`/`RangeError` from DOMException names; cover applicable
   `SyntaxError`, `InvalidAccessError`, `NotSupportedError`,
   `InvalidCharacterError`, `InvalidStateError`, `OperationError`,
   `NotAllowedError`, `NotFoundError`, `OverconstrainedError`,
   `NotReadableError`, `AbortError`, `SecurityError`, and `RTCError` details without
   treating this prose list as exhaustive. Test synchronous throw versus rejected
   Promise, message/privacy policy, and exact operation timing from the frozen
   algorithms.
8. Audit task sources, run-to-completion snapshots, event order, attribute handlers,
   listeners, `this`, object identity, reentrancy, and microtask checkpoints against
   focused WPT and differential browser traces.
9. Add property/state-model tests that generate valid and invalid operation sequences,
   compare the Web-visible state to an independent reference model, and shrink any
   divergence to a reproducible case.
10. Close the frame/context exposure dependency: implement per-frame origins and
    realms, top-level delegation and Permissions Policy for camera/microphone, and
    correct same-origin/cross-origin allowed and denied cases. If general iframe
    support is not ready, it blocks the corresponding stable conformance claim; do
    not remove those tests from the manifest to make RTC appear complete.
11. Close every accepted WPT manifest gap with product code or a reviewed spec
    interpretation. Do not add runner shims, expected passes, or shape-only globals.

**Exit gate:** the stable API inventory has no unowned or untested member; focused
WebRTC/media/stats WPT passes every applicable mandatory test for the claimed profile
with no in-scope failure, timeout, crash, skip, or expected-failure waiver;
model-generated state sequences, negotiation/rollback/restart races,
exception/Promise timing, object identity, event ordering, and stats snapshots match
the frozen standards and interoperate with reference browsers.

## RTC-9 — Harden networks, privacy, native code, and abuse limits

**Owner:** Browser networking/security, Broiler.WebRtc native owner, application
policy, test infrastructure, and release security.

**Current evidence:** Broiler has no RTC network policy, TURN lab, candidate privacy,
native RTC fuzzing, resource limits, SBOM/vulnerability workflow, or renderer/process
sandbox. Current server/proxy examples are not an RTC security boundary and must not
be treated as one.

**Next actions:**

1. Build a hermetic network lab with ephemeral STUN and TURN endpoints for UDP, TCP,
   and TLS; IPv4 and IPv6; direct, NAT-like, relay-only, unreachable, bad credential,
   expired credential, packet loss, duplication, reorder, delay, jitter, bandwidth
   change, path change, and consent failure. It uses no production secrets.
2. Implement the reviewed candidate policy: interface filtering, host-candidate
   suppression or mDNS mapping where correct, server-reflexive/relay handling,
   `iceTransportPolicy: "relay"`, local-network protections, bad-port rules, and
   redaction. Test what the page, remote peer, logs, and stats can each observe.
3. Validate STUN/TURN URI schemes, transports, DNS, redirects if any, certificate
   validation, credential types/lifetimes, authentication failures, server limits,
   and proxy/firewall behavior. TURN credentials always arrive through page config;
   browser/application configuration contains no shared service secret.
4. Exercise interface changes, suspend/resume, Wi-Fi/Ethernet transitions, default
   route changes, IPv4/IPv6 preference, transient DNS, ICE restart, relay failover,
   consent expiry, and clock jumps with deterministic state expectations.
5. Set measured, configurable limits for peer connections, transceivers, senders,
   receivers, data channels, pending operations, candidates, ICE servers, SDP,
   certificates, message size, buffered bytes, media resolution/rate/bitrate, queues,
   sockets, native threads, memory, and diagnostics. Define exceptions/closure when
   each limit is reached.
6. Add managed property/fuzz tests and native libFuzzer/AFL-equivalent harnesses for
   SDP, candidate, STUN/TURN, DTLS, SCTP, data, RTP, RTCP, codec and image boundaries.
   Run ASan/UBSan and platform-appropriate memory/thread diagnostics on reviewed
   corpora; preserve minimized regressions.
7. Audit crypto configuration, certificate/fingerprint validation, cipher and SRTP
   profiles, replay windows, key lifetime/zeroization, RNG failures, downgrade
   resistance, identity separation, and error redaction against RFC 8826/8827.
8. Implement and test a least-privilege native RTC broker/process boundary for a
   general-Web stable claim, including authenticated/versioned IPC, handle/buffer
   limits, crash detection, resource cleanup, restart/disable behavior, and sandbox
   policy. Close or explicitly depend on the Browser's broader untrusted-content
   isolation/hardening gate as well. An in-process backend may receive security
   sign-off only for a controlled/trusted-content preview with that limitation in
   feature detection, packaging, and support statements; it cannot graduate to
   general-browser stable.
9. Add dependency monitoring, upstream-change intake, vulnerability triage, CVE SLA,
   emergency rebuild/signing, revocation/kill switch, rollback, and EOL policy.
   Verify the exact shipped native commit and flags from the binary/SBOM.
10. Commission an independent security review and resolve all critical/high findings;
    attach medium residual risks to named owners and stable-release decisions.

**Exit gate:** the network lab passes every advertised route and expected failure;
IP/device/credential/media privacy tests find no prohibited disclosure; consent and
route changes recover or close deterministically; abuse limits keep memory, queues,
threads, and sockets bounded; fuzz/sanitizer campaigns meet their recorded duration
and clean-result gates; supply-chain and emergency-update drills succeed; security
review approves the exact preview/stable exposure statement; and a general-Web stable
candidate runs hostile native RTC processing in the approved broker/sandbox and
passes the Browser-wide untrusted-content security gate. An in-process or otherwise
unsandboxed candidate remains explicitly controlled/trusted-content preview-only.

## RTC-10 — Add Linux and Android profiles without weakening platform boundaries

**Owner:** Browser Linux and Android heads with Broiler.Input, Graphics, WebRtc native
packaging, permissions, audio output, CI, and release owners.

**Current evidence:** Linux Browser has graphics and keyboard/mouse input but no
camera/microphone or RTC assets. Android Browser targets API 36/min 24 and currently
declares only `INTERNET`; it has no camera/microphone runtime flow, audio/video RTC
adapter, or background-call policy. Windows-native dependencies must not enter these
solution closures.

**Next actions:**

1. For each platform, choose and document the first claim independently: absent,
   data-only preview, receive-only experiment, or full A/V. Never infer A/V support
   from successful native-library loading or a data channel.
2. Package and load the approved backend for each supported architecture with the
   same ABI/version/security gates, platform symbols, notices, SBOM, and clean-host
   smoke tests.
3. Linux: implement approved camera and microphone providers using the chosen native
   stack (for example V4L2/PipeWire and PipeWire/PulseAudio/ALSA as the platform ADR
   decides), device watching, default changes, clocking, bounded delivery, and a real
   audio renderer. Define desktop-portal/permission behavior rather than bypassing
   the desktop security model.
4. Linux: validate X11/Wayland composition paths, software/hardware video decode,
   live surfaces, device loss, distro library/driver matrix, sandbox/service access,
   package dependencies, and headless behavior.
5. Android: add `CAMERA` and `RECORD_AUDIO` manifest declarations only with complete
   feature delivery; implement runtime permission requests and rationale/deny/
   don't-ask-again mappings, camera/audio adapters, device changes, rotation,
   foreground indicators, audio focus/routing, and Bluetooth/headset behavior.
6. Android: define activity recreation, window loss, pause/resume, background,
   screen lock, process death, connectivity changes, power/thermal throttling, and
   foreground-service policy. Default to stopping or suspending capture unless an
   explicitly reviewed product policy permits continuity.
7. Apply the same secure-context, origin, device-ID, constraints, indicator, resource,
   network privacy, WPT, interop, hardware, soak, and teardown gates on each platform;
   add platform-specific expected results only for standards-permitted differences.
8. Keep application composition platform-local. Add portable contracts/tests to
   `Broiler.Tests.slnx`, Windows native tests to `Broiler.Windows.Tests.slnx`, and
   Linux/Android projects only to applicable closures through `eng/solutions.json`.
9. Publish an exact platform/architecture/device/driver/OS support matrix with known
   limitations. Feature detection and packages must match it at runtime.

**Exit gate:** each newly claimed platform passes its own native-package, secure
permission, fake/hardware, direct/TURN, Chromium/Firefox or reference-endpoint,
focused WPT, lifecycle, performance, soak, and leak gates on the published matrix.
No unsupported platform loads the wrong native asset or exposes an unusable API,
and Windows/portable/WebAssembly architecture guards remain clean.

## RTC-11 — Prove conformance and ship the supported profile

**Owner:** Cross-repository release owner with standards, Browser, WebRtc, platform,
security, performance, documentation, and support owners.

**Current evidence:** there is no WebRTC support claim, release workflow, WPT
testharness report, interop matrix, operational dashboard, hardware certification,
or native update drill. The current aggregate xUnit graph cannot be made a new
blocking gate without first separating known unrelated failures.

**Next actions:**

1. Freeze release candidates for Broiler source/submodule commits, native upstream
   revision/patches, build toolchains, WPT, reference browsers, test servers, OS/RID
   matrix, feature flags, and support manifest.
2. Make the focused portable and native RTC PR jobs required from their first green
   baseline. Run WPT, interop, network, sanitizer/fuzzer, hardware, soak/performance,
   and package lanes at their documented cadence and trust boundary.
3. Produce a machine-readable WPT report with pass/fail/timeout/crash/not-run. Every
   applicable mandatory test in the stable manifest must pass: no in-scope failure,
   timeout, crash, skip, or expected-failure waiver. Standards-permitted platform
   expectations are justified separately and may not conceal a missing advertised
   behavior; deferred tests stay in an explicit out-of-scope inventory.
4. Complete Broiler↔Broiler, Chromium, and Firefox interop for data, every advertised
   codec/direction, renegotiation, device switch, ICE restart, direct/relay, network
   changes, close, and long-call scenarios on each claimed platform.
5. Establish measured performance budgets from reference/control runs and pass them
   on release hardware: setup/ICE/DTLS time, media latency/A/V skew, CPU/GPU,
   allocations/memory, queue depths, data throughput, audio underruns/concealment,
   video frames dropped/frozen, and teardown.
6. Complete privacy/security review, native dependency/vulnerability scan, notices,
   codec/patent approval, SBOM, binary provenance, signing, clean-feed install,
   rollback/disable, crash and CVE response drills.
7. Run the declared camera/microphone/output hardware matrix, denial/revoke/remove/
   busy cases, OS lifecycle, repeated navigation, multi-hour sessions, and multi-peer
   resource caps. Verify physical indicators and OS device release.
8. Publish developer documentation, supported API/platform/codec/network matrix,
   permissions and secure-context behavior, signaling example boundary, TURN setup
   guidance without secrets, diagnostics/redaction guide, known limitations,
   troubleshooting, security policy, and upstream-update runbook.
9. Stage enablement: developer flag, controlled preview, opt-in beta, then default
   for the exact stable profile. Define telemetry-free/manual evidence alternatives
   where product telemetry is not approved, rollback thresholds, and an emergency
   remote/local disable path consistent with Broiler release policy.
10. Remove obsolete WebRTC expected-failure records only after the real harness runs
    them. Reconcile root/component roadmaps and support docs without erasing useful
    historical evidence.

**Exit gate:** the exact signed release passes all required lanes with no unexpected
conformance result, unresolved critical/high security issue, unsupported native
asset, privacy leak, or resource leak; every applicable mandatory in-scope WPT passes
without failure, timeout, crash, skip, or expected-failure waiver; it installs and
rolls back on clean supported hosts, interoperates across the published matrix, and
its documentation, feature detection, package contents, runtime behavior, and
support statement agree. Only then is WebRTC enabled by default for that profile.

## API and behavior completion ledger

RTC-0 converts this planning table into a machine-readable inventory. Rows close
only when WebIDL, behavior, backend, policy, and test evidence all exist.

| Surface | Primary owner | Earliest phase | Required evidence |
| --- | --- | --- | --- |
| Durable JS realm, task source, wakeup, disposal | HtmlBridge/Browser | RTC-1 | Owner-thread/order/lifetime integration tests |
| `RTCPeerConnection` core/JSEP | HtmlBridge + WebRtc | RTC-4/RTC-8 | Model tests, WPT, direct/TURN interop |
| ICE candidates/config/transports | WebRtc + network policy | RTC-4/RTC-9 | Protocol tests, privacy matrix, WPT/interop |
| `RTCDataChannel` | HtmlBridge + WebRtc | RTC-4 | Reliability/order/backpressure/size WPT and interop |
| `MediaDevices` and device info | HtmlBridge + Browser + Input | RTC-3/RTC-5 | Secure permission/privacy/constraints WPT + hardware |
| `MediaStream`/`MediaStreamTrack` | HtmlBridge + Input/WebRtc | RTC-3/RTC-8 | State/clone/source/track WPT and lifecycle tests |
| Senders/receivers/transceivers | HtmlBridge + WebRtc | RTC-6–RTC-8 | Unified Plan model, WPT, renegotiation interop |
| Local camera/microphone | Input + platform head | RTC-5/RTC-10 | Fake contracts, hardware, privacy, teardown |
| Audio RTP and playout | WebRtc + platform output | RTC-6/RTC-10 | Codec/quality/loss/AEC/interop/performance |
| Video RTP and live render | WebRtc + Graphics/HTML | RTC-7/RTC-10 | Codec/visual/A/V sync/interop/performance |
| `HTMLMediaElement.srcObject` | HtmlBridge + HTML/Graphics/output | RTC-5–RTC-7 | Layout/render/play/mute/source/lifetime tests |
| `getStats()` | HtmlBridge + WebRtc | RTC-8 | Mandatory-field WPT, unit/reference/unit/privacy tests |
| Secure contexts/permissions/indicators | Browser policy/platform | RTC-3/RTC-5/RTC-10 | origin/frame/deny/revoke/UI/privacy tests |
| Native build/update/security | WebRtc native + release/security | RTC-0/RTC-2/RTC-9 | ABI, SBOM, scan, fuzz, provenance, rollback |
| Browser WebSocket for common signaling | Separate networking owner | Parallel to RTC-4 | WebSocket WPT, origin/auth/backpressure/proxy tests |

## Verification strategy

### Test lanes and promotion rules

| Lane | Trigger/cadence | Environment | Blocks |
| --- | --- | --- | --- |
| Portable RTC PR | Every relevant PR | Deterministic fake backend, fake Input, injected clock/RNG/network | Merge from first green baseline |
| Native ABI/loopback PR | Every native/backend PR per supported OS | Reviewed native assets, no physical devices | Native merge/package promotion |
| Focused WPT | Every relevant PR when stable enough; otherwise nightly until promoted | Pinned WPT, real `wptserve`, HTTPS/host aliases, fake permission/devices | Stable manifest changes and release |
| Network lab | Nightly and release | Ephemeral STUN/TURN, isolated route/loss/NAT scenarios | M1/M3 and release route claims |
| Browser interop | Nightly and release | Pinned Chromium/Firefox plus Broiler endpoints | Preview/stable interoperability claims |
| Native fuzz/sanitizer | Continuous scheduled and release-duration campaign | Isolated ASan/UBSan/platform diagnostics | Native promotion and security release |
| Hardware/platform | Opt-in lab, release required | Published camera/mic/audio/GPU/OS matrix | A/V platform claim |
| Soak/performance | Nightly/release | Controlled reference hardware/network | M3 and stable release |
| Package/release | Release candidate | Clean supported hosts, feed/install/update/rollback | Stable release |

Do not add the currently red aggregate `dotnet test Broiler.Tests.slnx` as an
unqualified WebRTC gate. Add focused green RTC projects and explicitly invoked
Input/Media executable contract suites, then baseline and ratchet existing unrelated
failures under their owners. A WebRTC change may not make those baselines worse.

### Portable deterministic coverage

Required PR tests include:

- JSEP and ICE state tables, operation chains, glare, rollback, negotiation-needed,
  ICE restart/generation, candidate races, close and backend failure;
- transceiver/sender/receiver identity, directions, track/stream association,
  parameters, replaceTrack, clone, enable/mute/end, source sharing and teardown;
- data channel negotiation, ID/reliability, strings/binary, order, loss, partial
  reliability, fragmentation, backpressure, thresholds, close, and limits;
- permission, secure context, origin/frame isolation, label/device-ID privacy,
  constraints/fitness, defaults, device changes, denial/revoke/remove/busy/fault;
- WebIDL/prototype/constructor/descriptors, synchronous throws versus Promise
  rejection, DOMExceptions, task/microtask/event order, reentrancy, GC roots;
- late callback wakeup, document generation, two-session isolation, navigation and
  shutdown during every pending state;
- synthetic audio/video timing, formats, drops/discontinuities, loss/reorder/jitter,
  queue limits, stats units/references, redaction, and deterministic snapshots;
- resource/abuse negatives and property/state-model sequences with shrinking.

### Real WPT testharness lane

The visual/reftest runner remains intact. Add a separate behavioral mode that:

1. Pins WPT by commit and records the exact manifest and browser build.
2. Runs upstream `wptserve` with HTTP/HTTPS, trusted local CA, WPT host aliases,
   WebSocket support required by the corpus, and isolated loopback networking.
3. Loads the real `testharness.js`; supports asynchronous completion, long-lived
   documents, Promise tests, per-test timeout, subtests, structured result capture,
   cleanup, and crash attribution.
4. May inject deterministic fake devices, permission decisions, clocks, and faults
   through product configuration—not through altered test scripts or page-visible
   runner shims. A separate development manifest may use the fake RTC engine for
   deterministic state tests, but the stable conformance manifest always runs the
   exact shipping native backend and package; fake-backend passes never promote or
   substitute for it.
5. Starts with explicit manifests for `webrtc` and `mediacapture-streams`, then adds
   `webrtc-stats` and HTML media tests. Deferred experimental suites remain not-run,
   not expected-pass.
6. Publishes pass/fail/timeout/crash/not-run plus unexpected-result diffs. Baselines
   may record reviewed expected failures during development, each with owner and
   expiry gate. Stable scope requires every applicable mandatory in-scope test to
   pass, with no failure, timeout, crash, skip, or expected-failure waiver.
7. Runs untrusted upstream code with read-only workflow permissions,
   `persist-credentials: false`, no repository/write token or production secrets,
   and no `pull_request_target`. A separate trusted job may publish artifacts.

### Network and interop matrix

At minimum, exercise:

| Dimension | Values |
| --- | --- |
| Endpoint pair | Broiler↔Broiler, Broiler↔Chromium, Broiler↔Firefox |
| Signaling | In-process test exchange, HTTPS fetch fixture, WebSocket after its separate gate |
| Candidate route | Host/privacy-filtered, server-reflexive, TURN UDP, TURN TCP, TURN TLS, relay-only |
| Address/network | IPv4, IPv6 where supported, dual-stack, route change, failed DNS/server/auth |
| Channel | Ordered, unordered, negotiated, in-band, retransmit/time-limited, text/binary, saturation |
| Media | Send/receive/sendrecv/inactive; each advertised codec; replace/source switch; mute/end |
| Impairment | Loss, duplication, reorder, delay, jitter, constrained bandwidth, path loss/recovery |
| Lifecycle | Close at each state, navigation, revoke, device removal, suspend/resume, long soak |

### Hardware and lifecycle matrix

The Windows release lab covers built-in and USB cameras, at least one NV12/YUY2 and
MJPEG source where available, built-in/USB/Bluetooth audio as supported, no-device,
deny, revoke, busy/exclusive, unplug, default change, slow consumer, format change,
screen lock, sleep/resume, network change, renderer device loss, repeated start/stop,
and application crash/restart. Linux and Android define equivalent platform-specific
matrices before claiming A/V.

Every lifecycle case checks user-visible state, events/exceptions, capture indicator,
OS device ownership, sockets, native handles/refs, managed objects, threads, queues,
memory, and absence of late callbacks.

## Observability and diagnostics

Web-facing `getStats()` and internal product diagnostics are separate contracts.
`getStats()` follows WebRTC Stats object identity, types, units, references, selection,
and privacy rules. Internal diagnostics are injected, bounded, off by default, and
must not become a process-global logger or a packet trace.

### Internal event model

Use stable event IDs and random/session-local correlation tokens for:

- factory/backend load, ABI/version, peer create/close, track/channel create/close;
- permission outcome category and capture start/stop/fault without origin/device name;
- ICE gather/connect/restart/selected-route type and protocol without address;
- DTLS/SRTP/SCTP state and redacted error category;
- codec start/stop/reconfigure, source discontinuity, output underrun, video freeze;
- queue/backpressure/limit reached, callback discarded by stale generation, and
  bounded shutdown duration/result.

Internal counters/gauges may include active/created/closed peers, tracks and channels;
candidate type/protocol outcome; setup duration; RTT, jitter, loss and bitrate;
audio concealment/underruns; encoded/decoded/dropped/frozen frames; capture and
renderer drops; data buffered amount; bounded queue depth; native handles, threads,
sockets and memory. Do not use origin, URL, room, user, peer, device ID, candidate
address, or TURN username as metric labels.

Extend CLI diagnostic bundles only with an explicitly requested, size-bounded,
redacted state timeline and summary. Add golden redaction tests proving that SDP,
candidate IPs, device identifiers, credentials, keys, payloads, media, and page
identities cannot appear. Full packet/native traces are developer-only artifacts in
isolated environments and are never enabled by page content.

## Performance, quality, and resource budgets

RTC-0 records control measurements on named reference machines, routes, devices,
resolutions, codecs, and builds. RTC-6/RTC-7 turn those measurements into reviewed
budgets and tolerances. Until then this roadmap specifies metrics and bounding rules,
not invented pass numbers.

| Area | Required measurements | Gate method |
| --- | --- | --- |
| Setup | permission, device open, offer/answer, ICE gather/connect, DTLS/data/media first-use p50/p95 | Compare to recorded Broiler control and reference-browser run under identical lab conditions |
| Audio quality | capture-to-wire, wire-to-playout, round-trip, underruns, concealment, drift, AEC residual/quality fixture | Absolute functional thresholds plus no unexplained regression beyond approved tolerance |
| Video quality | capture/encode/decode/render time, glass-to-glass fixture, FPS, drops/freezes, keyframe recovery, A/V skew | Resolution/network profile budgets and reference/control differential |
| Data | throughput, message latency, buffered amount, low-threshold timing, memory under saturation | Correct backpressure and fixed memory/queue ceiling before throughput optimization |
| Resources | CPU/GPU, allocations/copies, managed/native memory, threads, handles, sockets, queue depth | Idle/call profile budgets and return to post-warm baseline after bounded teardown |
| Longevity | repeated create/close/navigation, multi-hour A/V/data, route/device changes | No monotonic resource growth, stale callback, deadlock, crash, or quality collapse |

Safety bounds are present before preview even if optimization is incomplete. Queue,
message, SDP, candidate, object-count, bitrate, resolution, and memory limits are
configurable only through trusted application policy, have safe defaults derived
from measurements and spec minima, produce deterministic Web-visible failure, and
cannot be raised by page content beyond the product ceiling.

## Packaging, release, and upstream maintenance

Each native release must provide:

- exact upstream source commit, Broiler patch commits, build recipes/container or
  immutable toolchain lock, feature/codec flags, compiler/CRT/NDK versions, and
  reproducible input hashes;
- RID/ABI-specific binaries with ABI version metadata, debug symbols and source
  mapping, signed artifact hashes, notices/licenses/source obligations, codec/patent
  decision, provenance attestation, and SBOM;
- correct framework-dependent and self-contained publish behavior, no accidental
  asset on unsupported profiles, clear missing/incompatible-asset diagnostics, and
  clean-feed install smoke tests;
- vulnerability/advisory monitoring, named update owner, response SLA, patch intake,
  ABI/interop/performance regression suite, emergency rebuild/sign path, supported
  version window, and EOL process;
- feature disable and rollback that closes existing resources and makes future
  capability detection consistent after restart. It must never silently fall back
  from secure native RTC to a shape-only or incompatible implementation.

Release workflows pin all third-party actions and corpora, use least privilege,
separate untrusted test execution from artifact signing/publishing, and retain the
machine-readable evidence used to approve the exact binary. The native component is
added to publish-approval and human-review coverage before any public package ships.

## Signaling, WebSocket, and TURN boundary

JSEP leaves signaling unspecified. The page is responsible for exchanging offers,
answers, candidates, and application metadata through its chosen authenticated
service. The browser must not invent a Broiler room/participant protocol or couple
`RTCPeerConnection` to Broiler Office Server.

Three adjacent deliverables remain distinct:

1. **Browser WebSocket API:** a separate standards feature needed by many real WebRTC
   applications. It needs its own WebIDL, handshake/origin/cookie/security policy,
   binary and text behavior, compression decision, backpressure, close, resource
   limits, WPT, and proxy interop. It can progress beside RTC-4.
2. **Test/demo signaling fixture:** an ephemeral HTTPS/WSS service for repository
   examples and interop automation. It has deterministic schemas, tiny limits,
   no production identity claims, no persistence, and is never a browser dependency.
3. **Production signaling/TURN operations:** outside this browser roadmap. If Broiler
   later ships them, use separate product architecture and operations gates.

If a product decision extends Broiler Office Server, it must first add trustworthy
TLS deployment, authentication/authorization, room isolation, origin validation,
request/message/concurrency/rate limits, bounded backpressure/timeouts, a dedicated
WebSocket proxy upgrade path, readiness, metrics/redaction, abuse handling, and
package smoke tests. Public HTTP defaults cannot host a production capture demo.

A production TURN deployment is separate from Office Server: hardened non-root
service, UDP/TCP 3478 and TLS 5349 as selected, bounded relay port range/firewall,
DNS/certificate renewal, quotas/rate/bandwidth alarms, short-lived credentials from
an authenticated issuer, rotation/revocation, and no shared secret in browser code,
client-visible app settings, examples, or logs.

## Initial implementation slices

These are subordinate, reviewable slices of the owning RTC phases. The explicit
owner, evidence, action, and handoff gate prevent this table from becoming a second
unowned backlog. None advertises WebRTC in a production profile prematurely.

| Slice | Owner / phase | Current evidence | Next action | Objective handoff gate |
| --- | --- | --- | --- | --- |
| 1. Locks and inventories | Standards + security / RTC-0 | No frozen WebIDL/WPT/reference-browser lock or RTC threat model | Add machine-readable locks, API ledger, threat-model template, scorecard, and spike inputs; no global change | Review reproduces every input and assigns every API/security decision |
| 2. WPT testharness seed | Test infrastructure / RTC-0 | Current runner substitutes a visual-only harness | Run one ordinary async test and one fake RTC/media test through real `wptserve`/HTTPS with structured output and least-privilege CI | Both results and cleanup are reproducible; no test-script shim or write credential exists |
| 3. Document-session lifetime | HtmlBridge/Browser / RTC-1 | Settled load can dispose `InteractiveSession` | Retain a settled realm through idle, add generation/cancellation and late-wakeup tests, preserve static rendering | A callback posted after settlement runs once on the owner thread; navigation releases the realm |
| 4. Ordered external tasks | HtmlBridge/Browser / RTC-1 | Event loop lacks external wakeup/activity ownership | Add typed posting, wakeup, owner-thread dispatch, microtask checkpoint, bounds and disposal; migrate Worker as first consumer | Worker and fake-network tasks pass ordering, wakeup, reentrancy, stale-generation and teardown tests |
| 5. Permission/security foundation | Browser policy / RTC-3 | No trustworthy-context or permission broker | Add neutral trustworthy-context and session-only origin permission contracts, fake broker, device-exposure/privacy and accessibility tests; no physical device | Allow/deny/dismiss/revoke and pre-capture empty enumeration pass without exposing a production global |
| 6. Backend comparison | WebRtc native + security/release / RTC-0 | No approved RTC protocol engine | Build both shortlisted candidates behind the same disposable C ABI; collect direct/relay/interop/media/build/package/license/security/teardown evidence | Reviewed scorecard selects one backend or records a no-go; estimate and residual risks are explicit |
| 7. ADR and component scaffold | WebRtc + architecture/release / RTC-2 | No governed native deployment seam | Approve the boundary, create neutral/native/testing projects, guards, solution entries, notices, SBOM seed, load/destroy smoke | Clean supported host loads the pinned ABI and all architecture/package/provenance checks pass |
| 8. Fake peer vertical slice | HtmlBridge + WebRtc.Testing / RTC-4 | No peer/data WebIDL or state model | Implement peer/data objects and state/order tests against the fake engine only in a dedicated test profile | Focused shape, state, Promise/event order, close, navigation and limit tests pass; production globals remain absent |
| 9. Native data vertical slice | WebRtc native + HtmlBridge / RTC-4 | Fake slice has no protocol interop | Replace fake engine through unchanged neutral contracts and run direct/TURN Chromium/Firefox data interop | M1 mandatory data/JSEP tests and leak/limit gates pass on the exact packaged native backend |
| 10. Fake media and live-output proofs | HtmlBridge/Input/Graphics/audio / RTC-3, RTC-5 | Capture fakes exist; no MediaStream model or live output | Implement RTC-3 over Input fakes and land mutable-video/clocked-audio proof paths before physical composition | Fake capture/privacy/track tests pass and one bounded frame/PCM stream reaches and releases each live output |

The first review checkpoint follows slice 6. It confirms or revises architecture,
scope, estimates, staffing, support platforms, and the go/no-go decision before the
repository accepts the long-lived native dependency.

## Risk register

| Risk | Impact | Mitigation and decision trigger | Owner |
| --- | --- | --- | --- |
| Native backend is too large, unstable, or difficult to update | Release/security failure | RTC-0 comparative C ABI/build/package spike; pinned patch-minimal fork; replacement seam; stop if update drill fails | WebRtc native + release |
| Browser session/event-loop model drops or races late events | Incorrect API, crashes, leaks | RTC-1 before exposure; owner-thread task model, generation fences, model/lifetime tests | HtmlBridge/Browser |
| Unsandboxed native parsing/networking expands exploit impact | Critical security exposure | Threat model, fuzz/sanitizers, limits and independent review; in-process is controlled/trusted preview-only, approved broker/sandbox is mandatory before a general-Web stable claim | Security/Browser |
| Capture permissions or identifiers leak device/user information | Privacy breach | Canonical secure context and origin broker; pre-capture one-per-kind empty exposure, post-capture salted/scoped IDs; redaction, accessible indicators/revoke and privacy tests | Browser policy |
| Current renderer/audio output cannot meet real-time needs | M3 blocked or unusable quality | RTC-0 live output spikes; mutable surfaces, bounded audio device path, measured budgets before A/V commitment | Graphics/Media/platform |
| Input callbacks block capture or copies overwhelm CPU | Drops/latency/deadlock | Bounded async adapters, native formats, pooling/ownership measurements, slow-consumer tests | WebRtc.Input/Input |
| Codec/license/patent obligations prevent distribution | Cannot ship A/V | RTC-0 legal review and codec flag matrix; no public artifact before approval | Legal/release |
| Visual WPT runner gives false confidence | Invalid conformance claim | Separate real testharness lane; machine-readable pass/fail/timeout/crash/not-run; every applicable mandatory stable test passes; no runner shims | Test infrastructure |
| Browser WebSocket/signaling scope consumes RTC work | Schedule/product ambiguity | Treat as separate standards/product tracks; use fetch/in-process fixture for early interop | Browser networking/product |
| TURN/NAT behavior works only on developer LAN | Real-world connection failure | Ephemeral multi-route network lab, relay-only and failed-route gates, external interop before preview | WebRtc/network test |
| Linux/Android are advertised from Windows evidence | Misleading support and packaging defects | Per-profile absence by default; independent platform gates and solution closures | Platform/release |
| Performance targets are guessed or optimized at expense of correctness | Rework or hidden regressions | Record controls in RTC-0; correctness/security first; explicit measured tolerances and bounded queues | Performance owners |
| Upstream/spec revisions churn during implementation | Unbounded scope | Freeze locks per milestone; candidate amendment ledger; scheduled refresh with reviewed diffs | Standards/WebRtc |

## Release evidence package

For each milestone, retain generated evidence under `tests/` or ignored `artifacts/`,
not as hand-maintained prose results in `docs/`:

- source/native/toolchain/corpus/reference-browser lock and build provenance;
- API inventory and support manifest with implemented/deferred/not-run status;
- focused unit/model/architecture/ABI results and unexpected-result diff;
- WPT structured report and manifest;
- redacted network-route and browser-interop matrices;
- hardware/platform/lifecycle result matrix;
- fuzz/sanitizer duration, corpus identity, crash/minimization status;
- performance/quality/resource controls, candidate results and tolerances;
- license/notices/patent decision, SBOM/vulnerability scan, security-review closure;
- package/feed/install/update/rollback/disable results and signed hashes;
- reviewed support, limitation, diagnostics, security, and operations documents.

Prose roadmaps link these artifacts and retain only durable decisions and open work.
An old passing report never substitutes for a fresh exact-release run.

## Terminal definition of done

This roadmap closes for a platform profile only when:

1. every in-scope API-ledger row has one owner, frozen requirement, production
   implementation, focused tests, and passing results for every applicable mandatory
   stable-manifest test, with no failure, timeout, crash, skip, or expected waiver;
2. Web-visible state, ordering, errors, constraints, object identity, stats, media,
   and teardown conform without test-only page shims or shape-only stubs;
3. direct and TURN-relayed data/audio/video interoperate with the pinned reference
   browser matrix across declared codecs, routes, changes, and failure modes;
4. secure context, origin/frame permission, private device identity, accessible and
   spoof-resistant indicator/revoke UI, IP policy, limits, logging redaction,
   fuzzing, and security review pass; a general-Web stable profile runs the native
   RTC boundary in the approved broker/sandbox and passes the Browser-wide
   untrusted-content security gate, while any in-process/unsandboxed profile remains
   explicitly controlled/trusted-content preview-only;
5. fake CI, native CI, WPT, network, interop, hardware, performance, soak, package,
   and rollback lanes pass for the exact release locks;
6. navigation, close, revoke, removal, suspend/resume, failure, and shutdown return
   devices, audio output, render resources, queues, sockets, threads, handles, native
   references, and managed roots to the approved post-warm baseline with no callback;
7. signed packages contain only correct reviewed assets, install on clean supported
   hosts, expose only supported capabilities, and can be disabled/rolled back;
8. developer, user, support, security, platform, codec, network, signaling/TURN
   boundary, diagnostics, limitation, and update documentation agrees with runtime;
9. the root/component roadmaps and expected-failure inventories are reconciled; and
10. the release owner signs the evidence package for that exact platform/profile.

Completion for Windows does not close Linux, Android, WebAssembly, deferred APIs, or
production signaling/TURN products. Those remain absent, separately open, or
explicitly unsupported until their own gates pass.

## Repository evidence and related plans

- [Root roadmap](ROADMAP.md) — cross-component sequencing and HtmlBridge Phase 6
- [HtmlBridge architecture](architecture/htmlbridge.md) — assembly and ownership
  boundaries
- [Browser load-window pump](browser-load-window-pump.md) — present bounded session
  and render-pump behavior
- [Broiler.JS gaps roadmap](broiler-js-gaps-roadmap.md) — Web API scope and the rule
  against shape-only stubs
- [HTML5 test exceptions](html5test-exceptions.md) and
  [privacy test page gaps](privacy-test-page-gaps.md) — current honest WebRTC and
  WebSocket absence
- [WPT rendering gaps](wpt-rendering-gaps.md) and
  [xUnit suite status](xunit-suite-status.md) — current harness and CI evidence limits
- [Broiler.Input README](../Broiler.Input/README.md),
  [camera](../Broiler.Input/docs/camera.md),
  [microphone](../Broiler.Input/docs/microphone.md), and
  [hardware validation](../Broiler.Input/docs/hardware-validation.md) — capture
  ownership, contracts, and open hardware gates
- [Broiler.Media README](../Broiler.Media/README.md) — decode-first component boundary
- [Android architecture](architecture/android.md) — platform composition and security
  baseline

## Maintenance rule

Update this document only for open cross-component outcomes, phase gates, support
decisions, and durable ownership changes. Once a phase closes, move lasting API,
security, dependency, or platform decisions into the owning README/architecture/ADR,
link the reproducible evidence, and remove delivery-history detail that is no longer
needed to understand remaining work. Refresh standards and dependency links through
the RTC-0/RTC-11 lock process rather than silently changing the target mid-phase.

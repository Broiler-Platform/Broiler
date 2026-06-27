# Broiler.Input Component Roadmap

**Status:** Proposed  
**Date:** 2026-06-27  
**Scope:** Architecture, extraction, and delivery plan only. This document does not include implementation.

## 1. Executive decision

Create `Broiler.Input` as a standalone, platform-neutral component family for
discovering input devices, opening them, receiving input, and reporting device
and session state. Windows is the first platform implementation.

The design has three layers:

1. `Broiler.Input` contains one small abstract root class, `InputDevice`, plus
   identity, lifecycle, diagnostics, timing, and discovery primitives shared by
   every input kind.
2. Each input kind has its own abstraction assembly and exactly one abstract
   device base class derived from `InputDevice`.
3. Each platform implementation of a kind has its own assembly. The first set
   uses a `.Windows` suffix.

The initial device kinds are:

| Product term | Canonical kind | Abstraction assembly | First implementation assembly |
|---|---|---|---|
| Keyboard | Keyboard | `Broiler.Input.Keyboard` | `Broiler.Input.Keyboard.Windows` |
| Mouse | Mouse | `Broiler.Input.Mouse` | `Broiler.Input.Mouse.Windows` |
| Webcam / camera | Camera | `Broiler.Input.Camera` | `Broiler.Input.Camera.Windows` |
| Microphone | Microphone | `Broiler.Input.Microphone` | `Broiler.Input.Microphone.Windows` |
| Touchscreen / touchpad contact | Touch | `Broiler.Input.Touch` | `Broiler.Input.Touch.Windows` |
| Pen / stylus | Pen | `Broiler.Input.Pen` | `Broiler.Input.Pen.Windows` |
| Game controller | Gamepad | `Broiler.Input.Gamepad` | `Broiler.Input.Gamepad.Windows` |

`Camera` is the API name rather than `Webcam` because it covers built-in,
external USB, virtual, depth-capable, and future camera sources. User-facing
documentation may still use “webcam.”

Keyboard and mouse are the first migration milestone because partial support
already exists inside `Broiler.Graphics`. Camera and microphone are the first
new capture milestone. Touch, pen, and gamepad follow after the shared contracts
and high-throughput capture model have stabilized.

There will be no monolithic implementation assembly containing all devices.
Applications reference only the abstractions and Windows implementations they
need. An optional convenience package may be considered after the individual
packages are stable, but it must not become a dependency of any abstraction.

## 2. Current-state findings

### 2.1 Input is currently embedded in Graphics

`Broiler.Graphics/Broiler.Graphics/Windowing/BInputEvents.cs` currently defines:

- `BMouseButtons`;
- `BPointerEventArgs`;
- `BMouseWheelEventArgs`;
- `BKeyEventArgs`;
- `BTextInputEventArgs`; and
- a short list of Win32-shaped virtual-key constants in `BVirtualKey`.

`BWindow` exposes protected callbacks for mouse movement, mouse buttons, wheel,
keyboard key down/up, and translated text. These are input responsibilities
attached to a rendering/window base class rather than independently selectable
device abstractions.

### 2.2 The Windows path supports only a small cooked-input subset

`Broiler.Graphics.Windows/Direct2DWindow.cs` translates these messages directly:

- `WM_MOUSEMOVE`, left/right/middle button down/up, `WM_MOUSEWHEEL`, and
  `WM_MOUSELEAVE`;
- `WM_KEYDOWN`, `WM_SYSKEYDOWN`, and `WM_KEYUP`; and
- `WM_CHAR`.

The current path does not expose:

- physical device identity or multiple keyboards/mice;
- scan codes, left/right modifier identity, key repeat metadata, layout changes,
  dead-key composition, or a complete IME composition lifecycle;
- horizontal wheel, extra mouse buttons, raw relative motion, pointer capture,
  or explicit canceled/capture-lost events;
- touch contacts, pen pressure/tilt/eraser, game controllers, cameras, or
  microphones;
- device arrival/removal, capability negotiation, permission state, or input
  session diagnostics; or
- bounded high-throughput delivery suitable for audio samples or camera frames.

This path is useful behavior to preserve, but it is not yet a general input
component.

### 2.3 Application code consumes the Graphics callbacks directly

`src/Broiler.App.Graphics/BrowserWindow.cs` overrides the Graphics callbacks and
forwards mouse operations into the HTML container. Wheel and selected keys are
handled as browser scrolling and navigation commands. The WPF application also
handles WPF `KeyDown` events directly for browser chrome such as Enter and F12.

The migration must therefore separate three concerns that are currently joined:

| Concern | Final owner |
|---|---|
| Native device acquisition and normalization | `Broiler.Input.*.Windows` |
| Window creation, HWND ownership, rendering, and DPI | `Broiler.Graphics.Windows` |
| Browser shortcuts, hit testing, scrolling, focus, DOM events, and permissions by origin | Application and HTML integration layers |

### 2.4 Camera and microphone capture do not exist

No repository component currently enumerates or opens capture endpoints, emits
camera frames, or reads microphone samples. Camera and microphone work is new
capability, not extraction.

`Broiler.Media` remains a separate concern. Media decodes/encodes stored or
streamed formats; Input acquires live samples from operating-system devices.
Input must not absorb codecs, file containers, playback, speakers, or video
rendering.

### 2.5 Repository topology affects delivery

`Broiler.Graphics` is a Git submodule, and `Broiler.HTML` contains a nested
checkout of `Broiler.Graphics`. Moving the existing event types out of Graphics
therefore crosses component repositories and cannot land safely as an
uncoordinated file move.

The recommended topology is a standalone `Broiler.Input` repository included as
one top-level component in the aggregate Broiler workspace. Downstream standalone
components consume versioned packages or one explicitly pinned canonical
checkout. Nested copies must not drift at different revisions.

## 3. Goals

1. Define one abstract root class named `InputDevice` in `Broiler.Input`.
2. Define one direct abstract device base class per supported input kind, in its
   own abstraction assembly.
3. Put every Windows device implementation in its own implementation assembly.
4. Make device enumeration, selection, opening, stopping, removal, and disposal
   explicit and deterministic.
5. Support both event-like input and bounded high-throughput sample capture
   without unbounded queues or blocking the Windows UI thread.
6. Preserve current keyboard, text, mouse, and wheel behavior while extracting
   it from Graphics.
7. Correct the current loss of device identity, key metadata, pointer metadata,
   and hot-plug state.
8. Add Windows camera and microphone capture with explicit format negotiation,
   backpressure, timestamps, privacy state, and teardown rules.
9. Keep platform-neutral assemblies free of Win32, COM, WinRT, WPF, HWND,
   Direct2D, Media Foundation, WASAPI, and XInput types.
10. Keep Graphics, HTML, DOM, JavaScript, and browser policy out of the Input
    component.
11. Make the contracts testable using fake providers and recorded inputs without
    requiring physical hardware in ordinary unit tests.
12. Establish extension points for later operating systems without designing
    Windows behavior into the public abstractions.

## 4. Non-goals

- Rendering a cursor, camera preview, waveform, or game overlay.
- Owning application windows, controls, HWNDs, message loops, or focus policy.
- Encoding camera/microphone data or writing media files.
- Decoding compressed media, playing audio, or presenting video.
- Implementing HTML/DOM events, browser shortcut policy, origin permissions, or
  site-facing `getUserMedia` behavior in the Input assemblies.
- Providing global keylogging, global mouse monitoring, input injection, or
  automation APIs.
- Silently enabling background capture.
- Treating speakers, haptics, controller vibration, or camera lamps as input.
  Output/control capabilities require a separate, explicit design.
- Guaranteeing that every HID is represented by the first release.
- Creating one untyped `Read` operation returning `object` for all device kinds.
- Using assembly scanning, mutable process-wide registries, or module
  initializers to select implementations.
- Making a concrete Windows assembly a transitive dependency of a platform-
  neutral abstraction.

## 5. Terminology and responsibility rules

| Term | Meaning |
|---|---|
| Input kind | A typed category such as Keyboard, Mouse, Camera, or Microphone |
| Device descriptor | Immutable enumeration result containing identity, display name, kind, capabilities summary, and availability |
| Device | An `InputDevice` instance representing one selected OS device or one logical aggregate device |
| Provider | A platform implementation that enumerates descriptors and opens typed devices |
| Session | A time-bounded open/start/stop operation that owns capture resources |
| Event | A small discrete observation such as key transition or mouse button change |
| Sample | A potentially high-rate payload such as an audio buffer or camera frame |
| Semantic input | Focused, layout-aware OS input intended for application/UI behavior |
| Raw input | Device-specific or low-level reports preserving source identity and physical data |
| Capture format | The negotiated sample shape: dimensions/pixel format or sample rate/channels/sample format |
| Consumer | Application code that maps normalized Input data to browser, UI, game, media, or other behavior |

The following rules are mandatory:

1. `InputDevice` has no universal read method. Typed operations belong to the
   derived abstract class for that kind.
2. Device descriptors are safe to retain; open device/session objects are
   disposable and have explicit ownership.
3. Device IDs are opaque. Callers may compare and persist them, but must not
   parse platform-specific paths.
4. Display names are not unique identifiers.
5. Enumeration does not open a camera or microphone and must not activate a
   privacy-sensitive capture indicator.
6. Opening is separate from starting capture when the OS API supports that
   distinction.
7. A device removal is a state transition, not an arbitrary exception from an
   unrelated callback.
8. All event/sample timestamps use a documented monotonic clock domain. Wall
   clock time may be included as diagnostic metadata but is never used for
   ordering.
9. Device capabilities report facts. They do not make browser permission,
   focus, gesture-recognition, or shortcut decisions.
10. Native handles are borrowed only by Windows implementation contracts and
    never leak into the platform-neutral public API.

## 6. Target component and assembly structure

### 6.1 Runtime assemblies

```text
Broiler.Input/
  Broiler.Input
  Broiler.Input.Windows

  Broiler.Input.Keyboard
  Broiler.Input.Keyboard.Windows

  Broiler.Input.Mouse
  Broiler.Input.Mouse.Windows

  Broiler.Input.Camera
  Broiler.Input.Camera.Windows

  Broiler.Input.Microphone
  Broiler.Input.Microphone.Windows

  Broiler.Input.Touch
  Broiler.Input.Touch.Windows

  Broiler.Input.Pen
  Broiler.Input.Pen.Windows

  Broiler.Input.Gamepad
  Broiler.Input.Gamepad.Windows
```

`Broiler.Input.Windows` is a narrow platform-support assembly, not a device
bundle. It owns only reusable Windows host/dispatcher contracts, native error
translation, clock conversion, and carefully shared interop lifetime support.
It must not contain concrete keyboard, mouse, camera, microphone, touch, pen, or
gamepad behavior.

Every abstraction and implementation also receives a corresponding test
assembly. Test assemblies are not shipped runtime components.

### 6.2 Abstract type hierarchy

| Abstraction assembly | Required abstract device class | Direct base | First concrete class and assembly |
|---|---|---|---|
| `Broiler.Input` | `InputDevice` | None | No concrete device in the core assembly |
| `Broiler.Input.Keyboard` | `KeyboardInputDevice` | `InputDevice` | `WindowsKeyboardInputDevice` in `Broiler.Input.Keyboard.Windows` |
| `Broiler.Input.Mouse` | `MouseInputDevice` | `InputDevice` | `WindowsMouseInputDevice` in `Broiler.Input.Mouse.Windows` |
| `Broiler.Input.Camera` | `CameraInputDevice` | `InputDevice` | `WindowsCameraInputDevice` in `Broiler.Input.Camera.Windows` |
| `Broiler.Input.Microphone` | `MicrophoneInputDevice` | `InputDevice` | `WindowsMicrophoneInputDevice` in `Broiler.Input.Microphone.Windows` |
| `Broiler.Input.Touch` | `TouchInputDevice` | `InputDevice` | `WindowsTouchInputDevice` in `Broiler.Input.Touch.Windows` |
| `Broiler.Input.Pen` | `PenInputDevice` | `InputDevice` | `WindowsPenInputDevice` in `Broiler.Input.Pen.Windows` |
| `Broiler.Input.Gamepad` | `GamepadInputDevice` | `InputDevice` | `WindowsGamepadInputDevice` in `Broiler.Input.Gamepad.Windows` |

Each Windows implementation assembly also owns its typed Windows provider, such
as `WindowsKeyboardProvider`. The provider is responsible for enumeration and
opening; the concrete device class is responsible for one selected device and
its sessions. These names are roadmap defaults to be frozen in Phase 0, not
implemented by this document.

The hierarchy remains intentionally shallow. Touch, pen, and mouse may share
value types such as position, buttons, or contact geometry from the core, but
they do not require an extra public `PointerInputDevice` inheritance layer.
This avoids forcing camera/microphone stream semantics and pointer semantics
through the same methods.

### 6.3 Dependency direction

```text
Broiler.Input.<Kind>.Windows -> Broiler.Input.<Kind> -> Broiler.Input
Broiler.Input.<Kind>.Windows -> Broiler.Input.Windows -> Broiler.Input

Broiler.Graphics.Windows -> Broiler.Graphics
Application composition root -> selected Input abstractions and implementations
Application/HTML adapter -> Input abstractions + browser/UI layers
```

During migration, `Broiler.Graphics.Windows` may implement the narrow native
message-source contract declared by `Broiler.Input.Windows`. Input must not
reference Graphics. The application composition root connects the two.

Forbidden references:

- `Broiler.Input` to a typed input assembly or platform implementation.
- One typed abstraction to a sibling typed abstraction.
- An abstraction assembly to `Broiler.Input.Windows`.
- Any Input assembly to HTML, DOM, JavaScript, application projects, WPF, or a
  concrete graphics backend.
- Camera to Microphone or Microphone to Camera. Synchronized A/V is orchestration
  above both abstractions.
- Input to concrete Media codecs or players.
- Platform-neutral assemblies to native interop packages.
- Concrete implementations to each other merely to share registration or
  global state.

### 6.4 Optional packages are deferred

Possible later packages include:

- `Broiler.Input.Windows.All`, a package-only convenience reference;
- `Broiler.Input.Media`, for explicit capture-to-media buffer adapters;
- `Broiler.Input.Testing`, for public fake providers and deterministic replay;
  and
- `Broiler.Input.AudioVideo`, for synchronized camera/microphone capture.

None is required for the first stable release. In particular, synchronized A/V
must not be simulated by hidden coupling between the Camera and Microphone
Windows assemblies.

## 7. Shared `Broiler.Input` contract

### 7.1 `InputDevice`

The root abstract class owns only cross-kind concerns:

- opaque device ID and stable descriptor snapshot;
- input kind;
- human-readable display name;
- connection/availability state;
- open/running/stopped/faulted/disposed lifecycle state;
- common diagnostics and last fault;
- a monotonic timestamp source or clock-domain descriptor;
- cancellation-aware asynchronous lifecycle; and
- deterministic synchronous/asynchronous disposal rules.

It must not own:

- untyped payload events;
- format negotiation;
- frame or sample buffers;
- buttons, keys, coordinates, sample rates, or pixel formats;
- a native device handle; or
- a platform provider singleton.

### 7.2 Discovery and opening

The shared component defines generic discovery vocabulary while each typed
assembly exposes a typed provider/factory. The provider responsibilities are:

- take a snapshot of available devices;
- optionally observe device added, removed, changed, and default-device changes;
- expose capability and permission status without opening when possible;
- open a selected descriptor with kind-specific options; and
- report whether an ID is no longer present rather than silently selecting a
  replacement.

The default device is an explicit query result, not a magic empty ID. A caller
that requests “current default microphone” can opt into following later default
changes; a caller that opens a particular ID remains bound to that ID.

### 7.3 Lifecycle state machine

All device kinds conform to the same conceptual state transitions:

```text
discovered -> opening -> open -> starting -> running
                       open <- stopping <- running
                       open -> closed
any live state -> unavailable | faulted -> closed
closed -> disposed
```

Required behavior:

- repeated `Start`, `Stop`, `Close`, and `Dispose` behavior is documented;
- cancellation during open/start returns the object to a defined state;
- removal wakes pending readers and completes streams with a typed reason;
- callbacks cannot arrive after final disposal returns;
- disposal from within a callback cannot deadlock; and
- provider shutdown cannot dispose caller-owned devices without an explicit
  ownership contract.

### 7.4 Delivery model and backpressure

Two delivery profiles are required:

| Profile | Kinds | Required behavior |
|---|---|---|
| Discrete/state input | Keyboard, Mouse, Touch, Pen, Gamepad | Ordered transitions, optional state snapshots, bounded/coalesced movement where appropriate |
| Sample capture | Camera, Microphone | Asynchronous bounded stream, explicit buffer ownership, drop/backpressure policy, timestamps, discontinuity reporting |

Small transition events may use callbacks or asynchronous streams. Camera and
microphone payloads must not be delivered through an unbounded event queue.

Each high-throughput session declares one of these policies:

- block the producer only when the native API safely permits it;
- drop oldest;
- drop newest; or
- keep latest/coalesce.

The selected policy and drop counts are observable. Defaults are kind-specific:
camera preview normally keeps the latest frame, while microphone capture normally
uses a bounded queue sized to prevent routine loss and reports any discontinuity.

### 7.5 Buffer ownership

Camera frames and microphone buffers have explicit ownership and validity:

- immutable caller-owned copy;
- pooled lease requiring disposal; or
- callback-scoped borrowed memory that cannot escape the callback.

The first public release should prefer pooled disposable leases for sustained
capture and immutable copies for small tests/convenience APIs. Native pointers
must not appear in platform-neutral models.

### 7.6 Diagnostics and errors

Common failure categories include:

- device not found or removed;
- device busy/in use;
- permission denied or privacy-disabled;
- unsupported format/capability;
- initialization or native API failure;
- capture discontinuity or overrun;
- host/focus unavailable; and
- operation canceled.

Native HRESULTs and Win32 error codes may be attached as diagnostic details in
Windows assemblies but do not replace stable, cross-platform error categories.
Diagnostics must never include audio samples, image pixels, text input, key
content, or raw HID reports by default.

## 8. Device-kind contracts

### 8.1 Keyboard

`Broiler.Input.Keyboard` owns:

- `KeyboardInputDevice : InputDevice`;
- logical key and physical key/scan-code representations;
- key location, repeat, extended-key, transition, and modifier state;
- current key-state snapshots;
- keyboard layout/locale change notification;
- translated text input as a separate stream from key transitions; and
- composition events sufficient for dead keys and future IME integration.

The contract explicitly distinguishes a physical key from generated text. A key
press may generate no text, one Unicode scalar, multiple scalars, or an IME
composition update. Shortcuts consume key transitions; text editors consume
text/composition.

The abstraction must not expose Win32 virtual-key values as its canonical key
identity. A Windows event may retain native virtual key and scan code as optional
diagnostic/native metadata.

### 8.2 Mouse

`Broiler.Input.Mouse` owns:

- `MouseInputDevice : InputDevice`;
- absolute client position and optional raw relative motion;
- left, right, middle, X1, X2, and extensible button state;
- vertical and horizontal wheel deltas with units/mode;
- entered, moved, pressed, released, exited, canceled, and capture-lost
  transitions;
- pointer capture requests expressed through a host capability; and
- device identity when raw input is enabled.

Coordinates identify their space explicitly: screen pixels, client pixels,
device-independent client units, or normalized device coordinates. Input does
not hit-test or convert a point to a DOM target.

Double-click, drag, context-menu, hover intent, scrolling distance, and gestures
are consumer semantics and remain outside the device abstraction.

### 8.3 Camera

`Broiler.Input.Camera` owns:

- `CameraInputDevice : InputDevice`;
- camera descriptors, source groups, and capabilities;
- supported frame formats, resolutions, frame-rate ranges, color space, rotation,
  and optional depth/infrared source metadata;
- capture format negotiation and the selected effective format;
- timestamped camera frame leases with planes, strides, and visible/coded size;
- photo capture as a separately advertised capability;
- optional controls such as focus, exposure, zoom, and white balance only when
  capability-discovered; and
- device-lost, format-changed, frame-dropped, and privacy-state diagnostics.

The initial baseline is uncompressed frames suitable for preview or processing.
Compressed camera output may be exposed later with an explicit format and Media
adapter; Camera does not become a codec assembly.

Camera does not create a preview window or graphics texture. A preview consumer
chooses whether to copy into `Broiler.Graphics`, hand a frame to another API, or
discard it.

### 8.4 Microphone

`Broiler.Input.Microphone` owns:

- `MicrophoneInputDevice : InputDevice`;
- endpoint descriptors and default communication/multimedia role metadata;
- supported/native mix formats;
- sample rate, channel count/layout, sample format, and frame count;
- timestamped audio buffer leases;
- shared/exclusive-mode request as an explicit option, with shared mode as the
  safe default;
- device change, discontinuity, silence, and overrun diagnostics; and
- optional capture controls only when the OS exposes them safely.

The abstraction captures raw PCM-like samples. It does not encode WAV, Opus,
AAC, or another format, perform speech recognition, apply browser echo
cancellation policy, or play monitor audio.

Audio processing options such as gain control, noise suppression, and echo
cancellation must report whether they are hardware, OS, or application effects.
They are not silently claimed when unavailable.

### 8.5 Touch

`Broiler.Input.Touch` owns:

- `TouchInputDevice : InputDevice`;
- stable contact ID for the life of each contact;
- down, move, up, cancel, enter/leave, and capture-lost transitions;
- position, contact rectangle, pressure when available, confidence, primary
  contact, and in-range/in-contact state; and
- simultaneous contact limits and device capabilities.

Pinch, rotate, pan, inertia, tap, and long-press are gestures above raw touch.
They may later live in a separate interaction component.

### 8.6 Pen

`Broiler.Input.Pen` owns:

- `PenInputDevice : InputDevice`;
- stable pointer/contact identity;
- tip, barrel buttons, eraser, hover, and in-contact state;
- pressure, X/Y tilt, rotation/twist, contact geometry, and confidence when
  supported; and
- proximity, down, move, up, cancel, and capture-lost transitions.

Missing hardware capabilities produce absent values or capability flags, not
fabricated constants that appear measured.

### 8.7 Gamepad

`Broiler.Input.Gamepad` owns:

- `GamepadInputDevice : InputDevice`;
- connected controller descriptors and user association when available;
- button, D-pad, trigger, and stick state;
- polling cadence and timestamped state snapshots;
- dead-zone metadata/options without hard-coding one game genre’s policy; and
- added, removed, battery/capability change, and packet/state change semantics.

Vibration and other force feedback are output. They may be added later through a
separate capability or output component, but the first input contract does not
mix commands into state acquisition.

## 9. Windows implementation strategy

### 9.1 Shared Windows host boundary

Keyboard, mouse, touch, and pen depend on a window/message source for focused UI
input. `Broiler.Input.Windows` defines a narrow borrowed-host contract with:

- an externally owned HWND or equivalent native message source;
- dispatcher/UI-thread affinity;
- message subscription/unsubscription;
- focus and DPI/coordinate conversion notifications; and
- deterministic notification before the native host is destroyed.

The Input component never creates or destroys the host window. The first host
adapter is supplied by `Broiler.Graphics.Windows`; a WPF adapter can be added by
the application without adding WPF to the Input abstractions.

Raw input registration is coordinated process-wide because Windows permits only
one target window per raw-input device class for a process. Device-specific
assemblies must therefore acquire a registration lease through one coordinator
rather than racing independent `RegisterRawInputDevices` calls. Microsoft’s Raw
Input documentation also distinguishes focused device-independent messages from
registered `WM_INPUT`, and notes that raw input can identify multiple devices.

Background raw input is disabled by default. Enabling it is a separate explicit
option with prominent documentation and diagnostics. Low-level keyboard/mouse
hooks are not part of the baseline.

### 9.2 Keyboard on Windows

The baseline uses the focused Win32 keyboard message stream for semantic keys
and text, with optional Raw Input for physical device identity and scan-level
data.

Required message/API coverage includes:

- key down/up and system-key transitions without swallowing OS-reserved behavior;
- repeat count, previous state, transition, scan code, and extended-key bits;
- current modifier state including left/right distinctions where available;
- `WM_CHAR`/Unicode scalar handling, surrogate pairing, dead-character messages,
  and keyboard-layout changes; and
- a staged IME composition path rather than claiming complete international text
  support from `WM_CHAR` alone.

Windows documentation explicitly distinguishes keystrokes from characters and
shows that IMEs and dead keys can produce text that does not map one-to-one to key
transitions. That distinction is a contract requirement, not an implementation
detail.

### 9.3 Mouse, touch, and pen on Windows

The Windows pointer stack is the primary source for touch and pen, preserving
pointer ID, device type, contact, pressure, geometry, tilt, primary state, and
cancellation where supplied. Mouse retains compatibility with existing
`WM_MOUSE*` behavior while adding horizontal wheel and X buttons.

Raw Input is optional for high-rate relative mouse motion and physical mouse
identity. It must not cause duplicate semantic and raw transitions to appear as
two logical clicks.

The adapter must define:

- DPI-aware screen-to-client conversion;
- pointer history/coalescing rules;
- capture and capture-lost behavior;
- cancellation on device removal, display changes, desktop lock, or OS cancel;
- ownership of native pointer-message cleanup; and
- whether compatibility mouse messages generated from touch/pen are suppressed
  to prevent duplication.

### 9.4 Camera on Windows

The first camera implementation uses Windows Media Foundation device
enumeration and a Source Reader-based capture path unless the Phase 0 spike
demonstrates a blocking privacy or frame-source requirement that mandates
`MediaCapture`/`MediaFrameReader` for a specific camera class.

The Media Foundation baseline is selected because Microsoft documents:

- video capture device enumeration through
  `MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID` and
  `MFEnumDeviceSources`;
- stable symbolic-link metadata distinct from friendly display names; and
- the Source Reader as the normal higher-level mechanism for receiving capture
  frames.

The implementation must centralize Media Foundation startup/shutdown, COM
apartment rules, source activation, asynchronous reads, media-type negotiation,
device shutdown, and callback teardown. It may use Direct3D-backed frames
internally, but the first platform-neutral contract cannot require a D3D device.

`MediaCapture`/`MediaFrameReader` remains a deliberate second provider candidate
for synchronized color/depth/infrared frame sources or features not cleanly
available through the baseline. It must be a named provider choice, not a silent
behavior switch.

### 9.5 Microphone on Windows

The first microphone implementation uses WASAPI through Core Audio endpoint
enumeration and an event-driven shared-mode capture client. Microsoft documents
WASAPI as the endpoint data-flow API and `IAudioCaptureClient` as the packet
reader for captured audio.

The implementation owns:

- endpoint enumeration and default-role change notifications;
- shared-mode format negotiation and explicit conversion policy;
- event-driven buffer wakeup and packet draining;
- device position/QPC timestamp correlation;
- silence/discontinuity flags;
- endpoint invalidation and hot-plug handling; and
- COM/callback/thread teardown.

Exclusive mode is opt-in and not required for the first milestone. Loopback
capture is system-output capture, not microphone input, and is excluded from the
initial Microphone assembly.

### 9.6 Gamepad on Windows

The first provider uses XInput for the standard controller baseline. XInput is
poll-based, supports up to four compatible controllers, and provides standard
buttons, sticks, and triggers. The implementation polls on a caller-selected or
default cadence, avoids publishing unchanged snapshots unless requested, and
normalizes reconnect behavior.

`Windows.Gaming.Input` is a later provider candidate for broader device/user and
capability coverage. It must not be merged invisibly with XInput when doing so
would duplicate one physical controller.

### 9.7 Windows support matrix

Each Windows implementation assembly declares its own minimum supported Windows
version based on both the native API and the supported .NET runtime. The project
must not infer support merely because an API existed in an older Windows SDK.

Before the first preview, an ADR records:

- target framework monikers and `SupportedOSPlatform` annotations;
- minimum supported client versions per device kind;
- packaged versus unpackaged desktop behavior;
- x64/arm64 coverage;
- COM apartment and UI-thread expectations; and
- CI/hardware-lab coverage for each supported combination.

## 10. Registration and application composition

Implementation selection is explicit. The application composition root creates
the providers it needs and passes abstractions to consumers.

The default design uses an immutable typed provider set. It does not use:

- reflection-based assembly scanning;
- “first loaded assembly wins” behavior;
- mutable global provider state;
- a service locator hidden inside `InputDevice`; or
- module initializer registration.

A provider reports a stable provider ID and capability set. If multiple Windows
providers exist for one kind, selection is configured by the application or by
an explicit ordered policy with observable reasoning.

Fake and replay providers implement the same typed provider contracts. They are
first-class test tools, not special branches inside production devices.

## 11. Privacy, consent, and security

Camera, microphone, raw keyboard, and background input are privacy-sensitive.
The following requirements apply before public integration:

1. Enumeration never starts capture.
2. Capture starts only after an explicit application call.
3. The Windows provider reports OS privacy denial distinctly from missing or busy
   hardware.
4. The application/browser layer owns user prompts, origin grants, revocation,
   persistence, and visible capture UI.
5. Input exposes enough state for that layer to stop sessions immediately when
   permission or document/activity state changes.
6. No keyboard text, camera frame, microphone sample, or raw report is written to
   logs, crash telemetry, or exception messages by default.
7. Background raw keyboard/mouse input is opt-in, foreground-scoped by default,
   and covered by dedicated security review.
8. Device identifiers are treated as potentially fingerprinting information.
   Browser-facing code maps them to origin-scoped identifiers; Input itself does
   not expose native symbolic links to web content.
9. Buffer lengths, native structure sizes, format metadata, and frame strides are
   validated before allocation or copy.
10. Stop/dispose promptly releases camera and microphone native sources so OS
    indicators and exclusive device claims do not linger.

## 12. Integration boundaries

### 12.1 Graphics

Graphics owns windows, render surfaces, coordinate transforms needed for drawing,
and presentation. It does not own abstract input events after migration.

The temporary migration adapter follows this flow:

```text
Broiler.Graphics.Windows HWND/message source
  -> Broiler.Input.<Kind>.Windows
  -> platform-neutral typed input
  -> application/browser adapter
```

Graphics may continue to expose compatibility callbacks for one deprecation
window. Those callbacks adapt from Input; they must not remain a second native
message parser.

Camera preview follows a different flow:

```text
CameraInputDevice -> CameraFrame lease -> application preview adapter
                  -> graphics upload -> renderer-owned image/surface
```

Input does not reference Graphics to make that flow possible.

### 12.2 HTML, DOM, and browser behavior

The HTML/browser adapter is responsible for:

- focus and event-target selection;
- hit testing;
- DOM `KeyboardEvent`, `MouseEvent`, `PointerEvent`, `TouchEvent`, and wheel
  construction;
- event propagation, cancellation, default actions, and synthetic-event rules;
- CSS-pixel and viewport coordinate conversion;
- scrolling, text editing, selection, drag behavior, and shortcuts; and
- `getUserMedia` constraints, permission by origin, track lifecycle, and page
  visibility policy.

Input only supplies normalized device observations and capture samples. It does
not know which DOM node receives them.

### 12.3 Media

The boundary is:

| Responsibility | Input | Media |
|---|---:|---:|
| Enumerate/open live camera or microphone | Yes | No |
| Capture timestamped raw frames/samples | Yes | No |
| Decode files/network streams | No | Yes |
| Encode captured samples | No | Yes |
| Container mux/demux | No | Yes |
| Playback and codec selection | No | Yes |
| Optional buffer conversion adapters | Optional interop package | Optional interop package |

Camera and microphone define minimal capture buffer models until Media has
stable compatible frame/sample primitives. A later interop assembly may perform
zero-copy adaptation where possible. Neither component may introduce a reverse
reference that creates a cycle.

### 12.4 Application chrome and native controls

WPF and native edit controls should continue using their framework’s text,
accessibility, caret, and IME behavior unless there is a concrete reason to
replace it. Broiler.Input is most valuable for the custom-rendered browser
surface and device capture; it should not degrade mature native control input.

## 13. Threading, ordering, and teardown

### 13.1 Thread ownership

- Focused window input originates on the host UI/message thread.
- Consumers choose an explicit dispatcher or consume on the source thread.
- Camera and microphone native callbacks never execute arbitrary consumer work
  while holding COM/native locks.
- High-throughput samples enter bounded queues and are consumed asynchronously.
- Gamepad polling uses one owned scheduler per provider, not one unmanaged thread
  per controller.

### 13.2 Ordering

Within one device/session, transitions are delivered in source order. Events from
different devices are not assigned a fictional total order; their monotonic
timestamps allow consumers to correlate them.

Coalescing is allowed for movement/state updates only when:

- press/release, contact start/end, cancellation, and discontinuity boundaries
  are retained;
- the policy is documented; and
- diagnostics expose coalesced/dropped counts.

### 13.3 Teardown invariants

Every Windows provider must prove:

- native callbacks are detached before managed state is freed;
- no callback uses an HWND after host destruction notification;
- Raw Input registrations are released without disrupting another active lease;
- Media Foundation sources are shut down;
- WASAPI clients stop before COM objects are released;
- pending asynchronous readers complete exactly once;
- outstanding pooled buffers remain valid until their lease is disposed; and
- process shutdown does not depend on finalizers.

## 14. Testing strategy

### 14.1 Contract tests

Every typed abstraction has reusable provider/device contract tests covering:

- stable enumeration snapshots;
- duplicate IDs and display-name collisions;
- open/start/stop/restart/dispose behavior;
- cancellation at every asynchronous transition;
- removal during open and capture;
- fault propagation and recovery;
- event ordering and no callbacks after dispose; and
- capability/format validation.

Every concrete Windows provider must pass the relevant contract suite.

### 14.2 Deterministic fake and replay tests

Fakes must support scripted:

- device add/remove/default changes;
- key, text/composition, mouse, touch, pen, and gamepad timelines;
- camera frames and microphone buffers with timestamps;
- permission denial, device busy, format changes, discontinuities, and faults;
- slow consumers and queue overflow; and
- clock advancement without wall-clock sleeps.

Recorded replay fixtures contain synthetic/non-sensitive input only.

### 14.3 Windows integration tests

Automated Windows tests use a hidden test HWND/message source where possible and
cover:

- focused keyboard/text message translation;
- mouse buttons, X buttons, horizontal/vertical wheel, capture, and leave;
- Raw Input registration lease conflicts;
- pointer cancellation and DPI coordinate conversion;
- COM initialization and teardown on supported apartment models;
- endpoint/camera enumeration without starting capture; and
- no native callbacks after disposal.

Input injection can validate the semantic message path but does not prove
physical Raw Input, camera, microphone, or controller behavior.

### 14.4 Hardware lab tests

A small labeled Windows hardware matrix is required for release candidates:

- two simultaneous keyboards and mice for source identity;
- high-polling-rate mouse;
- touch screen with multi-contact;
- pressure/tilt-capable pen;
- built-in and USB cameras, plus device removal during capture;
- built-in, USB, and communications-default microphones;
- one and multiple XInput controllers; and
- privacy-disabled, busy-device, sleep/resume, lock/unlock, and hot-plug cases.

Tests that require hardware are categorized and skipped with an explicit reason
when the device is absent; they are never reported as ordinary green unit tests.

### 14.5 Performance and soak tests

Release gates include:

- bounded managed allocation during sustained camera/microphone capture;
- stable memory over multi-hour start/stop and hot-plug loops;
- no UI-thread blocking from frame/audio consumers;
- measured event-to-consumer latency for keyboard/mouse;
- drop/discontinuity counters under deliberately slow consumers; and
- no leaked handles, COM objects, registrations, or capture indicators.

Numeric budgets are frozen after the first representative prototype rather than
invented without measurements.

## 15. Delivery roadmap

### Phase 0 - Architecture decisions and repository preparation

Deliverables:

- Create the standalone component repository and aggregate-workspace reference.
- Record ownership, package naming, semantic-versioning, and project-reference
  policy across the top-level and nested submodules.
- Freeze the abstract class naming and direct-inheritance rule.
- Write ADRs for Windows support matrix, event/stream delivery, buffer ownership,
  clock domain, and Media interop boundary.
- Prototype, without committing public API, Raw Input registration ownership,
  Media Foundation camera enumeration/frame reads, and WASAPI event-driven
  capture.
- Inventory all existing Graphics/Application input call sites and behavior tests.

Exit criteria:

- No unresolved dependency cycle.
- Each public type has one intended owning assembly.
- Camera and microphone spikes demonstrate clean start/stop and device removal.
- The repository/package topology can be updated atomically.

### Phase 1 - Shared core, Windows host, and testing foundation

Deliverables:

- Specify `InputDevice`, descriptors, lifecycle states, common faults,
  diagnostics, monotonic timing, and typed provider conventions.
- Specify the narrow Windows host/message-source and Raw Input registration lease
  contracts.
- Establish deterministic fake clocks, fake providers, and reusable contract
  tests.
- Add package metadata, API compatibility checks, analyzers, and CI matrices.

Exit criteria:

- Core contracts build without Windows references.
- A no-hardware fake provider proves lifecycle, cancellation, removal, and
  bounded-delivery semantics.
- Windows support code contains no concrete device behavior.

### Phase 2 - Keyboard and mouse Windows parity extraction

Deliverables:

- Specify Keyboard and Mouse abstraction assemblies and Windows providers.
- Move native message translation out of `Direct2DWindow` into the new Windows
  providers behind the borrowed host contract.
- Preserve current key, text, pointer, button, leave, and wheel behavior.
- Add missing key metadata, X buttons, horizontal wheel, coordinate-space labels,
  capture-lost state, and device hot-plug reporting.
- Add a compatibility adapter for existing `BWindow` callbacks.
- Migrate `Broiler.App.Graphics` to consume Input abstractions directly.

Exit criteria:

- Existing browser interaction tests remain behaviorally equivalent.
- Graphics no longer parses input-native messages in its final path.
- No duplicate click/key event occurs when semantic and Raw Input are both
  enabled.
- Keyboard and mouse work without referencing Camera/Microphone assemblies.

### Phase 3 - Keyboard/text and raw-input hardening

Deliverables:

- Complete Unicode scalar/surrogate handling and dead-key behavior.
- Add layout-change and left/right modifier coverage.
- Define and implement the first IME composition milestone or explicitly mark
  unsupported composition states.
- Add optional physical keyboard/mouse identity via Raw Input.
- Add high-rate raw mouse buffering and coalescing metrics.
- Complete foreground/background privacy review.

Exit criteria:

- International layout and dead-key fixtures pass.
- System shortcuts are not swallowed by default.
- Background capture cannot be enabled accidentally.
- Multiple physical devices can be distinguished when raw mode is requested.

### Phase 4 - Microphone Windows capture

Deliverables:

- Specify Microphone abstraction, format model, audio buffer lease, provider, and
  session options.
- Implement endpoint enumeration/default roles and event-driven WASAPI shared-
  mode capture.
- Add bounded delivery, device/QPC timestamps, silence/discontinuity reporting,
  and device invalidation.
- Surface OS permission/privacy, busy, and unsupported-format failures.
- Add synthetic contract tests and labeled microphone hardware tests.

Exit criteria:

- Sustained capture has bounded memory and no routine discontinuities under the
  measured baseline workload.
- Default-device changes are observable without silently replacing an explicitly
  selected endpoint.
- Stop/dispose releases the endpoint and produces no later callback.
- No encoder or playback dependency is introduced.

### Phase 5 - Camera Windows capture

Deliverables:

- Specify Camera abstraction, capability/format model, frame planes, and frame
  lease ownership.
- Implement Media Foundation enumeration, activation, Source Reader negotiation,
  and asynchronous frame delivery.
- Add bounded latest-frame preview behavior plus an explicit loss-sensitive mode.
- Handle rotation/color metadata, device removal, format change, privacy denial,
  and source shutdown.
- Add an application-level preview adapter without introducing an Input-to-
  Graphics reference.
- Evaluate `MediaFrameReader` as a separately named provider for multi-source or
  depth/infrared requirements.

Exit criteria:

- Built-in and USB cameras enumerate with non-unique names but stable opaque IDs.
- Negotiated format is reported exactly and unsupported constraints fail clearly.
- Slow consumers do not grow memory without bound.
- Camera indicators/resources are released promptly after stop/dispose.

### Phase 6 - Touch and pen Windows input

Deliverables:

- Specify Touch and Pen abstractions and Windows pointer-stack providers.
- Preserve contact IDs and expose cancellation/capture-lost state.
- Add touch geometry/pressure/confidence and pen pressure/tilt/eraser/barrel data
  when supported.
- Prevent compatibility mouse messages from duplicating touch/pen actions.
- Connect the application adapter to future DOM Pointer/Touch event work without
  putting DOM types in Input.

Exit criteria:

- Multi-contact ordering and cancellation contract tests pass.
- Unsupported pressure/tilt remains absent rather than fabricated.
- DPI and client-coordinate tests pass across monitor changes.
- Pen/touch removal or cancel never leaves a stuck contact.

### Phase 7 - Gamepad Windows input

Deliverables:

- Specify Gamepad abstraction and state snapshot semantics.
- Implement XInput discovery/polling, reconnect, dead-zone metadata, and change
  suppression.
- Test multiple controllers and disconnect/reconnect.
- Record an ADR before adding `Windows.Gaming.Input` or output/haptics.

Exit criteria:

- Polling stops completely when no consumer/session is active.
- Unchanged states do not create an unbounded event rate.
- Reconnection has deterministic identity/state behavior.
- No haptic output API is smuggled into the input-only baseline.

### Phase 8 - Browser/application integration and compatibility removal

Deliverables:

- Complete browser adapters for focus, hit testing, DOM event construction, and
  default actions.
- Add origin-scoped camera/microphone permission and track lifecycle outside
  Input.
- Migrate all custom-rendered application surfaces.
- Deprecate, then remove `BInputEvents`, `BVirtualKey`, and native input parsing
  from Graphics after the compatibility window.
- Publish migration guidance for host applications.

Exit criteria:

- Graphics owns no platform-neutral input event model.
- Browser policy has no reverse dependency into Input.
- Current interaction behavior and new device integrations pass end-to-end tests.
- Compatibility APIs have a documented removal version and replacement mapping.

### Phase 9 - Stabilization and cross-platform readiness

Deliverables:

- Freeze public API after preview feedback and compatibility review.
- Publish trimming/AOT annotations and native dependency documentation.
- Run hardware soak, privacy, accessibility, and failure-injection gates.
- Document the provider checklist for a future Linux/macOS implementation.
- Consider convenience, replay, A/V synchronization, and Media adapter packages
  only after the core packages are stable.

Exit criteria:

- All stable packages meet API, diagnostics, documentation, and lifecycle gates.
- No Windows type appears in the abstraction API baseline.
- A second-platform design review can map the contracts without pretending to be
  Win32.

## 16. Package and compatibility policy

- Each runtime assembly ships as its own package with the same name.
- Typed Windows packages depend on exact compatible major versions of their
  abstraction and shared Windows support packages.
- Abstraction packages do not depend on implementations.
- Preview versions may revise contracts; stable versions follow semantic
  versioning and API compatibility checks.
- Type forwarding or a narrow adapter package may preserve source/binary
  compatibility for moved Graphics types when practical.
- Obsolete compatibility members include a replacement and removal milestone.
- Native Windows requirements and privacy-sensitive behavior are visible in
  package descriptions, not buried in implementation notes.

## 17. Observability

Each provider/session emits structured, opt-in diagnostics for:

- enumeration duration and device count;
- open/start/stop/close state transitions;
- selected/negotiated format;
- event/sample counts and queue depth;
- coalesced, dropped, silent, and discontinuous sample counts;
- hot-plug/default-device changes;
- native error category and sanitized code; and
- callback/processing latency histograms.

Diagnostics exclude payload content and native device paths by default. Stable
opaque IDs may be replaced with per-process diagnostic aliases.

## 18. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Root base class becomes a universal device framework | Brittle API and type checks | Keep `InputDevice` lifecycle-only; put payload behavior in typed derived classes |
| Too many assemblies create maintenance overhead | Versioning/release complexity | Shared build conventions, synchronized releases, optional package-only bundle later |
| Raw Input registration is process-global per class | Providers interfere or steal messages | One lease/coordinator in `Broiler.Input.Windows`; integration tests for conflicts |
| Semantic and raw paths duplicate input | Double clicks/keys | Correlate paths and make raw/semantic delivery modes explicit |
| Text is treated as keys | Broken IME/dead-key/international input | Separate key, text, and composition contracts from the beginning |
| Slow sample consumer grows memory | OOM and latency | Bounded queues, explicit drop/backpressure policy, pooled leases, counters |
| Native buffer outlives callback | Use-after-free/corruption | Explicit leases/copies and teardown tests |
| Camera/mic privacy behavior varies by app packaging | Confusing failures or silent denial | Support-matrix ADR, permission status categories, packaged/unpackaged tests |
| Camera and microphone drift prevents synchronized A/V | Bad recordings | Timestamp clock contract now; separate aggregate-session package later |
| Device IDs change across reboot/driver updates | Broken persisted selection | Opaque IDs, fallback policy in application, never use display name as identity |
| Device is removed mid-callback | Deadlock/stuck session | Removal state transition, callback detachment order, fault injection |
| Graphics/Input repository revisions drift | Broken build and duplicate types | Canonical checkout/package policy and coordinated migration releases |
| Generic HID leaks unsafe/raw reports into browser | Security and fingerprinting exposure | Defer generic HID; require a separate threat model and explicit typed access |
| XInput and Windows.Gaming.Input see the same controller | Duplicate logical devices | One provider by default; provider-specific IDs and deduplication ADR before combining |

## 19. Required ADRs before API stabilization

1. Assembly names, repository topology, and release version alignment.
2. Exact `InputDevice` lifecycle and idempotency rules.
3. Provider discovery, default-device, and hot-plug semantics.
4. Callback versus asynchronous-stream shape for discrete input.
5. Camera/microphone buffer ownership and pool policy.
6. Monotonic clock domain and cross-device timestamp correlation.
7. Queue bounds and default drop/backpressure policy per kind.
8. Canonical key identity, physical-key mapping, and native metadata.
9. Text composition/IME baseline.
10. Pointer coordinate spaces, DPI, capture, cancellation, and coalescing.
11. Raw Input process-wide registration ownership and background policy.
12. Camera provider baseline: Media Foundation Source Reader versus named
    `MediaFrameReader` provider.
13. Microphone format conversion and shared/exclusive-mode policy.
14. Windows/.NET support matrix and packaged/unpackaged privacy behavior.
15. Input/Media buffer interop and dependency direction.
16. Graphics compatibility/type-forwarding window.
17. Gamepad provider choice and future haptics boundary.
18. Trimming, AOT, COM interop, and native dependency strategy.

## 20. Future device kinds

The component can later add more typed abstraction/implementation pairs without
changing `InputDevice`. Candidates include:

- generic HID and accessibility switches;
- MIDI instruments;
- barcode/document scanners;
- depth, infrared, motion-tracking, and eye-gaze devices;
- accelerometer, gyroscope, compass, and other sensors;
- location receivers;
- biometric devices, subject to a separate security model; and
- specialized simulation/racing/flight controls beyond standard gamepads.

Each candidate requires a real use case, privacy review, capability model,
Windows provider choice, fixtures/hardware tests, and its own assembly pair.

Clipboard, drag/drop, networking, speech recognition, screen capture, speakers,
and haptics are not device kinds in this component merely because applications
may experience them as “input” or “interaction.”

## 21. Definition of done for the first stable release

The first stable `Broiler.Input` release is complete when:

- `InputDevice` and the Keyboard, Mouse, Camera, and Microphone abstract device
  classes are stable and live in separate assemblies;
- each of those four kinds has a separately packaged Windows implementation;
- shared Windows support contains no concrete device logic;
- keyboard/mouse behavior is migrated out of Graphics with compatibility and
  international-text tests;
- camera/microphone capture is bounded, timestamped, permission-aware, hot-plug
  safe, and hardware-tested;
- fake/replay providers cover all public lifecycle and failure contracts;
- no platform-native type leaks into an abstraction assembly;
- no sensitive payload is logged by default;
- all stop/dispose paths prove no late callbacks or native-resource leaks; and
- Touch, Pen, and Gamepad are either delivered under the same quality bar or
  remain explicitly preview packages without blocking the four-kind stable core.

## 22. Primary Windows references

The Windows implementation plan is grounded in the following Microsoft platform
documentation:

- [Raw Input overview](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)
- [RegisterRawInputDevices](https://learn.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-registerrawinputdevices)
- [Keyboard input](https://learn.microsoft.com/en-us/windows/win32/learnwin32/keyboard-input)
- [WM_DEADCHAR](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-deadchar)
- [Handle pointer input](https://learn.microsoft.com/en-us/windows/apps/develop/input/handle-pointer-input)
- [WM_POINTERUP and pointer lifetime/cancellation](https://learn.microsoft.com/en-us/windows/win32/inputmsg/wm-pointerup)
- [Audio/video capture in Media Foundation](https://learn.microsoft.com/en-us/windows/win32/medfound/audio-video-capture-in-media-foundation)
- [MediaFrameReader frame processing](https://learn.microsoft.com/en-us/windows/apps/develop/camera/process-media-frames-with-mediaframereader)
- [Core Audio APIs](https://learn.microsoft.com/en-us/windows/win32/api/_coreaudio/)
- [WASAPI overview](https://learn.microsoft.com/en-us/windows/win32/coreaudio/wasapi)
- [Getting started with XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput)
- [Windows.Gaming.Input gamepad overview](https://learn.microsoft.com/en-us/windows/uwp/gaming/gamepad-and-vibration)

These sources select the initial Windows mechanisms; the public Broiler.Input
contracts remain platform-neutral and must be validated again when a second
operating-system provider is designed.

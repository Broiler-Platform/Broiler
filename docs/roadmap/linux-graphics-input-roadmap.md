# Linux Second-OS Roadmap for Broiler.Graphics and Broiler.Input

**Status:** Phase 7 Linux preview hardening first slice implemented  
**Date:** 2026-07-06  
**Scope:** Add Linux as the second operating system for `Broiler.Graphics` and
`Broiler.Input`. Graphics targets Mesa-backed OpenGL and Vulkan. Input targets
keyboard and mouse first, with direct Linux `evdev` event-device support.

## 1. Executive Decision

Linux becomes the second Broiler platform through the same pattern already used
by the Windows work:

- platform-neutral contracts stay in `Broiler.Graphics`, `Broiler.Input`,
  `Broiler.Input.Keyboard`, and `Broiler.Input.Mouse`;
- Linux platform support lives in separately named implementation assemblies;
- applications select the backend/provider explicitly at the composition root;
- no Linux, Mesa, Wayland, X11, Vulkan, OpenGL, `evdev`, or file-descriptor type
  appears in platform-neutral public APIs.

Graphics should use Mesa through standard APIs, not private Mesa internals:

- OpenGL path: EGL plus OpenGL/OpenGL ES functions provided by Mesa drivers;
- Vulkan path: Vulkan loader plus the active Mesa Vulkan ICD;
- CI/headless path: Mesa software drivers, especially llvmpipe for OpenGL and
  lavapipe for Vulkan, where the host supports them.

Input should start with direct `/dev/input/event*` reads:

- keyboard key transitions and modifier state;
- mouse relative movement, buttons, and wheel;
- device discovery, hot-plug, removal, timestamps, and diagnostics;
- explicit permission/security behavior because event devices can expose global
  keyboard and mouse activity.

Text input, IME, touch, pen, gamepad, camera, microphone, gestures, and
libinput-style desktop policy are deferred. They need separate design work and
must not be smuggled into the first keyboard/mouse slice.

## 2. Current Repository Fit

### 2.1 Graphics

`Broiler.Graphics` already has a useful split:

- `Broiler.Graphics` targets `net10.0` and owns platform-neutral types such as
  `IBroilerRenderer`, `IBroilerSurface`, `BRenderList`, `BRenderCommand`,
  `BBitmap`, and `BCanvas`.
- `Broiler.Graphics.Windows` targets `net10.0-windows` and implements the first
  GPU/window backend with Direct2D, D3D11, DXGI, DirectWrite, HWND surfaces, and
  Win32 controls.

That shape can be extended instead of replaced. Linux backends should implement
the same renderer/surface contracts and reuse the managed bitmap/canvas/codecs
already available in the core.

The main Graphics cleanup needed for Linux is ownership: `BWindow` still exposes
input callbacks and `BInputEvents` still carries platform-shaped input values.
Linux should not deepen that coupling. The Linux window backend should create
windows and presentation surfaces; normalized keyboard and mouse events should
come from `Broiler.Input`.

### 2.2 Input

`Broiler.Input` is already close to the desired provider shape:

- `Broiler.Input` owns lifecycle, identity, diagnostics, timestamps, and device
  descriptors.
- `Broiler.Input.Keyboard` and `Broiler.Input.Mouse` own typed event contracts.
- `.Windows` implementation assemblies own Windows message and Raw Input paths.

Linux can follow the same dependency direction:

```text
Broiler.Input.Keyboard.Linux -> Broiler.Input.Keyboard -> Broiler.Input
Broiler.Input.Mouse.Linux    -> Broiler.Input.Mouse    -> Broiler.Input
Broiler.Input.Keyboard.Linux -> Broiler.Input.Linux    -> Broiler.Input
Broiler.Input.Mouse.Linux    -> Broiler.Input.Linux    -> Broiler.Input
```

`MouseMoveEvent` currently carries an `InputPoint`, not a relative-motion type.
The first Linux evdev mouse slice should use an explicit coordinate-space label,
for example `raw-relative-counts`, and record an ADR before adding new movement
event shapes.

## 3. Target Assembly Layout

### 3.1 Graphics Assemblies

```text
Broiler.Graphics/
  Broiler.Graphics
  Broiler.Graphics.Windows
  Broiler.Graphics.Linux
  Broiler.Graphics.Linux.OpenGL
  Broiler.Graphics.Linux.Vulkan
  Broiler.Graphics.Linux.Tests
  Broiler.Graphics.Linux.Demo
```

Responsibilities:

| Assembly | Responsibility |
|---|---|
| `Broiler.Graphics` | Backend-neutral render, image, surface, geometry, color, text, and window contracts |
| `Broiler.Graphics.Linux` | Shared Linux display/window/event-loop/native-loading support, with no renderer-specific command replay |
| `Broiler.Graphics.Linux.OpenGL` | Mesa/EGL/OpenGL renderer and surfaces |
| `Broiler.Graphics.Linux.Vulkan` | Vulkan loader/ICD renderer and surfaces |
| `Broiler.Graphics.Linux.Tests` | Linux backend contract, offscreen, software-driver, and architecture tests |
| `Broiler.Graphics.Linux.Demo` | Small Linux demo that can run with OpenGL or Vulkan |

The implementation assemblies should target `net10.0`, not a fake Linux TFM.
Linux support is expressed with runtime checks, supported-platform annotations,
RIDs for packaging, and clear diagnostics when native dependencies are absent.

### 3.2 Input Assemblies

```text
Broiler.Input/
  Broiler.Input
  Broiler.Input.Linux
  Broiler.Input.Keyboard
  Broiler.Input.Keyboard.Linux
  Broiler.Input.Mouse
  Broiler.Input.Mouse.Linux
```

Responsibilities:

| Assembly | Responsibility |
|---|---|
| `Broiler.Input.Linux` | `evdev` file handling, native ioctls, polling, clock conversion, device discovery helpers, sanitized diagnostics |
| `Broiler.Input.Keyboard.Linux` | Keyboard provider/device built from Linux event devices |
| `Broiler.Input.Mouse.Linux` | Mouse provider/device built from Linux event devices |

`Broiler.Input.Linux` is a support assembly, not an all-devices bundle. It must
not contain concrete keyboard or mouse delivery logic beyond reusable event-file
parsing and Linux lifetime primitives.

## 4. Graphics Architecture

### 4.1 Backend Selection

Use explicit construction first:

```csharp
IBroilerRenderer renderer = new LinuxOpenGlRenderer(...);
IBroilerRenderer renderer = new LinuxVulkanRenderer(...);
```

A diagnostic/demo selector can come later:

```text
BROILER_GRAPHICS_LINUX_BACKEND=opengl|vulkan|auto
```

The default should be conservative:

1. prefer Vulkan only after swap-chain, device-lost, and software-driver CI
   gates are stable;
2. otherwise choose OpenGL/EGL as the first broadly compatible Linux path;
3. provide clear failure diagnostics listing missing native libraries,
   unsupported display server, or unavailable Mesa driver.

### 4.2 Window-System Strategy

Start with desktop Linux:

- Wayland primary for modern desktops;
- X11/XCB fallback for environments where Wayland is unavailable;
- headless/offscreen support for CI and image tests.

Direct DRM/KMS and GBM-only fullscreen are out of scope for the first release.
They are useful later for appliance or kiosk environments but would complicate
permissions, input seats, and presentation too early.

### 4.3 OpenGL Path

The OpenGL backend should use EGL where possible:

- dynamically load `libEGL.so.1`;
- create a display connection for Wayland, X11/XCB, or surfaceless/headless;
- create an OpenGL 3.3 core context or OpenGL ES 3.0 context, based on what the
  Mesa driver exposes;
- create window-backed EGL surfaces and offscreen framebuffer objects;
- upload `BPixelBuffer`/`BBitmap` data as textures;
- present through `eglSwapBuffers`;
- map device/context loss to `BDeviceLostException` or a Linux-specific
  diagnostic wrapping the existing Graphics error model.

First bring-up should be a CPU-present path:

```text
BRenderList -> existing BImageRenderer/BCanvas -> RGBA pixels
            -> OpenGL texture upload -> fullscreen textured quad -> window
```

This gives Linux a working renderer quickly and keeps pixel behavior close to
the existing managed raster path. Direct GPU replay of `BRenderCommand` can
follow after correctness is visible.

### 4.4 Vulkan Path

The Vulkan backend should use the standard Vulkan loader:

- dynamically load `libvulkan.so.1`;
- create an instance with `VK_KHR_surface` plus the needed Wayland/XCB surface
  extension;
- enumerate physical devices and prefer hardware Mesa ICDs, then lavapipe for
  test/headless fallback when allowed;
- create swap chains, images, command pools, command buffers, fences, and
  semaphores with deterministic disposal;
- implement resize/recreate and out-of-date swap-chain handling;
- expose selected device name, driver name, API version, and presentation mode
  in diagnostics.

First bring-up should mirror the OpenGL path:

```text
BRenderList -> existing BImageRenderer/BCanvas -> RGBA pixels
            -> staging upload -> Vulkan image -> textured present pass
```

GPU-native command replay is a later milestone. A Vulkan command renderer is
valuable, but a first Linux release should prove windowing, Mesa ICD selection,
swap-chain lifetime, and parity before writing a larger 2D pipeline.

### 4.5 Text and Images

Linux graphics must not depend on DirectWrite. The first backend should:

- reuse the managed text/font path already present in `Broiler.Graphics`;
- support explicit local font files and bundled test fonts first;
- add fontconfig-based system font discovery only after a boundary decision;
- render text through the CPU-present path initially;
- move to glyph atlases for OpenGL/Vulkan acceleration later.

Image decode remains backend-neutral. Linux renderers receive decoded pixels or
existing `BImageHandle` uploads; they do not own image codec selection.

### 4.6 Controls

Win32 child controls used by `Broiler.Graphics.Windows` do not have a direct
Linux equivalent. For Linux first release:

- keep `BEditControl`, `BButtonControl`, and `BLabelControl` unsupported or
  route them through a future Broiler.UI/control layer;
- do not introduce GTK/Qt dependencies just to match Win32 child controls;
- make demos and tests use render-list UI until the cross-platform UI layer owns
  controls.

## 5. Input Architecture

### 5.1 Evdev Scope

The first Linux input provider reads Linux event devices directly:

```text
/dev/input/event* -> input_event records -> typed Broiler keyboard/mouse events
```

Supported first:

- `EV_KEY` keyboard key up/down/repeat;
- left/right modifier state and common key location mapping;
- `EV_REL` mouse relative X/Y movement;
- `EV_KEY` mouse buttons;
- `REL_WHEEL`, `REL_HWHEEL`, and high-resolution wheel variants when present;
- `EV_SYN` batching and `SYN_DROPPED` recovery;
- device add/remove/change observation.

Deferred:

- layout-aware text input;
- dead keys and IME;
- touchpad gestures, acceleration, palm rejection, natural scrolling;
- touch, pen, gamepad, and generic HID;
- global shortcuts or input injection.

### 5.2 Device Discovery

Use a layered discovery strategy:

1. Prefer udev metadata when available, including properties such as keyboard or
   mouse classification.
2. Fall back to scanning `/dev/input/event*` and querying capabilities with
   evdev ioctls.
3. Use `/sys/class/input` to relate event files, names, physical paths, and
   removal state.

Device IDs must be opaque. A practical first ID can combine sanitized Linux
identity data into a hash:

- event device major/minor;
- bus/vendor/product/version from `EVIOCGID`;
- physical path from sysfs or `EVIOCGPHYS`, if present;
- unique string from `EVIOCGUNIQ`, if present.

Raw paths should not be exposed by default diagnostics because they can reveal
hardware and seat topology.

### 5.3 Event File Reading

The Linux support assembly should provide:

- `open` with `O_RDONLY`, `O_CLOEXEC`, and `O_NONBLOCK`;
- `SafeFileHandle` ownership and deterministic close;
- `poll` or `epoll` based read loops;
- binary parsing of `struct input_event`;
- retry handling for `EINTR` and nonblocking reads;
- removal handling for `ENODEV`, `EIO`, EOF, and udev removal notices;
- `EVIOCSCLOCKID` set to `CLOCK_MONOTONIC` where supported;
- fallback timestamp mapping when the kernel or permissions do not allow that
  clock selection.

The parser should be unit-testable without physical devices. Feed it recorded
or synthetic `input_event` byte sequences and assert typed Broiler events.

### 5.4 Keyboard Mapping

Keyboard first release should emit key transitions, not text:

- map Linux `KEY_*` codes to the existing `KeyboardKey` names used by Windows
  where possible, for example `KeyA`, `Digit1`, `Enter`, `Escape`, `ArrowLeft`;
- preserve native Linux key code in `NativeKeyCode`;
- set `ScanCode` to the evdev code unless an ADR chooses a different physical
  mapping;
- handle repeat value `2` as a down transition with repeat metadata;
- track modifier state from delivered events, including left/right variants;
- mark source as `InputEventSource.Raw`.

Text input requires layout, compose/dead-key, IME, and focus policy. The next
step should be an explicit `xkbcommon` milestone, not ad hoc ASCII conversion.

### 5.5 Mouse Mapping

Mouse first release should emit:

- movement from `REL_X` and `REL_Y`, batched until `EV_SYN`;
- coordinate space `raw-relative-counts` for relative movement;
- button transitions for left, right, middle, X1, and X2 where available;
- wheel events for vertical and horizontal wheel deltas;
- high-resolution wheel data converted to notches when the kernel provides it;
- current button state on move, button, and wheel events.

Window-relative positions are an integration concern. A Linux graphics window
can later provide focus, cursor position, scaling, and bounds so Input events can
be projected to `client-pixels` or `client-dip` without pretending raw evdev
movement is already a focused window coordinate.

### 5.6 Security and Permissions

Direct `evdev` is powerful. It can expose global keystrokes even when Broiler is
not focused. The first provider must make that visible:

- do not use setuid helpers;
- fail with `PermissionDenied` plus clear remediation when event files cannot be
  opened;
- require an explicit option acknowledging raw/background input behavior;
- allow the application to pause delivery when its window is not active;
- do not log typed text, key timelines, raw device paths, or unique hardware IDs
  by default;
- document common development options such as `input` group membership, udev
  rules, container device pass-through, or running under a seat broker.

Longer term, systemd-logind or seatd integration should be considered before
shipping a polished desktop package.

## 6. Delivery Roadmap

### Phase 0 - Decisions, Contracts, and Baselines

**Status:** Requirements baseline chosen in
[Linux Phase 0 Requirements Baseline](./linux-phase0-requirements-baseline.md).

Deliverables:

- Use the Phase 0 baseline as the first-preview support matrix:
  Ubuntu 24.04 LTS x64 as primary CI/development target, Ubuntu 22.04.4 LTS and
  Debian 12 x64 as compatibility smoke targets, `linux-x64` and `linux-arm64`
  self-contained publish RIDs, Wayland primary, X11/XCB fallback, Mesa OpenGL
  3.3 or OpenGL ES 3.0, Vulkan 1.2 plus WSI/swapchain, and direct evdev
  keyboard/mouse input.
- Approve the project names and dependency allowlists already listed in section
  3.
- Record ADRs for Linux native loading, Mesa backend selection, window-system
  support, evdev permissions, raw relative mouse coordinates, and keyboard text
  deferral.
- Add architecture tests proving Linux assemblies do not leak into neutral APIs.
- Add Linux build jobs for platform-neutral projects before native backend code.

Exit criteria:

- Phase 0 requirements baseline is accepted.
- Project graph is approved.
- Backends/providers have clear package names.
- No Windows-only project is required to build the neutral Linux plan.
- Security decision for direct event-device access is explicit.

### Phase 1 - Linux Scaffolding

**Status:** Scaffolded on 2026-07-06. Linux project shells, native dependency
probes, package-free scaffold tests, aggregate/component solution registration,
and Ubuntu 24.04 CI build/publish workflow are in place. Evdev delivery landed
in Phase 2; Mesa presentation remains Phase 3-4 work.

Deliverables:

- Add `Broiler.Graphics.Linux`, `Broiler.Graphics.Linux.OpenGL`,
  `Broiler.Graphics.Linux.Vulkan`, and Linux tests/demo projects.
- Add `Broiler.Input.Linux`, `Broiler.Input.Keyboard.Linux`, and
  `Broiler.Input.Mouse.Linux`.
- Add native library loading helpers with diagnostics for missing `libEGL`,
  `libGL`, `libvulkan`, Wayland, XCB, udev, and input device permissions.
- Add CI build matrix entries that compile Linux projects on Linux.

Exit criteria:

- Linux projects compile on Linux.
- Windows projects still compile on Windows.
- Neutral abstractions remain free of platform references.
- Missing native dependencies fail with actionable messages, not type-load
  crashes.

Phase 1 delivered:

- `Broiler.Graphics.Linux` with EGL/OpenGL/Vulkan/Wayland/XCB dependency probes.
- `Broiler.Graphics.Linux.OpenGL` and `.Vulkan` placeholder renderers with
  scoped dependency checks and explicit pending-implementation diagnostics.
- `Broiler.Graphics.Linux.Tests` and `.Linux.Demo`.
- `Broiler.Input.Linux` with udev and `/dev/input/event*` access diagnostics.
- `Broiler.Input.Keyboard.Linux` and `.Mouse.Linux` provider/device shells.
- `Broiler.Input.Linux.Tests`.
- `.github/workflows/linux-port-build.yml`, building/running scaffold tests
  and publishing a self-contained `linux-x64` demo artifact.

Those renderer placeholders have since been replaced by the Phase 3 OpenGL
preview and the Phase 4 Vulkan loader/device preview below.

### Phase 2 - Evdev Keyboard and Mouse MVP

**Status:** Implemented on 2026-07-06 for the first MVP. Direct evdev delivery
now covers sysfs capability discovery, nonblocking event-device reads, parser
tests, keyboard key transitions, mouse relative movement/buttons/wheel, raw
input acknowledgement, refresh-based add/remove observation, and a sanitized
diagnostic console. udev hot-plug, seat-broker integration, and text/IME remain
future work.

Deliverables:

- Implement event-device discovery and capability filtering.
- Implement nonblocking event-file read loop and parser.
- Implement keyboard key transition mapping.
- Implement mouse relative movement, buttons, and wheel mapping.
- Add synthetic parser tests and opt-in hardware tests.
- Add a small console diagnostic tool that lists devices and prints sanitized
  event summaries for development.

Exit criteria:

- One USB or built-in keyboard produces key down/up/repeat events.
- One USB or built-in mouse produces movement, button, and wheel events.
- Device removal ends delivery cleanly.
- Permission denial is reported as a stable Input fault category.
- Event callbacks stop before disposal returns.

Phase 2 delivered:

- `Broiler.Input.Linux` evdev constants, sysfs capability parser/discovery,
  native open/read/poll/ioctl bridge, fault mapping, 64-bit `input_event` parser,
  and read loop.
- `Broiler.Input.Keyboard.Linux` provider/device with raw opt-in, refresh-based
  device changes, and `EV_KEY` transition/modifier/repeat mapping.
- `Broiler.Input.Mouse.Linux` provider/device with raw opt-in, refresh-based
  device changes, and `EV_REL` movement plus button/wheel mapping.
- `Broiler.Input.Linux.Tests` synthetic parser, fake sysfs discovery, provider,
  keyboard mapping, and mouse mapping tests; hardware smoke is opt-in through
  `BROILER_LINUX_EVDEV_HARDWARE_TEST=1`.
- `Broiler.Input.Linux.Diagnostic` for sanitized device listing and explicitly
  acknowledged event-summary streaming.

### Phase 3 - OpenGL Mesa Present Path

**Status:** Implemented on 2026-07-06 as the OpenGL CPU-present preview. The
backend creates EGL/OpenGL pbuffer contexts where available, renders Broiler
lists through the managed `BImageRenderer`, uploads the RGBA frame into an
OpenGL texture/FBO, supports readback parity checks, and exposes an opt-in X11
window surface for Linux desktop smoke testing. Wayland-native windows,
software-driver CI with real llvmpipe execution, and GPU-native command replay
remain future work.

Deliverables:

- Implement EGL display/context creation for at least one desktop path.
- Implement offscreen FBO rendering and window surface presentation.
- Implement CPU-present texture upload from `BImageRenderer` output.
- Add resize, DPI scale, frame context, and device/context loss handling.
- Add llvmpipe-capable CI or an opt-in local test profile.

Exit criteria:

- Demo opens a Linux window and presents rendered content through Mesa OpenGL.
- `RenderToImage` works on Linux for smoke fixtures.
- Pixel output is compared against the managed CPU baseline within documented
  tolerances.
- Direct2D/Windows behavior is unchanged.

Phase 3 delivered:

- `LinuxOpenGlRenderer` CPU-present implementation with managed render-list
  replay, image-handle lifecycle delegated to `BImageRenderer`, and backend
  surface validation.
- `LinuxOpenGlSurface` pbuffer/offscreen surface with deterministic CPU fallback
  on non-Linux hosts or missing EGL/OpenGL.
- EGL/OpenGL native binding layer for context creation, pbuffer/window surfaces,
  texture upload, FBO attachment, blit, swap, and readback.
- `LinuxOpenGlX11WindowSurface` for opt-in X11/EGL window presentation from the
  Linux demo.
- Graphics Linux tests for dependency scoping, CPU-present pixel parity,
  bottom-up OpenGL pixel conversion, and fallback diagnostics.

### Phase 4 - Vulkan Mesa Present Path

Phase 4 first slice delivered:

- `LinuxVulkanRenderer` CPU-present implementation with managed render-list
  replay and image-handle lifecycle delegated to `BImageRenderer`.
- `LinuxVulkanSurface` offscreen surface with deterministic CPU fallback on
  non-Linux hosts or missing Vulkan loader/device support.
- Vulkan native binding layer for `vkCreateInstance`, physical-device
  enumeration, queue-family selection, logical-device creation, queue lookup,
  idle wait, and deterministic disposal.
- Vulkan 1.2 baseline enforcement before instance/device creation, matching the
  Phase 0 requirements baseline.
- Linux demo backend selection with `--vulkan` / `--backend=vulkan`, rendering
  the same smoke fixture as the OpenGL preview.
- Graphics Linux tests for Vulkan dependency scoping, CPU-present pixel parity,
  fallback diagnostics, and strict non-Linux behavior.

Deliverables:

- Implement Vulkan loader binding and instance/device selection. **Done for the
  first slice.**
- Implement Wayland or XCB surface creation for the selected first window path.
- Implement swap-chain creation, recreate on resize, and synchronized present.
- Implement CPU-present image upload and textured present pass.
- Add lavapipe-capable CI or an opt-in local test profile.

Exit criteria:

- Demo can switch to Vulkan and render the same render-list fixtures through the
  CPU-present preview.
- Swap-chain out-of-date and resize are deterministic.
- Selected loader/device diagnostics are visible; Mesa driver/ICD naming will
  become richer when physical-device properties are added.
- Vulkan resources are disposed without validation-layer leaks in debug runs.

### Phase 5 - Linux Window and Input Integration

Phase 5 first slice delivered:

- Linux demo composition root now references the Linux keyboard and mouse
  providers without adding Input references to graphics backend assemblies.
- `LinuxOpenGlX11WindowSurface` now exposes a non-blocking X11 event pump,
  focus state, focus-change notification, and close-request detection.
- `--window --enable-evdev-input` opens one readable keyboard and mouse through
  `Broiler.Input.Keyboard.Linux` and `Broiler.Input.Mouse.Linux` after explicit
  raw evdev acknowledgement.
- Raw event-device reads are started only while the X11 window is focused and
  are stopped again on focus loss, so background raw input delivery is paused.
- Typed Input keyboard/mouse events update demo render state; Escape requests
  demo exit, Space and mouse button/wheel events alter the accent color, and
  relative mouse motion moves an on-screen marker.
- The legacy migration bridge is documented as `Broiler.Input.Legacy` rather
  than a new graphics-owned `BInputEvents` path.

Deliverables:

- Add Linux demo wiring: create a window, choose OpenGL/Vulkan, open keyboard and
  mouse providers, and route typed Input events to the demo/application layer.
  **Done for the OpenGL/X11 preview path; Vulkan remains offscreen until WSI.**
- Add focus/activation hooks so raw evdev delivery can be paused or ignored when
  the app is inactive. **Done for the X11 preview window.**
- Add compatibility mapping from Input events to existing temporary `BWindow`
  callbacks only where needed. **Covered by existing `Broiler.Input.Legacy`;
  no new graphics-owned adapter was needed.**
- Document the migration path away from `BInputEvents`. **Started here; detailed
  UI/app migration remains a later cutover task.**

Exit criteria:

- Demo supports keyboard shortcuts and mouse interaction on Linux.
- Input still has no reference to Graphics.
- Graphics Linux code does not parse `/dev/input/event*` directly.
- The compatibility adapter is isolated and removable.

### Phase 6 - GPU-Native Render Command Replay

Phase 6 first slice delivered:

- `LinuxOpenGlNativeReplay` inspects render lists and accepts the first native
  subset: frame clear, opaque fill/stroke rectangles, and rectangular clips.
- OpenGL native replay executes accepted operations through `glScissor` and
  `glClear`, using the existing EGL/FBO presentation path and preserving the
  CPU-present fallback when native replay is unavailable or unsupported.
- Unsupported commands now produce explicit fallback diagnostics for translucent
  fills/strokes, rounded rectangles, image draws, text, and transforms.
- Native replay can be disabled with `LinuxOpenGlRendererOptions` when isolating
  the CPU-present path.
- The Linux demo prints native-replay inspection output for OpenGL surfaces,
  measures total render time for the smoke loop, and can save backend/CPU PNG
  artifacts with `--artifact-dir=...`.
- Vulkan intentionally remains CPU-present/present-only for this phase while
  WSI/swapchain presentation and Vulkan command replay are still pending.

Deliverables:

- Implement direct OpenGL replay for core commands: clear, fill/stroke rect,
  rounded rect, image draw, clips, transforms, and text via glyph atlas.
  **Started with clear, opaque fill/stroke rectangles, and rectangular clips.**
- Implement Vulkan replay for the same command subset, or keep Vulkan in
  CPU-present mode until OpenGL command replay is stable. **Vulkan remains
  CPU-present for this phase.**
- Add backend-labeled visual artifacts for CPU, OpenGL, and Vulkan comparisons.
  **Started through demo artifact export for backend and CPU PNGs.**
- Measure upload cost, command replay cost, frame latency, and memory.
  **Started with demo render-time reporting.**

Exit criteria:

- OpenGL command replay matches CPU-present parity for the first clear/rect/clip
  subset.
- Vulkan either matches that subset or is explicitly documented as present-only
  for the release. **Documented as CPU-present/present-only in this first
  Phase 6 slice.**
- Performance improves for representative interactive scenes without regressing
  fidelity beyond agreed thresholds.

### Phase 7 - Linux Hardening and Preview

Phase 7 first slice delivered:

- `LinuxGraphicsRuntimeDiagnostics` reports OS, process architecture, runtime
  identifier, .NET framework, and Wayland/X11 display environment.
- OpenGL context diagnostics now include vendor, renderer, GL version, and GLSL
  version when an EGL context is created.
- Vulkan loader/device diagnostics now include the selected physical-device
  name, type, API version, driver version, vendor ID, and device ID.
- The Linux graphics demo prints runtime/display diagnostics before dependency
  probes, keeps Phase 6 native-replay inspection, and reports sanitized selected
  evdev keyboard/mouse summaries when input is enabled.
- `Broiler.Input.Linux.Diagnostic` now prints a device summary with total,
  available, and permission-denied counts before listing sanitized devices.
- Added [Linux preview hardening notes](./linux-preview-hardening.md) with
  distro package starting points, evdev permission guidance, hardware matrix,
  validation commands, and preview caveats.

Deliverables:

- Add packaging notes for native dependencies by distro family. **Started in
  the Linux preview hardening notes.**
- Add runtime diagnostics for Mesa driver, GL version, Vulkan API version,
  display server, input permission state, and selected event devices. **Started
  across the graphics demo, OpenGL/Vulkan surfaces, and input diagnostic.**
- Add hardware matrix: Intel/AMD Mesa GPU, VM/software Mesa, Wayland, X11,
  at least one keyboard, and at least one mouse. **Documented for preview
  validation.**
- Add docs for security limitations of direct evdev. **Documented in the
  preview hardening notes.**
- Freeze preview API shape and compatibility caveats.

Exit criteria:

- A Linux preview app can render with OpenGL and Vulkan on at least one Mesa
  hardware driver and one software-driver path.
- Keyboard and mouse work through evdev with documented permissions.
- Build, tests, demos, and docs can be followed from a clean Linux machine.
- No platform-neutral package gains a Linux-native dependency.

## 7. Testing Strategy

### 7.1 Architecture Tests

- No Linux implementation assembly references Windows implementation assemblies.
- No platform-neutral abstraction exposes Linux native handles, paths, Vulkan
  handles, EGL handles, Wayland/X11 objects, or file descriptors.
- Input implementation assemblies do not reference Graphics.
- Graphics implementation assemblies do not parse evdev events.
- OpenGL and Vulkan implementations can be omitted independently.

### 7.2 Graphics Tests

- Offscreen render smoke tests for CPU, OpenGL, and Vulkan.
- Pixel comparisons against the existing managed renderer.
- Window resize and surface recreation tests.
- Device/context loss simulations where possible.
- Software-driver CI using Mesa where reliable.
- Hardware smoke tests for Mesa Intel and AMD drivers before preview.

### 7.3 Input Tests

- Byte-level parser fixtures for `input_event` records.
- Keyboard mapping fixtures for common `KEY_*` codes and left/right modifiers.
- Mouse movement/button/wheel fixtures, including high-resolution wheel data.
- `SYN_DROPPED` and device-removal behavior.
- Permission-denied and missing-device diagnostics.
- Optional `/dev/uinput` integration tests when CI has privileges.

### 7.4 End-to-End Tests

- Linux demo starts, renders, resizes, and exits cleanly.
- Mouse interaction changes demo state.
- Keyboard shortcuts change demo state.
- OpenGL and Vulkan modes produce comparable screenshots.
- Disposing the window stops input delivery and releases graphics resources.

## 8. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Direct evdev reads global input | Privacy and security concern | Require explicit opt-in, document permissions, pause on app deactivation, avoid payload logging |
| Evdev lacks text/layout semantics | Broken text input if faked | Emit key transitions only; plan xkbcommon text milestone separately |
| Touchpads need libinput policy | Poor pointer behavior | Start with mouse devices; defer touchpad gestures/acceleration to libinput or compositor path |
| Mesa driver capabilities vary | Backend instability | Runtime capability checks, software-driver CI, backend-labeled diagnostics |
| Vulkan scope grows too quickly | Long delay before visible Linux support | Start with CPU-present path, then add GPU-native replay |
| Wayland/X11 split doubles work | Windowing delay | Choose one first path, keep the other behind the same Linux window boundary |
| Native controls do not port from Win32 | UI gaps | Keep controls unsupported or route through Broiler.UI instead of adding GTK/Qt dependency |
| Relative mouse data does not match `InputPoint` assumptions | Integration bugs | Document `raw-relative-counts` and add an ADR before changing event shapes |
| Permissions differ across distros | Confusing setup | Provide diagnostics and distro-specific notes; consider logind/seatd later |

## 9. Definition of Done for First Linux Preview

The first Linux preview is complete when:

- `Broiler.Graphics` builds on Linux with the neutral core unchanged.
- `Broiler.Graphics.Linux.OpenGL` presents through Mesa OpenGL.
- `Broiler.Graphics.Linux.Vulkan` presents through the Vulkan loader and a Mesa
  ICD, or is explicitly marked preview-present-only with passing smoke tests.
- At least one Linux demo can switch between OpenGL and Vulkan.
- `Broiler.Input.Keyboard.Linux` reads evdev keyboard key transitions.
- `Broiler.Input.Mouse.Linux` reads evdev mouse movement, buttons, and wheel.
- Input device removal, permission denial, and disposal are deterministic.
- Graphics does not own Linux input parsing.
- Input does not reference Graphics.
- Documentation explains native dependencies, permissions, display-server
  support, Mesa diagnostics, and known limitations.

## 10. Required ADRs

1. Linux support matrix: distro baseline, architectures, display servers, and CI.
2. Linux native library loading and failure diagnostics.
3. Mesa backend policy: OpenGL, Vulkan, software drivers, and default selection.
4. Linux window ownership, event loop, resize, DPI, and teardown.
5. Evdev permission and privacy model.
6. Evdev device discovery, opaque IDs, and hot-plug semantics.
7. Raw relative mouse coordinate-space contract.
8. Linux keyboard mapping and text-input deferral.
9. Font discovery on Linux: managed fonts first, fontconfig later.
10. Graphics/Input compatibility window for `BWindow` input callbacks.

## 11. Primary References

- Mesa documents OpenGL, OpenGL ES, Vulkan, and software drivers such as
  llvmpipe: https://www.mesa3d.org/
- Mesa llvmpipe driver documentation:
  https://docs.mesa3d.org/drivers/llvmpipe.html
- Khronos OpenGL `glClear` reference:
  https://registry.khronos.org/OpenGL-Refpages/gl4/html/glClear.xhtml
- Khronos OpenGL `glScissor` reference:
  https://registry.khronos.org/OpenGL-Refpages/gl4/html/glScissor.xhtml
- Vulkan Window System Integration:
  https://docs.vulkan.org/spec/latest/chapters/VK_KHR_surface/wsi.html
- Linux input subsystem introduction and evdev overview:
  https://docs.kernel.org/input/input.html
- Linux input event codes:
  https://docs.kernel.org/input/event-codes.html
- libevdev kernel header notes, including event constants and clock-id support:
  https://www.freedesktop.org/software/libevdev/doc/1.5/kernel_header.html
- Ubuntu 24.04 `mesa-vulkan-drivers` package and runtime dependencies:
  https://packages.ubuntu.com/noble/mesa-vulkan-drivers
- Debian 12 `mesa-vulkan-drivers` package:
  https://packages.debian.org/bookworm/mesa-vulkan-drivers
- Fedora `mesa-vulkan-drivers` package:
  https://packages.fedoraproject.org/pkgs/mesa/mesa-vulkan-drivers/
- Arch `vulkan-intel` package, one Mesa Vulkan ICD example:
  https://archlinux.org/packages/extra/x86_64/vulkan-intel/

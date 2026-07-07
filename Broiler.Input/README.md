# Broiler.Input

**Status:** Phase 5 camera Windows capture  
**Runtime:** .NET 10

Broiler.Input is the standalone input component family. The Phase 0 slice in
this workspace created the component root, froze the dependency direction, and
added the first Windows keyboard and mouse implementation prototypes. Phase 1
adds the shared lifecycle/diagnostic contracts, Windows host boundary, Raw Input
lease contract, deterministic fakes, and package-free contract tests. Phase 4
adds the microphone abstraction and the first Windows WASAPI shared/event
capture provider. Phase 5 adds camera contracts and the first Windows Media
Foundation Source Reader provider.

## Projects

```text
Broiler.Input
Broiler.Input.Windows
Broiler.Input.Camera
Broiler.Input.Camera.Windows
Broiler.Input.Keyboard
Broiler.Input.Keyboard.Linux
Broiler.Input.Keyboard.Windows
Broiler.Input.Legacy
Broiler.Input.Linux.Diagnostic
Broiler.Input.Linux
Broiler.Input.Linux.Tests
Broiler.Input.Microphone
Broiler.Input.Microphone.Windows
Broiler.Input.Mouse
Broiler.Input.Mouse.Linux
Broiler.Input.Mouse.Windows
Broiler.Input.Pen
Broiler.Input.Text
Broiler.Input.Touch
Broiler.Input.Testing
Broiler.Input.Contract.Tests
```

The platform-neutral projects target `net10.0`. Windows implementation projects
target `net10.0-windows`.

## Dependency Rules

```text
Broiler.Input.Keyboard.Windows -> Broiler.Input.Keyboard -> Broiler.Input
Broiler.Input.Keyboard.Windows -> Broiler.Input.Windows -> Broiler.Input
Broiler.Input.Mouse.Windows -> Broiler.Input.Mouse -> Broiler.Input
Broiler.Input.Mouse.Windows -> Broiler.Input.Windows -> Broiler.Input
Broiler.Input.Camera.Windows -> Broiler.Input.Camera -> Broiler.Input
Broiler.Input.Camera.Windows -> Broiler.Input.Windows -> Broiler.Input
Broiler.Input.Microphone.Windows -> Broiler.Input.Microphone -> Broiler.Input
Broiler.Input.Microphone.Windows -> Broiler.Input.Windows -> Broiler.Input
Broiler.Input.Keyboard.Linux -> Broiler.Input.Keyboard -> Broiler.Input
Broiler.Input.Keyboard.Linux -> Broiler.Input.Linux -> Broiler.Input
Broiler.Input.Mouse.Linux -> Broiler.Input.Mouse -> Broiler.Input
Broiler.Input.Mouse.Linux -> Broiler.Input.Linux -> Broiler.Input
Broiler.Input.Pen -> Broiler.Input
Broiler.Input.Text -> Broiler.Input
Broiler.Input.Touch -> Broiler.Input
```

Abstraction assemblies do not reference Windows assemblies. Input assemblies do
not reference Graphics, HTML, DOM, JavaScript, WPF, Windows Forms, or application
projects.

## Native Boundary

The new component has no third-party package dependencies. Windows-specific
calls use `LibraryImport` or `DllImport` against .NET runtime interop:

- `QueryPerformanceCounter` and `QueryPerformanceFrequency`;
- `RegisterRawInputDevices`;
- `GetRawInputData`;
- `GetKeyState`;
- `ScreenToClient`; and
- `TrackMouseEvent`;
- `CoInitializeEx`, `CoCreateInstance`, `CoTaskMemFree`, and
  `PropVariantClear`; and
- `CreateEventW`, `SetEvent`, `WaitForSingleObject`, and `CloseHandle`;
- `MFStartup`, `MFShutdown`, `MFCreateAttributes`, `MFGetAttributeSize`, and
  `MFGetAttributeRatio`;
- `MFEnumDeviceSources`; and
- `MFCreateSourceReaderFromMediaSource`.

Linux-specific calls use `DllImport` against libc for direct evdev delivery:

- `open` with `O_RDONLY`, `O_CLOEXEC`, and `O_NONBLOCK`;
- `read`;
- `poll`; and
- `ioctl` for best-effort `EVIOCSCLOCKID`.

## Phase 0 Scope

This slice includes Windows keyboard and mouse message translation plus explicit
Raw Input registration ownership. Camera, microphone, touch, pen, and gamepad
remain roadmap work and are not scaffolded as runtime projects in this slice.

## Phase 1 Checks

```powershell
dotnet build Broiler.Input\Broiler.Input.slnx
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
```

The contract runner proves fake lifecycle, cancellation, removal, bounded
delivery, no package references, core-without-Windows references, and public API
baseline compatibility.

## Phase 2 Scope

Phase 2 support in this workspace adds platform-neutral Touch, Pen, and Text
contract assemblies used by Broiler.UI routing tests. The Windows provider work
for these devices remains a later Input implementation slice. Existing Windows
keyboard and mouse extraction tests continue to validate the current providers.

## Phase 3 Scope

Phase 3 hardens Windows keyboard/text and Raw Input behavior inside the
component: surrogate-safe committed text, dead-key events, layout-change events,
explicit unsupported IME composition milestones, system-key pass-through
defaults, opaque raw physical device identity, raw mouse coalescing metrics, and
explicit background-input acknowledgement.

## Phase 4 Scope

Phase 4 adds platform-neutral microphone contracts and a Windows WASAPI
shared-mode/event-driven provider. Normal contract tests use synthetic
microphone devices to prove bounded delivery, lease disposal, silence and
discontinuity accounting, default-device change observation, and assembly
isolation. Hardware microphone checks are documented separately under
`docs/phase4` and remain opt-in.

## Phase 5 Scope

Phase 5 adds platform-neutral camera contracts and a Windows Media Foundation
Source Reader provider. Normal contract tests use synthetic camera devices to
prove latest-frame preview behavior, explicit loss-sensitive overflow, frame
lease ownership, preview adapter behavior without a Graphics reference, and
assembly isolation. Hardware camera checks are documented separately under
`docs/phase5` and remain opt-in.

## Linux Follow-Up

The Linux second-platform plan adds `Broiler.Input.Linux`,
`Broiler.Input.Keyboard.Linux`, and `Broiler.Input.Mouse.Linux` around direct
`/dev/input/event*` (`evdev`) reads for the first keyboard/mouse slice. The
initial Linux scope is key transitions, mouse relative movement, buttons, wheel,
hot-plug, permission diagnostics, and deterministic disposal; layout-aware text
input and IME are deferred.

Phase 2 evdev MVP is present: sysfs capability filtering, direct event-device
open/read/poll, 64-bit `input_event` parsing, keyboard key transition mapping,
mouse relative movement/buttons/wheel mapping, refresh-based add/remove
observation, raw-input acknowledgement, synthetic parser/translator tests, and
the `Broiler.Input.Linux.Diagnostic` console tool.

Phase 7 hardening adds a clearer diagnostic summary for Linux input preview
runs: native dependency status, event-device permission counts, sanitized
keyboard/mouse event names, and explicit raw-input streaming acknowledgement.
See [Linux preview hardening notes](../docs/roadmap/linux-preview-hardening.md).

See [Linux second-OS roadmap](../docs/roadmap/linux-graphics-input-roadmap.md).

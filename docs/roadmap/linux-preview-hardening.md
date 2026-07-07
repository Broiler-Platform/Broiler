# Linux Preview Hardening Notes

**Status:** Phase 7 first hardening slice  
**Date:** 2026-07-06  

These notes turn the Linux second-OS roadmap into a repeatable preview checklist.
They are intentionally conservative: a .NET 10 self-contained Broiler publish
still needs native graphics/input libraries from the host distribution.

## Runtime Diagnostics

Run the graphics demo first. It prints:

- OS description, process architecture, runtime identifier, and .NET framework;
- display environment from `XDG_SESSION_TYPE`, `WAYLAND_DISPLAY`, and `DISPLAY`;
- native library status for EGL, OpenGL, Vulkan, Wayland, XCB, and X11;
- OpenGL vendor, renderer, GL version, and GLSL version once an EGL context is
  created;
- Vulkan selected device name, device type, API version, driver version, vendor
  ID, and device ID once the Vulkan loader/device path is created;
- OpenGL native-replay support for the current smoke render list.

```bash
./Broiler.Graphics.Linux.Demo
./Broiler.Graphics.Linux.Demo --vulkan
./Broiler.Graphics.Linux.Demo --window --enable-evdev-input --interactive
./Broiler.Graphics.Linux.Demo --artifact-dir "$PWD/artifacts"
```

Run the input diagnostic separately. It prints native dependency status,
event-device permission state, sanitized event-device names, availability, and
optional sanitized event summaries.

```bash
./Broiler.Input.Linux.Diagnostic
./Broiler.Input.Linux.Diagnostic --events --acknowledge-raw-input --duration-ms 10000
```

## Native Packages

Package names vary by distribution and driver stack. These commands are starting
points for the preview matrix, not a full desktop graphics setup guide.

| Family | Runtime packages |
|---|---|
| Ubuntu 24.04 / Debian 12 | `sudo apt install libegl1 libgl1 libgles2 libvulkan1 mesa-vulkan-drivers libwayland-client0 libx11-6 libx11-xcb1 libxcb1` |
| Fedora | `sudo dnf install mesa-libEGL mesa-libGL mesa-libGLES vulkan-loader mesa-vulkan-drivers wayland libX11 libxcb` |
| Arch | `sudo pacman -S mesa libglvnd vulkan-icd-loader wayland libx11 libxcb vulkan-intel vulkan-radeon vulkan-swrast` |

For hardware Vulkan on Arch, install the ICD matching the GPU (`vulkan-intel`
or `vulkan-radeon`). `vulkan-swrast` is the software Vulkan path. Ubuntu and
Debian carry Mesa Vulkan drivers in `mesa-vulkan-drivers`; Fedora carries them
in `mesa-vulkan-drivers`.

## Evdev Permissions

Direct evdev reads can observe global keyboard and mouse activity. Broiler keeps
that behavior behind explicit acknowledgement, pauses the demo reads when the
X11 window loses focus, and prints only sanitized event-device names such as
`event3`.

Development machines usually need one of these policies:

- run the diagnostic without `--events` to inspect permission state first;
- add the developer user to the distro's input group when that group owns
  `/dev/input/event*`, then sign out and back in;
- add a local udev rule for a controlled test machine only;
- pass `/dev/input` into containers explicitly;
- prefer a future logind/seatd broker before any polished desktop package.

Do not log typed text, raw device paths, `phys`, or `uniq` values from production
apps. The Linux providers hash hardware identity into opaque IDs and expose only
sanitized capabilities.

## Hardware Matrix

Before calling the Linux preview shippable, run this matrix and attach the demo
output plus artifacts:

| Area | Required preview coverage |
|---|---|
| OpenGL hardware | Intel Mesa or AMD Mesa, EGL context, GL vendor/renderer/version visible |
| OpenGL software | llvmpipe or equivalent software Mesa path |
| Vulkan hardware | Intel or AMD Mesa ICD creates a Vulkan 1.2 loader/device path, or release notes explicitly mark Vulkan present-only |
| Vulkan software | lavapipe or another software Vulkan ICD where available |
| Display server | X11 preview window today; Wayland environment detected and documented as pending native window work |
| Input | One readable keyboard and one readable mouse through evdev |
| Permissions | Permission-denied state documented on a normal user account without input access |
| Packaging | `linux-x64` self-contained demo and input diagnostic publish artifacts |

## Validation Commands

```bash
dotnet run --project Broiler.Graphics/Broiler.Graphics.Linux.Tests/Broiler.Graphics.Linux.Tests.csproj -c Release
dotnet run --project Broiler.Input/Broiler.Input.Linux.Tests/Broiler.Input.Linux.Tests.csproj -c Release
dotnet publish Broiler.Graphics/Broiler.Graphics.Linux.Demo/Broiler.Graphics.Linux.Demo.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish Broiler.Input/Broiler.Input.Linux.Diagnostic/Broiler.Input.Linux.Diagnostic.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Current Preview Caveats

- OpenGL native replay is limited to clear, opaque fill/stroke rectangles, and
  rectangular clips.
- Text, images, rounded rectangles, transforms, and translucent operations still
  fall back to CPU-present rendering.
- Vulkan creates a loader/device path but remains CPU-present/offscreen until
  WSI/swapchain presentation lands.
- Wayland is detected in diagnostics but not yet used for a native window.
- evdev emits key transitions and raw relative mouse counts only; layout-aware
  text, IME, touchpad policy, gestures, touch, pen, and gamepad are deferred.

## Source Notes

- Ubuntu 24.04 package pages list `libegl1`, `libgl1`, and
  `mesa-vulkan-drivers`; `mesa-vulkan-drivers` depends on `libvulkan1`,
  `libwayland-client0`, `libx11-xcb1`, and XCB libraries:
  https://packages.ubuntu.com/noble/mesa-vulkan-drivers
- Debian 12 lists `mesa-vulkan-drivers`, `libegl1`, `libgl1`, and related Mesa
  runtime libraries in bookworm:
  https://packages.debian.org/bookworm/mesa-vulkan-drivers
- Fedora packages Mesa Vulkan drivers as `mesa-vulkan-drivers`:
  https://packages.fedoraproject.org/pkgs/mesa/mesa-vulkan-drivers/
- Arch packages Mesa Vulkan ICDs separately, including `vulkan-intel` and the
  CPU/software `vulkan-swrast` driver:
  https://archlinux.org/packages/extra/x86_64/vulkan-intel/

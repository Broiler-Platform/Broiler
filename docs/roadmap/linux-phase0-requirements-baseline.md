# Linux Phase 0 Requirements Baseline

**Status:** Approved baseline for Linux Phase 0  
**Date:** 2026-07-06  
**Scope:** Runtime, OS, native dependency, graphics, input, CI, and packaging
requirements for the first Linux bring-up of `Broiler.Graphics` and
`Broiler.Input`.

## 1. Decision

The first Linux preview baseline follows the requirements shape of a .NET 10
self-contained application:

- Broiler Linux applications publish for explicit Linux RIDs and carry the .NET
  runtime in the application output.
- The host machine still supplies native OS libraries required by .NET, Mesa,
  Vulkan, the display server, udev, and input-device access.
- The first required runtime IDs are `linux-x64` and `linux-arm64`.
- `linux-x64` is the required development, CI, and hardware-validation path.
- `linux-arm64` is a required compile/publish path and an opt-in hardware path
  until ARM Linux graphics hardware is available in the test matrix.
- `linux-musl-*`, Alpine, arm32, direct DRM/KMS, GBM-only presentation, Native
  AOT, and touchpad/libinput policy are deferred.

The baseline optimizes for a predictable first preview rather than the broadest
Linux matrix.

## 2. .NET Baseline

| Area | Baseline |
|---|---|
| Target framework | `net10.0` for platform-neutral and Linux assemblies |
| App deployment | Self-contained publish for a specific RID |
| Required RIDs | `linux-x64`, `linux-arm64` |
| Optional later RIDs | `linux-musl-x64`, `linux-musl-arm64`, arm32 |
| Runtime patch policy | Current .NET 10 servicing runtime; do not pin preview apps to one patch unless a regression forces it |
| SDK policy | Current .NET 10 SDK available on CI and developer machines |
| Single-file | Supported for demos/tools after native library probing is explicit |
| Native AOT | Not a Phase 0/first-preview requirement |
| Trimming | Platform-neutral assemblies should stay trim-friendly; Linux implementation assemblies are warning-clean but not trim-blocking for first preview |

Reference publish command for a demo or diagnostic tool:

```powershell
dotnet publish path/to/App.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

Self-contained means the managed .NET runtime is included with the app. It does
not remove the need for Linux system libraries such as the C runtime, OpenSSL,
ICU, Mesa, Vulkan loader, Wayland/XCB, or input permissions.

## 3. Supported OS Baseline

Required first-preview support:

| Tier | Distribution | Architecture | Purpose |
|---|---|---|---|
| Primary | Ubuntu 24.04 LTS | x64 | CI, development, release smoke, Mesa software-driver tests |
| Compatibility | Ubuntu 22.04.4 LTS | x64 | Oldest LTS compatibility smoke |
| Compatibility | Debian 12 | x64 | Debian packaging and runtime-dependency smoke |
| Build-only | Ubuntu 24.04 LTS | arm64 | Publish/restore/build validation |

Deferred:

- Ubuntu 26.04 LTS as a required target. It can be added once CI images and
  package names settle.
- Fedora, RHEL, openSUSE, Arch, and Azure Linux as formal support tiers. They
  remain expected-compatible where .NET 10 and Mesa packages are available.
- Alpine/musl. .NET 10 supports musl RIDs, but Mesa/windowing/input dependency
  validation should be separate from the first glibc preview.

Kernel baseline:

- Linux kernel 5.15 or newer for the first preview baseline.
- Newer kernels are expected and should work; kernel-specific behavior must be
  diagnosed rather than silently changing provider contracts.

## 4. Native Runtime Dependencies

The host must provide the .NET 10 Linux dependency set for its distribution. For
the chosen Debian/Ubuntu baseline, that means the distro-specific equivalents
of:

- C library / `libc6`;
- GCC low-level runtime / `libgcc-s1`;
- C++ runtime / `libstdc++6`;
- ICU / distro `libicu*`;
- OpenSSL / distro `libssl*`;
- Kerberos / GSSAPI package;
- CA certificates;
- time-zone data.

Broiler does not vendor these OS libraries into the application bundle. Runtime
diagnostics should report missing native dependencies by library name and
subsystem.

## 5. Graphics Dependency Baseline

OpenGL path:

- Mesa EGL available as `libEGL.so.1`;
- OpenGL/OpenGL ES loader available through distro Mesa packages;
- minimum required context: OpenGL 3.3 core or OpenGL ES 3.0;
- llvmpipe accepted for CI/headless software-driver validation.

Vulkan path:

- Vulkan loader available as `libvulkan.so.1`;
- Mesa Vulkan ICD installed for the active GPU or lavapipe;
- required instance/device features limited to Vulkan 1.2 plus
  `VK_KHR_surface`, the selected platform surface extension, and
  `VK_KHR_swapchain`;
- Vulkan 1.3+ may be used opportunistically after feature checks, but must not
  be required for the first preview.

Display/windowing:

- Wayland is the primary desktop target.
- X11/XCB is the fallback target. The Phase 3 OpenGL preview uses X11 for its
  first opt-in window surface while keeping XCB in the broader dependency
  baseline.
- Headless/offscreen tests may use surfaceless EGL and software Mesa.
- Direct DRM/KMS and GBM-only presentation are out of first-preview scope.

Suggested Ubuntu 24.04 development packages:

```bash
sudo apt-get update
sudo apt-get install -y \
  ca-certificates \
  libc6 \
  libgcc-s1 \
  libgssapi-krb5-2 \
  libicu74 \
  libssl3t64 \
  libstdc++6 \
  tzdata \
  libegl1 \
  libgl1 \
  libglx-mesa0 \
  libvulkan1 \
  mesa-vulkan-drivers \
  mesa-utils \
  vulkan-tools \
  libwayland-client0 \
  libxcb1 \
  libudev1
```

Package names differ by distribution. The runtime check must probe libraries and
capabilities, not assume Ubuntu package names.

## 6. Input Dependency Baseline

Required:

- readable Linux event devices under `/dev/input/event*`;
- evdev `input_event` records and event codes from the Linux input subsystem;
- permission diagnostics for inaccessible event devices;
- hot-plug/removal detection through udev when available;
- fallback scan of `/dev/input` and `/sys/class/input` when udev is absent.

Not required for first preview:

- libinput;
- xkbcommon;
- compositor-provided text input;
- setuid helpers;
- systemd-logind or seatd integration;
- touchpad gesture policy;
- input injection through uinput.

Direct event-device access is raw/background-capable. Applications must opt into
that behavior explicitly, and diagnostics must not log typed text, raw hardware
paths, or unique IDs by default.

## 7. Build and CI Baseline

Required CI jobs:

- Windows build/test jobs remain green.
- Linux `ubuntu-24.04` builds platform-neutral projects.
- Linux `ubuntu-24.04` builds the future Linux projects once scaffolded.
- Linux publishes at least one self-contained `linux-x64` demo/tool artifact.
- Architecture tests verify that platform-neutral assemblies do not expose
  Linux native handles, paths, Vulkan/EGL handles, Wayland/XCB objects, or file
  descriptors.

Deferred CI jobs:

- hardware GPU validation;
- real `/dev/input/event*` hardware tests;
- Wayland compositor integration tests;
- lavapipe/llvmpipe visual parity gates if hosted runners are too variable;
- `linux-arm64` execution. Restore/build/publish is enough for Phase 0.

## 8. Phase 0 Exit Gate

Phase 0 is complete when:

- this baseline is accepted as the Linux support matrix for first preview;
- the Linux roadmap links to this baseline;
- required ADR topics are enumerated;
- project names and dependency directions are frozen for scaffolding;
- the first implementation can start without re-deciding OS/RID/package scope.

## 9. References

- .NET 10 download page:
  https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- .NET application publishing overview:
  https://learn.microsoft.com/en-us/dotnet/core/deploying/
- .NET single-file deployment:
  https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- .NET RID catalog:
  https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
- .NET 10 Linux package dependencies:
  https://github.com/dotnet/core/blob/main/release-notes/10.0/dotnet-dependencies.md
- Mesa:
  https://www.mesa3d.org/
- Vulkan WSI:
  https://docs.vulkan.org/spec/latest/chapters/VK_KHR_surface/wsi.html
- Linux input subsystem:
  https://docs.kernel.org/input/input.html

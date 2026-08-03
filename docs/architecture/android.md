# Android application architecture

- **Status:** Emulator-verified preview host. The shared Activity/SurfaceView
  host, Writer and Browser applications, hardware-accelerated Canvas
  presentation, touch/pen/keyboard/IME bridge, system-bar insets, scoped-storage
  Writer flow, and Android workspaces build and run on an API 36 x86_64 emulator.
  Physical-device validation remains outstanding.
- **Last reconciled:** 2026-08-01

Android is an application host for the existing `Broiler.Browser.Core` and
`Broiler.Writer.Core`. It does not introduce another rendering engine, layout
engine, or UI toolkit.

## Platform baseline

Android support uses .NET for Android directly. .NET MAUI is out of scope because
Broiler already owns its control, rendering, and input stacks. Each application is
one `Activity` containing one Broiler-owned `SurfaceView`; there are no XML
layouts or Android widgets in application content.

| Decision | Preview baseline |
| --- | --- |
| Target framework / compile API | `net10.0-android36.0` / API 36 |
| Minimum API | API 24 (`SupportedOSPlatformVersion=24`) |
| Shipped ABIs | `android-arm64`; `android-x64` is retained for emulator CI. `BroilerAndroidAbis` overrides the pair per publish — the preview package's APK uses it to build `arm64-v8a` alone. |
| Managed runtime | The workload's default Mono runtime with JIT |
| AOT and trimming | Disabled. Browser uses an expression compiler based on `Reflection.Emit`, which is incompatible with full AOT. |
| Packages | `org.broiler.writer` and `org.broiler.browser` |
| Artifacts | Self-contained Debug APK; Release AAB for upload and arm64 Release APK for installation |
| Signing | Debug signing is workload-managed. Release keys stay outside the repository — the delivery pipeline signs, reading the key from encrypted repository secrets. |
| Network policy | Only Browser declares `INTERNET`; cleartext traffic is disabled for both applications. |

The generated roots are `Broiler.Android.Writer.slnx`,
`Broiler.Android.Browser.slnx`, and the host-runnable
`Broiler.Android.Tests.slnx`. Their forbidden-project patterns exclude Windows,
Linux, and WebAssembly heads from Android closures.

## Development environment

Verified on Windows on 2026-08-01:

| Requirement | Verified state |
| --- | --- |
| .NET | .NET SDK 10.0.302 and Android workload 36.1.43 |
| JDK | OpenJDK 21 |
| Android SDK | API 36 platform, build-tools, and platform-tools at `C:\Program Files (x86)\Android\android-sdk` |

When the SDK is not discoverable through `ANDROID_HOME` or
`ANDROID_SDK_ROOT`, pass:

```powershell
-p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
```

The repository's `scripts/install-android-sdk.sh` remains the Linux/container
provisioning path. Runtime checks use an API 36 `sdk_gphone64_x86_64` emulator.

## Project and ownership boundaries

| Concern | Owner |
| --- | --- |
| Shared Activity view, lifecycle, vsync, clipboard, and IME glue | `Broiler.App.Android` |
| Product Activity and capability policy | `Broiler.Browser.Android` / `Broiler.Writer.Android` |
| Shared application behavior | `Broiler.Browser.Core` / `Broiler.Writer.Core` |
| On-screen hardware Canvas presentation and native image cache | `Broiler.App.Android` |
| Portable off-screen rasterization and image ownership | `Broiler.Graphics` |
| Standalone EGL/OpenGL ES backend and host-runnable tests | `Broiler.Graphics.Android` |
| Neutral device and text contracts | `Broiler.Input` |
| Android touch, pen, keyboard, and IME providers | `Broiler.Input.*.Android` |
| Generic controls and routing | `Broiler.UI` |
| Scoped-storage document access | `Broiler.Writer.Android` |

Android framework types stop at `.Android` assemblies. They do not appear in
`Broiler.Graphics` core, `Broiler.Input` core, or `Broiler.UI`. The Android input
providers intentionally target plain `net10.0` and accept primitive snapshots so
their translation and lifecycle tests run on any host.

## Rendering, lifecycle, and scheduling

`AndroidCanvasRenderer` replays the neutral `BRenderList` directly through a
hardware-accelerated Android `Canvas`. The portable `BImageRenderer` remains the
image-resource owner and off-screen rendering fallback; native bitmaps are
cached by image handle. This avoids allocating, rasterizing, copying, and
uploading a full-screen managed bitmap for every input event. The standalone
`Broiler.Graphics.Android` EGL/GLES backend remains available and tested, but it
is no longer the application heads' on-screen presentation route.

`AndroidBroilerView` owns surface readiness, density-to-logical-pixel mapping,
render invalidation, and `Choreographer` callbacks. Vsync scheduling stops while
the Activity is paused. Surface destruction releases product graphics resources;
the next surface resumes Canvas presentation without rebuilding application
state. `AndroidInsetLayout` keeps both SurfaceViews outside status/navigation bars
and display cutouts.

The application and UI session stay on Android's main thread. Browser posted work
returns through the view's main-thread queue. Rotation, density, UI-mode, and
multi-window size changes are handled by Activity configuration changes and
`SurfaceChanged` without recreating application state.

The API 36 emulator validates launch, full-resolution drawing, touch response,
system-font drawing, rotation, and pause/resume. Cold Debug presentation measured
77 ms for Writer and 48 ms for Browser; subsequent input frames were normally
within a 16 ms frame interval. The superseded managed-raster path took 11.1
seconds for the same Writer frame. Still to validate on physical hardware:
rotation stress,
long-running resource leakage, font scale, diverse cutouts, and reduced-motion
settings.

## Input and text

`AndroidInputCoordinator` owns real `MotionEvent` and `KeyEvent` objects and
forwards primitive samples to the Android providers. `UiInputEvent` preserves
contact identity, phase, pressure, pen buttons, and tilt. `UiSession` keeps a
stable route per touch contact and supplies a pointer fallback for existing
button and edit controls. ScrollView and RichEdit consume touch drags; Browser
content supports touch panning and pinch zoom.

The neutral `IUiTextEditor` contract exposes surrounding text, selection,
composing-region mutation, deletion, and editor actions. StandardEdit and
StandardRichEdit implement it. `BroilerInputConnection` adapts Android
`InputConnection` queries and mutations to `Broiler.Input.Text.Android` and the
focused editor. Caret/focus changes control soft-keyboard show/hide and clear the
previous editor's caret ownership.

Remaining interaction gaps are explicit: no kinetic fling, long-press or
double-tap recognizer, selection handles, or hardware validation of stylus tilt
and CJK candidate placement. Device descriptors still use provider defaults
rather than `InputManager` hot-plug enumeration.

## Writer storage

Writer uses the Storage Access Framework through `ActionOpenDocument` and
`ActionCreateDocument`; it requests no broad storage permission. `WriterApp`
accepts streams plus display names, allowing RTF, DOCX, HTML, and Markdown to
reuse the platform-neutral document stack. Persistable URI permission is retained
when a provider supports it. App-private RTF recovery is written during pause and
restored on a fresh Activity process.

The Android Writer uses compact chrome and hides the desktop formatting-code pane
on narrow screens. Clipboard and soft-keyboard behavior are supplied by the
shared Android view. Share and print intents, a recent-document UI, and explicit
recovery cleanup are not implemented.

## Browser policy and interaction

Browser declares `INTERNET`, rejects cleartext traffic in its manifest, accepts
only HTTPS launch deep links, and maps system Back to shared browser history.
The toolbar becomes compact below 600 logical pixels. Page content supports
single-finger panning and bounded pinch zoom.

This remains an experimental browser, not a security sandbox. The shared
JavaScript engine and network content require the same preview warning on Android
as on desktop. Steady-state emulator drawing is measured, but memory pressure,
process recreation, TLS behavior, arbitrary page interaction, and physical-device
performance remain unmeasured.

## Build and verification

```powershell
dotnet build Broiler.Android.Writer.slnx -c Debug `
  -p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
dotnet build Broiler.Android.Browser.slnx -c Debug `
  -p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
dotnet run --project Broiler.Input/Broiler.Input.Android.Tests
dotnet run --project Broiler.Graphics/Broiler.Graphics.Android.Tests
```

Debug builds create self-contained APKs (`EmbedAssembliesIntoApk=true`) so a
normal `adb install` cannot reuse stale fast-deployment assemblies. Release builds
create AABs.

The [Broiler Preview Package workflow](../../.github/workflows/broiler-preview-package.yml)
builds both heads with `dotnet publish -c Release` — the configuration name has to
be exactly `Release`, since that is what switches `AndroidPackageFormat` to `aab` —
and ships them as `BPP-Android.zip`. One publish leaves three packages side by
side: the unsigned `<applicationId>.aab` plus a `-Signed.aab` and `-Signed.apk`
carrying the workload's debug key. The workflow takes the unsigned one and fails if
the file it picked turns out to be signed already, so a debug-signed artifact
cannot pass itself off as a release. Each bundle carries `arm64-v8a` and `x86_64`,
which is what the heads' `RuntimeIdentifiers` resolve to when the publish passes no
`--runtime`.

A bundle is the upload format and cannot be installed, so each head is published a
second time as a directly installable APK:

```
dotnet publish <head> -c Release -p:AndroidPackageFormat=apk -p:BroilerAndroidAbis=android-arm64
```

`arm64-v8a` alone, because that is the ABI physical devices run and an APK carries
one ABI where a bundle carries the split set.

The ABI override goes through `BroilerAndroidAbis`, which each head expands into
its own `RuntimeIdentifiers`, rather than passing `RuntimeIdentifiers` on the
command line directly. **A property on the command line is an MSBuild global
property: it reaches every project in the graph.** `RuntimeIdentifiers=android-arm64`
therefore also lands on the referenced `net10.0` libraries — `Broiler.Browser.Core`,
`Broiler.CSS`, `Broiler.Dom` and the rest — which were restored without it and fail
with `NETSDK1047`, asking for a `net10.0/android-arm64` target they have no reason
to have. Only the two heads read `BroilerAndroidAbis`, so nothing below them
changes and both publishes keep `--no-restore`.

The workflow checks that what it collected really is an APK — manifest at the
archive root, no `BundleConfig.pb` — and that `lib/` holds `arm64-v8a` and nothing
else, since a second ABI would mean the override did not take.

### Release signing

The workflow signs every package it ships with the release key, through
[`scripts/sign-android-packages.ps1`](../../scripts/sign-android-packages.ps1).
The key stays out of the repository, as the baseline requires: it reaches the
runner as four repository secrets, set under *Settings > Secrets and variables >
Actions*.

| Secret | Contents |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | The keystore file, base64-encoded (`base64 -w0 release.keystore`). PKCS#12 or JKS. |
| `ANDROID_KEYSTORE_PASSWORD` | Password of that keystore. |
| `ANDROID_KEY_ALIAS` | Alias of the signing key inside it. |
| `ANDROID_KEY_PASSWORD` | Password of that key. For a PKCS#12 keystore this is the store password — the format keeps one password for both. |

The two formats go through different tools, because they are checked by different
things:

* A bundle is signed with `jarsigner`. `apksigner` only speaks the APK signature
  schemes, which an `.aab` does not carry, and the JAR signature is what Play
  checks on upload.
* An APK is zipaligned and signed with `apksigner`, which writes the v2/v3
  signature blocks. An APK carrying only a JAR signature is refused at install
  from Android 11 on, so `jarsigner` is not an option here. Alignment comes first:
  `zipalign` moves entries within the archive, which would invalidate a signature
  already over them. For `minSdkVersion` 24 `apksigner` signs v2+v3 and leaves v1
  out, so `keytool -printcert -jarfile` cannot read these APKs — use
  `apksigner verify --print-certs -v`.

The keystore is decoded to a temporary file outside the workspace and deleted
again when the script returns; the passwords reach both signers through their
environment-variable forms (`-storepass:env`, `--ks-pass env:`), so they never
appear in the process list.

Signing is verified rather than assumed. Each package has to verify and — the part
that catches a wrong key — present the same certificate the keystore holds under
`ANDROID_KEY_ALIAS`; verifying alone only says the package is signed by *some*
key, which a debug key satisfies. Nor is an exit code enough on its own:
`jarsigner -verify` exits 0 on an *unsigned* jar, reporting the verdict in its
output only. The certificate's SHA-256 fingerprint is published in the package's
`BUILD-INFO.txt` and in the draft release notes, so a download can be checked
against it.

A missing or wrong secret fails the job rather than falling back to an unsigned
package, and the check runs before the build so it fails in seconds rather than
after an hour. The same check runs locally against the four values in the
environment, without building or signing anything:

```powershell
$env:ANDROID_KEYSTORE_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes('release.keystore'))
$env:ANDROID_KEYSTORE_PASSWORD = '…'; $env:ANDROID_KEY_ALIAS = '…'; $env:ANDROID_KEY_PASSWORD = '…'
./scripts/sign-android-packages.ps1 -ValidateOnly
```

Play App Signing, release channels, and versioning are still open — see
[the roadmap](../ROADMAP.md#a6--android-build-ci-and-delivery). What the pipeline
signs with today is the key those secrets hold.

## Non-goals and support boundary

- No .NET MAUI, Android XML content layouts, or Android widget copy of the UI.
- No Android fork of the HTML, CSS, JavaScript, layout, or rasterizer stacks.
- No broad filesystem permission and no in-app replacement for the system picker.
- Emulator support is verified; physical-device support remains gated by the
  checks in [the root roadmap](../ROADMAP.md#android-applications).

# Android application architecture

- **Status:** Proposed, with the input providers implemented. The
  `Broiler.Input.*.Android` backends exist and are unit-tested; nothing has run
  on a device. The rest of this document records the target topology and the
  decisions that must be frozen before the application projects start.
- **Last reconciled:** 2026-07-30

This document covers hosting the existing Broiler applications —
`Broiler.Browser.Core` and `Broiler.Writer.Core` — on Android. It is an
application hosting architecture. It does not propose a new rendering engine, a
new layout engine, or a second UI framework.

## Platform baseline

Android support is built on the .NET for Android workload only.

- The target framework is `net10.0-android`, provided by the `android` workload
  (`dotnet workload install android`) and the `Microsoft.Android.Sdk.*` SDK
  packs. This gives the Android class-library bindings and the APK/AAB build
  pipeline.
- **.NET MAUI is out of scope.** MAUI is a separate workload layered on the same
  Android SDK; nothing in this architecture requires it. Broiler already owns its
  own control set (`Broiler.UI`), its own rasterizer (`Broiler.Graphics`), and its
  own input contracts (`Broiler.Input`). Introducing MAUI would add a second,
  competing retained-mode UI stack and a second input route for no capability
  Broiler lacks.
- The application is a plain `Activity` hosting one `SurfaceView`. Broiler draws
  every pixel inside that surface. No Android `View` hierarchy, no XML layouts,
  and no Android widgets participate in application content. The only Android
  UI objects Broiler cooperates with are the ones the platform owns outright:
  the soft keyboard, the system insets, and the system file picker.

The decisions below must be frozen before the first application project is
added, because they change project files, CI images, and the support statement:
minimum and target API levels (against the workload's documented floor and the
Play target-API requirement in force at release), the shipped ABI set
(`android-arm64` is the required one; `android-x64` for emulator CI), the
managed runtime (the workload's default Mono configuration, or CoreCLR-on-Android
if it is selected and verified), the linker/AOT mode, and the signing and release
channel policy.

## Target and ownership

| Concern | Owner |
| --- | --- |
| Activity, surface, and lifecycle policy | `Broiler.Browser.Android` / `Broiler.Writer.Android` |
| Shared application behavior | `Broiler.Browser.Core` / `Broiler.Writer.Core`, unchanged |
| EGL/OpenGL ES presentation | `Broiler.Graphics.Android` |
| Frame rasterization | `Broiler.Graphics` core (`BImageRenderer`), unchanged |
| Neutral device/text contracts | `Broiler.Input` |
| Android touch, pen, keyboard, and IME providers | `Broiler.Input.Touch.Android`, `Broiler.Input.Pen.Android`, `Broiler.Input.Keyboard.Android`, `Broiler.Input.Text.Android` |
| Generic UI contracts, controls, gestures | `Broiler.UI` |
| Scoped-storage document access | The Android application layer |
| Full HTML/JavaScript browser engine | Unchanged; Android is a host, not an engine fork |

Android types (`Android.Views.MotionEvent`, `Android.Views.Surface`,
`Android.Content.Context`, SAF `Uri`s, `Java.Lang.*`) must not appear in
`Broiler.Graphics` core, `Broiler.Input` core, or `Broiler.UI`. They stop at the
`.Android` backend assemblies and the application project, exactly as Win32,
X11/evdev, and browser types do today.

## Rendering route

The important property of the current design is that **Broiler already
rasterizes on the CPU on every platform**. `LinuxOpenGlRenderer.Render` calls
`BImageRenderer.RenderToImage` and then presents the resulting bitmap
([`LinuxOpenGlRenderer.cs:64-78`](../../Broiler.Graphics/Broiler.Graphics.Linux.OpenGL/LinuxOpenGlRenderer.cs)).
The GPU backends upload one RGBA frame and blit it; they do not replay render
commands. The entry points used are `glGenTextures`, `glTexImage2D`,
`glBindFramebuffer`, `glFramebufferTexture2D`, `glBlitFramebuffer`, `glViewport`,
`glScissor`, `glClear`, and `eglSwapBuffers` — there is no shader pipeline.

Consequences for Android:

- `Broiler.Graphics.Android` is a small backend: create an EGL display and
  context against the `ANativeWindow` behind the `SurfaceView`, keep one texture
  sized to the surface, upload the frame, blit, swap. It is a near-translation
  of `LinuxOpenGlCpuPresentSession`, not new rendering work.
- Three concrete differences from the Linux path must be handled rather than
  copied. Linux binds desktop GL — `eglBindAPI(EGL_OPENGL_API)` with
  `EGL_RENDERABLE_TYPE = EGL_OPENGL_BIT`
  ([`LinuxOpenGlCpuPresentSession.cs:241-276`](../../Broiler.Graphics/Broiler.Graphics.Linux.OpenGL/LinuxOpenGlCpuPresentSession.cs));
  Android must use `EGL_OPENGL_ES_API` and `EGL_OPENGL_ES3_BIT`. Linux
  P/Invokes `libEGL.so.1`
  ([`LinuxEglNative.cs:41`](../../Broiler.Graphics/Broiler.Graphics.Linux.OpenGL/LinuxEglNative.cs));
  the Android soname is `libEGL.so`. And `glBlitFramebuffer` requires GL ES 3.0,
  which fixes the ES feature floor — an ES 2.0 fallback would need a
  textured-quad path instead.
- Whether to P/Invoke `libEGL.so`/`libGLESv3.so` directly (mirroring the Linux
  backend's style, and reusable across surface hosts) or to use the managed
  `Android.Opengl.EGL14` bindings is an implementation decision for the backend;
  the neutral `IBroilerRenderer`/`IBroilerSurface` contracts do not change either
  way.
- Rendering conformance does not fork. Android inherits the same rasterizer, so
  it adds no new WPT, Acid, or pixel-reference surface. Android-specific pixel
  claims are about *presentation* — colour space, scaling, and surface
  format — not about layout or paint correctness.

`Broiler.Graphics` is a git submodule. The Android backend therefore follows the
submodule workflow in [CLAUDE.md](../../CLAUDE.md): push to the component remote
and bump the pointer, or, if that push is denied, deliver the change as a patch
under [`patches/`](../../patches/README.md) with the pointer left alone.
`Broiler.UI`, `Broiler.Input`, `Broiler.Documents`, and the application projects
live in this repository and are ordinary commits.

Font discovery is the one core-graphics gap. `FallbackSystemFont.KnownFontPairs`
enumerates Linux, Windows, and macOS paths and `FontRoots()` scans
`/usr/share/fonts`
([`FallbackSystemFont.cs:164-190`](../../Broiler.Graphics/Broiler.Graphics/Rendering/FallbackSystemFont.cs));
neither knows about Android's `/system/fonts` (Roboto/Noto). Text renders through
Broiler's own TrueType/CFF parsing, so this is a path-list problem, not a text
engine problem — but until it is fixed, an Android build has no usable system
font.

## Host, lifecycle, and scheduling

The Android host implements `IUiHost`, `IUiClipboardHost`, and `IUiTextInputHost`
exactly as `BrowserUiHost` and the WebAssembly host do. `IUiHost` is five members
([`IUiHost.cs`](../../Broiler.UI/src/Foundation/Broiler.UI/Host/IUiHost.cs)), so
the host seam itself is not the hard part. The lifecycle is.

- **Frame pacing.** The desktop runners poll a `PeriodicTimer` at 16 ms and
  render when something changed
  ([`LinuxBrowserRunner.cs:71-125`](../../src/Broiler.Browser.Linux/LinuxBrowserRunner.cs)).
  Android must instead drive frames from `Choreographer` vsync callbacks and stop
  scheduling entirely when the activity is not resumed. A polling loop on a
  handheld is a battery defect, not a portability detail.
- **Surface loss is normal.** `SurfaceDestroyed` arrives on every rotation,
  backgrounding, and configuration change, and the EGL surface and every GPU
  resource die with it. The application must release and rebuild graphics
  resources across that boundary without losing document or page state.
  `BrowserApp.ReleaseGraphicsResources` and the existing `BDeviceLostException`
  are the hooks; the Android host owns the policy.
- **Threading.** Android delivers input, lifecycle, and IME callbacks on the main
  thread. `UiSession` is not thread-safe, so the host must marshal everything
  onto one loop through `IUiHost.Post`, which is the same discipline the Linux
  runner already applies to its posted-action queue.
- **Configuration changes.** Rotation, density changes, dark-mode changes, and
  multi-window resizes all resize the viewport and change `Scale`. `Scale` maps
  to `DisplayMetrics.Density`; the system font-scale setting must reach the
  UI text-scale token rather than being ignored.
- **Insets and system UI.** The drawable area is not the window. Status bar,
  navigation bar, display cutout, and — critically — the soft keyboard all
  subtract from it. The host publishes insets to the UI layer; content must
  never be permanently occluded by the keyboard.

## Input and text

This is the largest genuine gap, and it is a gap in the neutral layer rather
than in an Android backend.

`Broiler.Input` defined `TouchInputDevice`, `PenInputDevice`, and
`TextInputDevice` as abstract contracts that **no platform implemented** —
Windows and Linux ship keyboard and mouse providers only. The Android backends
are now the first implementations of all three, so Android is the first real test
of whether those contracts are right.

Those backends target plain `net10.0` and take primitive event data — the `int`
and `float` values read from `MotionEvent`, `KeyEvent`, and `InputConnection` —
instead of referencing `Mono.Android`. The host owns the Activity and the View,
so it is already the only component that can call the Android APIs; having it
forward primitives keeps `Android.Views` and `Java.Lang` out of the neutral
contracts by construction, and makes the translation layer testable on any host
without the workload. A boundary test pins the absence of those references.

Two neutral-layer defects still block touch end to end. The Android backends
produce contact identity, phase, and pressure; the UI layer throws them away:

1. `UiInputEvent.FromTouchContact` keeps only the position. `ContactId`,
   `TouchContactState`, and `Pressure` are all discarded
   ([`UiInputEvent.cs:83-87`](../../Broiler.UI/src/Foundation/Broiler.UI/Input/UiInputEvent.cs)),
   so the UI layer cannot tell a press from a release, cannot track a gesture,
   and cannot see a second finger. Multi-touch is structurally impossible until
   `UiInputEvent` carries contact identity and phase. `PenContact` loses the
   same information.
2. No control consumes `UiInputEventKind.TouchContact`. `StandardScrollView`
   handles the wheel and drags on the scrollbar thumb or track, and its pointer
   path requires `MouseButton.Left`
   ([`StandardScrollView.cs:137-276`](../../Broiler.UI/src/Implementations/Standard/Layout/Broiler.UI.ScrollView.Standard/StandardScrollView.cs)),
   which a touch-synthesized event does not carry. There is no content-drag
   scrolling and no fling. On a touch device that means the primary scrolling
   gesture does nothing.

So Broiler.UI needs a gesture layer — tap, long-press, drag, fling with
momentum, and pinch-zoom — recognized once over neutral contact streams and
shared by every control, plus touch-appropriate hit-target sizes and
selection/caret handles. Building gesture recognition inside the Android backend
would strand it there and leave Windows and Linux touch to reimplement it.

Text input is the second gap. `IUiTextInputHost` exposes only `PublishCaret` and
`ClearCaret`
([`IUiTextInputHost.cs`](../../Broiler.UI/src/Foundation/Broiler.UI/Host/IUiTextInputHost.cs)).
Android's `InputConnection` is a two-way protocol: the IME asks the editor for
text around the cursor and the current selection, sets and clears a composing
region, and commits or replaces spans. Satisfying it requires an editor-side
contract that Broiler.UI does not have yet. That contract is not Android-specific
— it is what Windows TSF and browser composition need too — so it belongs in
`Broiler.UI`, with `Broiler.Input.Text.Android` supplying the Android half.
`IAndroidEditorTextSource` and `AndroidTextEditRequest` in that assembly are the
Android-side statement of the missing contract: the queries an IME makes, and the
mutations (`deleteSurroundingText`, `setSelection`, `setComposingRegion`) that
`TextInputDevice` has no way to express. They should collapse into the neutral
contract once it exists rather than becoming a parallel abstraction.
Soft-keyboard show/hide, keyboard type selection, and the IME action button are
host responsibilities driven by editor focus.

`MotionEvent` maps cleanly onto the neutral contracts once they carry enough
data: pointer id → `ContactId`, `ActionMasked` → `TouchContactState`,
`GetPressure` → `Pressure`, and `GetToolType` separates finger, stylus, and
eraser so pen events route to the pen contract with tilt and pressure.

## Documents, storage, and capability policy

Android scoped storage does not permit the desktop file model. `StandardFileDialog`
builds its places list from `Environment.SpecialFolder` values and
`Directory.GetLogicalDrives`, and enumerates directories directly
([`StandardFileDialog.cs:616-799`](../../Broiler.UI/src/Implementations/Standard/Shell/Broiler.UI.FileDialog.Standard/StandardFileDialog.cs)),
while `WriterApp` reads and writes through `File.ReadAllBytes` /
`File.WriteAllBytes`. On Android those paths reach only the app-private sandbox.

The decision is to use the system picker (`ActionOpenDocument` /
`ActionCreateDocument`) rather than to draw a file browser Broiler is not allowed
to populate. That requires the Writer's open/save path to be expressible as a
stream pair plus a display name, instead of a full path — a change that also
benefits the WebAssembly Writer, which has the same constraint. Persistable URI
permissions cover reopening recent documents; the app-private directory remains
the right place for autosave and crash recovery.

The document stack itself ports unchanged: `Broiler.Documents` (RTF, DOCX, HTML,
Markdown) and the media codecs are platform-neutral managed code. The one native
path in the image codecs is a Windows-only WIC WebP accelerator guarded by an
`OperatingSystem.IsWindows()` check and backed by a managed decoder, so Android
takes the managed route with no work — but the managed WebP path becomes
load-bearing for the browser there and should be measured, not assumed.

For the browser, `BrowserApp` fetches through `HttpClient`
([`BrowserApp.cs:346`](../../src/Broiler.Browser.Core/BrowserApp.cs)), which needs
the `INTERNET` permission and an explicit cleartext-traffic policy. The existing
preview safety notice — *JavaScript is not a sandbox* — becomes materially more
serious on a phone holding personal data, so the Android browser ships against
controlled content with the same honest statement, not a broader claim.

One runtime interaction is worth recording: `Broiler.JavaScript.ExpressionCompiler`
emits IL through `System.Reflection.Emit`, while `Broiler.JavaScript.Portable` is
marked trimmable and does not. Android permits JIT, so the emitting path can work
under the default Mono configuration, but it is incompatible with a full-AOT
build. The linker/AOT decision and the JS execution mode must therefore be made
together, and the Writer (which needs no JS engine) can adopt a stricter mode
than the browser.

## Non-goals

- No .NET MAUI, no Android XML layouts, and no Android widgets in application
  content.
- No second rendering or layout engine, and no Android-specific fork of the
  HTML/CSS/JS stack.
- No new WPT or Acid conformance surface; Android inherits the shared rasterizer.
- No claim of Android support until the gates in
  [the root roadmap](../ROADMAP.md#android-applications) are met on real hardware.
  A build that starts on an emulator is not support evidence.

# Broiler.Media Component Roadmap

**Status:** In progress — foundation, image, and audio phases landed; see the
implementation-status tracker below.  
**Date:** 2026-06-27 (plan) · reconciled with the codebase 2026-07-12  
**Scope:** Architecture and migration plan. Sections 1–16 are the original plan and remain the
design of record; the tracker in section 0 records what has actually shipped, so the plan's
"no implementation" framing in §2/§5 is historical.

## 0. Implementation status (reconciled 2026-07-12)

`Broiler.Media` now exists at `Broiler.Media/` with all **7 runtime assemblies + 7 test projects**
(~11.7k LOC, no `NotImplementedException`/TODO stubs), wired into `Broiler.slnx`. The original
header ("Proposed / no implementation") was stale. Actual per-phase state:

| Phase | State | Evidence / notes |
| --- | --- | --- |
| 0 — Freeze / ADRs | **Done** | 5 ADRs in `Broiler.Media/docs/adr/0001…0005`, baselines + API inventory under `Broiler.Media/docs/`. |
| 1 — Scaffold 7 assemblies + shared contracts | **Done** | Base `MediaCodec`/immutable `MediaCodecCatalog`/probe/input/limits/output; arch + catalog tests. Dependency graph matches §6.1. |
| 2 — Image contracts + managed codecs | **Done (exceeded)** | `ImageBuffer`/`ImageFrame`/`ImageSequence` + real PNG/APNG, JPEG (baseline+progressive), BMP. **GIF and WebP — listed "future" in §6.8 — are now implemented** and registered by `ManagedImageCodecs.CreateCodecs()` (five codecs). Update `ImageEncodeFormat` expectations accordingly. |
| 3 — Graphics consumes decoded `ImageBuffer` | **Done (2026-07-12)** | No PNG/JPEG/BMP parser or `BImageCodec`/`UseManagedIfUnset` remains in Graphics. Exit gate now met: `Broiler.Graphics.csproj` references only the `Broiler.Media.Image` **abstraction**; `Imaging/MediaImageBridge.cs` no longer constructs a catalog or names concrete codecs — it reads an injected catalog via the new `Imaging/BImageCodecs.cs` registration seam and selects encoders by MIME through the abstraction-only `ImageCodecCatalogExtensions.FindEncoder`. The application composition root registers codecs explicitly (`BImageCodecs.Use(new MediaCodecCatalog(ManagedImageCodecs.CreateCodecs()))`) at each entry point (Cli/Wpt/DevSite/App.Graphics + the Graphics/Windows/Linux test harnesses + Cli.Tests). Verified: Graphics builds abstraction-only; Graphics.Tests 55/55, Graphics.Windows.Tests (Direct2D) 10/10, HTML-render decode path 31/31. |
| 4 — HTML image consolidation | **Done (2026-07-12)** | The bridge's parallel detector in `src/Broiler.HtmlBridge.Rendering/ImagePipeline.cs` (own `ImageFormat` enum, `DetectFormat`/`DetectFormatFromBytes`, `DecodedImage`, dead `FetchExternalImageBytes`, superseded `SvgParser`/`SvgRenderer`) was dead/test-only and has been removed; the live raster path already routes through `MediaCodecCatalog` via `Broiler.HTML.Image.BBitmap.Decode`. Only the live Canvas 2D recorder remains (`CanvasCommandRecorder.cs`). SVG-vs-raster sniffing stays bridge-owned via `BSvgRasterizer.IsSvgData`. This also satisfies the Phase 8 "no duplicate image model/format detector in the HTML bridge" item. |
| 5 — Audio abstraction + WAVE PCM | **Done** | `Broiler.Media.Audio` abstraction + real streaming RIFF/WAVE PCM decoder (`WaveAudioCodec`, 8/16/24/32-bit, chunk validation, limits/backpressure). Only PCM advertised. |
| 6 — Video + `IMFMediaEngine` | **Done (2026-07-12)** | Real COM implementation (`MediaFoundationNative`/`…MediaEngine`/`…VideoSession`, `net10.0-windows`). §6.6 ownership now correct: the HWND presentation target is **declared and owned by `Broiler.Graphics.Windows`** as `HwndVideoOutput` (+ `HwndVideoTargetChanged*`), implementing `Broiler.Media.Video.IVideoOutput`; `Broiler.Graphics.Windows → Broiler.Media.Video` (abstraction only) and `Broiler.Media.Video.MediaFoundation → Broiler.Graphics.Windows` (borrows the target, drives `IMFMediaEngine`, never creates/destroys the window) are the two mandated edges. Verified: Graphics.Windows 10/10, MediaFoundation 11/11 (incl. arch-boundary test rewritten to allow the sanctioned edge), Media architecture allowlist 12/12. |
| 7 — Browser playback integration | **In progress (2026-07-12)** | Foundational app/HTML-layer **playback engine** landed as new `src/Broiler.Playback` (references only the Media abstractions — no impl/HTML deps): `IMediaPlaybackSession`, an HTMLMediaElement-shaped state machine (readyState / paused / currentTime / duration / ended; play/pause/seek/stop); `AudioPlaybackSession` (eager WAVE decode → real `BufferedAudioOutput` sink + an `Advance`-driven clock); `VideoPlaybackSession` (adapter over the `IVideoSession`/`IMFMediaEngine` session); and `MediaPlayer` (probe-based audio/video routing, `CanPlayType`, deterministic `UnsupportedFormat` capability error). 17 tests green: audio decode→output end-to-end, pause halts the clock, seek repositions/clamps, replay-after-end, dispose idempotent + blocks transport, video-adapter state/event mapping, and capability errors. **Remaining:** HTML DOM media-element classes (`HTMLMediaElement`/`Video`/`Audio`/`Source`) + JS bindings + `<source>` selection; wire the engine into `<audio>`/`<video>` rendering (replace the `DomParser` black-box placeholder); a real audio-device sink + real-time driver; a real MediaFoundation video→HWND end-to-end; and removing the WPT media skips one format at a time. |
| 8 — Compatibility cleanup / release | **In progress (2026-07-12)** | **Cleanup done:** no duplicate image model/format detector remains in Graphics or the HTML bridge, and no expired/obsolete codec adapters exist (both verified); dependency architecture tests 12/12 pass. **Release hardening advanced:** the component `README.md` was rewritten to release quality (assemblies + dependency direction, supported-format table, security guidance, packaging notes, ADR links — this README is packed into every package); per-assembly packaging verified — all 7 runtime assemblies `dotnet pack` cleanly into `0.1.0-preview.1` packages with correct dependency metadata (including the cross-repo `Broiler.Graphics.Windows` dependency), Apache-2.0, symbol packages + SourceLink. **Remaining:** end-to-end package consumption from a feed without the aggregate repo (needs the whole `Broiler.*` suite published), formal performance gates (§12.6 — conformance + malformed-input gates already exist, perf benchmarks do not), a public-name / XML-doc freeze after consumer review, and the actual version bump + publish. |

**Open follow-ups:** (a) finish Phase 7 — DOM media-element classes + JS bindings + `<source>` selection,
wire `src/Broiler.Playback` into `<audio>`/`<video>` rendering, a real audio-device sink + real-time driver,
a real MediaFoundation video→HWND end-to-end, and incremental WPT media-skip removal (the playback **engine**
already landed, see the §0 tracker); (b) finish Phase 8 release hardening — feed-based package consumption
validation, performance gates (§12.6), public-name/XML-doc freeze after consumer review, and the version
bump + publish (cleanup, README, and per-assembly `dotnet pack` are done); (c) optional: apply the same
explicit-catalog seam to the parallel `Broiler.HTML.Image.BBitmap`, which still builds its own managed
catalog (legitimate today — it directly owns the impl reference — but the same smell as the now-closed
Graphics case).

**Phase 6 exit-gate closure note (2026-07-12).** The HWND video target (formerly
`MediaFoundationBorrowedHwndVideoOutput`, internal to the backend) was relocated to
`Broiler.Graphics.Windows` as `HwndVideoOutput` — where the window lifetime already lives (`Direct2DWindow`)
— and its change-notification types became `HwndVideoTargetChangeKind` / `HwndVideoTargetChangedEventArgs`.
The type is MF-agnostic (its only native call, `IsWindow`, is a Win32 user32 P/Invoke that now lives with the
other Graphics.Windows interop; the orphaned copy in `MediaFoundationNative` was removed). Dependency
direction is a clean DAG: `MediaFoundation → {Media.Video, Graphics.Windows}`, `Graphics.Windows →
{Graphics, Media.Video}` — no cycle. The abstraction stays HWND/Graphics-free (enforced by
`Broiler.Media.Tests` neutrality + allowlist tests, both updated).

**Phase 3 exit-gate closure note (2026-07-12).** Graphics is now decoupled from the codec implementation via
an explicit registration seam rather than by threading a catalog through every renderer/`BBitmap` call. This
matches §8.5 ("composition root registers concrete codecs explicitly"): `BImageCodecs.Use(...)` is an explicit,
application-owned registration with **no** auto-populating default and no managed fallback, so it does not
reintroduce the `BImageCodec.Current`/`UseManagedIfUnset` global the roadmap prohibits — an unregistered
process fails fast with an actionable error instead of silently self-selecting managed codecs.

## 1. Executive decision

Create `Broiler.Media` as a standalone component containing a small shared media
foundation, one abstract codec assembly for each media kind, and one concrete
implementation assembly for each kind.

The required runtime assembly set is:

| Layer | Assembly | Primary responsibility |
|---|---|---|
| Shared abstraction | `Broiler.Media` | Abstract `MediaCodec`, probing, codec selection, common input, limits, diagnostics, and the base output contract |
| Audio abstraction | `Broiler.Media.Audio` | Abstract `AudioCodec`, decoded audio models, audio decode options, and typed audio output |
| Audio implementation | `Broiler.Media.Audio.Managed` | Initial managed audio decoders, beginning with RIFF/WAVE PCM |
| Video abstraction | `Broiler.Media.Video` | Abstract `VideoCodec`, video metadata/session contracts, decode options, and a typed video presentation target |
| Video implementation | `Broiler.Media.Video.MediaFoundation` | First real video implementation, built exclusively around Windows `IMFMediaEngine` and an externally owned HWND |
| Image abstraction | `Broiler.Media.Image` | Abstract `ImageCodec`, pixel/frame/animation models, image decode options, and typed image output |
| Image implementation | `Broiler.Media.Image.Managed` | Existing managed PNG/APNG, JPEG, and BMP codecs moved from `Broiler.Graphics` |

`Broiler.Media` is the base assembly, not an everything-bundle. Applications opt
into the media kinds and implementations they need. An optional convenience
package may be considered later, but it must not become a dependency of the
abstraction assemblies.

The first real video implementation is fixed: `IMFMediaEngine` through
`Broiler.Media.Video.MediaFoundation`. FFmpeg and other software/native codec
stacks are staged future providers and are not part of the first implementation
milestone.

The component is decode-first. Decoding, probing, media-session control, decoded
audio/image transfer, and codec selection are its core responsibilities. Existing
image encoders must remain functional during the move, but new encoding work is
secondary. `Broiler.Media` must not gain a graphics surface backend. For the first
video path, `IMFMediaEngine` presents video to an HWND declared and owned by
`Broiler.Graphics.Windows`; networking policy, HTML element behavior, window
creation, and rendering remain outside Media.

## 2. Current-state findings

### 2.1 Image codecs currently live in the graphics component

`Broiler.Graphics/Broiler.Graphics/Imaging` currently contains 21 C# files and
approximately 3,202 lines. The code includes:

- `IBImageCodec`, the process-wide static `BImageCodec` selector, and
  `ManagedImageCodec`;
- `BPixelBuffer`, `BImageFrame`, `BImageSequence`, and `BImageEncodeFormat`;
- PNG and APNG decode/encode, including Adam7 and animated frame compositing;
- baseline and progressive JPEG decode plus baseline JPEG encode;
- uncompressed 24/32-bit BMP decode and 32-bit BMP encode; and
- codec-specific CRC, bit-stream, DCT, Huffman, and format-table helpers.

These are encoded-media and decoded-image responsibilities. They do not need a
window, render list, graphics device, or Direct2D, so they belong in
`Broiler.Media.Image` and `Broiler.Media.Image.Managed`.

The current codec path has two architectural limitations that the extraction
should correct:

1. `BImageCodec` is mutable process-wide state. Renderer construction can call
   `UseManagedIfUnset`, so backend creation has the side effect of choosing a
   decoder for the entire process.
2. `ManagedImageCodec` is an aggregate dispatcher. Format probing, codec
   selection, and each concrete format implementation are not independently
   addressable.

The move should replace hidden global registration with an explicit codec
catalog supplied by the application composition root.

### 2.2 Not every type containing "Image" belongs in media

`Broiler.Graphics` also owns image-named types that are genuinely rendering
types. Moving them merely because of their names would reverse the dependency
and mix decoding with drawing.

| Current area/type | Target owner | Reason |
|---|---|---|
| `BPixelBuffer` | `Broiler.Media.Image` | Neutral decoded pixel exchange model |
| `BImageFrame`, `BImageSequence` | `Broiler.Media.Image` | Decoded still/animated media models |
| `BImageEncodeFormat` | `Broiler.Media.Image` | Codec format/capability model |
| `IBImageCodec`, `BImageCodec` | Replaced by `ImageCodec` plus the shared catalog | Codec abstraction and selection belong to media |
| `ManagedImageCodec` and PNG/JPEG/BMP internals | `Broiler.Media.Image.Managed` | Concrete codec implementation |
| `BBitmap` | Split boundary; drawing surface remains in Graphics | It is mutable, opens `BCanvas`, uses `BColor`, and acts as a render target |
| `BCanvas` | `Broiler.Graphics` | CPU raster drawing API, not a decoder |
| `BImageSurface`, `BImageRenderer` | `Broiler.Graphics` | Render surfaces and command replay |
| `BImageHandle`, `Direct2DImageStore` | `Broiler.Graphics` | Backend resource identity and GPU upload/storage |
| Direct2D BGRA-premultiplication | `Broiler.Graphics.Windows` | Backend-specific pixel upload conversion |

`BBitmap` should stop owning codec discovery. It may keep explicit conversion
helpers between a graphics bitmap and the media `ImageBuffer`, but final decode
and encode convenience methods must either accept an `ImageCodec`/catalog or
live in media-side extensions. The final Graphics assembly must not select a
concrete codec implementation.

### 2.3 Graphics currently decodes at the rendering boundary

Both the managed renderer and Direct2D renderer currently accept encoded bytes,
select the managed image codec, decode the bytes, and then upload the result.
The final boundary should accept an already-decoded `ImageBuffer`. Decoding is
performed by a media service before the graphics call.

This produces a strict pipeline:

```text
resource loader -> media probe/decoder -> decoded ImageBuffer
                -> graphics upload -> BImageHandle -> render list
```

It also allows the same decoded image to be sent to Direct2D, a CPU renderer, a
test sink, an encoder, or another consumer without coupling the codec to a
graphics backend.

### 2.4 HTML has a second, partial image pipeline

`src/Broiler.HtmlBridge.Rendering/ImagePipeline.cs` separately defines
`ImageFormat`, `DecodedImage`, format detection, data-URI decoding, synchronous
HTTP/file loading, SVG parsing, SVG draw commands, and canvas commands.

That file should not move as a unit:

- magic-byte probing and the neutral decoded raster model move or adapt to
  `Broiler.Media.Image`;
- URL resolution, HTTP/file fetching, caching, CSP/CORS policy, and data-URI
  policy remain in the HTML resource-loading layer;
- SVG parsing and rasterization remain in the vector/HTML rendering path until
  a separate vector-media decision is made; and
- canvas commands remain rendering behavior.

The media component accepts bytes or a stream plus optional hints. It does not
fetch URLs. This removes the current synchronous network helper from the decode
boundary and keeps browser policy out of codecs.

### 2.5 Audio and video are not decoded today

The current browser stack recognizes common audio/video MIME types and creates
placeholder replaced elements. WPT cases requiring external video/audio playback
are explicitly skipped because stream decoding is unsupported. This means:

- image extraction is a behavior-preserving migration of working code;
- audio is a new capability with a small managed baseline available through
  RIFF/WAVE PCM; and
- video is a new, Windows-first capability whose first real implementation is
  `IMFMediaEngine`; alternate decoder stacks are deliberately deferred.

No roadmap phase may claim browser audio/video support merely because the codec
assemblies exist. HTML media elements also need source selection, loading,
timelines, playback state, output integration, and rendering.

### 2.6 Repository topology affects the migration

`Broiler.Graphics` and `Broiler.HTML` are Git submodules, and `Broiler.HTML`
contains a nested `Broiler.Graphics` submodule at the same revision. Some current
Graphics project references resolve through that nested checkout. A media
extraction therefore spans component repositories and cannot be safely landed as
an uncoordinated file move.

The recommended topology is a standalone `Broiler.Media` repository included as
one top-level component in the aggregate Broiler workspace. Downstream standalone
components should consume versioned Media packages or one explicitly pinned
checkout; they must not create independent source copies. Phase 0 must record the
canonical checkout and package/project-reference policy before files move.

## 3. Goals

1. Provide exactly one common abstract codec base class and one abstract derived
   codec class for Audio, Video, and Image.
2. Put every abstraction and every concrete implementation category in its own
   assembly as listed in section 1.
3. Make codec discovery and selection explicit, deterministic, and testable.
4. Make buffer-producing decoding stream-oriented and cancellable, with bounded
   allocation and backpressure; define deterministic session cancellation and
   lifetime rules for `IMFMediaEngine` video presentation.
5. Move neutral image codec/data responsibilities out of `Broiler.Graphics`
   without moving graphics surfaces, canvases, or backend resource handles.
6. Preserve all currently working PNG/APNG, JPEG, and BMP behavior, including
   image encoding, during migration.
7. Give audio, video, and image outputs a consistent lifecycle while allowing the
   first video implementation to present directly to an externally owned HWND.
8. Keep the abstraction assemblies platform-neutral, safe-code compatible,
   trimming-friendly, and AOT-friendly where their dependencies permit.
9. Define security limits for untrusted media before browser integration.
10. Prepare the browser for real `<audio>` and `<video>` support without putting
    HTML policy inside the media component.

## 4. Non-goals

- Implement every browser codec in the first release.
- Put network loading, caches, cookies, CSP, CORS, or URL policy in Media.
- Move `BCanvas`, render lists, Direct2D, GPU resources, windows, or controls out
  of Graphics.
- Add a surface, swap-chain, HWND, or graphics-presentation backend to
  `Broiler.Media`.
- Use FFmpeg or another alternate video provider as the first real video
  implementation.
- Treat SVG as a raster codec during the initial extraction.
- Build audio-device or window-system output into the abstraction assemblies.
- Implement HTML media-element state machines in Media.
- Introduce a plugin/module initializer that silently mutates global codec state.
- Rewrite working PNG/JPEG/BMP algorithms during the assembly move.
- Guarantee binary compatibility without first choosing and documenting a
  compatibility policy.

## 5. Terminology and responsibility rules

The implementation must distinguish these concepts:

| Concept | Meaning | Owner |
|---|---|---|
| Encoded input | Compressed/container bytes from a stream | Caller/resource loader |
| Probe | Bounded inspection used to identify a format and confidence | Media codec/catalog |
| Container/demuxer | Extracts timed encoded tracks from formats such as MP4, WebM, Ogg, or WAV | Implementation detail initially; separable future component |
| Codec | Converts encoded data to/from decoded samples or frames | Media type plus implementation assemblies |
| Decoded buffer/frame | Typed audio samples or image pixels; optional future video frames | Audio/Image abstractions; Video only when a later frame-output provider needs it |
| Output sink | Receives decoded typed data or identifies a presentation target | Media type abstraction; concrete output/target elsewhere |
| Graphics upload | Converts decoded pixels into a renderer-owned resource | Graphics backend |
| HWND video target | Non-owning Windows presentation endpoint for `IMFMediaEngine` | Declared and owned by `Broiler.Graphics.Windows` |
| Playback | Source selection and HTML controls/events are integration concerns; engine playback is delegated to `IMFMediaEngine` | MediaFoundation implementation plus application/HTML integration |

Two rules are especially important:

1. MIME type, file extension, and URL are hints. A codec is selected primarily by
   bounded content probing.
2. A decoder does not fetch its own input and does not choose its own output
   device.
3. Media never creates or owns the HWND used for video. The Media Foundation
   implementation receives a live target from `Broiler.Graphics.Windows` and
   treats its lifetime and thread affinity as external constraints.

## 6. Target component and assembly structure

```text
Broiler.Media/
  Broiler.Media.slnx
  Broiler.Media/
    Broiler.Media.csproj
  Broiler.Media.Audio/
    Broiler.Media.Audio.csproj
  Broiler.Media.Audio.Managed/
    Broiler.Media.Audio.Managed.csproj
  Broiler.Media.Video/
    Broiler.Media.Video.csproj
  Broiler.Media.Video.MediaFoundation/
    Broiler.Media.Video.MediaFoundation.csproj
  Broiler.Media.Image/
    Broiler.Media.Image.csproj
  Broiler.Media.Image.Managed/
    Broiler.Media.Image.Managed.csproj
  Broiler.Media.Tests/
  Broiler.Media.Audio.Tests/
  Broiler.Media.Audio.Managed.Tests/
  Broiler.Media.Video.Tests/
  Broiler.Media.Video.MediaFoundation.Tests/
  Broiler.Media.Image.Tests/
  Broiler.Media.Image.Managed.Tests/
```

Test projects are separate assemblies but are not shipped runtime components.
`Broiler.Media.Video.MediaFoundation` is Windows-only and targets the Windows
framework needed by `IMFMediaEngine`. FFmpeg is not included in this initial
runtime set; it may be introduced later as a separately named provider.

### 6.1 Dependency direction

```text
Broiler.Media.Audio.Managed  -> Broiler.Media.Audio  -> Broiler.Media
Broiler.Media.Video.MediaFoundation -> Broiler.Media.Video -> Broiler.Media
Broiler.Media.Video.MediaFoundation -> Broiler.Graphics.Windows
Broiler.Media.Image.Managed  -> Broiler.Media.Image  -> Broiler.Media

Broiler.Graphics             -> Broiler.Media.Image -> Broiler.Media
Broiler.Graphics.Windows     -> Broiler.Graphics + Broiler.Media.Video

Application composition root -> chosen implementation assemblies
HTML resource/playback layer -> media abstractions and application catalog
```

Forbidden references:

- `Broiler.Media` must not reference Audio, Video, Image, Graphics, HTML, or a
  concrete implementation.
- A typed abstraction must not reference a sibling typed abstraction.
- Implementations must not reference HTML. The only approved Graphics dependency
  is `Broiler.Media.Video.MediaFoundation -> Broiler.Graphics.Windows`, and only
  for the HWND presentation-target contract.
- The platform-neutral `Broiler.Graphics` core must not reference Audio or Video.
- `Broiler.Graphics.Windows` may reference `Broiler.Media.Video` solely to declare
  the Windows video target that exposes its externally owned HWND.
- No abstraction assembly may reference Media Foundation, FFmpeg, or another
  native/platform package.
- No Media assembly may implement a graphics surface, swap chain, or window.
- No cycle may be resolved through service locators, reflection-only loading, or
  module-initializer side effects.

### 6.2 `Broiler.Media`

The shared assembly owns only concepts common to all media kinds:

- abstract `MediaCodec`;
- `MediaKind` (`Audio`, `Video`, `Image`);
- codec identity, supported format/MIME/extension descriptors, and capability
  flags;
- bounded probe request/result and confidence;
- stream/input abstraction and source hints;
- common decode limits, cancellation, progress, and diagnostics;
- normalized media timestamps/time bases where needed;
- the explicit immutable `MediaCodecCatalog`; and
- a minimal base output lifecycle contract.

It must not define an untyped `Decode` method that returns `object`. Typed decode
operations belong to the derived classes.

### 6.3 `Broiler.Media.Audio`

This assembly owns:

- abstract `AudioCodec : MediaCodec`;
- audio stream information: sample rate, channels/channel layout, duration, and
  source sample format;
- decoded `AudioBuffer` with sample format, frame count, timestamp, and duration;
- `AudioDecodeOptions` including requested output sample format and limits;
- typed `IAudioOutput`; and
- audio-specific exceptions/diagnostics only when the shared model is
  insufficient.

The canonical initial decoded format should be interleaved signed 16-bit PCM or
32-bit floating point, selected explicitly. The choice must be frozen by an ADR
before public API stabilization; implicit platform-endian buffers are forbidden.

### 6.4 `Broiler.Media.Audio.Managed`

The first concrete implementation is a dependency-free RIFF/WAVE PCM decoder.
It validates chunk lengths, skips unknown chunks safely, supports non-seekable
input where practical, and emits bounded `AudioBuffer` chunks.

Initial formats:

- PCM 8/16/24/32-bit little-endian;
- IEEE float 32-bit if the sample model supports it; and
- mono/stereo first, followed by validated multi-channel layouts.

MP3, AAC, Vorbis, Opus, and FLAC are later codec decisions. Their names must not
be advertised until real fixtures, malformed-input tests, and streaming behavior
exist.

### 6.5 `Broiler.Media.Video`

This assembly owns:

- abstract `VideoCodec : MediaCodec`;
- video stream information: coded/display dimensions, pixel aspect ratio,
  duration, time base, frame-rate hint, rotation, and color metadata;
- `VideoDecodeOptions` and session/open options;
- video session state, events, cancellation, seek, and lifetime contracts; and
- typed `IVideoOutput`/presentation-target lifecycle without any surface API.

The first contract must support `IMFMediaEngine` direct presentation without
forcing decoded frames through managed memory. A later frame-output provider may
add `VideoFrame` with planes/strides and timestamps, but that is staged work and
is not a prerequisite for the Media Foundation baseline.

### 6.6 `Broiler.Media.Video.MediaFoundation`

This is the first and only real video implementation in the initial roadmap. It
is a Windows-only adapter built around `IMFMediaEngine`.

The baseline presentation pipeline is:

```text
media source -> IMFMediaEngine -> video presentation -> externally owned HWND
                                                     (`Broiler.Graphics.Windows`)
```

Boundary rules:

- `Broiler.Graphics.Windows` creates, declares, owns, and destroys the HWND. It
  may expose a small `HwndVideoOutput`/Windows video-target contract implementing
  the typed `Broiler.Media.Video` output lifecycle.
- `Broiler.Media.Video.MediaFoundation` borrows that target and drives
  `IMFMediaEngine`; it never creates a window and never assumes ownership of the
  handle.
- Media contains no `IBroilerSurface` equivalent, swap chain, Direct2D surface,
  GPU image store, or generic presentation backend.
- HWND validity, UI-thread affinity, resize notification, teardown ordering, and
  engine callbacks must be explicit and tested.
- Media Foundation/COM startup and shutdown, callbacks, event translation,
  diagnostics, and engine-session disposal stay inside the implementation.
- Source selection and HTML events remain in the browser/application integration
  layer.

FFmpeg and other alternative frame-decoding backends are staged until this
pipeline is stable. They require their own later roadmap/ADR and separately named
implementation assembly; they must not reshape the initial API around a surface
backend that `IMFMediaEngine` does not need.

### 6.7 `Broiler.Media.Image`

This assembly owns:

- abstract `ImageCodec : MediaCodec`;
- `ImageBuffer` and its dimensions, pixel format, alpha representation, stride,
  and color metadata;
- `ImageFrame`, frame placement/disposal/blend metadata, and duration;
- `ImageSequence`, animation loop count, and canvas dimensions;
- image decode/encode options and resource limits;
- typed `IImageOutput`; and
- conversions that are media-neutral and do not draw.

The initial canonical compatibility format is tightly packed, row-major,
straight-alpha RGBA8 because that is the current `BPixelBuffer` contract.
Stride and pixel-format fields should nevertheless be present in the final model
so video/native codecs are not forced through needless copies later.

### 6.8 `Broiler.Media.Image.Managed`

Move the existing algorithms here with no intentional behavioral rewrite:

- `PngImageCodec`: PNG and APNG probe, decode, and encode;
- `JpegImageCodec`: baseline/progressive JPEG decode and baseline encode;
- `BmpImageCodec`: uncompressed 24/32-bit BMP decode and 32-bit encode; and
- internal CRC, bit, DCT, Huffman, and format helpers.

Prefer one concrete codec instance per encoded format. The catalog, not an
aggregate `ManagedImageCodec`, selects the winning codec. A managed default set
may expose a helper that returns all three codecs without installing process-wide
state.

GIF and WebP — originally staged as future additions — are now implemented and registered
by `ManagedImageCodecs.CreateCodecs()` (decode + encode, with animation for both). The
managed default set therefore exposes five image codecs: PNG/APNG, JPEG, BMP, GIF, WebP.

## 7. Abstract class hierarchy and contract rules

The required hierarchy is:

```text
abstract MediaCodec
  abstract AudioCodec
    concrete codecs in Broiler.Media.Audio.Managed or another Audio implementation
  abstract VideoCodec
    IMFMediaEngineVideoCodec in Broiler.Media.Video.MediaFoundation
  abstract ImageCodec
    PngImageCodec / JpegImageCodec / BmpImageCodec in Broiler.Media.Image.Managed
```

### 7.1 `MediaCodec` responsibilities

The common base exposes metadata and probing, not typed decode results:

| Member concept | Requirement |
|---|---|
| Stable codec ID and display name | Required; independent of localized text |
| `MediaKind` | Fixed by the derived class |
| Supported format/MIME/extension descriptors | Read-only; hints do not override probing |
| Capability flags | Decode, encode, metadata, animation, streaming, seek, hardware, and similar explicit flags |
| Probe operation | Bounded, side-effect-free, and repeatable |
| Priority/tie-break metadata | Deterministic and visible to diagnostics |

The base must not know `AudioBuffer`, `VideoFrame`, or `ImageFrame`.

### 7.2 Typed derived codec responsibilities

Each typed codec defines its own decode/session contract and typed options/output.
Decode or media-session opening is required. `VideoCodec` must permit direct
presentation through `IMFMediaEngine` without requiring a decoded-frame callback.
Encode may be a capability-gated virtual operation so existing image encoders
survive without making encoding mandatory for every codec.

Unsupported operations must fail through one documented media exception/result,
not arbitrary `NotImplementedException` or silent empty output.

### 7.3 Catalog behavior

`MediaCodecCatalog` should be immutable after construction and supplied through
dependency injection or an explicit constructor. Selection order is:

1. filter by requested media kind;
2. run bounded content probes;
3. apply MIME/extension hints only as a confidence adjustment;
4. reject codecs that violate requested capabilities or limits;
5. select the highest confidence; and
6. use a stable registered priority and codec ID as tie-breakers.

The result must include selection diagnostics so an unsupported media error can
explain which codecs probed the input and why none was selected.

There is no mutable `Current` singleton in the final design. Applications may
construct and reuse one catalog, but ownership is explicit.

### 7.4 Input contract

The input abstraction must support:

- seekable and non-seekable streams;
- a bounded prefix for probing without losing bytes before decode;
- optional MIME type, extension, source URI, and declared length hints;
- asynchronous reads and cancellation;
- caller ownership of the underlying stream by default; and
- a clear maximum input/probe budget.

File and network opening are caller responsibilities. A byte-array convenience
adapter is acceptable, but it must use the same limits and probe path.

### 7.5 Output contract

The base `IMediaOutput` is a small lifecycle contract. Typed outputs extend it:

- `IAudioOutput` receives stream information and `AudioBuffer` chunks;
- `IVideoOutput` identifies the presentation lifecycle/target; the initial Windows
  implementation is backed by an HWND supplied by `Broiler.Graphics.Windows`;
  and
- `IImageOutput` receives image information and one or more `ImageFrame`s.

Buffer writes return an awaitable result so a slow speaker, encoder, or test sink
applies backpressure. Completion, cancellation, and failure have one defined
lifecycle. For `IMFMediaEngine`, the implementation maps this lifecycle to engine
events and the externally owned HWND instead of manufacturing a Media surface or
managed video-frame stream.

This output is a decode-pipeline sink, not necessarily a physical playback
device. The HWND target is declared by `Broiler.Graphics.Windows`; it is not a
surface backend owned by Media. Raw video-frame output and alternate providers
are staged extensions.

### 7.6 Buffer ownership

Every decoded buffer/frame contract must state:

- who owns its memory;
- whether the data is mutable;
- how long the memory remains valid after an output callback;
- whether consumers may retain it; and
- who disposes pooled or native memory.

The recommended contract is that a frame/buffer is disposable when it owns
pooled/native memory and remains valid until disposed. Implementations may use
managed arrays initially, but callers must not depend on arrays being the only
storage mechanism.

## 8. Image extraction map

### 8.1 Move to `Broiler.Media.Image`

| Current type | Target concept | Migration note |
|---|---|---|
| `BPixelBuffer` | `ImageBuffer` | Preserve RGBA8 semantics first; add explicit stride/format metadata |
| `BImageFrame` | `ImageFrame` | Preserve APNG duration; add placement/blend/disposal metadata if not already materialized |
| `BImageSequence` | `ImageSequence` | Preserve loop-count semantics and single-frame convenience |
| `BImageEncodeFormat` | Image format/encode options | Avoid one enum becoming the full codec registry |
| `IBImageCodec` | `ImageCodec` abstract contract | Adapter required because interface-to-abstract-class is not a type-forwarding move |

### 8.2 Move to `Broiler.Media.Image.Managed`

| Current area | Target |
|---|---|
| `ManagedImageCodec` dispatch | Managed codec-set factory plus individual concrete codecs |
| `Png/*` and `Crc32` | `PngImageCodec` internals |
| `Jpeg/*` | `JpegImageCodec` internals |
| `Bmp/*` | `BmpImageCodec` internals |

### 8.3 Keep in `Broiler.Graphics`

- `BBitmap` storage used as a mutable drawing target;
- `BCanvas` and all raster drawing operations;
- `BImageSurface` and `BImageRenderer`;
- renderer image handles and render commands;
- colors, geometry, fonts, controls, and windows; and
- backend-neutral upload API accepting a media `ImageBuffer`.

### 8.4 Keep in `Broiler.Graphics.Windows`

- Direct2D device/surface/resource lifetime;
- straight-RGBA to premultiplied-BGRA upload conversion;
- native bitmap creation and caching; and
- Direct2D image drawing.

### 8.5 Split or replace

| Current behavior | Final behavior |
|---|---|
| `BBitmap.Decode(...)` silently installs the managed codec | Caller decodes through an explicit image codec/catalog, then constructs/converts the bitmap |
| `BBitmap.Encode(...)` uses the global codec | Caller supplies encode service/catalog, or uses a media-side extension |
| `IBroilerRenderer.CreateImage(encodedBytes)` decodes internally | Resource layer decodes; renderer accepts `ImageBuffer` |
| renderer constructors call `UseManagedIfUnset` | Composition root registers concrete codecs explicitly |
| HTML `ImageDecoder` probes extensions/headers independently | HTML uses the media catalog for raster probing while keeping fetch/policy outside |

## 9. Compatibility policy

The extraction changes assembly ownership, namespaces, and the codec abstraction
from an interface/static facade to abstract classes plus a catalog. This cannot be
made fully binary-compatible by type forwarding alone.

Recommended policy:

1. Treat the new Media API as the canonical clean surface.
2. Keep source-level obsolete adapters in Graphics for one announced transition
   window where practical.
3. Preserve `BBitmap`, renderer handles, and rendering APIs that genuinely remain
   in Graphics.
4. Do not retain global codec selection indefinitely merely to preserve
   `BImageCodec.Current`.
5. Use type forwarding only for types whose full identity and semantics can be
   preserved; use adapters for renamed or structurally changed types.
6. Publish the Graphics and Media package versions as a compatible set.
7. Remove adapters only in a major/breaking release or before a declared 1.0 API
   freeze.

Before Phase 2, record whether the project currently promises binary compatibility.
If it does, add an explicit compatibility assembly rather than compromising the
final dependency direction.

## 10. Implementation roadmap

### Phase 0 - Freeze behavior and decide repository/package policy

**Objective:** Establish evidence and land no functional changes.

Tasks:

- Record the canonical `Broiler.Graphics` checkout and resolve project references
  that unexpectedly point through the nested HTML checkout.
- Decide whether `Broiler.Media` is consumed through packages, a pinned submodule,
  or conditional local project references; prohibit duplicate editable copies.
- Capture the current public Graphics image API and all direct consumers.
- Run and archive the current PNG/APNG/JPEG/BMP, bitmap/canvas, Direct2D image,
  HTML image, CLI capture, WPT, and pixel-diff baselines.
- Add malformed/truncated and allocation-limit baselines before moving parsers.
- Record image encode golden files and decode pixel hashes.
- Write ADRs for buffer ownership, pixel/alpha format, compatibility window, and
  the `IMFMediaEngine`/borrowed-HWND lifetime and threading contract.

Exit gate:

- baseline commands/results are documented;
- no canonical-source ambiguity remains; and
- all decisions needed to scaffold public contracts are approved.

### Phase 1 - Scaffold the seven assemblies and shared contracts

**Objective:** Establish the final dependency graph without moving working codecs.

Tasks:

- Create the seven runtime projects and matching test projects.
- Target the repository's current `net10.0` baseline; enable nullable, explicit
  usings, warning-as-error, safe code, trimming, and AOT metadata where applicable.
- Add abstract `MediaCodec`, `AudioCodec`, `VideoCodec`, and `ImageCodec` in their
  prescribed assemblies.
- Add probe, catalog, limits, input, diagnostics, and output lifecycle contracts.
- Add architecture tests that inspect project references and public signatures.
- Add fake codecs and recording outputs in test projects only to validate catalog
  ordering, cancellation, errors, and backpressure.
- Add projects under `/Dependencies/Media/` in `Broiler.slnx` without cutting over
  consumers.

Exit gate:

- all abstraction projects build independently;
- implementation assemblies follow the reference allowlist in section 6.1;
- catalog behavior is deterministic; and
- no HTML reference or graphics surface implementation exists in Media.

### Phase 2 - Move image data contracts and managed codecs

**Objective:** Rehome working codecs without changing their algorithmic behavior.

Tasks:

- Introduce `ImageBuffer`, `ImageFrame`, `ImageSequence`, and image options in
  `Broiler.Media.Image`.
- Port PNG/APNG, JPEG, and BMP internals to `Broiler.Media.Image.Managed` in small,
  format-specific commits.
- Wrap each format in a concrete `ImageCodec` implementation.
- Preserve the existing encoders behind capability-gated image encode operations.
- Port codec unit tests beside each implementation.
- Run differential decode tests against the old Graphics codec for the same
  corpus, including progressive JPEG and all APNG blend/dispose cases.
- Run byte or semantic equivalence tests for encoders as appropriate.
- Do not delete old Graphics code until the new path passes the gate.

Exit gate:

- new and old paths decode the corpus to identical RGBA pixels and animation
  timing;
- current image encode behavior remains available;
- malformed input produces bounded documented failures; and
- Media.Image.Managed has no Graphics reference.

### Phase 3 - Cut Graphics over to decoded media images

**Status: Done (2026-07-12)** — see the section 0 tracker. Graphics references only the
`Broiler.Media.Image` abstraction; codec selection is delegated to an injected catalog registered by the
composition root through `BImageCodecs.Use(...)`.

**Objective:** Make Graphics a consumer of decoded pixels, not an image codec owner.

Tasks:

- Add conversion between Graphics `BBitmap` and Media `ImageBuffer` at one explicit
  adapter boundary.
- Change CPU and Direct2D renderer upload paths to consume `ImageBuffer`.
- Move encoded-byte decode calls to the resource/media layer.
- Stop renderer constructors from mutating codec registration.
- Add transitional obsolete wrappers for approved compatibility cases.
- Repoint Graphics tests: codec tests move to Media; canvas/render/upload tests stay
  in Graphics.
- Delete codec algorithms and neutral frame/sequence models from Graphics only
  after every consumer has moved.
- Update `Broiler.Graphics/README.md` and its roadmap to describe Media as the
  decoder owner.

Exit gate:

- no PNG/JPEG/BMP parser or encoder remains in Graphics;
- no Graphics constructor selects a codec;
- CPU and Direct2D image rendering match the baseline; and
- Graphics references only `Broiler.Media.Image`, not its implementation.

### Phase 4 - Consolidate HTML image loading and probing

**Status: Done (2026-07-12)** — see the section 0 tracker. The live raster path already decodes
through `MediaCodecCatalog` (`Broiler.HTML.Image.BBitmap.Decode`); the bridge's parallel
`ImagePipeline.cs` detector was dead/test-only and was removed, leaving `BSvgRasterizer.IsSvgData`
as the bridge-owned SVG-vs-raster discriminator and the Media catalog as the sole raster authority.

**Objective:** Route browser raster images through Media while preserving browser
policy boundaries.

Tasks:

- Keep fetch, URI resolution, data-URI policy, CSP/CORS, caching, and cancellation
  in HTML/resource-loading services.
- Replace duplicate raster magic-byte/extension selection with the media catalog.
- Adapt decoded `ImageBuffer` to existing HTML image handles and Graphics upload.
- Keep SVG parser/rasterizer and canvas commands outside Media.
- Keep transparent/broken-image placeholders in the browser/rendering layer unless
  they become generally reusable image factories.
- Ensure animated image timing is not discarded, even if the first integration
  initially renders only frame zero; document that limitation explicitly.

Exit gate:

- HTML has one raster codec authority;
- network and browser policy do not leak into Media;
- existing image/data-URI/SVG tests remain green; and
- unsupported GIF/WebP fail honestly rather than returning misleading placeholders.

### Phase 5 - Deliver the audio abstraction and managed baseline

**Objective:** Prove the common design with a streaming, non-image decoder.

Tasks:

- Finalize `AudioBuffer`, format, channel, timing, and ownership contracts.
- Implement RIFF/WAVE PCM probing and chunked decode in
  `Broiler.Media.Audio.Managed`.
- Add recording, null, and bounded-buffer `IAudioOutput` test sinks.
- Verify cancellation, slow-output backpressure, non-seekable input, unknown RIFF
  chunks, truncated data, and declared-size attacks.
- Integrate metadata/source selection with HTML behind an experimental flag, but
  keep the current visual placeholder until an actual output/playback service is
  connected.

Exit gate:

- representative WAVE fixtures decode to exact sample values and timing;
- memory use is bounded by configured chunk size;
- no playback support is claimed; and
- Audio assemblies remain independent of Graphics and HTML.

### Phase 6 - Deliver the video abstraction and first backend

**Status: Done (2026-07-12)** — see the section 0 tracker. The HWND presentation target is declared and
owned by `Broiler.Graphics.Windows` (`HwndVideoOutput : Broiler.Media.Video.IVideoOutput`); the Media
Foundation backend borrows it via the sanctioned `MediaFoundation → Broiler.Graphics.Windows` edge. Browser
playback (§Phase 7) and multi-format fixtures remain future work.

**Objective:** Deliver the first real video path through `IMFMediaEngine` and an
HWND owned by `Broiler.Graphics.Windows`.

Prerequisite: the borrowed-HWND lifetime/threading ADR is approved.

Tasks:

- Finalize `VideoCodec`, video metadata, engine-session, event, cancellation,
  seek, and presentation-target contracts around `IMFMediaEngine` behavior.
- Add the Windows-only `Broiler.Media.Video.MediaFoundation` implementation and
  its explicit COM/Media Foundation startup, callback, and disposal handling.
- Declare the HWND-bearing video target in `Broiler.Graphics.Windows`; Graphics
  owns the HWND and Media Foundation borrows it for the session lifetime.
- Establish the direct `video -> IMFMediaEngine -> HWND` presentation path.
- Handle resize, visibility, UI-thread affinity, window destruction, source
  replacement, cancellation, end/error state, and teardown ordering.
- Add representative Windows video fixtures for every format that the first
  release explicitly claims through Media Foundation.
- Keep HTML source selection, element controls/events, and application policy in
  the higher integration layer.
- Do not add a Media surface, decoded-frame upload adapter, FFmpeg dependency, or
  generic GPU backend in this phase.

Exit gate:

- a real video is presented by `IMFMediaEngine` into an HWND owned by
  `Broiler.Graphics.Windows`;
- cancellation, resize, HWND destruction, and engine disposal are deterministic
  and leak-free;
- no graphics surface or swap chain is implemented by a Media assembly;
- the abstraction projects remain Media-Foundation- and Windows-handle-free; and
- browser support remains feature-gated until Phase 7.

### Phase 7 - Add output and browser playback integration

**Status: In progress (2026-07-12)** — the app/HTML-layer playback engine (`src/Broiler.Playback`) landed:
the `IMediaPlaybackSession` state machine, `AudioPlaybackSession` (decode→real output + clock),
`VideoPlaybackSession` (adapter over the IMFMediaEngine session), `MediaPlayer` (routing + `CanPlayType` +
deterministic capability errors), and a reusable `BufferedAudioOutput` sink. This satisfies the "playback
clock, pause/resume, seek, buffering, end/error state, and cancellation in the application/HTML layer" task
and keeps all playback policy out of Broiler.Media. Still open: the DOM media-element surface + JS bindings,
wiring the engine into `<audio>`/`<video>` rendering, a real audio-device/real-time driver, a real
MediaFoundation video→HWND path, and removing the WPT media skips incrementally. See the §0 tracker.

**Objective:** Connect typed outputs and the Media Foundation HWND session to
application/browser behavior.

Tasks:

- Add optional audio output components only where needed.
- Reuse the HWND target declared by `Broiler.Graphics.Windows` for video; do not
  introduce a second Media-owned surface or video presenter.
- Implement playback clock, pause/resume, seek, buffering, end/error state, and
  cancellation in the application/HTML layer.
- Use `IMFMediaEngine` timing/events as the baseline for video playback and map
  them to the HTML/application state machine.
- Wire HTML source selection and events to real capability results.
- Replace WPT media skips incrementally, one supported behavior/format at a time.
- Add telemetry for selected codec, fallback, dropped frames, decode latency, and
  output underruns without exposing media contents.

Exit gate:

- at least one audio and one video end-to-end scenario works through real outputs;
- playback state survives cancellation/disposal without leaks;
- supported HTML media tests no longer use placeholders; and
- unsupported formats continue to report deterministic capability errors.

### Phase 8 - Compatibility cleanup and release hardening

**Status: In progress (2026-07-12)** — see the §0 tracker. Done: duplicate image model/format detectors are
gone from Graphics and the HTML bridge, no expired adapters remain, dependency architecture tests pass, the
component README is release-quality (supported-format table + security guidance, packed into every package),
and all 7 runtime assemblies `dotnet pack` into per-assembly `0.1.0-preview.1` packages with correct
dependency metadata. Remaining: feed-based consumption validation, performance gates, a name/XML-doc freeze
after consumer review, and the actual publish.

**Objective:** Remove temporary ownership and publish stable component boundaries.

Tasks:

- Remove expired Graphics codec adapters according to the compatibility policy.
- Ensure no duplicate image model/format detector remains in Graphics or the HTML
  bridge.
- Freeze public names and XML documentation after consumer review.
- Publish per-assembly packages with dependency and native-asset metadata.
- Add component READMEs, supported-format tables, security guidance, examples,
  and upgrade notes.
- Update aggregate architecture documents and the Graphics/HTML roadmaps.

Exit gate:

- dependency architecture tests pass;
- package consumption works without the aggregate repository;
- all supported codecs have conformance, malformed-input, and performance gates;
  and
- the definition of done in section 15 is satisfied.

## 11. Suggested pull-request sequence

1. Baseline inventory, corpus manifest, and ADRs.
2. `Broiler.Media` foundation plus catalog tests.
3. Audio/Video/Image abstraction assemblies and architecture guards.
4. Image buffer/frame/sequence contracts plus compatibility adapters.
5. PNG/APNG move and differential tests.
6. JPEG move and differential tests.
7. BMP move and differential tests.
8. Graphics CPU renderer cutover.
9. Direct2D and HTML image cutover.
10. Remove codec implementations from Graphics and update documentation.
11. WAVE PCM audio implementation.
12. `IMFMediaEngine` plus `Broiler.Graphics.Windows` HWND presentation path.
13. Output/playback integration in independently reviewable slices.
14. Compatibility cleanup and package release.

Each pull request should leave the aggregate solution buildable. Cross-repository
changes should be ordered provider first, then consumer, then parent submodule
pointer updates.

## 12. Testing strategy

### 12.1 Shared foundation tests

- deterministic probe scoring and tie-breaking;
- duplicate codec IDs and invalid descriptors;
- MIME/extension hints never overriding contradictory strong signatures;
- non-seekable prefix replay;
- cancellation before/during probe and decode;
- output completion/failure lifecycle;
- explicit stream and buffer ownership; and
- catalog concurrency without global mutation.

### 12.2 Image tests

- existing PNG color types, bit depths, transparency, and Adam7 cases;
- APNG frame blend/disposal, delay, loop count, and round-trip cases;
- baseline/progressive/grayscale/subsampled/restart-marker JPEG cases;
- BMP 24/32-bit cases;
- exact pixels for lossless formats and bounded error for JPEG;
- encode/decode round trips and independent-decoder interoperability;
- truncated chunks/segments, invalid dimensions, integer overflows, CRC policy,
  decompression bombs, and excessive frames; and
- CPU/Direct2D upload conversion and rendered pixel comparisons.

### 12.3 Audio tests

- exact decoded PCM samples for every supported depth/channel layout;
- RIFF padding, unknown chunks, chunk order, extended format records, and
  truncated/oversized lengths;
- timestamps and total frame counts;
- chunk boundaries independent of input read boundaries;
- non-seekable decode, cancellation, and backpressure; and
- duration/sample allocation limits.

### 12.4 Video tests

- `IMFMediaEngine` creation, source load, play, pause, seek, end, and error events;
- real presentation into a test HWND created and owned by
  `Broiler.Graphics.Windows`;
- HWND resize, visibility change, UI-thread affinity, destruction, and recreation;
- session cancellation and teardown in every loading/playback state;
- unsupported-source and Media Foundation initialization diagnostics;
- no HWND ownership transfer to Media and no use after window destruction; and
- feature detection/skipping on non-Windows test hosts.

### 12.5 Architecture tests

- exact project-reference allowlists;
- no implementation reference from any abstraction assembly;
- no Graphics/HTML/Media Foundation namespaces or HWND types in abstraction
  public APIs;
- the single approved implementation edge to `Broiler.Graphics.Windows` contains
  only the Windows presentation-target integration;
- no process-wide mutable codec singleton;
- no public mutable collections;
- no synchronous network/file access in codec assemblies; and
- every concrete codec derives from the correct typed abstract base.

### 12.6 Performance gates

Capture baselines before the image move and compare:

- decode time and allocations for representative small/large images;
- time to first audio buffer/first `IMFMediaEngine` video presentation;
- peak working set and maximum queued decoded data;
- image pixel-format conversion/upload cost;
- steady-state audio/video throughput; and
- Media Foundation startup/session creation cost on Windows.

The extraction phase should not regress image decode throughput or allocations by
more than an agreed threshold without a documented reason.

## 13. Security and reliability requirements

All media is untrusted input. Before browser integration, enforce configurable
limits for:

- encoded byte count;
- image/video width, height, pixel count, planes, and frame count;
- audio channels, sample rate, duration, and total decoded samples;
- metadata/chunk/segment counts and lengths;
- probe bytes and probe time;
- queued decoded buffers/frames;
- recursion/nesting where a format permits it; and
- total decoded memory.

Use checked arithmetic for dimensions, strides, durations, and allocation sizes.
Malformed data must produce a media-specific error with codec and offset/context
where safe. It must not cause unbounded allocation, hangs, silent partial success,
or arbitrary exception leakage.

The Media Foundation implementation additionally requires COM/Media Foundation
lifetime management, callback disconnection, deterministic session disposal, and
strict borrowed-HWND validation. Media must never destroy the HWND or use it after
`Broiler.Graphics.Windows` reports that the target has closed.

## 14. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Cross-submodule move lands out of order | Broken standalone or aggregate builds | Provider-first package/revision sequence and one canonical checkout policy |
| Generic base becomes an untyped dumping ground | Casts and runtime errors | Keep common base limited to metadata/probe; typed decode only on derived classes |
| Global codec state survives under another name | Test interference and hidden behavior | Immutable explicit catalog; no module initializers or `Current` singleton |
| Graphics loses convenient image APIs | Consumer churn | Time-boxed adapters and explicit conversion helpers |
| Pixel alpha/stride semantics change | Color halos or corrupted uploads | Freeze RGBA8 straight-alpha compatibility, test Direct2D premultiplication |
| Large decoded media exhausts memory | Browser instability/security issue | Streaming outputs, backpressure, pooled ownership, hard limits |
| Codec/container responsibilities are conflated | MP4/WebM design dead-end | Treat demux as a distinct concept and extract it when more than one implementation needs it |
| Borrowed HWND outlives its Graphics window | Native callbacks target an invalid handle | Explicit target lifetime notifications, UI-thread rules, cancellation, and teardown tests |
| A Media surface backend is added for symmetry | Duplicate presentation stack and confused ownership | Keep the baseline `IMFMediaEngine -> HWND`; all HWND/surface ownership stays in Graphics.Windows |
| Windows-first video is mistaken for a cross-platform promise | Unsupported platform behavior | Name the implementation `MediaFoundation`, target Windows explicitly, and stage other providers |
| Audio/video assemblies exist but browser still uses placeholders | Misleading support claims | Separate codec milestones from playback/WPT milestones |
| SVG is pulled into raster codecs | Rendering/DOM dependencies leak into Media | Keep SVG outside initial raster extraction |
| Existing encoders disappear in a decode-first rewrite | Regression in CLI/tests | Capability-gated encode path and migration gate |
| FFmpeg shapes the first API prematurely | Unnecessary raw-frame/surface abstractions | Stage FFmpeg until the IMFMediaEngine session and HWND contract are stable |

## 15. Definition of done

The Broiler.Media component is complete for this roadmap when:

- `MediaCodec`, `AudioCodec`, `VideoCodec`, and `ImageCodec` are abstract classes
  in their prescribed separate assemblies;
- each media kind has at least one separately shipped concrete implementation
  assembly, with `Broiler.Media.Video.MediaFoundation` as the first real Video
  implementation;
- the shared catalog explicitly selects codecs without mutable global state;
- PNG/APNG, JPEG, and BMP codec/data code no longer lives in
  `Broiler.Graphics`;
- `Broiler.Graphics` retains rendering surfaces, canvases, handles, and backends
  and consumes already-decoded `ImageBuffer` data;
- no Media assembly owns a graphics surface, swap chain, window, or HWND;
- real video presentation follows `IMFMediaEngine -> HWND`, with the HWND declared
  and owned by `Broiler.Graphics.Windows`;
- existing image decode, animation, encode, CPU rendering, Direct2D rendering,
  HTML image, capture, and pixel-diff tests meet their baselines;
- audio decoding and Media Foundation video sessions are cancellable, bounded,
  lifetime-safe, and tested with real fixtures;
- browser/network/playback policies remain outside Media;
- every assembly passes dependency architecture tests;
- security limits and ownership rules are documented and enforced; and
- standalone package and aggregate-workspace builds both pass.

## 16. Recommended decisions to approve before implementation

1. Approve the seven-assembly runtime layout in section 1.
2. Approve `Broiler.Media` as the common base assembly and component name.
3. Approve decode-first scope while preserving existing image encoders.
4. Approve an explicit immutable catalog instead of static global registration.
5. Approve the ownership split: codec/data models move; graphics drawing and GPU
   types stay.
6. Approve RGBA8 straight-alpha as the initial image compatibility format, with
   explicit stride/pixel-format extensibility.
7. Approve RIFF/WAVE PCM as the first managed Audio implementation.
8. Approve `IMFMediaEngine` as the only first real Video implementation and stage
   FFmpeg/other providers until the Windows baseline is stable.
9. Choose the source/binary compatibility window for the current public Graphics
   image API.
10. Choose package versus pinned-checkout consumption so the nested submodule
    pattern does not create multiple editable Media copies.
11. Approve the presentation boundary: `Broiler.Graphics.Windows` declares and
    owns the HWND; Media Foundation borrows it; Media provides no surface backend.

These decisions keep the first implementation phase mechanical: establish the
contracts, move working image codecs unchanged, cut consumers over, and only then
expand into new audio/video capability.

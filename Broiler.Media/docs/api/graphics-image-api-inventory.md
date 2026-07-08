# Graphics Image API Inventory

Date: 2026-07-03

This inventory records the image-related public surface in `Broiler.Graphics`
before media extraction. It is a Phase 0 baseline, not the target Media API.

Implementation update: the Graphics codec facade and in-tree codec
implementations listed here have since been removed from both `Broiler.Graphics`
checkouts. Graphics image load/save helpers now route through
`Broiler.Media.Image.Managed`.

## Public image codec/data types

Source folder: `Broiler.Graphics/Broiler.Graphics/Imaging`.

| Type | Phase 0 role | Implemented target |
| --- | --- | --- |
| `BPixelBuffer` | Tightly packed RGBA8 decoded pixel buffer | Move/replace with `ImageBuffer` in `Broiler.Media.Image` |
| `BImageFrame` | One composited image animation frame plus rational delay | Move/replace with `ImageFrame` in `Broiler.Media.Image` |
| `BImageSequence` | Still or animated image frame sequence | Move/replace with `ImageSequence` in `Broiler.Media.Image` |
| `BImageEncodeFormat` | PNG/JPEG/BMP encode selector | Move/replace with image codec capability/format model |
| `IBImageCodec` | Decode/encode interface | Replace with abstract `ImageCodec : MediaCodec` |
| `BImageCodec` | Process-wide mutable codec facade | Replace with explicit immutable `MediaCodecCatalog` |
| `ManagedImageCodec` | Aggregate PNG/APNG/JPEG/BMP dispatcher | Split into concrete managed image codecs |
| `BBitmap` | Graphics-owned mutable bitmap and canvas entry point | Stay in Graphics; remove hidden codec discovery later |

## Public members to preserve or intentionally replace

### `BPixelBuffer`

- `const int BytesPerPixel = 4`
- `BPixelBuffer(int width, int height, byte[] rgba)`
- `int Width`
- `int Height`
- `byte[] Rgba`

### `BImageFrame`

- `BImageFrame(BPixelBuffer pixels, int delayNumerator, int delayDenominator)`
- `BPixelBuffer Pixels`
- `int DelayNumerator`
- `int DelayDenominator`
- `TimeSpan Delay`

### `BImageSequence`

- `BImageSequence(IReadOnlyList<BImageFrame> frames, int width, int height, int loopCount)`
- `IReadOnlyList<BImageFrame> Frames`
- `int Width`
- `int Height`
- `int LoopCount`
- `bool IsAnimated`
- `BPixelBuffer FirstFrame`
- `static BImageSequence Static(BPixelBuffer pixels)`

### `BImageEncodeFormat`

- `Png`
- `Jpeg`
- `Bmp`

### `IBImageCodec`

- `BPixelBuffer Decode(ReadOnlySpan<byte> data)`
- `byte[] Encode(BPixelBuffer buffer, BImageEncodeFormat format, int quality = 100)`
- `BImageSequence DecodeAnimation(ReadOnlySpan<byte> data)`
- `byte[] EncodeAnimation(BImageSequence sequence, BImageEncodeFormat format = BImageEncodeFormat.Png)`

### `BImageCodec`

- `IBImageCodec Current`
- `bool IsRegistered`
- `void Register(IBImageCodec codec)`
- `void UseManaged()`
- `bool UseManagedIfUnset()`
- `BPixelBuffer Decode(ReadOnlySpan<byte> data)`
- `BImageSequence DecodeAnimation(ReadOnlySpan<byte> data)`
- `byte[] Encode(BPixelBuffer buffer, BImageEncodeFormat format, int quality = 100)`
- `byte[] EncodeAnimation(BImageSequence sequence, BImageEncodeFormat format = BImageEncodeFormat.Png)`

### `ManagedImageCodec`

- `static ManagedImageCodec Instance`
- `BPixelBuffer Decode(ReadOnlySpan<byte> data)`
- `BImageSequence DecodeAnimation(ReadOnlySpan<byte> data)`
- `byte[] EncodeAnimation(BImageSequence sequence, BImageEncodeFormat format = BImageEncodeFormat.Png)`
- `byte[] Encode(BPixelBuffer buffer, BImageEncodeFormat format, int quality = 100)`

### `BBitmap` image-adjacent members

- `BBitmap(int width, int height)`
- `BBitmap(BPixelBuffer pixels)`
- `BBitmap(int width, int height, byte[] rgba, bool takeOwnership = false)`
- `int Width`
- `int Height`
- `ReadOnlySpan<byte> Rgba`
- `BColor GetPixel(int x, int y)`
- `void SetPixel(int x, int y, BColor color)`
- `void Clear(BColor color)`
- `BCanvas OpenCanvas()`
- `BPixelBuffer ToPixelBuffer(bool copy = true)`
- `byte[] CopyRgba()`
- `BBitmap Copy()`
- `BBitmap ResizeNearest(int width, int height)`
- `byte[] Encode(BImageEncodeFormat format = BImageEncodeFormat.Png, int quality = 100)`
- `void Save(string filePath, BImageEncodeFormat format = BImageEncodeFormat.Png, int quality = 100)`
- `static BBitmap Decode(byte[] data)`
- `static BBitmap Decode(ReadOnlySpan<byte> data)`
- `static BBitmap Decode(Stream stream)`
- `static BBitmap Decode(string path)`

## Removed codec implementation files

Graphics-owned managed codec implementation files removed during the Media
handoff:

- `BImageCodec.cs`
- `IBImageCodec.cs`
- `ManagedImageCodec.cs`
- `BPixelBuffer.cs`
- `BImageFrame.cs`
- `BImageSequence.cs`
- `BImageEncodeFormat.cs`
- `Crc32.cs`
- `Png/PngDecoder.cs`
- `Png/PngEncoder.cs`
- `Bmp/BmpDecoder.cs`
- `Bmp/BmpEncoder.cs`
- `Jpeg/JpegBitReader.cs`
- `Jpeg/JpegBitWriter.cs`
- `Jpeg/JpegDct.cs`
- `Jpeg/JpegDecoder.cs`
- `Jpeg/JpegEncoder.cs`
- `Jpeg/JpegHuffmanTable.cs`
- `Jpeg/JpegOptimalHuffman.cs`
- `Jpeg/JpegTables.cs`

## Original direct consumers found

Representative Phase 0 source references found with `rg` before migration:

| Consumer | Current image dependency |
| --- | --- |
| `Broiler.Graphics/Broiler.Graphics/Rendering/BImageRenderer.cs` | `CreateImage(ReadOnlySpan<byte>)` calls `BImageCodec.UseManagedIfUnset()` and `BImageCodec.Decode` |
| `Broiler.Graphics/Broiler.Graphics/Rendering/IBroilerRenderer.cs` | Public encoded-image and `BPixelBuffer` upload methods |
| `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DRenderer.cs` | Constructor calls `BImageCodec.UseManagedIfUnset`; encoded images decode before upload |
| `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DImageStore.cs` | Stores `BPixelBuffer` and converts to Direct2D BGRA premultiplied data |
| `Broiler.Graphics/Broiler.Graphics.Windows/Direct2DOffscreenSurface.cs` | Reads renderer output back to `BBitmap`/RGBA |
| `Broiler.HTML/Source/Broiler.HTML.Image/BBitmap.cs` | Bridges HTML bitmap encode/decode through `Broiler.Graphics.BImageCodec` |
| `Broiler.HTML/Source/Broiler.HTML.Image/HtmlRender.cs` | Exposes file/render output formats as `Broiler.Graphics.BImageEncodeFormat` |
| `Broiler.HTML/Source/Broiler.HTML.Image/PixelDiffRunner.cs` | Normalizes comparisons through PNG encode/decode |
| `Broiler.HTML/Source/Broiler.HTML.Image.Compat/Adapters/StubImageAdapter.cs` | Decodes streams through HTML `BBitmap` |
| `Broiler.HTML/Source/Broiler.HTML.Graphics/HtmlGraphicsRenderList.cs` | Uploads decoded pixels to Graphics renderer |
| `src/Broiler.Cli/CaptureService.cs` | Selects PNG/JPEG output via `Broiler.Graphics.BImageEncodeFormat` |
| `src/Broiler.Wpt/WptTestRunner.cs` | Decodes PNG references for comparison |
| `src/Broiler.DevSite` | Decodes references and encodes PNG output |
| `src/Broiler.Engines.Baseline` | Encodes rendered images for baseline checks |
| `src/Broiler.Cli.Tests` | Uses encode/decode, data URI images, output files, and pixel comparisons |

Current implementation note: app-facing image encode/decode call sites use
`Broiler.Media.Image.ImageEncodeFormat` and the managed Media codec catalog.
Graphics renderers decode encoded image bytes through Media before upload.

## Project-reference notes

- `Broiler.HTML/Source` projects that reference Graphics resolve to the aggregate
  root `Broiler.Graphics` checkout.
- `Broiler.HTML/Broiler.Graphics` is a nested mirror at the same commit and has
  its own nested Graphics solution/tests. It must not receive a separate Media
  source copy.
- Phase 1 should add Media projects only under the new root `Broiler.Media`
  component and should not cut existing consumers over.

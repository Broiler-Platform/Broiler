# Image Baseline Record

Date: 2026-07-03

This record captures the current image behavior before moving codecs from
Graphics to Media.

## Commands run

### Graphics core image suite

```powershell
dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj --no-restore
```

Result: `76/76` passed, exit code `0`.

Covered areas from the test output:

- render-list validation and image handle value semantics;
- `BBitmap` RGBA storage/copy behavior;
- `BCanvas` fill, clip, opacity layer, gradient, and bitmap-region behavior;
- CPU image renderer upload and draw behavior;
- PNG round trip, CRC validation, signature selection, 1/8/16-bit variants,
  palette transparency, grayscale alpha, color-key transparency, Adam7, and
  frozen Adam7 fixture;
- BMP round trip;
- JPEG DCT, canonical Huffman, signed magnitude coding, gradient/flat/odd/1x1
  cases, grayscale source, restart markers, quality behavior, optimized Huffman,
  baseline/progressive comparison, and frozen progressive fixture;
- APNG frame count, canvas size, loop count, SOURCE/OVER blend, BACKGROUND and
  PREVIOUS dispose, per-frame delays, static decode behavior, plain PNG animation
  fallback, animation encode round trip, default-image behavior, mismatched frame
  rejection, and non-PNG animation encode rejection.

### Direct2D image/backend suite

```powershell
dotnet run --project Broiler.Graphics\Broiler.Graphics.Windows.Tests\Broiler.Graphics.Windows.Tests.csproj
```

Result: `9/9` passed, exit code `0`.

Covered areas from the test output:

- RGBA to BGRA premultiplied conversion;
- fully transparent pixel normalization;
- Direct2D image-store add/get/remove lifecycle;
- unknown image-handle rejection;
- encoded image decode through renderer `CreateImage`;
- garbage encoded image rejection;
- decoded `BPixelBuffer` upload/release;
- Direct2D command-list smoke rendering;
- render-to-image readback.

## Current malformed-input baselines

Existing tests cover these malformed or unsupported cases:

- unknown image signatures throw `NotSupportedException`;
- corrupted PNG CRC throws `FormatException`;
- Direct2D renderer rejects garbage encoded bytes;
- APNG animation encoding rejects mismatched frame sizes;
- APNG animation encoding rejects non-PNG formats.

Before moving parsers in Phase 2, add explicit tests for:

- truncated PNG chunks and IDAT data;
- decompression-size limits;
- invalid image dimensions and integer overflow;
- malformed BMP headers and declared pixel sizes;
- malformed JPEG markers/segments and restart-marker attacks;
- excessive frame counts and animation canvas sizes;
- allocation limits for encoded and decoded image sizes.

## Golden-file and hash policy

No standalone golden-file manifest existed before this Phase 0 pass. The current
source of truth is the passing Graphics test fixture set listed above.

Before parser files move in Phase 2, generate a checked-in manifest containing:

- encoded SHA-256 hashes for PNG, APNG, JPEG, and BMP fixtures;
- decoded RGBA SHA-256 hashes for every lossless fixture;
- per-frame RGBA SHA-256 hashes and delays for APNG fixtures;
- bounded-error metadata for lossy JPEG fixtures;
- fixture dimensions, pixel format, alpha mode, and expected exception type for
  malformed fixtures.

The manifest must be generated from the canonical root `Broiler.Graphics`
checkout recorded in `docs/phase-0.md`, not from the nested HTML mirror.


# Pixel comparison: 284 ms → 4.5 ms

`PixelDiffRunner.Compare` was the WPT runner's second-largest phase — **16–21% of a run**,
327–501 ms per comparison of two 1024x768 bitmaps
([`render-fixed-cost.md`](render-fixed-cost.md)). Reading the method suggested two
candidate causes calling for opposite fixes, so both were measured before either was written.

Produced by `--pixel-compare-cost`, which re-runs the identity check alongside the timings.

## Where the cost was

| case | before | after |
|---|---:|---:|
| **Compare — match path** | **284.61** | **4.50** |
| **Compare — mismatch path** | **285.23** | **7.92** |
| NormalizeForComparison (Encode+Decode), one bitmap | 130.65 | 137.11 |
|   of which: Encode(Png,100), one bitmap | 92.02 | 103.49 |
| Copy(), one bitmap | 0.97 | 2.39 |

**62× on the match path, 36× on the mismatch path.** The normalize rows are measured
standalone, so they stay as a record of what was being paid; they are no longer in `Compare`.

The obvious-looking culprit was not the culprit. The per-pixel `GetPixel`/`SetPixel` loop
reads badly and is real, but it was **~22 ms of 284**. The other **~262 ms — 92% — was
`NormalizeForComparison`**, which ran `BBitmap.Decode(source.Encode(Png, 100))` on *both*
inputs before a single pixel was read: a full PNG compress and decompress of 3 MB each, per
comparison.

## The round trip was an identity, and that was checked rather than argued

| case | result |
|---|---|
| synthetic, opaque | identical |
| synthetic, graded alpha | identical |
| synthetic, transparent with non-zero RGB | identical |
| 25 real WPT reference PNGs off disk | all identical |

The two synthetic alpha cases are the ones where a PNG codec is entitled to premultiply or
collapse the colour type; the reference PNGs are what the golden-image path actually compares
against. It could not have been anything else here: `Encode` serialises the same `_pixels`
array `GetPixel` reads, so a lossless round trip has nothing to normalise between two
`BBitmap`s — and the method's own `catch (InvalidOperationException) => source.Copy()`
fallback already treated a plain copy as an acceptable substitute.

## What changed

1. **Normalization removed.** The inputs are compared directly.
2. **The loop reads backing spans** (`BBitmap.PixelBytes`, assembly-internal) instead of
   `GetPixel` — the same store, without a call and a `BColor` per pixel, 1.57 M times.
3. **The diff bitmap is built only when the comparison failed.** It used to be allocated and
   written for every pixel of every comparison and then discarded on the match path — a 3 MB
   image and 786 432 `SetPixel` calls nothing ever looked at, on the ~62% of tests that pass.
   It is now a second pass, byte-identical, run only on the failure path.

## On the whole suite

`css/css-backgrounds` reftests (713 tests), `--workers 4`, same session, two runs each:

| | runs (s) | median |
|---|---|---:|
| before | 373.2, 363.8 | **368.5** |
| after | 299.1, 295.4 | **297.3** |

**1.24×, 71 s off a 6-minute subset.** Less than the 262 ms × 711 = 186 s of CPU removed,
because four workers divide it — the saving is CPU-parallel, the clock is not.

## Correctness

- **WPT reftest classification identical**: 444 passed / 266 failed / 1 skipped before and
  after, and the *failing-test sets are identical name for name*.
- **`Broiler.Wpt.Tests`: 748 passed / 57 failed both ways, failure sets identical name for
  name.** Those 57 are pre-existing on this host.

### One scare, and it was the suite, not the change

The first post-fix run differed from the pre-fix runs by three tests — `background-size-007`
and `background-size-010` newly passing, `background-size-cover-003` newly failing. Restoring
normalization while keeping the loop rewrite reproduced *the same three*, which ruled out both
of the changes. Running the **pristine, unmodified** tree with the same invocation then produced
**exactly the 266-failure set the fix produces** — so those three tests are pre-existing
run-to-run nondeterminism, and the pre-fix runs they were compared against had all been taken
back-to-back inside one script invocation.

Worth recording twice over: the three tests are a real (small) nondeterministic fringe in this
suite, and "my change altered results" was itself a false alarm that only a pristine-tree
control run could settle. A before/after taken under different invocation conditions is not a
before/after.

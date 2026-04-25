# Acid Test Triage Roadmap

This roadmap is the umbrella tracker for rendering the three classic acid
tests with **Chromium/Playwright** and **`broiler.cli`**, comparing the
outputs, and translating the remaining gaps into actionable renderer work.

## Scope and baseline

- **Acid1** — compare the complete static layout, including the floated column,
  form area, lower block layout, and footer paragraph.
- **Acid2** — compare the `#top` face render against Chromium and use the
  existing face-region breakdown as the primary signal.
- **Acid3** — compare the 1024×768 viewport render (not a full-page screenshot)
  against Chromium after the scripted score animation settles.

## Current workflow

- `scripts/acid1-pixel-test.sh`
- `scripts/acid2-pixel-test.sh`
- `scripts/acid3-pixel-test.sh`

Each pipeline renders with `broiler.cli`, captures a Chromium/Playwright
reference, and writes a diff image plus a text report into the matching
`acid/acid*/` directory.

## Current comparison snapshot (2026-04-25)

| Test | Baseline | Full-image match | Content-area match | Main hot spots |
|---|---|---:|---:|---|
| Acid1 | Natural document bounds (`520×420` reference and Broiler) | 88.83% | 85.26% | Footer text rasterization, residual form-control/widget differences |
| Acid2 | `acid2.html#top` at `1024×768` | 99.48% | 82.07% | Forehead/top text band, smaller smile/text fidelity deltas |
| Acid3 | Viewport `1024×768` | 92.64% | 90.18% | Bucket area layout/fill, residual 1px frame-bottom drift; score area already 100% |

## Acid1

### Expected comparison target

- Full document render at the page's natural layout size.
- Playwright capture is resized to the document bounds before the screenshot so
  Chromium and Broiler compare the same rendered region.

### Mismatch checklist

- Current rerun: **88.83% full-image**, **85.26% content-area** match.
- Broiler now matches Chromium's natural full-page capture size at `520×420`,
  so the dominant remaining mismatch is the footer paragraph text itself rather
  than truncated layout height.
- Native radio widgets still differ from Chromium in glyph, border, and fill,
  but they no longer collapse the `#bar` form layout.
- Text anti-aliasing remains the largest single source of mismatch in the
  footer paragraph and also appears in the `#bar` form text.
- The left red `toggle` column and the lower black/yellow layout are now
  mostly geometry-stable; treat any new drift there as a real layout/CSS
  regression instead of known baseline noise.

### Roadmap

1. **P0 — Preserve the current geometry win**
   - Keep the `520×420` full-page bounds and the repaired `#bar` form layout
     stable while iterating on the remaining visual gaps.
   - Treat any renewed drift in the floated column, lower black/yellow blocks,
     or overall frame as higher priority than font noise.
2. **P1 — Form control fidelity**
   - Compare Broiler's static form-control rendering against Chromium's radio
   widget appearance and decide whether to emulate Chromium or document the
   difference as acceptable.
3. **P2 — Text rasterization**
   - Track footer and `#bar` text anti-aliasing differences separately from
     layout bugs; this is now the dominant Acid1 gap.

## Acid2

### Current findings

- The existing focused roadmap is `acid/acid2/acid2-compliance-roadmap.md`.
- The current comparison artifacts live beside the test in `acid/acid2/`.
- The latest rerun is dramatically better than the older checked-in roadmap
  numbers: **99.48% full-image**, **82.07% content-area**, and **0 red pixels**.
- The dominant remaining mismatch is now concentrated in the forehead/top text
  band, with smaller smile/text-fidelity deltas below it.

### Mismatch checklist

- Forehead/top-band fidelity is still the clear outlier (**2.12%** region
  match), but it is now isolated instead of being mixed with broad face-geometry
  failures.
- Eyes (**96.24%**), nose (**93.23%**), and chin (**100.00%**) are now
  geometry-stable and should be preserved.
- Smile fidelity improved materially (**80.88%**) but still has visible text or
  line-shape deltas.
- Red pixel leakage is fully suppressed again (**0 red pixels**), so the CSS
  failure signal is no longer an active blocker.

### Roadmap

1. **P0 — Preserve face geometry**
   - Prevent regressions in the eyes, nose, smile, and now-perfect chin while
     tightening the remaining visual deltas.
   - Keep the `#top` anchor render path and zero-red-pixel behavior stable.
2. **P1 — Forehead/top-line fidelity**
   - Continue tightening the fixed-position/text rendering differences in the
     top band; this is now the main Acid2 gap.
3. **P2 — Widget/font cleanup**
   - Separate the remaining text-rasterization noise from true layout
     mismatches in reports so the top band does not hide geometry regressions.

## Acid3

### Current findings

- The detailed history is `docs/roadmap/acid3-compliance.md`.
- The Playwright reference must be a **viewport screenshot**. The pipeline now
  captures `fullPage: false` so the generated reference matches the
  1024×768 Broiler output and the documented baseline.
- Remaining gaps are still renderer-fidelity issues, not JavaScript score
  issues: the JS harness can reach 100/100 while the pixels still diverge.
- Current rerun: **92.64% full-image**, **90.18% content-area**. The score area
  is already **100%**, so the visible gaps are concentrated elsewhere.
- The Broiler render logs still emit repeated `JSException.Throw` traces with
  the `"Roses"` value during DOM filter callbacks while the image capture
  completes; this should be treated as harness noise to investigate alongside
  bucket-layout parity work.

### Mismatch checklist

- Border/frame geometry improved again: the black body frame is now only **1px**
  too tall (bottom row **449** vs Chromium **448**), and the gray outer frame is
  likewise down to a **1px** bottom drift (**453** vs **452**).
- Bucket layout/fill remains the dominant miss (**72.42%** region match), but it
  improved slightly while the overmatched Acid3 `* + * > * > p` selector leak
  was removed from later body paragraphs.
- Bottom instruction text is now very close (**99.13%**), so it is no longer the
  highest-value target.
- Any comparison based on a full-page reference should be treated as invalid
  because it distorts the viewport baseline.

### Roadmap

1. **P0 — Keep the comparison baseline correct**
   - Always compare Broiler's viewport render against a Playwright viewport
     screenshot.
2. **P1 — Frame and overflow geometry**
   - Fix border extents and page-height differences first because they skew the
     whole render.
3. **P2 — Bucket/text fidelity**
   - Tighten bucket spacing, background fill, and text once frame geometry is
     stable.

## Priority summary

1. **P0** — Maintain correct capture/comparison baselines for all three acid
   tests, especially Acid3 viewport capture.
2. **P0** — Treat any Acid1 main-layout or Acid2/Acid3 geometry regressions as
   higher priority than widget/font noise.
3. **P1** — Focus renderer work on the remaining module groups:
   floats/positioning, border geometry, overflow/page height, anchor scrolling,
   and static form controls.
4. **P2** — Triage font and anti-aliasing differences separately so they do not
   hide real layout bugs.

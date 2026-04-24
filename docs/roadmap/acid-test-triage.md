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

## Current comparison snapshot (2026-04-24)

| Test | Baseline | Full-image match | Content-area match | Main hot spots |
|---|---|---:|---:|---|
| Acid1 | Natural document bounds (`520×420` reference vs `520×392` Broiler) | 75.46% | 69.95% | Footer paragraph height/text, lower layout, radio controls |
| Acid2 | `acid2.html#top` at `1024×768` | 97.13% | 43.02% | Forehead/scalp bar, eyes, smile/chin, red failure pixels |
| Acid3 | Viewport `1024×768` | 91.89% | 89.27% | Bucket area layout/fill, frame geometry; score area already 100% |

## Acid1

### Expected comparison target

- Full document render at the page's natural layout size.
- Playwright capture is resized to the document bounds before the screenshot so
  Chromium and Broiler compare the same rendered region.

### Mismatch checklist

- Current rerun: **75.46% full-image**, **69.95% content-area** match.
- Broiler currently renders a shorter full-page image than Chromium
  (`520×392` vs `520×420` reference), so the footer paragraph is the largest
  single mismatch zone.
- Native radio widgets may differ from Chromium in glyph, border, and fill.
- Text anti-aliasing differs heavily in the footer paragraph and also appears
  in the `#bar` form text.
- Any non-widget mismatch in the left red `toggle` column, lower black/yellow
  blocks, or overall frame should be treated as a real layout/CSS regression.

### Roadmap

1. **P0 — Layout parity**
   - Keep float, width, min/max-width, and border geometry aligned with the
     Playwright render for the main fixture.
   - Investigate why Broiler's full-page capture is 28px shorter than the
     Playwright reference; the footer mismatch strongly suggests content-height
     truncation or different line wrapping.
2. **P1 — Form control fidelity**
   - Compare Broiler's static form-control rendering against Chromium's radio
     widget appearance and decide whether to emulate Chromium or document the
     difference as acceptable.
3. **P2 — Text rasterization**
   - Track font and anti-aliasing differences separately from layout bugs.

## Acid2

### Current findings

- The existing focused roadmap is `acid/acid2/acid2-compliance-roadmap.md`.
- The current comparison artifacts live beside the test in `acid/acid2/`.
- The 2026-04-24 Playwright rerun is materially worse than the older
  checked-in roadmap numbers: **97.13% full-image**, **43.02% content-area**,
  and **144 red pixels**.
- The dominant remaining mismatches are concentrated in the forehead/top text
  band, eyes, smile/chin, and the reintroduced red-failure pixels.

### Mismatch checklist

- Forehead/scalp bar positioning is still almost completely wrong
  (**1.60%** region match).
- Eyes (**54.14%**) and smile (**65.57%**) are still visibly divergent.
- Chin fidelity collapsed on this rerun (**4.86%**), so bottom-of-face layout
  needs renewed investigation.
- The nose remains the strongest region (**91.67%**) and should be preserved.
- Red pixel leakage is back at **144 pixels**, meaning the CSS-failure signal is
  no longer fully suppressed.

### Roadmap

1. **P0 — Preserve face geometry**
   - Prevent regressions in the nose while restoring chin/smile positioning.
   - Re-check the `#top` anchor render path against the earlier roadmap because
     the new Playwright baseline exposes a larger gap than previously tracked.
2. **P1 — Forehead/top-line fidelity**
   - Continue tightening fixed-position/text rendering differences in the top
     band.
   - Trace the new red-pixel leak before treating any smaller visual deltas as
     the top priority.
3. **P2 — Widget/font cleanup**
   - Separate font/widget noise from true layout mismatches in reports.

## Acid3

### Current findings

- The detailed history is `docs/roadmap/acid3-compliance.md`.
- The Playwright reference must be a **viewport screenshot**. The pipeline now
  captures `fullPage: false` so the generated reference matches the
  1024×768 Broiler output and the documented baseline.
- Remaining gaps are still renderer-fidelity issues, not JavaScript score
  issues: the JS harness can reach 100/100 while the pixels still diverge.
- Current rerun: **91.89% full-image**, **89.27% content-area**. The score area
  is already **100%**, so the visible gaps are concentrated elsewhere.
- The Broiler render logs still emit repeated `JSException.Throw` traces with
  the `"Roses"` value during DOM filter callbacks while the image capture
  completes; this should be treated as harness noise to investigate alongside
  bucket-layout parity work.

### Mismatch checklist

- Border/frame geometry and page-height overflow remain visible mismatch zones.
- Bucket layout/fill remains the dominant miss (**72.24%** region match).
- Bottom instruction text is close (**97.99%**), so it is no longer the
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

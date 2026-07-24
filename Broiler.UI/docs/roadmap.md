# Broiler.UI Roadmap

**Status:** Active preview. The retained-mode foundation, standard control
families, RichEdit, Formatting Codes view, component directory topology, and
preview packages exist. This file replaces completed phase records with the work
that is still open.

## Remove temporary host and Graphics integration

- Migrate remaining Writer/demo users of `StandardLegacyGraphicsInputAdapter` to
  explicit Broiler.Input providers.
- Remove the application dependency on Graphics-owned `BControl`,
  `BButtonControl`, `BEditControl`, `BLabelControl`, and `BControlOptions` after
  all consumers have equivalent managed-control behavior.
- Narrow `BWindow`/`Direct2DWindow` to graphics hosting and presentation after
  input and control migration gates pass.
- Preserve browser-content input routing separately from application chrome
  routing.

## Host parity and review

- Produce evidence for Windows IME candidate placement, clipboard, cursor,
  drag/drop, accessibility bridge, screen-reader, keyboard-only, high-contrast,
  text-scale, reduced-motion, and RTL behavior.
- Decide and document whether secondary logical windows may map to native
  top-level windows.
- Replace the pending Phase-0-era human review with a review of a named current
  revision before expanding the preview claim.

## Design-system and UX conformance

- Finish token enforcement: CI contrast coverage, raw-color/size linting,
  explicit override behavior, and text-scale application.
- Implement consistent visual states, focus-visible policy, tab traversal,
  modal focus trapping, composite navigation, and minimum target sizes.
- Add typography, spacing, density, and motion tokens with deterministic
  reduced-motion behavior.
- Complete semantic relationships and live regions, automated accessibility
  checks, screen-reader scripts, pseudo-localization, bidi/RTL, and fractional
  DPI/reflow tests.
- Publish the design-system, interaction, content, accessibility, and
  per-control maturity references after the behavior is enforceable.

## RichEdit and Formatting Codes

- Render paragraph alignment, lists, and indentation consistently with the
  document model and Formatting Codes projection.
- Complete optional rich HTML/RTF host integration without adding DOM/codecs to
  the core RichEdit assemblies.
- Add formatting-aware accessibility evidence, bidi/RTL and IME host tests,
  incremental/visible-range layout where measurements require it, large-document
  benchmarks, and operation fuzzing.
- Make an explicit go/no-go decision for advanced textual Formatting Codes
  source editing; keep the shipped structured editor canonical and safe by
  default.

## Stabilization and release

- Freeze public names and XML documentation after application consumer review.
- Run performance, leak, fuzz, accessibility, localization, DPI, IME, and
  long-duration soak gates.
- Validate independent package consumption and non-Windows builds.
- Complete dependency, license, API, and attributable human review before a
  stable release.

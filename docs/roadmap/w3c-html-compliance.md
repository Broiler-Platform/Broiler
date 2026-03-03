# Roadmap: Advance HTML-Renderer to Full W3C HTML Compliance

> **Scope:** W3C HTML compliance for the HTML-Renderer component only.
> JavaScript handling is **explicitly out of scope** — see
> [html-js-engine.md](html-js-engine.md) for the broader engine roadmap.

## Background

Broiler uses [HTML-Renderer 1.5.2](https://github.com/ArtOfSettling/HTML-Renderer),
a managed C# HTML/CSS rendering library ([ADR-001](../adr/001-use-html-renderer-for-rendering.md)).
The engine currently supports **HTML 4.01** and **CSS Level 2**, which limits
its ability to render modern web pages correctly.

This roadmap documents the gaps between the current implementation and the
[W3C HTML 5.2 specification](https://www.w3.org/TR/html52/) and related CSS
specifications, then proposes concrete milestones to close those gaps.

---

## Current-State Audit

### HTML Parsing

| Capability | Current Status | W3C Requirement |
|------------|---------------|-----------------|
| Tag tokenisation | ✅ Generic — accepts any tag name | WHATWG §13 tokeniser |
| Void / self-closing elements | ⚠️ Partial — `area`, `base`, `br`, `col`, `frame`, `hr`, `img`, `input`, `link`, `meta`, `param` | Full HTML5 void list incl. `embed`, `source`, `track`, `wbr` |
| Error recovery | ❌ None — malformed markup may be silently dropped | WHATWG tree-construction error handling |
| Character references | ⚠️ Basic entity decoding only | Full named & numeric character references |
| `<!DOCTYPE>` handling | ❌ Ignored | Quirks / standards mode switching |
| `<template>` element | ❌ Not supported | Inert template content model |
| `<script>` / `<noscript>` | ⚠️ Hidden via CSS; not executed | Script-execution lifecycle (out of scope for this roadmap) |

### HTML5 Semantic Elements

The following elements are **not recognised** by the default stylesheet and
receive no special rendering or semantic treatment:

- **Sectioning:** `section`, `article`, `nav`, `aside`, `header`, `footer`, `main`
- **Grouping:** `figure`, `figcaption`, `details`, `summary`
- **Text-level:** `mark`, `time`, `data`, `output`, `progress`, `meter`
- **Embedded:** `video`, `audio`, `source`, `track`, `canvas`, `picture`
- **Interactive:** `dialog`, `menu`

### CSS Selectors

| Selector | Status | Spec Reference |
|----------|--------|----------------|
| Type, class, ID | ✅ Supported | Selectors Level 3 §5 |
| Descendant (` `) | ✅ Supported | |
| Child (`>`) | ✅ Supported | |
| Adjacent sibling (`+`) | ❌ Not supported | Selectors Level 3 §8.3.2 |
| General sibling (`~`) | ❌ Not supported | Selectors Level 3 §8.3.3 |
| Attribute (`[attr]`, `[attr=val]`) | ❌ Not supported | Selectors Level 3 §6 |
| Pseudo-classes (`:hover`, `:link`) | ⚠️ Limited | Selectors Level 3 §6.6 |
| Pseudo-elements (`::before`, `::after`) | ❌ Rejected by parser | Selectors Level 3 §7 |
| `:nth-child`, `:not()` | ❌ Not supported | Selectors Level 3 §6.6 |

### CSS Properties

| Category | Supported | Missing |
|----------|-----------|---------|
| Box model (margin, padding, border) | ✅ Full | — |
| Float & clear | ✅ Supported | — |
| Positioning (static, absolute, fixed) | ✅ Supported | `relative`, `sticky` |
| Display (block, inline, inline-block, table) | ✅ Supported | `flex`, `grid`, `contents` |
| Background (color, image, repeat, position) | ✅ Core | `background-size`, `background-clip`, `background-origin` |
| Text & font | ✅ Core | `text-shadow`, `text-overflow`, `@font-face` |
| Border radius | ✅ Supported | — |
| Box shadow | ✅ Basic | Spread radius, inset |
| Opacity | ✅ Supported | — |
| Transforms | ❌ None | `transform`, `transform-origin` |
| Transitions | ❌ None | `transition-*` |
| Animations | ❌ None | `@keyframes`, `animation-*` |
| Flexbox | ⚠️ Properties exist, minimal layout | Full single/multi-axis flex |
| Grid | ❌ None | `grid-*` properties |
| Custom properties | ❌ None | `var()`, `--*` |
| Media queries | ⚠️ `@media screen` and `@media print` filtering | `min-width`/`max-width` queries |
| `calc()` | ❌ Not supported | CSS Values Level 3 |

### CSS Units

| Unit | Status | Spec Reference |
|------|--------|----------------|
| `px`, `pt`, `in`, `cm`, `mm`, `pc` | ✅ Supported | CSS Values Level 3 |
| `em`, `ex` | ✅ Supported | |
| `%` | ✅ Supported | |
| `rem` | ❌ Not supported | CSS Values Level 3 §5.1.2 |
| `ch` | ❌ Not supported | CSS Values Level 3 §5.1.3 |
| `vw`, `vh`, `vmin`, `vmax` | ❌ Not supported | CSS Values Level 3 §5.1.4 |

### Layout

| Layout Mode | Status | Spec Reference |
|-------------|--------|----------------|
| Block formatting context | ✅ Supported | CSS 2.1 §9.4.1 |
| Inline formatting context | ✅ Supported | CSS 2.1 §9.4.2 |
| Table layout | ✅ Supported | CSS 2.1 §17 |
| Float layout | ✅ Supported | CSS 2.1 §9.5 |
| Absolute / fixed positioning | ✅ Supported | CSS 2.1 §9.6 / §9.7 |
| Relative positioning | ❌ Not supported | CSS 2.1 §9.4.3 |
| Flexbox | ⚠️ Stub only | CSS Flexible Box Module Level 1 |
| Grid | ❌ Not supported | CSS Grid Layout Module Level 1 |
| Multi-column | ❌ Not supported | CSS Multi-column Layout Module Level 1 |

---

## Gap Summary

The gaps are grouped into three priority tiers based on their impact on
rendering real-world web pages.

### Priority 1 — Core Compliance (blocks most pages)

1. **HTML5 default stylesheet** — add default `display`, margin/padding rules
   for all HTML5 semantic and sectioning elements.
2. **Void element list** — add `embed`, `source`, `track`, `wbr` to the void
   element set.
3. **CSS selectors** — implement attribute selectors, sibling combinators,
   structural pseudo-classes (`:nth-child`, `:not`, `:first-of-type`), and
   pseudo-elements (`::before`, `::after`).
4. **`rem` unit** — resolve relative to root element font size.
5. **Relative positioning** — implement `position: relative` with offset
   properties.
6. **`background-size`** — support `cover`, `contain`, and length/percentage
   values.
7. **Media queries** — support `@media screen` and `min-width`/`max-width`.

### Priority 2 — Modern Layout (needed for most modern sites)

8. **Flexbox layout** — implement the full CSS Flexible Box Module Level 1
   algorithm (main/cross axis, wrapping, alignment).
9. **Viewport units** — `vw`, `vh`, `vmin`, `vmax`.
10. **`calc()` expressions** — support basic arithmetic in property values.
11. **`text-overflow` and `word-break`** — implement text truncation and
    line-breaking rules.
12. **`@font-face`** — support web-font loading and fallback.
13. **`<img>` srcset / `<picture>`** — responsive image selection.

### Priority 3 — Advanced Features (full spec alignment)

14. **CSS Grid layout** — implement CSS Grid Layout Module Level 1.
15. **CSS Transforms** — 2-D transforms (`translate`, `rotate`, `scale`).
16. **CSS Transitions & Animations** — `transition-*`, `@keyframes`.
17. **CSS Custom Properties** — `var()` and `--*` declarations.
18. **WHATWG-compliant HTML parser** — full tokeniser and tree-builder with
    error recovery per [WHATWG §13](https://html.spec.whatwg.org/multipage/parsing.html).
19. **`<template>` element** — inert content model.
20. **`<!DOCTYPE>` mode switching** — quirks vs. standards mode.

---

## Milestones

### Phase 1 — HTML5 Baseline (Target: +2 months)

**Goal:** Render HTML5 documents with correct default styling and basic modern
CSS support.

- [x] Add HTML5 elements to `CssDefaults.cs` with correct default display
      values (`block` for sectioning, `none` for `template`, etc.)
- [x] Extend void-element list in `HtmlUtils.cs` to include HTML5 void tags
- [x] Implement `position: relative` in `CssBox.cs` (visual offset after layout)
- [x] Add `rem` unit support to `CssUnit.cs` and `CssLength.cs`
- [x] Implement basic `@media screen` in `CssParser.cs`
- [x] Fix `@media print` leaking into screen rendering (`StripAtRules`)
- [x] Add `background-size` property to `CssBoxProperties.cs`

**Validation:**
- Acid1 test pass rate maintained (132 → 132, no regressions)
- 16 new xUnit tests in `W3cPhase1ComplianceTests.cs`
- Capture of [https://www.w3.org/TR/html52/](https://www.w3.org/TR/html52/)
  renders all sectioning elements correctly

### Phase 2 — CSS Selectors & Cascade (Target: +4 months)

**Goal:** Full CSS Selectors Level 3 support and correct cascade/specificity.

- [ ] Implement attribute selectors (`[attr]`, `[attr=val]`,
      `[attr~=val]`, `[attr|=val]`, `[attr^=val]`, `[attr$=val]`,
      `[attr*=val]`)
- [ ] Implement adjacent sibling (`+`) and general sibling (`~`) combinators
- [ ] Implement structural pseudo-classes: `:nth-child()`, `:nth-last-child()`,
      `:first-child`, `:last-child`, `:only-child`, `:first-of-type`,
      `:last-of-type`, `:not()`
- [ ] Implement pseudo-elements `::before` and `::after` with `content`
      property
- [ ] Audit and fix specificity calculation for new selector types
- [ ] Verify cascade ordering matches CSS 2.1 §6.4

**Validation:**
- Import a curated subset of
  [Web Platform Tests — selectors](https://github.com/nicosResworworworworworworwb-platform-tests/wpt/tree/master/css/selectors)
- All selector tests pass in xUnit

### Phase 3 — Flexbox & Modern Values (Target: +7 months)

**Goal:** Flexbox layout and modern CSS value functions.

- [ ] Implement CSS Flexible Box Module Level 1 layout algorithm
  - Main axis and cross axis sizing
  - `flex-wrap`, `flex-grow`, `flex-shrink`, `flex-basis`
  - `align-items`, `align-self`, `align-content`, `justify-content`
  - `order`, `gap`
- [ ] Implement `calc()` expression parser and evaluator
- [ ] Add viewport units (`vw`, `vh`, `vmin`, `vmax`) to `CssUnit.cs`
- [ ] Implement `text-overflow: ellipsis` and `word-break` modes
- [ ] Support `@font-face` declarations and font-family fallback

**Validation:**
- Import WPT flexbox tests
- Acid2 reference test introduced and tracked
- Real-world page captures (heise.de, github.com) show improved layout

### Phase 4 — Grid, Transforms & Advanced CSS (Target: +12 months)

**Goal:** Complete CSS3 layout and visual effects.

- [ ] Implement CSS Grid Layout Module Level 1
  - Explicit and implicit grid tracks
  - `grid-template-*`, `grid-area`, `grid-gap`
  - Auto-placement algorithm
- [ ] Implement 2-D CSS Transforms (`translate`, `rotate`, `scale`, `skew`)
- [ ] Implement CSS Transitions (`transition-property`, `transition-duration`,
      `transition-timing-function`, `transition-delay`)
- [ ] Implement CSS Animations (`@keyframes`, `animation-*`)
- [ ] Support CSS Custom Properties (`--*` and `var()`)

**Validation:**
- WPT Grid and Transform test subsets
- Acid3 reference test as visual regression baseline
- Performance benchmarks for layout with grid/flex pages

### Phase 5 — WHATWG Parser & Full Compliance (Target: +16 months)

**Goal:** Fully standards-compliant HTML parser and rendering.

- [ ] Replace ad-hoc HTML tokeniser with WHATWG §13 compliant tokeniser
  - Named character references (full table)
  - Error recovery per spec (foster parenting, adoption agency, etc.)
  - `<!DOCTYPE>` quirks/standards/limited-quirks mode detection
- [ ] Implement `<template>` inert content model
- [ ] Support `<picture>` and `srcset` responsive image selection
- [ ] CSS Multi-column Layout Module Level 1
- [ ] Final compliance audit against W3C HTML 5.2 and CSS Snapshot 2023

**Validation:**
- Run full WPT HTML-parser test suite
- Track and publish pass-rate metrics
- Zero regressions against existing Acid1 test suite

---

## Testing & Validation Process

### Test Layers

| Layer | Tool | Purpose |
|-------|------|---------|
| Unit tests | xUnit | Individual parser/layout/property changes |
| Acid tests | Acid1 (existing), Acid2, Acid3 | Visual regression baselines |
| WPT subset | [web-platform-tests](https://github.com/nicosResworworworworworworwb-platform-tests/wpt) | Per-feature standards conformance |
| Capture tests | `Broiler.Cli --capture-image` | Real-world rendering verification |

### Compliance Metrics

Each phase tracks:

- **WPT pass rate** — percentage of imported WPT tests that pass.
- **Acid score** — pixel-match percentage against Acid reference images.
- **Capture quality** — visual comparison of heise.de / github.com captures
  against reference browser screenshots.

Results are published in CI via GitHub Actions after each PR.

### Continuous Integration

- All tests run on every PR (existing workflow).
- Phase completion is gated on:
  1. All new xUnit tests pass.
  2. No regressions in existing Acid1 test suite.
  3. Website capture verification succeeds (`--test-engines`).

---

## Relationship to Other Roadmaps

- **[html-js-engine.md](html-js-engine.md)** — the broader engine roadmap
  covering both rendering and JavaScript. This document focuses exclusively on
  HTML/CSS rendering compliance.
- **[cli-website-capture.md](cli-website-capture.md)** — the CLI capture tool
  roadmap. Improved rendering compliance directly benefits capture quality.
- **[ADR-001](../adr/001-use-html-renderer-for-rendering.md)** — the original
  decision to use HTML-Renderer. This roadmap works within that decision,
  extending the engine rather than replacing it.

---

## Action Items

- [ ] Create tracking issues for each phase (Phase 1–5)
- [ ] Import initial WPT test subsets for selectors and box model
- [ ] Establish baseline compliance metrics in CI
- [ ] Review and refine with community feedback

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `W3cPhase1ComplianceTests.cs` | `Broiler.Cli.Tests` | HTML5 semantic elements, void elements, rem units, @media |
| `W3cPhase2ComplianceTests.cs` | `Broiler.Cli.Tests` | Box model, colours, text, display, tables, specificity |
| `Css2Chapter4Tests.cs` | `HtmlRenderer.Image.Tests` | CSS2 syntax and values |
| `Css2Chapter5Tests.cs` | `HtmlRenderer.Image.Tests` | CSS2 selectors |
| `Css2Chapter8Tests.cs` | `HtmlRenderer.Image.Tests` | CSS2 box model |
| `Css2Chapter9Tests.cs` | `HtmlRenderer.Image.Tests` | CSS2 visual formatting model |
| `Css2Chapter10Tests.cs` | `HtmlRenderer.Image.Tests` | CSS2 visual formatting model details |

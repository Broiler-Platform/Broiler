# Roadmap: Fully-Featured HTML and JavaScript Engine

## Project Scope & Vision

Broiler currently uses [HTML-Renderer](https://github.com/ArtOfSettling/HTML-Renderer)
(HTML 4.01 / CSS Level 2) and [YantraJS](https://github.com/yantrajs/yantra)
(ES2020+). This roadmap defines the milestones, features, and standards-compliance
targets required to evolve these foundations into a production-grade HTML and
JavaScript engine.

### Guiding Principles

- **Standards-first** — target WHATWG HTML Living Standard and ECMAScript 2023+.
- **Incremental delivery** — each milestone produces a working, testable product.
- **Managed code** — keep the engine 100 % managed C# / .NET where practical.
- **Extensibility** — expose a plugin API so rendering and scripting behaviour
  can be extended or replaced without forking.

### Reference Engines

| Engine | Useful As |
|--------|-----------|
| [Servo](https://servo.org/) | Modern layout-engine architecture reference |
| [LibreWolf / Gecko](https://www.mozilla.org/) | Standards-compliance baseline |
| [Chromium / Blink](https://www.chromium.org/) | Performance and Web-Platform-Tests runner |
| [Jint](https://github.com/sebastienros/jint) | Lightweight .NET JS runtime comparison |

---

## Milestone Planning

### Milestone 1 — Enhanced MVP (Current → +3 months)

Build on the existing HTML-Renderer + YantraJS integration to close the most
impactful gaps.

- [ ] Upgrade HTML-Renderer to support a subset of CSS3
  - Flexbox (single-axis)
  - `border-radius`, `box-shadow`, `opacity`
  - Media queries (`@media screen`)
- [ ] Implement `<style>` and `<link rel="stylesheet">` cascading per CSS
      Specificity (Level 3)
- [ ] Extend DOM bridge
  - `document.createTextNode`, `appendChild`, `removeChild`, `replaceChild`
  - `parentNode`, `childNodes`, `firstChild`, `lastChild`, `nextSibling`
  - `addEventListener` / `removeEventListener` (click, input, submit)
- [ ] Implement `window` global
  - `window.location` (read-only)
  - `window.setTimeout` / `window.setInterval` (single-threaded event queue)
  - `window.alert`, `window.console.log`
- [ ] Basic `XMLHttpRequest` / `fetch()` polyfill backed by `HttpClient`

### Milestone 2 — Standards Compliance (Months 4–8)

Focus on WHATWG HTML parsing and CSS layout correctness.

- [x] Replace ad-hoc HTML parsing with a WHATWG-compliant HTML tokeniser & tree
      builder (ref: [HTML Standard §13](https://html.spec.whatwg.org/multipage/parsing.html))
- [x] Implement CSS Box Model (Level 3)
  - Block, inline, and inline-block formatting contexts
  - Float and clear
  - Positioning: static, relative, absolute, fixed
- [x] CSS Selectors Level 4 support in `querySelector` / `querySelectorAll`
  - Combinators (`>`, `+`, `~`)
  - Pseudo-classes (`:nth-child`, `:not`, `:first-of-type`)
  - Pseudo-elements (`::before`, `::after`)
- [x] Implement `<form>` elements
  - Input types: text, checkbox, radio, select, textarea
  - Form validation and `submit` event
- [x] DOM Events Level 3
  - Event propagation (capture, target, bubble phases)
  - `stopPropagation`, `preventDefault`
  - Keyboard and mouse events

### Milestone 3 — Advanced Layout & Rendering (Months 9–14)

Add modern layout modes and visual effects.

- [x] CSS Grid Layout (Level 1)
- [x] Full Flexbox support (multi-line, `align-items`, `justify-content`, `gap`)
- [x] CSS Transitions and basic Animations (`@keyframes`, `transition`)
- [x] Text rendering improvements
  - `text-overflow`, `word-break`, `white-space`
  - Web-font loading (`@font-face`)
- [x] Image decoding pipeline
  - SVG rendering (inline and `<img>` source)
  - `<canvas>` 2-D context (basic drawing operations)
- [x] `<iframe>` support (sandboxed, same-origin)

### Milestone 4 — JavaScript Runtime Hardening (Months 12–18)

Strengthen the JS engine and align with ECMAScript 2023+.

- [x] Module system (`import` / `export`, `<script type="module">`)
- [x] Promises, `async` / `await`, and micro-task queue
- [x] Error handling: `try` / `catch` / `finally` with stack traces
- [x] `Proxy`, `Reflect`, `WeakRef`, `FinalizationRegistry`
- [x] Strict mode enforcement
- [x] `eval()` sandboxing and Content Security Policy hooks
- [x] Performance profiling hooks (script-execution timing)

### Milestone 5 — Extensibility & Plugin Architecture (Months 16–22)

Allow third-party extensions and alternative engines.

- [ ] Define `IRenderEngine` and `IScriptEngine` interfaces
- [ ] Plugin discovery via NuGet packages or filesystem scanning
- [ ] Expose rendering-pipeline hooks
  - `BeforeParse`, `AfterParse`, `BeforeLayout`, `AfterLayout`, `BeforePaint`
- [ ] Allow custom protocol handlers (`broiler://`, `data:`, `blob:`)
- [ ] DevTools-like inspector API
  - DOM tree viewer
  - Console output panel
  - Network request log

### Milestone 6 — Production Release (Months 22–28)

Polish, optimise, and prepare for public consumption.

- [ ] Accessibility: ARIA attributes, screen-reader output, keyboard navigation
- [ ] Performance targets
  - First Contentful Paint < 500 ms for a 100 KB HTML page
  - JavaScript micro-benchmark within 5× of Jint on equivalent workloads
- [ ] Security audit
  - Script sandboxing review
  - XSS and injection hardening
- [ ] Internationalisation
  - RTL text layout
  - Unicode BiDi algorithm
- [ ] Packaging as a reusable .NET library (`Broiler.Engine` NuGet)

---

## Core Features

### Parser & Lexer

| Feature | Standard | Milestone |
|---------|----------|-----------|
| HTML tokeniser (WHATWG §13) | HTML Living Standard | 2 |
| CSS tokeniser (CSS Syntax Module Level 3) | W3C | 2 |
| JavaScript parser (ESTree AST) | ECMAScript 2023 | 4 |

### DOM Implementation

| Feature | Standard | Milestone |
|---------|----------|-----------|
| Core interfaces (`Node`, `Element`, `Document`) | DOM Level 3 | 1 |
| Tree traversal (`TreeWalker`, `NodeIterator`) | DOM Level 2 Traversal | 2 |
| Mutation observers (`MutationObserver`) | DOM Level 4 | 3 |

### CSS Styling & Layout

| Feature | Standard | Milestone |
|---------|----------|-----------|
| Box model & cascading | CSS Level 2.1 | 1 (partial) |
| Selectors Level 4 | W3C | 2 |
| Flexbox | CSS Flexible Box Module Level 1 | 1 (basic), 3 (full) |
| Grid | CSS Grid Layout Module Level 1 | 3 |
| Transitions & animations | CSS Transitions / Animations Level 1 | 3 |

### JavaScript Runtime

| Feature | Standard | Milestone |
|---------|----------|-----------|
| ES2020 core (current via YantraJS) | ECMAScript 2020 | — (exists) |
| DOM bindings (`document`, `window`) | WHATWG | 1 |
| Module system | ECMAScript 2015+ | 4 |
| Micro-task queue / event loop | HTML Living Standard | 4 |

### Event System

| Feature | Standard | Milestone |
|---------|----------|-----------|
| Basic click / input events | DOM Level 2 Events | 1 |
| Full propagation model | DOM Level 3 Events | 2 |
| Custom events (`CustomEvent`) | DOM Level 4 | 3 |

### Rendering Pipeline

| Feature | Description | Milestone |
|---------|-------------|-----------|
| HTML → DOM tree | Parse HTML into in-memory DOM | 1 (exists) |
| Style resolution | Cascade, specificity, inheritance | 2 |
| Layout (reflow) | Compute box positions & sizes | 2–3 |
| Paint | Rasterise boxes to WPF visual tree | 3 |
| Composite | Layer management, z-index, opacity | 3 |

---

## Testing & Quality Assurance

### Test Pyramid

| Layer | Tools | Purpose |
|-------|-------|---------|
| Unit tests | xUnit, Moq | Individual classes and methods |
| Integration tests | xUnit + `CaptureService` | End-to-end HTML → rendered output |
| Compliance tests | WPT subset, Acid3 | Standards conformance |
| Performance tests | BenchmarkDotNet | Regression detection |

### Compliance Test Strategy

1. Import a curated subset of [Web Platform Tests (WPT)](https://github.com/web-platform-tests/wpt)
   relevant to supported features.
2. Run Acid2 and Acid3 as visual regression baselines.
3. Track pass-rates per milestone and publish results in CI.

### Continuous Integration

- All tests run on every PR via GitHub Actions.
- Website capture verification (`--url https://www.heise.de/`) validates the
  rendering pipeline after each change (existing workflow).
- Add compliance-test dashboards once Milestone 2 is reached.

---

## Documentation & Developer Experience

### API Documentation

- XML doc comments on all public types and members (existing convention).
- Auto-generated API reference via `docfx` or similar tool.

### Usage Documentation

- Getting-started guide for embedding `Broiler.Engine` in a .NET application.
- DOM API reference with interactive examples.
- Plugin development tutorial.

### Contribution Guidelines

- `CONTRIBUTING.md` covering code style, PR workflow, and ADR process.
- Issue templates for bug reports, feature requests, and compliance gaps.
- Architecture overview diagram kept up-to-date in `docs/`.

---

## Release Timeline

| Quarter | Milestone | Key Deliverable |
|---------|-----------|-----------------|
| Q1 | 1 — Enhanced MVP | CSS3 subset, extended DOM bridge, `window` global |
| Q2–Q3 | 2 — Standards Compliance | WHATWG parser, CSS box model, DOM Events L3 |
| Q3–Q4 | 3 — Advanced Layout | Grid, animations, `<canvas>`, SVG |
| Q4–Q5 | 4 — JS Runtime Hardening | Modules, event loop, ECMAScript 2023+ |
| Q5–Q6 | 5 — Extensibility | Plugin API, DevTools inspector |
| Q6–Q7 | 6 — Production Release | Accessibility, performance, security, NuGet package |

> Quarters are relative to the project start date and will be adjusted as work
> progresses. Each milestone has its own tracking issue.

---

## Action Items

- [x] Draft the first version of this roadmap
- [ ] Review and refine with team input
- [ ] Create tracking issues for each milestone
- [ ] Integrate feedback from stakeholders and potential users

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `DomBridgeTests.cs` | `Broiler.App.Tests` | JavaScript ↔ DOM interop (createElement, appendChild, etc.) |
| `Milestone3DomBridgeTests.cs` | `Broiler.App.Tests` | Extended DOM bridge methods |
| `Milestone4RuntimeTests.cs` | `Broiler.App.Tests` | Runtime feature tests |
| `ScriptEngineTests.cs` | `Broiler.App.Tests` | YantraJS evaluation |
| `DomEventsTests.cs` | `Broiler.App.Tests` | DOM event propagation |
| `FormElementTests.cs` | `Broiler.App.Tests` | Form input, select, textarea handling |
| `CssAnimationsTests.cs` | `Broiler.App.Tests` | CSS transitions and keyframes |
| `CssGridFlexTests.cs` | `Broiler.App.Tests` | Flexbox and grid layout |

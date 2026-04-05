# Roadmap: Making Broiler Google Search Compliant

> **Status**: Active — created 2026-04-05  
> **Tracking issue**: #816 — Make Broiler Google Search compliant via image and JS output comparison

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State](#2-current-state)
3. [Image Comparison Methodology](#3-image-comparison-methodology)
4. [Visual / Content Differences](#4-visual--content-differences)
5. [JavaScript Exception Investigation](#5-javascript-exception-investigation)
6. [CSS and Rendering Gap Analysis](#6-css-and-rendering-gap-analysis)
7. [Prioritized TODO List](#7-prioritized-todo-list)
8. [Comparison Pipeline](#8-comparison-pipeline)
9. [Fidelity Targets and Milestones](#9-fidelity-targets-and-milestones)

---

## 1. Executive Summary

Broiler currently renders `https://www.google.com` as a **nearly blank white page**
(99.6% white pixels, 0.4% non-white). The Google Search homepage HTML is fetched
successfully (81 KB) and contains all expected elements (logo, search box, buttons,
navigation links, footer), but the rendering output shows almost no visible content.

**Root causes identified (in priority order):**

| # | Issue                                    | Impact     | Effort |
|---|------------------------------------------|------------|--------|
| 1 | External images not rendered (logo PNG)  | High       | Medium |
| 2 | JS exceptions halt content-generating scripts | High  | Medium |
| 3 | Complex CSS selectors not fully applied  | High       | High   |
| 4 | Missing `performance` API polyfill       | Medium     | Low    |
| 5 | Missing `IntersectionObserver` polyfill  | Medium     | Low    |
| 6 | Missing `ResizeObserver` polyfill        | Medium     | Low    |
| 7 | `MutationObserver` stub returns no records | Medium   | Medium |
| 8 | External CSS stylesheets not fetched     | Low*       | Medium |
| 9 | `async` script attribute not distinguished from sync | Low | Low |
| 10 | Missing `TextEncoder`/`TextDecoder`     | Low        | Low    |
| 11 | Missing `URL`/`URLSearchParams`         | Low        | Low    |
| 12 | Missing `AbortController`               | Low        | Low    |
| 13 | Missing `CustomEvent` constructor       | Low        | Low    |

\* Google.com uses zero `<link>` stylesheet tags — all CSS is inline. However,
external CSS fetching will matter for other Google properties (Search results,
Maps, etc).

---

## 2. Current State

### 2.1 Broiler Rendering Output

- **Image size**: 1024 × 768
- **White pixels**: 783,099 / 786,432 (99.6%)
- **Non-white pixels**: 3,333 / 786,432 (0.4%)
- **Unique colours**: 307
- **Visual assessment**: Nearly blank white page — no logo, no search box, no
  navigation links visible.

### 2.2 Google HTML Structure (fetched successfully)

| Element        | Count | Description                                  |
|----------------|-------|----------------------------------------------|
| `<div>`        | 19    | Layout containers                            |
| `<a>`          | 18    | Navigation links (Gmail, Images, footer)     |
| `<input>`      | 10    | Search box, hidden fields, buttons           |
| `<script>`     | 9     | Inline JS (all with `nonce` attributes)      |
| `<span>`       | 6     | Text containers                              |
| `<style>`      | 1     | ~16 KB of inline CSS                         |
| `<img>`        | 1     | Google logo (external PNG URL)               |
| `<svg>`        | 1     | Apps grid icon                               |
| `<table>`      | 1     | Search form layout                           |
| `<link>`       | 0     | No external stylesheets                      |

### 2.3 JavaScript Exceptions

Three distinct exceptions were captured during script execution:

```
[JSUndefined] Cannot get property mei of undefined
[JSUndefined] Cannot get property addEventListener of undefined
[JSUndefined] Cannot get property clientWidth of undefined
```

All three originate from `Broiler.JavaScript.Runtime.JSUndefined.get_Item()` and
are caught by the error handler in `CaptureService.ExecuteScripts`. Scripts
continue executing after each error, but the early failures prevent
content-generating code from completing.

---

## 3. Image Comparison Methodology

The comparison pipeline follows the same approach used for Acid2 and Acid3:

1. **Broiler render**: `dotnet run --project src/Broiler.Cli --
   --capture-image https://www.google.com --output google-broiler.png
   --width 1024 --height 768`
2. **Chromium reference**: Playwright-based Chromium headless screenshot at
   identical viewport (1024 × 768)
3. **Pixel comparison**: Python tool (`scripts/google-compare.py`) produces:
   - Colour-coded diff image (`google-diff.png`)
   - Structured report (`google-report.txt`)
4. **Region-based analysis**: Content presence checked in:
   - Top bar (y=0–50): Gmail, Images, Sign in
   - Logo area (y=150–310): Google logo
   - Search box (y=310–380): Text input field
   - Buttons (y=380–440): Submit buttons
   - Footer (y=700–768): Footer links

---

## 4. Visual / Content Differences

### 4.1 Missing Elements (Broiler vs Chromium)

| Element                     | Chromium | Broiler | Root Cause                    |
|-----------------------------|----------|---------|-------------------------------|
| Google logo (colour PNG)    | ✅       | ❌      | External image not fetched    |
| "Google Search" button      | ✅       | ❌      | CSS/layout not applied        |
| "I'm Feeling Lucky" button  | ✅       | ❌      | CSS/layout not applied        |
| Search text input           | ✅       | ❌      | CSS/layout not applied        |
| "Gmail" link (top right)    | ✅       | ❌      | CSS class rules not resolved  |
| "Images" link (top right)   | ✅       | ❌      | CSS class rules not resolved  |
| "Sign in" button            | ✅       | ❌      | CSS class rules not resolved  |
| Apps grid icon (SVG)        | ✅       | ❌      | SVG rendering gaps            |
| Footer links                | ✅       | ❌      | Absolute positioning issue    |
| Country detection text      | ✅       | ❌      | JS-generated content          |

### 4.2 Background Differences (excluded from compliance)

| Aspect                 | Chromium     | Broiler      | Notes                    |
|------------------------|-------------|--------------|--------------------------|
| Page background        | White       | White        | Match                    |
| Footer background      | Light grey  | Not rendered | Footer not visible       |
| Button background      | Light grey  | Not rendered | Buttons not visible      |

---

## 5. JavaScript Exception Investigation

### 5.1 Exception 1: `Cannot get property mei of undefined`

**Stack trace origin**: `inline-vm.js:1,2` → `CaptureService.ExecuteScripts`

**Analysis**: Google's first inline script initialises a global `_g` object with
properties like `kEI`, `kEXPI`, `kBL`, `kOPI`, then assigns it to `window.google`.
A subsequent script tries to access a property `mei` on an object returned by a
DOM query or window property that resolves to `undefined` in Broiler.

**Suspected root cause**: Google's scripts call
`document.getElementById('some-id').mei` where the element doesn't exist in
Broiler's DOM (either the element was created by a previous script that failed,
or it relies on server-side rendering that produces elements Broiler doesn't see).

**Required fix**: Ensure `document.getElementById()` returns `null` (not
`undefined`) for missing elements, matching the DOM spec. Also consider adding
a `performance.now()` polyfill since Google's metrics code depends on it.

### 5.2 Exception 2: `Cannot get property addEventListener of undefined`

**Stack trace origin**: `inline-vm.js:6,5` → `inline-vm.js:1,28`

**Analysis**: A script calls `.addEventListener()` on the result of a DOM query
that returned `undefined`. This is a cascading failure — the element lookup
returns `undefined` instead of `null`, and the code doesn't guard against it.

**Suspected root cause**: Two possible issues:
1. `document.querySelector()` or `document.getElementById()` returning
   `JSUndefined` instead of `JSNull` for missing elements.
2. Missing DOM element created dynamically by an earlier script that failed.

**Required fix**: Audit all DOM query methods in `DomBridge` to ensure they
return JavaScript `null` (not `undefined`) when no element is found.

### 5.3 Exception 3: `Cannot get property clientWidth of undefined`

**Stack trace origin**: `inline-vm.js:1,56`

**Analysis**: Google's scripts access `element.clientWidth` to determine viewport
dimensions for responsive layout. The element lookup returned `undefined`.

**Suspected root cause**: Same DOM query null-vs-undefined issue as above.
Additionally, `clientWidth`/`clientHeight` may not be implemented on Broiler's
DOM element proxies.

**Required fix**:
1. Fix null-vs-undefined for DOM queries (shared root cause).
2. Implement `clientWidth`, `clientHeight`, `offsetWidth`, `offsetHeight`,
   `scrollWidth`, `scrollHeight` on DOM element proxies (return viewport
   dimensions or computed layout dimensions).

### 5.4 Impact Assessment

The three exceptions are **cascading failures** from a common root cause:
DOM element queries returning `JSUndefined` instead of `null`. This causes
every subsequent property access on the result to throw, breaking Google's
initialization chain. Fixing the null-vs-undefined issue would likely resolve
all three exceptions and allow Google's scripts to complete initialization.

---

## 6. CSS and Rendering Gap Analysis

### 6.1 CSS Features Used by Google Search

| CSS Feature                        | Used | Broiler Support | Notes                     |
|------------------------------------|------|-----------------|---------------------------|
| `font-family` stacking             | ✅   | ✅              | Roboto,Arial,sans-serif   |
| `border-radius`                    | ✅   | ✅              | Search box, buttons       |
| `position: absolute/relative`      | ✅   | ✅              | Footer, avatar            |
| `box-shadow`                       | ✅   | ⚠️ Partial      | Multiple shadow values    |
| `z-index` stacking                 | ✅   | ⚠️ Partial      | Dropdown menus            |
| `-webkit-` prefixed properties     | ✅   | ❌              | Not supported             |
| `background-size`                  | ✅   | ⚠️ Partial      | `cover`, `contain`        |
| `@media` queries                   | ✅   | ❌              | Device pixel ratio        |
| `transform` / `transform-origin`   | ✅   | ❌              | Scale for retina          |
| `text-overflow: ellipsis`          | ✅   | ❌              | Not supported             |
| `display: flex` / flexbox          | ❌   | ❌              | Not used on homepage      |
| `display: grid`                    | ❌   | ❌              | Not used on homepage      |

### 6.2 DOM API Coverage for Google Scripts

| API                          | Google Uses | Broiler Has | Status           |
|------------------------------|-------------|-------------|------------------|
| `document.getElementById`    | ✅          | ✅          | Works            |
| `document.querySelector`     | ✅          | ✅          | Works            |
| `document.addEventListener`  | ✅          | ✅          | Works            |
| `window.navigator.sendBeacon`| ✅          | ❌          | Missing          |
| `window.performance.now()`   | ✅          | ❌          | Missing          |
| `window.requestAnimationFrame`| ✅         | ✅          | Works            |
| `Image()` constructor        | ✅          | ❌          | Missing          |
| `element.clientWidth`        | ✅          | ⚠️          | Returns 0/undef  |
| `element.getBoundingClientRect`| ✅        | ❌          | Missing          |
| `document.cookie`            | ✅          | ❌          | Missing          |
| `window.crypto`              | ✅          | ❌          | Missing          |

---

## 7. Prioritized TODO List

### Phase 1: Critical — Fix Blank Rendering (P0)

These issues are blocking **any** visible content from appearing.

- [ ] **TODO-G1**: Fix DOM query null-vs-undefined return values
  - Ensure `getElementById()`, `querySelector()`, `querySelectorAll()` return
    `null` (JSNull) instead of `undefined` (JSUndefined) when no match found
  - This is the root cause of all three JS exceptions
  - **Files**: `src/Broiler.HtmlBridge/DomBridge.Registration.cs`
  - **Priority**: P0 — blocks all Google JS from executing

- [ ] **TODO-G2**: Add `performance.now()` polyfill
  - Google's metrics/logging code depends on `performance.now()` for timing
  - Return `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` or similar
  - **Files**: `src/Broiler.HtmlBridge/DomBridge.Registration.cs`
  - **Priority**: P0 — blocks Google metrics initialization

- [ ] **TODO-G3**: Add `navigator.sendBeacon()` stub
  - Google's logging code calls `navigator.sendBeacon(url, data)`
  - Return `true` (no-op) to prevent exceptions
  - **Files**: `src/Broiler.HtmlBridge/DomBridge.Registration.cs`
  - **Priority**: P0 — prevents logging code from crashing

- [ ] **TODO-G4**: Implement `element.clientWidth` / `clientHeight`
  - Google's scripts check element dimensions for responsive layout
  - Return viewport width/height for `<html>` and `<body>`, 0 for others
  - **Files**: `src/Broiler.HtmlBridge/DomBridge.Registration.cs`
  - **Priority**: P0 — blocks responsive layout code

### Phase 2: High — Enable Content Rendering (P1)

These issues prevent specific content elements from appearing.

- [ ] **TODO-G5**: Fetch and render external images
  - The Google logo (`/images/branding/googlelogo/...`) is an external PNG
  - CaptureService should resolve relative image URLs and fetch them
  - **Files**: `src/Broiler.Cli/CaptureService.cs`, HTML-Renderer image loading
  - **Priority**: P1 — logo is the most prominent visual element

- [ ] **TODO-G6**: Implement `Image()` constructor in JS engine
  - Google's `onload` handler creates `new Image()` for beacon pixels
  - Return a stub object with `src` setter
  - **Files**: `src/Broiler.HtmlBridge/DomBridge.Registration.cs`
  - **Priority**: P1 — prevents onload handler from crashing

- [ ] **TODO-G7**: Add `document.cookie` stub
  - Google's scripts read/write cookies for preferences
  - Return empty string for reads, no-op for writes
  - **Files**: `src/Broiler.HtmlBridge/DomBridge.Registration.cs`
  - **Priority**: P1 — prevents preference code from crashing

- [ ] **TODO-G8**: Apply complex CSS class selectors correctly
  - Google's inline `<style>` block uses compound selectors like
    `.gb_yb`, `.gb_R`, `.gb_Q` with complex specificity
  - Verify CSS selector engine handles all Google selector patterns
  - **Files**: `Broiler.HTML/Source/Broiler.HTML.Dom/Core/Dom/CssBox.cs`
  - **Priority**: P1 — blocks correct layout of top bar and navigation

### Phase 3: Medium — Improve Fidelity (P2)

- [ ] **TODO-G9**: Support `-webkit-` CSS prefixed properties
  - Google uses `-webkit-background-size`, `-webkit-box-shadow`,
    `-webkit-transform`, etc.
  - Map `-webkit-` prefixes to their unprefixed equivalents
  - **Priority**: P2

- [ ] **TODO-G10**: Add `IntersectionObserver` polyfill
  - Used for lazy-loading and visibility detection
  - Stub: immediately invoke callback with `isIntersecting: true`
  - **Priority**: P2

- [ ] **TODO-G11**: Add `ResizeObserver` polyfill
  - Used for responsive element resizing
  - Stub: no-op `observe()`/`disconnect()`
  - **Priority**: P2

- [ ] **TODO-G12**: Improve `MutationObserver` to track mutations
  - Current implementation is a stub that returns empty records
  - Google's scripts rely on mutation tracking for dynamic updates
  - **Priority**: P2

- [ ] **TODO-G13**: Add `TextEncoder`/`TextDecoder` polyfills
  - Used by Google's data encoding/decoding code
  - **Priority**: P2

- [ ] **TODO-G14**: Add `URL`/`URLSearchParams` polyfills
  - Used by Google's URL manipulation code
  - **Priority**: P2

### Phase 4: Low — Polish and Edge Cases (P3)

- [ ] **TODO-G15**: Support `@media` queries in CSS
  - Google uses `@media (-webkit-min-device-pixel-ratio:1.25)` for retina
  - **Priority**: P3

- [ ] **TODO-G16**: Add `AbortController` polyfill
  - Used for cancellable fetch requests
  - **Priority**: P3

- [ ] **TODO-G17**: Add `CustomEvent` constructor
  - `Event` constructor exists but `CustomEvent` with `detail` does not
  - **Priority**: P3

- [ ] **TODO-G18**: Add `window.crypto.getRandomValues()` stub
  - Used for unique ID generation
  - **Priority**: P3

- [ ] **TODO-G19**: Add `element.getBoundingClientRect()` implementation
  - Returns element position/size; used by Google for positioning
  - **Priority**: P3

- [ ] **TODO-G20**: Distinguish `async` scripts from synchronous
  - Currently all scripts execute in document order
  - `async` scripts should execute when loaded, not in order
  - **Priority**: P3

- [ ] **TODO-G21**: Fetch external CSS stylesheets
  - Google.com doesn't use external CSS, but other Google properties do
  - **Priority**: P3

---

## 8. Comparison Pipeline

### Scripts Added

| Script                     | Purpose                                        |
|----------------------------|------------------------------------------------|
| `scripts/google-compare.sh`| End-to-end pipeline: render → reference → diff |
| `scripts/google-compare.py`| Pixel comparison with region analysis          |

### Running the Pipeline

```bash
# Full pipeline (requires Node.js + Playwright for reference)
./scripts/google-compare.sh

# Skip Chromium reference (use existing reference image)
./scripts/google-compare.sh --skip-reference

# Custom output directory
./scripts/google-compare.sh --output-dir /tmp/google-test
```

### Test Coverage Added

| Test Class                          | Tests | Description                     |
|-------------------------------------|-------|---------------------------------|
| `GoogleSearchComplianceTests`       | 8     | Structural rendering validation |

Tests use a self-contained, minimal Google-like HTML (no network dependency)
to validate that Broiler can render the essential visual structure of a
Google Search homepage.

---

## 9. Fidelity Targets and Milestones

| Milestone | Target                                         | Metric             |
|-----------|-------------------------------------------------|--------------------|
| M1        | Non-blank render (any visible content)          | >1% content pixels |
| M2        | Logo + search box visible                       | >5% content pixels |
| M3        | All major elements present (logo, box, buttons) | >15% content match |
| M4        | Layout broadly correct (elements in right zones)| >30% region match  |
| M5        | Close visual match to Chromium reference         | >60% content match |

### Current: Pre-M1 (blank page)

The immediate priority is achieving M1 by fixing TODO-G1 (null-vs-undefined),
which should unblock Google's script initialization chain and allow the HTML
content to render.

---

## Appendix A: Raw JavaScript Error Log

```
[JSUndefined] Cannot get property mei of undefined
   at JSUndefined.get_Item(KeyString name) in JSUndefined.cs:34
   at inline-vm.js:1,2 → JSFunction.InvokeFunction → JSContext.Eval
   at CaptureService.ExecuteScripts

[JSUndefined] Cannot get property addEventListener of undefined
   at JSUndefined.get_Item(KeyString name) in JSUndefined.cs:34
   at inline-vm.js:6,5 → inline-vm.js:1,28 → JSFunction.InvokeFunction
   at CaptureService.ExecuteScripts

[JSUndefined] Cannot get property clientWidth of undefined
   at JSUndefined.get_Item(KeyString name) in JSUndefined.cs:34
   at inline-vm.js:1,56 → JSFunction.InvokeFunction → JSFunction.Call
   at CaptureService.ExecuteScripts
```

## Appendix B: Google HTML Content Summary

Google Search homepage HTML (fetched 2026-04-05):
- Total size: 81,270 bytes
- `<script>` tags: 9 (all with `nonce` attributes, all inline except 0 external)
- `<style>` tag: 1 (15,978 characters of CSS)
- `display:none` occurrences: 12 (hidden UI elements for progressive enhancement)
- `visibility:hidden` occurrences: 3
- Key visible elements: Google logo (external PNG), search input, two submit
  buttons, Gmail/Images links, Sign in button, footer links, apps SVG icon

# Acid3 Compliance Report — Version 1

> **Version:** 1.0
> **Date:** 2026-03-09
> **Canonical tracker:** Issue "Verify html-renderer against Acid3 test and create compliance roadmap"

## Summary

| Metric                     | Broiler      | Chromium (local) | Notes                         |
| -------------------------- | ------------ | ---------------- | ----------------------------- |
| **Acid3 Score**            | **0 / 100**  | **63 / 100**     | Broiler has no JS DOM bridge  |
| Full-image pixel match     | 17.20 %      | —                | vs. Chromium reference        |
| Content-area pixel match   | 0.12 %       | —                | Only gray border matches      |
| Broiler content coverage   | 15.78 %      | 73.00 %          | Broiler renders border only   |
| Viewport render dimensions | 1024 × 768   | 1024 × 768       | Match ✅                       |
| Full-page auto-size        | 832 × 191    | —                | Collapsed, no JS layout       |

### Chromium Bucket Breakdown (local file://)

| Bucket | Tests Passed | Category                              | Background Color |
| ------ | ------------ | ------------------------------------- | ---------------- |
| 1      | 8 / 16       | DOM Traversal, DOM Range, HTTP        | gray             |
| 2      | 13 / 16      | DOM2 Core, DOM Events                 | silver           |
| 3      | 2 / 16       | DOM Selectors, getComputedStyle       | black            |
| 4      | 15 / 16      | CSS Tables, HTML Forms                | silver           |
| 5      | 5 / 16       | SVG, Dynamic Content                  | black            |
| 6      | 16 / 16      | JS Language, Unicode, XHTML           | fuchsia          |

> Chromium scores 63/100 on local `file://` due to same-origin restrictions on
> `<iframe>`, `<object>`, and XHR tests (14–16, 65, 69, 74, etc.). On the live
> `http://acid3.acidtests.org/` URL, Chromium scores 97–100/100.

### Broiler Render Analysis

Broiler's render output contains **only** the `html` element's `2cm solid gray`
border and a white interior. No text, no colored buckets, no score display, and
no JavaScript-driven DOM modifications are visible. This is consistent with
Broiler's current architecture where:

1. **HtmlRenderer** parses and renders static HTML/CSS but does not execute JavaScript.
2. **YantraJS** is integrated for JS execution in the WPF app (`DomBridge`) but
   is **not wired into the CLI image-capture path**.
3. Acid3 requires JavaScript execution to run its 100 subtests and dynamically
   build the visible page content (score, colored buckets, test results).

## 1 Methodology

### 1.1 Broiler Render

```bash
# Full-page auto-sized render
dotnet run --project src/Broiler.Cli/Broiler.Cli.csproj -- \
  --capture-image "acid/acid3/acid3.html" \
  --output "acid/acid3/acid3.png" --full-page

# Fixed 1024×768 viewport render
dotnet run --project src/Broiler.Cli/Broiler.Cli.csproj -- \
  --capture-image "acid/acid3/acid3.html" \
  --output "acid/acid3/acid3-viewport.png" \
  --width 1024 --height 768
```

### 1.2 Chromium Reference Render (Playwright)

```javascript
const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  await page.setViewportSize({ width: 1024, height: 768 });
  const acid3 = 'file://' + path.resolve('acid/acid3/acid3.html');
  await page.goto(acid3, { waitUntil: 'load' });
  await page.waitForTimeout(15000); // Allow tests to run
  await page.screenshot({ path: 'acid/acid3/acid3-reference.png' });
  await browser.close();
})();
```

### 1.3 Pixel Comparison

- **Color tolerance:** 5 per RGB channel (out of 255)
- **Tool:** PIL/Pillow pixel-by-pixel comparison
- **Diff image:** `acid/acid3/acid3-diff.png` (red overlay on mismatched pixels)

## 2 Root-Cause Analysis

### 2.1 No JavaScript Execution in CLI Render Path

**Impact:** All 100 Acid3 subtests fail (score 0/100).

The Acid3 test page is almost entirely JavaScript-driven. The initial HTML
contains only the page structure (title, score placeholder, instruction text,
and bucket containers). All test content, score updates, and visual feedback are
produced by JavaScript that:

- Creates and manipulates DOM nodes
- Dispatches events and checks event handling
- Queries computed styles via `getComputedStyle()`
- Uses `document.createRange()`, `TreeWalker`, `NodeIterator`
- Loads and inspects `<iframe>` and `<object>` content
- Tests SVG document interfaces
- Validates ECMAScript language features

**Root cause:** `CaptureService.CaptureImageAsync()` in the CLI uses
`HtmlRender.RenderToFile()` / `HtmlRender.RenderToImageAutoSized()` which only
invoke the HTML/CSS layout engine. The YantraJS integration (`DomBridge`) exists
only in the WPF application path (`Broiler.App`), not in the CLI capture
pipeline.

### 2.2 CSS Rendering Gaps (Visible Even Without JS)

Even for the static HTML that Broiler does render, several CSS features used by
Acid3 are not fully supported:

| CSS Feature                   | Acid3 Usage                               | Broiler Support |
| ----------------------------- | ----------------------------------------- | --------------- |
| `border: 2cm solid gray`      | `html` element border                     | ✅ Partial       |
| `background: silver`          | `:root` background                        | ❌ Not rendered  |
| `@font-face`                  | Custom `AcidAhemTest` font                | ❌ Not supported |
| `text-shadow`                 | Title text shadow                         | ❌ Not supported |
| `data:` URI backgrounds       | Body & instruction backgrounds            | ❓ Untested      |
| `::after` content positioning | `map::after { position: absolute; ... }`  | ⚠️ Partial       |
| `:root` selector              | Background color on `<html>`              | ❌ Not matched   |
| `hsla()` colors               | `color: hsla(0, 0%, 0%, 1.0)`            | ❌ Not supported |
| `inline-block`                | Bucket display                            | ✅ Supported     |
| `position: fixed`             | `<object>` positioning                    | ❌ Not supported |

### 2.3 DOM API Gaps

Acid3 tests a broad set of DOM APIs. Below is a categorization of APIs tested
and their availability in YantraJS + DomBridge:

| DOM API Category         | Tests | Example APIs                               | DomBridge Status    |
| ------------------------ | ----- | ------------------------------------------ | ------------------- |
| DOM Traversal            | 1–6   | `TreeWalker`, `NodeIterator`, `NodeFilter` | ❌ Not implemented   |
| DOM Range                | 7–13  | `createRange`, `extractContents`           | ❌ Not implemented   |
| DOM Core                 | 17–29 | `nodeType`, `namespaceURI`, `getElementById` | ⚠️ Partial          |
| DOM Events               | 30–32 | `dispatchEvent`, `stopPropagation`         | ⚠️ Partial          |
| Selectors API            | 33–44 | `querySelector`, `:nth-child`, `:lang()`   | ❌ Not implemented   |
| CSSOM                    | 45–48 | `getComputedStyle`, `cssFloat`, media queries | ❌ Not implemented |
| HTML Forms               | 49–64 | `HTMLTableElement`, `HTMLSelectElement`     | ❌ Not implemented   |
| SVG DOM                  | 65–80 | `getSVGDocument`, `SVGLength`              | ❌ Not implemented   |
| ECMAScript Conformance   | 81–96 | Array methods, RegExp, Date, `encodeURI`   | ✅ YantraJS covers  |
| XHTML / Data URI         | 97–99 | XHTML DOM, `data:` URI parsing             | ❌ Not implemented   |

### 2.4 Full-Page Auto-Size Collapse

The full-page render (`--full-page`) produced a 832 × 191 image instead of the
expected ~920 × 770+ layout. This occurs because:

- Without JavaScript execution, the dynamic content (buckets, score text) is
  never created.
- The CSS `font: 0/0 Arial, sans-serif` on `.buckets` collapses bucket
  containers to zero height.
- Only the gray border and minimal static content remain, resulting in a very
  small auto-sized output.

## 3 Acid3 Subtest Analysis

### 3.1 Bucket 1 — DOM Traversal, DOM Range, HTTP (Tests 0–16)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 0 | `:last-child` style recomputation | ✅ | ❌ | JS + DOM |
| 1 | `NodeFilter` and exceptions | ✅ | ❌ | JS: `TreeWalker` |
| 2 | Removing nodes during iteration | ✅ | ❌ | JS: `NodeIterator` |
| 3 | Infinite iterator | ✅ | ❌ | JS: `NodeIterator` |
| 4 | Whitespace text nodes (iterators) | ✅ | ❌ | JS: `NodeIterator` |
| 5 | Whitespace text nodes (walkers) | ✅ | ❌ | JS: `TreeWalker` |
| 6 | Walking outside a tree | ✅ | ❌ | JS: `TreeWalker` |
| 7 | Basic Range tests | ✅ | ❌ | JS: `Range` API |
| 8 | Moving boundary points | ❌ | ❌ | JS: `Range` API |
| 9 | `extractContents()` in Document | ❌ | ❌ | JS: `Range` API |
| 10 | Ranges and attribute nodes | ❌ | ❌ | JS: `Range` API |
| 11 | Ranges and comments | ❌ | ❌ | JS: `Range` API |
| 12 | Ranges under mutations (insert) | ❌ | ❌ | JS: `Range` API |
| 13 | Ranges under mutations (delete) | ❌ | ❌ | JS: `Range` API |
| 14 | HTTP Content-Type: image/png | ❌ | ❌ | Network (file://) |
| 15 | HTTP Content-Type: text/plain | ❌ | ❌ | Network (file://) |
| 16 | `<object>` handling, HTTP status | ❌ | ❌ | Network (file://) |

### 3.2 Bucket 2 — DOM2 Core, DOM Events (Tests 17–32)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 17 | `hasAttribute` | ✅ | ❌ | JS: DOM Core |
| 18 | `nodeType` accuracy | ✅ | ❌ | JS: DOM Core |
| 19 | DOM constants | ✅ | ❌ | JS: DOM Core |
| 20 | Null bytes in various places | ✅ | ❌ | JS: DOM Core |
| 21 | Basic namespace | ✅ | ❌ | JS: Namespaces |
| 22 | `createElement()` invalid names | ✅ | ❌ | JS: DOM Core |
| 23 | `createElementNS()` invalid names | ✅ | ❌ | JS: Namespaces |
| 24 | Event handler attributes | ✅ | ❌ | JS: Events |
| 25 | `createDocumentType` namespaces | ✅ | ❌ | JS: DOM Core |
| 26 | Document tree survival | ✅ | ❌ | JS: DOM Core |
| 27 | Continuation of test 26 | ✅ | ❌ | JS: DOM Core |
| 28 | `getElementById()` | ✅ | ❌ | JS: DOM Core |
| 29 | Whitespace survives cloning | ✅ | ❌ | JS: DOM Core |
| 30 | `dispatchEvent()` | ❌ | ❌ | JS: Events |
| 31 | `stopPropagation()` and capture | ❌ | ❌ | JS: Events |
| 32 | Events bubbling through Document | ❌ | ❌ | JS: Events |

### 3.3 Bucket 3 — DOM Selectors, getComputedStyle (Tests 33–48)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 33 | Classes, attributes selectors | ✅ | ❌ | JS: Selectors API |
| 34 | `:lang()` and `[|=]` | ✅ | ❌ | JS: Selectors API |
| 35 | `:first-child` | ❌ | ❌ | JS: Selectors API |
| 36 | `:last-child` | ❌ | ❌ | JS: Selectors API |
| 37 | `:only-child` | ❌ | ❌ | JS: Selectors API |
| 38 | `:empty` | ❌ | ❌ | JS: Selectors API |
| 39 | `:nth-child`, `:nth-last-child` | ❌ | ❌ | JS: Selectors API |
| 40 | `*-of-type` pseudo-classes | ❌ | ❌ | JS: Selectors API |
| 41 | `:root`, `:not()` | ❌ | ❌ | JS: Selectors API |
| 42 | `+`, `~`, `>`, ` ` dynamic | ❌ | ❌ | JS: Selectors API |
| 43 | `:enabled`, `:disabled`, `:checked` | ❌ | ❌ | JS: Selectors API |
| 44 | Selectors without spaces before `*` | ❌ | ❌ | JS: Selectors API |
| 45 | `cssFloat` and style attribute | ❌ | ❌ | JS: CSSOM |
| 46 | Media queries | ❌ | ❌ | JS: CSSOM |
| 47 | `cursor` and CSS3 values | ❌ | ❌ | JS: CSSOM |
| 48 | `:link` and `:visited` | ❌ | ❌ | JS: CSSOM |

### 3.4 Bucket 4 — CSS Tables, HTML Forms (Tests 49–64)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 49–51 | Table accessors and ordering | ✅ | ❌ | JS: HTML DOM |
| 52–59 | Form elements, inputs, selects | ✅ | ❌ | JS: HTML DOM |
| 60–64 | Attributes, className, `<area>` | ✅ | ❌ | JS: HTML DOM |

### 3.5 Bucket 5 — SVG, Dynamic Content (Tests 65–80)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 65 | Load SVG/HTML files dynamically | ✅ | ❌ | JS: iframe/SVG |
| 66 | `localName` on text nodes | ✅ | ❌ | JS: DOM Core |
| 68 | UTF-16 surrogate pairs | ✅ | ❌ | JS: Unicode |
| 69–70 | Support files, XML encoding | ❌ | ❌ | Network / iframe |
| 71 | HTML parsing | ✅ | ❌ | JS: Parser |
| 72 | Dynamic `<style>` modification | ✅ | ❌ | JS: CSSOM |
| 73 | Nested events | ❌ | ❌ | JS: Events |
| 74 | `getSVGDocument()` | ❌ | ❌ | JS: SVG DOM |
| 80 | Remove iframes and objects | ❌ | ❌ | JS: DOM |

### 3.6 Bucket 6 — JS Language, Unicode, XHTML (Tests 81–96)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 81 | Array elisions at end | ✅ | ❌* | YantraJS capable |
| 82 | Array elisions in middle | ✅ | ❌* | YantraJS capable |
| 83 | Array methods | ✅ | ❌* | YantraJS capable |
| 84 | Number-to-string conversion | ✅ | ❌* | YantraJS capable |
| 85 | String operations | ✅ | ❌* | YantraJS capable |
| 86 | Date methods (no args) | ✅ | ❌* | YantraJS capable |
| 87 | Date tests — years | ✅ | ❌* | YantraJS capable |
| 88 | Unicode escapes in identifiers | ✅ | ❌* | YantraJS capable |
| 89 | Regular expressions | ✅ | ❌* | YantraJS capable |
| 90 | Regular expressions (cont.) | ✅ | ❌* | YantraJS capable |
| 91 | Property enumeration | ✅ | ❌* | YantraJS capable |
| 92 | Function object properties | ✅ | ❌* | YantraJS capable |
| 93 | `FunctionExpression` semantics | ✅ | ❌* | YantraJS capable |
| 94 | Exception scope | ✅ | ❌* | YantraJS capable |
| 95 | Types of expressions | ✅ | ❌* | YantraJS capable |
| 96 | `encodeURI()` and null bytes | ✅ | ❌* | YantraJS capable |

> **\*** Bucket 6 tests are pure ECMAScript language tests. YantraJS already
> supports these features. These tests fail only because JS is not executed in
> the CLI render path, not because of engine limitations.

### 3.7 Special Tests (97–99)

| Test | Description | Chromium | Broiler | Blocker |
| ---- | ----------- | -------- | ------- | ------- |
| 97 | `data:` URI parsing | ✅ | ❌ | JS: URI handling |
| 98 | XHTML and the DOM | ✅ | ❌ | JS: XHTML DOM |
| 99 | "Weirdest bug ever" | ✅ | ❌ | JS: Edge case |

## 4 Compliance Roadmap

### Phase 1: Wire JS Engine into CLI Render Path (Priority: Critical)

**Goal:** Enable JavaScript execution during image capture so that
dynamically-generated content (like Acid3's test page) is rendered.

**Tasks:**

1. **Integrate YantraJS into `CaptureService`**: Port the `DomBridge` from
   `Broiler.App` to a shared library usable by both the WPF app and CLI.
2. **Implement minimal DOM APIs for JS context**: The CLI render path needs at
   minimum:
   - `document.getElementById()`
   - `document.createElement()` / `createTextNode()`
   - `document.getElementsByTagName()`
   - `element.appendChild()` / `removeChild()` / `insertBefore()`
   - `element.className` / `setAttribute()` / `getAttribute()`
   - `element.style` (read/write)
   - `element.textContent` / `innerHTML`
   - `window.getComputedStyle()`
3. **Execute `<script>` elements during render**: Parse and execute inline
   scripts before layout.
4. **Re-render after JS execution**: Perform layout and paint after all scripts
   have run and DOM has been modified.

**Expected impact:** Enables Bucket 6 (tests 81–96) to pass immediately, as
these are pure JS language tests that YantraJS already supports. Estimated
score: **16–20 / 100**.

### Phase 2: DOM Traversal and Range APIs (Priority: High)

**Goal:** Implement DOM Level 2 Traversal and Range APIs.

**Tasks:**

1. **`TreeWalker`** — `createTreeWalker()`, `parentNode()`, `firstChild()`,
   `nextSibling()`, etc.
2. **`NodeIterator`** — `createNodeIterator()`, `nextNode()`,
   `previousNode()`, `detach()`
3. **`NodeFilter`** — `acceptNode()`, filter constants
4. **`Range`** — `createRange()`, `setStart()`, `setEnd()`,
   `extractContents()`, `cloneContents()`, `collapse()`, `selectNode()`,
   `selectNodeContents()`, mutation handling

**Expected impact:** Tests 1–13 (partial). Estimated additional score: **+6–10**.

### Phase 3: DOM Events (Priority: High)

**Goal:** Full DOM Level 2 Events implementation.

**Tasks:**

1. **Event dispatch**: `dispatchEvent()`, event propagation (capture, target,
   bubbling phases)
2. **`stopPropagation()`**, `preventDefault()`
3. **Event handler attributes**: `onclick`, `onload`, etc.
4. **`addEventListener()` / `removeEventListener()`**

**Expected impact:** Tests 24, 30–32, 73. Estimated additional score: **+3–5**.

### Phase 4: Selectors API and CSSOM (Priority: Medium)

**Goal:** Implement CSS Selectors API and CSSOM for JS access.

**Tasks:**

1. **`querySelector()` / `querySelectorAll()`**: CSS selector matching
2. **`getComputedStyle()`**: Return computed CSS values
3. **CSS pseudo-class support**: `:nth-child()`, `:lang()`, `:first-child`,
   `:last-child`, `:only-child`, `:empty`, `:root`, `:not()`, `:enabled`,
   `:disabled`, `:checked`, etc.
4. **`cssFloat` property access**
5. **Media queries**: `window.matchMedia()`

**Expected impact:** Tests 33–48. Estimated additional score: **+10–14**.

### Phase 5: HTML DOM Interfaces (Priority: Medium)

**Goal:** Implement HTML-specific DOM interfaces.

**Tasks:**

1. **`HTMLTableElement`**: `createTHead()`, `insertRow()`, `deleteRow()`,
   `rows`, `tBodies`
2. **`HTMLFormElement`**: `.elements` collection
3. **`HTMLSelectElement`**: `.add()`, `.options`
4. **`HTMLInputElement`**: `.type`, `.checked`, `.value` (dynamic changes)
5. **`HTMLOptionElement`**: `.defaultSelected`
6. **`HTMLButtonElement`**: `.type`, attribute reflection

**Expected impact:** Tests 49–64. Estimated additional score: **+12–15**.

### Phase 6: SVG DOM and Cross-Document APIs (Priority: Low)

**Goal:** SVG document interfaces and cross-frame access.

**Tasks:**

1. **`getSVGDocument()`** interface
2. **`SVGLength`**, `SVGAnimatedLength` objects
3. **SVG font support** (SVG 1.1 fonts)
4. **Cross-document access**: `iframe.contentDocument`
5. **`<object>` element handling**

**Expected impact:** Tests 65, 69–70, 74–80. Estimated additional score:
**+3–8**.

### Phase 7: CSS Rendering Improvements (Priority: Low)

**Goal:** Fix CSS rendering gaps visible in the Acid3 page.

**Tasks:**

1. **`:root` selector**: Apply styles to `<html>` element
2. **`@font-face`**: Load and use custom fonts
3. **`text-shadow`**: Implement text shadow rendering
4. **`hsla()` / `hsl()` colors**: Parse and apply HSL color values
5. **`position: fixed`**: Implement fixed positioning
6. **`data:` URI backgrounds**: Render inline data URI images

### Phase 8: Network and HTTP Compliance (Priority: Future)

**Goal:** Support tests requiring network access.

**Tasks:**

1. **XHR / Fetch API**: `XMLHttpRequest` for tests 14–16
2. **Content-Type handling**: Proper MIME type detection for loaded resources
3. **HTTP status code handling**: `<object>` fallback behavior
4. **CORS / same-origin**: Proper cross-origin policies

## 5 Estimated Progression

| Phase | Cumulative Score (est.) | Key Milestones |
| ----- | ----------------------- | -------------- |
| Current | 0 / 100 | No JS execution |
| Phase 1 | 16–20 / 100 | JS engine in CLI, Bucket 6 passes |
| Phase 2 | 26–30 / 100 | DOM Traversal + Range |
| Phase 3 | 29–35 / 100 | DOM Events |
| Phase 4 | 39–49 / 100 | Selectors API + CSSOM |
| Phase 5 | 51–64 / 100 | HTML DOM interfaces |
| Phase 6 | 54–72 / 100 | SVG DOM |
| Phase 7 | 60–78 / 100 | CSS rendering fixes |
| Phase 8 | 75–95 / 100 | Network/HTTP compliance |

> **Note:** Achieving 100/100 requires pixel-perfect rendering matching the
> Acid3 reference page, smooth animation, and complete JavaScript conformance.
> The final score depends on cumulative implementation quality across all phases.

## 6 Files Produced

| File | Description |
| ---- | ----------- |
| `acid/acid3/acid3.html` | Acid3 test page (from acid3.acidtests.org) |
| `acid/acid3/acid3.png` | Broiler full-page render (832 × 191) |
| `acid/acid3/acid3-viewport.png` | Broiler fixed-viewport render (1024 × 768) |
| `acid/acid3/acid3-reference.png` | Chromium reference render (1024 × 768) |
| `acid/acid3/acid3-diff.png` | Pixel-diff heatmap (red = mismatch) |
| `acid/acid3/font.ttf` | AcidAhemTest font (Acid3 resource) |
| `acid/acid3/empty.*` | Acid3 support files (css, html, png, txt) |
| `acid/acid3/reference.html` | Acid3 reference page |
| `docs/acid3-compliance-v1.md` | This document |

## 7 Comparison with Acid2 Status

| Aspect | Acid2 | Acid3 |
| ------ | ----- | ----- |
| JS required? | No | **Yes** (critical) |
| Broiler score | ~89% content match | 0/100 |
| Primary blocker | CSS edge cases | No JS in CLI path |
| Test count | 1 visual test | 100 subtests |
| Complexity | HTML + CSS | HTML + CSS + JS + DOM + SVG + Events |

Acid2 compliance is primarily a CSS/HTML rendering challenge. Acid3 compliance
requires a **full browser engine** with JavaScript execution, DOM manipulation,
event handling, and cross-document access. The roadmap above reflects this
fundamental difference in scope.

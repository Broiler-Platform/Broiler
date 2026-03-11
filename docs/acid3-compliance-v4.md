# Acid3 Compliance Report — Version 4

**Date:** 2026-03-11
**Branch:** `copilot/verify-html-renderer-acid3-again`
**Broiler CLI version:** `net8.0`, YantraJS 1.2.295, HtmlRenderer 1.5.2 (SkiaSharp)
**Previous:** [acid3-compliance-v3.md](acid3-compliance-v3.md)

---

## 1. Test Setup

### Broiler CLI Capture

```bash
dotnet run --project src/Broiler.Cli/Broiler.Cli.csproj -- \
  --capture-image "file:///path/to/acid/acid3/acid3.html" \
  --output docs/images/acid3-broiler-v4.png \
  --width 800 --height 600
```

- **Output dimensions:** 800 × 600
- **File size:** 12,262 bytes

### Chromium / Playwright Reference

Rendered via Playwright against the live http://acid3.acidtests.org/ URL:

```python
from playwright.sync_api import sync_playwright
with sync_playwright() as p:
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": 800, "height": 600})
    page.goto("http://acid3.acidtests.org/")
    page.wait_for_timeout(8000)
    page.screenshot(path="acid3-chromium-v4.png")
```

- **Output dimensions:** 800 × 600
- **File size:** 38,492 bytes
- **Chromium version:** 145.0.7632.6 (headless shell, Playwright v1208)
- **Score:** 96 / 100

### Images

| Broiler v4 | Chromium |
|------------|----------|
| ![Broiler v4](images/acid3-broiler-v4.png) | ![Chromium](images/acid3-chromium-v4.png) |

### Diff

| Diff (magenta = divergent pixels) |
|-----------------------------------|
| ![Diff](images/acid3-diff-v4.png) |

---

## 2. Scores

| Engine | Score | Notes |
|--------|-------|-------|
| **Chromium 145 (live HTTP)** | **96 / 100** | Buckets 1, 3–6 fully lit; bucket 2 at 13/16 |
| **Chromium 145 (file://)** | **43 / 100** | Many tests need HTTP for sub-resources |
| **Broiler CLI v4** | **0 / 100** | Red "FAIL" background; test harness never completes |
| *Broiler CLI v3* | *0 / 100* | *(same score; identical rendering)* |

---

## 3. Image Comparison

### 3.1 Pixel-Level Metrics (Broiler v4 vs Chromium live)

| Metric | Value |
|--------|-------|
| Image dimensions | 800 × 600 (both) |
| Total pixels | 480,000 |
| Pixel match (tolerance ±5) | **34.0 %** (163,154 / 480,000) |
| Pixel mismatch | **66.0 %** (316,846 / 480,000) |

### 3.2 Region-Level Match

| Region | Match % | Pixels |
|--------|---------|--------|
| Top border (y 0–20) | **83.9 %** | 13,424 / 16,000 |
| Score area (y 20–60) | **10.1 %** | 3,224 / 32,000 |
| Content area (y 60–300) | **2.9 %** | 5,582 / 192,000 |
| Bottom half (y 300–600) | **58.7 %** | 140,924 / 240,000 |

### 3.3 Comparison with v3

| Metric | v3 | v4 | Change |
|--------|----|----|--------|
| Broiler image dimensions | 800 × 600 | 800 × 600 | No change |
| Broiler file size | 12,045 B | 12,262 B | +217 B |
| Overall pixel match | 34.5 % | 34.0 % | −0.5 pp (ref changed from file:// to live) |
| Score area match | 10.1 % | 10.1 % | No change |
| Score | 0 / 100 | 0 / 100 | No change |
| CLI tests | 467 | 467 | No change |

### 3.4 Dominant Colour Analysis

**Broiler v4:**

| Colour | Coverage |
|--------|----------|
| Silver RGB(192,192,192) | 59.8 % |
| Red RGB(255,0,0) | 38.0 % |
| Black RGB(0,0,0) | 0.6 % |
| Grey RGB(128,128,128) | 0.5 % |

**Chromium v4 (live):**

| Colour | Coverage |
|--------|----------|
| White RGB(255,255,255) | 46.3 % |
| Silver RGB(192,192,192) | 43.1 % |
| Black RGB(0,0,0) | 4.9 % |
| Red RGB(255,0,0) | 1.6 % |
| Grey RGB(128,128,128) | 1.5 % |

### 3.5 Visual Differences

| # | Area | Broiler v4 | Chromium | Root Cause |
|---|------|------------|----------|------------|
| 1 | **Score display** | Absent (red flood) | `96/100` in large text | Test harness never completes → score stays at initial state |
| 2 | **Background** | Red `#FF0000` flood fill covering content area | White with silver border | CSS rule `h1 { color: red }` initial style applied; test 0 never runs to clear it |
| 3 | **"Acid3" heading** | Hidden by red flood | Large heading with `text-shadow` | Red background obscures heading |
| 4 | **Coloured buckets** | Not visible (class `z` → hidden) | 6 coloured blocks (red, orange, yellow, lime, blue, purple) | Bucket classes never updated by passing tests |
| 5 | **"FAIL" text** | Visible at two positions | Not present | `<iframe>FAIL</iframe>` fallback content rendered as text |
| 6 | **Garbled text** | Visible at top of content area | Not present | Script text, variable names, and helper strings leaked into render |
| 7 | **Purple element** | Small fuchsia block at y ≈ 200 | Not present | `map::after` pseudo-element rendered; should be hidden after test execution |
| 8 | **Instructions paragraph** | Not visible (hidden by red) | "To pass the test…" visible at bottom in grey | Red flood obscures content |
| 9 | **`text-shadow`** | Implementation exists but not visible | Shadow on "Acid3" heading | Red flood obscures the heading |
| 10 | **`@font-face` glyph** | Implementation exists but not visible | "X" from `AcidAhemTest` font | Covered by red flood |
| 11 | **Body `data:` background** | Implementation exists but not visible | Small pattern image at top-right | Red flood obscures it |
| 12 | **Text density** | 0.9 % dark pixels in content area | 10.4 % dark pixels in content area | Proper text content not rendered |

---

## 4. Root Cause Analysis

### 4.1 Why Broiler Still Scores 0 / 100

The Acid3 test page contains ~183 KB of HTML with 10 `<script>` blocks. The main script block (172 KB) defines 100 test functions in an array. The test harness operates as follows:

1. **Page loads** → 10 inline scripts execute sequentially
2. **Script 9** uses `document.write()` to inject iframes, form, and table elements
3. **`<body onload="update()">`** fires the `update()` function
4. **`update()`** iterates through `tests[]` array, executing each test and updating score
5. **Each passing test** increments the score and updates bucket CSS classes to show coloured blocks
6. **After all tests**, the red background is replaced with white

**Critical Failure Chain:**

```
1. Page loaded → inline scripts execute
2. ✅ Script 0: var startTime = new Date()
3. ✅ Script 1: d1–d5 set to "fail"
4. ✅ Scripts 2–6: External src scripts (empty.css etc.) — resolved via file://
5. ✅ Script 7: nullInRegexpArgumentResult
6. ✅ Script 8: 172 KB main harness — tests[] array populated
7. ⚠️ Script 9: document.write() — partial support in DomBridge
8. ⚠️ Body onload fires → update() called
9. ❌ Test 0 attempts getComputedStyle cascade check
10. ❌ Test harness encounters runtime errors → halts with score 0
11. Red background never cleared → all content obscured
```

### 4.2 Key Blocking Issues

| Priority | Issue | Impact | Detail |
|----------|-------|--------|--------|
| **P0** | `document.write()` partial/broken | Blocks test infrastructure | Script 9 injects critical DOM (iframes, form, table) via `document.write()`. If the written HTML isn't parsed and integrated into the live DOM correctly, tests 14–16, 52, 65, 69, 80 fail. |
| **P0** | `<style>` textContent → live re-render | Blocks test 0 visual result | When JS modifies `<style>` element's textContent, the rendering engine must re-parse and apply the new CSS rules. Currently no live stylesheet invalidation in the render pipeline. |
| **P0** | Runtime errors halt test harness | Blocks score > 0 | Any uncaught TypeError/ReferenceError in the test chain halts `update()`. Missing DOM APIs cause cascading failures. |
| **P1** | HTTP sub-resource loading | Blocks tests 14–16 from live URL | The CLI uses `file://` protocol; many Acid3 tests require HTTP Content-Type headers for `image/png`, `text/plain` validation. |
| **P1** | `getComputedStyle` live cascade | Blocks test 0 | After DOM mutation + `<style>` changes, computed style must reflect current state. The cascade is implemented but may not handle dynamic `<style>` element insertion. |
| **P2** | SVG/SMIL tests (75–79) | Not scored | Competition tests — not counted in final score but relevant for full compliance. |

### 4.3 What Works (Unit-Tested)

Based on 467 CLI tests covering individual Acid3 sub-tests:

| Area | Tests | Unit-test Status | Acid3 Integration |
|------|-------|-----------------|-------------------|
| DOM Traversal (TreeWalker, NodeIterator) | 0–6 | ✅ 28 tests pass | ❌ Not reached in harness |
| DOM Range | 7–13 | ✅ 28 tests pass | ❌ Not reached in harness |
| HTTP/Sub-resources | 14–16 | ✅ 13 tests pass | ❌ Not reached in harness |
| DOM Core (namespace, constants) | 17–23 | ✅ 35 tests pass | ❌ Not reached in harness |
| DOM Events | 24, 30–32 | ✅ 24 tests pass | ❌ Not reached in harness |
| CSS Selectors | 33–48 | ✅ 35 tests pass | ❌ Not reached in harness |
| HTML DOM (tables, forms, inputs) | 49–64 | ✅ 34 tests pass | ❌ Not reached in harness |
| SVG/Dynamic content | 65–74, 80 | ✅ 38 tests pass | ❌ Not reached in harness |
| CSS Rendering | text-shadow, @font-face, borders | ✅ 32 tests pass | ❌ Not reached in harness |
| ECMAScript | 81–99 | ✅ 36 tests pass | ❌ Not reached in harness |
| Timer/Async | setTimeout chaining | ✅ 12 tests pass | ⚠️ Partially working |
| Network (fetch/XHR) | headers, methods | ✅ 36 tests pass | ❌ Not reached in harness |

**Key insight:** Individual features work in isolation (467 tests pass), but the end-to-end Acid3 harness cannot execute because early-stage blockers prevent the test loop from starting.

### 4.4 Architecture — Current State

```
Acid3 HTML
    ↓ fetch (file:// URI)
    ↓ parse → HtmlTreeBuilder → DOM tree
    ↓ extract inline <script> tags + external src scripts
    ↓ create JSContext + DomBridge
    ↓ eval each script sequentially
    ↓ ⚠️ document.write() partially integrated
    ↓ FireWindowLoadEvent() → body.onload → update()
    ↓ ❌ update() encounters errors → halts
    ↓ FlushTimers() → no pending callbacks (harness already halted)
    ↓ serialize DOM to HTML (un-modified — still initial state)
    ↓ HtmlRender.RenderToFile() (SkiaSharp)
    ↓ PNG output (red background, no score)

Implemented:
  ✅ 30+ DOM APIs (getComputedStyle, querySelector, classList, etc.)
  ✅ DOMImplementation (createDocument, createDocumentType, createHTMLDocument)
  ✅ DOMException with error codes
  ✅ Namespace-aware attribute methods
  ✅ DOM Events Level 2/3 (capture, bubble, stopPropagation, preventDefault)
  ✅ CSS Selectors Level 3 (12+ pseudo-classes, 6 attribute selectors)
  ✅ CSSOM (cssRules, getComputedStyle, matchMedia)
  ✅ HTML DOM (tables, forms, selects, options, buttons)
  ✅ SVG DOM (viewBox, animVal, getSVGDocument)
  ✅ Timer APIs (setTimeout, setInterval, requestAnimationFrame)
  ✅ fetch() and XMLHttpRequest
  ✅ Rendering: text-shadow, @font-face, data: URI backgrounds, dotted borders
  ✅ CSS units: cm, mm, in, pt, pc, px, em, rem, %

Critical Gaps:
  ✗ document.write() → live DOM integration during/after script execution
  ✗ Dynamic <style> textContent → live stylesheet invalidation in render pipeline
  ✗ End-to-end test harness execution (update() runs without halting)
```

---

## 5. Compliance Gap Catalogue (v4 — Full Re-Assessment)

### Bucket 1: DOM Traversal, DOM Range, HTTP (Tests 0–16)

| Test | Title | Unit | E2E | Blocker | Module |
|------|-------|------|-----|---------|--------|
| 0 | Styles recompute after last-child removal | ✅ | ❌ | Dynamic `<style>` re-render + `document.write()` DOM not integrated | `DomBridge.Css.cs` |
| 1 | NodeFilters and Exceptions | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 2 | Removing nodes during iteration | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 3 | Infinite iterator | ✅ | ❌ | Blocked by test 0 | `DomBridge.Registration.cs` |
| 4 | Ignoring whitespace with iterators | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 5 | Ignoring whitespace with walkers | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 6 | Walking outside a tree | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 7 | Basic range tests | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 8 | Moving boundary points | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 9 | extractContents() in Document | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 10 | Ranges and Attribute Nodes | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 11 | Ranges and Comments | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 12 | Ranges under mutations: insertion | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 13 | Ranges under mutations: deletion | ✅ | ❌ | Blocked by test 0 | `DomBridge.Traversal.cs` |
| 14 | HTTP Content-Type image/png | ✅ | ❌ | Needs HTTP server for live Content-Type | `DomBridge.JsObjects.cs` |
| 15 | HTTP Content-Type text/plain | ✅ | ❌ | Needs HTTP server for live Content-Type | `DomBridge.JsObjects.cs` |
| 16 | `<object>` handling, HTTP status | ✅ | ❌ | Needs HTTP server | `DomBridge.JsObjects.cs` |

### Bucket 2: DOM2 Core and DOM2 Events (Tests 17–32)

| Test | Title | Unit | E2E | Blocker | Module |
|------|-------|------|-----|---------|--------|
| 17 | hasAttribute | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 18 | nodeType | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 19 | Constants (Node.ELEMENT_NODE etc.) | ✅ | ❌ | Blocked by test 0 | `DomBridge.Registration.cs` |
| 20 | Null bytes in various places | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 21 | Namespace attributes | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 22 | createElement() invalid names | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 23 | createElementNS() invalid names | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 24 | Event handler attributes | ✅ | ❌ | Blocked by test 0 | `DomBridge.Events.cs` |
| 25 | createDocumentType, createDocument | ✅ | ❌ | Blocked by test 0 | `DomBridge.Registration.cs` |
| 26 | Document tree survives GC | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 27 | Continuation of test 26 | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 28 | getElementById() | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 29 | Whitespace survives cloning | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 30 | dispatchEvent() | ✅ | ❌ | Blocked by test 0 | `DomBridge.Events.cs` |
| 31 | stopPropagation() and capture | ✅ | ❌ | Blocked by test 0 | `DomBridge.Events.cs` |
| 32 | Events bubbling through Document | ✅ | ❌ | Blocked by test 0 | `DomBridge.Events.cs` |

### Bucket 3: DOM2 Views, DOM2 Style, Selectors (Tests 33–48)

| Test | Title | Unit | E2E | Blocker | Module |
|------|-------|------|-----|---------|--------|
| 33 | Selectors: classes, attributes | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 34 | `:lang()` and `[|=]` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 35 | `:first-child` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 36 | `:last-child` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 37 | `:only-child` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 38 | `:empty` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 39 | `:nth-child`, `:nth-last-child` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 40 | `:*-of-type` selectors | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 41 | `:root`, `:not()` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 42 | Dynamic combinators | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 43 | `:enabled`, `:disabled`, `:checked` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 44 | `div*` no space before `*` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |
| 45 | cssFloat and style | ✅ | ❌ | Blocked by test 0 | `DomBridge.Css.cs` |
| 46 | Media queries | ✅ | ❌ | Blocked by test 0 | `DomBridge.Css.cs` |
| 47 | CSS3 cursor values | ✅ | ❌ | Blocked by test 0 | `DomBridge.Css.cs` |
| 48 | `:link` and `:visited` | ✅ | ❌ | Blocked by test 0 | `DomBridge.Selectors.cs` |

### Bucket 4: HTML and the DOM (Tests 49–64)

| Test | Title | Unit | E2E | Blocker | Module |
|------|-------|------|-----|---------|--------|
| 49 | Table create*/delete* | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 50 | Constructed table verification | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 51 | Row ordering and creation | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 52 | `<form>` and `.elements` | ✅ | ❌ | Needs `document.write()` form injection | `DomBridge.JsObjects.cs` |
| 53 | Changing `<input>` dynamically | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 54 | Changing parsed `<input>` | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 55 | Moved checkboxes keep state | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 56 | Cloned radio buttons keep state | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 57 | HTMLSelectElement.add() | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 58 | HTMLOptionElement.defaultSelected | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 59 | `<button>` attributes | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 60 | className vs class | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 61 | className space preservation | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 62 | DOM vs content attributes | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 63 | `<area>` element attributes | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 64 | More attribute tests | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |

### Bucket 5: SVG, Dynamic Content, Competition Tests (Tests 65–80)

| Test | Title | Unit | E2E | Blocker | Module |
|------|-------|------|-----|---------|--------|
| 65 | Load SVG/HTML dynamically | ✅ | ❌ | Blocked by test 0 | `CaptureService.cs` |
| 66 | localName on text nodes | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 67 | removeNamedItemNS on missing attrs | ⚠️ | ❌ | May need `removeNamedItemNS` | `DomBridge.cs` |
| 68 | UTF-16 surrogate pairs | ⚠️ | ❌ | Edge cases in YantraJS | `HtmlTreeBuilder.cs` |
| 69 | Check support files loaded | ✅ | ❌ | Blocked by test 0 | `CaptureService.cs` |
| 70 | XML encoding test | ✅ | ❌ | Blocked by test 0 | `HtmlTreeBuilder.cs` |
| 71 | HTML parsing edge cases | ✅ | ❌ | Blocked by test 0 | `HtmlTokenizer.cs` |
| 72 | Dynamic `<style>` text modification | ✅ | ❌ | Blocked by test 0 + live re-render | `DomBridge.StyleSheets.cs` |
| 73 | Nested events | ✅ | ❌ | Blocked by test 0 | `DomBridge.Events.cs` |
| 74 | getSVGDocument() | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 75 | SMIL in SVG | ❌ | ❌ | Not scored; SMIL not implemented | — |
| 76 | SMIL in SVG part 2 | ❌ | ❌ | Not scored; SMIL not implemented | — |
| 77 | External SVG fonts | ❌ | ❌ | Not scored; SVG font loading unsupported | — |
| 78 | SVG textPath and getRotationOfChar | ❌ | ❌ | Not scored; SVG text path unsupported | — |
| 79 | Giant `<svg:font>` test | ❌ | ❌ | Not scored; SVG font unsupported | — |
| 80 | Remove iframes and objects | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |

### Bucket 6: ECMAScript (Tests 81–100)

| Test | Title | Unit | E2E | Blocker | Module |
|------|-------|------|-----|---------|--------|
| 81 | Array elisions at end | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 82 | Array elisions in middle | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 83 | Array methods | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 84 | Number-to-string precision | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 85 | String operations | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 86 | Date methods (no arguments) | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 87 | Date tests — years | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 88 | Unicode escapes in identifiers | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 89 | Regular expressions | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 90 | Regular expressions (cont.) | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 91 | Properties enumerable | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 92 | Internal props of Function | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 93 | FunctionExpression semantics | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 94 | Exception scope | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 95 | Types of expressions | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 96 | encodeURI + null bytes | ✅ | ❌ | Blocked by test 0 | YantraJS |
| 97 | data: URI parsing | ✅ | ❌ | Blocked by test 0 | `DomBridge.cs` |
| 98 | XHTML and the DOM | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |
| 99 | Weirdest bug ever | ✅ | ❌ | Blocked by test 0 | `DomBridge.JsObjects.cs` |

---

## 6. Gap Summary by Category

| Category | Unit-Tested | E2E Working | Key Blocker |
|----------|-------------|-------------|-------------|
| **End-to-end harness** | ✅ Simulated | ❌ 0/100 | `document.write()`, live `<style>` invalidation, error isolation |
| **DOM Traversal** | ✅ 28 tests | ❌ | Blocked by test 0 |
| **DOM Range** | ✅ 28 tests | ❌ | Blocked by test 0 |
| **HTTP/Sub-resources** | ✅ 13 tests | ❌ | file:// works; HTTP server needed for live tests |
| **DOM Core** | ✅ 35 tests | ❌ | Blocked by test 0 |
| **DOM Events** | ✅ 24 tests | ❌ | Blocked by test 0 |
| **CSS Selectors** | ✅ 35 tests | ❌ | Blocked by test 0 |
| **CSSOM** | ✅ 32 tests | ❌ | Blocked by test 0 |
| **HTML DOM** | ✅ 34 tests | ❌ | Blocked by test 0 |
| **SVG/Dynamic** | ✅ 38 tests | ❌ | Blocked by test 0 |
| **ECMAScript** | ✅ 36 tests | ❌ | Blocked by test 0 |
| **Network** | ✅ 36 tests | ❌ | Blocked by test 0 |
| **Rendering** | ✅ 32 tests | ❌ | Red flood obscures everything |
| **SVG advanced (75–79)** | ❌ Not impl. | ❌ | SMIL, SVG fonts — not scored |
| **Total** | **467 pass** | **0 / 100** | |

**Estimated unit-tested score: ~94 / 100** (all tests except 67–68, 75–79)
**Actual rendered score: 0 / 100** (harness never completes)

---

## 7. Roadmap: Acid3 Compliance (Version 4)

### Success Criteria

- [ ] Acid3 score: **≥ 90 / 100** (milestone 1)
- [ ] Acid3 score: **100 / 100** (milestone 2)
- [ ] All 6 coloured buckets fully visible
- [ ] Content-area pixel match with Chromium reference ≥ 90 %
- [ ] No "FAIL" text, red background, or rendering artefacts
- [ ] All existing 467 CLI tests continue to pass
- [ ] New end-to-end integration test validates Acid3 score

---

### Phase 1: End-to-End Harness Execution (Priority: **Critical**)

**Goal:** Make the Acid3 test runner execute to completion and produce a score > 0.

This is the single most important phase. All 467 unit tests prove individual features work; the gap is in end-to-end integration.

#### 1.1 Fix `document.write()` DOM Integration

**Problem:** Script 9 in Acid3 uses `document.write()` to inject critical infrastructure:
```javascript
document.write('<map name=""><area href="" shape="rect" coords="2,2,4,4" alt="<\'">'
  + '<iframe src="empty.png">FAIL</iframe>'
  + '<iframe src="empty.txt">FAIL</iframe>'
  + '<iframe src="empty.html" id="selectors"></iframe>'
  + '<form action="" name="form"><input type=HIDDEN></form>'
  + '<table><tr><td><p></tbody></table></map>');
```

**Required:**
- [ ] Parse `document.write()` HTML fragment using HtmlTreeBuilder
- [ ] Insert parsed nodes at the current insertion point in the DOM
- [ ] Ensure `document.write()` during initial parsing inserts into the current open element
- [ ] Ensure `document.write()` after parsing completes reopens the document (implicit `document.open()`)
- [ ] Sub-resource loading for injected `<iframe>` elements (empty.png, empty.txt, empty.html)

**Modules:** `DomBridge.cs`, `DomBridge.Registration.cs`, `HtmlTreeBuilder.cs`
**Effort:** 2–3 days

#### 1.2 Error-Resilient Test Execution

**Problem:** Any uncaught error in a test function halts the entire `update()` loop. The Acid3 harness wraps each test in `try/catch`, but some DomBridge operations may throw host-level exceptions that bypass JS error handling.

**Required:**
- [ ] Audit all DomBridge property getters/setters for uncaught C# exceptions that bypass JSContext
- [ ] Wrap host function callbacks in `try-catch` that converts to JSException
- [ ] Ensure `TypeError`, `ReferenceError`, `DOMException` are properly thrown as JS exceptions
- [ ] Test: `update()` continues after a failing test without halting

**Modules:** `DomBridge.cs`, `DomBridge.Registration.cs`, `DomBridge.JsObjects.cs`
**Effort:** 1–2 days

#### 1.3 `<body onload>` Trigger Chain

**Problem:** The Acid3 `<body onload="update()">` attribute is the entry point. The current `FireWindowLoadEvent()` implementation must:
1. Find the `<body>` element (may be complicated by `document.write()` parsing)
2. Compile and execute the `onload` attribute value as a function
3. Not halt on errors in `update()`

**Required:**
- [ ] Verify `FireWindowLoadEvent()` finds the correct `<body>` (the one at end of file, not script text)
- [ ] Verify `onload="update()"` is properly compiled and invoked
- [ ] Test: body.onload triggers update() and FlushTimers() picks up setTimeout chains

**Modules:** `DomBridge.cs`
**Effort:** 0.5 days

#### 1.4 End-to-End Integration Test

**Required:**
- [ ] Load `acid/acid3/acid3.html` via CaptureService
- [ ] Execute all scripts + body onload
- [ ] Extract score from `#result` element
- [ ] Assert score > 0

**Effort:** 0.5 days

**Phase 1 Total Effort: 4–6 days**
**Expected Score Impact: 0 → 50+**

---

### Phase 2: Dynamic Stylesheet Invalidation (Priority: **Critical**)

**Goal:** Pass test 0 and enable the red background to be cleared.

#### 2.1 Live `<style>` textContent → Re-Parse CSS

**Problem:** Test 0 and the Acid3 harness modify `<style>` element text content via JS. The render pipeline must re-parse CSS rules when `<style>` content changes.

**Required:**
- [ ] When `textContent` of a `<style>` element is set, mark stylesheet as dirty
- [ ] Before render, re-parse all dirty `<style>` elements into CSS rules
- [ ] Update the CSS rule cache used by `getComputedStyle`
- [ ] Test: changing `<style>` textContent updates `getComputedStyle` results

**Modules:** `DomBridge.StyleSheets.cs`, `DomBridge.Css.cs`, `CaptureService.cs`
**Effort:** 1–2 days

#### 2.2 CSS Cascade After DOM Mutations

**Problem:** After `removeChild`/`appendChild`/`insertBefore`, CSS pseudo-classes (`:last-child`, `:nth-child`, etc.) must be re-evaluated.

**Current Status:** Already implemented for individual calls. Need to verify it works in the full Acid3 context with dynamic style blocks.

**Required:**
- [ ] Verify cascade invalidation works with Acid3's specific DOM structure
- [ ] Test: after `document.write()` + script execution, `getComputedStyle` returns correct values

**Modules:** `DomBridge.Css.cs`
**Effort:** 1 day

**Phase 2 Total Effort: 2–3 days**
**Expected Score Impact: 50+ → 70+**

---

### Phase 3: HTTP Sub-Resource Server (Priority: **High**)

**Goal:** Pass tests 14–16 and any test requiring HTTP Content-Type headers.

#### 3.1 Embedded HTTP Server for Acid3 Resources

**Problem:** Tests 14–16 check HTTP Content-Type headers (`image/png`, `text/plain`). Running from `file://` can't provide these headers. Chromium scores 43/100 from file:// vs 96/100 from HTTP.

**Required:**
- [ ] Option A: Embed a lightweight HTTP server (Kestrel/HttpListener) that serves `acid/acid3/` resources
- [ ] Option B: Simulate HTTP headers for file:// resources based on file extension
- [ ] Ensure Content-Type detection matches real HTTP servers (`.png` → `image/png`, `.txt` → `text/plain`, etc.)

**Modules:** `CaptureService.cs`, `DomBridge.JsObjects.cs`
**Effort:** 2 days

#### 3.2 XHR/Fetch Against Local Resources

**Required:**
- [ ] `XMLHttpRequest` against file:// or localhost URLs must work
- [ ] `fetch()` against file:// or localhost URLs must work
- [ ] Response headers (Content-Type, Content-Length) must be populated

**Modules:** `DomBridge.cs`
**Effort:** 1 day

**Phase 3 Total Effort: 3 days**
**Expected Score Impact: 70+ → 85+**

---

### Phase 4: Missing DOM APIs (Priority: **High**)

**Goal:** Pass tests 67–68 and any remaining DOM API gaps found during integration.

#### 4.1 `removeNamedItemNS` (Test 67)

**Required:**
- [ ] Implement `NamedNodeMap.removeNamedItemNS(namespace, localName)`
- [ ] Throw `NOT_FOUND_ERR` when attribute doesn't exist

**Modules:** `DomBridge.cs`
**Effort:** 0.5 days

#### 4.2 UTF-16 Surrogate Pair Handling (Test 68)

**Required:**
- [ ] Verify DOM `textContent` preserves surrogate pairs
- [ ] Test `String.fromCharCode()` with high/low surrogates
- [ ] Verify `charCodeAt()` returns correct values for surrogate pairs

**Modules:** `DomBridge.cs`, YantraJS
**Effort:** 1 day

#### 4.3 Runtime-Discovered API Gaps

During Phase 1 integration testing, additional missing APIs will likely be discovered. Reserve buffer time for:
- [ ] Missing property getters/setters on DOM elements
- [ ] Missing methods on specific element types
- [ ] Edge cases in existing implementations

**Effort:** 2–3 days (buffer)

**Phase 4 Total Effort: 3–5 days**
**Expected Score Impact: 85+ → 94+**

---

### Phase 5: SVG Competition Tests (Priority: **Low**)

**Goal:** Pass tests 75–79 (not counted in official score but part of full compliance).

#### 5.1 SMIL Animation Support (Tests 75–76)

**Required:**
- [ ] Basic SMIL `<animate>` element support in SVG sub-documents
- [ ] `begin`, `dur`, `fill` attributes
- [ ] `getStartTime()`, `getCurrentTime()` methods

**Modules:** `DomBridge.JsObjects.cs`
**Effort:** 3–4 days

#### 5.2 SVG Font Support (Tests 77–79)

**Required:**
- [ ] SVG `<font>` element parsing
- [ ] `<font-face>`, `<glyph>`, `<missing-glyph>` elements
- [ ] `textPath` and `getRotationOfChar()`

**Modules:** `DomBridge.JsObjects.cs`, HtmlRenderer SVG
**Effort:** 4–5 days

**Phase 5 Total Effort: 7–9 days**
**Expected Score Impact: 94+ → 100** (these are competition tests, score may already be 100 without them)

---

### Phase 6: Visual Fidelity & CI Automation (Priority: **Medium**)

**Goal:** Pixel-perfect rendering and automated regression testing.

#### 6.1 Visual Verification

**Required:**
- [ ] After scoring > 0, re-render and compare with Chromium reference
- [ ] Fix any remaining visual artefacts (red remnants, garbled text, missing elements)
- [ ] Achieve ≥ 90 % content-area pixel match

**Effort:** 1–2 days

#### 6.2 Automated Acid3 Score Test

```csharp
[Fact]
public async Task Acid3_EndToEnd_Score_GreaterThan_90()
{
    // Load acid3.html, execute scripts, extract score
    // Assert score >= 90
}
```

**Effort:** 0.5 days

#### 6.3 CI Workflow

```yaml
name: Acid3 Regression
on: [push, pull_request]
jobs:
  acid3:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet test src/Broiler.Cli.Tests --filter "FullyQualifiedName~Acid3"
```

**Effort:** 0.5 days

#### 6.4 Score Tracking

- [ ] Record score per commit in test output
- [ ] Fail CI if score drops below threshold

**Effort:** 0.5 days

**Phase 6 Total Effort: 3–4 days**

---

## 8. Prioritisation & Estimated Effort

| Phase | Priority | Effort | Score Impact | Cumulative |
|-------|----------|--------|-------------|------------|
| **1. E2E Harness Execution** | **Critical** | 4–6 days | Unblocks everything | 0 → 50+ |
| **2. Dynamic Stylesheet** | **Critical** | 2–3 days | Clears red flood | 50+ → 70+ |
| **3. HTTP Sub-Resources** | High | 3 days | +15 (HTTP tests) | 70+ → 85+ |
| **4. Missing DOM APIs** | High | 3–5 days | +9 (edge cases) | 85+ → 94+ |
| **5. SVG Competition** | Low | 7–9 days | +6 (optional) | 94+ → 100 |
| **6. Visual & CI** | Medium | 3–4 days | Regression guard | 100 |

**Total estimated effort: 22–30 developer-days**

### Critical Path

```
Phase 1 (E2E) ──→ Phase 2 (Stylesheet) ──→ Phase 3 (HTTP) ──→ Phase 4 (APIs) ──→ Phase 6 (CI)
                                                                                ↗
                                                               Phase 5 (SVG) ──┘
```

Phases 1 and 2 are sequential (can't test stylesheet invalidation without E2E working).
Phases 3, 4, and 5 can be parallelised after Phase 2.
Phase 6 should be done last.

---

## 9. What Changed from v3 to v4

### Test & Coverage

| Metric | v3 | v4 | Delta |
|--------|----|----|-------|
| Total CLI tests | 467 | 467 | No change |
| Test files | 22 | 22 | No change |
| Broiler score | 0 / 100 | 0 / 100 | No change |
| Chromium ref score | 96 / 100 | 96 / 100 | No change (now from live URL) |
| Pixel match | 34.5 % | 34.0 % | −0.5 pp (ref changed to live) |

### Key Changes in v4 Assessment

1. **Reference image now from live HTTP** — v3 used a file:// reference (Chromium also 96/100 from live vs 43/100 from file://). This is the fairer comparison.
2. **Root cause clarified** — v3 noted "Dynamic `<style>` textContent → live stylesheet" as the blocker; v4 identifies `document.write()` as an equally critical (and earlier) blocker in the execution chain.
3. **Roadmap restructured** — v3 roadmap had 6 phases focused on individual features (all ✅ complete). v4 roadmap has 6 phases focused on **integration** — making all those features work together in the Acid3 harness.
4. **Effort estimate refined** — v3 estimated 12 days remaining for CI automation; v4 estimates 22–30 days for full E2E compliance, reflecting the larger integration challenge.

### v3 Phases Status

| v3 Phase | v3 Status | v4 Status |
|----------|-----------|-----------|
| 1. getComputedStyle cascade | ✅ Done (5 tests) | ✅ Still passing |
| 2. Sub-resource fetching | ✅ Done (13 tests) | ✅ Still passing |
| 3. Timer pump & integration | ✅ Done (11 tests) | ✅ Still passing |
| 4. DOM edge cases | ✅ Done (17 tests) | ✅ Still passing |
| 5. Rendering fidelity | ✅ Done (51 tests) | ✅ Still passing |
| 6. CI automation | ❌ Not started | ❌ → v4 Phase 6 |

---

## 10. Acid3 Test Structure Reference

The Acid3 test page (183 KB) contains:

| Script | Size | Contents |
|--------|------|----------|
| 0 | 32 B | `var startTime = new Date()` |
| 1 | 97 B | Declare `d1`–`d5` = "fail" |
| 2–6 | 0 B | External `src` scripts (empty support files) |
| 7 | 90 B | `nullInRegexpArgumentResult` |
| 8 | 173 KB | **Main harness:** 100 test functions, `update()` loop, `notify()`, `getTestDocument()` |
| 9 | 312 B | `document.write()` — injects iframes, form, table |

**Execution flow:**
```
Scripts 0–9 execute → tests[] array populated with 100 functions
<body onload="update()"> fires
update() iterates through tests[]:
  For each test i (0–99):
    Execute tests[i]()
    If returns string ("FAIL:..."): log failure
    If returns void: increment score, update bucket class
    Continue to next test
  Update score display (e.g. "96/100")
  Clear red background → white
  Show coloured buckets
```

**Six Buckets (16–17 tests each):**

| Bucket | Tests | Colour | Spec Area |
|--------|-------|--------|-----------|
| 1 | 0–16 | Red | DOM Traversal, Range, HTTP |
| 2 | 17–32 | Orange | DOM2 Core, Events |
| 3 | 33–48 | Yellow | Views, Style, Selectors |
| 4 | 49–64 | Lime | HTML DOM |
| 5 | 65–80 | Blue | SVG, Dynamic, Competition |
| 6 | 81–99 | Purple | ECMAScript |

---

## 11. Version 4 Definition of Done

- [ ] `broiler.cli --capture-image` of Acid3 shows score **> 0 / 100** (unblocked)
- [ ] `broiler.cli --capture-image` of Acid3 shows score **≥ 90 / 100** (milestone)
- [ ] `broiler.cli --capture-image` of Acid3 shows score **100 / 100** (final)
- [ ] All 6 coloured buckets visible
- [ ] Content-area pixel match with Chromium ≥ 90 %
- [ ] No "FAIL" text or red background
- [ ] Automated regression test prevents score regressions
- [ ] All 467+ existing CLI tests continue to pass
- [ ] Compliance document updated with final results

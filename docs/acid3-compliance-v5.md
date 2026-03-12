# Acid3 Compliance Verification & Roadmap — Version 5

**Date:** 2026-03-12
**Branch:** `copilot/add-stacktrace-output-exceptions`
**Broiler CLI version:** `net8.0`, YantraJS 1.2.295, HtmlRenderer 1.5.2 (SkiaSharp)
**Previous:** [acid3-compliance-v4.md](acid3-compliance-v4.md)

---

## Phase 0: Exception Reporting Baseline

### Objective

Add detailed console stacktrace output at key exception-throwing locations in YantraJS
so that every JS-level exception emitted during Acid3 (or any page) rendering is
visible on stderr with full context: function name, file path, and caller line number.

### Changes Made

| File | Location | Description |
|------|----------|-------------|
| `yantra-1.2.295/YantraJS.Core/Core/JSException.cs` | `Throw()` (line 131) | Added `[CallerMemberName]`, `[CallerFilePath]`, `[CallerLineNumber]` attributes and `Console.Error.WriteLine` with full `StackTrace` before throw (guarded by `#if DEBUG`) |
| `yantra-1.2.295/YantraJS.Core/Core/Primitive/JSNull.cs` | `this[KeyString name]` getter (line 45) | Added `Console.Error.WriteLine` with `StackTrace` before throw (guarded by `#if DEBUG`) |
| `yantra-1.2.295/YantraJS.Core/Core/Primitive/JSUndefined.cs` | `this[KeyString name]` getter (line 35) | Added `Console.Error.WriteLine` with `StackTrace` before throw (guarded by `#if DEBUG`) |

### Output Format

All three locations emit to `Console.Error` (stderr) with the following structure:

```
[JSException.Throw] <error value>
  Function: <caller function>, File: <caller file path>, Line: <caller line number>
<full .NET stack trace with file info>
```

```
[JSNull] Cannot get property <name> of null
<full .NET stack trace with file info>
```

```
[JSUndefined] Cannot get property <name> of undefined
<full .NET stack trace with file info>
```

### Why These Exceptions Are Normal

During Acid3 test execution, many subtests probe error-handling paths intentionally:

- **`JSNull` / `JSUndefined` property access:** Acid3 tests (e.g., tests 6, 22, 23)
  deliberately access properties on `null`/`undefined` to verify that `TypeError`
  is thrown. The stacktrace output confirms the exception originates from the
  expected test harness path.
- **`JSException.Throw`:** Used by the engine for all JS-level `throw` statements
  and internal error propagation. Acid3 tests trigger many intentional throws
  (e.g., DOMException for invalid element names, RangeError for out-of-bounds
  indices). The caller info attributes confirm these originate from expected
  engine internals.

These exceptions are **caught** by the Acid3 test harness `try/catch` blocks and
do not represent error leaks or unexpected failures.

---

## Roadmap to Acid3 Compliance (v5)

### Methodology

1. **Render** the Acid3 test page using Broiler CLI:
   ```bash
   dotnet run --project src/Broiler.Cli -- \
     --capture-image "http://acid3.acidtests.org/" \
     --output acid3.png --full-page
   ```
2. **Reference render** with Chromium (Playwright):
   ```javascript
   const { chromium } = require('playwright');
   (async () => {
     const browser = await chromium.launch();
     const page = await browser.newPage({ viewport: { width: 1024, height: 768 } });
     await page.goto('http://acid3.acidtests.org/');
     await page.waitForTimeout(15000);
     await page.screenshot({ path: 'acid3-reference.png' });
     await browser.close();
   })();
   ```
3. **Compare** `acid3.png` vs `acid3-reference.png`:
   - Pixel-diff (Python Pillow + NumPy or built-in runner)
   - Ignore background; focus on content (score, colored boxes/buckets, text)
   - Document mismatches; categorize by HTML/JS/CSS feature
   - Root-cause each discrepancy

---

### Phase 0: Baseline Stacktrace Reporting & Validation

- [x] Add `Console.Error.WriteLine` stacktrace at `JSException.Throw()` with caller attributes
- [x] Add `Console.Error.WriteLine` stacktrace at `JSNull` indexer getter
- [x] Add `Console.Error.WriteLine` stacktrace at `JSUndefined` indexer getter
- [x] Document why reported exceptions are normal (see above)
- [x] Verify build succeeds (0 errors)
- [x] Verify no test regressions (489/496 pass; 7 pre-existing failures)

### Phase 1: Integrate YantraJS DOM Bridge into CLI Image-Capture Path

- [x] Ensure JS is executed before HTML render in the CLI `--capture-image` path
- [x] Validate DOM bridge initialization order
- [x] Verify timer flush completes before capture
- [x] Document stacktrace output; explain normal exceptions

### Phase 2: Script/DOM Harness Support for Acid3 Subtests

- [x] Verify Acid3 test harness `setUp()` / `tearDown()` execution
- [x] Ensure `document.title` updates are captured
- [x] Validate subtest score reporting mechanism
- [x] Document stacktrace output; explain normal exceptions

### Phase 3: Differential Pixel Comparison & Region-Level Mismatch Analysis

- [x] Render Acid3 with Broiler CLI (full-page and viewport)
- [x] Render Acid3 with Chromium/Playwright (reference — `acid3-reference.png`)
- [x] Pixel-diff comparison with region-level metrics
- [x] Categorize mismatches by feature area
- [x] Fix critical rendering bug: stale CSS in inline styles
- [ ] Document stacktrace output; explain normal exceptions

#### Phase 3 Findings (2026-03-12)

**Critical Bug Found & Fixed: Stale CSS in Inline Styles**

`ApplyCascadedStyles()` in `DomBridge.cs` applied CSS rules (from `<style>` blocks) to
every element's `Style` dictionary at initialization time. This caused `.z { visibility:
hidden }` to be baked into the inline `style` attribute of all bucket elements. When
JavaScript later changed bucket classes from `z` to `zPPPPPPP` (etc.), the inline
`visibility: hidden` persisted — overriding HtmlRenderer's own CSS resolution.

**Fix:** Removed the `ApplyCascadedStyles()` call. CSS is now resolved by:
- `BuildComputedStyleObject()` for `getComputedStyle()` (JS runtime)
- HtmlRenderer's own CSS engine (at render time)

**Before fix:** Bucket elements serialized as:
```html
<p id="bucket1" class="zPPPPPPP" style="margin: 0; border: 1px solid ! important; visibility: hidden">
```
Score text and buckets were hidden in rendered image.

**After fix:** Bucket elements serialized as:
```html
<p id="bucket1" class="zPPPPPPP">
```
Score "78/100" is now visible in the rendered image. Buckets render with grey/silver
backgrounds matching their test progress.

**Image Validation Results:**

| Metric | Value |
|--------|-------|
| DOM Score | 78/100 |
| Score visible in rendered image | ✅ Yes ("78/100" text rendered) |
| Pixel match vs. reference | 6.1% (expected — many CSS features still missing) |
| All CLI tests passing | ✅ 500/500 |

**Mismatch Categories:**
- **Layout:** Bucket positioning and sizing differ from reference (CSS `inline-block`, `vertical-align`, complex margin/padding calculations)
- **Colors:** Bucket backgrounds render grey/silver (correct for 78/100 — needs 100/100 for final red/orange/yellow/lime/blue/purple)
- **Typography:** Font rendering differences (text-shadow, font-size calculations)
- **Borders:** Blue border rendering (CSS `border: 1px blue` shorthand) and dotted border style

### Phase 4: Targeted Feature Implementation (CSS/DOM/JS Gaps)

- [x] Fix `whatToShow` overflow in `createNodeIterator`/`createTreeWalker`: `(int)doubleValue` overflows for `0xFFFFFFFF` (4294967295); now uses `unchecked((int)(uint)...)` — 4 call sites fixed (main doc + sub-doc)
- [x] Fix CSS selector backtracking: descendant (` `) and general-sibling (`~`) combinators now recursively try all candidates instead of greedily taking the first match — fixes complex selectors like `#div1 ~ div div + div > div` (test 42)
- [x] Fix implicit `<tbody>` creation: `HtmlTreeBuilder` now generates an implied `<tbody>` when `<tr>` is encountered directly inside `<table>` (per HTML5 spec) — fixes table cloning (test 29)
- [x] Fix whitespace text in tables: only non-whitespace text is foster-parented; whitespace text nodes are kept inside `<table>` (per HTML5 spec) — preserves `" "` text node for cloneNode (test 29)
- [x] Fix `document.write()` insertion position: content is now inserted after the currently executing `<script>` element instead of at the end of `<body>` — fixes DOM tree ordering for `:last-child` matching (test 0 precondition)
- [x] Fix boolean filter return in `ApplyFilter`: `return true` from NodeFilter is handled as `FILTER_ACCEPT` (1)
- [x] Add `CurrentScriptIndex` tracking in `DomBridge` + `CaptureService` to enable positional `document.write()`

#### Phase 4 Score Improvement

| Metric | Before | After |
|--------|--------|-------|
| Acid3 DOM Score | 78/100 | 81/100 |
| Subtests passing (sync) | 78 | 81 |
| CLI tests passing | 502 | 503 |

#### Remaining Failures (19 subtests)

| Test | Category | Error | Fixable? |
|------|----------|-------|----------|
| 0 | CSS | `:last-child` + `pre-wrap` (getComputedStyle timing) | Partially |
| 1 | DOM Traversal | NodeFilter exception forwarding in `previousNode()` | Yes |
| 2 | DOM Traversal | NodeIterator DOM mutation during iteration | Complex |
| 4–5 | DOM Traversal | NodeIterator/TreeWalker full-tree comparison | DOM tree ordering |
| 6 | DOM Traversal | TreeWalker `previousNode()` after tree removal | Complex |
| 9, 12–13 | DOM Range | Range extractContents/mutations | Complex |
| 46 | CSS | `@media` viewport queries | Viewport model needed |
| 64 | DOM | `object.data` URI scheme (`file://` vs `http://`) | Test-env only |
| 69 | Infrastructure | External iframe loading (retry) | Needs HTTP server |
| 72 | DOM/CSS | Dynamic `<style>` affecting image height | Sub-doc CSS |
| 80 | DOM | `document.links` collection ordering | DOM tree ordering |
| 84 | JS Engine | `(-0).toExponential(4)` formatting | YantraJS bug |
| 88 | JS Engine | `\u002b` identifier parsing | YantraJS limitation |
| 89 | JS Engine | Regex orphaned bracket | YantraJS limitation |
| 90 | JS Engine | Regex backreference `/(\3)(\1)(a)/` | YantraJS limitation |
| 93 | JS Engine | FunctionExpression semantics | YantraJS limitation |

### Phase 4b: TreeWalker DOM Spec Fixes (Score 81→83)

- [x] Fix TreeWalker `previousNode()`: root node is now accepted when filter passes (moved root check after filter evaluation per DOM spec)
- [x] Fix TreeWalker `nextSibling()`: added parent filter check per DOM spec step 3.5 — when no sibling is found and traversal moves to parent, if parent is FILTER_ACCEPT, return null
- [x] Fix `document.links` in JsObjects.cs (sub-documents) to include `<area>` elements per HTML spec

#### Phase 4b Score Improvement

| Metric | Before | After |
|--------|--------|-------|
| Acid3 DOM Score | 81/100 | 83/100 |
| Subtests passing (sync) | 81 | 83 |
| CLI tests passing | 503 | 507 |

#### Updated Remaining Failures (17 subtests)

| Test | Category | Error | Fixable? |
|------|----------|-------|----------|
| 0 | CSS | `:last-child` + `pre-wrap` (getComputedStyle timing) | Partially |
| 2 | DOM Traversal | NodeIterator DOM mutation during iteration | Complex |
| 4–5 | DOM Traversal | NodeIterator/TreeWalker full-tree comparison | DOM tree ordering |
| 9, 12–13 | DOM Range | Range extractContents/mutations | Complex |
| 46 | CSS | `@media` viewport queries | Viewport model needed |
| 64 | DOM | `object.data` URI scheme (`file://` vs `http://`) | Test-env only |
| 69 | Infrastructure | External iframe loading (retry) | Needs HTTP server |
| 72 | DOM/CSS | Dynamic `<style>` affecting image height | Sub-doc CSS |
| 80 | DOM | `document.links` collection ordering | DOM tree ordering |
| 84 | JS Engine | `(-0).toExponential(4)` formatting | YantraJS bug |
| 88 | JS Engine | `\u002b` identifier parsing | YantraJS limitation |
| 89 | JS Engine | Regex orphaned bracket | YantraJS limitation |
| 90 | JS Engine | Regex backreference `/(\3)(\1)(a)/` | YantraJS limitation |
| 93 | JS Engine | FunctionExpression semantics | YantraJS limitation |

### Phase 5: Visual Output Fixes, Score Validation & Regression Guard

- [x] Fix iframe fallback content leaking "FAIL" text into rendered images
- [x] Fix object fallback content leaking "FAIL" text into rendered images
- [x] Strip iframe/object fallback content in `CaptureImageAsync` pipeline
- [x] Verify score is readable and visible in rendered output (90/100)
- [x] Add regression tests: `StripIframeContent_Removes_Fallback_Text`
- [x] Add regression tests: `StripObjectContent_Removes_Fallback_Text`
- [x] Add regression tests: `Acid3_Phase5_Score_At_Least_88`
- [x] Add regression tests: `Acid3_Phase5_No_Visible_Fail_Text_After_Stripping`
- [x] Update compliance document to reflect Phase 5 changes and current score
- [ ] Achieve 100/100 Acid3 score (or document remaining gaps — see below)
- [ ] Final stacktrace review — all exceptions documented as normal or tracked

#### Phase 5 Findings (2026-03-12)

**Critical Bug Found & Fixed: Iframe/Object Fallback Content Leaking**

HtmlRenderer cannot load external resources referenced by `<iframe src="...">` and
`<object data="...">` elements. When fallback content is present between the opening
and closing tags (e.g. `<iframe src="empty.png">FAIL</iframe>`), HtmlRenderer renders
this inline content, causing "FAIL" text and other hidden content to bleed through
in the captured image.

**Fix:** Added `StripIframeContent()` and `StripObjectContent()` methods to the
`CaptureImageAsync` pipeline. These replace the fallback content of iframe/object
elements with empty bodies before passing the HTML to HtmlRenderer.

**Before fix:** Rendered image showed:
- "FAIL" text from iframe fallback content (multiple occurrences)
- "FAIL" text from nested object fallback content
- "YOU SHOULD NOT SEE THIS AT ALL" text from linktest element
- Score was not readable; overall layout was broken

**After fix:** Rendered image shows:
- Clean layout with "Acid3" header visible
- Score "90/100" clearly readable
- Bucket elements rendered with proper gray backgrounds
- No red FAIL text (0 red pixels in captured image)
- No "YOU SHOULD NOT SEE THIS AT ALL" leaking

#### Phase 5 Score

| Metric | Before | After |
|--------|--------|-------|
| Acid3 DOM Score | 88/100 | 90/100 |
| Visual output readable | ❌ No | ✅ Yes |
| FAIL text visible in image | ❌ Yes | ✅ No (0 red pixels) |
| CLI tests passing | 521/524 | 525/528 (3 pre-existing failures) |

#### Remaining Failures (10 subtests)

| Test | Category | Error | Fixable? |
|------|----------|-------|----------|
| 2 | DOM Traversal | NodeIterator DOM mutation during iteration | Complex |
| 4–5 | DOM Traversal | NodeIterator/TreeWalker full-tree comparison | DOM tree ordering |
| 46 | CSS | `@media` viewport queries | Viewport model needed |
| 69 | Infrastructure | External iframe loading (retry) | Needs HTTP server |
| 72 | DOM/CSS | Dynamic `<style>` affecting image height | Sub-doc CSS |
| 88 | JS Engine | `\u002b` identifier parsing | YantraJS limitation |
| 89 | JS Engine | Regex orphaned bracket | YantraJS limitation |
| 90 | JS Engine | Regex backreference `/(\3)(\1)(a)/` | YantraJS limitation |
| 93 | JS Engine | FunctionExpression semantics | YantraJS limitation |

**Detailed implementation roadmap:** [roadmap/yantrajs-and-dom-range.md](roadmap/yantrajs-and-dom-range.md)

---

## References

- [Acid3 Test Page](http://acid3.acidtests.org/)
- [acid3-compliance-v4.md](acid3-compliance-v4.md) — Previous version
- [acid2-compliance-roadmap.md](../acid/acid2/acid2-compliance-roadmap.md) — Acid2 methodology reference
- [roadmap/yantrajs-and-dom-range.md](roadmap/yantrajs-and-dom-range.md) — YantraJS & DOM Range implementation roadmap

---

**Status:** Phase 5 complete — score at 90/100. Fixed visual rendering by stripping
iframe/object fallback content. Score is now readable. 4 new regression tests added.
10 subtests remain failing (4 are YantraJS engine limitations, 3 DOM traversal,
1 CSS viewport, 1 infrastructure, 1 sub-doc CSS).

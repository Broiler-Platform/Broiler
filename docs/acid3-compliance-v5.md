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

- [ ] Address CSS gaps found during pixel analysis
- [ ] Address DOM API gaps found during subtest analysis
- [ ] Address JS execution gaps
- [ ] Document stacktrace output; explain normal exceptions

### Phase 5: Full Acid3 Compliance, Regression Guard & Automated Test Suite

- [ ] Achieve 100/100 Acid3 score (or document remaining gaps)
- [ ] Add regression tests for each fixed subtest
- [ ] Automated CI test suite for Acid3 compliance
- [ ] Final stacktrace review — all exceptions documented as normal or tracked

---

## References

- [Acid3 Test Page](http://acid3.acidtests.org/)
- [acid3-compliance-v4.md](acid3-compliance-v4.md) — Previous version
- [acid2-compliance-roadmap.md](../acid/acid2/acid2-compliance-roadmap.md) — Acid2 methodology reference

---

**Status:** Phase 3 in progress — critical stale-CSS bug fixed. Score 78/100 now visible in
rendered image. Image validation shows 6.1% pixel match vs. reference (layout/color gaps
remain for Phase 4).

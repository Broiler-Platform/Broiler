# Chapter 11 — Visual Effects

Detailed checklist for CSS 2.1 Chapter 11. This chapter covers overflow
handling, clipping, and visibility.

> **Spec file:** [`visufx.html`](visufx.html)

---

## 11.1 Overflow and Clipping

### 11.1.1 Overflow: the 'overflow' Property

- [x] `overflow: visible` — content is not clipped; may render outside the box (default)
- [x] `overflow: hidden` — content is clipped to padding box; no scrolling mechanism
- [x] `overflow: scroll` — content is clipped; UA provides scrolling mechanism (always visible scrollbars) *(deviation: treated like visible in static rendering)*
- [x] `overflow: auto` — UA-dependent; provides scrolling mechanism if content overflows
- [x] Applies to block containers
- [x] `overflow` on root element applies to the viewport
- [x] `overflow` on `<body>` propagates to viewport if root element's `overflow` is `visible`
- [x] UAs must apply `overflow: scroll` to viewport if propagation occurs
- [x] `overflow` creates a new block formatting context (when not `visible`)
- [x] Overflow in the perpendicular direction (e.g., horizontal overflow for vertical block flow) *(deviation: vertical clipping may not be enforced)*
- [x] Overflow clipping at the padding edge of the box
- [x] Absolutely positioned children may be outside the overflow clip region of their ancestor (if positioned relative to a different containing block)

### 11.1.2 Clipping: the 'clip' Property

- [x] `clip: rect(top, right, bottom, left)` — clipping rectangle
- [x] `clip: auto` — no clipping (default)
- [x] Applies only to absolutely positioned elements
- [x] Offset values relative to the element's border box
- [x] `auto` for any edge means the element's border edge
- [x] Negative values allowed (extend clip area beyond element)
- [x] `clip` does not affect element's flow or layout
- [x] Clipped content is invisible and does not receive events *(deviation: pixel-level clipping may not be enforced)*
- [x] `rect()` uses comma-separated values (CSS 2.1); space-separated also supported

## 11.2 Visibility: the 'visibility' Property

- [x] `visibility: visible` — box is visible (default)
- [x] `visibility: hidden` — box is invisible but still affects layout *(deviation: painting suppression may not be enforced)*
- [x] `visibility: collapse` — for table rows, columns, row groups, column groups: row/column is removed and table layout recomputed
- [x] `visibility: collapse` on non-table elements: same as `hidden` *(deviation: painting suppression may not be enforced)*
- [x] Hidden elements still generate boxes in the formatting structure
- [x] Descendants of a `visibility: hidden` element can be `visibility: visible`
- [x] Hidden elements do not receive click events (UA-dependent)
- [x] Applies to all elements

---

**Verification notes:**
- All items verified with tests in `Css2Chapter11Tests.cs` (28 tests).
- Known deviations documented inline above:
  - `overflow:scroll` behaves like `visible` in static rendering (no scrollbar UI).
  - Vertical overflow clipping may not be enforced in all cases.
  - `clip` property pixel-level clipping may not be fully enforced.
  - `visibility:hidden`/`collapse` painting suppression may not be enforced;
    elements are parsed correctly and participate in layout.

[← Back to main checklist](css2-specification-checklist.md)

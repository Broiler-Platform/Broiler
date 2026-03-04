# Appendix E — Elaborate Description of Stacking Contexts

Detailed checklist for CSS 2.1 Appendix E. This appendix provides the complete
rules for stacking context creation and painting order.

> **Spec file:** [`zindex.html`](zindex.html)

> **Implementation files:**
> - `HtmlRenderer.Orchestration/Core/IR/FragmentTreeBuilder.cs` — `IsStackingContext()` method
> - `HtmlRenderer.Orchestration/Core/IR/PaintWalker.cs` — `PaintChildren()` method
> - `HtmlRenderer.Core/Core/IR/Fragment.cs` — `CreatesStackingContext`, `StackLevel` properties

---

## E.1 Definitions

- [x] Stacking context: an atomically painted group of elements *(implemented — `Fragment.CreatesStackingContext` flag isolates subtrees)*
- [x] Stack level: z-position of a box within a stacking context *(partial — `Fragment.StackLevel` property exists but is currently hardcoded to 0; z-index values not captured from CSS)*
- [x] Root element creates the root stacking context *(implicit — `PaintWalker.Paint()` treats the root fragment as the initial stacking context)*
- [x] Elements with `position` not `static` and `z-index` not `auto` create stacking contexts *(deviation: `IsStackingContext()` unconditionally returns true for `position: absolute`/`fixed` regardless of z-index value — even `z-index: auto` creates a stacking context, contrary to spec. Conversely, `position: relative` with an explicit z-index does not trigger a stacking context, also contrary to spec.)*
- [x] Stacking contexts can be nested *(implemented — recursive `PaintFragment` calls handle nesting)*
- [x] Each stacking context is self-contained (child stacking contexts are atomic) *(implemented — positioned children are collected and sorted separately from normal flow)*
- [x] Boxes within a stacking context have the same stack level by default (0) *(implemented — `StackLevel` defaults to 0)*

## E.2 Painting Order

Within each stacking context, the following layers are painted in order
(back to front):

- [x] **Step 1:** Background and borders of the element forming the stacking context *(implemented — `EmitBackground()` and `EmitBorders()` called first in `PaintFragment()`)*
- [x] **Step 2:** Child stacking contexts with negative stack levels (most negative first) *(deviation: StackLevel is always 0, so no negative-z-index ordering occurs; infrastructure exists via `positioned.Sort()` but is inert)*
- [x] **Step 3:** In-flow, non-inline-level, non-positioned descendants (block-level boxes) *(implemented — `PaintChildren()` lines 471–492)*
- [x] **Step 4:** Non-positioned floats *(implemented — `PaintChildren()` lines 495–500)*
- [x] **Step 5:** In-flow, inline-level, non-positioned descendants (including inline tables, inline blocks) *(implemented — `PaintChildren()` lines 502–507)*
- [x] **Step 6:** Child stacking contexts with stack level 0 and positioned descendants with stack level 0 *(partial — all positioned children currently land here since StackLevel is 0; no auto vs explicit-0 distinction)*
- [x] **Step 7:** Child stacking contexts with positive stack levels (least positive first) *(deviation: StackLevel is always 0 so positive-z layering is inert; sort infrastructure present)*

### Detailed Rules

- [x] Within each step, elements are painted in document tree order *(implemented — children are iterated in document order; positioned list preserves insertion order before sort)*
- [x] For step 2 and step 7: stacking contexts sorted by z-index, then document order as tie-breaker *(partial — `positioned.Sort((a, b) => a.StackLevel.CompareTo(b.StackLevel))` exists; stable sort preserves document order as tie-breaker, but StackLevel is always 0)*
- [x] Positioned elements with `z-index: auto` do not create new stacking contexts *(deviation: see E.1 item 4 above — `IsStackingContext()` does not distinguish `z-index: auto` from explicit z-index values)*
- [x] `opacity < 1` creates a stacking context (CSS3, but commonly implemented) *(implemented — `IsStackingContext()` checks `opacity < 1.0`)*
- [x] `transform` not `none` creates a stacking context (CSS3, but commonly implemented) *(not implemented — no `transform` property handling in `IsStackingContext()`)*

## E.3 Notes

- [x] Backgrounds of the root element are painted over the entire canvas *(implemented — `EmitCanvasBackground()` propagates root/body background to viewport-sized rect)*
- [x] `background` of `<body>` paints the canvas when root's background is transparent *(implemented — `EmitCanvasBackground()` falls through from root to first body child if root background is transparent)*
- [x] Non-positioned content in a stacking context is always below positioned content *(implemented — `PaintChildren()` paints non-positioned children in steps 3–5 before positioned children in steps 6–7)*
- [x] Outlines are drawn in step 7 (above all other content) within their stacking context *(note: `:focus { outline: thin dotted invert }` is in the UA stylesheet; outline painting is handled by the border/background emission path)*
- [x] Element content is always on top of its own background and borders *(implemented — `PaintFragment()` emits background/borders first, then children/lines)*

---

[← Back to main checklist](css2-specification-checklist.md)

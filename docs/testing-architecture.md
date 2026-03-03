# Testing Architecture – Testable IR Boundaries

> Phase 1 deliverable for the [Automated Multi-Layer Test Suite Roadmap](testing-roadmap.md).
>
> **Scope:** Defines the four testable IR boundaries (Style → Layout →
> Paint → Raster) and their invariants. This describes the **target testing
> architecture** — see the [Testing Roadmap](testing-roadmap.md) for
> implementation status of each phase. For the practical "how to run tests"
> guide see [Testing Guide](testing-guide.md).

---

## Overview

This document defines the four testable IR (Intermediate Representation)
boundaries in the Broiler rendering pipeline. For each boundary it specifies the
inputs consumed, the output structure produced, and the invariants that can be
asserted automatically.

The IR types are defined in
`HtmlRenderer.Core/Core/IR/` with builder classes in
`HtmlRenderer.Orchestration/Core/IR/`.

---

## Pipeline

```
  HTML + CSS         ComputedStyle         Fragment tree         DisplayList         Pixels
 ──────────── ──▶ ──────────────── ──▶ ──────────────── ──▶ ──────────────── ──▶ ─────────
   (source)         Style phase          Layout phase          Paint phase         Raster
```

Each arrow represents a testable boundary.

---

## 1. Style Phase – `ComputedStyle`

### Location

- Type: `HtmlRenderer.Core.IR.ComputedStyle` (sealed record)
- Builder: `HtmlRenderer.Orchestration.Core.IR.ComputedStyleBuilder`

### Inputs

| Input | Type | Description |
|-------|------|-------------|
| DOM element | `CssBoxProperties` | Parsed HTML element with inline styles, class attributes, and inherited properties |
| Cascade result | (implicit) | Specificity-ordered CSS rules already resolved by the CSS cascade |

### Output Structure

```
ComputedStyle
├── Kind            : BoxKind (Block, Inline, ReplacedImage, TableCell, …)
├── Display         : string
├── Position        : string
├── Float / Clear   : string
├── Overflow        : string
├── Visibility      : string
├── Direction       : string
├── Width / Height  : string (CSS value) + ActualWidth / ActualHeight (resolved px)
├── MaxWidth        : string
├── Margin          : BoxEdges (Top, Right, Bottom, Left – resolved px)
├── Border          : BoxEdges + per-side Color + per-side Style
├── Padding         : BoxEdges
├── FontFamily / FontSize / FontWeight / FontStyle : string / double
├── LineHeight      : double
├── TextAlign / TextDecoration / WhiteSpace / WordBreak : string
├── Color           : string (text colour)
├── BackgroundColor / BackgroundGradient / BoxShadow : string
├── ListStart / ListReversed : int? / bool? (Phase 2 additions)
├── ImageSource     : string?
├── FlexDirection   : string
└── BorderCollapse  : string
```

### Testable Invariants

| Invariant | Assertion |
|-----------|-----------|
| No `null` for required properties | `Display`, `Position`, `Float`, `Clear` are never null |
| Resolved pixel values are finite | `ActualWidth`, `ActualHeight` ≠ NaN/Inf |
| `BoxEdges` values are non-negative | `Margin.Top` ≥ 0, `Padding.Left` ≥ 0, etc. (margins may be negative per spec, but border/padding must not) |
| `FontSize` is positive | `FontSize > 0` |
| `Kind` matches `Display` | When `Display == "block"`, `Kind ∈ {Block, ListItem, TableCaption, …}` |
| Inheritance correctness | `Color`, `FontFamily`, `FontSize` inherit from parent when not explicitly set |

---

## 2. Layout Phase – `Fragment` Tree

### Location

- Type: `HtmlRenderer.Core.IR.Fragment` (sealed record)
- Sub-types: `LineFragment`, `InlineFragment`
- Builder: `HtmlRenderer.Orchestration.Core.IR.FragmentTreeBuilder`

### Inputs

| Input | Type | Description |
|-------|------|-------------|
| Post-layout box tree | `CssBox` (root) | Fully laid-out box tree with resolved geometry |

### Output Structure

```
Fragment
├── Location        : (X, Y) relative to parent
├── Size            : (Width, Height)
├── Bounds          : RRect (absolute)
├── Margin          : BoxEdges
├── Border          : BoxEdges
├── Padding         : BoxEdges
├── Style           : ComputedStyle (back-reference)
├── CreatesStackingContext : bool
├── StackLevel      : int
├── Children        : Fragment[]
├── Lines           : LineFragment[]
│   └── LineFragment
│       ├── X, Y, Width, Height
│       ├── Baseline : double
│       └── Inlines  : InlineFragment[]
│           └── InlineFragment
│               ├── X, Y, Width, Height
│               ├── Text       : string?
│               ├── FontHandle : object?
│               ├── FontSize   : double
│               ├── Color      : string
│               └── Selected / SelectedStartOffset / SelectedEndOffset
├── InlineRects     : Dictionary<int, RRect> (per-line-box rects)
├── BackgroundImageHandle : object?
└── ImageHandle     : object?
```

### Testable Invariants

| Invariant | Assertion |
|-----------|-----------|
| No NaN/Inf geometry | `X`, `Y`, `Width`, `Height` are all finite for every Fragment, LineFragment, InlineFragment |
| Non-negative dimensions | `Width ≥ 0`, `Height ≥ 0` for all fragments |
| Children inside parent bounds | For non-positioned, non-float children: `child.X ≥ 0` and `child.X + child.Width ≤ parent.Width` (content-box) |
| Lines ordered vertically | `Lines[i].Y ≤ Lines[i+1].Y` for all consecutive line fragments |
| Inlines ordered horizontally (LTR) | Within a line (LTR direction): `Inlines[i].X + Inlines[i].Width ≤ Inlines[i+1].X` (approximately) |
| Floats do not overlap | Left-floats and right-floats in the same BFC must not overlap horizontally |
| Baseline within line height | `0 ≤ LineFragment.Baseline ≤ LineFragment.Height` |
| Block children stack vertically | Consecutive block-level children have `child[i].Y + child[i].Height ≤ child[i+1].Y` (before margin collapse) |

### Proposed JSON Dump Format

```json
{
  "x": 0, "y": 0, "width": 800, "height": 600,
  "margin": { "top": 0, "right": 0, "bottom": 0, "left": 0 },
  "border": { "top": 1, "right": 1, "bottom": 1, "left": 1 },
  "padding": { "top": 8, "right": 8, "bottom": 8, "left": 8 },
  "stackLevel": 0,
  "createsStackingContext": false,
  "lines": [
    {
      "x": 8, "y": 8, "width": 784, "height": 20,
      "baseline": 16,
      "inlines": [
        { "x": 8, "y": 8, "width": 100, "height": 20, "text": "Hello" }
      ]
    }
  ],
  "children": [ /* nested Fragment objects */ ]
}
```

---

## 3. Paint Phase – `DisplayList`

### Location

- Type: `HtmlRenderer.Core.IR.DisplayList` (sealed record)
- Item types: `DisplayItem` subclasses with `[JsonDerivedType]` discriminators
- Builder: `HtmlRenderer.Orchestration.Core.IR.PaintWalker`

### Inputs

| Input | Type | Description |
|-------|------|-------------|
| Fragment tree root | `Fragment` | Immutable layout tree with geometry and style references |

### Output Structure

```
DisplayList
└── Items : DisplayItem[] (ordered, flat)
    ├── FillRectItem    { X, Y, Width, Height, Color }
    ├── DrawBorderItem  { X, Y, Width, Height, TopColor, RightColor, BottomColor, LeftColor,
    │                     TopStyle, RightStyle, BottomStyle, LeftStyle,
    │                     TopWidth, RightWidth, BottomWidth, LeftWidth,
    │                     TopLeftRadius, TopRightRadius, BottomRightRadius, BottomLeftRadius }
    ├── DrawTextItem    { X, Y, Text, Color, FontFamily, FontSize, FontWeight, FontStyle,
    │                     IsRtl, FontHandle }
    ├── DrawImageItem   { SourceRect, DestRect, ImageHandle }
    ├── ClipItem        { X, Y, Width, Height }
    ├── RestoreItem     { }
    ├── OpacityItem     { Opacity }
    └── DrawLineItem    { X1, Y1, X2, Y2, Color, Width, DashStyle }
```

### Testable Invariants

| Invariant | Assertion |
|-----------|-----------|
| Deterministic ordering | Same input Fragment tree always produces identical DisplayList |
| Proper clip nesting | Every `ClipItem` has a matching `RestoreItem`; nesting is balanced |
| No negative sizes | `Width ≥ 0`, `Height ≥ 0` for all rect-based items |
| Finite coordinates | No NaN/Inf in any coordinate or dimension field |
| Text items have font metadata | `DrawTextItem.FontFamily` is non-empty, `FontSize > 0` |
| Colour values are valid | All colour strings are parseable or colour objects are non-null |
| Paint order matches stacking context | Items for higher `StackLevel` fragments appear after lower ones |

### Existing JSON Serialisation

DisplayList items already have `[JsonDerivedType]` annotations:

```csharp
[JsonDerivedType(typeof(FillRectItem), "FillRect")]
[JsonDerivedType(typeof(DrawBorderItem), "DrawBorder")]
// ... etc.
```

This enables `System.Text.Json.JsonSerializer.Serialize(displayList)` for golden
tests and debugging.

---

## 4. Raster Phase – Pixels

### Location

- Interface: `HtmlRenderer.Core.IR.IRasterBackend`
- Implementation: `HtmlRenderer.Orchestration.Core.IR.RGraphicsRasterBackend`

### Inputs

| Input | Type | Description |
|-------|------|-------------|
| Display list | `DisplayList` | Ordered drawing primitives |
| Target surface | `object` (platform-specific) | Platform graphics context (WPF `DrawingContext`, SkiaSharp `SKCanvas`, etc.) |

### Output

- Platform-rendered bitmap (PNG, JPEG, or WPF visual).
- CLI produces images via `--capture-image` flag.

### Testable Invariants

| Invariant | Assertion |
|-----------|-----------|
| Deterministic output | Same DisplayList + same DPR + same fonts → identical pixels |
| Image dimensions match layout | Output image width/height matches root Fragment dimensions |
| Non-empty output | Rendered image has at least one non-transparent pixel |

### Current Testing Approach

Pixel tests use `SkiaSharp.SKBitmap` with colour predicate functions:

```csharp
bool IsRed(SKColor p) => p.Red > 150 && p.Green < 50 && p.Blue < 50;
int CountPixels(SKBitmap bmp, Func<SKColor, bool> pred);
RectangleF GetColorBounds(SKBitmap bmp, Func<SKColor, bool> pred);
```

This approach validates colour presence/absence in regions but cannot detect
subtle rendering regressions (anti-aliasing, sub-pixel shifts, border artefacts).

---

## Summary – Testing Surface Area

| Layer | Input | Output | Dump Format | Invariants | Golden Tests |
|-------|-------|--------|-------------|------------|--------------|
| **Style** | `CssBoxProperties` | `ComputedStyle` | JSON (proposed) | 6 defined | ❌ Not yet |
| **Layout** | `CssBox` tree | `Fragment` tree | JSON (proposed) | 8 defined | ❌ Not yet |
| **Paint** | `Fragment` tree | `DisplayList` | JSON (existing) | 7 defined | ❌ Not yet |
| **Raster** | `DisplayList` | Pixels | PNG/JPEG (existing) | 3 defined | ❌ Not yet |

---

## Related Documents

- [Testing Current State](testing-current-state.md) — full audit of test coverage and gaps
- [Testing Roadmap](testing-roadmap.md) — staged implementation plan
- [Testing Guide](testing-guide.md) — how to run, write, and organise tests
- [Architecture Separation](architecture-separation.md) — current pipeline module map and data flow

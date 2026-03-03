# Architecture Separation: Style / Layout / Paint / Raster

## 1. Current Pipeline (As-Is)

### Module Map

| Phase | Module(s) | Key Files |
|-------|-----------|-----------|
| **HTML Parsing** | `HtmlRenderer.Orchestration` | `DomParser.cs`, `HtmlParser.cs` |
| **CSS Parsing** | `HtmlRenderer.CSS` | `CssParser.cs`, `CssValueParser.cs` |
| **Selector Matching** | `HtmlRenderer.Orchestration` | `DomParser.CascadeApplyStyles()` |
| **Computed Styles** | `HtmlRenderer.Dom` | `CssBoxProperties.cs` (lazy-parsed CSS lengths) |
| **Layout** | `HtmlRenderer.Dom` | `CssBox.PerformLayoutImp()`, `CssLayoutEngine.cs`, `CssLayoutEngineTable.cs` |
| **Paint** | `HtmlRenderer.Dom` + `HtmlRenderer.Rendering` | `CssBox.Paint()`/`PaintImp()`, `BordersDrawHandler`, `BackgroundImageDrawHandler` |
| **Raster** | `HtmlRenderer.Image` (Skia) / `HtmlRenderer.WPF` | `GraphicsAdapter`, platform `RGraphics` |
| **Orchestration** | `HtmlRenderer.Orchestration` | `HtmlContainerInt.PerformLayout()`, `HtmlContainerInt.PerformPaint()` |

### Data Flow

```
HTML string
  │
  ▼
DomParser.GenerateCssTree()
  ├── HtmlParser  → CssBox tree (DOM + style in one object)
  └── CascadeApplyStyles() → CSS property strings on each CssBox
  │
  ▼
HtmlContainerInt.PerformLayout(RGraphics)
  └── CssBox.PerformLayout(RGraphics)
        ├── CssBoxProperties: lazy-resolve CSS lengths → cached doubles
        ├── CssLayoutEngine.CreateLineBoxes() → inline text layout
        ├── CssLayoutEngineTable.PerformLayout() → table layout
        └── Writes: Location, Size, ActualBottom, ActualRight, Rectangles
  │
  ▼
HtmlContainerInt.PerformPaint(RGraphics)
  └── CssBox.Paint(RGraphics)
        ├── CssBox.PaintImp() → iterates Rectangles per line-box
        │     ├── BackgroundImageDrawHandler.DrawBackgroundImage()
        │     ├── BordersDrawHandler.DrawBoxBorders()
        │     └── PaintDecoration() (text underline/overline)
        ├── Child.Paint() recursion
        └── RGraphics.DrawXxx() → platform surface
```

### Current Intermediate Representations

| IR | Representation | Notes |
|----|---------------|-------|
| DOM | `CssBox` tree (also carries style + layout) | Single "god object" |
| CSS data | `CssData` — map of selector → `CssBlock` | Clean, separate module |
| Computed style | String properties on `CssBoxProperties`, lazily parsed to doubles | No standalone `ComputedStyle` struct |
| Layout result | Fields on `CssBox`: `Location`, `Size`, `Rectangles`, `LineBoxes` | Mixed into the DOM node |
| Display list | None — paint walks the box tree directly | No display list IR |
| Raster | `RGraphics` (adapter over Skia / WPF) | Clean abstraction via interfaces |

### Problem Areas

**`CssBox` is a "god object."** It combines:
- DOM identity (tag name, children, parent)
- CSS property storage (string fields + cached computed values)
- Layout state (position, size, line boxes, rectangles)
- Paint logic (`Paint()`, `PaintImp()`, `PaintDecoration()`)
- I/O side effects (image loading during layout)

**No display list.** `PaintImp()` calls `RGraphics` draw methods directly.
The paint phase cannot be replayed, cached, or inspected without re-walking
the tree.

**No standalone layout output.** Layout results are stored as mutable fields
on `CssBox` itself (`Location`, `Size`, `Rectangles`). There is no
`FragmentTree` or `LayoutResult` IR that paint can consume independently.

---

## 2. Target Pipeline (To-Be)

### Minimal Interfaces

The target state introduces four clean boundaries with explicit IRs:

```
HTML + CSS
  │
  ▼
┌─────────────────────────────────────┐
│  Style Phase                        │
│  Input:  CssBox tree, CssData       │
│  Output: ComputedStyle per box      │
└───────────────┬─────────────────────┘
                │
                ▼
┌─────────────────────────────────────┐
│  Layout Phase                       │
│  Input:  LayoutTree + ComputedStyle │
│  Output: FragmentTree               │
└───────────────┬─────────────────────┘
                │
                ▼
┌─────────────────────────────────────┐
│  Paint Phase                        │
│  Input:  FragmentTree               │
│  Output: DisplayList                │
└───────────────┬─────────────────────┘
                │
                ▼
┌─────────────────────────────────────┐
│  Raster Phase                       │
│  Input:  DisplayList                │
│  Output: Pixels (SKSurface / WPF)   │
└─────────────────────────────────────┘
```

### IR Definitions (Pseudo-Interfaces)

#### ComputedStyle

Output of the style system. Layout consumes only this (not raw CSS strings).

```csharp
/// <summary>
/// Resolved, typed CSS property values for a single element.
/// Produced by the style phase; consumed by layout. Immutable once created.
/// </summary>
public sealed class ComputedStyle
{
    // Box model
    public CssDisplay Display { get; init; }
    public CssPosition Position { get; init; }
    public CssFloat Float { get; init; }
    public CssClear Clear { get; init; }

    // Dimensions (resolved to device pixels; percentage → absolute)
    public float? Width { get; init; }
    public float? Height { get; init; }
    public float? MaxWidth { get; init; }
    public float? MaxHeight { get; init; }
    public float? MinWidth { get; init; }
    public float? MinHeight { get; init; }

    // Spacing (resolved to device pixels)
    public BoxEdges Margin { get; init; }
    public BoxEdges Border { get; init; }
    public BoxEdges Padding { get; init; }

    // Positioning offsets
    public float? Top { get; init; }
    public float? Right { get; init; }
    public float? Bottom { get; init; }
    public float? Left { get; init; }

    // Typography
    public string FontFamily { get; init; }
    public float FontSize { get; init; }
    public int FontWeight { get; init; }
    public string FontStyle { get; init; }    // normal | italic | oblique
    public string TextAlign { get; init; }
    public string TextDecoration { get; init; }
    public float LineHeight { get; init; }
    public string WhiteSpace { get; init; }

    // Visual
    public string Color { get; init; }
    public string BackgroundColor { get; init; }
    public string BackgroundImage { get; init; }
    public string BorderColor { get; init; }
    public string BorderStyle { get; init; }
    public float Opacity { get; init; }
    public string Visibility { get; init; }
    public string Overflow { get; init; }
    public int ZIndex { get; init; }
    public bool ZIndexAuto { get; init; }

    // List
    public string ListStyleType { get; init; }
    public string ListStylePosition { get; init; }
}
```

#### LayoutTree (Input to Layout)

```csharp
/// <summary>
/// A layout-only view of the DOM tree. Strips away raw HTML attributes;
/// carries only ComputedStyle + children + text content.
/// </summary>
public sealed class LayoutNode
{
    public ComputedStyle Style { get; init; }
    public string? TextContent { get; init; }       // null for element nodes
    public IReadOnlyList<LayoutNode> Children { get; init; }

    // Identity (for incremental invalidation; opaque to layout)
    public int NodeId { get; init; }
}
```

#### FragmentTree (Output of Layout)

```csharp
/// <summary>
/// Immutable layout result for a single box/fragment.
/// Produced by layout; consumed by paint.
/// </summary>
public sealed class Fragment
{
    // Geometry (absolute coordinates)
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }

    // Box-model edges (resolved)
    public BoxEdges Margin { get; init; }
    public BoxEdges Border { get; init; }
    public BoxEdges Padding { get; init; }

    // Inline fragments (line boxes)
    public IReadOnlyList<LineFragment>? Lines { get; init; }

    // Children
    public IReadOnlyList<Fragment> Children { get; init; }

    // Back-reference to style (read-only; for paint to pick colors etc.)
    public ComputedStyle Style { get; init; }

    // Stacking context hint
    public bool CreatesStackingContext { get; init; }
    public int StackLevel { get; init; }
}

public sealed class LineFragment
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public float Baseline { get; init; }
    public IReadOnlyList<InlineFragment> Inlines { get; init; }
}

public sealed class InlineFragment
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public string? Text { get; init; }        // for text runs
    public ComputedStyle Style { get; init; }  // for font/color
}
```

#### DisplayList (Output of Paint)

```csharp
/// <summary>
/// Flat, ordered list of drawing primitives.
/// Produced by paint; consumed by raster. No DOM/style references.
/// </summary>
public sealed class DisplayList
{
    public IReadOnlyList<DisplayItem> Items { get; init; }
}

public abstract class DisplayItem
{
    public RectangleF Bounds { get; init; }
}

public sealed class FillRectItem : DisplayItem
{
    public Color Color { get; init; }
}

public sealed class DrawBorderItem : DisplayItem
{
    public BoxEdges Widths { get; init; }
    public Color TopColor { get; init; }
    public Color RightColor { get; init; }
    public Color BottomColor { get; init; }
    public Color LeftColor { get; init; }
    public string Style { get; init; }   // solid | dashed | dotted | …
}

public sealed class DrawTextItem : DisplayItem
{
    public string Text { get; init; }
    public string FontFamily { get; init; }
    public float FontSize { get; init; }
    public int FontWeight { get; init; }
    public Color Color { get; init; }
    public PointF Origin { get; init; }
}

public sealed class DrawImageItem : DisplayItem
{
    public object ImageHandle { get; init; }    // platform-specific image
    public RectangleF SourceRect { get; init; }
    public RectangleF DestRect { get; init; }
}

public sealed class ClipItem : DisplayItem
{
    public RectangleF ClipRect { get; init; }
}

public sealed class RestoreItem : DisplayItem { }

public sealed class OpacityItem : DisplayItem
{
    public float Opacity { get; init; }
}
```

#### RasterBackend (Raster Phase)

```csharp
/// <summary>
/// Draws a DisplayList to a platform surface.
/// Implementations: SkiaRasterBackend, WpfRasterBackend.
/// </summary>
public interface IRasterBackend
{
    void Render(DisplayList list, object surface);
}
```

---

## 3. Leak Checks — Concrete Violations

The following violations cross the intended phase boundaries. Each entry
states the **rule violated**, **file and location**, and a **suggested fix**.

### Violation 1: Layout reads raw DOM attributes for list numbering

- **Rule**: Layout must not read DOM attributes directly.
- **Location**: `CssBox.cs` → `GetIndexForList()` (~line 529)
  ```csharp
  bool reversed = !string.IsNullOrEmpty(ParentBox.GetAttribute("reversed"));
  int.TryParse(ParentBox.GetAttribute("start"), out int index);
  ```
- **Fix**: Add `ListReversed` and `ListStart` properties to `ComputedStyle`
  (populated during style resolution from the `<ol>` attributes).

### Violation 2: Image loading triggered during paint

- **Rule**: Paint must not perform I/O or trigger layout.
- **Location**: `CssBoxImage.cs` → `PaintImp()` (~line 29)
  ```csharp
  _imageLoadHandler.LoadImage(GetAttribute("src"), HtmlTag?.Attributes);
  ```
- **Fix**: Move image loading into a pre-paint resource-resolution pass.
  Paint should receive a resolved `ImageHandle` on the `Fragment`.

### Violation 3: Background image loading during layout (word measurement)

- **Rule**: Layout must be a pure geometry computation, no I/O.
- **Location**: `CssBox.cs` → `MeasureWordsSize()` (~line 505–508)
  ```csharp
  _imageLoadHandler = ContainerInt.CreateImageLoadHandler(OnImageLoadComplete);
  _imageLoadHandler.LoadImage(BackgroundImage, HtmlTag?.Attributes);
  ```
- **Fix**: Pre-resolve all images before layout begins (resource loading
  phase). Layout receives only decoded dimensions.

### Violation 4: Paint re-calculates rectangle offsets and border adjustments

- **Rule**: Paint must not recompute layout; only consume FragmentTree.
- **Location**: `CssBoxImage.cs` → `PaintImp()` (~lines 45–48)
  ```csharp
  r.Height -= (float)(ActualBorderTopWidth + ActualBorderBottomWidth
                     + ActualPaddingTop + ActualPaddingBottom);
  r.Y += (float)(ActualBorderTopWidth + ActualPaddingTop);
  ```
- **Fix**: Layout should produce a content-area rectangle inside the
  `Fragment`. Paint picks up the pre-computed rect directly.

### Violation 5: `CssBoxHelper` checks raw tag names during layout

- **Rule**: Layout should operate on `LayoutNode` / `ComputedStyle`, not
  HTML tag names.
- **Location**: `CssBoxHelper.cs` → `CreateBox()` (~lines 12–31)
  ```csharp
  if (tag.Name == HtmlConstants.Img)    // hardcoded tag-name check
  if (tag.Name == HtmlConstants.Iframe) // hardcoded tag-name check
  ```
- **Fix**: Map tag-specific behavior to a `BoxKind` enum in `ComputedStyle`
  or `LayoutNode` (e.g., `BoxKind.ReplacedImage`, `BoxKind.ReplacedIframe`).
  Layout switches on the enum, not the raw string.

---

## 4. Positive Separation Already Achieved

Not everything is tangled. The earlier modularization (ADR-006 / 007 / 008)
established several clean boundaries:

| Boundary | Mechanism | Status |
|----------|-----------|--------|
| CSS parsing ↔ rendering | `IColorResolver` interface | ✅ Clean |
| Paint handlers ↔ CssBox | `IBorderRenderData`, `IBackgroundRenderData` interfaces | ✅ Clean |
| Platform raster ↔ core | `RGraphics` / `RAdapter` abstract adapters | ✅ Clean |
| Font handling ↔ platform | `IFontCreator` interface | ✅ Clean |
| DOM orchestration ↔ CssBox | `IHtmlContainerInt` interface | ✅ Clean |

These existing interfaces are a strong foundation for the full separation.

---

## Related Documents

- [Architecture Roadmap](architecture-roadmap.md) — phased plan for completing the Style/Layout/Paint/Raster separation
- [ADR-006: Modular HtmlRenderer Split](adr/006-modular-htmlrenderer-split.md) — initial modularisation decision
- [ADR-007: Advanced HtmlRenderer Modularization](adr/007-advanced-htmlrenderer-modularization.md) — advanced modularisation plan
- [ADR-008: Further HtmlRenderer Modularization](adr/008-further-htmlrenderer-modularization.md) — further modularisation proposal
- [Testing Architecture](testing-architecture.md) — testable IR boundaries in the rendering pipeline

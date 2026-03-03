# Roadmap: Replace Custom Types in HTML-Renderer with .NET Standard Alternatives

> **Scope:** Audit and replace custom/reimplemented types, structs, classes,
> and algorithms in the `html-renderer` components with modern .NET 8.0/10.0
> standard library equivalents.

## Background

The HTML-Renderer library (version 1.5.2) was originally written to be
platform-agnostic, defining its own primitive types (`RColor`, `RRect`, etc.)
and utility classes so that it could target multiple rendering back-ends
(GDI+, WPF, Skia). Many of these custom types now overlap with types and
APIs available in the .NET 8.0+ base class library.

This document inventories every such candidate, assesses the feasibility and
impact of replacement, and proposes a phased refactoring plan.

---

## Inventory of Custom Types

### Tier 1 — Primitive Value Types (`HtmlRenderer.Primitives`)

These are the lowest-level types used pervasively throughout the codebase.

| Custom Type | File | .NET Standard Alternative | Usage (files) | Notes |
|---|---|---|---|---|
| `RColor` | `Primitives/Adapters/Entities/RColor.cs` | `System.Drawing.Color` | ~23 | Readonly struct, ARGB 32-bit packed. `System.Drawing.Color` has identical semantics and richer API (named colours, `FromArgb`, `ToArgb`). |
| `RRect` | `Primitives/Adapters/Entities/RRect.cs` | `System.Drawing.RectangleF` | ~32 | Mutable struct with `double` precision. `RectangleF` uses `float`; if double precision is required, consider keeping a thin wrapper or using `System.Windows.Rect` (WPF-only). |
| `RSize` | `Primitives/Adapters/Entities/RSize.cs` | `System.Drawing.SizeF` | ~21 | Mutable struct with `double` precision. Same float-vs-double consideration as `RRect`. |
| `RPoint` | `Primitives/Adapters/Entities/RPoint.cs` | `System.Drawing.PointF` | ~27 | Primary-constructor struct with `double` X/Y. Same float-vs-double consideration. |
| `RDashStyle` | `Primitives/Adapters/Entities/RDashStyle.cs` | `System.Drawing.Drawing2D.DashStyle` | ~5 | Enum values are identical (`Solid`, `Dash`, `Dot`, `DashDot`, `DashDotDot`, `Custom`). Drop-in replacement. |
| `RFontStyle` | `Primitives/Adapters/Entities/RFontStyle.cs` | `System.Drawing.FontStyle` | ~11 | Flags enum with identical values (`Regular=0`, `Bold=1`, `Italic=2`, `Underline=4`, `Strikeout=8`). Drop-in replacement. |
| `RKeyEvent` | `Primitives/Adapters/Entities/RKeyEvent.cs` | *(none)* | Low | Thin abstraction for keyboard events (Control, A, C keys). No standard cross-platform equivalent; **keep as-is**. |
| `RMouseEvent` | `Primitives/Adapters/Entities/RMouseEvent.cs` | *(none)* | Low | Single `LeftButton` boolean. No standard equivalent; **keep as-is**. |

### Tier 2 — Abstract Adapter Types (`HtmlRenderer.Adapters`)

These are abstract base classes that platform-specific backends (WPF, Skia)
implement. They define the rendering API surface.

| Custom Type | File | .NET Standard Alternative | Usage (files) | Assessment |
|---|---|---|---|---|
| `RBrush` | `Adapters/Adapters/RBrush.cs` | `System.Drawing.Brush` | Moderate | Abstract + `IDisposable`. Platform adapters subclass this; replacement requires rewriting every backend adapter. **High effort, low benefit** — keep as adapter abstraction. |
| `RPen` | `Adapters/Adapters/RPen.cs` | `System.Drawing.Pen` | Moderate | Abstract with `Width` and `DashStyle`. Same as `RBrush` — keeps adapter pattern intact. |
| `RFont` | `Adapters/Adapters/RFont.cs` | `System.Drawing.Font` | Moderate | Abstract with `Size`, `Height`, `UnderlineOffset`, `LeftPadding`, `GetWhitespaceWidth()`. Custom API surface; not a 1:1 replacement. |
| `RFontFamily` | `Adapters/Adapters/RFontFamily.cs` | `System.Drawing.FontFamily` | Low | Abstract with `Name` property. Minimal but backend-specific. |
| `RImage` | `Adapters/Adapters/RImage.cs` | `System.Drawing.Image` | Moderate | Abstract + `IDisposable` with `Width`/`Height`. Platform-dependent. |
| `RGraphics` | `Adapters/Adapters/RGraphics.cs` | `System.Drawing.Graphics` | High | Core rendering surface. Platform-specific; replacement would be a full rewrite. **Keep as-is.** |
| `RGraphicsPath` | `Adapters/Adapters/RGraphicsPath.cs` | `System.Drawing.Drawing2D.GraphicsPath` | Moderate | Abstract path with `Start`, `LineTo`, `ArcTo`. Backend-specific. |

**Recommendation:** The abstract adapter types are *by design* platform-agnostic
abstractions. Replacing them with `System.Drawing` concrete types would
**couple the core engine to GDI+**, defeating the adapter pattern. These should
remain as-is unless the project decides to target a single rendering backend.

### Tier 3 — Utility Classes (`HtmlRenderer.Utils`)

| Custom Type / Method | File | .NET Standard Alternative | Usage (files) | Assessment |
|---|---|---|---|---|
| `SubString` | `Utils/Core/Utils/SubString.cs` | `ReadOnlyMemory<char>` / `ReadOnlySpan<char>` | ~7 | Custom class for zero-allocation substring views. `ReadOnlyMemory<char>` is the modern equivalent and supports slicing without allocation. `ReadOnlySpan<char>` is even faster for synchronous paths. **Good candidate.** |
| `ArgChecker` | `Utils/Core/Utils/ArgChecker.cs` | `ArgumentNullException.ThrowIfNull()` (.NET 6+), `ArgumentException.ThrowIfNullOrEmpty()` (.NET 7+) | ~31 | Heavily used. .NET 6+ provides built-in throw helpers with identical semantics and better JIT inlining. **Good candidate — high-impact cleanup.** |
| `CommonUtils.TryGetUri()` | `Utils/Core/Utils/CommonUtils.cs` | `Uri.TryCreate()` | ~4 | Wraps `Uri.TryCreate()` already; may be thin enough to inline. |
| `CommonUtils.GetNextSubString()` | `Utils/Core/Utils/CommonUtils.cs` | `string.Split()` / `MemoryExtensions.Split()` | Moderate | Custom tokeniser for space/delimiter splitting. Modern `MemoryExtensions` provides span-based splitting. |
| `CommonUtils.IsDigit()` / `ToDigit()` | `Utils/Core/Utils/CommonUtils.cs` | `char.IsDigit()` / `char.GetNumericValue()` | Low | Trivial wrappers; can be inlined. |
| `CommonUtils.IsAsianCharacter()` | `Utils/Core/Utils/CommonUtils.cs` | `char.GetUnicodeCategory()` | Low | Custom CJK range check. `UnicodeCategory` provides more robust coverage. |
| `CommonUtils.ConvertToAlphaNumber()` | `Utils/Core/Utils/CommonUtils.cs` | *(none)* | Low | Multi-script ordered-list numbering (Roman, Greek, Armenian, Georgian, Hebrew, Hiragana, Katakana). No standard equivalent; **keep as-is**. |
| `HtmlUtils.DecodeHtml()` | `Utils/Core/Utils/HtmlUtils.cs` | `System.Net.WebUtility.HtmlDecode()` | ~3 | Custom HTML entity decoding with large lookup tables. `WebUtility.HtmlDecode()` handles the full HTML5 named character reference table. **Good candidate.** |
| `HtmlUtils.EncodeHtml()` | `Utils/Core/Utils/HtmlUtils.cs` | `System.Net.WebUtility.HtmlEncode()` | ~3 | Same as above. **Good candidate.** |
| `HtmlConstants` | `Utils/Core/Utils/HtmlConstants.cs` | *(none)* | Moderate | String constants for HTML tag/attribute names. No standard replacement; **keep as-is**. |
| `CssConstants` | `Utils/Core/Utils/CssConstants.cs` | *(none)* | Moderate | CSS property value constants. No standard replacement; **keep as-is**. |

### Tier 4 — CSS Parsing (`HtmlRenderer.CSS`)

| Custom Type / Pattern | File | .NET Standard Alternative | Assessment |
|---|---|---|---|
| `RegexParserUtils` / `RegexParserHelper` regex caching | `CSS/Core/Parse/RegexParserUtils.cs`, `RegexParserHelper.cs` | `[GeneratedRegex]` source generator (.NET 7+) | Both classes use `ConcurrentDictionary` to cache compiled regex instances. .NET 7+ `[GeneratedRegex]` generates the regex at compile time, eliminating runtime compilation and dictionary lookup. **Good candidate.** |
| `CssLength` | `CSS/Core/Dom/CssLength.cs` | *(none)* | CSS-specific length parsing with unit conversion. No standard equivalent. |
| `CssValueParser` colour parsing | `CSS/Core/Parse/CssValueParser.cs` | `System.Drawing.ColorTranslator.FromHtml()` | Custom hex/rgb/rgba/named colour parsing. `ColorTranslator.FromHtml()` handles `#RGB`, `#RRGGBB`, and named colours. Does not handle `rgba()` — partial replacement only. |

### Tier 5 — DOM & Rendering (Domain-Specific)

| Custom Type | File | Assessment |
|---|---|---|
| `CssBox`, `CssBoxProperties`, `CssLayoutEngine` | `Dom/Core/Dom/` | CSS box model implementation — no standard equivalent. |
| `CssLineBox`, `CssRect`, `CssRectWord`, `CssRectImage` | `Dom/Core/Dom/` | Layout primitives — no standard equivalent. |
| `CssBlock`, `CssData`, `CssDefaults` | `Core/Core/Entities/`, `Core/Core/` | CSS data model — no standard equivalent. |
| `HtmlTag` | `Dom/Core/Dom/HtmlTag.cs` | Could use `System.Xml.Linq.XElement` but semantics differ for HTML. **Keep as-is.** |
| `HoverBoxBlock`, `CssSpacingBox` | `Dom/Core/Dom/` | Internal layout helpers — no standard equivalent. |
| `CssUnit`, `Border` | `Core/Core/Dom/` | Small enums — no standard equivalent. |
| `ImageLoadHandler`, `FontsHandler`, `BordersDrawHandler` | `Rendering/Core/Handlers/` | Rendering pipeline — no standard equivalent. |

---

## Comparison Summary

| Priority | Custom Implementation | .NET Replacement | Effort | Impact | Risk |
|---|---|---|---|---|---|
| **P1** | `RColor` | `System.Drawing.Color` | Medium | High (23 files) | Low — identical semantics |
| **P1** | `RDashStyle` | `System.Drawing.Drawing2D.DashStyle` | Low | Low (5 files) | Minimal — identical values |
| **P1** | `RFontStyle` | `System.Drawing.FontStyle` | Low | Low (11 files) | Minimal — identical values |
| **P2** | `ArgChecker` | `ArgumentNullException.ThrowIfNull()` etc. | Medium | High (31 files) | Low — semantically equivalent |
| **P2** | `HtmlUtils.DecodeHtml/EncodeHtml` | `System.Net.WebUtility` | Low | Low (3 files) | Low — verify entity coverage |
| **P2** | Regex caching | `[GeneratedRegex]` attribute | Medium | Medium (2 files) | Low — compile-time generation |
| **P3** | `SubString` | `ReadOnlyMemory<char>` | High | Medium (7 files) | Medium — API surface differs |
| **P3** | `RRect` / `RSize` / `RPoint` | `System.Drawing.RectangleF` / `SizeF` / `PointF` | High | Very High (46 files) | **Completed** — Decision A (accept `float` precision) |
| **P4** | `CssValueParser` colour parsing | `ColorTranslator.FromHtml()` | Medium | Low | Medium — partial coverage only |
| **Skip** | Abstract adapters (`RBrush`, `RPen`, `RFont`, `RGraphics`, etc.) | — | — | — | Replacing defeats adapter pattern |
| **Skip** | Domain types (`CssBox`, `CssData`, `CssLayoutEngine`, etc.) | — | — | — | No standard equivalents exist |

---

## Refactor Plan

### Phase 1 — Drop-In Enum Replacements (Low Risk)

**Goal:** Replace custom enums that have identical .NET equivalents.

**Targets:**
- Replace `RDashStyle` → `System.Drawing.Drawing2D.DashStyle`
  - Files: `RDashStyle.cs`, `RPen.cs`, `PenAdapter.cs` (Image + WPF),
    `BordersDrawHandler.cs`, `CssBox.cs`
- Replace `RFontStyle` → `System.Drawing.FontStyle`
  - Files: `RFontStyle.cs`, `RFont.cs`, `IFontCreator.cs`, `IAdapter.cs`,
    `FontAdapter.cs`, `FontsHandler.cs`, `CssBoxProperties.cs`, `CssBox.cs`,
    `WpfAdapter.cs`, `SkiaImageAdapter.cs`

**Approach:**
1. Add `using System.Drawing` / `using System.Drawing.Drawing2D` where needed.
2. Delete the custom enum files.
3. Update all references with find-and-replace.
4. Run full test suite to verify.

**Expected impact:** ~16 files changed; zero behavioural change.

### Phase 2 — Replace `RColor` with `System.Drawing.Color`

**Goal:** Remove the custom colour struct in favour of the standard one.

**Targets:**
- Delete `RColor.cs`
- Update ~23 files referencing `RColor`
- Update `CssValueParser` colour construction to use `Color.FromArgb()`
- Update `IColorResolver` return type

**Approach:**
1. Introduce a `using RColor = System.Drawing.Color` alias in a shared file or
   `Directory.Build.props` global using to minimise diff size.
2. Migrate call sites incrementally (module by module, bottom-up through the
   dependency layers: Primitives → Utils → Adapters → Core → CSS → …).
3. Remove the alias once all call sites use `Color` directly.
4. Delete `RColor.cs`.

**Key API mapping:**
| `RColor` API | `System.Drawing.Color` API |
|---|---|
| `RColor(int a, int r, int g, int b)` | `Color.FromArgb(a, r, g, b)` |
| `RColor.A`, `.R`, `.G`, `.B` | `Color.A`, `.R`, `.G`, `.B` |
| `RColor.IsEmpty` | `Color.IsEmpty` or `Color == Color.Empty` |
| `RColor.Black`, `.White`, etc. | `Color.Black`, `Color.White`, etc. |

**Expected impact:** ~23 files changed; zero behavioural change.

### Phase 3 — Modernise `ArgChecker` with Built-In Throw Helpers

**Goal:** Replace custom argument validation with .NET 6+ built-in throw
helpers for better JIT inlining and reduced code.

**Targets:**
- ~31 files using `ArgChecker.AssertArgNotNull()`
- ~31 files using `ArgChecker.AssertArgNotNullOrEmpty()`

**Approach:**
1. Replace `ArgChecker.AssertArgNotNull(value, "name")` with
   `ArgumentNullException.ThrowIfNull(value)`.
2. Replace `ArgChecker.AssertArgNotNullOrEmpty(value, "name")` with
   `ArgumentException.ThrowIfNullOrEmpty(value)`.
3. Replace `ArgChecker.AssertIsTrue<T>(condition, message)` with
   direct `if (!condition) throw new T(message)`.
4. Delete `ArgChecker.cs` once all call sites are migrated.

**Expected impact:** ~31 files changed; identical runtime behaviour.

### Phase 4 — Replace HTML Entity Encoding/Decoding

**Goal:** Use `System.Net.WebUtility` for HTML entity handling.

**Targets:**
- `HtmlUtils.DecodeHtml()` → `WebUtility.HtmlDecode()`
- `HtmlUtils.EncodeHtml()` → `WebUtility.HtmlEncode()`
- ~3 call sites in `HtmlParser.cs`, `DomUtils.cs`, `CssBox.cs`

**Approach:**
1. Verify that `WebUtility.HtmlDecode()` handles all entities currently
   supported by the custom implementation (ISO 8859-1, Greek, math, special).
2. Write comparison tests for edge cases.
3. Replace call sites.
4. Remove the entity lookup tables from `HtmlUtils.cs`.

**Risk:** `WebUtility.HtmlDecode()` supports the full HTML5 named character
reference table, which is a superset of the current implementation. Verify no
behavioural differences for the entities actually used.

### Phase 5 — Source-Generated Regex

**Goal:** Replace runtime regex compilation and caching with compile-time
source generation.

**Targets:**
- `RegexParserUtils.cs` — `ConcurrentDictionary<string, Regex>` cache
- `RegexParserHelper.cs` — same pattern

**Approach:**
1. Convert each regex pattern string to a `[GeneratedRegex]` partial method.
2. Remove the `ConcurrentDictionary` cache.
3. Mark the containing class as `partial`.

**Expected impact:** 2 files changed; improved startup performance and
reduced allocations.

### Phase 6 — Replace `SubString` with `ReadOnlyMemory<char>` ✅ Completed

**Goal:** Replace the custom `SubString` class with modern .NET memory types.

**Targets:**
- `SubString.cs` (deleted) and ~7 referencing files

**Completed changes:**
1. Replaced `SubString` field/property in `CssBox` with `ReadOnlyMemory<char>`.
2. Updated `HtmlParser` to use `str.AsMemory(start, len)` instead of
   `new SubString(str, start, len)`.
3. Updated `DomParser` to use `.IsEmpty`, `.Span.IsWhiteSpace()`, and
   `.ToString()` instead of SubString-specific methods.
4. Updated `DomUtils` and `CssLayoutEngine` null/whitespace checks to use
   `.Length > 0 && .Span.IsWhiteSpace()`.
5. Rewrote `SubStringTests` to validate `ReadOnlyMemory<char>` behaviour.
6. Deleted `SubString.cs`.

**API mapping used:**
| SubString API | ReadOnlyMemory&lt;char&gt; API |
|---|---|
| `new SubString(str)` | `str.AsMemory()` |
| `new SubString(str, start, len)` | `str.AsMemory(start, len)` |
| `.Length` | `.Length` |
| `[idx]` indexer | `.Span[idx]` |
| `.IsEmpty()` | `.IsEmpty` |
| `.IsEmptyOrWhitespace()` | `.Span.IsWhiteSpace()` |
| `.IsWhitespace()` | `.Length > 0 && .Span.IsWhiteSpace()` |
| `.CutSubstring()` | `.ToString()` |
| `.Substring(start, len)` | `.Slice(start, len).ToString()` |
| `== null` | `.IsEmpty` |

### Phase 7 — Replace Geometric Types with `System.Drawing` ✅ Completed

**Goal:** Replace `RRect`, `RSize`, `RPoint` with `System.Drawing.RectangleF`,
`SizeF`, `PointF` (Decision A — accept float precision).

**Targets:**
- `RRect.cs`, `RSize.cs`, `RPoint.cs` (deleted) and ~46 referencing files

**Completed changes:**
1. Deleted `RRect.cs`, `RSize.cs`, `RPoint.cs` from `HtmlRenderer.Primitives`.
2. Replaced all `RRect` → `RectangleF`, `RSize` → `SizeF`, `RPoint` → `PointF`
   across 46 files spanning all modules (Primitives, Utils, Adapters, Core,
   CSS, Rendering, Dom, Orchestration, HtmlRenderer façade, Image, WPF).
3. Added `(float)` explicit casts at all construction sites where `double`
   values are passed to `float` constructor parameters.
4. Used type aliases (`using PointF = System.Drawing.PointF;` etc.) in WPF
   files to avoid ambiguity with `System.Windows.Point`/`Size`.
5. Removed unused `using TheArtOfDev.HtmlRenderer.Adapters.Entities;` imports
   from all files that no longer reference types in that namespace.
6. Updated `PrimitivesTests` to validate `RectangleF`/`SizeF`/`PointF`
   behaviour.
7. Updated `CommonUtils.Max(SizeF, SizeF)` to use `(float)` casts.

**API mapping used:**
| Custom API | `System.Drawing` API |
|---|---|
| `new RRect(x, y, w, h)` | `new RectangleF((float)x, (float)y, (float)w, (float)h)` |
| `new RRect(location, size)` | `new RectangleF(location.X, location.Y, size.Width, size.Height)` |
| `RRect.Empty` | `RectangleF.Empty` |
| `RRect.FromLTRB(…)` | `RectangleF.FromLTRB(…)` |
| `RRect.Intersect(a, b)` | `RectangleF.Intersect(a, b)` |
| `RRect.Union(a, b)` | `RectangleF.Union(a, b)` |
| `new RSize(w, h)` | `new SizeF((float)w, (float)h)` |
| `RSize.Empty` | `SizeF.Empty` |
| `RSize.ToPointF()` | `SizeF.ToPointF()` |
| `new RPoint(x, y)` | `new PointF((float)x, (float)y)` |
| `PointF.Empty` | `PointF.Empty` |
| `RPoint + RSize` | `PointF + SizeF` (operator exists) |
| `RPoint - RSize` | `PointF - SizeF` (operator exists) |

---

## Types Explicitly Not Recommended for Replacement

| Type | Reason |
|---|---|
| `RBrush`, `RPen`, `RFont`, `RFontFamily`, `RImage`, `RGraphics`, `RGraphicsPath` | Abstract adapter types — replacing with concrete `System.Drawing` types would couple the engine to GDI+ and break the platform-agnostic adapter pattern. |
| `CssBox`, `CssBoxProperties`, `CssLayoutEngine`, `CssLineBox` | Domain-specific CSS layout types with no standard equivalent. |
| `CssBlock`, `CssData`, `CssDefaults`, `CssLength`, `CssUnit` | CSS data model types with no standard equivalent. |
| `HtmlTag`, `HtmlParser`, `DomParser` | HTML parsing types with no standard equivalent. |
| `RKeyEvent`, `RMouseEvent` | Minimal input event abstractions with no cross-platform standard equivalent. |
| `HtmlConstants`, `CssConstants` | String constant collections — no standard replacement exists. |
| `CommonUtils.ConvertToAlphaNumber()` | Multi-script numeral conversion with no standard equivalent. |

---

## Dependency Order for Migration

Replacements must follow the module dependency graph (bottom-up):

```
L0  HtmlRenderer.Primitives    ← Phase 1 (enums), Phase 2 (RColor), Phase 7 (geometry)
L1  HtmlRenderer.Utils         ← Phase 3 (ArgChecker), Phase 4 (HtmlUtils), Phase 6 (SubString)
L2a HtmlRenderer.Adapters      ← Updated to match Primitives changes
L2b HtmlRenderer.Core          ← Updated to match Primitives + Utils changes
L3  HtmlRenderer.CSS           ← Phase 5 (regex), updated for Primitives changes
L3a HtmlRenderer.Rendering     ← Updated to match all lower-layer changes
L4a HtmlRenderer.Dom           ← Updated to match all lower-layer changes
L5  HtmlRenderer.Orchestration ← Updated to match all lower-layer changes
L6  HtmlRenderer (façade)      ← Updated to match all lower-layer changes
    HtmlRenderer.Image         ← Backend adapter updates
    HtmlRenderer.WPF           ← Backend adapter updates
```

---

## Success Criteria

Each phase is considered complete when:

1. All existing tests pass (`dotnet test Broiler.slnx`) with zero regressions.
2. Website capture verification succeeds (`--url https://www.heise.de/`).
3. Engine smoke test passes (`--test-engines`).
4. No precision or rendering differences in Acid1 test suite.

---

## Relationship to Other Roadmaps

- **[w3c-html-compliance.md](w3c-html-compliance.md)** — W3C compliance work
  may benefit from modernised types (e.g. `Color` interop with new CSS colour
  functions).
- **[cli-website-capture.md](cli-website-capture.md)** — capture quality must
  not regress during type replacement.
- **[ADR-006](../adr/006-modular-htmlrenderer-split.md)** /
  **[ADR-007](../adr/007-advanced-htmlrenderer-modularization.md)** /
  **[ADR-008](../adr/008-further-htmlrenderer-modularization.md)** — the
  modular split defines the dependency layers that constrain migration order.

---

## Action Items

- [x] Phase 1 — Replace `RDashStyle` and `RFontStyle` with `System.Drawing` enums
- [x] Phase 2 — Replace `RColor` with `System.Drawing.Color`
- [x] Phase 3 — Replace `ArgChecker` with .NET built-in throw helpers
- [x] Phase 4 — Replace `HtmlUtils.DecodeHtml/EncodeHtml` with `WebUtility`
- [x] Phase 5 — Convert regex caching to `[GeneratedRegex]` source generation
- [x] Phase 6 — Replace `SubString` with `ReadOnlyMemory<char>`
- [x] Phase 7 — Replace `RRect`/`RSize`/`RPoint` with `System.Drawing.RectangleF`/`SizeF`/`PointF`

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `PrimitivesTests.cs` | `HtmlRenderer.Image.Tests` | RRect, RPoint, RSize, Color type correctness |
| `CssLengthTests.cs` | `HtmlRenderer.Image.Tests` | CSS length value parsing and unit conversion |
| `SubStringTests.cs` | `HtmlRenderer.Image.Tests` | SubString wrapper (Phase 6 replacement target) |
| `CommonUtilsTests.cs` | `HtmlRenderer.Image.Tests` | URI handling utilities |

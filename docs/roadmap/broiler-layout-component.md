# Broiler.Layout Component Plan

**Status:** **Complete (2026-06-28).** Phases 1–5 are done: the ~9.7k-line layout
engine lives in `Broiler.Layout`, whose only project dependencies are `Broiler.CSS`,
`Broiler.CSS.Dom`, and `Broiler.Dom`. The namespace flip and identical-copy cleanup
are complete. Phase 5 reduced the internal friend surface from 16 assemblies to seven,
documented each remaining direct box-tree consumer, locked that set in architecture
tests, and removed facade/application box-tree traversal.

The repeatable closeout runner is `scripts/run-rf-layout-validation.ps1`. Its final
2026-06-28 run built the solution without errors or warnings and recorded layout 13/13,
diagnostic seam 5/5, Acid2 25/25, Acid3 65 passes with two established exceptions, and
the curated WPT pixel baseline at 69 passes, 71 accepted failures, five missing-reference
skips, and zero unexpected outcomes. RF-LAYOUT-1 and RF-LAYOUT-2 are closed in
[`refactor-gap.md`](refactor-gap.md).
**Date:** 2026-06-26

**Progress (Phase 1):** `Broiler.Layout/Broiler.Layout/` created in the parent repo
(top-level peer, no submodule per §8), referencing only `Broiler.CSS`, `Broiler.CSS.Dom`,
`Broiler.Dom`. `ILayoutEnvironment` + `ILayoutFont` / `LayoutFontStyle` / `ImageIntrinsics`
define the §4 host seam (BCL-only surface). `Broiler.Layout.Tests` mirrors
`CssDomArchitectureTests` (refs-whitelist, no-consumer-leak, no-mutable-collections) — 3/3
green; project added to `Broiler.slnx` under `/Dependencies/Layout/`.

**Progress (Phase 2a — graphics/measurement coupling):** The `RGraphics g` parameter
threaded through the 27 layout method signatures is now `ILayoutEnvironment g`; the two
measurement sites route through it (`g.MeasureText`, `g.GetWhitespaceWidth`). `RFont`
adopts `ILayoutFont` (members already matched — zero-cost). `LayoutEnvironment`
(`Broiler.HTML.Dom`, internal) is the renderer-side adapter wrapping `RGraphics` +
`IHtmlContainerInt`; the sole external caller `HtmlContainerInt.PerformLayout` constructs
it. `Broiler.HTML.Adapters` + `.Dom` reference `Broiler.Layout` via the existing
`..\..\..\` reach-to-root pattern. Full HTML stack + `Broiler.Cli` build green; flex/border
layout tests match baseline (forwarding-only = behavior-identical; the flaky
`FlexChildren_DisplayBlock_SideBySide` and pre-existing `MaxWidth_CapsFlexItemWidth` are
not regressions — verified via stash A/B with an identical narrow filter).

**Progress (Phase 2b — image intrinsics):** `CssLayoutEngine.MeasureImageSize` now takes
`ILayoutEnvironment g` and computes `ImageIntrinsics? image = imageWord.Image is { } handle ?
g.GetImageIntrinsics(handle) : null` once, replacing all 10 direct
`imageWord.Image.Width/Height/HasIntrinsicRatio` reads. Sole caller
`CssBoxImage.MeasureWordsSize` (already env-threaded) passes `g`. Verified behavior-identical
via stash A/B: the 2 failing Acid2 tests (`CssBackgroundAttachmentFixed_RendersFromViewportOrigin`,
`CssDataUriBackgroundImage_RendersCorrectly`) fail at baseline too — pre-existing background-image
issues, unrelated to replaced-element sizing; the other 23 Acid2 image tests pass.

**Structural finding (de-scopes the rest of §4's container row):** font/colour are **already
decoupled behind an abstract seam** — `GetActualColor(string)` and `GetCachedFont(...)` are
`abstract` on the moving base `CssBoxProperties` (decls at 1046/1344) and called there for all
colour/font resolution; only the *overrides* in `CssBox` (4193/4195) and the async
`OnImageLoadComplete`/`OnLoadImageComplete` glue (RequestRefresh) touch `ContainerInt`. Those
overrides are renderer glue that can **stay behind at the Phase 4 move** (e.g. a partial/subclass
split), so they need no env routing — and forcing them through a stored env would reintroduce a
null-before-layout lifetime hazard for zero decoupling benefit. The env's `GetFont`/`ParseColor`
remain available but are effectively redundant with this seam.

**Net:** Phase 2's environment abstraction is complete for every *direct, non-seamed* coupling in
the moving algorithm (measurement + image intrinsics). The remaining direct `ContainerInt` uses
are not env material — they split into **layout-context inputs** (`ViewportSize` 15×,
`RootLocation`, `ActualSize` = the initial containing block → Phase 3 computed-style/used-value)
and **host services / renderer glue** (`ReportError`, `CreateImageLoadHandler`,
`AvoidImagesLateLoading`/`AvoidAsyncImagesLoading`, the async RequestRefresh → stay behind at the
move per §2.2).

**Phase 3 reality-check (2026-06-26).** §3's premise — that `Broiler.CSS` already provides the
`CssLength`/units/constants layout needs — is **false**. The value types layout depends on have no
compatible `Broiler.CSS` equivalent and are **shared with the renderer**: `CssConstants` (412×, in
`Broiler.HTML.Utils`, used across 21 renderer files), the pixel-resolving `CssValueParser` (76×,
`Broiler.HTML.CSS` — a *different* API from `Broiler.CSS`'s syntax-only parser), `CssLength` (13×,
`Broiler.HTML.CSS`), `CssUnit` (12×, `Broiler.HTML.Core.Core.Dom` — 16 members `Pixels`/`Ems`, vs
`Broiler.CSS.CssUnit`'s 28 members `Px`/`Em`), `CssSpacingBox` (4×, a `CssBox` subclass that simply
moves with layout in Phase 4). **Decision (user):** *add the primitives to `Broiler.CSS`, repoint
layout only* — the renderer keeps its `Broiler.HTML.*` copies until Phase 7 dedups; temporary
duplication accepted. Executed in behavior-neutral sub-steps:

- **Phase 3.1 — `CssConstants` DONE 2026-06-26.** Added `Broiler.CSS/Broiler.CSS/CssConstants.cs`
  (`public static`, 136 constants byte-identical to the `Broiler.HTML.Utils` copy — verified by
  diff). `Broiler.HTML.Dom` now references `Broiler.CSS`; each of the 10 moving files that use
  `CssConstants` got a `using CssConstants = Broiler.CSS.CssConstants;` alias (shadows just that
  identifier — leaves other `Broiler.HTML.Utils` resolution intact, no reference-level edits). All
  413 references now resolve to `Broiler.CSS`. Broiler.CSS + Dom + Cli build green;
  `Acid3BorderLayoutTests` 5/5.

- **Phase 3.2 — `CssValueParser` (length core) DONE 2026-06-26.** The moving files use only the
  *static* length surface (`ParseLength` 65×, `ParseNumber` 4×, `GetActualBorderWidth` 4×,
  `IsValidLength` 3×) — **not** the instance colour methods (colour goes through the abstract
  `GetActualColor` seam). Extracted that static core verbatim into
  `Broiler.CSS/Broiler.CSS/CssLengthParser.cs` (`public static class`; `IsFloat`/`IsInt`/
  `IsValidLength`/`ParseNumber`/`ParseLength` overloads + `TryEvaluate*`/`GetUnit`/
  `NormalizeSingleValueLengthFunction` helpers + `GetActualBorderWidth` + viewport `ThreadStatic` +
  `SetViewportSize`). It touches no `Broiler.HTML.Core` and no `Color` (only `SizeF`). Named
  `CssLengthParser` to avoid colliding with the pre-existing syntax-only `Broiler.CSS.CssValueParser`;
  the 7 moving files that call it got a `using CssValueParser = Broiler.CSS.CssLengthParser;` alias
  (all 76 refs repointed, no per-call edits). Viewport `ThreadStatic` is shared mutable state set at
  the single caller `HtmlContainerInt` (602) — added a sibling `Broiler.CSS.CssLengthParser.SetViewportSize`
  call there so the port stays in sync per pass. Broiler.CSS + Dom + Orchestration + Cli build green.
  Verified behavior-preserving: in the layout-heavy batch the only failures were the 3 known
  pre-existing (`MaxWidth_CapsFlexItemWidth`, `CssDataUriBackgroundImage`, `CssBackgroundAttachmentFixed`)
  plus 2 that **pass in isolation** (`Acid2_Render_IsNotBlankOrAllRed`, `FixedChild_DoesNot_Inflate_Parent_AutoHeight`)
  = the suite's documented order-flakiness; Acid3BorderLayoutTests 5/5 alone. (WPT not re-run.)

- **Phase 3.3 (`CssLength`) + 3.4 (`CssUnit`) + 3.5 (`RegexParserUtils`) DONE 2026-06-26.** Ported
  `CssLength` to `Broiler.CSS/Broiler.CSS/CssLength.cs` (`public sealed`), remapping its unit enum onto
  the existing `Broiler.CSS.CssUnit` (`Px`/`Em`/… for `Pixels`/`Ems`/…) — so 3.4 folds in. Layout uses
  only 3 enum members (`Pixels` 9×→`Px`, `Ems` 2×→`Em`, `None`); all 10 `CssLength` instances are local
  `new CssLength(...)` (never cross to the renderer). Repointed the 4 files via
  `using CssLength = Broiler.CSS.CssLength;` + `using CssUnit = Broiler.CSS.CssUnit;` aliases plus the
  two member renames. Ported the self-contained `RegexParserUtils` (116 lines, `[GeneratedRegex]`, only
  `System.Text.RegularExpressions`) to `Broiler.CSS/Broiler.CSS/RegexParserUtils.cs`; `CssBoxProperties`
  repointed via `using RegexParserUtils = Broiler.CSS.RegexParserUtils;`.
  **Milestone: the moving files are now fully decoupled from `Broiler.HTML.CSS`** — the dead
  `using Broiler.HTML.CSS;` was removed from all 8 files that had it, and the `Broiler.HTML.CSS.Core.Parse`
  import is gone; a grep for `Broiler.HTML.CSS` across the 13 moving files returns nothing. Broiler.CSS +
  Dom + Cli build green; verified behavior-preserving (Acid3BorderLayout 5/5; Acid3CssCompliance 58/59
  with only the memory-flagged pre-existing `Border_Shorthand_Expands_Color_To_Individual_Sides`,
  confirmed failing at baseline via stash A/B; FlexLayout only the pre-existing `MaxWidth`). WPT not re-run.

- **Phase 3 computed-style input + ICB inputs DONE 2026-06-26 → Phase 3 COMPLETE.**
  - *Computed-style input confirmed clean (no code change):* a grep of the 13 moving files for
    `CssData`/`CssBlock`/`GetComputedStyle`/`CssComputedStyle` returns nothing (only `CssBox.SourceElement`,
    a property the renderer's projection *sets*). Layout consumes the projected `CssBoxProperties` string
    fields that Phase 5's `GetCascadedStyle`→`SetPropertyValue` fills — exactly the §5 boundary.
  - *ICB inputs abstracted:* added `ViewportSize` (get), `RootLocation` (get) and `ActualSize` (get/set) to
    `ILayoutEnvironment` (BCL `SizeF`/`PointF`); the renamed renderer adapter `HtmlLayoutEnvironment` forwards
    them to `IHtmlContainerInt`. `CssBox` gained a stored `LayoutEnvironment` property with parent-chain
    resolution (mirroring `ContainerInt`), set on the root in `HtmlContainerInt.PerformLayout` before
    `Root.PerformLayout`. Replaced all 19 `ContainerInt.{ViewportSize×15, RootLocation×2, ActualSize×2}`
    reads/writes in `CssBox.cs` with `LayoutEnvironment.*` (adapter renamed `LayoutEnvironment`→
    `HtmlLayoutEnvironment` to avoid colliding with the new property name). Dom + Orchestration + Cli build
    green; Layout arch tests 3/3. Behavior-preserving across a 98-test layout sweep — all 6 failures are
    pre-existing or flaky (`MaxWidth`, `Border_Shorthand`, the 3 Acid3 DOM-removal/cascade tests — all
    stash-A/B-confirmed baseline-failing — plus the isolation-passing `FlexChildren` flake). WPT not re-run.

**Phase 4 prep — decisions (user) + first increment (2026-06-26):** Survey of the residual `Broiler.HTML.*`
coupling drove two decisions: **(1) move `HtmlTag` into `Broiler.Layout`** (self-contained, used 30× for
Name/Attributes/TryGetAttribute/HasAttribute; constructed by the parser + 4 projects — moving ripples there,
sequence carefully); **(2) extend `ILayoutEnvironment` and route host-services through the stored env**
(over the abstract-seam/subclass-split alternative).
- *Env-lifetime refactor done:* `HtmlLayoutEnvironment` is now container-owned (`ctor(IHtmlContainerInt)` +
  `SetGraphics(g)` per pass) and bound to the root at construction in `DomParser` (`root.LayoutEnvironment =
  new HtmlLayoutEnvironment(htmlContainer)` beside `root.ContainerInt = …`), so it's available before the
  first layout pass. `HtmlContainerInt.PerformLayout` now reuses the bound env and just `SetGraphics(g)`.
- *Routed via the stored env:* `CssBox`'s `GetCachedFont`→`env.GetFont` (with `(LayoutFontStyle)(int)st` round-trip
  + `(RFont)` cast), `GetActualColor`→`env.ParseColor`, and `OnImageLoadComplete`'s `RequestRefresh`. Also fixed
  an ICB site the Phase-3 sed missed (`ContainerInt!.ViewportSize`@401, the `!` form → `LayoutEnvironment.ViewportSize`).
  Dom+Orchestration+Cli build green; behavior-preserving (89-test font/colour sweep: only the flaky
  `Acid2_Render` (isolation-pass) + 3 A/B-confirmed pre-existing background/border failures).

**Phase 4 prep — increment 2 done + verified (2026-06-26):** Established the env/container **lockstep** and
converted the root-detection guards. Found a latent bug: the list-item marker box (`CreateListItemBox`, 3227) is
created with a **null parent** and set `_htmlContainer` directly but not `_layoutEnvironment` — so after increment 1
its marker `GetCachedFont`→`env.GetFont` would NRE. Fixed by also propagating `_listItemBox._layoutEnvironment =
LayoutEnvironment` (correctness + lockstep). Audited every `_htmlContainer`/`.ContainerInt` assignment: only the
root (`DomParser`, lockstep with the env) and that list-item site touch a CssBox's container directly; all else is
parent-chain. With lockstep holding, converted the **14 `ContainerInt != null`** guards → `LayoutEnvironment != null`
and `AvoidGeometryAntialias` → a new bool on `ILayoutEnvironment` (adapter forwards). Layout+Cli build green;
behavior-preserving (72-test sweep + 41 List* tests — only flaky `FlexChildren` + pre-existing `MaxWidth`/
`Border_Shorthand`/`SelectListBox`, the last A/B-confirmed baseline-failing).

**ACCURATE remaining `ContainerInt` surface (after increments 1–2):** in `CssBox.cs` — `ReportError` (555, needs
`HtmlRenderErrorType` abstracted into Layout), `CreateImageLoadHandler` (3069, needs `IImageLoadHandler` abstracted),
and `BreakPage` (3583)'s `PageSize`/`MarginTop` (pagination inputs — add to `ILayoutEnvironment`, same pattern as the
ICB inputs); plus the list-item propagation (now sets both container+env) and the property/field definition itself.
In `CssBoxImage.cs` — `RequestRefresh`, `CreateImageLoadHandler`, `AvoidImagesLateLoading`/`AvoidAsyncImagesLoading`;
in `CssLayoutEngine`/`Table` — `ReportError`. **Next easy increment:** `PageSize`/`MarginTop` → env. **Then the two
new abstractions** (`HtmlRenderErrorType`-equivalent enum + `IImageLoadHandler`-equivalent interface in Layout) to
clear `ReportError`/`CreateImageLoadHandler`/image-loading across `CssBox`+`CssBoxImage`+`CssLayoutEngine`.

**Phase 4 prep — increment 3 done + verified (2026-06-26):** Added `PageSize` (SizeF), `MarginTop` (int) and
`ReportLayoutError(message, exception)` to `ILayoutEnvironment` (the moving files only ever report
`HtmlRenderErrorType.Layout`, so a focused method beats dragging the enum into Layout; adapter maps to `.Layout`).
Routed `BreakPage`'s pagination reads and both `ReportError` sites (`CssBox` catch @555, `CssLayoutEngineTable`
@83 via `tableBox.LayoutEnvironment`) through the env. Layout+Dom+Cli green; behavior-preserving (72-test sweep —
only flaky `FixedChild` (isolation-pass) + pre-existing `MaxWidth`/`Border_Shorthand`). **`CssBox.cs` now has a
single remaining `ContainerInt` member access: `CreateImageLoadHandler` @3069.** All remaining `ContainerInt`
coupling is in the **image-loading path**: `CssBox.CreateImageLoadHandler`, and `CssBoxImage`'s `RequestRefresh` +
`CreateImageLoadHandler` + `AvoidImagesLateLoading`/`AvoidAsyncImagesLoading`. This is the entangled increment —
it needs an `IImageLoadHandler`-equivalent interface in Layout AND the `RImage` callback re-typed to an opaque
`object` (the `OnImageLoadComplete(RImage,…)`/`OnLoadImageComplete(RImage,…)` signatures). Do it together with the
`RImage` image-handle re-typing.

**Phase 4 prep — increment 4 (safe subset) done + verified (2026-06-26):** Added `AvoidAsyncImagesLoading` +
`AvoidImagesLateLoading` (bools) to `ILayoutEnvironment`; routed `CssBoxImage`'s two flag reads + its
`RequestRefresh` through the env. Cli green; Acid2 image suite 23/25 (only the 2 A/B-confirmed pre-existing
background failures). **The moving files now have exactly ONE `ContainerInt` member access left:
`CreateImageLoadHandler` (CssBox @3069 for backgrounds, CssBoxImage @40 for `<img>`).**

**FINAL `ContainerInt` increment — `CreateImageLoadHandler` + `IImageLoadHandler` + `RImage` (the 4-project one):**
The full surface (mapped): `IImageLoadHandler` (`internal`, `Broiler.HTML.Core`, `IDisposable`) exposes
`RImage Image`/`RectangleF Rectangle`/`LoadImage(string, Dictionary<string,string>, Uri)`. It threads through
`CssBox._backgroundImageLoadHandlers` (`List<IImageLoadHandler>`, reads `.Image` into `object?[]` at 170/176),
`CssBoxImage._imageLoadHandler`, the callback delegate `ActionInt<RImage, RectangleF, bool>` (passed
`OnImageLoadComplete`/`OnLoadImageComplete`), and the renderer's
`BackgroundImageDrawHandler.DrawBackgroundImage(…, IImageLoadHandler, …)` which reads `.Image` (RImage) 6× to
`DrawImage`/`GetTextureBrush`. Plan: define `ILayoutImageLoader : IDisposable` in Layout
(`object? Image`, `RectangleF Rectangle`, `LoadImage(string, IReadOnlyDictionary<string,string>?, Uri)` — note
`IReadOnlyDictionary` to satisfy the no-mutable-collections arch test); add
`ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete)` to `ILayoutEnvironment`;
have the concrete `ImageLoadHandler`/`IImageLoadHandler` provide the `object? Image` via explicit interface
impl (covariance: `RImage Image` doesn't auto-satisfy `object Image`); re-type the box fields/callbacks to
`ILayoutImageLoader`/`object`; cast `(RImage)` at the renderer paint boundary (`BackgroundImageDrawHandler` +
the replaced-`<img>` paint site) — also re-types `CssRect.Image`/`CssRectImage.Image`/`CssBoxImage.Image`
`RImage`→`object`. Large + crosses Layout/Core/Dom/Rendering; do as a focused step.

**Phase 4 prep — increment 5 (image-loader abstraction) DONE + verified (2026-06-26) → MOVING FILES ARE NOW
`ContainerInt`-SERVICE-FREE.** Key simplifier discovered: the legacy `BackgroundImageDrawHandler.DrawBackgroundImage(IImageLoadHandler)`
is **dead** (no callers — only its own interface-dispatch shim), and the active background paint path already reads
`box.LoadedBackgroundImage` as **`object?`** (via `FragmentTreeBuilder`). So no paint-boundary changes were needed.
Implemented with a **wrapper adapter** (no `Broiler.HTML.Core` edits): new `ILayoutImageLoader : IDisposable` in Layout
(`object? Image`, `RectangleF Rectangle`, `LoadImage(string, IReadOnlyDictionary<string,string>?, Uri)` — `IReadOnlyDictionary`
keeps the no-mutable-collections arch test green); `ILayoutImageLoader CreateImageLoader(Action<object?,RectangleF,bool>)`
on `ILayoutEnvironment`; the adapter's private `LayoutImageLoader` wraps the renderer's `IImageLoadHandler` and adapts the
`RImage`→`object` callback. Re-typed `CssBox._backgroundImageLoadHandlers` (`List<IImageLoadHandler>`→`List<ILayoutImageLoader>`)
and `CssBoxImage._imageLoadHandler`; both `CreateImageLoadHandler` calls → `LayoutEnvironment.CreateImageLoader`; callbacks
`OnImageLoadComplete`/`OnLoadImageComplete` now take `object?` (the latter casts `(RImage)image` to set the inline word — RImage
re-typing deferred). Build green Layout+Dom+Orch+Cli; Layout arch 3/3; behavior-preserving (Acid2 image 23/25 only the 2
A/B-pre-existing bg; broad sweep only flaky `FlexChildren` + pre-existing `MaxWidth`/`Border_Shorthand`).

**STATE: moving files have ZERO `ContainerInt` member accesses + ZERO `IImageLoadHandler`** — every `IHtmlContainerInt`
host-service now flows through `ILayoutEnvironment`. The only residual `ContainerInt` is the property/field definition itself
plus the list-item propagation `_listItemBox._htmlContainer = ContainerInt` (vestigial — nothing reads it now; can be dropped,
keeping only the `_layoutEnvironment` propagation). **Remaining before the file move:** (a) drop the vestigial `ContainerInt`
property/field + `DomParser` `root.ContainerInt =` if truly unread; (b) move `HtmlTag` into Layout; (c) re-type the `RImage`
image handle → `object` (`CssRect.Image`/`CssRectImage.Image`/`CssBoxImage.Image` + the `(RImage)` cast) and `ActualFont`
`RFont`→`ILayoutFont` (renderer casts at paint); plus the smaller `Broiler.HTML.Utils`/`Adapters`/`Core.Entities` bits; then
the file move (`DomUtils`/`CssUtils` travel with the code).

**Phase 4 prep — increment 6 (RImage + RFont handle re-typing) DONE + verified (2026-06-27).** Confirmed the paint
path stores both as opaque `object?` handles (`DisplayList.FontHandle`/`ImageHandle` are `object?`; the raster
backend casts `is RFont`/`is RImage`), and the active path is `FragmentTreeBuilder` (reads `imgBox.Image` and
`ActualFont` as handles) — so **no paint-boundary changes needed**, the runtime objects stay `RFont`/`RImage`.
- *RImage→object:* re-typed `CssRect.Image` (virtual), `CssRectImage.Image`/`_image`, `CssBoxImage.Image` to
  `object`; dropped the `(RImage)` cast in `OnLoadImageComplete`. Renderer-side casts added where external code reads
  the box image as `RImage`: `ContextMenuHandler` (`SaveToFile`/`SetToClipboard`).
- *RFont/ActualFont→ILayoutFont (+ FontStyle→LayoutFontStyle):* re-typed `_actualFont`/`ActualFont` and
  `GetCachedFont` (abstract + `CssBox` override) to `ILayoutFont`; the font-style computation now builds
  `LayoutFontStyle` (was `Broiler.Graphics.FontStyle`); the override is now `=> LayoutEnvironment.GetFont(…)` (no cast/
  conversion). Renderer-side cast added: `SelectionHandler` (`control.MeasureString((RFont)…ActualFont, …)`).
  `ActualFont` is only read for `.Size`/`.Height` (both on `ILayoutFont`). All builds green
  (Dom/Orch/Cli/WPF/Image/Graphics); behavior-preserving (Acid2 image 23/25 pre-existing-only; broad font/layout sweep
  only flaky `Acid2_Render` (isolation-pass) + pre-existing `Border_Shorthand`). **`RImage` and `RFont` are gone from
  the moving files.**

**Remaining `Broiler.HTML.*` in moving files (post-incr-6):** `HtmlTag` (30, MOVE into Layout — ripples to parser+4
projects), `DomUtils` (7, layout helpers move/port — split from its serialization methods), `HtmlConstants` (5) +
`CommonUtils` (4, layout bits `IsAsianCharecter`/`Max`) + `HtmlUtils` (2) → PORT to Layout, `IHtmlContainerInt` (3, the
`ContainerInt` property — read EXTERNALLY by SelectionHandler/DomParser/DomUtils, so a move-time rework not a simple
drop), plus residual `using Broiler.HTML.Adapters` (likely now vestigial — RFont/RImage gone; test-remove later) /
`Core.Core.Dom`/`Core.Entities`/`Core.IR`. **Next:** move `HtmlTag`; port the `Utils` bits.

**Phase 4 prep — increment 7 (Utils ports) DONE + verified (2026-06-27).** New
`Broiler.Layout/Broiler.Layout/LayoutHtmlSupport.cs` ports `HtmlConstants` (A/Hr/Iframe/Img/Href — the 5 layout uses),
`CommonUtils.IsAsianCharecter`/`Max`, and `HtmlUtils.DecodeHtml` (renderer keeps its copies). The heavier
`CommonUtils.ConvertToAlphaNumber` (list-marker numbering — pulls Greek/Roman/Armenian/Hebrew/Hiragana tables) stays
host-side: added `string FormatListMarker(int, string)` to `ILayoutEnvironment` (adapter → the renderer's
`CommonUtils.ConvertToAlphaNumber`); the one call site (`CssBox.CreateListItemBox` @3252) now uses
`LayoutEnvironment.FormatListMarker`. Aliased `HtmlConstants`/`CommonUtils`/`HtmlUtils` → `Broiler.Layout.*` in `CssBox`
(+ `HtmlConstants` in `CssBoxHelper`). Dom+Cli build green; behavior-preserving (Acid3 sweep — ordered-list markers /
word-break / text-decode exercised — only the known pre-existing DOM-removal/cascade + `Border_Shorthand` failures).

**Remaining `Broiler.HTML.*` in moving files (post-incr-7):** `HtmlTag` (30, MOVE into Layout — only `Broiler.HTML.Image`
reads it externally besides the parser; `Broiler.HTML.CSS`'s "HtmlTag" was a false positive = a regex string const),
`DomUtils` (7, layout helpers — `GetPreviousSibling`/whitespace/inline checks — move/port; split from its serialization
methods), `IHtmlContainerInt` (the `ContainerInt` property, read externally → move-time rework), residual
`using Broiler.HTML.Adapters` (test-remove — likely vestigial) / `Core.Core.Dom` / `Core.Entities` / `Core.IR`.
**Next:** move `HtmlTag` (make public, relocate to Layout, add `Broiler.Layout` ref to `Broiler.HTML.Image`, repoint
parser constructors + alias readers).

**Phase 4 prep — increment 8 (Adapters-vestigial removal) DONE + verified (2026-06-27).** With `RFont`/`RImage` gone,
`using Broiler.HTML.Adapters;` is now dead in all moving files — test-removed (0 build errors), then deleted
permanently (incl. the 3 BOM-prefixed files via Edit). Dom+Cli green. **`Broiler.HTML.Adapters` is fully decoupled
from the moving files.**

**`HtmlTag` move — ANALYZED, deferred as a focused step (largest remaining ripple).** Blocker: `HtmlTag.Attributes`
is a `public Dictionary<string,string>` (+ `Dictionary` ctor param) → trips the Layout arch test's no-mutable-collections
rule. Fix is `IReadOnlyDictionary` on the public surface (`Attributes` read-only everywhere — the `tag.Attributes[att]`
in DomParser is an indexer *get*), BUT two consumers forward it to `Dictionary`-typed PUBLIC FACADE APIs
(`HtmlLinkClickedEventArgs(string, Dictionary)`; `IStylesheetLoader.LoadStylesheet(…, Dictionary, …)` which further
forwards into `HtmlStylesheetLoadEventArgs`). Plan (non-breaking): HtmlTag exposes `IReadOnlyDictionary` (ctor accepts
`Dictionary` implicitly), and cast `(Dictionary<string,string>)…HtmlTag.Attributes` at the 2 forward sites
(`HtmlContainerInt:875`, `DomParser:98` — runtime is always a `Dictionary`). Then: create public
`Broiler.Layout/HtmlTag.cs`, delete `Broiler.HTML.Dom/HtmlTag.cs`, add `Broiler.Layout` ref to `Broiler.HTML.Image`
(Orchestration gets it via Dom transitively, but add explicit), alias `using HtmlTag = Broiler.Layout.HtmlTag;` in the
~12 referencing files (6 moving + `HtmlParser` + `DomUtils` + `DomParser` + `HtmlContainerInt` + `FragmentTreeBuilder` +
`Image/HtmlContainer`). ~15-18 edits across 3 projects — do as a focused step.

**Phase 4 prep — increment 9 (HtmlTag move) DONE + verified (2026-06-27).** Executed the plan above. Created public
`Broiler.Layout/Broiler.Layout/HtmlTag.cs` (`Attributes` as `IReadOnlyDictionary<string,string>?` — passes the arch
test; ctor still accepts `Dictionary` implicitly); deleted `Broiler.HTML.Dom/HtmlTag.cs`; aliased
`using HtmlTag = Broiler.Layout.HtmlTag;` in all 13 referencing files (6 moving + `HtmlParser` + `HtmlParserDump` +
`DomUtils` + `Image/HtmlContainer` + `HtmlContainerInt` + `FragmentTreeBuilder` + `DomParser`); cast
`(Dictionary<string,string>)…HtmlTag.Attributes` at the two facade-forward sites (`HtmlContainerInt:875`,
`DomParser:98`). Transitive `Broiler.Layout` ref flows to Orchestration/Image (no explicit csproj ref needed). All
projects build green (Dom/Orch/Image/Cli/WPF); Layout arch tests 3/3 (IReadOnlyDictionary not flagged);
behavior-preserving (Acid3 58/59 + Acid2 23/25 — only flaky `FixedChild` + pre-existing). **`HtmlTag` (the biggest
single coupling, 30 refs) is resolved — it now lives in `Broiler.Layout`.**

**Remaining `Broiler.HTML.*` in moving files (post-incr-9):** `DomUtils` (7, `Broiler.HTML.Dom.Utils` — layout helpers
`GetPreviousSibling`/`GetPreviousInFlowSibling`/`IsBoxHasWhitespace`/`ContainsInlinesOnly`; move/port, split from its
serialization methods which stay), `IHtmlContainerInt` (the `ContainerInt` property — read externally → move-time
rework), small `Core.Core.Dom`/`Core.Entities`/`Core.IR` types (`IBorderRenderData`/`IBackgroundRenderData` on
`CssBoxProperties`, etc.), and `using Broiler.HTML.Utils;` (now likely vestigial after the Utils ports — test-remove
like Adapters). **Next:** test-remove vestigial `Broiler.HTML.Utils`; then `DomUtils` split; then the `ContainerInt`
property + `Core.*` interfaces; then the file move.

**Phase 4 prep — increment 10 (Utils-vestigial + DomUtils split) DONE + verified (2026-06-27).**
- *`using Broiler.HTML.Utils;` removed* from all moving files (vestigial after the Utils ports — test-removed, 0 errors,
  deleted). **`Broiler.HTML.Utils` decoupled from moving files.**
- *`DomUtils` split:* extracted the 5 pure `CssBox`-tree helpers (`ContainsInlinesOnly`, `GetPreviousSibling`,
  `GetPreviousInFlowSibling`, `GetPreviousContainingBlockSibling` [internal dep], `IsBoxHasWhitespace`) into a new moving
  file `Broiler.HTML.Dom/Utils/LayoutBoxUtils.cs` (internal static, `Broiler.HTML.Dom` ns, `CssConstants` via Broiler.CSS
  alias). Deleted them from `DomUtils.cs` (which keeps its renderer-only serialization). Repointed the 4 externally-called
  ones at all call sites: 3 moving files (`CssBox`/`CssLayoutEngine`/`CssBoxHr`) + `DomParser` (Orchestration) +
  `DomUtils` itself (one internal `IsBoxHasWhitespace` call in its serialization). Dom+Orch+Cli green; behavior-preserving
  (layout sweep — only flaky `FlexChildren` + pre-existing `MaxWidth`/`Border_Shorthand`). **`DomUtils` decoupled from
  moving files.**

**FINAL remaining `Broiler.HTML.*` in moving files — the move-time / inverse-direction couplings:**
- `IHtmlContainerInt` (the `CssBox.ContainerInt` property, `Broiler.HTML.Core`) — read EXTERNALLY (`SelectionHandler`,
  `DomParser`, `DomUtils`) as the box→container link; needs a move-time rework (renderer obtains the container another
  way, or the property is dropped/relocated).
- `IBorderRenderData`/`IBackgroundRenderData` (`Broiler.HTML.Core.IR`) — `CssBoxProperties` IMPLEMENTS these so the
  renderer reads border/background geometry off the box; either move the interfaces into `Broiler.Layout` (renderer
  references them there) or expose the data differently.
- `using Broiler.HTML.Dom.Utils;` is now only for the moving peers (`CssUtils`/`LayoutBoxUtils`) — resolves after the move.
  Likely-vestigial `Core.Core.Dom`/`Core.Entities` imports — test-remove like Adapters/Utils.
These are the inverse-direction couplings (renderer reading the box); resolve them, then **do the file move** (relocate
the 13 + `LayoutBoxUtils` into `Broiler.Layout`, repoint the `InternalsVisibleTo` consumers, flip namespaces).

**Phase 4 prep — increment 11 (vestigial Core.* removal) DONE + verified (2026-06-27).** `using Broiler.HTML.Core.Core.Dom;`
(only `CssUnit`, now Broiler.CSS-aliased + `Border`, unused) and `using Broiler.HTML.Core.Entities;` (only `HtmlRenderErrorType`,
now via env) were vestigial in the moving files — test-removed (0 errors), deleted. Dom+Cli green.

**PHASE 4 PREP ESSENTIALLY COMPLETE.** The moving files are now decoupled from EVERY `Broiler.HTML.*` namespace except the
two intrinsic move-boundary couplings + the moving peers:
- `using Broiler.HTML.Dom.Utils;` — only for the moving peers `CssUtils`/`LayoutBoxUtils` (they relocate together; namespace
  flips at the move).
- `using Broiler.HTML.Core;` — the `CssBox.ContainerInt` property (`IHtmlContainerInt`). **Inverse-direction:** the renderer
  SETS it (`DomParser:49`) and READS it (`SelectionHandler`, `DomParser`, `DomUtils`) as the box→container link; the layout
  algorithm itself no longer touches it (0 member accesses). Resolution at the move: re-type to `object` (renderer casts at
  the ~9 read sites) OR eliminate it (external readers obtain the container another way — they're all renderer code that
  already has it).
- `using Broiler.HTML.Core.IR;` — `CssBoxProperties` IMPLEMENTS `IBorderRenderData`/`IBackgroundRenderData`. **Inverse-direction
  + legacy:** these (BCL-only string/double/Color contracts) are consumed ONLY by the dead `BordersDrawHandler`/
  `BackgroundImageDrawHandler` (the active `FragmentTreeBuilder`/`PaintWalker` paint path does NOT use them). Resolution at the
  move: relocate the two interfaces into `Broiler.Layout` (Core/Rendering reference them there) — or drop the implementation if
  the legacy paint handlers are confirmed removable (Phase 7 territory).

**Remaining = Phase 4 proper (the file move):** handle the 2 inverse-direction couplings above as part of relocating the 13
files + `LayoutBoxUtils` into `Broiler.Layout` (set `RootNamespace`, flip `namespace Broiler.HTML.Dom` → `Broiler.Layout`,
repoint the `InternalsVisibleTo`/8 consumers, move the `Dom.Utils` peers' namespace too). The forward-direction decoupling is
done: moving files reference only `Broiler.CSS`/`Broiler.CSS.Dom`/`Broiler.Dom`/`Broiler.Layout` + BCL for all their logic.

**Phase 4 prep — increment 12 (the two inverse-direction couplings) DONE + verified (2026-06-27) → PHASE 4 PREP COMPLETE.**
- *Render-data interfaces (`Core.IR`):* the legacy `BordersDrawHandler`/`BackgroundImageDrawHandler` (+ their
  `IBorderRenderData`/`IBackgroundRenderData` contracts) are **dead** (no external callers; nothing passes a `CssBox`; active
  paint = `FragmentTreeBuilder`/`PaintWalker`). So just dropped `: IBorderRenderData, IBackgroundRenderData` from
  `CssBoxProperties`. The other `Core.IR` type it used — the `BoxKind` enum — was **relocated to `Broiler.Layout/BoxKind.cs`**
  (public; `ComputedStyle`/`DomParser` repointed — `DomParser` via `using BoxKind = …` to dodge an `HtmlConstants` ambiguity;
  `Broiler.HTML.Core` reaches it transitively via Adapters→Layout, no new ref/cycle). `using Broiler.HTML.Core.IR;` removed.
- *`ContainerInt` (`IHtmlContainerInt`):* re-typed `CssBox.ContainerInt` + `_htmlContainer` to **`object`** (layout never reads
  it); removed `using Broiler.HTML.Core;` from the 3 files; cast `(IHtmlContainerInt)` at the 8 external read sites (`DomUtils`
  6, `DomParser` 2). Caught a missed layout reader — `CssRect.BreakPage` used `OwnerBox.ContainerInt.PageSize`/`MarginTop` →
  routed through `OwnerBox.LayoutEnvironment`. Build green Dom/Core/Orch/Cli; Layout arch 3/3; behavior-preserving (only flaky
  `Acid2_Render`/`Acid2_IntroSection` [isolation+baseline pass] + pre-existing `Border_Shorthand`/bg).

**✅ PHASE 4 PREP COMPLETE — forward-direction decoupling 100%.** Moving files (13 + `LayoutBoxUtils`) reference ONLY
`Broiler.CSS`/`Broiler.CSS.Dom`/`Broiler.Dom`/`Broiler.Layout` + BCL + the moving peers (`CssUtils`/`LayoutBoxUtils`, ns
`Broiler.HTML.Dom.Utils` — flips at the move). No renderer coupling remains.

**NEXT = PHASE 4 PROPER (physical file move):** relocate the 13 + `LayoutBoxUtils` + `CssUtils` into the `Broiler.Layout`
project (move the `.cs`; flip `namespace Broiler.HTML.Dom`/`.Utils` → `Broiler.Layout`; drop them from `Broiler.HTML.Dom.csproj`).
Then cross-assembly visibility: `CssBox` etc. are `internal` → add `[InternalsVisibleTo]` from `Broiler.Layout` to the 8 consumer
assemblies (mirror `Broiler.HTML.Dom`'s IVT set); renderer `using Broiler.HTML.Dom;` resolves to the relocated types via
`renderer → Dom → Layout` transitively or an added `using Broiler.Layout;`. Verify full `Broiler.slnx` + WPT/Acid gates.

**✅ PHASE 4 PROPER — THE FILE MOVE DONE + verified (2026-06-27).** Moved **16 files** into
`Broiler.Layout/Broiler.Layout/Engine/`: the 13 layout files + `LayoutBoxUtils` + two same-namespace move-blockers
found via the cycle check — `VerticalFlowPrototype` (self-contained feature flag) and `ISelectionHandler` (interface
over `CssRect`). Pre-move checks: nullable warnings are NoWarn'd solution-wide (root `Directory.Build.props`), the lone
`global using FontStyle` is unused by the movers, no remaining cycle. **Kept `namespace Broiler.HTML.Dom`/`.Utils`** so
the renderer needs no `using` changes (types resolve from `Broiler.Layout.dll` via `renderer→Dom→Layout` transitively).
Added `[InternalsVisibleTo]` to `Broiler.Layout.csproj` for `Broiler.HTML.Dom` (staying files access the moved internals)
+ the full consumer set (incl. `DevConsole`/`DevConsole.Tests`, initially missed → caught by the slnx build). The
HtmlTag move had also left `DevConsole.Tests` needing a `using HtmlTag = Broiler.Layout.HtmlTag;` alias (only the full
slnx build surfaced it — added). **Result:** `Broiler.Layout` builds clean with the moved files (0 errors); the staying
`Broiler.HTML.Dom` + all consumers (Orchestration/Image/WPF/HtmlBridge/Cli) build clean; **full `Broiler.slnx` green**;
Layout arch 3/3 (moved types are `internal` → off the public surface); behavior-preserving (97-test + 72-test sweeps —
only the known flaky `FixedChild`/`Acid2_Render` + pre-existing `MaxWidth`/`Border_Shorthand`/bg). The layout engine now
lives in `Broiler.Layout`; the renderer keeps the `HtmlLayoutEnvironment` adapter, `DomUtils` serialization, `HtmlParser`,
`BoxTreeVisitor`, `HtmlSerializer` etc.

**Post-move polish DONE + verified (2026-06-27):**
- *Namespace flip:* the 16 `Engine/` files flipped `namespace Broiler.HTML.Dom`/`.Utils` → `Broiler.Layout` (and their
  now-redundant `using Broiler.HTML.Dom.Utils;` + `using …=Broiler.Layout.*` aliases removed). Added `using Broiler.Layout;`
  to the staying `Broiler.HTML.Dom` files that reference the moved types + the ~19 consumer files (sed after each
  `using Broiler.HTML.Dom;`). Disambiguated the renderer files that use the *full* `Broiler.HTML.Utils` `HtmlConstants`/
  `CommonUtils`/`HtmlUtils` against `Broiler.Layout`'s subsets via `using … = Broiler.HTML.Utils.…;` aliases (HtmlParser,
  DomUtils, DomParser, HtmlContainerInt, Orchestration). Full `Broiler.slnx` green; Layout arch 3/3; behavior-preserving.
- *Identical-copy dedup:* deleted the renderer's `Broiler.HTML.CSS.Core.Parse.RegexParserUtils` (only `CssParser` used it;
  `Broiler.HTML.CSS` already refs `Broiler.CSS`) and `Broiler.HTML.Utils.CssConstants` (10 files across CSS/Dom/Orchestration/
  Rendering/Utils → aliased to `Broiler.CSS.CssConstants`; added a `Broiler.CSS` ref to `Broiler.HTML.Utils` for its
  `CommonUtils`). Both were byte-identical to the `Broiler.CSS` versions. Full `Broiler.slnx` green; behavior-preserving.
  **Not deduped (inherent):** `CssLength`/`CssValueParser` (renderer's carry colour parsing the layout copies lack),
  `CssUnit` (renderer's `Pixels`/`Ems` vs `Broiler.CSS`'s `Px`/`Em`), `HtmlConstants`/`CommonUtils`/`HtmlUtils` (Layout holds
  layout-only subsets and cannot reference the renderer to share one copy).

**Other `Broiler.HTML.*` couplings before the move (NOT Phase 3):** the moving files still carry these,
all Phase-4 concerns:
- `ContainerInt` host-services (11 calls): `GetFont`/`ParseColor` (behind the abstract `GetCachedFont`/
  `GetActualColor` seam — could route through the now-stored `LayoutEnvironment`, but watch the
  null-before-layout lifetime since font/colour can resolve pre-layout), `RequestRefresh`, `ReportError`,
  `CreateImageLoadHandler`, `AvoidImagesLateLoading`/`AvoidAsyncImagesLoading` (renderer glue; §2.2 "stay
  behind", or split into a renderer subclass at the move).
- `Broiler.HTML.Adapters` (`RFont`/`RImage`; e.g. `ActualFont` is still `RFont`-typed → would become
  `ILayoutFont`), `Broiler.HTML.Core.Entities`/`Core.Core.Dom`/`Core.IR`, `Broiler.HTML.Utils` (now only
  `HtmlUtils`/`CommonUtils`), `Broiler.HTML.Rendering`.
**Scope:** Extract the renderer's CSS box-model and layout engine out of
`Broiler.HTML.Dom` into a standalone `Broiler.Layout` component that consumes a
computed style (from `Broiler.CSS.Dom`) over the canonical `Broiler.Dom` tree and
produces box geometry, without depending on a graphics backend, the JavaScript
bridge, networking, or the HTML facade.

## 1. Decision

**Yes — create `Broiler.Layout`.** This is the natural next extraction after
`Broiler.CSS` / `Broiler.CSS.Dom` (see [`broiler-css-component.md`](broiler-css-component.md)):
CSS owns syntax/cascade/computed style; `Broiler.Layout` owns formatting and box
geometry; the HTML renderer keeps painting, resource loading, and platform glue.

Per project direction at this stage, the **cons** in the usual pro/con framing for
this extraction — "the component could absorb too much and become another monolith"
and "extraction may be premature" — are **explicitly out of scope**. They are
recorded here only so the decision is traceable; they do not gate the work. The
mitigations are the same ones the CSS extraction already proved: an ownership table,
forbidden-dependency architecture tests, and a dual-run migration.

The top-level component name is `Broiler.Layout` (alongside `Broiler.CSS` and
`Broiler.DOM`), matching the requested casing.

## 2. Scope

### 2.1 Moves into `Broiler.Layout`

The ~9,700 lines of box/layout code currently in
`Broiler.HTML/Source/Broiler.HTML.Dom/`:

| File | Lines | Role |
|---|---:|---|
| `CssBox.cs` | 4,222 | Box tree + block/inline layout, positioning, containing blocks |
| `CssBoxProperties.cs` | 1,781 | ~80 CSS box properties (string storage) + `Actual*` used-value getters |
| `CssLayoutEngine.cs` | 1,624 | Block/inline flow, line breaking, image sizing |
| `CssLayoutEngineTable.cs` | 997 | Table layout |
| `CssBoxHelper.cs` | 586 | Box-tree construction |
| `CssLineBox.cs` | 170 | Inline line rectangles |
| `CssRect.cs` / `CssRectWord.cs` / `CssRectImage.cs` | ~150 | Inline word/image rectangles |
| `CssBoxImage.cs` / `CssBoxHr.cs` | ~150 | Replaced-element / `<hr>` boxes |
| `Utils/CssUtils.cs` | 797 | Whitespace/length/property helpers |

Entry point: `CssBox.PerformLayout(RGraphics g)` (`CssBox.cs:519`).
`CssBox` is `internal`; reached by 8 assemblies via `InternalsVisibleTo`.

### 2.2 Stays with consumers

| Responsibility | Owner after extraction |
|---|---|
| Cascade / computed style | `Broiler.CSS.Dom` (already extracted) |
| Painting, borders, backgrounds, images, SVG | `Broiler.HTML.Rendering` + backends |
| Hit-testing, selection, JS box bindings | `Broiler.HtmlBridge.*` |
| Resource loading (fonts, images, stylesheets) | HTML orchestration / host |
| HTML parsing, `<style>`/`<link>` discovery | `Broiler.HTML.Orchestration` |

## 3. Dependency target

```
Broiler.Layout
 ├─ Broiler.CSS.Dom   (CssComputedStyle — the layout input)
 ├─ Broiler.CSS       (CssLength/units/constants used during used-value resolution)
 ├─ Broiler.Dom       (canonical element link, SourceElement)
 └─ ILayoutEnvironment (host-injected: text metrics, image intrinsics, color parse)
```

**Forbidden:** `Broiler.HTML.*` facade, `Broiler.HtmlBridge.*`, `Broiler.JavaScript.*`,
concrete `Broiler.Graphics` backends, WPF/Image/network/filesystem. Enforced by
architecture tests, mirroring `CssDomArchitectureTests`.

## 4. The coupling to break (the design problem)

Layout currently reaches platform/renderer services directly. These become an
injected abstraction at layout time — the same move the CSS extraction made when it
replaced `IColorResolver` + font-availability with explicit environment inputs.

| Today | Used for | Becomes |
|---|---|---|
| `RGraphics.MeasureString(text, RFont)` | inline width/height, line breaking | `ILayoutEnvironment.MeasureText(font, text)` |
| `RGraphics.GetWhitespaceWidth` / font metrics | word spacing, baselines | `ILayoutEnvironment` font metrics |
| `RImage.Width/Height/HasIntrinsicRatio` | replaced-element sizing | `ILayoutEnvironment.GetImageIntrinsics(handle)` |
| `IHtmlContainerInt.GetFont` / `ParseColor` / `RequestRefresh` | font resolution, color parse, invalidation | `ILayoutEnvironment` (font + color), host callback |
| `System.Drawing` `Color`/`RectangleF`/`PointF`/`SizeF` | box geometry, colors | kept initially (intrinsic to the box model); a `Broiler.Layout` geometry primitive set is a later option |

`RGraphics` itself is already backend-agnostic (abstract base in
`Broiler.HTML.Adapters`); the goal is to depend on a *narrow layout-metrics
interface* rather than the full adapter/container surface.

## 5. Input seam — depends on Phase 5

Phase 5 makes the renderer project an immutable `CssComputedStyle` onto
`CssBoxProperties` (the `GetCascadedStyle` → `CssUtils.SetPropertyValue` path).
That projection is exactly the boundary `Broiler.Layout` consumes: layout receives
already-cascaded/inherited style and resolves only *used* values (containing-block
%, font metrics, intrinsic sizes). **This extraction should start only after the
Phase 5 flag is flipped**, so layout has a single, principled style input and the
legacy `CssData`/`CssBlock` cascade no longer writes into `CssBoxProperties`.

## 6. Phased migration (mirrors the CSS extraction)

0. **Guard.** Architecture tests freezing `Broiler.HTML.Dom`'s current references and
   the `InternalsVisibleTo` surface; characterization of layout outputs (box rects)
   for representative pages, to distinguish movement from behavior change.
1. **Create `Broiler.Layout` + tests.** Introduce the project; define
   `ILayoutEnvironment`; no code moved yet (arch tests forbid Broiler.HTML refs).
2. **Abstract the environment.** Replace direct `RGraphics`/`RImage`/`IHtmlContainerInt`
   use in the layout files with `ILayoutEnvironment`, adapted by a thin renderer-side
   implementation. Dual-run: renderer still owns the files.
3. **Adopt the computed-style input.** Layout takes `CssComputedStyle` (post Phase 5)
   instead of reading `CssData`-assigned fields; keep `Actual*` used-value resolution.
4. **Move the files.** Relocate `CssBox*`/`CssLayout*`/`CssRect*`/`CssLineBox`/`CssUtils`
   into `Broiler.Layout`; repoint the 8 consumers; keep the public/`internal` surface.
5. **Cleanup.** Trim now-unneeded `InternalsVisibleTo`; update architecture/API docs;
   remove dead renderer glue.

## 7. Verification

- Architecture tests: `Broiler.Layout` references only `Broiler.CSS`, `Broiler.CSS.Dom`,
  `Broiler.Dom`, and BCL; no graphics/JS/HTML-facade leak.
- Layout characterization (box-rect) parity across Acid2/Acid3 and the WPT pixel
  subset (`tests/wpt`), dual-run before each consumer cutover.
- Full `Broiler.slnx` build green; renderer pixel + WPT gates within the documented
  baseline.

## 8. Non-goals (initial)

- No painting, compositing, image decode, SVG raster, or font shaping moves.
- No new public NuGet package or Git submodule before the in-repo migration proves
  the API.
- No layout-algorithm rewrite — this is an extraction, not a re-implementation.

# Architectural Investigation: DOM, HTML, Layout, and CSS Responsibilities

**Date / source recheck:** 2026-10-15  
**Status:** Source-verified investigation; proposed changes are not implemented by this document.  
**Scope:** The local checkout of `Broiler.DOM`, `Broiler.HTML`, `Broiler.Layout`, and `Broiler.CSS`. This is not a certification of standards compliance or a full renderer/CI test result. See the [transition roadmap](../dom-html-transition-roadmap.md) for implementation gates.

> **Later status (2026-10-15):** the roadmap's Waves 1, 2 and 4 have since been implemented on branches (see its [implementation record](../dom-html-transition-roadmap.md#7-implementation-record)), and `DomParser` is now `LayoutTreeBuilder`. The sections below describe the code as this recheck found it, before those changes.

## 1. Findings

The main architectural conclusion holds: `Broiler.HTML.Orchestration.Parse.DomParser` coordinates construction and styling of the layout box tree. It delegates HTML parsing to `Broiler.Dom.Html.HtmlDocumentParser` through `Broiler.HTML.Dom.Parse.HtmlParser`; it is not an independent HTML tokenizer or DOM tree builder. `LayoutTreeBuilder` would describe its role more clearly.

`CssBox` and the core layout algorithms live in `Broiler.Layout`. However, the renderer still supplies intrinsic sizes, form-control fallback geometry, box normalization, and backend measurement services. Saying that *all* layout calculations have left `Broiler.HTML` overstates the separation.

The proposed DOM reuse is useful, but several helpers are not behaviorally interchangeable with their renderer counterparts. In particular:

- Parsed template contents are already absent from the projected box tree; the claimed template-base regression is not established on this path.
- The renderer's box-based base-URL scan does not stop at projected nested browsing contexts, so a sub-document's `<base>` can leak outward (from reading the source; see §5).
- `InheritRowAlignment` already supports `td`, `th`, and `tr`; only `SetForAllCells` uses the fragile depth-based traversal.
- `GetDefaultValue` does not supply rendered Submit/Reset labels, and `GetSize` does not supply the renderer's four-row fallback.
- HTML export carries selection and style policies that cannot be replaced by a direct traversal of `CssBox.Boxes`.

## 2. Current Component Boundaries

| Component | Current responsibility | Boundary / qualification |
| :--- | :--- | :--- |
| `Broiler.DOM` (`Broiler.Dom`, `Broiler.Dom.Html`) | DOM nodes, HTML tokenization/tree building, document/form/table/select queries, HTML serialization | No renderer layout geometry or painting. The existence of a shared helper does not by itself prove complete WHATWG conformance. |
| `Broiler.CSS` / `Broiler.CSS.Dom` | CSS syntax, values, selectors, and cascade resolution against DOM elements | No layout-box or graphics dependency. `CssStyleEngine` supplies cascaded declarations; renderer inheritance, projection, and used-value calculation remain separate work. |
| `Broiler.Layout` | `CssBox`, `CssLineBox`, `CssRect`, block/inline/flex/grid/table layout, sizing and positioning | No HTML tokenizer or concrete painting backend. It does depend on graphics primitives and accepts measurement through `ILayoutEnvironment`. |
| `Broiler.HTML` | DOM-to-box projection, stylesheet loading, cascade orchestration, presentational hints, generated boxes, renderer-specific sizing/normalization, display-list generation and painting | Consumes the DOM, CSS, and layout components; it still contains some HTML query duplication and renderer-specific layout preparation. |

Evidence: [HTML projection](../../Broiler.HTML/Source/Broiler.HTML.Dom/Parse/HtmlParser.cs), [box-tree orchestration](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/Parse/DomParser.cs), [cascade integration](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/Parse/SharedRendererCascade.cs), [layout project dependencies and friend assemblies](../../Broiler.Layout/Broiler.Layout/Broiler.Layout.csproj), and [renderer layout environment](../../Broiler.HTML/Source/Broiler.HTML.Dom/LayoutEnvironment.cs).

`Broiler.Layout` grants `InternalsVisibleTo` access to `Broiler.HTML.Dom` and `Broiler.HTML.Orchestration`, among others (`Broiler.HTML.Image`, `Broiler.HtmlBridge.Dom`, and several tools and tests in this repository). The grant comes from the assembly owning the internals. Across these component repositories, dependencies are pinned NuGet packages, while projects within a component use project references. For example, [HTML.Core](../../Broiler.HTML/Source/Broiler.HTML.Core/Broiler.HTML.Core.csproj) references the `Broiler.Layout` package and [HTML.Dom](../../Broiler.HTML/Source/Broiler.HTML.Dom/Broiler.HTML.Dom.csproj) references `Broiler.Dom.Html`. Editing the sibling DOM checkout does not automatically change the dependency exercised by HTML tests.

## 3. What `DomParser` Actually Does

The legacy name reflects the renderer's box-tree terminology. Its current entry points are two `GenerateCssTree` overloads: one accepts an HTML string, the other a `DomDocument`. Both obtain initial boxes from `HtmlParser` and then call `PrepareCssTree`.

```mermaid
sequenceDiagram
    participant HC as HtmlContainerInt
    participant DP as DomParser (HTML.Orchestration)
    participant HP as HtmlParser (HTML.Dom)
    participant DOM as HtmlDocumentParser (DOM)
    participant SC as SharedRendererCascade (HTML.Orchestration)
    participant CSS as CssStyleEngine (CSS.Dom)
    participant L as CssBox / layout engine (Layout)
    HC->>DP: GenerateCssTree(html or document)
    DP->>HP: ParseDocument(html or document)
    opt HTML string input
        HP->>DOM: ParseDocument(html)
        DOM-->>HP: Parsed document
    end
    HP->>HP: Project canonical nodes into boxes
    HP-->>DP: Initial CssBox root
    DP->>DP: Apply base URL; bind layout environment; collect stylesheets
    DP->>SC: BuildEngine / BuildAuthorEngine
    SC->>CSS: Configure origin-aware cascade
    CSS-->>SC: Cascaded declarations
    SC-->>DP: Engines for style projection
    DP->>DP: Inherit styles; apply hints and cascade; generate boxes
    DP->>L: Blockify flex/grid items; generate anonymous table boxes
    DP->>DP: Renderer box normalization and fallback sizing
    DP-->>HC: Prepared box tree
    HC->>L: Perform layout (later pipeline stage)
```

The main responsibilities in [DomParser.cs](../../Broiler.HTML/Source/Broiler.HTML.Orchestration/Parse/DomParser.cs) are:

- **Environment and resource setup:** `DocumentBaseUrl.Apply`, `HtmlLayoutEnvironment`, and stylesheet collection before cascade projection.
- **Cascade coordination:** `SharedRendererCascade` is an HTML orchestration helper that constructs `Broiler.CSS.Dom.CssStyleEngine` instances. `Broiler.Layout.Engine.CssStyleRecalc.Warm` precomputes cascade results. These are not all types owned by `Broiler.CSS.Dom`.
- **Presentational hints:** `TranslateAttributes`, `TranslateAlign`, and `InheritRowAlignment` update box properties. `_presentationalHints` and the author-only engine protect recorded hints from UA defaults while allowing author declarations to override them; they do not insert a new hint stylesheet into the shared cascade.
- **Generated content:** `::before` / `::after` boxes, bridge-marked native backdrops, and summary disclosure markers. `SetTextSelectionStyle` reads `::selection` declarations into container colors; it does **not** create a selection box.
- **Normalization and sizing:** anonymous text handling, inline/block splitting, replaced-element preparation, SVG sizing, and fallback boxes for progress/meter and multiple-select controls. Some normalization is already delegated to `FlexGridItemBlockification` and `AnonymousTableBoxes` in Layout.

`SharedRendererCascade.ProjectCascadedStyle` projects cascade-resolved longhands through renderer setters. Its own documentation explicitly distinguishes these from a complete computed-style model with inheritance and initial-value backfill. The sequence therefore must not label its output simply as “Computed Styles.”

## 4. Layout Ownership and Remaining Renderer Work

The concrete box types and layout algorithms are under [Broiler.Layout/Broiler.Layout/Engine](../../Broiler.Layout/Broiler.Layout/Engine). `Broiler.HTML` constructs these types and later paints their results.

The boundary still has deliberate renderer contributions:

- `HtmlLayoutEnvironment` obtains font measurements and image intrinsic dimensions through renderer adapters.
- `DomParser.ApplySvgReplacedSizing`, media/canvas preparation, and progress/select fallbacks supply dimensions before core layout.
- `DomParser` still performs several box-tree corrections itself.

These facts support renaming the coordinator, but not describing the whole migration as finished or moving every renderer-specific sizing decision into DOM. DOM queries should describe document semantics; renderer policy still decides how those semantics become boxes and pixels.

## 5. Verified Reuse Opportunities and Constraints

### Void-element classification

[HtmlUtils.IsSingleTag](../../Broiler.HTML/Source/Broiler.HTML.Core/Utils/HtmlUtils.cs) uses a case-sensitive `List<string>.Contains`. [HtmlSerializer.VoidElements](../../Broiler.DOM/Broiler.Dom.Html/HtmlSerializer.cs) exposes the shared set from [HtmlElementNames](../../Broiler.DOM/Broiler.Dom.Html/HtmlElementNames.cs).

This removes duplicated classification, but the sets differ: the legacy renderer includes `basefont` and `isindex`, which the shared set does not; the shared set also handles names/case differently. The shared set omits `basefont`, `bgsound`, and `keygen` from the standard's serialization-void list, so it is not a complete conformance reference. See [WHATWG fragment serialization](https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments). Treat compatibility spellings explicitly, and do not claim a measured performance improvement from this small fixed-size lookup.

The dependency question is smaller than it looks. `HtmlUtils` is `internal` to `HTML.Core`, which has no direct `Broiler.Dom.Html` reference, but its only caller is `HtmlParser.AppendCanonicalNode` in `HTML.Dom`, which does. Switching that call site and deleting the helper therefore needs no new package reference and changes no public API. The classification's reach is wider than export, though: it becomes `HtmlTag.IsSingle`, which `DomUtils` export (end tags and `/>`), `HtmlParserDump`'s `isSingle` JSON, and `Broiler.Layout`'s inline-SVG serialization (`FragmentTreeBuilder.SerializeSvgBox`) all read.

### Progress / meter ratios

`DomParser.ResolveProgressValueRatio` and its numeric parsing duplicate [HtmlElementQueries.ResolveProgressLikeValueRatio](../../Broiler.DOM/Broiler.Dom.Html/HtmlElementQueries.cs). Delegate for source-backed elements, passing the normalized `meter` or `progress` tag and defining handling for synthetic boxes without a `SourceElement`.

Migrating this method alone does not remove `DomParser.ReadNumericAttribute`: `CorrectSelectMultipleBoxes` also calls it for `size`. The same parsing rule is public as `HtmlElementQueries.ReadNumericAttribute(DomElement, string, double)`, so both callers can move to it for source-backed boxes.

The shared ratio helper reflects the existing approximation, including acceptance of nonfinite doubles: `value="NaN"` yields a NaN ratio, because `Math.Clamp` passes NaN through, and `value="Infinity"` yields 1. The renderer's copy behaves the same way. Sharing the helper is not evidence that every progress/meter edge case conforms to the standard.

### Document base URL

[DocumentBaseUrl.Resolve](../../Broiler.HTML/Source/Broiler.HTML.Dom/Parse/DocumentBaseUrl.cs) recursively scans boxes and uses a return value equal to the fallback as its “nothing found” signal. Thus a first base resolving to the fallback can be skipped in favor of a later base. It also continues after an unresolvable candidate.

[HtmlDocumentQueries.GetEffectiveBaseHref](../../Broiler.DOM/Broiler.Dom.Html/HtmlDocumentQueries.cs) instead returns the first **nonblank, trimmed attribute string**, or null. It does not return a resolved `Uri`, validate schemes, or implement all document-base semantics. Both implementations need an explicit decision for empty/invalid first candidates. The standard selects the first base carrying an `href`, not the first base that changes the fallback URL. See [WHATWG document base URLs](https://html.spec.whatwg.org/multipage/urls-and-fetching.html#document-base-url).

The earlier assertion that parsed `<base>` elements inside `<template>` affect this renderer is unsupported: [DomElement.TemplateContents](../../Broiler.DOM/Broiler.Dom/DomElement.cs) holds them outside `ChildNodes`, and `HtmlParser.AppendCanonicalNode` walks `ChildNodes`. The canonical DOM query relies on the same separation. Bridge/custom DOM representations require their own coverage.

Nested browsing contexts are a different matter. `Resolve` and `Rebase` walk every box, including the sub-viewport subtrees that `HtmlParser` projects under an `<iframe>`/`<object>`/`<frame>` (`CssBox.IsNestedBrowsingContextRoot`). As written, a `<base>` inside a projected sub-document can become the outer document's base when the outer document declares none earlier in tree order, and the outer base is stamped onto the nested boxes. This finding came from reading the source; the Wave 1 implementation later reproduced it with a resolver-supplied sub-document, in a test that fails on this code. A resolver-supplied content document is not part of the canonical tree, so `GetEffectiveBaseHref` would not see it. A legacy in-tree `#subdoc-root` subtree is part of that tree, so the shared query needs the same boundary decision.

A migration must retain URI resolution, `CssBox.BaseUrl` propagation, `HtmlContainerInt.DocumentBaseUrl` behavior, and the resource-log path in [HtmlParserDump](../../Broiler.HTML/Source/Broiler.HTML.Dom/Parse/HtmlParserDump.cs). Both string and bound-document inputs, rebuilds, and nested browsing contexts matter.

### Table traversal and hints

`SetForAllCells` has an explicit `td` check at its second level and an unfiltered third-level branch. For a section/row/cell structure, that third branch can reach **both** `td` and `th`. For direct table/row/`th` structures, it can act on the header's children instead of the header. The problem is fragile structural traversal and missing filtering, not universal omission of `th`.

[HtmlElementQueries.CollectTableRows](../../Broiler.DOM/Broiler.Dom.Html/HtmlElementQueries.cs) and [HtmlTableOperations.GetRowCells](../../Broiler.DOM/Broiler.Dom.Html/HtmlTableOperations.cs) provide DOM traversal. Rows are collected as header rows, then direct/body rows, then footer rows; cells include both `td` and `th`. Applying hints still needs a mapping from these elements to the correct boxes, without including nested-table cells or synthetic wrappers. These helpers do not promise arbitrary malformed-tree traversal.

`InheritRowAlignment` is separate: it already checks `td`, `th`, and `tr`, inherits the parent's styled alignment when recorded as a hint, and respects a cell's own `valign`. Preserve its cascade behavior.

### Form values and select options

[HtmlFormQueries.GetDefaultValue](../../Broiler.DOM/Broiler.Dom.Html/HtmlFormQueries.cs) reflects markup defaults. For an input it returns `value` or the empty string; it supplies neither missing-value Submit/Reset labels nor current edited state. `HtmlParser.AppendInputValueText` supplies those labels only when the attribute is missing and projects text only for selected input types. `IsRadioInput` / `IsCheckboxInput` are useful predicates, but do not replace that rendering policy.

[HtmlSelectQueries.GetSize](../../Broiler.DOM/Broiler.Dom.Html/HtmlSelectQueries.cs) returns zero for missing, malformed, zero, negative, and fractional values. The renderer currently defaults to four visible tracks and clamps to 2–8 after its numeric conversion, so today `size="0"` and `size="2.5"` both render two tracks. A direct replacement would change the missing-attribute default to two; adding the fallback after `GetSize` would turn those two cases into four. `HtmlElementQueries.ReadNumericAttribute(select, "size", 4)` reproduces the current reader exactly and is the behavior-preserving option. `GetOptions` provides canonical option enumeration, while `CorrectSelectMultipleBoxes` currently hides all immediate children and creates generic track boxes. Enumeration does not replace hiding, selected-state rendering, or track sizing.

### HTML export

[DomUtils.GenerateHtml / WriteHtml](../../Broiler.HTML/Source/Broiler.HTML.Dom/Utils/DomUtils.cs) is a recursive renderer export path. It handles selected word slices, whitespace, ancestor/element filtering, fragment comments, and `HtmlGenerationStyle` modes including stylesheet injection in the header. It writes rendered `Words`, not simply `box.Text`. It also has quirks that the export contract must either keep or deliberately change. Void tags are written as `/>`, whereas the shared serializer writes `>`. `InHeader` inserts an extra `<head>` right after `<html>`, ahead of the document's own head.

[HtmlSerializer.Serialize<TNode>](../../Broiler.DOM/Broiler.Dom.Html/HtmlSerializer.cs) offers iterative traversal and shared escaping/raw-text handling, but a complete adapter requires **seven** delegates, including `GetStyles`. Anonymous roots/wrappers need `Fragment` semantics. Source comments and template contents have already been omitted during box projection, so a box adapter cannot reconstruct them.

The migration should define a renderer serialization view that can express synthetic text, fragment comments, and header styles. The generic serializer ignores element text when children exist, so a box with words and child boxes needs an explicit text node in that view. Selection prepasses must also become iterative before claiming the entire selected-HTML path is stack-safe. `HtmlParserDump` emits JSON and is a separate diagnostic path.

The generic API receives no namespace or scripting-mode callback. It lowercases every element name, so SVG camel-case names such as `foreignObject` and `linearGradient` are not preserved. It also treats `noscript` as raw text unconditionally. Its tag-name rules alone do not establish complete foreign-content/raw-text conformance; use targeted tests and the [WHATWG HTML fragment serialization algorithm](https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments) as the reference, rather than promising strict conformance from the adapter substitution.

## 6. Verification Record

The recheck inspected the implementation, project dependencies, existing tests, and CI definitions. No renderer implementation was changed.

The following focused command passed on 2026-10-15 with .NET SDK 10.0.401: **63 passed, 0 failed, 0 skipped**. A second run on the same day reproduced that result.

```powershell
dotnet test Broiler.DOM/Broiler.Dom.Html.Tests/Broiler.Dom.Html.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~HtmlDocumentQueriesTests|FullyQualifiedName~HtmlElementQueriesTests|FullyQualifiedName~HtmlSelectQueriesTests|FullyQualifiedName~HtmlFormQueriesTests|FullyQualifiedName~HtmlTableOperationsTests|FullyQualifiedName~HtmlSerializerTests" --logger "console;verbosity=minimal"
```

This confirms the existing tests in those classes; it does not validate the proposed renderer integration or fill the edge-case gaps identified above. The test project uses a project reference, so it exercises the `Broiler.DOM` checkout (`00b21bf`), not the `Broiler.Dom.Html` `0.1.0-preview.14` package that `Broiler.HTML` consumes.

To cover that gap, a throwaway probe against the published `0.1.0-preview.14` package confirmed the behaviors this document relies on:

- `VoidElements` holds 15 names, none of them `basefont`, `bgsound`, or `keygen`.
- `GetEffectiveBaseHref` skips a first `<base href="">` and returns a later, trimmed `href`.
- `CollectTableRows` and `GetRowCells` return both `th` and `td` through a parser-inserted `tbody`.
- `GetSize` returns 0 for `size="0"`, `size="2.5"`, and a missing attribute, where `ReadNumericAttribute(…, 4)` returns 0, 2.5, and 4.
- `ResolveProgressLikeValueRatio` returns NaN for `<meter value="NaN">`.
- `GetDefaultValue` returns an empty string for `<input type=submit>` without `value`.

The probe is not part of the repository. Full renderer, layout, CSS, WPT visual suites, and Windows/Linux CI were not run as part of this documentation recheck. Their execution remains an implementation gate in the roadmap.

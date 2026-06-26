# HtmlBridge API to standards map

This document maps the current public `Broiler.HtmlBridge` API surface to the
closest WHATWG/W3C specification anchors for M1. Each entry also flags whether
the surface is standard-facing, Broiler-specific, or known to be incomplete.

## Legend

- **Aligned** — intended to model a standards-defined concept
- **Partial** — mapped to a standards-defined concept but still incomplete
- **Non-standard** — Broiler-specific helper/diagnostic API

## Script execution and lifecycle

| API surface | Spec anchor | Status | Notes |
|---|---|---|---|
| `IScriptEngine.Execute(...)` / `ScriptEngine.Execute(...)` | HTML Living Standard — scripting, classic script processing, window load/event-loop sequencing | Partial | Returns serialized HTML instead of a typed DOM/document result |
| `IScriptEngine.ExecuteInteractive(...)` / `InteractiveSession` | HTML timers, animation frame processing, event loop checkpoints | Non-standard | Step-wise tooling API for capture/debug workflows |
| `IScriptEngine.ExecuteDetailed(...)` / `ScriptExecutionResult` / `ScriptError` | No direct standards API | Non-standard | Diagnostic wrapper around execution failures |
| `MicroTaskQueue` | HTML event loop "perform a microtask checkpoint"; ECMAScript PromiseJobs | Partial | Bridge-owned helper, not a browser-exposed API |
| `ScriptProfilingHook` / `ScriptTimingEntry` | No direct standards API | Non-standard | Perf instrumentation used by PR gating |
| `ContentSecurityPolicy` | CSP Level 3 (`default-src`, `script-src`, `script-src-elem`) | Partial | Parser/enforcement is intentionally scoped to currently wired script directives |
| `ScriptExtractor`, `IScriptExtractor`, `ScriptExtractionResult`, `PageContent` | HTML script element extraction / parser-inserted script ordering concepts | Partial | Extraction model is tooling-oriented rather than a full parser integration API |

## DOM / CSSOM bridge

| API surface | Spec anchor | Status | Notes |
|---|---|---|---|
| `DomBridge` stable orchestration members (`FlushTimers`, `FireWindowLoadEvent`, `SerializeToHtml`, viewport helpers) | HTML event loop, load events, CSSOM View, DOM Parsing/Serialization concepts | Partial | Public orchestrator over a bridge-owned DOM/runtime |
| `DomBridge.Attach(...)`, `RegisterNamedElementGlobals(...)` | HTML Window named access; script execution environment setup | Partial | Still leaks concrete `JSContext`; frozen as compatibility-only surface |
| `DomBridge.Document`, `DocumentElement`, `Elements`, `DomElement` | WHATWG DOM concepts | Partial | `DomBridge` now owns a canonical `Broiler.Dom.DomDocument`; the legacy `DomElement` surface is a temporary compatibility facade over canonical nodes |
| `DomBridge.CalculateSpecificity(...)`, `CssRules` | Selectors Level 4, CSSOM | Partial | Shared internal/bridge cache surface, not a browser-native CSSOM API |
| DOM event plumbing inside `DomBridge` | DOM Standard events / UI Events | Partial | Coverage is expanding via targeted bridge tests; constructors/legacy init methods remain selectively implemented |
| DOM traversal/range plumbing inside `DomBridge` | DOM Standard traversal and Range | Partial | Bridge-owned state types still surface internally/publicly in places |
| Stylesheet/CSS rule plumbing inside `DomBridge` | CSSOM / CSS Conditional Rules / CSS Animations / CSS Counter Styles / CSS Properties & Values API | Partial | Major rule-object coverage exists; unsupported rules and edge semantics remain tracked through WPT/CLI regressions |
| Selector matching inside `DomBridge` | Selectors Level 4 | Partial | Functional pseudo-class specificity and several near-pass cases are implemented; broader WPT parity remains open |
| Anchor and animation resolvers (`ResolveAnchorPositions`, `ResolveAnimationSnapshots`) | CSS Anchor Positioning draft; Web Animations / CSS Animations concepts | Partial | Pipeline helpers over incomplete standards support |

## Parsing and shared document helpers

| API surface | Spec anchor | Status | Notes |
|---|---|---|---|
| `HtmlTreeBuilder` | HTML Living Standard parsing/tree-construction algorithms | Partial | Explicitly WHATWG-aligned, but still a bridge-owned parser projection |
| HTML serialization helpers reachable through `DomBridge.SerializeToHtml()` | DOM Parsing/Serialization concepts | Partial | Used as a compatibility hand-off to the renderer |

## Rendering, layout, and image utilities shipped from `Broiler.HtmlBridge`

These public surfaces are not browser APIs. They are project-level utilities
that help Broiler render or analyze the post-bridge DOM state.

| API surface | Closest spec anchor | Status | Notes |
|---|---|---|---|
| `CssBoxModel`, `LayoutBox`, `Rect`, box/flex/grid enums and helpers | CSS Display / Box Model / Flexbox / Grid | Non-standard | Renderer-facing convenience model, not a web-exposed API |
| `RenderingStages` types (`PaintCommand`, `Painter`, `Compositor`, `RenderOutput`, etc.) | CSS painting/compositing concepts | Non-standard | Broiler renderer pipeline helpers |
| `CssTextProperties` types (`CssFontFace`, `TextLayout`, whitespace/text enums) | CSS Fonts / CSS Text | Non-standard | Broiler text-layout helpers |
| `ImagePipeline` / `ImageDecoder` / SVG parser/renderer / canvas command types | HTML image decode, Canvas 2D, SVG, CSS Images concepts | Non-standard | Tooling/runtime helpers, not a standards-shaped DOM API |
| `RenderLogger` | Console/devtools-adjacent diagnostics | Non-standard | Broiler logging surface |

## Known non-conformance / missing-feature themes called out by this map

1. **Public bridge leakage still exists.** `DomBridge.Attach(...)`,
   `RegisterNamedElementGlobals(...)`, `DocumentElement`, and `Elements` still
   expose concrete YantraJS or bridge-internal DOM types.
2. **The bridge-to-renderer hand-off is still serialized HTML.** That preserves
   compatibility but is not the target long-term typed seam.
3. **Several public types are Broiler utilities rather than browser-shaped APIs.**
   They are documented here so future cleanup can decide whether they should stay
   public, move behind a dedicated rendering assembly, or become internal.
4. **Standards coverage remains intentionally partial.** DOM Events, traversal,
   CSSOM, Selectors, CSP, anchor positioning, and animation handling all have
   focused coverage, but broader WPT parity work is still roadmap-managed.

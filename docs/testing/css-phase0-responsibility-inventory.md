# CSS Phase 0 Test Responsibility Inventory

**Date:** 2026-06-25  
**Purpose:** Classify the current CSS regression surface before the renderer and
bridge CSS implementations are converged into `Broiler.CSS`.

## Responsibility classes

| Class | Owns |
|---|---|
| Parser | Tokens, declarations, shorthands, values, at-rules, error recovery |
| Selector | Selector syntax, specificity, matching, scopes, query APIs |
| Cascade | Origins, importance, source order, inheritance, custom properties, computed values |
| CSSOM | Stylesheet/rule/declaration JavaScript wrappers and live mutation |
| Layout | Box generation, flex/grid/table flow, sizing, positioning, writing modes |
| Paint | Backgrounds, borders, text, compositing, filters, raster output |

## Focused suites

| Suite | Primary | Secondary | Migration use |
|---|---|---|---|
| `CssExtractionPhaseZeroTests` | Parser, Selector | Architecture | Direct characterization without pixel assertions |
| `SelectorsLevel4SpecificityTests` | Selector | Cascade | Specificity and functional pseudo-class gate |
| `SelectorScopeTests` | Selector | DOM | `:scope`, `matches`, and `closest` gate |
| `SelectorsAndCssomTests` | Selector, CSSOM | Cascade | Bridge API and live-object gate |
| `CssImportantCascadeTests` | Cascade | Parser, Paint | `!important` and shorthand propagation |
| `WptCssVariablesTests` | Cascade | Parser | Custom-property and `var()` gate |
| `WptFontAndSelectorTests` | Parser, Selector | Cascade | Font shorthand and selector case/escape behavior |
| `CssRenderingTests` | CSSOM, Cascade | Parser, Layout, Paint | Broad bridge/CSSOM compatibility suite |
| `CssSelectorsPolishTests` | Selector, Cascade | DOM | Dynamic matching and computed-style behavior |
| `FlexLayoutTests` | Layout | Cascade, Paint | Flex formatting-context gate |
| `RootBackgroundTests` | Paint | Cascade, Layout | Canvas background propagation |
| `WptCompositingTests` | Paint | Cascade | Blend/compositing behavior |

## Acid coverage

| Area | Primary responsibility |
|---|---|
| Acid2 external stylesheet and parser recovery cases | Parser, Cascade |
| Acid2 box model, overflow, positioning, and table cases | Layout, Paint |
| `Acid3CssSelectorRegressionTests` | Selector, Cascade |
| `Acid3CssComplianceTests` | Parser, Cascade, computed style |
| `Acid3CascadeDebugTests` | Cascade |
| `Acid3BorderLayoutTests` and `Acid3BarPositionTest` | Layout, Paint |
| `Acid3SvgAndParsingRegressionTests` | Parser, Paint |
| `Acid3RenderingFixTests` | Layout, Paint |

## WPT subsets

| WPT path/theme | Primary responsibility |
|---|---|
| `css/selectors` | Selector |
| `css/css-cascade`, `css/css-variables` | Cascade |
| `css/css-values`, `css/css-syntax` | Parser |
| `css/cssom`, `css/cssom-view` | CSSOM; CSSOM View geometry remains bridge/layout-owned |
| `css/css-fonts`, `css/css-text` | Parser/Cascade, then Layout/Paint |
| `css/css-flexbox`, `css/css-grid`, `css/css-tables` | Layout |
| `css/css-backgrounds`, `css/css-borders` | Cascade, Paint |
| `css/compositing`, `filter-effects` | Paint |
| `css/css-writing-modes` | Cascade, Layout, Paint |

## Phase gates

- Parser extraction changes must run `CssExtractionPhaseZeroTests`,
  `WptCssVariablesTests`, `WptFontAndSelectorTests`, and parser-classified WPT
  subsets.
- Selector extraction changes must run the three focused selector suites,
  Acid3 selector regressions, and `css/selectors`.
- Cascade/computed-style changes must run CSS variables, important cascade,
  computed-style Acid3 coverage, and renderer property-level differential tests.
- Renderer cutover changes additionally require layout suites, Acid2/Acid3
  image comparisons, CSS WPT image subsets, and performance baselines.
- CSSOM changes require `SelectorsAndCssomTests`, the CSSOM sections of
  `CssRenderingTests`, dynamic stylesheet mutation tests, and `css/cssom`.

This inventory classifies ownership; it does not imply that every test is
isolated to one subsystem. Integration tests intentionally cross boundaries.

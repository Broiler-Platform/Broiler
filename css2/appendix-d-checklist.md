# Appendix D — Default Style Sheet for HTML 4

Detailed checklist for CSS 2.1 Appendix D. This appendix provides the
informative default (user-agent) style sheet for HTML 4 elements.

> **Spec file:** [`sample.html`](sample.html)

> **Implementation file:** `HtmlRenderer.Core/Core/CssDefaults.cs`

---

## Default Display Values

- [x] `html`, `address`, `blockquote`, `body`, `dd`, `div`, `dl`, `dt`, `fieldset`, `form`, `frame`, `frameset`, `h1`–`h6`, `noframes`, `ol`, `p`, `ul`, `center`, `dir`, `hr`, `menu`, `pre` → `display: block`
- [x] `li` → `display: list-item`
- [x] `head` → `display: none`
- [x] `table` → `display: table`
- [x] `tr` → `display: table-row`
- [x] `thead` → `display: table-header-group`
- [x] `tbody` → `display: table-row-group`
- [x] `tfoot` → `display: table-footer-group`
- [x] `col` → `display: table-column`
- [x] `colgroup` → `display: table-column-group`
- [x] `td`, `th` → `display: table-cell`
- [x] `caption` → `display: table-caption`

## Default Margins

- [x] `body` → `margin: 8px`
- [x] `h1` → `margin: 0.67em 0` with `font-size: 2em`
- [x] `h2` → `margin: 0.83em 0` with `font-size: 1.5em` *(deviation: UA uses `margin: 0.75em 0`)*
- [x] `h3` → `margin: 1em 0` with `font-size: 1.17em` *(deviation: UA uses `margin: 0.83em 0`)*
- [x] `h4` → `margin: 1.33em 0` *(deviation: UA groups h4 with p/blockquote/ul/ol/etc. at `margin: 1.12em 0`)*
- [x] `h5` → `margin: 1.67em 0` with `font-size: 0.83em` *(deviation: UA uses `margin: 1.5em 0`)*
- [x] `h6` → `margin: 2.33em 0` with `font-size: 0.67em` *(deviation: UA uses `margin: 1.67em 0; font-size: 0.75em`)*
- [x] `p`, `blockquote`, `ul`, `fieldset`, `form`, `ol`, `dl`, `dir`, `menu` → `margin: 1.12em 0`
- [x] `blockquote`, `figure` → `margin-left: 40px; margin-right: 40px` *(deviation: only `blockquote` has this rule; `figure` gets `display: block` only)*
- [x] `dd` → `margin-left: 40px` *(present — dd is grouped with ol/ul/dir/menu in `margin-left: 40px` rule)*
- [x] `ol`, `ul`, `dir`, `menu` → `padding-left: 40px` *(deviation: UA uses `margin-left: 40px` instead of `padding-left`)*

## Default Font Styles

- [x] `h1`–`h6` → `font-weight: bolder`
- [x] `b`, `strong` → `font-weight: bolder`
- [x] `i`, `cite`, `em`, `var`, `address` → `font-style: italic`
- [x] `pre`, `tt`, `code`, `kbd`, `samp` → `font-family: monospace`
- [x] `big` → `font-size: 1.17em`
- [x] `small`, `sub`, `sup` → `font-size: 0.83em`
- [x] `sub` → `vertical-align: sub`
- [x] `sup` → `vertical-align: super`

## Default Text Styles

- [x] `center` → `text-align: center`
- [x] `u`, `ins` → `text-decoration: underline`
- [x] `s`, `strike`, `del` → `text-decoration: line-through`
- [x] `pre` → `white-space: pre`

## Default Table Styles

- [x] `table` → `border-spacing: 2px; border-collapse: separate` (UA-typical) *(note: `border-collapse` defaults to `separate` via `CssBoxProperties`; not explicitly in stylesheet)*
- [x] `td`, `th` → `padding: 1px` *(deviation: UA stylesheet does not explicitly set `padding: 1px`; `CssBoxProperties` defaults padding to `0`, so cells get `0` padding rather than the spec-recommended `1px`)*
- [x] `th` → `font-weight: bolder; text-align: center`
- [x] `caption` → `text-align: center`

## Default List Styles

- [x] `ol` → `list-style-type: decimal`
- [x] `ul`, `dir`, `menu` → `list-style-type: disc` *(note: `disc` is the engine default for `list-item` display; nested `ul` overrides to `circle`/`square`)*

## Other Defaults

- [x] `hr` → `border: 1px inset` (typical UA rendering)
- [x] `a:link` → `color: blue; text-decoration: underline` (typical UA) *(deviation: UA uses `color: #0055BB` instead of `blue`)*
- [x] `a:visited` → `color: purple; text-decoration: underline` (typical UA) *(deviation: no distinct visited color; `:link, :visited` share `text-decoration: underline`)*
- [x] `a:active` → `color: red` (typical UA) *(deviation: no `:active` pseudo-class styling in UA stylesheet)*
- [x] `:focus` → `outline: thin dotted invert` (typical UA)
- [x] `abbr`, `acronym` → no special default styles *(correct — no rules defined)*
- [x] `img` → `border: none` (for linked images, UA may add border) *(note: no explicit `img` rule; engine default applies)*
- [x] `br:before` → `content: "\A"; white-space: pre-line` *(deviation: `br:before` sets `content: "\A"` but `white-space: pre-line` is applied globally to all `:before, :after` pseudo-elements instead of only to `br:before`. This produces equivalent behavior for `br` but also applies `pre-line` to all other generated content, which may preserve unexpected whitespace in non-`br` pseudo-elements.)*
- [x] `noframes` in frameset: `display: none` *(note: `noframes` is in the `display: block` group; no special frameset-context rule)*
- [x] `head` and head children: `display: none` *(head → `display: none`; children like `style`, `title`, `script`, `link`, `meta` also `display: none`)*

## Bidirectionality

- [x] `BDO[DIR="ltr"]` → `direction: ltr; unicode-bidi: bidi-override`
- [x] `BDO[DIR="rtl"]` → `direction: rtl; unicode-bidi: bidi-override`
- [x] Elements with `dir` attribute → set `direction` and `unicode-bidi: embed`

---

[← Back to main checklist](css2-specification-checklist.md)

# Why www.7-zip.org renders with very small fonts

An investigation into the reported symptom: the real-world suite's `seven-zip`
case (<https://www.7-zip.org/>) renders in Broiler with text far smaller than
Chromium produces.

**Two independent defects are involved**, and they mask each other. The page's
stylesheet was dropped entirely, so the CLI render showed *unstyled, too-large*
text; the moment that first defect is fixed, the second one takes over and the
page renders with the tiny text that was reported.

| | Chromium | Broiler (before) | Broiler (after) |
| --- | --- | --- | --- |
| `TD` at table nesting depth 1 | 12.8px | 10.22px | 12.80px |
| `TD` at table nesting depth 2 | 12.8px | 8.18px | 12.80px |
| `TD` at table nesting depth 3 | 12.8px | 6.49px | 12.80px |

Numbers measured with the calibrated probe described under
[Measurement method](#measurement-method).

**Both are fixed**; see [Resolution](#resolution). One of the two lives in the
`Broiler.HTML` submodule and ships as a patch under `patches/`, because the
submodule remote is outside this session's GitHub scope.

## The page

`https://www.7-zip.org/` is legacy HTML. Two properties of it drive everything
below:

1. Its doctype is `<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">`
   with **no system identifier**. That public identifier is on the HTML
   Standard's unconditional quirks list, so the document is in **quirks mode** —
   Chromium reports `document.compatMode === "BackCompat"`.
2. Its stylesheet is linked root-relative, `<LINK href="/style.css" rel="stylesheet">`,
   and consists almost entirely of compounding percentages:

   ```css
   BODY { font-family: Verdana, Arial, Helvetica; font-size: 80% }
   TH   { font-size: 80% }
   TD   { font-size: 80% }
   H1   { text-align: center; font-size: 140% }
   ```

The page nests tables three deep (outer layout table → content-column table →
data/news table), so `TD { font-size: 80% }` is encountered three times along a
single ancestor chain.

## Finding 1 — a root-relative stylesheet href is never fetched (Unix only)

Rendering the page today produces **no styling at all**: `H1`/`H2` are not
centred, no table has its background colour, and the body's 80% is absent. A
request log from a local server confirms Broiler never asks for the sheet — it
fetches only the document and `/7ziplogo.png`.

Isolated with a matrix of link forms against a logging server (background set to
lime by the linked sheet):

| `href` form | doctype | sheet applied |
| --- | --- | --- |
| `probe.css` (document-relative) | HTML 4.0 Transitional | yes |
| `/probe.css` (root-relative) | HTML 4.0 Transitional | **no** |
| `probe.css`, uppercase `<LINK>` | HTML 4.0 Transitional | yes |
| `/probe.css`, uppercase `<LINK>` | HTML 4.0 Transitional | **no** |
| `/probe.css` | `<!DOCTYPE html>` | **no** |
| `/probe.css`, unquoted attributes | HTML 4.0 Transitional | **no** |

Tag case and doctype are irrelevant; the leading `/` is the whole trigger.

### Root cause

`Broiler.HTML/Source/Broiler.HTML.Orchestration/Handlers/StylesheetLoadHandler.cs`,
`ResolveStylesheetSource`:

```csharp
if (Uri.TryCreate(src, UriKind.Absolute, out _))
    return src;                                   // treated as already absolute
```

On Unix, `Uri.TryCreate("/style.css", UriKind.Absolute, out _)` returns **`true`**,
yielding `file:///style.css`. (Verified directly: on the same runtime,
`"probe.css"` returns `false` and `"/probe.css"` returns `true` with
`Scheme == "file"`.) So the root-relative href is misclassified as absolute and
returned unrebased. `LoadStylesheet` then takes the `uri.Scheme == "file"` branch
and tries to read `/style.css` **off the local filesystem**, which does not exist —
the failure is swallowed as a `CssParsing` error and the page renders unstyled.

This is platform-specific: on Windows the same call returns `false`, so the sheet
would resolve and load correctly. That asymmetry is a plausible reason it has gone
unnoticed.

The correct idiom already exists a few files away, in
`HtmlContainerInt.TryResolveHttpFontUrl`, which guards the same call with a scheme
check:

```csharp
if (Uri.TryCreate(src, UriKind.Absolute, out var abs) && IsHttp(abs))
```

**Confirmed by experiment.** Adding the equivalent guard to
`ResolveStylesheetSource` and rebuilding makes the root-relative probe fetch and
apply its sheet (`GET /probe.css` appears in the request log, background turns
lime), and makes the live 7-Zip page load `style.css`. This is the fix that
shipped, as a submodule patch.

`HtmlContainerInt.ResolveHref` (used for link clicks and form actions) contains the
same unguarded idiom and is likely affected in the same way. Image loading is *not*
affected — `/7ziplogo.png` resolves and loads correctly through a different path.

## Finding 2 — the quirks-mode table font reset is not implemented

This is the actual cause of the small fonts. With the stylesheet loading, Broiler
renders 7-Zip with text at roughly half Chromium's size, shrinking further with
each level of table nesting.

In quirks mode a `<table>` does **not** inherit the font properties of its parent.
The HTML Standard's Rendering section specifies it directly:

> In quirks mode, the following rules are also expected to apply:
>
> ```css
> @namespace "http://www.w3.org/1999/xhtml";
> table {
>   font-weight: initial;
>   font-style: initial;
>   font-variant: initial;
>   font-size: initial;
>   line-height: initial;
>   white-space: initial;
>   text-align: initial;
> }
> ```

Because of this, on 7-Zip every `<table>` resets to 16px and each `TD { font-size:
80% }` resolves against 16px — so **every** cell is 12.8px, no matter how deep.
Broiler inherits `font-size` into the table instead, so the 80% compounds:
12.8 → 10.24 → 8.19 → 6.55px.

Measured in Chromium against the same probe, quirks vs. standards mode:

| | body | `table` d1 | `td` d1 | `table` d2 | `td` d2 |
| --- | --- | --- | --- | --- | --- |
| Quirks (`BackCompat`) | 12.8px | 16px | 12.8px | 16px | 12.8px |
| Standards (`CSS1Compat`) | 12.8px | 12.8px | 10.24px | 10.24px | 8.192px |

Broiler's numbers match the **standards-mode** column exactly. The cascade is
behaving correctly; what is missing is the quirks-mode reset.

Probing which properties actually reset on a `<table>` in Chromium's quirks mode
reproduces the spec list, plus `text-indent`:

| Reset on `<table>` | Inherited normally |
| --- | --- |
| `font-size` → `medium` | `font-family` |
| `font-weight` → `normal` | `font-stretch` |
| `font-style` → `normal` | `letter-spacing` |
| `font-variant` → `normal` | `word-spacing` |
| `line-height` → `normal` | `text-transform` |
| `white-space` → `normal` | `color` (has its own separate quirk) |
| `text-align` → `start` | |
| `text-indent` → `0` (Blink extra, not in the spec list) | |

Note `font-size: initial` resolves to `medium`, which is the *default size for the
resolved font family* — 16px for proportional families but 13px for `monospace`.
Chromium reproduces that; a body at `200%` or at `10px` both yield a 13px
monospace table.

### Where it belongs

Broiler currently has **no quirks-mode UA stylesheet at all**. `CssDefaults.cs`
carries a single unconditional sheet, and searching `Broiler.HTML.Core` and
`Broiler.CSS` for quirks handling returns nothing. The two quirks that do exist are
implemented ad hoc in `Broiler.Layout`:

- `Broiler.Layout/Engine/TablesInheritColorFromBodyQuirk.cs` — the colour quirk,
  applied as a pass over the finished box tree.
- The body/html fill-viewport behaviour, via `DocumentModeContext.CurrentQuirksMode`.

`DocumentModeContext.CurrentQuirksMode` is already published on both render paths
and cached on the tree root, and `CssBox.DocumentQuirksMode` is already how
`TablesInheritColorFromBodyQuirk` gates itself — so the quirks-mode signal needed
here is in place. The reset is a natural sibling of the existing colour quirk, and
`TablesInheritColorFromBodyQuirk` is a good structural precedent for it (including
its handling of re-inheritance into descendants after the value changes).

### The quirks-mode flag was itself wrong, and 7-Zip was on the wrong side of it

`DocumentModeContext.IsQuirksHtml` decided the mode from the doctype **name**
alone: no doctype, or a name that is not `html`, meant quirks. 7-Zip's doctype is
`<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">`, whose name *is*
`html` — so the page was classified **standards mode**, and every quirks-mode
behaviour keyed off the flag (the body/html viewport fill, the colour quirk, and
the reset above) was silently inert on it. `WptDocumentRenderer.SelectsQuirksMode`
had the same limitation independently.

This is not a detail that can be deferred: the whole point of the reset is legacy
pages, and legacy pages are exactly the ones that declare a public identifier. The
fix implements the HTML Standard's DOCTYPE conditions — the exact-match and
prefix lists for the public identifier, the listed system identifier, and the
HTML 4.01 Transitional/Frameset rule whose answer depends on whether a system
identifier is present (full quirks without one, limited-quirks with one, and
limited-quirks is not what this predicate reports).

### Verification

With the reset in place the calibrated probe reports **12.80px at all three
nesting depths**, matching Chromium exactly (was 10.22 / 8.18 / 6.49px), and the
full 7-Zip page renders at Chromium's typographic scale.

The residual gap is unrelated to font sizing — `TABLE.News { width: 220px }` is not
honoured, `TD.NewsTitle { color: white }` does not apply, and cell padding differs.
Those are separate issues and were not investigated here.

## Resolution

| defect | where the fix lives | ships as |
| --- | --- | --- |
| Root-relative stylesheet href read off the local disk | `Broiler.HTML`, `StylesheetLoadHandler.ResolveStylesheetSource` | `patches/0001-stylesheet-root-relative-href.patch` |
| Quirks-mode table font reset missing | `Broiler.Layout.Engine.TableFontInheritanceQuirk`, applied from `CssBoxProperties.InheritStyle` | in-repo |
| Quirks mode decided from the doctype name alone | `Broiler.Layout.DocumentModeContext.IsQuirksHtml` | in-repo |

The reset runs during inheritance rather than as a pass over the finished tree,
which is what separates it from its neighbour `TablesInheritColorFromBodyQuirk`.
That quirk has to run afterwards because it reads the *document's* body, which need
not be an ancestor. This one reads nothing outside the box — it only declines to
copy the parent's values — and doing it inline gets the ordering right for free:
the cascade is strictly top-down, so a cell's `80%` is resolved after its table has
been reset. A later pass could not fix that without re-resolving every descendant's
font size, because `FontSize` resolves percentages eagerly when they are set. It is
also what gives the spec's UA-origin precedence for free: the element's own
declarations are applied next and still win.

The submodule half could not be pushed — the git proxy answers 403 for
`Broiler-Platform/Broiler.HTML`, which is outside this session's GitHub scope — so
it follows the repository's patch workflow: committed in the submodule, exported
with `git format-patch`, the working tree reverted, and **the gitlink left
unbumped**. It is registered in `scripts/apply-pending-wpt-patches.sh`, which the
real-world render workflow runs, so the `seven-zip` case exercises the fix rather
than testing against the un-fixed pointer. Until a maintainer applies it, a build
from the pinned pointer still drops the sheet and renders 7-Zip unstyled.

## Measurement method

Broiler's CLI has no computed-style dump, so font sizes were measured optically
with a self-calibrating probe. A single page carries a ladder of `<div>`s at known
absolute sizes (16, 12.8, 11.2, 10.24, 8.192, 6.554, 5.243, 4.194px) followed by
the nested-table structure under test, all in one font family and the same
16-character string. The render is scanned for horizontal bands of ink; band width
is linear in font size, so the ladder converts a measured width directly into a
size. Recovered ladder values were 16.00, 12.80, 11.20, 10.22, 8.18, 6.58, 5.24 and
4.18px against nominals — accurate to well under a tenth of a pixel, which bounds
the error on the reported cell sizes.

Both engines were pointed at a byte-identical local snapshot of the page served
over `127.0.0.1`, so the comparison is not subject to live-site drift. Chromium's
side is read directly from `getComputedStyle`.

## Adjacent finding (not a contributor here)

Broiler's absolute-size keyword table is *additive in points* rather than the CSS
scaling table. From `CssBoxProperties.ComputedFontSizePoints` (and duplicated near
line 2170 of the same file), `xx-small` is `medium - 4pt`, `x-large` is
`medium + 3pt`, and so on. Measured against spec:

| keyword | Broiler | CSS |
| --- | --- | --- |
| `xx-small` | 10.7px | 9px |
| `x-small` | 12.0px | 10px |
| `small` | 13.3px | 13px |
| `medium` | 16.0px | 16px |
| `large` | 18.6px | 18px |
| `x-large` | 20.0px | 24px |
| `xx-large` | 21.3px | 32px |

7-Zip uses no keyword sizes, so this does not contribute to the reported symptom.
It is worth noting only because the fix for Finding 2 is expressed as
`font-size: initial` — which resolves to `medium`, the one entry Broiler already
gets right.

## Ruled out

- **Broiler's default font size.** `CssMetrics.DefaultFontSizePt` is 12pt = 16px,
  matching the browser default; the root and `<body>` resolve correctly.
- **Percentage font-size resolution.** Percentages resolve against the parent's
  computed size and are eagerly resolved to an absolute length by the `FontSize`
  setter, so an inheriting descendant does not re-apply them. Broiler's chain
  matches Chromium's standards-mode chain exactly, which would not happen if
  percentage handling were wrong.
- **Uppercase tag/selector matching.** The page and its stylesheet are uppercase
  throughout; type selectors match case-insensitively, and the link matrix above
  shows tag case makes no difference.
- **A missing minimum font size.** Chromium applies a 6px "smart minimum" to sizes
  derived from percentages and keywords, and Broiler has no equivalent clamp. On
  this page Chromium never goes below 12.8px, so the clamp is never reached and is
  not what separates the two engines. It would only matter as a safety net for the
  compounding in Finding 2 — which is better fixed at its cause.

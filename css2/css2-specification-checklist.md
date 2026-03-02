# CSS 2.1 Specification Checklist

A concise checklist of every CSS 2.1 rule and feature, organized by
specification chapter. Use this document to track implementation coverage and
audit CSS 2.1 support in the html-renderer.

> **Reference:** The full CSS 2.1 specification is available in the HTML files
> alongside this document (W3C Recommendation, June 2011).

---

## Chapter Checklists

Detailed per-chapter checklists with granular specification rules and
implementation targets:

| Chapter | Title | Checklist |
|---------|-------|-----------|
| 1 | About the CSS 2.1 Specification | [chapter-1-checklist.md](chapter-1-checklist.md) |
| 2 | Introduction to CSS 2.1 | [chapter-2-checklist.md](chapter-2-checklist.md) |
| 3 | Conformance: Requirements and Recommendations | [chapter-3-checklist.md](chapter-3-checklist.md) |
| 4 | Syntax and Basic Data Types | [chapter-4-checklist.md](chapter-4-checklist.md) |
| 5 | Selectors | [chapter-5-checklist.md](chapter-5-checklist.md) |
| 6 | Cascading and Inheritance | [chapter-6-checklist.md](chapter-6-checklist.md) |
| 7 | Media Types | [chapter-7-checklist.md](chapter-7-checklist.md) |
| 8 | Box Model | [chapter-8-checklist.md](chapter-8-checklist.md) |
| 9 | Visual Formatting Model | [chapter-9-checklist.md](chapter-9-checklist.md) |
| 10 | Visual Formatting Model Details | [chapter-10-checklist.md](chapter-10-checklist.md) |
| 11 | Visual Effects | [chapter-11-checklist.md](chapter-11-checklist.md) |
| 12 | Generated Content, Automatic Numbering, and Lists | [chapter-12-checklist.md](chapter-12-checklist.md) |
| 13 | Paged Media | [chapter-13-checklist.md](chapter-13-checklist.md) |
| 14 | Colors and Backgrounds | [chapter-14-checklist.md](chapter-14-checklist.md) |
| 15 | Fonts | [chapter-15-checklist.md](chapter-15-checklist.md) |
| 16 | Text | [chapter-16-checklist.md](chapter-16-checklist.md) |
| 17 | Tables | [chapter-17-checklist.md](chapter-17-checklist.md) |
| 18 | User Interface | [chapter-18-checklist.md](chapter-18-checklist.md) |
| A | Aural Style Sheets | [appendix-a-checklist.md](appendix-a-checklist.md) |
| D | Default Style Sheet for HTML 4 | [appendix-d-checklist.md](appendix-d-checklist.md) |
| E | Elaborate Description of Stacking Contexts | [appendix-e-checklist.md](appendix-e-checklist.md) |

---

## Chapter 4 — Syntax and Basic Data Types

- [ ] Tokenization and parsing rules
- [ ] `!important` declaration parsing
- [ ] Keywords: `inherit`, `initial`
- [ ] Length units: `em`, `ex`, `px`, `in`, `cm`, `mm`, `pt`, `pc`
- [ ] Percentage values (`%`)
- [ ] URL values (`url()`)
- [ ] Color values: keyword colors (16 named colors + `orange`)
- [ ] Color values: `#rgb` and `#rrggbb` hex notation
- [ ] Color values: `rgb()` functional notation (integer and percentage)
- [ ] String values (single and double-quoted)
- [ ] Counter values (`counter()`, `counters()`)
- [ ] `attr()` functional notation
- [ ] Character escaping and Unicode escapes (`\` notation)
- [ ] At-rules: `@import`, `@media`, `@page`, `@charset`
- [ ] Rule sets, declarations, and statement structure
- [ ] Shorthand property handling and expansion
- [ ] Forward-compatible parsing (ignoring unknown properties/values)

## Chapter 5 — Selectors

- [ ] Universal selector (`*`)
- [ ] Type selectors (`E`)
- [ ] Descendant selectors (`E F`)
- [ ] Child selectors (`E > F`)
- [ ] Adjacent sibling selectors (`E + F`)
- [ ] Attribute selector: presence (`E[attr]`)
- [ ] Attribute selector: exact value (`E[attr="val"]`)
- [ ] Attribute selector: space-separated list (`E[attr~="val"]`)
- [ ] Attribute selector: hyphen-separated prefix (`E[attr|="val"]`)
- [ ] Class selectors (`.class`)
- [ ] ID selectors (`#id`)
- [ ] Pseudo-class: `:link`
- [ ] Pseudo-class: `:visited`
- [ ] Pseudo-class: `:hover`
- [ ] Pseudo-class: `:active`
- [ ] Pseudo-class: `:focus`
- [ ] Pseudo-class: `:first-child`
- [ ] Pseudo-class: `:lang()`
- [ ] Pseudo-element: `::first-line` (`:first-line`)
- [ ] Pseudo-element: `::first-letter` (`:first-letter`)
- [ ] Pseudo-element: `::before` (`:before`)
- [ ] Pseudo-element: `::after` (`:after`)
- [ ] Grouping selectors (comma-separated)
- [ ] Selector specificity calculation (`a`, `b`, `c`, `d`)

## Chapter 6 — Cascading and Inheritance

- [x] Cascade order: user-agent → user → author
- [x] `!important` declarations override normal declarations
- [x] `!important` priority: user `!important` > author `!important`
- [x] Specificity-based ordering within same origin
- [x] Source order as final tie-breaker
- [x] Inline `style` attribute specificity
- [x] Inheritance of inherited properties
- [x] `inherit` keyword on any property
- [x] Specified, computed, used, and actual value stages
- [x] Initial values for non-inherited properties

## Chapter 7 — Media Types

- [ ] `@media` rule
- [ ] `@import` with media types
- [ ] Media type: `all`
- [ ] Media type: `screen`
- [ ] Media type: `print`
- [ ] Media type: `aural` / `speech`
- [ ] Media type: `braille`, `embossed`, `handheld`, `projection`, `tty`, `tv`
- [ ] Media-dependent property application

## Chapter 8 — Box Model

### 8.1–8.2 Box Dimensions and Padding

- [ ] Content area, padding area, border area, margin area
- [ ] `padding-top` — space above content
- [ ] `padding-right` — space to the right of content
- [ ] `padding-bottom` — space below content
- [ ] `padding-left` — space to the left of content
- [ ] `padding` — shorthand for all four padding values

### 8.3 Margins

- [ ] `margin-top` — top margin
- [ ] `margin-right` — right margin
- [ ] `margin-bottom` — bottom margin
- [ ] `margin-left` — left margin
- [ ] `margin` — shorthand for all four margin values
- [ ] `margin: auto` — horizontal centering for block boxes
- [ ] Negative margin values
- [ ] Collapsing vertical margins between adjacent block boxes
- [ ] Collapsing margins between parent and first/last child
- [ ] Margins do not collapse with padding or borders between them
- [ ] Margins of floating and absolutely positioned elements do not collapse

### 8.4 Borders

- [ ] `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`
- [ ] `border-width` — shorthand for all four border widths
- [ ] Border width keywords: `thin`, `medium`, `thick`
- [ ] `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`
- [ ] `border-color` — shorthand for all four border colors
- [ ] `border-top-style`, `border-right-style`, `border-bottom-style`, `border-left-style`
- [ ] `border-style` — shorthand for all four border styles
- [ ] Border style values: `none`, `hidden`, `dotted`, `dashed`, `solid`, `double`, `groove`, `ridge`, `inset`, `outset`
- [ ] `border-top`, `border-right`, `border-bottom`, `border-left` — per-side shorthand
- [ ] `border` — shorthand for all borders

## Chapter 9 — Visual Formatting Model

### 9.1–9.2 Box Generation

- [ ] `display: block` — block-level box
- [ ] `display: inline` — inline-level box
- [ ] `display: inline-block` — inline-level block container
- [ ] `display: list-item` — block box with list marker
- [ ] `display: none` — no box generated
- [ ] `display: table`, `inline-table`, `table-row-group`, `table-header-group`, `table-footer-group`, `table-row`, `table-column-group`, `table-column`, `table-cell`, `table-caption`
- [ ] `display: run-in` — context-dependent box type
- [ ] Anonymous block boxes
- [ ] Anonymous inline boxes

### 9.3 Positioning Schemes

- [ ] `position: static` — normal flow
- [ ] `position: relative` — offset from normal flow position
- [ ] `position: absolute` — positioned relative to containing block
- [ ] `position: fixed` — positioned relative to viewport
- [ ] `top` — vertical offset from top of containing block
- [ ] `right` — horizontal offset from right of containing block
- [ ] `bottom` — vertical offset from bottom of containing block
- [ ] `left` — horizontal offset from left of containing block

### 9.4 Normal Flow

- [ ] Block formatting contexts (BFC establishment rules)
- [ ] Inline formatting contexts
- [ ] Relative positioning within normal flow

### 9.5 Floats

- [ ] `float: left` — float to left edge
- [ ] `float: right` — float to right edge
- [ ] `float: none` — no floating
- [ ] Float placement rules (§9.5.1 rules 1–9)
- [ ] Line box shortening around floats
- [ ] `clear: none` — no clearance
- [ ] `clear: left` — clear left floats
- [ ] `clear: right` — clear right floats
- [ ] `clear: both` — clear all floats
- [ ] Clearance computation

### 9.6–9.7 Absolute Positioning and Relationships

- [ ] Containing block for absolutely positioned elements
- [ ] Fixed positioning relative to viewport
- [ ] Relationship between `display`, `position`, and `float`

### 9.9–9.10 Stacking and Text Direction

- [ ] `z-index: auto` — stack level from parent
- [ ] `z-index: <integer>` — explicit stack level, creates stacking context
- [ ] Stacking context painting order (7 layers)
- [ ] `direction: ltr` — left-to-right
- [ ] `direction: rtl` — right-to-left
- [ ] `unicode-bidi: normal`
- [ ] `unicode-bidi: embed`
- [ ] `unicode-bidi: bidi-override`

## Chapter 10 — Visual Formatting Model Details

### 10.1–10.2 Containing Block and Content Width

- [ ] Containing block determination rules
- [ ] `width: <length>` — explicit content width
- [ ] `width: <percentage>` — percentage of containing block
- [ ] `width: auto` — shrink-to-fit or fill available width
- [ ] `min-width` — minimum content width
- [ ] `max-width` — maximum content width
- [ ] Width computation for block, inline, replaced, and absolutely positioned elements

### 10.3–10.7 Height, Line Height, and Vertical Alignment

- [ ] `height: <length>` — explicit content height
- [ ] `height: <percentage>` — percentage of containing block
- [ ] `height: auto` — determined by content
- [ ] `min-height` — minimum content height
- [ ] `max-height` — maximum content height
- [ ] Height computation for block, inline, replaced, and absolutely positioned elements
- [ ] `line-height: normal` — UA-determined line height
- [ ] `line-height: <number>` — font-size multiplier
- [ ] `line-height: <length>` — fixed line height
- [ ] `line-height: <percentage>` — percentage of font-size
- [ ] `vertical-align: baseline`
- [ ] `vertical-align: sub`
- [ ] `vertical-align: super`
- [ ] `vertical-align: top`
- [ ] `vertical-align: text-top`
- [ ] `vertical-align: middle`
- [ ] `vertical-align: bottom`
- [ ] `vertical-align: text-bottom`
- [ ] `vertical-align: <percentage>`
- [ ] `vertical-align: <length>`

## Chapter 11 — Visual Effects

- [ ] `overflow: visible` — content not clipped
- [ ] `overflow: hidden` — content clipped, no scrollbar
- [ ] `overflow: scroll` — content clipped, scrollbar provided
- [ ] `overflow: auto` — UA-dependent scrollbar behavior
- [ ] `clip: rect(top, right, bottom, left)` — clipping rectangle
- [ ] `clip: auto` — no clipping
- [ ] `visibility: visible` — box is visible
- [ ] `visibility: hidden` — box is invisible but affects layout
- [ ] `visibility: collapse` — for table elements, removes row/column

## Chapter 12 — Generated Content, Automatic Numbering, and Lists

### 12.1–12.2 Generated Content

- [ ] `:before` pseudo-element content generation
- [ ] `:after` pseudo-element content generation
- [ ] `content: normal` — no generated content (default)
- [ ] `content: none` — no generated content
- [ ] `content: <string>` — text string
- [ ] `content: <uri>` — external resource
- [ ] `content: counter()` — counter value
- [ ] `content: counters()` — nested counter values
- [ ] `content: attr()` — attribute value
- [ ] `content: open-quote` / `close-quote`
- [ ] `content: no-open-quote` / `no-close-quote`
- [ ] `quotes` — define quotation mark pairs

### 12.3–12.5 Counters and Lists

- [ ] `counter-reset` — reset one or more counters
- [ ] `counter-increment` — increment one or more counters
- [ ] Counter scoping and nesting
- [ ] `list-style-type` — marker type (disc, circle, square, decimal, lower-roman, upper-roman, lower-alpha, upper-alpha, lower-latin, upper-latin, lower-greek, none)
- [ ] `list-style-image` — image as list marker
- [ ] `list-style-position: inside` — marker inside content flow
- [ ] `list-style-position: outside` — marker outside content flow
- [ ] `list-style` — shorthand for list marker properties

## Chapter 13 — Paged Media

- [x] `page-break-before: auto | always | avoid | left | right`
- [x] `page-break-after: auto | always | avoid | left | right`
- [x] `page-break-inside: auto | avoid`
- [x] `orphans` — minimum lines at bottom of page
- [x] `widows` — minimum lines at top of page
- [x] Page box model and `@page` rule
- [x] Allowed page breaks (between and inside block boxes)
- [x] Forced page breaks

## Chapter 14 — Colors and Backgrounds

- [ ] `color` — foreground (text) color
- [ ] `background-color` — element background color
- [ ] `background-image` — background image (`url()` or `none`)
- [ ] `background-repeat: repeat | repeat-x | repeat-y | no-repeat`
- [ ] `background-attachment: scroll | fixed`
- [ ] `background-position` — horizontal and vertical position
- [ ] `background` — shorthand for all background properties
- [ ] Background painting area (extends to border edge)
- [ ] Background of `<body>` propagates to canvas

## Chapter 15 — Fonts

- [ ] `font-family` — prioritized list of font family names and generic families
- [ ] Generic font families: `serif`, `sans-serif`, `monospace`, `cursive`, `fantasy`
- [ ] `font-style: normal | italic | oblique`
- [ ] `font-variant: normal | small-caps`
- [ ] `font-weight: normal | bold | bolder | lighter | 100–900`
- [ ] `font-size: <absolute-size> | <relative-size> | <length> | <percentage>`
- [ ] Absolute size keywords: `xx-small` through `xx-large`
- [ ] Relative size keywords: `larger`, `smaller`
- [ ] `font` — shorthand for all font properties
- [ ] System fonts: `caption`, `icon`, `menu`, `message-box`, `small-caption`, `status-bar`
- [ ] Font matching algorithm

## Chapter 16 — Text

- [ ] `text-indent` — first-line indentation
- [ ] `text-align: left | right | center | justify`
- [ ] `text-decoration: none | underline | overline | line-through | blink`
- [ ] `letter-spacing: normal | <length>` — inter-character spacing
- [ ] `word-spacing: normal | <length>` — inter-word spacing
- [ ] `text-transform: capitalize | uppercase | lowercase | none`
- [ ] `white-space: normal` — collapse whitespace, wrap lines
- [ ] `white-space: pre` — preserve whitespace, no wrapping
- [ ] `white-space: nowrap` — collapse whitespace, no wrapping
- [ ] `white-space: pre-wrap` — preserve whitespace, wrap lines
- [ ] `white-space: pre-line` — collapse whitespace but preserve newlines, wrap lines

## Chapter 17 — Tables

### 17.1–17.4 Table Model

- [x] Table box generation (`display: table`, `inline-table`)
  <!-- Verified: Css2Chapter17Tests – S17_2_DisplayTable_BlockLevel, S17_2_DisplayInlineTable -->
- [x] Table row/column/cell/caption box generation
  <!-- Verified: Css2Chapter17Tests – S17_2_DisplayTableRow through S17_2_DisplayTableCaption -->
- [x] Anonymous table objects (missing wrappers)
  <!-- Verified: Css2Chapter17Tests – S17_2_1_* tests for anonymous table/row/column wrappers -->
- [x] Table layers: cells → rows → row groups → columns → column groups → table
  <!-- Verified: Css2Chapter17Tests – S17_5_1_* layer tests (1-6) -->

### 17.5–17.6 Table Layout

- [x] `table-layout: auto` — automatic layout algorithm
  <!-- Verified: Css2Chapter17Tests – S17_5_2_2_AutoLayout_* tests -->
- [x] `table-layout: fixed` — fixed layout algorithm
  <!-- Verified: Css2Chapter17Tests – S17_5_2_1_FixedLayout_* tests -->
- [x] `border-collapse: separate` — separate border model
  <!-- Verified: Css2Chapter17Tests – S17_6_1_SeparateBorders, S17_6_1_Golden_SeparateBorders -->
- [x] `border-collapse: collapse` — collapsing border model
  <!-- Verified: Css2Chapter17Tests – S17_6_2_CollapsingBorders, S17_6_2_Golden_CollapsingBorders -->
- [x] `border-spacing` — spacing between cell borders (separate model)
  <!-- Verified: Css2Chapter17Tests – S17_6_1_BorderSpacing_OneValue, S17_6_1_BorderSpacing_TwoValues -->
- [x] `empty-cells: show | hide` — rendering of empty cells (separate model)
  <!-- Verified: Css2Chapter17Tests – S17_6_1_1_EmptyCellsShow, S17_6_1_1_EmptyCellsHide -->
- [x] `caption-side: top | bottom` — caption position
  <!-- Verified: Css2Chapter17Tests – S17_4_1_CaptionSideTop, S17_4_1_CaptionSideBottom -->
- [x] Border conflict resolution in collapsing model
  <!-- Verified: Css2Chapter17Tests – S17_6_2_1_* border conflict tests -->
- [x] Column width computation
  <!-- Verified: Css2Chapter17Tests – S17_3_ColumnWidthSetsMinimum, S17_5_2_* width algorithm tests -->
- [x] Horizontal and vertical alignment in cells
  <!-- Verified: Css2Chapter17Tests – S17_5_4_TextAlignInCells, S17_5_4_ColumnAlignmentInheritance -->

## Chapter 18 — User Interface

- [ ] `cursor: auto | crosshair | default | pointer | move | e-resize | ne-resize | nw-resize | n-resize | se-resize | sw-resize | s-resize | w-resize | text | wait | help | progress | url()`
- [ ] `outline-color` — outline color
- [ ] `outline-style` — outline style (same values as `border-style`, plus `invert`)
- [ ] `outline-width` — outline width
- [ ] `outline` — shorthand for outline properties
- [ ] Outlines do not affect layout (drawn over the box)
- [ ] System colors (deprecated but part of CSS 2.1)

## Appendix A — Aural Style Sheets

- [ ] `volume` — speech volume
- [ ] `speak: normal | none | spell-out`
- [ ] `pause-before`, `pause-after`, `pause` — pauses around speech
- [ ] `cue-before`, `cue-after`, `cue` — auditory cues
- [ ] `play-during` — background sound during speech
- [ ] `azimuth` — horizontal sound position
- [ ] `elevation` — vertical sound position
- [ ] `speech-rate` — rate of speech
- [ ] `voice-family` — voice selection
- [ ] `pitch` — voice pitch
- [ ] `pitch-range` — pitch variation
- [ ] `stress` — stress pattern
- [ ] `richness` — voice richness
- [ ] `speak-punctuation: code | none`
- [ ] `speak-numeral: digits | continuous`
- [ ] `speak-header: once | always`

## Appendix D — Default Style Sheet for HTML 4

- [ ] UA default styles for HTML elements
- [ ] Default `display` values for all HTML elements
- [ ] Default margins, padding, and font styles

## Appendix E — Stacking Contexts

- [ ] Stacking context creation rules
- [ ] Full painting order within a stacking context (7 steps)
- [ ] Positioned descendants and stacking order

---

## How to Use This Checklist

1. **Mark items** `[x]` as they are verified to work in the html-renderer.
2. **Add notes** after any item that is partially supported or has known
   limitations.
3. **Reference** the corresponding spec HTML file for full details on any item
   (e.g., `box.html` for Chapter 8, `visuren.html` for Chapter 9).
4. **Track progress** by counting checked vs unchecked items.

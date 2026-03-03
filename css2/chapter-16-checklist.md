# Chapter 16 — Text

Detailed checklist for CSS 2.1 Chapter 16. This chapter defines properties for
text formatting, including indentation, alignment, decoration, spacing,
transformation, and white space handling.

> **Spec file:** [`text.html`](text.html)

---

## 16.1 Indentation: the 'text-indent' Property

- [x] `text-indent: <length>` — fixed indentation of first line
- [x] `text-indent: <percentage>` — percentage of containing block width
- [x] Initial value: `0`
- [x] Applies to block containers
- [x] Inherited: yes
- [x] Indentation applies to the first line of the block (or first line after a forced line break in some contexts)
- [x] Negative values allowed (hanging indent)

## 16.2 Alignment: the 'text-align' Property

- [x] `text-align: left` — left-aligned (default for LTR)
- [x] `text-align: right` — right-aligned (default for RTL)
- [x] `text-align: center` — centered
- [x] `text-align: justify` — justified (spread to fill line box)
- [x] Applies to block containers
- [x] Inherited: yes
- [x] Justification adjusts spacing between words and/or characters
- [x] Last line of a justified block is not justified (unless it's the only line)
- [x] UA may justify by adjusting word spacing, letter spacing, or both

## 16.3 Decoration

### 16.3.1 Underlining, Overlining, Striking, and Blinking: 'text-decoration'

- [x] `text-decoration: none` — no decoration (default)
- [x] `text-decoration: underline` — underline below text
- [x] `text-decoration: overline` — line above text
- [x] `text-decoration: line-through` — strikethrough
- [x] `text-decoration: blink` — blinking text (UAs may ignore)
- [x] Multiple values: `text-decoration: underline overline`
- [x] Not inherited, but decorations are drawn across descendant text
- [x] Color of decoration is the `color` of the decorating element
- [x] Decorations propagate to anonymous inline boxes
- [x] Inline elements: decoration is drawn across the entire element
- [x] Block elements: decoration applied to first formatted line
- [x] Floating and absolutely positioned descendants are not decorated by parent
- [x] `text-decoration` on `:first-line` and `:first-letter` pseudo-elements

## 16.4 Letter and Word Spacing

- [x] `letter-spacing: normal` — no additional spacing (default)
- [x] `letter-spacing: <length>` — additional spacing between characters
- [x] Negative `letter-spacing` values allowed
- [x] `word-spacing: normal` — no additional spacing (default)
- [x] `word-spacing: <length>` — additional spacing between words
- [x] Negative `word-spacing` values allowed
- [x] Both properties inherited: yes
- [x] Spacing added in addition to default spacing
- [x] For justified text, UAs may vary word/letter spacing
- [x] Word boundaries are UA-dependent (typically whitespace-delimited)

## 16.5 Capitalization: the 'text-transform' Property

- [x] `text-transform: capitalize` — first letter of each word to uppercase
- [x] `text-transform: uppercase` — all characters to uppercase
- [x] `text-transform: lowercase` — all characters to lowercase
- [x] `text-transform: none` — no transformation (default)
- [x] Inherited: yes
- [x] Applies to all elements
- [x] What constitutes a "word" is UA/language-dependent
- [x] Transformation is applied to the text content, not to the DOM

## 16.6 White Space: the 'white-space' Property

- [x] `white-space: normal` — collapse whitespace, wrap lines (default)
- [x] `white-space: pre` — preserve whitespace and newlines, no wrapping
- [x] `white-space: nowrap` — collapse whitespace, no wrapping (force single line)
- [x] `white-space: pre-wrap` — preserve whitespace, wrap at end of line
- [x] `white-space: pre-line` — collapse whitespace but preserve newlines, wrap lines
- [x] Inherited: yes

### 16.6.1 The 'white-space' Processing Model

- [x] Step 1: Collapse whitespace — tabs and spaces to single space (for `normal`, `nowrap`, `pre-line`)
- [x] Step 2: Remove segment breaks per collapsing rules (for `normal`, `nowrap`, `pre-line`)
- [x] Step 3: Preserve newlines as forced line breaks (for `pre`, `pre-wrap`, `pre-line`)
- [x] Step 4: Collapse spaces at beginning/end of line (for `normal`, `nowrap`, `pre-line`)
- [x] Step 5: Tab characters treated as space in collapsed contexts
- [x] Line breaking opportunities: at spaces, after hyphens, UA-determined
- [x] `white-space: pre` and `pre-wrap`: lines broken at preserved newlines and at ends of line boxes
- [x] `white-space: nowrap`: no line breaking except at forced breaks
- [x] Trailing white space at end of lines is removed (for `normal`, `nowrap`, `pre-line`)

### 16.6.2 Example of Bidirectionality with White Space Collapsing

- [x] (Informative example — no separate implementation requirements)

### 16.6.3 Control and Combining Characters' Details

- [x] Zero-width characters (e.g., zero-width space, zero-width joiner) do not affect layout
- [x] Combining characters rendered as part of their base character

---

[← Back to main checklist](css2-specification-checklist.md)

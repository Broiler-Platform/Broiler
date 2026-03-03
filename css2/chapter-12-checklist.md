# Chapter 12 — Generated Content, Automatic Numbering, and Lists

Detailed checklist for CSS 2.1 Chapter 12. This chapter covers content
generation via `:before` and `:after` pseudo-elements, CSS counters, and list
marker styling.

> **Spec file:** [`generate.html`](generate.html)

---

## 12.1 The :before and :after Pseudo-elements

- [x] `:before` creates a pseudo-element as the first child of the element
- [x] `:after` creates a pseudo-element as the last child of the element
- [x] Generated content is rendered content but not in the document tree
- [x] Generated content inherits properties from the element
- [x] Generated content of block-level elements participates in the block formatting context
- [x] `:before` and `:after` on replaced elements is UA-dependent (CSS 2.1 does not define)
- [x] `display` property on pseudo-elements determines box type

## 12.2 The 'content' Property

- [x] `content: normal` — no generated content (default for elements; equivalent to `none` for pseudo-elements)
- [x] `content: none` — pseudo-element is not generated
- [x] `content: <string>` — text string
- [x] `content: <uri>` — replaced content (image or other external resource)
- [x] `content: counter(name)` — value of named counter
- [x] `content: counter(name, style)` — value of named counter in given list-style-type
- [x] `content: counters(name, string)` — nested counter values separated by string
- [x] `content: counters(name, string, style)` — nested counter values with style
- [x] `content: attr(X)` — value of attribute X on the element
- [x] `content: open-quote` — appropriate opening quotation mark
- [x] `content: close-quote` — appropriate closing quotation mark
- [x] `content: no-open-quote` — no content but increments quote nesting level
- [x] `content: no-close-quote` — no content but decrements quote nesting level
- [x] Multiple values concatenated: `content: "(" counter(n) ")"`
- [x] If `:before`/`:after` content is empty string, pseudo-element generates empty inline box

## 12.3 Quotation Marks

- [x] `quotes: none` — no quotation marks generated
- [x] `quotes: <string> <string> [<string> <string>]*` — pairs of open/close quotes
- [x] Nested quote levels use successive pairs
- [x] If nesting deeper than available pairs, last pair is repeated
- [x] `open-quote` and `close-quote` insert from `quotes` property
- [x] Quote depth tracking (increment on `open-quote`, decrement on `close-quote`)
- [x] Negative quote depth clamped to 0

## 12.4 Automatic Counters and Numbering

- [x] `counter-reset: <identifier> [<integer>]? [<identifier> [<integer>]?]*` — reset counters
- [x] `counter-reset: none` — no counter reset (default)
- [x] `counter-increment: <identifier> [<integer>]? [<identifier> [<integer>]?]*` — increment counters
- [x] `counter-increment: none` — no counter increment (default)
- [x] Default reset value is 0
- [x] Default increment value is 1
- [x] Negative increment values allowed
- [x] Multiple counters in a single declaration
- [x] Counter used before being reset: implicitly created with value 0

### 12.4.1 Nested Counters and Scope

- [x] Counter scope: from `counter-reset` to end of the element's subtree
- [x] Nested elements create new counter instances (self-nesting)
- [x] `counters()` function concatenates all counters of the same name with a separator

### 12.4.2 Counter Styles

- [x] Counter styles are the same as `list-style-type` values
- [x] `decimal`, `decimal-leading-zero`, `lower-roman`, `upper-roman`, `lower-alpha`, `upper-alpha`, `lower-latin`, `upper-latin`, `lower-greek`, `disc`, `circle`, `square`, `none`

### 12.4.3 Counters in Elements with 'display: none'

- [x] Elements with `display: none` do not increment or reset counters
- [x] Elements with `visibility: hidden` do increment and reset counters

## 12.5 Lists

- [x] `list-style-type` — marker type
  - [x] `disc` (default for `<ul>`)
  - [x] `circle`
  - [x] `square`
  - [x] `decimal` (default for `<ol>`)
  - [x] `decimal-leading-zero`
  - [x] `lower-roman`
  - [x] `upper-roman`
  - [x] `lower-alpha` / `lower-latin`
  - [x] `upper-alpha` / `upper-latin`
  - [x] `lower-greek`
  - [x] `none`
- [x] `list-style-image: <uri> | none` — image as list marker
- [x] `list-style-image` takes precedence over `list-style-type` (if image available)
- [x] `list-style-position: outside` — marker outside the content flow (default)
- [x] `list-style-position: inside` — marker as first inline box of the content
- [x] `list-style` shorthand — combines type, position, and image
- [x] List markers on `display: list-item` elements
- [x] Marker box positioning outside the principal box

---

[← Back to main checklist](css2-specification-checklist.md)

# Chapter 14 — Colors and Backgrounds

Detailed checklist for CSS 2.1 Chapter 14. This chapter defines foreground
color and background properties.

> **Spec file:** [`colors.html`](colors.html)

---

## 14.1 Foreground Color: the 'color' Property

- [x] `color: <color>` — sets the foreground (text) color
- [x] `color: inherit` — inherits from parent
- [x] Applies to all elements
- [x] Inherited: yes
- [x] Initial value: UA-dependent
- [x] Foreground color is used for text content
- [x] Foreground color is the default for `border-color` and `text-decoration` color
- [x] Color values: named colors, `#rgb`, `#rrggbb`, `rgb()`, `inherit`

## 14.2 The Background

- [x] Background is painted behind the content, padding, and border areas
- [x] Background of root element covers the entire canvas
- [x] Background of `<body>` element propagates to canvas (if root element background is transparent)
- [x] Background is not inherited (but appears to be due to initial `transparent` value)

### 14.2.1 Background Properties

- [x] `background-color: <color> | transparent` — background color
  - [x] Initial value: `transparent`
  - [x] Not inherited
  - [x] Painted behind background image
- [x] `background-image: <uri> | none` — background image
  - [x] Initial value: `none`
  - [x] Not inherited
  - [x] Image rendered on top of background color
  - [x] If image cannot be loaded, UA must treat as `none`
- [x] `background-repeat: repeat | repeat-x | repeat-y | no-repeat`
  - [x] `repeat` — tiled in both directions (default)
  - [x] `repeat-x` — tiled horizontally only
  - [x] `repeat-y` — tiled vertically only
  - [x] `no-repeat` — image not repeated
  - [x] Tiling covers the padding and content areas
- [x] `background-attachment: scroll | fixed`
  - [x] `scroll` — background scrolls with the element (default)
  - [x] `fixed` — background fixed relative to the viewport
  - [x] When `fixed`, background is positioned relative to the viewport but only visible in the element's padding/content area
- [x] `background-position` — position of background image
  - [x] Keyword values: `top`, `right`, `bottom`, `left`, `center`
  - [x] Length values: horizontal and vertical offsets from top-left corner
  - [x] Percentage values: position is (container size - image size) × percentage
  - [x] Default: `0% 0%` (top-left)
  - [x] One value specified: second defaults to `center` (50%)
  - [x] Two values: horizontal then vertical
  - [x] Keyword pairs may be in any order (except mixing keyword and length/percentage)
  - [x] Not inherited
- [x] `background` shorthand — combines all background properties
  - [x] Order: `color` `image` `repeat` `attachment` `position`
  - [x] Omitted values reset to their initial values
  - [x] Single declaration sets all background sub-properties

---

[← Back to main checklist](css2-specification-checklist.md)
